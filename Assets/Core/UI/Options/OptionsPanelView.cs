using Core.UI.Upgrades;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Options
{
    /// <summary>
    /// Le panneau d'options, partagé entre le menu principal et le menu de pause : c'est le
    /// même prefab, posé dans chaque scène, et le même presenter dans chaque scope.
    ///
    /// Le composant vit sur la racine du prefab, qui reste ACTIVE ; seul <c>_panelRoot</c> est
    /// allumé ou éteint. Sans ça, l'Awake ne tournerait qu'à la première ouverture et les
    /// boutons ne seraient pas câblés quand le presenter s'abonne.
    /// </summary>
    public class OptionsPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Onglets")]
        [Tooltip("Un bouton, un visuel et une page par onglet, dans le même ordre : Affichage, " +
                 "Audio, Accessibilité. L'ordre est celui des constantes de l'OptionsPanelPresenter.")]
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private TabButtonView[] _tabVisuals;
        [SerializeField] private GameObject[] _tabPages;

        [Header("Lignes")]
        [SerializeField] private OptionSelectorView _language;
        [SerializeField] private OptionSelectorView _displayMode;
        [SerializeField] private OptionSelectorView _resolution;
        [SerializeField] private OptionSelectorView _vSync;
        [SerializeField] private OptionSelectorView _frameRateCap;
        [SerializeField] private OptionSelectorView _uiScale;
        [SerializeField] private OptionSelectorView _colorVision;

        [Header("Lignes — audio")]
        [SerializeField] private OptionSelectorView _masterMute;
        [SerializeField] private OptionSelectorView _audioInBackground;
        [SerializeField] private OptionSelectorView _musicVolume;
        [SerializeField] private OptionSelectorView _musicMute;
        [SerializeField] private OptionSelectorView _sfxVolume;
        [SerializeField] private OptionSelectorView _sfxMute;

        [Header("Retour")]
        [SerializeField] private Button _backButton;
        [SerializeField] private TextMeshProUGUI _backLabel;

        public event Action OnBackClicked;
        public event Action<int> OnTabClicked;

        public OptionSelectorView Language => _language;
        public OptionSelectorView DisplayMode => _displayMode;
        public OptionSelectorView Resolution => _resolution;
        public OptionSelectorView VSync => _vSync;
        public OptionSelectorView FrameRateCap => _frameRateCap;
        public OptionSelectorView UiScale => _uiScale;
        public OptionSelectorView ColorVision => _colorVision;
        public OptionSelectorView MasterMute => _masterMute;
        public OptionSelectorView AudioInBackground => _audioInBackground;
        public OptionSelectorView MusicVolume => _musicVolume;
        public OptionSelectorView MusicMute => _musicMute;
        public OptionSelectorView SfxVolume => _sfxVolume;
        public OptionSelectorView SfxMute => _sfxMute;

        public int TabCount => _tabPages.Length;

        public bool IsVisible => _panelRoot.activeSelf;

        private void Awake()
        {
            _backButton.onClick.AddListener(RaiseBack);

            // Une closure par onglet, allouée une fois à l'initialisation : l'index doit voyager
            // avec le clic, et trois délégués pour toute la vie du panneau ne coûtent rien.
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => OnTabClicked?.Invoke(index));
            }
        }

        private void OnDestroy()
        {
            _backButton.onClick.RemoveListener(RaiseBack);

            // Les boutons d'onglet appartiennent à cette vue : rien d'autre ne s'y abonne.
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                _tabButtons[i].onClick.RemoveAllListeners();
            }
        }

        public void SetTabLabel(int index, string label) => _tabVisuals[index].SetLabel(label);

        /// <summary>Affiche une page et allume son onglet ; éteint les autres.</summary>
        public void ShowTab(int index)
        {
            for (int i = 0; i < _tabPages.Length; i++)
            {
                bool isActive = i == index;
                if (_tabPages[i].activeSelf != isActive) _tabPages[i].SetActive(isActive);
                _tabVisuals[i].SetActiveState(isActive);
            }
        }

        private void RaiseBack() => OnBackClicked?.Invoke();

        public void SetTexts(string title, string back)
        {
            _titleText.text = title;
            _backLabel.text = back;
        }

        public void Show() => _panelRoot.SetActive(true);

        public void Hide() => _panelRoot.SetActive(false);
    }
}
