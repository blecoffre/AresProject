using Core.Services.Localization;
using Core.Services.Persistence;
using Core.Services.Platform;
using Core.Services.Scene;
using Core.UI.Options;
using Cysharp.Threading.Tasks;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Pause
{
    /// <summary>
    /// Le menu de pause : Reprendre, Options, Menu principal, Quitter.
    ///
    /// <b>Fige la simulation</b> tant qu'il est ouvert (<see cref="SimulationHold.PauseMenu"/>).
    /// C'est un refuge assumé contre la Trace — décision de game design : on ne punit pas le
    /// joueur qui règle ses options. L'arbre de prestige, lui, ne fige rien.
    ///
    /// Ne lit aucune touche : c'est le <c>GameSceneBootstrapper</c>, seul à avoir une boucle,
    /// qui route Échap entre les panneaux et appelle <see cref="Open"/> / <see cref="Close"/>.
    /// </summary>
    public class PauseMenuPresenter : IStartable, IDisposable
    {
        private readonly PauseMenuView _view;
        private readonly OptionsPanelPresenter _options;
        private readonly ISimulationClock _clock;
        private readonly ISceneLoader _sceneLoader;
        private readonly ISaveRequester _saveRequester;
        private readonly IApplicationQuitter _quitter;
        private readonly ILocalizationService _loc;

        private IDisposable _languageSubscription;
        private bool _isLeaving;

        public PauseMenuPresenter(
            PauseMenuView view,
            OptionsPanelPresenter options,
            ISimulationClock clock,
            ISceneLoader sceneLoader,
            ISaveRequester saveRequester,
            IApplicationQuitter quitter,
            ILocalizationService loc)
        {
            _view = view;
            _options = options;
            _clock = clock;
            _sceneLoader = sceneLoader;
            _saveRequester = saveRequester;
            _quitter = quitter;
            _loc = loc;
        }

        public bool IsOpen => _view.IsVisible;

        public void Start()
        {
            _view.Hide();

            _view.OnResumeClicked += Close;
            _view.OnOptionsClicked += HandleOptions;
            _view.OnMainMenuClicked += HandleMainMenu;
            _view.OnQuitClicked += HandleQuit;

            // Premier rendu, puis réécriture à chaque changement de langue — y compris celui
            // fait depuis le panneau d'options ouvert par-dessus ce menu.
            _languageSubscription = _loc.CurrentLanguage.Subscribe(_ => RenderTexts());
        }

        private void RenderTexts()
        {
            _view.SetTexts(
                _loc.GetText("PAUSE_TITLE"),
                _loc.GetText("PAUSE_RESUME"),
                _loc.GetText("PAUSE_OPTIONS"),
                _loc.GetText("PAUSE_MAIN_MENU"),
                _loc.GetText("PAUSE_QUIT"));
        }

        public void Open()
        {
            if (IsOpen || _isLeaving) return;

            _clock.Hold(SimulationHold.PauseMenu);
            _view.Show();
        }

        /// <summary>Referme le menu — et les options ouvertes par-dessus — puis relance la partie.</summary>
        public void Close()
        {
            if (!IsOpen || _isLeaving) return;

            _options.Close();
            _view.Hide();
            _clock.Release(SimulationHold.PauseMenu);
        }

        private void HandleOptions()
        {
            if (_isLeaving) return;
            _options.Open();
        }

        /// <summary>
        /// Sauvegarde, puis retour à l'écran titre. La simulation reste figée de bout en bout :
        /// la retenue PauseMenu tient jusqu'à la destruction de ce presenter, et la GameScene pose
        /// OutOfGame en se déchargeant. L'état capturé pour la sauvegarde ne bouge donc plus.
        /// </summary>
        private void HandleMainMenu()
        {
            if (_isLeaving) return;
            _isLeaving = true;
            _view.SetInteractable(false);

            _saveRequester.RequestSave("retour au menu principal");
            LoadMainMenuAsync().Forget();
        }

        /// <summary>
        /// Le jeton de fermeture du jeu, jamais celui de ce presenter : il vit dans la scène que
        /// la transition décharge (voir <c>SceneLoader</c>).
        /// </summary>
        private async UniTaskVoid LoadMainMenuAsync()
        {
            try
            {
                await _sceneLoader.LoadMainMenuSceneAsync(Application.exitCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Le jeu se ferme pendant la transition : rien à signaler.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PAUSE] Échec du retour au menu principal : {ex}");
            }
        }

        private void HandleQuit()
        {
            if (_isLeaving) return;
            _isLeaving = true;
            _view.SetInteractable(false);

            // La sauvegarde de fermeture (Application.quitting) couvre ce chemin.
            _quitter.Quit();
        }

        public void Dispose()
        {
            _languageSubscription?.Dispose();

            _view.OnResumeClicked -= Close;
            _view.OnOptionsClicked -= HandleOptions;
            _view.OnMainMenuClicked -= HandleMainMenu;
            _view.OnQuitClicked -= HandleQuit;

            // INDISPENSABLE : l'horloge vit dans le Root et survit à la scène. Sans cette ligne,
            // revenir au menu depuis la pause laisserait la retenue posée, et la partie suivante
            // démarrerait figée — sans aucun menu ouvert pour la libérer.
            _clock.Release(SimulationHold.PauseMenu);
        }
    }
}
