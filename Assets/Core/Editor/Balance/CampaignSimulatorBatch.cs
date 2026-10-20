using Core.Models.Economy;
using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Le harnais en ligne de commande, Unity fermé :
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath &lt;projet&gt; -logFile &lt;fichier&gt;
    ///           -executeMethod Core.Editor.Balance.CampaignSimulatorBatch.Run -quit
    ///           [-simSeed 1337] [-simVictory 1e12] [-simFirstThreshold 2e6] [-simGrowth 1.12]
    ///           [-simProxyScale 1] [-simAmiExp 0.1] [-simExit 0.75] [-simFirstExit 0.5]
    ///           [-simDecision 0.5] [-simMaxProxies 40] [-simTraceRuns 3] [-simOut &lt;dossier&gt;]
    ///           [-simJournal &lt;journal-runs.csv à comparer&gt;]
    /// </code>
    /// Sans argument, les leviers sont ceux de l'asset (victoire : la cible du GD, 1e12). Le
    /// rapport s'écrit dans le log d'Unity et, avec le CSV des runs, dans <c>Logs/Simulations/</c>.
    ///
    /// ⚠️ Unity ne s'ouvre pas deux fois sur le même projet : fermer l'éditeur avant de lancer.
    /// </summary>
    public static class CampaignSimulatorBatch
    {
        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();

                var balancing = AssetDatabase.LoadAssetAtPath<BalancingConfigSO>(CampaignSimulator.BalancingPath);
                if (balancing == null) throw new InvalidOperationException($"Asset introuvable : {CampaignSimulator.BalancingPath}");

                CampaignSettings settings = CampaignSettings.FromAsset(balancing);
                settings.Seed = (int)ReadNumber(args, "-simSeed", settings.Seed);
                settings.VictoryTFlops = ReadNumber(args, "-simVictory", settings.VictoryTFlops);
                settings.PrestigeFirstThresholdDatas = ReadNumber(args, "-simFirstThreshold", settings.PrestigeFirstThresholdDatas);
                settings.PrestigeThresholdGrowth = ReadNumber(args, "-simGrowth", settings.PrestigeThresholdGrowth);
                settings.ProxyWipeScale = (float)ReadNumber(args, "-simProxyScale", settings.ProxyWipeScale);
                settings.AmiSpeedExponent = (float)ReadNumber(args, "-simAmiExp", settings.AmiSpeedExponent);
                settings.ExitFraction = (float)ReadNumber(args, "-simExit", settings.ExitFraction);
                settings.FirstRunExitFraction = (float)ReadNumber(args, "-simFirstExit", settings.FirstRunExitFraction);
                settings.DecisionIntervalSeconds = (float)ReadNumber(args, "-simDecision", settings.DecisionIntervalSeconds);
                settings.MaxProxiesPerRun = (int)ReadNumber(args, "-simMaxProxies", settings.MaxProxiesPerRun);
                settings.TraceRuns = (int)ReadNumber(args, "-simTraceRuns", settings.TraceRuns);

                string directory = ReadText(args, "-simOut", CampaignOutput.DefaultDirectory);
                string journal = ReadText(args, "-simJournal", JournalRun.DefaultPath);

                CampaignResult result = CampaignSimulator.Run(settings);
                string report = CampaignOutput.Write(result, directory, journal, out _, out _);

                Debug.Log("[SIMULATION]\n" + report);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[SIMULATION] Échec : " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static double ReadNumber(string[] args, string name, double fallback)
        {
            string text = ReadText(args, name, null);
            if (text == null) return fallback;

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new ArgumentException($"Argument {name} illisible : « {text} » (nombre attendu, point décimal).");
            }

            return value;
        }

        private static string ReadText(string[] args, string name, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }

            return fallback;
        }
    }
}
