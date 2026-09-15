using Core.Models.Economy;
using System.Collections.Generic;
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
                          + $"{seized} saisie(s), {(campaign.Victory ? "VICTOIRE" : "JAMAIS FINI")}, arbre "
                          + $"{(campaign.TreeComplete ? "complet" : $"{campaign.TreeProgress * 100f:0.0} %")}");
            sb.AppendLine($"              Datas cumulées {datas:0.000e+00}, sortie à "
                          + $"{(counted > 0 ? (sumExit / counted * 100f).ToString("0.0") : "—")} % de jauge");

            // Cycles de la PREMIÈRE et de la DERNIÈRE run, plus le bonus d'Extraction acquis.
            // C'est la preuve que la méta-progression est bien dans la boucle : si la dernière
            // run rapportait autant que la première, la campagne serait un tapis roulant.
            if (campaign.Runs.Count > 0)
            {
                double first = campaign.Runs[0].CpuCyclesEarned;
                double last = campaign.Runs[campaign.Runs.Count - 1].CpuCyclesEarned;
                sb.AppendLine($"              Cycles run 1 → dernière : {first:0} → {last:0}"
                              + $" (×{(first > 0d ? last / first : 0d):0.0}), "
                              + $"bonus Extraction ×{campaign.FinalCleanExitBonus:0.00}");

                // DURÉE des runs au fil de la campagne, et marge après le dernier palier.
                //
                // Teste une hypothèse structurelle : la jauge est alimentée par TOUTE la
                // production, mais son plafond ne grandit qu'avec le Hardware. Si c'est le cas,
                // plus le joueur devient puissant, plus ses runs raccourcissent — jusqu'à ce que
                // la fin de jauge ne dure plus que quelques secondes, ce qui expliquerait d'un
                // coup le dernier palier injouable, le plafond de contenu et les points qui se
                // tarissent.
                AppendRunDurations(sb, campaign);

                // Réduction réellement maintenue par les Proxies. Sans cette ligne, le troisième
                // pilier est invisible dans toutes les mesures — et il l'a été : le modèle
                // n'achetait de la défense qu'en urgence et tenait 16 % pour une asymptote à 85 %,
                // soit deux piliers sur trois.
                float peak = 0f;
                float sumPeak = 0f;
                for (int i = 0; i < campaign.Runs.Count; i++)
                {
                    sumPeak += campaign.Runs[i].PeakReduction;
                    if (campaign.Runs[i].PeakReduction > peak) peak = campaign.Runs[i].PeakReduction;
                }

                sb.AppendLine($"              dissipation : {sumPeak / campaign.Runs.Count * 100f:0.0} % en moyenne, "
                              + $"{peak * 100f:0.0} % au mieux");

                // Usage des deux mécaniques actives. Un zéro ici signale une mécanique que le
                // modèle n'exploite pas — donc une mesure incomplète, pas un réglage à corriger.
                int wipers = 0;
                int exploits = 0;
                for (int i = 0; i < campaign.Runs.Count; i++)
                {
                    wipers += campaign.Runs[i].EmergencyUses;
                    exploits += campaign.Runs[i].OverdriveTriggers;
                }

                sb.AppendLine($"              mécaniques actives : Data Wiper {wipers} fois, "
                              + $"Zéro-Day {exploits} fois (sur {campaign.Runs.Count} runs)");

                // Jusqu'où le joueur monte dans les générateurs. Sert à savoir si un palier de
                // CONTENU — « débloquer SCR_15 » — est un objectif atteignable, donc utilisable
                // comme vraie fin de partie à la place de la complétion de l'arbre.
                int topEver = 0;
                for (int i = 0; i < campaign.Runs.Count; i++)
                {
                    if (campaign.Runs[i].TopScriptOrder > topEver) topEver = campaign.Runs[i].TopScriptOrder;
                }

                int topHw = 0;
                double peakTFlops = 0d;
                for (int i = 0; i < campaign.Runs.Count; i++)
                {
                    if (campaign.Runs[i].TopHardwareOrder > topHw) topHw = campaign.Runs[i].TopHardwareOrder;
                    if (campaign.Runs[i].PeakTFlops > peakTFlops) peakTFlops = campaign.Runs[i].PeakTFlops;
                }

                // Bornes LUES dans le catalogue. Codées en dur à 15, elles ont survécu au
                // passage à 25 Scripts et 20 Hardware en affichant « SCR_18 / 15 ».
                using var probe = new SimulationHarness();
                int lastScript = SimulationRunner.ResolveTopOrderInCatalog(probe, UpgradeType.Script);
                int lastHardware = SimulationRunner.ResolveTopOrderInCatalog(probe, UpgradeType.Hardware);

                sb.AppendLine($"              contenu atteint : SCR_{topEver:00} / {lastScript}, "
                              + $"HW_{topHw:00} / {lastHardware}, pic {peakTFlops:0.000e+00} TFlops");
            }
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

        /// <summary>
        /// Combien de temps l'A.M.I. laisse avant de tomber, sur la PREMIERE run et sur elle
        /// seule — aucun bonus de prestige, aucune sortie volontaire.
        ///
        /// C'est la mesure du plafond DUR. Toutes les autres postures exfiltrent, donc elles
        /// mesurent l'habilete du joueur a sortir au bon moment, jamais le temps qu'il avait
        /// devant lui. Les trois postures ne different que par leur posture defensive : l'ecart
        /// entre elles chiffre exactement ce que la defense achete comme temps de survie.
        ///
        /// Les seuils affiches sont lus depuis les PALIERS d'extraction reglés, jamais codes en
        /// dur : si on retouche les paliers, cette mesure suit.
        /// </summary>
        [MenuItem("Tools/Core/Équilibrage/Première run — temps avant le Game Over")]
        public static void FirstRunTimeToSeizure()
        {
            var options = new SimulationOptions();

            var sb = new StringBuilder(1024);
            sb.AppendLine("[Équilibrage] Première run — temps avant la saisie fédérale "
                          + "(aucun prestige, aucune sortie volontaire) :");

            AppendSeizure(sb, GreedyStrategy.UntilSeizedAcquisitionOnly(), options);
            AppendSeizure(sb, GreedyStrategy.UntilSeizedBalanced(), options);
            AppendSeizure(sb, GreedyStrategy.UntilSeizedHeavy(), options);

            Debug.Log(sb.ToString());
        }

        private static void AppendSeizure(StringBuilder sb, GreedyStrategy strategy,
                                          SimulationOptions options)
        {
            // Un harnais NEUF par posture : sans ça la deuxième mesurerait une run d'après
            // prestige, et ne serait plus une première run.
            using var harness = new SimulationHarness();
            RunResult run = SimulationRunner.RunOnce(harness, strategy, options);

            sb.AppendLine($"  {strategy.Name,-18} : {DescribeSeizure(run)}, "
                          + $"SCR_{run.TopScriptOrder:00}, {run.RunMoney:0.000e+00} Datas");

            IReadOnlyList<CleanExitTier> tiers = harness.Balancing.CleanExitTiers;
            if (tiers == null || tiers.Count == 0) return;

            var line = new StringBuilder(128);
            for (int i = 0; i < tiers.Count; i++)
            {
                float threshold = tiers[i].TraceThreshold;
                float reached = TimeAtFraction(run, threshold);

                if (line.Length > 0) line.Append("  |  ");
                line.Append($"{threshold * 100f:0} % à {FormatMinutes(reached)}");
            }

            sb.AppendLine($"                       paliers atteints : {line}");
        }

        /// <summary>Instant où la jauge franchit une fraction, en secondes. -1 si jamais atteinte.</summary>
        private static float TimeAtFraction(RunResult run, float fraction)
        {
            for (int i = 0; i < run.Samples.Count; i++)
            {
                if (run.Samples[i].TraceFraction >= fraction) return run.Samples[i].TimeSeconds;
            }
            return -1f;
        }

        private static string DescribeSeizure(RunResult run)
        {
            if (run.TimedOut) return $"JAMAIS SAISI en {run.DurationSeconds / 60f:0.0} min (plafond de mesure)";
            if (run.CleanExit) return "sortie volontaire — posture mal réglée, elle devait aller au bout";
            return $"saisi à {run.DurationSeconds / 60f:0.0} min";
        }

        private static string FormatMinutes(float seconds)
        {
            return seconds < 0f ? "jamais" : $"{seconds / 60f:0.0} min";
        }

        /// <summary>
        /// OU SE TROUVE LE TEMPS MORT — achats par minute sur la premiere run.
        ///
        /// Rapporte en jouant : « passe les cinq premieres minutes, cela devient long, les gains
        /// ne sont pas enormes donc on achete tres rarement quoi que ce soit, le spam d'Overclock
        /// devient la seule solution ». Un jeu incremental se juge a la frequence a laquelle le
        /// joueur a quelque chose a faire ; cette mesure la chiffre minute par minute au lieu de
        /// la deviner.
        ///
        /// La posture simulee vise le dernier palier d'extraction, donc c'est bien une run
        /// jouee normalement — pas un cas limite.
        /// </summary>
        [MenuItem("Tools/Core/Équilibrage/Première run — où se trouve le temps mort ?")]
        public static void FirstRunPurchaseDensity()
        {
            var options = new SimulationOptions();
            using var harness = new SimulationHarness();
            RunResult run = SimulationRunner.RunOnce(harness,
                                                     GreedyStrategy.TierHunter("palier 90 %", 0.90f),
                                                     options);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"[Équilibrage] Première run — densité d'achats ({run.DurationSeconds / 60f:0.0} min, "
                          + $"{DescribeOutcome(run)})");
            sb.AppendLine("  minute | achats | jauge | Datas");

            int previousLevels = 0;
            int minute = 0;
            int longestDrought = 0;
            int currentDrought = 0;
            int droughtStart = -1;
            int worstStart = -1;

            for (int i = 0; i < run.Samples.Count; i++)
            {
                RunSample sample = run.Samples[i];
                int m = (int)(sample.TimeSeconds / 60f);
                if (m < minute) continue;

                int bought = sample.TotalLevels - previousLevels;
                previousLevels = sample.TotalLevels;

                if (bought == 0)
                {
                    if (currentDrought == 0) droughtStart = minute;
                    currentDrought++;
                    if (currentDrought > longestDrought)
                    {
                        longestDrought = currentDrought;
                        worstStart = droughtStart;
                    }
                }
                else currentDrought = 0;

                sb.AppendLine($"  {minute,6} | {bought,6} | {sample.TraceFraction * 100f,4:0} % | "
                              + $"{sample.RunMoney,16:N0}");
                minute++;
            }

            sb.Append(longestDrought > 0
                ? $"  PLUS LONG TEMPS MORT : {longestDrought} min d'affilée sans un seul achat, "
                  + $"à partir de la minute {worstStart}."
                : "  Aucune minute entière sans achat.");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// CE QUE CHAQUE FAMILLE DE NOEUDS APPORTE REELLEMENT.
        ///
        /// Rejoue la meme campagne en privant le joueur d'une famille a la fois : l'ecart de
        /// duree avec la campagne complete EST la contribution de cette famille. Repond a
        /// « ces noeuds sont-ils vraiment les plus utiles ? » par une mesure plutot que par une
        /// intuition — question posee au moment de decider lesquels supprimer en reduisant
        /// l'arbre.
        ///
        /// Une famille dont la suppression ne change rien est une famille a couper.
        /// </summary>
        [MenuItem("Tools/Core/Équilibrage/Et si le joueur évitait une famille de nœuds ?")]
        public static void PrestigeFamilyWorth()
        {
            CampaignResult full = SimulationRunner.RunCampaign(
                GreedyStrategy.TierHunter("complet", 0.90f), BudgetedOptions());

            var sb = new StringBuilder(1024);
            sb.AppendLine("[Équilibrage] Contribution de chaque famille de nœuds "
                          + "(15 runs pour tout le monde, le joueur ÉVITE une famille) :");
            sb.AppendLine($"  {"arbre complet",-22} : {TotalDatas(full):0.000e+00} Datas   →  100 % (arbre entier, référence)");

            AppendWithout(sb, full, "sans rendement ciblé", PrestigeBonusType.SpecificUpgradeYieldBoost);
            AppendWithout(sb, full, "sans coût ciblé", PrestigeBonusType.SpecificUpgradeCostReduction);
            AppendWithout(sb, full, "sans durée ciblée", PrestigeBonusType.SpecificUpgradeTimeReduction);
            AppendWithout(sb, full, "sans automatisation", PrestigeBonusType.SpecificUpgradeAutomationTresholdReduction);

            Debug.Log(sb.ToString());
        }

        private static void AppendWithout(StringBuilder sb, CampaignResult reference, string label,
                                          params PrestigeBonusType[] ignored)
        {
            CampaignResult r = SimulationRunner.RunCampaign(
                GreedyStrategy.TierHunterWithout(label, ignored), BudgetedOptions());

            double mine = TotalDatas(r);
            double theirs = TotalDatas(reference);
            double ratio = theirs > 0d ? mine / theirs : 0d;

            sb.AppendLine($"  {label,-22} : {mine:0.000e+00} Datas   →  "
                          + $"{ratio * 100d:0} % de la puissance de référence");
        }

        private static double TotalDatas(CampaignResult c)
        {
            double total = 0d;
            for (int i = 0; i < c.Runs.Count; i++) total += c.Runs[i].RunMoney;
            return total;
        }

        /// <summary>
        /// Budget FIXE en nombre de runs. Sans ça la mesure est inexploitable : « arbre complet »
        /// exige que tous les nœuds soient au maximum, donc priver le joueur d'une famille rend la
        /// complétion impossible par construction — la campagne va toujours au bout du budget
        /// horaire et toutes les variantes se ressemblent. À nombre de runs égal, ce qu'on compare
        /// est bien la PUISSANCE que la famille apporte, et plus la taille de l'arbre.
        /// </summary>
        private static SimulationOptions BudgetedOptions()
        {
            return new SimulationOptions { MaxRuns = 15, MaxCampaignHours = 100f };
        }

        /// <summary>
        /// Durée des runs au fil de la campagne, et temps restant après le DERNIER palier
        /// d'extraction.
        ///
        /// Cette seconde colonne est la mesure décisive : si elle tombe à quelques secondes en
        /// fin de campagne, aucun réglage de brouillard ne peut rendre le dernier palier jouable,
        /// puisqu'il n'y a tout simplement plus de temps pour réagir.
        /// </summary>
        private static void AppendRunDurations(StringBuilder sb, CampaignResult campaign)
        {
            int count = campaign.Runs.Count;
            if (count == 0) return;

            var line = new StringBuilder(160);
            int[] marks = count >= 4
                ? new[] { 0, count / 3, 2 * count / 3, count - 1 }
                : new[] { 0, count - 1 };

            float highestTier = ResolveHighestTier();

            for (int i = 0; i < marks.Length; i++)
            {
                RunResult r = campaign.Runs[marks[i]];
                if (line.Length > 0) line.Append("  →  ");
                line.Append($"run {marks[i] + 1} : {r.DurationSeconds / 60f:0.0} min");

                // Secondes entre le franchissement du dernier palier et la fin de la run.
                float lastTier = TimeAtFraction(r, highestTier);
                if (lastTier >= 0f)
                {
                    line.Append($" (marge {r.DurationSeconds - lastTier:0} s)");
                }
            }

            sb.AppendLine($"              durée des runs : {line}");
        }

        /// <summary>
        /// Seuil du plus haut palier d'extraction, résolu UNE fois et mémorisé.
        ///
        /// Construire un harnais par run pour lire un réglage chargerait les catalogues des
        /// centaines de fois : la mesure coûterait plus cher que la simulation qu'elle décrit.
        /// </summary>
        private static float ResolveHighestTier()
        {
            if (_cachedHighestTier > 0f) return _cachedHighestTier;

            using var harness = new SimulationHarness();
            IReadOnlyList<CleanExitTier> tiers = harness.Balancing.CleanExitTiers;
            if (tiers == null || tiers.Count == 0) return 1f;

            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].TraceThreshold > _cachedHighestTier) _cachedHighestTier = tiers[i].TraceThreshold;
            }

            return _cachedHighestTier;
        }

        private static float _cachedHighestTier;

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
