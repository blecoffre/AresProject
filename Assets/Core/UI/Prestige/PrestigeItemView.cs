using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Prestige
{
    public class PrestigeItemView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _costText;

        [Header("Interaction")]
        [SerializeField] private Button _buyButton;
        [SerializeField] private RectTransform _rectTransform; // Pour le positionnement absolu

        [Header("Fog of War")]
        [SerializeField] private GameObject _contentPanel; // Le panel qui contient le nom, la description, le bouton
        [SerializeField] private GameObject _unknownPanel; // Un panel avec juste un gros "?"
        [SerializeField] private Image _nodeBackground;    // Pour griser le fond si besoin

        public event Action OnBuyClicked;

        private void Awake()
        {
            _buyButton.onClick.AddListener(() => OnBuyClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _buyButton.onClick.RemoveAllListeners();
        }

        public void InitializeStaticData(string name, string description)
        {
            if (_nameText != null) _nameText.text = name;
            if (_descriptionText != null) _descriptionText.text = description;
        }

        public void UpdateDynamicData(string levelText, string costText, bool canAfford, bool isMaxed)
        {
            if (_levelText != null) _levelText.text = levelText;

            if (_costText != null)
            {
                _costText.text = isMaxed ? "MAX" : costText;
            }

            // On désactive le bouton si on n'a pas l'argent OU si c'est au niveau max
            _buyButton.interactable = canAfford && !isMaxed;
        }

        // Méthode clé pour la Toile d'Araignée !
        public void SetNodePosition(Vector2 localPosition)
        {
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = localPosition;
            }
        }

        public void SetLockState(bool isLocked)
        {
            // Si c'est bloqué, on affiche le "?" et on cache le contenu
            if (_contentPanel != null) _contentPanel.SetActive(!isLocked);
            if (_unknownPanel != null) _unknownPanel.SetActive(isLocked);

            if (_nodeBackground != null)
            {
                _nodeBackground.color = isLocked ? Color.gray : Color.white;
            }
        }
    }
}