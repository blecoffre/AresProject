using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Upgrades
{
    public class GeneratorView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private TextMeshProUGUI _statsText;

        [Tooltip("Facultatif : laisser vide tant que le prefab n'affiche pas de description.")]
        [SerializeField] private TextMeshProUGUI _descriptionText;

        [Tooltip("Image en Filled : c'est la barre de progression du cycle.")]
        [SerializeField] private Image _sweepBackgroundImage;

        [Header("Interaction")]
        [SerializeField] private Button _buyButton;

        [Tooltip("Bouton qui lance un cycle à la main. Requis pour les Scripts, inutile ailleurs.")]
        [SerializeField] private Button _runButton;

        [Tooltip("Facultatif : le libellé DANS le bouton d'achat. Il porte le multiplicateur " +
                 "courant (« ACHETER x10 »), donc laisser ce champ vide fait perdre au joueur " +
                 "la seule indication, sur la carte, du lot qu'il s'apprête à acheter.")]
        [SerializeField] private TextMeshProUGUI _buyLabelText;

        public event Action OnBuyClicked;
        public event Action OnRunClicked;

        private void Awake()
        {
            _buyButton.onClick.AddListener(HandleBuyClicked);

            if (_runButton != null)
            {
                _runButton.onClick.AddListener(HandleRunClicked);
            }
        }

        private void OnDestroy()
        {
            _buyButton.onClick.RemoveListener(HandleBuyClicked);

            if (_runButton != null)
            {
                _runButton.onClick.RemoveListener(HandleRunClicked);
            }
        }

        // Méthodes nommées plutôt que des lambdas : un "-=" sur une lambda crée un nouveau
        // delegate et ne désabonne rien.
        private void HandleBuyClicked() => OnBuyClicked?.Invoke();
        private void HandleRunClicked() => OnRunClicked?.Invoke();

        /// <summary>Signale au presenter si ce prefab dispose d'un bouton de lancement câblé.</summary>
        public bool HasRunButton => _runButton != null;

        /// <summary>Signale au presenter si le libellé du bouton d'achat est câblé.</summary>
        public bool HasBuyLabel => _buyLabelText != null;

        public void InitializeStaticData(string generatorName, string description, string stats)
        {
            if (_nameText != null) _nameText.text = generatorName;
            if (_descriptionText != null) _descriptionText.text = description;
            if (_statsText != null) _statsText.text = stats;
        }

        public void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }

        public void UpdateSweepProgress(float normalizedProgress)
        {
            if (_sweepBackgroundImage != null) _sweepBackgroundImage.fillAmount = normalizedProgress;
        }

        /// <summary>
        /// Le niveau courant et le prix sont écrits séparément depuis l'arrivée de l'achat
        /// multiple : ils ne changent plus au même moment. Le niveau ne bouge qu'à l'achat, le
        /// prix suit aussi le mode choisi et — en MAX — le solde du joueur.
        /// </summary>
        public void UpdateLevel(string localizedLevel)
        {
            if (_levelText != null) _levelText.text = localizedLevel;
        }

        public void UpdateCost(string localizedCost)
        {
            if (_costText != null) _costText.text = localizedCost;
        }

        /// <summary>Libellé du bouton d'achat : il porte le multiplicateur courant.</summary>
        public void UpdateBuyLabel(string localizedLabel)
        {
            if (_buyLabelText != null) _buyLabelText.text = localizedLabel;
        }

        public void UpdateStats(string statsText)
        {
            if (_statsText != null) _statsText.text = statsText;
        }

        public void SetBuyButtonInteractable(bool canAfford)
        {
            _buyButton.interactable = canAfford;
        }

        /// <summary>
        /// État du bouton de lancement. Il disparaît une fois le générateur automatisé :
        /// laisser un bouton mort à l'écran ferait croire à une action encore disponible.
        /// </summary>
        public void SetRunState(bool isVisible, bool isInteractable)
        {
            if (_runButton == null) return;

            if (_runButton.gameObject.activeSelf != isVisible)
            {
                _runButton.gameObject.SetActive(isVisible);
            }

            _runButton.interactable = isInteractable;
        }
    }
}
