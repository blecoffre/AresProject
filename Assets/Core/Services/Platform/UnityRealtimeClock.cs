using UnityEngine;

namespace Core.Services.Platform
{
    /// <summary>L'horloge réelle du jeu : le temps depuis le lancement, pause comprise. Sans état.</summary>
    public sealed class UnityRealtimeClock : IRealtimeClock
    {
        public double Now => Time.realtimeSinceStartupAsDouble;
    }
}
