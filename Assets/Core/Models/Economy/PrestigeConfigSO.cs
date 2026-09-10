using UnityEngine;

namespace Core.Models.Economy
{
    public enum PrestigeBonusType
    {
        GlobalComputeMultiplier, // Multiplicateur global de TFlops
        TraceReduction,          // Réduction passive de la Trace
        ClickPowerMultiplier,    // Puissance du clic manuel
        CostMultiplierReduction, // Réduction de l'inflation des coûts (M)
        StartingMoney,            // Argent de départ pour la prochaine Run
        StartingComputerPower,   // Botnets persistants
        UnlockEmergencyButton,    // Déblocage du système d'urgence
        SpecificUpgradeCostReduction, // Réduit le coût de base
        SpecificUpgradeYieldBoost,    // Augmente le rendement de base
        SpecificUpgradeTimeReduction, // Réduit le temps de cycle

        // Zéro-Day Exploit. Appendés en fin d'enum à dessein : l'index est sérialisé dans les
        // .asset générés, une insertion au milieu redéfinirait silencieusement tous les nœuds.
        UnlockExploitCharges,         // Nombre de charges de Ghost Cache stockables (0 = verrouillé)
        ExploitYieldBoost,            // Majore le multiplicateur de rendement de l'Exploit
        ExploitTracePenaltyReduction, // Allège le malus de Trace subi pendant l'Exploit

        // Le clic d'Overclock réveille les Scripts à l'arrêt. Nœud tardif : galérer au lancement
        // manuel fait partie de l'expérience pendant une bonne partie de la partie.
        OverclockWakesScripts,

        // Palier d'automatisation des Scripts. APPENDÉ EN FIN, comme tous les autres :
        // ce type avait été inséré après SpecificUpgradeTimeReduction, ce qui a décalé de un
        // l'index de TOUTE la famille Zéro-Day dans les .asset déjà générés. Symptôme relevé
        // en console le 2026-08-30 : P_EXPLOIT_CHARGES se croyait un nœud d'automatisation.
        // Seuls les Scripts sont concernés : eux seuls relancent des cycles.
        SpecificUpgradeAutomationTresholdReduction,

        // Blindage — multiplie le PLAFOND de la jauge de Trace. Ajouté le 2026-08-31, et
        // appendé en fin comme tous les autres, pour la raison ci-dessus.
        //
        // C'est la colonne vertébrale de la méta-progression, et elle manquait. Mesuré en
        // simulation de campagne : sans elle, le joueur enchaînait 125 runs identiques de
        // quatre minutes — aucun des 134 nœuds ne déplaçait la contrainte qui met fin à une
        // run. Multiplier le plafond par 1 000 fait passer une run de fin de partie de 6 à
        // 80 050 CPU Cycles. Chaque prestige laisse survivre plus longtemps, donc construire
        // plus haut, donc gagner plus, donc racheter du plafond : c'est la boucle qui rend
        // une campagne progressive au lieu d'un tapis roulant.
        TraceCapacityMultiplier
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