namespace Core.UI.Prestige
{
    /// <summary>
    /// Les cinq codes d'état d'un nœud de l'arbre. Un nœud est TOUJOURS dans exactement un de
    /// ces états : c'est le presenter qui tranche, la vue ne fait que peindre.
    ///
    /// L'ordre de priorité est celui de la maquette et il compte : un nœud déjà entamé reste
    /// « en cours » même quand le joueur pourrait s'offrir le niveau suivant. La pulsation ambre
    /// est réservée aux nœuds jamais achetés — c'est un appel à ouvrir une branche, pas un
    /// rappel permanent que tout est achetable.
    /// </summary>
    public enum PrestigeNodeState
    {
        /// <summary>Le prérequis n'est pas possédé. Gris, inerte, mais lisible.</summary>
        Locked,

        /// <summary>Accessible, jamais acheté, hors budget. Rouge alerte.</summary>
        TooExpensive,

        /// <summary>Accessible, jamais acheté, dans le budget. Ambre, pulsant.</summary>
        Affordable,

        /// <summary>Acheté au moins une fois, pas encore au maximum. Vert standard.</summary>
        InProgress,

        /// <summary>Niveau maximum atteint. Couleurs inversées : fond plein, texte noir.</summary>
        Maxed
    }

    /// <summary>
    /// La règle de priorité, en un seul endroit. Le nœud dans l'arbre et l'inspecteur doivent
    /// forcément s'accorder : voir une pastille verte et lire « fonds insuffisants » juste à côté
    /// détruirait la confiance dans tout l'écran.
    /// </summary>
    public static class PrestigeNodeStates
    {
        public static PrestigeNodeState Resolve(bool isUnlocked, bool isMaxedOut, int currentLevel, bool canAfford)
        {
            if (isMaxedOut) return PrestigeNodeState.Maxed;
            if (!isUnlocked) return PrestigeNodeState.Locked;
            if (currentLevel > 0) return PrestigeNodeState.InProgress;

            return canAfford ? PrestigeNodeState.Affordable : PrestigeNodeState.TooExpensive;
        }
    }
}
