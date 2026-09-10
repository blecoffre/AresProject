using Core.Economy.Data;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Platform;
using Core.Services.Security;
using Core.Services.Simulation;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Source de temps virtuelle : le harnais décide du pas, la simulation le subit.
    ///
    /// C'est tout ce qui sépare une campagne de dix heures d'un calcul de quelques secondes.
    /// </summary>
    public sealed class SimulatedTimeSource : ITimeSource
    {
        public float DeltaTime { get; set; }
    }

    /// <summary>
    /// Monte à la main le graphe complet des services de jeu et les fait tourner hors Play Mode.
    ///
    /// <b>Pourquoi c'est possible :</b> tous les systèmes de simulation sont des POCO. Aucun n'est
    /// un MonoBehaviour, aucun ne touche à une scène, aucun ne dépend de l'UI. VContainer les
    /// assemble en production ; ici on fait le même assemblage à la main, ce qui évite d'avoir à
    /// charger une scène ou à démarrer le conteneur.
    ///
    /// <b>Pourquoi ça vaut mieux qu'une réplique :</b> le simulateur Python qui a servi au calage
    /// d'août recopiait les formules dans un autre langage. Une réplique dérive toujours de
    /// l'original qu'elle imite — et elle avait fini par ne plus reproduire la campagne. Ici il
    /// n'y a rien à recopier : c'est <b>le</b> code de jeu qui tourne.
    ///
    /// Ce qui n'est délibérément PAS branché : la sauvegarde, la localisation, le chargement de
    /// scène et l'UI. Le harnais mesure une économie, pas une application.
    /// </summary>
    public sealed class SimulationHarness : IDisposable
    {
        private const string UpgradeCatalogPath = "Assets/GameData/Catalogs/UpgradeCatalog.asset";
        private const string PrestigeCatalogPath = "Assets/GameData/Catalogs/PrestigeCatalog.asset";
        private const string BalancingConfigPath = "Assets/GameData/Balancing/BalancingConfig.asset";

        /// <summary>Tampon réutilisé : repartir d'une run neuve ne doit pas allouer.</summary>
        private static readonly Dictionary<string, int> EmptyLevels = new Dictionary<string, int>();

        private readonly SimulatedTimeSource _time = new SimulatedTimeSource();

        public BalancingConfigSO Balancing { get; }

        /// <summary>Exposé par le harnais : le PrestigeManager garde son catalogue privé.</summary>
        public PrestigeCatalogSO PrestigeCatalog { get; }

        public UserCurrencies Currencies { get; }
        public PrestigeManager Prestige { get; }
        public UpgradeManager Upgrades { get; }
        public ThreatManager Threat { get; }
        public EmergencyProtocolSystem Emergency { get; }
        public GhostCacheSystem GhostCache { get; }
        public GameSessionManager Session { get; }
        public ScriptCycleRunner CycleRunner { get; }
        public SimulationTicker Ticker { get; }
        public ExfiltrationSystem Exfiltration { get; }

        /// <summary>Temps de jeu simulé écoulé depuis la construction, en secondes.</summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>
        /// Débit de Trace observé au dernier pas, en points par seconde.
        ///
        /// Reconstitué depuis la variation de la jauge plutôt que lu quelque part : le
        /// SimulationTicker calcule le débit en interne et ne l'expose pas, et lui ajouter une
        /// propriété pour le confort d'un outil serait payer la production pour l'éditeur. Une
        /// baisse (Data Wiper, wipe de fin de run) rend zéro plutôt qu'un débit négatif.
        /// </summary>
        public float LastTraceDebitPerSecond { get; private set; }

        public SimulationHarness()
        {
            Balancing = Load<BalancingConfigSO>(BalancingConfigPath);
            UpgradeCatalogSO upgradeCatalog = Load<UpgradeCatalogSO>(UpgradeCatalogPath);
            PrestigeCatalog = Load<PrestigeCatalogSO>(PrestigeCatalogPath);
            PrestigeCatalogSO prestigeCatalog = PrestigeCatalog;

            // Ordre de construction impose par les dependances, exactement comme le conteneur le
            // resout. Aucun cycle : Currencies -> Prestige -> Upgrades -> Threat -> Emergency ->
            // GhostCache -> Session -> CycleRunner -> Ticker.
            Currencies = new UserCurrencies(Balancing);
            Prestige = new PrestigeManager(prestigeCatalog, Currencies);
            Upgrades = new UpgradeManager(upgradeCatalog, Currencies, Prestige, Balancing);
            Threat = new ThreatManager(Balancing);
            Emergency = new EmergencyProtocolSystem(Threat, Upgrades, Prestige, Balancing, _time);
            GhostCache = new GhostCacheSystem(Upgrades, Prestige, Balancing);
            Session = new GameSessionManager(Currencies, Threat, Upgrades, Prestige,
                                             Emergency, GhostCache, Balancing, _time);
            CycleRunner = new ScriptCycleRunner(Currencies, Upgrades, Session, _time);
            Ticker = new SimulationTicker(Upgrades, CycleRunner, Prestige, Threat,
                                          Session, GhostCache, Balancing, _time);
            Exfiltration = new ExfiltrationSystem(Currencies, Session, Balancing);

            // Les IStartable, dans l'ordre ou le conteneur les appellerait.
            Upgrades.Start();
            Session.Start();
            CycleRunner.Start();

            // Partie neuve : aucun niveau, ni d'upgrade ni de prestige.
            Prestige.InitializeFromSave(EmptyLevels);
            Upgrades.InitializeFromSave(EmptyLevels);
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"[Balance] Asset introuvable : {path}. Les generateurs d'editeur ont-ils ete lances ?");
            }
            return asset;
        }

        /// <summary>
        /// Avance la simulation d'un pas, dans l'ordre de tick de la production.
        ///
        /// Le ScriptCycleRunner passe en premier : il rafraichit sa trace active en fin de Tick(),
        /// et le SimulationTicker la lit. L'inverse marcherait aussi — la production accepte ce
        /// decalage d'une frame — mais autant reproduire l'ordre reel.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _time.DeltaTime = deltaTime;

            float traceBefore = Threat.CurrentTrace.CurrentValue;

            CycleRunner.Tick();
            Session.Tick();
            Emergency.Tick();
            Ticker.Tick();

            float delta = Threat.CurrentTrace.CurrentValue - traceBefore;
            LastTraceDebitPerSecond = delta > 0f && deltaTime > 0f ? delta / deltaTime : 0f;

            ElapsedSeconds += deltaTime;
        }

        /// <summary>Ouvre une run neuve. Les bonus de prestige deja achetes sont appliques par le wipe.</summary>
        public void BeginRun()
        {
            Prestige.SetPurchaseWindowOpen(false);
            Session.ResetSession();
            Threat.ResetTrace();
        }

        /// <summary>
        /// Ferme la fenetre de run et ouvre celle des achats de prestige.
        /// Miroir de ce que fait le GameSessionManager entre deux runs.
        /// </summary>
        public void OpenPrestigeWindow()
        {
            Prestige.SetPurchaseWindowOpen(true);
        }

        public void Dispose()
        {
            // Ordre inverse de la construction : les abonnements des systemes aval d'abord.
            Exfiltration?.Dispose();
            CycleRunner?.Dispose();
            Session?.Dispose();
            GhostCache?.Dispose();
            Emergency?.Dispose();
            Threat?.Dispose();
            Upgrades?.Dispose();
            Prestige?.Dispose();
            Currencies?.Dispose();
        }
    }
}
