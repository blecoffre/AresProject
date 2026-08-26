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
    }
}
