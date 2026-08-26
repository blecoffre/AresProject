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

        /// <summary>
        /// Position finale, en pixels, de chaque nœud déjà placé. Indispensable au tracé des
        /// liens : un prérequis peut apparaître APRÈS son enfant dans le catalogue, donc les
        /// lignes ne peuvent être tracées qu'une fois tous les nœuds positionnés.
        /// </summary>
        private Dictionary<string, Vector2> _nodePositions;

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
            IReadOnlyList<PrestigeConfigSO> configs = _catalog.GetAllUpgrades();

            // Boucles indexées et collections dimensionnées d'avance : la construction de l'arbre
            // instancie déjà ~170 nœuds, inutile d'y ajouter des redimensionnements de listes.
            _childPresenters.Capacity = configs.Count;
            _nodePositions = new Dictionary<string, Vector2>(configs.Count);

            BuildNodes(configs);
            BuildLinks(configs);
        }

        /// <summary>
        /// Instancie, place et lie un nœud par entrée du catalogue — <b>un seul</b>.
        /// </summary>
        private void BuildNodes(IReadOnlyList<PrestigeConfigSO> configs)
        {
            for (int i = 0; i < configs.Count; i++)
            {
                PrestigeConfigSO config = configs[i];

                // La coordonnée du JSON est une case de grille, pas un pixel. La conversion
                // appartient à la vue, qui est le seul endroit à connaître l'échelle d'affichage.
                Vector2 pixelPosition = _view.GridToPixels(config.UiPosition);

                PrestigeItemView node = _view.SpawnNode(pixelPosition);
                _nodePositions[config.Id] = pixelPosition;

                _childPresenters.Add(
                    new PrestigeItemPresenter(config, node, _prestigeManager, _currencies, _loc));
            }
        }

        /// <summary>
        /// Trace un lien par nœud possédant un prérequis. Passe séparée de <see cref="BuildNodes"/>
        /// car les deux extrémités doivent déjà être positionnées.
        /// </summary>
        private void BuildLinks(IReadOnlyList<PrestigeConfigSO> configs)
        {
            for (int i = 0; i < configs.Count; i++)
            {
                PrestigeConfigSO config = configs[i];
                if (config.Prerequisite == null) continue;

                if (!_nodePositions.TryGetValue(config.Prerequisite.Id, out Vector2 parentPosition))
                {
                    // Prérequis absent du catalogue : le lien ne mène nulle part. On le signale
                    // plutôt que de tracer une ligne vers l'origine.
                    Debug.LogError(
                        $"[PRESTIGE] Le nœud '{config.Id}' déclare le prérequis '{config.Prerequisite.Id}', " +
                        "absent du catalogue. Lien ignoré.");
                    continue;
                }

                // Les DEUX extrémités doivent être lues dans le même repère, en pixels. L'ancien
                // code passait `config.UiPosition` — une coordonnée de grille — ce qui écrasait
                // toutes les lignes près de l'origine.
                if (!_nodePositions.TryGetValue(config.Id, out Vector2 childPosition)) continue;

                UILineConnection line = _view.SpawnLine();
                line.DrawLine(parentPosition, childPosition);

                line.BindState(
                    _prestigeManager.GetLevelObservable(config.Prerequisite.Id),
                    _prestigeManager.GetLevelObservable(config.Id),
                    config.MaxLevel);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _childPresenters.Count; i++)
            {
                _childPresenters[i].Dispose();
            }

            _childPresenters.Clear();
            _nodePositions?.Clear();
        }
    }
}