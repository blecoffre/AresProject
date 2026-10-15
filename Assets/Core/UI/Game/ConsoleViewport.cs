using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.UI.Game
{
    /// <summary>
    /// Fait défiler la console comme un terminal : les lignes partent du haut tant qu'il reste de
    /// la place, puis la dernière reste toujours visible en bas et les plus anciennes sortent par
    /// le haut.
    ///
    /// À poser sur le masque de la console (la fenêtre visible). Le contenu, lui, est ancré EN BAS
    /// du masque et grandit vers le haut ; ce composant lui donne une hauteur minimale égale à
    /// celle du masque. Tant que les lignes tiennent, le contenu fait exactement la fenêtre et son
    /// <see cref="VerticalLayoutGroup"/> (aligné en haut) les pose en haut ; dès qu'elles
    /// débordent, le contenu dépasse par le haut, et c'est le masque qui cache les anciennes.
    ///
    /// Jusqu'au 2026-10-15 le contenu était ancré EN HAUT et grandissait vers le bas : une fois la
    /// fenêtre pleine, les nouvelles lignes passaient sous son bord et restaient invisibles
    /// jusqu'à ce qu'une vingtaine d'autres les fassent remonter.
    ///
    /// Purement de la mise en page : aucune dépendance, rien à injecter.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ConsoleViewport : UIBehaviour
    {
        [Tooltip("Le LayoutElement du contenu de la console (l'objet qui porte le " +
                 "VerticalLayoutGroup et le ContentSizeFitter).")]
        [SerializeField] private LayoutElement _content;

        protected override void OnEnable()
        {
            base.OnEnable();
            SyncMinHeight();
        }

        /// <summary>
        /// La fenêtre change de taille avec la résolution et la taille d'interface : le minimum
        /// du contenu la suit, sans quoi les premières lignes ne partiraient plus du haut.
        /// </summary>
        protected override void OnRectTransformDimensionsChange()
        {
            SyncMinHeight();
        }

        private void SyncMinHeight()
        {
            if (_content == null) return;

            float height = ((RectTransform)transform).rect.height;

            // Chaque écriture effective relance une reconstruction de la mise en page : on ne la
            // paie pas pour un écart de virgule flottante.
            if (Mathf.Approximately(_content.minHeight, height)) return;

            _content.minHeight = height;
        }
    }
}
