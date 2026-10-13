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
        /// La fenêtre de compilation. Fermée pendant toute une run, ouverte entre deux.
        ///
        /// C'est une règle de GAME DESIGN, pas d'ergonomie : les CPU Cycles se gagnent en
        /// terminant une run, et pouvoir les dépenser au milieu d'une autre transformerait
        /// l'arbre en boutique d'appoint qu'on rouvre dès qu'on est en difficulté. Pendant une
        /// run, l'arbre reste consultable — c'est même son intérêt, pour planifier — mais
        /// strictement en lecture.
        ///
        /// Vaut `true` par défaut : sans GameSessionManager pour la piloter (un test, une scène
        /// isolée), le prestige reste utilisable plutôt que muet.
        /// </summary>
        private readonly ReactiveProperty<bool> _arePurchasesAllowed = new(true);

        /// <summary>La fenêtre de compilation est-elle ouverte. Piloté par le GameSessionManager.</summary>
        public ReadOnlyReactiveProperty<bool> ArePurchasesAllowed => _arePurchasesAllowed;

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

        private readonly ReactiveProperty<float> _totalHardenBonus = new(0f);
        private readonly ReactiveProperty<float> _totalInterceptBonus = new(0f);
        private readonly ReactiveProperty<float> _totalOverclockBonus = new(0f);

        /// <summary>
        /// Somme de la branche Blindage. La vitesse de l'A.M.I. est DIVISÉE par
        /// <c>1 + cette somme</c> : à 10, l'arbre complet, une jauge dure onze fois plus
        /// longtemps. Vaut 0 sans aucun nœud acheté.
        /// </summary>
        public ReadOnlyReactiveProperty<float> TotalHardenBonus => _totalHardenBonus;

        /// <summary>
        /// Somme de la branche Interception. Le Wipe d'un Proxy est MULTIPLIÉ par
        /// <c>1 + cette somme</c>. Vaut 0 sans aucun nœud acheté.
        /// </summary>
        public ReadOnlyReactiveProperty<float> TotalInterceptBonus => _totalInterceptBonus;

        /// <summary>
        /// Somme de la branche Overclock, en SECONDES ajoutées à l'avance de base d'un Overclock.
        /// Elle ne touche jamais la pénalité. Vaut 0 sans aucun nœud acheté.
        /// </summary>
        public ReadOnlyReactiveProperty<float> TotalOverclockBonus => _totalOverclockBonus;

        /// <summary>
        /// Multiplie le bonus des paliers d'extraction. Jamais sous 1 : un noeud d'Extraction ne
        /// peut pas rendre une sortie propre moins payante qu'a l'origine.
        /// </summary>
        public ReactiveProperty<float> CleanExitBonusMultiplier { get; } = new(1f);

        /// <summary>
        /// Niveaux retirés au palier d'automatisation de TOUS les Scripts. Remplace quinze
        /// nœuds ciblés qui éparpillaient le même effet.
        /// </summary>
        public ReactiveProperty<float> GlobalAutomationReduction { get; } = new(0f);

        public ReactiveProperty<float> CostMultiplierReduction { get; } = new(0f);
        public ReactiveProperty<double> StartingMoney { get; } = new(0d);
        public ReactiveProperty<double> StartingComputerPower { get; } = new(0d);
        public ReactiveProperty<bool> IsEmergencyUnlocked { get; } = new(false);

        /// <summary>
        /// Les paliers d'extraction marqués « sous condition » — aujourd'hui le seul palier à
        /// 90 % — versent-ils leur bonus. Faux par défaut : le palier le plus dangereux était
        /// proposé dès la première run, alors que le joueur pilote encore à l'aveugle.
        /// </summary>
        public ReactiveProperty<bool> IsHighRiskExtractionUnlocked { get; } = new(false);

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
        /// Nœud accessible : son parent est monté au niveau exigé, ou il n'a pas de parent.
        /// Exposé pour que la vue reflète la règle au lieu de la redéfinir de son côté.
        ///
        /// Le seuil n'est plus figé à « niveau &gt; 0 » depuis le 2026-09-09 : un nœud peut
        /// désormais exiger un rang précis du parent, voire son maximum. Sans quoi une ligne à
        /// dix rangs comme les paliers d'Overclock s'ouvrait entièrement pour un seul cran acheté.
        /// </summary>
        public bool IsUnlocked(PrestigeConfigSO config)
        {
            if (config == null) return false;

            PrestigeRequirement requirement = config.Requirement;
            if (!requirement.HasNode) return true;

            return GetLevel(requirement.Node.Id) >= requirement.ResolveRequiredLevel();
        }

        /// <summary>
        /// Ouvre ou ferme la fenêtre de compilation. Un seul appelant légitime : le
        /// GameSessionManager, qui est le seul à savoir si une run est en cours.
        /// </summary>
        public void SetPurchaseWindowOpen(bool isOpen)
        {
            _arePurchasesAllowed.Value = isOpen;
        }

        public bool TryPurchasePrestige(string id)
        {
            // Première garde, avant même de chercher le nœud : on n'achète pas pendant une run.
            // Elle vit ICI et pas dans la vue — un bouton grisé n'est pas une règle, c'est une
            // politesse, et le 2026-08-27 on a déjà vu ce qu'un raccourci direct pouvait faire
            // avec la profondeur de l'arbre.
            if (!_arePurchasesAllowed.CurrentValue) return false;

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

            double cost = config.GetCostAtLevel(currentLevel);

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
            float hardenBonus = 0f;
            float interceptBonus = 0f;
            float overclockBonus = 0f;
            float costReduction = 0f;
            double startingFunds = 0d;
            double startingPower = 0d;
            float cleanExitBonus = 0f;
            float automationReduction = 0f;
            bool emergencyUnlocked = false;
            bool highRiskExtraction = false;

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

                    // Les trois branches du compte à rebours. Toutes trois des SOMMES, que les
                    // systèmes appliquent en (1 + Σ) ou en secondes ajoutées : aucun nœud ne peut
                    // rendre le joueur plus fragile qu'à l'origine.
                    case PrestigeBonusType.HardenBonus:
                        hardenBonus += totalBonus;
                        break;

                    case PrestigeBonusType.InterceptBonus:
                        interceptBonus += totalBonus;
                        break;

                    case PrestigeBonusType.OverclockBonus:
                        overclockBonus += totalBonus;
                        break;

                    case PrestigeBonusType.CleanExitBonusMultiplier:
                        cleanExitBonus += totalBonus;
                        break;

                    case PrestigeBonusType.GlobalAutomationReduction:
                        automationReduction += totalBonus;
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

                    // Déblocage booléen, comme le précédent : seul le fait de posséder le nœud
                    // compte, la magnitude n'aurait aucun sens.
                    case PrestigeBonusType.UnlockHighRiskExtraction:
                        if (currentLevel > 0) highRiskExtraction = true;
                        break;

                    // La racine n'a aucun effet : elle ne sert qu'à ancrer l'arbre. Le cas est
                    // écrit explicitement pour que le switch reste exhaustif et qu'un futur type
                    // oublié se remarque, plutôt que de tomber dans un default silencieux.
                    case PrestigeBonusType.Root:
                        break;

                    case PrestigeBonusType.SpecificUpgradeCostReduction:
                        AccumulateSpecific(config.TargetUpgradeId, totalBonus, UpgradePrestigeNodeBonusType.Cost);
                        break;

                    case PrestigeBonusType.SpecificUpgradeYieldBoost:
                        AccumulateSpecific(config.TargetUpgradeId, totalBonus, UpgradePrestigeNodeBonusType.Yield);
                        break;

                    case PrestigeBonusType.SpecificUpgradeTimeReduction:
                        AccumulateSpecific(config.TargetUpgradeId, totalBonus, UpgradePrestigeNodeBonusType.Time);
                        break;

                    case PrestigeBonusType.SpecificUpgradeAutomationTresholdReduction:
                        AccumulateSpecific(config.TargetUpgradeId, totalBonus, UpgradePrestigeNodeBonusType.Automation);
                        break;
                }
            }

            // Application mathématique des bonus
            GlobalComputeMultiplier.Value = 1f + computeBonus;

            // Jamais sous zéro : un nœud ne peut pas accélérer l'A.M.I., affaiblir un Proxy ou
            // retirer de l'avance à l'Overclock.
            _totalHardenBonus.Value = UnityEngine.Mathf.Max(0f, hardenBonus);
            _totalInterceptBonus.Value = UnityEngine.Mathf.Max(0f, interceptBonus);
            _totalOverclockBonus.Value = UnityEngine.Mathf.Max(0f, overclockBonus);

            // Jamais sous 1 : un nœud d'Extraction ne peut pas rendre une sortie propre moins
            // payante qu'à l'origine.
            CleanExitBonusMultiplier.Value = UnityEngine.Mathf.Max(1f, 1f + cleanExitBonus);
            GlobalAutomationReduction.Value = UnityEngine.Mathf.Max(0f, automationReduction);
            CostMultiplierReduction.Value = costReduction;
            StartingMoney.Value = startingFunds;
            StartingComputerPower.Value = startingPower;
            IsEmergencyUnlocked.Value = emergencyUnlocked;
            IsHighRiskExtractionUnlocked.Value = highRiskExtraction;

            // Émis EN DERNIER, une fois la table et les scalaires cohérents. C'est ce signal qui
            // pousse les bonus ciblés dans les UpgradeModel : sans lui, ils seraient ignorés au
            // chargement, puisque le GameStateGateway restaure les générateurs (étape 2) AVANT
            // le prestige (étape 3).
            _onBonusesRecalculated.OnNext(Unit.Default);
        }
        private enum UpgradePrestigeNodeBonusType { Cost, Yield, Time, Automation }

        /// <summary>
        /// Cumule un bonus ciblé dans la table. Plusieurs nœuds peuvent viser la même upgrade —
        /// c'est même la règle : chaque upgrade a son COST, son PROD et, pour les Scripts, son TIME.
        /// </summary>
        private void AccumulateSpecific(string targetUpgradeId, float amount, UpgradePrestigeNodeBonusType kind)
        {
            if (string.IsNullOrEmpty(targetUpgradeId)) return;

            _specificBonuses.TryGetValue(targetUpgradeId, out SpecificUpgradeBonuses current);

            switch (kind)
            {
                case UpgradePrestigeNodeBonusType.Cost:
                    current = current.WithCostReduction(current.CostReduction + amount);
                    break;

                case UpgradePrestigeNodeBonusType.Yield:
                    current = current.WithYieldBoost(current.YieldBoost + amount);
                    break;

                case UpgradePrestigeNodeBonusType.Time:
                    current = current.WithTimeReduction(current.TimeReduction + amount);
                    break;

                // Des NIVEAUX, pas une fraction : ce nœud avance le palier d'automatisation d'un
                // cran par rang. Le cas existait déjà dans le switch appelant, mais il n'avait
                // aucune branche ici — le bonus était cumulé puis jeté en silence.
                case UpgradePrestigeNodeBonusType.Automation:
                    current = current.WithAutomationThresholdReduction(
                        current.AutomationThresholdReduction + amount);
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
            _arePurchasesAllowed.Dispose();
            _specificBonuses.Clear();

            GlobalComputeMultiplier.Dispose();
            _totalHardenBonus.Dispose();
            _totalInterceptBonus.Dispose();
            _totalOverclockBonus.Dispose();
            CleanExitBonusMultiplier.Dispose();
            GlobalAutomationReduction.Dispose();
            CostMultiplierReduction.Dispose();
            StartingMoney.Dispose();
            StartingComputerPower.Dispose();
            IsEmergencyUnlocked.Dispose();
            IsHighRiskExtractionUnlocked.Dispose();
        }
    }
}
