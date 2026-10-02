using Core.Services.Localization;
using R3;
using System;
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
        private IDisposable _languageSubscription;

        // Injection automatique par VContainer même sur un MonoBehaviour dans la scène
        [Inject]
        public void Construct(ILocalizationService localizationService)
        {
            _localizationService = localizationService;

            // Suit la langue à chaud. L'abonnement émet tout de suite la langue courante : il
            // tient donc aussi lieu de premier rendu.
            _languageSubscription?.Dispose();
            _languageSubscription = _localizationService.CurrentLanguage.Subscribe(_ => UpdateLocalizedText());
        }

        private void Awake()
        {
            _textComponent = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            // Si le service a été injecté avant l'Awake, le premier rendu n'a rien trouvé à écrire.
            if (_localizationService != null)
            {
                UpdateLocalizedText();
            }
        }

        private void OnDestroy()
        {
            _languageSubscription?.Dispose();
        }

        public void UpdateLocalizedText()
        {
            if (_textComponent == null) _textComponent = GetComponent<TextMeshProUGUI>();

            if (_textComponent != null && _localizationService != null && !string.IsNullOrEmpty(_localizationKey))
            {
                _textComponent.text = _localizationService.GetText(_localizationKey);
            }
        }
    }
}
