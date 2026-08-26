using Core.Services.Economy;
using Core.Services.Security;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Assemble le débit de Trace de la frame et l'applique à la jauge.
    ///
    /// Le calcul est réparti sur trois responsables, et pas par goût de la découpe :
    ///  - l'UpgradeManager tient les parts STATIQUES (Hardware possédés, dissipation des
    ///    Proxies), qui ne bougent qu'à l'achat et restent donc en cache ;
    ///  - le ScriptCycleRunner tient la part DYNAMIQUE, car lui seul sait quels cycles tournent
    ///    à cet instant — un Script ne laisse de trace que pendant un cycle actif ;
    ///  - ce ticker combine les deux et applique la réduction de prestige.
    ///
    /// Aucune allocation, aucune closure, aucun boxing : que des lectures de champs et de
    /// CurrentValue. C'est le standard des boucles de frame du projet.
    /// </summary>
    public class SimulationTicker : ITickable
    {
        /// <summary>
        /// Convertit une Trace par seconde en fraction de jauge par seconde. La jauge est
        /// normalisée entre 0 et 1 : 100 de Trace par seconde la remplissent en une seconde.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        private const float TraceToGaugeDivisor = 100f;

        private readonly UpgradeManager _upgradeManager;
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly PrestigeManager _prestigeManager;
        private readonly ThreatManager _threatManager;
        private readonly GameSessionManager _sessionManager;

        public SimulationTicker(
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            PrestigeManager prestigeManager,
            ThreatManager threatManager,
            GameSessionManager sessionManager)
        {
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _prestigeManager = prestigeManager;
            _threatManager = threatManager;
            _sessionManager = sessionManager;
        }

        public void Tick()
        {
            if (!_sessionManager.IsGameActive.Value) return;

            // TraceBrute = (Σ ScriptsActifs + Σ HardwarePossédés) × (1 − RéductionPrestige)
            //
            // La réduction s'applique AVANT la soustraction des Proxies : l'ordre compte, sinon
            // elle rognerait aussi la dissipation et les Proxies deviendraient moins efficaces
            // à mesure que le joueur progresse — exactement l'inverse de l'intention.
            float generated = _cycleRunner.ActiveScriptTracePerSecond
                            + _upgradeManager.HardwareTracePerSecond.CurrentValue;

            float brute = generated * _prestigeManager.TraceReductionMultiplier.CurrentValue;

            // DebitTrace = max(0, TraceBrute − Σ DissipationProxies)
            //
            // Les Proxies agissent sur le DÉBIT, jamais sur la jauge : un excédent de
            // dissipation ne fait pas redescendre la Trace déjà accumulée. Seuls le Bouton
            // d'Urgence et le wipe le peuvent — « le FBI n'oublie jamais, sauf si tu formates ».
            float debit = brute - _upgradeManager.ProxyDissipationPerSecond.CurrentValue;
            if (debit <= 0f) return;

            _threatManager.AddThreat((debit / TraceToGaugeDivisor) * Time.deltaTime);
        }
    }
}
