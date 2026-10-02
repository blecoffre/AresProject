using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Options
{
    /// <summary>
    /// Une ligne de réglage : « Libellé ....... &lt; Valeur &gt; ».
    ///
    /// Des flèches plutôt que des menus déroulants : un sélecteur se pilote à la manette d'un
    /// geste gauche/droite (cible Steam Deck), là où un menu déroulant exige d'ouvrir, de
    /// naviguer puis de valider. C'est aussi plus fidèle à l'esthétique terminal.
    ///
    /// Vue passive : elle ne sait rien de ce qu'elle règle.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OptionSelectorView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _value;
        [SerializeField] private Button _previousButton;
        [SerializeField] private TextMeshProUGUI _previousGlyph;
        [SerializeField] private Button _nextButton;
        [SerializeField] private TextMeshProUGUI _nextGlyph;

        [Tooltip("Opacité de la ligne quand le réglage est sans effet (ex. : plafond de FPS sous V-Sync).")]
        [SerializeField, Range(0f, 1f)] private float _disabledAlpha = 0.35f;

        private CanvasGroup _canvasGroup;

        public event Action OnPrevious;
        public event Action OnNext;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _previousButton.onClick.AddListener(RaisePrevious);
            _nextButton.onClick.AddListener(RaiseNext);
        }

        private void OnDestroy()
        {
            _previousButton.onClick.RemoveListener(RaisePrevious);
            _nextButton.onClick.RemoveListener(RaiseNext);
        }

        // Méthodes nommées plutôt que des lambdas : un retrait de lambda ne désabonne rien.
        private void RaisePrevious() => OnPrevious?.Invoke();
        private void RaiseNext() => OnNext?.Invoke();

        public void SetLabel(string label) => _label.text = label;

        public void SetGlyphs(string previous, string next)
        {
            _previousGlyph.text = previous;
            _nextGlyph.text = next;
        }

        public void SetValue(string value) => _value.text = value;

        /// <summary>
        /// Grise une flèche en bout de liste plutôt que de boucler : sur une échelle ordonnée
        /// (résolution, FPS, taille), passer de la plus grande à la plus petite d'un clic
        /// surprendrait — surtout pour la résolution.
        /// </summary>
        public void SetArrows(bool canGoPrevious, bool canGoNext)
        {
            _previousButton.interactable = canGoPrevious;
            _nextButton.interactable = canGoNext;
        }

        /// <summary>Estompe toute la ligne quand le réglage n'a pas d'effet dans l'état courant.</summary>
        public void SetRowEnabled(bool enabled)
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = enabled ? 1f : _disabledAlpha;
            _canvasGroup.interactable = enabled;
        }
    }
}
