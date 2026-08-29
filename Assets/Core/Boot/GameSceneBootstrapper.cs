using Core.Models.Console;
using Core.Services.Localization;
using Core.UI.Game;
using Core.UI.Prestige;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Core.Boot
{
    public class GameSceneBootstrapper : IStartable, ITickable
    {
        private readonly ClickerView _clickerView;
        private readonly ClickerPresenter _clickerPresenter;
        private readonly ConsolePresenter _consolePresenter;
        private readonly PrestigePanelPresenter _prestigePanel;
        private readonly ILocalizationService _loc;

        public GameSceneBootstrapper(
            ClickerView clickerView,
            ClickerPresenter clickerPresenter,
            ConsolePresenter consolePresenter,
            PrestigePanelPresenter prestigePanel,
            ILocalizationService loc)
        {
            _clickerView = clickerView;
            _clickerPresenter = clickerPresenter;
            _consolePresenter = consolePresenter;
            _prestigePanel = prestigePanel;
            _loc = loc;
        }

        /// <summary>
        /// Échap ouvre et ferme l'arbre de prestige.
        ///
        /// Le raccourci vit ici parce que c'est le seul point de la scène qui possède déjà une
        /// boucle de frame : un presenter POCO n'en a pas, et lui en créer une pour une touche
        /// serait disproportionné. Une lecture par frame, aucune allocation.
        ///
        /// Passe par le NOUVEL Input System : le projet a basculé dessus dans les Player
        /// Settings, et l'ancienne classe `Input` y lève une InvalidOperationException à chaque
        /// appel. Le test de nullité sur `Keyboard.current` n'est pas décoratif — il vaut null
        /// quand aucun clavier n'est connecté.
        /// </summary>
        public void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            _prestigePanel.ToggleFromKeyboard();
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
