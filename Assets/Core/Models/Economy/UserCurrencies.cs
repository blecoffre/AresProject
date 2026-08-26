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

        private readonly ReactiveProperty<double> _totalMoneyGenerated;
        public ReadOnlyReactiveProperty<double> TotalMoneyGenerated => _totalMoneyGenerated;

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
            _totalCpuCyclesGenerated = new ReactiveProperty<double>(0d);
            _totalNumberOfDetections = new ReactiveProperty<int>(0);
        }

        public void AddMoney(double amount)
        {
            if (amount <= 0) return;

            Money.Add(amount);
            _totalMoneyGenerated.Value += amount;
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

        public double CalculatePendingCpuCycles()
        {
            // Note : le prestige se base toujours sur l'argent total généré.
            // C'est un bon choix si l'argent est la ressource la plus dure à avoir.
            return Math.Floor(Math.Sqrt(_totalMoneyGenerated.Value / 1000d));
        }

        public void LoadFromSave(
            double savedMoney,
            double savedCpuCycles,
            double totalMoney,
            double totalCpuCycles,
            int totalDetections)
        {
            Money.Reset(savedMoney);
            CpuCycles.Reset(savedCpuCycles);

            _totalMoneyGenerated.Value = totalMoney;
            _totalCpuCyclesGenerated.Value = totalCpuCycles;
            _totalNumberOfDetections.Value = totalDetections;
        }

        public void Dispose()
        {
            Money.Dispose();
            CpuCycles.Dispose();

            _totalMoneyGenerated.Dispose();
            _totalCpuCyclesGenerated.Dispose();
            _totalNumberOfDetections.Dispose();
        }
    }
}
