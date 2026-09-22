using Core.Models.Console;
using Core.Services.Localization;
using Core.Services.Simulation;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Déroule le moment de victoire dans la console de jeu.
    ///
    /// La partie n'est ni figée ni interrompue : le joueur garde la main pendant la séquence, ses
    /// cycles continuent de tourner, et la Trace continue de monter. C'est voulu — gagner ne doit
    /// pas coûter la run en cours, et l'aboutissement se célèbre sans confisquer le jeu.
    ///
    /// <b>Ne décide rien.</b> Le franchissement du seuil appartient au <see cref="VictorySystem"/>,
    /// service du scope racine ; ce presenter ne fait que le raconter.
    /// </summary>
    public sealed class VictoryPresenter : IStartable, IDisposable
    {
        /// <summary>
        /// Les lignes de la séquence, dans l'ordre. Clés dérivées, comme partout ailleurs : aucun
        /// texte affichable ne vit dans le code.
        /// </summary>
        private static readonly string[] VictoryLineKeys =
        {
            "LOG_VICTORY_1",
            "LOG_VICTORY_2",
            "LOG_VICTORY_3",
            "LOG_VICTORY_4",
            "LOG_VICTORY_5",
            "LOG_VICTORY_6"
        };

        /// <summary>
        /// Rythme de la séquence. Constante de mise en scène, pas un réglage d'équilibrage : elle
        /// ne change aucune règle et n'a donc rien à faire dans BalancingConfig.
        /// </summary>
        private const float LineDelaySeconds = 0.7f;

        private readonly VictorySystem _victory;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private DisposableBag _disposables;
        private CancellationTokenSource _cts;

        public VictoryPresenter(
            VictorySystem victory,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _victory = victory;
            _console = console;
            _loc = loc;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            // On s'abonne à l'ÉVÉNEMENT, pas à l'état : HasWon reste vrai pour toujours, donc s'y
            // abonner rejouerait la séquence à chaque rechargement de la GameScene.
            _victory.OnVictory
                    .Subscribe(_ => RunVictorySequenceAsync(_cts.Token).Forget())
                    .AddTo(ref _disposables);
        }

        private async UniTaskVoid RunVictorySequenceAsync(CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < VictoryLineKeys.Length; i++)
                {
                    _console.Log(_loc.GetText(VictoryLineKeys[i]), ConsoleLogType.Narrative);
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(LineDelaySeconds),
                        cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Sortie du Play Mode ou rechargement de la scène pendant la séquence. Rien à
                // remonter : la victoire est déjà actée dans le VictorySystem et sauvegardée, seule
                // sa mise en scène est perdue.
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _disposables.Dispose();
        }
    }
}
