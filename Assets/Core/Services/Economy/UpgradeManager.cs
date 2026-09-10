using Core.Economy.Data;
using Core.Models;
using Core.Models.Economy;
using R3;
using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace Core.Services.Economy
{
    public class UpgradeManager : IStartable, IDisposable
    {
        // Tableau statique plutôt qu'un ToList() sur les clés du dictionnaire : pas de LINQ en
        // code runtime, et aucune allocation à la réinitialisation.
        private static readonly UpgradeType[] AllTypes =
        {
            UpgradeType.Script,
            UpgradeType.Hardware,
            UpgradeType.Proxy
        };

        /// <summary>
        /// Garde-fou du mode MAX. Il ne borne PAS le jeu : à un multiplicateur de 1,07, un solde
        /// mille milliards de fois supérieur au prix du prochain cran ne paie qu'environ quatre
        /// cents niveaux — la courbe exponentielle plafonne bien avant cette valeur. Il n'existe
        /// que pour qu'un générateur au coût raboté à zéro, ou un multiplicateur collé à son
        /// plancher de 1,01, ne puisse pas rendre un lot déraisonnable.
        /// </summary>
        public const int MaxBulkLevels = 100_000;

        private readonly UpgradeCatalogSO _catalog;
        private readonly UserCurrencies _userCurrencies;
        private readonly PrestigeManager _prestigeManager;
        private readonly BalancingConfigSO _balancing;

        private DisposableBag _disposables;

        private readonly Dictionary<string, UpgradeModel> _activeUpgrades;
        private readonly Dictionary<UpgradeType, List<UpgradeModel>> _upgradesByType;

        private readonly Subject<Unit> _onUpgradesRebuilt = new();

        /// <summary>
        /// Émis à chaque reconstruction complète des modèles (chargement de sauvegarde, wipe).
        /// Tout système qui garde une référence vers un UpgradeModel doit s'y abonner, sinon il
        /// continuerait à faire tourner des modèles disposés.
        /// </summary>
        public Observable<Unit> OnUpgradesRebuilt => _onUpgradesRebuilt;

        public Subject<UpgradeModel> OnUpgradeRevealed { get; }

        /// <summary>Rendement théorique cumulé des Scripts, si tous leurs cycles tournaient en continu.</summary>
        public ReactiveProperty<double> TotalMoneyYieldPerSecond { get; } = new(0d);

        /// <summary>
        /// Capacité de calcul totale du joueur : somme du parc Hardware possédé, plus le bonus
        /// persistant de prestige. Ce n'est PAS un débit — la valeur ne bouge qu'à l'achat, elle
        /// ne s'accumule pas et ne se dépense pas. D'où l'abandon du suffixe « PerSecond ».
        /// </summary>
        public ReactiveProperty<double> TotalTFlops { get; } = new(0d);

        /// <summary>
        /// Trace générée en continu par le parc Hardware possédé. Un Hardware n'a pas de cycle :
        /// il chauffe dès l'achat, contrairement aux Scripts dont la Trace ne court que pendant
        /// un cycle actif. Cette part-là est donc statique et ne bouge qu'à l'achat.
        /// </summary>
        public ReactiveProperty<float> HardwareTracePerSecond { get; } = new(0f);

        /// <summary>
        /// Plafond de Trace apporté par le parc Hardware, hors plafond de base. Le Hardware est
        /// le rempart : il achète de la marge avant saisie, là où les Proxies achètent du débit.
        /// </summary>
        public ReactiveProperty<float> TraceCapacityBonus { get; } = new(0f);

        /// <summary>
        /// Puissance de dissipation cumulée des Proxies, TFlops déjà appliquées :
        /// Σ (base × niveau × paliers) × (1 + log10(1 + TFlops)).
        ///
        /// <b>Ce n'est plus un débit à soustraire depuis le 2026-08-31</b>, et le renommage n'est
        /// pas cosmétique. Tant qu'on la soustrayait platement, on opposait une valeur NON BORNÉE
        /// à une génération bornée : 2,7 M$ de Proxies annulaient toute la Trace du jeu, pour
        /// toujours. C'est désormais une grandeur qui n'a de sens que RAPPORTÉE au brut, et c'est
        /// le SimulationTicker qui la convertit en fraction de réduction.
        /// </summary>
        public ReactiveProperty<float> ProxyDissipationPower { get; } = new(0f);

        /// <summary>
        /// Accélération des cycles apportée par le parc de Proxies : 1 + niveaux cumulés × 1 %.
        ///
        /// Donne une raison d'acheter des Proxies en permanence, et pas seulement quand la Trace
        /// menace : un niveau acheté n'est jamais perdu. Multiplicateur DÉDIÉ et non branché sur
        /// les TFlops — y passer créerait une boucle, la dissipation dépendant elle-même des
        /// TFlops par son log10.
        ///
        /// ⚠️ Linéaire et sans plafond, contrairement au reste du jeu qui est asymptotique.
        /// Depuis le 2026-08-31 le mur MinCycleDuration a disparu : seul le plancher ABSOLU de
        /// BalancingConfig borne encore l'effet, et bien plus loin. À surveiller à l'équilibrage.
        /// </summary>
        public ReactiveProperty<double> ProxySynergyMultiplier { get; } = new(1d);

        /// <summary>
        /// Multiplicateur temporaire appliqué au rendement des Scripts. Vaut 1 hors Overdrive.
        ///
        /// Champ simple et non ReactiveProperty : le seul lecteur est la boucle de poussée
        /// ci-dessous, et TotalMoneyYieldPerSecond porte déjà l'information vers l'UI.
        /// </summary>
        private double _globalYieldMultiplier = 1d;

        /// <summary>
        /// Fraction du parc immobilisée par le Bouton d'Urgence, de 0 à 0,9. Le « Data Wiper »
        /// envoie un ver dans les serveurs fédéraux : la puissance qu'il mobilise n'est plus
        /// disponible pour le joueur pendant une minute.
        /// </summary>
        private float _tflopsBlockedFraction;

        public UpgradeManager(
            UpgradeCatalogSO catalog,
            UserCurrencies userCurrencies,
            PrestigeManager prestigeManager,
            BalancingConfigSO balancing)
        {
            _catalog = catalog;
            _userCurrencies = userCurrencies;
            _prestigeManager = prestigeManager;
            _balancing = balancing;

            _activeUpgrades = new Dictionary<string, UpgradeModel>();

            _upgradesByType = new Dictionary<UpgradeType, List<UpgradeModel>>
            {
                { UpgradeType.Script, new List<UpgradeModel>() },
                { UpgradeType.Hardware, new List<UpgradeModel>() },
                { UpgradeType.Proxy, new List<UpgradeModel>() }
            };

            OnUpgradeRevealed = new Subject<UpgradeModel>();
        }

        public void Start()
        {
            // Un seul abonnement pour TOUS les bonus de prestige : ciblés comme globaux. S'abonner
            // aux sept propriétés séparément serait fragile, et OnPrestigePurchased ne suffirait
            // pas — il n'est pas émis par InitializeFromSave, donc le chargement d'une partie
            // n'appliquerait aucun bonus.
            // Abonnement dans Start() et non dans le constructeur : un constructeur appelé par
            // le conteneur ne doit pas avoir d'effets de bord.
            _prestigeManager.OnBonusesRecalculated
                .Subscribe(_ => ApplyPrestigeBonuses())
                .AddTo(ref _disposables);
        }

        /// <summary>
        /// Redistribue les bonus ciblés dans les modèles, puis recalcule les agrégats.
        /// Les deux dans cet ordre : les totaux dépendent des rendements et des durées que ces
        /// bonus viennent de modifier.
        /// </summary>
        private void ApplyPrestigeBonuses()
        {
            // Lue une fois hors de la boucle : la valeur est la même pour les quarante-cinq
            // modèles, c'est précisément ce qui la distingue des bonus ciblés.
            float globalCostReduction = _prestigeManager.CostMultiplierReduction.CurrentValue;

            foreach (var kvp in _activeUpgrades)
            {
                kvp.Value.SetSpecificBonuses(_prestigeManager.GetSpecificBonuses(kvp.Key));

                // Poussée dans le modèle depuis le 2026-08-30, au lieu d'être passée en argument
                // au seul calcul d'achat. Tant qu'elle restait un argument, la vue l'oubliait et
                // affichait un prix plus élevé que celui réellement débité.
                kvp.Value.SetGlobalCostMultiplierReduction(globalCostReduction);
            }

            RecalculateTotals();
        }

        public void InitializeFromSave(Dictionary<string, int> savedUpgradeLevels)
        {
            // Chaque UpgradeModel porte un ReactiveProperty<int> auquel la vue du générateur est
            // abonnée. Les vider sans Dispose faisait fuir un abonnement par générateur à chaque
            // redémarrage de run — et cette méthode est appelée à chaque wipe.
            foreach (var model in _activeUpgrades.Values)
            {
                model.Dispose();
            }

            _activeUpgrades.Clear();

            for (int i = 0; i < AllTypes.Length; i++)
            {
                _upgradesByType[AllTypes[i]].Clear();
            }

            foreach (var config in _catalog.GetAllUpgrades())
            {
                int level = savedUpgradeLevels != null && savedUpgradeLevels.TryGetValue(config.Id, out int savedLevel)
                    ? savedLevel
                    : 0;

                var model = new UpgradeModel(config, _balancing, level);
                _activeUpgrades.Add(config.Id, model);
                _upgradesByType[config.Type].Add(model);
            }

            for (int i = 0; i < AllTypes.Length; i++)
            {
                _upgradesByType[AllTypes[i]].Sort((a, b) => a.Config.Order.CompareTo(b.Config.Order));
            }

            // Les modèles viennent d'être recréés : ils repartent sans bonus. On les réapplique
            // avant tout calcul, sinon un rechargement de sauvegarde perdrait la méta-progression
            // ciblée jusqu'au prochain achat de nœud.
            ApplyPrestigeBonuses();

            // Notifié en dernier : les abonnés doivent voir un état complet et cohérent.
            _onUpgradesRebuilt.OnNext(Unit.Default);
        }

        /// <summary>Modèles d'une catégorie, triés par Order. Liste vivante : ne pas conserver au-delà d'un rebuild.</summary>
        public IReadOnlyList<UpgradeModel> GetUpgradesOfType(UpgradeType type) => _upgradesByType[type];

        public void CaptureLevelsInto(List<UpgradeSaveEntry> buffer)
        {
            buffer.Clear();

            foreach (var kvp in _activeUpgrades)
            {
                int level = kvp.Value.CurrentLevel.CurrentValue;
                if (level <= 0) continue; // Inutile d'écrire les générateurs jamais achetés.

                buffer.Add(new UpgradeSaveEntry(kvp.Key, level));
            }
        }

        public List<UpgradeModel> GetInitiallyVisibleUpgrades()
        {
            var visibleUpgrades = new List<UpgradeModel>();

            foreach (var typeList in _upgradesByType.Values)
            {
                foreach (var model in typeList)
                {
                    visibleUpgrades.Add(model);

                    // Dès qu'on trouve un objet non acheté DANS CETTE CATÉGORIE, on arrête de
                    // révéler la suite pour CETTE catégorie uniquement.
                    if (model.CurrentLevel.CurrentValue == 0)
                    {
                        break;
                    }
                }
            }

            return visibleUpgrades;
        }

        /// <summary>Nombre de niveaux visés par un mode à quantité fixe. MAX n'en a pas : il dépend du solde.</summary>
        private static int ToLevelCount(BuyQuantity quantity) => quantity switch
        {
            BuyQuantity.X10 => 10,
            BuyQuantity.X100 => 100,
            _ => 1
        };

        /// <summary>
        /// Nombre de niveaux réellement achetables MAINTENANT pour ce mode, avec ce solde.
        /// Zéro veut dire « bouton gris ».
        ///
        /// En x10 et x100 c'est TOUT OU RIEN, et c'est une décision de conception : un lot
        /// partiel ferait du montant affiché un plafond mensonger, et « x10 » se comporterait
        /// comme un MAX déguisé en début de partie. Le prix qu'on lit doit toujours être un prix
        /// qui suffit — c'est la règle déjà posée par CurrencyFormatter.FormatCost, qui arrondit
        /// les prix vers le haut pour la même raison.
        /// </summary>
        public int GetAffordableLevels(string upgradeId, BuyQuantity quantity, double money)
        {
            return _activeUpgrades.TryGetValue(upgradeId, out var model)
                ? GetAffordableLevels(model, quantity, money)
                : 0;
        }

        private static int GetAffordableLevels(UpgradeModel model, BuyQuantity quantity, double money)
        {
            if (quantity == BuyQuantity.Max)
            {
                return model.GetAffordableLevels(money, MaxBulkLevels);
            }

            // Une comparaison contre une somme fermée, sans logarithme : c'est ce chemin-là que
            // les quarante-cinq générateurs affichés parcourent à chaque versement de cycle, et
            // les trois modes fixes sont le cas courant.
            int target = ToLevelCount(quantity);
            return money >= model.GetCumulativeCost(target) ? target : 0;
        }

        /// <summary>
        /// Ce qu'un clic sur « Acheter » coûterait et rapporterait maintenant. Destiné à
        /// l'affichage : l'achat, lui, recalcule tout à partir du solde de l'instant.
        /// </summary>
        public PurchaseQuote GetQuote(string upgradeId, BuyQuantity quantity, double money)
        {
            if (!_activeUpgrades.TryGetValue(upgradeId, out var model))
            {
                return new PurchaseQuote(1, 0d, false);
            }

            int affordable = GetAffordableLevels(model, quantity, money);

            // Rien de payable : on affiche quand même un lot, pour que le bouton gris porte un
            // montant. En MAX ce lot est d'UN niveau — le prochain cran est ce qui manque au
            // joueur ; dans les modes fixes c'est le lot entier, puisque c'est tout ou rien.
            int displayedLevels = affordable > 0
                ? affordable
                : (quantity == BuyQuantity.Max ? 1 : ToLevelCount(quantity));

            return new PurchaseQuote(displayedLevels, model.GetCumulativeCost(displayedLevels), affordable > 0);
        }

        /// <summary>Achat simple. Conservé pour les appelants qui n'ont pas de mode à passer.</summary>
        public bool TryPurchaseUpgrade(string upgradeId) => TryPurchaseUpgrade(upgradeId, BuyQuantity.X1);

        /// <summary>
        /// Achète le lot que <paramref name="quantity"/> désigne, ou rien du tout.
        ///
        /// Le nombre de niveaux est recalculé ICI depuis le solde de l'instant, jamais repris
        /// d'un devis d'affichage : entre le rafraîchissement de la vue et le clic, un cycle a pu
        /// verser — ou le Bouton d'Urgence avoir été pressé.
        /// </summary>
        public bool TryPurchaseUpgrade(string upgradeId, BuyQuantity quantity)
        {
            if (!_activeUpgrades.TryGetValue(upgradeId, out var model)) return false;

            int levels = GetAffordableLevels(model, quantity, _userCurrencies.Money.Amount.CurrentValue);
            if (levels <= 0) return false;

            // Le montant débité sort de la MÊME somme géométrique que le test d'abordabilité
            // ci-dessus. Si TryRemove échoue quand même, c'est que le solde a bougé entre les
            // deux lignes, pas que le calcul ment — et dans ce cas on n'achète rien.
            if (!_userCurrencies.Money.TryRemove(model.GetCumulativeCost(levels))) return false;

            bool isFirstPurchase = model.CurrentLevel.CurrentValue == 0;

            model.AddLevels(levels);
            RecalculateTotals();

            if (isFirstPurchase)
            {
                RevealNextUpgrade(model);
            }

            return true;
        }

        private void RevealNextUpgrade(UpgradeModel justPurchased)
        {
            var typeList = _upgradesByType[justPurchased.Config.Type];
            int index = typeList.IndexOf(justPurchased);

            if (index >= 0 && index + 1 < typeList.Count)
            {
                OnUpgradeRevealed.OnNext(typeList[index + 1]);
            }
        }

        /// <summary>
        /// Applique un multiplicateur temporaire au rendement de TOUS les Scripts. Réservé au
        /// Zéro-Day Exploit, qui l'élève à 50 pendant 30 s puis le ramène à 1.
        ///
        /// Passe par les caches des modèles plutôt que par une multiplication au moment du
        /// versement : c'est ce qui garantit que le chiffre affiché dans le header et le montant
        /// réellement crédité racontent la même chose. Le coût est de 15 recalculs de cache aux
        /// deux extrémités de l'Overdrive — négligeable devant une frame.
        /// </summary>
        /// <summary>
        /// Immobilise une fraction du parc de TFlops. Réservé au Bouton d'Urgence, qui la porte à
        /// 0,3 et plus pendant une minute avant de la rendre à zéro.
        ///
        /// La tranche s'applique à la capacité EFFECTIVE, donc à tout ce qu'elle alimente : la
        /// compression des cycles ET la dissipation des Proxies. C'est exactement le contrecoup
        /// voulu — purger la Trace affaiblit la défense juste après.
        /// </summary>
        public void SetTFlopsBlockedFraction(float fraction)
        {
            float safe = UnityEngine.Mathf.Clamp01(fraction);
            if (safe == _tflopsBlockedFraction) return;

            _tflopsBlockedFraction = safe;
            RecalculateTotals();
        }

        public void SetGlobalYieldMultiplier(double multiplier)
        {
            double safe = multiplier < 1d ? 1d : multiplier;
            if (safe == _globalYieldMultiplier) return;

            _globalYieldMultiplier = safe;
            RecalculateTotals();
        }

        /// <summary>
        /// Deux passes, et l'ordre n'est pas négociable : le débit théorique d'un Script se
        /// calcule à partir de sa durée de cycle, laquelle dépend des TFlops. Tout sommer en une
        /// seule passe utiliserait les durées de l'achat PRÉCÉDENT.
        /// </summary>
        private void RecalculateTotals()
        {
            // Passe 1 — la capacité de calcul, seule grandeur dont rien d'autre ne dépend.
            double hardwareTFlops = 0d;

            var hardwareList = _upgradesByType[UpgradeType.Hardware];
            for (int i = 0; i < hardwareList.Count; i++)
            {
                hardwareTFlops += hardwareList[i].GetCurrentYield();
            }

            // Le multiplicateur global de calcul s'applique à la capacité ENTIÈRE, bonus de
            // départ compris : c'est une amélioration du matériel, pas de son seul parc acheté.
            double totalTFlops = (hardwareTFlops + _prestigeManager.StartingComputerPower.CurrentValue)
                               * _prestigeManager.GlobalComputeMultiplier.CurrentValue;

            // Immobilisation du Bouton d'Urgence, appliquée en DERNIER : c'est une amputation de
            // la capacité finale, pas un facteur à composer avec le multiplicateur de prestige.
            totalTFlops *= 1d - _tflopsBlockedFraction;

            // Synergie : chaque niveau de Proxy possédé accélère TOUS les Scripts. C'est ce qui
            // rend un achat de Proxy jamais perdu, même quand la Trace est basse.
            int totalProxyLevels = 0;
            var proxyList = _upgradesByType[UpgradeType.Proxy];
            for (int i = 0; i < proxyList.Count; i++)
            {
                totalProxyLevels += proxyList[i].CurrentLevel.CurrentValue;
            }

            double synergy = 1d + totalProxyLevels * _balancing.ProxySynergyPerLevel;

            // Poussée dans les modèles : c'est ce qui invalide leur cache de durée. Seuls les
            // Scripts ont un cycle, mais on pousse à tous — SetTFlops s'auto-garde sur l'égalité,
            // et un Hardware n'a pas de durée à recalculer de toute façon.
            var scriptList = _upgradesByType[UpgradeType.Script];
            for (int i = 0; i < scriptList.Count; i++)
            {
                scriptList[i].SetTFlops(totalTFlops);
                scriptList[i].SetProxySynergy(synergy);

                // Poussé aux seuls Scripts, comme le reste de cette boucle. Le modèle refuse de
                // toute façon d'appliquer le multiplicateur à un autre type — ceinture et
                // bretelles, parce qu'un ×50 égaré sur le Hardware ne se verrait pas tout de
                // suite et fausserait toute l'économie de la run.
                scriptList[i].SetGlobalYieldMultiplier(_globalYieldMultiplier);
            }

            // Passe 2 — les agrégats qui dépendent des durées fraîchement recalculées.
            //
            // Le SIGNE dépend du type, et c'est tout l'enjeu : additionner aveuglément
            // GetTraceMagnitudePerSecond() sur les trois types faisait qu'acheter un Proxy
            // AUGMENTAIT la Trace au lieu de la dissiper.
            double moneyPerSecond = 0d;
            float hardwareTrace = 0f;
            float proxyBase = 0f;
            float traceCapacity = 0f;

            foreach (var model in _activeUpgrades.Values)
            {
                switch (model.Config.Type)
                {
                    case UpgradeType.Script:
                        // Débit théorique (versement ÷ durée de cycle), pas un versement par seconde.
                        moneyPerSecond += model.GetYieldPerSecond();

                        // Sa Trace n'est PAS comptée ici : elle ne court que pendant un cycle
                        // actif, et seul le ScriptCycleRunner sait lesquels tournent.
                        break;

                    case UpgradeType.Hardware:
                        hardwareTrace += model.GetTraceMagnitudePerSecond();
                        traceCapacity += model.GetTraceCapIncrease();
                        break;

                    case UpgradeType.Proxy:
                        proxyBase += model.GetTraceMagnitudePerSecond();
                        break;
                }
            }

            // Second rôle des TFlops. Le log10 donne un gros gain au début puis aplatit la
            // courbe : le joueur ne doit jamais devenir indétectable.
            //
            // La COMPRESSION s'y ajoute depuis le 2026-08-31, et ce n'est pas un bonus de plus :
            // c'est ce qui empêche la défense de décrocher mécaniquement. Un Script comprimé
            // ×33 verse sa trace de cycle trente-trois fois plus souvent, donc son Trace/s est
            // multiplié d'autant — alors que la dissipation, exprimée par seconde, ne gagnait
            // que le log10. Mesuré en simulation de fin de partie : 1 500 niveaux de Proxies
            // achetés ne tenaient plus que 24 % de réduction.
            //
            // Un Proxy filtre du TRAFIC, pas des secondes, et il tourne sur le matériel du
            // joueur : si les Scripts vont trente fois plus vite, sa charge suit. La compression
            // devient ainsi NEUTRE sur le rapport dissipation/génération, et tout le danger du
            // Hardware passe par où il doit — sa propre chaleur et le boost de rendement qu'il
            // donne aux Scripts, tous deux comptés en puissance^exposant.
            double compressionFactor = Math.Pow(
                1d + totalTFlops * _balancing.TFlopsTimeCompression,
                _balancing.TFlopsCompressionExponent);

            double dissipationFactor = (1d + Math.Log10(1d + totalTFlops)) * compressionFactor;

            TotalMoneyYieldPerSecond.Value = moneyPerSecond;
            TotalTFlops.Value = totalTFlops;
            ProxySynergyMultiplier.Value = synergy;
            HardwareTracePerSecond.Value = hardwareTrace;
            TraceCapacityBonus.Value = traceCapacity;
            ProxyDissipationPower.Value = (float)(proxyBase * dissipationFactor);
        }

        public void Dispose()
        {
            _disposables.Dispose();

            foreach (var model in _activeUpgrades.Values)
            {
                model.Dispose();
            }
            _activeUpgrades.Clear();

            _onUpgradesRebuilt.Dispose();
            OnUpgradeRevealed.Dispose();
            TotalMoneyYieldPerSecond.Dispose();
            TotalTFlops.Dispose();
            HardwareTracePerSecond.Dispose();
            TraceCapacityBonus.Dispose();
            ProxyDissipationPower.Dispose();
            ProxySynergyMultiplier.Dispose();
        }
    }
}
