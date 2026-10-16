using Core.Models.Economy;
using Core.Services.Platform;
using Core.Services.Security;
using R3;
using System;
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

        /// <summary>UT ajoutées à la jauge par la pénalité. Zéro sur un coup critique.</summary>
        public readonly float TracePenalty;

        /// <summary>
        /// Coup critique (« Instabilité Zéro-Day », 2026-10-16) : avance multipliée, aucune
        /// pénalité. Toujours faux hors d'un Overclock appliqué.
        /// </summary>
        public readonly bool IsCritical;

        public OverclockResult(OverclockOutcome outcome, float secondsGained, float tracePenalty, bool isCritical)
        {
            Outcome = outcome;
            SecondsGained = secondsGained;
            TracePenalty = tracePenalty;
            IsCritical = isCritical;
        }
    }

    /// <summary>
    /// L'Overclock — le bouton à l'écran, ou la barre d'Espace une fois « Saisie Multiplexée »
    /// installée.
    ///
    /// <b>Refonte « Time Attack » du 2026-10-13 : c'est un pari, plus un clic gratuit.</b> Chaque
    /// appui avance tous les cycles en cours de <c>0,5 s + branche Overclock</c>, et coûte
    /// <c>V_AMI × 5 s</c> de jauge — toujours les mêmes cinq secondes de survie, quels que soient
    /// les TFlops et le Blindage. Sans arbre, l'échange est perdant d'un facteur dix : c'est voulu,
    /// l'Overclock est un geste de désespoir en début de partie (gratter les Datas qui manquent
    /// avant la saisie) et ne devient une vraie mécanique qu'une fois la branche investie.
    ///
    /// <b>Le coup critique</b> (`P_OVERCLOCK_CRIT`, 2026-10-16) : avec la chance que donne
    /// l'arbre, un appui multiplie son avance et ne coûte rien. La machine à sous du GD.
    ///
    /// Service du scope RACINE depuis le 2026-10-16, et non plus de la scène : toutes ses
    /// dépendances y vivent, le son du critique (<c>GameEventSounds</c>) et le journal de fin de
    /// run l'écoutent depuis le Root, et le harnais d'équilibrage doit pouvoir le composer sans
    /// scène.
    /// </summary>
    public sealed class OverclockSystem : IDisposable
    {
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly PrestigeManager _prestigeManager;
        private readonly ThreatManager _threatManager;
        private readonly BalancingConfigSO _balancing;
        private readonly IRandomSource _random;

        private readonly Subject<OverclockResult> _onApplied = new();

        /// <summary>
        /// Instant réel du dernier Overclock pris en compte. Temps RÉEL et non temps de
        /// simulation : le garde-fou protège contre une main qui martèle, pas contre la partie.
        /// Un seul instant pour le clic ET l'Espace : le délai est partagé entre les deux gestes.
        /// </summary>
        private double _lastAppliedAt = double.NegativeInfinity;

        public OverclockSystem(
            ScriptCycleRunner cycleRunner,
            PrestigeManager prestigeManager,
            ThreatManager threatManager,
            BalancingConfigSO balancing,
            IRandomSource random)
        {
            _cycleRunner = cycleRunner;
            _prestigeManager = prestigeManager;
            _threatManager = threatManager;
            _balancing = balancing;
            _random = random;
        }

        /// <summary>
        /// Émis à chaque Overclock APPLIQUÉ, critique ou non — jamais pour un appui ignoré ou à
        /// vide. Écouté par le son du critique et par le journal de fin de run.
        /// </summary>
        public Observable<OverclockResult> OnApplied => _onApplied;

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
                return new OverclockResult(OverclockOutcome.Throttled, 0f, 0f, false);
            }

            // Tiré AVANT l'avance, qu'il multiplie. Sans le nœud, aucun tirage : la source de
            // hasard n'est pas consommée, et le harnais rejoue la même campagne.
            float critChance = _prestigeManager.TotalOverclockCritChance.CurrentValue;
            bool isCritical = critChance > 0f && _random.NextFloat01() < critChance;

            float seconds = CurrentAdvanceSeconds;
            if (isCritical) seconds *= _balancing.OverclockCriticalAdvanceMultiplier;

            // Rien ne tourne : rien n'avance, et rien ne se paie. Le garde-fou n'est pas réarmé,
            // l'appui n'a rien coûté.
            if (_cycleRunner.AdvanceAllActive(seconds) == 0)
            {
                return new OverclockResult(OverclockOutcome.NoActiveCycle, 0f, 0f, false);
            }

            _lastAppliedAt = now;

            float penalty = isCritical ? 0f : _threatManager.ApplyOverclockPenalty();

            var result = new OverclockResult(OverclockOutcome.Applied, seconds, penalty, isCritical);
            _onApplied.OnNext(result);
            return result;
        }

        public void Dispose()
        {
            _onApplied.Dispose();
        }
    }
}
