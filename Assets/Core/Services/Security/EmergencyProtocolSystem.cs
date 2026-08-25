using Core.Models.Economy;
using R3;
using System;

namespace Core.Services.Security
{
    public class EmergencyProtocolSystem : IDisposable
    {
        private readonly UserCurrencies _currencies;
        private readonly ThreatManager _threatManager;

        // Configuration de base
        private const double BaseCost = 500d;
        private const double CostMultiplier = 3.0d; // Le prix fait x3 à chaque clic
        private const float TraceReductionAmount = 0.20f; // -20% de trace

        private readonly ReactiveProperty<int> _timesUsedInCurrentRun;

        // Le coût actuel exposé pour l'UI
        public ReadOnlyReactiveProperty<double> CurrentCost { get; }

        /// <summary>
        /// Nombre d'utilisations sur la run en cours. Fait partie de l'état sauvegardé : sans lui,
        /// fermer et rouvrir le jeu remettrait le coût du bouton à son prix plancher.
        /// </summary>
        public int UsesInCurrentRun => _timesUsedInCurrentRun.CurrentValue;

        public EmergencyProtocolSystem(UserCurrencies currencies, ThreatManager threatManager)
        {
            _currencies = currencies;
            _threatManager = threatManager;

            _timesUsedInCurrentRun = new ReactiveProperty<int>(0);

            // On recalcule le coût dynamiquement via R3 dès que le nombre d'utilisations change
            CurrentCost = _timesUsedInCurrentRun
                .Select(uses => BaseCost * Math.Pow(CostMultiplier, uses))
                .ToReadOnlyReactiveProperty(BaseCost);
        }

        public bool TryTriggerEmergency()
        {
            double cost = CurrentCost.CurrentValue;

            if (_currencies.Money.TryRemove(cost))
            {
                _threatManager.ReduceThreat(TraceReductionAmount);
                _timesUsedInCurrentRun.Value++;
                return true;
            }

            return false;
        }

        /// <summary>Restaure le compteur depuis une sauvegarde. Recalcule le coût par effet de bord.</summary>
        public void RestoreUses(int uses)
        {
            _timesUsedInCurrentRun.Value = uses < 0 ? 0 : uses;
        }

        // Appelé par le GameSessionManager quand on redémarre une partie
        public void ResetSystem()
        {
            _timesUsedInCurrentRun.Value = 0;
        }

        public void Dispose()
        {
            CurrentCost.Dispose();
            _timesUsedInCurrentRun.Dispose();
        }
    }
}
