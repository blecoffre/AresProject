using Core.Models;
using Core.Models.Economy;
using Core.Models.Simulation;
using Core.Services.Economy;
using Core.Services.Security;
using Core.Services.Simulation;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Services.Persistence
{
    /// <summary>
    /// Point unique de traduction entre l'état vivant du jeu et le fichier de sauvegarde.
    ///
    /// Les deux sens (Restore et Capture) sont volontairement dans la même classe et se lisent
    /// champ pour champ : c'est la seule façon de garantir qu'une donnée ajoutée d'un côté ne soit
    /// pas oubliée de l'autre. C'est exactement ce qui était arrivé au prestige, hydraté nulle part
    /// alors que le champ existait dans SaveData depuis le début.
    /// </summary>
    public class GameStateGateway
    {
        /// <summary>
        /// On ne restaure jamais une jauge pleine. Sinon la partie reprend en Game Over immédiat,
        /// avant même que l'écran de fin ne soit abonné et prêt à l'afficher. Le joueur reprend
        /// au bord du gouffre, ce qui est aussi plus juste que de le punir d'avoir fermé le jeu.
        /// </summary>
        private const float MaxRestorableThreat = 0.99f;

        private readonly UserCurrencies _currencies;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;
        private readonly ThreatManager _threatManager;
        private readonly TraceReadout _traceReadout;
        private readonly EmergencyProtocolSystem _emergencyProtocol;
        private readonly GameSessionManager _sessionManager;
        private readonly BuyQuantitySelector _buyQuantity;
        private readonly VictorySystem _victory;

        // Instance et tampons réutilisés d'une capture à l'autre : un autosave ne doit rien allouer
        // en dehors de la chaîne JSON elle-même.
        private readonly SaveData _buffer = SaveData.CreateNew();
        private readonly Dictionary<string, int> _upgradeLevels = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _prestigeLevels = new Dictionary<string, int>();

        /// <summary>
        /// Faux tant que Restore n'a pas eu lieu. Le SaveScheduler s'en sert comme verrou : écrire
        /// avant la restauration remplacerait la sauvegarde du joueur par un état vide.
        /// </summary>
        public bool HasRestored { get; private set; }

        /// <summary>
        /// Une partie existe-t-elle sur disque : restaurée d'un vrai fichier, ou écrite depuis
        /// (le premier autosave, le retour au menu, la fin de run). Faux au tout premier
        /// lancement, jusqu'à la première écriture — c'est ce qui fait dire « Commencer » au
        /// menu principal plutôt que « Continuer ».
        /// </summary>
        public bool HasSavedGame { get; private set; }

        /// <summary>
        /// Le joueur est-il entré en jeu au moins une fois pendant cette session. Posé par la
        /// GameScene à son démarrage.
        /// </summary>
        public bool HasGameStarted { get; private set; }

        /// <summary>
        /// Une écriture a-t-elle un sens. Il faut que l'état soit restauré (sinon on écraserait
        /// la sauvegarde par un état vide) ET qu'une partie existe : sur disque, ou entamée dans
        /// cette session. Une partie neuve jamais jouée n'est pas écrite — il n'y a rien à perdre,
        /// elle sera recréée à l'identique, et l'écrire ferait dire « Continuer » au menu à qui
        /// a seulement ouvert puis fermé le jeu.
        /// </summary>
        public bool CanWrite => HasRestored && (HasSavedGame || HasGameStarted);

        /// <summary>Appelé à l'entrée en jeu : à partir d'ici, la partie mérite d'être sauvegardée.</summary>
        public void MarkGameStarted() => HasGameStarted = true;

        public GameStateGateway(
            UserCurrencies currencies,
            UpgradeManager upgradeManager,
            PrestigeManager prestigeManager,
            ThreatManager threatManager,
            TraceReadout traceReadout,
            EmergencyProtocolSystem emergencyProtocol,
            GameSessionManager sessionManager,
            BuyQuantitySelector buyQuantity,
            VictorySystem victory)
        {
            _currencies = currencies;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _threatManager = threatManager;
            _traceReadout = traceReadout;
            _emergencyProtocol = emergencyProtocol;
            _sessionManager = sessionManager;
            _buyQuantity = buyQuantity;
            _victory = victory;
        }

        /// <summary>
        /// Hydrate tous les systèmes depuis une sauvegarde. Appelé une seule fois, au démarrage.
        /// </summary>
        public void Restore(SaveData data)
        {
            data.EnsureValid();

            // 1. Monnaies et compteurs cumulés
            _currencies.LoadFromSave(
                data.Money,
                data.CpuCycles,
                data.TotalMoney,
                data.RunMoney,
                data.TotalCpuCycles,
                data.TotalDetections);

            // Le cumul de campagne est de la MÉTA-progression : il survit au wipe de run comme
            // aux Game Over, et sans lui une sauvegarde rechargée réattribuerait les paliers
            // déjà encaissés.
            _currencies.RestoreCampaignProgress(data.CampaignDatasBanked, data.CpuCyclesAwarded);

            // ⚠️ La victoire se restaure ICI, AVANT les générateurs de l'étape 2 — et l'ordre est
            // la seule chose qui empêche le bug. InitializeFromSave recalcule les totaux, donc
            // pousse une valeur dans NetTFlops, que le VictorySystem observe : sur une partie
            // déjà gagnée, poser le drapeau après rejouerait la séquence de victoire à chaque
            // ouverture du jeu.
            _victory.Restore(data.HasWon);

            // ⚠️ Le sacrifice du Data Wiper se restaure lui aussi AVANT les générateurs, pour la
            // même raison. Le VictorySystem écoute déjà (IStartable, avant ce chargement
            // asynchrone) : restauré après, le parc serait d'abord compté sans sacrifice, et une
            // partie fermée en pleine run, deux usages au compteur, pouvait déclencher la victoire
            // à la réouverture. Le sacrifice est une fraction tenue par l'UpgradeManager : il
            // s'applique à tous les générateurs recréés ensuite.
            _emergencyProtocol.Restore(data.EmergencyUsesInRun);

            // 2. Générateurs de la run
            FillLevels(data.Upgrades, _upgradeLevels);
            _upgradeManager.InitializeFromSave(_upgradeLevels);

            // 3. Méta-progression
            FillLevels(data.PrestigeUpgrades, _prestigeLevels);
            _prestigeManager.InitializeFromSave(_prestigeLevels);

            // 4. État de menace de la run. Le plafond est fixe depuis la refonte « Time Attack » :
            // plus rien à recomposer avant de restaurer la jauge.
            _threatManager.RestoreTrace(Mathf.Min(data.CurrentTrace, _threatManager.TraceCap * MaxRestorableThreat));

            // Le relevé reprend TEL QU'IL ÉTAIT, et non sur la vérité : recaler au chargement
            // révélait la position exacte de la jauge — fermer puis rouvrir levait le brouillard
            // (session du 2026-10-14). Après la jauge, dont il vérifie qu'il ne la dépasse pas.
            _traceReadout.Restore(data.Readout.LastKnownTrace, data.Readout.SampleAge, data.Readout.KnownJumps);
            _sessionManager.RestoreElapsed(data.RunElapsedSeconds);

            // Partie fermée sur l'écran de fin : la run est déjà effacée, on désarme la partie et
            // l'écran se rouvrira au chargement de la GameScene, fenêtre d'achat comprise.
            if (data.PendingRunEnd.IsPending)
            {
                PendingRunEndSave pending = data.PendingRunEnd;
                _sessionManager.RestorePendingRunEnd(new RunSummary(
                    (RunEndReason)pending.Reason,
                    pending.CpuCyclesEarned,
                    pending.CpuCyclesBeforeBonus,
                    pending.DataGenerated,
                    pending.ThreatAtEnd,
                    pending.EmergencyUses,
                    pending.ElapsedSeconds));
            }

            // 5. Préférences d'interface
            //
            // Restaurées ici, donc AVANT que la GameScene ne soit chargée : l'UpgradePanelView
            // n'existe pas encore, et c'est bien pour ça que le sélecteur est un service du scope
            // racine que les presenters observent, plutôt qu'un état qu'ils détiendraient.
            _buyQuantity.Restore(data.BuyQuantityMode);

            HasRestored = true;
            HasSavedGame = !data.IsFresh;
        }

        /// <summary>
        /// Reconstitue une SaveData depuis l'état vivant.
        /// Retourne un tampon interne réutilisé : l'appelant doit le sérialiser immédiatement
        /// et ne pas le conserver au-delà de l'appel.
        /// </summary>
        public SaveData Capture()
        {
            // Tout appelant de Capture écrit le résultat sur disque : à partir d'ici, une
            // partie existe, et le menu principal dira « Continuer ».
            HasSavedGame = true;

            // Les TFlops ne sont pas capturées : elles se recalculent depuis les niveaux
            // d'Upgrades, qui le sont. Les écrire créerait une seconde source de vérité.
            _buffer.Money = _currencies.Money.Amount.CurrentValue;
            _buffer.CpuCycles = _currencies.CpuCycles.Amount.CurrentValue;

            _buffer.TotalMoney = _currencies.TotalMoneyGenerated.CurrentValue;
            _buffer.RunMoney = _currencies.RunMoneyGenerated.CurrentValue;
            _buffer.TotalCpuCycles = _currencies.TotalCpuCyclesGenerated.CurrentValue;
            _buffer.CampaignDatasBanked = _currencies.CampaignDatasBanked.CurrentValue;
            _buffer.CpuCyclesAwarded = _currencies.CpuCyclesAwarded.CurrentValue;
            _buffer.HasWon = _victory.Capture();
            _buffer.TotalDetections = _currencies.TotalNumberOfDetections.CurrentValue;

            _buffer.CurrentTrace = _threatManager.CurrentTrace.CurrentValue;
            _buffer.Readout.LastKnownTrace = _traceReadout.LastKnownTrace;
            _buffer.Readout.SampleAge = _traceReadout.SecondsSinceSample;
            _buffer.Readout.KnownJumps = _traceReadout.KnownJumps;
            _buffer.EmergencyUsesInRun = _emergencyProtocol.UsesInCurrentRun;
            _buffer.RunElapsedSeconds = _sessionManager.RunElapsedSeconds;

            // Struct à plat, assignée champ par champ : aucune allocation à l'autosave.
            if (_sessionManager.TryGetPendingRunEnd(out RunSummary pending))
            {
                _buffer.PendingRunEnd.IsPending = true;
                _buffer.PendingRunEnd.Reason = (int)pending.Reason;
                _buffer.PendingRunEnd.CpuCyclesEarned = pending.CpuCyclesEarned;
                _buffer.PendingRunEnd.CpuCyclesBeforeBonus = pending.CpuCyclesBeforeBonus;
                _buffer.PendingRunEnd.DataGenerated = pending.DataGenerated;
                _buffer.PendingRunEnd.ThreatAtEnd = pending.ThreatAtEnd;
                _buffer.PendingRunEnd.EmergencyUses = pending.EmergencyUses;
                _buffer.PendingRunEnd.ElapsedSeconds = pending.ElapsedSeconds;
            }
            else
            {
                _buffer.PendingRunEnd = default;
            }

            _buffer.BuyQuantityMode = _buyQuantity.Capture();

            _upgradeManager.CaptureLevelsInto(_buffer.Upgrades);
            _prestigeManager.CaptureLevelsInto(_buffer.PrestigeUpgrades);

            return _buffer;
        }

        private static void FillLevels(List<UpgradeSaveEntry> source, Dictionary<string, int> target)
        {
            target.Clear();

            // Boucle indexée plutôt que foreach : List<T> expose un énumérateur struct, mais on
            // reste explicite ici pour ne laisser aucune ambiguïté sur l'absence d'allocation.
            for (int i = 0; i < source.Count; i++)
            {
                UpgradeSaveEntry entry = source[i];
                if (string.IsNullOrEmpty(entry.Id)) continue;

                target[entry.Id] = entry.Level;
            }
        }
    }
}
