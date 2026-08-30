using UnityEngine;

namespace Core.Models.Economy
{
    /// <summary>
    /// Source unique de tous les réglages d'équilibrage.
    ///
    /// Avant ce ScriptableObject, ces valeurs étaient des `const` éparpillées dans huit fichiers :
    /// changer le multiplicateur du Zéro-Day Exploit ou le coût du Data Wiper demandait d'éditer
    /// du C# et de recompiler. Un game designer ne pouvait rien régler seul, et une session
    /// d'équilibrage coûtait une recompilation par essai.
    ///
    /// Tout est ici, et tout est modifiable dans l'inspecteur — <b>y compris en Play Mode</b>,
    /// puisque les systèmes lisent les propriétés à l'usage plutôt que de les recopier au
    /// démarrage. C'est le seul asset du projet qu'on édite à la main sans qu'un générateur ne
    /// l'écrase.
    ///
    /// Les bornes des `[Range]` ne sont pas décoratives : elles empêchent de saisir une valeur qui
    /// casserait une formule (un diviseur nul, une réduction supérieure à 100 %).
    /// </summary>
    [CreateAssetMenu(fileName = "BalancingConfig", menuName = "Core/Economy/Balancing Config")]
    public class BalancingConfigSO : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Trace
        // ------------------------------------------------------------------
        [Header("Trace")]
        [Tooltip("Convertit une Trace par seconde en fraction de jauge par seconde. À 100, un " +
                 "débit de 100 Trace/s remplit la jauge en une seconde. BAISSER cette valeur rend " +
                 "tout le jeu plus dangereux.")]
        [SerializeField, Min(1f)] private float _baseTraceCap = 100f;

        // ------------------------------------------------------------------
        // TFlops et générateurs
        // ------------------------------------------------------------------
        [Header("TFlops et générateurs")]
        [Tooltip("Compression du temps par TFlop : durée = base / (1 + TFlops × k). " +
                 "Décroissance asymptotique, la durée ne tombe jamais à zéro.")]
        [SerializeField, Range(0.001f, 1f)] private float _tflopsTimeCompression = 0.05f;

        [Tooltip("Plafond de TOUTES les réductions ciblées de prestige (coût, temps). " +
                 "Empêche qu'un générateur devienne gratuit ou son cycle instantané.")]
        [SerializeField, Range(0.5f, 0.99f)] private float _maxTargetedReduction = 0.95f;

        [Tooltip("Accélération des cycles par niveau de Proxy possédé. 0,01 = +1 % par niveau. " +
                 "⚠️ Linéaire et sans plafond : c'est minCycleDuration qui borne l'effet.")]
        [SerializeField, Range(0f, 0.1f)] private float _proxySynergyPerLevel = 0.01f;

        // ------------------------------------------------------------------
        // Run et prestige
        // ------------------------------------------------------------------
        [Header("Run et prestige")]
        [Tooltip("Argent de départ d'une run neuve, AVANT le bonus de prestige qui s'y ajoute.")]
        [SerializeField, Min(0f)] private double _baseStartingMoney = 10d;

        [Tooltip("Datas à générer pour valoir un CPU Cycle. Cycles = floor(sqrt(RunMoney / valeur)). " +
                 "Pilote aussi le seuil de déblocage de l'exfiltration.")]
        [SerializeField, Min(1f)] private double _moneyPerCpuCycle = 1000d;

        [Tooltip("Bonus « Clean Exit » : multiplicateur des CPU Cycles quand le joueur sort de " +
                 "lui-même au lieu de se faire saisir. 1,2 = +20 %.")]
        [SerializeField, Min(1f)] private double _cleanExitMultiplier = 1.2d;

        [Tooltip("Secondes de progression offertes par clic d'Overclock, avant bonus de prestige.")]
        [SerializeField, Min(0f)] private float _overclockWarpSeconds = 0.5f;

        // ------------------------------------------------------------------
        // Ghost Cache et Zéro-Day Exploit
        // ------------------------------------------------------------------
        [Header("Ghost Cache / Zéro-Day Exploit")]
        [Tooltip("Secondes d'excédent de dissipation à accumuler pour UNE charge. Le nombre de " +
                 "charges stockables vient du nœud de prestige P_EXPLOIT_CHARGES.")]
        [SerializeField, Min(1f)] private float _ghostCacheCapacitySeconds = 300f;

        [Tooltip("Durée du Zéro-Day Exploit, en secondes.")]
        [SerializeField, Min(1f)] private float _overdriveDurationSeconds = 30f;

        [Tooltip("Multiplicateur de rendement des Scripts pendant l'Exploit. Ne touche que les " +
                 "Scripts : chez un Hardware, le rendement EST la capacité en TFlops.")]
        [SerializeField, Min(1f)] private double _overdriveYieldMultiplier = 50d;

