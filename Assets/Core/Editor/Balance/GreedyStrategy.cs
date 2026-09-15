using Core.Models.Economy;
using Core.Services.Economy;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Le joueur de référence : il achète ce qui rembourse le plus vite, et se défend quand la
    /// mort approche.
    ///
    /// <b>Sa posture défensive est le paramètre qui compte.</b> `SafetySeconds` dit combien de
    /// secondes de survie il exige avant de remettre son argent dans la production. À 0 il ne
    /// pose jamais un Proxy, à 900 il sur-investit dans la défense. Comparer ces réglages dans les
    /// mêmes conditions est précisément ce qui a permis de trancher κ et de prouver que
    /// l'arbitrage existait.
    ///
    /// <b>Aucune formule d'équilibrage n'est recopiée ici.</b> Pour estimer ce que rapporterait un
    /// achat, la stratégie construit un <see cref="UpgradeModel"/> jetable au niveau visé et lit
    /// son rendement : c'est le vrai <c>RecalculateCache</c> qui répond. C'est ce qui distingue cet
    /// outil du simulateur Python, dont la réplique avait fini par diverger de l'original.
    /// </summary>
    public sealed class GreedyStrategy : ISimulationStrategy
    {
        /// <summary>Garde-fou : un point de décision ne doit pas boucler indéfiniment.</summary>
        private const int MaxPurchasesPerDecision = 40;

        /// <summary>Part du temps encore passée à cliquer une fois le joueur bien fatigué.</summary>
        private const float MinimumDutyCycle = 0.12f;

        /// <summary>Minutes au bout desquelles la fatigue a produit tout son effet.</summary>
        private const float FatigueRampMinutes = 12f;

        /// <summary>Plafond de clics rattrapés sur un seul pas de simulation.</summary>
        private const int MaxClicksPerStep = 60;

        /// <summary>Sous ce facteur de croissance, la run plafonne : mieux vaut repartir à neuf.</summary>
        private const double PlateauGrowth = 1.6d;

        private readonly float _safetySeconds;

        /// <summary>
        /// Fraction de Trace que ce joueur veut voir dissipée en permanence, de 0 à l'asymptote.
        /// C'est SA posture défensive, et le seul paramètre qui décrit honnêtement le troisième
        /// pilier : les Proxies ne s'achètent pas en urgence, ils se maintiennent.
        /// </summary>
        private readonly float _targetReduction;
        private readonly float _exitTraceFraction;
        private readonly float _reactionSeconds;

        /// <summary>
        /// Le joueur se fie-t-il au CHIFFRE AFFICHÉ plutôt qu'au bord de la fourchette ?
        ///
        /// C'est là que se joue tout le modèle de risque. Le prudent lit le bord haut : il sort
        /// trop tôt et laisse des Datas sur la table. Le gourmand lit le nombre, qui date — et
        /// se fait saisir. Le brouillard ne tue pas en soi, il force à choisir son poison.
        /// </summary>
        private readonly bool _trustsDisplayedNumber;

        /// <summary>
        /// Le joueur n'achète QUE des Scripts — ni Hardware, ni Proxy.
        ///
        /// Reproduit la posture d'un joueur qui découvre le jeu et se rue sur ce qui rapporte,
        /// en ignorant les deux autres piliers. Sert à vérifier que le modèle correspond à ce
        /// qui se passe dans une vraie partie : c'est la seule façon de distinguer un défaut de
        /// build d'un défaut de game design.
        /// </summary>
        private readonly bool _scriptsOnly;

        /// <summary>Cadence de clic PENDANT une rafale, en clics par seconde.</summary>
        private readonly float _clicksPerSecond;

        /// <summary>Part du temps réellement passée à cliquer, au début de la run.</summary>
        private readonly float _clickDutyCycle;

        /// <summary>Reliquat fractionnaire de clics, reporté d'un pas de simulation au suivant.</summary>
        private float _pendingClicks;

        /// <summary>
        /// Le joueur ne sort JAMAIS de lui-même : il joue jusqu'à la saisie fédérale.
        ///
        /// Toutes les autres postures exfiltrent, donc aucune ne mesure le PLAFOND DUR d'une run —
        /// combien de temps l'A.M.I. laisse avant de tomber, indépendamment de l'habileté du
        /// joueur à sortir au bon moment. C'est ce chiffre-là qui dit si une première run est
        /// brutale ou confortable, et lui seul.
        /// </summary>
        private readonly bool _runsUntilSeized;

        /// <summary>
        /// Familles de bonus que ce joueur n'achètera JAMAIS. Sert à mesurer ce qu'une branche
        /// apporte réellement : on rejoue la même campagne sans elle, et l'écart de durée est sa
        /// contribution. C'est la seule façon de répondre « ce nœud est-il utile ? » autrement
        /// que par une intuition.
        /// </summary>
        private readonly HashSet<PrestigeBonusType> _ignoredBonuses;

        /// <summary>
        /// Instant du dernier coup d'œil à la jauge. Sert au modèle de joueur imparfait : entre
        /// deux regards, il ne voit rien monter.
        /// </summary>
        private float _lastGlance;

        /// <summary>Tampon réutilisé entre les évaluations : elles sont nombreuses.</summary>
        private readonly List<UpgradeModel> _ownedScripts = new List<UpgradeModel>(16);

        /// <param name="reactionSeconds">
        /// Secondes entre deux consultations de la jauge. À 0 le joueur est omniscient : il sort
        /// toujours à la fraction voulue, à la frame près, et ne meurt donc JAMAIS.
        ///
        /// C'est une limite du modèle, pas du jeu : un agent parfait ne peut pas mesurer un Game
        /// Over. Avec un délai réaliste, la saisie redevient possible — et la fréquence des
        /// saisies devient une mesure de la brutalité réelle du début de partie.
        /// </param>
        public GreedyStrategy(string name, float safetySeconds,
                              float exitTraceFraction = 0.95f, float reactionSeconds = 0f,
                              bool trustsDisplayedNumber = false, bool scriptsOnly = false,
                              bool runsUntilSeized = false,
                              HashSet<PrestigeBonusType> ignoredBonuses = null,
                              float clicksPerSecond = 8f, float clickDutyCycle = 0.62f,
                              float targetReduction = 0.5f)
        {
            _targetReduction = targetReduction;
            _clicksPerSecond = clicksPerSecond;
            _clickDutyCycle = clickDutyCycle;
            _ignoredBonuses = ignoredBonuses;
            _scriptsOnly = scriptsOnly;
            _runsUntilSeized = runsUntilSeized;
            Name = name;
            _safetySeconds = safetySeconds;
            _exitTraceFraction = exitTraceFraction;
            _reactionSeconds = reactionSeconds;
            _trustsDisplayedNumber = trustsDisplayedNumber;
        }

        public string Name { get; }

        /// <summary>Les trois postures qui ont servi à trancher l'arbitrage défensif.</summary>
        public static GreedyStrategy NoDefense() => new GreedyStrategy("aucun Proxy", 0f, targetReduction: 0f);
        public static GreedyStrategy Balanced() => new GreedyStrategy("défense modérée", 120f, targetReduction: 0.5f);
        public static GreedyStrategy HeavyDefense() => new GreedyStrategy("défense lourde", 900f, targetReduction: 0.8f);

        /// <summary>
        /// Un joueur humain : il se défend raisonnablement, pousse sa chance plus loin (90 % de
        /// jauge plutôt que 95), et ne regarde la Trace que toutes les huit secondes.
        ///
        /// C'est la SEULE posture qui puisse produire une saisie fédérale, donc la seule qui
        /// mesure vraiment si le début de partie punit.
        /// </summary>
        public static GreedyStrategy Human() =>
            new GreedyStrategy("humain prudent", 120f, exitTraceFraction: 0.90f, reactionSeconds: 8f);

        /// <summary>
        /// Le joueur gourmand : il pousse jusqu'à 95 % du chiffre AFFICHÉ, sans se méfier de son
        /// âge. C'est la seule posture qui puisse se faire saisir, donc la seule qui mesure si le
        /// brouillard mord — et à quel prix pour qui le sous-estime.
        /// </summary>
        public static GreedyStrategy HumanGreedy() =>
            new GreedyStrategy("humain gourmand", 120f, exitTraceFraction: 0.95f,
                               reactionSeconds: 8f, trustsDisplayedNumber: true);

        /// <summary>
        /// Le joueur qui VISE UN PALIER d'extraction et sort dès que le chiffre affiché le lui
        /// annonce franchi.
        ///
        /// <b>C'est le modèle de joueur pertinent depuis que le bonus est en paliers.</b> Les
        /// postures « prudent / gourmand » comparaient deux façons de lire la même jauge, ce qui
        /// n'a plus grand sens : au-dessus du dernier seuil, s'attarder ne rapporte plus rien et
        /// pousser jusqu'à 97 % est simplement une faute. La vraie question est devenue « quel
        /// palier je vais chercher ? », et c'est ce que compare cette posture — viser haut paie
        /// mieux, mais le relevé peut annoncer le seuil franchi trop tard.
        /// </summary>
        /// <summary>
        /// Les trois postures qui mesurent le PLAFOND DUR d'une run : elles ne sortent jamais et
        /// vont au Game Over. L'écart entre elles chiffre exactement ce que la défense achète.
        /// </summary>
        public static GreedyStrategy UntilSeizedAcquisitionOnly() =>
            new GreedyStrategy("acquisition seule", 0f, scriptsOnly: true, runsUntilSeized: true,
                               targetReduction: 0f);

        public static GreedyStrategy UntilSeizedBalanced() =>
            new GreedyStrategy("défense modérée", 120f, runsUntilSeized: true, targetReduction: 0.5f);

        public static GreedyStrategy UntilSeizedHeavy() =>
            new GreedyStrategy("défense lourde", 900f, runsUntilSeized: true, targetReduction: 0.8f);

        /// <summary>Le joueur qui ne mise que sur l'acquisition. Reproduit une vraie partie de découverte.</summary>
        public static GreedyStrategy AcquisitionOnly() =>
            new GreedyStrategy("acquisition seule", 0f, exitTraceFraction: 0.90f,
                               reactionSeconds: 8f, trustsDisplayedNumber: true, scriptsOnly: true);

        public static GreedyStrategy TierHunter(string label, float tierThreshold) =>
            new GreedyStrategy(label, 120f, exitTraceFraction: tierThreshold,
                               reactionSeconds: 8f, trustsDisplayedNumber: true);

        /// <summary>
        /// Le même joueur, privé d'une ou plusieurs familles de nœuds de prestige.
        ///
        /// ⚠️ Se lit « que se passe-t-il si le joueur évite cette famille », JAMAIS « combien
        /// vaut cette famille ». L'isolation inverse — n'autoriser qu'une famille — a été
        /// essayée et ne peut pas fonctionner : l'arbre est gaté par des prérequis en chaîne
        /// jusqu'à la racine, donc interdire les autres familles rend l'arbre entièrement
        /// inachetable et toutes les variantes rendent le résultat d'un joueur sans prestige.
        /// </summary>
        public static GreedyStrategy TierHunterWithout(string label, params PrestigeBonusType[] ignored) =>
            new GreedyStrategy(label, 120f, exitTraceFraction: 0.90f, reactionSeconds: 8f,
                               trustsDisplayedNumber: true,
                               ignoredBonuses: new HashSet<PrestigeBonusType>(ignored));

        // ------------------------------------------------------------------
        // Achats pendant la run
        // ------------------------------------------------------------------
        /// <summary>
        /// Le clic d'Overclock, modélisé comme une MAIN HUMAINE et non comme un auto-clicker.
        ///
        /// Un joueur clique par rafales entrecoupées de pauses — il lit, il achète, il souffle —
        /// et il se fatigue : les pauses s'allongent à mesure que la run dure. Le modèle ne
        /// simule pas les rafales une à une : l'apport d'un clic étant LINÉAIRE et sans temps de
        /// recharge, une cadence moyenne donne exactement le même résultat qu'une alternance
        /// rafale/pause, pour un dixième du coût de calcul.
        ///
        /// Ce que ça corrige. Le harnais ne cliquait pas du tout, et se trompait donc d'un
        /// facteur 87 sur les Datas d'une première run — 2,9 M contre 196 M en partie réelle.
        /// Tout réglage exprimé en Datas absolues était calibré sur un joueur qui n'existe pas.
        /// </summary>
        public void OnTick(SimulationHarness h, float deltaTime)
        {
            if (_clicksPerSecond <= 0f || deltaTime <= 0f) return;

            // La fatigue ronge le temps effectivement passé à cliquer, jamais la vitesse : un
            // joueur fatigué ne clique pas plus lentement, il clique moins souvent.
            float minutes = h.Session.RunElapsedSeconds / 60f;
            float duty = Mathf.Lerp(_clickDutyCycle, MinimumDutyCycle,
                                    Mathf.Clamp01(minutes / FatigueRampMinutes));

            _pendingClicks += _clicksPerSecond * duty * deltaTime;

            int clicks = (int)_pendingClicks;
            if (clicks <= 0) return;

            _pendingClicks -= clicks;

            // Garde-fou : un grand pas de simulation ne doit pas se transformer en millier de
            // clics d'un coup, ce qu'aucune main ne ferait.
            if (clicks > MaxClicksPerStep) clicks = MaxClicksPerStep;

            for (int i = 0; i < clicks; i++)
            {
                h.Overclock.TriggerManualOverclock();
            }
        }

        public void OnDecisionPoint(SimulationHarness h)
        {
            // Relancer les cycles à la main, D'ABORD. Sous son seuil d'automatisation, un Script
            // ne repart pas seul : c'est le joueur qui clique. Sans ça la simulation ne produit
            // rien du tout — constaté au premier essai, `SCR_01` acheté et jamais démarré.
            //
            // Les relances n'ont lieu QU'aux points de décision, ce qui modélise au passage la
            // latence humaine : un joueur ne relance pas quinze Scripts dans la même frame.
            RestartIdleScripts(h);

            // La défense passe EN PREMIER : ce qu'elle consomme n'ira pas aux Scripts, et c'est
            // exactement le coût d'opportunité qu'on cherche à mesurer.
            //
            // Le critère est une POSTURE À MAINTENIR — « garder au moins tant de réduction » — et
            // non plus « acheter quand il me reste moins de N secondes à vivre ».
            //
            // L'ancienne règle faisait jouer le modèle à DEUX PILIERS SUR TROIS, ce qui invalidait
            // toute mesure : à 120 s de seuil et des runs de 174 s, le joueur commençait au-dessus
            // du seuil, n'achetait rien, et ne touchait aux Proxies qu'en urgence — trop tard, la
            // Trace déjà haute. Relevé : 16 % de réduction pour une asymptote à 85 %.
            //
            // Une posture est aussi la seule formulation qui capture la dilution : la dissipation
            // se dévalue toute seule à mesure que l'économie grossit, donc tenir un niveau exige
            // de réinvestir en permanence. C'est précisément l'arbitrage du pilier.
            if (!_scriptsOnly && _targetReduction > 0f)
            {
                for (int i = 0; i < MaxPurchasesPerDecision; i++)
                {
                    if (h.Ticker.CurrentTraceReduction >= _targetReduction) break;
                    if (!BuyBestProxy(h)) break;
                }
            }

            for (int i = 0; i < MaxPurchasesPerDecision; i++)
            {
                if (!BuyBestProducer(h)) break;
            }
        }

        /// <summary>
        /// Redémarre les cycles des Scripts possédés qui sont à l'arrêt et pas encore automatisés.
        /// Les automatisés repartent seuls au Tick suivant, les toucher ne servirait à rien.
        /// </summary>
        private static void RestartIdleScripts(SimulationHarness h)
        {
            IReadOnlyList<UpgradeModel> scripts = h.Upgrades.GetUpgradesOfType(UpgradeType.Script);
            for (int i = 0; i < scripts.Count; i++)
            {
                UpgradeModel model = scripts[i];
                if (!model.IsOwned || model.IsAutomated) continue;
                if (h.CycleRunner.IsRunning(model.Config.Id)) continue;

                h.CycleRunner.TryStartCycle(model.Config.Id);
            }
        }

        /// <summary>Secondes avant la saisie au débit courant. Infini si la jauge ne monte pas.</summary>
        public static float SurvivalSeconds(SimulationHarness h)
        {
            float cap = h.Threat.TraceCap;
            float current = h.Threat.CurrentTrace.CurrentValue;

            // Le débit n'est pas exposé : on le reconstitue depuis la jauge, seule source de
            // vérité. Un pas de simulation suffit à le rendre observable, mais tant que la trace
            // n'a pas bougé on considère la survie infinie.
            float remaining = cap - current;
            if (remaining <= 0f) return 0f;

            float debit = h.LastTraceDebitPerSecond;
            return debit <= 0f ? float.PositiveInfinity : remaining / debit;
        }

        private bool BuyBestProxy(SimulationHarness h)
        {
            double money = h.Currencies.Money.Amount.CurrentValue;
            IReadOnlyList<UpgradeModel> proxies = h.Upgrades.GetUpgradesOfType(UpgradeType.Proxy);

            UpgradeModel best = null;
            double bestScore = 0d;

            for (int i = 0; i < proxies.Count; i++)
            {
                UpgradeModel model = proxies[i];
                double cost = model.GetCurrentCost();
                if (cost <= 0d || cost > money) continue;

                // Le facteur de compression s'applique à TOUS les Proxies : il se simplifie
                // dans un classement, inutile de le calculer.
                double gain = ProbeTraceMagnitude(h, model, 1) - model.GetTraceMagnitudePerSecond();
                if (gain <= 0d) continue;

                double score = gain / cost;
                if (score > bestScore)
                {
                    best = model;
                    bestScore = score;
                }
            }

            return best != null && h.Upgrades.TryPurchaseUpgrade(best.Config.Id, BuyQuantity.X1);
        }

        private bool BuyBestProducer(SimulationHarness h)
        {
            double money = h.Currencies.Money.Amount.CurrentValue;

            UpgradeModel best = null;
            double bestScore = 0d;

            IReadOnlyList<UpgradeModel> scripts = h.Upgrades.GetUpgradesOfType(UpgradeType.Script);
            for (int i = 0; i < scripts.Count; i++)
            {
                UpgradeModel model = scripts[i];
                double cost = model.GetCurrentCost();
                if (cost <= 0d || cost > money) continue;

                // Un Script n'influence que lui-même : la sonde suffit, et elle est exacte.
                double gain = ProbeYieldPerSecond(h, model, 1, h.Upgrades.TotalTFlops.CurrentValue)
                            - model.GetYieldPerSecond();
                if (gain <= 0d) continue;

                double score = gain / cost;
                if (score > bestScore)
                {
                    best = model;
                    bestScore = score;
                }
            }

            if (_scriptsOnly) return best != null && h.Upgrades.TryPurchaseUpgrade(best.Config.Id, BuyQuantity.X1);

            IReadOnlyList<UpgradeModel> hardware = h.Upgrades.GetUpgradesOfType(UpgradeType.Hardware);
            CollectOwnedScripts(scripts);

            for (int i = 0; i < hardware.Count; i++)
            {
                UpgradeModel model = hardware[i];
                double cost = model.GetCurrentCost();
                if (cost <= 0d || cost > money) continue;

                // Un Hardware agit sur TOUT le parc de Scripts, par les TFlops. On reconstitue la
                // capacité qu'il donnerait, puis on sonde chaque Script possédé à cette valeur —
                // aucune formule dupliquée, c'est le modèle qui répond.
                double newTFlops = ProjectTotalTFlops(h, hardware, model);
                double gain = 0d;

                for (int s = 0; s < _ownedScripts.Count; s++)
                {
                    UpgradeModel script = _ownedScripts[s];
                    gain += ProbeYieldPerSecond(h, script, 0, newTFlops) - script.GetYieldPerSecond();
                }

                if (gain <= 0d) continue;

                double score = gain / cost;
                if (score > bestScore)
                {
                    best = model;
                    bestScore = score;
                }
            }

            return best != null && h.Upgrades.TryPurchaseUpgrade(best.Config.Id, BuyQuantity.X1);
        }

        private void CollectOwnedScripts(IReadOnlyList<UpgradeModel> scripts)
        {
            _ownedScripts.Clear();
            for (int i = 0; i < scripts.Count; i++)
            {
                if (scripts[i].IsOwned) _ownedScripts.Add(scripts[i]);
            }
        }

        /// <summary>
        /// Capacité de calcul totale si <paramref name="candidate"/> gagnait un niveau.
        /// Miroir de l'agrégation d'UpgradeManager, moins la tranche immobilisée du Data Wiper —
        /// elle vaut zéro hors usage du bouton, que cette stratégie n'actionne jamais.
        /// </summary>
        private double ProjectTotalTFlops(SimulationHarness h, IReadOnlyList<UpgradeModel> hardware,
                                          UpgradeModel candidate)
        {
            double sum = 0d;
            for (int i = 0; i < hardware.Count; i++)
            {
                sum += hardware[i] == candidate
                    ? ProbeCurrentYield(h, candidate, 1)
                    : hardware[i].GetCurrentYield();
            }

            return (sum + h.Prestige.StartingComputerPower.CurrentValue)
                 * h.Prestige.GlobalComputeMultiplier.CurrentValue;
        }

        // ------------------------------------------------------------------
        // Sondes : un modèle jetable au niveau visé, donc le VRAI calcul
        // ------------------------------------------------------------------
        private UpgradeModel BuildProbe(SimulationHarness h, UpgradeModel source, int extraLevels, double tflops)
        {
            var probe = new UpgradeModel(source.Config, h.Balancing,
                                         source.CurrentLevel.CurrentValue + extraLevels);
            probe.SetSpecificBonuses(h.Prestige.GetSpecificBonuses(source.Config.Id));
            probe.SetGlobalCostMultiplierReduction(h.Prestige.CostMultiplierReduction.CurrentValue);
            probe.SetTFlops(tflops);
            return probe;
        }

        private double ProbeYieldPerSecond(SimulationHarness h, UpgradeModel source, int extraLevels, double tflops)
        {
            UpgradeModel probe = BuildProbe(h, source, extraLevels, tflops);
            double value = probe.GetYieldPerSecond();
            probe.Dispose();
            return value;
        }

        private double ProbeCurrentYield(SimulationHarness h, UpgradeModel source, int extraLevels)
        {
            UpgradeModel probe = BuildProbe(h, source, extraLevels, h.Upgrades.TotalTFlops.CurrentValue);
            double value = probe.GetCurrentYield();
            probe.Dispose();
            return value;
        }

        private double ProbeTraceMagnitude(SimulationHarness h, UpgradeModel source, int extraLevels)
        {
            UpgradeModel probe = BuildProbe(h, source, extraLevels, h.Upgrades.TotalTFlops.CurrentValue);
            double value = probe.GetTraceMagnitudePerSecond();
            probe.Dispose();
            return value;
        }

        // ------------------------------------------------------------------
        // Sortie de run
        // ------------------------------------------------------------------
        public bool ShouldExfiltrate(SimulationHarness h, in RunProgress p)
        {
            // Court-circuit AVANT toute autre règle, y compris la détection de plateau : une
            // posture « jusqu'à la saisie » qui sortirait sur un plateau mesurerait autre chose
            // que ce qu'on lui demande.
            if (_runsUntilSeized) return false;

            // Une run neuve remet le compteur de regards à zéro : l'instance de stratégie est
            // réutilisée d'une run à l'autre par le pilote de campagne. Le reliquat de clics part
            // avec, pour la même raison.
            if (p.ElapsedSeconds < _lastGlance)
            {
                _lastGlance = 0f;
                _pendingClicks = 0f;
            }

            if (_reactionSeconds > 0f)
            {
                if (p.ElapsedSeconds - _lastGlance < _reactionSeconds) return false;
                _lastGlance = p.ElapsedSeconds;
            }

            // Le joueur ne peut sortir que si le bouton est DÉVERROUILLÉ, c'est-à-dire au-delà
            // d'un seuil de Datas sur la run.
            //
            // Cette garde lisait « au moins un point à gagner », ce qui n'a plus de sens depuis
            // que les points se décrochent sur un cumul de campagne : tant que le cumul n'a pas
            // franchi le premier palier, une run n'en rapporte aucun — et le joueur refusait donc
            // de sortir, jouait jusqu'à la saisie, ne banquait rien, et ne franchissait jamais ce
            // palier. Mesuré : quinze runs, quinze saisies, zéro exfiltration, sur les quatre
            // postures à la fois.
            //
            // Exfiltrer reste utile même sans point à la clé : ce qu'on ramène alimente le cumul
            // et rapproche du palier suivant. Se faire saisir, non.
            if (p.RunMoney < h.Balancing.ExfiltrationUnlockDatas) return false;

            // Un joueur qui a un temps de réaction lit aussi l'INTERFACE, pas la jauge interne :
            // il décide contre le bord de la zone d'incertitude. C'est ce couple — délai de
            // réaction ET capteur imparfait — qui rend une saisie fédérale possible. Les postures
            // omniscientes gardent la vérité, et servent de référence.
            float observed;
            if (_reactionSeconds <= 0f) observed = p.TraceFraction;              // omniscient
            else if (_trustsDisplayedNumber) observed = p.ReadoutLastKnownFraction; // gourmand
            else observed = p.ReadoutMaxFraction;                                 // prudent

            if (observed >= _exitTraceFraction) return true;

            // Plateau : la run ne monte plus assez pour mériter le risque qu'on prend.
            return p.GrowthSinceMark > 0d && p.GrowthSinceMark < PlateauGrowth;
        }

        // ------------------------------------------------------------------
        // Achats de prestige
        // ------------------------------------------------------------------
        /// <summary>
        /// Les nœuds débloqués les moins chers d'abord. Simple, mais monotone : aucun nœud de cet
        /// arbre n'est un mauvais achat. La règle de niveau requis fait le reste — pour ouvrir un
        /// enfant, il faut d'abord monter le parent au rang exigé.
        /// </summary>
        public void SpendCpuCycles(SimulationHarness h)
        {
            while (true)
            {
                string cheapestId = null;
                double cheapestCost = double.MaxValue;
                double budget = h.Currencies.CpuCycles.Amount.CurrentValue;

                IReadOnlyList<PrestigeConfigSO> all = h.PrestigeCatalog.GetAllUpgrades();
                for (int i = 0; i < all.Count; i++)
                {
                    PrestigeConfigSO config = all[i];
                    int level = h.Prestige.GetLevel(config.Id);
                    if (level >= config.MaxLevel) continue;
                    if (_ignoredBonuses != null && _ignoredBonuses.Contains(config.BonusType)) continue;
                    if (!h.Prestige.IsUnlocked(config)) continue;

                    double cost = config.GetCostAtLevel(level);
                    if (cost > budget || cost >= cheapestCost) continue;

                    cheapestId = config.Id;
                    cheapestCost = cost;
                }

                if (cheapestId == null) return;
                if (!h.Prestige.TryPurchasePrestige(cheapestId)) return;
            }
        }
    }
}
