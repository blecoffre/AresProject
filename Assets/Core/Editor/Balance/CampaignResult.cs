using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Une run de la campagne simulée. Mêmes colonnes que le journal de fin de run du jeu
    /// (<c>RunJournal</c>), pour comparer le bot à un humain ligne à ligne, plus deux colonnes
    /// propres au harnais.
    /// </summary>
    public sealed class RunRecord
    {
        public int Index;

        /// <summary>« sortie », « saisie », ou « victoire » pour la run interrompue par la victoire.</summary>
        public string Outcome;

        public float DurationSeconds;
        public float ThreatAtEnd;
        public double DataGenerated;
        public double CyclesEarned;
        public double CyclesBeforeBonus;
        public double CyclesAwarded;
        public int PointsSpent;
        public double PeakNetTFlops;
        public double PeakGrossTFlops;
        public int Overclocks;
        public int Proxies;

        /// <summary>Secondes de survie achetées en Proxies sur la run, avant la branche Interception.</summary>
        public double ProxySeconds;

        public int Wipers;
        public bool Victory;

        /// <summary>Temps de campagne à la fin de cette run, écrans de fin compris, en heures.</summary>
        public double CampaignHours;

        /// <summary>Part de l'arbre utile achetée en sortant de l'écran de fin, de 0 à 100.</summary>
        public double UsefulTreePercent;

        /// <summary>Les rangs achetés sur l'écran de fin de cette run, « P_HARDEN_2 P_REFAC×3 ».</summary>
        public string TreePurchases = string.Empty;
    }

    /// <summary>Le premier franchissement d'un palier de TFlops nettes.</summary>
    public struct TFlopsMilestone
    {
        public double Threshold;
        public double Hours;
        public int Run;
    }

    /// <summary>
    /// Le résultat d'une campagne simulée : les runs, les grands repères, et le rapport comparé aux
    /// cibles du GD (GDD, « Les cibles de la campagne »).
    /// </summary>
    public sealed class CampaignResult
    {
        private const string CsvHeader =
            "run;issue;duree_s;jauge_fin;datas_run;cycles_gagnes;cycles_avant_bonus;cycles_cumules;" +
            "points_depenses;tflops_nettes_max;tflops_brutes_max;overclocks;proxies;data_wiper;victoire;" +
            "proxies_s;campagne_h;arbre_utile_pct;achats_arbre";

        /// <summary>Tolérance du GD sur les temps et les revenus.</summary>
        private const double Tolerance = 0.2d;

        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public CampaignSettings Settings;
        public readonly List<RunRecord> Runs = new List<RunRecord>();
        public readonly List<TFlopsMilestone> Milestones = new List<TFlopsMilestone>();
        public readonly List<string> Warnings = new List<string>();

        /// <summary>Les gestes du bot sur les premières runs (<see cref="CampaignSettings.TraceRuns"/>), une ligne chacun.</summary>
        public readonly StringBuilder Trace = new StringBuilder();

        public bool Victory;
        public double VictoryHours;
        public int VictoryRun;

        /// <summary>Pourquoi la campagne s'est arrêtée sans victoire. Vide en cas de victoire.</summary>
        public string StopReason = string.Empty;

        public double TotalHours;
        public double PointsGenerated;
        public int UsefulTreePoints;

        /// <summary>Heure de campagne où l'arbre utile a été complété, NaN s'il ne l'a pas été.</summary>
        public double UsefulTreeCompletedHours = double.NaN;

        /// <summary>Durée de calcul, pour information.</summary>
        public double WallClockSeconds;

        // ---- Fichiers -------------------------------------------------------------------------

        public void WriteCsv(string path)
        {
            var sb = new StringBuilder(Runs.Count * 160 + 256);
            sb.Append(CsvHeader).Append('\n');

            for (int i = 0; i < Runs.Count; i++)
            {
                RunRecord r = Runs[i];
                sb.Append(r.Index.ToString(Inv)).Append(';')
                  .Append(r.Outcome).Append(';')
                  .Append(r.DurationSeconds.ToString("0.0", Inv)).Append(';')
                  .Append(r.ThreatAtEnd.ToString("0.000", Inv)).Append(';')
                  .Append(r.DataGenerated.ToString("R", Inv)).Append(';')
                  .Append(r.CyclesEarned.ToString("R", Inv)).Append(';')
                  .Append(r.CyclesBeforeBonus.ToString("R", Inv)).Append(';')
                  .Append(r.CyclesAwarded.ToString("R", Inv)).Append(';')
                  .Append(r.PointsSpent.ToString(Inv)).Append(';')
                  .Append(r.PeakNetTFlops.ToString("R", Inv)).Append(';')
                  .Append(r.PeakGrossTFlops.ToString("R", Inv)).Append(';')
                  .Append(r.Overclocks.ToString(Inv)).Append(';')
                  .Append(r.Proxies.ToString(Inv)).Append(';')
                  .Append(r.Wipers.ToString(Inv)).Append(';')
                  .Append(r.Victory ? "oui" : "non").Append(';')
                  .Append(r.ProxySeconds.ToString("0.0", Inv)).Append(';')
                  .Append(r.CampaignHours.ToString("0.000", Inv)).Append(';')
                  .Append(r.UsefulTreePercent.ToString("0.0", Inv)).Append(';')
                  .Append(r.TreePurchases)
                  .Append('\n');
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        // ---- Rapport ---------------------------------------------------------------------------

        /// <summary>
        /// Le rapport lisible : réglages, issue, chaque cible du GD avec son verdict, puis le
        /// détail. <paramref name="journal"/> (facultatif) ajoute la comparaison au joueur réel.
        /// </summary>
        public string BuildReport(IReadOnlyList<JournalRun> journal)
        {
            var sb = new StringBuilder(8192);
            CampaignSettings s = Settings;

            sb.AppendLine("=== Ares — campagne simulée (harnais d'équilibrage, lot C3) ===");
            sb.AppendLine();
            sb.AppendLine(F("Leviers : victoire {0} TFlops · 1re boîte {1} Datas · croissance {2} · Wipe des Proxies ×{3} · exposant A.M.I. {4}",
                Sci(s.VictoryTFlops), Num(s.PrestigeFirstThresholdDatas), s.PrestigeThresholdGrowth.ToString("0.###", Fr),
                s.ProxyWipeScale.ToString("0.##", Fr), s.AmiSpeedExponent.ToString("0.###", Fr)));
            sb.AppendLine(F("Joueur : sortie à {0} (run 1) puis {1} sur l'aiguille · urgence à {2} de borne pessimiste · Data Wiper à {3} · {4} Overclock(s) avant la sortie · évasion à {5} appui(s)",
                Pct(s.FirstRunExitFraction), Pct(s.ExitFraction), Pct(s.DangerFraction), Pct(s.WiperFraction),
                s.PreExitOverclocks, s.EscapeOverclockReach));
            sb.AppendLine(F("Proxies : rentables à ×{0}, Wipe utile à {1}, {2} par run au plus",
                s.ProxyPaybackFactor.ToString("0.##", Fr), Pct(s.ProxyWasteTolerance),
                s.MaxProxiesPerRun < 0 ? "sans plafond" : s.MaxProxiesPerRun.ToString(Fr)));
            sb.AppendLine(F("Simulation : graine {0} · pas {1} s · une décision toutes les {2} s · {3} s d'écran de fin par run · calcul en {4} s",
                s.Seed, s.StepSeconds.ToString("0.###", Fr), s.DecisionIntervalSeconds.ToString("0.###", Fr),
                s.SecondsBetweenRuns.ToString("0.#", Fr), WallClockSeconds.ToString("0.0", Fr)));
            sb.AppendLine();

            if (Victory)
            {
                sb.AppendLine(F("ISSUE : VICTOIRE à {0}, pendant la run {1}.", Hours(VictoryHours), VictoryRun));
            }
            else
            {
                sb.AppendLine(F("ISSUE : PAS DE VICTOIRE — {0} ({1}, {2} runs).", StopReason, Hours(TotalHours), Runs.Count));
            }

            sb.AppendLine();
            AppendTargets(sb);
            sb.AppendLine();
            AppendOutcomes(sb);
            sb.AppendLine();
            AppendDurationCurve(sb);
            sb.AppendLine();
            AppendMilestones(sb);

            if (journal != null && journal.Count > 0)
            {
                sb.AppendLine();
                AppendJournalComparison(sb, journal);
            }

            if (Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Avertissements :");
                for (int i = 0; i < Warnings.Count; i++) sb.Append("  ⚠ ").AppendLine(Warnings[i]);
            }

            return sb.ToString();
        }

        private void AppendTargets(StringBuilder sb)
        {
            sb.AppendLine("Cibles du GD (tolérance ±20 % sur les temps et les revenus)");
            sb.AppendLine(Row("Cible", "Mesure", "Cible GD", "Verdict"));

            double campaignHours = Victory ? VictoryHours : TotalHours;
            sb.AppendLine(Row("Durée de campagne", Hours(campaignHours), "10 à 12 h",
                Victory ? Verdict(campaignHours, 10d, 12d) : "pas de victoire"));

            int runCount = Victory ? VictoryRun : Runs.Count;
            sb.AppendLine(Row("Nombre de runs", runCount.ToString(Fr), "80 à 100",
                Victory ? Verdict(runCount, 80d, 100d) : "pas de victoire"));

            int firstPointRun = FirstRunWithPoint();
            sb.AppendLine(Row("Premier point", firstPointRun > 0 ? F("fin de la run {0}", firstPointRun) : "jamais",
                "fin de la run 2", firstPointRun == 2 ? "dans la cible" : "hors cible"));

            List<RunRecord> finished = FinishedRuns();
            if (finished.Count > 0)
            {
                int n = finished.Count;
                int edge = Math.Max(1, Math.Min(10, n / 5));

                double early = MeanMinutes(finished, 0, edge);
                double middle = MeanMinutes(finished, n / 3, n - n / 3);
                double late = MeanMinutes(finished, n - edge, n);

                sb.AppendLine(Row(F("Durée des runs — début ({0} premières)", edge), Minutes(early), "~5 min", Verdict(early, 5d, 5d)));
                sb.AppendLine(Row("Durée des runs — tiers central", Minutes(middle), "8 à 11 min", Verdict(middle, 8d, 11d)));
                sb.AppendLine(Row(F("Durée des runs — fin ({0} dernières)", edge), Minutes(late), "~5 min 30", Verdict(late, 5.5d, 5.5d)));
            }

            if (!double.IsNaN(UsefulTreeCompletedHours) && campaignHours > 0d)
            {
                double share = UsefulTreeCompletedHours / campaignHours * 100d;
                sb.AppendLine(Row(F("Arbre utile ({0} points) complété", UsefulTreePoints),
                    F("à {0} % ({1})", share.ToString("0", Fr), Hours(UsefulTreeCompletedHours)),
                    "à 85–90 %", Verdict(share, 85d, 90d)));
            }
            else
            {
                sb.AppendLine(Row(F("Arbre utile ({0} points) complété", UsefulTreePoints), "jamais", "à 85–90 %", "hors cible"));
            }

            sb.AppendLine(Row("Points générés", Num(PointsGenerated), "~170 à 180",
                Victory ? Verdict(PointsGenerated, 170d, 180d) : "pas de victoire"));
        }

        private void AppendOutcomes(StringBuilder sb)
        {
            int exits = 0, seizures = 0, withWiper = 0, overclocks = 0, proxies = 0;
            for (int i = 0; i < Runs.Count; i++)
            {
                RunRecord r = Runs[i];
                if (r.Outcome == "sortie") exits++;
                else if (r.Outcome == "saisie") seizures++;
                if (r.Wipers > 0) withWiper++;
                overclocks += r.Overclocks;
                proxies += r.Proxies;
            }

            sb.AppendLine(F("Runs : {0} sorties, {1} saisies · Data Wiper dans {2} runs · {3} Overclocks et {4} Proxies au total",
                exits, seizures, withWiper, overclocks, proxies));

            sb.Append("Saisies aux runs : ");
            int listed = 0;
            for (int i = 0; i < Runs.Count && listed < 30; i++)
            {
                if (Runs[i].Outcome != "saisie") continue;
                sb.Append(listed == 0 ? string.Empty : ", ").Append(Runs[i].Index.ToString(Fr));
                listed++;
            }
            sb.AppendLine(listed == 0 ? "aucune" : (seizures > listed ? " …" : string.Empty));
        }

        /// <summary>La cloche des durées : la moyenne de chaque dixième de la campagne, en barres.</summary>
        private void AppendDurationCurve(StringBuilder sb)
        {
            List<RunRecord> finished = FinishedRuns();
            if (finished.Count == 0) return;

            sb.AppendLine("Durée moyenne des runs, par dixième de campagne :");

            int n = finished.Count;
            int slices = Math.Min(10, n);
            for (int k = 0; k < slices; k++)
            {
                int from = k * n / slices;
                int to = (k + 1) * n / slices;
                double minutes = MeanMinutes(finished, from, to);
                int bar = (int)Math.Round(Math.Min(60d, minutes * 3d));

                sb.Append(F("  runs {0,3}–{1,-3} ", finished[from].Index, finished[to - 1].Index))
                  .Append(new string('█', Math.Max(0, bar)))
                  .Append(' ').AppendLine(Minutes(minutes));
            }
        }

        private void AppendMilestones(StringBuilder sb)
        {
            sb.AppendLine("Premiers franchissements de TFlops nettes :");
            if (Milestones.Count == 0)
            {
                sb.AppendLine("  aucun");
                return;
            }

            for (int i = 0; i < Milestones.Count; i++)
            {
                TFlopsMilestone m = Milestones[i];
                sb.AppendLine(F("  {0,-8} à {1} (run {2})", Sci(m.Threshold), Hours(m.Hours), m.Run));
            }
        }

        /// <summary>
        /// Les premières runs du joueur réel face à celles du bot : c'est la base du calage. Les
        /// écarts à regarder d'abord sont la durée, les Datas et les TFlops atteintes.
        /// </summary>
        private void AppendJournalComparison(StringBuilder sb, IReadOnlyList<JournalRun> journal)
        {
            sb.AppendLine("Calage — journal du joueur réel (journal-runs.csv) face au bot :");
            sb.AppendLine("  run │ issue           │ durée               │ jauge         │ Datas de la run       │ TFlops nettes max     │ Overclocks  │ Proxies     │ Proxies (s)");
            sb.AppendLine("      │ joueur / bot    │ joueur / bot        │ joueur / bot  │ joueur / bot          │ joueur / bot          │ joueur/bot  │ joueur/bot  │ joueur / bot");

            int count = Math.Min(journal.Count, Runs.Count);
            for (int i = 0; i < count; i++)
            {
                JournalRun h = journal[i];
                RunRecord b = Runs[i];

                string humanProxySeconds = double.IsNaN(h.ProxySeconds) ? "—" : h.ProxySeconds.ToString("0", Fr);

                sb.AppendLine(F("  {0,3} │ {1,-6} / {2,-6} │ {3,8} / {4,-8} │ {5,5} / {6,-5} │ {7,9} / {8,-9} │ {9,9} / {10,-9} │ {11,4} / {12,-4} │ {13,4} / {14,-4} │ {15,5} / {16}",
                    i + 1, h.Outcome, b.Outcome, Minutes(h.DurationSeconds / 60d), Minutes(b.DurationSeconds / 60d),
                    Pct(h.ThreatAtEnd), Pct(b.ThreatAtEnd), Num(h.DataGenerated), Num(b.DataGenerated),
                    Num(h.PeakNetTFlops), Num(b.PeakNetTFlops), h.Overclocks, b.Overclocks, h.Proxies, b.Proxies,
                    humanProxySeconds, b.ProxySeconds.ToString("0", Fr)));
            }

            if (journal.Count > Runs.Count)
            {
                sb.AppendLine(F("  ({0} runs du journal au-delà de la campagne simulée, non comparées.)", journal.Count - Runs.Count));
            }
        }

        // ---- Mesures ---------------------------------------------------------------------------

        /// <summary>La première run qui a crédité au moins un point, 0 si aucune.</summary>
        public int FirstRunWithPoint()
        {
            for (int i = 0; i < Runs.Count; i++)
            {
                if (Runs[i].CyclesEarned >= 1d) return Runs[i].Index;
            }

            return 0;
        }

        /// <summary>Les runs terminées — sortie ou saisie —, sans la run interrompue par la victoire ou une borne.</summary>
        private List<RunRecord> FinishedRuns()
        {
            var finished = new List<RunRecord>(Runs.Count);
            for (int i = 0; i < Runs.Count; i++)
            {
                if (Runs[i].Outcome == "sortie" || Runs[i].Outcome == "saisie") finished.Add(Runs[i]);
            }

            return finished;
        }

        private static double MeanMinutes(List<RunRecord> runs, int from, int to)
        {
            if (to <= from) return 0d;

            double sum = 0d;
            for (int i = from; i < to; i++) sum += runs[i].DurationSeconds;
            return sum / (to - from) / 60d;
        }

        /// <summary>Dans la fourchette, dans la tolérance de ±20 % autour d'elle, ou hors cible.</summary>
        private static string Verdict(double value, double low, double high)
        {
            if (value >= low && value <= high) return "dans la cible";
            if (value >= low * (1d - Tolerance) && value <= high * (1d + Tolerance)) return "dans la tolérance (±20 %)";
            return value < low ? "hors cible (trop bas)" : "hors cible (trop haut)";
        }

        // ---- Mise en forme ---------------------------------------------------------------------

        private static string F(string format, params object[] args) => string.Format(Fr, format, args);

        private static string Row(string a, string b, string c, string d) => F("  {0,-38} {1,-20} {2,-16} {3}", a, b, c, d);

        private static string Pct(float fraction) => (fraction * 100f).ToString("0", Fr) + " %";

        private static string Hours(double hours)
        {
            int h = (int)Math.Floor(hours);
            int m = (int)Math.Round((hours - h) * 60d);
            if (m == 60) { h++; m = 0; }
            return F("{0} h {1:00}", h, m);
        }

        private static string Minutes(double minutes)
        {
            int total = (int)Math.Round(minutes * 60d);
            return F("{0} min {1:00}", total / 60, total % 60);
        }

        private static string Sci(double value) => value.ToString("0.##e+0", Fr);

        /// <summary>Lisible à toutes les échelles : entier groupé sous le million, notation scientifique au-delà.</summary>
        private static string Num(double value)
        {
            if (Math.Abs(value) < 1e6) return value.ToString("#,0", Fr);
            return Sci(value);
        }
    }
}
