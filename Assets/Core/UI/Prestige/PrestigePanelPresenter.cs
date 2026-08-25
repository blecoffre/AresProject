using Core.Economy.Data;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Prestige
{
    public class PrestigePanelPresenter : IStartable, IDisposable
    {
        private readonly PrestigePanelView _view;
        private readonly PrestigeCatalogSO _catalog;
        private readonly PrestigeManager _prestigeManager;
        private readonly UserCurrencies _currencies;
        private readonly ILocalizationService _loc;

        private readonly List<PrestigeItemPresenter> _childPresenters = new();

        private readonly Vector2 _gridCellSize = new Vector2(600f, 300f);

        public PrestigePanelPresenter(
            PrestigePanelView view,
            PrestigeCatalogSO catalog,
            PrestigeManager prestigeManager,
            UserCurrencies currencies,
            ILocalizationService loc)
        {
            _view = view;
            _catalog = catalog;
            _prestigeManager = prestigeManager;
            _currencies = currencies;
            _loc = loc;
        }

        public void Start()
        {
            // On spawn tous les nœuds de la toile d'araignée
            foreach (var config in _catalog.GetAllUpgrades())
            {
                var nodeView = _view.SpawnNode();
                var nodePresenter = new PrestigeItemPresenter(config, nodeView, _prestigeManager, _currencies, _loc);

                _childPresenters.Add(nodePresenter);
            }

            var nodePositions = new Dictionary<string, Vector2>();

            // Étape 1 : On place tous les nœuds
            foreach (var config in _catalog.GetAllUpgrades())
            {
                // 1. Calcul de la position finale en multipliant la coordonnée JSON par la taille de la cellule
                Vector2 finalPosition = new Vector2(
                    config.UiPosition.x * _gridCellSize.x,
                    config.UiPosition.y * _gridCellSize.y
                );

                // 2. Instanciation et placement
                PrestigeItemView node = _view.SpawnNode();
                node.GetComponent<RectTransform>().anchoredPosition = finalPosition;

                // On sauvegarde la position finale pour le tracé des lignes
                nodePositions[config.Id] = finalPosition;

                var nodePresenter = new PrestigeItemPresenter(config, node, _prestigeManager, _currencies, _loc);
                _childPresenters.Add(nodePresenter);
            }

            // Étape 2 : On trace les lignes
            foreach (var config in _catalog.GetAllUpgrades())
            {
                if (config.Prerequisite != null)
                {
                    if (nodePositions.TryGetValue(config.Prerequisite.Id, out Vector2 parentPos))
                    {
                        UILineConnection line = _view.SpawnLine();
                        line.DrawLine(parentPos, config.UiPosition);

                        // On récupère ou on crée les flux réactifs du niveau actuel
                        var parentLevelObs = _prestigeManager.GetLevelObservable(config.Prerequisite.Id);
                        var childLevelObs = _prestigeManager.GetLevelObservable(config.Id);

                        // On lie l'état de la ligne à la progression de l'arbre
                        line.BindState(parentLevelObs, childLevelObs, config.MaxLevel);
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (var presenter in _childPresenters)
            {
                presenter.Dispose();
            }
            _childPresenters.Clear();
        }
    }
}