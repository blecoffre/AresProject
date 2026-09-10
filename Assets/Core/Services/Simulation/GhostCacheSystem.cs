using Core.Models.Economy;
using Core.Services.Economy;
using R3;
using System;
using UnityEngine;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Le « Ghost Cache » et son déclencheur, le Zéro-Day Exploit.
    ///
    /// Ce système récompense le MAINTIEN D'UNE POSTURE DÉFENSIVE et le transforme en charges,
    /// chacune échangeable contre 30 secondes de production démesurée — pendant lesquelles tous
    /// les Proxies s'éteignent et la Trace brute est elle-même multipliée. Le joueur est à
    /// découvert, deux fois.
    ///
    /// <b>La condition de charge a changé le 2026-08-31.</b> Elle captait l'excédent de
    /// dissipation, c'est-à-dire la part au-delà du point où la Trace cessait de monter. Ce point
    /// n'existe plus : la réduction des Proxies est désormais asymptotique, donc la Trace monte
    /// toujours et il n'y a plus rien à « jeter ». Le seuil porte maintenant sur la FRACTION de
    /// réduction tenue, ce qui dit la même chose que le GDD en des termes qui ont encore un sens.
    /// Conséquence assumée : charger n'est plus gratuit, la jauge continue de grimper pendant ce
    /// temps.
    ///
    /// <b>Le nombre de charges vient du prestige</b>, via le nœud `P_EXPLOIT_CHARGES` : zéro au
    /// départ, donc l'Exploit est verrouillé tant qu'il n'a pas été acheté, et rien ne
    /// s'accumule. Deux autres nœuds ajustent l'effet : `P_EXPLOIT_MULT` (rendement) et
    /// `P_EXPLOIT_TRACE_REDUC` (malus de Trace).
    ///
    /// <b>La charge se remplit en TEMPS, pas en magnitude.</b> Une seconde passée en excédent
    /// vaut une seconde de charge, que l'excédent soit de 1 ou de 100 000. C'est ce qui rend la
    /// mécanique lisible à tous les stades de la partie : un joueur de fin de partie, dont la
    /// dissipation dépasse la génération de plusieurs ordres de grandeur, ne remplit pas la
    /// jauge instantanément. Le prix à payer est un maintien de posture défensive, pas un
    /// empilement de Proxies.
    ///
    /// La règle d'or du GDD tient : l'excédent ne fait PAS redescendre la jauge de Trace. Le
    /// Ghost Cache se remplit donc à la hauteur où le joueur s'est arrêté — à 30 % il a de la
    /// marge, à 80 % déclencher l'Exploit est un pari. C'est là que naît le choix « j'exfiltre
    /// maintenant ou je charge encore ».
    ///
    /// Aucune allocation dans les deux méthodes de frame : que des lectures et des écritures de
    /// champs, plus les notifications R3 des ReactiveProperty — qui ne se déclenchent qu'aux
    /// changements réels grâce aux gardes d'égalité.
    /// </summary>
    public class GhostCacheSystem : IDisposable
    {
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;
        private readonly BalancingConfigSO _balancing;

        /// <summary>
        /// Secondes d'excédent de dissipation à accumuler pour UNE charge. Le nombre de charges
        /// stockables vient du nœud de prestige `P_EXPLOIT_CHARGES`.
        /// </summary>
        public float CapacitySeconds => _balancing.GhostCacheCapacitySeconds;

        /// <summary>Durée du Zéro-Day Exploit, en secondes.</summary>
        public float OverdriveDurationSeconds => _balancing.OverdriveDurationSeconds;

        private readonly ReactiveProperty<float> _chargeSeconds = new(0f);
        private readonly ReactiveProperty<bool> _isOverdriveActive = new(false);
        private readonly ReactiveProperty<float> _overdriveRemaining = new(0f);

        /// <summary>
        /// Charge accumulée, en secondes d'excédent, toutes charges confondues. Bornée par
        /// <see cref="TotalCapacitySeconds"/>, qui dépend du prestige.
        /// </summary>
        public ReadOnlyReactiveProperty<float> ChargeSeconds => _chargeSeconds;

        /// <summary>Le Zéro-Day Exploit tourne-t-il en ce moment.</summary>
        public ReadOnlyReactiveProperty<bool> IsOverdriveActive => _isOverdriveActive;

        /// <summary>Secondes restantes d'Exploit. Vaut 0 hors Overdrive.</summary>
        public ReadOnlyReactiveProperty<float> OverdriveRemaining => _overdriveRemaining;

        /// <summary>
        /// Le Zéro-Day Exploit est-il débloqué. Faux tant que le nœud `P_EXPLOIT_CHARGES` n'a pas
        /// été acheté : la capacité vaut alors zéro et rien ne s'accumule.
        /// </summary>
        public bool IsUnlocked => _prestigeManager.ExploitMaxCharges.CurrentValue > 0;

        /// <summary>Charges complètes stockables, depuis le prestige. Vaut 0 tant que verrouillé.</summary>
        public int MaxCharges => _prestigeManager.ExploitMaxCharges.CurrentValue;

        /// <summary>Charges complètes disponibles à cet instant.</summary>
        public int AvailableCharges => (int)(_chargeSeconds.CurrentValue / CapacitySeconds);

        /// <summary>Plafond d'accumulation, en secondes : une charge pleine par rang acheté.</summary>
        public float TotalCapacitySeconds => MaxCharges * CapacitySeconds;

        /// <summary>Au moins une charge pleine, et Exploit à l'arrêt : le bouton est actionnable.</summary>
        public bool IsReady => AvailableCharges >= 1 && !_isOverdriveActive.CurrentValue;

        /// <summary>
        /// Avancement de la charge EN COURS, de 0 à 1 — pas de la réserve entière. La vue montre
        /// ainsi « deux charges prêtes, la troisième à 40 % » plutôt qu'une barre qui rampe :
        /// c'est la COULEUR qui dit « armé », la barre reste libre de montrer la suite.
        /// Vaut 1 quand toutes les charges sont pleines, et 0 tant que l'Exploit est verrouillé.
        /// </summary>
        public float NormalizedCharge
        {
            get
            {
                if (!IsUnlocked) return 0f;
                if (AvailableCharges >= MaxCharges) return 1f;

                return (_chargeSeconds.CurrentValue % CapacitySeconds) / CapacitySeconds;
            }
        }

        /// <summary>
        /// Multiplicateur de rendement effectif, nœud `P_EXPLOIT_MULT` compris.
        /// ×50 de base, ×100 au rang 10.
        /// </summary>
        public double EffectiveYieldMultiplier =>
            _balancing.OverdriveYieldMultiplier * (1d + _prestigeManager.ExploitYieldBoost.CurrentValue);

        /// <summary>
        /// Malus de Trace effectif, nœud `P_EXPLOIT_TRACE_REDUC` compris. Borné à ×1 : le joueur
        /// peut alléger le malus, jamais le supprimer ni le retourner en bonus.
        /// </summary>
        public float EffectiveTraceMultiplier => Mathf.Max(
            1f,
            _balancing.OverdriveTraceMultiplier - _prestigeManager.ExploitTracePenaltyReduction.CurrentValue);

        public GhostCacheSystem(
            UpgradeManager upgradeManager,
            PrestigeManager prestigeManager,
            BalancingConfigSO balancing)
        {
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _balancing = balancing;
        }

        /// <summary>
        /// Capte une frame de posture défensive tenue. Appelé par le SimulationTicker, et par lui
        /// seul : c'est lui qui tient le calcul du débit et connaît donc la fraction de réduction
        /// effectivement atteinte cette frame.
        ///
        /// Sans effet une fois la réserve pleine — le plafond étant borné par le prestige, on ne
        /// thésaurise jamais à l'infini — ni tant que l'Exploit est verrouillé.
        /// </summary>
        public void Accumulate(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (_isOverdriveActive.CurrentValue) return;

            float capacity = TotalCapacitySeconds;
            if (capacity <= 0f) return; // Exploit encore verrouillé : rien à stocker.

            float current = _chargeSeconds.CurrentValue;
            if (current >= capacity) return;

            _chargeSeconds.Value = Mathf.Min(capacity, current + deltaTime);
        }

        /// <summary>
        /// Fait s'écouler l'Exploit. Appelé à chaque frame par le SimulationTicker, avant le
        /// calcul du débit : le ticker doit savoir dans la MÊME frame si les Proxies sont
        /// éteints, sinon la dernière frame d'Overdrive dissiperait déjà de nouveau.
        /// </summary>
        public void TickOverdrive(float deltaTime)
        {
            if (!_isOverdriveActive.CurrentValue) return;

            float remaining = _overdriveRemaining.CurrentValue - deltaTime;

            if (remaining > 0f)
            {
                _overdriveRemaining.Value = remaining;
                return;
            }

            EndOverdrive();
        }

        /// <summary>
        /// Déclenche le Zéro-Day Exploit. La charge est consommée IMMÉDIATEMENT et sans retour :
        /// mourir à la troisième seconde ne la rend pas, et l'Exploit ne peut pas être
        /// interrompu. Le joueur assume son pari, c'est tout l'intérêt du choix.
        ///
        /// Retourne false si aucune charge n'est prête ou si un Exploit tourne déjà — la vue
        /// grise le bouton, mais elle n'est pas la garde.
        /// </summary>
        public bool TryTriggerOverdrive()
        {
            if (!IsReady) return false;

            // UNE charge consommée, pas la réserve : un joueur qui en a stocké trois doit pouvoir
            // enchaîner trois Exploits. Le reste de la barre est conservé tel quel.
            _chargeSeconds.Value = Mathf.Max(0f, _chargeSeconds.CurrentValue - CapacitySeconds);
            _overdriveRemaining.Value = OverdriveDurationSeconds;
            _isOverdriveActive.Value = true;

            _upgradeManager.SetGlobalYieldMultiplier(EffectiveYieldMultiplier);

            Debug.Log("[GhostCache] ZÉRO-DAY EXPLOIT. Proxies hors ligne pour "
                      + OverdriveDurationSeconds + " s.");
            return true;
        }

        /// <summary>
        /// Remet le système à neuf. Appelé par le wipe : le Ghost Cache est de l'état de RUN,
        /// le formatage l'efface — charge comprise, même pleine.
        /// </summary>
        public void ResetForNewRun()
        {
            _chargeSeconds.Value = 0f;
            EndOverdrive();
        }

        /// <summary>
        /// Restaure la charge depuis une sauvegarde. L'Overdrive en cours n'est PAS restauré :
        /// la charge ayant déjà été consommée à son déclenchement, fermer le jeu pendant les
        /// 30 secondes revient à les perdre. Choix assumé — sauvegarder un effet temporaire
        /// ouvrirait la porte à une mise en pause de l'Exploit par fermeture du jeu, dans un
        /// jeu qui n'a justement aucune progression hors-ligne.
        /// </summary>
        public void RestoreCharge(float seconds)
        {
            // Borné sur la capacité COURANTE : le GameStateGateway restaure le prestige (étape 3)
            // avant le Ghost Cache (étape 4), donc le nombre de charges est déjà connu ici.
            _chargeSeconds.Value = Mathf.Clamp(seconds, 0f, TotalCapacitySeconds);
            EndOverdrive();
        }

        /// <summary>
        /// Coupe l'Exploit et rend leur rendement normal aux Scripts. Idempotent : appelée aussi
        /// bien à l'échéance qu'au wipe et à la restauration, où rien ne tourne.
        /// </summary>
        private void EndOverdrive()
        {
            if (!_isOverdriveActive.CurrentValue) return;

            _isOverdriveActive.Value = false;
            _overdriveRemaining.Value = 0f;

            _upgradeManager.SetGlobalYieldMultiplier(1d);

            Debug.Log("[GhostCache] Exploit terminé. Proxies de nouveau en ligne.");
        }

        public void Dispose()
        {
            _chargeSeconds.Dispose();
            _isOverdriveActive.Dispose();
            _overdriveRemaining.Dispose();
        }
    }
}
