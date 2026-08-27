using Core.Services.Economy;
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
        /// <summary>
        /// TFlops à posséder pour le premier usage de la run. Ordre de grandeur : `HW_01` au
        /// niveau 10, palier ×2 compris, en fournit 40.
        /// ⚠️ Valeur PROVISOIRE — le GD n'a pas chiffré le palier, seulement sa progression.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        private const double BaseRequiredTFlops = 50d;

        /// <summary>
        /// Facteur d'escalade du palier requis à chaque usage. Repris du ×3 de l'ancien coût en
        /// argent, faute de chiffre du GD. ⚠️ Valeur PROVISOIRE.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        private const double RequirementMultiplier = 3d;

        /// <summary>
        /// Points de jauge effacés, en ABSOLU et non en proportion : à 63 % on tombe à 43 %.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        private const float TraceReductionAmount = 0.20f;

        /// <summary>Tranche de TFlops immobilisée au premier usage de la run.</summary>
        private const float BaseBlockedFraction = 0.30f;

        /// <summary>Points de tranche ajoutés à chaque usage : 30 %, puis 40 %, puis 50 %…</summary>
        private const float BlockedFractionIncreasePerUse = 0.10f;

        /// <summary>
        /// Plafond de la tranche. Sans lui, le septième usage d'une run immobiliserait 100 % du
        /// parc : plus une seule TFlop, donc plus aucune dissipation ni compression de cycle. Le
        /// bouton deviendrait un suicide pur, ce qui n'est pas un choix mais un piège.
        /// </summary>
        private const float MaxBlockedFraction = 0.90f;

        /// <summary>Durée d'immobilisation des TFlops, en secondes.</summary>
        private const float BlockDurationSeconds = 60f;

        /// <summary>Délai entre deux activations, en secondes.</summary>
        private const float CooldownSeconds = 300f;

        private readonly ThreatManager _threatManager;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;

        private readonly ReactiveProperty<int> _timesUsedInCurrentRun = new(0);
        private readonly ReactiveProperty<float> _blockRemaining = new(0f);
        private readonly ReactiveProperty<float> _cooldownRemaining = new(0f);

        /// <summary>Palier de TFlops à atteindre pour l'activation suivante.</summary>
        public ReadOnlyReactiveProperty<double> RequiredTFlops { get; }

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
            MaxBlockedFraction,
            BaseBlockedFraction + BlockedFractionIncreasePerUse * _timesUsedInCurrentRun.CurrentValue);

        /// <summary>Le parc atteint-il le palier requis. Lu sur la capacité EFFECTIVE, immobilisation comprise.</summary>
        public bool HasEnoughPower => _upgradeManager.TotalTFlops.CurrentValue >= RequiredTFlops.CurrentValue;

        public bool IsOnCooldown => _cooldownRemaining.CurrentValue > 0f;

        /// <summary>Durée totale du délai, pour que la vue puisse en tirer une progression.</summary>
        public static float TotalCooldownSeconds => CooldownSeconds;

        /// <summary>Points de jauge effacés par une activation, pour l'affichage.</summary>
        public static float TraceReduction => TraceReductionAmount;

        /// <summary>Durée d'immobilisation, pour l'affichage.</summary>
        public static float BlockDuration => BlockDurationSeconds;

        public EmergencyProtocolSystem(
            ThreatManager threatManager,
            UpgradeManager upgradeManager,
            PrestigeManager prestigeManager)
        {
            _threatManager = threatManager;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;

            RequiredTFlops = _timesUsedInCurrentRun
                .Select(uses => BaseRequiredTFlops * Math.Pow(RequirementMultiplier, uses))
                .ToReadOnlyReactiveProperty(BaseRequiredTFlops);
        }

        public void Tick()
        {
            // Volontairement NON gardé par IsGameActive : le contrecoup et le délai doivent
            // continuer de s'écouler pendant l'écran de fin. Le wipe les remet à zéro de toute
            // façon, mais les figer donnerait un état incohérent si la run reprenait.
            float deltaTime = Time.deltaTime;

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

            _threatManager.ReduceThreat(TraceReductionAmount);

            // La tranche est calculée AVANT l'incrément : le premier usage d'une run en
            // immobilise 30 %, pas 40 %.
            _upgradeManager.SetTFlopsBlockedFraction(NextBlockedFraction);

            _blockRemaining.Value = BlockDurationSeconds;
            _cooldownRemaining.Value = CooldownSeconds;
            _timesUsedInCurrentRun.Value++;

            Debug.Log("[EMERGENCY] Data Wiper lancé. Trace −" + (TraceReductionAmount * 100f)
                      + " pts, TFlops immobilisées pendant " + BlockDurationSeconds + " s.");
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
            _cooldownRemaining.Value = Mathf.Clamp(cooldownRemaining, 0f, CooldownSeconds);

            float block = Mathf.Clamp(blockRemaining, 0f, BlockDurationSeconds);
            _blockRemaining.Value = block;

            // La tranche à réappliquer est celle de l'usage PRÉCÉDENT, d'où le −1 : le compteur
            // a déjà été incrémenté au moment du déclenchement.
            if (block > 0f && uses > 0)
            {
                _upgradeManager.SetTFlopsBlockedFraction(Mathf.Min(
                    MaxBlockedFraction,
                    BaseBlockedFraction + BlockedFractionIncreasePerUse * (uses - 1)));
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
            RequiredTFlops.Dispose();
            _timesUsedInCurrentRun.Dispose();
            _blockRemaining.Dispose();
            _cooldownRemaining.Dispose();
        }
    }
}
