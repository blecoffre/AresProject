using Core.Economy.Data;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Utils;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Prestige
{
    /// <summary>
    /// Construit l'arbre, tient la sélection, et rediffuse les signaux du modèle.
    ///
    /// <b>C'est ici que vivent TOUS les abonnements de l'écran.</b> Les 116 nœuds et l'inspecteur
    /// réagissent aux trois mêmes sources ; les leur faire écouter chacun coûtait 351 abonnements
    /// pour trois signaux, et surtout laissait la fraîcheur de l'affichage dépendre d'un détail
    /// d'implémentation de chaque enfant.
    /// </summary>
    public class PrestigePanelPresenter : IStartable, IDisposable
    {
        private readonly PrestigePanelView _view;
        private readonly PrestigeCatalogSO _catalog;
        private readonly PrestigeManager _prestigeManager;
        private readonly UserCurrencies _currencies;
        private readonly ILocalizationService _loc;

        private readonly List<PrestigeItemPresenter> _childPresenters = new();
        private readonly CompositeDisposable _disposables = new();

        private PrestigeDetailsPresenter _details;
        private PrestigeItemPresenter _selected;

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
            // instancie déjà ~116 nœuds, inutile d'y ajouter des redimensionnements de listes.
            _childPresenters.Capacity = configs.Count;
            _nodePositions = new Dictionary<string, Vector2>(configs.Count);

            _details = new PrestigeDetailsPresenter(_view.Details, _prestigeManager, _currencies, _loc);

            // L'inspecteur part masqué. Son propre Awake() le ferait, mais il ne s'exécutera
            // qu'à la première ouverture de l'écran — Unity n'appelle pas Awake() sous un parent
            // inactif. D'ici là, il resterait affiché avec le contenu du prefab.
            _details.Clear();

            BuildNodes(configs);
            BuildLinks(configs);

            _view.OnOpenClicked += HandleOpen;
            _view.OnCloseClicked += HandleClose;

            // Le solde change à chaque achat et à chaque fin de run : il pilote l'affichage du
            // budget ET la couleur de tous les nœuds.
            _currencies.CpuCycles.Amount
                .Subscribe(_ => HandleCyclesChanged())
                .AddTo(_disposables);

            // Le signal qui manquait. L'écran ne se rafraîchissait que sur la monnaie : un achat
            // n'ouvrait donc visuellement ses enfants que parce qu'il coûtait quelque chose. Ce
            // flux-là est émis après CHAQUE recalcul — achat comme chargement de sauvegarde.
            _prestigeManager.OnBonusesRecalculated
                .Subscribe(_ => RefreshAll())
                .AddTo(_disposables);

            // Ouverture et fermeture de la fenêtre de compilation : la pulsation ambre s'éteint
            // pendant une run, et le bouton d'achat annonce pourquoi il refuse.
            _prestigeManager.ArePurchasesAllowed
                .Subscribe(_ => RefreshAll())
                .AddTo(_disposables);
        }

        /// <summary>
        /// Bascule l'arbre sur Échap. Appelé par le bootstrapper de scène, seul à avoir un
        /// Update — un presenter POCO n'en a pas, et lui en donner un pour une touche serait
        /// disproportionné.
        /// </summary>
        public void ToggleFromKeyboard()
        {
            // Sans effet sur l'écran de fin de run : il n'y a rien derrière à quoi revenir, et
            // l'escamoter laisserait le joueur devant une partie déjà terminée.
            if (_view.IsRunEndMode) return;

            if (_view.IsVisible) HandleClose();
            else HandleOpen();
        }

        /// <summary>
        /// Affiche l'écran en mode fin de run : le bilan ET l'arbre, puisque c'est le moment de
        /// dépenser ce qu'on vient de gagner. Appelé par le GameOverPresenter.
        /// </summary>
        public void ShowRunEnd()
        {
            _details.Clear();
            ClearSelection();
            _view.ShowRunEnd();
        }

        /// <summary>
        /// <b>Le jeu ne se met PAS en pause.</b> La Trace continue de monter pendant que le
        /// joueur planifie ses achats, et c'est délibéré : le GDD interdit toute planque gratuite
        /// — c'est la raison même de `runInBackground = true`. Un panneau qui gèle le jeu serait
        /// exactement le refuge qu'alt-tab ne doit pas offrir.
        /// </summary>
        private void HandleOpen()
        {
            _view.ShowConsultation();
        }

        private void HandleClose()
        {
            _details.Clear();
            ClearSelection();
            _view.Hide();
        }

        /// <summary>
        /// Le clic sur un nœud le porte dans l'inspecteur. Un seul nœud est surligné à la fois :
        /// c'est ce qui rend lisible « ce que je lis à droite décrit CE bloc-là ».
        /// </summary>
        private void HandleNodeSelected(PrestigeItemPresenter presenter)
        {
            if (_selected != null) _selected.SetSelected(false);

            _selected = presenter;
            _selected.SetSelected(true);

            _details.Select(presenter.Config);
        }

        private void ClearSelection()
        {
            if (_selected == null) return;

            _selected.SetSelected(false);
            _selected = null;
        }

        private void HandleCyclesChanged()
        {
            _view.SetCyclesText(_loc.GetText(
                "UI_PRESTIGE_SCREEN_CYCLES",
                CurrencyFormatter.Format(_currencies.CpuCycles.Amount.CurrentValue)));

            RefreshAll();
        }

        /// <summary>
        /// Repeint l'arbre entier et l'inspecteur. Une boucle indexée sur 116 nœuds à chaque
        /// achat : c'est le prix d'un affichage qui ne peut pas mentir, et un achat de prestige
        /// n'arrive que quelques fois par run.
        ///
        /// Un achat déclenche les TROIS signaux dans la même frame, donc trois passes. Les
        /// coalescer par numéro de frame serait un piège : le débit de la monnaie précède
        /// l'incrément du niveau dans TryPurchasePrestige, si bien que la première passe lit
        /// encore l'ancien niveau. Garder la dernière passe est ce qui garantit l'état final.
        /// </summary>
        private void RefreshAll()
        {
            for (int i = 0; i < _childPresenters.Count; i++)
            {
                _childPresenters[i].Refresh();
            }

            _details.Refresh();
        }

        /// <summary>
        /// Instancie, place et lie un nœud par entrée du catalogue — <b>un seul</b>.
        /// </summary>
        private void BuildNodes(IReadOnlyList<PrestigeConfigSO> configs)
        {
            // Une seule allocation de délégué pour les 116 nœuds, plutôt qu'une par nœud.
            Action<PrestigeItemPresenter> onSelected = HandleNodeSelected;

            for (int i = 0; i < configs.Count; i++)
            {
                PrestigeConfigSO config = configs[i];

                // La coordonnée du JSON est une case de grille, pas un pixel. La conversion
                // appartient à la vue, qui est le seul endroit à connaître l'échelle d'affichage.
                Vector2 pixelPosition = _view.GridToPixels(config.UiPosition);

                PrestigeItemView node = _view.SpawnNode(pixelPosition);
                _nodePositions[config.Id] = pixelPosition;

                _childPresenters.Add(new PrestigeItemPresenter(
                    config, node, _prestigeManager, _currencies, _loc, onSelected));
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
            _view.OnOpenClicked -= HandleOpen;
            _view.OnCloseClicked -= HandleClose;

            _disposables.Dispose();

            for (int i = 0; i < _childPresenters.Count; i++)
            {
                _childPresenters[i].Dispose();
            }

            _childPresenters.Clear();
            _nodePositions?.Clear();

            _details?.Dispose();
        }
    }
}
