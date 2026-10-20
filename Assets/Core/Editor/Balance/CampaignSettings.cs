using Core.Models.Economy;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Tout ce qu'une campagne simulée prend en entrée : les leviers d'équilibrage que le GD
    /// autorise (appliqués à une COPIE du BalancingConfig, jamais à l'asset), le comportement du
    /// joueur de référence, et les bornes de la simulation.
    ///
    /// Les valeurs par défaut du joueur sont un point de départ, à caler sur les runs jouées par
    /// Bertrand (<c>journal-runs.csv</c>) : une simulation ne mesure que son bot.
    /// </summary>
    public sealed class CampaignSettings
    {
        // ---- Leviers autorisés par le GD (GDD, « Les cibles de la campagne ») ----------------

        /// <summary>
        /// Seuil de victoire en TFlops nettes. La cible du GD est 1e12, c'est la valeur par défaut
        /// ici ; l'asset porte encore 5e11, provisoire.
        /// </summary>
        public double VictoryTFlops = 1e12;

        /// <summary>Datas de cumul pour la première boîte de points.</summary>
        public double PrestigeFirstThresholdDatas;

        /// <summary>Croissance d'une boîte à la suivante (1,12 au 2026-10-20).</summary>
        public double PrestigeThresholdGrowth;

        /// <summary>Échelle commune du Wipe des Proxies.</summary>
        public float ProxyWipeScale;

        /// <summary>Exposant des TFlops brutes dans la vitesse de l'A.M.I.</summary>
        public float AmiSpeedExponent;

        // ---- Le joueur de référence : « joueur actif moyen » du GD ---------------------------

        /// <summary>
        /// Sortie visée à la première run, sur l'AIGUILLE (le dernier relevé), comme un humain.
        /// ⚠️ La sortie exige au moins un point : une première run qui n'en rapporte aucun ne peut
        /// pas sortir, quel que soit ce seuil — elle finit saisie.
        /// </summary>
        public float FirstRunExitFraction = 0.5f;

        /// <summary>Sortie visée ensuite : « majoritairement à 75 % » (GD).</summary>
        public float ExitFraction = 0.75f;

        /// <summary>Sortie d'urgence quand la borne pessimiste du brouillard atteint ce niveau.</summary>
        public float DangerFraction = 0.97f;

        /// <summary>
        /// Data Wiper (s'il est débloqué) quand la sortie est encore verrouillée et que la borne
        /// pessimiste atteint ce niveau.
        /// </summary>
        public float WiperFraction = 0.9f;

        /// <summary>
        /// Overclock « pour se sortir d'un mauvais pas » (GD) : sortie verrouillée, danger, et le
        /// premier point de la run à portée de ce nombre d'appuis. 0 désactive le geste.
        /// </summary>
        public int EscapeOverclockReach = 3;

        /// <summary>Overclocks « pour forcer un palier juste avant la sortie » (GD), par run.</summary>
        public int PreExitOverclocks = 3;

        /// <summary>Fenêtre, sous la sortie visée et sur l'aiguille, où ces Overclocks partent.</summary>
        public float PreExitWindow = 0.05f;

        /// <summary>
        /// Une décision toutes les N secondes de jeu : c'est aussi le délai de réaction, donc le
        /// temps perdu à relancer un Script manuel (en moyenne la moitié de cet intervalle).
        /// </summary>
        public float DecisionIntervalSeconds = 0.5f;

        /// <summary>Un Proxy s'achète s'il rend au moins ce multiple de son prix en revenu, pendant le répit qu'il offre.</summary>
        public float ProxyPaybackFactor = 1f;

        /// <summary>
        /// Un Proxy ne s'achète que si l'aiguille montre au moins cette part de son Wipe : au-delà,
        /// trop d'UT partiraient dans le vide (l'excédent est perdu).
        /// </summary>
        public float ProxyWasteTolerance = 0.8f;

        /// <summary>
        /// Proxies achetés par le bot sur une run, au plus (la Routine Pare-Feu n'y compte pas).
        /// Sans plafond, la règle de rentabilité ci-dessus suffit à figer la jauge dès que le
        /// revenu dépasse le prix des Proxies : la run ne finit plus (constaté le 2026-10-20). Un
        /// joueur qui « sort à 75 % » n'en achète pas sans fin — à caler sur la colonne
        /// <c>proxies</c> du journal. 0 = aucun Proxy, négatif = sans plafond.
        /// </summary>
        public int MaxProxiesPerRun = 40;

        /// <summary>Data Wiper utilisé par le bot quand la sortie est verrouillée et la saisie proche.</summary>
        public bool UseDataWiper = true;

        /// <summary>
        /// Arbre « Blindage d'abord » (choix de Bertrand) : si le nœud prioritaire ouvert est trop
        /// cher, le bot garde ses points au lieu de les dépenser ailleurs.
        /// </summary>
        public bool SaveForPriorityNode = true;

        /// <summary>
        /// Temps passé sur l'écran de fin entre deux runs (bilan, achats dans l'arbre), compté dans
        /// la durée de campagne. Le bot, lui, y passe zéro seconde.
        /// </summary>
        public float SecondsBetweenRuns = 20f;

        // ---- La simulation -------------------------------------------------------------------

        /// <summary>Pas de temps, en secondes de jeu.</summary>
        public float StepSeconds = 0.1f;

        /// <summary>Graine du hasard (coup critique).</summary>
        public int Seed = 1337;

        /// <summary>
        /// Les N premières runs sont tracées geste par geste (achats, Overclocks, sortie) dans un
        /// fichier à part : c'est ce qu'on compare à une run jouée pour caler le bot. 0 = aucune.
        /// </summary>
        public int TraceRuns = 3;

        /// <summary>Bornes de sécurité : la campagne s'arrête là, victoire ou non.</summary>
        public int MaxRuns = 250;
        public float MaxCampaignHours = 40f;
        public float MaxRunMinutes = 45f;

        /// <summary>Les leviers à leurs valeurs actuelles dans l'asset (victoire exceptée) ; le reste par défaut.</summary>
        public static CampaignSettings FromAsset(BalancingConfigSO balancing)
        {
            return new CampaignSettings
            {
                PrestigeFirstThresholdDatas = balancing.PrestigeFirstThresholdDatas,
                PrestigeThresholdGrowth = balancing.PrestigeThresholdGrowth,
                ProxyWipeScale = balancing.ProxyWipeScale,
                AmiSpeedExponent = balancing.AmiSpeedExponent,
            };
        }
    }
}
