using UnityEngine;

namespace Core.Services.Platform
{
    /// <summary>
    /// D'où les boucles de simulation tirent leur pas de temps.
    ///
    /// Quatre systèmes lisaient <c>Time.deltaTime</c> en dur — <see cref="Core.Services.Economy.ScriptCycleRunner"/>,
    /// <see cref="Core.Services.Simulation.SimulationTicker"/>, <see cref="Core.Services.Simulation.GameSessionManager"/>
    /// et <see cref="Core.Services.Security.EmergencyProtocolSystem"/>. Cela liait la simulation à
    /// la boucle de frame d'Unity, avec deux conséquences.
    ///
    /// D'abord, <b>rien n'était mesurable hors Play Mode</b> : évaluer un rythme de campagne de dix
    /// heures demandait dix heures, ou une réplique du modèle écrite à côté — c'est ce qu'était le
    /// simulateur Python, avec le défaut qu'une réplique dérive de l'original qu'elle imite.
    /// Ensuite, Unity gèle sa boucle quand sa fenêtre perd le focus : toute mesure exigeait de ne
    /// pas toucher à la machine pendant qu'elle tournait.
    ///
    /// L'abstraction lève les deux : en production <see cref="UnityTimeSource"/> rend exactement
    /// <c>Time.deltaTime</c> — aucun changement de comportement — et un harnais d'éditeur peut
    /// substituer une source virtuelle pour dérouler une campagne entière en quelques secondes,
    /// sans scène, sans UI et de façon déterministe. Les quatre systèmes deviennent au passage
    /// testables unitairement.
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>Secondes écoulées depuis le pas précédent.</summary>
        float DeltaTime { get; }
    }

    /// <summary>
    /// La source de production : le temps d'Unity, sans interprétation.
    ///
    /// <c>Time.deltaTime</c> et non <c>unscaledDeltaTime</c>, à dessein : la simulation doit suivre
    /// <c>Time.timeScale</c>, ce qui laisse la porte ouverte à une pause ou à un mode accéléré
    /// observable en Play Mode.
    /// </summary>
    public sealed class UnityTimeSource : ITimeSource
    {
        public float DeltaTime => Time.deltaTime;
    }
}
