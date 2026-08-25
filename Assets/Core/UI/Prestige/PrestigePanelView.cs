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

        private IObjectResolver _resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public PrestigeItemView SpawnNode()
        {
            var instance = Instantiate(_nodePrefab, _nodeContainer);
            return instance;
        }

        // NOUVEAU : La méthode manquante pour instancier la ligne
        public UILineConnection SpawnLine()
        {
            return Instantiate(_linePrefab, _lineContainer);
        }
    }
}