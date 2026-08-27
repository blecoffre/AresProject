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

        /// <summary>
        /// Bonus « Clean Exit » : +20 % de CPU Cycles quand le joueur sort de lui-même au lieu
        /// de se faire saisir. C'est ce qui doit le pousser à flirter avec 95 % de Trace plutôt
        /// qu'à wiper dès qu'il le peut.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        private const double CleanExitMultiplier = 1.2d;

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

        /// <summary>Saisie Fédérale : la Trace a atteint 100 %, aucun bonus, écran de fin immédiat.</summary>
        private void HandleGameOver()
        {
            // Une run déjà résolue ne peut pas se terminer une seconde fois. Le ThreatManager
            // émet son lockdown dès que la jauge atteint 1, sans savoir si la partie est encore
            // en cours : pendant les ~2 s de la séquence d'exfiltration, une saisie viendrait
            // sinon écraser l'Effacement Propre que le joueur venait d'obtenir — et afficherait
            // l'écran de fin par-dessus la séquence.
            if (!IsGameActive.CurrentValue) return;

            Debug.Log("[GameSessionManager] A.M.I. CORRUPTION DÉTECTÉE. Fin de session en cours...");
            _userCurrencies.RecordDetection();

            AnnounceRunEnded(ResolveRunEnd(1d));
        }

        /// <summary>
        /// Effacement Propre : le joueur sort de lui-même avant les 100 %, et touche le bonus
        /// « Clean Exit ». Aucune détection enregistrée — il n'a jamais été pris.
        ///
        /// <b>Résout la run sans annoncer sa fin.</b> C'est volontaire et c'est le cœur du
        /// découpage : la séquence console du Protocole Terre Brûlée dure ~2 s, et si on la
        /// jouait avant de figer le résultat, la Trace continuerait de monter pendant ce
        /// temps — un joueur exfiltrant à 98 % pourrait se faire saisir pendant sa propre
        /// exfiltration et perdre les +20 % qu'il était justement venu chercher.
        ///
        /// L'appelant DOIT appeler <see cref="AnnounceRunEnded"/> une fois sa séquence finie,
        /// sinon l'écran de fin ne s'affichera jamais.
        ///
        /// Retourne false si la run est déjà terminée.
        /// </summary>
        public bool TryResolveVoluntaryExit(out double awardedPrestige)
        {
            awardedPrestige = 0d;
            if (!IsGameActive.CurrentValue) return false;

            Debug.Log("[GameSessionManager] Exfiltration volontaire. Effacement propre.");
            awardedPrestige = ResolveRunEnd(CleanExitMultiplier);
            return true;
        }

        /// <summary>
        /// Déclenche l'écran de fin. Sans effet si aucune run n'a été résolue : cette garde rend
        /// un appel isolé inoffensif, la méthode étant publique pour le besoin du découpage.
        /// </summary>
        public void AnnounceRunEnded(double awardedPrestige)
        {
            if (IsGameActive.CurrentValue) return;

            OnSessionEnded.OnNext(awardedPrestige);
        }

        /// <summary>
        /// Fige le résultat de la run : gain calculé et crédité, run effacée, partie désarmée.
        /// Ne notifie personne — voir <see cref="AnnounceRunEnded"/>.
        /// </summary>
        private double ResolveRunEnd(double prestigeMultiplier)
        {
            IsGameActive.Value = false;

            double pendingPrestige = _userCurrencies.CalculatePendingCpuCycles() * prestigeMultiplier;
            pendingPrestige = Math.Floor(pendingPrestige);

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

            return pendingPrestige;
        }

        /// <summary>
        /// Remet la run à son état de départ, bonus de méta-progression appliqués.
        /// Les TFlops n'y figurent pas : elles sont dérivées du parc Hardware, donc elles
        /// retombent d'elles-mêmes quand les niveaux sont remis à zéro.
        /// </summary>
        private void WipeRun()
        {
            _userCurrencies.Money.Reset(BaseStartingMoney + _prestigeManager.StartingMoney.CurrentValue);
            _userCurrencies.ResetRunCounters();
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
