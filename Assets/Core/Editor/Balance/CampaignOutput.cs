using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Où vont les résultats d'une campagne simulée : <c>Logs/Simulations/</c> à la racine du
    /// projet, ignoré par git comme le reste de <c>Logs/</c>. Un CSV des runs et le rapport texte,
    /// horodatés pour qu'on puisse comparer deux réglages côte à côte.
    /// </summary>
    public static class CampaignOutput
    {
        public static string DefaultDirectory =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "Simulations");

        /// <summary>
        /// Écrit le CSV et le rapport, et rend le rapport. Les chemins écrits sont ajoutés en fin de
        /// rapport. <paramref name="journalPath"/> : le journal de runs jouées à comparer — celui du
        /// jeu par défaut (<see cref="JournalRun.DefaultPath"/>), ou une copie mise de côté.
        /// </summary>
        public static string Write(CampaignResult result, string directory, string journalPath,
                                   out string csvPath, out string reportPath)
        {
            Directory.CreateDirectory(directory);

            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string name = $"campagne_{stamp}_graine{result.Settings.Seed}";

            csvPath = Path.Combine(directory, name + ".csv");
            reportPath = Path.Combine(directory, name + ".txt");

            var warnings = new List<string>();
            List<JournalRun> journal = JournalRun.Read(journalPath ?? JournalRun.DefaultPath, warnings);
            result.Warnings.AddRange(warnings);

            string report = result.BuildReport(journal)
                          + Environment.NewLine
                          + "Fichiers : " + csvPath + Environment.NewLine
                          + "           " + reportPath + Environment.NewLine;

            result.WriteCsv(csvPath);
            File.WriteAllText(reportPath, report, new UTF8Encoding(false));

            if (result.Trace.Length > 0)
            {
                File.WriteAllText(Path.Combine(directory, name + "_trace.txt"), result.Trace.ToString(), new UTF8Encoding(false));
            }

            return report;
        }
    }
}
