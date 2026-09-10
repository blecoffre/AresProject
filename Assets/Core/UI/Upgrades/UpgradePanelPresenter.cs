using Core.Models.Console;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.UI.Game;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Upgrades
{
    public class UpgradePanelPresenter : IStartable, IDisposable
    {
        private readonly UpgradePanelView _panelView;
        private readonly UpgradeManager _upgradeManager;
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly UserCurrencies _currencies;
        private readonly BuyQuantitySelector _buyQuantity;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;
        private readonly Dictionary<UpgradeModel, GeneratorPresenter> _childPresenters = new();

        private readonly ReactiveProperty<UpgradeType> _currentTab;

        /// <summary>
        /// L'avertissement sur le libellé d'achat manquant n'est émis qu'une fois. Le laisser
        /// partir depuis GeneratorPresenter ou depuis l'Awake du prefab en cracherait quarante-cinq :
        /// c'est le MÊME prefab, un seul message dit déjà tout ce qu'il y a à corriger.
        /// </summary>
        private bool _hasWarnedAboutBuyLabel;

        public UpgradePanelPresenter(
            UpgradePanelView panelView,
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            UserCurrencies currencies,
            BuyQuantitySelector buyQuantity,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _panelView = panelView;
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _currencies = currencies;
            _buyQuantity = buyQuantity;
            _console = console;
            _loc = loc;

            _disposables = new CompositeDisposable();
            _currentTab = new ReactiveProperty<UpgradeType>(UpgradeType.Script);
        }

        public void Start()
        {
            // Méthode nommée : un "-=" sur une lambda crée un nouveau delegate et ne désabonne rien.
            _panelView.OnTabClicked += HandleTabClicked;
            _panelView.OnBuyQuantityClicked += HandleBuyQuantityClicked;

            _currentTab
                .Subscribe(_panelView.ShowTab)
                .AddTo(_disposables);

            // L'état du sélecteur vit dans un service du scope RACINE, pas ici : il est restauré
            // depuis la sauvegarde AVANT que cette scène n'existe. S'abonner plutôt que de lire
            // une fois est donc indispensable — c'est aussi ce qui rallume le bon bouton après un
            // rechargement de la GameScene.
            _buyQuantity.Current
                .Subscribe(_panelView.ShowBuyQuantity)
                .AddTo(_disposables);

            InitializePanel();
        }

        private void HandleTabClicked(UpgradeType tab)
        {
            _currentTab.Value = tab;
        }

        private void HandleBuyQuantityClicked(BuyQuantity quantity)
        {
            _buyQuantity.Select(quantity);
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

            if (!viewInstance.HasBuyLabel && !_hasWarnedAboutBuyLabel)
            {
                _hasWarnedAboutBuyLabel = true;

                Debug.LogWarning(
                    "[UPGRADES] Le prefab de générateur n'a pas de libellé d'achat câblé : le " +
                    "bouton n'affichera pas le multiplicateur courant, et le joueur en mode MAX " +
                    "ne verra nulle part combien de niveaux son clic va acheter.");
            }

            var presenter = new GeneratorPresenter(
                model, viewInstance, _upgradeManager, _cycleRunner, _currencies, _buyQuantity, _console, _loc);

            _childPresenters.Add(model, presenter);

            viewInstance.SetVisible(true);
        }

        public void Dispose()
        {
            _panelView.OnTabClicked -= HandleTabClicked;
            _panelView.OnBuyQuantityClicked -= HandleBuyQuantityClicked;

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
