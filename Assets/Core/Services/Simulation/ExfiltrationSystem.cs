using Core.Models.Economy;
using Core.Models.Simulation;
using R3;
using System;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Le « Protocole Terre Brûlée » : la sortie volontaire de run.
    ///
    /// Jusqu'ici, seule la Trace à 100 % menait à l'écran de prestige — donc la seule façon de
    /// boucler la méta-progression était d'attendre de se faire prendre. Ce système donne au
    /// joueur la main sur la fin de sa run, avec un bonus à la clé pour l'inciter à repousser
    /// le moment plutôt qu'à sortir dès qu'il le peut.
    ///
    /// La condition de déblocage est volontairement UNIQUE : avoir de quoi gagner au moins un
    /// CPU Cycle. Pas de palier d'upgrade, pas de seuil de TFlops — verrouiller derrière un achat
    /// précis casserait la liberté systémique, alors qu'un joueur qui farme mal doit quand même
    /// pouvoir sortir s'il a farmé assez longtemps.
    /// </summary>
    public class ExfiltrationSystem : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// En éditeur et en build de test, le bouton reste CLIQUABLE même à zéro cycle, pour
        /// enchaîner des runs vides sans avoir à farmer. Jamais actif en production.
        ///
        /// Ne touche que l'interactivité, jamais l'état AFFICHÉ : sans cette séparation, l'éditeur
        /// montrerait en permanence « [PRÊT] Gain : +0 Cycles CPU » — un libellé absurde — et la
        /// jauge de progression, invisible une fois débloqué, ne serait jamais observable pendant
        /// le développement.
        /// </summary>
        private const bool BypassUnlockCondition = true;
#else
        private const bool BypassUnlockCondition = false;
