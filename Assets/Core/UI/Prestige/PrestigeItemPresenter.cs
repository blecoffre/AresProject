using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Utils;
using System;

namespace Core.UI.Prestige
{
    /// <summary>
    /// Un nœud de l'arbre. Traduit l'état du modèle en code couleur et en deux lignes de texte.
    ///
    /// <b>N'a aucun abonnement.</b> C'est délibéré : l'arbre compte 116 nœuds, et leur faire
    /// écouter chacun les trois mêmes sources — la monnaie, l'arbre, la fenêtre d'achat — coûtait
    /// 348 abonnements pour trois signaux. Le panneau écoute une fois et rediffuse.
    /// </summary>
    public class PrestigeItemPresenter
    {
        private readonly PrestigeItemView _view;
        private readonly PrestigeManager _prestigeManager;
        private readonly UserCurrencies _currencies;
        private readonly ILocalizationService _loc;
        private readonly Action<PrestigeItemPresenter> _onSelected;

        public PrestigeConfigSO Config { get; }

        public PrestigeItemPresenter(
            PrestigeConfigSO config,
            PrestigeItemView view,
            PrestigeManager prestigeManager,
            UserCurrencies currencies,
            ILocalizationService loc,
            Action<PrestigeItemPresenter> onSelected)
        {
            Config = config;
            _view = view;
            _prestigeManager = prestigeManager;
            _currencies = currencies;
            _loc = loc;
            _onSelected = onSelected;

            // Initialisation statique. Le nom ne change jamais : il est résolu ici, une seule
            // fois, et jamais depuis Refresh() qui tourne pour chacun des 116 nœuds à chaque
            // variation de CPU Cycles.
            _view.InitializeStaticData(PrestigeLabels.ResolveName(config, loc));

            _view.OnNodeClicked += HandleClicked;

            Refresh();
        }

        /// <summary>Le nœud est-il celui qu'affiche l'inspecteur.</summary>
        public void SetSelected(bool isSelected)
        {
            _view.SetSelected(isSelected);
        }

        /// <summary>
        /// Recalcule l'état et le repeint. Appelé par le panneau à chaque signal — achat,
        /// chargement de sauvegarde, variation de monnaie, ouverture ou fermeture de la fenêtre
        /// de compilation.
        /// </summary>
        public void Refresh()
        {
            int currentLevel = _prestigeManager.GetLevel(Config.Id);
            bool isUnlocked = _prestigeManager.IsUnlocked(Config);
            bool isMaxedOut = currentLevel >= Config.MaxLevel;

            double cost = Config.BaseCost * Math.Pow(Config.CostMultiplier, currentLevel);
            bool canAfford = _currencies.CpuCycles.Amount.CurrentValue >= cost;

            PrestigeNodeState state = PrestigeNodeStates.Resolve(isUnlocked, isMaxedOut, currentLevel, canAfford);

            // UNE seule ligne d'information sous le nom : le nœud n'a jamais qu'une chose à dire,
            // et c'est l'état qui décide laquelle.
            //
            // Tout ce qui part à l'écran passe par une clé : ni le suffixe de monnaie, ni le
            // gabarit « Niv. x / y », ni la mention de niveau max ne sont écrits en dur.
            string statusText;

            switch (state)
            {
                case PrestigeNodeState.Locked:
                    statusText = _loc.GetText("UI_PRESTIGE_LOCKED");
                    break;

                case PrestigeNodeState.Maxed:
                    statusText = _loc.GetText("UI_MAX_LEVEL");
                    break;

                // Entamé sans être fini : la progression prime sur le prix du rang suivant, que
                // l'inspecteur donne de toute façon. Ce cas implique MaxLevel > 1 — un nœud à
                // achat unique passe directement de « jamais acheté » à « au maximum ».
                case PrestigeNodeState.InProgress:
                    statusText = _loc.GetText("UI_PRESTIGE_LEVEL", currentLevel, Config.MaxLevel);
                    break;

                // Jamais acheté, branche ouverte : le prix est la seule information actionnable,
                // et « Niv. 0 / 10 » n'en est pas une.
                default:
                    statusText = _loc.GetText("UI_PRESTIGE_COST", CurrencyFormatter.FormatCost(cost));
                    break;
            }

            // La pulsation est un appel à l'action : elle n'a pas lieu d'être quand la fenêtre
            // de compilation est fermée, c'est-à-dire pendant toute une run.
            bool pulse = _prestigeManager.ArePurchasesAllowed.CurrentValue;

            _view.Render(statusText, state, pulse);
        }

        /// <summary>
        /// Un clic SÉLECTIONNE, il n'achète pas. L'achat vit dans l'inspecteur, derrière un
        /// bouton qui dit ce qu'il fait : sur un arbre de 116 nœuds, un clic qui dépense
        /// immédiatement une monnaie gagnée en une run entière est un piège.
        /// </summary>
        private void HandleClicked()
        {
            _onSelected?.Invoke(this);
        }

        public void Dispose()
        {
            _view.OnNodeClicked -= HandleClicked;
        }
    }
}
