using UnityEngine;

namespace Core.UI.Theme
{
    /// <summary>
    /// Les couleurs qui PORTENT UN SENS dans l'interface, pour un mode de vision donné.
    ///
    /// <b>Un champ par usage, pas des rôles génériques.</b> Fusionner « abordable », « palier
    /// incertain » et « urgence prête » en un seul rôle aurait aplati des teintes réglées à la
    /// main, et changé le rendu du mode Standard. Ici, Standard reproduit exactement les couleurs
    /// d'origine, et chaque mode daltonien est optimisé groupe par groupe.
    ///
    /// <b>Ce qui compte, ce sont les groupes.</b> Deux couleurs ne doivent se distinguer que si
    /// elles s'affichent ensemble : les états d'un nœud entre eux, les paliers de la jauge entre
    /// eux, etc. — et chacune contre le vert terminal du décor. C'est exactement ce que mesure
    /// <c>Tools/Core/Vérifier les palettes (daltonisme)</c>, à relancer après toute retouche.
    ///
    /// Le décor (vert terminal, verts sombres, gris) n'est PAS ici : il se lit par sa
    /// luminosité, que les daltonismes conservent.
    /// </summary>
    [CreateAssetMenu(fileName = "UiPalette", menuName = "Core/Palette d'interface")]
    public class UiPaletteSO : ScriptableObject
    {
        [Header("Arbre de prestige — états des nœuds")]
        public Color NodeLocked = new Color(0.200f, 0.333f, 0.200f, 1f);
        public Color NodeTooExpensive = new Color(1f, 0.200f, 0.200f, 1f);
        public Color NodeAffordable = new Color(1f, 0.600f, 0f, 1f);
        public Color NodeInProgress = new Color(0.290f, 0.965f, 0.149f, 1f);
        public Color NodeMaxed = new Color(0.290f, 0.965f, 0.149f, 1f);

        [Header("Arbre de prestige — inspecteur (réglé à part : du texte, pas une pastille)")]
        public Color InspectorLocked = new Color(0.200f, 0.333f, 0.200f, 1f);
        public Color InspectorTooExpensive = new Color(1f, 0.200f, 0.200f, 1f);
        public Color InspectorAffordable = new Color(1f, 0.600f, 0f, 1f);
        public Color InspectorInProgress = new Color(0.290f, 0.965f, 0.149f, 1f);
        public Color InspectorMaxed = new Color(0.290f, 0.965f, 0.149f, 1f);

        [Header("Arbre de prestige — liens")]
        public Color LinkLocked = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        public Color LinkUnlocked = Color.cyan;
        public Color LinkCompleted = Color.green;

        [Header("Jauge de Trace — paliers")]
        public Color TierReached = new Color(0.29f, 0.96f, 0.15f, 1f);
        public Color TierUncertain = new Color(0.94f, 0.63f, 0.15f, 1f);
        public Color TierUpcoming = new Color(0.24f, 0.35f, 0.24f, 1f);
        public Color TierLocked = new Color(0.60f, 0.60f, 0.60f, 1f);

        [Header("Fin de run — titre")]
        public Color RunEndSeized = new Color(1f, 0.2f, 0.2f, 1f);
        public Color RunEndCleanExit = new Color(0.29f, 0.96f, 0.15f, 1f);

        [Header("Bouton d'urgence / Data Wiper")]
        public Color EmergencyUnavailable = new Color(0.35f, 0.35f, 0.40f, 1f);
        public Color EmergencyReady = new Color(0.95f, 0.75f, 0.20f, 1f);
        public Color EmergencyBlocked = new Color(0.85f, 0.35f, 0.55f, 1f);

        [Header("Console — étiquettes de ligne")]
        public Color ConsoleSuccess = new Color(0f, 1f, 0f, 1f);
        public Color ConsoleNarrative = new Color(1f, 0f, 1f, 1f);
        public Color ConsoleStandard = new Color(0.8f, 0.8f, 0.8f, 1f);

        // Les balises <color=#…> de la console, calculées une fois : sans ce cache, chaque ligne
        // de log convertirait une couleur en chaîne.
        public string ConsoleSuccessHex { get; private set; }
        public string ConsoleNarrativeHex { get; private set; }
        public string ConsoleStandardHex { get; private set; }

        private void OnEnable() => RefreshHexCache();

        // Retouche dans l'inspecteur en Play Mode : le cache suit.
        private void OnValidate() => RefreshHexCache();

        private void RefreshHexCache()
        {
            ConsoleSuccessHex = "#" + ColorUtility.ToHtmlStringRGB(ConsoleSuccess);
            ConsoleNarrativeHex = "#" + ColorUtility.ToHtmlStringRGB(ConsoleNarrative);
            ConsoleStandardHex = "#" + ColorUtility.ToHtmlStringRGB(ConsoleStandard);
        }
    }
}
