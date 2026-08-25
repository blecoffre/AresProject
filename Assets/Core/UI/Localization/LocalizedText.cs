using Core.Services.Localization;
using TMPro;
using UnityEngine;
using VContainer;

namespace Core.UI.Localization
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _localizationKey;

        private TextMeshProUGUI _textComponent;
        private ILocalizationService _localizationService;

        // Injection automatique par VContainer même sur un MonoBehaviour dans la scène
        [Inject]
        public void Construct(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
            UpdateLocalizedText();
        }

        private void Awake()
        {
            _textComponent = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            // Si le service a déjà été injecté avant Start (cas standard)
            if (_localizationService != null)
            {
                UpdateLocalizedText();
            }
        }

        public void UpdateLocalizedText()
        {
            if (_textComponent != null && _localizationService != null && !string.IsNullOrEmpty(_localizationKey))
            {
                _textComponent.text = _localizationService.GetText(_localizationKey);
            }
        }
    }
}