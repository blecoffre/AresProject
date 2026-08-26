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

            // Initialisation statique. Nom et description ne changent jamais : ils sont résolus
            // ici, une seule fois, et jamais depuis RefreshView() qui tourne à chaque variation
            // de CPU Cycles pour chacun des ~170 nœuds.
            _view.InitializeStaticData(ResolveName(), ResolveDescription());

            // Abonnement réactif : on écoute les changements de budget (CpuCycles)
            // Pour être ultra précis, il faudrait exposer un Subject dans le PrestigeManager 
            // quand un achat est fait, mais écouter la monnaie suffit pour refresh l'UI.
            _currencies.CpuCycles.Amount
                .Subscribe(_ => RefreshView())
                .AddTo(_disposables);

            _view.OnBuyClicked += HandleBuyRequest;

            RefreshView(); // Premier affichage
        }

        /// <summary>
        /// Les nœuds ciblant une upgrade précise n'ont pas de nom en base : on compose
        /// « &lt;nom de l'upgrade&gt; (Opti Coût) » depuis un gabarit localisé. Les autres portent
        /// leur propre clé, dérivée de leur id par le générateur.
        /// </summary>
        private string ResolveName()
        {
            string templateKey = GetSpecificNameTemplate(_config.BonusType);

            return templateKey == null
                ? _loc.GetText(_config.DisplayNameKey)
                : _loc.GetText(templateKey, ResolveTargetUpgradeName());
        }

        private string ResolveDescription()
        {
            string templateKey = GetSpecificDescriptionTemplate(_config.BonusType);

            return templateKey == null
                ? _loc.GetText(_config.DisplayDescriptionKey)
                : _loc.GetText(templateKey, ResolveTargetUpgradeName());
        }

        /// <summary>
        /// Résout le nom de l'upgrade ciblée par sa seule clé, sans passer par le catalogue :
        /// la clé se dérive de l'id, donc le presenter n'a aucune dépendance à injecter pour ça.
        /// </summary>
        private string ResolveTargetUpgradeName()
        {
            if (string.IsNullOrEmpty(_config.TargetUpgradeId))
            {
                // Nœud marqué « spécifique » mais sans cible : donnée incohérente, on le dit.
                UnityEngine.Debug.LogError(
                    $"[PRESTIGE] Le nœud '{_config.Id}' a un bonus ciblé ({_config.BonusType}) " +
                    "mais aucun targetUpgradeId. Son libellé sera incomplet.");

                return string.Empty;
            }

            return _loc.GetText(LocalizationKeys.UpgradeName(_config.TargetUpgradeId));
        }

        /// <summary>Retourne null si le bonus n'est pas ciblé — le nœud a alors sa propre clé.</summary>
        private static string GetSpecificNameTemplate(PrestigeBonusType bonusType)
        {
            switch (bonusType)
            {
                case PrestigeBonusType.SpecificUpgradeCostReduction:
                    return LocalizationKeys.PrestigeSpecificCostName;

                case PrestigeBonusType.SpecificUpgradeYieldBoost:
                    return LocalizationKeys.PrestigeSpecificYieldName;

                case PrestigeBonusType.SpecificUpgradeTimeReduction:
                    return LocalizationKeys.PrestigeSpecificTimeName;

                default:
                    return null;
            }
        }

        private static string GetSpecificDescriptionTemplate(PrestigeBonusType bonusType)
        {
            switch (bonusType)
            {
                case PrestigeBonusType.SpecificUpgradeCostReduction:
                    return LocalizationKeys.PrestigeSpecificCostDescription;

                case PrestigeBonusType.SpecificUpgradeYieldBoost:
                    return LocalizationKeys.PrestigeSpecificYieldDescription;

                case PrestigeBonusType.SpecificUpgradeTimeReduction:
                    return LocalizationKeys.PrestigeSpecificTimeDescription;

                default:
                    return null;
            }
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

            // Tout ce qui part à l'écran passe par une clé : ni le suffixe de monnaie, ni le
            // gabarit « Niv. x / y », ni la mention de niveau max ne sont écrits en dur.
            string costText = isMaxedOut
                ? _loc.GetText("UI_MAX_LEVEL")
                : _loc.GetText("UI_PRESTIGE_COST", CurrencyFormatter.Format(cost));

            string levelText = string.Empty;
            if (_config.MaxLevel > 1)
            {
                levelText = isMaxedOut
                    ? _loc.GetText("UI_MAX_LEVEL")
                    : _loc.GetText("UI_PRESTIGE_LEVEL", currentLevel, _config.MaxLevel);
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