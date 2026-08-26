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

        /// <summary>
        /// Résolution paresseuse, et surtout PAS dans Awake().
        ///
        /// L'arbre est construit alors que son panneau est encore désactivé, et Unity n'appelle
        /// pas Awake() sur un objet inactif : un repli placé là ne se serait jamais exécuté, et
        /// SetNodePosition serait resté un no-op silencieux laissant les 169 nœuds empilés à
        /// l'origine. Le GetComponent n'a lieu qu'une fois, puis la référence est en cache.
        /// </summary>
        private RectTransform Rect
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }

                return _rectTransform;
            }
        }

        private void Awake()
        {
            _buyButton.onClick.AddListener(HandleBuyClicked);
        }

        private void OnDestroy()
        {
            _buyButton.onClick.RemoveListener(HandleBuyClicked);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void HandleBuyClicked() => OnBuyClicked?.Invoke();

        public void InitializeStaticData(string name, string description)
        {
            if (_nameText != null) _nameText.text = name;
            if (_descriptionText != null) _descriptionText.text = description;
        }

        /// <summary>
        /// La vue n'écrit que ce qu'on lui donne : les deux chaînes arrivent déjà localisées et
        /// déjà formatées, cas « niveau max » compris. Aucun texte affichable ne vit ici.
        /// </summary>
        public void UpdateDynamicData(string levelText, string costText, bool canAfford, bool isMaxed)
        {
            if (_levelText != null) _levelText.text = levelText;
            if (_costText != null) _costText.text = costText;

            // On désactive le bouton si on n'a pas l'argent OU si c'est au niveau max
            _buyButton.interactable = canAfford && !isMaxed;
        }

        // Méthode clé pour la Toile d'Araignée !
        public void SetNodePosition(Vector2 localPosition)
        {
            Rect.anchoredPosition = localPosition;
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