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
        /// Version du schéma. À incrémenter à CHAQUE changement de format, en ajoutant le cas
        /// correspondant dans Migrate(). Sans ça, le premier changement post-lancement casse
        /// silencieusement les sauvegardes des joueurs.
        /// </summary>
        public const int CurrentVersion = 12;

        /// <summary>
        /// Id de la racine de l'arbre, seul nœud gratuit de la v10. Recopié ici plutôt que lu
        /// dans le catalogue : la migration v10 → v11 décrit un FORMAT figé, pas l'arbre vivant.
        /// </summary>
        private const string LegacyRootNodeId = "P_ROOT";

        /// <summary>Argent de départ d'une run neuve en v10, pour la remise à neuf de la v11.</summary>
        private const double LegacyStartingMoney = 10d;

        /// <summary>
        /// Le plafond de Trace tel qu'il était FIGÉ jusqu'en v6. Sert uniquement à convertir
        /// l'ancienne fraction en points absolus lors de la migration ; il ne doit jamais suivre
        /// le plafond courant, devenu dynamique.
        /// </summary>
        private const float LegacyTraceCap = 100f;

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
        /// runs — c'est ce compteur qui décroche les points de prestige, et une saisie ne
        /// l'alimente pas.
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
        // Note : ni ComputerPower ni TotalComputerPower ne figurent plus ici. Les TFlops sont
        // devenues une capacité DÉRIVÉE du parc Hardware possédé, donc entièrement recalculable
        // depuis Upgrades. Les sauvegarder revenait à stocker une valeur qui pouvait diverger de
        // l'état réel — et le total cumulé n'avait plus de sens pour une grandeur qu'on
        // n'accumule pas. Retirés en v2.
        public double Money;

        /// <summary>
        /// Argent généré depuis le début de la run. Pilote le gain de CPU Cycles en fin de run —
        /// c'est de l'état de RUN, pas une statistique cumulée, d'où sa place dans ce bloc.
        /// </summary>
        public double RunMoney;

        /// <summary>
        /// Trace accumulée, en points ABSOLUS. Remplace la fraction normalisée en v7 : le
        /// plafond étant devenu dynamique, une fraction ne veut plus rien dire hors du parc
        /// Hardware qui l'a produite — acheter une machine aurait rescalé la trace en silence.
        /// </summary>
        public float CurrentTrace;

        /// <summary>
        /// OBSOLÈTE depuis la v7, conservé pour la seule migration. JsonUtility ne peut lire un
        /// champ que s'il existe encore dans la classe : le retirer rendrait les sauvegardes
        /// antérieures illisibles, et le joueur repartirait de zéro sans un mot.
        /// </summary>
        public float NormalizedThreat;

        public int EmergencyUsesInRun;

        // GhostCacheSeconds a quitté le format en v11, avec le Ghost Cache lui-même. JsonUtility
        // ignore le champ resté dans un ancien fichier : rien à faire pour le lire.

        // EmergencyBlockRemainingSeconds et EmergencyCooldownRemainingSeconds ont quitté le format
        // en v12 : le Data Wiper n'a plus ni contrecoup temporaire ni délai. Son sacrifice découle
        // du seul EmergencyUsesInRun.

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
        /// Amène une sauvegarde d'un format ancien vers le format courant.
        /// Les cas s'enchaînent sans "break" pour qu'une sauvegarde v1 traverse v2, v3, etc.
        /// </summary>
        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return CreateNew();

            data.EnsureValid();

            switch (data.Version)
            {
                case 0:
                    // Fichiers antérieurs à l'introduction du versionnement : ils ne portaient ni
                    // Trace, ni compteur d'urgence, ni prestige. Les valeurs par défaut de
                    // EnsureValid() suffisent, on se contente de tamponner la version.
                    data.Version = 1;
                    goto case 1;

                case 1:
                    // v1 -> v2 : ComputerPower et TotalComputerPower disparaissent. Les TFlops
                    // sont désormais recalculées depuis les niveaux d'Upgrades, donc il n'y a
                    // rien à reporter — JsonUtility ignore simplement les champs en trop d'un
                    // ancien fichier. On se contente de tamponner la version.
                    data.Version = 2;
                    goto case 2;

                case 2:
                    // v2 -> v3 : RunMoney apparaît. Une sauvegarde antérieure ne distinguait pas
                    // l'argent de la run du cumul à vie ; on repart de zéro pour la run en cours
                    // plutôt que de recopier le cumul, ce qui offrirait un gain de prestige
                    // immérité pour une run déjà entamée.
                    data.RunMoney = 0d;
                    data.Version = 3;
                    goto case 3;

                case 3:
                    // v3 -> v4 : le Ghost Cache apparaissait. Son champ a quitté le format en
                    // v11 ; il ne reste qu'à tamponner la version.
                    data.Version = 4;
                    goto case 4;

                case 4:
                    // v4 -> v5 : le Bouton d'Urgence passait au « Data Wiper » et gagnait deux
                    // compteurs, sortis du format en v12. Il ne reste qu'à tamponner la version.
                    data.Version = 5;
                    goto case 5;

                case 5:
                    // v5 -> v6 : la durée de la run apparaît. Une sauvegarde antérieure n'a pas
                    // mesuré son temps de jeu ; on repart de zéro plutôt que d'inventer une durée.
                    data.RunElapsedSeconds = 0f;
                    data.Version = 6;
                    goto case 6;

                case 6:
                    // v6 -> v7 : la jauge passe de la FRACTION à la valeur absolue.
                    //
                    // L'ancien champ valait trace / plafond, avec un plafond alors figé à 100.
                    // La conversion est donc une multiplication par ce plafond historique — et
                    // NON par le plafond courant, que le parc Hardware du joueur a pu élever :
                    // on rendrait sinon une trace bien plus lourde que celle qu'il avait.
                    data.CurrentTrace = data.NormalizedThreat * LegacyTraceCap;
                    data.NormalizedThreat = 0f;
                    data.Version = 7;
                    goto case 7;

                case 7:
                    // v7 -> v8 : l'achat multiple apparaît, et avec lui la préférence de
                    // quantité. Une sauvegarde antérieure n'a par définition rien choisi ; on
                    // part de x1, qui est aussi le seul comportement qu'elle a connu.
                    data.BuyQuantityMode = 0;
                    data.Version = 8;
                    goto case 8;

                case 8:
                    // v8 -> v9 : les points de prestige se décrochent désormais sur un cumul de
                    // Datas exfiltrées à l'échelle de la campagne, plus sur l'argent de la seule
                    // run. Une sauvegarde antérieure n'a aucun cumul à reprendre : on repart de
                    // zéro sur les DEUX compteurs à la fois.
                    //
                    // Les mettre tous les deux à zéro est volontaire et sans perte : le joueur
                    // garde ses CPU Cycles et ses nœuds déjà achetés, et son prochain palier
                    // sera simplement le premier. Ne remettre à zéro que le cumul en gardant les
                    // points versés lui bloquerait la progression jusqu'à ce qu'il rattrape son
                    // ancien total.
                    data.CampaignDatasBanked = 0d;
                    data.CpuCyclesAwarded = 0d;
                    data.Version = 9;
                    goto case 9;

                case 9:
                    // v9 -> v10 : la victoire existe enfin dans le jeu, et pas seulement dans le
                    // harnais de mesure. Une sauvegarde antérieure n'a jamais pu la déclencher,
                    // donc elle part à FAUX même si son parc dépasse déjà le seuil : le joueur
                    // verra sa séquence au prochain recalcul des totaux, ce qui est exactement ce
                    // qu'on veut — il l'a méritée, il ne l'avait simplement jamais reçue.
                    data.HasWon = false;
                    data.Version = 10;
                    goto case 10;

                case 10:
                    // v10 -> v11 : refonte « Time Attack ». La jauge passe à 10 000 UT fixes et
                    // l'arbre de prestige change de nœuds, de prix et d'effets.
                    //
                    // La RUN en cours est effacée : sa Trace se comptait sur un plafond de
                    // plusieurs millions, et la ramener sur 10 000 UT la mettrait au bord de la
                    // saisie.
                    //
                    // L'ARBRE est remis à zéro et chaque point dépensé RENDU : des nœuds ont
                    // disparu (Furtivité, Zéro-Day, réveil, barre d'espace), d'autres ont changé
                    // de prix — garder un Blindage acheté 1 point qui en coûte désormais 10 serait
                    // un cadeau, perdre les points des nœuds retirés une punition. En v10, tout
                    // rang coûtait exactement 1 point, sauf la racine, gratuite.
                    //
                    // Le cumul de campagne, les points déjà accordés et la victoire, eux, ne
                    // bougent pas : le joueur garde tout ce qu'il a gagné, il le redépense.
                    WipeRunForFormatChange(data);
                    RefundPrestigeTree(data);
                    data.Version = 11;
                    goto case 11;

                case 11:
                    // v11 -> v12 : Data Wiper du lot 2 et écran de fin sauvegardé.
                    //
                    // Le compteur d'usages change de sens : il valait un nombre de purges à délai,
                    // il fixe désormais un SACRIFICE PERMANENT de TFlops (30 % par usage). Le
                    // reprendre tel quel infligerait d'un coup 60 % de sacrifice à qui avait purgé
                    // deux fois sous l'ancien modèle : on repart de zéro.
                    //
                    // Aucun bilan en attente : une sauvegarde v11 ne savait pas qu'elle était
                    // fermée sur l'écran de fin.
                    data.EmergencyUsesInRun = 0;
                    data.PendingRunEnd = default;
                    data.Version = 12;
                    goto case 12;

                case 12:
                    // Format courant : rien à faire.
                    break;

                default:
                    // Sauvegarde plus récente que le binaire (rollback de build, partage de fichier).
                    // On ne tente pas de la deviner : mieux vaut une partie neuve qu'un état incohérent.
                    UnityEngine.Debug.LogError(
                        $"[SAVE] Version de sauvegarde inconnue ({data.Version}, binaire en v{CurrentVersion}). Nouvelle partie créée.");
                    return CreateNew();
            }

            return data;
        }

        /// <summary>
        /// Remet la run au départ : générateurs, argent, Trace, Data Wiper et chronomètre. Ce que
        /// fait le wipe de fin de run, transcrit sur le FORMAT — la migration tourne avant que les
        /// services n'existent.
        /// </summary>
        private static void WipeRunForFormatChange(SaveData data)
        {
            data.Money = LegacyStartingMoney;
            data.RunMoney = 0d;
            data.CurrentTrace = 0f;
            data.EmergencyUsesInRun = 0;
            data.RunElapsedSeconds = 0f;
            data.Upgrades.Clear();
        }

        /// <summary>
        /// Vide l'arbre et rend au solde de CPU Cycles tout ce qui y a été dépensé, au tarif de la
        /// v10 : un point par rang, la racine exceptée. La racine reste acquise.
        /// </summary>
        private static void RefundPrestigeTree(SaveData data)
        {
            double refund = 0d;

            for (int i = data.PrestigeUpgrades.Count - 1; i >= 0; i--)
            {
                UpgradeSaveEntry entry = data.PrestigeUpgrades[i];
                if (entry.Id == LegacyRootNodeId) continue;

                if (entry.Level > 0) refund += entry.Level;
                data.PrestigeUpgrades.RemoveAt(i);
            }

            data.CpuCycles += refund;
        }
    }

    public interface ISaveService
    {
        UniTask<SaveData> FetchSaveAsync(CancellationToken ct);
        UniTask SaveAsync(SaveData data, CancellationToken ct);
    }
}
