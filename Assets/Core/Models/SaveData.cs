using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Core.Models
{
    /// <summary>
    /// Le bilan d'une run terminée que le joueur n'a pas encore relancée — ce qu'affiche l'écran de
    /// fin. Copie à plat de <c>RunSummary</c> : ce fichier décrit un FORMAT, il ne dépend pas des
    /// types vivants du jeu.
    /// </summary>
    [Serializable]
    public struct PendingRunEndSave
    {
        /// <summary>Vrai tant que l'écran de fin attend « Nouvelle intrusion ».</summary>
        public bool IsPending;

        /// <summary>0 = saisie, 1 = sortie propre. Un format de fichier, comme BuyQuantityMode.</summary>
        public int Reason;

        public double CpuCyclesEarned;
        public double CpuCyclesBeforeBonus;
        public double DataGenerated;
        public float ThreatAtEnd;
        public int EmergencyUses;
        public float ElapsedSeconds;
    }

    /// <summary>
    /// L'état du relevé de Trace — ce que le joueur VOIT de la jauge. Sauvegardé depuis la v13 :
    /// recaler le relevé sur la vérité au chargement révélait la position exacte de la jauge.
    /// </summary>
    [Serializable]
    public struct TraceReadoutSave
    {
        /// <summary>Valeur du dernier relevé, en UT : l'aiguille.</summary>
        public float LastKnownTrace;

        /// <summary>Secondes écoulées depuis ce relevé.</summary>
        public float SampleAge;

        /// <summary>UT ajoutées d'un coup depuis ce relevé (pénalités d'Overclock).</summary>
        public float KnownJumps;
    }

    [Serializable]
    public struct UpgradeSaveEntry
    {
        public string Id;
        public int Level;

        public UpgradeSaveEntry(string id, int level)
        {
            Id = id;
            Level = level;
        }
    }

    /// <summary>
    /// Photographie complète de l'état du joueur.
    ///
    /// Classe et non struct, volontairement : l'objet porte deux List&lt;&gt; mutables. Une copie de
    /// struct partagerait ces références tout en donnant l'illusion d'une copie indépendante, ce qui
    /// est exactement le genre de bug qu'on ne trouve qu'en production.
    /// Le SaveScheduler réutilise une instance unique pour qu'un autosave n'alloue rien.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// Version du schéma. À incrémenter à CHAQUE changement de format. Pendant le
        /// développement, une sauvegarde d'une autre version ouvre une partie neuve (voir
        /// <see cref="Migrate"/>) ; après le premier build public, chaque incrément devra
        /// s'accompagner de sa migration — sans quoi il casserait les sauvegardes des joueurs.
        /// </summary>
        public const int CurrentVersion = 13;

        /// <summary>
        /// Dernière valeur valide de <c>BuyQuantityMode</c>, soit BuyQuantity.Max.
        ///
        /// Recopiée ici plutôt qu'importée depuis Core.Models.Economy, volontairement : ce fichier
        /// décrit un FORMAT DE FICHIER, pas l'état vivant du jeu. Ajouter une quantité d'achat
        /// oblige donc à toucher cette borne consciemment — c'est le but.
        /// </summary>
        private const int MaxBuyQuantityMode = 3;

        /// <summary>Dernière valeur valide de <c>PendingRunEnd.Reason</c> : 1, la sortie propre.</summary>
        private const int MaxRunEndReason = 1;

        public int Version;

        // ---------------------------------------------------------------------
        // Méta-progression : survit au Game Over et au wipe volontaire.
        // ---------------------------------------------------------------------
        public double CpuCycles;
        public double TotalMoney;
        public double TotalCpuCycles;
        public int TotalDetections;
        public List<UpgradeSaveEntry> PrestigeUpgrades;

        /// <summary>
        /// Datas exfiltrées depuis le début de la campagne. Ne retombe jamais à zéro entre deux
        /// runs — c'est ce compteur qui décroche les points de prestige. Une sortie y verse la
        /// contribution de la run, bonus de palier compris ; une saisie, 30 % seulement
        /// (`SeizedContributionRatio`, depuis le 2026-10-02).
        /// </summary>
        public double CampaignDatasBanked;

        /// <summary>
        /// Points de prestige déjà accordés. Sauvegardé SÉPARÉMENT du solde de CPU Cycles, qui
        /// baisse à chaque achat : sans ce compteur, recharger une partie où le joueur a tout
        /// dépensé lui rendrait tous les paliers déjà encaissés.
        /// </summary>
        public double CpuCyclesAwarded;

        /// <summary>
        /// Le seuil de victoire en TFlops a été atteint au moins une fois.
        ///
        /// De la MÉTA-progression, donc insensible au wipe : la victoire est un fait acquis de la
        /// campagne, pas un état de run. Elle n'est sauvegardée que pour ne pas rejouer sa mise en
        /// scène à chaque ouverture du jeu — la partie, elle, continue normalement après.
        /// </summary>
        public bool HasWon;

        // ---------------------------------------------------------------------
        // Run en cours : remise à zéro au wipe.
        // La Trace et le compteur d'urgence en font partie — sans eux, fermer et rouvrir
        // le jeu vide la jauge gratuitement, ce qui rend le Bouton d'Urgence inutile.
        // ---------------------------------------------------------------------
        // Note : aucune TFlop ici. C'est une capacité DÉRIVÉE du parc Hardware possédé, donc
        // entièrement recalculable depuis Upgrades ; la sauvegarder stockerait une valeur qui
        // pourrait diverger de l'état réel.
        public double Money;

        /// <summary>
        /// Argent généré depuis le début de la run. Pilote le gain de CPU Cycles en fin de run —
        /// c'est de l'état de RUN, pas une statistique cumulée, d'où sa place dans ce bloc.
        /// </summary>
        public double RunMoney;

        /// <summary>
        /// La jauge de l'A.M.I., en UT (0 à 10 000) : la VÉRITÉ, que le joueur ne voit qu'à
        /// travers le relevé ci-dessous.
        /// </summary>
        public float CurrentTrace;

        /// <summary>
        /// Le relevé de la jauge tel que le joueur le voyait (v13). Restauré tel quel : le
        /// brouillard ne doit pas se lever en fermant puis rouvrant le jeu.
        /// </summary>
        public TraceReadoutSave Readout;

        /// <summary>
        /// Usages du Data Wiper dans la run. Le sacrifice de TFlops en découle (30 % par usage) :
        /// le sauvegarder empêche qu'une fermeture rende la puissance engloutie.
        /// </summary>
        public int EmergencyUsesInRun;

        /// <summary>
        /// Secondes de JEU écoulées sur la run en cours. Sauvegardée pour qu'une run reprise
        /// après une fermeture affiche sa durée réelle et non celle de la dernière session. Le
        /// temps passé fenêtre close n'est jamais crédité : elle n'avance que dans Tick().
        /// </summary>
        public float RunElapsedSeconds;
        public List<UpgradeSaveEntry> Upgrades;

        // ---------------------------------------------------------------------
        // Entre deux runs : la run est déjà effacée, l'écran de fin attend.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Le bilan affiché par l'écran de fin, tant que le joueur n'a pas relancé. Ajouté en v12 :
        /// sans lui, quitter le jeu sur l'écran de fin relançait une run neuve au retour, et la
        /// fenêtre d'achat du prestige — ouverte seulement entre deux runs — était perdue.
        /// </summary>
        public PendingRunEndSave PendingRunEnd;

        // ---------------------------------------------------------------------
        // Préférences d'interface : ni méta-progression, ni run. Le wipe n'y touche pas —
        // perdre son mode d'achat parce qu'on s'est fait repérer n'aurait aucun sens.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Mode d'achat multiple sélectionné : 0 = x1, 1 = x10, 2 = x100, 3 = MAX.
        ///
        /// Stocké en int et non en BuyQuantity : JsonUtility sérialiserait l'enum par son entier
        /// de toute façon, et l'int rend explicite le fait que ces valeurs sont un format de
        /// fichier. Réordonner l'enum redéfinirait silencieusement la préférence de tous les
        /// joueurs installés.
        /// </summary>
        public int BuyQuantityMode;

        /// <summary>
        /// Horodatage de l'écriture (UTC, secondes Unix). Sert uniquement à départager une
        /// sauvegarde locale d'une sauvegarde Steam Cloud. Aucune progression hors-ligne n'en
        /// découle : le jeu se joue fenêtre ouverte, il n'y a pas de rattrapage au retour.
        /// </summary>
        public long SavedAtUnixSeconds;

        /// <summary>
        /// Vrai pour une partie créée faute de sauvegarde (premier lancement, ou fichiers
        /// illisibles). JAMAIS écrit sur disque : une SaveData relue d'un fichier vaut toujours
        /// faux, puisque JsonUtility ignore ce champ. Sert au menu principal, qui dit
        /// « Commencer » plutôt que « Continuer » à qui n'a encore rien joué.
        /// </summary>
        [NonSerialized] public bool IsFresh;

        public static SaveData CreateNew() => new SaveData
        {
            IsFresh = true,
            Version = CurrentVersion,

            CpuCycles = 0d,
            TotalMoney = 0d,
            TotalCpuCycles = 0d,
            TotalDetections = 0,
            PrestigeUpgrades = new List<UpgradeSaveEntry>(),

            Money = 10d,
            RunMoney = 0d,
            CurrentTrace = 0f,
            EmergencyUsesInRun = 0,
            RunElapsedSeconds = 0f,
            Upgrades = new List<UpgradeSaveEntry>(),

            BuyQuantityMode = 0,

            SavedAtUnixSeconds = 0L
        };

        /// <summary>
        /// Répare un objet issu d'une désérialisation : JsonUtility laisse les listes absentes à null
        /// et ne valide aucune borne. Appelé après chaque lecture, avant toute utilisation.
        /// </summary>
        public void EnsureValid()
        {
            if (Upgrades == null) Upgrades = new List<UpgradeSaveEntry>();
            if (PrestigeUpgrades == null) PrestigeUpgrades = new List<UpgradeSaveEntry>();

            // Borne basse seulement : le plafond appartient au ThreatManager, qui le lit dans les
            // réglages. Le dupliquer ici créerait deux sources de vérité.
            if (CurrentTrace < 0f) CurrentTrace = 0f;
            if (EmergencyUsesInRun < 0) EmergencyUsesInRun = 0;

            if (RunElapsedSeconds < 0f) RunElapsedSeconds = 0f;

            // Borne basse seulement : le TraceReadout vérifie lui-même qu'un relevé ne dépasse pas
            // la vérité, et se recale sinon.
            if (Readout.LastKnownTrace < 0f) Readout.LastKnownTrace = 0f;
            if (Readout.SampleAge < 0f) Readout.SampleAge = 0f;
            if (Readout.KnownJumps < 0f) Readout.KnownJumps = 0f;

            // Un bilan à la raison inconnue (fichier édité, binaire plus récent) ne rouvre pas
            // l'écran de fin : mieux vaut une run neuve qu'un écran qui affiche n'importe quoi.
            if (PendingRunEnd.Reason < 0 || PendingRunEnd.Reason > MaxRunEndReason) PendingRunEnd = default;

            // Le mode d'achat vient d'un fichier que le joueur peut avoir édité, ou d'un binaire
            // plus récent : tout ce qui n'est pas un mode connu retombe sur x1 plutôt que de
            // propager un enum hors domaine jusque dans les presenters.
            if (BuyQuantityMode < 0 || BuyQuantityMode > MaxBuyQuantityMode) BuyQuantityMode = 0;

            if (Money < 0d) Money = 0d;
            if (RunMoney < 0d) RunMoney = 0d;
            if (CpuCycles < 0d) CpuCycles = 0d;
            if (TotalDetections < 0) TotalDetections = 0;

            // Le cumul de campagne pilote directement le nombre de points de prestige : une
            // valeur négative arrivée d'un fichier édité à la main fausserait tout le palier.
            if (CampaignDatasBanked < 0d) CampaignDatasBanked = 0d;
            if (CpuCyclesAwarded < 0d) CpuCyclesAwarded = 0d;
        }

        /// <summary>
        /// Amène une sauvegarde lue sur disque au format courant, ou la remplace par une partie
        /// neuve.
        ///
        /// <b>Aucune migration tant que le jeu n'a pas de joueurs externes</b> (Bertrand,
        /// 2026-10-14). Le seul joueur supprime sa sauvegarde à chaque changement de format :
        /// treize migrations écrites pendant le développement (v0 → v13) n'ont jamais servi qu'une
        /// fois chacune, et ont été retirées au lot 5 de la refonte — elles restent dans
        /// l'historique git. Une sauvegarde d'un autre format ouvre donc une partie neuve.
        ///
        /// La première migration publique s'écrira ici : un <c>switch</c> sur la version, des cas
        /// chaînés par <c>goto case</c>, et le cas courant qui ne fait rien.
        /// </summary>
        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return CreateNew();

            data.EnsureValid();

            if (data.Version == CurrentVersion) return data;

            // Plus ancienne pendant le développement, plus récente après un retour en arrière de
            // build : dans les deux cas, on ne devine pas un format qu'on ne connaît pas.
            UnityEngine.Debug.LogWarning(
                $"[SAVE] Sauvegarde au format v{data.Version}, binaire en v{CurrentVersion} : aucune " +
                "migration pendant le développement. Nouvelle partie créée.");
            return CreateNew();
        }
    }

    public interface ISaveService
    {
        UniTask<SaveData> FetchSaveAsync(CancellationToken ct);
        UniTask SaveAsync(SaveData data, CancellationToken ct);
    }
}
