using TMPro;

namespace Core.UI.Prestige
{
    /// <summary>
    /// Écrit la ligne du Noyau Compilé dans un texte de vue, ou la masque. Partagé par le panneau
    /// d'upgrades et l'écran de l'arbre, qui l'affichent tous deux en tête.
    /// </summary>
    public static class CoreSummaryText
    {
        /// <summary>
        /// Une chaîne vide masque la ligne — c'est le cas tant qu'aucun point n'est dépensé.
        /// Les gardes d'écriture évitent de réveiller TextMeshPro et le layout pour rien : la
        /// ligne est repeinte à chaque recalcul de l'arbre, rarement pour un texte différent.
        /// </summary>
        public static void Apply(TextMeshProUGUI target, string text)
        {
            if (target == null) return;

            bool isVisible = !string.IsNullOrEmpty(text);

            if (target.gameObject.activeSelf != isVisible)
            {
                target.gameObject.SetActive(isVisible);
            }

            if (isVisible && target.text != text)
            {
                target.text = text;
            }
        }
    }
}
