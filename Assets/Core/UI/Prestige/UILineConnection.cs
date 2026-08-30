using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Prestige
{
    /// <summary>
    /// Un lien entre deux nœuds de l'arbre.
    ///
    /// Ses trois états se distinguent par le MOTIF et l'ÉPAISSEUR autant que par la couleur. Une
    /// ligne n'a pas de texte : c'était le seul endroit du jeu où une information ne passait que
    /// par la teinte, donc invisible pour un joueur daltonien — et pour quiconque sur un mauvais
    /// écran. Les couleurs restent, elles ne sont simplement plus seules à porter le sens.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UILineConnection : MonoBehaviour
    {
        /// <summary>Les trois situations qu'un lien peut décrire, dans l'ordre de progression.</summary>
        private enum LinkState
        {
            /// <summary>Le parent n'est pas acheté : la branche est fermée.</summary>
            Locked,

            /// <summary>Le parent est acquis, l'enfant reste à compléter.</summary>
            InProgress,

            /// <summary>L'enfant est au niveau maximum.</summary>
            Completed
        }

        [SerializeField] private Image _lineImage;

        [Header("Motifs")]
        [Tooltip("Trait plein, pour les liens ouverts. Affiché en mode Simple.")]
        [SerializeField] private Sprite _solidSprite;

        [Tooltip("Trait pointillé, pour les liens verrouillés. Affiché en mode Tiled, donc " +
                 "répété le long de la ligne quelle que soit sa longueur.")]
        [SerializeField] private Sprite _dashedSprite;

        [Header("États Visuels")]
        [SerializeField] private Color _lockedColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color _unlockedColor = Color.cyan;
        [SerializeField] private Color _completedColor = Color.green;

        [Header("Épaisseurs")]
        [Tooltip("Les trois valeurs doivent rester nettement différentes : c'est le second canal " +
                 "qui remplace la couleur pour qui ne la distingue pas.")]
        [SerializeField] private float _lockedThickness = 2f;
        [SerializeField] private float _unlockedThickness = 4f;
        [SerializeField] private float _completedThickness = 7f;

        private RectTransform _rectTransform;
        private CompositeDisposable _disposables = new();

        /// <summary>
        /// Extrémités mémorisées. L'épaisseur dépend désormais de l'état, et l'état arrive APRÈS
        /// le tracé : sans ces deux champs, il faudrait redemander la géométrie à l'appelant à
        /// chaque changement d'état.
        /// </summary>
        private Vector2 _start;
        private Vector2 _end;

        private LinkState _state = LinkState.Locked;

        /// <summary>
        /// Résolution paresseuse, et surtout PAS dans Awake().
        ///
        /// L'arbre de prestige est construit alors que son panneau est encore désactivé : Unity
        /// n'appelle jamais Awake() sur un objet inactif dans la hiérarchie, donc le champ restait
        /// null et DrawLine levait une NullReferenceException dès le premier lien — ce qui
        /// interrompait la construction de tout l'arbre.
        /// Le GetComponent n'a lieu qu'une fois, à la construction, puis la référence est en cache.
        /// </summary>
        private RectTransform Rect
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                    _rectTransform.pivot = new Vector2(0.5f, 0.5f);
                }

                return _rectTransform;
            }
        }

        /// <summary>
        /// Place la ligne entre deux points. L'épaisseur n'est plus un paramètre : elle appartient
        /// à l'état, que <see cref="BindState"/> fournira juste après.
        /// </summary>
        public void DrawLine(Vector2 startPos, Vector2 endPos)
        {
            _start = startPos;
            _end = endPos;

            ApplyGeometry();
        }

        public void BindState(Observable<int> parentLevel, Observable<int> childLevel, int childMaxLevel)
        {
            _disposables.Clear(); // Nettoyage en cas de réutilisation (Pooling)

            Observable.CombineLatest(parentLevel, childLevel, (parent, child) =>
            {
                if (parent == 0) return LinkState.Locked;                  // Le parent n'est pas acheté
                if (child >= childMaxLevel) return LinkState.Completed;    // L'enfant est au max
                return LinkState.InProgress;                               // En cours de progression
            })
            .Subscribe(Render)
            .AddTo(_disposables);
        }

        /// <summary>
        /// Peint l'état : motif, couleur, épaisseur. La géométrie est refaite parce que
        /// l'épaisseur en fait partie — c'est la hauteur du rectangle.
        /// </summary>
        private void Render(LinkState state)
        {
            _state = state;

            bool isLocked = state == LinkState.Locked;

            // Tiled et non Sliced pour le pointillé : le motif doit se RÉPÉTER le long de la
            // ligne, pas s'étirer. Simple pour le trait plein, dont l'étirement est justement ce
            // qu'on veut — et qui ne coûte qu'un quad, là où le pointillé en produit un par tiret.
            _lineImage.sprite = isLocked ? _dashedSprite : _solidSprite;
            _lineImage.type = isLocked ? Image.Type.Tiled : Image.Type.Simple;
            _lineImage.color = ResolveColor(state);

            ApplyGeometry();
        }

        private void ApplyGeometry()
        {
            RectTransform rect = Rect;

            Vector2 direction = _end - _start;

            rect.sizeDelta = new Vector2(direction.magnitude, ResolveThickness(_state));
            rect.anchoredPosition = _start + (direction / 2f);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private Color ResolveColor(LinkState state)
        {
            switch (state)
            {
                case LinkState.Locked: return _lockedColor;
                case LinkState.Completed: return _completedColor;
                default: return _unlockedColor;
            }
        }

        private float ResolveThickness(LinkState state)
        {
            switch (state)
            {
                case LinkState.Locked: return _lockedThickness;
                case LinkState.Completed: return _completedThickness;
                default: return _unlockedThickness;
            }
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
