using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Pause
{
    /// <summary>
    /// Le menu de pause en jeu. Vue passive ; comme le panneau d'options, le composant vit sur une
    /// racine qui reste active et seul <c>_panelRoot</c> s'allume.
    /// </summary>
    public class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Boutons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private TextMeshProUGUI _resumeLabel;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private TextMeshProUGUI _optionsLabel;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private TextMeshProUGUI _mainMenuLabel;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TextMeshProUGUI _quitLabel;

        public event Action OnResumeClicked;
        public event Action OnOptionsClicked;
        public event Action OnMainMenuClicked;
        public event Action OnQuitClicked;

        public bool IsVisible => _panelRoot.activeSelf;

        private void Awake()
        {
            _resumeButton.onClick.AddListener(RaiseResume);
            _optionsButton.onClick.AddListener(RaiseOptions);
            _mainMenuButton.onClick.AddListener(RaiseMainMenu);
            _quitButton.onClick.AddListener(RaiseQuit);
        }

        private void OnDestroy()
        {
            _resumeButton.onClick.RemoveListener(RaiseResume);
            _optionsButton.onClick.RemoveListener(RaiseOptions);
            _mainMenuButton.onClick.RemoveListener(RaiseMainMenu);
            _quitButton.onClick.RemoveListener(RaiseQuit);
        }

        // Méthodes nommées plutôt que des lambdas : un retrait de lambda ne désabonne rien.
        private void RaiseResume() => OnResumeClicked?.Invoke();
        private void RaiseOptions() => OnOptionsClicked?.Invoke();
        private void RaiseMainMenu() => OnMainMenuClicked?.Invoke();
        private void RaiseQuit() => OnQuitClicked?.Invoke();

        public void SetTexts(string title, string resume, string options, string mainMenu, string quit)
        {
            _titleText.text = title;
            _resumeLabel.text = resume;
            _optionsLabel.text = options;
            _mainMenuLabel.text = mainMenu;
            _quitLabel.text = quit;
        }

        /// <summary>Verrouille les boutons pendant une sortie, contre un double clic.</summary>
        public void SetInteractable(bool interactable)
        {
            _resumeButton.interactable = interactable;
            _optionsButton.interactable = interactable;
            _mainMenuButton.interactable = interactable;
            _quitButton.interactable = interactable;
        }

        public void Show() => _panelRoot.SetActive(true);

        public void Hide() => _panelRoot.SetActive(false);
    }
}
