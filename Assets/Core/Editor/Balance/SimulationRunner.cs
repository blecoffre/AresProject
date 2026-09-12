using Core.Models.Economy;
using Core.Models.Simulation;
using R3;
using System;
using System.Collections.Generic;

namespace Core.Editor.Balance
{
    /// <summary>Réglages du pilote. Les défauts reproduisent le rythme de mesure du calage d'août.</summary>
    public sealed class SimulationOptions
    {
        /// <summary>Secondes entre deux points de décision d'achat, au début de la run.</summary>
        public float DecisionIntervalSeconds = 0.5f;

        /// <summary>Plafond de durée d'une run, en secondes de jeu. Évite une run qui ne finit jamais.</summary>
        public float MaxRunSeconds = 9000f;

        /// <summary>Budget de la campagne, en heures de jeu.</summary>
        public float MaxCampaignHours = 16f;

        public int MaxRuns = 300;

        /// <summary>Instant où l'on relève les Datas, pour surveiller l'explosion du début.</summary>
        public float ProbeSeconds = 300f;

        /// <summary>Générateur dont on chronomètre le passage au seuil d'automatisation.</summary>
        public string AutomationProbeId = "SCR_01";

        /// <summary>Secondes de jeu entre deux points de courbe.</summary>
        public float SampleIntervalSeconds = 10f;

        /// <summary>Fenêtre glissante de détection de plateau, en fraction du temps écoulé.</summary>
        public float PlateauWindow = 0.35f;
    }

    public struct RunSample
    {
        public float TimeSeconds;
        public double RunMoney;
        public float TraceFraction;

        /// <summary>
        /// Somme des niveaux possédés, tous piliers confondus. Sa dérivée est la DENSITÉ
        /// D'ACHATS : combien de fois le joueur a eu quelque chose à faire dans la minute.
        ///
        /// Relevée plutôt que comptée à l'achat pour ne rien exiger de la stratégie — n'importe
        /// quelle posture, présente ou future, se mesure ainsi sans être modifiée.
        /// </summary>
        public int TotalLevels;
    }

    public sealed class RunResult
    {
        public string StrategyName;
        public float DurationSeconds;
        public double RunMoney;
        public double CpuCyclesEarned;
        public bool CleanExit;
        public float PeakTraceFraction;
        public float PeakReduction;
        public float TimeToAutomation = -1f;
        public double MoneyAtProbe = -1d;
        public int TopScriptOrder;

        /// <summary>
        /// Plus haut Hardware possédé, et pic de puissance de calcul atteint sur la run.
        ///
        /// Ce sont les deux candidats à la condition de VICTOIRE : le lore du jeu est le hack
        /// d'une clé USB surpuissante, donc la fin se mesure en capacité de calcul, pas en
        /// nombre de Scripts. Relevés PENDANT la run comme TopScriptOrder — après WipeRun, tout
        /// est revenu à zéro.
        /// </summary>
        public int TopHardwareOrder;
        public double PeakTFlops;
        public float FirstCycleAtSeconds = -1f;

        /// <summary>
        /// La run a été coupée au plafond de durée plutôt que par une fin de jeu. C'est un
        /// résultat à part : ni saisie, ni exfiltration, juste une mesure incomplète.
        /// </summary>
        public bool TimedOut;

        /// <summary>
        /// Jauge REELLE a l'instant ou le joueur a decide de sortir. Diagnostic central : une
        /// valeur tres inferieure au seuil de sortie signifie que la run s'est terminee sur la
        /// detection de plateau, pas sur la peur. Le brouillard n'y peut alors rien.
        /// </summary>
        public float ExitTraceFraction = -1f;

        /// <summary>Chiffre AFFICHE au meme instant. L'ecart avec le precedent est le mensonge.</summary>
        public float ExitReadoutFraction = -1f;

        /// <summary>Plus grand ecart constate sur la run entre la verite et le chiffre affiche.</summary>
        public float PeakReadoutLag;

        public readonly List<RunSample> Samples = new List<RunSample>(256);
    }

    public sealed class CampaignResult
    {
        public string StrategyName;
        public readonly List<RunResult> Runs = new List<RunResult>(64);
        public double TotalSeconds;
        public bool TreeComplete;
        public float TreeProgress;
        public float FirstPrestigeSeconds = -1f;

