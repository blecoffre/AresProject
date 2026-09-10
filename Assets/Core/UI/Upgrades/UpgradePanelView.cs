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

        [Header("Sélecteur d'achat multiple")]
        [Tooltip("Les quatre boutons de quantité. Ils sont GLOBAUX : le mode choisi vaut pour " +
                 "les trois onglets, comme dans la plupart des incrémentaux. Laisser les champs " +
                 "vides désactive simplement la fonctionnalité, le jeu reste jouable en x1.")]
        [SerializeField] private Button _buyX1Button;
        [SerializeField] private Button _buyX10Button;
        [SerializeField] private Button _buyX100Button;
        [SerializeField] private Button _buyMaxButton;

        [Header("État visuel du sélecteur")]
        [Tooltip("Facultatif, dans le même ordre que les boutons ci-dessus. On réutilise " +
                 "TabButtonView : un bouton de quantité a exactement le même état visuel qu'un " +
                 "onglet — un seul actif à la fois, fond allumé et libellé vif.")]
        [SerializeField] private TabButtonView _buyX1Visual;
        [SerializeField] private TabButtonView _buyX10Visual;
        [SerializeField] private TabButtonView _buyX100Visual;
        [SerializeField] private TabButtonView _buyMaxVisual;

        [Header("Content Prefab")]
        [SerializeField] private GeneratorView _generatorPrefab;

        [Header("Containers")]
        [Tooltip("Le Content de chaque ScrollRect : c'est LÀ que les générateurs sont " +
                 "instanciés. Ce champ ne sert qu'à ça — ce n'est pas lui qu'on allume et " +
                 "qu'on éteint pour changer d'onglet, voir les racines ci-dessous.")]
        [SerializeField] private Transform _scriptContainer;
        [SerializeField] private Transform _hardwareContainer;
        [SerializeField] private Transform _proxyContainer;

        [Header("Racines d'onglet")]
        [Tooltip("Le panneau ENTIER de chaque onglet — ScriptPanel / HardwarePanel / ProxyPanel, " +
                 "au-dessus du Scroll View. C'est ce qu'on allume et qu'on éteint.\n\n" +
                 "Surtout pas le Viewport ni le Content : le ScrollRect est porté par le " +
                 "« Scroll View », au-dessus d'eux. N'éteindre que ce qui est EN DESSOUS laisse " +
                 "les trois ScrollRect actifs et superposés — ils ont les mêmes ancres — et " +
                 "c'est le dernier de la fratrie qui capte la molette pour toute la zone. On " +
                 "scrollerait la liste des Proxies en regardant celle des Scripts.\n\n" +
                 "Facultatif : sans eux, on retombe sur l'ancien comportement (bascule du " +
                 "Content), qui reste jouable mais rend la molette imprévisible.")]
        [SerializeField] private Transform _scriptTabRoot;
        [SerializeField] private Transform _hardwareTabRoot;
        [SerializeField] private Transform _proxyTabRoot;

        public event Action<UpgradeType> OnTabClicked;
        public event Action<BuyQuantity> OnBuyQuantityClicked;

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

            // Même mapping bouton → valeur d'enum que pour les onglets, et mêmes gardes de nullité :
            // le sélecteur d'achat multiple est neuf et pas encore posé dans la scène.
            if (_buyX1Button != null)
                _buyX1Button.onClick.AddListener(() => OnBuyQuantityClicked?.Invoke(BuyQuantity.X1));

            if (_buyX10Button != null)
                _buyX10Button.onClick.AddListener(() => OnBuyQuantityClicked?.Invoke(BuyQuantity.X10));

            if (_buyX100Button != null)
                _buyX100Button.onClick.AddListener(() => OnBuyQuantityClicked?.Invoke(BuyQuantity.X100));

            if (_buyMaxButton != null)
                _buyMaxButton.onClick.AddListener(() => OnBuyQuantityClicked?.Invoke(BuyQuantity.Max));

            // Un seul avertissement, et seulement si AUCUN bouton n'est câblé : c'est le signe
            // que le montage dans la scène n'a pas été fait, pas qu'un bouton manque à l'appel.
            if (_buyX1Button == null && _buyX10Button == null && _buyX100Button == null && _buyMaxButton == null)
            {
                Debug.LogWarning(
                    "[UPGRADES] Aucun bouton de quantité assigné sur l'UpgradePanelView : " +
                    "l'achat multiple est indisponible et le jeu reste en x1. Pose les quatre " +
                    "boutons dans le panneau et glisse-les dans les champs pour l'activer.");
            }

            if (_scriptTabRoot == null || _hardwareTabRoot == null || _proxyTabRoot == null)
            {
                Debug.LogWarning(
                    "[UPGRADES] Racines d'onglet incomplètes sur l'UpgradePanelView : le " +
                    "changement d'onglet retombe sur la bascule du Content. Les trois ScrollRect " +
                    "restent alors actifs et superposés, et c'est le dernier de la fratrie qui " +
                    "capte la molette. Glisse ScriptPanel / HardwarePanel / ProxyPanel dans les " +
                    "champs de racine.");
            }
        }

        private void OnDestroy()
        {
            _scriptTabButton?.onClick.RemoveAllListeners();
            _hardwareTabButton?.onClick.RemoveAllListeners();
            _proxyTabButton?.onClick.RemoveAllListeners();

            _buyX1Button?.onClick.RemoveAllListeners();
            _buyX10Button?.onClick.RemoveAllListeners();
            _buyX100Button?.onClick.RemoveAllListeners();
            _buyMaxButton?.onClick.RemoveAllListeners();
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
            // UNE seule bascule par onglet, et le plus haut possible. Basculer à deux niveaux
            // (racine ET Content) serait redondant, et surtout ça rend imprévisible le moment où
            // le VerticalLayoutGroup et le ContentSizeFitter reconstruisent : ni l'un ni l'autre
            // ne recalcule quoi que ce soit tant que son objet est inactif.
            SetTabActive(_scriptTabRoot, _scriptContainer, type == UpgradeType.Script);
            SetTabActive(_hardwareTabRoot, _hardwareContainer, type == UpgradeType.Hardware);
            SetTabActive(_proxyTabRoot, _proxyContainer, type == UpgradeType.Proxy);

            // Les trois onglets sont notifiés, pas seulement le nouveau : c'est ce qui éteint
            // celui qu'on quitte. Chaque vue se garde elle-même contre une écriture inutile.
            if (_scriptTabVisual != null) _scriptTabVisual.SetActiveState(type == UpgradeType.Script);
            if (_hardwareTabVisual != null) _hardwareTabVisual.SetActiveState(type == UpgradeType.Hardware);
            if (_proxyTabVisual != null) _proxyTabVisual.SetActiveState(type == UpgradeType.Proxy);
        }

        /// <summary>
        /// Allume ou éteint un onglet par sa racine. Le repli sur le Content n'existe que pour
        /// garder la scène jouable tant que les racines ne sont pas câblées ; il ne masque rien,
        /// l'Awake a déjà prévenu.
        /// </summary>
        private static void SetTabActive(Transform tabRoot, Transform container, bool isActive)
        {
            Transform target = tabRoot != null ? tabRoot : container;
            if (target == null) return;

            // Garde d'écriture : SetActive sur une hiérarchie UI déclenche OnEnable en cascade et
            // une reconstruction de layout. ShowTab est appelé sur les TROIS onglets à chaque
            // clic, donc deux des trois appels ne changent rien.
            if (target.gameObject.activeSelf == isActive) return;

            target.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// Allume le bouton du mode d'achat courant et éteint les trois autres. Les quatre sont
        /// notifiés, pas seulement le nouveau — c'est ce qui éteint celui qu'on quitte, exactement
        /// comme pour les onglets.
        /// </summary>
        public void ShowBuyQuantity(BuyQuantity quantity)
        {
            if (_buyX1Visual != null) _buyX1Visual.SetActiveState(quantity == BuyQuantity.X1);
            if (_buyX10Visual != null) _buyX10Visual.SetActiveState(quantity == BuyQuantity.X10);
            if (_buyX100Visual != null) _buyX100Visual.SetActiveState(quantity == BuyQuantity.X100);
            if (_buyMaxVisual != null) _buyMaxVisual.SetActiveState(quantity == BuyQuantity.Max);
        }
    }
}