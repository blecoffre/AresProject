using R3;
using System;

namespace Core.Models.Economy
{
    /// <summary>
    /// Les monnaies que le joueur accumule et dépense.
    ///
    /// Les TFlops n'en font PAS partie, volontairement : ce ne sont pas un stock mais une
    /// capacité dérivée du parc Hardware possédé, recalculée par l'UpgradeManager et exposée
    /// par sa propriété TotalTFlops. Les garder ici sous forme de Currency alimentée par Add()
    /// laissait croire à une ressource qu'on accumule et qu'on dépense, ce qu'elles ne sont pas.
    /// </summary>
    public class UserCurrencies : IDisposable
    {
        public Currency Money { get; }
        public Currency CpuCycles { get; }     // La monnaie de Prestige

        /// <summary>Argent généré depuis le début de la partie. Statistique, sans effet de jeu.</summary>
        private readonly ReactiveProperty<double> _totalMoneyGenerated;
        public ReadOnlyReactiveProperty<double> TotalMoneyGenerated => _totalMoneyGenerated;

        /// <summary>
        /// Argent généré depuis le début de la RUN en cours. C'est lui, et lui seul, qui détermine
        /// le gain de CPU Cycles en fin de run.
        ///
        /// Le distinguer du cumul à vie n'est pas un détail : sur le compteur à vie, chaque run
        /// rapportait mécaniquement au moins autant que la précédente, sans rien faire. La boucle
        /// de méta-progression n'avait plus aucune tension, et flirter avec 95 % de Trace ne
        /// rapportait pas plus que mourir tôt.
        /// </summary>
        private readonly ReactiveProperty<double> _runMoneyGenerated;
        public ReadOnlyReactiveProperty<double> RunMoneyGenerated => _runMoneyGenerated;

        private readonly ReactiveProperty<double> _totalCpuCyclesGenerated;
        public ReadOnlyReactiveProperty<double> TotalCpuCyclesGenerated => _totalCpuCyclesGenerated;

        private readonly ReactiveProperty<int> _totalNumberOfDetections;
        public ReadOnlyReactiveProperty<int> TotalNumberOfDetections => _totalNumberOfDetections;

        public UserCurrencies()
        {
            Money = new Currency();
            CpuCycles = new Currency();

            // NOTE : La Trace a été supprimée d'ici, elle vit désormais dans le ThreatManager !

            _totalMoneyGenerated = new ReactiveProperty<double>(0d);
            _runMoneyGenerated = new ReactiveProperty<double>(0d);
            _totalCpuCyclesGenerated = new ReactiveProperty<double>(0d);
            _totalNumberOfDetections = new ReactiveProperty<int>(0);
        }

        public void AddMoney(double amount)
        {
            if (amount <= 0) return;

            Money.Add(amount);
            _totalMoneyGenerated.Value += amount;
            _runMoneyGenerated.Value += amount;
        }

        public void AddCpuCycles(double amount)
        {
            if (amount <= 0) return;

            CpuCycles.Add(amount);
            _totalCpuCyclesGenerated.Value += amount;
        }

        public void RecordDetection()
        {
            _totalNumberOfDetections.Value += 1;
        }

        /// <summary>
        /// CPU Cycles que rapporterait une fin de run maintenant. Basé sur l'argent de la RUN,
        /// jamais sur le cumul à vie — décision de game design du 2026-08-26.
        /// </summary>
        public double CalculatePendingCpuCycles()
        {
            return Math.Floor(Math.Sqrt(_runMoneyGenerated.Value / 1000d));
        }

        /// <summary>
        /// Remet à zéro ce qui appartient à la run. Appelé par le wipe, APRÈS que le gain de
        /// prestige a été calculé — l'ordre compte, sinon la run ne rapporterait jamais rien.
        /// </summary>
        public void ResetRunCounters()
        {
            _runMoneyGenerated.Value = 0d;
        }

        public void LoadFromSave(
            double savedMoney,
            double savedCpuCycles,
            double totalMoney,
            double runMoney,
            double totalCpuCycles,
            int totalDetections)
        {
            Money.Reset(savedMoney);
            CpuCycles.Reset(savedCpuCycles);

            _totalMoneyGenerated.Value = totalMoney;
            _runMoneyGenerated.Value = runMoney;
            _totalCpuCyclesGenerated.Value = totalCpuCycles;
            _totalNumberOfDetections.Value = totalDetections;
        }

        public void Dispose()
        {
            Money.Dispose();
            CpuCycles.Dispose();

            _totalMoneyGenerated.Dispose();
            _runMoneyGenerated.Dispose();
            _totalCpuCyclesGenerated.Dispose();
            _totalNumberOfDetections.Dispose();
        }
    }
}
