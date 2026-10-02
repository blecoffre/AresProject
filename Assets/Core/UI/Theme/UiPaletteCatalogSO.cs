using Core.Models.Settings;
using UnityEngine;

namespace Core.UI.Theme
{
    /// <summary>Les quatre palettes, une par mode de vision. Glissé sur le RootLifetimeScope.</summary>
    [CreateAssetMenu(fileName = "UiPaletteCatalog", menuName = "Core/Catalogue de palettes")]
    public class UiPaletteCatalogSO : ScriptableObject
    {
        public UiPaletteSO Standard;
        public UiPaletteSO Protanopia;
        public UiPaletteSO Deuteranopia;
        public UiPaletteSO Tritanopia;

        /// <summary>Retombe sur Standard si une palette manque : un champ vide ne doit pas tout casser.</summary>
        public UiPaletteSO Resolve(ColorVisionMode mode)
        {
            UiPaletteSO palette = mode switch
            {
                ColorVisionMode.Protanopia => Protanopia,
                ColorVisionMode.Deuteranopia => Deuteranopia,
                ColorVisionMode.Tritanopia => Tritanopia,
                _ => Standard,
            };

            return palette != null ? palette : Standard;
        }
    }
}
