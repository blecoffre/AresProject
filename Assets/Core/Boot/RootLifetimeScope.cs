using Core.Boot;
using Core.Economy.Data;
using Core.Models;
using Core.Models.Economy;
using Core.Services.Audio;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Persistence;
using Core.Services.Platform;
using Core.Services.Save;
using Core.Services.Scene;
using Core.Services.Security;
using Core.Services.Settings;
using Core.Services.Simulation;
using Core.UI.Theme;
using UnityEngine;
using UnityEngine.Audio;
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

        [Tooltip("Les palettes de couleurs d'état, une par mode de vision (daltonisme).")]
        [SerializeField] private UiPaletteCatalogSO _paletteCatalog;

        [Tooltip("Le mixer du jeu. Ses paramètres exposés MusicVolume et SfxVolume suivent les " +
                 "volumes des options.")]
        [SerializeField] private AudioMixer _audioMixer;

        [Tooltip("Musiques, son de clic et groupes de sortie du mixer.")]
        [SerializeField] private AudioLibrarySO _audioLibrary;

        [Tooltip("Le canal par lequel les boutons (UiClickSound) demandent leur son.")]
        [SerializeField] private UiSoundChannelSO _uiSoundChannel;

        protected override void Configure(IContainerBuilder builder)
        {
            // 1. Données statiques (catalogues glissés dans l'inspecteur)
            builder.RegisterInstance(_upgradeCatalog);
            builder.RegisterInstance(_prestigeCatalog);
            builder.RegisterInstance(_balancingConfig);
            builder.RegisterInstance(_paletteCatalog);

            // 2. Localisation. Le changement de langue passe par l'interface (LoadLanguageAsync,
            //    CurrentLanguage) : le chargement à chaud se fait depuis les options et l'écran de
            //    premier lancement, qui ne doivent pas dépendre de l'implémentation JSON.
            builder.Register<JsonLocalizationService>(Lifetime.Singleton).As<ILocalizationService>();

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
            // En production elle rend Time.deltaTime, ou zéro quand la partie est retenue (menu
            // principal, menu de pause). C'est aussi le point d'entrée d'un futur harnais de
            // mesure : dérouler une campagne hors Play Mode, sur CE code plutôt que sur une réplique.
            // ISimulationClock à part : lire le temps et le suspendre sont deux droits distincts.
            builder.Register<SimulationClock>(Lifetime.Singleton).As<ITimeSource>().As<ISimulationClock>();

            // Réglages du joueur (settings.json, hors sauvegarde). Lus dès la construction : le
            // FrameRateGovernor et les CanvasScaler doivent trouver les valeurs du joueur à leur
            // premier abonnement. AsImplementedInterfaces pour l'IStartable qui applique l'affichage.
            builder.Register<SettingsStore>(Lifetime.Singleton);
            builder.Register<SettingsService>(Lifetime.Singleton).AsImplementedInterfaces();

            // La palette du mode de vision choisi, suivie à chaud par tous les presenters d'état.
            builder.Register<PaletteService>(Lifetime.Singleton).As<IPaletteService>();

            // Volumes des options appliqués au mixer. Conditionnel, comme les vues facultatives de
            // la GameScene : RegisterInstance(null) ferait échouer tout le conteneur pour un son
            // qui n'existe pas encore.
            if (_audioMixer != null)
            {
                builder.RegisterInstance(_audioMixer);
                builder.RegisterEntryPoint<AudioVolumeApplier>();
            }
            else
            {
                Debug.LogWarning("[AUDIO] Aucun AudioMixer sur le RootLifetimeScope : les volumes des options sont sans effet.");
            }

            // Lecteurs audio. Même parti pris conditionnel : sans bibliothèque, le jeu reste muet
            // au lieu de refuser de démarrer. Les scopes de scène dépendent d'IMusicPlayer, d'où
            // un lecteur silencieux de repli plutôt qu'une absence d'enregistrement.
            if (_audioLibrary != null && _uiSoundChannel != null)
            {
                builder.RegisterInstance(_audioLibrary);
                builder.RegisterInstance(_uiSoundChannel);
                builder.Register<AudioHost>(Lifetime.Singleton);
                builder.Register<MusicPlayer>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.RegisterEntryPoint<UiSoundPlayer>();
                // Saisie, victoire, paliers de Trace : écoute les systèmes du Root en lecture seule.
                builder.RegisterEntryPoint<GameEventSounds>();
            }
            else
            {
                Debug.LogWarning("[AUDIO] AudioLibrary ou UiSoundChannel manquant sur le RootLifetimeScope : le jeu est muet.");
                builder.Register<SilentMusicPlayer>(Lifetime.Singleton).As<IMusicPlayer>();
            }

            // Quitter l'application : derrière une interface pour garder Application.Quit (et son
            // équivalent d'éditeur) hors des presenters.
            builder.Register<ApplicationQuitter>(Lifetime.Singleton).As<IApplicationQuitter>();

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

            // IStartable depuis la refonte « Time Attack » : il s'abonne aux TFlops brutes et au
            // Blindage pour tenir la vitesse de l'A.M.I. en cache, et aux achats de Proxies.
            builder.Register<ThreatManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // Le capteur imparfait posé sur la jauge. AsImplementedInterfaces pour son ITickable :
            // il doit s'écouler même quand rien d'autre ne bouge, sinon l'âge du relevé se fige.
            builder.Register<TraceReadout>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            // Le Data Wiper : trois usages par run, chacun sacrifiant 30 % des TFlops nettes jusqu'au
            // wipe. Plus de délai ni de contrecoup qui s'écoule : ce n'est plus un ITickable.
            builder.Register<EmergencyProtocolSystem>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<GameSessionManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // La sortie volontaire termine une run : elle appartient à la partie, pas à
            // l'affichage, donc au scope racine comme le reste.
            builder.Register<ExfiltrationSystem>(Lifetime.Singleton);

            // La victoire est un fait de CAMPAGNE : elle survit au wipe, elle est sauvegardée, et
            // elle doit être armée avant que le gateway ne restaure la partie. AsImplementedInterfaces
            // pour son IStartable, AsSelf pour que le gateway et le presenter puissent l'injecter.
            builder.Register<VictorySystem>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

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
