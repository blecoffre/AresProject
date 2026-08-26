using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Core.Economy.Editor
{
    /// <summary>
    /// Supprime, dans un dossier 100 % généré, les assets dont l'id a disparu des JSON sources.
    ///
    /// Sans cette passe, renommer ou retirer une entrée laissait un `.asset` orphelin sur le
    /// disque : hors catalogue, donc inerte au runtime, mais accumulé à chaque changement de
    /// schéma — 33 fichiers rien qu'après le dernier remaniement de l'arbre de prestige.
    ///
    /// La purge est sûre parce que ces dossiers n'ont aucune source de vérité autre que les JSON,
    /// mais elle reste ceinture ET bretelles : elle refuse de tourner sur un ensemble d'ids vide,
    /// et journalise nommément chaque suppression.
    /// </summary>
    public static class GeneratedAssetPruner
    {
        /// <summary>
        /// Retire du dossier tout asset dont le nom de fichier n'est pas dans <paramref name="keptIds"/>.
        /// Retourne le nombre de fichiers supprimés.
        /// </summary>
        public static int Prune(string folder, HashSet<string> keptIds, string logPrefix)
        {
            if (keptIds == null || keptIds.Count == 0)
            {
                // Garde-fou capital : si la lecture des JSON a échoué, l'ensemble est vide et une
                // purge naïve effacerait TOUT le dossier généré. On préfère laisser des orphelins.
                Debug.LogError(
                    $"{logPrefix} Purge annulée : aucun id à conserver. " +
                    "Les JSON sources sont probablement illisibles — rien n'a été supprimé.");
                return 0;
            }

            if (!Directory.Exists(folder)) return 0;

            string[] assetPaths = Directory.GetFiles(folder, "*.asset");
            List<string> toDelete = new List<string>();

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string id = Path.GetFileNameWithoutExtension(assetPaths[i]);
                if (!keptIds.Contains(id))
                {
                    toDelete.Add(assetPaths[i].Replace('\\', '/'));
                }
            }

            if (toDelete.Count == 0) return 0;

            for (int i = 0; i < toDelete.Count; i++)
            {
                // DeleteAsset et non File.Delete : c'est ce qui emporte le .meta et tient
                // l'AssetDatabase à jour.
                if (AssetDatabase.DeleteAsset(toDelete[i]))
                {
                    Debug.Log($"{logPrefix} Orphelin supprimé : {Path.GetFileNameWithoutExtension(toDelete[i])}");
                }
                else
                {
                    Debug.LogWarning($"{logPrefix} Suppression refusée par l'AssetDatabase : {toDelete[i]}");
                }
            }

            return toDelete.Count;
        }
    }
}
