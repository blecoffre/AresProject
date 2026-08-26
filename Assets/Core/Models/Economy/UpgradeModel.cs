using R3;
using System;

namespace Core.Models.Economy
{
    /// <summary>
    /// État vivant d'un générateur pour la partie en cours. Aucune référence à Unity.
    ///
    /// Le versement, la durée de cycle et le coût sont mis en cache et recalculés uniquement
    /// quand le niveau change. Sans ce cache, chaque tick d'argent déclenchait un Math.Pow et un
    /// parcours des paliers par générateur affiché — avec 45 générateurs, ça se sent.
    /// </summary>
    public class UpgradeModel : IDisposable
    {
        /// <summary>
        /// Coefficient de compression du temps par TFlop : TempsReel = TempsBase / (1 + TFlops × k).
        /// Décroissance asymptotique — la durée tend vers zéro sans jamais l'atteindre, et le
        /// plancher MinCycleDuration reprend la main avant.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        private const double TFlopsTimeCompression = 0.05d;

        public UpgradeConfigSO Config { get; }

        private readonly ReactiveProperty<int> _currentLevel;
        public ReadOnlyReactiveProperty<int> CurrentLevel => _currentLevel;

        private double _cachedYield;
        private double _cachedCost;
        private float _cachedCycleDuration;

        /// <summary>
        /// Capacité de calcul globale du joueur. Elle ne dépend pas de CE générateur mais de tout
        /// le parc Hardware : c'est l'UpgradeManager qui la pousse ici à chaque variation, ce qui
        /// invalide le cache de durée. Sans cette poussée, acheter un Hardware ne raccourcirait
        /// jamais les cycles déjà en cache.
        /// </summary>
        private double _tflops;

        /// <summary>
        /// Niveaux retirés au seuil d'automatisation par les nœuds de prestige ciblés.
        /// Alimenté par le PrestigeManager (thème Prestige) ; reste à 0 pour l'instant.
        /// </summary>
        private int _automationThresholdReduction;

        public UpgradeModel(UpgradeConfigSO config, int savedLevel = 0)
        {
            Config = config;
            _currentLevel = new ReactiveProperty<int>(savedLevel < 0 ? 0 : savedLevel);

            RecalculateCache();
        }

        public bool IsOwned => _currentLevel.CurrentValue > 0;

        /// <summary>
        /// Niveau à partir duquel le générateur relance ses cycles seul.
        /// Jamais sous 1 : un nœud de prestige ne doit pas pouvoir automatiser un générateur
        /// que le joueur ne possède pas encore.
        /// </summary>
        public int AutomationThreshold => Math.Max(1, Config.AutomationLevel - _automationThresholdReduction);

        public bool IsAutomated => _currentLevel.CurrentValue >= AutomationThreshold;

        public void SetAutomationThresholdReduction(int levels)
        {
            _automationThresholdReduction = levels < 0 ? 0 : levels;
        }

        /// <summary>
        /// Met à jour la capacité de calcul et recalcule la durée de cycle si elle a changé.
        /// La garde d'égalité compte : l'UpgradeManager pousse la valeur à tous les modèles à
        /// chaque achat, et un achat de Script ne change pas les TFlops.
        /// </summary>
        public void SetTFlops(double tflops)
        {
            double safe = tflops < 0d ? 0d : tflops;
            if (safe == _tflops) return;

            _tflops = safe;
            RecalculateCache();
        }

        /// <summary>
        /// Coût du prochain niveau : C(n) = C_base * M^n.
        /// Sans réduction de prestige, la valeur mise en cache est renvoyée directement.
        /// </summary>
        public double GetCurrentCost(float costMultiplierReduction = 0f)
        {
            if (costMultiplierReduction <= 0f) return _cachedCost;

            // On empêche le multiplicateur de descendre sous 1.01, sinon la courbe de coût s'aplatit
            // et l'économie n'a plus de frein.
            double finalMultiplier = Math.Max(1.01d, Config.CostMultiplier - costMultiplierReduction);
            return Config.BaseCost * Math.Pow(finalMultiplier, _currentLevel.CurrentValue);
        }

        /// <summary>Montant versé à la fin d'un cycle, paliers inclus.</summary>
        public double GetCurrentYield() => _cachedYield;

        /// <summary>Durée d'un cycle, paliers inclus, plancher appliqué.</summary>
        public float GetCurrentCycleDuration() => _cachedCycleDuration;

        public float GetCurrentTracePerSecond()
        {
            return (float)(Config.BaseTraceGeneratedPerSecond * _currentLevel.CurrentValue);
        }

        /// <summary>Rendement théorique par seconde si le cycle tourne en continu. Sert à l'affichage.</summary>
        public double GetYieldPerSecond()
        {
            if (_cachedCycleDuration <= 0f) return 0d;
            return _cachedYield / _cachedCycleDuration;
        }

        public void LevelUp()
        {
            _currentLevel.Value++;
            RecalculateCache();
        }

        private void RecalculateCache()
        {
            int level = _currentLevel.CurrentValue;

            double yield = Config.BaseProductionYield * level;
            float duration = Config.BaseCycleDuration;

            // Paliers : effets multiplicatifs, cumulatifs, et définitifs une fois atteints.
            var milestones = Config.Milestones;
            for (int i = 0; i < milestones.Count; i++)
            {
                UpgradeMilestone milestone = milestones[i];
                if (level < milestone.Level) continue;

                if (milestone.Effect == MilestoneEffect.YieldMultiplier)
                {
                    yield *= milestone.Factor;
                }
                else
                {
                    duration *= milestone.Factor;
                }
            }

            // Compression par les TFlops, APRÈS les paliers et AVANT le plancher : le tooltip du
            // champ dit « plancher absolu, aucun palier ni bonus ne peut descendre sous cette
            // durée ». Les TFlops sont un bonus comme un autre, ils ne le franchissent pas.
            // Calcul en double : à 1,5e9 TFlops, un float perdrait la précision du diviseur.
            double compressed = duration / (1d + _tflops * TFlopsTimeCompression);

            _cachedYield = yield;
            _cachedCycleDuration = Math.Max(Config.MinCycleDuration, (float)compressed);
            _cachedCost = Config.BaseCost * Math.Pow(Config.CostMultiplier, level);
        }

        public void Dispose()
        {
            _currentLevel.Dispose();
        }
    }
}
