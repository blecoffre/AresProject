using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Utils;
using System;
using System.Text;

namespace Core.UI.Prestige
{
    /// <summary>
    /// L'inspecteur du nœud sélectionné, et le seul endroit du jeu d'où part un achat de
    /// prestige.
    ///
    /// Sans abonnement, comme les nœuds : c'est le panneau qui écoute et rediffuse.
    /// </summary>
    public class PrestigeDetailsPresenter
    {
        private readonly PrestigeDetailsView _view;
        private readonly PrestigeManager _prestigeManager;
        private readonly UserCurrencies _currencies;
        private readonly ILocalizationService _loc;

        /// <summary>Réutilisé d'une sélection à l'autre : le bloc d'effet fait deux lignes.</summary>
        private readonly StringBuilder _effects = new StringBuilder(160);

        /// <summary>Le séparateur de lignes du bloc d'effet. Jamais Environment.NewLine.</summary>
        private const char NewLine = '\n';

        private PrestigeConfigSO _selected;

        public PrestigeDetailsPresenter(
            PrestigeDetailsView view,
            PrestigeManager prestigeManager,
            UserCurrencies currencies,
            ILocalizationService loc)
        {
            _view = view;
            _prestigeManager = prestigeManager;
            _currencies = currencies;
            _loc = loc;

            _view.OnBuyClicked += HandleBuy;
        }

        /// <summary>Le nœud actuellement inspecté. Null tant que rien n'a été cliqué.</summary>
        public PrestigeConfigSO Selected => _selected;

        public void Select(PrestigeConfigSO config)
        {
            _selected = config;
            Refresh();
        }

        public void Clear()
        {
            _selected = null;
            _view.Hide();
        }

        /// <summary>
        /// Repeint l'inspecteur. Sans effet si rien n'est sélectionné — le panneau appelle cette
        /// méthode à chaque signal, sans avoir à savoir si le joueur a déjà cliqué un nœud.
        /// </summary>
        public void Refresh()
        {
            if (_selected == null) return;

            int currentLevel = _prestigeManager.GetLevel(_selected.Id);
            bool isUnlocked = _prestigeManager.IsUnlocked(_selected);
            bool isMaxedOut = currentLevel >= _selected.MaxLevel;
            bool isWindowOpen = _prestigeManager.ArePurchasesAllowed.CurrentValue;

            double cost = _selected.BaseCost * Math.Pow(_selected.CostMultiplier, currentLevel);
            bool canAfford = _currencies.CpuCycles.Amount.CurrentValue >= cost;

            PrestigeNodeState state = PrestigeNodeStates.Resolve(isUnlocked, isMaxedOut, currentLevel, canAfford);

            _view.RenderIdentity(
                PrestigeLabels.ResolveName(_selected, _loc),
                PrestigeLabels.ResolveDescription(_selected, _loc),
                BuildEffects(currentLevel));

            _view.RenderStatus(
                BuildPrerequisiteLine(isUnlocked),
                BuildCostLine(cost, isMaxedOut),
                isWindowOpen || isMaxedOut ? string.Empty : _loc.GetText("UI_PRESTIGE_DETAILS_NOTICE_RUN"),
                ResolveButtonLabel(isMaxedOut, isUnlocked, isWindowOpen, canAfford),
                !isMaxedOut && isUnlocked && isWindowOpen && canAfford,
                state);
        }

        /// <summary>L'effet par niveau, puis où en est le joueur sur ce nœud.</summary>
        private string BuildEffects(int currentLevel)
        {
            _effects.Clear();
            _effects.Append(PrestigeLabels.ResolveEffect(_selected, _loc));

            // Un saut de ligne simple, et non AppendLine() : cette dernière ajoute le retour
            // chariot de Windows, que TextMeshPro rend comme un caractère de plus.
            _effects.Append(NewLine);
            _effects.Append(_loc.GetText("UI_PRESTIGE_DETAILS_LEVEL", currentLevel, _selected.MaxLevel));

            return _effects.ToString();
        }

        private string BuildPrerequisiteLine(bool isUnlocked)
        {
            if (_selected.Prerequisite == null) return _loc.GetText("UI_PRESTIGE_DETAILS_PREREQ_NONE");

            string parentName = PrestigeLabels.ResolveName(_selected.Prerequisite, _loc);

            return _loc.GetText(
                isUnlocked ? "UI_PRESTIGE_DETAILS_PREREQ_OK" : "UI_PRESTIGE_DETAILS_PREREQ_MISSING",
                parentName);
        }

        private string BuildCostLine(double cost, bool isMaxedOut)
        {
            return isMaxedOut
                ? _loc.GetText("UI_PRESTIGE_DETAILS_MAXED")
                : _loc.GetText("UI_PRESTIGE_DETAILS_COST", CurrencyFormatter.Format(cost));
        }

        /// <summary>
        /// Le bouton porte le refus, pas seulement l'action. L'ordre des tests donne la raison la
        /// plus PROFONDE d'abord : dire « fonds insuffisants » sur un nœud dont la branche n'est
        /// même pas ouverte enverrait le joueur économiser pour rien.
        /// </summary>
        private string ResolveButtonLabel(bool isMaxedOut, bool isUnlocked, bool isWindowOpen, bool canAfford)
        {
            if (isMaxedOut) return _loc.GetText("UI_PRESTIGE_DETAILS_BTN_MAXED");
            if (!isUnlocked) return _loc.GetText("UI_PRESTIGE_DETAILS_BTN_LOCKED");
            if (!isWindowOpen) return _loc.GetText("UI_PRESTIGE_DETAILS_BTN_RUN");
            if (!canAfford) return _loc.GetText("UI_PRESTIGE_DETAILS_BTN_FUNDS");

            return _loc.GetText("UI_PRESTIGE_DETAILS_BTN_BUY");
        }

        /// <summary>
        /// L'achat. Aucune garde ici au-delà de la sélection : toutes les règles — profondeur de
        /// l'arbre, niveau max, budget, fenêtre de compilation — vivent dans le PrestigeManager,
        /// qui refusera de lui-même. Les répliquer ici, c'est se condamner à les voir diverger.
        ///
        /// Aucun rafraîchissement non plus : un achat réussi émet OnBonusesRecalculated, et c'est
        /// le panneau qui repeint l'arbre entier ET cet inspecteur.
        /// </summary>
        private void HandleBuy()
        {
            if (_selected == null) return;

            _prestigeManager.TryPurchasePrestige(_selected.Id);
        }

        public void Dispose()
        {
            _view.OnBuyClicked -= HandleBuy;
        }
    }
}