        /// <summary>
        /// Multiplicateur d'Effacement Propre acquis au bout de la campagne, branche Extraction
        /// comprise. Sert à vérifier que la seule branche qui majore DIRECTEMENT le gain de
        /// Cycles est bien prise en compte par la mesure, plutôt que de le supposer.
        /// </summary>
        public float FinalCleanExitBonus = 1f;

        /// <summary>La machine finale a-t-elle été construite. C'est la vraie fin de partie.</summary>
        public bool Victory;
    }

    /// <summary>
    /// Déroule des runs et des campagnes sur le harnais.
    ///
    /// Le pas de temps est <b>adaptatif</b> : fin au démarrage, où quelques secondes décident du
    /// ressenti, grossier ensuite, où seule la tendance compte. Sans cela, dix heures de jeu à
    /// 100 ms feraient 360 000 pas par run. Le <c>ScriptCycleRunner</c> encaisse les grands pas
    /// sans rien perdre : son <c>AdvanceSlot</c> boucle tant que le cycle est échu.
    /// </summary>
    public static class SimulationRunner
    {
        public static RunResult RunOnce(SimulationHarness h, ISimulationStrategy strategy,
                                        SimulationOptions options)
        {
            var result = new RunResult { StrategyName = strategy.Name };

            RunSummary captured = default;
            bool hasSummary = false;
            using IDisposable subscription = h.Session.OnSessionEnded.Subscribe(summary =>
            {
                captured = summary;
                hasSummary = true;
            });

            h.BeginRun();

            UpgradeModel automationProbe = FindById(h, options.AutomationProbeId);
            float startElapsed = h.Session.RunElapsedSeconds;
            float nextDecision = 0f;
            float nextSample = 0f;
            float markTime = 0f;
            double markMoney = 0d;
            double growth = double.MaxValue;

            while (h.Session.IsGameActive.CurrentValue)
            {
                float elapsed = h.Session.RunElapsedSeconds - startElapsed;
                if (elapsed >= options.MaxRunSeconds)
                {
                    result.TimedOut = true;
                    break;
                }

                h.Tick(ResolveStep(h, elapsed));
                elapsed = h.Session.RunElapsedSeconds - startElapsed;

                if (result.TimeToAutomation < 0f && automationProbe != null && automationProbe.IsAutomated)
                {
                    result.TimeToAutomation = elapsed;
                }

                if (result.MoneyAtProbe < 0d && elapsed >= options.ProbeSeconds)
                {
                    result.MoneyAtProbe = h.Currencies.RunMoneyGenerated.CurrentValue;
                }

                double pending = h.Currencies.CalculatePendingCpuCycles();
                if (result.FirstCycleAtSeconds < 0f && pending >= 1d)
                {
                    result.FirstCycleAtSeconds = elapsed;
                }

                float traceFraction = h.Threat.NormalizedThreat.CurrentValue;
                if (traceFraction > result.PeakTraceFraction) result.PeakTraceFraction = traceFraction;

                if (elapsed >= nextSample)
                {
                    nextSample = elapsed + options.SampleIntervalSeconds;
                    result.Samples.Add(new RunSample
                    {
                        TimeSeconds = elapsed,
                        RunMoney = h.Currencies.RunMoneyGenerated.CurrentValue,
                        TraceFraction = traceFraction,
                        TotalLevels = ResolveTotalLevels(h)
                    });
                }

                // Repère de plateau : la croissance sur la dernière fenêtre glissante.
                if (elapsed - markTime >= options.PlateauWindow * Math.Max(elapsed, 1f))
                {
                    double now = h.Currencies.RunMoneyGenerated.CurrentValue;
                    growth = markMoney > 0d ? now / markMoney : double.MaxValue;
                    markTime = elapsed;
                    markMoney = now;
                }

                var progress = new RunProgress(elapsed, h.Currencies.RunMoneyGenerated.CurrentValue,
                                               growth, traceFraction, pending,
                                               h.Readout.EstimatedMaxFraction.CurrentValue,
                                               h.Readout.LastKnownFraction.CurrentValue);

                float lag = traceFraction - h.Readout.LastKnownFraction.CurrentValue;
                if (lag > result.PeakReadoutLag) result.PeakReadoutLag = lag;

                if (strategy.ShouldExfiltrate(h, progress))
                {
                    result.ExitTraceFraction = traceFraction;
                    result.ExitReadoutFraction = h.Readout.LastKnownFraction.CurrentValue;

                    if (h.Session.TryResolveVoluntaryExit(out RunSummary voluntary))
                    {
                        h.Session.AnnounceRunEnded(voluntary);
                    }
                    break;
                }

                if (elapsed < nextDecision) continue;
                nextDecision = elapsed + ResolveDecisionInterval(elapsed, options);

                strategy.OnDecisionPoint(h);

                float reduction = ResolveReduction(h);
                if (reduction > result.PeakReduction) result.PeakReduction = reduction;

                // Relevé PENDANT la run, jamais après : ResolveRunEnd appelle WipeRun(), qui
                // remet tous les niveaux à zéro. Lu à la sortie de boucle, ce compteur valait
                // toujours SCR_00.
                int top = ResolveTopScript(h);
                if (top > result.TopScriptOrder) result.TopScriptOrder = top;

                int topHw = ResolveTopOrder(h, UpgradeType.Hardware);
                if (topHw > result.TopHardwareOrder) result.TopHardwareOrder = topHw;

                double tflops = h.Upgrades.TotalTFlops.CurrentValue;
                if (tflops > result.PeakTFlops) result.PeakTFlops = tflops;
            }

            result.DurationSeconds = hasSummary ? captured.ElapsedSeconds : h.Session.RunElapsedSeconds;
            result.RunMoney = hasSummary ? captured.DataGenerated : h.Currencies.RunMoneyGenerated.CurrentValue;
            result.CpuCyclesEarned = hasSummary ? captured.CpuCyclesEarned : 0d;
            result.CleanExit = hasSummary && captured.Reason == RunEndReason.CleanExit;
            return result;
        }

