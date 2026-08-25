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
