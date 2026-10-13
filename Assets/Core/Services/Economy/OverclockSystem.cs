using Core.Models.Economy;
using Core.Services.Security;
using UnityEngine;

namespace Core.Services.Economy
{
    /// <summary>Ce qu'il est advenu d'une demande d'Overclock.</summary>
    public enum OverclockOutcome
    {
        /// <summary>Les cycles en cours ont avancé, et la pénalité a été infligée.</summary>
        Applied,

        /// <summary>Aucun cycle ne tournait : rien n'a avancé, et rien n'a été payé.</summary>
        NoActiveCycle,

        /// <summary>Trop tôt après le précédent : ignoré, sans effet ni coût.</summary>
        Throttled
    }

    /// <summary>
    /// Résultat d'un Overclock. Struct readonly : la vue en reçoit un par appui, et un type
    /// référence allouerait à chaque fois.
    /// </summary>
    public readonly struct OverclockResult
    {
        public readonly OverclockOutcome Outcome;

        /// <summary>Secondes d'avance accordées à chaque cycle en cours.</summary>
        public readonly float SecondsGained;

        /// <summary>UT ajoutées à la jauge par la pénalité.</summary>
        public readonly float TracePenalty;

        public OverclockResult(OverclockOutcome outcome, float secondsGained, float tracePenalty)
        {
            Outcome = outcome;
            SecondsGained = secondsGained;
            TracePenalty = tracePenalty;
        }
    }

    /// <summary>
    /// L'Overclock — la barre d'espace, ou le bouton à l'écran.
    ///
    /// <b>Refonte « Time Attack » du 2026-10-13 : c'est un pari, plus un clic gratuit.</b> Chaque
    /// appui avance tous les cycles en cours de <c>0,5 s + branche Overclock</c>, et coûte
    /// <c>V_AMI × 5 s</c> de jauge — toujours les mêmes cinq secondes de survie, quels que soient
    /// les TFlops et le Blindage. Sans arbre, l'échange est perdant d'un facteur dix : c'est voulu,
    /// l'Overclock est un geste de désespoir en début de partie (gratter les Datas qui manquent
    /// avant la saisie) et ne devient une vraie mécanique qu'une fois la branche investie.
    ///
    /// Il ne démarre plus rien : le « réveil » des Scripts à l'arrêt a disparu avec son nœud.
    /// </summary>
    public class OverclockSystem
    {
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly PrestigeManager _prestigeManager;
        private readonly ThreatManager _threatManager;
        private readonly BalancingConfigSO _balancing;

        /// <summary>
        /// Instant réel du dernier Overclock pris en compte. Temps RÉEL et non temps de
        /// simulation : le garde-fou protège contre une main qui martèle, pas contre la partie.
        /// </summary>
        private double _lastAppliedAt = double.NegativeInfinity;

        public OverclockSystem(
            ScriptCycleRunner cycleRunner,
            PrestigeManager prestigeManager,
            ThreatManager threatManager,
            BalancingConfigSO balancing)
        {
            _cycleRunner = cycleRunner;
            _prestigeManager = prestigeManager;
            _threatManager = threatManager;
            _balancing = balancing;
        }

        /// <summary>Secondes d'avance qu'un appui accorderait en l'état. Exposé pour que l'UI dise la vérité.</summary>
        public float CurrentAdvanceSeconds =>
            _balancing.OverclockWarpSeconds + _prestigeManager.TotalOverclockBonus.CurrentValue;

        /// <summary>
        /// Déclenche l'Overclock.
        ///
        /// L'avance passe AVANT la pénalité : un appui qui déclenche la saisie verse quand même
        /// les cycles qu'il vient d'achever. Le joueur a payé, il touche — puis il tombe.
        /// </summary>
        public OverclockResult TriggerManualOverclock()
        {
            // Un appui maintenu ne se répète pas (une pression = un appui), mais un double clic ou
            // une touche martelée peuvent arriver deux fois dans la même fraction de seconde. À
            // cinq secondes de survie l'appui, c'est un Game Over accidentel en puissance.
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - _lastAppliedAt < _balancing.OverclockMinIntervalSeconds)
            {
                return new OverclockResult(OverclockOutcome.Throttled, 0f, 0f);
            }

            float seconds = CurrentAdvanceSeconds;

            // Rien ne tourne : rien n'avance, et rien ne se paie. Le garde-fou n'est pas réarmé,
            // l'appui n'a rien coûté.
            if (_cycleRunner.AdvanceAllActive(seconds) == 0)
            {
                return new OverclockResult(OverclockOutcome.NoActiveCycle, 0f, 0f);
            }

            _lastAppliedAt = now;

            float penalty = _threatManager.ApplyOverclockPenalty();
            return new OverclockResult(OverclockOutcome.Applied, seconds, penalty);
        }
    }
}
