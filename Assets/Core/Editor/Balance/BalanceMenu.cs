using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Entrées de menu du harnais d'équilibrage.
    ///
    /// Les libellés sont en dur, comme ceux d'un <c>Debug.Log</c> : c'est de l'outillage de
    /// développement, jamais embarqué dans un build joueur. La règle de localisation du projet
    /// vise le texte qui atteint l'écran du joueur — si tu préfères l'étendre à l'éditeur, dis-le
    /// et je passerai ces libellés par des clés.
    /// </summary>
    public static class BalanceMenu
    {
        [MenuItem("Tools/Core/Équilibrage/Test rapide (une run)")]
        public static void QuickRun()
        {
            var options = new SimulationOptions();
            var strategy = GreedyStrategy.Balanced();

            var watch = Stopwatch.StartNew();
            using var harness = new SimulationHarness();
            RunResult run = SimulationRunner.RunOnce(harness, strategy, options);
            watch.Stop();

            var sb = new StringBuilder(512);
            sb.AppendLine($"[Équilibrage] Run simulée en {watch.ElapsedMilliseconds} ms de calcul.");
            sb.AppendLine($"  stratégie          : {run.StrategyName}");
            sb.AppendLine($"  durée de run       : {run.DurationSeconds / 60f:0.0} min ({run.DurationSeconds:0} s)");
            sb.AppendLine($"  issue              : {DescribeOutcome(run)}");
            sb.AppendLine($"  Datas générées     : {run.RunMoney:0.000e+00}");
            sb.AppendLine($"  CPU Cycles gagnés  : {run.CpuCyclesEarned:0}");
            sb.AppendLine($"  automatisation     : {FormatSeconds(run.TimeToAutomation)}");
            sb.AppendLine($"  Datas à 5 min      : {(run.MoneyAtProbe >= 0d ? run.MoneyAtProbe.ToString("N0") : "run trop courte")}");
            sb.AppendLine($"  1er CPU Cycle      : {FormatSeconds(run.FirstCycleAtSeconds)}");
            sb.AppendLine($"  plus haut Script   : SCR_{run.TopScriptOrder:00}");
            sb.AppendLine($"  réduction max      : {run.PeakReduction * 100f:0.0} %");
            sb.Append($"  points de courbe   : {run.Samples.Count}");

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Core/Équilibrage/Campagne complète")]
        public static void FullCampaign()
        {
            var options = new SimulationOptions();
            var strategy = GreedyStrategy.Balanced();

            var watch = Stopwatch.StartNew();
            CampaignResult campaign = SimulationRunner.RunCampaign(strategy, options);
            watch.Stop();

            var sb = new StringBuilder(512);
            sb.AppendLine($"[Équilibrage] Campagne simulée en {watch.ElapsedMilliseconds} ms de calcul.");
            sb.AppendLine($"  runs               : {campaign.Runs.Count}");
            sb.AppendLine($"  durée de campagne  : {campaign.TotalSeconds / 3600d:0.00} h");
            sb.AppendLine($"  arbre de prestige  : {(campaign.TreeComplete ? "COMPLET" : $"{campaign.TreeProgress * 100f:0.0} %")}");
            sb.Append($"  1er prestige       : {FormatSeconds(campaign.FirstPrestigeSeconds)}");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Campagne + écriture immédiate des CSV, sans boîte de dialogue.
        ///
        /// Existe pour que les résultats d'une campagne ne s'évaporent pas avec la console : la
        /// fenêtre a un bouton d'export, mais un raccourci sans clic est ce qu'on veut quand on
        /// enchaîne les mesures.
        /// </summary>
        [MenuItem("Tools/Core/Équilibrage/Campagne + export CSV")]
        public static void FullCampaignWithExport()
        {
            var options = new SimulationOptions();
            var strategy = GreedyStrategy.Balanced();

            CampaignResult campaign = SimulationRunner.RunCampaign(strategy, options);

            // À côté d'Assets, donc hors du dossier importé par Unity : ces CSV sont des mesures,
            // pas des assets, et n'ont rien à faire dans la base de données du projet.
            string root = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(root, "BalanceReports");
            Directory.CreateDirectory(folder);

            string basePath = Path.Combine(folder, $"campagne-{System.DateTime.Now:yyyyMMdd-HHmmss}.csv");
            string[] written = BalanceCsvExporter.Export(basePath, campaign.Runs);

            Debug.Log($"[Équilibrage] Campagne : {campaign.Runs.Count} runs, "
                      + $"{campaign.TotalSeconds / 3600d:0.00} h, arbre "
                      + $"{(campaign.TreeComplete ? "COMPLET" : $"{campaign.TreeProgress * 100f:0.0} %")}.\n"
                      + $"  {written[0]}\n  {written[1]}");
        }

        private static string DescribeOutcome(RunResult run)
        {
            if (run.TimedOut) return "PLAFOND DE DURÉE atteint (mesure incomplète)";
            return run.CleanExit ? "exfiltration propre" : "saisie fédérale";
        }

        private static string FormatSeconds(float seconds)
        {
            return seconds < 0f ? "jamais atteint" : $"{seconds:0} s ({seconds / 60f:0.0} min)";
        }
    }
}
