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

        [Tooltip("L'unique ligne d'information sous le nom : « VERROUILLÉ », un prix, un niveau " +
                 "ou « MAX ». Un seul emplacement, parce qu'un nœud n'a jamais qu'une seule " +
                 "chose à dire — le presenter choisit laquelle selon l'état.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Habillage")]
        [Tooltip("Le cadre du nœud (Image creuse, Fill Center décoché). Il porte la couleur d'état.")]
        [SerializeField] private Image _borderImage;

        [Tooltip("Le fond plein. TOUJOURS opaque : c'est lui qui masque les liens de l'arbre " +
                 "passant derrière le nœud. Il porte une teinte plus ou moins marquée selon " +
                 "l'état, jusqu'aux couleurs inversées du nœud entièrement compilé.")]
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
        [Tooltip("La couleur d'aplat du nœud au repos. À accorder au fond de l'arbre — c'est " +
                 "elle qui occulte les liens qui passeraient derrière.")]
        [SerializeField] private Color _nodeBackgroundColor = new Color(0.012f, 0.027f, 0.012f, 1f);

        [Tooltip("Dose de teinte d'état mêlée au fond pour les états qui en gardent une trace " +
                 "(en cours, trop cher). 0 = fond nu, 1 = couleur d'état pure.")]
        [SerializeField, Range(0f, 1f)] private float _subtleTintAmount = 0.12f;

        [Tooltip("Opacité du voile de sélection, par-dessus la couleur d'état du nœud.")]
        [SerializeField, Range(0f, 1f)] private float _selectionAlpha = 0.18f;

        [Header("Pulsation")]
        [Tooltip("Vitesse de la pulsation quand le nœud est achetable TOUT DE SUITE, en radians " +
                 "par seconde. C'est l'appel à l'action : franc et rapide.")]
        [SerializeField] private float _pulseSpeed = 3f;

        [Tooltip("Opacité minimale au creux de la pulsation d'appel à l'action.")]
        [SerializeField, Range(0f, 1f)] private float _pulseMinAlpha = 0.35f;

        [Tooltip("Vitesse de la pulsation pendant une run, où l'achat est impossible. Plus lente : " +
                 "elle signale où porter son attention sans réclamer un geste irréalisable.")]
        [SerializeField] private float _idlePulseSpeed = 1.2f;

        [Tooltip("Opacité minimale au creux de la pulsation de veille. Plus haute, donc " +
                 "respiration discrète plutôt que clignotement.")]
        [SerializeField, Range(0f, 1f)] private float _idlePulseMinAlpha = 0.7f;

        public event Action OnNodeClicked;

        /// <summary>Couleur de l'état courant. Sert de base à la pulsation et au voile de sélection.</summary>
        private Color _stateColor;

        private bool _isSelected;

        /// <summary>
        /// La pulsation est-elle en régime d'appel à l'action, par opposition à la veille. Le
        /// nœud respire dans les deux cas ; seule l'insistance change.
        /// </summary>
        private bool _isPulseUrgent;

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
        /// pulser (voir <see cref="Render"/>) : sur 134 nœuds, laisser 134 Update() tourner pour
        /// que 3 d'entre eux respirent serait payer le pire des deux mondes.
        ///
        /// `unscaledTime` et non `time` : toutes les branches ouvertes battent alors en phase,
        /// ce qui donne un balayage de terminal plutôt qu'un sapin de Noël.
        ///
        /// <b>C'est aussi le seul canal accessible à TOUS les daltonismes.</b> Le mouvement est
        /// perçu indépendamment de la teinte, et c'est ce qui permet de repérer un nœud
        /// actionnable parmi 134 sans lire un seul libellé.
        /// </summary>
        private void Update()
        {
            float speed = _isPulseUrgent ? _pulseSpeed : _idlePulseSpeed;
            float minAlpha = _isPulseUrgent ? _pulseMinAlpha : _idlePulseMinAlpha;

            float wave = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(minAlpha, 1f, wave);

            Color border = _stateColor;
            border.a = alpha;
            _borderImage.color = border;
        }

        public void InitializeStaticData(string name)
        {
            if (_nameText != null) _nameText.text = name;
        }

        /// <summary>
        /// Peint le nœud. La chaîne arrive déjà localisée et déjà formatée, cas « niveau max » et
        /// « verrouillé » compris : aucun texte affichable ne vit ici.
        /// </summary>
        /// <param name="canPurchaseNow">
        /// L'achat est-il possible à l'instant. Un nœud débloquable respire dans les DEUX cas —
        /// il faut bien pouvoir le repérer quand on consulte l'arbre pour planifier, ce qui se
        /// fait justement pendant une run — mais l'appel à l'action est plus insistant quand le
        /// geste est réalisable. La pulsation était coupée pendant une run jusqu'au 2026-08-30 :
        /// elle s'éteignait donc précisément au moment où le balayage sert le plus.
        /// </param>
        public void Render(string statusText, PrestigeNodeState state, bool canPurchaseNow)
        {
            if (_statusText != null) _statusText.text = statusText;

            _stateColor = ResolveStateColor(state);

            // Le fond reste OPAQUE quelle que soit l'intensité de la teinte : c'est lui qui
            // occulte les liens de l'arbre passant derrière le nœud. La teinte est donc mélangée
            // à la main par-dessus la couleur de fond, au lieu d'être laissée à l'alpha — un
            // aplat translucide laisserait forcément voir ce qu'il y a derrière.
            Color fill = Color.Lerp(_nodeBackgroundColor, _stateColor, ResolveTintAmount(state));
            fill.a = 1f;
            _fillImage.color = fill;

            Color textColor = state == PrestigeNodeState.Maxed ? _maxedTextColor : _stateColor;
            if (_nameText != null) _nameText.color = textColor;
            if (_statusText != null) _statusText.color = textColor;

            // Repeindre le cadre AVANT d'armer la pulsation : sinon un nœud qui vient de cesser
            // de pulser resterait figé sur l'opacité du dernier creux de sinusoïde.
            _borderImage.color = _stateColor;

            _isPulseUrgent = canPurchaseNow;
            enabled = state == PrestigeNodeState.Affordable;

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
        /// Verrouillé et débloquable gardent un fond NU : le premier doit s'effacer du regard, le
        /// second tire déjà l'œil par sa pulsation et n'a pas besoin d'en rajouter. Nu ne veut
        /// pas dire transparent — le fond est là, simplement sans teinte.
        /// </summary>
        private float ResolveTintAmount(PrestigeNodeState state)
        {
            switch (state)
            {
                case PrestigeNodeState.Locked:
                case PrestigeNodeState.Affordable:
                    return 0f;

                case PrestigeNodeState.Maxed:
                    return 1f;

                default:
                    return _subtleTintAmount;
            }
        }

        // Méthode clé pour la Toile d'Araignée !
        public void SetNodePosition(Vector2 localPosition)
        {
            Rect.anchoredPosition = localPosition;
        }
    }
}
