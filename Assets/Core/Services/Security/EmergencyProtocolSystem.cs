using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Platform;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Security
{
    /// <summary>
    /// Le Bouton d'Urgence, refondu le 2026-08-27 : « Data Wiper ».
    ///
    /// L'ancienne version achetait l'effacement avec de l'argent, comme un pot-de-vin. La nouvelle
    /// ne coûte RIEN : elle <b>exige</b> une puissance de calcul. Effacer partiellement ses traces,
    /// c'est envoyer un ver dans les serveurs fédéraux — il faut de la force brute, pas des
    /// billets. Le palier requis grimpe à chaque usage, l'agence durcissant ses défenses.
    ///
    /// Le contrecoup est le cœur de la mécanique : l'opération <b>immobilise une tranche des
    /// TFlops</b> pendant une minute. Or les TFlops alimentent aussi la dissipation des Proxies —
    /// purger la Trace affaiblit donc la défense juste après, et la jauge remonte plus vite. La
    /// tranche immobilisée s'alourdit de dix points par usage, ce qui rend le bouton de plus en
    /// plus dangereux à mesure que la run avance.
    ///
    /// Les deux compteurs s'écoulent dans Tick(), sans allocation : deux soustractions de float.
    /// </summary>
    public class EmergencyProtocolSystem : ITickable, IDisposable
    {
        private readonly ITimeSource _time;
        private readonly ThreatManager _threatManager;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;
        private readonly BalancingConfigSO _balancing;

        private readonly ReactiveProperty<int> _timesUsedInCurrentRun = new(0);
        private readonly ReactiveProperty<float> _blockRemaining = new(0f);
        private readonly ReactiveProperty<float> _cooldownRemaining = new(0f);

        /// <summary>
        /// Palier de TFlops à atteindre pour l'activation suivante.
        ///
        /// Propriété CALCULÉE et non ReactiveProperty, volontairement : une chaîne R3 dérivée du
        /// compteur d'usages ne se réévalue qu'à chaque usage, donc régler le palier dans
        /// l'inspecteur en Play Mode restait sans effet jusqu'au prochain déclenchement — ce qui
        /// vide de son sens l'équilibrage à chaud. Les abonnés écoutent
        /// <see cref="UsesInCurrentRun"/>, qui est la seule chose qui bouge en jeu.
        /// </summary>
        public double RequiredTFlops => _balancing.EmergencyBaseRequiredTFlops
            * Math.Pow(_balancing.EmergencyRequirementMultiplier, _timesUsedInCurrentRun.CurrentValue);

        /// <summary>Nombre d'usages, observable. C'est lui qui pilote le rafraîchissement de la vue.</summary>
        public ReadOnlyReactiveProperty<int> Uses => _timesUsedInCurrentRun;

        /// <summary>Secondes restantes d'immobilisation des TFlops. Vaut 0 hors contrecoup.</summary>
        public ReadOnlyReactiveProperty<float> BlockRemaining => _blockRemaining;

        /// <summary>Secondes restantes avant la prochaine activation possible.</summary>
        public ReadOnlyReactiveProperty<float> CooldownRemaining => _cooldownRemaining;

        /// <summary>
        /// Nombre d'utilisations sur la run en cours. Fait partie de l'état sauvegardé : sans lui,
        /// fermer et rouvrir le jeu remettrait le palier du bouton à son plancher.
        /// </summary>
        public int UsesInCurrentRun => _timesUsedInCurrentRun.CurrentValue;

        /// <summary>Le nœud de prestige a-t-il été acheté.</summary>
        public bool IsUnlocked => _prestigeManager.IsEmergencyUnlocked.CurrentValue;

        /// <summary>Tranche de TFlops que la PROCHAINE activation immobilisera.</summary>
        public float NextBlockedFraction => Mathf.Min(
            _balancing.EmergencyMaxBlockedFraction,
            _balancing.EmergencyBaseBlockedFraction
                + _balancing.EmergencyBlockedIncreasePerUse * _timesUsedInCurrentRun.CurrentValue);

        /// <summary>Le parc atteint-il le palier requis. Lu sur la capacité EFFECTIVE, immobilisation comprise.</summary>
        public bool HasEnoughPower => _upgradeManager.TotalTFlops.CurrentValue >= RequiredTFlops;

        public bool IsOnCooldown => _cooldownRemaining.CurrentValue > 0f;

        /// <summary>Durée totale du délai, pour que la vue puisse en tirer une progression.</summary>
        public float TotalCooldownSeconds => _balancing.EmergencyCooldownSeconds;

        /// <summary>Points de jauge effacés par une activation, pour l'affichage.</summary>
        public float TraceReduction => _balancing.EmergencyTraceReduction;

        /// <summary>Durée d'immobilisation, pour l'affichage.</summary>
        public float BlockDuration => _balancing.EmergencyBlockDurationSeconds;

        public EmergencyProtocolSystem(
            ThreatManager threatManager,
            UpgradeManager upgradeManager,
            PrestigeManager prestigeManager,
            BalancingConfigSO balancing,
            ITimeSource time)
        {
            _threatManager = threatManager;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _balancing = balancing;
            _time = time;
        }

        public void Tick()
        {
            // Volontairement NON gardé par IsGameActive : le contrecoup et le délai doivent
            // continuer de s'écouler pendant l'écran de fin. Le wipe les remet à zéro de toute
            // façon, mais les figer donnerait un état incohérent si la run reprenait.
            float deltaTime = _time.DeltaTime;

            if (_blockRemaining.CurrentValue > 0f)
            {
                float remaining = _blockRemaining.CurrentValue - deltaTime;

                if (remaining <= 0f)
                {
                    _blockRemaining.Value = 0f;
                    _upgradeManager.SetTFlopsBlockedFraction(0f);
                }
                else
                {
                    _blockRemaining.Value = remaining;
                }
            }

            if (_cooldownRemaining.CurrentValue > 0f)
            {
                _cooldownRemaining.Value = Mathf.Max(0f, _cooldownRemaining.CurrentValue - deltaTime);
            }
        }

        /// <summary>
        /// Déclenche la purge. Trois conditions : le nœud de prestige acheté, le délai écoulé, et
        /// le palier de TFlops atteint. Rien n'est dépensé — c'est un prérequis, pas un coût.
        ///
        /// Retourne false si l'une des trois manque ; la vue grise le bouton, mais elle n'est
        /// pas la garde.
        /// </summary>
        public bool TryTriggerEmergency()
        {
            if (!IsUnlocked) return false;
            if (IsOnCooldown) return false;
            if (!HasEnoughPower) return false;

            _threatManager.ReduceTraceByFraction(_balancing.EmergencyTraceReduction);

            // La tranche est calculée AVANT l'incrément : le premier usage d'une run en
            // immobilise 30 %, pas 40 %.
            _upgradeManager.SetTFlopsBlockedFraction(NextBlockedFraction);

            _blockRemaining.Value = _balancing.EmergencyBlockDurationSeconds;
            _cooldownRemaining.Value = _balancing.EmergencyCooldownSeconds;
            _timesUsedInCurrentRun.Value++;

            Debug.Log("[EMERGENCY] Data Wiper lancé. Trace −" + (_balancing.EmergencyTraceReduction * 100f)
                      + " pts, TFlops immobilisées pendant " + _balancing.EmergencyBlockDurationSeconds + " s.");
            return true;
        }

        /// <summary>
        /// Restaure l'état depuis une sauvegarde. Le contrecoup ET le délai sont sauvegardés,
        /// contrairement à l'Overdrive du Ghost Cache — et l'asymétrie est volontaire : là-bas,
        /// sauvegarder aurait permis de mettre en PAUSE un bonus ; ici, ne pas sauvegarder
        /// permettrait d'ÉCHAPPER à une pénalité en fermant la fenêtre.
        /// </summary>
        public void Restore(int uses, float blockRemaining, float cooldownRemaining)
        {
            _timesUsedInCurrentRun.Value = uses < 0 ? 0 : uses;
            _cooldownRemaining.Value = Mathf.Clamp(cooldownRemaining, 0f, _balancing.EmergencyCooldownSeconds);

            float block = Mathf.Clamp(blockRemaining, 0f, _balancing.EmergencyBlockDurationSeconds);
            _blockRemaining.Value = block;

            // La tranche à réappliquer est celle de l'usage PRÉCÉDENT, d'où le −1 : le compteur
            // a déjà été incrémenté au moment du déclenchement.
            if (block > 0f && uses > 0)
            {
                _upgradeManager.SetTFlopsBlockedFraction(Mathf.Min(
                    _balancing.EmergencyMaxBlockedFraction,
                    _balancing.EmergencyBaseBlockedFraction
                        + _balancing.EmergencyBlockedIncreasePerUse * (uses - 1)));
            }
            else
            {
                _upgradeManager.SetTFlopsBlockedFraction(0f);
            }
        }

        /// <summary>Remet le système à neuf. Appelé par le wipe : tout ceci est de l'état de run.</summary>
        public void ResetSystem()
        {
            _timesUsedInCurrentRun.Value = 0;
            _blockRemaining.Value = 0f;
            _cooldownRemaining.Value = 0f;
            _upgradeManager.SetTFlopsBlockedFraction(0f);
        }

        public void Dispose()
        {
            _timesUsedInCurrentRun.Dispose();
            _blockRemaining.Dispose();
            _cooldownRemaining.Dispose();
        }
    }
}
