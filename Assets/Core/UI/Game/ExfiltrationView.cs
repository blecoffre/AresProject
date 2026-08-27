using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    /// <summary>
    /// Le bouton du « Protocole Terre Brûlée ».
    ///
    /// Vue autonome et non un champ de plus sur le HeaderView : le bouton pourra être déplacé
    /// ailleurs dans l'écran sans toucher une ligne de code.
    ///
    /// Comme toutes les vues du projet : aucune logique, que des références sérialisées et un
    /// événement brut.
    /// </summary>
    public class ExfiltrationView : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Button _button;

        [Tooltip("Libellé du bouton. Porte les trois états : verrouillé, prêt, et gain estimé.")]
        [SerializeField] private TextMeshProUGUI _label;

        [Header("Jauge du 1er Cycle")]
        [Tooltip("Image en Filled : progression vers les 1 000 Datas du premier CPU Cycle. " +
                 "Masquée une fois le bouton débloqué.")]
        [SerializeField] private Image _progressFill;

        [Header("Séquence de purge")]
        [Tooltip("Facultatif : panneau plein écran qui intercepte les clics pendant la séquence. " +
                 "Sans lui la séquence se déroule quand même — les systèmes sont déjà gardés par " +
                 "IsGameActive — mais le joueur peut cliquer dans le vide.")]
        [SerializeField] private GameObject _interactionBlocker;

        [Tooltip("Délai entre deux lignes de la console. Six lignes à 0,33 s font les ~2 s du GDD.")]
        [SerializeField, Range(0.05f, 1f)] private float _lineDelaySeconds = 0.33f;

        public event Action OnExfiltrateClicked;

        /// <summary>Rythme de la séquence, réglable dans l'inspecteur sans recompiler.</summary>
        public float LineDelaySeconds => _lineDelaySeconds;

        private void Awake()
        {
            _button.onClick.AddListener(HandleClicked);
            SetInteractionBlocked(false);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(HandleClicked);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void HandleClicked() => OnExfiltrateClicked?.Invoke();

        /// <summary>
        /// Applique un état complet. Un seul point d'entrée plutôt que trois setters : le libellé,
        /// l'interactivité et la jauge changent toujours ensemble.
        /// </summary>
        public void ApplyState(string label, bool interactable, bool showProgress, float progress)
        {
            if (_label != null) _label.text = label;

            _button.interactable = interactable;

            if (_progressFill != null)
            {
                if (_progressFill.gameObject.activeSelf != showProgress)
                {
                    _progressFill.gameObject.SetActive(showProgress);
                }

                if (showProgress) _progressFill.fillAmount = progress;
            }
        }

        public void SetInteractionBlocked(bool isBlocked)
        {
            if (_interactionBlocker == null) return;

            if (_interactionBlocker.activeSelf != isBlocked)
            {
                _interactionBlocker.SetActive(isBlocked);
            }
        }
    }
}
