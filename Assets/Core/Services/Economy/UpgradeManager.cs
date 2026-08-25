using Core.Economy.Data;
using Core.Models;
using Core.Models.Economy;
using R3;
using System;
using System.Collections.Generic;

namespace Core.Services.Economy
{
    public class UpgradeManager : IDisposable
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

        public ReactiveProperty<double> TotalTFlopsYieldPerSecond { get; } = new(0d);
        public ReactiveProperty<float> TotalTracePerSecond { get; } = new(0f);

        public UpgradeManager(UpgradeCatalogSO catalog, UserCurrencies userCurrencies)
        {
            _catalog = catalog;
            _userCurrencies = userCurrencies;

            _activeUpgrades = new Dictionary<string, UpgradeModel>();

            _upgradesByType = new Dictionary<UpgradeType, List<UpgradeModel>>
            {
                { UpgradeType.Script, new List<UpgradeModel>() },
                { UpgradeType.Hardware, new List<UpgradeModel>() },
                { UpgradeType.Proxy, new List<UpgradeModel>() }
            };

            OnUpgradeRevealed = new Subject<UpgradeModel>();
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

            RecalculateTotals();

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

            double currentCost = model.GetCurrentCost(); // TODO (thème Prestige) : passer CostMultiplierReduction.

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

        private void RecalculateTotals()
        {
            double moneyPerSecond = 0d;
            double tflopsCapacity = 0d;
            float totalTrace = 0f;

            foreach (var model in _activeUpgrades.Values)
            {
                totalTrace += model.GetCurrentTracePerSecond();

                switch (model.Config.Type)
                {
                    case UpgradeType.Script:
                        // Désormais un débit théorique (versement ÷ durée de cycle) et non plus
                        // un versement par seconde : c'est ce que le Header doit afficher.
                        moneyPerSecond += model.GetYieldPerSecond();
                        break;

                    case UpgradeType.Hardware:
                        tflopsCapacity += model.GetCurrentYield();
                        break;

                    case UpgradeType.Proxy:
                        // Le Proxy ne produit rien, il n'agit que sur la trace.
                        break;
                }
            }

            TotalMoneyYieldPerSecond.Value = moneyPerSecond;
            TotalTFlopsYieldPerSecond.Value = tflopsCapacity;

            // La génération globale de trace ne peut pas devenir négative : les Proxies
            // ralentissent l'enquête, ils ne l'effacent pas.
            TotalTracePerSecond.Value = UnityEngine.Mathf.Max(0f, totalTrace);
        }

        public void Dispose()
        {
            foreach (var model in _activeUpgrades.Values)
            {
                model.Dispose();
            }
            _activeUpgrades.Clear();

            _onUpgradesRebuilt.Dispose();
            OnUpgradeRevealed.Dispose();
            TotalMoneyYieldPerSecond.Dispose();
            TotalTFlopsYieldPerSecond.Dispose();
            TotalTracePerSecond.Dispose();
        }
    }
}
