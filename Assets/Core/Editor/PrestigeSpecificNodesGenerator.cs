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

        /// <summary>Seule source légitime d'upgrades : le dossier que génère UpgradeCatalogGenerator.</summary>
        private const string UpgradeFolder = "Assets/GameData/Upgrades";

        /// <summary>Écartement des arêtes pour les branches à deux ramifications.</summary>
        private const int DefaultColumnStep = 2;

        /// <summary>
        /// Écartement des arêtes de la branche Scripts, qui en porte trois. La troisième part en
        /// diagonale : sans cette case supplémentaire, sa ligne passerait au ras du nœud TEMPS.
        /// </summary>
        private const int ScriptColumnStep = 3;

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
            // Recherche LIMITÉE au dossier généré. Un FindAssets sur tout le projet ramassait
            // Assets/Data/Addressables/Upgrades/NewUpgradeConfig.asset, un orphelin dont l'id
            // vaut « 000 » — d'où trois nœuds de prestige ciblant une upgrade inexistante.
            string[] guids = AssetDatabase.FindAssets("t:UpgradeConfigSO", new[] { UpgradeFolder });
            List<UpgradeConfigSO> allUpgrades = new List<UpgradeConfigSO>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UpgradeConfigSO config = AssetDatabase.LoadAssetAtPath<UpgradeConfigSO>(path);

                if (config == null) continue;

                if (string.IsNullOrEmpty(config.Id))
                {
                    Debug.LogWarning("[Générateur] Asset sans id ignoré : " + path);
                    continue;
                }

                allUpgrades.Add(config);
            }

            if (allUpgrades.Count == 0)
            {
                Debug.LogError("[Générateur] Aucune UpgradeConfigSO trouvée dans " + UpgradeFolder + " !");
                return;
            }

            PrestigeJsonDatabase database = new PrestigeJsonDatabase { items = new List<PrestigeItemData>() };

            // On génère les 3 branches en s'assurant de passer la bonne racine de référence.
            //
            // Les Scripts avancent de TROIS cases par arête, les deux autres de deux. Eux seuls
            // portent quatre nœuds : la quatrième ramification part en diagonale, et à deux cases
            // d'écart sa ligne de liaison frôlerait le nœud TEMPS à cinquante pixels. La branche
            // Scripts est donc plus longue que les deux autres — c'est le prix d'un lien lisible.
            CreateFishboneBranch(database, allUpgrades.Where(u => u.Type == UpgradeType.Script).OrderBy(u => u.Order).ToList(),
                new Vector2Int(-4, 6), _scriptBranchRootId, ScriptColumnStep);

            CreateFishboneBranch(database, allUpgrades.Where(u => u.Type == UpgradeType.Hardware).OrderBy(u => u.Order).ToList(),
                new Vector2Int(-4, 0), _hardwareBranchRootId, DefaultColumnStep);

            CreateFishboneBranch(database, allUpgrades.Where(u => u.Type == UpgradeType.Proxy).OrderBy(u => u.Order).ToList(),
                new Vector2Int(-4, -6), _proxyBranchRootId, DefaultColumnStep);

            // Sauvegarde du fichier JSON
            string jsonOutput = JsonUtility.ToJson(database, true);
            string directory = "Assets/GameData/Editor/PrestigeData";

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, _outputFileName);
            File.WriteAllText(fullPath, jsonOutput);

            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[Générateur]</color> Fichier JSON généré avec succès : {fullPath} ({database.items.Count} nœuds).");
        }

        private void CreateFishboneBranch(PrestigeJsonDatabase db, List<UpgradeConfigSO> sortedUpgrades, Vector2Int startPos, string branchRootPrereqId, int columnStep)
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
                // Pertinent pour les trois types, mais pas avec le même sens : versement du
                // cycle pour un Script, capacité TFlops pour un Hardware, et efficacité de
                // DISSIPATION pour un Proxy — dont le rendement de production vaut zéro.
                string prodId = $"P_UPG_{target.Id}_PROD";
                Vector2Int prodPos = currentPos + new Vector2Int(0, 1);
                db.items.Add(CreateGridItem(prodId, target, "SpecificUpgradeYieldBoost", mainCostId, prodPos));

                // --- 3. RAMIFICATION BAS : RÉDUCTION DE TEMPS, SCRIPTS UNIQUEMENT ---
                // Seuls les Scripts ont un cycle. Générer ce nœud pour un Hardware ou un Proxy
                // (baseCycleDuration = 0) produisait un piège à débutant : un nœud achetable,
                // payé en CPU Cycles, et sans le moindre effet.
                if (target.Type == UpgradeType.Script)
                {
                    string timeId = $"P_UPG_{target.Id}_TIME";
                    Vector2Int timePos = currentPos + new Vector2Int(0, -1);
                    db.items.Add(CreateGridItem(timeId, target, "SpecificUpgradeTimeReduction", mainCostId, timePos));

                    // --- 4. RAMIFICATION DIAGONALE : PALIER D'AUTOMATISATION, SCRIPTS UNIQUEMENT ---
                    // Eux seuls relancent des cycles, donc eux seuls ont quelque chose à automatiser.
                    //
                    // Le prérequis est le nœud COST, comme les deux autres ramifications : les
                    // trois rayonnent depuis la même arête, et c'est ce qui doit se LIRE. La
                    // position part en diagonale parce que la dorsale occupe déjà la gauche et la
                    // droite du nœud principal, PROD le haut et TEMPS le bas — il ne reste que
                    // les diagonales pour un quatrième lien qui ne traverse rien.
                    //
                    // Le bonus vaut 1 : c'est un NOMBRE DE NIVEAUX retirés au palier, pas une
                    // fraction. Seul nœud du jeu dans ce cas, d'où le paramètre explicite.
                    string autoId = $"P_UPG_{target.Id}_AUTO";
                    Vector2Int autoPos = currentPos + new Vector2Int(-1, -1);
                    db.items.Add(CreateGridItem(
                        autoId, target, "SpecificUpgradeAutomationTresholdReduction", mainCostId, autoPos, 1f));
                }

                // --- MISE À JOUR POUR LE PROCHAIN TOUR ---
                // On mémorise le nœud COST actuel pour qu'il devienne le parent du suivant
                lastCostId = mainCostId;

                // On avance sur la grille vers la gauche
                currentPos += new Vector2Int(-columnStep, 0);
            }
        }

        /// <summary>
        /// Ces nœuds ne portent plus de libellé : leur nom est composé à l'affichage à partir
        /// d'un gabarit de localisation et du nom de l'upgrade ciblée, retrouvée via
        /// targetUpgradeId. Écrire ici « Phishing familial (Opti Coût) » remettrait du français
        /// en dur dans les données et obligerait à régénérer tout l'arbre au moindre renommage.
        /// </summary>
        /// <param name="bonus">
        /// Apport par rang. Une FRACTION pour les trois ramifications historiques — 0,1 vaut
        /// −10 % de coût, +10 % de rendement, −10 % de durée. Un NOMBRE DE NIVEAUX pour le nœud
        /// d'automatisation, qui déplace un palier et non un pourcentage.
        /// </param>
        private PrestigeItemData CreateGridItem(string id, UpgradeConfigSO target, string bonusType, string prereqId, Vector2Int pos, float bonus = 0.1f)
        {
            return new PrestigeItemData
            {
                id = id,
                bonusType = bonusType,
                targetUpgradeId = target.Id,
                maxLevel = 5,

                // Prix PLAT, et non indexé sur le palier de l'upgrade ciblée.
                //
                // Il valait 50 × Order, ce qui portait à lui seul 525 389 des 527 552 CPU Cycles
                // de l'arbre — 99,6 % du total, pour un arbre calé sur ~3 500. La campagne
                // plafonnait à 17 % en seize heures, faute de pouvoir rien s'offrir.
                //
                // Le piège est que ce fichier est une SORTIE de générateur : régler les prix dans
                // 04_SpecificUpgrades.json ne survit pas au prochain passage de ce menu. C'est
                // ici, et nulle part ailleurs, que se règle le prix des 120 nœuds spécifiques.
                //
                // La progression par palier n'est pas perdue pour autant : elle est portée par la
                // CHAÎNE DE PRÉREQUIS — le nœud COST d'un palier ouvre le suivant — et par le
                // costMult ci-dessous, qui rend les rangs profonds coûteux.
                baseCost = 1.0,
                costMult = 1.4,
                bonus = bonus,
                posX = pos.x,
                posY = pos.y,
                prerequisiteId = prereqId // Le lien logique crucial
            };
        }
    }
}