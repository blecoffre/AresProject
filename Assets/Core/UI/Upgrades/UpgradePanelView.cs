using Core.Models.Economy; // Pour accéder à l'enum UpgradeType
using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Core.UI.Upgrades
{
    public class UpgradePanelView : MonoBehaviour
    {
        [Header("Tabs (Onglets)")]
        [SerializeField] private Button _scriptTabButton;
        [SerializeField] private Button _hardwareTabButton;
        [SerializeField] private Button _proxyTabButton;

        [Header("État visuel des onglets")]
        [Tooltip("Facultatif : l'état actif de chaque onglet, dans le même ordre que les boutons " +
                 "ci-dessus. Sans eux, seuls les conteneurs basculent — c'était le cas jusqu'au " +
                 "2026-08-28, et l'ActiveBackground de la scène n'était jamais allumé.")]
        [SerializeField] private TabButtonView _scriptTabVisual;
        [SerializeField] private TabButtonView _hardwareTabVisual;
        [SerializeField] private TabButtonView _proxyTabVisual;

        [Header("Content Prefab")]
        [SerializeField] private GeneratorView _generatorPrefab;
        
        [Header("Containers")]
        [SerializeField] private Transform _scriptContainer;
        [SerializeField] private Transform _hardwareContainer;
        [SerializeField] private Transform _proxyContainer;

        public event Action<UpgradeType> OnTabClicked;

        private IObjectResolver _resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private void Awake()
        {
            // On map chaque bouton à son type d'enum correspondant
            if (_scriptTabButton != null)
                _scriptTabButton.onClick.AddListener(() => OnTabClicked?.Invoke(UpgradeType.Script));

            if (_hardwareTabButton != null)
                _hardwareTabButton.onClick.AddListener(() => OnTabClicked?.Invoke(UpgradeType.Hardware));

            if (_proxyTabButton != null)
                _proxyTabButton.onClick.AddListener(() => OnTabClicked?.Invoke(UpgradeType.Proxy));
        }

        private void OnDestroy()
        {
            _scriptTabButton?.onClick.RemoveAllListeners();
            _hardwareTabButton?.onClick.RemoveAllListeners();
            _proxyTabButton?.onClick.RemoveAllListeners();
        }

        public GeneratorView SpawnGeneratorView(UpgradeType type)
        {
            Transform targetContainer = type switch
            {
                UpgradeType.Script => _scriptContainer,
                UpgradeType.Hardware => _hardwareContainer,
                UpgradeType.Proxy => _proxyContainer,
                _ => _scriptContainer
            };

            return Instantiate(_generatorPrefab, targetContainer);
        }

        public void ShowTab(UpgradeType type)
        {
            if (_scriptContainer != null) _scriptContainer.gameObject.SetActive(type == UpgradeType.Script);
            if (_hardwareContainer != null) _hardwareContainer.gameObject.SetActive(type == UpgradeType.Hardware);
            if (_proxyContainer != null) _proxyContainer.gameObject.SetActive(type == UpgradeType.Proxy);

            // Les trois onglets sont notifiés, pas seulement le nouveau : c'est ce qui éteint
            // celui qu'on quitte. Chaque vue se garde elle-même contre une écriture inutile.
            if (_scriptTabVisual != null) _scriptTabVisual.SetActiveState(type == UpgradeType.Script);
            if (_hardwareTabVisual != null) _hardwareTabVisual.SetActiveState(type == UpgradeType.Hardware);
            if (_proxyTabVisual != null) _proxyTabVisual.SetActiveState(type == UpgradeType.Proxy);
        }
    }
}