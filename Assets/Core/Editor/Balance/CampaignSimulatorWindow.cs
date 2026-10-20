using Core.Models.Economy;
using System;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>
    /// La fenêtre du harnais : régler les leviers et le joueur de référence, lancer une campagne,
    /// lire le rapport. Les mêmes résultats que la ligne de commande
    /// (<see cref="CampaignSimulatorBatch"/>), écrits au même endroit.
    ///
    /// Outil de développement : textes en clair, comme les autres fenêtres d'éditeur du projet.
    /// </summary>
    public sealed class CampaignSimulatorWindow : EditorWindow
    {
        private CampaignSettings _settings;
        private string _report = string.Empty;
        private Vector2 _scroll;
        private bool _showPlayer;
        private GUIStyle _reportStyle;

        [MenuItem("Tools/Core/Équilibrage/Simuler une campagne")]
        private static void Open()
        {
            GetWindow<CampaignSimulatorWindow>("Campagne simulée");
        }

        private void OnEnable()
        {
            if (_settings == null) ResetFromAsset();
        }

        /// <summary>La police créée pour le rapport est un objet Unity : elle part avec la fenêtre.</summary>
        private void OnDisable()
        {
            if (_reportStyle?.font != null) DestroyImmediate(_reportStyle.font);
            _reportStyle = null;
        }

        private void ResetFromAsset()
        {
            var balancing = AssetDatabase.LoadAssetAtPath<BalancingConfigSO>(CampaignSimulator.BalancingPath);
            _settings = balancing != null ? CampaignSettings.FromAsset(balancing) : new CampaignSettings();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Leviers du GD (appliqués à une copie du BalancingConfig)", EditorStyles.boldLabel);
            _settings.VictoryTFlops = EditorGUILayout.DoubleField("Victoire (TFlops nettes)", _settings.VictoryTFlops);
            _settings.PrestigeFirstThresholdDatas = EditorGUILayout.DoubleField("Première boîte (Datas)", _settings.PrestigeFirstThresholdDatas);
            _settings.PrestigeThresholdGrowth = EditorGUILayout.DoubleField("Croissance des boîtes", _settings.PrestigeThresholdGrowth);
            _settings.ProxyWipeScale = EditorGUILayout.FloatField("Échelle du Wipe des Proxies", _settings.ProxyWipeScale);
            _settings.AmiSpeedExponent = EditorGUILayout.FloatField("Exposant de l'A.M.I.", _settings.AmiSpeedExponent);

            if (GUILayout.Button("Reprendre les valeurs de l'asset")) ResetFromAsset();

            EditorGUILayout.Space();
            _showPlayer = EditorGUILayout.Foldout(_showPlayer, "Joueur de référence et simulation", true);
            if (_showPlayer) DrawPlayerSettings();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Lancer la campagne", GUILayout.Height(28))) RunCampaign();
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Hors Play Mode seulement : le harnais compose ses propres systèmes.", MessageType.Info);
            }

            if (GUILayout.Button("Ouvrir le dossier des résultats"))
            {
                System.IO.Directory.CreateDirectory(CampaignOutput.DefaultDirectory);
                EditorUtility.RevealInFinder(CampaignOutput.DefaultDirectory);
            }

            EditorGUILayout.Space();
            // Police à chasse fixe : le rapport aligne ses colonnes. Créée une fois, pas à chaque repeint.
            _reportStyle ??= new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 11)
            };

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, _reportStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void DrawPlayerSettings()
        {
            EditorGUI.indentLevel++;
            _settings.FirstRunExitFraction = EditorGUILayout.Slider("Sortie visée, run 1", _settings.FirstRunExitFraction, 0f, 1f);
            _settings.ExitFraction = EditorGUILayout.Slider("Sortie visée ensuite", _settings.ExitFraction, 0f, 1f);
            _settings.DangerFraction = EditorGUILayout.Slider("Urgence (borne pessimiste)", _settings.DangerFraction, 0f, 1f);
            _settings.WiperFraction = EditorGUILayout.Slider("Data Wiper (borne pessimiste)", _settings.WiperFraction, 0f, 1f);
            _settings.UseDataWiper = EditorGUILayout.Toggle("Utiliser le Data Wiper", _settings.UseDataWiper);
            _settings.PreExitOverclocks = EditorGUILayout.IntField("Overclocks avant la sortie", _settings.PreExitOverclocks);
            _settings.PreExitWindow = EditorGUILayout.Slider("Fenêtre sous la sortie", _settings.PreExitWindow, 0f, 0.3f);
            _settings.EscapeOverclockReach = EditorGUILayout.IntField("Évasion : point à N appuis", _settings.EscapeOverclockReach);
            _settings.ProxyPaybackFactor = EditorGUILayout.FloatField("Proxy : revenu ≥ prix ×", _settings.ProxyPaybackFactor);
            _settings.ProxyWasteTolerance = EditorGUILayout.Slider("Proxy : part utile du Wipe", _settings.ProxyWasteTolerance, 0f, 1f);
            _settings.MaxProxiesPerRun = EditorGUILayout.IntField("Proxies par run (−1 = sans plafond)", _settings.MaxProxiesPerRun);
            _settings.SaveForPriorityNode = EditorGUILayout.Toggle("Arbre : économiser pour le prioritaire", _settings.SaveForPriorityNode);
            _settings.DecisionIntervalSeconds = EditorGUILayout.FloatField("Décision toutes les (s)", _settings.DecisionIntervalSeconds);
            _settings.SecondsBetweenRuns = EditorGUILayout.FloatField("Écran de fin (s par run)", _settings.SecondsBetweenRuns);
            _settings.StepSeconds = EditorGUILayout.FloatField("Pas de simulation (s)", _settings.StepSeconds);
            _settings.Seed = EditorGUILayout.IntField("Graine", _settings.Seed);
            _settings.TraceRuns = EditorGUILayout.IntField("Runs tracées geste par geste", _settings.TraceRuns);
            _settings.MaxRuns = EditorGUILayout.IntField("Borne : runs", _settings.MaxRuns);
            _settings.MaxCampaignHours = EditorGUILayout.FloatField("Borne : heures de campagne", _settings.MaxCampaignHours);
            _settings.MaxRunMinutes = EditorGUILayout.FloatField("Borne : minutes par run", _settings.MaxRunMinutes);
            EditorGUI.indentLevel--;
        }

        private void RunCampaign()
        {
            try
            {
                CampaignResult result = CampaignSimulator.Run(_settings,
                    (progress, label) => EditorUtility.DisplayProgressBar("Campagne simulée", label, progress));

                _report = CampaignOutput.Write(result, CampaignOutput.DefaultDirectory, JournalRun.DefaultPath, out _, out _);
                _scroll = Vector2.zero;
                Debug.Log("[SIMULATION]\n" + _report);
            }
            catch (Exception ex)
            {
                _report = "Échec de la simulation :\n" + ex;
                Debug.LogError("[SIMULATION] Échec : " + ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
