using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>Une run jouée, lue dans le journal de fin de run du jeu (<c>RunJournal</c>).</summary>
    public struct JournalRun
    {
        public string Outcome;
        public double DurationSeconds;
        public float ThreatAtEnd;
        public double DataGenerated;
        public double PeakNetTFlops;
        public int Overclocks;
        public int Proxies;

        /// <summary>Secondes de survie achetées en Proxies ; NaN pour un journal d'avant le 2026-10-20.</summary>
        public double ProxySeconds;

        private const string FileName = "journal-runs.csv";

        /// <summary>Le journal du jeu, là où il l'écrit : à côté de la sauvegarde.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Lit le journal, colonnes repérées par leur nom d'en-tête. Liste vide si le fichier
        /// n'existe pas ; une ligne illisible est sautée et signalée dans <paramref name="warnings"/>.
        /// </summary>
        public static List<JournalRun> Read(string path, List<string> warnings)
        {
            var runs = new List<JournalRun>();
            if (!File.Exists(path)) return runs;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                warnings.Add($"Journal illisible ({path}) : {ex.Message}");
                return runs;
            }

            if (lines.Length == 0) return runs;

            string[] header = lines[0].Split(';');
            int outcome = Array.IndexOf(header, "issue");
            int duration = Array.IndexOf(header, "duree_s");
            int threat = Array.IndexOf(header, "jauge_fin");
            int data = Array.IndexOf(header, "datas_run");
            int peak = Array.IndexOf(header, "tflops_nettes_max");
            int overclocks = Array.IndexOf(header, "overclocks");
            int proxies = Array.IndexOf(header, "proxies");
            int proxySeconds = Array.IndexOf(header, "proxies_s");

            if (outcome < 0 || duration < 0 || threat < 0 || data < 0 || peak < 0 || overclocks < 0 || proxies < 0)
            {
                warnings.Add($"Journal au format inattendu ({path}) : en-tête « {lines[0]} ».");
                return runs;
            }

            CultureInfo inv = CultureInfo.InvariantCulture;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;

                string[] cells = lines[i].Split(';');
                try
                {
                    runs.Add(new JournalRun
                    {
                        Outcome = cells[outcome],
                        DurationSeconds = double.Parse(cells[duration], inv),
                        ThreatAtEnd = float.Parse(cells[threat], inv),
                        DataGenerated = double.Parse(cells[data], inv),
                        PeakNetTFlops = double.Parse(cells[peak], inv),
                        Overclocks = int.Parse(cells[overclocks], inv),
                        Proxies = int.Parse(cells[proxies], inv),
                        ProxySeconds = proxySeconds >= 0 ? double.Parse(cells[proxySeconds], inv) : double.NaN,
                    });
                }
                catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException || ex is OverflowException)
                {
                    warnings.Add($"Journal, ligne {i + 1} illisible : « {lines[i]} ».");
                }
            }

            return runs;
        }
    }
}
