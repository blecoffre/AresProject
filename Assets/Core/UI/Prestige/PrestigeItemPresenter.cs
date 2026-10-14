using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Utils;
using Core.UI.Theme;
using System;

namespace Core.UI.Prestige
{
    /// <summary>
    /// Un nœud de l'arbre. Traduit l'état du modèle en code couleur et en deux lignes de texte.
    ///
    /// <b>N'a aucun abonnement.</b> C'est délibéré : faire écouter à chaque nœud les trois mêmes
    /// sources — la monnaie, l'arbre, la fenêtre d'achat — multipliait les abonnements par le
    /// nombre de nœuds (348 quand l'arbre en comptait 116) pour trois signaux. Le panneau écoute
    /// une fois et rediffuse.
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

            // Initialisation statique. Le nom ne change qu'avec la langue : il est résolu ici et
            // dans RefreshLocalizedTexts(), jamais depuis Refresh() qui tourne pour chaque nœud
            // à chaque variation de CPU Cycles.
            _view.InitializeStaticData(PrestigeLabels.ResolveName(config, loc));

            _view.OnNodeClicked += HandleClicked;

            Refresh();
        }

        /// <summary>Changement de palette (daltonisme) : la vue la garde, le panneau repeindra.</summary>
        public void SetPalette(UiPaletteSO palette) => _view.SetPalette(palette);

        /// <summary>Changement de langue à chaud : le nom, puis l'état dans la nouvelle langue.</summary>
        public void RefreshLocalizedTexts()
        {
            _view.InitializeStaticData(PrestigeLabels.ResolveName(Config, _loc));
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

            double cost = Config.GetCostAtLevel(currentLevel);
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
                    // Un nœud sans plafond (P_CORE_OVERRIDE) n'a pas de « / max » à afficher :
                    // « Niv. 12 / 2147483647 » serait un mensonge, et un compteur suffit.
                    statusText = Config.IsUnlimited
                        ? _loc.GetText("UI_PRESTIGE_LEVEL_UNLIMITED", currentLevel)
                        : _loc.GetText("UI_PRESTIGE_LEVEL", currentLevel, Config.MaxLevel);
                    break;

                // Jamais acheté, branche ouverte : le prix est la seule information actionnable,
                // et « Niv. 0 / 10 » n'en est pas une.
                default:
                    statusText = _loc.GetText("UI_PRESTIGE_COST", CurrencyFormatter.FormatCost(cost));
                    break;
            }

            // Le nœud débloquable respire dans tous les cas ; la fenêtre de compilation ne décide
            // que de l'insistance. Pendant une run l'achat est impossible, mais c'est aussi le
            // moment où l'on consulte l'arbre pour planifier — et on repère les actionnables du
            // coin de l'œil, pas en lisant les libellés.
            bool canPurchaseNow = _prestigeManager.ArePurchasesAllowed.CurrentValue;

            _view.Render(statusText, state, canPurchaseNow);
        }

        /// <summary>
        /// Un clic SÉLECTIONNE, il n'achète pas. L'achat vit dans l'inspecteur, derrière un
        /// bouton qui dit ce qu'il fait : un clic qui dépense immédiatement une monnaie gagnée
        /// en une run entière est un piège.
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
