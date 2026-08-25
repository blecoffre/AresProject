using Core.UI.Localization;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace Core.Editor
{
    public class LocalizationAutoInjectorEditor : EditorWindow
    {
        [MenuItem("Tools/Localization/Auto-Register Localized Texts in VContainer")]
        public static void ShowWindow()
        {
            GetWindow<LocalizationAutoInjectorEditor>("Auto-Inject Localized Texts");
        }

        private void OnGUI()
        {
            GUILayout.Label("Gestionnaire d'injection automatique", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Ce bouton scanne la scène active à la recherche de tous les composants 'LocalizedText' " +
                "et met à jour la liste 'AutoInjectGameObjects' du LifetimeScope de la scène.",
                MessageType.Info
            );

            EditorGUILayout.Space();

            if (GUILayout.Button("Scanner et Enregistrer", GUILayout.Height(40)))
            {
                ScanAndRegisterInScene();
            }
        }

        private static void ScanAndRegisterInScene()
        {
            // 1. Trouver le LifetimeScope de la scène actuelle
            LifetimeScope scope = Object.FindObjectOfType<LifetimeScope>();
            if (scope == null)
            {
                EditorUtility.DisplayDialog("Erreur", "Aucun LifetimeScope trouvé dans cette scène !", "OK");
                return;
            }

            // 2. Trouver tous les LocalizedText présents dans la scène (même désactivés)
            LocalizedText[] foundLocalizedTexts = Object.FindObjectsOfType<LocalizedText>(true);

            // 3. Préparer la liste cible via la réflexion (car le champ AutoInjectGameObjects est sérialisé)
            SerializedObject serializedScope = new SerializedObject(scope);
            SerializedProperty autoInjectProp = serializedScope.FindProperty("autoInjectGameObjects");

            if (autoInjectProp == null)
            {
                EditorUtility.DisplayDialog("Erreur", "Impossible de trouver la propriété 'autoInjectGameObjects' sur le LifetimeScope.", "OK");
                return;
            }

            // 4. On vide la liste et on la repeuple proprement (zéro doublon, nettoie les supprimés)
            autoInjectProp.ClearArray();

            int addedCount = 0;
            foreach (var locText in foundLocalizedTexts)
            {
                GameObject obj = locText.gameObject;

                // Éviter d'ajouter plusieurs fois le même GameObject s'il a plusieurs textes localisés dessus
                bool alreadyAdded = false;
                for (int i = 0; i < autoInjectProp.arraySize; i++)
                {
                    var element = autoInjectProp.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == obj)
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    autoInjectProp.InsertArrayElementAtIndex(autoInjectProp.arraySize);
                    autoInjectProp.GetArrayElementAtIndex(autoInjectProp.arraySize - 1).objectReferenceValue = obj;
                    addedCount++;
                }
            }

            // 5. Sauvegarder les modifications dans la scène
            serializedScope.ApplyModifiedProperties();
            EditorUtility.SetDirty(scope);

            Debug.Log($"[Localization Auto-Injector] Succès ! {addedCount} GameObject(s) enregistrés dans le LifetimeScope de la scène '{scope.gameObject.scene.name}'.");
            EditorUtility.DisplayDialog("Succès", $"{addedCount} éléments localisés ont été enregistrés pour l'injection automatique.", "OK");
        }
    }
}