using Core.Models.Simulation;
using Core.Services.Localization;
using Core.Services.Scene;
using Core.Services.Simulation;
using Core.UI.Prestige;
using Core.Utils;
using R3;
using System;
using System.Text;
using System.Threading;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// L'écran de fin de run et son bilan.
    ///
    /// Le bilan se compose ici, ligne par ligne, à partir du <see cref="RunSummary"/>. La vue ne
    /// reçoit qu'un texte déjà assemblé : elle n'a aucune raison de connaître les statistiques
    /// d'une run, ni de décider lesquelles méritent d'apparaître.
    /// </summary>
    public class GameOverPresenter : IStartable, IDisposable
    {
        private readonly GameOverView _view;
        private readonly GameSessionManager _sessionManager;
        private readonly PrestigePanelPresenter _prestigePanel;
        private readonly ISceneLoader _sceneLoader;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;

        /// <summary>
        /// Réutilisé d'une fin de run à l'autre. Une fin de run est rare, mais le tampon coûte
        /// un champ et évite d'allouer une demi-douzaine de chaînes intermédiaires.
        /// </summary>
        private readonly StringBuilder _bilan = new StringBuilder(256);

        public GameOverPresenter(
            GameOverView view,
            GameSessionManager sessionManager,
            PrestigePanelPresenter prestigePanel,
            ISceneLoader sceneLoader,
            ILocalizationService loc)
        {
            _view = view;
            _sessionManager = sessionManager;
            _prestigePanel = prestigePanel;
            _sceneLoader = sceneLoader;
            _loc = loc;

            _disposables = new CompositeDisposable();
        }

        public void Start()
        {
            _sessionManager.OnSessionEnded
                .Subscribe(Show)
                .AddTo(_disposables);

            _view.OnRestartClicked += HandleRestart;
            _view.OnPrestigeClicked += HandlePrestige;
        }

        private void Show(RunSummary summary)
        {
            bool isCleanExit = summary.Reason == RunEndReason.CleanExit;

            string narrative = _loc.GetText(isCleanExit
                ? "UI_RUN_END_CLEAN_NARRATIVE"
                : "UI_RUN_END_SEIZED_NARRATIVE");

            _view.ShowGameOver(
                narrative,
                BuildBilan(summary, isCleanExit),
                isCleanExit ? _view.CleanExitColor : _view.SeizedColor);
        }

        /// <summary>
        /// Assemble le bilan. Une clé de localisation par ligne, jamais de texte en dur ni de
        /// ponctuation d'assemblage : c'est le gabarit de chaque clé qui porte sa mise en forme.
        /// </summary>
        private string BuildBilan(RunSummary summary, bool isCleanExit)
        {
            _bilan.Clear();

            _bilan.AppendLine(_loc.GetText(
                "UI_RUN_END_STAT_DATA",
                CurrencyFormatter.Format(summary.DataGenerated)));

            // La Trace atteinte ne dit quelque chose que sur une sortie VOLONTAIRE : après une
            // saisie elle vaut 100 % par définition, et l'afficher serait du bruit.
            if (isCleanExit)
            {
                _bilan.AppendLine(_loc.GetText(
                    "UI_RUN_END_STAT_TRACE",
                    UnityEngine.Mathf.RoundToInt(summary.ThreatAtEnd * 100f)));
            }

            // Deux gabarits plutôt qu'un : une run de quarante secondes afficherait
            // « 0 min 40 s » avec un format unique, ce qui se lit mal pour la statistique la
            // plus regardée de l'écran. Les unités vivent dans les clés, jamais ici.
            int totalSeconds = UnityEngine.Mathf.FloorToInt(summary.ElapsedSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            _bilan.AppendLine(minutes > 0
                ? _loc.GetText("UI_RUN_END_STAT_DURATION", minutes, seconds)
                : _loc.GetText("UI_RUN_END_STAT_DURATION_SHORT", seconds));

            if (summary.EmergencyUses > 0)
            {
                _bilan.AppendLine(_loc.GetText("UI_RUN_END_STAT_WIPER", summary.EmergencyUses));
            }

            _bilan.AppendLine(_loc.GetText(
                "UI_RUN_END_STAT_CYCLES",
                CurrencyFormatter.Format(summary.CpuCyclesEarned)));

            // L'écart n'est affiché que s'il existe vraiment : un Effacement Propre qui ne
            // rapporte rien de plus — parce que le gain de base est nul — n'a pas de bonus à
            // annoncer, et le proclamer quand même sonnerait faux.
            if (summary.HasCleanExitBonus)
            {
                _bilan.Append(_loc.GetText(
                    "UI_RUN_END_STAT_CLEAN_BONUS",
                    CurrencyFormatter.Format(summary.CpuCyclesEarned - summary.CpuCyclesBeforeBonus)));
            }

            return _bilan.ToString();
        }

        /// <summary>
        /// Ouvre l'arbre depuis l'écran de fin. C'est le moment naturel pour dépenser ce qu'on
        /// vient de gagner — et l'écran de fin recouvre le bouton du header.
        /// </summary>
        private void HandlePrestige()
        {
            _prestigePanel.ToggleFromKeyboard();
        }

        private void HandleRestart()
        {
            _sessionManager.ResetSession();
            _ = _sceneLoader.LoadGameSceneAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            _view.OnRestartClicked -= HandleRestart;
            _view.OnPrestigeClicked -= HandlePrestige;

            _disposables.Dispose();
        }
    }
}
