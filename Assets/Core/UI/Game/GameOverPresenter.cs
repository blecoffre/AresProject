using Core.Services.Localization;
using Core.Services.Scene;
using Core.Services.Simulation;
using Core.Utils;
using R3;
using System;
using System.Threading;
using VContainer.Unity;

namespace Core.UI.Game
{
    public class GameOverPresenter : IStartable, IDisposable
    {
        private readonly GameOverView _view;
        private readonly GameSessionManager _sessionManager;
        private readonly ISceneLoader _sceneLoader;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;
        private CancellationTokenSource _cts;

        public GameOverPresenter(
            GameOverView view,
            GameSessionManager sessionManager,
            ISceneLoader sceneLoader,
            ILocalizationService loc)
        {
            _view = view;
            _sessionManager = sessionManager;
            _sceneLoader = sceneLoader;
            _loc = loc;

            _disposables = new CompositeDisposable();
            _cts = new CancellationTokenSource();
        }

        public void Start()
        {
            _sessionManager.OnSessionEnded
                .Subscribe(prestigeEarned =>
                {
                    // 1. Le texte d'ambiance (Ex: "L'A.M.I. a saisi votre matériel.")
                    string narrativeMsg = _loc.GetText("UI_GAME_OVER_NARRATIVE");

                    // 2. Le texte de bilan formaté (Ex: "Cycles CPU Exfiltrés : {0}")
                    string formattedPrestige = CurrencyFormatter.Format(prestigeEarned);
                    string bilanMsg = _loc.GetText("UI_GAME_OVER_BILAN", formattedPrestige);

                    _view.ShowGameOver(narrativeMsg, bilanMsg);
                })
                .AddTo(_disposables);

            _view.OnRestartClicked += HandleRestart;
        }

        private void HandleRestart()
        {
            _sessionManager.ResetSession();
            _ = _sceneLoader.LoadGameSceneAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            _view.OnRestartClicked -= HandleRestart;
            _disposables.Dispose();
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}