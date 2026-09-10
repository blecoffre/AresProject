using Core.Models.Economy;

namespace Core.Editor.Balance
{
    /// <summary>Ce que la stratégie sait de la run au moment de décider si elle sort.</summary>
    public readonly struct RunProgress
    {
        /// <summary>Secondes de jeu écoulées depuis le début de la run.</summary>
        public readonly float ElapsedSeconds;

        /// <summary>Datas générées sur la run.</summary>
        public readonly double RunMoney;

        /// <summary>
        /// Croissance des Datas depuis le dernier repère de plateau. Sous ~1, la run ne rapporte
        /// plus assez pour mériter le risque : les CPU Cycles allant en racine carrée des Datas,
        /// mieux vaut repartir à neuf avec les bonus de prestige que gratter un plateau.
        /// </summary>
        public readonly double GrowthSinceMark;

        /// <summary>Jauge de Trace, de 0 à 1.</summary>
        public readonly float TraceFraction;

        /// <summary>CPU Cycles que la run rapporterait si elle s'arrêtait maintenant.</summary>
        public readonly double PendingCycles;

        public RunProgress(float elapsed, double runMoney, double growth, float traceFraction, double pending)
        {
            ElapsedSeconds = elapsed;
            RunMoney = runMoney;
            GrowthSinceMark = growth;
            TraceFraction = traceFraction;
            PendingCycles = pending;
        }
    }

    /// <summary>
    /// Le joueur simulé. Interchangeable à dessein : c'est en COMPARANT deux postures dans les
    /// mêmes conditions qu'on répond à « est-ce que se défendre vaut le coup ? ». Un agent unique
    /// mesure un rythme, il ne tranche pas un arbitrage.
    /// </summary>
    public interface ISimulationStrategy
    {
        /// <summary>Libellé du tableau de résultats. Outil d'éditeur : pas de clé de localisation.</summary>
        string Name { get; }

        /// <summary>Achats pendant la run. Appelé aux points de décision, pas à chaque tick.</summary>
        void OnDecisionPoint(SimulationHarness harness);

        /// <summary>Le joueur exfiltre-t-il maintenant ?</summary>
        bool ShouldExfiltrate(SimulationHarness harness, in RunProgress progress);

        /// <summary>Achats de prestige, entre deux runs, fenêtre déjà ouverte.</summary>
        void SpendCpuCycles(SimulationHarness harness);
    }
}
