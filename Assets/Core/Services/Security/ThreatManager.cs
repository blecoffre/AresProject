using Core.Models.Economy;
using R3;
using System;
using UnityEngine;

namespace Core.Services.Security
{
    /// <summary>
    /// La jauge de Trace.
    ///
    /// <b>Elle tient une valeur ABSOLUE depuis le 2026-08-30, plus une fraction.</b> Le plafond
    /// était figé à 100 points face à une économie qui s'étale sur sept ordres de grandeur : quel
    /// que soit le barème de trace choisi, la fenêtre de survie s'effondrait sous la seconde vers
    /// le dixième palier. Le plafond suit désormais le parc Hardware — c'est lui le rempart,
    /// quand les Proxies achètent du débit.
    ///
    /// <see cref="NormalizedThreat"/> survit en tant que valeur DÉRIVÉE, ce qui laisse intacts
    /// l'affichage, le bilan de fin de run et la sauvegarde du côté des lecteurs.
    /// </summary>
    public class ThreatManager : IDisposable
    {
        private readonly float _baseCap;

        /// <summary>Trace accumulée, en points absolus.</summary>
        public ReactiveProperty<float> CurrentTrace { get; }

        /// <summary>La jauge de 0 (Safe) à 1 (Corruption Critique). Dérivée de la trace et du plafond.</summary>
        public ReactiveProperty<float> NormalizedThreat { get; }

        /// <summary>Plafond courant, en points absolus. Jamais sous le plafond de base.</summary>
        public float TraceCap { get; private set; }

        // Événement déclenché quand la jauge atteint 100%
        public Subject<Unit> OnCriticalLockdown { get; }

        public ThreatManager(BalancingConfigSO balancing)
        {
            _baseCap = Mathf.Max(1f, balancing.BaseTraceCap);
            TraceCap = _baseCap;

            CurrentTrace = new ReactiveProperty<float>(0f);
            NormalizedThreat = new ReactiveProperty<float>(0f);
            OnCriticalLockdown = new Subject<Unit>();
        }

        /// <summary>
        /// Déclare la capacité apportée par le Hardware. Poussée plutôt qu'observée : ce
        /// gestionnaire ne dépend d'aucun autre service, et c'est ce qui le garde testable.
        /// </summary>
        public void SetCapacityBonus(float bonus)
        {
            float cap = _baseCap + Mathf.Max(0f, bonus);
            if (Mathf.Approximately(cap, TraceCap)) return;

            TraceCap = cap;

            // Relever le plafond ne SOIGNE pas : la trace absolue ne bouge pas, seule la fraction
            // affichée descend. C'est exactement l'analogie des points de vie maximum.
            RefreshNormalized();
        }

        /// <summary>Ajoute des points de trace ABSOLUS.</summary>
        public void AddThreat(float amount)
        {
            // Si on est déjà en lockdown, on ignore
            if (CurrentTrace.CurrentValue >= TraceCap) return;

            CurrentTrace.Value = Mathf.Clamp(CurrentTrace.CurrentValue + amount, 0f, TraceCap);
            RefreshNormalized();

            if (CurrentTrace.CurrentValue >= TraceCap)
            {
                TriggerLockdown();
            }
        }

        /// <summary>
        /// Efface une FRACTION de la trace accumulée. Proportionnel et non absolu : le Data Wiper
        /// retirait 20 points de jauge, ce qui ne serait plus qu'un grain de sable devant un
        /// plafond de deux millions au dernier palier.
        /// </summary>
        public void ReduceTraceByFraction(float fraction)
        {
            float safe = Mathf.Clamp01(fraction);
            if (safe <= 0f) return;

            CurrentTrace.Value = CurrentTrace.CurrentValue * (1f - safe);
            RefreshNormalized();
        }

        /// <summary>Vide la jauge. Réservé au wipe de fin de run.</summary>
        public void ResetTrace()
        {
            CurrentTrace.Value = 0f;
            RefreshNormalized();
        }

        /// <summary>
        /// Repositionne la jauge sans déclencher de lockdown : réservé au chargement d'une
        /// sauvegarde. Le déclenchement du Game Over appartient à AddThreat, c'est-à-dire au jeu
        /// en cours — pas à la restauration, où l'UI n'est pas encore abonnée.
        /// Le GameStateGateway plafonne déjà la valeur restaurée sous le maximum.
        /// </summary>
        public void RestoreTrace(float absoluteTrace)
        {
            CurrentTrace.Value = Mathf.Clamp(absoluteTrace, 0f, TraceCap);
            RefreshNormalized();
        }

        private void RefreshNormalized()
        {
            NormalizedThreat.Value = Mathf.Clamp01(CurrentTrace.CurrentValue / TraceCap);
        }

        private void TriggerLockdown()
        {
            Debug.LogWarning("[THREAT] CORRUPTION CRITIQUE. Arrêt des scripts.");
            OnCriticalLockdown.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            OnCriticalLockdown.Dispose();
            NormalizedThreat.Dispose();
            CurrentTrace.Dispose();
        }
    }
}
