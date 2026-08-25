using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Utils;
using R3;
using System;

namespace Core.UI.Prestige
{
    public class PrestigeItemPresenter : IDisposable
    {
        private readonly PrestigeConfigSO _config;
        private readonly PrestigeItemView _view;
        private readonly PrestigeManager _prestigeManager;
        private readonly UserCurrencies _currencies;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;

        public PrestigeItemPresenter(
            PrestigeConfigSO config,
            PrestigeItemView view,
            PrestigeManager prestigeManager,
            UserCurrencies currencies,
            ILocalizationService loc)
        {
            _config = config;
            _view = view;
            _prestigeManager = prestigeManager;
            _currencies = currencies;
            _loc = loc;

            _disposables = new CompositeDisposable();

            // Initialisation statique
            string name = _loc.GetText(_config.DisplayNameKey);
            string desc = _loc.GetText(_config.DisplayDescriptionKey);
            _view.InitializeStaticData(name, desc);

            // Placement du nœud (si tu ajoutes un champ _uiPosition dans le SO)
            // _view.SetNodePosition(_config.UiPosition);

            // Abonnement réactif : on écoute les changements de budget (CpuCycles)
            // Pour être ultra précis, il faudrait exposer un Subject dans le PrestigeManager 
            // quand un achat est fait, mais écouter la monnaie suffit pour refresh l'UI.
            _currencies.CpuCycles.Amount
                .Subscribe(_ => RefreshView())
                .AddTo(_disposables);

            _view.OnBuyClicked += HandleBuyRequest;

            RefreshView(); // Premier affichage
        }

        private void RefreshView()
        {
            // 1. Vérification du prérequis (Le Brouillard de Guerre)
            bool isLocked = false;
            if (_config.Prerequisite != null)
            {
                // Si le niveau du parent est 0, ce nœud est bloqué (caché sous un "?")
                if (_prestigeManager.GetLevel(_config.Prerequisite.Id) == 0)
                {
                    isLocked = true;
                }
            }

            _view.SetLockState(isLocked);

            // Si c'est bloqué, pas besoin de calculer l'économie
            if (isLocked) return;

            int currentLevel = _prestigeManager.GetLevel(_config.Id);
            bool isMaxedOut = currentLevel >= _config.MaxLevel;

            double cost = _config.BaseCost * Math.Pow(_config.CostMultiplier, currentLevel);
            bool canAfford = _currencies.CpuCycles.Amount.CurrentValue >= cost;

            string costText = CurrencyFormatter.Format(cost) + " CPU";

            string levelText = "";
            if (_config.MaxLevel > 1)
            {
                levelText = isMaxedOut ? _loc.GetText("UI_MAX_LEVEL") : $"Niv. {currentLevel} / {_config.MaxLevel}";
            }
            else if (isMaxedOut)
            {
                levelText = _loc.GetText("UI_ACQUIRED");
            }

            _view.UpdateDynamicData(levelText, costText, canAfford, isMaxedOut);
        }

        private void HandleBuyRequest()
        {
            if (_prestigeManager.TryPurchasePrestige(_config.Id))
            {
                RefreshView(); // Force la mise à jour immédiate
            }
        }

        public void Dispose()
        {
            _view.OnBuyClicked -= HandleBuyRequest;
            _disposables.Dispose();
        }
    }
}