#endif

        private readonly UserCurrencies _currencies;
        private readonly GameSessionManager _sessionManager;
        private readonly BalancingConfigSO _balancing;

        private readonly ReadOnlyReactiveProperty<double> _pendingCycles;
        private readonly ReadOnlyReactiveProperty<bool> _isUnlocked;
        private readonly ReadOnlyReactiveProperty<bool> _isClickable;
        private readonly ReadOnlyReactiveProperty<float> _progressToFirstCycle;

        /// <summary>
        /// CPU Cycles que rapporterait une sortie maintenant, <b>SANS</b> le bonus de palier.
        ///
        /// Sert de garde pour <see cref="IsUnlocked"/>, et rien d'autre : c'est délibérément la
        /// valeur BASSE, celle qui ne peut que sous-promettre. Adosser le déverrouillage au bonus
        /// le ferait osciller au rythme du relevé brouillé de la Trace, puisque le palier retenu
        /// dépend de ce que le joueur a CRU lire.
        ///
        /// Pour le chiffre annoncé au joueur, utiliser <see cref="PreviewCyclesAt"/>. Ce
        /// commentaire annonçait « bonus Clean Exit inclus », ce qui était faux — et c'est ce
        /// contresens qui a fait multiplier les points au lieu de la contribution dans la vue.
        ///
        /// `DistinctUntilChanged` est indispensable, pas cosmétique : l'argent de la run bouge à
        /// chaque cycle de Script, plusieurs fois par seconde, alors que ce nombre entier ne
        /// change qu'une poignée de fois sur toute une run. Sans lui, la vue se reconstruirait
        /// en boucle pour afficher la même valeur.
        /// </summary>
        public ReadOnlyReactiveProperty<double> PendingCycles => _pendingCycles;

        /// <summary>
        /// La condition de jeu est-elle remplie : au moins 1 CPU Cycle à gagner. Pilote ce que la
        /// vue AFFICHE — libellé et jauge — et dit toujours la vérité, y compris en éditeur.
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsUnlocked => _isUnlocked;

        /// <summary>
        /// Le bouton est-il actionnable. Identique à <see cref="IsUnlocked"/> en production, mais
        /// toujours vrai en éditeur et en build de test. Pilote la seule interactivité.
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsClickable => _isClickable;

        /// <summary>
        /// Avancement vers le premier CPU Cycle, de 0 à 1. Sert la jauge du bouton verrouillé et
        /// n'a de sens que dans cet état — au-delà, elle reste à 1.
        /// </summary>
        public ReadOnlyReactiveProperty<float> ProgressToFirstCycle => _progressToFirstCycle;

        public ExfiltrationSystem(
            UserCurrencies currencies,
            GameSessionManager sessionManager,
            BalancingConfigSO balancing)
        {
            _currencies = currencies;
            _sessionManager = sessionManager;
            _balancing = balancing;

            _pendingCycles = _currencies.RunMoneyGenerated
                .Select(money => _currencies.PreviewPointsForContribution(money))
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty();

            // Le bouton s'ouvre quand la run rapporte AU MOINS UN POINT — la condition que le GDD
            // énonce depuis le 2026-08-26, « avoir de quoi gagner au moins 1 CPU Cycle ».
            //
            // <b>Elle a été un seuil de Datas pendant un temps, et c'était un contournement.</b>
            // Les points se calculaient alors sur l'argent de la seule run, donc déblocage et gain
            // partageaient la même valeur : raréfier les points rendait mécaniquement la sortie
            // inatteignable. Un seuil séparé cassait ce couplage — au prix d'un mensonge, puisque
            // le bouton s'annonçait « [VERROUILLÉ] Compilation du 1er Cycle CPU » tout en
            // s'ouvrant sur un critère qui n'avait plus rien à voir avec ce cycle.
            //
            // Constaté en jeu le 2026-09-16 : une run de 7 min 38 pouvait s'exfiltrer en ne
            // rapportant ZÉRO point. Le joueur sortait sur la promesse d'un cycle qu'il n'obtenait
            // pas.
            //
            // Le couplage d'origine ne revient pas pour autant : depuis que les points se
            // décrochent sur un CUMUL DE CAMPAGNE, la condition porte sur ce que rapporte CETTE
            // run, cumul déjà acquis déduit. Une run tardive n'ouvre donc pas le bouton à la
            // première seconde — il lui faut d'abord franchir son propre palier.
            _isUnlocked = _pendingCycles
                .Select(points => points >= 1d)
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty();

            _isClickable = _isUnlocked
                .Select(unlocked => BypassUnlockCondition || unlocked)
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty();

            _progressToFirstCycle = _currencies.RunMoneyGenerated
                .Select(ResolveProgressToFirstCycle)
                .ToReadOnlyReactiveProperty();
        }

        /// <summary>
        /// Datas à générer sur la run pour décrocher le cycle SUIVANT. Sert le « Prochain à
        /// X Datas » du bouton, qui donne un objectif au joueur qui hésite à continuer.
        /// </summary>
        /// <summary>
        /// Datas à générer sur CETTE run pour décrocher son PREMIER point — donc pour ouvrir le
        /// bouton. Rend 0 quand le cumul de campagne suffit déjà à lui seul.
        ///
        /// Le palier du point n° <c>awarded + 1</c> vaut <c>premier × croissance^awarded</c> de
        /// cumul ; ce qui manque se prend sur la run en cours.
        /// </summary>
        public double GetFirstCycleRunThreshold()
        {
            double awarded = _currencies.CpuCyclesAwarded.CurrentValue;
            double bankedForFirst = _currencies.CumulativeForPoints(awarded + 1d);

            double missing = bankedForFirst - _currencies.CampaignDatasBanked.CurrentValue;
            return missing < 0d ? 0d : missing;
        }

        /// <summary>
        /// Avancement vers le premier point de la run, pour la jauge du bouton verrouillé. Le
        /// dénominateur n'est plus une constante : il dépend du cumul déjà acquis, donc il change
        /// d'une run à l'autre.
        /// </summary>
        private float ResolveProgressToFirstCycle(double runMoney)
        {
            double required = GetFirstCycleRunThreshold();
            if (required <= 0d) return 1f;

            return (float)Math.Min(1d, runMoney / required);
        }

        /// <summary>
        /// Points que rapporterait une sortie au multiplicateur donné — le nombre que la fin de
        /// run créditera réellement.
        ///
        /// ⚠️ Le multiplicateur porte sur la CONTRIBUTION, jamais sur les points, exactement comme
        /// dans <c>GameSessionManager.ResolveRunEnd</c>. Majorer des points entiers produit des
        /// demi-cycles et un chiffre que la fin de run ne tient pas : mesuré sur une sauvegarde
        /// réelle, une sortie au palier 75 % annonçait <b>6,5 cycles pour 6 crédités</b>.
        /// </summary>
        public double PreviewCyclesAt(double cleanExitMultiplier)
        {
            double money = _currencies.RunMoneyGenerated.CurrentValue;
            return _currencies.PreviewPointsForContribution(money * cleanExitMultiplier);
        }

        /// <summary>
        /// Datas qu'il reste à voler SUR CETTE RUN pour décrocher le point SUIVANT, une fois
        /// encaissés ceux qu'une sortie immédiate rapporterait.
        ///
        /// Le palier vise le CUMUL de campagne : on déduit donc ce qui est déjà acquis, sans quoi
        /// le bouton afficherait un objectif que le joueur a en réalité déjà à moitié atteint.
        ///
        /// ⚠️ Le résultat est ramené en Datas BRUTES de run, en divisant par le multiplicateur :
        /// le cumul reçoit la contribution majorée, alors que le compteur que le joueur regarde
        /// monter, lui, est brut. Sans cette division, l'objectif annoncé serait toujours trop
        /// haut pour un joueur qui sort sur un palier.
        /// </summary>
        public double GetNextCycleThreshold(double cleanExitMultiplier)
        {
            double awarded = _currencies.CpuCyclesAwarded.CurrentValue;
            double pointsAfterExit = awarded + PreviewCyclesAt(cleanExitMultiplier);

            double bankedForNext = _currencies.CumulativeForPoints(pointsAfterExit + 1d);

            double missing = bankedForNext - _currencies.CampaignDatasBanked.CurrentValue;
            if (missing < 0d) return 0d;

            double multiplier = cleanExitMultiplier < 1d ? 1d : cleanExitMultiplier;
            return missing / multiplier;
        }

        /// <summary>
        /// Premier temps : fige le résultat de la run — gain crédité, run effacée, partie
        /// désarmée — mais n'affiche pas encore l'écran de fin.
        ///
        /// Ce découpage existe pour la séquence console du protocole, qui dure ~2 s. La jouer
        /// avant de figer le résultat laisserait la Trace monter pendant ce temps : un joueur
        /// exfiltrant à 98 % pourrait se faire saisir au milieu de sa propre exfiltration.
        ///
        /// Retourne false si la run est finie ou la condition non remplie — la vue grise le
        /// bouton, mais elle n'est pas la garde.
        /// </summary>
        public bool TryBeginExfiltration(out RunSummary summary)
        {
            summary = default;
            if (!_isClickable.CurrentValue) return false;

            return _sessionManager.TryResolveVoluntaryExit(out summary);
        }

        /// <summary>
        /// Second temps, une fois la séquence terminée : déclenche l'écran de fin.
        /// À appeler impérativement après un <see cref="TryBeginExfiltration"/> réussi, sinon le
        /// joueur reste bloqué sur une partie désarmée sans écran de prestige.
        /// </summary>
        public void CompleteExfiltration(RunSummary summary)
        {
            _sessionManager.AnnounceRunEnded(summary);
        }

        public void Dispose()
        {
            _pendingCycles.Dispose();
            _isUnlocked.Dispose();
            _isClickable.Dispose();
            _progressToFirstCycle.Dispose();
        }
    }
}
