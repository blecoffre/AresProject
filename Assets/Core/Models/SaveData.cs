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
        public const int CurrentVersion = 1;

        public int Version;

        // ---------------------------------------------------------------------
        // Méta-progression : survit au Game Over et au wipe volontaire.
        // ---------------------------------------------------------------------
        public double CpuCycles;
        public double TotalMoney;
        public double TotalComputerPower;
        public double TotalCpuCycles;
        public int TotalDetections;
        public List<UpgradeSaveEntry> PrestigeUpgrades;

        // ---------------------------------------------------------------------
        // Run en cours : remise à zéro au wipe.
        // La Trace et le compteur d'urgence en font partie — sans eux, fermer et rouvrir
        // le jeu vide la jauge gratuitement, ce qui rend le Bouton d'Urgence inutile.
        // ---------------------------------------------------------------------
        public double Money;
        public double ComputerPower;
        public float NormalizedThreat;
        public int EmergencyUsesInRun;
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
            TotalComputerPower = 0d,
            TotalCpuCycles = 0d,
            TotalDetections = 0,
            PrestigeUpgrades = new List<UpgradeSaveEntry>(),

            Money = 10d,
            ComputerPower = 10d,
            NormalizedThreat = 0f,
            EmergencyUsesInRun = 0,
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

            if (Money < 0d) Money = 0d;
            if (ComputerPower < 0d) ComputerPower = 0d;
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
