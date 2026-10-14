using UnityEngine;

namespace Core.Models.Economy
{
    /// <summary>
    /// Ce qu'un nœud de prestige apporte.
    ///
    /// ⚠️ <b>L'index est sérialisé dans les .asset générés.</b> Un type s'APPEND en fin de liste.
    /// En retirer un décale tous les suivants : ce n'est permis qu'en régénérant TOUT l'arbre
    /// dans la foulée (`Tools/Core/Générer l'Arbre depuis JSON`), ce qui a été fait le 2026-10-13
    /// (Furtivité, Zéro-Day, réveil, barre d'espace) puis le 2026-10-14 (les quatre types ciblés,
    /// l'argent et les TFlops de départ, l'automatisation globale). Une insertion au milieu sans
    /// régénération avait fait croire à P_EXPLOIT_CHARGES qu'il était un nœud d'automatisation,
    /// le 2026-08-30.
    /// </summary>
    public enum PrestigeBonusType
    {
        /// <summary>Multiplicateur des TFlops NETTES : <c>× (1 + Σ)</c> (P_DATAM).</summary>
        GlobalComputeMultiplier,

        /// <summary>
        /// Secondes AJOUTÉES à l'avance d'un Overclock (branche Overclock). S'appelait
        /// ClickPowerMultiplier quand l'Overclock multipliait une avance de base.
        /// </summary>
        OverclockBonus,

        /// <summary>
        /// Retranché du multiplicateur de coût de BASE de chaque générateur (P_REFAC) : à −0,10,
        /// une courbe en ×1,15 par niveau passe à ×1,05. Elle aplatit la courbe entière, là où le
        /// Noyau Compilé abaisse le prix d'entrée.
        /// </summary>
        CostMultiplierReduction,

        /// <summary>Déblocage du Data Wiper (P_EMERG).</summary>
        UnlockEmergencyButton,

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

        /// <summary>
        /// Extraction — multiplie le bonus des TROIS paliers d'exfiltration : <c>× (1 + Σ)</c>.
        ///
        /// La seule branche qui récompense la manière de jouer plutôt que la puissance brute.
        /// Elle achète de l'appât, pas de la sécurité.
        /// </summary>
        CleanExitBonusMultiplier,

        /// <summary>
        /// Extraction Haut Risque — ouvre les paliers d'extraction marqués « sous condition »,
        /// aujourd'hui le seul palier à 90 %. Déblocage booléen : seul le fait de posséder le
        /// nœud compte, sa magnitude n'a aucun sens.
        /// </summary>
        UnlockHighRiskExtraction,

        /// <summary>La racine de l'arbre, et rien d'autre. Aucun effet, aucune magnitude.</summary>
        Root,

        /// <summary>
        /// Le puits à points (P_CORE_OVERRIDE, 2026-10-14). Aucun effet propre : chaque rang est
        /// un point DÉPENSÉ, et c'est tout ce que lit le Noyau Compilé. Il absorbe les points
        /// qu'une campagne produit au-delà des 107 de l'arbre utile.
        /// </summary>
        CoreOverride
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
        [Tooltip("Si 1 = Achat unique (le niveau sera masqué dans l'UI). " +
                 "0 = SANS PLAFOND : le nœud s'achète à l'infini (P_CORE_OVERRIDE).")]
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

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string DisplayDescriptionKey => _displayDescriptionKey;
        public PrestigeBonusType BonusType => _bonusType;

        /// <summary>
        /// Le nœud s'achète-t-il sans fin. Un <c>maxLevel</c> nul ou négatif dans les données.
        /// Seule la vue a besoin de le savoir, pour afficher « Niv. X » sans maximum.
        /// </summary>
        public bool IsUnlimited => _maxLevel <= 0;

        /// <summary>
        /// Niveau maximum. <see cref="int.MaxValue"/> pour un nœud sans plafond : toutes les
        /// comparaisons « niveau ≥ max » du jeu restent justes sans cas particulier — un tel nœud
        /// n'est simplement jamais « au maximum ».
        /// </summary>
        public int MaxLevel => _maxLevel > 0 ? _maxLevel : int.MaxValue;

        public double BaseCost => _baseCost;
        public double CostMultiplier => _costMultiplier;

        /// <summary>
        /// Coût du PROCHAIN niveau de ce nœud, en CPU Cycles.
        ///
        /// <b>Plat depuis le 2026-09-12 : le niveau ne renchérit plus rien.</b> Le coût vaut
        /// <see cref="BaseCost"/> — 1 point par rang presque partout, 2 pour `P_DATAM`, 1 à 10
        /// pour le Blindage. Le joueur n'a qu'à choisir lequel.
        ///
        /// Ce que ça supprime. La courbe <c>BaseCost × CostMultiplier^niveau</c> était recopiée
        /// dans QUATRE fichiers qui n'en savaient rien les uns des autres, et l'arbre s'était
        /// déjà retrouvé 152 fois trop cher pour une erreur sur un seul de ces termes. Elle
        /// créait surtout un piège invisible : mesuré, un joueur qui achetait naturellement le
        /// nœud le moins cher d'abord jouait cent fois moins bien qu'un joueur qui les évitait,
        /// parce que 145 nœuds affichés au même prix n'avaient pas du tout la même valeur.
        ///
        /// <paramref name="currentLevel"/> est conservé dans la signature : c'est le seul
        /// endroit à toucher si une courbe devait réapparaître un jour, avec
        /// <see cref="GetTotalCostOfLevels"/> juste en dessous.
        /// </summary>
        public double GetCostAtLevel(int currentLevel)
        {
            return _baseCost;
        }

        /// <summary>
        /// Points dépensés pour amener ce nœud au niveau <paramref name="level"/> : la somme des
        /// <see cref="GetCostAtLevel"/> de 0 à level − 1. Forme fermée tant que le coût est plat.
        /// C'est ce que lit le Noyau Compilé — une courbe réintroduite ci-dessus doit l'être ici
        /// aussi, sinon le Noyau compterait des points que le joueur n'a pas payés.
        /// </summary>
        public double GetTotalCostOfLevels(int level)
        {
            return level <= 0 ? 0d : _baseCost * level;
        }
        public float BonusPerLevel => _bonusPerLevel;
        public Vector2 UiPosition => _uiPosition;
        /// <summary>
        /// La condition d'ouverture : quel parent, et à quel niveau. Remplace depuis le
        /// 2026-09-09 la simple référence <c>_prerequisite</c>, qui ne pouvait exprimer que
        /// « posséder le parent » et ouvrait donc une branche entière au premier rang acheté.
        /// </summary>
        public PrestigeRequirement Requirement => _requirement;
    }
}
