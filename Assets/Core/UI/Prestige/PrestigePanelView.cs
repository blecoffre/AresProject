using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Core.UI.Prestige
{
    public class PrestigePanelView : MonoBehaviour
    {
        [Header("Ouverture / fermeture")]
        [Tooltip("La racine du panneau, celle qu'on active et désactive. Ce composant vit sur " +
                 "l'objet UI et non sur le panneau lui-même : il lui faut donc une référence.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("Bouton qui ouvre l'arbre, placé hors du panneau — dans le header.")]
        [SerializeField] private Button _openButton;

        [Tooltip("Bouton de fermeture, à l'intérieur du panneau.")]
        [SerializeField] private Button _closeButton;

        public event Action OnOpenClicked;
        public event Action OnCloseClicked;

        /// <summary>Le panneau est-il à l'écran. Sert au presenter pour basculer sur Échap.</summary>
        public bool IsVisible => _panel != null && _panel.activeSelf;

        [Header("Prefabs")]
        [SerializeField] private PrestigeItemView _nodePrefab;
        [SerializeField] private UILineConnection _linePrefab; // Nouveau : Le prefab de la ligne

        [Header("Containers")]
        [Tooltip("Conteneur pour les lignes (à placer en premier dans la hiérarchie pour être en arrière-plan)")]
        [SerializeField] private RectTransform _lineContainer;

        [Tooltip("Conteneur pour les nœuds (à placer en dernier pour être au premier plan et cliquables)")]
        [SerializeField] private RectTransform _nodeContainer;

        [Header("Grille")]
        [Tooltip("Taille d'une case de grille, en pixels. Les coordonnées posX/posY des JSON de " +
                 "prestige sont exprimées en cases : c'est ici qu'on décide de l'écartement réel.")]
        [SerializeField] private Vector2 _gridCellSize = new Vector2(600f, 300f);

        private IObjectResolver _resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private void Awake()
        {
            // L'arbre se CONSTRUIT au démarrage mais reste caché : les 116 nœuds et leurs liens
            // sont instanciés une fois pour toutes, l'ouverture n'est plus qu'un SetActive.
            //
            // Attention : Awake() ne s'exécute pas sur un objet inactif dans la hiérarchie. Ce
            // composant vit donc sur l'objet UI, actif, et pas sur le panneau — sans quoi rien
            // ici ne tournerait jamais.
            if (_panel != null) _panel.SetActive(false);

            if (_openButton != null) _openButton.onClick.AddListener(RaiseOpen);
            if (_closeButton != null) _closeButton.onClick.AddListener(RaiseClose);
        }

        private void OnDestroy()
        {
            if (_openButton != null) _openButton.onClick.RemoveListener(RaiseOpen);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(RaiseClose);
        }

        // Méthodes nommées plutôt que des lambdas : un retrait de lambda ne désabonne rien.
        private void RaiseOpen() => OnOpenClicked?.Invoke();
        private void RaiseClose() => OnCloseClicked?.Invoke();

        public void SetVisible(bool isVisible)
        {
            if (_panel != null && _panel.activeSelf != isVisible) _panel.SetActive(isVisible);
        }

        /// <summary>
        /// Convertit une coordonnée de grille (celle des JSON) en pixels. Le presenter n'a pas à
        /// connaître l'échelle d'affichage — et les deux extrémités d'un lien passent forcément
        /// par ici, donc elles ne peuvent plus diverger de repère.
        /// </summary>
        public Vector2 GridToPixels(Vector2 gridPosition)
        {
            return new Vector2(
                gridPosition.x * _gridCellSize.x,
                gridPosition.y * _gridCellSize.y);
        }

        /// <summary>
        /// Instancie un nœud déjà positionné. Le placement est fait ici, avant que le presenter ne
        /// reçoive la vue : un nœud ne peut donc jamais rester à la position du prefab.
        /// </summary>
        public PrestigeItemView SpawnNode(Vector2 anchoredPosition)
        {
            PrestigeItemView instance = Instantiate(_nodePrefab, _nodeContainer);
            instance.SetNodePosition(anchoredPosition);
            return instance;
        }

        public UILineConnection SpawnLine()
        {
            return Instantiate(_linePrefab, _lineContainer);
        }
    }
}