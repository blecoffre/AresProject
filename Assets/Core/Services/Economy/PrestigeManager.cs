using Core.Economy.Data;
using Core.Models;
using Core.Models.Economy;
using R3;
using System;
using System.Collections.Generic;

namespace Core.Services.Economy
{
    public class PrestigeManager : IDisposable
    {
        private readonly PrestigeCatalogSO _catalog;
        private readonly UserCurrencies _currencies;

        // Dictionnaire des niveaux achetés (Id -> Niveau actuel)
        private readonly Dictionary<string, ReactiveProperty<int>> _prestigeLevels = new();

        private readonly Subject<string> _onPrestigePurchased = new();

        /// <summary>
        /// Émet l'ID du nœud acheté. Le SaveScheduler s'en sert pour forcer une écriture : un achat
        /// de méta-progression est trop coûteux pour risquer de le perdre au prochain autosave.
        /// </summary>
        public Observable<string> OnPrestigePurchased => _onPrestigePurchased;

        // Propriétés réactives globales que le reste du jeu écoutera
        public ReactiveProperty<float> GlobalComputeMultiplier { get; } = new(1f);
        public ReactiveProperty<float> TraceReductionMultiplier { get; } = new(1f);
        public ReactiveProperty<float> ClickPowerMultiplier { get; } = new(1f);
        public ReactiveProperty<float> CostMultiplierReduction { get; } = new(0f);
        public ReactiveProperty<double> StartingMoney { get; } = new(0d);
        public ReactiveProperty<double> StartingComputerPower { get; } = new(0d);
        public ReactiveProperty<bool> IsEmergencyUnlocked { get; } = new(false);

        public PrestigeManager(PrestigeCatalogSO catalog, UserCurrencies currencies)
        {
            _catalog = catalog;
            _currencies = currencies;
        }

        public void InitializeFromSave(Dictionary<string, int> savedPrestigeLevels)
        {
            // Les ReactiveProperty sortantes portent les abonnements des lignes de l'arbre :
            // les jeter sans Dispose laisserait fuir un abonnement par nœud, à chaque rechargement.
            foreach (var levelProp in _prestigeLevels.Values)
            {
                levelProp.Dispose();
            }
            _prestigeLevels.Clear();

            if (savedPrestigeLevels != null)
            {
                foreach (var kvp in savedPrestigeLevels)
                {
                    _prestigeLevels[kvp.Key] = new ReactiveProperty<int>(kvp.Value);
                }
            }

            RecalculateBonuses();
        }

        /// <summary>
        /// Écrit les niveaux achetés dans le tampon fourni. On remplit une liste existante plutôt
        /// que d'en retourner une neuve : cette méthode est appelée à chaque autosave.
        /// </summary>
        public void CaptureLevelsInto(List<UpgradeSaveEntry> buffer)
        {
            buffer.Clear();

            foreach (var kvp in _prestigeLevels)
            {
                int level = kvp.Value.CurrentValue;
                if (level <= 0) continue; // Un nœud jamais acheté n'a rien à faire dans le fichier.

                buffer.Add(new UpgradeSaveEntry(kvp.Key, level));
            }
        }

        public bool TryPurchasePrestige(string id)
        {
            var config = _catalog.GetById(id);
            if (config == null) return false;

            int currentLevel = GetLevel(id);
            if (currentLevel >= config.MaxLevel) return false;

            double cost = config.BaseCost * Math.Pow(config.CostMultiplier, currentLevel);

            if (_currencies.CpuCycles.TryRemove(cost))
            {
                if (_prestigeLevels.TryGetValue(id, out var levelProp))
                {
                    levelProp.Value++; // Cela notifiera instantanément la ligne UI connectée !
                }
                else
                {
                    _prestigeLevels[id] = new ReactiveProperty<int>(1);
                }

                RecalculateBonuses();
                _onPrestigePurchased.OnNext(id);
                return true;
            }

            return false;
        }

        public int GetLevel(string id)
        {
            return _prestigeLevels.TryGetValue(id, out var levelProp) ? levelProp.CurrentValue : 0;
        }

        public Observable<int> GetLevelObservable(string id)
        {
            // Si la clé n'existe pas encore, on la crée avec un niveau à 0
            if (!_prestigeLevels.TryGetValue(id, out var levelProp))
            {
                levelProp = new ReactiveProperty<int>(0);
                _prestigeLevels[id] = levelProp;
            }

            return levelProp;
        }

        private void RecalculateBonuses()
        {
            float computeBonus = 0f;
            float traceReduction = 0f;
            float clickBonus = 0f;
            float costReduction = 0f;
            double startingFunds = 0d;
            double startingPower = 0d;
            bool emergencyUnlocked = false;

            foreach (var kvp in _prestigeLevels)
            {
                var config = _catalog.GetById(kvp.Key);
                if (config == null) continue;

                int currentLevel = kvp.Value.CurrentValue;
                float totalBonus = config.BonusPerLevel * currentLevel;

                switch (config.BonusType)
                {
                    case PrestigeBonusType.GlobalComputeMultiplier:
                        computeBonus += totalBonus;
                        break;

                    case PrestigeBonusType.TraceReduction:
                        traceReduction += totalBonus;
                        break;

                    case PrestigeBonusType.ClickPowerMultiplier:
                        clickBonus += totalBonus;
                        break;

                    case PrestigeBonusType.CostMultiplierReduction:
                        costReduction += totalBonus;
                        break;

                    case PrestigeBonusType.StartingMoney:
                        startingFunds += (config.BaseCost * currentLevel);
                        break;

                    case PrestigeBonusType.StartingComputerPower:
                        startingPower += (config.BaseCost * currentLevel);
                        break;

                    case PrestigeBonusType.UnlockEmergencyButton:
                        if (currentLevel > 0) emergencyUnlocked = true;
                        break;

                    // TODO (thème Économie) : SpecificUpgradeCostReduction, SpecificUpgradeYieldBoost
                    // et SpecificUpgradeTimeReduction ne sont pas encore traités. Les nœuds
                    // correspondants sont achetables mais n'appliquent rien.
                }
            }

            // Application mathématique des bonus
            GlobalComputeMultiplier.Value = 1f + computeBonus;
            TraceReductionMultiplier.Value = UnityEngine.Mathf.Max(0.1f, 1f - traceReduction); // Ne pas descendre sous 10%
            ClickPowerMultiplier.Value = 1f + clickBonus;
            CostMultiplierReduction.Value = costReduction;
            StartingMoney.Value = startingFunds;
            StartingComputerPower.Value = startingPower;
            IsEmergencyUnlocked.Value = emergencyUnlocked;
        }

        public void Dispose()
        {
            foreach (var levelProp in _prestigeLevels.Values)
            {
                levelProp.Dispose();
            }
            _prestigeLevels.Clear();

            _onPrestigePurchased.Dispose();

            GlobalComputeMultiplier.Dispose();
            TraceReductionMultiplier.Dispose();
            ClickPowerMultiplier.Dispose();
            CostMultiplierReduction.Dispose();
            StartingMoney.Dispose();
            StartingComputerPower.Dispose();
            IsEmergencyUnlocked.Dispose();
        }
    }
}
