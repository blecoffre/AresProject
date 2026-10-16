using Core.Models.Console;
using Core.Services.Localization;
using Core.Services.Security;
using R3;
using System;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Raconte dans la console les achats d'office de la Routine Pare-Feu. Sans vue : le service
    /// vit dans le Root, la console dans la scène, et un Proxy acheté à l'insu du joueur doit se
    /// lire — sinon il verrait ses Datas fondre sans comprendre pourquoi.
    /// </summary>
    public sealed class FirewallRoutinePresenter : IStartable, IDisposable
    {
        private readonly FirewallRoutine _firewall;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private DisposableBag _disposables;

        public FirewallRoutinePresenter(FirewallRoutine firewall, ConsolePresenter console, ILocalizationService loc)
        {
            _firewall = firewall;
            _console = console;
            _loc = loc;
        }

        public void Start()
        {
            _firewall.OnTriggered
                .Subscribe(this, (purchase, self) => self.Log(purchase))
                .AddTo(ref _disposables);
        }

        private void Log(FirewallPurchase purchase)
        {
            _console.Log(
                _loc.GetText("LOG_FIREWALL_ROUTINE", _loc.GetText(purchase.ProxyNameKey), purchase.TraceWiped),
                ConsoleLogType.Success);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
