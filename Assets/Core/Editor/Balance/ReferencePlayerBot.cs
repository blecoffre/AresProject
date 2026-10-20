using Core.Economy.Data;
using Core.Models.Economy;
using Core.Models.Simulation;
using Core.Services.Economy;
using Core.Services.Security;
using Core.Services.Simulation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Le joueur de référence du GD — « joueur actif moyen » — rejoué par un programme. Il ne
    /// voit que ce que voit un humain : l'AIGUILLE et la borne pessimiste du brouillard, jamais la
    /// vraie jauge, et la sortie verrouillée tant que la run ne rapporte pas un point.
    ///
    /// <b>Ses gestes, dans l'ordre d'une décision :</b>
    /// <list type="number">
    /// <item>relancer les Scripts manuels arrêtés ;</item>
    /// <item>sortir si l'aiguille atteint la sortie visée (50 % à la run 1, 75 % ensuite), ou si la
    /// borne pessimiste frôle la saisie ;</item>
    /// <item>sortie encore verrouillée et danger : Data Wiper, puis Overclock « pour se sortir d'un
    /// mauvais pas » si le premier point est à portée ;</item>
    /// <item>juste sous la sortie visée : quelques Overclocks « pour forcer le palier » ;</item>
    /// <item>un Proxy, s'il rapporte plus qu'il ne coûte sans gaspiller son Wipe, dans la limite
    /// d'un plafond par run ;</item>
    /// <item>les générateurs, au meilleur gain par Data (choix de Bertrand) : attente + amortissement
    /// le plus court, quitte à économiser pour le meilleur.</item>
    /// </list>
    ///
    /// Entre deux runs, il dépense ses points dans l'ordre « Blindage d'abord ».
    ///
    /// ⚠️ Pour CHOISIR ses achats, le bot estime le revenu avec sa propre copie des formules des
    /// Scripts (<see cref="ScriptYieldAt"/>, <see cref="ScriptDurationAt"/>). La simulation, elle,
    /// passe par le vrai <c>UpgradeModel</c> ; une dérive entre les deux est détectée et consignée
    /// dans les avertissements du rapport (<see cref="VerifyIncomeModel"/>).
    /// </summary>
    internal sealed class ReferencePlayerBot
    {
        /// <summary>
        /// « Blindage d'abord », puis l'Économie, l'Extraction, l'Interception, le reste, et
        /// « Recompilation Forcée » en dernier (choix de Bertrand, 2026-10-15). Un nœud du catalogue
        /// absent de cette liste est acheté juste avant P_CORE_OVERRIDE, avec un avertissement.
        /// </summary>
        private static readonly string[] PrestigePriority =
        {
            "P_ROOT",
            "P_HARDEN_1", "P_HARDEN_2", "P_HARDEN_3", "P_HARDEN_4",
            "P_REFAC", "P_DATAM",
            "P_EXTRACT_1", "P_EXTRACT_2", "P_EXTRACT_3",
            "P_INTERCEPT_1", "P_INTERCEPT_2", "P_INTERCEPT_3", "P_INTERCEPT_4",
            "P_EXTRACT_HIGHRISK",
            "P_EMERG",
            "P_STARTING_FUNDS_1", "P_STARTING_FUNDS_2",
            "P_CLICK", "P_OVERCLOCK_POWER_1", "P_OVERCLOCK_POWER_2", "P_OVERCLOCK_POWER_3",
            "P_OVERCLOCK_SPACEBAR", "P_OVERCLOCK_CRIT",
            "P_ALERT_STATE",
            "P_FIREWALL_ROUTINE",
            "P_CORE_OVERRIDE"
        };

        private const string SinkNodeId = "P_CORE_OVERRIDE";

        /// <summary>Garde-fou : achats de générateurs par décision.</summary>
        private const int MaxPurchasesPerDecision = 200;

        /// <summary>Une vérification du modèle de revenu toutes les N décisions (~2 min de jeu).</summary>
        private const int VerifyEveryDecisions = 240;

        private readonly CampaignSettings _settings;
        private readonly BalancingConfigSO _balancing;
        private readonly UserCurrencies _currencies;
        private readonly UpgradeManager _upgrades;
        private readonly PrestigeManager _prestige;
        private readonly ThreatManager _threat;
        private readonly TraceReadout _readout;
        private readonly ExfiltrationSystem _exfiltration;
        private readonly EmergencyProtocolSystem _emergency;
        private readonly OverclockSystem _overclock;
        private readonly ScriptCycleRunner _runner;
        private readonly GameSessionManager _session;
        private readonly List<string> _warnings;
        private readonly StringBuilder _trace;

        private readonly List<PrestigeConfigSO> _prestigeOrder = new List<PrestigeConfigSO>();

        /// <summary>Temps perdu en moyenne à relancer un Script manuel : la moitié d'un intervalle de décision.</summary>
        private readonly double _manualIdleSeconds;

        private int _runIndex;
        private bool _tracing;
        private int _preExitOverclocks;
        private int _proxiesBought;
        private int _decisions;
        private bool _incomeDriftReported;

        /// <summary>Revenu estimé au début de la décision en cours, en Datas par seconde.</summary>
        private double _income;

        public ReferencePlayerBot(
            CampaignSettings settings,
            BalancingConfigSO balancing,
            PrestigeCatalogSO prestigeCatalog,
            UserCurrencies currencies,
            UpgradeManager upgrades,
            PrestigeManager prestige,
            ThreatManager threat,
            TraceReadout readout,
            ExfiltrationSystem exfiltration,
            EmergencyProtocolSystem emergency,
            OverclockSystem overclock,
            ScriptCycleRunner runner,
            GameSessionManager session,
            List<string> warnings,
            StringBuilder trace)
        {
            _settings = settings;
            _balancing = balancing;
            _currencies = currencies;
            _upgrades = upgrades;
            _prestige = prestige;
            _threat = threat;
            _readout = readout;
            _exfiltration = exfiltration;
            _emergency = emergency;
            _overclock = overclock;
            _runner = runner;
            _session = session;
            _warnings = warnings;
            _trace = trace;

            _manualIdleSeconds = Math.Max(0d, settings.DecisionIntervalSeconds * 0.5d);

            BuildPrestigeOrder(prestigeCatalog);
        }

        /// <summary>Une run commence : les compteurs propres à la run repartent de zéro.</summary>
        public void BeginRun(int runIndex)
        {
            _runIndex = runIndex;
            _preExitOverclocks = 0;
            _proxiesBought = 0;
            _tracing = _trace != null && runIndex <= _settings.TraceRuns;

            if (_tracing) _trace.AppendLine().AppendLine($"=== Run {runIndex} ===");
        }

        private float CurrentExitTarget => _runIndex <= 1 ? _settings.FirstRunExitFraction : _settings.ExitFraction;

        private bool IsRunActive => _session.IsGameActive.CurrentValue;

        public void Decide()
        {
            if (!IsRunActive) return;

            _decisions++;
            _income = ComputeIncome(_upgrades.NetTFlops.CurrentValue);

            if (_decisions % VerifyEveryDecisions == 0) VerifyIncomeModel();

            RestartManualScripts();
            if (TryExit()) return;

            HandleDangerWhileLocked();
            if (!IsRunActive) return;

            // L'Overclock d'évasion a pu ouvrir la sortie : on la prend tout de suite.
            if (TryExit()) return;

            TryPreExitOverclock();
            if (!IsRunActive) return;

            TryBuyProxy();
            BuyGenerators();
        }

        // ---- Pendant la run ---------------------------------------------------------------------

        private void RestartManualScripts()
        {
            IReadOnlyList<UpgradeModel> scripts = _upgrades.GetUpgradesOfType(UpgradeType.Script);
            for (int i = 0; i < scripts.Count; i++)
            {
                UpgradeModel model = scripts[i];
                if (!model.IsOwned || model.IsAutomated) continue;

                // Sans effet si le cycle tourne déjà : c'est le runner qui tranche.
                _runner.TryStartCycle(model.Config.Id);
            }
        }

        /// <summary>
        /// La sortie, sur la VRAIE règle : au moins un point à gagner. En éditeur, le bouton du jeu
        /// reste cliquable à vide (<c>ExfiltrationSystem.IsClickable</c>) ; le bot, lui, ne
        /// s'autorise pas ce raccourci de développement.
        /// </summary>
        private bool TryExit()
        {
            if (!_exfiltration.IsUnlocked.CurrentValue) return false;

            float needle = _readout.LastKnownFraction.CurrentValue;
            float worst = _readout.EstimatedMaxFraction.CurrentValue;
            if (needle < CurrentExitTarget && worst < _settings.DangerFraction) return false;

            if (!_exfiltration.TryBeginExfiltration(out RunSummary summary)) return false;

            if (_tracing) Log($"SORTIE à {summary.ThreatAtEnd:P0} de vraie jauge : {summary.CpuCyclesEarned:0} point(s)");
            _exfiltration.CompleteExfiltration(summary);
            return true;
        }

        /// <summary>
        /// Sortie verrouillée et saisie qui approche : le Data Wiper d'abord, s'il est débloqué ;
        /// sinon un Overclock de désespoir, si le premier point de la run est à portée.
        /// </summary>
        private void HandleDangerWhileLocked()
        {
            if (_exfiltration.IsUnlocked.CurrentValue) return;

            float worst = _readout.EstimatedMaxFraction.CurrentValue;

            if (_settings.UseDataWiper && worst >= _settings.WiperFraction
                && _emergency.IsUnlocked && !_emergency.IsExhausted)
            {
                float wiped = _emergency.TryTriggerEmergency();
                if (_tracing) Log($"DATA WIPER : −{wiped:0} UT, sacrifice {_emergency.SacrificedFraction:P0}");
                return;
            }

            if (_settings.EscapeOverclockReach <= 0 || worst < _settings.DangerFraction) return;

            double missing = _exfiltration.GetFirstCycleRunThreshold() - _currencies.RunMoneyGenerated.CurrentValue;
            double perPress = _income * _overclock.CurrentAdvanceSeconds;

            if (missing > 0d && missing <= perPress * _settings.EscapeOverclockReach)
            {
                OverclockResult result = _overclock.TriggerManualOverclock();
                if (_tracing) Log($"OVERCLOCK d'évasion ({result.Outcome}) : il manquait {missing:0} Datas pour le premier point");
            }
        }

        /// <summary>
        /// « Forcer un palier juste avant la sortie » : l'aiguille est juste sous la sortie visée,
        /// la pénalité de l'Overclock pousse la jauge vers le palier, et l'avance rapporte au passage.
        /// </summary>
        private void TryPreExitOverclock()
        {
            if (_preExitOverclocks >= _settings.PreExitOverclocks) return;
            if (!_exfiltration.IsUnlocked.CurrentValue) return;

            float needle = _readout.LastKnownFraction.CurrentValue;
            float target = CurrentExitTarget;
            if (needle >= target || needle < target - _settings.PreExitWindow) return;

            OverclockResult result = _overclock.TriggerManualOverclock();
            if (result.Outcome == OverclockOutcome.Applied) _preExitOverclocks++;
            if (_tracing) Log($"OVERCLOCK avant la sortie ({result.Outcome}) : +{result.SecondsGained:0.#} s, jauge +{result.TracePenalty:0} UT");
        }

        /// <summary>
        /// Un Proxy par décision au plus : son Wipe ne recale l'aiguille qu'au tick suivant, et en
        /// acheter un second sur l'aiguille d'avant gaspillerait son effet.
        /// </summary>
        private void TryBuyProxy()
        {
            if (_settings.MaxProxiesPerRun >= 0 && _proxiesBought >= _settings.MaxProxiesPerRun) return;

            double money = _currencies.Money.Amount.CurrentValue;
            if (money <= 0d || _income <= 0d) return;

            double speed = _threat.CurrentSpeed.CurrentValue;
            double interception = 1d + Math.Max(0f, _prestige.TotalInterceptBonus.CurrentValue);
            double knownTrace = _readout.LastKnownTrace;

            UpgradeModel best = null;
            double bestSecondsPerData = 0d;

            IReadOnlyList<UpgradeModel> proxies = _upgrades.GetUpgradesOfType(UpgradeType.Proxy);
            for (int i = 0; i < proxies.Count; i++)
            {
                UpgradeModel model = proxies[i];
                double cost = model.GetCumulativeCost(1);

                if (cost > 0d && cost <= money)
                {
                    double seconds = model.GetWipeSecondsForNextLevels(1) * interception;
                    double wipe = seconds * speed;

                    bool notWasted = knownTrace >= wipe * _settings.ProxyWasteTolerance;
                    bool paysBack = _income * seconds >= cost * _settings.ProxyPaybackFactor;

                    if (seconds > 0d && notWasted && paysBack)
                    {
                        double secondsPerData = seconds / cost;
                        if (secondsPerData > bestSecondsPerData)
                        {
                            best = model;
                            bestSecondsPerData = secondsPerData;
                        }
                    }
                }

                // Visible = possédé, plus le premier jamais acheté.
                if (!model.IsOwned) break;
            }

            if (best == null) return;

            double price = best.GetCumulativeCost(1);
            if (_upgrades.TryPurchaseUpgrade(best.Config.Id, BuyQuantity.X1))
            {
                _proxiesBought++;
                if (_tracing) Log($"achète {best.Config.Id} → niv. {best.CurrentLevel.CurrentValue} ({price:G4} Datas)");
            }
        }

        /// <summary>
        /// Meilleur gain par Data : parmi les Scripts et Hardware visibles, celui dont
        /// <c>attente + prix ÷ gain de revenu</c> est le plus court. Acheté s'il est payable ; sinon
        /// le bot économise pour lui, sans se rabattre sur un achat moins rentable.
        /// </summary>
        private void BuyGenerators()
        {
            for (int guard = 0; guard < MaxPurchasesPerDecision; guard++)
            {
                double money = _currencies.Money.Amount.CurrentValue;
                double netTFlops = _upgrades.NetTFlops.CurrentValue;
                double income = ComputeIncome(netTFlops);

                UpgradeModel best = null;
                double bestScore = double.PositiveInfinity;
                double bestCost = 0d;

                IReadOnlyList<UpgradeModel> scripts = _upgrades.GetUpgradesOfType(UpgradeType.Script);
                for (int i = 0; i < scripts.Count; i++)
                {
                    UpgradeModel model = scripts[i];
                    int level = model.CurrentLevel.CurrentValue;
                    double gain = ScriptRate(model.Config, level + 1, netTFlops) - ScriptRate(model.Config, level, netTFlops);

                    Consider(model, gain, money, income, ref best, ref bestScore, ref bestCost);
                    if (!model.IsOwned) break;
                }

                double netPerGross = NetTFlopsPerGross();
                IReadOnlyList<UpgradeModel> hardware = _upgrades.GetUpgradesOfType(UpgradeType.Hardware);
                for (int i = 0; i < hardware.Count; i++)
                {
                    UpgradeModel model = hardware[i];
                    double addedNet = model.GetYieldGainForNextLevels(1) * netPerGross;
                    double gain = ComputeIncome(netTFlops + addedNet) - income;

                    Consider(model, gain, money, income, ref best, ref bestScore, ref bestCost);
                    if (!model.IsOwned) break;
                }

                if (best == null || bestCost > money) return;
                if (!_upgrades.TryPurchaseUpgrade(best.Config.Id, BuyQuantity.X1)) return;

                if (_tracing) Log($"achète {best.Config.Id} → niv. {best.CurrentLevel.CurrentValue} ({bestCost:G4} Datas, score {bestScore:0.#} s)");
            }
        }

        private static void Consider(UpgradeModel model, double gain, double money, double income,
                                     ref UpgradeModel best, ref double bestScore, ref double bestCost)
        {
            if (gain <= 0d) return;

            double cost = model.GetCumulativeCost(1);
            if (cost <= 0d) return;

            double wait = cost <= money
                ? 0d
                : (income > 0d ? (cost - money) / income : double.PositiveInfinity);

            double score = wait + cost / gain;
            if (score < bestScore)
            {
                best = model;
                bestScore = score;
                bestCost = cost;
            }
        }

        // ---- Entre deux runs ----------------------------------------------------------------------

        /// <summary>
        /// Dépense les points dans l'ordre de priorité, un rang à la fois. Avec
        /// <see cref="CampaignSettings.SaveForPriorityNode"/>, le premier nœud ouvert qu'on ne peut
        /// pas payer arrête les achats : le bot économise pour lui.
        ///
        /// Rend les achats sous forme compacte (« P_HARDEN_2 P_REFAC×3 »), vide s'il n'y en a pas :
        /// la colonne <c>achats_arbre</c> du CSV.
        /// </summary>
        public string SpendPrestige()
        {
            var bought = new StringBuilder();
            PrestigeConfigSO current = null;
            int ranks = 0;

            // Les points d'une campagne se comptent par centaines : la garde ne borne rien de réel.
            for (int guard = 0; guard < 100_000; guard++)
            {
                PrestigeConfigSO node = TryBuyNextPrestigeRank();
                if (node == null) break;

                if (node != current)
                {
                    AppendPurchase(bought, current, ranks);
                    current = node;
                    ranks = 0;
                }

                ranks++;
            }

            AppendPurchase(bought, current, ranks);
            return bought.ToString();
        }

        private static void AppendPurchase(StringBuilder bought, PrestigeConfigSO node, int ranks)
        {
            if (node == null || ranks <= 0) return;

            if (bought.Length > 0) bought.Append(' ');
            bought.Append(node.Id);
            if (ranks > 1) bought.Append('×').Append(ranks.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Achète le prochain rang selon la priorité ; rend le nœud acheté, ou null.</summary>
        private PrestigeConfigSO TryBuyNextPrestigeRank()
        {
            for (int i = 0; i < _prestigeOrder.Count; i++)
            {
                PrestigeConfigSO node = _prestigeOrder[i];
                if (_prestige.GetLevel(node.Id) >= node.MaxLevel) continue;
                if (!_prestige.IsUnlocked(node)) continue;

                if (_prestige.TryPurchasePrestige(node.Id))
                {
                    if (_tracing) _trace.AppendLine($"  arbre : {node.Id} → niv. {_prestige.GetLevel(node.Id)}");
                    return node;
                }

                if (_settings.SaveForPriorityNode) return null;
            }

            return null;
        }

        private void BuildPrestigeOrder(PrestigeCatalogSO catalog)
        {
            IReadOnlyList<PrestigeConfigSO> all = catalog.GetAllUpgrades();
            var placed = new HashSet<string>();

            for (int p = 0; p < PrestigePriority.Length; p++)
            {
                PrestigeConfigSO node = FindNode(all, PrestigePriority[p]);
                if (node == null)
                {
                    _warnings.Add($"Nœud {PrestigePriority[p]} absent du catalogue : ignoré par le bot.");
                    continue;
                }

                _prestigeOrder.Add(node);
                placed.Add(node.Id);
            }

            // Un nœud ajouté à l'arbre sans que le bot le connaisse : acheté avant le puits à points,
            // pour que la complétion de l'arbre reste mesurable.
            int sinkIndex = _prestigeOrder.FindIndex(n => n.Id == SinkNodeId);
            for (int i = 0; i < all.Count; i++)
            {
                PrestigeConfigSO node = all[i];
                if (node == null || placed.Contains(node.Id)) continue;

                _warnings.Add($"Nœud {node.Id} absent de l'ordre du bot : acheté juste avant {SinkNodeId}.");

                if (sinkIndex >= 0) _prestigeOrder.Insert(sinkIndex++, node);
                else _prestigeOrder.Add(node);

                placed.Add(node.Id);
            }
        }

        private static PrestigeConfigSO FindNode(IReadOnlyList<PrestigeConfigSO> all, string id)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].Id == id) return all[i];
            }

            return null;
        }

        /// <summary>Une ligne de trace, avec l'état que voit le joueur à cet instant. Rien hors des runs tracées.</summary>
        private void Log(string action)
        {
            if (!_tracing) return;

            _trace.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[{0,7:0.0} s | aiguille {1,4:P0} borne {2,4:P0} | {3,10:G4} Datas | {4,9:G4}/s | {5,9:G4} TF] {6}",
                _session.RunElapsedSeconds,
                _readout.LastKnownFraction.CurrentValue,
                _readout.EstimatedMaxFraction.CurrentValue,
                _currencies.Money.Amount.CurrentValue,
                _income,
                _upgrades.NetTFlops.CurrentValue,
                action));
        }

        // ---- Le modèle de revenu du bot ----------------------------------------------------------

        /// <summary>TFlops nettes apportées par une TFlop brute : P_DATAM × Noyau × (1 − sacrifice).</summary>
        private double NetTFlopsPerGross()
        {
            return _prestige.GlobalComputeMultiplier.CurrentValue
                 * (double)_prestige.TotalYieldBonus.CurrentValue
                 * (1d - _emergency.SacrificedFraction);
        }

        /// <summary>Revenu de tous les Scripts possédés, à la capacité nette donnée, relance manuelle comprise.</summary>
        private double ComputeIncome(double netTFlops)
        {
            double income = 0d;

            IReadOnlyList<UpgradeModel> scripts = _upgrades.GetUpgradesOfType(UpgradeType.Script);
            for (int i = 0; i < scripts.Count; i++)
            {
                UpgradeModel model = scripts[i];
                if (!model.IsOwned) continue;

                income += ScriptRate(model.Config, model.CurrentLevel.CurrentValue, netTFlops);
            }

            return income;
        }

        /// <summary>Datas par seconde d'un Script au niveau donné ; un Script manuel attend qu'on le relance.</summary>
        private double ScriptRate(UpgradeConfigSO config, int level, double netTFlops)
        {
            if (level <= 0) return 0d;

            double duration = ScriptDurationAt(config, level, netTFlops);
            if (level < Math.Max(1, config.AutomationLevel)) duration += _manualIdleSeconds;

            return ScriptYieldAt(config, level, netTFlops) / duration;
        }

        /// <summary>Copie de <c>UpgradeModel.ComputeYield</c> pour un Script : paliers, TFlops, Noyau.</summary>
        private double ScriptYieldAt(UpgradeConfigSO config, int level, double netTFlops)
        {
            double power = level;

            IReadOnlyList<UpgradeMilestone> milestones = config.Milestones;
            for (int i = 0; i < milestones.Count; i++)
            {
                if (level >= milestones[i].Level && milestones[i].Effect == MilestoneEffect.YieldMultiplier)
                {
                    power *= milestones[i].Factor;
                }
            }

            return config.BaseProductionYield * power
                 * Math.Pow(1d + netTFlops, _balancing.TFlopsYieldExponent)
                 * _prestige.TotalYieldBonus.CurrentValue;
        }

        /// <summary>Copie de la durée de <c>UpgradeModel.RecalculateCache</c> : paliers, compression, plancher.</summary>
        private double ScriptDurationAt(UpgradeConfigSO config, int level, double netTFlops)
        {
            float duration = config.BaseCycleDuration;

            IReadOnlyList<UpgradeMilestone> milestones = config.Milestones;
            for (int i = 0; i < milestones.Count; i++)
            {
                if (level >= milestones[i].Level && milestones[i].Effect == MilestoneEffect.DurationMultiplier)
                {
                    duration *= milestones[i].Factor;
                }
            }

            double compressed = duration / Math.Pow(1d + netTFlops * _balancing.TFlopsTimeCompression,
                                                    _balancing.TFlopsCompressionExponent);

            return Math.Max(_balancing.AbsoluteMinCycleDuration, (float)compressed);
        }

        /// <summary>
        /// Compare la copie des formules au vrai modèle sur les Scripts possédés. Une dérive veut
        /// dire qu'une formule du jeu a changé sans que le bot suive : ses choix d'achat deviennent
        /// faux, même si la simulation, elle, reste juste. Consignée une fois.
        /// </summary>
        private void VerifyIncomeModel()
        {
            if (_incomeDriftReported) return;

            double netTFlops = _upgrades.NetTFlops.CurrentValue;
            IReadOnlyList<UpgradeModel> scripts = _upgrades.GetUpgradesOfType(UpgradeType.Script);

            for (int i = 0; i < scripts.Count; i++)
            {
                UpgradeModel model = scripts[i];
                if (!model.IsOwned) continue;

                int level = model.CurrentLevel.CurrentValue;
                double yield = ScriptYieldAt(model.Config, level, netTFlops);
                double duration = ScriptDurationAt(model.Config, level, netTFlops);

                if (!IsClose(yield, model.GetCurrentYield(), 1e-6) || !IsClose(duration, model.GetCurrentCycleDuration(), 1e-4))
                {
                    _incomeDriftReported = true;
                    _warnings.Add(
                        $"Le modèle de revenu du bot diverge du jeu sur {model.Config.Id} (niveau {level}) : " +
                        $"versement {yield:G6} contre {model.GetCurrentYield():G6}, durée {duration:G6} contre " +
                        $"{model.GetCurrentCycleDuration():G6}. Mettre à jour ReferencePlayerBot.");
                    return;
                }
            }
        }

        private static bool IsClose(double a, double b, double relativeTolerance)
        {
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            return scale <= 0d || Math.Abs(a - b) <= scale * relativeTolerance;
        }
    }
}
