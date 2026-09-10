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

        // Conservés uniquement pour DÉTECTER un fichier resté à l'ancien format : les clés sont
        // dérivées de l'id, et un libellé écrit ici serait ignoré en silence.
        public string nameKey;
        public string descKey;

        public string bonusType;
        public int maxLevel;
        public double baseCost;
        public double costMult;
        public float bonus;
        public float posX;
        public float posY;
        public string prerequisiteId;

        /// <summary>
        /// Niveau à atteindre sur le prérequis. 1 = il suffit de le posséder, 0 = son niveau
        /// MAXIMUM (voir <see cref="PrestigeRequirement.RequireParentMaxLevel"/>).
        ///
        /// <b>L'initialisation à 1 est porteuse de sens et ne doit pas être retirée.</b>
        /// JsonUtility construit l'objet avant de le remplir : un champ absent du fichier
        /// conserve donc cette valeur. Sans elle, un champ omis vaudrait 0 — c'est-à-dire
        /// « parent au maximum » — et les cent trente nœuds existants, qui ne déclarent rien,
        /// deviendraient tous silencieusement bien plus durs à ouvrir.
        /// </summary>
        public int requiredLevel = 1;

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
            // Le récapitulatif ci-dessous n'est pas décoratif : c'est le filet de sécurité du
            // champ `requiredLevel`. Absent du JSON, il doit valoir 1 grâce à son initialiseur
            // de champ ; si ce mécanisme venait à céder, TOUS les nœuds tomberaient à 0 —
            // c'est-à-dire « parent au maximum » — et l'arbre deviendrait silencieusement
            // infranchissable. En ne listant que les exigences non triviales, la régression
            // saute aux yeux : la liste passerait de quelques lignes à plus de cent trente.
            List<string> reinforced = new List<string>();

            foreach (var item in allItems)
            {
                if (string.IsNullOrEmpty(item.prerequisiteId)) continue;

                if (createdAssets.TryGetValue(item.id, out PrestigeConfigSO childAsset) &&
                    createdAssets.TryGetValue(item.prerequisiteId, out PrestigeConfigSO parentAsset))
                {
                    int required = ResolveRequiredLevel(item, parentAsset);
                    LinkPrerequisite(childAsset, parentAsset, item.requiredLevel);

                    if (required > 1)
                    {
                        bool followsMax = item.requiredLevel <= PrestigeRequirement.RequireParentMaxLevel;
                        reinforced.Add(
                            $"{item.id} ← {item.prerequisiteId} niveau {required}"
                            + (followsMax ? " (suit le max du parent)" : string.Empty));
                    }
                }
                else
                {
                    Debug.LogWarning($"[PrestigeGenerator] Prérequis '{item.prerequisiteId}' introuvable pour '{item.id}'.");
                }
            }

            if (reinforced.Count > 0)
            {
                Debug.Log($"[PrestigeGenerator] {reinforced.Count} prérequis exigent plus que le premier rang :\n  "
                          + string.Join("\n  ", reinforced));
            }

            // On extrait la liste des valeurs de notre dictionnaire
            List<PrestigeConfigSO> allCreatedConfigs = new List<PrestigeConfigSO>(createdAssets.Values);

            // On injecte tout dans le catalogue !
            UpdateCatalogAsset(allCreatedConfigs);

            // Purge APRÈS la mise à jour du catalogue : si la génération avait échoué en route,
            // on n'aura pas supprimé d'assets encore référencés.
            HashSet<string> keptIds = new HashSet<string>();
            for (int i = 0; i < allItems.Count; i++)
            {
                if (!string.IsNullOrEmpty(allItems[i].id)) keptIds.Add(allItems[i].id);
            }

            int pruned = GeneratedAssetPruner.Prune(TargetFolder, keptIds, "[PrestigeGenerator]");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[PrestigeGenerator]</color> Génération terminée ! {allItems.Count} items créés à partir de {jsonFiles.Length} fichiers, {pruned} orphelin(s) supprimé(s).");
        }

        /// <summary>
        /// Les trois bonus ciblant une upgrade précise. Leur libellé est composé à l'affichage,
        /// pas stocké.
        /// </summary>
        private static bool IsSpecificBonus(PrestigeBonusType type)
        {
            return type == PrestigeBonusType.SpecificUpgradeCostReduction
                || type == PrestigeBonusType.SpecificUpgradeYieldBoost
                || type == PrestigeBonusType.SpecificUpgradeTimeReduction
                || type == PrestigeBonusType.SpecificUpgradeAutomationTresholdReduction;
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

            // Même garde que dans UpgradeCatalogGenerator : un libellé laissé dans le JSON serait
            // ignoré EN SILENCE, et l'auteur croirait l'avoir renseigné.
            if (!isSpecific && (!string.IsNullOrEmpty(data.nameKey) || !string.IsNullOrEmpty(data.descKey)))
            {
                Debug.LogWarning(
                    $"[PrestigeGenerator] '{data.id}' porte encore un champ nameKey/descKey. " +
                    "Il est IGNORÉ : la clé est dérivée de l'id. Déplace ce texte dans " +
                    $"Localization/fr.json sous '{LocalizationKeys.PrestigeName(data.id)}', " +
                    "puis retire le champ du JSON.");
            }

            so.FindProperty("_bonusType").enumValueIndex = (int)type;
            so.FindProperty("_maxLevel").intValue = data.maxLevel;
            so.FindProperty("_baseCost").doubleValue = data.baseCost;
            so.FindProperty("_costMultiplier").doubleValue = data.costMult;
            so.FindProperty("_bonusPerLevel").floatValue = data.bonus;
            so.FindProperty("_uiPosition").vector2Value = new Vector2(data.posX, data.posY);

            // La condition est remise à neuf ici et recâblée en passe 2 : un nœud dont le
            // prérequis disparaît du JSON ne doit pas conserver l'ancien.
            so.FindProperty("_requirement._node").objectReferenceValue = null;
            so.FindProperty("_requirement._requiredLevel").intValue = 1;
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

        /// <summary>
        /// Écrit la condition d'ouverture. Le niveau est stocké <b>brut</b>, sentinelle comprise :
        /// résoudre 0 en un entier figé ici ferait perdre le « suit le maximum du parent », et
        /// l'exigence cesserait de se maintenir toute seule si ce maximum changeait plus tard.
        /// </summary>
        private static void LinkPrerequisite(PrestigeConfigSO child, PrestigeConfigSO parent, int requiredLevel)
        {
            SerializedObject so = new SerializedObject(child);
            so.FindProperty("_requirement._node").objectReferenceValue = parent;
            so.FindProperty("_requirement._requiredLevel").intValue = requiredLevel;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(child);
        }

        /// <summary>
        /// Le niveau réellement exigé, sentinelle résolue. Sert au récapitulatif et à la
        /// détection d'exigences intenables ; la résolution qui fait foi au runtime vit dans
        /// <see cref="PrestigeRequirement.ResolveRequiredLevel"/>.
        /// </summary>
        private static int ResolveRequiredLevel(PrestigeItemData item, PrestigeConfigSO parent)
        {
            if (item.requiredLevel <= PrestigeRequirement.RequireParentMaxLevel) return parent.MaxLevel;

            if (item.requiredLevel > parent.MaxLevel)
            {
                Debug.LogWarning(
                    $"[PrestigeGenerator] '{item.id}' exige le niveau {item.requiredLevel} de " +
                    $"'{item.prerequisiteId}', qui plafonne à {parent.MaxLevel}. L'exigence sera " +
                    "ramenée au maximum du parent — sans quoi la branche resterait fermée pour toujours.");
                return parent.MaxLevel;
            }

            return item.requiredLevel;
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