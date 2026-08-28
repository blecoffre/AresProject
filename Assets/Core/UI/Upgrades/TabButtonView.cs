using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Upgrades
{
    /// <summary>
    /// L'état visuel d'un onglet : actif ou non.
    ///
    /// Reprend la maquette au caractère près — un onglet actif porte un fond très sombre, une
    /// barre de soulignement lumineuse et un libellé vif ; un onglet au repos n'a qu'un libellé
    /// éteint. Jusqu'ici `UpgradePanelView.ShowTab` ne faisait que basculer les CONTENEURS :
    /// l'`ActiveBackground` et la `HighlightBar` existaient dans la scène sans que rien ne les
    /// allume jamais.
    ///
    /// Vue et non presenter : c'est un état purement visuel, dérivé de l'onglet courant, qui ne
    /// consulte aucun modèle. Le presenter continue de ne connaître qu'`UpgradePanelView`.
    /// </summary>
    public class TabButtonView : MonoBehaviour
    {
        [Header("Éléments allumés quand l'onglet est actif")]
        [Tooltip("Le fond sombre de l'onglet sélectionné.")]
        [SerializeField] private GameObject _activeBackground;

        [Tooltip("La barre de soulignement lumineuse.")]
        [SerializeField] private GameObject _highlightBar;

        [Header("Libellé")]
        [SerializeField] private TextMeshProUGUI _label;

        [Tooltip("Couleur du libellé quand l'onglet est actif.")]
        [SerializeField] private Color _activeColor = new Color(0.29f, 0.96f, 0.15f, 1f);

        [Tooltip("Couleur du libellé au repos. Éteinte : dans la maquette, un onglet inactif " +
                 "se lit à peine.")]
        [SerializeField] private Color _inactiveColor = new Color(0.10f, 0.30f, 0.10f, 1f);

        [Header("Matériaux du libellé")]
        [Tooltip("Matériau de halo, appliqué au libellé de l'onglet ACTIF uniquement.")]
        [SerializeField] private Material _activeMaterial;

        [Tooltip("Matériau sans halo, pour les onglets au repos. Un libellé éteint qui brille " +
                 "serait un contresens.")]
        [SerializeField] private Material _inactiveMaterial;

        public void SetActiveState(bool isActive)
        {
            if (_activeBackground != null && _activeBackground.activeSelf != isActive)
            {
                _activeBackground.SetActive(isActive);
            }

            if (_highlightBar != null && _highlightBar.activeSelf != isActive)
            {
                _highlightBar.SetActive(isActive);
            }

            if (_label == null) return;

            _label.color = isActive ? _activeColor : _inactiveColor;

            Material target = isActive ? _activeMaterial : _inactiveMaterial;
            if (target != null && _label.fontSharedMaterial != target)
            {
                _label.fontSharedMaterial = target;
            }
        }
    }
}
