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

        /// <summary>
        /// Génère le fichier avec les réglages par défaut, sans ouvrir la fenêtre.
        ///
        /// La génération n'était accessible que par un bouton, donc impossible à déclencher
        /// autrement qu'à la main — et ce fichier étant une SORTIE, toute retouche de son JSON
        /// était condamnée au prochain passage. Le piège avait déjà coûté deux sessions.
        /// </summary>
        [MenuItem("Tools/Core/Générer les nœuds ciblés (04_SpecificUpgrades)")]
        public static void GenerateFromMenu()
        {
            CreateInstance<PrestigeSpecificNodesGenerator>().GenerateGridJson();
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

            // Les trois branches avancent au même pas depuis que chaque générateur ne porte
            // plus que DEUX nœuds. Les Scripts avaient besoin d'une case de plus tant qu'ils en
            // portaient quatre, la ramification diagonale frôlant sinon le nœud voisin.
            CreateFishboneBranch(database, allUpgrades.Where(u => u.Type == UpgradeType.Script).OrderBy(u => u.Order).ToList(),
                new Vector2Int(-4, 6), _scriptBranchRootId, DefaultColumnStep);

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

                // --- DEUX NŒUDS PAR GÉNÉRATEUR, ET PAS QUATRE ---
                //
                // Les Scripts en avaient deux de plus : réduction de DURÉE et abaissement du
                // palier d'AUTOMATISATION. Tous deux supprimés le 2026-09-15, pour rendre les
                // nœuds restants significatifs plutôt que d'éparpiller le même budget sur quatre.
                //
                // La DURÉE faisait doublon : c'est le rôle du Hardware, via la compression par
                // TFlops, et la ladder de paliers des Scripts en donne déjà à 10, 50 et 150. On
                // venait de passer des semaines à séparer les rôles des trois piliers ; ce nœud
                // les remélangeait. Son apport est reversé dans le RENDEMENT, qui double
                // désormais au lieu de majorer de moitié.
                //
                // L'AUTOMATISATION devient un nœud GLOBAL unique, écrit à la main. Mesuré avant
                // la fusion, sa famille pesait NÉGATIVEMENT sur la puissance d'une campagne : elle
                // ne porte pas de production, elle porte un objectif — celui d'arrêter de cliquer.
                // Un objectif se donne une fois, pas quinze.

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
        private PrestigeItemData CreateGridItem(string id, UpgradeConfigSO target, string bonusType, string prereqId, Vector2Int pos, float bonus = 1.0f)
        {
            return new PrestigeItemData
            {
                id = id,
                bonusType = bonusType,
                targetUpgradeId = target.Id,

                // Nœud à RANG UNIQUE depuis le 2026-09-13, et son bonus porte d'un coup ce que
                // cinq rangs apportaient. Même puissance totale, mais un point de prestige achète
                // cinq fois plus — c'est ce qui rend le point rare ET utile.
                //
                // Mesuré avant ce changement : l'arbre valait 783 niveaux quand une campagne n'en
                // finançait que quelques dizaines. Le joueur achetait 1 % de l'arbre, sa
                // production ne bougeait donc pas, et comme les paliers de points montent en
                // exponentielle pendant que le cumul monte linéairement, la progression se
                // bloquait DÉFINITIVEMENT au bout de sept runs.
                maxLevel = 1,

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