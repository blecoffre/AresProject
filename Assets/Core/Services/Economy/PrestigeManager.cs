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
        private readonly Subject<Unit> _onBonusesRecalculated = new();

        /// <summary>Bonus ciblés, indexés par l'id de l'upgrade visée. Reconstruite à chaque recalcul.</summary>
        private readonly Dictionary<string, SpecificUpgradeBonuses> _specificBonuses = new();

        /// <summary>
        /// Émis après CHAQUE recalcul — achat de nœud comme chargement de sauvegarde.
        /// À préférer à OnPrestigePurchased pour quiconque doit refléter les bonus : ce dernier
        /// n'est pas émis par InitializeFromSave, donc s'y abonner raterait le chargement.
        /// </summary>
        public Observable<Unit> OnBonusesRecalculated => _onBonusesRecalculated;

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

        /// <summary>
        /// Nombre de charges de Ghost Cache que le joueur peut stocker. <b>Vaut 0 par défaut</b> :
        /// le Zéro-Day Exploit est verrouillé tant que le nœud `P_EXPLOIT_CHARGES` n'a pas été
        /// acheté au moins une fois. Le Ghost Cache continue de capter l'excédent, mais la
        /// capacité étant nulle, rien ne s'accumule.
        /// </summary>
        public ReactiveProperty<int> ExploitMaxCharges { get; } = new(0);

        /// <summary>
        /// Majoration du multiplicateur de rendement de l'Exploit, en fraction. 1 = doublement du
        /// ×50 de base, donc ×100. Additif et non exponentiel, comme tous les bonus du projet.
        /// </summary>
        public ReactiveProperty<double> ExploitYieldBoost { get; } = new(0d);

        /// <summary>
        /// Points retirés au malus de Trace de l'Exploit. Le multiplicateur effectif vaut
        /// `×10 − cette valeur`, borné à ×1 : le joueur ne peut jamais rendre l'Exploit indolore.
        /// </summary>
        public ReactiveProperty<float> ExploitTracePenaltyReduction { get; } = new(0f);

        /// <summary>
        /// Le clic d'Overclock démarre-t-il aussi les Scripts à l'arrêt. Faux par défaut : le
        /// lancement manuel est une contrainte assumée du début et du milieu de partie, et ce
        /// nœud tardif ne sert qu'à fluidifier les runs de haut niveau.
        /// </summary>
        public ReactiveProperty<bool> OverclockWakesScripts { get; } = new(false);

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

        /// <summary>
        /// Nœud accessible : son prérequis est possédé, ou il n'en a pas. Exposé pour que la vue
        /// reflète la règle au lieu de la redéfinir de son côté.
        /// </summary>
        public bool IsUnlocked(PrestigeConfigSO config)
        {
            if (config == null) return false;
            if (config.Prerequisite == null) return true;

            return GetLevel(config.Prerequisite.Id) > 0;
        }

        public bool TryPurchasePrestige(string id)
        {
            var config = _catalog.GetById(id);
            if (config == null) return false;

            // La profondeur de l'arbre est une RÈGLE, elle vit donc ici et pas seulement dans la
            // vue. Jusqu'au 2026-08-27, seul PrestigeItemPresenter la faisait respecter : un appel
            // direct achetait n'importe quel nœud sans ses parents — vérifié en Play Mode sur
            // P_OVERCLOCK_AWAKE, acheté sans un seul de ses quatre ascendants. Un bouton « tout
            // acheter », un raccourci de test oublié ou un refactor du panneau auraient suffi à
            // rendre toute la méta-progression facultative, sans rien casser de visible.
            if (!IsUnlocked(config)) return false;

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
            // Table reconstruite intégralement : un nœud dont le niveau retombe à zéro doit voir
            // son apport disparaître, pas rester coincé dans une entrée obsolète.
            _specificBonuses.Clear();

            float computeBonus = 0f;
            float traceReduction = 0f;
            float clickBonus = 0f;
            float costReduction = 0f;
            double startingFunds = 0d;
            double startingPower = 0d;
            bool emergencyUnlocked = false;
            int exploitCharges = 0;
            double exploitYieldBoost = 0d;
            float exploitTraceRelief = 0f;
            bool overclockWakes = false;

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

                    // BonusPerLevel et non BaseCost : indexer l'effet d'un nœud sur son PRIX
                    // couplait deux réglages qui doivent bouger séparément à l'équilibrage.
                    case PrestigeBonusType.StartingMoney:
                        startingFunds += totalBonus;
                        break;

                    case PrestigeBonusType.StartingComputerPower:
                        startingPower += totalBonus;
                        break;

                    case PrestigeBonusType.UnlockEmergencyButton:
                        if (currentLevel > 0) emergencyUnlocked = true;
                        break;

                    // Arrondi et non troncature : un BonusPerLevel de 1 stocké en float peut
                    // valoir 0,99999994, et (int)(0,99999994 × 5) rendrait 4 charges au lieu de 5.
                    case PrestigeBonusType.UnlockExploitCharges:
                        exploitCharges += UnityEngine.Mathf.RoundToInt(totalBonus);
                        break;

                    case PrestigeBonusType.ExploitYieldBoost:
                        exploitYieldBoost += totalBonus;
                        break;

                    case PrestigeBonusType.ExploitTracePenaltyReduction:
                        exploitTraceRelief += totalBonus;
                        break;

                    case PrestigeBonusType.OverclockWakesScripts:
                        if (currentLevel > 0) overclockWakes = true;
                        break;

                    case PrestigeBonusType.SpecificUpgradeCostReduction:
                        AccumulateSpecific(config.TargetUpgradeId, totalBonus, SpecificKind.Cost);
                        break;

                    case PrestigeBonusType.SpecificUpgradeYieldBoost:
                        AccumulateSpecific(config.TargetUpgradeId, totalBonus, SpecificKind.Yield);
                        break;

                    case PrestigeBonusType.SpecificUpgradeTimeReduction:
                        AccumulateSpecific(config.TargetUpgradeId, totalBonus, SpecificKind.Time);
                        break;
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
            ExploitMaxCharges.Value = exploitCharges < 0 ? 0 : exploitCharges;
            ExploitYieldBoost.Value = exploitYieldBoost < 0d ? 0d : exploitYieldBoost;
            ExploitTracePenaltyReduction.Value = exploitTraceRelief < 0f ? 0f : exploitTraceRelief;
            OverclockWakesScripts.Value = overclockWakes;

            // Émis EN DERNIER, une fois la table et les scalaires cohérents. C'est ce signal qui
            // pousse les bonus ciblés dans les UpgradeModel : sans lui, ils seraient ignorés au
            // chargement, puisque le GameStateGateway restaure les générateurs (étape 2) AVANT
            // le prestige (étape 3).
            _onBonusesRecalculated.OnNext(Unit.Default);
        }

        private enum SpecificKind { Cost, Yield, Time }

        /// <summary>
        /// Cumule un bonus ciblé dans la table. Plusieurs nœuds peuvent viser la même upgrade —
        /// c'est même la règle : chaque upgrade a son COST, son PROD et, pour les Scripts, son TIME.
        /// </summary>
        private void AccumulateSpecific(string targetUpgradeId, float amount, SpecificKind kind)
        {
            if (string.IsNullOrEmpty(targetUpgradeId)) return;

            _specificBonuses.TryGetValue(targetUpgradeId, out SpecificUpgradeBonuses current);

            switch (kind)
            {
                case SpecificKind.Cost:
                    current = current.WithCostReduction(current.CostReduction + amount);
                    break;

                case SpecificKind.Yield:
                    current = current.WithYieldBoost(current.YieldBoost + amount);
                    break;

                case SpecificKind.Time:
                    current = current.WithTimeReduction(current.TimeReduction + amount);
                    break;
            }

            _specificBonuses[targetUpgradeId] = current;
        }

        /// <summary>Bonus cumulés visant cette upgrade. Retourne None si aucun nœud ne la cible.</summary>
        public SpecificUpgradeBonuses GetSpecificBonuses(string upgradeId)
        {
            return _specificBonuses.TryGetValue(upgradeId, out SpecificUpgradeBonuses bonuses)
                ? bonuses
                : SpecificUpgradeBonuses.None;
        }

        public void Dispose()
        {
            foreach (var levelProp in _prestigeLevels.Values)
            {
                levelProp.Dispose();
            }
            _prestigeLevels.Clear();

            _onPrestigePurchased.Dispose();
            _onBonusesRecalculated.Dispose();
            _specificBonuses.Clear();

            GlobalComputeMultiplier.Dispose();
            TraceReductionMultiplier.Dispose();
            ClickPowerMultiplier.Dispose();
            CostMultiplierReduction.Dispose();
            StartingMoney.Dispose();
            StartingComputerPower.Dispose();
            IsEmergencyUnlocked.Dispose();
            ExploitMaxCharges.Dispose();
            ExploitYieldBoost.Dispose();
            ExploitTracePenaltyReduction.Dispose();
            OverclockWakesScripts.Dispose();
        }
    }
}
