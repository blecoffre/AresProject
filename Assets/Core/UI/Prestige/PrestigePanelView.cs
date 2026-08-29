using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Prestige
{
    /// <summary>
    /// L'écran méta : l'arbre de compilation, son inspecteur, et — quand une run vient de se
    /// terminer — le bilan de fin de run par-dessus.
    ///
    /// <b>Un seul écran, deux modes.</b> Depuis la fusion des panneaux, l'arbre vit à l'intérieur
    /// de la racine de l'écran de fin ; il ne peut donc plus s'afficher seul en activant son
    /// propre GameObject, son parent étant éteint. C'est cette vue qui possède la racine et les
    /// deux groupes d'éléments — sinon deux composants se disputeraient les mêmes SetActive.
    /// </summary>
    public class PrestigePanelView : MonoBehaviour
    {
        [Header("Écran")]
        [Tooltip("La racine de l'écran méta, celle qu'on allume et qu'on éteint. Ce composant " +
                 "vit sur l'objet UI et non sur l'écran : il lui faut donc une référence.")]
        [SerializeField] private GameObject _screenRoot;

        [Tooltip("Ce qui n'existe QU'À la fin d'une run : titre, narration, bilan, bouton de " +
                 "relance. Masqué quand l'écran est ouvert en consultation pendant une run.")]
        [SerializeField] private GameObject[] _runEndOnly;

        [Tooltip("Ce qui n'existe QUE hors fin de run — le bouton de fermeture au premier chef : " +
                 "on ne referme pas un écran de fin de run, on relance.")]
        [SerializeField] private GameObject[] _consultationOnly;

        [Header("Ouverture / fermeture")]
        [Tooltip("Bouton qui ouvre l'arbre, placé hors de l'écran — dans le header.")]
        [SerializeField] private Button _openButton;

        [Tooltip("Bouton de fermeture, à l'intérieur de l'écran.")]
        [SerializeField] private Button _closeButton;

        [Header("Affichage")]
        [Tooltip("Le solde de CPU Cycles, en haut de l'écran. C'est le chiffre qu'on vient " +
                 "consulter quand on planifie une branche.")]
        [SerializeField] private TextMeshProUGUI _cyclesText;

        [Header("Inspecteur")]
        [SerializeField] private PrestigeDetailsView _detailsView;

        [Header("Prefabs")]
        [SerializeField] private PrestigeItemView _nodePrefab;
        [SerializeField] private UILineConnection _linePrefab;

        [Header("Containers")]
        [Tooltip("Conteneur pour les lignes (à placer en premier dans la hiérarchie pour être en arrière-plan)")]
        [SerializeField] private RectTransform _lineContainer;

        [Tooltip("Conteneur pour les nœuds (à placer en dernier pour être au premier plan et cliquables)")]
        [SerializeField] private RectTransform _nodeContainer;

        [Header("Grille")]
        [Tooltip("Taille d'une case de grille, en pixels. Les coordonnées posX/posY des JSON de " +
                 "prestige sont exprimées en cases : c'est ici qu'on décide de l'écartement réel.")]
        [SerializeField] private Vector2 _gridCellSize = new Vector2(600f, 300f);

        public event Action OnOpenClicked;
        public event Action OnCloseClicked;

        /// <summary>L'écran est-il affiché. Sert au presenter pour basculer sur Échap.</summary>
        public bool IsVisible => _screenRoot != null && _screenRoot.activeSelf;

        /// <summary>
        /// L'écran est-il affiché en mode fin de run. Échap ne doit pas pouvoir l'escamoter :
        /// la run est terminée, il n'y a rien derrière à quoi revenir.
        /// </summary>
        public bool IsRunEndMode { get; private set; }

        public PrestigeDetailsView Details => _detailsView;

        private void Awake()
        {
            // L'arbre se CONSTRUIT au démarrage mais reste caché : les 116 nœuds et leurs liens
            // sont instanciés une fois pour toutes, l'ouverture n'est plus qu'un SetActive.
            //
            // Attention : Awake() ne s'exécute pas sur un objet inactif dans la hiérarchie. Ce
            // composant vit donc sur l'objet UI, actif, et pas sur l'écran — sans quoi rien ici
            // ne tournerait jamais.
            if (_screenRoot != null) _screenRoot.SetActive(false);

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

        /// <summary>Consultation pendant une run : l'arbre seul, sans rien du bilan.</summary>
        public void ShowConsultation()
        {
            IsRunEndMode = false;
            SetGroupActive(_runEndOnly, false);
            SetGroupActive(_consultationOnly, true);

            if (_screenRoot != null) _screenRoot.SetActive(true);
        }

        /// <summary>Fin de run : le bilan ET l'arbre, puisque c'est le moment de dépenser.</summary>
        public void ShowRunEnd()
        {
            IsRunEndMode = true;
            SetGroupActive(_runEndOnly, true);
            SetGroupActive(_consultationOnly, false);

            if (_screenRoot != null) _screenRoot.SetActive(true);
        }

        public void Hide()
        {
            IsRunEndMode = false;
            if (_screenRoot != null) _screenRoot.SetActive(false);
        }

        public void SetCyclesText(string text)
        {
            if (_cyclesText != null) _cyclesText.text = text;
        }

        private static void SetGroupActive(GameObject[] group, bool isActive)
        {
            if (group == null) return;

            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != null) group[i].SetActive(isActive);
            }
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
