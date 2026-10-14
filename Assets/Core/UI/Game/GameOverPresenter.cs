using Core.Models.Simulation;
using Core.Services.Localization;
using Core.Services.Scene;
using Core.Services.Simulation;
using Core.UI.Prestige;
using Core.Utils;
using Core.UI.Theme;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Text;
using UnityEngine;
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
    public class GameOverPresenter : IStartable, IPostStartable, IDisposable
    {
        private readonly GameOverView _view;
        private readonly GameSessionManager _sessionManager;
        private readonly PrestigePanelPresenter _prestigePanel;
        private readonly ISceneLoader _sceneLoader;
        private readonly ILocalizationService _loc;
        private readonly IPaletteService _palette;

        private readonly CompositeDisposable _disposables;

        // Le dernier bilan, pour le réécrire si la langue change pendant l'écran de fin.
        private RunSummary _lastSummary;
        private bool _hasSummary;

        /// <summary>
        /// Une relance est en cours. Un double clic sur « [ NOUVELLE INTRUSION ] » remettait la
        /// session à zéro deux fois et lançait deux rechargements de scène concurrents.
        /// </summary>
        private bool _isRestarting;

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
            ILocalizationService loc,
            IPaletteService palette)
        {
            _view = view;
            _sessionManager = sessionManager;
            _prestigePanel = prestigePanel;
            _sceneLoader = sceneLoader;
            _loc = loc;
            _palette = palette;

            _disposables = new CompositeDisposable();
        }

        public void Start()
        {
            _sessionManager.OnSessionEnded
                .Subscribe(Show)
                .AddTo(_disposables);

            _view.OnRestartClicked += HandleRestart;

            // Premier rendu du bouton, puis réécriture à chaque changement de langue — bilan
            // compris. ShowGameOver n'allume rien, il ne fait qu'écrire : sans danger même si
            // l'écran est caché.
            _loc.CurrentLanguage
                .Subscribe(_ => RenderLocalizedTexts())
                .AddTo(_disposables);

            // Mode daltonien changé pendant l'écran de fin : le titre se repeint.
            _palette.Current
                .Skip(1)
                .Subscribe(_ => RenderLocalizedTexts())
                .AddTo(_disposables);
        }

        /// <summary>
        /// Une run terminée et pas encore relancée : la partie a été fermée sur l'écran de fin, ou
        /// on y revient depuis le menu. On le rouvre tel quel, arbre compris — sans quoi le joueur
        /// perdait sa fenêtre d'achat du prestige.
        ///
        /// En PostStart et non en Start : l'écran appartient au panneau de prestige, dont
        /// l'inspecteur n'existe qu'une fois son propre Start passé — et ce presenter-ci est
        /// enregistré avant lui.
        /// </summary>
        public void PostStart()
        {
            if (_sessionManager.TryGetPendingRunEnd(out RunSummary pending))
            {
                Show(pending);
            }
        }

        private void Show(RunSummary summary)
        {
            _lastSummary = summary;
            _hasSummary = true;

            Render(summary);

            // L'écran appartient au panneau de prestige, qui l'ouvre avec l'arbre : la fin d'une
            // run est le seul moment où l'on peut dépenser, il serait absurde d'obliger le joueur
            // à un clic de plus pour y accéder.
            _prestigePanel.ShowRunEnd();
        }

        private void RenderLocalizedTexts()
        {
            _view.SetRestartLabel(_loc.GetText("UI_RUN_END_RESTART"));
            if (_hasSummary) Render(_lastSummary);
        }

        private void Render(RunSummary summary)
        {
            bool isCleanExit = summary.Reason == RunEndReason.CleanExit;

            string narrative = _loc.GetText(isCleanExit
                ? "UI_RUN_END_CLEAN_NARRATIVE"
                : "UI_RUN_END_SEIZED_NARRATIVE");

            _view.ShowGameOver(
                narrative,
                BuildBilan(summary, isCleanExit),
                isCleanExit ? _palette.Current.CurrentValue.RunEndCleanExit : _palette.Current.CurrentValue.RunEndSeized);
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
                CurrencyFormatter.Format(summary.DataGenerated, _loc.Culture)));

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
                CurrencyFormatter.Format(summary.CpuCyclesEarned, _loc.Culture)));

            // L'écart n'est affiché que s'il existe vraiment : un Effacement Propre qui ne
            // rapporte rien de plus — parce que le gain de base est nul — n'a pas de bonus à
            // annoncer, et le proclamer quand même sonnerait faux.
            if (summary.HasCleanExitBonus)
            {
                _bilan.Append(_loc.GetText(
                    "UI_RUN_END_STAT_CLEAN_BONUS",
                    CurrencyFormatter.Format(summary.CpuCyclesEarned - summary.CpuCyclesBeforeBonus, _loc.Culture)));
            }

            // Le symétrique, sur une saisie. Même règle que ci-dessus : rien à annoncer quand
            // l'écart est nul, sinon l'écran reprocherait au joueur une perte qui n'a pas eu lieu.
            else if (summary.HasSeizedLoss)
            {
                _bilan.Append(_loc.GetText(
                    "UI_RUN_END_STAT_SEIZED_LOSS",
                    CurrencyFormatter.Format(summary.CpuCyclesBeforeBonus - summary.CpuCyclesEarned, _loc.Culture)));
            }

            return _bilan.ToString();
        }

        private void HandleRestart()
        {
            if (_isRestarting) return;
            _isRestarting = true;

            _sessionManager.ResetSession();
            RestartAsync().Forget();
        }

        /// <summary>
        /// Recharge la GameScene. La tâche était jetée (<c>_ = …</c>) jusqu'au 2026-10-14 : une
        /// exception y disparaissait sans un mot, et le joueur restait bloqué sur l'écran de fin.
        ///
        /// Le jeton est celui de la fermeture du jeu, jamais celui de ce presenter : il vit dans la
        /// scène que ce chargement décharge, son jeton serait annulé en pleine transition et le
        /// joueur se retrouverait sans aucune scène (voir <see cref="ISceneLoader"/>).
        /// </summary>
        private async UniTaskVoid RestartAsync()
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
                Debug.LogError($"[GAME OVER] Échec du rechargement de la GameScene : {ex}");
            }
        }

        public void Dispose()
        {
            _view.OnRestartClicked -= HandleRestart;

            _disposables.Dispose();
        }
    }
}
