using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    /// <summary>
    /// Le bouton du Ghost Cache et son compte à rebours de Zéro-Day Exploit.
    ///
    /// Vue autonome, sur le modèle de l'ExfiltrationView : elle peut être posée n'importe où
    /// dans l'écran sans toucher au code. Comme toutes les vues du projet, elle ne porte aucune
    /// logique — que des références sérialisées et un événement brut.
    /// </summary>
    public class GhostCacheView : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Button _button;

        [Tooltip("Libellé du bouton. Porte les trois états : en charge, prêt, et Exploit en cours.")]
        [SerializeField] private TextMeshProUGUI _label;

        [Header("Jauge")]
        [Tooltip("Image en Filled. Montre la charge accumulée hors Exploit, puis le temps " +
                 "restant pendant l'Exploit — d'où la couleur distincte ci-dessous.")]
        [SerializeField] private Image _fill;

        [Header("Couleurs")]
        [Tooltip("Jauge pendant l'accumulation de la charge.")]
        [SerializeField] private Color _chargingColor = new Color(0.25f, 0.65f, 0.45f, 1f);

        [Tooltip("Jauge une fois la charge pleine et le bouton actionnable.")]
        [SerializeField] private Color _readyColor = new Color(0.35f, 0.85f, 0.95f, 1f);

        [Tooltip("Jauge pendant l'Exploit : elle se VIDE, et le joueur est à découvert.")]
        [SerializeField] private Color _overdriveColor = new Color(0.95f, 0.3f, 0.2f, 1f);

        public event Action OnOverdriveClicked;

        public Color ChargingColor => _chargingColor;
        public Color ReadyColor => _readyColor;
        public Color OverdriveColor => _overdriveColor;

        private void Awake()
        {
            _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(HandleClicked);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void HandleClicked() => OnOverdriveClicked?.Invoke();

        /// <summary>
        /// Applique un état complet. Un seul point d'entrée plutôt que quatre setters : le
        /// libellé, l'interactivité, le remplissage et la couleur changent toujours ensemble.
        /// </summary>
        public void ApplyState(string label, bool interactable, float fillAmount, Color fillColor)
        {
            if (_label != null) _label.text = label;

            _button.interactable = interactable;

            if (_fill == null) return;

            _fill.fillAmount = fillAmount;

            // Comparaison avant écriture : assigner une couleur identique salit quand même le
            // canvas, et cette méthode est appelée à chaque pourcent de charge.
            if (_fill.color != fillColor)
            {
                _fill.color = fillColor;
            }
        }
    }
}
