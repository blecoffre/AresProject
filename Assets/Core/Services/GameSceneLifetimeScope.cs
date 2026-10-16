using Core.Services.Audio;
using Core.Services.Economy;
using Core.Services.Simulation;
using Core.UI.Game;
using Core.UI.Header;
using Core.UI.Options;
using Core.UI.Pause;
using Core.UI.Prestige;
using Core.UI.Theme;
using Core.UI.Upgrades;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Core.Boot
{
    public class GameSceneLifetimeScope : LifetimeScope
    {
        [Header("Vues (glisser-déposer depuis la hiérarchie)")]
        [SerializeField] private ConsoleView _consoleView;
        [SerializeField] private ClickerView _clickerView;
        [SerializeField] private HeaderView _headerView;
        [SerializeField] private UpgradePanelView _upgradePanelView;
        [SerializeField] private GameOverView _gameOverView;
        [SerializeField] private PrestigePanelView _prestigeView;

        [Tooltip("Bouton du Protocole Terre Brûlée. Vue autonome : elle peut être posée " +
                 "n'importe où dans l'écran sans toucher au code.")]
        [SerializeField] private ExfiltrationView _exfiltrationView;

        [Tooltip("Bouton d'Urgence / Data Wiper. Vue autonome, comme la précédente.")]
        [SerializeField] private EmergencyView _emergencyView;

        [Tooltip("Écran de victoire. Vue autonome qui possède sa propre racine : elle ne partage " +
                 "rien avec l'écran de fin de run, la victoire n'arrêtant pas la partie.")]
        [SerializeField] private VictoryView _victoryView;

        [Header("Pause et options")]
        [Tooltip("Menu de pause (Échap). Instance du prefab PauseMenu.")]
        [SerializeField] private PauseMenuView _pauseMenuView;

        [Tooltip("Panneau d'options ouvert depuis la pause. Instance du prefab OptionsPanel, le même que sur l'écran titre.")]
        [SerializeField] private OptionsPanelView _optionsPanelView;

        [Tooltip("Le CanvasScaler de la scène, sur lequel s'applique l'échelle d'interface.")]
        [SerializeField] private CanvasScaler _canvasScaler;

        /// <summary>
        /// Sans RootScene — Play lancé depuis cette scène —, rien ne se construit : voir
        /// <see cref="RootSceneGuard"/>.
        /// </summary>
        protected override void Awake()
        {
            if (!RootSceneGuard.IsRootSceneLoaded(this)) return;

            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // Aucun service local : l'OverclockSystem est monté dans le scope racine le 2026-10-16,
            // à côté du ScriptCycleRunner — les cycles et leur accélération appartiennent à la
            // partie, pas à l'affichage.

            // Vues
            builder.RegisterComponent(_consoleView);
            builder.RegisterComponent(_clickerView);
            builder.RegisterComponent(_headerView);
            builder.RegisterComponent(_upgradePanelView);
            builder.RegisterComponent(_gameOverView);
            builder.RegisterComponent(_prestigeView);

            // Seule vue enregistrée sous condition : elle est neuve et pas encore posée dans la
            // scène. RegisterComponent(null) ferait échouer la construction du conteneur, donc
            // planter le démarrage — pour un bouton qui n'existe pas encore. On préfère un
            // message qui dit quoi faire.
            if (_exfiltrationView != null)
            {
                builder.RegisterComponent(_exfiltrationView);
                builder.Register<ExfiltrationPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            }
            else
            {
                Debug.LogWarning(
                    "[SCENE] Aucune ExfiltrationView assignée sur le GameSceneLifetimeScope : " +
                    "le Protocole Terre Brûlée est indisponible. Pose le bouton dans la scène et " +
                    "glisse-le dans le champ pour l'activer.");
            }

            // Même traitement conditionnel que la vue ci-dessus.
            if (_emergencyView != null)
            {
                builder.RegisterComponent(_emergencyView);
                builder.Register<EmergencyPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            }
            else
            {
                Debug.LogWarning(
                    "[SCENE] Aucune EmergencyView assignée sur le GameSceneLifetimeScope : le " +
                    "Data Wiper est injouable. Le système tourne, mais rien ne permet de le " +
                    "déclencher.");
            }

            // Même traitement conditionnel. La conséquence est ici plus douce que pour les deux
            // vues ci-dessus : sans écran, la victoire se raconte quand même dans la console, le
            // VictoryPresenter étant enregistré séparément et sans vue.
            if (_victoryView != null)
            {
                builder.RegisterComponent(_victoryView);
                builder.Register<VictoryScreenPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            }
            else
            {
                Debug.LogWarning(
                    "[SCENE] Aucune VictoryView assignée sur le GameSceneLifetimeScope : la " +
                    "victoire ne s'annoncera que dans la console, sans écran.");
            }

            // Presenters (MVP)
            builder.Register<ConsolePresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<ClickerPresenter>(Lifetime.Scoped).AsSelf();
            builder.Register<HeaderPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<UpgradePanelPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<GameOverPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // Sans vue : raconte dans la console les achats d'office de la Routine Pare-Feu.
            builder.Register<FirewallRoutinePresenter>(Lifetime.Scoped).AsImplementedInterfaces();

            // N'a AUCUNE vue : il ne fait que raconter la victoire dans la console, donc rien à
            // câbler dans la scène. L'écran dédié viendra s'ajouter ici quand il existera.
            builder.Register<VictoryPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<PrestigePanelPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // Points de démarrage
            builder.RegisterEntryPoint<GameSceneBootstrapper>();
            builder.RegisterEntryPoint<SimulationTicker>();

            // L'ambiance musicale de la partie. Un rechargement de fin de run la redemande : le
            // lecteur ignore une piste qui joue déjà, la musique continue sans reprendre au début.
            builder.RegisterEntryPoint<SceneMusic>().WithParameter(MusicTrack.Game);

            // Menu de pause et options : le même panneau d'options que sur l'écran titre.
            builder.RegisterComponent(_pauseMenuView);
            builder.RegisterComponent(_optionsPanelView);
            builder.Register<OptionsPanelPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<PauseMenuPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // L'échelle d'interface choisie dans les options.
            builder.RegisterComponent(_canvasScaler);
            builder.RegisterEntryPoint<UiScalePresenter>();
        }
    }
}
