using Core.Economy.Data;
using Core.Models.Economy;
using Core.Services.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Core.Economy.Editor
{
    [Serializable]
    public class UpgradeJsonDatabase
    {
        public List<UpgradeItemData> items;
    }

    [Serializable]
    public class MilestoneJsonData
    {
        public int level;
        public string effect; // "YieldMultiplier" ou "DurationMultiplier"
        public float factor;
    }

    [Serializable]
    public class UpgradeItemData
    {
        public string id;

        // Conservés uniquement pour DÉTECTER un fichier resté à l'ancien format : les clés sont
        // désormais dérivées de l'id, et un libellé écrit ici serait ignoré en silence.
        public string displayNameKey;
        public string displayName;

        public string type;        // "Script", "Hardware", "Proxy"
        public int order;
        public double baseCost;
        public double costMultiplier;
        public double baseProductionYield;
        public double traceGeneratedPerSecond;

        // Cycle
        public float baseCycleDuration;
        public float minCycleDuration;
        public float durationReductionPerLevel; // Obsolète, recopié pour ne pas perdre la donnée.

        // Automatisation et paliers
        public int automationLevel;
        public List<MilestoneJsonData> milestones;
    }

    public class UpgradeCatalogGenerator : EditorWindow
    {
        private const string TargetFolder = "Assets/GameData/Upgrades";
        private const string JsonFolderPath = "Assets/GameData/Editor/UpgradeData";
        private const string CatalogAssetPath = "Assets/GameData/Catalogs/UpgradeCatalog.asset";

        /// <summary>Valeurs de repli quand le JSON ne renseigne pas le champ (fichiers antérieurs).</summary>
        private const float DefaultCycleDuration = 1f;
        private const float DefaultMinCycleDuration = 0.1f;
        private const int DefaultAutomationLevel = 10;

        [MenuItem("Tools/Core/Générer Catalogue Upgrades depuis JSON")]
        public static void GenerateCatalogFromFolder()
        {
            EnsureFolderExists(JsonFolderPath);
            EnsureFolderExists(TargetFolder);

            string[] jsonFiles = Directory.GetFiles(JsonFolderPath, "*.json");
            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"[UpgradeGenerator] Aucun fichier .json trouvé dans {JsonFolderPath}");
                return;
            }

            List<UpgradeItemData> allItems = new List<UpgradeItemData>();

            foreach (string filePath in jsonFiles)
            {
                string jsonContent = File.ReadAllText(filePath);
                UpgradeJsonDatabase database = JsonUtility.FromJson<UpgradeJsonDatabase>(jsonContent);

                if (database != null && database.items != null)
                {
                    allItems.AddRange(database.items);
                }
            }

            if (allItems.Count == 0)
            {
                Debug.LogError("[UpgradeGenerator] Les fichiers JSON sont vides ou mal formatés.");
                return;
            }

            allItems.Sort((a, b) => a.order.CompareTo(b.order));

            List<UpgradeConfigSO> createdConfigs = new List<UpgradeConfigSO>();
            int milestoneCount = 0;

            foreach (var item in allItems)
            {
                if (!Enum.TryParse(item.type, out UpgradeType parsedType))
                {
                    Debug.LogError($"[UpgradeGenerator] Type d'amélioration '{item.type}' invalide pour l'ID {item.id}.");
                    continue;
                }

                createdConfigs.Add(CreateOrUpdateUpgradeConfig(item, parsedType));
                milestoneCount += item.milestones?.Count ?? 0;
            }

            UpdateCatalogAsset(createdConfigs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[UpgradeGenerator]</color> Catalogue généré : {createdConfigs.Count} améliorations, {milestoneCount} paliers.");
        }

        private static UpgradeConfigSO CreateOrUpdateUpgradeConfig(UpgradeItemData data, UpgradeType type)
        {
            string assetPath = $"{TargetFolder}/{data.id}.asset";

            UpgradeConfigSO asset = AssetDatabase.LoadAssetAtPath<UpgradeConfigSO>(assetPath);
            bool isNew = false;

            if (asset == null)
            {
                asset = CreateInstance<UpgradeConfigSO>();
                isNew = true;
            }

            SerializedObject so = new SerializedObject(asset);

            so.FindProperty("_id").stringValue = data.id;

            // Les clés se DÉRIVENT de l'id, elles ne sont plus lues depuis le JSON : c'est ce qui
            // rend impossible une désynchronisation entre la donnée et la table de localisation.
            so.FindProperty("_displayNameKey").stringValue = LocalizationKeys.UpgradeName(data.id);
            so.FindProperty("_displayDescriptionKey").stringValue = LocalizationKeys.UpgradeDescription(data.id);

            if (!string.IsNullOrEmpty(data.displayName) || !string.IsNullOrEmpty(data.displayNameKey))
            {
                Debug.LogWarning(
                    $"[UpgradeGenerator] '{data.id}' porte encore un champ displayName/displayNameKey. " +
                    "Il est IGNORÉ : la clé est dérivée de l'id. Déplace ce texte dans " +
                    $"Localization/fr.json sous '{LocalizationKeys.UpgradeName(data.id)}', puis retire le champ du JSON.");
            }

            so.FindProperty("_type").enumValueIndex = (int)type;
            so.FindProperty("_order").intValue = data.order;
            so.FindProperty("_baseCost").doubleValue = data.baseCost;
            so.FindProperty("_costMultiplier").doubleValue = data.costMultiplier;
            so.FindProperty("_baseProductionYield").doubleValue = data.baseProductionYield;
            so.FindProperty("_traceGeneratedPerSecond").doubleValue = data.traceGeneratedPerSecond;

            // Ces trois champs étaient déjà écrits dans les JSON mais n'étaient jamais recopiés :
            // tous les générateurs héritaient donc de la durée par défaut du ScriptableObject.
            so.FindProperty("_baseCycleDuration").floatValue =
                data.baseCycleDuration > 0f ? data.baseCycleDuration : DefaultCycleDuration;

            so.FindProperty("_minCycleDuration").floatValue =
                data.minCycleDuration > 0f ? data.minCycleDuration : DefaultMinCycleDuration;

            so.FindProperty("_durationReductionPerLevel").floatValue = data.durationReductionPerLevel;

            so.FindProperty("_automationLevel").intValue =
                data.automationLevel > 0 ? data.automationLevel : DefaultAutomationLevel;

            WriteMilestones(so.FindProperty("_milestones"), data);

            so.ApplyModifiedProperties();

            if (isNew)
            {
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            return asset;
        }

        private static void WriteMilestones(SerializedProperty listProp, UpgradeItemData data)
        {
            listProp.ClearArray();

            if (data.milestones == null || data.milestones.Count == 0) return;

            // Tri par niveau : le modèle applique les paliers dans l'ordre du tableau, et les
            // effets étant multiplicatifs, un ordre incohérent rendrait l'équilibrage illisible.
            data.milestones.Sort((a, b) => a.level.CompareTo(b.level));

            listProp.arraySize = data.milestones.Count;

            for (int i = 0; i < data.milestones.Count; i++)
            {
                MilestoneJsonData milestone = data.milestones[i];
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);

                if (!Enum.TryParse(milestone.effect, out MilestoneEffect parsedEffect))
                {
                    Debug.LogError(
                        $"[UpgradeGenerator] Effet de palier '{milestone.effect}' inconnu pour {data.id} " +
                        $"(niveau {milestone.level}). Attendu : YieldMultiplier ou DurationMultiplier.");
                    parsedEffect = MilestoneEffect.YieldMultiplier;
                }

                element.FindPropertyRelative("Level").intValue = milestone.level;
                element.FindPropertyRelative("Effect").enumValueIndex = (int)parsedEffect;
                element.FindPropertyRelative("Factor").floatValue = milestone.factor <= 0f ? 1f : milestone.factor;
            }
        }

        private static void UpdateCatalogAsset(List<UpgradeConfigSO> configs)
        {
            EnsureFolderExists(Path.GetDirectoryName(CatalogAssetPath));

            UpgradeCatalogSO catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalogSO>(CatalogAssetPath);
            bool isNew = false;

            if (catalog == null)
            {
                catalog = CreateInstance<UpgradeCatalogSO>();
                isNew = true;
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty listProp = so.FindProperty("_upgrades");

            listProp.ClearArray();
            listProp.arraySize = configs.Count;

            for (int i = 0; i < configs.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = configs[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);

            if (isNew)
            {
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }
        }

        private static void EnsureFolderExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
