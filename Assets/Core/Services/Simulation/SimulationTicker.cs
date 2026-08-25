using Core.Services.Economy;
using Core.Services.Security;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Moteur central du jeu. Implémente ITickable de VContainer pour s'exécuter à chaque frame
    /// sans avoir besoin d'hériter de MonoBehaviour.
    /// </summary>
    public class SimulationTicker : ITickable
    {
        private readonly UpgradeManager _upgradeManager;
        private readonly ThreatManager _threatManager;
        private readonly GameSessionManager _sessionManager;

        public SimulationTicker(UpgradeManager upgradeManager, ThreatManager threatManager, GameSessionManager sessionManager)
        {
            _upgradeManager = upgradeManager;
            _threatManager = threatManager;
            _sessionManager = sessionManager;
        }

        public void Tick()
        {
            if (!_sessionManager.IsGameActive.Value) return;

            // 1. On récupère la trace générée par seconde
            float currentTraceRate = _upgradeManager.TotalTracePerSecond.CurrentValue;

            // 2. On l'applique lissée sur le temps de la frame
            if (currentTraceRate > 0f)
            {
                _threatManager.AddThreat((currentTraceRate / 100f) * Time.deltaTime);
            }
        }
    }
}