        public static CampaignResult RunCampaign(ISimulationStrategy strategy, SimulationOptions options)
        {
            var campaign = new CampaignResult { StrategyName = strategy.Name };
            using var h = new SimulationHarness();

            double budget = options.MaxCampaignHours * 3600d;

            while (campaign.TotalSeconds < budget
                   && campaign.Runs.Count < options.MaxRuns
                   && !IsVictory(campaign))
            {
                RunResult run = RunOnce(h, strategy, options);
                campaign.TotalSeconds += run.DurationSeconds;
                campaign.Runs.Add(run);

                if (campaign.FirstPrestigeSeconds < 0f && run.FirstCycleAtSeconds >= 0f)
                {
                    campaign.FirstPrestigeSeconds = run.FirstCycleAtSeconds;
                }

                h.OpenPrestigeWindow();
                strategy.SpendCpuCycles(h);

                // Progression bloquée : une run qui ne rapporte plus rien ne rapportera pas
                // davantage à la suivante, l'état de départ étant le même.
                if (run.CpuCyclesEarned < 1d && campaign.Runs.Count > 3) break;
            }

            campaign.FinalCleanExitBonus = h.Prestige.CleanExitBonusMultiplier.CurrentValue;
            campaign.Victory = IsVictory(campaign);
            campaign.TreeComplete = IsTreeComplete(h);
            campaign.TreeProgress = ResolveTreeProgress(h);
            return campaign;
        }

        // ------------------------------------------------------------------
        // Outils
        // ------------------------------------------------------------------
        /// <summary>
        /// Pas de temps adaptatif. Il se resserre quand la mort approche : rater l'instant de la
        /// saisie de plusieurs secondes fausserait la durée de run, qui est la mesure centrale.
        /// </summary>
        private static float ResolveStep(SimulationHarness h, float elapsed)
        {
            float step = Math.Min(Math.Max(0.25f, elapsed / 400f), 5f);

            float survival = GreedyStrategy.SurvivalSeconds(h);
            if (survival < 30f)
            {
                step = Math.Min(step, Math.Max(0.1f, survival / 20f));
            }

            return step;
        }

        private static float ResolveDecisionInterval(float elapsed, SimulationOptions options)
        {
            return Math.Min(Math.Max(options.DecisionIntervalSeconds, elapsed / 200f), 10f);
        }