        [Tooltip("Multiplicateur de la génération BRUTE de Trace pendant l'Exploit, en plus de " +
                 "l'extinction des Proxies.\n\n" +
                 "⚠️ RÉGLAGE LE PLUS SENSIBLE DU JEU. La dissipation valant zéro pendant " +
                 "l'Exploit, le temps de survie depuis une jauge vide vaut " +
                 "100 / (génération × cette valeur). À 10, tenir les 30 secondes exige de générer " +
                 "moins de 0,33 Trace/s — soit moins qu'un seul Script de niveau 1.")]
        [SerializeField, Min(1f)] private float _overdriveTraceMultiplier = 10f;

        // ------------------------------------------------------------------
        // Bouton d'Urgence / Data Wiper
        // ------------------------------------------------------------------
        [Header("Bouton d'Urgence / Data Wiper")]
        [Tooltip("TFlops à POSSÉDER pour le premier usage de la run. Rien n'est dépensé : c'est " +
                 "un prérequis. Repère : HW_01 au niveau 10, palier ×2 compris, en fournit 40.")]
        [SerializeField, Min(1f)] private double _emergencyBaseRequiredTFlops = 50d;

        [Tooltip("Facteur d'escalade du palier requis à chaque usage sur la run.")]
        [SerializeField, Min(1.01f)] private double _emergencyRequirementMultiplier = 3d;

        [Tooltip("Points de jauge effacés, en ABSOLU : à 0,2 on passe de 63 % à 43 %.")]
        [SerializeField, Range(0.01f, 1f)] private float _emergencyTraceReduction = 0.20f;

        [Tooltip("Tranche de TFlops immobilisée au premier usage de la run.")]
        [SerializeField, Range(0f, 0.95f)] private float _emergencyBaseBlockedFraction = 0.30f;

        [Tooltip("Points de tranche ajoutés à chaque usage : 30 %, puis 40 %, puis 50 %…")]
        [SerializeField, Range(0f, 0.5f)] private float _emergencyBlockedIncreasePerUse = 0.10f;

        [Tooltip("Plafond de la tranche immobilisée. À 1, un usage tardif couperait 100 % du parc " +
                 "— plus aucune dissipation ni compression de cycle. Le bouton deviendrait un " +
                 "suicide pur plutôt qu'un choix.")]
        [SerializeField, Range(0.1f, 0.99f)] private float _emergencyMaxBlockedFraction = 0.90f;

        [Tooltip("Durée d'immobilisation des TFlops, en secondes.")]
        [SerializeField, Min(0f)] private float _emergencyBlockDurationSeconds = 60f;

        [Tooltip("Délai entre deux activations, en secondes.")]
        [SerializeField, Min(0f)] private float _emergencyCooldownSeconds = 300f;

        // ------------------------------------------------------------------
        // Lecture. Propriétés et non champs publics : l'asset est une donnée de
        // référence, aucun système n'a le droit de la modifier au runtime.
        // ------------------------------------------------------------------
        /// <summary>
        /// Plafond de Trace avant saisie, hors apport du Hardware. Ancien « diviseur de jauge » :
        /// il jouait déjà ce rôle, la jauge normalisée valant trace / diviseur. Le nommer pour ce
        /// qu'il est était le préalable au plafond dynamique.
        /// </summary>
        public float BaseTraceCap => _baseTraceCap;

        public double TFlopsTimeCompression => _tflopsTimeCompression;
        public float MaxTargetedReduction => _maxTargetedReduction;
        public double ProxySynergyPerLevel => _proxySynergyPerLevel;

        public double BaseStartingMoney => _baseStartingMoney;
        public double MoneyPerCpuCycle => _moneyPerCpuCycle;
        public double CleanExitMultiplier => _cleanExitMultiplier;
        public float OverclockWarpSeconds => _overclockWarpSeconds;

        public float GhostCacheCapacitySeconds => _ghostCacheCapacitySeconds;
        public float OverdriveDurationSeconds => _overdriveDurationSeconds;
        public double OverdriveYieldMultiplier => _overdriveYieldMultiplier;
        public float OverdriveTraceMultiplier => _overdriveTraceMultiplier;

        public double EmergencyBaseRequiredTFlops => _emergencyBaseRequiredTFlops;
        public double EmergencyRequirementMultiplier => _emergencyRequirementMultiplier;
        public float EmergencyTraceReduction => _emergencyTraceReduction;
        public float EmergencyBaseBlockedFraction => _emergencyBaseBlockedFraction;
        public float EmergencyBlockedIncreasePerUse => _emergencyBlockedIncreasePerUse;
        public float EmergencyMaxBlockedFraction => _emergencyMaxBlockedFraction;
        public float EmergencyBlockDurationSeconds => _emergencyBlockDurationSeconds;
        public float EmergencyCooldownSeconds => _emergencyCooldownSeconds;
    }
}
