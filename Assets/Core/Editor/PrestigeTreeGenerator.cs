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
    public class PrestigeJsonDatabase
    {
        public List<PrestigeItemData> items;
    }

    [Serializable]
    public class PrestigeItemData
    {
        public string id;
        public string bonusType;
        public int maxLevel;
        public double baseCost;
        public double costMult;
        public float bonus;
        public float posX;
        public float posY;
        public string prerequisiteId;
        public string targetUpgradeId; // <- Ajoute cette ligne
    }

    public class PrestigeTreeGenerator : EditorWindow
    {
        private const string TargetFolder = "Assets/GameData/Prestige";

        // NOUVEAU : On pointe vers un dossier, plus vers un fichier précis
        private const string JsonFolderPath = "Assets/GameData/Editor/PrestigeData";
        private const string CatalogAssetPath = "Assets/GameData/Catalogs/PrestigeCatalog.asset";

        [MenuItem("Tools/Core/Générer l'Arbre depuis JSON (Dossier)")]
        public static void GenerateTreeFromFolder()
        {
            EnsureFolderExists(JsonFolderPath);
            EnsureFolderExists(TargetFolder);

            // 1. Scanner le dossier pour trouver tous les fichiers .json
            string[] jsonFiles = Directory.GetFiles(JsonFolderPath, "*.json");

            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"[PrestigeGenerator] Aucun fichier .json trouvé dans {JsonFolderPath}");
                return;
            }

            List<PrestigeItemData> allItems = new List<PrestigeItemData>();

            // 2. Lire et fusionner tous les fichiers
            foreach (string filePath in jsonFiles)
            {
                string jsonContent = File.ReadAllText(filePath);
                PrestigeJsonDatabase database = JsonUtility.FromJson<PrestigeJsonDatabase>(jsonContent);

                if (database != null && database.items != null)
                {
                    allItems.AddRange(database.items);
                }
            }

            if (allItems.Count == 0)
            {
                Debug.LogError("[PrestigeGenerator] Les fichiers JSON sont vides ou mal formatés.");
                return;
            }

            Dictionary<string, PrestigeConfigSO> createdAssets = new Dictionary<string, PrestigeConfigSO>();

            // ==========================================
            // PASSE 1 : Création de tous les ScriptableObjects
            // ==========================================
            foreach (var item in allItems)
            {
                if (Enum.TryParse(item.bonusType, out PrestigeBonusType parsedEnum))
                {
                    var asset = CreateNodeBase(item, parsedEnum);
                    createdAssets[item.id] = asset;
                }
                else
                {
                    Debug.LogError($"[PrestigeGenerator] Type de bonus '{item.bonusType}' invalide pour l'ID {item.id}.");
                }
            }

            // ==========================================
            // PASSE 2 : Câblage des Prérequis entre eux
            // ==========================================
            // Note : Grâce à la fusion, un objet dans Stealth.json peut très bien 
            // avoir comme prérequis un objet situé dans Core.json !
            foreach (var item in allItems)
            {
                if (!string.IsNullOrEmpty(item.prerequisiteId))
                {
                    if (createdAssets.TryGetValue(item.id, out PrestigeConfigSO childAsset) &&
                        createdAssets.TryGetValue(item.prerequisiteId, out PrestigeConfigSO parentAsset))
                    {
                        LinkPrerequisite(childAsset, parentAsset);
                    }
                    else
                    {
                        Debug.LogWarning($"[PrestigeGenerator] Prérequis '{item.prerequisiteId}' introuvable pour '{item.id}'.");
                    }
                }
            }

            // On extrait la liste des valeurs de notre dictionnaire
            List<PrestigeConfigSO> allCreatedConfigs = new List<PrestigeConfigSO>(createdAssets.Values);

            // On injecte tout dans le catalogue !
            UpdateCatalogAsset(allCreatedConfigs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[PrestigeGenerator]</color> Génération terminée ! {allItems.Count} items créés à partir de {jsonFiles.Length} fichiers.");
        }

        /// <summary>
        /// Les trois bonus ciblant une upgrade précise. Leur libellé est composé à l'affichage,
        /// pas stocké.
        /// </summary>
        private static bool IsSpecificBonus(PrestigeBonusType type)
        {
            return type == PrestigeBonusType.SpecificUpgradeCostReduction
                || type == PrestigeBonusType.SpecificUpgradeYieldBoost
                || type == PrestigeBonusType.SpecificUpgradeTimeReduction;
        }

        private static PrestigeConfigSO CreateNodeBase(PrestigeItemData data, PrestigeBonusType type)
        {
            string assetPath = $"{TargetFolder}/{data.id}.asset";

            PrestigeConfigSO asset = AssetDatabase.LoadAssetAtPath<PrestigeConfigSO>(assetPath);
            bool isNew = false;

            if (asset == null)
            {
                asset = CreateInstance<PrestigeConfigSO>();
                isNew = true;
            }

            SerializedObject so = new SerializedObject(asset);

            so.FindProperty("_id").stringValue = data.id;

            // Un nœud « spécifique » n'a pas de nom propre : le presenter compose son libellé à
            // partir d'un gabarit et du nom de l'upgrade ciblée. Lui fabriquer une clé dédiée
            // imposerait 324 entrées de traduction quasi identiques dans fr.json.
            bool isSpecific = IsSpecificBonus(type);

            so.FindProperty("_displayNameKey").stringValue =
                isSpecific ? string.Empty : LocalizationKeys.PrestigeName(data.id);

            so.FindProperty("_displayDescriptionKey").stringValue =
                isSpecific ? string.Empty : LocalizationKeys.PrestigeDescription(data.id);

            so.FindProperty("_bonusType").enumValueIndex = (int)type;
            so.FindProperty("_maxLevel").intValue = data.maxLevel;
            so.FindProperty("_baseCost").doubleValue = data.baseCost;
            so.FindProperty("_costMultiplier").doubleValue = data.costMult;
            so.FindProperty("_bonusPerLevel").floatValue = data.bonus;
            so.FindProperty("_uiPosition").vector2Value = new Vector2(data.posX, data.posY);

            so.FindProperty("_prerequisite").objectReferenceValue = null;
            so.FindProperty("_targetUpgradeId").stringValue = string.IsNullOrEmpty(data.targetUpgradeId) ? "" : data.targetUpgradeId;

            so.ApplyModifiedProperties();

            if (isNew)
            {
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            return asset;
        }

        private static void UpdateCatalogAsset(List<PrestigeConfigSO> configs)
        {
            // On s'assure que le dossier Catalogs existe
            EnsureFolderExists(Path.GetDirectoryName(CatalogAssetPath));

            PrestigeCatalogSO catalog = AssetDatabase.LoadAssetAtPath<PrestigeCatalogSO>(CatalogAssetPath);
            bool isNew = false;

            if (catalog == null)
            {
                catalog = CreateInstance<PrestigeCatalogSO>();
                isNew = true;
            }

            SerializedObject so = new SerializedObject(catalog);

            // ATTENTION : Remplace "_prestiges" par le nom exact de ta liste dans PrestigeCatalogSO.cs
            SerializedProperty listProp = so.FindProperty("_upgrades");

            if (listProp == null)
            {
                Debug.LogError("[PrestigeGenerator] Impossible de trouver la liste dans le PrestigeCatalogSO. Vérifie le nom de la variable !");
                return;
            }

            // On vide la liste existante et on la remplit avec nos objets générés
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

        private static void LinkPrerequisite(PrestigeConfigSO child, PrestigeConfigSO parent)
        {
            SerializedObject so = new SerializedObject(child);
            so.FindProperty("_prerequisite").objectReferenceValue = parent;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(child);
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