        private static float ResolveReduction(SimulationHarness h)
        {
            float power = h.Upgrades.ProxyDissipationPower.CurrentValue;
            if (power <= 0f) return 0f;

            // Le brut n'est pas exposé : on le reconstitue depuis ce que le ticker additionne.
            float brute = h.CycleRunner.ActiveScriptTracePerSecond
                        + h.Upgrades.HardwareTracePerSecond.CurrentValue
                        + h.Balancing.PassiveTracePerSecond;
            brute *= h.Prestige.TraceReductionMultiplier.CurrentValue;
            if (brute <= 0f) return 0f;

            return h.Balancing.MaxTraceReduction * power
                 / (power + h.Balancing.DissipationHalfPointRatio * brute);
        }

        private static UpgradeModel FindById(SimulationHarness h, string id)
        {
            IReadOnlyList<UpgradeModel> scripts = h.Upgrades.GetUpgradesOfType(UpgradeType.Script);
            for (int i = 0; i < scripts.Count; i++)
            {
                if (scripts[i].Config.Id == id) return scripts[i];
            }
            return null;
        }

        /// <summary>Niveaux cumulés des trois piliers. Le compteur d'activité du joueur.</summary>
        private static int ResolveTotalLevels(SimulationHarness h)
        {
            return CountLevels(h, UpgradeType.Script)
                 + CountLevels(h, UpgradeType.Hardware)
                 + CountLevels(h, UpgradeType.Proxy);
        }

        private static int CountLevels(SimulationHarness h, UpgradeType type)
        {
            IReadOnlyList<UpgradeModel> all = h.Upgrades.GetUpgradesOfType(type);
            int total = 0;
            for (int i = 0; i < all.Count; i++) total += all[i].CurrentLevel.CurrentValue;
            return total;
        }

        private static int ResolveTopScript(SimulationHarness h)
        {
            return ResolveTopOrder(h, UpgradeType.Script);
        }

        private static int ResolveTopOrder(SimulationHarness h, UpgradeType type)
        {
            IReadOnlyList<UpgradeModel> all = h.Upgrades.GetUpgradesOfType(type);
            int top = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsOwned && all[i].Config.Order > top) top = all[i].Config.Order;
            }
            return top;
        }

        /// <summary>
        /// La campagne est GAGNÉE quand le joueur a construit la machine finale.
        ///
        /// Remplace « l'arbre de prestige est complet », qui était une commodité d'outillage
        /// prise à tort pour une règle du jeu. Le lore est le hack d'une clé USB surpuissante :
        /// la fin se mesure en capacité de calcul, et l'arbre n'est qu'un moyen. Mesuré avec
        /// l'ancien critère, la campagne s'arrêtait à SCR_07 sur 15 — elle finissait à
        /// mi-parcours du contenu réel, et toute calibration faite dessus mesurait un autre jeu.
        /// </summary>
        private static bool IsVictory(CampaignResult campaign)
        {
            for (int i = 0; i < campaign.Runs.Count; i++)
            {
                if (campaign.Runs[i].TopHardwareOrder >= FinalHardwareOrder) return true;
            }
            return false;
        }

        /// <summary>Rang du dernier Hardware, « Architecture IA Non Alignée ».</summary>
        private const int FinalHardwareOrder = 15;

        private static bool IsTreeComplete(SimulationHarness h)
        {
            IReadOnlyList<PrestigeConfigSO> all = h.PrestigeCatalog.GetAllUpgrades();
            for (int i = 0; i < all.Count; i++)
            {
                if (h.Prestige.GetLevel(all[i].Id) < all[i].MaxLevel) return false;
            }
            return true;
        }

        private static float ResolveTreeProgress(SimulationHarness h)
        {
            IReadOnlyList<PrestigeConfigSO> all = h.PrestigeCatalog.GetAllUpgrades();
            int owned = 0;
            int total = 0;
            for (int i = 0; i < all.Count; i++)
            {
                owned += h.Prestige.GetLevel(all[i].Id);
                total += all[i].MaxLevel;
            }
            return total > 0 ? (float)owned / total : 0f;
        }
    }
}
