using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Le poste de pilotage de l'équilibrage : lancer une simulation, lire les courbes, exporter.
    ///
    /// Tout est synchrone. Une campagne de seize heures de jeu se calcule en ~1 seconde, donc
    /// découper le travail en tâches asynchrones coûterait plus de complexité que d'attente.
    ///
    /// Libellés en dur, comme ceux d'un <c>Debug.Log</c> : outillage de développement, jamais
    /// embarqué dans un build joueur.
    /// </summary>
    public sealed class BalanceWindow : EditorWindow
    {
        private enum Mode
        {
            SingleRun,
            Campaign,
            ComparePostures
        }

        private static readonly string[] ModeLabels =
        {
            "Une run", "Campagne complète", "Comparer les 3 postures"
        };

        private static readonly Color MoneyColor = new Color(0.35f, 0.85f, 0.55f);
        private static readonly Color TraceColor = new Color(0.95f, 0.35f, 0.35f);
        private static readonly Color CyclesColor = new Color(0.45f, 0.65f, 1f);
        private static readonly Color DurationColor = new Color(0.95f, 0.75f, 0.3f);

        private Mode _mode = Mode.Campaign;
        private int _strategyIndex = 1;
        private bool _showOptions;
        private Vector2 _scroll;

        private readonly SimulationOptions _options = new SimulationOptions();
        private readonly List<CampaignResult> _results = new List<CampaignResult>(3);
        private long _computeMs;
        private int _selectedRun;

        // Séries réutilisées : OnGUI repasse des dizaines de fois par seconde, rebâtir des
        // listes à chaque image serait du gaspillage pur.
        private readonly List<float> _times = new List<float>(512);
        private readonly List<double> _values = new List<double>(512);
        private readonly List<float> _runIndices = new List<float>(64);
        private readonly List<double> _runValues = new List<double>(64);

        [MenuItem("Tools/Core/Équilibrage/Fenêtre de simulation")]
        public static void Open()
        {
            BalanceWindow window = GetWindow<BalanceWindow>("Équilibrage");
            window.minSize = new Vector2(760f, 600f);
        }

        private static string[] StrategyLabels => new[] { "aucun Proxy", "défense modérée", "défense lourde" };

        private static ISimulationStrategy BuildStrategy(int index)
        {
            switch (index)
            {
                case 0: return GreedyStrategy.NoDefense();
                case 2: return GreedyStrategy.HeavyDefense();
                default: return GreedyStrategy.Balanced();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawOptions();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Aucune simulation lancée. Les mesures tournent sur le code de jeu réel, "
                    + "hors Play Mode — une campagne de seize heures prend environ une seconde.",
                    MessageType.Info);
            }
            else if (_mode == Mode.ComparePostures)
            {
                DrawComparison();
            }
            else
            {
                DrawSingleResult(_results[0]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _mode = (Mode)EditorGUILayout.Popup((int)_mode, ModeLabels,
                                                    EditorStyles.toolbarPopup, GUILayout.Width(180f));

                using (new EditorGUI.DisabledScope(_mode == Mode.ComparePostures))
                {
                    _strategyIndex = EditorGUILayout.Popup(_strategyIndex, StrategyLabels,
                                                           EditorStyles.toolbarPopup, GUILayout.Width(150f));
                }

                if (GUILayout.Button("Lancer", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    Launch();
                }

                using (new EditorGUI.DisabledScope(_results.Count == 0))
                {
                    if (GUILayout.Button("Exporter CSV", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    {
                        Export();
                    }
                }

                GUILayout.FlexibleSpace();

                if (_computeMs > 0)
                {
                    GUILayout.Label($"calcul : {_computeMs} ms", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawOptions()
        {
            _showOptions = EditorGUILayout.Foldout(_showOptions, "Réglages de simulation", true);
            if (!_showOptions) return;

            using (new EditorGUI.IndentLevelScope())
            {
                _options.MaxCampaignHours = EditorGUILayout.FloatField(
                    new GUIContent("Budget de campagne (h)", "Heures de JEU simulées, pas de calcul."),
                    _options.MaxCampaignHours);
                _options.MaxRuns = EditorGUILayout.IntField("Runs maximum", _options.MaxRuns);
                _options.MaxRunSeconds = EditorGUILayout.FloatField(
                    new GUIContent("Plafond d'une run (s)", "Au-delà, la run est coupée et signalée comme incomplète."),
                    _options.MaxRunSeconds);
                _options.ProbeSeconds = EditorGUILayout.FloatField(
                    new GUIContent("Sonde à (s)", "Instant du relevé de Datas, pour surveiller le début de partie."),
                    _options.ProbeSeconds);
                _options.SampleIntervalSeconds = EditorGUILayout.FloatField(
                    new GUIContent("Pas de courbe (s)", "Secondes de jeu entre deux points tracés."),
                    _options.SampleIntervalSeconds);
                _options.DecisionIntervalSeconds = EditorGUILayout.FloatField(
                    new GUIContent("Intervalle de décision (s)", "Fréquence des achats, et latence de relance manuelle."),
                    _options.DecisionIntervalSeconds);
            }
        }

        private void Launch()
        {
            _results.Clear();
            _selectedRun = 0;

            var watch = Stopwatch.StartNew();

            if (_mode == Mode.ComparePostures)
            {
                for (int i = 0; i < 3; i++)
                {
                    _results.Add(SimulationRunner.RunCampaign(BuildStrategy(i), _options));
                }
            }
            else if (_mode == Mode.Campaign)
            {
                _results.Add(SimulationRunner.RunCampaign(BuildStrategy(_strategyIndex), _options));
            }
            else
            {
                ISimulationStrategy strategy = BuildStrategy(_strategyIndex);
                using var harness = new SimulationHarness();
                RunResult run = SimulationRunner.RunOnce(harness, strategy, _options);

                var wrapper = new CampaignResult { StrategyName = strategy.Name };
                wrapper.Runs.Add(run);
                wrapper.TotalSeconds = run.DurationSeconds;
                wrapper.FirstPrestigeSeconds = run.FirstCycleAtSeconds;
                _results.Add(wrapper);
            }

            watch.Stop();
            _computeMs = watch.ElapsedMilliseconds;
        }

        private void Export()
        {
            string path = EditorUtility.SaveFilePanel(
                "Exporter les résultats", Application.dataPath.Replace("/Assets", string.Empty),
                $"ares-equilibrage-{System.DateTime.Now:yyyyMMdd-HHmm}", "csv");

            if (string.IsNullOrEmpty(path)) return;

            var all = new List<RunResult>(64);
            for (int i = 0; i < _results.Count; i++)
            {
                all.AddRange(_results[i].Runs);
            }

            string[] written = BalanceCsvExporter.Export(path, all);
            UnityEngine.Debug.Log($"[Équilibrage] Export : {string.Join("  ·  ", written)}");
        }

        // ------------------------------------------------------------------
        // Rendu des résultats
        // ------------------------------------------------------------------
        private void DrawSingleResult(CampaignResult campaign)
        {
            DrawSummary(campaign);

            if (campaign.Runs.Count == 0) return;

            if (campaign.Runs.Count > 1)
            {
                DrawCampaignCharts(campaign);
                EditorGUILayout.Space(6f);
                _selectedRun = EditorGUILayout.IntSlider(
                    new GUIContent("Run détaillée", "Choisit la run dont les courbes sont tracées ci-dessous."),
                    _selectedRun + 1, 1, campaign.Runs.Count) - 1;
            }

            RunResult run = campaign.Runs[Mathf.Clamp(_selectedRun, 0, campaign.Runs.Count - 1)];
            DrawRunCharts(run);
            DrawRunTable(campaign);
        }

        private void DrawSummary(CampaignResult campaign)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Stratégie : {campaign.StrategyName}", EditorStyles.boldLabel);

                if (campaign.Runs.Count > 1)
                {
                    EditorGUILayout.LabelField($"Runs : {campaign.Runs.Count}");
                    EditorGUILayout.LabelField($"Durée de campagne : {campaign.TotalSeconds / 3600d:0.00} h");
                    EditorGUILayout.LabelField(
                        $"Arbre de prestige : {(campaign.TreeComplete ? "COMPLET" : $"{campaign.TreeProgress * 100f:0.0} %")}");
                }

                EditorGUILayout.LabelField($"Premier CPU Cycle : {FormatSeconds(campaign.FirstPrestigeSeconds)}");

                if (campaign.Runs.Count > 0)
                {
                    RunResult first = campaign.Runs[0];
                    EditorGUILayout.LabelField($"Run 1 — durée : {first.DurationSeconds / 60f:0.0} min");
                    EditorGUILayout.LabelField($"Run 1 — automatisation : {FormatSeconds(first.TimeToAutomation)}");
                    EditorGUILayout.LabelField(
                        $"Run 1 — Datas à {_options.ProbeSeconds / 60f:0} min : "
                        + (first.MoneyAtProbe >= 0d ? first.MoneyAtProbe.ToString("N0") : "run trop courte"));
                    EditorGUILayout.LabelField($"Run 1 — plus haut Script : SCR_{first.TopScriptOrder:00}");
                }
            }
        }

        private void DrawRunCharts(RunResult run)
        {
            EditorGUILayout.LabelField($"Run — {run.DurationSeconds / 60f:0.0} min, {Outcome(run)}",
                                       EditorStyles.boldLabel);

            _times.Clear();
            _values.Clear();
            for (int i = 0; i < run.Samples.Count; i++)
            {
                _times.Add(run.Samples[i].TimeSeconds);
                _values.Add(run.Samples[i].RunMoney);
            }
            BalanceChart.Draw(GUILayoutUtility.GetRect(10f, 130f), "Datas cumulées",
                              _times, _values, MoneyColor, true);

            EditorGUILayout.Space(4f);

            _values.Clear();
            for (int i = 0; i < run.Samples.Count; i++)
            {
                _values.Add(run.Samples[i].TraceFraction);
            }
            BalanceChart.Draw(GUILayoutUtility.GetRect(10f, 90f), "Jauge de Trace",
                              _times, _values, TraceColor, false);
        }

        private void DrawCampaignCharts(CampaignResult campaign)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Progression de campagne", EditorStyles.boldLabel);

            BuildRunSeries(campaign, RunMetric.Money);
            BalanceChart.Draw(GUILayoutUtility.GetRect(10f, 110f), "Datas par run",
                              _runIndices, _runValues, MoneyColor, true);

            EditorGUILayout.Space(4f);
            BuildRunSeries(campaign, RunMetric.Cycles);
            BalanceChart.Draw(GUILayoutUtility.GetRect(10f, 110f), "CPU Cycles par run",
                              _runIndices, _runValues, CyclesColor, true);

            EditorGUILayout.Space(4f);
            BuildRunSeries(campaign, RunMetric.Duration);
            BalanceChart.Draw(GUILayoutUtility.GetRect(10f, 90f), "Durée de run (min)",
                              _runIndices, _runValues, DurationColor, false, " min");
        }

        private enum RunMetric { Money, Cycles, Duration }

        private void BuildRunSeries(CampaignResult campaign, RunMetric metric)
        {
            _runIndices.Clear();
            _runValues.Clear();

            for (int i = 0; i < campaign.Runs.Count; i++)
            {
                RunResult r = campaign.Runs[i];
                _runIndices.Add(i + 1);

                switch (metric)
                {
                    case RunMetric.Money: _runValues.Add(r.RunMoney); break;
                    case RunMetric.Cycles: _runValues.Add(r.CpuCyclesEarned); break;
                    default: _runValues.Add(r.DurationSeconds / 60f); break;
                }
            }
        }

        private void DrawRunTable(CampaignResult campaign)
        {
            if (campaign.Runs.Count < 2) return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Détail par run", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("run", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
                GUILayout.Label("durée", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
                GUILayout.Label("Datas", EditorStyles.miniBoldLabel, GUILayout.Width(100f));
                GUILayout.Label("CPU Cycles", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
                GUILayout.Label("réduc. max", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
                GUILayout.Label("issue", EditorStyles.miniBoldLabel);
            }

            for (int i = 0; i < campaign.Runs.Count; i++)
            {
                RunResult r = campaign.Runs[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(40f));
                    GUILayout.Label($"{r.DurationSeconds / 60f:0.0} min", EditorStyles.miniLabel, GUILayout.Width(70f));
                    GUILayout.Label($"{r.RunMoney:0.00e+00}", EditorStyles.miniLabel, GUILayout.Width(100f));
                    GUILayout.Label($"{r.CpuCyclesEarned:N0}", EditorStyles.miniLabel, GUILayout.Width(90f));
                    GUILayout.Label($"{r.PeakReduction * 100f:0.0} %", EditorStyles.miniLabel, GUILayout.Width(80f));
                    GUILayout.Label(Outcome(r), EditorStyles.miniLabel);
                }
            }
        }

        private void DrawComparison()
        {
            EditorGUILayout.LabelField("Trois postures, mêmes conditions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "L'arbitrage défensif existe si la meilleure posture CHANGE selon le stade. "
                + "Si l'une gagne partout, se défendre est soit inutile, soit obligatoire.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("posture", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
                GUILayout.Label("runs", EditorStyles.miniBoldLabel, GUILayout.Width(50f));
                GUILayout.Label("durée", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
                GUILayout.Label("arbre", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
                GUILayout.Label("Datas finales", EditorStyles.miniBoldLabel);
            }

            for (int i = 0; i < _results.Count; i++)
            {
                CampaignResult c = _results[i];
                double lastMoney = c.Runs.Count > 0 ? c.Runs[c.Runs.Count - 1].RunMoney : 0d;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(c.StrategyName, EditorStyles.miniLabel, GUILayout.Width(120f));
                    GUILayout.Label(c.Runs.Count.ToString(), EditorStyles.miniLabel, GUILayout.Width(50f));
                    GUILayout.Label($"{c.TotalSeconds / 3600d:0.00} h", EditorStyles.miniLabel, GUILayout.Width(70f));
                    GUILayout.Label(c.TreeComplete ? "COMPLET" : $"{c.TreeProgress * 100f:0.0} %",
                                    EditorStyles.miniLabel, GUILayout.Width(80f));
                    GUILayout.Label($"{lastMoney:0.00e+00}", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(8f);
            for (int i = 0; i < _results.Count; i++)
            {
                EditorGUILayout.LabelField(_results[i].StrategyName, EditorStyles.boldLabel);
                BuildRunSeries(_results[i], RunMetric.Cycles);
                BalanceChart.Draw(GUILayoutUtility.GetRect(10f, 90f), "CPU Cycles par run",
                                  _runIndices, _runValues, CyclesColor, true);
                EditorGUILayout.Space(4f);
            }
        }

        private static string Outcome(RunResult run)
        {
            if (run.TimedOut) return "plafond de durée";
            return run.CleanExit ? "exfiltration" : "saisie";
        }

        private static string FormatSeconds(float seconds)
        {
            return seconds < 0f ? "jamais atteint" : $"{seconds:0} s ({seconds / 60f:0.0} min)";
        }
    }
}
