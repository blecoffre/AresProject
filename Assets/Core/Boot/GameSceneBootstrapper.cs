using Core.Models.Console;
using Core.Services.Localization;
using Core.UI.Game;
using VContainer.Unity;

namespace Core.Boot
{
    public class GameSceneBootstrapper : IStartable
    {
        private readonly ClickerView _clickerView;
        private readonly ClickerPresenter _clickerPresenter;
        private readonly ConsolePresenter _consolePresenter;
        private readonly ILocalizationService _loc;

        public GameSceneBootstrapper(
            ClickerView clickerView,
            ClickerPresenter clickerPresenter,
            ConsolePresenter consolePresenter,
            ILocalizationService loc)
        {
            _clickerView = clickerView;
            _clickerPresenter = clickerPresenter;
            _consolePresenter = consolePresenter;
            _loc = loc;
        }

        public void Start()
        {
            _clickerView.Construct(_clickerPresenter);

            // La production passive n'a plus de démarrage global : chaque Script pilote son propre
            // cycle via le ScriptCycleRunner, qui tourne au niveau du scope racine.
            _consolePresenter.Log(_loc.GetText("LOG_NETWORK_CONNECTED"), ConsoleLogType.Success);
            _consolePresenter.Log(_loc.GetText("LOG_BREACH_STARTED"), ConsoleLogType.Standard);
        }
    }
}
