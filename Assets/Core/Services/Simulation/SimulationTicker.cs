using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Platform;
using Core.Services.Security;
using VContainer.Unity;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Fait avancer le compte à rebours de l'A.M.I., frame par frame.
    ///
    /// Depuis la refonte « Time Attack » du 2026-10-13, la Trace n'est plus un débit assemblé
    /// ici à partir des Scripts, du Hardware et des Proxies : c'est une vitesse unique, que le
    /// ThreatManager tient en cache. Ce ticker ne décide plus que deux choses — la run est-elle
    /// en cours, et la traque est-elle armée.
    ///
    /// Il vit dans le scope de scène, et c'est délibéré : la simulation ne tourne que lorsque la
    /// GameScene existe. Le ThreatManager ne peut pas se faire avancer lui-même — il lui faudrait
    /// le GameSessionManager, qui dépend déjà de lui.
    ///
    /// Aucune allocation, aucune closure, aucun boxing : que des lectures de CurrentValue.
    /// </summary>
    public class SimulationTicker : ITickable
    {
        private readonly UserCurrencies _currencies;
        private readonly UpgradeManager _upgradeManager;
        private readonly ThreatManager _threatManager;
        private readonly GameSessionManager _sessionManager;
        private readonly ITimeSource _time;

        public SimulationTicker(
            UserCurrencies currencies,
            UpgradeManager upgradeManager,
            ThreatManager threatManager,
            GameSessionManager sessionManager,
            ITimeSource time)
        {
            _currencies = currencies;
            _upgradeManager = upgradeManager;
            _threatManager = threatManager;
            _sessionManager = sessionManager;
            _time = time;
        }

        public void Tick()
        {
            if (!_sessionManager.IsGameActive.CurrentValue) return;

            // ⚠️ La traque ne démarre QUE lorsque le joueur s'est manifesté (corrigé le
            // 2026-09-21 sur retour de jeu, conservé par la refonte) : avoir volé sa première
            // Data, ou posséder du Hardware. Sans cette garde, la jauge montait avant le moindre
            // achat — « je suis puni sans avoir commencé à jouer ». L'A.M.I. cherche un intrus,
            // pas une machine éteinte.
            //
            // Aucune des deux conditions ne redescend pendant une run : la traque ne clignote pas.
            bool isHunted = _currencies.RunMoneyGenerated.CurrentValue > 0d
                         || _upgradeManager.GrossTFlops.CurrentValue > 0d;

            if (!isHunted) return;

            _threatManager.Advance(_time.DeltaTime);
        }
    }
}
