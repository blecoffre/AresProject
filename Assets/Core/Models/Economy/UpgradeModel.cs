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

        /// <summary>
        /// Plafond des réductions ciblées. Avec maxLevel 5 et 0,1 par rang on plafonne à 0,5, donc
        /// cette borne ne sert à rien aujourd'hui — elle existe pour qu'augmenter maxLevel plus
        /// tard ne rende jamais un générateur gratuit ni son cycle instantané.
        /// </summary>
        private const float MaxReduction = 0.95f;

        public UpgradeConfigSO Config { get; }

        private readonly ReactiveProperty<int> _currentLevel;
        public ReadOnlyReactiveProperty<int> CurrentLevel => _currentLevel;

        private double _cachedYield;
        private float _cachedTraceMagnitude;
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
        /// Bonus de prestige visant CE générateur. Poussés par l'UpgradeManager sur le signal
        /// OnBonusesRecalculated, comme les TFlops — jamais lus dans une boucle chaude.
        /// </summary>
        private SpecificUpgradeBonuses _bonuses = SpecificUpgradeBonuses.None;

        /// <summary>
        /// Accélération apportée par le parc de Proxies possédé. Poussée par l'UpgradeManager
        /// comme les TFlops, et pour la même raison : elle ne dépend pas de CE générateur.
        /// Vaut 1 quand aucun Proxy n'est possédé.
        /// </summary>
        private double _proxySynergy = 1d;

        /// <summary>
        /// Multiplicateur temporaire de rendement, poussé par l'UpgradeManager. Sert le Zéro-Day
        /// Exploit (×50 pendant 30 s) et vaut 1 le reste du temps.
        ///
        /// Il n'agit que sur les Scripts, et cette restriction n'est pas cosmétique : chez un
        /// Hardware, GetCurrentYield() EST sa contribution en TFlops. Le laisser passer
        /// multiplierait par 50 la capacité de calcul du joueur, donc la compression des cycles
        /// ET la dissipation des Proxies — un buff économique deviendrait une invulnérabilité.
        /// </summary>
        private double _globalYieldMultiplier = 1d;

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
        /// Met à jour l'accélération apportée par les Proxies. Même garde d'égalité que
        /// SetTFlops : la valeur est poussée à tous les modèles à chaque achat.
        /// </summary>
        public void SetProxySynergy(double multiplier)
        {
            double safe = multiplier < 1d ? 1d : multiplier;
            if (safe == _proxySynergy) return;

            _proxySynergy = safe;
            RecalculateCache();
        }

        /// <summary>
        /// Met à jour le multiplicateur temporaire de rendement. Même garde d'égalité que
        /// SetTFlops : la valeur est poussée à tous les Scripts à chaque recalcul des totaux,
        /// alors qu'elle ne bouge qu'aux deux extrémités d'un Overdrive.
        /// </summary>
        public void SetGlobalYieldMultiplier(double multiplier)
        {
            double safe = multiplier < 1d ? 1d : multiplier;
            if (safe == _globalYieldMultiplier) return;

            _globalYieldMultiplier = safe;
            RecalculateCache();
        }

        /// <summary>
        /// Applique les bonus de prestige ciblant ce générateur. Même garde d'égalité que
        /// SetTFlops : la valeur est poussée à tous les modèles à chaque recalcul, et la plupart
        /// ne sont visés par aucun nœud.
        /// </summary>
        public void SetSpecificBonuses(SpecificUpgradeBonuses bonuses)
        {
            if (bonuses.CostReduction == _bonuses.CostReduction
                && bonuses.YieldBoost == _bonuses.YieldBoost
                && bonuses.TimeReduction == _bonuses.TimeReduction)
            {
                return;
            }

            _bonuses = bonuses;
            RecalculateCache();
        }

        /// <summary>
        /// Coût du prochain niveau : C(n) = C_base_ajusté * M^n.
        ///
        /// Deux réductions de prestige distinctes s'appliquent, et il ne faut pas les confondre :
        /// un nœud CIBLÉ rabote le coût de BASE (déjà intégré au cache), un nœud GLOBAL rabote le
        /// MULTIPLICATEUR M, ce qui aplatit la courbe entière.
        /// </summary>
        public double GetCurrentCost(float costMultiplierReduction = 0f)
        {
            if (costMultiplierReduction <= 0f) return _cachedCost;

            // On empêche le multiplicateur de descendre sous 1.01, sinon la courbe de coût s'aplatit
            // et l'économie n'a plus de frein.
            double finalMultiplier = Math.Max(1.01d, Config.CostMultiplier - costMultiplierReduction);
            return AdjustedBaseCost * Math.Pow(finalMultiplier, _currentLevel.CurrentValue);
        }

        /// <summary>Coût de base après réduction ciblée. Bornée pour qu'un générateur ne soit jamais gratuit.</summary>
        private double AdjustedBaseCost =>
            Config.BaseCost * (1d - Math.Min(MaxReduction, _bonuses.CostReduction));

        /// <summary>Montant versé à la fin d'un cycle, paliers inclus.</summary>
        public double GetCurrentYield() => _cachedYield;

        /// <summary>Durée d'un cycle, paliers inclus, plancher appliqué.</summary>
        public float GetCurrentCycleDuration() => _cachedCycleDuration;

        /// <summary>
        /// Magnitude de Trace par seconde, à ce niveau. Le SENS dépend du type du générateur :
        /// c'est une <b>génération</b> pour un Script ou un Hardware, mais une <b>dissipation</b>
        /// pour un Proxy — dont le rendement de production vaut zéro et dont tout l'effet passe
        /// par ce champ. Le nom du champ de données (`traceGeneratedPerSecond`) ment pour ce
        /// troisième cas ; l'interprétation du signe appartient à l'UpgradeManager, qui est le
        /// seul à connaître le type.
        /// </summary>
        public float GetTraceMagnitudePerSecond() => _cachedTraceMagnitude;

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

            // Les bonus ciblés s'appliquent aux valeurs de BASE — c'est le sens littéral des
            // trois types de nœuds (« réduit le coût de base », « augmente le rendement de
            // base », « réduit le temps de cycle »). Les paliers puis les TFlops s'appliquent
            // ensuite par-dessus, et le plancher tranche en dernier.
            double yield = Config.BaseProductionYield * (1d + _bonuses.YieldBoost) * level;
            float duration = Config.BaseCycleDuration * (1f - Math.Min(MaxReduction, _bonuses.TimeReduction));

            // La magnitude de Trace suit la même logique : base × niveau, puis les paliers.
            // Pour un Proxy — et pour lui seul — le « boost de rendement » du prestige amplifie
            // cette magnitude, qui est chez lui une DISSIPATION. L'appliquer à un Script ou à un
            // Hardware augmenterait leur trace générée, soit l'exact inverse d'un bonus.
            float traceMagnitude = (float)(Config.BaseTraceGeneratedPerSecond * level);
            if (Config.Type == UpgradeType.Proxy)
            {
                traceMagnitude *= 1f + _bonuses.YieldBoost;
            }

            // Paliers : effets multiplicatifs, cumulatifs, et définitifs une fois atteints.
            var milestones = Config.Milestones;
            for (int i = 0; i < milestones.Count; i++)
            {
                UpgradeMilestone milestone = milestones[i];
                if (level < milestone.Level) continue;

                switch (milestone.Effect)
                {
                    case MilestoneEffect.YieldMultiplier:
                        yield *= milestone.Factor;
                        break;

                    case MilestoneEffect.DurationMultiplier:
                        duration *= milestone.Factor;
                        break;

                    case MilestoneEffect.TraceMultiplier:
                        // Bonus pour un Proxy (dissipation), malus assumé ailleurs (génération).
                        traceMagnitude *= milestone.Factor;
                        break;
                }
            }

            // Zéro-Day Exploit, tout à la fin de la chaîne de rendement et pour les seuls
            // Scripts : chez un Hardware ce « rendement » est sa capacité en TFlops, et la
            // multiplier par 50 rendrait le joueur quasi indétectable au lieu de le mettre à nu.
            if (Config.Type == UpgradeType.Script)
            {
                yield *= _globalYieldMultiplier;
            }

            // Compression par les TFlops, APRÈS les paliers et AVANT le plancher : le tooltip du
            // champ dit « plancher absolu, aucun palier ni bonus ne peut descendre sous cette
            // durée ». Les TFlops sont un bonus comme un autre, ils ne le franchissent pas.
            // Calcul en double : à 1,5e9 TFlops, un float perdrait la précision du diviseur.
            double compressed = duration / (1d + _tflops * TFlopsTimeCompression);

            // Puis la synergie des Proxies, qui accélère les cycles au même titre que les TFlops.
            // Multiplicateur DÉDIÉ et non branché sur les TFlops : y passer créerait une boucle
            // de rétroaction, la dissipation dépendant elle-même des TFlops par son log10.
            compressed /= _proxySynergy;

            _cachedYield = yield;
            _cachedTraceMagnitude = traceMagnitude;
            _cachedCycleDuration = Math.Max(Config.MinCycleDuration, (float)compressed);
            _cachedCost = AdjustedBaseCost * Math.Pow(Config.CostMultiplier, level);
        }

        public void Dispose()
        {
            _currentLevel.Dispose();
        }
    }
}
