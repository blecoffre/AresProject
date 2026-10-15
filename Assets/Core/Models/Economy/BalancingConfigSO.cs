using R3;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Models.Economy
{
    /// <summary>
    /// Source unique de tous les réglages d'équilibrage.
    ///
    /// Avant ce ScriptableObject, ces valeurs étaient des `const` éparpillées dans huit fichiers :
    /// changer un multiplicateur ou le coût du Data Wiper demandait d'éditer
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
        // La jauge de l'A.M.I. — le compte à rebours (refonte « Time Attack », 2026-10-13)
        // ------------------------------------------------------------------
        [Header("Jauge de l'A.M.I.")]
        [Tooltip("Plafond de la jauge, en Unités de Trace (UT). FIXE : il ne varie jamais en " +
                 "cours de partie. Le franchir déclenche la Saisie Fédérale.\n\n" +
                 "Tout le reste s'exprime contre lui — les paliers d'extraction en sont des " +
                 "fractions, l'Overclock et les Proxies des secondes de vitesse de l'A.M.I.")]
        [SerializeField, Min(1f)] private float _amiTraceCap = 10000f;

        [Tooltip("Vitesse de l'A.M.I. à zéro TFlop et sans Blindage, en UT par seconde. À 33, " +
                 "une jauge vide se remplit en ~5 minutes.\n\n" +
                 "Formule : V = cette valeur × (1 + facteur × TFlops bruts) ^ exposant " +
                 "÷ (1 + Blindage).")]
        [SerializeField, Min(0f)] private float _amiBaseSpeed = 33f;

        [Tooltip("Poids des TFlops BRUTS dans la vitesse de l'A.M.I. (le facteur de la formule " +
                 "ci-dessus). Bruts = la seule somme du parc Hardware : ni P_DATAM ni le Noyau " +
                 "n'accélèrent la menace, c'est ce qui permet au prestige d'accélérer les revenus " +
                 "sans accélérer la mort.")]
        [SerializeField, Min(0f)] private float _amiTFlopsFactor = 0.05f;

        [Tooltip("Exposant des TFlops dans la vitesse de l'A.M.I.\n\n" +
                 "⚠️ RÉGLAGE STRUCTURANT, distinct à dessein de l'exposant de compression des " +
                 "Scripts (0,25). Plus bas que lui, le Hardware fait GAGNER la course : les " +
                 "Scripts accélèrent plus vite que la menace. Calculé : à 0,25, une run de victoire " +
                 "durait ~10 s Blindage compris ; à 0,10 (tranché par le GD le 2026-10-13), " +
                 "~5 min 30.")]
        [SerializeField, Range(0f, 1f)] private float _amiSpeedExponent = 0.10f;

        [Tooltip("Puissance de calcul à atteindre pour GAGNER la partie — la clé USB du lore. " +
                 "Comparée aux TFlops NETTES (après P_DATAM, Noyau et sacrifice du Data Wiper), " +
                 "celles qu'affiche l'en-tête.\n\n" +
                 "Posée à 1e12 le 2026-09-15 : débloquer la dernière machine NE SUFFIT PAS, il " +
                 "faut revenir la faire grandir.")]
        [SerializeField, Min(1f)] private double _victoryTFlops = 1e12d;

        // ------------------------------------------------------------------
        // Relevé de Trace — le brouillard de guerre (refonte « Time Attack », lot 3)
        // ------------------------------------------------------------------
        [Header("Relevé de Trace (incertitude)")]
        [Tooltip("UT à remplir entre deux relevés, AU DÉPART (zéro TFlop). À 800 et 33 UT/s, " +
                 "l'aiguille se met à jour toutes les ~24 s.\n\n" +
                 "En UT et non en secondes depuis le 2026-10-14 : la vitesse de l'A.M.I. variant " +
                 "d'un ordre de grandeur sur la partie, un intervalle en secondes figeait la jauge " +
                 "une minute au départ et durait plus qu'une run en fin de partie. MONTER cette " +
                 "valeur épaissit le brouillard.")]
        [SerializeField, Min(1f)] private float _traceReadoutMaxIntervalUT = 800f;

        [Tooltip("UT entre deux relevés, PLANCHER atteint avec un parc conséquent. Sous lui, " +
                 "l'aiguille deviendrait continue pour l'œil et le brouillard n'existerait plus.")]
        [SerializeField, Min(1f)] private float _traceReadoutMinIntervalUT = 200f;

        [Tooltip("Pessimisme de la borne haute AU DÉPART : borne = relevé + V_AMI × âge × " +
                 "pessimisme + sauts connus. Le capteur le ramène vers 1, jamais en dessous — sous " +
                 "1 la borne passerait sous la vérité et ne serait plus une borne.\n\n" +
                 "À 3 et 800 UT, juste avant un relevé, la borne devance la vérité d'au plus 24 % " +
                 "de jauge au départ. C'est l'écart entre prudence et gourmandise.")]
        [SerializeField, Range(1f, 4f)] private float _traceReadoutPessimism = 3f;

        [Tooltip("Puissance du CAPTEUR : ce que les TFlops NETTES achètent en visibilité. Le " +
                 "diviseur vaut 1 + puissance × log10(1 + TFlops / échelle) ; il divise " +
                 "l'intervalle (jusqu'au plancher) et ramène le pessimisme vers 1.\n\n" +
                 "À 0,5 (2026-10-14) et une échelle de 1000, le plancher de 200 UT tombe vers 1e9 " +
                 "TFlops — en fin de partie, comme le veut le GD. À 1, il tombait dès 1e6. À 0 la " +
                 "mécanique est DÉSACTIVÉE : brouillard nominal à tous les stades.")]
        [SerializeField, Range(0f, 3f)] private float _traceReadoutSensorPower = 0.5f;

        [Tooltip("ÉCHELLE du capteur, en TFlops : la capacité en dessous de laquelle acheter du " +
                 "Hardware n'achète presque PAS de visibilité. Sans elle, le logarithme " +
                 "concentrait tout le rachat sur les premiers TFlops (mesuré le 2026-09-16 : le " +
                 "joueur achetait sa clarté avant même de voir le brouillard). MONTER cette valeur " +
                 "repousse le rachat plus tard.")]
        [SerializeField, Min(1f)] private float _traceReadoutSensorReferenceTFlops = 1000f;

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

        // ------------------------------------------------------------------
        // Noyau Compilé — la méta-progression passive (refonte, lot 4, 2026-10-14)
        // ------------------------------------------------------------------
        [Header("Noyau Compilé")]
        [Tooltip("Le coût de TOUS les générateurs — Scripts, Hardware, Proxies — est multiplié " +
                 "par cette base élevée au nombre de points DÉPENSÉS dans l'arbre. 0,99 = −1 % " +
                 "par point, composé : −66 % à 107 points, l'arbre utile complet.\n\n" +
                 "Remplace les 120 nœuds ciblés, qui rabotaient le coût d'un générateur à la fois.")]
        [SerializeField, Range(0.9f, 1f)] private float _coreCostBase = 0.99f;

        [Tooltip("Plancher du multiplicateur de coût ci-dessus. 0,05 = jamais moins de 5 % du " +
                 "prix d'origine, atteint vers 298 points dépensés.")]
        [SerializeField, Range(0.01f, 1f)] private float _coreCostFloor = 0.05f;

        [Tooltip("Rendement ajouté par point dépensé : les Datas des Scripts et les TFlops NETTES " +
                 "sont multipliées par (1 + points × ce réglage). Sans plafond — P_CORE_OVERRIDE " +
                 "le fait monter à l'infini.\n\n" +
                 "Ne touche JAMAIS la vitesse de l'A.M.I. (elle lit les TFlops brutes) ni le Wipe " +
                 "des Proxies : le Noyau accélère les revenus, pas la traque.")]
        [SerializeField, Range(0f, 0.1f)] private float _coreYieldPerPoint = 0.01f;

        // ------------------------------------------------------------------
        // Run et prestige
        // ------------------------------------------------------------------
        [Header("Run et prestige")]
        [Tooltip("Argent de départ d'une run neuve.")]
        [SerializeField, Min(0f)] private double _baseStartingMoney = 10d;

        [Tooltip("Prix de la PREMIÈRE boîte, en Datas. C'est ce qu'il faut exfiltrer pour décrocher " +
                 "le premier point de prestige.\n\n" +
                 "⚠️ RÉGLAGE STRUCTURANT, avec la croissance ci-dessous.\n\n" +
                 "MODÈLE DES BOÎTES : chaque point est une boîte à remplir ENTIÈREMENT, et le cumul " +
                 "demandé pour N points est la SOMME des N boîtes. À 1 M et ×1,5 : première boîte " +
                 "1 M, deuxième 1,5 M, donc 2,5 M de cumul pour détenir deux points.\n\n" +
                 "Le compteur ne retombe jamais à zéro entre deux runs : les boîtes remplies restent " +
                 "acquises, le reliquat travaille pour la suivante, et une run qui n'achève pas sa " +
                 "boîte n'est jamais perdue. Une SAISIE ne verse plus qu'une fraction de la run — " +
                 "voir le ratio ci-dessous.")]
        [SerializeField, Min(1f)] private double _prestigeFirstThresholdDatas = 400000d;

        [Tooltip("Facteur multiplicatif entre le PRIX de deux boîtes consécutives. À 1,5, la " +
                 "deuxième boîte coûte 1,5 fois la première, la troisième 1,5 fois la deuxième.\n\n" +
                 "⚠️ Il porte sur le prix d'UNE boîte, pas sur le cumul : le cumul, lui, croît " +
                 "comme la somme, donc bien plus vite. C'est exactement ce qui était confondu " +
                 "avant le 2026-09-25.\n\n" +
                 "C'est le bouton de DURÉE de campagne. Le rapport coût/rendement des échelles " +
                 "de générateurs, lui, ne règle que l'ATTEIGNABILITÉ du contenu : mesuré le " +
                 "2026-09-12, il fait passer la partie de « jamais finie en 16 h » à « finie en " +
                 "17 minutes » sans jamais offrir de valeur intermédiaire. Les deux sont " +
                 "orthogonaux, ne pas les confondre.")]
        [SerializeField, Range(1.05f, 4f)] private double _prestigeThresholdGrowth = 1.5d;

        [Tooltip("Part des Datas de la run versée au cumul de campagne quand le joueur se fait " +
                 "SAISIR. 0,30 = il garde 30 %. À 0, une saisie ne rapporte rien — l'ancien " +
                 "comportement.\n\n" +
                 "Existe parce que la saisie ne versait RIEN, et que rien dans le jeu ne " +
                 "capitalisait une run perdue. Constaté sur une partie réelle le 2026-09-22 : " +
                 "294 M de Datas générées sur deux morts, zéro banquée, zéro point, arbre vide. " +
                 "Le joueur restait à distance constante de son premier point quelle que soit " +
                 "sa performance — « le seuil monte » et « je n'avance jamais » sont la même " +
                 "sensation vues du fauteuil.\n\n" +
                 "⚠️ Doit rester NETTEMENT sous 1 : l'exfiltration verse 100 % ET le bonus de " +
                 "palier par-dessus. Au-delà, se faire prendre exprès deviendrait jouable.")]
        [SerializeField, Range(0f, 0.9f)] private double _seizedContributionRatio = 0.30d;

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
            // Le dernier palier attend le nœud « Extraction Haut Risque ».
            new CleanExitTier(0.90f, 0.50d, requiresUnlock: true)
        };

        // ------------------------------------------------------------------
        // Overclock — la barre d'espace
        // ------------------------------------------------------------------
        [Header("Overclock")]
        [Tooltip("Secondes d'avance offertes à chaque cycle en cours par un Overclock, AVANT la " +
                 "branche Overclock de l'arbre, qui s'y AJOUTE (jusqu'à +22 s).")]
        [SerializeField, Min(0f)] private float _overclockWarpSeconds = 0.5f;

        [Tooltip("Ce que coûte un Overclock, en SECONDES DE SURVIE : la pénalité vaut " +
                 "V_AMI × cette valeur, en UT. Indexée sur la vitesse et non fixe — 500 UT fixes " +
                 "pesaient 15 s au départ et une fraction de seconde en fin de partie.\n\n" +
                 "Le Blindage n'y change rien en secondes : il ralentit l'A.M.I., donc la " +
                 "pénalité en UT baisse d'autant. Sans cycle en cours, l'Overclock ne fait rien " +
                 "et ne coûte rien.")]
        [SerializeField, Min(0f)] private float _overclockPenaltySeconds = 5f;

        [Tooltip("Délai minimal, en secondes réelles, entre deux Overclocks pris en compte. " +
                 "Garde-fou contre le Game Over accidentel d'une touche martelée ou d'un double " +
                 "clic : à 5 s de survie l'appui, dix appuis par seconde videraient une jauge en " +
                 "quelques secondes.")]
        [SerializeField, Range(0f, 1f)] private float _overclockMinIntervalSeconds = 0.1f;

        // ------------------------------------------------------------------
        // Proxies — l'échelle commune du Wipe
        // ------------------------------------------------------------------
        [Header("Proxies")]
        [Tooltip("Multiplie les secondes de survie effacées par TOUS les Proxies, avant la branche " +
                 "Interception. À 1, chaque Proxy efface ce que porte son JSON (2,5 s pour le " +
                 "premier, 10 s pour PRX_05).\n\n" +
                 "Levier d'équilibrage demandé par le GD (session « chiffres », 2026-10-15) : un " +
                 "seul chiffre pour régler le poids des Proxies dans la campagne, sans retoucher " +
                 "quinze générateurs. Il change aussi ce qu'annonce la carte de chaque Proxy.")]
        [SerializeField, Min(0f)] private float _proxyWipeScale = 1f;

        // ------------------------------------------------------------------
        // Bouton d'Urgence / Data Wiper
        // ------------------------------------------------------------------
        [Header("Bouton d'Urgence / Data Wiper")]
        [Tooltip("UT retirées de la jauge par chaque usage. 2 500 = un quart de la jauge. La jauge " +
                 "ne descend pas sous zéro : l'excédent est perdu, comme pour un Proxy.")]
        [SerializeField, Min(0f)] private float _emergencyTraceRemoval = 2500f;

        [Tooltip("Part des TFlops NETTES sacrifiée à chaque usage, définitivement pour la run : " +
                 "30 %, puis 60 %, puis 90 %. Les Scripts ralentissent et rapportent moins ; la " +
                 "vitesse de l'A.M.I., qui lit les TFlops BRUTES, ne baisse jamais — sans quoi le " +
                 "bouton serait un soin gratuit.")]
        [SerializeField, Range(0.01f, 1f)] private float _emergencySacrificeStep = 0.30f;

        [Tooltip("Plafond STRICT du sacrifice. Une fois atteint, le bouton est épuisé jusqu'à la fin " +
                 "de la run : à 0,9 et 0,3 par usage, trois usages.")]
        [SerializeField, Range(0.01f, 1f)] private float _emergencyMaxSacrifice = 0.90f;

        // ------------------------------------------------------------------
        // Lecture. Propriétés et non champs publics : l'asset est une donnée de
        // référence, aucun système n'a le droit de la modifier au runtime.
        // ------------------------------------------------------------------
        /// <summary>Plafond fixe de la jauge, en UT. Le franchir déclenche la Saisie Fédérale.</summary>
        public float AmiTraceCap => _amiTraceCap;

        /// <summary>Vitesse de l'A.M.I. à zéro TFlop et sans Blindage, en UT/s.</summary>
        public float AmiBaseSpeed => _amiBaseSpeed;

        /// <summary>Poids des TFlops bruts dans la vitesse de l'A.M.I.</summary>
        public float AmiTFlopsFactor => _amiTFlopsFactor;

        /// <summary>Exposant des TFlops bruts dans la vitesse de l'A.M.I.</summary>
        public float AmiSpeedExponent => _amiSpeedExponent;

        public double VictoryTFlops => _victoryTFlops;

        /// <summary>
        /// Émis à chaque retouche de l'asset dans l'inspecteur, éditeur seulement.
        ///
        /// Les systèmes lisent leurs réglages à l'usage, ce qui les rend modifiables en Play
        /// Mode sans rien faire. Le ThreatManager et le PrestigeManager sont les exceptions : l'un
        /// met la vitesse de l'A.M.I. en cache, l'autre les multiplicateurs du Noyau, et tous deux
        /// ne les recalculent que lorsque leurs entrées bougent. Sans ce signal, régler l'exposant
        /// ou le Noyau en jouant resterait sans effet jusqu'au prochain achat.
        /// </summary>
        public Observable<Unit> Edited => _edited;

        private readonly Subject<Unit> _edited = new Subject<Unit>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            _edited.OnNext(Unit.Default);
        }
