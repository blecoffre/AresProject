namespace Core.Models.Economy
{
    /// <summary>
    /// Bonus de prestige visant UNE upgrade précise, cumulés tous rangs et tous nœuds confondus.
    ///
    /// Struct et non classe : le PrestigeManager en tient un par upgrade ciblée et reconstruit
    /// la table entière à chaque recalcul. Des classes obligeraient à allouer ~45 objets à chaque
    /// achat de nœud.
    ///
    /// La composition est ADDITIVE, comme pour les six bonus globaux : cinq rangs à 0,1 donnent
    /// 0,5, soit −50 % de coût, ×1,5 de rendement, −50 % de durée. Une seule règle de composition
    /// dans tout le jeu, c'est ce qui la rend prévisible pour le joueur.
    /// </summary>
    public readonly struct SpecificUpgradeBonuses
    {
        /// <summary>Fraction retirée au coût de base. 0,5 = coût divisé par deux.</summary>
        public readonly float CostReduction;

        /// <summary>Fraction ajoutée au rendement de base. 0,5 = rendement × 1,5.</summary>
        public readonly float YieldBoost;

        /// <summary>Fraction retirée à la durée de cycle de base. 0,5 = cycle deux fois plus court.</summary>
        public readonly float TimeReduction;

        /// <summary>
        /// NIVEAUX retirés au seuil d'automatisation — et non une fraction, contrairement aux
        /// trois autres.
        ///
        /// C'est la seule exception à la règle du « tout en fraction », et elle est assumée :
        /// un seuil d'automatisation est un numéro de niveau, pas un pourcentage. 3 ici veut dire
        /// « ce Script s'automatise trois niveaux plus tôt ». Stocké en float par uniformité de
        /// cumul ; c'est <see cref="UpgradeModel"/> qui l'arrondit au moment de s'en servir.
        ///
        /// <b>Ne concerne que les Scripts</b> : eux seuls relancent des cycles, donc eux seuls
        /// ont quelque chose à automatiser.
        /// </summary>
        public readonly float AutomationThresholdReduction;

        public SpecificUpgradeBonuses(
            float costReduction,
            float yieldBoost,
            float timeReduction,
            float automationThresholdReduction)
        {
            CostReduction = costReduction;
            YieldBoost = yieldBoost;
            TimeReduction = timeReduction;
            AutomationThresholdReduction = automationThresholdReduction;
        }

        public SpecificUpgradeBonuses WithCostReduction(float value) =>
            new SpecificUpgradeBonuses(value, YieldBoost, TimeReduction, AutomationThresholdReduction);

        public SpecificUpgradeBonuses WithYieldBoost(float value) =>
            new SpecificUpgradeBonuses(CostReduction, value, TimeReduction, AutomationThresholdReduction);

        public SpecificUpgradeBonuses WithTimeReduction(float value) =>
            new SpecificUpgradeBonuses(CostReduction, YieldBoost, value, AutomationThresholdReduction);

        public SpecificUpgradeBonuses WithAutomationThresholdReduction(float value) =>
            new SpecificUpgradeBonuses(CostReduction, YieldBoost, TimeReduction, value);

        public static readonly SpecificUpgradeBonuses None = new SpecificUpgradeBonuses(0f, 0f, 0f, 0f);
    }
}
