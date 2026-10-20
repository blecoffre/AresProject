using Core.Services.Platform;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Le temps d'une campagne simulée. Il sert à la fois de pas de simulation
    /// (<see cref="ITimeSource"/>) et d'heure « réelle » (<see cref="IRealtimeClock"/>) : en
    /// simulation il n'existe qu'une horloge, celle que le harnais fait avancer.
    /// </summary>
    public sealed class SimulatedClock : ITimeSource, IRealtimeClock
    {
        public float DeltaTime { get; private set; }

        public double Now { get; private set; }

        /// <summary>Un pas de simulation : les systèmes tickés ensuite liront ce delta.</summary>
        public void Advance(float seconds)
        {
            DeltaTime = seconds;
            Now += seconds;
        }
    }
}
