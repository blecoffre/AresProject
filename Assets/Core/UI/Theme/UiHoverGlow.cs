using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.UI.Theme
{
    /// <summary>
    /// Survol d'un bouton du terminal : le contour s'allume et un fond très sombre apparaît.
    ///
    /// <b>Pourquoi ni Color Tint, ni Animation.</b> Le Color Tint de Unity ne sait que multiplier
    /// la couleur du `targetGraphic` : il ne peut ni changer un matériau, ni toucher un enfant,
    /// donc il ne peut pas produire le halo de la maquette. Le mode Animation, lui, exige un
    /// Animator et quatre clips PAR bouton, et surtout une animation réécrit ses propriétés à
    /// chaque frame — elle entrerait en collision avec les presenters, qui posent le libellé et
    /// l'état d'interactivité en fonction du jeu. D'où ce composant : il ne touche QUE deux
    /// choses dont aucun presenter ne s'occupe, le matériau du contour et le fond.
    ///
    /// Le halo n'existe qu'au survol, conformément à la maquette : au repos, un bouton n'est
    /// qu'un cadre de 1 px. Mettre le halo en permanence noierait l'écran.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UiHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Cibles")]
        [Tooltip("Le contour du bouton. Laissé vide, le composant prend l'Image du bouton lui-même.")]
        [SerializeField] private Image _border;

        [Tooltip("Facultatif : le fond qui apparaît au survol, sur le modèle de l'ActiveBackground " +
                 "des onglets. Sans lui, seul le contour réagit.")]
        [SerializeField] private Image _background;

        [Header("Apparence au survol")]
        [Tooltip("Matériau de halo appliqué au contour pendant le survol. Doit correspondre à la " +
                 "couleur du bouton : un Mat_UIGlow_Green sur un bouton orange donnerait un cadre " +
                 "orange cerné de vert.")]
        [SerializeField] private Material _borderGlowMaterial;

        private Selectable _selectable;
        private Material _restingBorderMaterial;
        private bool _isPointerInside;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (_border == null) _border = _selectable.targetGraphic as Image;

            // Mémorisé une fois pour toutes : c'est l'état de repos qu'on restaurera. Le relire à
            // la sortie du survol renverrait le matériau de halo, qu'on vient justement de poser.
            if (_border != null) _restingBorderMaterial = _border.material;

            ApplyResting();
        }

        private void OnEnable()
        {
            // Un bouton peut être désactivé alors que le pointeur est dessus — changement d'onglet,
            // ouverture d'un panneau. Il ne recevra jamais son PointerExit et resterait allumé.
            _isPointerInside = false;
            ApplyResting();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            Refresh();
        }

        /// <summary>
        /// Ne tourne QUE pendant un survol, donc au plus sur un bouton à la fois, et ne fait
        /// qu'une lecture de booléen. C'est le prix à payer pour un cas bien réel : cliquer le
        /// Data Wiper le grise instantanément alors que le curseur est encore dessus. Sans cette
        /// surveillance, il resterait allumé jusqu'à ce que la souris bouge — et le joueur lirait
        /// « disponible » sur un bouton qui ne l'est plus.
        ///
        /// L'alternative — faire appeler Refresh() par les presenters — coupleraient trois vues à
        /// un détail d'habillage. Une lecture par frame vaut mieux que ce fil-là.
        /// </summary>
        private void Update()
        {
            if (!_isPointerInside) return;
            Refresh();
        }

        /// <summary>
        /// Recalcule l'apparence depuis l'état courant. Public pour qu'un presenter puisse
        /// forcer la mise à jour s'il le souhaite, mais rien ne l'y oblige.
        /// </summary>
        public void Refresh()
        {
            if (_isPointerInside && _selectable != null && _selectable.IsInteractable())
            {
                ApplyHover();
                return;
            }

            ApplyResting();
        }

        private void ApplyHover()
        {
            if (_border != null && _borderGlowMaterial != null) _border.material = _borderGlowMaterial;
            if (_background != null && !_background.gameObject.activeSelf) _background.gameObject.SetActive(true);
        }

        private void ApplyResting()
        {
            if (_border != null) _border.material = _restingBorderMaterial;
            if (_background != null && _background.gameObject.activeSelf) _background.gameObject.SetActive(false);
        }
    }
}
