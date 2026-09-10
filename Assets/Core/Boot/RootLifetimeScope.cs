using Core.Boot;
using Core.Economy.Data;
using Core.Models;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Persistence;
using Core.Services.Platform;
using Core.Services.Save;
using Core.Services.Scene;
using Core.Services.Security;
using Core.Services.Simulation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.Infrastructure
{
    public class RootLifetimeScope : LifetimeScope
    {
        [Header("Configuration Globale")]
        [SerializeField] private UpgradeCatalogSO _upgradeCatalog;
        [SerializeField] private PrestigeCatalogSO _prestigeCatalog;

        [Tooltip("Source unique des réglages d'équilibrage. Modifiable en Play Mode : les " +
                 "systèmes lisent les valeurs à l'usage, pas au démarrage.")]
        [SerializeField] private BalancingConfigSO _balancingConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            // 1. Données statiques (catalogues glissés dans l'inspecteur)
            builder.RegisterInstance(_upgradeCatalog);
            builder.RegisterInstance(_prestigeCatalog);
            builder.RegisterInstance(_balancingConfig);

            // 2. Localisation — AsSelf pour que le bootstrapper puisse appeler LoadLanguageAsync
            builder.Register<JsonLocalizationService>(Lifetime.Singleton).As<ILocalizationService>().AsSelf();

            // 3. Persistance
            //    Le disque local fait autorité tant qu'il n'y a pas d'AppId Steam. C'est aussi ce
            //    qui protège la progression d'un joueur hors ligne.
            builder.Register<LocalJsonSaveService>(Lifetime.Singleton)
                   .As<ISaveService>()
                   .As<ISyncSaveService>()
                   .AsSelf();

            builder.Register<GameStateGateway>(Lifetime.Singleton);

            // TODO (étape 1b) : réactiver la voie Steam Cloud derrière un ISteamRuntime injecté.
            //   builder.Register<SteamCloudSaveService>(Lifetime.Singleton);
            //   builder.Register<SaveServiceComposite>(Lifetime.Singleton).As<ISaveService>();

            // La source de temps de TOUTE la simulation. En Singleton dans le Root, donc résolue
            // aussi par le SimulationTicker qui vit dans le scope de scène — un scope enfant lit
            // les enregistrements de son parent.
            //
            // En production elle rend exactement Time.deltaTime : ce n'est pas un point
            // d'extension théorique, c'est ce qui permet au harnais d'équilibrage de dérouler une
            // campagne de dix heures en quelques secondes, hors Play Mode, sur CE code plutôt que
            // sur une réplique qui en dérive.
            builder.Register<UnityTimeSource>(Lifetime.Singleton).As<ITimeSource>();

            // 4. Modèles de données
            builder.Register<UserCurrencies>(Lifetime.Singleton);

            // Le mode d'achat multiple est une préférence d'interface, mais il vit ici et non
            // dans le scope de scène : le GameStateGateway le restaure AVANT que la GameScene
            // n'existe, et il doit survivre à son rechargement comme le reste de la partie.
            builder.Register<BuyQuantitySelector>(Lifetime.Singleton);

            // 5. Navigation
            builder.Register<SceneLoader>(Lifetime.Singleton).As<ISceneLoader>();

            // 6. Systèmes métier
            //    Tous en Singleton : un service enregistré en Scoped ICI serait ré-instancié dans
            //    chaque scope enfant, et la scène de jeu manipulerait alors une seconde instance.
            // AsImplementedInterfaces : l'UpgradeManager est devenu IStartable, il doit
            // s'abonner à StartingComputerPower pour tenir les TFlops à jour.
            builder.Register<UpgradeManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<PrestigeManager>(Lifetime.Singleton);
            builder.Register<ThreatManager>(Lifetime.Singleton);
            // ITickable depuis la refonte « Data Wiper » : il fait s'écouler l'immobilisation
            // des TFlops et le délai entre deux activations.
            builder.Register<EmergencyProtocolSystem>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<GameSessionManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // La sortie volontaire termine une run : elle appartient à la partie, pas à
            // l'affichage, donc au scope racine comme le reste.
            builder.Register<ExfiltrationSystem>(Lifetime.Singleton);

            // Le Ghost Cache accumule de l'état de run et survit au rechargement de la
            // GameScene : scope racine, comme le reste de la simulation.
            builder.Register<GhostCacheSystem>(Lifetime.Singleton);

            // 7. Points d'entrée
            // Enregistré en premier : il plafonne la cadence avant que quoi que ce soit ne
            // s'affiche, plutôt que de laisser le jeu tourner libre le temps du chargement.
            builder.RegisterEntryPoint<FrameRateGovernor>();

            builder.RegisterEntryPoint<GameBootstrapper>();
            builder.RegisterEntryPoint<SaveScheduler>();

            // Le moteur de cycles est un ITickable : il appartient à la partie et doit survivre
            // au rechargement de la GameScene, d'où sa place ici et non dans le scope de scène.
            builder.RegisterEntryPoint<ScriptCycleRunner>().AsSelf();
        }
    }
}
