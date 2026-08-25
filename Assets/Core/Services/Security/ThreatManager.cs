using R3;
using System;
using UnityEngine;

namespace Core.Services.Security
{
    public class ThreatManager : IDisposable
    {
        // La jauge normalisée de 0 (Safe) à 1 (Corruption Critique / Lockdown)
        public ReactiveProperty<float> NormalizedThreat { get; }

        // Événement déclenché quand la jauge atteint 100%
        public Subject<Unit> OnCriticalLockdown { get; }

        public ThreatManager()
        {
            NormalizedThreat = new ReactiveProperty<float>(0f);
            OnCriticalLockdown = new Subject<Unit>();
        }

        public void AddThreat(float amount)
        {
            // Si on est déjà en lockdown, on ignore
            if (NormalizedThreat.CurrentValue >= 1f) return;

            float newThreat = Mathf.Clamp01(NormalizedThreat.CurrentValue + amount);
            NormalizedThreat.Value = newThreat;

            if (newThreat >= 1f)
            {
                TriggerLockdown();
            }
        }

        public void ReduceThreat(float amount)
        {
            NormalizedThreat.Value = Mathf.Clamp01(NormalizedThreat.CurrentValue - amount);
        }

        /// <summary>
        /// Repositionne la jauge sans déclencher de lockdown : réservé au chargement d'une
        /// sauvegarde. Le déclenchement du Game Over appartient à AddThreat, c'est-à-dire au jeu
        /// en cours — pas à la restauration, où l'UI n'est pas encore abonnée.
        /// Le GameStateGateway plafonne déjà la valeur restaurée sous 1.
        /// </summary>
        public void RestoreThreat(float normalizedValue)
        {
            NormalizedThreat.Value = Mathf.Clamp01(normalizedValue);
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
        }
    }
}
