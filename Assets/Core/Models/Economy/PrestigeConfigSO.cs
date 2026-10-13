using UnityEngine;

namespace Core.Models.Economy
{
    /// <summary>
    /// Ce qu'un nœud de prestige apporte.
    ///
    /// ⚠️ <b>L'index est sérialisé dans les .asset générés.</b> Un type s'APPEND en fin de liste.
    /// En retirer un décale tous les suivants : ce n'est permis qu'en régénérant TOUT l'arbre
    /// dans la foulée (les trois outils de `Tools/Core`, dans l'ordre), ce qui a été fait le
    /// 2026-10-13 quand la refonte « Time Attack » a retiré la Furtivité, le Zéro-Day, le réveil
    /// et la barre d'espace. Une insertion au milieu sans régénération avait fait croire à
    /// P_EXPLOIT_CHARGES qu'il était un nœud d'automatisation, le 2026-08-30.
    /// </summary>
    public enum PrestigeBonusType
    {
        GlobalComputeMultiplier, // Multiplicateur des TFlops nettes (P_DATAM)

        /// <summary>
        /// Secondes AJOUTÉES à l'avance d'un Overclock (branche Overclock). S'appelait
        /// ClickPowerMultiplier quand l'Overclock multipliait une avance de base.
        /// </summary>
        OverclockBonus,

        CostMultiplierReduction, // Retranché du multiplicateur de coût de base (P_REFAC)
        StartingMoney,            // Argent de départ pour la prochaine Run
        StartingComputerPower,   // Botnets persistants
        UnlockEmergencyButton,    // Déblocage du Data Wiper
        SpecificUpgradeCostReduction, // Réduit le coût de base
        SpecificUpgradeYieldBoost,    // Augmente le rendement de base
        SpecificUpgradeTimeReduction, // Réduit le temps de cycle

        // Palier d'automatisation d'un Script. Seuls les Scripts relancent des cycles.
        SpecificUpgradeAutomationTresholdReduction,

        /// <summary>
        /// Blindage — S'AJOUTE au diviseur de la vitesse de l'A.M.I. :
        /// <c>V_AMI ÷ (1 + Σ Blindage)</c>. S'appelait TraceCapacityMultiplier quand il
        /// multipliait le plafond de la jauge, devenu fixe le 2026-10-13.
        ///
        /// La colonne vertébrale de la méta-progression : c'est le seul levier qui allonge une
        /// run sans rien dépenser pendant celle-ci.
        /// </summary>
        HardenBonus,

        /// <summary>
        /// Interception — multiplie le Wipe de tout Proxy acheté : <c>× (1 + Σ Interception)</c>.
        /// S'appelait ProxyEfficiencyMultiplier quand il multipliait une dissipation continue.
        /// </summary>
        InterceptBonus,

        // Extraction — multiplie le bonus des PALIERS d'exfiltration.
        //
        // La seule branche qui récompense la manière de jouer plutôt que la puissance brute.
        // Elle achète de l'appât, pas de la sécurité.
        CleanExitBonusMultiplier,

        /// Automatisation GLOBALE — abaisse le palier d'auto-relance de TOUS les Scripts d'un
        /// coup. Plus aucun nœud ne la porte depuis le retrait de P_AUTO_GLOBAL (2026-10-02).
        GlobalAutomationReduction,

        /// Extraction Haut Risque — ouvre les paliers d'extraction marqués « sous condition »,
        /// aujourd'hui le seul palier à 90 %. Déblocage booléen : seul le fait de posséder le
        /// nœud compte, sa magnitude n'a aucun sens.
        UnlockHighRiskExtraction,

        /// <summary>
        /// La racine de l'arbre, et rien d'autre. Aucun effet, aucune magnitude.
        /// </summary>
        Root
    }

    [CreateAssetMenu(fileName = "NewPrestigeConfig", menuName = "Core/Economy/Prestige Config")]
    public class PrestigeConfigSO : ScriptableObject
    {
        [Header("Identifiants & Lore")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField, TextArea] private string _displayDescriptionKey; // Clé de loc pour le lore

        [Header("Mécanique")]
        [SerializeField] private PrestigeBonusType _bonusType;
        [Tooltip("Si 1 = Achat unique (le niveau sera masqué dans l'UI)")]
        [SerializeField] private int _maxLevel = 1;

        [Header("Économie (Coût en CPU Cycles)")]
        [SerializeField] private double _baseCost = 1d;
        [SerializeField] private double _costMultiplier = 1.5d;

        [Header("Puissance du Bonus")]
        [Tooltip("La valeur ajoutée par niveau. Ex: 0.1 pour 10%")]
        [SerializeField] private float _bonusPerLevel = 0.1f;

        [Header("Toile d'Araignée (UI)")]
        [SerializeField] private Vector2 _uiPosition;

        [Tooltip("Le nœud précédent obligatoire et le niveau à y atteindre. " +
                 "Laisser le nœud vide si c'est le point de départ d'une branche.")]
        [SerializeField] private PrestigeRequirement _requirement;

        [Header("Ciblage Spécifique")]
        [Tooltip("L'ID de l'amélioration d'économie ciblée (laisser vide si le bonus est global)")]
        [SerializeField] private string _targetUpgradeId;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string DisplayDescriptionKey => _displayDescriptionKey;
        public PrestigeBonusType BonusType => _bonusType;
        public int MaxLevel => _maxLevel;
        public double BaseCost => _baseCost;
        public double CostMultiplier => _costMultiplier;

        /// <summary>
        /// Coût du PROCHAIN niveau de ce nœud, en CPU Cycles.
        ///
        /// <b>Plat depuis le 2026-09-12 : le niveau ne renchérit plus rien.</b> Le coût vaut
        /// <see cref="BaseCost"/>, réglé à 1 dans les données — chaque niveau de chaque nœud
        /// coûte donc un point, et le joueur n'a plus qu'à choisir lequel.
        ///
        /// Ce que ça supprime. La courbe <c>BaseCost × CostMultiplier^niveau</c> était recopiée
        /// dans QUATRE fichiers qui n'en savaient rien les uns des autres, et l'arbre s'était
        /// déjà retrouvé 152 fois trop cher pour une erreur sur un seul de ces termes. Elle
        /// créait surtout un piège invisible : mesuré, un joueur qui achetait naturellement le
        /// nœud le moins cher d'abord jouait cent fois moins bien qu'un joueur qui les évitait,
        /// parce que 145 nœuds affichés au même prix n'avaient pas du tout la même valeur.
        ///
        /// <paramref name="currentLevel"/> est conservé dans la signature : c'est le seul
        /// endroit à toucher si une courbe devait réapparaître un jour.
        /// </summary>
        public double GetCostAtLevel(int currentLevel)
        {
            return _baseCost;
        }
        public float BonusPerLevel => _bonusPerLevel;
        public Vector2 UiPosition => _uiPosition;
        /// <summary>
        /// La condition d'ouverture : quel parent, et à quel niveau. Remplace depuis le
        /// 2026-09-09 la simple référence <c>_prerequisite</c>, qui ne pouvait exprimer que
        /// « posséder le parent » et ouvrait donc une branche entière au premier rang acheté.
        /// </summary>
        public PrestigeRequirement Requirement => _requirement;
        public string TargetUpgradeId => _targetUpgradeId;
    }
}