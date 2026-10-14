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

        private readonly ReactiveProperty<double> _grossTFlops = new(0d);
        private readonly ReactiveProperty<double> _netTFlops = new(0d);
        private readonly Subject<float> _onProxyWipe = new();

        /// <summary>
        /// TFlops BRUTES : la seule somme du parc Hardware possédé, sans aucun bonus de prestige
        /// ni sacrifice. C'est la puissance de la MENACE — la vitesse de l'A.M.I. ne lit qu'elle.
        ///
        /// Séparée des TFlops nettes le 2026-10-13 : indexer la menace sur P_DATAM et le Noyau
        /// faisait de chaque nœud d'économie un accélérateur de mort. Le prestige accélère les
        /// revenus, jamais la traque.
        /// </summary>
        public ReadOnlyReactiveProperty<double> GrossTFlops => _grossTFlops;

        /// <summary>
        /// TFlops NETTES : les brutes multipliées par la méta-progression (`P_DATAM`, puis le
        /// rendement du Noyau Compilé), moins la tranche immobilisée par le Data Wiper. C'est la puissance de l'ÉCONOMIE —
        /// compression et rendement des Scripts, capteur du brouillard, victoire, affichage.
        ///
        /// Ce n'est PAS un débit : la valeur ne bouge qu'à l'achat, elle ne s'accumule pas et ne
        /// se dépense pas.
        /// </summary>
        public ReadOnlyReactiveProperty<double> NetTFlops => _netTFlops;

        /// <summary>
        /// Émis à chaque achat de Proxy : les SECONDES DE SURVIE que le lot rend, base, paliers de
        /// chaque niveau et quantité compris. Le ThreatManager les convertit en UT à la vitesse
        /// courante de l'A.M.I. — il est le seul à modifier la jauge, et ce gestionnaire ne la
        /// connaît pas.
        /// </summary>
        public Observable<float> OnProxyWipe => _onProxyWipe;

        /// <summary>
        /// Part des TFlops NETTES sacrifiée par le Data Wiper, de 0 à 0,9, jusqu'à la fin de la run.
        /// Le ver envoyé dans les serveurs fédéraux engloutit la puissance qu'il mobilise.
        /// </summary>
        private float _tflopsSacrificedFraction;

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
            // Un seul abonnement pour TOUS les bonus de prestige. S'abonner à chaque propriété
            // séparément serait fragile, et OnPrestigePurchased ne suffirait pas — il n'est pas
            // émis par InitializeFromSave, donc le chargement d'une partie n'appliquerait aucun
            // bonus.
            // Abonnement dans Start() et non dans le constructeur : un constructeur appelé par
            // le conteneur ne doit pas avoir d'effets de bord.
            _prestigeManager.OnBonusesRecalculated
                .Subscribe(_ => ApplyPrestigeBonuses())
                .AddTo(ref _disposables);
        }

        /// <summary>
        /// Pousse le Noyau Compilé et `P_REFAC` dans les modèles, puis recalcule les agrégats.
        /// Les deux dans cet ordre : les totaux dépendent des rendements que le Noyau vient de
        /// modifier.
        /// </summary>
        private void ApplyPrestigeBonuses()
        {
            // Lues une fois hors de la boucle : les valeurs sont les mêmes pour les soixante
            // modèles. Depuis le retrait des nœuds ciblés, tout le prestige est GLOBAL.
            float globalCostReduction = _prestigeManager.CostMultiplierReduction.CurrentValue;
            double coreCost = _prestigeManager.TotalCostMultiplier.CurrentValue;
            double coreYield = _prestigeManager.TotalYieldBonus.CurrentValue;

            foreach (var kvp in _activeUpgrades)
            {
                // Poussée dans le modèle depuis le 2026-08-30, au lieu d'être passée en argument
                // au seul calcul d'achat. Tant qu'elle restait un argument, la vue l'oubliait et
                // affichait un prix plus élevé que celui réellement débité.
                kvp.Value.SetGlobalCostMultiplierReduction(globalCostReduction);
                kvp.Value.SetCoreMultipliers(coreCost, coreYield);
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
            // avant tout calcul, sinon un rechargement de sauvegarde perdrait le Noyau Compilé et
            // P_REFAC jusqu'au prochain achat de nœud.
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

            // Chiffré AVANT la montée de niveau : ce sont les paliers des niveaux ACHETÉS qui
            // comptent, pas ceux du niveau d'arrivée appliqués à tout le lot.
            float wipeSeconds = model.Config.Type == UpgradeType.Proxy
                ? (float)model.GetWipeSecondsForNextLevels(levels)
                : 0f;

            model.AddLevels(levels);
            RecalculateTotals();

            if (wipeSeconds > 0f)
            {
                _onProxyWipe.OnNext(wipeSeconds);
            }

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
        /// Sacrifie une fraction des TFlops NETTES. Réservé au Data Wiper, qui la porte à 0,3, 0,6
        /// puis 0,9 au fil de ses usages, et la rend à zéro au wipe de fin de run.
        ///
        /// Les TFlops brutes n'y sont pas soumises : la vitesse de l'A.M.I. ne ralentit pas quand
        /// le joueur sacrifie sa puissance, sinon le Data Wiper deviendrait un soin gratuit.
        /// </summary>
        public void SetTFlopsSacrificedFraction(float fraction)
        {
            float safe = UnityEngine.Mathf.Clamp01(fraction);
            if (safe == _tflopsSacrificedFraction) return;

            _tflopsSacrificedFraction = safe;
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
            double grossTFlops = 0d;

            var hardwareList = _upgradesByType[UpgradeType.Hardware];
            for (int i = 0; i < hardwareList.Count; i++)
            {
                grossTFlops += hardwareList[i].GetCurrentYield();
            }

            // P_DATAM puis le Noyau Compilé, composés : TFlops nettes = brutes × P_DATAM × Noyau
            // (GD, 2026-10-13). Les brutes restent intactes — ce sont elles que lit l'A.M.I.
            double netTFlops = grossTFlops
                             * _prestigeManager.GlobalComputeMultiplier.CurrentValue
                             * _prestigeManager.TotalYieldBonus.CurrentValue;

            // Sacrifice du Data Wiper, appliqué en DERNIER : c'est une amputation de la capacité
            // finale, pas un facteur à composer avec le multiplicateur de prestige.
            netTFlops *= 1d - _tflopsSacrificedFraction;

            // Poussée dans les modèles : c'est ce qui invalide leur cache de durée. Seuls les
            // Scripts ont un cycle ; SetTFlops s'auto-garde sur l'égalité.
            var scriptList = _upgradesByType[UpgradeType.Script];
            for (int i = 0; i < scriptList.Count; i++)
            {
                scriptList[i].SetTFlops(netTFlops);
            }

            // Passe 2 — le débit théorique, qui dépend des durées fraîchement recalculées.
            double moneyPerSecond = 0d;

            for (int i = 0; i < scriptList.Count; i++)
            {
                // Débit théorique (versement ÷ durée de cycle), pas un versement par seconde.
                moneyPerSecond += scriptList[i].GetYieldPerSecond();
            }

            TotalMoneyYieldPerSecond.Value = moneyPerSecond;
            _grossTFlops.Value = grossTFlops;
            _netTFlops.Value = netTFlops;
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
            _onProxyWipe.Dispose();
            OnUpgradeRevealed.Dispose();
            TotalMoneyYieldPerSecond.Dispose();
            _grossTFlops.Dispose();
            _netTFlops.Dispose();
        }
    }
}