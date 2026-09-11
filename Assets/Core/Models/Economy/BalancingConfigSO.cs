using System.Collections.Generic;
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

        [Tooltip("ASYMPTOTE de la réduction apportée par les Proxies. Jamais atteinte, seulement " +
                 "approchée : il reste TOUJOURS au moins (1 − cette valeur) de la Trace brute qui " +
                 "passe.\n\n" +
                 "C'est ce qui rend l'invulnérabilité arithmétiquement impossible. Avant le " +
                 "2026-08-31 la dissipation était soustraite platement, donc une valeur non " +
                 "bornée face à une génération bornée : 2,7 M$ de Proxies annulaient toute la " +
                 "Trace du jeu, définitivement.")]
        [SerializeField, Range(0.1f, 0.99f)] private float _maxTraceReduction = 0.85f;

        [Tooltip("Rapport Dissipation/Brut qui procure la MOITIÉ de l'asymptote ci-dessus.\n\n" +
                 "Formule : R = MaxTraceReduction × D / (D + cette valeur × Brut).\n\n" +
                 "Seul le RATIO compte, jamais la magnitude : la formule se comporte pareil à " +
                 "1e0 et à 1e13 de Trace. Conséquence voulue — faire grossir son économie " +
                 "augmente le Brut, donc DILUE les Proxies déjà achetés. C'est là que naît " +
                 "l'arbitrage permanent entre pousser la production et tenir la défense.\n\n" +
                 "MONTER cette valeur rend la défense plus chère à tous les stades.")]
        [SerializeField, Min(0.01f)] private float _dissipationHalfPointRatio = 1f;

        [Tooltip("Exposant qui lie la Trace d'un générateur à son RENDEMENT : " +
                 "trace = base × (rendement / rendement de base) ^ exposant.\n\n" +
                 "⚠️ RÉGLAGE STRUCTURANT. À 1, monter un générateur accélère les gains ET la mort " +
                 "dans la même proportion — bien jouer ne change rien, c'est l'échec constaté " +
                 "avant le 2026-08-30. À 0, la Trace est forfaitaire et bornée pour toujours, " +
                 "c'est l'échec constaté après. Entre les deux, progresser paie sans jamais " +
                 "supprimer le danger : à 0,6, doubler son rendement ne multiplie la Trace " +
                 "que par 1,52.")]
        [SerializeField, Range(0f, 1f)] private float _traceYieldExponent = 0.6f;


        // ------------------------------------------------------------------
        // Relevé de Trace — le brouillard de guerre
        // ------------------------------------------------------------------
        [Header("Relevé de Trace (incertitude)")]
        [Tooltip("Intervalle MINIMUM entre deux relevés de la jauge, en secondes. C'est la " +
                 "précision dont dispose un joueur dont la Trace monte doucement.")]
        [SerializeField, Range(0.1f, 10f)] private float _traceReadoutMinInterval = 0.5f;

        [Tooltip("Intervalle MAXIMUM entre deux relevés, atteint quand la jauge se remplit à la " +
                 "vitesse de référence ci-dessous.\n\n" +
                 "C'est le cœur du modèle de risque : plus le joueur pousse, plus sa télémétrie " +
                 "bégaie, et plus il pilote au jugé. MONTER cette valeur rend la gourmandise " +
                 "aveuglante.")]
        [SerializeField, Range(1f, 300f)] private float _traceReadoutMaxInterval = 120f;

        [Tooltip("Vitesse de remplissage, en fraction de jauge par seconde, à laquelle " +
                 "l'intervalle atteint son maximum. À 0,005, la cécité totale s'installe quand " +
                 "la jauge se remplirait en 200 secondes.")]
        [SerializeField, Range(0.0001f, 0.05f)] private float _traceReadoutReferenceFillRate = 0.001f;

        [Tooltip("Facteur de pessimisme du bord haut de la fourchette. Au-dessus de 1 parce " +
                 "qu'entre deux relevés le débit a pu croître : une borne haute que la réalité " +
                 "dépasse ne serait plus une borne.\n\n" +
                 "Il en faut BEAUCOUP plus que l'intuition ne suggère. La projection est " +
                 "linéaire — dernier débit connu × âge du relevé — alors que la production d'une " +
                 "run accélère. À 1,5, mesuré : le joueur prudent croyait sortir à 90 % et " +
                 "sortait en réalité à 93,8 %. Sa « borne haute » passait SOUS la vérité, donc " +
                 "prudence et gourmandise se rejoignaient au bord du gouffre et il n'y avait " +
                 "plus rien à arbitrer. À 3, il sort à 91,6 % et paie enfin sa prudence d'une " +
                 "run supplémentaire — le choix redevient un choix.")]
        [SerializeField, Range(1f, 4f)] private float _traceReadoutPessimism = 3f;

        [Tooltip("Fraction de l'asymptote au-delà de laquelle le Ghost Cache se charge.\n\n" +
                 "Remplace l'ancienne condition « la dissipation dépasse la génération », qui " +
                 "n'a plus de sens : avec une réduction asymptotique il n'existe plus d'excédent. " +
                 "L'intention du GDD est conservée à l'identique — ce qui se paie est un MAINTIEN " +
                 "DE POSTURE DÉFENSIVE, et la charge se remplit toujours en temps, pas en " +
                 "magnitude. À 0,75 avec une asymptote de 0,85, il faut tenir 63,75 % de " +
                 "réduction pour charger.")]
        [SerializeField, Range(0f, 1f)] private float _ghostCacheReductionThreshold = 0.75f;

        // ------------------------------------------------------------------
        // TFlops et générateurs
        // ------------------------------------------------------------------
        [Header("TFlops et générateurs")]
        [Tooltip("Compression du temps par TFlop : durée = base / (1 + TFlops × k) ^ exposant. " +
                 "Décroissance asymptotique, la durée ne tombe jamais à zéro.")]
        [SerializeField, Range(0.001f, 1f)] private float _tflopsTimeCompression = 0.05f;

        [Tooltip("Exposant de la compression ci-dessus. À 1 on retrouve la formule linéaire " +
                 "d'origine, qui saturait : le mur MinCycleDuration était atteint entre 4 et " +
                 "180 TFlops selon le Script, alors que HW_01 niveau 10 en fournit déjà 40. " +
                 "Passé ce point le Hardware n'apportait plus rien. Un exposant < 1 étale la " +
                 "compression sur toute la partie sans jamais la borner.")]
        [SerializeField, Range(0.05f, 1f)] private float _tflopsCompressionExponent = 0.25f;

        [Tooltip("Les TFlops multiplient aussi le RENDEMENT des Scripts : " +
                 "rendement × (1 + TFlops) ^ exposant. Réservé aux Scripts — l'appliquer au " +
                 "Hardware créerait une boucle, son rendement ÉTANT la capacité en TFlops.\n\n" +
                 "C'est le versant « récompense » du Hardware. La compression, elle, accélère " +
                 "l'argent ET la Trace dans la même proportion : elle est neutre sur le risque " +
                 "par Data gagnée, et ne nuit qu'en gonflant le Brut, ce qui dilue les Proxies.")]
        [SerializeField, Range(0f, 0.5f)] private float _tflopsYieldExponent = 0.15f;

        [Tooltip("Plancher ABSOLU de durée de cycle, en secondes, tous générateurs confondus. " +
                 "Garde-fou de boucle, pas un levier d'équilibrage : depuis le 2026-08-31 le " +
                 "champ MinCycleDuration de chaque générateur n'est plus un mur — c'est lui qui " +
                 "tuait le pilier Hardware.")]
        [SerializeField, Range(0.01f, 1f)] private float _absoluteMinCycleDuration = 0.05f;

        [Tooltip("Plafond de TOUTES les réductions ciblées de prestige (coût, temps). " +
                 "Empêche qu'un générateur devienne gratuit ou son cycle instantané.")]
        [SerializeField, Range(0.5f, 0.99f)] private float _maxTargetedReduction = 0.95f;

        // ------------------------------------------------------------------
        // Run et prestige
        // ------------------------------------------------------------------
        [Header("Run et prestige")]
        [Tooltip("Argent de départ d'une run neuve, AVANT le bonus de prestige qui s'y ajoute.")]
        [SerializeField, Min(0f)] private double _baseStartingMoney = 10d;

        [Tooltip("Datas à générer pour valoir un CPU Cycle. Cycles = floor(sqrt(RunMoney / valeur)). " +
                 "Pilote aussi le seuil de déblocage de l'exfiltration.")]
        [SerializeField, Min(1f)] private double _moneyPerCpuCycle = 1000d;

        [Tooltip("Paliers d'extraction : à ranger du MOINS au PLUS exigeant. Le joueur empoche le " +
                 "bonus du plus haut palier dont il a atteint le seuil de Trace, et rien du tout " +
                 "sous le premier.\n\n" +
                 "⚠️ RÉGLAGE STRUCTURANT — c'est ici que vit le risque/récompense du jeu.\n\n" +
                 "Ce bonus a d'abord été FORFAITAIRE (+20 % quoi qu'il arrive). Mesuré : les Datas " +
                 "s'accumulent proportionnellement à la jauge (50 % de jauge = 31 % des Datas, " +
                 "90 % = 90 %), la Trace étant un compteur de production. Le dernier pour-cent de " +
                 "jauge valait donc exactement ce que valait le premier : pousser de 90 à 100 % " +
                 "rapportait ~5 % de Cycles quand une seule saisie en coûtait 17. Un joueur qui " +
                 "gagnait TOUS ses paris perdait quand même — un impôt, pas un arbitrage.\n\n" +
                 "Il a ensuite été une COURBE CONTINUE, et c'était l'excès inverse : le bonus " +
                 "passait de +25 % à +89 % entre 70 % et 90 % de jauge, ce qui rendait chaque " +
                 "seconde de retard monnayable et transformait la sortie en optimisation au " +
                 "chronomètre plutôt qu'en décision.\n\n" +
                 "Un palier ne bouge pas entre deux seuils : traîner ne rapporte rien, et la seule " +
                 "question qui se pose est franche — « je tente le suivant, oui ou non ? ».")]
        [SerializeField] private CleanExitTier[] _cleanExitTiers =
        {
            new CleanExitTier(0.50f, 0.15d),
            new CleanExitTier(0.75f, 0.30d),
            new CleanExitTier(0.90f, 0.50d)
        };

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

        /// <summary>Asymptote de la réduction des Proxies. Jamais atteinte, seulement approchée.</summary>
        public float MaxTraceReduction => _maxTraceReduction;

        /// <summary>Rapport Dissipation/Brut donnant la moitié de <see cref="MaxTraceReduction"/>.</summary>
        public float DissipationHalfPointRatio => _dissipationHalfPointRatio;

        /// <summary>Exposant liant la Trace d'un générateur à son rendement. Strictement entre 0 et 1.</summary>
        public float TraceYieldExponent => _traceYieldExponent;


        public float TraceReadoutMinInterval => _traceReadoutMinInterval;
        public float TraceReadoutMaxInterval => _traceReadoutMaxInterval;
        public float TraceReadoutReferenceFillRate => _traceReadoutReferenceFillRate;
        public float TraceReadoutPessimism => _traceReadoutPessimism;

        /// <summary>Fraction de l'asymptote au-delà de laquelle le Ghost Cache se charge.</summary>
        public float GhostCacheReductionThreshold => _ghostCacheReductionThreshold;

        public double TFlopsTimeCompression => _tflopsTimeCompression;
        public double TFlopsCompressionExponent => _tflopsCompressionExponent;
        public double TFlopsYieldExponent => _tflopsYieldExponent;
        public float AbsoluteMinCycleDuration => _absoluteMinCycleDuration;
        public float MaxTargetedReduction => _maxTargetedReduction;

        public double BaseStartingMoney => _baseStartingMoney;
        public double MoneyPerCpuCycle => _moneyPerCpuCycle;
        public IReadOnlyList<CleanExitTier> CleanExitTiers => _cleanExitTiers;
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
