using Core.Models.Console;
using Core.Services.Localization;
using Core.Services.Platform;
using System;
using Core.UI.Game;
using Core.UI.Options;
using Core.UI.Pause;
using Core.UI.Prestige;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Core.Boot
{
    public class GameSceneBootstrapper : IStartable, ITickable, IDisposable
    {
        private readonly ClickerView _clickerView;
        private readonly ClickerPresenter _clickerPresenter;
        private readonly ConsolePresenter _consolePresenter;
        private readonly PrestigePanelPresenter _prestigePanel;
        private readonly ILocalizationService _loc;
        private readonly ISimulationClock _clock;
        private readonly PauseMenuPresenter _pauseMenu;
        private readonly OptionsPanelPresenter _options;

        public GameSceneBootstrapper(
            ClickerView clickerView,
            ClickerPresenter clickerPresenter,
            ConsolePresenter consolePresenter,
            PrestigePanelPresenter prestigePanel,
            ILocalizationService loc,
            ISimulationClock clock,
            PauseMenuPresenter pauseMenu,
            OptionsPanelPresenter options)
        {
            _clickerView = clickerView;
            _clickerPresenter = clickerPresenter;
            _consolePresenter = consolePresenter;
            _prestigePanel = prestigePanel;
            _loc = loc;
            _clock = clock;
            _pauseMenu = pauseMenu;
            _options = options;
        }

        /// <summary>
        /// Les raccourcis clavier de la scène : <b>P</b> ouvre et ferme l'arbre de prestige,
        /// <b>Échap</b> referme ou ouvre le menu de pause, <b>Espace</b> déclenche un Overclock.
        /// Pendant la pause, P et Espace sont sans effet : la partie est figée.
        ///
        /// Ils vivent ici parce que c'est le seul point de la scène qui possède déjà une boucle de
        /// frame : un presenter POCO n'en a pas, et lui en créer une pour deux touches serait
        /// disproportionné. Deux lectures par frame, aucune allocation.
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

            ReleaseUiSelection();

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleEscape();
                return;
            }

            if (_pauseMenu.IsOpen) return;

            if (keyboard.pKey.wasPressedThisFrame) _prestigePanel.ToggleFromKeyboard();

            // Une pression = un Overclock, exactement comme un clic. Pas de répétition au maintien :
            // le modèle d'équilibrage traite le clic comme « une MAIN, pas un auto-clicker », avec
            // une fatigue qui allonge les pauses. Une touche que l'on garde enfoncée retirerait
            // cette fatigue et changerait l'économie, pas seulement le confort.
            if (keyboard.spaceKey.wasPressedThisFrame) _clickerPresenter.OnClickActionTriggered();
        }

        /// <summary>
        /// Échap suit la convention des jeux PC : il referme d'abord ce qui est ouvert, du plus
        /// haut au plus bas, et n'ouvre le menu de pause que si rien ne l'était. Une pression ne
        /// fait jamais deux choses.
        /// </summary>
        private void HandleEscape()
        {
            if (_options.IsOpen)
            {
                _options.Close();
                return;
            }

            if (_pauseMenu.IsOpen)
            {
                _pauseMenu.Close();
                return;
            }

            if (_prestigePanel.CloseFromKeyboard()) return;

            _pauseMenu.Open();
        }

        /// <summary>
        /// Désélectionne l'élément d'interface courant.
        ///
        /// <b>Sans ça, Espace ferait DEUX choses.</b> uGUI sélectionne un bouton quand on le
        /// clique, et l'action `UI/Submit` de l'Input System est liée à la liaison générique
        /// <c>*/{Submit}</c> — qui couvre Entrée ET Espace sur un clavier. Après un clic sur
        /// « Acheter », chaque appui sur Espace rachèterait donc l'upgrade en plus de lancer
        /// l'Overclock.
        ///
        /// Vider la sélection à chaque frame est le remède le moins invasif : aucune navigation
        /// clavier ou manette n'existe aujourd'hui dans ce projet — l'EventSystem n'a même pas de
        /// `FirstSelected` — donc il n'y a rien à casser.
        ///
        /// ⚠️ <b>À retirer le jour où la navigation manette arrivera</b> (cible Steam Deck) : il
        /// faudra alors relier `UI/Submit` à Entrée et au bouton Sud de la manette explicitement,
        /// au lieu de la liaison générique, et cette méthode devra disparaître avec.
        /// </summary>
        private static void ReleaseUiSelection()
        {
            EventSystem events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject == null) return;

            events.SetSelectedGameObject(null);
        }

        public void Start()
        {
            // La partie est sous les yeux du joueur : la simulation peut tourner. C'est le SEUL
            // endroit qui lève cette retenue, et Dispose() la repose au déchargement de la scène.
            _clock.Release(SimulationHold.OutOfGame);

            _clickerView.Construct(_clickerPresenter);

            // La production passive n'a plus de démarrage global : chaque Script pilote son propre
            // cycle via le ScriptCycleRunner, qui tourne au niveau du scope racine.
            _consolePresenter.Log(_loc.GetText("LOG_NETWORK_CONNECTED"), ConsoleLogType.Success);
            _consolePresenter.Log(_loc.GetText("LOG_BREACH_STARTED"), ConsoleLogType.Standard);
        }

        /// <summary>
        /// Appelé par VContainer à la destruction du scope de scène : retour au menu ou
        /// rechargement de fin de run. La simulation se fige avant même que la scène suivante
        /// n'existe, si bien qu'aucune frame de transition ne tourne sans affichage.
        /// </summary>
        public void Dispose()
        {
            _clock.Hold(SimulationHold.OutOfGame);
        }
    }
}
