using Core.Models.Console;
using Core.Services.Localization;
using Core.Services.Simulation;
using Core.Utils;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Pilote le bouton du Protocole Terre Brûlée et la séquence de purge.
    ///
    /// L'ordre des opérations est la partie délicate : on FIGE le résultat de la run avant de
    /// jouer la séquence, puis on annonce la fin. Voir ExfiltrationSystem pour le pourquoi.
    /// </summary>
    public class ExfiltrationPresenter : IStartable, IDisposable
    {
        /// <summary>Les six lignes du GDD, dans l'ordre. Clés dérivées, comme partout ailleurs.</summary>
        private static readonly string[] PurgeLineKeys =
        {
            "LOG_PURGE_1",
            "LOG_PURGE_2",
            "LOG_PURGE_3",
            "LOG_PURGE_4",
            "LOG_PURGE_5",
            "LOG_PURGE_6"
        };

        private readonly ExfiltrationView _view;
        private readonly ExfiltrationSystem _exfiltration;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private DisposableBag _disposables;
        private CancellationTokenSource _cts;

        /// <summary>Verrou : la séquence dure ~2 s, le bouton ne doit pas être relancé entre-temps.</summary>
        private bool _isPurging;

        public ExfiltrationPresenter(
            ExfiltrationView view,
            ExfiltrationSystem exfiltration,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _view = view;
            _exfiltration = exfiltration;
            _console = console;
            _loc = loc;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            _view.OnExfiltrateClicked += HandleClicked;

            // Trois sources, un seul rafraîchissement. Chacune est déjà filtrée en amont :
            // PendingCycles et IsUnlocked par leur DistinctUntilChanged, et la progression est
            // ramenée à un ENTIER de pourcent ici même. Sans ce dernier filtre, le libellé serait
            // reconstruit — donc une chaîne allouée — à chaque versement de cycle, plusieurs fois
            // par seconde, et la jauge salirait le canvas à chaque frame.
            _exfiltration.IsUnlocked
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            _exfiltration.PendingCycles
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            _exfiltration.ProgressToFirstCycle
                .Select(p => Mathf.RoundToInt(p * 100f))
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            Refresh();
        }

        private void Refresh()
        {
            // Pendant la purge, le bouton reste figé sur son dernier libellé : le rafraîchir
            // afficherait « 0 % » au moment même où la console raconte l'effacement.
            if (_isPurging) return;

            bool unlocked = _exfiltration.IsUnlocked.CurrentValue;

            if (!unlocked)
            {
                float progress = _exfiltration.ProgressToFirstCycle.CurrentValue;
                int percent = Mathf.RoundToInt(progress * 100f);

                _view.ApplyState(
                    _loc.GetText("UI_EXFIL_LOCKED", percent),
                    interactable: false,
                    showProgress: true,
                    progress: progress);

                return;
            }

            double cycles = _exfiltration.PendingCycles.CurrentValue;

            _view.ApplyState(
                _loc.GetText(
                    "UI_EXFIL_READY",
                    CurrencyFormatter.Format(cycles),
                    CurrencyFormatter.Format(_exfiltration.GetNextCycleThreshold())),
                interactable: true,
                showProgress: false,
                progress: 1f);
        }

        private void HandleClicked()
        {
            if (_isPurging) return;

            // Forget() explicite : sans lui, une exception dans cette tâche disparaîtrait sans
            // le moindre log, et le joueur resterait bloqué sur une partie désarmée.
            RunPurgeSequenceAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RunPurgeSequenceAsync(CancellationToken ct)
        {
            // 1. On FIGE le résultat avant toute théâtralisation. Si on jouait la séquence
            //    d'abord, la Trace continuerait de monter pendant ~2 s et un joueur exfiltrant
            //    à 98 % pourrait se faire saisir au milieu de sa propre sortie.
            if (!_exfiltration.TryBeginExfiltration(out double awarded)) return;

            _isPurging = true;
            _view.ApplyState(string.Empty, interactable: false, showProgress: false, progress: 1f);
            _view.SetInteractionBlocked(true);

            try
            {
                for (int i = 0; i < PurgeLineKeys.Length; i++)
                {
                    _console.Log(_loc.GetText(PurgeLineKeys[i]), ConsoleLogType.Narrative);
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_view.LineDelaySeconds),
                        cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Sortie du Play Mode ou destruction du scope pendant la séquence : on ne
                // remonte rien, mais le finally rend quand même la main au joueur.
            }
            finally
            {
                _isPurging = false;
                _view.SetInteractionBlocked(false);

                // 2. L'écran de fin n'apparaît qu'ICI, une fois la console déroulée.
                //    Sans cet appel, la partie resterait désarmée sans écran de prestige.
                _exfiltration.CompleteExfiltration(awarded);

                Refresh();
            }
        }

        public void Dispose()
        {
            _view.OnExfiltrateClicked -= HandleClicked;

            _disposables.Dispose();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
