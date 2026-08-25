using Core.Models;
using Cysharp.Threading.Tasks;
using Steamworks;
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Core.Services.Persistence
{
    public class SteamCloudSaveService : ISaveService
    {
        private const string _saveFileName = "core_exe_save_v1.json";
        private readonly string _localFallbackPath;

        public SteamCloudSaveService()
        {
            // Chemin de secours si Steam est fermé : C:/Users/.../AppData/LocalLow/TonCompagny/TonJeu/
            _localFallbackPath = Path.Combine(Application.persistentDataPath, _saveFileName);
        }

        public async UniTask<SaveData> FetchSaveAsync(CancellationToken ct)
        {
            // On rend la main à Unity pour la frame courante (évite un micro-freeze)
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            try
            {
                string json = string.Empty;

                // 1. Tentative Steam Cloud
                if (SteamManager.Initialized)
                {
                    if (SteamRemoteStorage.FileExists(_saveFileName))
                    {
                        int fileSize = SteamRemoteStorage.GetFileSize(_saveFileName);
                        byte[] bytes = new byte[fileSize];

                        SteamRemoteStorage.FileRead(_saveFileName, bytes, fileSize);
                        json = Encoding.UTF8.GetString(bytes);

                        Debug.Log("[SAVE] Sauvegarde chargée depuis le Steam Cloud.");
                    }
                }
                // 2. Fallback Local (Développement ou Mode Hors Ligne)
                else if (File.Exists(_localFallbackPath))
                {
                    json = await File.ReadAllTextAsync(_localFallbackPath, ct);
                    Debug.Log("[SAVE] Steam inactif. Sauvegarde locale chargée.");
                }

                // 3. Désérialisation ou Nouvelle partie
                if (!string.IsNullOrEmpty(json))
                {
                    return JsonUtility.FromJson<SaveData>(json);
                }

                Debug.Log("[SAVE] Aucune sauvegarde trouvée. Création d'une nouvelle partie.");
                return SaveData.CreateNew();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SAVE] Erreur critique lors du chargement : {ex.Message}");
                // En cas de corruption, on retourne une sauvegarde vierge pour ne pas bloquer le jeu.
                return SaveData.CreateNew();
            }
        }

        public async UniTask SaveAsync(SaveData data, CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            try
            {
                // Allocation inévitable mais contenue hors boucle chaude
                string json = JsonUtility.ToJson(data);

                if (SteamManager.Initialized)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    bool success = SteamRemoteStorage.FileWrite(_saveFileName, bytes, bytes.Length);

                    if (success)
                        Debug.Log("[SAVE] Sauvegarde synchronisée sur Steam Cloud.");
                    else
                        Debug.LogError("[SAVE] Échec de l'écriture sur Steam Cloud.");
                }
                else
                {
                    // Fallback écriture locale
                    await File.WriteAllTextAsync(_localFallbackPath, json, ct);
                    Debug.Log("[SAVE] Steam inactif. Sauvegarde écrite en local.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SAVE] Erreur critique lors de la sauvegarde : {ex.Message}");
            }
        }
    }
}