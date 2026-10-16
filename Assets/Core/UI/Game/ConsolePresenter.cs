using Core.Models.Console;
using Core.Services.Localization;
using Core.UI.Theme;
using R3;
using System;

namespace Core.UI.Game
{
    public class ConsolePresenter : IDisposable
    {
        private readonly ConsoleView _view;
        private readonly ILocalizationService _loc;
        private readonly IPaletteService _palette;
        private readonly CompositeDisposable _disposables;

        // Un Subject interne pour pousser des logs dynamiquement depuis n'importe quel système du jeu
        private readonly Subject<LogEntry> _onLogRequested = new();

        public ConsolePresenter(ConsoleView view, ILocalizationService loc, IPaletteService palette)
        {
            _view = view;
            _loc = loc;
            _palette = palette;
            _disposables = new CompositeDisposable();

            // Les étiquettes [OK] / [SYSTEM_WARN] / [INFO] suivent la langue ET la palette. Les
            // deux abonnements émettent tout de suite : les préfixes sont prêts avant le premier log.
            _loc.CurrentLanguage.Subscribe(_ => RenderTagPrefixes()).AddTo(_disposables);
            _palette.Current.Subscribe(_ => RenderTagPrefixes()).AddTo(_disposables);

            // Abonnement réactif R3 : dès qu'un log est demandé, on met à jour la vue
            _onLogRequested
                .Subscribe(entry => _view.AppendLog(entry.Message, entry.Type))
                .AddTo(_disposables);
        }

        /// <summary>
        /// Compose les quatre préfixes UNE fois par changement de langue ou de palette — jamais
        /// par ligne de log. Le critique de l'Overclock prend la couleur du succès : c'est un
        /// gain, et le vert est déjà celui du jeu pour « c'est passé ».
        /// </summary>
        private void RenderTagPrefixes()
        {
            UiPaletteSO p = _palette.Current.CurrentValue;
            _view.SetTagPrefixes(
                "<color=" + p.ConsoleSuccessHex + ">" + _loc.GetText("CONSOLE_TAG_SUCCESS") + "</color> ",
                "<color=" + p.ConsoleNarrativeHex + ">" + _loc.GetText("CONSOLE_TAG_NARRATIVE") + "</color> ",
                "<color=" + p.ConsoleStandardHex + ">" + _loc.GetText("CONSOLE_TAG_STANDARD") + "</color> ",
                "<color=" + p.ConsoleSuccessHex + ">" + _loc.GetText("CONSOLE_TAG_CRITICAL") + "</color> ");
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