#endif

        /// <summary>UT à remplir entre deux relevés, au départ.</summary>
        public float TraceReadoutMaxIntervalUT => _traceReadoutMaxIntervalUT;

        /// <summary>UT entre deux relevés, plancher atteint avec un parc conséquent.</summary>
        public float TraceReadoutMinIntervalUT => _traceReadoutMinIntervalUT;
        public float TraceReadoutPessimism => _traceReadoutPessimism;

        /// <summary>
        /// Puissance du capteur : le brouillard est divisé par
        /// <c>1 + TraceReadoutSensorPower × log10(1 + TFlops nettes / échelle)</c>. À 0, la
        /// mécanique est éteinte.
        /// </summary>
        public float TraceReadoutSensorPower => _traceReadoutSensorPower;

        /// <summary>
        /// Échelle du capteur, en TFlops. En dessous, le Hardware n'achète pas de visibilité.
        /// Sans elle, le logarithme donnait l'essentiel du rachat aux tout premiers TFlops.
        /// </summary>
        public float TraceReadoutSensorReferenceTFlops => _traceReadoutSensorReferenceTFlops;

        public double TFlopsTimeCompression => _tflopsTimeCompression;
        public double TFlopsCompressionExponent => _tflopsCompressionExponent;
        public double TFlopsYieldExponent => _tflopsYieldExponent;
        public float AbsoluteMinCycleDuration => _absoluteMinCycleDuration;

        /// <summary>Base du multiplicateur de coût du Noyau : <c>base ^ points dépensés</c>.</summary>
        public float CoreCostBase => _coreCostBase;

        /// <summary>Plancher du multiplicateur de coût du Noyau.</summary>
        public float CoreCostFloor => _coreCostFloor;

        /// <summary>Rendement ajouté par point dépensé, sur les Datas et les TFlops nettes.</summary>
        public float CoreYieldPerPoint => _coreYieldPerPoint;

        public double BaseStartingMoney => _baseStartingMoney;
        public double PrestigeFirstThresholdDatas => _prestigeFirstThresholdDatas;
        public double PrestigeThresholdGrowth => _prestigeThresholdGrowth;
        public double SeizedContributionRatio => _seizedContributionRatio;
        public IReadOnlyList<CleanExitTier> CleanExitTiers => _cleanExitTiers;
        public float OverclockWarpSeconds => _overclockWarpSeconds;
        public float OverclockPenaltySeconds => _overclockPenaltySeconds;
        public float OverclockMinIntervalSeconds => _overclockMinIntervalSeconds;

        /// <summary>Échelle commune du Wipe des Proxies, avant l'Interception.</summary>
        public float ProxyWipeScale => _proxyWipeScale;

        public float EmergencyTraceRemoval => _emergencyTraceRemoval;
        public float EmergencySacrificeStep => _emergencySacrificeStep;
        public float EmergencyMaxSacrifice => _emergencyMaxSacrifice;
    }
}
