using Core.Models.Console;
using R3;
using System;

namespace Core.UI.Game
{
    public class ConsolePresenter : IDisposable
    {
        private readonly ConsoleView _view;
        private readonly CompositeDisposable _disposables;

        // Un Subject interne pour pousser des logs dynamiquement depuis n'importe quel système du jeu
        private readonly Subject<LogEntry> _onLogRequested = new();

        public ConsolePresenter(ConsoleView view)
        {
            _view = view;
            _disposables = new CompositeDisposable();

            // Abonnement réactif R3 : dès qu'un log est demandé, on met à jour la vue
            _onLogRequested
                .Subscribe(entry => _view.AppendLog(entry.Message, entry.Type))
                .AddTo(_disposables);
        }

        public void Log(string message, ConsoleLogType type = ConsoleLogType.Standard)
        {
            _onLogRequested.OnNext(new LogEntry(message, type));
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _onLogRequested.Dispose();
        }
    }
}