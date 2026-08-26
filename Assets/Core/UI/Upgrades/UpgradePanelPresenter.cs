using Core.Models.Console;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.UI.Game;
using R3;
using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace Core.UI.Upgrades
{
    public class UpgradePanelPresenter : IStartable, IDisposable
    {
        private readonly UpgradePanelView _panelView;
        private readonly UpgradeManager _upgradeManager;
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly UserCurrencies _currencies;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;
        private readonly Dictionary<UpgradeModel, GeneratorPresenter> _childPresenters = new();

        private readonly ReactiveProperty<UpgradeType> _currentTab;

        public UpgradePanelPresenter(
            UpgradePanelView panelView,
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            UserCurrencies currencies,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _panelView = panelView;
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _currencies = currencies;
            _console = console;
            _loc = loc;

            _disposables = new CompositeDisposable();
            _currentTab = new ReactiveProperty<UpgradeType>(UpgradeType.Script);
        }

        public void Start()
        {
            // Méthode nommée : un "-=" sur une lambda crée un nouveau delegate et ne désabonne rien.
            _panelView.OnTabClicked += HandleTabClicked;

            _currentTab
                .Subscribe(_panelView.ShowTab)
                .AddTo(_disposables);

            InitializePanel();
        }

        private void HandleTabClicked(UpgradeType tab)
        {
            _currentTab.Value = tab;
        }

        private void InitializePanel()
        {
            foreach (var model in _upgradeManager.GetInitiallyVisibleUpgrades())
            {
                SpawnAndBindGenerator(model);
            }

            _upgradeManager.OnUpgradeRevealed
                .Subscribe(newModel =>
                {
                    SpawnAndBindGenerator(newModel);

                    _console.Log(
                        _loc.GetText("LOG_NEW_HARDWARE", _loc.GetText(newModel.Config.DisplayNameKey)),
                        ConsoleLogType.Narrative);
                })
                .AddTo(_disposables);
        }

        private void SpawnAndBindGenerator(UpgradeModel model)
        {
            GeneratorView viewInstance = _panelView.SpawnGeneratorView(model.Config.Type);

            var presenter = new GeneratorPresenter(
                model, viewInstance, _upgradeManager, _cycleRunner, _currencies, _console, _loc);

            _childPresenters.Add(model, presenter);

            viewInstance.SetVisible(true);
        }

        public void Dispose()
        {
            _panelView.OnTabClicked -= HandleTabClicked;

            _disposables.Dispose();
            _currentTab.Dispose();

            foreach (var presenter in _childPresenters.Values)
            {
                presenter.Dispose();
            }
            _childPresenters.Clear();
        }
    }
}
