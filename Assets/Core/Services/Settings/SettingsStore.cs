using Core.Models.Settings;
using System;
using System.IO;
using UnityEngine;

namespace Core.Services.Settings
{
    /// <summary>
    /// Lecture et écriture de <c>settings.json</c>, à côté de la sauvegarde.
    ///
    /// <b>Synchrone, à dessein.</b> Le fichier fait quelques centaines d'octets, et il n'est
    /// écrit que sur un clic du joueur dans le panneau d'options — jamais dans une boucle de
    /// jeu. Le lire de façon asynchrone obligerait à retarder l'application de la résolution
    /// au-delà de la première frame, pour un gain nul.
    ///
    /// Écriture atomique (fichier temporaire puis remplacement), comme la sauvegarde : un crash
    /// pendant l'écriture laisse l'ancien fichier intact plutôt qu'un fichier tronqué.
    /// </summary>
    public sealed class SettingsStore
    {
        private const string FileName = "settings.json";
        private const string TempFileName = "settings.tmp";

        private readonly string _path;
        private readonly string _tempPath;

        public SettingsStore()
        {
            // persistentDataPath n'est lisible que depuis le thread principal : capturé ici, à
            // la construction par le conteneur.
            string root = Application.persistentDataPath;
            _path = Path.Combine(root, FileName);
            _tempPath = Path.Combine(root, TempFileName);
        }

        /// <summary>Rend null si le fichier n'existe pas ou est illisible : premier lancement.</summary>
        public GameSettings Load()
        {
            if (!File.Exists(_path)) return null;

            try
            {
                return JsonUtility.FromJson<GameSettings>(File.ReadAllText(_path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SETTINGS] settings.json illisible, réglages par défaut : {ex.Message}");
                return null;
            }
        }

        public void Save(GameSettings settings)
        {
            try
            {
                File.WriteAllText(_tempPath, JsonUtility.ToJson(settings, true));

                if (File.Exists(_path)) File.Replace(_tempPath, _path, null, ignoreMetadataErrors: true);
                else File.Move(_tempPath, _path);
            }
            catch (Exception ex)
            {
                // Un réglage non sauvegardé n'est pas grave : il reste appliqué pour la session.
                Debug.LogWarning($"[SETTINGS] Échec d'écriture de settings.json : {ex.Message}");
            }
        }
    }
}
