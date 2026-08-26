using Core.Models.Console;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.UI.Game;
using Core.Utils;
using R3;
using System;
using UnityEngine;

namespace Core.UI.Upgrades
{
    public class GeneratorPresenter : IDisposable
    {
        private readonly UpgradeModel _model;
        private readonly GeneratorView _view;
        private readonly UpgradeManager _upgradeManager;
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly UserCurrencies _currencies;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;

        private readonly bool _isScript;

        public GeneratorPresenter(
            UpgradeModel model,
            GeneratorView view,
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            UserCurrencies currencies,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _model = model;
            _view = view;
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _currencies = currencies;
            _console = console;
            _loc = loc;

            _isScript = model.Config.Type == UpgradeType.Script;
            _disposables = new CompositeDisposable();

            _view.InitializeStaticData(
                _loc.GetText(_model.Config.DisplayNameKey),
                BuildStatsText());

            _view.OnBuyClicked += HandleBuyRequest;
            _view.OnRunClicked += HandleRunRequest;

            BindLevel();
            BindAffordability();
            BindCycle();
        }

        private void BindLevel()
        {
            _model.CurrentLevel
                .Subscribe(level =>
                {
                    _view.UpdateCostAndLevel(
                        _loc.GetText("UI_GENERATOR_LEVEL", level),
                        _loc.GetText("UI_GENERATOR_COST", CurrencyFormatter.Format(_model.GetCurrentCost())));

                    _view.UpdateStats(BuildStatsText());
                    RefreshRunButton();
                })
                .AddTo(_disposables);
        }

        private void BindAffordability()
        {
            // Le coût ne bouge qu'à l'achat, il est donc en cache dans le modèle. On ne pousse à
            // la vue que le passage de "pas assez" à "assez" : sans ce DistinctUntilChanged,
            // chaque versement de cycle réveillait les 45 générateurs affichés.
            _currencies.Money.Amount
                .Select(money => money >= _model.GetCurrentCost())
                .DistinctUntilChanged()
                .Subscribe(_view.SetBuyButtonInteractable)
                .AddTo(_disposables);
        }

        private void BindCycle()
        {
            if (!_isScript) return;

            ReadOnlyReactiveProperty<float> progress = _cycleRunner.GetProgress(_model.Config.Id);
            if (progress == null) return;

            progress
                .Subscribe(_view.UpdateSweepProgress)
                .AddTo(_disposables);

            // Un cycle manuel s'arrête de lui-même à la livraison : c'est ce flux qui rend le
            // bouton de relance cliquable, sans interroger le runner à chaque frame.
            ReadOnlyReactiveProperty<bool> running = _cycleRunner.GetRunning(_model.Config.Id);
            if (running != null)
            {
                running
                    .Subscribe(_ => RefreshRunButton())
                    .AddTo(_disposables);
            }

            if (!_view.HasRunButton)
            {
                Debug.LogWarning(
                    $"[GENERATOR] Le prefab de '{_model.Config.Id}' n'a pas de bouton de lancement câblé : " +
                    "ce Script sera injouable tant qu'il n'est pas automatisé.");
            }

            RefreshRunButton();
        }

        /// <summary>
        /// Le bouton de lancement n'existe que pour les Scripts non encore automatisés, et n'est
        /// cliquable que si le générateur est possédé et qu'aucun cycle ne tourne.
        /// </summary>
        private void RefreshRunButton()
        {
            if (!_isScript) return;

            bool isAutomated = _model.IsAutomated;
            bool isOwned = _model.IsOwned;

            _view.SetRunState(
                isVisible: isOwned && !isAutomated,
                isInteractable: isOwned && !isAutomated && !_cycleRunner.IsRunning(_model.Config.Id));
        }

        private string BuildStatsText()
        {
            if (_isScript)
            {
                // Pour un Script, la donnée utile est le versement et la cadence, pas un débit abstrait.
                return _loc.GetText(
                    "UI_GENERATES_DATAS",
                    CurrencyFormatter.Format(_model.GetCurrentYield()),
                    _model.GetCurrentCycleDuration().ToString("0.##"));
            }

            return _loc.GetText("UI_GENERATES_DATAS", CurrencyFormatter.Format(_model.GetCurrentYield()));
        }

        public void SetVisibility(bool isVisible)
        {
            _view.SetVisible(isVisible);
        }

        private void HandleBuyRequest()
        {
            if (!_upgradeManager.TryPurchaseUpgrade(_model.Config.Id)) return;

            // Le log était construit puis jeté : il part enfin dans la console.
            _console.Log(
                _loc.GetText(
                    "LOG_UPGRADE_PURCHASED",
                    _loc.GetText(_model.Config.DisplayNameKey),
                    _model.CurrentLevel.CurrentValue),
                ConsoleLogType.Standard);
        }

        private void HandleRunRequest()
        {
            if (_cycleRunner.TryStartCycle(_model.Config.Id))
            {
                RefreshRunButton();
            }
        }

        public void Dispose()
        {
            _view.OnBuyClicked -= HandleBuyRequest;
            _view.OnRunClicked -= HandleRunRequest;
            _disposables.Dispose();
        }
    }
}
