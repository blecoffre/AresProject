using Core.Economy.Editor;
using Core.Models.Economy;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Economy.Tools
{
    public class PrestigeSpecificNodesGenerator : EditorWindow
    {
        private string _scriptBranchRootId = "P_REFAC";
        private string _hardwareBranchRootId = "P_CLICK";
        private string _proxyBranchRootId = "P_STEALTH";
        private string _outputFileName = "04_SpecificUpgrades.json";

        [MenuItem("Tools/Core/Générer JSON (Grille - Arête de poisson Corrigée)")]
        public static void ShowWindow()
        {
            GetWindow<PrestigeSpecificNodesGenerator>("Générateur Grille Corrigé");
        }

        private void OnGUI()
        {
            GUILayout.Label("Paramètres de Génération (Arête de poisson)", EditorStyles.boldLabel);

            _scriptBranchRootId = EditorGUILayout.TextField("Racine Branche Scripts", _scriptBranchRootId);
            _hardwareBranchRootId = EditorGUILayout.TextField("Racine Branche Hardware", _hardwareBranchRootId);
            _proxyBranchRootId = EditorGUILayout.TextField("Racine Branche Proxies", _proxyBranchRootId);

            _outputFileName = EditorGUILayout.TextField("Nom du Fichier Sortie", _outputFileName);

            EditorGUILayout.Space();

            if (GUILayout.Button("Générer le Fichier JSON & Vérifier", GUILayout.Height(40)))
            {
                GenerateGridJson();
            }
        }

        private void GenerateGridJson()
        {
            string[] guids = AssetDatabase.FindAssets("t:UpgradeConfigSO");
            List<UpgradeConfigSO> allUpgrades = new List<UpgradeConfigSO>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UpgradeConfigSO config = AssetDatabase.LoadAssetAtPath<UpgradeConfigSO>(path);
                if (config != null) allUpgrades.Add(config);
            }

            if (allUpgrades.Count == 0)
            {
                Debug.LogError("[Générateur] Aucune UpgradeConfigSO trouvée dans le projet !");
                return;
            }

            PrestigeJsonDatabase database = new PrestigeJsonDatabase { items = new List<PrestigeItemData>() };

            // On génère les 3 branches en s'assurant de passer la bonne racine de référence
            CreateFishboneBranch(database, allUpgrades.Where(u => u.Type == UpgradeType.Script).OrderBy(u => u.Order).ToList(),
                new Vector2Int(-4, 6), _scriptBranchRootId);

            CreateFishboneBranch(database, allUpgrades.Where(u => u.Type == UpgradeType.Hardware).OrderBy(u => u.Order).ToList(),
                new Vector2Int(-4, 0), _hardwareBranchRootId);

            CreateFishboneBranch(database, allUpgrades.Where(u => u.Type == UpgradeType.Proxy).OrderBy(u => u.Order).ToList(),
                new Vector2Int(-4, -6), _proxyBranchRootId);

            // Sauvegarde du fichier JSON
            string jsonOutput = JsonUtility.ToJson(database, true);
            string directory = "Assets/GameData/Editor/PrestigeData";

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, _outputFileName);
            File.WriteAllText(fullPath, jsonOutput);

            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[Générateur]</color> Fichier JSON généré avec succès : {fullPath} ({database.items.Count} nœuds).");
        }

        private void CreateFishboneBranch(PrestigeJsonDatabase db, List<UpgradeConfigSO> sortedUpgrades, Vector2Int startPos, string branchRootPrereqId)
        {
            // Variable claire pour stocker l'ID du nœud COST précédent
            string lastCostId = branchRootPrereqId;
            Vector2Int currentPos = startPos;

            for (int i = 0; i < sortedUpgrades.Count; i++)
            {
                UpgradeConfigSO target = sortedUpgrades[i];

                // --- 1. DORSALE : RÉDUCTION DU COÛT ---
                string mainCostId = $"P_UPG_{target.Id}_COST";

                // Le tout premier s'accroche au root de la branche. 
                // Tous les suivants s'accrochent STRICTEMENT au dernier mainCostId créé.
                string currentPrereq = (i == 0) ? branchRootPrereqId : lastCostId;

                db.items.Add(CreateGridItem(mainCostId, target, "SpecificUpgradeCostReduction", currentPrereq, currentPos));

                // --- 2. RAMIFICATION HAUT : BOOST DE RENDEMENT ---
                string prodId = $"P_UPG_{target.Id}_PROD";
                Vector2Int prodPos = currentPos + new Vector2Int(0, 1);
                db.items.Add(CreateGridItem(prodId, target, "SpecificUpgradeYieldBoost", mainCostId, prodPos));

                // --- 3. RAMIFICATION BAS : RÉDUCTION DE TEMPS ---
                string timeId = $"P_UPG_{target.Id}_TIME";
                Vector2Int timePos = currentPos + new Vector2Int(0, -1);
                db.items.Add(CreateGridItem(timeId, target, "SpecificUpgradeTimeReduction", mainCostId, timePos));

                // --- MISE À JOUR POUR LE PROCHAIN TOUR ---
                // On mémorise le nœud COST actuel pour qu'il devienne le parent du suivant
                lastCostId = mainCostId;

                // On avance sur la grille vers la gauche
                currentPos += new Vector2Int(-2, 0);
            }
        }

        /// <summary>
        /// Ces nœuds ne portent plus de libellé : leur nom est composé à l'affichage à partir
        /// d'un gabarit de localisation et du nom de l'upgrade ciblée, retrouvée via
        /// targetUpgradeId. Écrire ici « Phishing familial (Opti Coût) » remettrait du français
        /// en dur dans les données et obligerait à régénérer tout l'arbre au moindre renommage.
        /// </summary>
        private PrestigeItemData CreateGridItem(string id, UpgradeConfigSO target, string bonusType, string prereqId, Vector2Int pos)
        {
            return new PrestigeItemData
            {
                id = id,
                bonusType = bonusType,
                targetUpgradeId = target.Id,
                maxLevel = 5,
                baseCost = 50.0 * target.Order,
                costMult = 1.4,
                bonus = 0.1f,
                posX = pos.x,
                posY = pos.y,
                prerequisiteId = prereqId // Le lien logique crucial
            };
        }
    }
}