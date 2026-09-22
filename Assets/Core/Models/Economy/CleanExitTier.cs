using System;
using UnityEngine;

namespace Core.Models.Economy
{
    /// <summary>
    /// Un palier d'extraction : la Trace minimale à avoir atteinte, et le bonus de CPU Cycles
    /// qu'on emporte en sortant au-delà.
    ///
    /// <b>Pourquoi des paliers et pas une courbe.</b> Le bonus a d'abord été forfaitaire (+20 %
    /// quoi qu'il arrive), ce qui ne donnait aucune raison de s'approcher du bord : le danger
    /// n'achetait rien, et prendre un risque était arithmétiquement perdant. Il a ensuite été une
    /// courbe continue, et c'était l'excès inverse — chaque seconde de retard devenait monnayable,
    /// le bonus passant de +25 % à +89 % entre 70 % et 90 % de jauge. La sortie se transformait en
    /// optimisation au chronomètre.
    ///
    /// Un palier ne bouge pas entre deux seuils. Traîner ne rapporte donc rien, et la seule
    /// question qui se pose est franche : <b>« je tente le palier suivant, oui ou non ? »</b>
    /// C'est une décision, pas un réglage fin — et c'est affichable, ce qu'une courbe n'était pas.
    /// </summary>
    [Serializable]
    public struct CleanExitTier
    {
        [Tooltip("Fraction de jauge à avoir atteinte pour empocher ce palier.")]
        [SerializeField, Range(0f, 1f)] private float _traceThreshold;

        [Tooltip("Bonus de CPU Cycles, en fraction. 0,15 = +15 %. Avant multiplicateur de prestige.")]
        [SerializeField, Min(0f)] private double _bonus;

        [Tooltip("Ce palier exige le nœud de prestige « Extraction Haut Risque ». Tant qu'il n'est " +
                 "pas acheté, le palier ne verse RIEN : le joueur retombe sur le meilleur palier " +
                 "libre qu'il a franchi.\n\n" +
                 "Existe pour le palier à 90 %, qui était proposé dès la première run alors que le " +
                 "joueur n'a encore aucun capteur et pilote à l'aveugle. Il invitait sans être " +
                 "tenable — mesuré, la posture qui le visait mourait 15 fois sur 18.")]
        [SerializeField] private bool _requiresUnlock;

        public float TraceThreshold => _traceThreshold;
        public double Bonus => _bonus;

        /// <summary>
        /// Vrai si ce palier attend le nœud de prestige « Extraction Haut Risque ». Voir
        /// <c>GameSessionManager.ResolveCleanExitMultiplier</c>, seul endroit où la règle s'applique.
        /// </summary>
        public bool RequiresUnlock => _requiresUnlock;

        public CleanExitTier(float traceThreshold, double bonus, bool requiresUnlock = false)
        {
            _traceThreshold = traceThreshold;
            _bonus = bonus;
            _requiresUnlock = requiresUnlock;
        }
    }
}
