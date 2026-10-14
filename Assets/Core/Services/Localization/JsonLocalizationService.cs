using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Core.Services.Localization
{
    public class JsonLocalizationService : ILocalizationService, IDisposable
    {
        private const string DefaultLanguage = "fr";
        private const string FallbackForeignLanguage = "en";

        private readonly Dictionary<string, string> _localizedText = new();
        private readonly List<string> _availableLanguages = new();
        private readonly ReactiveProperty<string> _currentLanguage = new(string.Empty);

        /// <summary>Invariante jusqu'au premier chargement : aucun nombre n'est affiché avant.</summary>
        private CultureInfo _culture = CultureInfo.InvariantCulture;

        public ReadOnlyReactiveProperty<string> CurrentLanguage => _currentLanguage;
        public CultureInfo Culture => _culture;
        public IReadOnlyList<string> AvailableLanguages => _availableLanguages;

        /// <summary>
        /// Les langues proposées sont celles dont un fichier existe : ajouter <c>de.json</c> dans
        /// <c>StreamingAssets/Localization</c> suffit à faire apparaître l'allemand, sans code.
        /// Lecture du dossier légitime ici : la cible est Windows seul, où StreamingAssets est un
        /// vrai dossier (ce ne serait pas le cas sur Android ou WebGL).
        /// </summary>
        public JsonLocalizationService()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "Localization");
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "*.json"))
                {
                    _availableLanguages.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            _availableLanguages.Sort(StringComparer.Ordinal);
        }

        public bool IsAvailable(string languageCode) =>
            !string.IsNullOrEmpty(languageCode) && _availableLanguages.Contains(languageCode);

        /// <summary>
        /// La langue à proposer quand le joueur n'en a pas encore choisi : celle du système si
        /// le jeu la parle, sinon l'anglais, sinon la première disponible.
        /// </summary>
        public string ResolveSystemLanguage()
        {
            string system = Application.systemLanguage switch
            {
                SystemLanguage.French => "fr",
                SystemLanguage.Spanish => "es",
                SystemLanguage.English => "en",
                _ => FallbackForeignLanguage,
            };

            if (IsAvailable(system)) return system;
            if (IsAvailable(FallbackForeignLanguage)) return FallbackForeignLanguage;
            return _availableLanguages.Count > 0 ? _availableLanguages[0] : DefaultLanguage;
        }

        /// <summary>
        /// Charge une langue et, une fois le dictionnaire remplacé, publie
        /// <see cref="CurrentLanguage"/> — c'est ce signal que suivent toutes les vues pour se
        /// réécrire. Le dictionnaire n'est remplacé qu'après une lecture complète et valide :
        /// un fichier corrompu laisse la langue précédente en place plutôt qu'un écran de clés.
        /// </summary>
        public async UniTask LoadLanguageAsync(string languageCode, CancellationToken ct)
        {
            if (!IsAvailable(languageCode))
            {
                Debug.LogError($"[LOCALIZATION] Langue « {languageCode} » indisponible. Fallback sur {DefaultLanguage}.");
                languageCode = DefaultLanguage;
            }

            string filePath = BuildPath(languageCode);

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

                // Avant la notification, comme le dictionnaire : les abonnés reformatent leurs
                // nombres dans leur callback.
                _culture = ResolveCulture(languageCode);

                // En DERNIER : les abonnés relisent le dictionnaire dans leur callback.
                _currentLanguage.Value = languageCode;
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

        /// <summary>
        /// La culture d'une langue : « fr » donne fr-FR. Une culture SPÉCIFIQUE et non neutre, car
        /// une culture neutre ne porte pas toujours de format de nombre. Un code que la plateforme
        /// ne connaît pas retombe sur la culture invariante — des points, jamais une exception.
        /// </summary>
        private static CultureInfo ResolveCulture(string languageCode)
        {
            try
            {
                return CultureInfo.CreateSpecificCulture(languageCode);
            }
            catch (CultureNotFoundException)
            {
                Debug.LogWarning($"[LOCALIZATION] Culture inconnue pour « {languageCode} » : format invariant.");
                return CultureInfo.InvariantCulture;
            }
        }

        public string GetText(string key)
        {
            if (_localizedText.TryGetValue(key, out string value))
                return value;

            return $"[{key}]"; // Fallback visuel si la clé manque
        }

        // TODO (thème Perf) : ces quatre surcharges promettent "Zero Boxing" mais string.Format
        // boxe chaque type valeur. À remplacer par ZString.Format<T0>.
        //
        // La culture de la langue est passée explicitement : sans elle, string.Format prend celle
        // du thread, c'est-à-dire de Windows, et « {0:0.#} » donnait « 1,5 » à un joueur anglais
        // sur un poste français.
        public string GetText<T0>(string key, T0 arg0)
        {
            return string.Format(_culture, GetText(key), arg0);
        }

        public string GetText<T0, T1>(string key, T0 arg0, T1 arg1)
        {
            return string.Format(_culture, GetText(key), arg0, arg1);
        }

        public string GetText<T0, T1, T2>(string key, T0 arg0, T1 arg1, T2 arg2)
        {
            return string.Format(_culture, GetText(key), arg0, arg1, arg2);
        }

        public string GetText<T0, T1, T2, T3>(string key, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            return string.Format(_culture, GetText(key), arg0, arg1, arg2, arg3);
        }

        public void Dispose() => _currentLanguage.Dispose();

        // --- Structures internes pour la sérialisation JSON de Unity ---
        // Champs remplis par JsonUtility, par réflexion : le compilateur ne voit aucune écriture et
        // le signale (CS0649). L'avertissement est faux ici, et seulement ici.
#pragma warning disable CS0649
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
#pragma warning restore CS0649
    }
}
