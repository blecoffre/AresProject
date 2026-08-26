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

        private readonly UpgradeCatalogSO _catalog;
        private readonly UserCurrencies _userCurrencies;
        private readonly PrestigeManager _prestigeManager;

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
        /// Dissipation cumulée des Proxies, TFlops déjà appliquées :
        /// Σ (base × niveau) × (1 + log10(1 + TFlops)).
        /// Valeur POSITIVE, à soustraire du débit brut — c'est le second rôle des TFlops.
        /// </summary>
        public ReactiveProperty<float> ProxyDissipationPerSecond { get; } = new(0f);

        public UpgradeManager(
            UpgradeCatalogSO catalog,
            UserCurrencies userCurrencies,
            PrestigeManager prestigeManager)
        {
            _catalog = catalog;
            _userCurrencies = userCurrencies;
            _prestigeManager = prestigeManager;

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
            foreach (var kvp in _activeUpgrades)
            {
                kvp.Value.SetSpecificBonuses(_prestigeManager.GetSpecificBonuses(kvp.Key));
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

                var model = new UpgradeModel(config, level);
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

        public bool TryPurchaseUpgrade(string upgradeId)
        {
            if (!_activeUpgrades.TryGetValue(upgradeId, out var model)) return false;

            double currentCost = model.GetCurrentCost(_prestigeManager.CostMultiplierReduction.CurrentValue);

            if (_userCurrencies.Money.TryRemove(currentCost))
            {
                bool isFirstPurchase = model.CurrentLevel.CurrentValue == 0;

                model.LevelUp();
                RecalculateTotals();

                if (isFirstPurchase)
                {
                    RevealNextUpgrade(model);
                }

                return true;
            }

            return false;
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

        public IReadOnlyDictionary<string, UpgradeModel> GetAllActiveUpgrades() => _activeUpgrades;

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

            // Poussée dans les modèles : c'est ce qui invalide leur cache de durée. Seuls les
            // Scripts ont un cycle, mais on pousse à tous — SetTFlops s'auto-garde sur l'égalité,
            // et un Hardware n'a pas de durée à recalculer de toute façon.
            var scriptList = _upgradesByType[UpgradeType.Script];
            for (int i = 0; i < scriptList.Count; i++)
            {
                scriptList[i].SetTFlops(totalTFlops);
            }

            // Passe 2 — les agrégats qui dépendent des durées fraîchement recalculées.
            //
            // Le SIGNE dépend du type, et c'est tout l'enjeu : additionner aveuglément
            // GetTraceMagnitudePerSecond() sur les trois types faisait qu'acheter un Proxy
            // AUGMENTAIT la Trace au lieu de la dissiper.
            double moneyPerSecond = 0d;
            float hardwareTrace = 0f;
            float proxyBase = 0f;

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
                        break;

                    case UpgradeType.Proxy:
                        proxyBase += model.GetTraceMagnitudePerSecond();
                        break;
                }
            }

            // Second rôle des TFlops. Le log10 donne un gros gain au début puis aplatit la
            // courbe : le joueur ne doit jamais devenir indétectable.
            double dissipationFactor = 1d + Math.Log10(1d + totalTFlops);

            TotalMoneyYieldPerSecond.Value = moneyPerSecond;
            TotalTFlops.Value = totalTFlops;
            HardwareTracePerSecond.Value = hardwareTrace;
            ProxyDissipationPerSecond.Value = (float)(proxyBase * dissipationFactor);
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
            ProxyDissipationPerSecond.Dispose();
        }
    }
}
