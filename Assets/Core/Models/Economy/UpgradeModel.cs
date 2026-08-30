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
        /// Réglages d'équilibrage. Passés par constructeur et non injectés : ce modèle est un POCO
        /// créé à la main par l'UpgradeManager, il ne passe pas par le conteneur.
        /// </summary>
        private readonly BalancingConfigSO _balancing;

        public UpgradeConfigSO Config { get; }

        /// <summary>
        /// Niveau courant, et <b>source de vérité de tous les calculs</b>. Le ReactiveProperty
        /// ci-dessous ne sert qu'à notifier.
        ///
        /// La distinction n'est pas cosmétique : `_currentLevel.Value++` réveille ses abonnés
        /// IMMÉDIATEMENT, avant que RecalculateCache() n'ait tourné. Tant que les calculs lisaient
        /// le ReactiveProperty, la vue était donc rafraîchie avec les caches du niveau PRÉCÉDENT :
        /// après le tout premier achat, le panneau affichait « Génère 0 Datas », la valeur du
        /// niveau 0. Constaté en jeu le 2026-08-27.
        /// </summary>
        private int _level;

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


        public UpgradeModel(UpgradeConfigSO config, BalancingConfigSO balancing, int savedLevel = 0)
        {
            Config = config;
            _balancing = balancing;
            _level = savedLevel < 0 ? 0 : savedLevel;
            _currentLevel = new ReactiveProperty<int>(_level);

            RecalculateCache();
        }

        public bool IsOwned => _level > 0;

        /// <summary>
        /// Niveau à partir duquel le générateur relance ses cycles seul.
        /// Jamais sous 1 : un nœud de prestige ne doit pas pouvoir automatiser un générateur
        /// que le joueur ne possède pas encore.
        /// </summary>
        public int AutomationThreshold
        {
            get
            {
                // Arrondi et non troncature : un bonus de 1 par rang stocké en float peut valoir
                // 0,99999994, et (int)(0,99999994 × 3) rendrait 2 niveaux au lieu de 3. Même
                // piège que celui déjà rencontré sur les charges du Ghost Cache.
                int reduction = UnityEngine.Mathf.RoundToInt(_bonuses.AutomationThresholdReduction);
                if (reduction < 0) reduction = 0;

                return Math.Max(1, Config.AutomationLevel - reduction);
            }
        }

        public bool IsAutomated => _level >= AutomationThreshold;

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
                && bonuses.TimeReduction == _bonuses.TimeReduction
                && bonuses.AutomationThresholdReduction == _bonuses.AutomationThresholdReduction)
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
            return AdjustedBaseCost * Math.Pow(finalMultiplier, _level);
        }

        /// <summary>Coût de base après réduction ciblée. Bornée pour qu'un générateur ne soit jamais gratuit.</summary>
        private double AdjustedBaseCost =>
            Config.BaseCost * (1d - Math.Min(_balancing.MaxTargetedReduction, _bonuses.CostReduction));

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
            _level++;

            // Recalcul AVANT notification, et l'ordre est tout l'enjeu : la vue se rafraîchit sur
            // ce signal et lit les caches dans la foulée. Notifier d'abord lui donnait les valeurs
            // du niveau précédent — rendement, coût et durée affichés avec un cran de retard.
            RecalculateCache();

            _currentLevel.Value = _level;
        }

        private void RecalculateCache()
        {
            int level = _level;

            // Les bonus ciblés s'appliquent aux valeurs de BASE — c'est le sens littéral des
            // trois types de nœuds (« réduit le coût de base », « augmente le rendement de
            // base », « réduit le temps de cycle »). Les paliers puis les TFlops s'appliquent
            // ensuite par-dessus, et le plancher tranche en dernier.
            double yield = Config.BaseProductionYield * (1d + _bonuses.YieldBoost) * level;
            float duration = Config.BaseCycleDuration * (1f - Math.Min(_balancing.MaxTargetedReduction, _bonuses.TimeReduction));

            // La magnitude de Trace ne suit PAS la même logique que le rendement, et c'est le
            // cœur de l'équilibrage arrêté le 2026-08-30.
            //
            // Tant qu'elle valait base × niveau, monter un générateur accélérait les gains ET la
            // mort dans la même proportion : une run rapportait 133 Datas quoi que fasse le
            // joueur, et le premier palier utile coûtait 138. Bien jouer ne changeait rien.
            //
            // Pour un générateur, la trace est désormais un COÛT DE SURFACE : on la paie à
            // l'acquisition et elle ne bouge plus. Approfondir un outil devient silencieux,
            // élargir l'arsenal est ce qui attire l'attention fédérale.
            //
            // Le Proxy fait exception, et il le DOIT : sa magnitude n'est pas une génération
            // mais une DISSIPATION. La figer rendrait un Proxy de niveau 50 aussi efficace qu'un
            // de niveau 1, et le monter deviendrait toujours un mauvais achat. Lui seul voit
            // aussi le « boost de rendement » du prestige amplifier cette magnitude — l'appliquer
            // à un Script augmenterait sa trace, soit l'exact inverse d'un bonus.
            float traceMagnitude;

            if (Config.Type == UpgradeType.Proxy)
            {
                traceMagnitude = (float)(Config.BaseTraceGeneratedPerSecond * level)
                               * (1f + _bonuses.YieldBoost);
            }
            else
            {
                // La garde sur le niveau remplace la multiplication : sans elle, un générateur
                // jamais acheté exposerait quand même le joueur.
                traceMagnitude = level > 0 ? (float)Config.BaseTraceGeneratedPerSecond : 0f;
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
            double compressed = duration / (1d + _tflops * _balancing.TFlopsTimeCompression);

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
