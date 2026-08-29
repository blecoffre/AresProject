using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Prestige
{
    /// <summary>
    /// Un nœud de l'arbre de compilation. Ne décide de rien : elle reçoit un état et deux
    /// chaînes déjà localisées, et peint.
    /// </summary>
    public class PrestigeItemView : MonoBehaviour
    {
        [Header("Container")]
        [SerializeField] private RectTransform _rectTransform;

        [Header("Textes")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelText;

        [Tooltip("Affiche le coût, ou la mention « verrouillé » quand la branche n'est pas ouverte.")]
        [SerializeField] private TextMeshProUGUI _costText;

        [Header("Habillage")]
        [Tooltip("Le cadre du nœud (Image creuse, Fill Center décoché). Il porte la couleur d'état.")]
        [SerializeField] private Image _borderImage;

        [Tooltip("Le fond plein. Discret la plupart du temps, OPAQUE au niveau max : c'est lui " +
                 "qui produit les couleurs inversées du nœud entièrement compilé.")]
        [SerializeField] private Image _fillImage;

        [Tooltip("Le voile de sélection — l'Image du bouton, transparente au repos.")]
        [SerializeField] private Image _selectionOverlay;

        [Header("Interaction")]
        [Tooltip("Sélectionne le nœud. Toujours cliquable, même verrouillé : lire pourquoi une " +
                 "branche est fermée fait partie de la planification.")]
        [SerializeField] private Button _interactionButton;

        [Header("Couleurs d'état")]
        [SerializeField] private Color _lockedColor = new Color(0.200f, 0.333f, 0.200f, 1f);
        [SerializeField] private Color _tooExpensiveColor = new Color(1f, 0.200f, 0.200f, 1f);
        [SerializeField] private Color _affordableColor = new Color(1f, 0.600f, 0f, 1f);
        [SerializeField] private Color _inProgressColor = new Color(0.290f, 0.965f, 0.149f, 1f);
        [SerializeField] private Color _maxedColor = new Color(0.290f, 0.965f, 0.149f, 1f);

        [Tooltip("Couleur des textes quand le nœud est au maximum : le fond devient plein, il " +
                 "faut écrire dessus en négatif.")]
        [SerializeField] private Color _maxedTextColor = Color.black;

        [Header("Réglages")]
        [Tooltip("Opacité du fond pour les états qui gardent un fond teinté (en cours, trop cher).")]
        [SerializeField, Range(0f, 1f)] private float _subtleFillAlpha = 0.06f;

        [Tooltip("Opacité du voile de sélection, par-dessus la couleur d'état du nœud.")]
        [SerializeField, Range(0f, 1f)] private float _selectionAlpha = 0.18f;

        [Tooltip("Vitesse de la pulsation ambre, en radians par seconde.")]
        [SerializeField] private float _pulseSpeed = 3f;

        [Tooltip("Opacité minimale atteinte au creux de la pulsation.")]
        [SerializeField, Range(0f, 1f)] private float _pulseMinAlpha = 0.35f;

        public event Action OnNodeClicked;

        /// <summary>Couleur de l'état courant. Sert de base à la pulsation et au voile de sélection.</summary>
        private Color _stateColor;

        private float _stateFillAlpha;
        private bool _isSelected;

        /// <summary>
        /// Résolution paresseuse, et surtout PAS dans Awake().
        ///
        /// L'arbre est construit alors que son écran est encore désactivé, et Unity n'appelle pas
        /// Awake() sur un objet inactif : un repli placé là ne se serait jamais exécuté, et
        /// SetNodePosition serait resté un no-op silencieux laissant les 116 nœuds empilés à
        /// l'origine. Le GetComponent n'a lieu qu'une fois, puis la référence est en cache.
        /// </summary>
        private RectTransform Rect
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }

                return _rectTransform;
            }
        }

        private void Awake()
        {
            _interactionButton.onClick.AddListener(HandleInteraction);
        }

        private void OnDestroy()
        {
            _interactionButton.onClick.RemoveListener(HandleInteraction);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void HandleInteraction() => OnNodeClicked?.Invoke();

        /// <summary>
        /// La pulsation ambre. Ce composant se DÉSACTIVE lui-même dès qu'un nœud n'a plus à
        /// pulser (voir <see cref="Render"/>) : sur 116 nœuds, laisser 116 Update() tourner pour
        /// que 3 d'entre eux clignotent serait payer le pire des deux mondes.
        ///
        /// `unscaledTime` et non `time` : toutes les branches ouvertes battent alors en phase,
        /// ce qui donne un balayage de terminal plutôt qu'un sapin de Noël.
        /// </summary>
        private void Update()
        {
            float wave = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(_pulseMinAlpha, 1f, wave);

            Color border = _stateColor;
            border.a = alpha;
            _borderImage.color = border;
        }

        public void InitializeStaticData(string name)
        {
            if (_nameText != null) _nameText.text = name;
        }

        /// <summary>
        /// Peint le nœud. Les deux chaînes arrivent déjà localisées et déjà formatées, cas
        /// « niveau max » et « verrouillé » compris : aucun texte affichable ne vit ici.
        /// </summary>
        /// <param name="pulse">
        /// La pulsation est un appel à l'action, pas un code couleur. Elle est donc coupée
        /// pendant une run, où la couleur ambre ne dit plus que « tu pourras te l'offrir ».
        /// </param>
        public void Render(string levelText, string costText, PrestigeNodeState state, bool pulse)
        {
            if (_levelText != null) _levelText.text = levelText;
            if (_costText != null) _costText.text = costText;

            _stateColor = ResolveStateColor(state);
            _stateFillAlpha = ResolveFillAlpha(state);

            Color fill = _stateColor;
            fill.a = _stateFillAlpha;
            _fillImage.color = fill;

            Color textColor = state == PrestigeNodeState.Maxed ? _maxedTextColor : _stateColor;
            if (_nameText != null) _nameText.color = textColor;
            if (_levelText != null) _levelText.color = textColor;
            if (_costText != null) _costText.color = textColor;

            // Repeindre le cadre AVANT d'armer la pulsation : sinon un nœud qui vient de cesser
            // de pulser resterait figé sur l'opacité du dernier creux de sinusoïde.
            _borderImage.color = _stateColor;
            enabled = pulse && state == PrestigeNodeState.Affordable;

            ApplySelectionTint();
        }

        /// <summary>
        /// Le nœud consulté dans l'inspecteur. Le voile réutilise l'Image du bouton, déjà
        /// présente et transparente : rien de neuf à instancier sur 116 nœuds.
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            ApplySelectionTint();
        }

        private void ApplySelectionTint()
        {
            if (_selectionOverlay == null) return;

            Color tint = _stateColor;
            tint.a = _isSelected ? _selectionAlpha : 0f;
            _selectionOverlay.color = tint;
        }

        private Color ResolveStateColor(PrestigeNodeState state)
        {
            switch (state)
            {
                case PrestigeNodeState.Locked: return _lockedColor;
                case PrestigeNodeState.TooExpensive: return _tooExpensiveColor;
                case PrestigeNodeState.Affordable: return _affordableColor;
                case PrestigeNodeState.InProgress: return _inProgressColor;
                default: return _maxedColor;
            }
        }

        /// <summary>
        /// Verrouillé et débloquable gardent un fond VIDE : le premier doit s'effacer du regard,
        /// le second tire déjà l'œil par sa pulsation et n'a pas besoin d'en rajouter.
        /// </summary>
        private float ResolveFillAlpha(PrestigeNodeState state)
        {
            switch (state)
            {
                case PrestigeNodeState.Locked:
                case PrestigeNodeState.Affordable:
                    return 0f;

                case PrestigeNodeState.Maxed:
                    return 1f;

                default:
                    return _subtleFillAlpha;
            }
        }

        // Méthode clé pour la Toile d'Araignée !
        public void SetNodePosition(Vector2 localPosition)
        {
            Rect.anchoredPosition = localPosition;
        }
    }
}
