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
        /// <summary>
        /// Argent de départ d'une run neuve, AVANT le bonus de prestige — qui s'y ajoute au lieu
        /// de le remplacer. Sans ce plancher, un joueur sans nœud StartingMoney repartait à zéro
        /// et ne pouvait même pas acheter son premier Script.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        private const double BaseStartingMoney = 10d;

        /// <summary>Tampon réutilisé : un wipe ne doit pas allouer un dictionnaire à chaque fois.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, int> EmptyLevels =
            new System.Collections.Generic.Dictionary<string, int>();

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

            // La run est effacée ICI, en entier, et AVANT la notification.
            //
            // Le SaveScheduler écrit sur OnSessionEnded. Tant que les niveaux d'upgrades n'étaient
            // effacés que par ResetSession() — au clic sur Restart —, la sauvegarde de fin de run
            // capturait un état à moitié réinitialisé : fermer le jeu sur l'écran de Game Over
            // rendait tous les générateurs au niveau max, avec zéro Trace. Une run gratuite.
            WipeRun();

            // On notifie l'UI et on envoie le montant gagné.
            OnSessionEnded.OnNext(pendingPrestige);
        }

        /// <summary>
        /// Remet la run à son état de départ, bonus de méta-progression appliqués.
        /// Les TFlops n'y figurent pas : elles sont dérivées du parc Hardware, donc elles
        /// retombent d'elles-mêmes quand les niveaux sont remis à zéro.
        /// </summary>
        private void WipeRun()
        {
            _userCurrencies.Money.Reset(BaseStartingMoney + _prestigeManager.StartingMoney.CurrentValue);
            _emergencyProtocolSystem.ResetSystem();
            _threatManager.ReduceThreat(1f);
            _upgradeManager.InitializeFromSave(EmptyLevels);
        }

        /// <summary>
        /// Redémarre une partie après l'écran de fin. La run a déjà été effacée par
        /// HandleGameOver : il ne reste qu'à réarmer, et à réappliquer l'argent de départ au cas
        /// où un nœud de prestige aurait été acheté depuis l'écran de fin.
        /// </summary>
        public void ResetSession()
        {
            WipeRun();
            IsGameActive.Value = true;
        }

        public void Dispose()
        {
            OnSessionEnded.Dispose();
            _disposables.Dispose();
        }
    }
}
