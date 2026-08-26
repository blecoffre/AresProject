using UnityEngine;
using VContainer;

namespace Core.UI.Prestige
{
    public class PrestigePanelView : MonoBehaviour
    {
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