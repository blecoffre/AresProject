using System.Collections.Generic;
using UnityEngine;

namespace Core.Models.Economy
{
    public enum UpgradeType
    {
        Script,     // Cycle de production : verse de l'argent à son terme
        Hardware,   // Fournit de la capacité de calcul (TFlops), qui accélère Scripts ET A.M.I.
        Proxy       // Consommable de survie : chaque achat retire des UT à la jauge
    }

    [CreateAssetMenu(fileName = "NewUpgradeConfig", menuName = "Core/Economy/Upgrade Config")]
    public class UpgradeConfigSO : ScriptableObject
    {
        [Header("Identifiants & Catégorisation")]
        [SerializeField] private string _id;

        [Tooltip("Clé de localisation, dérivée de l'id : UPG_<id>_NAME. Écrite par le générateur.")]
        [SerializeField] private string _displayNameKey;

        [Tooltip("Clé de localisation, dérivée de l'id : UPG_<id>_DESC. Écrite par le générateur.")]
        [SerializeField] private string _displayDescriptionKey;

        [SerializeField] private UpgradeType _type;
        [SerializeField] private int _order;

        [Header("Économie : Achat")]
        [Tooltip("C_base : Coût initial défini pour le niveau 0")]
        [SerializeField] private double _baseCost = 10d;

        [Tooltip("M : Multiplicateur de croissance (fixé entre 1.07 et 1.15)")]
        [SerializeField, Range(1.01f, 1.5f)] private double _costMultiplier = 1.07d;

        [Header("Économie : Production")]
        [Tooltip("Versement de base, multiplié par le niveau à chaque cycle")]
        [SerializeField] private double _baseProductionYield = 1d;

        [Header("Proxies uniquement")]
        [Tooltip("SECONDES DE SURVIE rendues par chaque niveau acheté, avant paliers et branche " +
                 "Interception. Converties en UT à la vitesse courante de l'A.M.I. au moment de " +
                 "l'achat : un Proxy offre le même répit à tous les stades de la partie.")]
        [SerializeField] private double _wipeSeconds;

        [Header("Cycle (Scripts uniquement)")]
        [Tooltip("Durée d'un cycle au niveau 1, avant paliers et TFlops. Le plancher est global : " +
                 "BalancingConfig.AbsoluteMinCycleDuration.")]
        [SerializeField] private float _baseCycleDuration = 1f;

        [Header("Automatisation")]
        [Tooltip("Niveau à partir duquel ce générateur relance ses cycles tout seul. " +
                 "Aucun nœud de prestige ne l'abaisse depuis le retrait des nœuds ciblés (2026-10-14).")]
        [SerializeField] private int _automationLevel = 10;

        [Header("Paliers")]
        [Tooltip("Bonus acquis à des niveaux précis. Effets multiplicatifs et cumulatifs.")]
        [SerializeField] private List<UpgradeMilestone> _milestones = new List<UpgradeMilestone>();

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string DisplayDescriptionKey => _displayDescriptionKey;
        public UpgradeType Type => _type;
        public int Order => _order;
        public double BaseCost => _baseCost;
        public double CostMultiplier => _costMultiplier;
        public double BaseProductionYield => _baseProductionYield;

        /// <summary>Secondes de survie rendues par niveau de Proxy acheté, avant bonus. Nul ailleurs.</summary>
        public double WipeSeconds => _wipeSeconds;
        public float BaseCycleDuration => _baseCycleDuration;
        public int AutomationLevel => _automationLevel;
        public IReadOnlyList<UpgradeMilestone> Milestones => _milestones;
    }
}
