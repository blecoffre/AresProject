using R3;
using System;

namespace Core.Models.Economy
{
    public class UserCurrencies : IDisposable
    {
        public Currency Money { get; }
        public Currency ComputerPower { get; } // Le Téraflops / Hashrate
        public Currency CpuCycles { get; }     // La monnaie de Prestige

        private readonly ReactiveProperty<double> _totalMoneyGenerated;
        public ReadOnlyReactiveProperty<double> TotalMoneyGenerated => _totalMoneyGenerated;

        private readonly ReactiveProperty<double> _totalComputerPowerAcquired;
        public ReadOnlyReactiveProperty<double> TotalComputerPowerAcquired => _totalComputerPowerAcquired;

        private readonly ReactiveProperty<double> _totalCpuCyclesGenerated;
        public ReadOnlyReactiveProperty<double> TotalCpuCyclesGenerated => _totalCpuCyclesGenerated;

        private readonly ReactiveProperty<int> _totalNumberOfDetections;
        public ReadOnlyReactiveProperty<int> TotalNumberOfDetections => _totalNumberOfDetections;



        public UserCurrencies()
        {


            Money = new Currency();
            ComputerPower = new Currency(); // CORRECTION : Initialisation ajoutée
            CpuCycles = new Currency();

            // NOTE : La Trace a été supprimée d'ici, elle vit désormais dans le ThreatManager !

            _totalMoneyGenerated = new ReactiveProperty<double>(0d);
            _totalComputerPowerAcquired = new ReactiveProperty<double>(0d);
            _totalCpuCyclesGenerated = new ReactiveProperty<double>(0d);
            _totalNumberOfDetections = new ReactiveProperty<int>(0);
        }

        public void AddMoney(double amount)
        {
            if (amount <= 0) return;

            Money.Add(amount);
            _totalMoneyGenerated.Value += amount;
        }

        public void AddComputerPower(double amount)
        {
            if (amount <= 0) return;

            ComputerPower.Add(amount); // CORRECTION : Ciblait "Money" avant
            _totalComputerPowerAcquired.Value += amount;
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

        public void LoadFromSave(double savedMoney, double savedComputerPower, double savedCpuCycles, double totalMoney, double totalComputerPower, double totalCpuCycles, int totalDetections)
        {
            Money.Reset(savedMoney);
            ComputerPower.Reset(savedComputerPower); // CORRECTION : Ajout de la restauration
            CpuCycles.Reset(savedCpuCycles);

            _totalMoneyGenerated.Value = totalMoney;
            _totalComputerPowerAcquired.Value = totalComputerPower;
            _totalCpuCyclesGenerated.Value = totalCpuCycles;
            _totalNumberOfDetections.Value = totalDetections;
        }

        public void Dispose()
        {
            Money.Dispose();
            ComputerPower.Dispose(); // CORRECTION : Ajout du Dispose
            CpuCycles.Dispose();

            _totalMoneyGenerated.Dispose();
            _totalComputerPowerAcquired.Dispose();
            _totalCpuCyclesGenerated.Dispose();
            _totalNumberOfDetections.Dispose();


        }
    }
}