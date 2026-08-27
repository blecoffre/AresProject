using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Core.Models
{
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
        public const int CurrentVersion = 4;

        public int Version;

        // ---------------------------------------------------------------------
        // Méta-progression : survit au Game Over et au wipe volontaire.
        // ---------------------------------------------------------------------
        public double CpuCycles;
        public double TotalMoney;
        public double TotalCpuCycles;
        public int TotalDetections;
        public List<UpgradeSaveEntry> PrestigeUpgrades;

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

        public float NormalizedThreat;
        public int EmergencyUsesInRun;

        /// <summary>
        /// Charge du Ghost Cache, en secondes d'excédent de dissipation accumulées.
        ///
        /// Sauvegardée alors que le reste des effets temporaires ne l'est pas : elle représente
        /// du temps de jeu investi, et le joueur qui a maintenu sa posture défensive pendant
        /// quatre minutes n'a pas à recommencer parce qu'il a fermé la fenêtre. Un Overdrive EN
        /// COURS, lui, n'est pas sauvegardé — voir GhostCacheSystem.RestoreCharge.
        /// </summary>
        public float GhostCacheSeconds;
        public List<UpgradeSaveEntry> Upgrades;

        /// <summary>
        /// Horodatage de l'écriture (UTC, secondes Unix). Sert uniquement à départager une
        /// sauvegarde locale d'une sauvegarde Steam Cloud. Aucune progression hors-ligne n'en
        /// découle : le jeu se joue fenêtre ouverte, il n'y a pas de rattrapage au retour.
        /// </summary>
        public long SavedAtUnixSeconds;

        public static SaveData CreateNew() => new SaveData
        {
            Version = CurrentVersion,

            CpuCycles = 0d,
            TotalMoney = 0d,
            TotalCpuCycles = 0d,
            TotalDetections = 0,
            PrestigeUpgrades = new List<UpgradeSaveEntry>(),

            Money = 10d,
            RunMoney = 0d,
            NormalizedThreat = 0f,
            EmergencyUsesInRun = 0,
            GhostCacheSeconds = 0f,
            Upgrades = new List<UpgradeSaveEntry>(),

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

            if (NormalizedThreat < 0f) NormalizedThreat = 0f;
            if (NormalizedThreat > 1f) NormalizedThreat = 1f;
            if (EmergencyUsesInRun < 0) EmergencyUsesInRun = 0;

            // Borne basse seulement : le plafond appartient au GhostCacheSystem, seul détenteur
            // de la capacité. Le dupliquer ici créerait deux constantes à maintenir.
            if (GhostCacheSeconds < 0f) GhostCacheSeconds = 0f;

            if (Money < 0d) Money = 0d;
            if (RunMoney < 0d) RunMoney = 0d;
            if (CpuCycles < 0d) CpuCycles = 0d;
            if (TotalDetections < 0) TotalDetections = 0;
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
                    // v3 -> v4 : le Ghost Cache apparaît. Une sauvegarde antérieure n'a par
                    // définition accumulé aucun excédent — la mécanique n'existait pas — donc
                    // rien à reporter, on part de zéro.
                    data.GhostCacheSeconds = 0f;
                    data.Version = 4;
                    goto case 4;

                case 4:
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
    }

    public interface ISaveService
    {
        UniTask<SaveData> FetchSaveAsync(CancellationToken ct);
        UniTask SaveAsync(SaveData data, CancellationToken ct);
    }
}
