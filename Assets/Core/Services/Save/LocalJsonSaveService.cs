using Core.Models;
using Core.Services.Persistence;
using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Core.Services.Save
{
    /// <summary>
    /// Sauvegarde locale sur disque, en JSON lisible.
    ///
    /// Le fichier reste éditable à la main : c'est un choix assumé tant que le jeu n'est pas
    /// publié, parce que pouvoir forcer un état de partie fait gagner des heures sur l'équilibrage.
    /// Le durcissement (somme de contrôle) se fera au moment de la sortie Steam.
    ///
    /// L'écriture est atomique : on écrit d'abord un fichier temporaire complet, puis on bascule.
    /// Avec un autosave toutes les 5 minutes, une coupure pendant l'écriture est une question de
    /// temps — sans cette bascule, elle laisserait un JSON tronqué et une progression perdue.
    /// </summary>
    public class LocalJsonSaveService : ISaveService, ISyncSaveService
    {
        private const string FileName = "savegame.json";
        private const string BackupFileName = "savegame.bak";
        private const string AsyncTempFileName = "savegame.tmp";
        private const string QuitTempFileName = "savegame.quit.tmp";

        private readonly string _path;
        private readonly string _backupPath;
        private readonly string _asyncTempPath;
        private readonly string _quitTempPath;

        public LocalJsonSaveService()
        {
            // Application.persistentDataPath n'est lisible que depuis le thread principal.
            // Le conteneur construit ce service au démarrage, donc au bon endroit : on capture
            // les chemins une fois pour toutes plutôt que de les recalculer à chaque écriture.
            string root = Application.persistentDataPath;

            _path = Path.Combine(root, FileName);
            _backupPath = Path.Combine(root, BackupFileName);
            _asyncTempPath = Path.Combine(root, AsyncTempFileName);
            _quitTempPath = Path.Combine(root, QuitTempFileName);
        }

        public async UniTask<SaveData> FetchSaveAsync(CancellationToken ct)
        {
            SaveData data = await TryReadAsync(_path, ct);

            if (data == null)
            {
                data = await TryReadAsync(_backupPath, ct);

                if (data != null)
                {
                    Debug.LogWarning("[SAVE] Fichier principal illisible. Restauration depuis la sauvegarde de secours.");
                }
            }

            if (data == null)
            {
                Debug.Log("[SAVE] Aucune sauvegarde exploitable. Création d'une nouvelle partie.");
                return SaveData.CreateNew();
            }

            return SaveData.Migrate(data);
        }

        private async UniTask<SaveData> TryReadAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path)) return null;

            try
            {
                string json = await File.ReadAllTextAsync(path, ct);
                if (string.IsNullOrWhiteSpace(json)) return null;

                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return null;

                data.EnsureValid();
                return data;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SAVE] Lecture impossible ({Path.GetFileName(path)}) : {e.Message}");
                return null;
            }
        }

        public async UniTask SaveAsync(SaveData data, CancellationToken ct)
        {
            // Sérialisation immédiate et synchrone : l'appelant réutilise son instance de SaveData
            // d'un autosave à l'autre. Si on attendait après un await pour lire ses champs, on
            // écrirait un mélange de deux états.
            string json = Serialize(data);

            try
            {
                await File.WriteAllTextAsync(_asyncTempPath, json, ct);
                Commit(_asyncTempPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SAVE] Écriture impossible : {e.Message}");
            }
        }

        public void SaveBlocking(SaveData data)
        {
            // Fichier temporaire distinct de la voie asynchrone : à la fermeture, une écriture
            // d'autosave peut être encore en vol sur _asyncTempPath.
            try
            {
                File.WriteAllText(_quitTempPath, Serialize(data));
                Commit(_quitTempPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SAVE] Écriture de fermeture impossible : {e.Message}");
            }
        }

        private static string Serialize(SaveData data)
        {
            data.Version = SaveData.CurrentVersion;
            data.SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // "true" = pretty print. Coût négligeable une fois toutes les 5 minutes, et le fichier
            // reste lisible pour le débogage et l'équilibrage.
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// Bascule atomique. Le fichier définitif n'est remplacé qu'une fois le temporaire
        /// intégralement écrit, et l'ancienne version part en .bak — qui devient le filet de
        /// sécurité relu par FetchSaveAsync si le fichier principal est corrompu.
        /// </summary>
        private void Commit(string tempPath)
        {
            if (File.Exists(_path))
            {
                File.Replace(tempPath, _path, _backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _path);
            }
        }
    }
}
