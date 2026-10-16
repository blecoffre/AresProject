using Core.Economy.Data;
using Core.Models;
using Core.Models.Economy;
using R3;
using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace Core.Services.Economy
{
    /// <summary>
    /// L'arbre de compilation : les niveaux achetés, les règles d'achat, et ce que tout cela
    /// rapporte, exposé en propriétés réactives en lecture seule.
    ///
    /// Chargé en premier dans la chaîne de la méta-progression (refonte « Time Attack ») : il ne
    /// connaît ni l'argent de la run ni la jauge. L'UpgradeManager et le ThreatManager lisent ses
    /// sommes ; jamais l'inverse.
    /// </summary>
    public class PrestigeManager : IStartable, IDisposable
    {
        private readonly PrestigeCatalogSO _catalog;
        private readonly UserCurrencies _currencies;
        private readonly BalancingConfigSO _balancing;

        // Dictionnaire des niveaux achetés (Id -> Niveau actuel)
        private readonly Dictionary<string, ReactiveProperty<int>> _prestigeLevels = new();

        private readonly Subject<string> _onPrestigePurchased = new();
        private readonly Subject<Unit> _onBonusesRecalculated = new();

        private IDisposable _balancingSubscription;

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

        private readonly ReactiveProperty<float> _globalComputeMultiplier = new(1f);
        private readonly ReactiveProperty<float> _costMultiplierReduction = new(0f);
        private readonly ReactiveProperty<float> _totalHardenBonus = new(0f);
        private readonly ReactiveProperty<float> _totalInterceptBonus = new(0f);
        private readonly ReactiveProperty<float> _totalOverclockBonus = new(0f);
        private readonly ReactiveProperty<float> _cleanExitBonusMultiplier = new(1f);
        private readonly ReactiveProperty<bool> _isEmergencyUnlocked = new(false);
        private readonly ReactiveProperty<bool> _isHighRiskExtractionUnlocked = new(false);
        private readonly ReactiveProperty<bool> _isSpacebarUnlocked = new(false);
        private readonly ReactiveProperty<double> _totalStartingMoneyBonus = new(0d);
        private readonly ReactiveProperty<float> _totalOverclockCritChance = new(0f);
        private readonly ReactiveProperty<float> _totalAlertBonus = new(0f);
        private readonly ReactiveProperty<bool> _isAutoProxyUnlocked = new(false);
        private readonly ReactiveProperty<int> _totalPointsSpent = new(0);
        private readonly ReactiveProperty<float> _totalCostMultiplier = new(1f);
        private readonly ReactiveProperty<float> _totalYieldBonus = new(1f);

        /// <summary>
        /// Multiplicateur des TFlops NETTES apporté par `P_DATAM` : <c>1 + Σ</c>. Vaut 1 sans
        /// aucun nœud. Les TFlops brutes, celles de la menace, n'y sont jamais soumises.
        /// </summary>
        public ReadOnlyReactiveProperty<float> GlobalComputeMultiplier => _globalComputeMultiplier;

        /// <summary>
        /// Ce que `P_REFAC` retranche au multiplicateur de coût de base de chaque générateur.
        /// L'UpgradeModel applique le plancher de 1,01.
        /// </summary>
        public ReadOnlyReactiveProperty<float> CostMultiplierReduction => _costMultiplierReduction;

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
        /// Multiplie le bonus des TROIS paliers d'extraction : <c>1 + Σ Extraction</c>. Jamais
        /// sous 1 : un nœud d'Extraction ne peut pas rendre une sortie propre moins payante qu'à
        /// l'origine.
        /// </summary>
        public ReadOnlyReactiveProperty<float> CleanExitBonusMultiplier => _cleanExitBonusMultiplier;

        /// <summary>Le Data Wiper est-il débloqué (`P_EMERG`).</summary>
        public ReadOnlyReactiveProperty<bool> IsEmergencyUnlocked => _isEmergencyUnlocked;

        /// <summary>
        /// Les paliers d'extraction marqués « sous condition » — aujourd'hui le seul palier à
        /// 90 % — versent-ils leur bonus. Faux par défaut : le palier le plus dangereux était
        /// proposé dès la première run, alors que le joueur pilote encore à l'aveugle.
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsHighRiskExtractionUnlocked => _isHighRiskExtractionUnlocked;

        /// <summary>
        /// La barre d'Espace déclenche-t-elle l'Overclock (`P_OVERCLOCK_SPACEBAR`, « Saisie
        /// Multiplexée »). Faux par défaut depuis le 2026-10-16 : le bouton à l'écran est libre
        /// dès le départ, le clavier s'achète.
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsSpacebarUnlocked => _isSpacebarUnlocked;

        /// <summary>
        /// Datas AJOUTÉES à l'argent de départ de chaque run (branche « Capital de Départ »).
        /// Vaut 0 sans aucun nœud.
        /// </summary>
        public ReadOnlyReactiveProperty<double> TotalStartingMoneyBonus => _totalStartingMoneyBonus;

        /// <summary>
        /// Chance qu'un Overclock soit critique (`P_OVERCLOCK_CRIT`), entre 0 et 1 : avance ×3,
        /// aucune pénalité. Vaut 0 sans le nœud.
        /// </summary>
        public ReadOnlyReactiveProperty<float> TotalOverclockCritChance => _totalOverclockCritChance;

        /// <summary>
        /// État d'Alerte (`P_ALERT_STATE`) : le rendement d'un Script est multiplié par
        /// <c>1 + jauge × cette somme</c> au versement. Vaut 0 sans le nœud.
        /// </summary>
        public ReadOnlyReactiveProperty<float> TotalAlertBonus => _totalAlertBonus;

        /// <summary>La Routine Pare-Feu achète-t-elle d'office un Proxy à 95 % de jauge (`P_FIREWALL_ROUTINE`).</summary>
        public ReadOnlyReactiveProperty<bool> IsAutoProxyUnlocked => _isAutoProxyUnlocked;

        /// <summary>
        /// Points de prestige DÉPENSÉS dans tout l'arbre, `P_CORE_OVERRIDE` compris. C'est la
        /// seule entrée du Noyau Compilé : il récompense l'investissement, quel qu'en soit
        /// l'objet.
        /// </summary>
        public ReadOnlyReactiveProperty<int> TotalPointsSpent => _totalPointsSpent;

        /// <summary>
        /// Noyau Compilé — multiplicateur du coût de TOUS les générateurs :
        /// <c>max(plancher ; base ^ points dépensés)</c>, soit 0,99^points borné à 0,05.
        /// </summary>
        public ReadOnlyReactiveProperty<float> TotalCostMultiplier => _totalCostMultiplier;

        /// <summary>
        /// Noyau Compilé — multiplicateur du RENDEMENT : <c>1 + points dépensés × 0,01</c>. Il
        /// s'applique aux Datas des Scripts et aux TFlops nettes, jamais à la vitesse de
        /// l'A.M.I. ni au Wipe des Proxies.
        /// </summary>
        public ReadOnlyReactiveProperty<float> TotalYieldBonus => _totalYieldBonus;

        public PrestigeManager(PrestigeCatalogSO catalog, UserCurrencies currencies, BalancingConfigSO balancing)
        {
            _catalog = catalog;
            _currencies = currencies;
            _balancing = balancing;
        }

        public void Start()
        {
            // Retouche du Noyau dans l'inspecteur, en Play Mode : les multiplicateurs sont en
            // cache, il faut les recalculer — et le signal de recalcul les pousse dans les
            // générateurs. Éditeur seulement, le sujet n'émet jamais dans un build.
            _balancingSubscription = _balancing.Edited.Subscribe(_ => RecalculateBonuses());
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
        /// désormais exiger un rang précis du parent, voire son maximum — c'est la règle de toute
        /// la refonte « Time Attack ».
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

            // Un nœud sans plafond a pour MaxLevel int.MaxValue : la garde reste la même.
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
            float computeBonus = 0f;
            float hardenBonus = 0f;
            float interceptBonus = 0f;
            float overclockBonus = 0f;
            float costReduction = 0f;
            float cleanExitBonus = 0f;
            bool emergencyUnlocked = false;
            bool highRiskExtraction = false;
            bool spacebarUnlocked = false;
            double startingMoneyBonus = 0d;
            float critChance = 0f;
            float alertBonus = 0f;
            bool autoProxyUnlocked = false;
            double pointsSpent = 0d;

            foreach (var kvp in _prestigeLevels)
            {
                // Un id absent du catalogue — nœud retiré depuis la sauvegarde — ne rapporte rien
                // et ne compte pas au Noyau : ses points ne sont plus dans l'arbre.
                var config = _catalog.GetById(kvp.Key);
                if (config == null) continue;

                int currentLevel = kvp.Value.CurrentValue;
                float totalBonus = config.BonusPerLevel * currentLevel;

                pointsSpent += config.GetTotalCostOfLevels(currentLevel);

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

                    case PrestigeBonusType.CostMultiplierReduction:
                        costReduction += totalBonus;
                        break;

                    case PrestigeBonusType.UnlockEmergencyButton:
                        if (currentLevel > 0) emergencyUnlocked = true;
                        break;

                    // Déblocage booléen, comme le précédent : seul le fait de posséder le nœud
                    // compte, la magnitude n'aurait aucun sens.
                    case PrestigeBonusType.UnlockHighRiskExtraction:
                        if (currentLevel > 0) highRiskExtraction = true;
                        break;

                    // Nœuds ajoutés le 2026-10-16 (GD).
                    case PrestigeBonusType.UnlockSpacebar:
                        if (currentLevel > 0) spacebarUnlocked = true;
                        break;

                    case PrestigeBonusType.StartingMoneyBonus:
                        startingMoneyBonus += totalBonus;
                        break;

                    case PrestigeBonusType.OverclockCritChance:
                        critChance += totalBonus;
                        break;

                    case PrestigeBonusType.AlertStateYieldBonus:
                        alertBonus += totalBonus;
                        break;

                    case PrestigeBonusType.UnlockAutoProxy:
                        if (currentLevel > 0) autoProxyUnlocked = true;
                        break;

                    // Ni la racine ni le puits à points n'ont d'effet propre. Le second ne sert
                    // qu'au Noyau, par ses points déjà comptés ci-dessus. Les cas sont écrits pour
                    // que le switch reste exhaustif et qu'un futur type oublié se remarque.
                    case PrestigeBonusType.Root:
                    case PrestigeBonusType.CoreOverride:
                        break;
                }
            }

            _globalComputeMultiplier.Value = 1f + UnityEngine.Mathf.Max(0f, computeBonus);
            _costMultiplierReduction.Value = UnityEngine.Mathf.Max(0f, costReduction);

            // Jamais sous zéro : un nœud ne peut pas accélérer l'A.M.I., affaiblir un Proxy ou
            // retirer de l'avance à l'Overclock.
            _totalHardenBonus.Value = UnityEngine.Mathf.Max(0f, hardenBonus);
            _totalInterceptBonus.Value = UnityEngine.Mathf.Max(0f, interceptBonus);
            _totalOverclockBonus.Value = UnityEngine.Mathf.Max(0f, overclockBonus);

            // Jamais sous 1 : un nœud d'Extraction ne peut pas rendre une sortie propre moins
            // payante qu'à l'origine.
            _cleanExitBonusMultiplier.Value = UnityEngine.Mathf.Max(1f, 1f + cleanExitBonus);
            _isEmergencyUnlocked.Value = emergencyUnlocked;
            _isHighRiskExtractionUnlocked.Value = highRiskExtraction;

            // Mêmes bornes que les branches ci-dessus : un nœud ne peut que donner. La chance de
            // critique est une probabilité, plafonnée à 1.
            _isSpacebarUnlocked.Value = spacebarUnlocked;
            _totalStartingMoneyBonus.Value = Math.Max(0d, startingMoneyBonus);
            _totalOverclockCritChance.Value = UnityEngine.Mathf.Clamp01(critChance);
            _totalAlertBonus.Value = UnityEngine.Mathf.Max(0f, alertBonus);
            _isAutoProxyUnlocked.Value = autoProxyUnlocked;

            ApplyCore((int)Math.Round(pointsSpent));

            // Émis EN DERNIER, une fois toutes les sommes cohérentes. C'est ce signal qui pousse
            // le Noyau et P_REFAC dans les UpgradeModel : sans lui, ils seraient ignorés au
            // chargement, puisque le GameStateGateway restaure les générateurs (étape 2) AVANT
            // le prestige (étape 3).
            _onBonusesRecalculated.OnNext(Unit.Default);
        }

        /// <summary>
        /// Le Noyau Compilé, dérivé des seuls points dépensés. Le coût décroît en puissance et
        /// bute sur un plancher ; le rendement croît sans fin, et c'est voulu : c'est ce qui
        /// donne une raison d'acheter `P_CORE_OVERRIDE` une fois l'arbre fini.
        /// </summary>
        private void ApplyCore(int pointsSpent)
        {
            float costMultiplier = UnityEngine.Mathf.Pow(_balancing.CoreCostBase, pointsSpent);

            // Les points EN DERNIER : un abonné aux points qui lit les multiplicateurs dans la
            // foulée doit les trouver déjà à jour.
            _totalCostMultiplier.Value = UnityEngine.Mathf.Max(_balancing.CoreCostFloor, costMultiplier);
            _totalYieldBonus.Value = 1f + pointsSpent * _balancing.CoreYieldPerPoint;
            _totalPointsSpent.Value = pointsSpent;
        }

        public void Dispose()
        {
            _balancingSubscription?.Dispose();

            foreach (var levelProp in _prestigeLevels.Values)
            {
                levelProp.Dispose();
            }
            _prestigeLevels.Clear();

            _onPrestigePurchased.Dispose();
            _onBonusesRecalculated.Dispose();
            _arePurchasesAllowed.Dispose();

            _globalComputeMultiplier.Dispose();
            _costMultiplierReduction.Dispose();
            _totalHardenBonus.Dispose();
            _totalInterceptBonus.Dispose();
            _totalOverclockBonus.Dispose();
            _cleanExitBonusMultiplier.Dispose();
            _isEmergencyUnlocked.Dispose();
            _isHighRiskExtractionUnlocked.Dispose();
            _isSpacebarUnlocked.Dispose();
            _totalStartingMoneyBonus.Dispose();
            _totalOverclockCritChance.Dispose();
            _totalAlertBonus.Dispose();
            _isAutoProxyUnlocked.Dispose();
            _totalPointsSpent.Dispose();
            _totalCostMultiplier.Dispose();
            _totalYieldBonus.Dispose();
        }
    }
}
