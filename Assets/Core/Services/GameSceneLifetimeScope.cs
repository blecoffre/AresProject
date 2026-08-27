using Core.Services.Economy;
using Core.Services.Simulation;
using Core.UI.Game;
using Core.UI.Header;
using Core.UI.Prestige;
using Core.UI.Upgrades;
using UnityEngine;
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

        [Tooltip("Bouton du Ghost Cache / Zéro-Day Exploit. Vue autonome, comme celle du " +
                 "Protocole Terre Brûlée : elle peut être posée n'importe où dans l'écran.")]
        [SerializeField] private GhostCacheView _ghostCacheView;

        protected override void Configure(IContainerBuilder builder)
        {
            // Services locaux à la scène.
            // Le ScriptCycleRunner, lui, vit dans le scope racine : les cycles appartiennent à la
            // partie, pas à l'affichage.
            builder.Register<OverclockSystem>(Lifetime.Scoped);

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

            // Même traitement conditionnel que ci-dessus, et pour la même raison : la vue est
            // neuve et pas encore posée dans la scène.
            if (_ghostCacheView != null)
            {
                builder.RegisterComponent(_ghostCacheView);
                builder.Register<GhostCachePresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            }
            else
            {
                Debug.LogWarning(
                    "[SCENE] Aucune GhostCacheView assignée sur le GameSceneLifetimeScope : le " +
                    "Zéro-Day Exploit est injouable. Le Ghost Cache continue d'accumuler sa " +
                    "charge en arrière-plan, mais rien ne permet de la dépenser.");
            }

            // Presenters (MVP)
            builder.Register<ConsolePresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<ClickerPresenter>(Lifetime.Scoped).AsSelf();
            builder.Register<HeaderPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<UpgradePanelPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<GameOverPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<PrestigePanelPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // Points de démarrage
            builder.RegisterEntryPoint<GameSceneBootstrapper>();
            builder.RegisterEntryPoint<SimulationTicker>();
        }
    }
}
