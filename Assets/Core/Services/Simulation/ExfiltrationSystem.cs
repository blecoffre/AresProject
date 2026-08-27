using Core.Models.Economy;
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
        /// CPU Cycles que rapporterait une sortie maintenant, bonus Clean Exit inclus.
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
                .Select(_ => _currencies.CalculatePendingCpuCycles())
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty();

            _isUnlocked = _pendingCycles
                .Select(cycles => cycles >= 1d)
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty();

            _isClickable = _isUnlocked
                .Select(unlocked => BypassUnlockCondition || unlocked)
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty();

            _progressToFirstCycle = _currencies.RunMoneyGenerated
                .Select(money => (float)Math.Min(1d, money / balancing.MoneyPerCpuCycle))
                .ToReadOnlyReactiveProperty();
        }

        /// <summary>
        /// Datas à générer sur la run pour décrocher le cycle SUIVANT. Sert le « Prochain à
        /// X Datas » du bouton, qui donne un objectif au joueur qui hésite à continuer.
        /// </summary>
        public double GetNextCycleThreshold()
        {
            double next = _pendingCycles.CurrentValue + 1d;
            return next * next * _balancing.MoneyPerCpuCycle;
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
        public bool TryBeginExfiltration(out double awardedPrestige)
        {
            awardedPrestige = 0d;
            if (!_isClickable.CurrentValue) return false;

            return _sessionManager.TryResolveVoluntaryExit(out awardedPrestige);
        }

        /// <summary>
        /// Second temps, une fois la séquence terminée : déclenche l'écran de fin.
        /// À appeler impérativement après un <see cref="TryBeginExfiltration"/> réussi, sinon le
        /// joueur reste bloqué sur une partie désarmée sans écran de prestige.
        /// </summary>
        public void CompleteExfiltration(double awardedPrestige)
        {
            _sessionManager.AnnounceRunEnded(awardedPrestige);
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
