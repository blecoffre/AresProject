using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    /// <summary>
    /// Le bouton du « Data Wiper », c'est-à-dire le Bouton d'Urgence.
    ///
    /// Vue autonome, sur le modèle de l'ExfiltrationView et de la GhostCacheView : elle peut être
    /// posée n'importe où dans l'écran sans toucher au code. Aucune logique, que des références
    /// sérialisées et un événement brut.
    /// </summary>
    public class EmergencyView : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Button _button;

        [Tooltip("Libellé du bouton. Porte les quatre états : verrouillé, puissance insuffisante, " +
                 "en recharge, et prêt.")]
        [SerializeField] private TextMeshProUGUI _label;

        [Header("Jauge")]
        [Tooltip("Image en Filled. Montre l'avancement du délai de recharge, puis reste pleine " +
                 "une fois le bouton disponible.")]
        [SerializeField] private Image _fill;

        [Header("Couleurs")]
        [Tooltip("Jauge quand le bouton est indisponible : verrouillé, en recharge, ou puissance " +
                 "de calcul insuffisante.")]
        [SerializeField] private Color _unavailableColor = new Color(0.35f, 0.35f, 0.40f, 1f);

        [Tooltip("Jauge quand la purge est déclenchable.")]
        [SerializeField] private Color _readyColor = new Color(0.95f, 0.75f, 0.20f, 1f);

        [Tooltip("Jauge tant que les TFlops restent immobilisées par le contrecoup.")]
        [SerializeField] private Color _blockedColor = new Color(0.85f, 0.35f, 0.55f, 1f);

        public event Action OnEmergencyClicked;

        public Color UnavailableColor => _unavailableColor;
        public Color ReadyColor => _readyColor;
        public Color BlockedColor => _blockedColor;

        private void Awake()
        {
            _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(HandleClicked);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void HandleClicked() => OnEmergencyClicked?.Invoke();

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
            // canvas, et cette méthode est appelée à chaque seconde de recharge.
            if (_fill.color != fillColor)
            {
                _fill.color = fillColor;
            }
        }
    }
}
