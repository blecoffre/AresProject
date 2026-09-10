using Core.Models.Economy;
using Core.Models.Simulation;
using Core.Services.Economy;
using Core.Services.Platform;
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
    public class GameSessionManager : IStartable, ITickable, IDisposable
    {
        /// <summary>Tampon réutilisé : un wipe ne doit pas allouer un dictionnaire à chaque fois.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, int> EmptyLevels =
            new System.Collections.Generic.Dictionary<string, int>();

        private readonly UserCurrencies _userCurrencies;
        private readonly ThreatManager _threatManager;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;
        private readonly EmergencyProtocolSystem _emergencyProtocolSystem;
        private readonly GhostCacheSystem _ghostCacheSystem;
        private readonly ITimeSource _time;
        private readonly BalancingConfigSO _balancing;

        /// <summary>
        /// Secondes de jeu écoulées sur la run en cours. Cumulées frame par frame plutôt que
        /// déduites d'un `Time.time` de départ : ce dernier repart de zéro à chaque lancement du
        /// jeu, et une run reprise le lendemain afficherait la durée de la seule session en cours.
        /// </summary>
        private float _runElapsedSeconds;

        /// <summary>Durée de la run en cours, en secondes. Fait partie de l'état sauvegardé.</summary>
        public float RunElapsedSeconds => _runElapsedSeconds;

        public Subject<RunSummary> OnSessionEnded { get; }
        public ReactiveProperty<bool> IsGameActive { get; }

        private DisposableBag _disposables;

        public GameSessionManager(UserCurrencies userCurrencies, ThreatManager threatManager, UpgradeManager upgradeManager, PrestigeManager prestigeManager, EmergencyProtocolSystem emergencyProtocolSystem, GhostCacheSystem ghostCacheSystem, BalancingConfigSO balancing, ITimeSource time)
        {
            _time = time;
            _userCurrencies = userCurrencies;
            _threatManager = threatManager;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _emergencyProtocolSystem = emergencyProtocolSystem;
            _ghostCacheSystem = ghostCacheSystem;
            _balancing = balancing;

            OnSessionEnded = new Subject<RunSummary>();
            IsGameActive = new ReactiveProperty<bool>(true);

            _disposables = new DisposableBag();
        }

        public void Start()
        {
            // Abonnement à l'événement de détection maximale
            _threatManager.OnCriticalLockdown
                .Subscribe(_ => HandleGameOver())
                .AddTo(ref _disposables);

            // La compilation de prestige n'est ouverte qu'ENTRE deux runs. Le PrestigeManager
            // porte la règle et la fait respecter ; lui, il ne peut pas la connaître, puisqu'il
            // ignore tout de la notion de run — et l'injecter dans l'autre sens fermerait le
            // cycle de dépendances. C'est donc la session qui déclare son état, une fois, et
            // l'abonnement émet immédiatement : la fenêtre est correcte dès la première frame.
            IsGameActive
                .Subscribe(isActive => _prestigeManager.SetPurchaseWindowOpen(!isActive))
                .AddTo(ref _disposables);
        }

        /// <summary>
        /// Fait avancer le chronomètre de la run. Une addition de float par frame, et rien
        /// pendant l'écran de fin : le temps passé à contempler son bilan n'est pas du jeu.
        /// </summary>
        public void Tick()
        {
            if (!IsGameActive.CurrentValue) return;

            _runElapsedSeconds += _time.DeltaTime;
        }

        /// <summary>Restaure le chronomètre depuis une sauvegarde.</summary>
        public void RestoreElapsed(float seconds)
        {
            _runElapsedSeconds = seconds < 0f ? 0f : seconds;
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

            AnnounceRunEnded(ResolveRunEnd(RunEndReason.Seized, 1d));
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
        public bool TryResolveVoluntaryExit(out RunSummary summary)
        {
            summary = default;
            if (!IsGameActive.CurrentValue) return false;

            Debug.Log("[GameSessionManager] Exfiltration volontaire. Effacement propre.");
            summary = ResolveRunEnd(RunEndReason.CleanExit, _balancing.CleanExitMultiplier);
            return true;
        }

        /// <summary>
        /// Déclenche l'écran de fin. Sans effet si aucune run n'a été résolue : cette garde rend
        /// un appel isolé inoffensif, la méthode étant publique pour le besoin du découpage.
        /// </summary>
        public void AnnounceRunEnded(RunSummary summary)
        {
            if (IsGameActive.CurrentValue) return;

            OnSessionEnded.OnNext(summary);
        }

        /// <summary>
        /// Fige le résultat de la run : gain calculé et crédité, run effacée, partie désarmée.
        /// Ne notifie personne — voir <see cref="AnnounceRunEnded"/>.
        /// </summary>
        private RunSummary ResolveRunEnd(RunEndReason reason, double prestigeMultiplier)
        {
            IsGameActive.Value = false;

            // Tout ce que l'écran de fin racontera est lu MAINTENANT, avant le wipe : trois lignes
            // plus bas, l'argent de la run, la Trace et le compteur d'urgence seront à zéro.
            double baseCycles = Math.Floor(_userCurrencies.CalculatePendingCpuCycles());
            double dataGenerated = _userCurrencies.RunMoneyGenerated.CurrentValue;
            float threatAtEnd = _threatManager.NormalizedThreat.CurrentValue;
            int emergencyUses = _emergencyProtocolSystem.UsesInCurrentRun;
            float elapsed = _runElapsedSeconds;

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

            return new RunSummary(reason, pendingPrestige, baseCycles, dataGenerated, threatAtEnd, emergencyUses, elapsed);
        }

        /// <summary>
        /// Remet la run à son état de départ, bonus de méta-progression appliqués.
        /// Les TFlops n'y figurent pas : elles sont dérivées du parc Hardware, donc elles
        /// retombent d'elles-mêmes quand les niveaux sont remis à zéro.
        /// </summary>
        private void WipeRun()
        {
            _userCurrencies.Money.Reset(_balancing.BaseStartingMoney + _prestigeManager.StartingMoney.CurrentValue);
            _userCurrencies.ResetRunCounters();
            _emergencyProtocolSystem.ResetSystem();

            // Le Ghost Cache est de l'état de RUN : le formatage l'efface, même chargé à bloc.
            // Le laisser survivre offrirait au joueur un Zéro-Day Exploit gratuit au démarrage
            // de chaque run, exactement au moment où sa Trace est à zéro et où l'Exploit ne lui
            // coûterait donc rien — la mécanique perdrait tout son pari.
            _ghostCacheSystem.ResetForNewRun();
            _runElapsedSeconds = 0f;

            _threatManager.ResetTrace();
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
