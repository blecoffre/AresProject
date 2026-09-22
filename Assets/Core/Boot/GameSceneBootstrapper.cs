using Core.Models.Console;
using Core.Services.Localization;
using Core.UI.Game;
using Core.UI.Prestige;
using UnityEngine.EventSystems;
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
        /// Les deux raccourcis clavier de la scène : <b>Échap</b> ouvre et ferme l'arbre de
        /// prestige, <b>Espace</b> déclenche un Overclock.
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

            if (keyboard.escapeKey.wasPressedThisFrame) _prestigePanel.ToggleFromKeyboard();

            // Une pression = un Overclock, exactement comme un clic. Pas de répétition au maintien :
            // le modèle d'équilibrage traite le clic comme « une MAIN, pas un auto-clicker », avec
            // une fatigue qui allonge les pauses. Une touche que l'on garde enfoncée retirerait
            // cette fatigue et changerait l'économie, pas seulement le confort.
            if (keyboard.spaceKey.wasPressedThisFrame) _clickerPresenter.OnClickActionTriggered();
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
            _clickerView.Construct(_clickerPresenter);

            // La production passive n'a plus de démarrage global : chaque Script pilote son propre
            // cycle via le ScriptCycleRunner, qui tourne au niveau du scope racine.
            _consolePresenter.Log(_loc.GetText("LOG_NETWORK_CONNECTED"), ConsoleLogType.Success);
            _consolePresenter.Log(_loc.GetText("LOG_BREACH_STARTED"), ConsoleLogType.Standard);
        }
    }
}
