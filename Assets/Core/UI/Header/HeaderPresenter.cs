using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Security;
using Core.Utils;
using R3;
using System;
using VContainer.Unity;

namespace Core.UI.Header
{
    public class HeaderPresenter : IStartable, IDisposable
    {
        private readonly UserCurrencies _userCurrencies;
        private readonly HeaderView _view;
        private readonly UpgradeManager _upgradeManager;
        private readonly ThreatManager _threatManager;

        private DisposableBag _disposables;

        public HeaderPresenter(UserCurrencies userCurrencies, HeaderView view, UpgradeManager upgradeManager, ThreatManager threatManager)
        {
            _userCurrencies = userCurrencies;
            _view = view;
            _upgradeManager = upgradeManager;
            _threatManager = threatManager;

            _disposables = new DisposableBag();

            _upgradeManager.TotalMoneyYieldPerSecond.Subscribe(yield =>
            {
                string formattedYield = CurrencyFormatter.Format(yield);
                _view.UpdateMoneyYieldDisplay(formattedYield);
            })
            .AddTo(ref _disposables);

            _userCurrencies.ComputerPower.Amount.Subscribe(val =>
            {
                _view.UpdateComputerPowerDisplay(CurrencyFormatter.Format(val));
            })
            .AddTo(ref _disposables);

            _threatManager.NormalizedThreat.Subscribe(threatValue =>
            {
                _view.UpdateTraceDisplay(threatValue);
            })
            .AddTo(ref _disposables);
        }

        public void Start()
        {
            BindEconomyToView();
        }

        private void BindEconomyToView()
        {
            // Dès que Datas.Amount change, on le formate et on l'envoie à la vue.
            _userCurrencies.Money.Amount
                .Subscribe(val => _view.UpdateMoneyDisplay(CurrencyFormatter.Format(val)))
                .AddTo(ref _disposables);


            // Et les Cycles CPU
            _userCurrencies.CpuCycles.Amount
                .Subscribe(val => _view.UpdateCpuCyclesDisplay(CurrencyFormatter.Format(val)))
                .AddTo(ref _disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}