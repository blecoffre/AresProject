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

        /// <summary>
        /// Campagne jouée par le modèle de joueur HUMAIN — le seul qui puisse se faire saisir.
        ///
        /// Compte les saisies fédérales run par run : c'est la mesure de la brutalité réelle du
        /// début de partie. Un agent omniscient sortirait toujours à temps et rendrait zéro.
        /// </summary>
        [MenuItem("Tools/Core/Équilibrage/Campagne « joueur gourmand » + export CSV")]
        public static void HumanCampaign()
        {
            var options = new SimulationOptions();
            CampaignResult campaign = SimulationRunner.RunCampaign(GreedyStrategy.HumanGreedy(), options);

            string root = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(root, "BalanceReports");
            Directory.CreateDirectory(folder);
            string basePath = Path.Combine(folder, $"humain-{System.DateTime.Now:yyyyMMdd-HHmmss}.csv");
            string[] written = BalanceCsvExporter.Export(basePath, campaign.Runs);

            var sb = new StringBuilder(512);
            sb.AppendLine($"[Équilibrage] Campagne « joueur gourmand » : {campaign.Runs.Count} runs, "
                          + $"{campaign.TotalSeconds / 3600d:0.00} h, arbre "
                          + $"{(campaign.TreeComplete ? "COMPLET" : $"{campaign.TreeProgress * 100f:0.0} %")}.");

            int seized = 0;
            var detail = new StringBuilder(256);
            for (int i = 0; i < campaign.Runs.Count; i++)
            {
                RunResult r = campaign.Runs[i];
                if (r.CleanExit || r.TimedOut) continue;
                seized++;
                if (detail.Length > 0) detail.Append(", ");
                detail.Append($"run {i + 1} à {r.DurationSeconds / 60f:0.0} min");
            }

            sb.AppendLine($"  SAISIES FÉDÉRALES : {seized} / {campaign.Runs.Count}");
            sb.AppendLine($"  {(seized > 0 ? detail.ToString() : "aucune — le joueur sort toujours à temps")}");

            // Diagnostic : POURQUOI les runs se terminent. Une jauge basse à la sortie signifie
            // que c'est le plateau qui a décidé, pas la peur — et alors le brouillard est hors
            // sujet, si épais soit-il.
            float sumExit = 0f;
            float sumLag = 0f;
            float worstLag = 0f;
            int counted = 0;
            for (int i = 0; i < campaign.Runs.Count; i++)
            {
                RunResult r = campaign.Runs[i];
                if (r.ExitTraceFraction < 0f) continue;
                sumExit += r.ExitTraceFraction;
                sumLag += r.ExitTraceFraction - r.ExitReadoutFraction;
                if (r.PeakReadoutLag > worstLag) worstLag = r.PeakReadoutLag;
                counted++;
            }

            if (counted > 0)
            {
                sb.AppendLine($"  jauge à la sortie : {sumExit / counted * 100f:0.0} % en moyenne "
                              + $"(sous ~90 %, c'est le plateau qui décide, pas la Trace)");
                sb.AppendLine($"  mensonge du relevé : {sumLag / counted * 100f:0.0} pts à la sortie, "
                              + $"{worstLag * 100f:0.0} pts au pire");
            }

            sb.Append($"  {written[0]}");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Les deux lectures de la MEME jauge, dos a dos.
        ///
        /// C'est la mesure qui valide — ou invalide — le modele d'incertitude. Le brouillard n'est
        /// une mecanique que s'il ouvre un vrai choix : le prudent lit le bord haut de la
        /// fourchette, survit, et paie sa prudence en Datas laissees sur la table ; le gourmand
        /// lit le chiffre affiche, gagne plus vite, et se fait saisir. Si les deux colonnes se
        /// ressemblent, il n'y a pas d'arbitrage, seulement une difficulte de plus.
        /// </summary>
        [MenuItem("Tools/Core/Équilibrage/Comparer les paliers d'extraction")]
        public static void ComparePostures()
        {
            var options = new SimulationOptions();

            var sb = new StringBuilder(768);
            sb.AppendLine("[Équilibrage] Quel palier d'extraction viser ? (3 campagnes complètes)");

            AppendPosture(sb, "palier 50 %",
                          SimulationRunner.RunCampaign(GreedyStrategy.TierHunter("p50", 0.50f), options));
            AppendPosture(sb, "palier 75 %",
                          SimulationRunner.RunCampaign(GreedyStrategy.TierHunter("p75", 0.75f), options));
            AppendPosture(sb, "palier 90 %",
                          SimulationRunner.RunCampaign(GreedyStrategy.TierHunter("p90", 0.90f), options));

            // Le prudent reste en référence : il vise le dernier palier mais décide contre le
            // BORD de la fourchette au lieu du chiffre. C'est le prix de la certitude.
            AppendPosture(sb, "90 % prudent",
                          SimulationRunner.RunCampaign(GreedyStrategy.Human(), options));

            Debug.Log(sb.ToString());
        }

        private static void AppendPosture(StringBuilder sb, string label, CampaignResult campaign)
        {
            int seized = 0;
            double datas = 0d;
            float sumExit = 0f;
            int counted = 0;

            for (int i = 0; i < campaign.Runs.Count; i++)
            {
                RunResult r = campaign.Runs[i];
                datas += r.RunMoney;
                if (!r.CleanExit && !r.TimedOut) seized++;
                if (r.ExitTraceFraction < 0f) continue;
                sumExit += r.ExitTraceFraction;
                counted++;
            }

            sb.AppendLine($"  {label,-9} : {campaign.Runs.Count,3} runs, {campaign.TotalSeconds / 3600d:0.00} h, "
                          + $"{seized} saisie(s), arbre "
                          + $"{(campaign.TreeComplete ? "COMPLET" : $"{campaign.TreeProgress * 100f:0.0} %")}");
            sb.AppendLine($"              Datas cumulées {datas:0.000e+00}, sortie à "
                          + $"{(counted > 0 ? (sumExit / counted * 100f).ToString("0.0") : "—")} % de jauge");
        }

        /// <summary>
        /// La PREMIERE run, minute par minute, jouee en n'achetant que de l'acquisition.
        ///
        /// Sert a confronter le modele a une vraie partie : quand Bertrand rapporte « 27 % de
        /// Trace apres vingt minutes », c'est cette courbe qui dit si le build derive du modele
        /// ou si le game design est en cause. Sans elle, on ne peut que speculer.
        /// </summary>
        [MenuItem("Tools/Core/Équilibrage/Première run — acquisition seule, minute par minute")]
        public static void FirstRunProfile()
        {
            var options = new SimulationOptions();
            using var harness = new SimulationHarness();
            RunResult run = SimulationRunner.RunOnce(harness, GreedyStrategy.AcquisitionOnly(), options);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"[Équilibrage] Première run, acquisition seule — {run.DurationSeconds / 60f:0.0} min, "
                          + $"{DescribeOutcome(run)}, plus haut Script SCR_{run.TopScriptOrder:00}");
            sb.AppendLine("  minute | jauge | Datas générées");

            float nextMinute = 5f;
            for (int i = 0; i < run.Samples.Count; i++)
            {
                RunSample sample = run.Samples[i];
                if (sample.TimeSeconds / 60f < nextMinute) continue;
                nextMinute += 5f;
                sb.AppendLine($"  {sample.TimeSeconds / 60f,5:0.0} | {sample.TraceFraction * 100f,4:0.0} % | "
                              + $"{sample.RunMoney,18:N0}");
            }

            Debug.Log(sb.ToString());
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
