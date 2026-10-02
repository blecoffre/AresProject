using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Options
{
    /// <summary>
    /// L'écran de choix de langue du premier lancement : un bouton par langue disponible, puis
    /// « Confirmer ». Les boutons sont instanciés depuis le prefab de bouton du terminal, au
    /// nombre de langues trouvées — ajouter un fichier de langue ajoute un bouton, sans toucher
    /// à la scène.
    ///
    /// Même principe que le panneau d'options : le composant vit sur une racine qui reste
    /// active, seul <c>_panelRoot</c> s'allume et s'éteint.
    /// </summary>
    public class LanguagePickerView : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _hintText;

        [Header("Langues")]
        [Tooltip("Conteneur à VerticalLayoutGroup qui reçoit un bouton par langue.")]
        [SerializeField] private Transform _languageContainer;
        [SerializeField] private Button _languageButtonPrefab;

        [Header("Confirmation")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmLabel;

        private readonly List<Button> _languageButtons = new();
        private readonly List<TextMeshProUGUI> _languageLabels = new();

        public event Action<int> OnLanguageClicked;
        public event Action OnConfirmClicked;

        public bool IsVisible => _panelRoot.activeSelf;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(RaiseConfirm);
        }

        private void OnDestroy()
        {
            _confirmButton.onClick.RemoveListener(RaiseConfirm);

            for (int i = 0; i < _languageButtons.Count; i++)
            {
                _languageButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void RaiseConfirm() => OnConfirmClicked?.Invoke();

        /// <summary>
        /// Crée un bouton par langue. Appelé une seule fois, à l'ouverture : la closure par bouton
        /// n'est allouée qu'à ce moment-là, jamais pendant le jeu.
        /// </summary>
        public void BuildLanguageButtons(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Button button = Instantiate(_languageButtonPrefab, _languageContainer);
                int index = i;
                button.onClick.AddListener(() => OnLanguageClicked?.Invoke(index));

                _languageButtons.Add(button);
                _languageLabels.Add(button.GetComponentInChildren<TextMeshProUGUI>(true));
            }
        }

        public void SetTexts(string title, string hint, string confirm)
        {
            _titleText.text = title;
            _hintText.text = hint;
            _confirmLabel.text = confirm;
        }

        public void SetLanguageLabel(int index, string label) => _languageLabels[index].text = label;

        public void SetLanguageButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < _languageButtons.Count; i++)
            {
                _languageButtons[i].interactable = interactable;
            }

            _confirmButton.interactable = interactable;
        }

        public void Show() => _panelRoot.SetActive(true);

        public void Hide() => _panelRoot.SetActive(false);
    }
}
