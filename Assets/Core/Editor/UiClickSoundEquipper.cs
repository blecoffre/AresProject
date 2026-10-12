using System.Collections.Generic;
using Core.Services.Audio;
using Core.UI.Theme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core.Editor
{
    /// <summary>
    /// Pose un <see cref="UiClickSound"/> câblé sur chaque <see cref="Button"/> du jeu : prefabs
    /// de <c>Assets/Data</c>, puis scènes du Build Settings. Idempotent : le relancer ne touche
    /// qu'à ce qui manque. « Vérifier » fait le même parcours sans rien modifier.
    ///
    /// Les objets issus d'un prefab imbriqué ou d'un prefab de base (variantes) sont laissés à
    /// leur prefab d'origine : c'est lui qui reçoit le composant, et tous ses usages en héritent,
    /// sans override à maintenir.
    /// </summary>
    public static class UiClickSoundEquipper
    {
        private const string PrefabRoot = "Assets/Data";
        private const string ChannelField = "_channel";

        [MenuItem("Tools/Core/Audio/Équiper les boutons du son de clic")]
        private static void Equip() => ShowResult(Run(apply: true));

        [MenuItem("Tools/Core/Audio/Vérifier le son de clic des boutons")]
        private static void Verify() => ShowResult(Run(apply: false));

        private static void ShowResult(string summary)
        {
            if (summary == null) return;
            EditorUtility.DisplayDialog("Son de clic des boutons", summary + "\n\nDétail dans la console.", "OK");
        }

        /// <summary>
        /// Le parcours complet, sans boîte de dialogue : appelable par script (MCP).
        /// Rend le résumé, ou null si le parcours n'a pas pu avoir lieu.
        /// </summary>
        public static string Run(bool apply)
        {
            UiSoundChannelSO channel = FindChannel();
            if (channel == null) return null;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;

            var report = new List<string>();
            int prefabFixes = ProcessPrefabs(channel, apply, report);
            int sceneFixes = ProcessScenes(channel, apply, report);

            int total = prefabFixes + sceneFixes;
            string verb = apply ? "équipé(s)" : "à équiper";
            string summary = total == 0
                ? "Tous les boutons ont leur son de clic."
                : $"{total} bouton(s) {verb} : {prefabFixes} dans les prefabs, {sceneFixes} dans les scènes.";

            foreach (string line in report) Debug.Log($"[AUDIO] {line}");
            Debug.Log($"[AUDIO] {summary}");
            return summary;
        }

        private static UiSoundChannelSO FindChannel()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(UiSoundChannelSO));
            if (guids.Length == 1)
            {
                return AssetDatabase.LoadAssetAtPath<UiSoundChannelSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            Debug.LogError($"[AUDIO] Il faut exactement un UiSoundChannelSO dans le projet ({guids.Length} trouvé(s)).");
            return null;
        }

        private static int ProcessPrefabs(UiSoundChannelSO channel, bool apply, List<string> report)
        {
            int fixes = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    int count = 0;
                    foreach (Button button in contents.GetComponentsInChildren<Button>(true))
                    {
                        // Appartient à un prefab imbriqué ou au prefab de base : traité là-bas.
                        if (PrefabUtility.IsPartOfPrefabInstance(button.gameObject)) continue;
                        if (Ensure(button, channel, apply)) count++;
                    }

                    if (count == 0) continue;

                    fixes += count;
                    report.Add($"{path} : {count} bouton(s)");
                    if (apply) PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return fixes;
        }

        private static int ProcessScenes(UiSoundChannelSO channel, bool apply, List<string> report)
        {
            SceneSetup[] initialSetup = EditorSceneManager.GetSceneManagerSetup();
            int fixes = 0;

            try
            {
                foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                {
                    if (!entry.enabled) continue;

                    Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                    int count = 0;

                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (Button button in root.GetComponentsInChildren<Button>(true))
                        {
                            // Un bouton venu d'un prefab est l'affaire de son prefab : seuls restent
                            // les boutons propres à la scène (ou ajoutés en override sur une instance).
                            if (PrefabUtility.GetCorrespondingObjectFromSource(button) != null) continue;
                            if (Ensure(button, channel, apply))
                            {
                                count++;
                                report.Add($"{entry.path} : {GetPath(button.transform)}");
                            }
                        }
                    }

                    fixes += count;
                    if (apply && count > 0) EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (initialSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(initialSetup);
            }

            return fixes;
        }

        /// <summary>Rend vrai si le bouton n'était pas (ou mal) équipé.</summary>
        private static bool Ensure(Button button, UiSoundChannelSO channel, bool apply)
        {
            UiClickSound sound = button.GetComponent<UiClickSound>();
            if (sound != null)
            {
                var serialized = new SerializedObject(sound);
                SerializedProperty channelProp = serialized.FindProperty(ChannelField);
                if (channelProp.objectReferenceValue != null) return false;

                if (apply)
                {
                    channelProp.objectReferenceValue = channel;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                return true;
            }

            if (!apply) return true;

            sound = button.gameObject.AddComponent<UiClickSound>();
            var created = new SerializedObject(sound);
            created.FindProperty(ChannelField).objectReferenceValue = channel;
            created.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
