using Core.UI.Localization;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    public class LocalizationAnalyzerWindow : EditorWindow
    {
        private List<string> _jsonKeys = new List<string>();
        private HashSet<string> _usedKeys = new HashSet<string>();
        private List<string> _unusedKeys = new List<string>();
        private List<string> _missingKeys = new List<string>();

        private Vector2 _scrollPosUnused;
        private Vector2 _scrollPosMissing;

        [MenuItem("Tools/Localization/Localization Analyzer")]
        public static void ShowWindow()
        {
            GetWindow<LocalizationAnalyzerWindow>("Loc Analyzer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Analyseur de Clés de Localisation", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Lancer l'Analyse (fr.json)", GUILayout.Height(40)))
            {
                RunAnalysis();
            }

            EditorGUILayout.Space();

            // --- SECTION CLÉS MANQUANTES ---
            GUILayout.Label($"Clés Manquantes (Dans le code/UI, pas dans le JSON) : {_missingKeys.Count}", EditorStyles.boldLabel);
            _scrollPosMissing = EditorGUILayout.BeginScrollView(_scrollPosMissing, GUILayout.Height(150));
            foreach (var key in _missingKeys)
            {
                EditorGUILayout.HelpBox(key, MessageType.Error);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // --- SECTION CLÉS INUTILISÉES ---
            GUILayout.Label($"Clés Inutilisées (Dans le JSON, introuvables) : {_unusedKeys.Count}", EditorStyles.boldLabel);
            _scrollPosUnused = EditorGUILayout.BeginScrollView(_scrollPosUnused, GUILayout.Height(150));
            foreach (var key in _unusedKeys)
            {
                EditorGUILayout.HelpBox(key, MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunAnalysis()
        {
            _jsonKeys.Clear();
            _usedKeys.Clear();
            _unusedKeys.Clear();
            _missingKeys.Clear();

            LoadJsonKeys();
            ScanCSharpScripts();
            ScanPrefabsAndScene();
            CrossReferenceKeys();
        }

        private void LoadJsonKeys()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "Localization", "fr.json");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[Loc Analyzer] Fichier introuvable : {filePath}");
                return;
            }

            string jsonString = File.ReadAllText(filePath);
            var localizationData = JsonUtility.FromJson<LocalizationData>(jsonString);

            if (localizationData != null && localizationData.Items != null)
            {
                foreach (var item in localizationData.Items)
                {
                    if (!string.IsNullOrEmpty(item.Key))
                        _jsonKeys.Add(item.Key);
                }
            }
        }

        private void ScanCSharpScripts()
        {
            // Trouve tous les scripts C# dans le dossier Assets
            string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            // Regex pour trouver _loc.GetText("CLE_ICI") ou _loc.GetText<T>("CLE_ICI", arg)
            Regex regex = new Regex(@"GetText(?:<[^>]+>)?\(\s*""([^""]+)""");

            foreach (string file in csFiles)
            {
                string content = File.ReadAllText(file);
                MatchCollection matches = regex.Matches(content);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        _usedKeys.Add(match.Groups[1].Value);
                    }
                }
            }
        }

        private void ScanPrefabsAndScene()
        {
            // 1. Scan de la scène active
            LocalizedText[] sceneTexts = FindObjectsOfType<LocalizedText>(true);
            foreach (var loc in sceneTexts)
            {
                ExtractKeyFromComponent(loc);
            }

            // 2. Scan de tous les Prefabs du projet
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null)
                {
                    LocalizedText[] prefabTexts = prefab.GetComponentsInChildren<LocalizedText>(true);
                    foreach (var loc in prefabTexts)
                    {
                        ExtractKeyFromComponent(loc);
                    }
                }
            }
        }

        private void ExtractKeyFromComponent(LocalizedText component)
        {
            SerializedObject so = new SerializedObject(component);
            SerializedProperty keyProp = so.FindProperty("_localizationKey");

            if (keyProp != null && !string.IsNullOrEmpty(keyProp.stringValue))
            {
                _usedKeys.Add(keyProp.stringValue);
            }
        }

        private void CrossReferenceKeys()
        {
            // Trouver les manquantes
            foreach (var usedKey in _usedKeys)
            {
                if (!_jsonKeys.Contains(usedKey))
                {
                    _missingKeys.Add(usedKey);
                }
            }

            // Trouver les inutilisées
            foreach (var jsonKey in _jsonKeys)
            {
                if (!_usedKeys.Contains(jsonKey))
                {
                    _unusedKeys.Add(jsonKey);
                }
            }
        }

        // --- Structures internes pour désérialiser ---
        [System.Serializable]
        private class LocalizationData { public List<LocalizationItem> Items; }

        [System.Serializable]
        private class LocalizationItem { public string Key; public string Value; }
    }
}