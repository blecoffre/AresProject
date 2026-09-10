using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Écrit les résultats sur disque, pour comparer deux jeux de réglages côte à côte ou garder
    /// une trace d'un avant/après. La fenêtre montre la tendance ; le CSV permet de la vérifier.
    ///
    /// <b>Convention française assumée</b> : séparateur point-virgule et virgule décimale, donc
    /// double-clic direct dans un Excel FR sans passer par l'assistant d'import. Pour relire ces
    /// fichiers en Python, préciser <c>sep=';', decimal=','</c>.
    /// </summary>
    public static class BalanceCsvExporter
    {
        private const char Separator = ';';
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-FR");

        /// <summary>
        /// Écrit deux fichiers à partir du chemin donné : <c>…_runs.csv</c> (une ligne par run) et
        /// <c>…_points.csv</c> (une ligne par point de courbe). Deux fichiers plutôt qu'un seul à
        /// sections, parce qu'un tableur ne sait pas lire deux tableaux dans une même feuille.
        /// Retourne les chemins réellement écrits.
        /// </summary>
        public static string[] Export(string basePath, IReadOnlyList<RunResult> runs)
        {
            string directory = Path.GetDirectoryName(basePath);
            string stem = Path.GetFileNameWithoutExtension(basePath);
            string runsPath = Path.Combine(directory, stem + "_runs.csv");
            string pointsPath = Path.Combine(directory, stem + "_points.csv");

            File.WriteAllText(runsPath, BuildRuns(runs), Encoding.UTF8);
            File.WriteAllText(pointsPath, BuildPoints(runs), Encoding.UTF8);

            return new[] { runsPath, pointsPath };
        }

        private static string BuildRuns(IReadOnlyList<RunResult> runs)
        {
            var sb = new StringBuilder(4096);
            Header(sb, "run", "strategie", "duree_s", "duree_min", "datas", "cpu_cycles",
                   "issue", "trace_max", "reduction_max", "automatisation_s", "datas_a_la_sonde",
                   "premier_cycle_s", "plus_haut_script");

            for (int i = 0; i < runs.Count; i++)
            {
                RunResult r = runs[i];
                Row(sb,
                    (i + 1).ToString(Culture),
                    r.StrategyName,
                    Num(r.DurationSeconds),
                    Num(r.DurationSeconds / 60f),
                    Num(r.RunMoney),
                    Num(r.CpuCyclesEarned),
                    Outcome(r),
                    Num(r.PeakTraceFraction),
                    Num(r.PeakReduction),
                    Num(r.TimeToAutomation),
                    Num(r.MoneyAtProbe),
                    Num(r.FirstCycleAtSeconds),
                    r.TopScriptOrder.ToString(Culture));
            }

            return sb.ToString();
        }

        private static string BuildPoints(IReadOnlyList<RunResult> runs)
        {
            var sb = new StringBuilder(65536);
            Header(sb, "run", "temps_s", "temps_min", "datas_cumulees", "jauge_trace");

            for (int i = 0; i < runs.Count; i++)
            {
                IReadOnlyList<RunSample> samples = runs[i].Samples;
                for (int s = 0; s < samples.Count; s++)
                {
                    RunSample sample = samples[s];
                    Row(sb,
                        (i + 1).ToString(Culture),
                        Num(sample.TimeSeconds),
                        Num(sample.TimeSeconds / 60f),
                        Num(sample.RunMoney),
                        Num(sample.TraceFraction));
                }
            }

            return sb.ToString();
        }

        private static string Outcome(RunResult r)
        {
            if (r.TimedOut) return "plafond de duree";
            return r.CleanExit ? "exfiltration" : "saisie";
        }

        private static string Num(double value)
        {
            // Les valeurs manquantes sont encodees -1 par le pilote : une cellule vide se lit
            // mieux qu'un -1 qu'un tableur prendrait pour une mesure.
            if (value < 0d) return string.Empty;
            return value.ToString("0.######", Culture);
        }

        private static void Header(StringBuilder sb, params string[] columns) => Row(sb, columns);

        private static void Row(StringBuilder sb, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(Separator);
                sb.Append(cells[i]);
            }
            sb.Append('\n');
        }
    }
}
