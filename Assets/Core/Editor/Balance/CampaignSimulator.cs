using Core.Economy.Data;
using Core.Models.Economy;
using Core.Models.Simulation;
using Core.Services.Economy;
using Core.Services.Platform;
using Core.Services.Security;
using Core.Services.Simulation;
using R3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Le harnais d'équilibrage de la session « chiffres » (lot C3, 2026-10-20) : une campagne
    /// entière, de la première run à la victoire, jouée hors Play Mode par le joueur de référence.
    ///
    /// <b>Il fait tourner les VRAIS systèmes du jeu</b>, composés dans un conteneur VContainer comme
    /// le fait le <c>RootLifetimeScope</c> : économie, jauge, brouillard, sortie, prestige, victoire.
    /// Seules trois choses sont remplacées — le temps (<see cref="SimulatedClock"/>), le hasard
    /// (<see cref="SeededRandomSource"/>) et le BalancingConfig, cloné pour y poser les leviers sans
    /// jamais toucher l'asset. Ce que la simulation mesure est donc le jeu ; ce qu'elle ne mesure pas
    /// est le joueur, d'où le calage sur le journal des runs jouées.
    ///
    /// Remplace le simulateur Python et le harnais Unity supprimés le 2026-10-13, qui recopiaient
    /// les formules et avaient fini par diverger du jeu.
    /// </summary>
    public static class CampaignSimulator
    {
        public const string BalancingPath = "Assets/GameData/Balancing/BalancingConfig.asset";
        public const string UpgradeCatalogPath = "Assets/GameData/Catalogs/UpgradeCatalog.asset";
        public const string PrestigeCatalogPath = "Assets/GameData/Catalogs/PrestigeCatalog.asset";

        private const string SinkNodeId = "P_CORE_OVERRIDE";

        /// <summary>Paliers de TFlops nettes dont le rapport donne le premier franchissement.</summary>
        private static readonly double[] TFlopsMilestones = { 1e3, 1e6, 1e9, 1e10, 1e11, 5e11, 1e12 };

        /// <summary>
        /// Déroule une campagne. Synchrone : quelques secondes à quelques dizaines de secondes selon
        /// sa longueur. <paramref name="progress"/> reçoit l'avancement (0 à 1) à chaque fin de run.
        /// </summary>
        public static CampaignResult Run(CampaignSettings settings, Action<float, string> progress = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.StepSeconds <= 0f || settings.DecisionIntervalSeconds <= 0f)
            {
                throw new ArgumentException("Le pas de simulation et l'intervalle de décision doivent être positifs.");
            }

            BalancingConfigSO balancingAsset = LoadAsset<BalancingConfigSO>(BalancingPath);
            UpgradeCatalogSO upgradeCatalog = LoadAsset<UpgradeCatalogSO>(UpgradeCatalogPath);
            PrestigeCatalogSO prestigeCatalog = LoadAsset<PrestigeCatalogSO>(PrestigeCatalogPath);

            BalancingConfigSO balancing = CloneWithLevers(balancingAsset, settings);

            // Les systèmes journalisent leurs événements (saisie, Data Wiper, victoire) : sur une
            // campagne de cent runs, la console serait noyée. Rétabli quoi qu'il arrive.
            bool logWasEnabled = UnityEngine.Debug.unityLogger.logEnabled;
            UnityEngine.Debug.unityLogger.logEnabled = false;

            var watch = Stopwatch.StartNew();
            try
            {
                using var session = new SimulatedCampaign(settings, balancing, upgradeCatalog, prestigeCatalog, progress);
                CampaignResult result = session.Play();
                result.WallClockSeconds = watch.Elapsed.TotalSeconds;
                return result;
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logEnabled = logWasEnabled;
                Object.DestroyImmediate(balancing);
            }
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Asset introuvable : {path}");
            return asset;
        }

        /// <summary>
        /// Une copie en mémoire du BalancingConfig, leviers posés. L'asset reste intact : c'est la
        /// seule façon de comparer deux réglages sans risquer d'en laisser un dans le projet.
        /// </summary>
        private static BalancingConfigSO CloneWithLevers(BalancingConfigSO asset, CampaignSettings settings)
        {
            BalancingConfigSO clone = Object.Instantiate(asset);
            clone.hideFlags = HideFlags.HideAndDontSave;

            var so = new SerializedObject(clone);
            SetDouble(so, "_victoryTFlops", settings.VictoryTFlops);
            SetDouble(so, "_prestigeFirstThresholdDatas", settings.PrestigeFirstThresholdDatas);
            SetDouble(so, "_prestigeThresholdGrowth", settings.PrestigeThresholdGrowth);
            SetFloat(so, "_proxyWipeScale", settings.ProxyWipeScale);
            SetFloat(so, "_amiSpeedExponent", settings.AmiSpeedExponent);
            so.ApplyModifiedPropertiesWithoutUndo();

            return clone;
        }

        private static void SetDouble(SerializedObject so, string field, double value)
        {
            Require(so, field).doubleValue = value;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            Require(so, field).floatValue = value;
        }

        private static SerializedProperty Require(SerializedObject so, string field)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Champ {field} introuvable dans BalancingConfigSO : le harnais doit suivre son renommage.");
            }

            return property;
        }

        /// <summary>Points de l'arbre utile : tous les nœuds à plafond, P_CORE_OVERRIDE exclu.</summary>
        internal static int CountUsefulTreePoints(PrestigeCatalogSO catalog)
        {
            double total = 0d;
            IReadOnlyList<PrestigeConfigSO> all = catalog.GetAllUpgrades();
            for (int i = 0; i < all.Count; i++)
            {
                PrestigeConfigSO node = all[i];
                if (node == null || node.IsUnlimited || node.Id == SinkNodeId) continue;
                total += node.GetTotalCostOfLevels(node.MaxLevel);
            }

            return (int)Math.Round(total);
        }

        /// <summary>
        /// Une campagne en cours : le conteneur, ses services, le bot et la boucle. Jetable — le
        /// conteneur dispose les services, et les abonnements du harnais tombent avec lui.
        /// </summary>
        private sealed class SimulatedCampaign : IDisposable
        {
            private readonly CampaignSettings _settings;
            private readonly PrestigeCatalogSO _prestigeCatalog;
            private readonly Action<float, string> _progress;

            private readonly SimulatedClock _clock = new SimulatedClock();
            private readonly IObjectResolver _resolver;

            private readonly UserCurrencies _currencies;
            private readonly UpgradeManager _upgrades;
            private readonly PrestigeManager _prestige;
            private readonly ThreatManager _threat;
            private readonly TraceReadout _readout;
            private readonly EmergencyProtocolSystem _emergency;
            private readonly GameSessionManager _session;
            private readonly ScriptCycleRunner _runner;
            private readonly SimulationTicker _ticker;
            private readonly VictorySystem _victory;
            private readonly ReferencePlayerBot _bot;

            private readonly CampaignResult _result = new CampaignResult();
            private DisposableBag _disposables;

            // État de la run en cours, compté comme le fait RunJournal.
            private int _runIndex = 1;
            private double _peakNet;
            private double _peakGross;
            private int _overclocks;
            private int _proxies;
            private double _proxySeconds;
            private bool _runEnded;
            private bool _victoryReached;
            private int _nextMilestone;

            /// <summary>Le record de TFlops nettes de toute la campagne : l'avancement vers la victoire.</summary>
            private double _bestNet;

            public SimulatedCampaign(
                CampaignSettings settings,
                BalancingConfigSO balancing,
                UpgradeCatalogSO upgradeCatalog,
                PrestigeCatalogSO prestigeCatalog,
                Action<float, string> progress)
            {
                _settings = settings;
                _prestigeCatalog = prestigeCatalog;
                _progress = progress;

                _result.Settings = settings;
                _result.UsefulTreePoints = CountUsefulTreePoints(prestigeCatalog);

                // Les mêmes enregistrements que le RootLifetimeScope (et le SimulationTicker du
                // scope de scène), moins tout ce qui touche à l'affichage, au son, à la sauvegarde
                // et au journal. Un constructeur qui gagne une dépendance déjà enregistrée ici ne
                // demande rien au harnais ; une dépendance NOUVELLE fera échouer Build() — c'est voulu.
                var builder = new ContainerBuilder();
                builder.RegisterInstance(upgradeCatalog);
                builder.RegisterInstance(prestigeCatalog);
                builder.RegisterInstance(balancing);
                builder.RegisterInstance(_clock).As<ITimeSource>().As<IRealtimeClock>();
                builder.RegisterInstance<IRandomSource>(new SeededRandomSource(settings.Seed));

                builder.Register<UserCurrencies>(Lifetime.Singleton);
                builder.Register<UpgradeManager>(Lifetime.Singleton);
                builder.Register<PrestigeManager>(Lifetime.Singleton);
                builder.Register<ThreatManager>(Lifetime.Singleton);
                builder.Register<TraceReadout>(Lifetime.Singleton);
                builder.Register<EmergencyProtocolSystem>(Lifetime.Singleton);
                builder.Register<GameSessionManager>(Lifetime.Singleton);
                builder.Register<OverclockSystem>(Lifetime.Singleton);
                builder.Register<FirewallRoutine>(Lifetime.Singleton);
                builder.Register<ExfiltrationSystem>(Lifetime.Singleton);
                builder.Register<VictorySystem>(Lifetime.Singleton);
                builder.Register<ScriptCycleRunner>(Lifetime.Singleton);
                builder.Register<SimulationTicker>(Lifetime.Singleton);

                _resolver = builder.Build();

                _currencies = _resolver.Resolve<UserCurrencies>();
                _upgrades = _resolver.Resolve<UpgradeManager>();
                _prestige = _resolver.Resolve<PrestigeManager>();
                _threat = _resolver.Resolve<ThreatManager>();
                _readout = _resolver.Resolve<TraceReadout>();
                _emergency = _resolver.Resolve<EmergencyProtocolSystem>();
                _session = _resolver.Resolve<GameSessionManager>();
                _runner = _resolver.Resolve<ScriptCycleRunner>();
                _ticker = _resolver.Resolve<SimulationTicker>();
                _victory = _resolver.Resolve<VictorySystem>();

                var exfiltration = _resolver.Resolve<ExfiltrationSystem>();
                var overclock = _resolver.Resolve<OverclockSystem>();
                var firewall = _resolver.Resolve<FirewallRoutine>();

                // Les IStartable, dans l'ordre où le RootLifetimeScope les enregistre — c'est
                // l'ordre dans lequel VContainer les démarre en jeu.
                _upgrades.Start();
                _prestige.Start();
                _threat.Start();
                _session.Start();
                firewall.Start();
                _victory.Start();
                _runner.Start();

                _bot = new ReferencePlayerBot(settings, balancing, prestigeCatalog, _currencies, _upgrades, _prestige,
                    _threat, _readout, exfiltration, _emergency, overclock, _runner, _session, _result.Warnings, _result.Trace);

                Observe(overclock);
            }

            private void Observe(OverclockSystem overclock)
            {
                _upgrades.NetTFlops
                    .Subscribe(this, (value, self) => self.OnNetTFlops(value))
                    .AddTo(ref _disposables);

                _upgrades.GrossTFlops
                    .Subscribe(this, (value, self) => { if (value > self._peakGross) self._peakGross = value; })
                    .AddTo(ref _disposables);

                overclock.OnApplied
                    .Subscribe(this, (_, self) => self._overclocks++)
                    .AddTo(ref _disposables);

                _upgrades.OnProxyWipe
                    .Subscribe(this, (seconds, self) =>
                    {
                        self._proxies++;
                        self._proxySeconds += seconds;
                    })
                    .AddTo(ref _disposables);

                _session.OnSessionEnded
                    .Subscribe(this, (summary, self) => self.RecordRunEnd(summary))
                    .AddTo(ref _disposables);

                _victory.OnVictory
                    .Subscribe(this, (_, self) => self._victoryReached = true)
                    .AddTo(ref _disposables);
            }

            private void OnNetTFlops(double value)
            {
                if (value > _peakNet) _peakNet = value;
                if (value > _bestNet) _bestNet = value;

                while (_nextMilestone < TFlopsMilestones.Length && value >= TFlopsMilestones[_nextMilestone])
                {
                    _result.Milestones.Add(new TFlopsMilestone
                    {
                        Threshold = TFlopsMilestones[_nextMilestone],
                        Hours = _clock.Now / 3600d,
                        Run = _runIndex
                    });
                    _nextMilestone++;
                }
            }

            /// <summary>
            /// La boucle : un pas de temps, les ticks dans l'ordre du jeu (Root puis scène), une
            /// décision du bot à intervalle régulier, et l'écran de fin entre deux runs.
            /// </summary>
            public CampaignResult Play()
            {
                // Une partie neuve : arbre vide, puis une run armée — argent de départ, jauge vide.
                _prestige.InitializeFromSave(new Dictionary<string, int>());
                _session.ResetSession();
                BeginRun();

                float step = _settings.StepSeconds;
                float interval = _settings.DecisionIntervalSeconds;
                double maxCampaignSeconds = _settings.MaxCampaignHours * 3600d;
                float maxRunSeconds = _settings.MaxRunMinutes * 60f;
                float sinceDecision = 0f;

                while (true)
                {
                    _clock.Advance(step);

                    _readout.Tick();
                    _session.Tick();
                    _runner.Tick();
                    _ticker.Tick();

                    sinceDecision += step;
                    if (!_runEnded && !_victoryReached && sinceDecision >= interval)
                    {
                        sinceDecision -= interval;
                        _bot.Decide();
                    }

                    if (_victoryReached)
                    {
                        _result.Victory = true;
                        _result.VictoryHours = _clock.Now / 3600d;
                        _result.VictoryRun = _runIndex;
                        RecordRunInProgress("victoire");
                        break;
                    }

                    if (_runEnded)
                    {
                        if (!CloseRun()) break;

                        sinceDecision = 0f;
                        continue;
                    }

                    if (_session.RunElapsedSeconds > maxRunSeconds)
                    {
                        RecordRunInProgress("coupée");
                        _result.StopReason = $"run {_runIndex} au-delà de {_settings.MaxRunMinutes:0} min";
                        break;
                    }

                    if (_clock.Now > maxCampaignSeconds)
                    {
                        RecordRunInProgress("coupée");
                        _result.StopReason = $"plus de {_settings.MaxCampaignHours:0} h de campagne";
                        break;
                    }
                }

                _result.TotalHours = _clock.Now / 3600d;
                _result.PointsGenerated = _currencies.CpuCyclesAwarded.CurrentValue;
                return _result;
            }

            private void BeginRun()
            {
                _runEnded = false;
                _peakNet = _upgrades.NetTFlops.CurrentValue;
                _peakGross = _upgrades.GrossTFlops.CurrentValue;
                _overclocks = 0;
                _proxies = 0;
                _proxySeconds = 0d;
                _bot.BeginRun(_runIndex);
            }

            /// <summary>
            /// L'écran de fin : le bot dépense ses points, le temps du bilan s'écoule, puis une
            /// nouvelle run. Faux quand la campagne doit s'arrêter.
            /// </summary>
            private bool CloseRun()
            {
                RunRecord last = _result.Runs[_result.Runs.Count - 1];
                last.TreePurchases = _bot.SpendPrestige();

                double usefulSpent = UsefulTreeSpent();
                last.UsefulTreePercent = _result.UsefulTreePoints > 0 ? usefulSpent / _result.UsefulTreePoints * 100d : 0d;

                if (double.IsNaN(_result.UsefulTreeCompletedHours) && usefulSpent >= _result.UsefulTreePoints)
                {
                    _result.UsefulTreeCompletedHours = _clock.Now / 3600d;
                }

                // Le bilan et les achats prennent du temps au joueur : ils comptent dans la campagne.
                // Aucun système n'est tické pendant ce temps — la partie est désarmée.
                if (_settings.SecondsBetweenRuns > 0f) _clock.Advance(_settings.SecondsBetweenRuns);

                // L'avancement se lit en ordres de grandeur de TFlops : c'est ce qui sépare de la
                // victoire, quelle que soit la durée de la campagne.
                double victory = Math.Max(10d, _settings.VictoryTFlops);
                _progress?.Invoke(
                    Mathf.Clamp01((float)(Math.Log10(1d + _bestNet) / Math.Log10(victory))),
                    $"Run {_runIndex} — {_clock.Now / 3600d:0.0} h, {_currencies.CpuCyclesAwarded.CurrentValue:0} points, " +
                    $"{_bestNet:0.##e+0} TFlops");

                if (_runIndex >= _settings.MaxRuns)
                {
                    _result.StopReason = $"{_settings.MaxRuns} runs atteintes";
                    return false;
                }

                _runIndex++;
                _session.ResetSession();
                BeginRun();
                return true;
            }

            /// <summary>
            /// Fin de run, sortie ou saisie : la même photographie que le journal du jeu. Lue au
            /// signal, comme lui — la run est déjà effacée, d'où les pics comptés pendant la run.
            /// </summary>
            private void RecordRunEnd(RunSummary summary)
            {
                _runEnded = true;

                _result.Runs.Add(new RunRecord
                {
                    Index = _runIndex,
                    Outcome = summary.Reason == RunEndReason.CleanExit ? "sortie" : "saisie",
                    DurationSeconds = summary.ElapsedSeconds,
                    ThreatAtEnd = summary.ThreatAtEnd,
                    DataGenerated = summary.DataGenerated,
                    CyclesEarned = summary.CpuCyclesEarned,
                    CyclesBeforeBonus = summary.CpuCyclesBeforeBonus,
                    CyclesAwarded = _currencies.CpuCyclesAwarded.CurrentValue,
                    PointsSpent = _prestige.TotalPointsSpent.CurrentValue,
                    PeakNetTFlops = _peakNet,
                    PeakGrossTFlops = _peakGross,
                    Overclocks = _overclocks,
                    Proxies = _proxies,
                    ProxySeconds = _proxySeconds,
                    Wipers = summary.EmergencyUses,
                    Victory = _victory.HasWon.CurrentValue,
                    CampaignHours = _clock.Now / 3600d,
                });
            }

            /// <summary>
            /// La campagne s'arrête en pleine run — victoire, ou borne de sécurité : la run est
            /// consignée telle qu'elle est à cet instant, sans points ni bilan.
            /// </summary>
            private void RecordRunInProgress(string outcome)
            {
                _result.Runs.Add(new RunRecord
                {
                    Index = _runIndex,
                    Outcome = outcome,
                    DurationSeconds = _session.RunElapsedSeconds,
                    ThreatAtEnd = _threat.NormalizedThreat.CurrentValue,
                    DataGenerated = _currencies.RunMoneyGenerated.CurrentValue,
                    CyclesAwarded = _currencies.CpuCyclesAwarded.CurrentValue,
                    PointsSpent = _prestige.TotalPointsSpent.CurrentValue,
                    PeakNetTFlops = _peakNet,
                    PeakGrossTFlops = _peakGross,
                    Overclocks = _overclocks,
                    Proxies = _proxies,
                    ProxySeconds = _proxySeconds,
                    Wipers = _emergency.UsesInCurrentRun,
                    Victory = _victory.HasWon.CurrentValue,
                    CampaignHours = _clock.Now / 3600d,
                    UsefulTreePercent = _result.UsefulTreePoints > 0
                        ? UsefulTreeSpent() / _result.UsefulTreePoints * 100d
                        : 0d,
                });
            }

            private double UsefulTreeSpent()
            {
                double spent = 0d;
                IReadOnlyList<PrestigeConfigSO> all = _prestigeCatalog.GetAllUpgrades();
                for (int i = 0; i < all.Count; i++)
                {
                    PrestigeConfigSO node = all[i];
                    if (node == null || node.IsUnlimited || node.Id == SinkNodeId) continue;
                    spent += node.GetTotalCostOfLevels(_prestige.GetLevel(node.Id));
                }

                return spent;
            }

            public void Dispose()
            {
                _disposables.Dispose();
                _resolver.Dispose();
            }
        }
    }
}
