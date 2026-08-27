using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Core.Services.Localization
{
    public class JsonLocalizationService : ILocalizationService
    {
        private readonly Dictionary<string, string> _localizedText;
        private const string DefaultLanguage = "fr";

        public JsonLocalizationService()
        {
            _localizedText = new Dictionary<string, string>();
        }

        public async UniTask LoadLanguageAsync(string languageCode, CancellationToken ct)
        {
            string filePath = BuildPath(languageCode);

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[LOCALIZATION] Fichier introuvable : {filePath}. Fallback sur {DefaultLanguage}.");
                filePath = BuildPath(DefaultLanguage);
            }

            try
            {
                // Le token traverse jusqu'à l'appel bloquant : fermer le jeu pendant le chargement
                // ne doit pas laisser une lecture disque orpheline.
                string jsonString = await File.ReadAllTextAsync(filePath, ct);

                var localizationData = JsonUtility.FromJson<LocalizationData>(jsonString);
                if (localizationData?.Items == null)
                {
                    Debug.LogError($"[LOCALIZATION] Format inattendu dans {Path.GetFileName(filePath)}.");
                    return;
                }

                _localizedText.Clear();
                foreach (var item in localizationData.Items)
                {
                    _localizedText[item.Key] = item.Value;
                }

                Debug.Log($"[LOCALIZATION] Langue {languageCode} chargée avec {_localizedText.Count} clés.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LOCALIZATION] Erreur de chargement : {e.Message}");
            }
        }

        private static string BuildPath(string languageCode)
        {
            return Path.Combine(Application.streamingAssetsPath, "Localization", $"{languageCode}.json");
        }

        public string GetText(string key)
        {
            if (_localizedText.TryGetValue(key, out string value))
                return value;

            return $"[{key}]"; // Fallback visuel si la clé manque
        }

        // TODO (thème Perf) : ces quatre surcharges promettent "Zero Boxing" mais string.Format
        // boxe chaque type valeur. À remplacer par ZString.Format<T0>.
        public string GetText<T0>(string key, T0 arg0)
        {
            return string.Format(GetText(key), arg0);
        }

        public string GetText<T0, T1>(string key, T0 arg0, T1 arg1)
        {
            return string.Format(GetText(key), arg0, arg1);
        }

        public string GetText<T0, T1, T2>(string key, T0 arg0, T1 arg1, T2 arg2)
        {
            return string.Format(GetText(key), arg0, arg1, arg2);
        }

        public string GetText<T0, T1, T2, T3>(string key, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            return string.Format(GetText(key), arg0, arg1, arg2, arg3);
        }

        // --- Structures internes pour la sérialisation JSON de Unity ---
        [Serializable]
        private class LocalizationData
        {
            public List<LocalizationItem> Items;
        }

        [Serializable]
        private class LocalizationItem
        {
            public string Key;
            public string Value;
        }
    }
}
