using Core.Models.Economy;
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
        private readonly UpgradeManager _upgradeManager;
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly PrestigeManager _prestigeManager;
        private readonly ThreatManager _threatManager;
        private readonly GameSessionManager _sessionManager;
        private readonly GhostCacheSystem _ghostCache;
        private readonly BalancingConfigSO _balancing;

        public SimulationTicker(
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            PrestigeManager prestigeManager,
            ThreatManager threatManager,
            GameSessionManager sessionManager,
            GhostCacheSystem ghostCache,
            BalancingConfigSO balancing)
        {
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _prestigeManager = prestigeManager;
            _threatManager = threatManager;
            _sessionManager = sessionManager;
            _ghostCache = ghostCache;
            _balancing = balancing;
        }

        public void Tick()
        {
            if (!_sessionManager.IsGameActive.Value) return;

            float deltaTime = Time.deltaTime;

            // L'Exploit s'écoule AVANT le calcul du débit, dans la même frame : sinon la frame
            // où il expire éteindrait encore les Proxies, et celle où il démarre les laisserait
            // dissiper une dernière fois.
            _ghostCache.TickOverdrive(deltaTime);

            // TraceBrute = (Σ ScriptsActifs + Σ HardwarePossédés) × (1 − RéductionPrestige)
            //
            // La réduction s'applique AVANT la soustraction des Proxies : l'ordre compte, sinon
            // elle rognerait aussi la dissipation et les Proxies deviendraient moins efficaces
            // à mesure que le joueur progresse — exactement l'inverse de l'intention.
            float generated = _cycleRunner.ActiveScriptTracePerSecond
                            + _upgradeManager.HardwareTracePerSecond.CurrentValue;

            float brute = generated * _prestigeManager.TraceReductionMultiplier.CurrentValue;

            // Pendant le Zéro-Day Exploit, la contrepartie est DOUBLE : tous les Proxies
            // s'éteignent, et la génération brute est elle-même multipliée. Le joueur produit
            // cinquante fois plus, mais il est à découvert et la Trace le rattrape d'autant.
            //
            // Le facteur s'applique APRÈS la réduction de prestige, ce qui est sans effet
            // arithmétique — la multiplication commute — mais dit la bonne chose : la réduction
            // passive protège toujours proportionnellement, y compris pendant l'Exploit.
            bool isOverdrive = _ghostCache.IsOverdriveActive.CurrentValue;
            if (isOverdrive) brute *= _ghostCache.EffectiveTraceMultiplier;

            float dissipation = isOverdrive
                ? 0f
                : _upgradeManager.ProxyDissipationPerSecond.CurrentValue;

            // DebitTrace = max(0, TraceBrute − Σ DissipationProxies)
            //
            // Les Proxies agissent sur le DÉBIT, jamais sur la jauge : un excédent de
            // dissipation ne fait pas redescendre la Trace déjà accumulée. Seuls le Bouton
            // d'Urgence et le wipe le peuvent — « le FBI n'oublie jamais, sauf si tu formates ».
            float debit = brute - dissipation;

            if (debit < 0f)
            {
                // Excédent strict : la dissipation dépasse la génération. C'est cette part-là,
                // autrefois jetée, que le Ghost Cache capte. Le test est « < 0 » et non « <= 0 »
                // à dessein : une partie sans aucun générateur ni Proxie donne un débit nul,
                // et charger l'Exploit en ne faisant rigoureusement rien n'aurait aucun sens.
                _ghostCache.Accumulate(deltaTime);
                return;
            }

            if (debit == 0f) return;

            _threatManager.AddThreat((debit / _balancing.TraceToGaugeDivisor) * deltaTime);
        }
    }
}
