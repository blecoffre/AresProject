using System;

namespace Core.Models.Economy
{
    /// <summary>
    /// Ce qu'un palier modifie quand le joueur l'atteint.
    /// </summary>
    public enum MilestoneEffect
    {
        /// <summary>Multiplie le versement du cycle. Facteur 2 = versement doublé.</summary>
        YieldMultiplier,

        /// <summary>Multiplie la durée du cycle. Facteur 0.75 = cycle 25 % plus court.</summary>
        DurationMultiplier,

        /// <summary>
        /// Multiplie les secondes de survie qu'un Proxy rend à chaque achat. Réservé aux Proxies :
        /// ils n'ont ni rendement ni cycle, c'est leur seule façon de progresser par niveau.
        ///
        /// S'appelait TraceMultiplier jusqu'au 2026-10-13, quand il amplifiait aussi la Trace
        /// générée des Scripts et du Hardware. L'index est conservé : il est sérialisé dans les
        /// .asset générés.
        /// </summary>
        WipeMultiplier
    }

    /// <summary>
    /// Un palier de progression atteint à un niveau donné.
    ///
    /// Les effets sont MULTIPLICATIFS et cumulatifs : trois paliers ×2 sur le versement donnent ×8.
    /// C'est ce qui permet à un palier lointain de rester spectaculaire au lieu d'être noyé
    /// par la croissance linéaire du niveau.
    /// </summary>
    [Serializable]
    public struct UpgradeMilestone
    {
        /// <summary>Niveau à partir duquel le palier est acquis (et le reste).</summary>
        public int Level;

        public MilestoneEffect Effect;

        /// <summary>Multiplicateur appliqué. &gt; 1 pour un versement, &lt; 1 pour une durée.</summary>
        public float Factor;
    }
}
