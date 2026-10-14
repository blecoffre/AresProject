using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    /// <summary>
    /// Le bouton du « Data Wiper », c'est-à-dire le Bouton d'Urgence.
    ///
    /// Vue autonome, sur le modèle de l'ExfiltrationView : elle peut être
    /// posée n'importe où dans l'écran sans toucher au code. Aucune logique, que des références
    /// sérialisées et un événement brut.
    /// </summary>
    public class EmergencyView : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private Button _button;

        [Tooltip("Libellé du bouton. Porte les trois états : verrouillé, prêt, épuisé.")]
        [SerializeField] private TextMeshProUGUI _label;

        [Header("Jauge")]
        [Tooltip("Image en Filled. Montre la part des usages encore disponibles sur la run ; " +
                 "pleine, à la couleur du sacrifice, une fois le bouton épuisé.")]
        [SerializeField] private Image _fill;

        // Les couleurs de la jauge viennent de la palette du mode de vision (EmergencyPresenter).

        public event Action OnEmergencyClicked;

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
