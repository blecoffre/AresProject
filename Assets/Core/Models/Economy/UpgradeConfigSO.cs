using System.Collections.Generic;
using UnityEngine;

namespace Core.Models.Economy
{
    public enum UpgradeType
    {
        Script,     // Cycle de production : verse de l'argent à son terme, génère de la Trace
        Hardware,   // Fournit de la capacité de calcul (TFlops), génère massivement de la Trace
        Proxy       // Dissipe la Trace, ne produit rien
    }

    [CreateAssetMenu(fileName = "NewUpgradeConfig", menuName = "Core/Economy/Upgrade Config")]
    public class UpgradeConfigSO : ScriptableObject
    {
        [Header("Identifiants & Catégorisation")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private UpgradeType _type;
        [SerializeField] private int _order;

        [Header("Économie : Achat")]
        [Tooltip("C_base : Coût initial défini pour le niveau 0")]
        [SerializeField] private double _baseCost = 10d;

        [Tooltip("M : Multiplicateur de croissance (fixé entre 1.07 et 1.15)")]
        [SerializeField, Range(1.01f, 1.5f)] private double _costMultiplier = 1.07d;

        [Header("Économie : Production & Trace")]
        [Tooltip("Versement de base, multiplié par le niveau à chaque cycle")]
        [SerializeField] private double _baseProductionYield = 1d;
        [SerializeField] private double _traceGeneratedPerSecond = 1f;

        [Header("Cycle (Scripts uniquement)")]
        [SerializeField] private float _baseCycleDuration = 1f;
        [Tooltip("Plancher absolu : aucun palier ni bonus ne peut descendre sous cette durée")]
        [SerializeField] private float _minCycleDuration = 0.1f;

        [Tooltip("OBSOLÈTE : la réduction se fait désormais par paliers, plus par niveau. " +
                 "Champ conservé pour ne pas perdre les valeurs déjà écrites dans le JSON.")]
        [SerializeField] private float _durationReductionPerLevel = 0.01f;

        [Header("Automatisation")]
        [Tooltip("Niveau à partir duquel ce générateur relance ses cycles tout seul. " +
                 "Les nœuds de prestige ciblés peuvent abaisser ce seuil.")]
        [SerializeField] private int _automationLevel = 10;

        [Header("Paliers")]
        [Tooltip("Bonus acquis à des niveaux précis. Effets multiplicatifs et cumulatifs.")]
        [SerializeField] private List<UpgradeMilestone> _milestones = new List<UpgradeMilestone>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public UpgradeType Type => _type;
        public int Order => _order;
        public double BaseCost => _baseCost;
        public double CostMultiplier => _costMultiplier;
        public double BaseProductionYield => _baseProductionYield;
        public double BaseTraceGeneratedPerSecond => _traceGeneratedPerSecond;
        public float BaseCycleDuration => _baseCycleDuration;
        public float MinCycleDuration => _minCycleDuration;
        public int AutomationLevel => _automationLevel;
        public IReadOnlyList<UpgradeMilestone> Milestones => _milestones;

        [System.Obsolete("Remplacé par le système de paliers (Milestones). Conservé pour ne pas perdre la donnée.")]
        public float DurationReductionPerLevel => _durationReductionPerLevel;
    }
}
