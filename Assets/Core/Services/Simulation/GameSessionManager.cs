using System.Collections.Generic;
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

        /// <summary>
        /// Le bilan de la run qui vient de se terminer, tant que le joueur n'a pas relancé.
        ///
        /// Gardé ICI, et sauvegardé, parce que l'écran de fin est le seul moment où l'on peut
        /// dépenser ses points : avant, quitter le jeu sur cet écran relançait une run neuve au
        /// retour, et la fenêtre d'achat du prestige était perdue (session du 2026-10-14).
        /// </summary>
        private RunSummary _pendingRunEnd;
        private bool _hasPendingRunEnd;

        public Subject<RunSummary> OnSessionEnded { get; }
        public ReactiveProperty<bool> IsGameActive { get; }

        private DisposableBag _disposables;

        public GameSessionManager(UserCurrencies userCurrencies, ThreatManager threatManager, UpgradeManager upgradeManager, PrestigeManager prestigeManager, EmergencyProtocolSystem emergencyProtocolSystem, BalancingConfigSO balancing, ITimeSource time)
        {
            _time = time;
            _userCurrencies = userCurrencies;
            _threatManager = threatManager;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _emergencyProtocolSystem = emergencyProtocolSystem;
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
            summary = ResolveRunEnd(RunEndReason.CleanExit, ResolveCleanExitMultiplier());
            return true;
        }

        /// <summary>
        /// Le bonus d'Effacement Propre : le PALIER d'extraction atteint, rien de plus.
        ///
        /// <b>C'est ici que vit le risque/récompense du jeu.</b> Tant que ce bonus était
        /// forfaitaire, sortir à 40 % ou à 99 % rapportait le même supplément : le joueur n'avait
        /// aucune raison de s'approcher du bord, et le danger n'achetait rien. Mesuré sur une
        /// campagne complète — le joueur téméraire mourait neuf fois sur trente-deux pour 1,3 %
        /// de Datas en plus. La prudence dominait strictement, ce qui n'est pas un arbitrage.
        ///
        /// La correction a d'abord été une courbe continue, et c'était l'excès inverse : chaque
        /// seconde de retard devenait monnayable, et la sortie se jouait au chronomètre. Des
        /// paliers rendent la décision DISCRÈTE — entre deux seuils le bonus ne bouge pas, donc
        /// traîner ne rapporte rien, et la seule question est « je tente le suivant, oui ou
        /// non ? ». C'est ce franchissement que le brouillard du <c>TraceReadout</c> rend
        /// incertain.
        ///
        /// La saisie fédérale, elle, garde son multiplicateur de 1 : le GDD n'a pas bougé. La
        /// punition est devenue implicite, et bien plus lourde — se faire prendre, c'est renoncer
        /// à un bonus qu'on a passé toute la run à faire grossir.
        /// </summary>
        private double ResolveCleanExitMultiplier()
            => ResolveCleanExitMultiplierFor(_threatManager.NormalizedThreat.CurrentValue);

        /// <summary>
        /// Le multiplicateur qu'une sortie rapporterait à la fraction de jauge donnée.
        ///
        /// Public pour que l'interface puisse ANNONCER le gain sans dupliquer la règle des
        /// paliers — une seconde implémentation finirait par diverger de celle-ci, et le joueur
        /// verrait un chiffre que la fin de run ne tiendrait pas.
        ///
        /// ⚠️ L'appelant choisit la fraction, et ce choix est un choix de DESIGN. La fin de run
        /// passe la vérité ; l'affichage doit passer le dernier relevé CONNU, sinon le gain
        /// annoncé trahirait la position exacte du joueur et viderait le brouillard de son sens.
        /// </summary>
        public double ResolveCleanExitMultiplierFor(float fraction)
        {
            IReadOnlyList<CleanExitTier> tiers = _balancing.CleanExitTiers;
            if (tiers == null) return 1d;

            // On garde le MEILLEUR palier atteint plutôt que de s'arrêter au premier : rien
            // n'oblige la liste réglée dans l'inspecteur à être triée, et un ordre involontaire
            // ferait silencieusement toucher un joueur à 95 % le bonus d'un palier à 50 %.
            double bonus = 0d;
            float bestThreshold = -1f;

            bool highRiskUnlocked = _prestigeManager.IsHighRiskExtractionUnlocked.CurrentValue;

            for (int i = 0; i < tiers.Count; i++)
            {
                CleanExitTier tier = tiers[i];
                if (fraction < tier.TraceThreshold) continue;
                if (tier.TraceThreshold <= bestThreshold) continue;

                // Palier sous condition non débloqué : on ne s'arrête PAS, on l'ignore. Le joueur
                // retombe ainsi sur le meilleur palier libre qu'il a franchi — pousser jusqu'à
                // 90 % sans le nœud rapporte donc ce que rapportent 75 %, jamais rien.
                //
                // Le palier à 90 % était proposé dès la première run, quand le joueur n'a encore
                // aucun capteur et pilote à l'aveugle : il invitait sans être tenable. Mesuré sans
                // capteur, la posture qui le visait mourait 15 fois sur 18 et plafonnait à 2,7 %
                // de l'arbre.
                if (tier.RequiresUnlock && !highRiskUnlocked) continue;

                bestThreshold = tier.TraceThreshold;
                bonus = tier.Bonus;
            }

            // Sous le premier palier, la sortie ne rapporte aucun bonus — mais elle reste une
            // sortie propre : les Cycles de base sont acquis. Se faire saisir ne coûte donc
            // jamais la run, seulement ce qu'on est allé chercher.
            return 1d + bonus * _prestigeManager.CleanExitBonusMultiplier.CurrentValue;
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
            double dataGenerated = _userCurrencies.RunMoneyGenerated.CurrentValue;
            float threatAtEnd = _threatManager.NormalizedThreat.CurrentValue;
            int emergencyUses = _emergencyProtocolSystem.UsesInCurrentRun;
            float elapsed = _runElapsedSeconds;

            // La contribution au cumul de campagne, et les points qu'elle débloque, ne sont
            // versés QUE sur une exfiltration réussie. Une saisie ne verse rien : ni Datas au
            // compteur, ni point.
            //
            // Les deux sont résolus au même instant, et c'est indispensable. Si les points
            // tombaient pendant la run alors que la saisie annule la contribution, il
            // deviendrait rentable de se faire prendre exprès — on encaisserait le palier sans
            // jamais faire monter le seuil suivant, et la run d'après le réencaisserait.
            //
            // Le multiplicateur du palier d'extraction s'applique à la CONTRIBUTION, jamais aux
            // points : majorer directement des points entiers produirait des demi-paliers et
            // rouvrirait exactement le même décalage.
            double baseCycles = _userCurrencies.PreviewPointsForContribution(dataGenerated);

            // Une saisie ne verse plus RIEN : elle verse une fraction. Le reste du raisonnement
            // ci-dessus tient toujours — les deux restent résolus au même instant, donc se faire
            // prendre exprès n'encaisse jamais un palier sans faire monter le seuil suivant.
            //
            // Ce qui a changé, c'est le constat : rien dans le jeu ne capitalisait une run perdue.
            // Mesuré sur une partie réelle le 2026-09-22 — 294 M de Datas générées sur deux morts,
            // zéro banquée, arbre vide. Le joueur restait à distance CONSTANTE de son premier
            // point quelle que soit sa performance.
            //
            // La saisie reste nettement moins rentable qu'une sortie : celle-ci verse la totalité
            // ET le bonus de palier par-dessus.
            double contributionRatio = reason == RunEndReason.CleanExit
                ? prestigeMultiplier
                : _balancing.SeizedContributionRatio;

            double pendingPrestige = _userCurrencies.BankRunContribution(dataGenerated * contributionRatio);

            // La run est effacée ICI, en entier, et AVANT la notification.
            //
            // Le SaveScheduler écrit sur OnSessionEnded. Tant que les niveaux d'upgrades n'étaient
            // effacés que par ResetSession() — au clic sur Restart —, la sauvegarde de fin de run
            // capturait un état à moitié réinitialisé : fermer le jeu sur l'écran de Game Over
            // rendait tous les générateurs au niveau max, avec zéro Trace. Une run gratuite.
            WipeRun();

            _pendingRunEnd = new RunSummary(reason, pendingPrestige, baseCycles, dataGenerated, threatAtEnd, emergencyUses, elapsed);
            _hasPendingRunEnd = true;
            return _pendingRunEnd;
        }

        /// <summary>
        /// Le bilan affiché par l'écran de fin, si une run est terminée et pas encore relancée.
        /// L'écran le relit à son chargement : c'est ce qui le rouvre après une fermeture du jeu.
        /// </summary>
        public bool TryGetPendingRunEnd(out RunSummary summary)
        {
            summary = _pendingRunEnd;
            return _hasPendingRunEnd && !IsGameActive.CurrentValue;
        }

        /// <summary>
        /// Restaure une partie fermée sur l'écran de fin : run déjà effacée, partie désarmée,
        /// fenêtre d'achat du prestige ouverte. Réservé au GameStateGateway.
        /// </summary>
        public void RestorePendingRunEnd(RunSummary summary)
        {
            _pendingRunEnd = summary;
            _hasPendingRunEnd = true;
            IsGameActive.Value = false;
        }

        /// <summary>
        /// Remet la run à son état de départ. L'argent de départ est la base du BalancingConfig
        /// PLUS la branche « Capital de Départ » (2026-10-16) ; ResetSession repasse ici, donc un
        /// nœud acheté sur l'écran de fin compte dès la run suivante.
        /// Les TFlops n'y figurent pas : elles sont dérivées du parc Hardware, donc elles
        /// retombent d'elles-mêmes quand les niveaux sont remis à zéro.
        /// </summary>
        private void WipeRun()
        {
            _userCurrencies.Money.Reset(
                _balancing.BaseStartingMoney + _prestigeManager.TotalStartingMoneyBonus.CurrentValue);
            _userCurrencies.ResetRunCounters();
            _runElapsedSeconds = 0f;

            _threatManager.ResetTrace();
            _upgradeManager.InitializeFromSave(EmptyLevels);

            // ⚠️ Le sacrifice du Data Wiper est rendu APRÈS le vidage du parc, jamais avant.
            // Rendu d'abord, il faisait recalculer les TFlops nettes sur tout le Hardware de la run
            // sans plus aucun sacrifice : un instant à pleine puissance, que le VictorySystem
            // observe. Une run finie avec deux usages et un parc au-dessus du seuil déclenchait
            // ainsi la victoire à la sortie (constaté par Bertrand le 2026-10-15).
            _emergencyProtocolSystem.ResetSystem();
        }

        /// <summary>
        /// Redémarre une partie après l'écran de fin. La run a déjà été effacée par
        /// HandleGameOver : il ne reste qu'à réarmer, et à réappliquer l'argent de départ au cas
        /// où un nœud de prestige aurait été acheté depuis l'écran de fin.
        /// </summary>
        public void ResetSession()
        {
            WipeRun();
            _hasPendingRunEnd = false;
            IsGameActive.Value = true;
        }

        public void Dispose()
        {
            OnSessionEnded.Dispose();
            _disposables.Dispose();
        }
    }
}
