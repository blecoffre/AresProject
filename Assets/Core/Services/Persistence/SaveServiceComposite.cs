using Core.Models;
using Core.Services.Save;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Core.Services.Persistence
{
    public class SaveServiceComposite : ISaveService
    {
        private readonly SteamCloudSaveService _steamService;
        private readonly LocalJsonSaveService _localService;

        public SaveServiceComposite(SteamCloudSaveService steamService, LocalJsonSaveService localService)
        {
            _steamService = steamService;
            _localService = localService;
        }

        public async UniTask<SaveData> FetchSaveAsync(CancellationToken ct)
        {
            try
            {
                SaveData data = await _steamService.FetchSaveAsync(ct);
                Debug.Log("[SaveServiceComposite] Chargement SteamCloud réussi.");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveServiceComposite] Échec du chargement Steam. Fallback Local. Erreur : {e.Message}");
                return await _localService.FetchSaveAsync(ct);
            }
        }

        public async UniTask SaveAsync(SaveData data, CancellationToken ct)
        {
            bool steamSuccess = false;
            try
            {
                await _steamService.SaveAsync(data, ct);
                Debug.Log("[SaveServiceComposite] Sauvegarde SteamCloud réussie.");
                steamSuccess = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveServiceComposite] Échec de la sauvegarde Steam : {e.Message}");
            }

            if (!steamSuccess)
            {
                try
                {
                    await _localService.SaveAsync(data, ct);
                    Debug.Log("[SaveServiceComposite] Sauvegarde locale (Fallback) réussie.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveServiceComposite] Échec de la sauvegarde de secours locale : {ex.Message}");
                }
            }
        }
    }
}
