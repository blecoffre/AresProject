namespace Core.Services.Localization
{
    /// <summary>
    /// Source unique de la convention de nommage des clés de localisation.
    ///
    /// La règle du projet est qu'une clé se DÉRIVE de l'id, jamais qu'elle s'invente : c'est ce
    /// qui permet aux outils d'éditeur de les générer et au LocalizationAnalyzerWindow de les
    /// vérifier. Recopier le gabarit « UPG_ + id + _NAME » dans chaque appelant reviendrait à
    /// avoir trois conventions qui divergeraient au premier renommage — d'où ce point unique.
    ///
    /// Les concaténations allouent : ces méthodes appartiennent à l'initialisation et au
    /// rafraîchissement d'UI, jamais à une boucle de frame.
    /// </summary>
    public static class LocalizationKeys
    {
        public static string UpgradeName(string upgradeId) => "UPG_" + upgradeId + "_NAME";

        public static string UpgradeDescription(string upgradeId) => "UPG_" + upgradeId + "_DESC";

        public static string PrestigeName(string prestigeId) => "PRESTIGE_" + prestigeId + "_NAME";

        public static string PrestigeDescription(string prestigeId) => "PRESTIGE_" + prestigeId + "_DESC";

        /// <summary>
        /// Gabarits des nœuds de prestige « spécifiques ». Ces nœuds n'ont pas de nom propre :
        /// leur libellé se compose d'un gabarit et du nom de l'upgrade ciblée, ce qui remplace
        /// 324 entrées de traduction quasi identiques par ces six-là. Le {0} reçoit le nom résolu
        /// de l'upgrade.
        ///
        /// Constantes et non concaténations : ces clés sont fixes, il n'y a rien à dériver.
        /// </summary>
        public const string PrestigeSpecificCostName = "PRESTIGE_SPECIFIC_COST_NAME";
        public const string PrestigeSpecificCostDescription = "PRESTIGE_SPECIFIC_COST_DESC";

        public const string PrestigeSpecificYieldName = "PRESTIGE_SPECIFIC_YIELD_NAME";
        public const string PrestigeSpecificYieldDescription = "PRESTIGE_SPECIFIC_YIELD_DESC";

        public const string PrestigeSpecificTimeName = "PRESTIGE_SPECIFIC_TIME_NAME";
        public const string PrestigeSpecificTimeDescription = "PRESTIGE_SPECIFIC_TIME_DESC";

        public const string PrestigeAutomationName = "PRESTIGE_AUTOMATION_NAME";
        public const string PrestigeAutomationDescription = "PRESTIGE_AUTOMATION_DESC";
    }
}
