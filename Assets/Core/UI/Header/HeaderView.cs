using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Header
{
    public class HeaderView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private TextMeshProUGUI _computerPowerText;
        [SerializeField] private TextMeshProUGUI _datasYieldText;
        [SerializeField] private TextMeshProUGUI _cpuCyclesText;

        [Header("Threat Gauge")]
        [SerializeField] private TextMeshProUGUI _traceText;
        [SerializeField] private Image _traceGaugeFill; // La nouvelle jauge visuelle
        [SerializeField] private Gradient _gaugeColorGradient; // Pour passer du vert au rouge

        [Header("Layout Reference")]
        [SerializeField] private RectTransform _headerContainer; // Le RectTransform parent qui contient le Layout Group

        private bool _isInitialized = false;

        public void UpdateMoneyDisplay(string formattedValue)
        {
            if (_moneyText != null) _moneyText.text = $": {formattedValue}";
            RefreshLayoutIfNeeded();
        }

        public void UpdateComputerPowerDisplay(string formattedValue)
        {
            if (_computerPowerText != null) _computerPowerText.text = $": {formattedValue} TFlops";
            RefreshLayoutIfNeeded();
        }

        public void UpdateMoneyYieldDisplay(string formattedYield)
        {
            if (_datasYieldText != null) _datasYieldText.text = $"+{formattedYield}/s";
            RefreshLayoutIfNeeded();
        }

        public void UpdateCpuCyclesDisplay(string formattedValue)
        {
            if (_cpuCyclesText != null) _cpuCyclesText.text = $": {formattedValue}";
            RefreshLayoutIfNeeded();
        }

        public void UpdateTraceDisplay(float normalizedValue)
        {
            if (_traceText != null)
            {
                _traceText.SetText(": {0:F1}%", normalizedValue * 100f);
            }

            if (_traceGaugeFill != null)
            {
                _traceGaugeFill.fillAmount = normalizedValue;

                // Bonus visuel : la couleur change dynamiquement selon le remplissage
                if (_gaugeColorGradient != null)
                {
                    _traceGaugeFill.color = _gaugeColorGradient.Evaluate(normalizedValue);
                }
            }

            RefreshLayoutIfNeeded();
        }

        /// <summary>
        /// Recalcule la géométrie du Header à la première mise à jour pour éviter la superposition.
        /// </summary>
        private void RefreshLayoutIfNeeded()
        {
            // On ne force le rebuild qu'une seule fois au démarrage pour préserver le CPU
            if (!_isInitialized)
            {
                _isInitialized = true;

                if (_headerContainer != null)
                {
                    // Force uGUI à calculer les largeurs de texte TMP et réagencer les éléments
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_headerContainer);
                }
            }
        }
    }
}