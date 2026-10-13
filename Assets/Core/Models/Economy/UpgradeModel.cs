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
        /// Plancher absolu du multiplicateur de coût. Sous 1,01, la courbe de coût s'aplatit et
        /// l'économie n'a plus de frein — aucune réduction de prestige ne peut le franchir.
        /// Il garantit aussi un dénominateur non nul aux sommes géométriques de l'achat multiple.
        /// </summary>
        private const double MinCostMultiplier = 1.01d;

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
        private double _cachedCost;
        private float _cachedCycleDuration;

        /// <summary>
        /// Multiplicateur de coût EFFECTIF (réduction globale de prestige déjà retranchée) et
        /// son logarithme naturel. Tous deux mis en cache pour la même raison que le reste :
        /// l'achat multiple les lit à chaque variation du solde, sur les quarante-cinq
        /// générateurs affichés. Le log ne sert qu'à <see cref="GetAffordableLevels"/>.
        /// </summary>
        private double _cachedCostMultiplier;
        private double _cachedLogCostMultiplier;

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
        /// Réduction GLOBALE du multiplicateur de coût, apportée par le prestige. Poussée par
        /// l'UpgradeManager comme les TFlops, et pour la même raison : elle ne dépend pas de CE
        /// générateur.
        ///
        /// Elle vit ici plutôt que d'être passée en argument à GetCurrentCost(), comme c'était
        /// le cas jusqu'au 2026-08-30. L'argument créait deux vérités : l'UpgradeManager le
        /// passait à l'achat, la vue ne le passait pas à l'affichage — le joueur lisait donc un
        /// prix plus élevé que celui réellement débité, et son bouton restait gris alors que
        /// l'achat serait passé. Dans le cache, il n'y a plus qu'un seul prix.
        /// </summary>
        private float _globalCostMultiplierReduction;

        /// <summary>
        /// Niveaux retirés au palier d'automatisation par la méta-progression GLOBALE,
        /// en plus du bonus ciblé. Un seul nœud d'arbre les donne à tous les Scripts.
        /// </summary>
        private float _globalAutomationReduction;


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
                int reduction = UnityEngine.Mathf.RoundToInt(
                    _bonuses.AutomationThresholdReduction + _globalAutomationReduction);
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
        /// Abaissement GLOBAL du palier d'automatisation. Ne touche pas au cache : le palier
        /// se recalcule à la lecture, il n'entre dans aucune formule mise en cache.
        /// </summary>
        public void SetGlobalAutomationReduction(float reduction)
        {
            _globalAutomationReduction = reduction < 0f ? 0f : reduction;
        }

        /// <summary>
        /// Met à jour la réduction GLOBALE du multiplicateur de coût. Même garde d'égalité que
        /// SetTFlops : la valeur est poussée à tous les modèles à chaque recalcul des bonus,
        /// alors qu'elle ne bouge qu'à l'achat d'un nœud de prestige.
        /// </summary>
        public void SetGlobalCostMultiplierReduction(float reduction)
        {
            float safe = reduction < 0f ? 0f : reduction;
            if (safe == _globalCostMultiplierReduction) return;

            _globalCostMultiplierReduction = safe;
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
        /// un nœud CIBLÉ rabote le coût de BASE, un nœud GLOBAL rabote le MULTIPLICATEUR M, ce
        /// qui aplatit la courbe entière. Les deux sont désormais dans le cache — voir
        /// <see cref="_globalCostMultiplierReduction"/> pour la raison.
        /// </summary>
        public double GetCurrentCost() => _cachedCost;

        /// <summary>
        /// Coût cumulé des <paramref name="levels"/> prochains niveaux, réductions comprises :
        /// Σ C₀·M^k pour k de 0 à N−1, soit C₀·(M^N − 1)/(M − 1).
        ///
        /// Forme fermée et non une boucle d'achats simulés : un MAX de fin de partie porte
        /// plusieurs centaines de niveaux, et chaque itération coûterait un Math.Pow. La division
        /// est sûre en toute circonstance — M est borné à 1,01 par le bas, donc M−1 ≥ 0,01.
        /// </summary>
        public double GetCumulativeCost(int levels)
        {
            if (levels <= 0) return 0d;
            if (levels == 1) return _cachedCost;

            double multiplier = _cachedCostMultiplier;
            return _cachedCost * (Math.Pow(multiplier, levels) - 1d) / (multiplier - 1d);
        }

        /// <summary>
        /// Nombre de niveaux que <paramref name="budget"/> permet d'acheter D'AFFILÉE, en tenant
        /// compte du renchérissement à chaque cran. C'est le moteur du mode MAX.
        ///
        /// Inversion de la somme géométrique ci-dessus :
        ///   budget ≥ C₀·(M^N − 1)/(M − 1)  ⟺  N ≤ ln(1 + budget·(M − 1)/C₀) / ln(M)
        ///
        /// Un log et une division, là où une boucle d'essais coûterait un Math.Pow par niveau.
        /// </summary>
        /// <param name="hardCap">
        /// Garde-fou de l'appelant. Il ne borne pas le jeu — la courbe exponentielle s'en charge
        /// bien avant — mais un générateur au coût de base nul rendrait sinon une infinité.
        /// </param>
        public int GetAffordableLevels(double budget, int hardCap)
        {
            if (hardCap <= 0) return 0;

            // Cas de loin le plus fréquent, et le seul parcouru par les quarante-cinq
            // générateurs affichés tant que le joueur n'a pas les moyens : on sort avant le log.
            if (budget < _cachedCost) return 0;

            // Générateur gratuit (coût de base nul, ou raboté à zéro) : aucun budget ne le borne,
            // seul le garde-fou le fait. Sans cette sortie, la division ci-dessous rendrait ∞.
            if (_cachedCost <= 0d) return hardCap;

            double multiplier = _cachedCostMultiplier;
            double raw = Math.Log(1d + budget * (multiplier - 1d) / _cachedCost) / _cachedLogCostMultiplier;

            if (double.IsNaN(raw)) return 1; // On a déjà établi que le premier niveau passe.

            int levels = raw >= hardCap ? hardCap : (int)raw;
            if (levels < 1) levels = 1;

            // Le logarithme est juste au bruit de virgule flottante près, et ce bruit tombe des
            // deux côtés du seuil. On corrige d'un cran contre la somme exacte plutôt que
            // d'annoncer un lot qui serait refusé à la caisse — ou de laisser un niveau payable
            // sur la table, ce qui ferait mentir le mot « MAX ». Zéro ou une itération en
            // pratique, et bornée par hardCap dans le pire cas.
            while (levels > 1 && GetCumulativeCost(levels) > budget) levels--;
            while (levels < hardCap && GetCumulativeCost(levels + 1) <= budget) levels++;

            return levels;
        }

        /// <summary>Coût de base après réduction ciblée. Bornée pour qu'un générateur ne soit jamais gratuit.</summary>
        private double AdjustedBaseCost =>
            Config.BaseCost * (1d - Math.Min(_balancing.MaxTargetedReduction, _bonuses.CostReduction));

        /// <summary>Montant versé à la fin d'un cycle, paliers inclus.</summary>
        public double GetCurrentYield() => _cachedYield;

        /// <summary>Durée d'un cycle, paliers inclus, plancher appliqué.</summary>
        public float GetCurrentCycleDuration() => _cachedCycleDuration;

        /// <summary>
        /// Secondes de survie que rendraient les <paramref name="levels"/> PROCHAINS niveaux de
        /// ce Proxy, avant la branche Interception. Vaut 0 hors Proxy.
        ///
        /// Chaque niveau compte avec les paliers de SON rang : acheter du niveau 8 au niveau 12
        /// paie deux niveaux nus et trois au palier ×1,32 du niveau 10. Un lot ×100 qui franchit
        /// trois paliers en tient donc compte au cran près, plutôt que d'appliquer à tout le lot
        /// le palier de son dernier niveau.
        ///
        /// Pas de cache : la valeur ne sert qu'à l'achat et à l'affichage du générateur, jamais
        /// dans une boucle de frame. Le parcours suit les paliers par tranches, sans itérer
        /// niveau par niveau — un MAX peut porter des milliers de niveaux.
        /// </summary>
        public double GetWipeSecondsForNextLevels(int levels)
        {
            if (levels <= 0 || Config.WipeSeconds <= 0d) return 0d;

            int cursor = _level + 1;
            int last = _level + levels;

            var milestones = Config.Milestones;
            double factor = 1d;
            int next = 0;

            // Paliers déjà acquis au premier niveau du lot. Le générateur trie les paliers par
            // niveau : on avance simplement dans la liste.
            while (next < milestones.Count && milestones[next].Level <= cursor)
            {
                if (milestones[next].Effect == MilestoneEffect.WipeMultiplier) factor *= milestones[next].Factor;
                next++;
            }

            double weightedLevels = 0d;

            while (cursor <= last)
            {
                // Tranche à facteur constant : jusqu'à la veille du prochain palier, ou la fin du lot.
                int segmentEnd = last;
                if (next < milestones.Count && milestones[next].Level - 1 < segmentEnd)
                {
                    segmentEnd = milestones[next].Level - 1;
                }

                weightedLevels += factor * (segmentEnd - cursor + 1);
                cursor = segmentEnd + 1;

                while (next < milestones.Count && milestones[next].Level <= cursor)
                {
                    if (milestones[next].Effect == MilestoneEffect.WipeMultiplier) factor *= milestones[next].Factor;
                    next++;
                }
            }

            return Config.WipeSeconds * weightedLevels;
        }

        /// <summary>Rendement théorique par seconde si le cycle tourne en continu. Sert à l'affichage.</summary>
        public double GetYieldPerSecond()
        {
            if (_cachedCycleDuration <= 0f) return 0d;
            return _cachedYield / _cachedCycleDuration;
        }

        public void LevelUp() => AddLevels(1);

        /// <summary>
        /// Monte de plusieurs niveaux d'un coup. UN seul recalcul de cache et UNE seule
        /// notification : appeler LevelUp() en boucle réveillerait la vue N fois, dont N−1 sur
        /// des états intermédiaires que le joueur ne verra jamais — et sur un MAX à trois cents
        /// niveaux, ce sont trois cents reconstructions de chaînes formatées pour rien.
        /// </summary>
        public void AddLevels(int levels)
        {
            if (levels <= 0) return;

            _level += levels;

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
            // « Puissance » du générateur : niveau × paliers de rendement, rapportée au niveau 1.
            double power = level;

            float duration = Config.BaseCycleDuration * (1f - Math.Min(_balancing.MaxTargetedReduction, _bonuses.TimeReduction));

            // Paliers : effets multiplicatifs, cumulatifs, et définitifs une fois atteints. Les
            // paliers de Wipe des Proxies n'entrent dans aucun cache : ils se lisent niveau par
            // niveau, à l'achat (GetWipeSecondsForNextLevels).
            var milestones = Config.Milestones;
            for (int i = 0; i < milestones.Count; i++)
            {
                UpgradeMilestone milestone = milestones[i];
                if (level < milestone.Level) continue;

                switch (milestone.Effect)
                {
                    case MilestoneEffect.YieldMultiplier:
                        power *= milestone.Factor;
                        break;

                    case MilestoneEffect.DurationMultiplier:
                        duration *= milestone.Factor;
                        break;
                }
            }

            double yield = Config.BaseProductionYield * (1d + _bonuses.YieldBoost) * power;

            // Second effet des TFlops sur les Scripts, ajouté le 2026-08-31 : ils multiplient le
            // RENDEMENT, et pas seulement la vitesse. Sans lui le Hardware était un pilier mort —
            // la compression butait sur MinCycleDuration entre 4 et 180 TFlops selon le Script,
            // quand HW_01 niveau 10 en fournit déjà 40.
            //
            // Réservé aux Scripts, et la restriction est vitale : chez un Hardware le rendement
            // EST la capacité en TFlops, donc le brancher là créerait une boucle de rétroaction
            // divergente — plus de TFlops, donc plus de TFlops.
            double tflopsYieldFactor = Config.Type == UpgradeType.Script
                ? Math.Pow(1d + _tflops, _balancing.TFlopsYieldExponent)
                : 1d;

            yield *= tflopsYieldFactor;

            // Compression par les TFlops. L'exposant est ce qui a remplacé le mur du 2026-08-30 :
            // à 1 on retrouve la formule linéaire d'origine, qui saturait contre MinCycleDuration
            // et tuait le pilier Hardware ; sous 1 elle s'étale sur toute la partie sans jamais
            // se borner. Calcul en double : à 1,5e9 TFlops, un float perdrait la précision.
            double compressed = duration
                / Math.Pow(1d + _tflops * _balancing.TFlopsTimeCompression,
                           _balancing.TFlopsCompressionExponent);

            // Le plancher est désormais GLOBAL et non plus par générateur. MinCycleDuration n'est
            // plus lu : c'était lui le mur qui rendait le Hardware inutile passé quelques dizaines
            // de TFlops. Ce qui reste n'est qu'un garde-fou de boucle pour le ScriptCycleRunner.
            float finalDuration = Math.Max(_balancing.AbsoluteMinCycleDuration, (float)compressed);

            // Multiplicateur de coût effectif : la réduction GLOBALE de prestige aplatit la
            // courbe entière, mais jamais en dessous de 1,01 — sous ce seuil le coût cesse de
            // croître et l'économie n'a plus de frein. C'est aussi ce plancher qui garantit un
            // dénominateur non nul aux sommes géométriques de GetCumulativeCost.
            double costMultiplier = Math.Max(
                MinCostMultiplier,
                Config.CostMultiplier - _globalCostMultiplierReduction);

            _cachedYield = yield;
            _cachedCycleDuration = finalDuration;
            _cachedCostMultiplier = costMultiplier;
            _cachedLogCostMultiplier = Math.Log(costMultiplier);
            _cachedCost = AdjustedBaseCost * Math.Pow(costMultiplier, level);
        }

        public void Dispose()
        {
            _currentLevel.Dispose();
        }
    }
}
