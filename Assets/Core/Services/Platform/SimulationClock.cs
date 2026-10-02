using System;
using UnityEngine;

namespace Core.Services.Platform
{
    /// <summary>
    /// Les raisons pour lesquelles la simulation peut être retenue. Des drapeaux et non un
    /// booléen : plusieurs raisons peuvent se superposer, et chacune ne lève que la sienne. Un
    /// simple booléen laisserait le menu de pause relancer une partie que le chargement de
    /// scène croyait encore figée.
    /// </summary>
    [Flags]
    public enum SimulationHold
    {
        None = 0,

        /// <summary>
        /// Aucune GameScene n'est chargée : démarrage, menu principal, transition de scène.
        /// La simulation n'a le droit de tourner que sous les yeux du joueur — sinon rester sur
        /// le menu rapporterait de l'argent sans que la Trace ne monte jamais.
        /// </summary>
        OutOfGame = 1 << 0,

        /// <summary>Le menu de pause est ouvert en pleine partie.</summary>
        PauseMenu = 1 << 1,
    }

    /// <summary>Le pilotage de la pause, séparé de la lecture du temps.</summary>
    public interface ISimulationClock
    {
        /// <summary>Vrai si aucune raison ne retient la simulation.</summary>
        bool IsRunning { get; }

        /// <summary>Pose une raison de retenue. Idempotent.</summary>
        void Hold(SimulationHold reason);

        /// <summary>Lève une raison de retenue, et elle seule. Idempotent.</summary>
        void Release(SimulationHold reason);
    }

    /// <summary>
    /// La source de temps de production : <c>Time.deltaTime</c>, ou zéro tant qu'une raison
    /// retient la partie.
    ///
    /// <b>Pourquoi pas <c>Time.timeScale = 0</c>.</b> Il gèlerait tout ce qui lit le temps
    /// échelonné, animations du menu de pause comprises, et lierait de nouveau la simulation au
    /// rendu — ce que la politique de cadence interdit. Ici, seul ce qui passe par
    /// <see cref="ITimeSource"/> s'arrête, c'est-à-dire exactement la simulation, et rien d'autre.
    ///
    /// Démarre retenue (<see cref="SimulationHold.OutOfGame"/>) : c'est l'arrivée de la GameScene
    /// qui lève la retenue, pas le démarrage de l'application.
    /// </summary>
    public sealed class SimulationClock : ITimeSource, ISimulationClock
    {
        private SimulationHold _holds = SimulationHold.OutOfGame;

        public float DeltaTime => _holds == SimulationHold.None ? Time.deltaTime : 0f;

        public bool IsRunning => _holds == SimulationHold.None;

        public void Hold(SimulationHold reason) => _holds |= reason;

        public void Release(SimulationHold reason) => _holds &= ~reason;
    }
}
