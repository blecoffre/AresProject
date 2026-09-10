using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Platform;
using Core.Services.Security;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Assemble le débit de Trace de la frame et l'applique à la jauge.
    ///
    /// Le calcul est réparti sur trois responsables, et pas par goût de la découpe :
    ///  - l'UpgradeManager tient les parts STATIQUES (Hardware possédés, dissipation des
    ///    Proxies), qui ne bougent qu'à l'achat et restent donc en cache ;
    ///  - le ScriptCycleRunner tient la part DYNAMIQUE, car lui seul sait quels cycles tournent
    ///    à cet instant — un Script ne laisse de trace que pendant un cycle actif ;
    ///  - ce ticker combine les deux, applique la réduction de prestige, puis convertit la
    ///    puissance de dissipation en FRACTION de réduction par une courbe saturante.
    ///
    /// Cette dernière étape est le correctif structurel du 2026-08-31 : tant que la dissipation
    /// était soustraite platement, elle pouvait dépasser une génération bornée et rendait le
    /// joueur définitivement indétectable pour 2,7 M$.
    ///
    /// Aucune allocation, aucune closure, aucun boxing : que des lectures de champs et de
    /// CurrentValue. C'est le standard des boucles de frame du projet.
    /// </summary>
    public class SimulationTicker : ITickable
    {
        private readonly UpgradeManager _upgradeManager;
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly PrestigeManager _prestigeManager;
        private readonly ThreatManager _threatManager;
        private readonly GameSessionManager _sessionManager;
        private readonly GhostCacheSystem _ghostCache;
        private readonly BalancingConfigSO _balancing;
        private readonly ITimeSource _time;

        public SimulationTicker(
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            PrestigeManager prestigeManager,
            ThreatManager threatManager,
            GameSessionManager sessionManager,
            GhostCacheSystem ghostCache,
            BalancingConfigSO balancing,
            ITimeSource time)
        {
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _prestigeManager = prestigeManager;
            _threatManager = threatManager;
            _sessionManager = sessionManager;
            _ghostCache = ghostCache;
            _balancing = balancing;
            _time = time;
        }

        public void Tick()
        {
            // Le plafond suit le parc Hardware. Poussé AVANT toute sortie anticipée : une run
            // stabilisée n'appelle jamais AddThreat, et le plafond resterait alors figé sur sa
            // valeur d'avant l'achat — la jauge afficherait un pourcentage périmé.
            _threatManager.SetCapacity(
                _upgradeManager.TraceCapacityBonus.CurrentValue,
                _prestigeManager.TraceCapacityMultiplier.CurrentValue);

            if (!_sessionManager.IsGameActive.Value) return;

            float deltaTime = _time.DeltaTime;

            // L'Exploit s'écoule AVANT le calcul du débit, dans la même frame : sinon la frame
            // où il expire éteindrait encore les Proxies, et celle où il démarre les laisserait
            // dissiper une dernière fois.
            _ghostCache.TickOverdrive(deltaTime);

            // TraceBrute = (Σ ScriptsActifs + Σ HardwarePossédés) × (1 − RéductionPrestige)
            //
            // La réduction s'applique AVANT la soustraction des Proxies : l'ordre compte, sinon
            // elle rognerait aussi la dissipation et les Proxies deviendraient moins efficaces
            // à mesure que le joueur progresse — exactement l'inverse de l'intention.
            float generated = _cycleRunner.ActiveScriptTracePerSecond
                            + _upgradeManager.HardwareTracePerSecond.CurrentValue;

            float brute = generated * _prestigeManager.TraceReductionMultiplier.CurrentValue;

            // Pendant le Zéro-Day Exploit, la contrepartie est DOUBLE : tous les Proxies
            // s'éteignent, et la génération brute est elle-même multipliée. Le joueur produit
            // cinquante fois plus, mais il est à découvert et la Trace le rattrape d'autant.
            //
            // Le facteur s'applique APRÈS la réduction de prestige, ce qui est sans effet
            // arithmétique — la multiplication commute — mais dit la bonne chose : la réduction
            // passive protège toujours proportionnellement, y compris pendant l'Exploit.
            bool isOverdrive = _ghostCache.IsOverdriveActive.CurrentValue;
            if (isOverdrive) brute *= _ghostCache.EffectiveTraceMultiplier;

            // ------------------------------------------------------------------
            // Réduction SATURANTE, depuis le 2026-08-31 :
            //
            //     R = MaxTraceReduction × D / (D + HalfPointRatio × Brut)
            //
            // Remplace la soustraction plate `Brut − Dissipation`, qui opposait une valeur non
            // bornée à une génération bornée : un unique PRX_01 monté au niveau 100, pour 2,7 M$
            // dans une économie qui atteint 1e13, annulait toute la Trace du jeu — définitivement.
            // Le joueur ne mourait plus que s'il le décidait.
            //
            // Trois propriétés de cette forme, et chacune répond à un défaut constaté :
            //
            //  · R tend vers MaxTraceReduction sans jamais l'atteindre. Une fraction de la Trace
            //    passe TOUJOURS, donc la jauge monte toujours et la saisie reste inéluctable.
            //    L'invulnérabilité n'est plus une question de réglage, elle est arithmétiquement
            //    hors d'atteinte.
            //
            //  · Le coût de chaque tranche explose : passer de 42,5 % à 76,5 % de réduction
            //    demande neuf fois plus de puissance de dissipation, et atteindre 84,9 % en
            //    demande 849 fois. La défense est chère par construction. C'est aussi ce qui rend
            //    tout plafond de niveau inutile — le rendement décroissant EST le plafond, et il
            //    est naturel plutôt qu'arbitraire.
            //
            //  · Seul le RATIO D/Brut compte, jamais la magnitude : la formule se comporte à
            //    l'identique à 1e0 et à 1e13 de Trace. Conséquence voulue et centrale — faire
            //    grossir son économie augmente le Brut, donc DILUE les Proxies déjà achetés. Le
            //    joueur doit réinvestir en permanence, ou assumer le risque. C'est là que naît
            //    l'arbitrage qui manquait au jeu.
            //
            // Les Proxies agissent toujours sur le DÉBIT et jamais sur la jauge : la Trace déjà
            // accumulée ne redescend pas. Seuls le Bouton d'Urgence et le wipe le peuvent —
            // « le FBI n'oublie jamais, sauf si tu formates tout ».
            // ------------------------------------------------------------------
            float reduction = 0f;

            if (!isOverdrive && brute > 0f)
            {
                float dissipationPower = _upgradeManager.ProxyDissipationPower.CurrentValue;

                if (dissipationPower > 0f)
                {
                    float halfPoint = _balancing.DissipationHalfPointRatio * brute;

                    reduction = _balancing.MaxTraceReduction
                              * dissipationPower / (dissipationPower + halfPoint);
                }
            }

            // Charge du Ghost Cache. L'ancienne condition — « la dissipation dépasse la
            // génération » — n'a plus d'objet : avec une réduction asymptotique il n'existe plus
            // d'excédent à capter. Ce qui se paie reste exactement ce que le GDD décrit, un
            // MAINTIEN DE POSTURE DÉFENSIVE, et la charge se remplit toujours en temps et non en
            // magnitude : une seconde tenue au-dessus du seuil vaut une seconde de charge.
            //
            // Différence assumée avec l'ancien comportement : charger n'est plus gratuit. La
            // Trace continue de monter pendant qu'on accumule, là où l'excédent mettait le joueur
            // à l'abri. Se constituer une réserve devient donc un pari sur la jauge, ce qui est
            // le propos même de la mécanique.
            if (!isOverdrive
                && reduction >= _balancing.MaxTraceReduction * _balancing.GhostCacheReductionThreshold)
            {
                _ghostCache.Accumulate(deltaTime);
            }

            float debit = brute * (1f - reduction);

            // Une partie sans aucun générateur donne un débit nul : rien à appliquer.
            if (debit <= 0f) return;

            _threatManager.AddThreat(debit * deltaTime);
        }
    }
}
