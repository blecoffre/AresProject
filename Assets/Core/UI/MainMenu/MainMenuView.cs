using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.MainMenu
{
    /// <summary>
    /// L'écran titre. Vue passive : elle relaie les clics et affiche ce qu'on lui donne.
    ///
    /// Les libellés sont écrits par le presenter et non par des <c>LocalizedText</c> : ceux-ci
    /// dépendent d'une liste d'injection tenue à la main sur le scope, et un oubli y affiche la
    /// clé brute sans le moindre avertissement.
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        [Header("Textes")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _subtitleText;

        [Header("Boutons")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private TextMeshProUGUI _continueLabel;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private TextMeshProUGUI _optionsLabel;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TextMeshProUGUI _quitLabel;

        public event Action OnContinueClicked;
        public event Action OnOptionsClicked;
        public event Action OnQuitClicked;

        private void Awake()
        {
            _continueButton.onClick.AddListener(RaiseContinue);
            _optionsButton.onClick.AddListener(RaiseOptions);
            _quitButton.onClick.AddListener(RaiseQuit);
        }

        private void OnDestroy()
        {
            _continueButton.onClick.RemoveListener(RaiseContinue);
            _optionsButton.onClick.RemoveListener(RaiseOptions);
            _quitButton.onClick.RemoveListener(RaiseQuit);
        }

        // Méthodes nommées plutôt que des lambdas : un retrait de lambda ne désabonne rien.
        private void RaiseContinue() => OnContinueClicked?.Invoke();
        private void RaiseOptions() => OnOptionsClicked?.Invoke();
        private void RaiseQuit() => OnQuitClicked?.Invoke();

        public void SetTexts(string title, string subtitle, string continueLabel, string optionsLabel, string quitLabel)
        {
            _titleText.text = title;
            _subtitleText.text = subtitle;
            _continueLabel.text = continueLabel;
            _optionsLabel.text = optionsLabel;
            _quitLabel.text = quitLabel;
        }

        /// <summary>
        /// Verrouille ou rouvre tous les boutons. Sert pendant une transition : un second clic sur
        /// « Continuer » lancerait un second chargement de la GameScene par-dessus le premier.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _continueButton.interactable = interactable;
            _optionsButton.interactable = interactable;
            _quitButton.interactable = interactable;
        }
    }
}
