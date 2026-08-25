using Core.Models.Economy;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Core.Services.Economy
{
    /// <summary>
    /// Gère la production passive de ressources (Le cœur du mode Idle).
    /// </summary>
    public class AutomationSystem : IDisposable
    {
        private readonly UserCurrencies _userCurrencies;
        private readonly UpgradeManager _upgradeManager;
        private readonly Core.Services.Simulation.GameSessionManager _sessionManager;

        private CancellationTokenSource _cts;
        private const float TickIntervalSeconds = 1.0f; // Fréquence de la boucle (1 tick par seconde)

        public AutomationSystem(UserCurrencies userCurrencies, UpgradeManager upgradeManager, Core.Services.Simulation.GameSessionManager sessionManager)
        {
            _userCurrencies = userCurrencies;
            _upgradeManager = upgradeManager;
            _sessionManager = sessionManager;
        }

        public void StartAutomation()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _ = AutomationLoopAsync(_cts.Token);
        }

        private async UniTaskVoid AutomationLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // Attente asynchrone sans allocation de mémoire superflue
                await UniTask.Delay(TimeSpan.FromSeconds(TickIntervalSeconds), cancellationToken: ct);

                if (!_sessionManager.IsGameActive.Value) continue;

                double totalDatasPerSecond = _upgradeManager.TotalMoneyYieldPerSecond.CurrentValue;

                // Application des ressources dans le modèle global si la production est active
                if (totalDatasPerSecond > 0d)
                {
                    _userCurrencies.AddMoney(totalDatasPerSecond);
                }
            }
        }

        /// <summary>
        /// Permet à l'OverclockSystem de forcer un tick de production manuelle.
        /// </summary>
        public void ProcessManualTick(double multiplier)
        {
            double instantDatas = _upgradeManager.TotalMoneyYieldPerSecond.CurrentValue * multiplier;
            if (instantDatas > 0d) _userCurrencies.AddMoney(instantDatas);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}