using Core.Services.Localization;
using Core.Services.Persistence;
using Core.Services.Platform;
using Core.Services.Scene;
using Core.UI.Options;
using Cysharp.Threading.Tasks;
using R3;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Core.UI.MainMenu
{
    /// <summary>
    /// L'écran titre : Continuer, Options, Quitter.
    ///
    /// Pas de « Nouvelle partie » : il n'existe qu'une sauvegarde, et un tel bouton à côté de
    /// « Continuer » serait un effacement de progression à un clic de distance.
    ///
    /// La simulation est figée pendant tout le temps passé ici, sans que ce presenter ait à s'en
    /// occuper : la <c>SimulationClock</c> démarre retenue, et seule la GameScene la libère.
    /// </summary>
    public class MainMenuPresenter : IStartable, ITickable, IDisposable
    {
        private readonly MainMenuView _view;
        private readonly ISceneLoader _sceneLoader;
        private readonly IApplicationQuitter _quitter;
        private readonly ILocalizationService _loc;
        private readonly OptionsPanelPresenter _options;
        private readonly GameStateGateway _gateway;

        private bool _isLeaving;
        private IDisposable _languageSubscription;

        public MainMenuPresenter(
            MainMenuView view,
            ISceneLoader sceneLoader,
            IApplicationQuitter quitter,
            ILocalizationService loc,
            OptionsPanelPresenter options,
            GameStateGateway gateway)
        {
            _view = view;
            _sceneLoader = sceneLoader;
            _quitter = quitter;
            _loc = loc;
            _options = options;
            _gateway = gateway;
        }

        public void Start()
        {
            // Émet la langue courante dès l'abonnement (premier rendu), puis à chaque changement
            // — depuis les options ou depuis l'écran de premier lancement.
            _languageSubscription = _loc.CurrentLanguage.Subscribe(_ => RenderTexts());

            _view.OnContinueClicked += HandleContinue;
            _view.OnOptionsClicked += HandleOptions;
            _view.OnQuitClicked += HandleQuit;
        }

        private void RenderTexts()
        {
            _view.SetTexts(
                _loc.GetText("MENU_TITLE"),
                _loc.GetText("MENU_SUBTITLE"),
                // « Commencer » tant qu'aucune partie n'existe sur disque : dire « Continuer » à qui
                // lance le jeu pour la première fois serait étrange. Relu à chaque rendu, donc
                // juste aussi après une première session suivie d'un retour au menu.
                _loc.GetText(_gateway.HasSavedGame ? "MENU_CONTINUE" : "MENU_START"),
                _loc.GetText("MENU_OPTIONS"),
                _loc.GetText("MENU_QUIT"));
        }

        private void HandleContinue()
        {
            if (_isLeaving) return;
            _isLeaving = true;
            _view.SetInteractable(false);

            LoadGameAsync().Forget();
        }

        /// <summary>
        /// Le jeton de fermeture du jeu, à dessein, jamais celui de ce presenter : il vit dans la
        /// scène que la transition décharge. Un jeton lié à son scope serait annulé en pleine
        /// transition, et le joueur se retrouverait sans aucune scène à l'écran. Celui de
        /// l'application ne coupe la transition que si le joueur quitte le jeu.
        /// </summary>
        private async UniTaskVoid LoadGameAsync()
        {
            try
            {
                await _sceneLoader.LoadGameSceneAsync(Application.exitCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Le jeu se ferme pendant la transition : rien à signaler.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MENU] Échec du chargement de la GameScene : {ex}");
            }
        }

        private void HandleOptions()
        {
            if (_isLeaving) return;
            _options.Open();
        }

        /// <summary>
        /// Échap referme le panneau d'options. Une lecture de touche par frame, sans allocation ;
        /// le presenter est le seul objet de la scène à avoir une boucle.
        /// </summary>
        public void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame && _options.IsOpen) _options.Close();
        }

        private void HandleQuit()
        {
            if (_isLeaving) return;
            _isLeaving = true;
            _view.SetInteractable(false);

            _quitter.Quit();
        }

        public void Dispose()
        {
            _languageSubscription?.Dispose();

            _view.OnContinueClicked -= HandleContinue;
            _view.OnOptionsClicked -= HandleOptions;
            _view.OnQuitClicked -= HandleQuit;
        }
    }
}
