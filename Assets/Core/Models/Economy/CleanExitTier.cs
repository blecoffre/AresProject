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

        public float TraceThreshold => _traceThreshold;
        public double Bonus => _bonus;

        public CleanExitTier(float traceThreshold, double bonus)
        {
            _traceThreshold = traceThreshold;
            _bonus = bonus;
        }
    }
}
