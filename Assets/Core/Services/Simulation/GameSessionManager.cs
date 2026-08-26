using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Security;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Supervise le cycle de vie d'une 'Run' et gère la mécanique de Prestige lors d'un Game Over (Lockdown).
    /// </summary>
    public class GameSessionManager : IStartable, IDisposable
    {
        private readonly UserCurrencies _userCurrencies;
        private readonly ThreatManager _threatManager;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;
        private readonly EmergencyProtocolSystem _emergencyProtocolSystem;

        public Subject<double> OnSessionEnded { get; }
        public ReactiveProperty<bool> IsGameActive { get; }

        private DisposableBag _disposables;

        public GameSessionManager(UserCurrencies userCurrencies, ThreatManager threatManager, UpgradeManager upgradeManager, PrestigeManager prestigeManager, EmergencyProtocolSystem emergencyProtocolSystem)
        {
            _userCurrencies = userCurrencies;
            _threatManager = threatManager;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _emergencyProtocolSystem = emergencyProtocolSystem;

            OnSessionEnded = new Subject<double>();
            IsGameActive = new ReactiveProperty<bool>(true);

            _disposables = new DisposableBag();
        }

        public void Start()
        {
            // Abonnement à l'événement de détection maximale
            _threatManager.OnCriticalLockdown
                .Subscribe(_ => HandleGameOver())
                .AddTo(ref _disposables);
        }

        private void HandleGameOver()
        {
            Debug.Log("[GameSessionManager] A.M.I. CORRUPTION DÉTECTÉE. Fin de session en cours...");
            IsGameActive.Value = false;

            _userCurrencies.RecordDetection();
            double pendingPrestige = _userCurrencies.CalculatePendingCpuCycles();

            if (pendingPrestige > 0d)
            {
                _userCurrencies.AddCpuCycles(pendingPrestige);
            }

            // 1. Réinitialisation des monnaies avec les bonus de Prestige !
            // Seul l'argent se remet à une valeur de départ. Les TFlops sont dérivées du parc
            // Hardware : elles retombent d'elles-mêmes quand les niveaux sont remis à zéro, et
            // StartingComputerPower est déjà intégré au total par l'UpgradeManager.
            _userCurrencies.Money.Reset(_prestigeManager.StartingMoney.CurrentValue);

            // 2. Remise à zéro de l'inflation du bouton d'urgence
            _emergencyProtocolSystem.ResetSystem();

            // 3. Réinitialisation de la trace
            _threatManager.ReduceThreat(1f);

            // NOUVEAU : On notifie l'UI et on envoie le montant gagné
            OnSessionEnded.OnNext(pendingPrestige);
        }

        public void ResetSession()
        {
            _threatManager.ReduceThreat(1f);
            _userCurrencies.Money.Reset(10d); // Valeur de départ
            _upgradeManager.InitializeFromSave(new System.Collections.Generic.Dictionary<string, int>());

            IsGameActive.Value = true;
        }

        public void Dispose()
        {
            OnSessionEnded.Dispose();
            _disposables.Dispose();
        }
    }
}
