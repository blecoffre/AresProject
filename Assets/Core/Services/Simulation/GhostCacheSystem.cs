using Core.Services.Economy;
using R3;
using System;
using UnityEngine;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Le « Ghost Cache » et son déclencheur, le Zéro-Day Exploit.
    ///
    /// Jusqu'ici, l'excédent de dissipation des Proxies était purement jeté : au-delà du point
    /// où la Trace cessait de monter, chaque Proxie supplémentaire ne servait plus à rien. Ce
    /// système le capte et le transforme en une charge unique, échangeable contre 30 secondes de
    /// production démesurée — pendant lesquelles tous les Proxies s'éteignent et le joueur se
    /// retrouve à découvert.
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
        /// <summary>
        /// Secondes d'excédent de dissipation à accumuler pour une charge. Cinq minutes de
        /// « sur-défense » pure (tranché par le GD le 2026-08-27).
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        public const float CapacitySeconds = 300f;

        /// <summary>
        /// Durée du Zéro-Day Exploit. Assez long pour que le joueur ait le temps de regarder la
        /// jauge de Trace monter et de le regretter.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        public const float OverdriveDurationSeconds = 30f;

        /// <summary>
        /// Multiplicateur de rendement pendant l'Exploit. Porte sur le RENDEMENT et non sur la
        /// vitesse : le plancher MinCycleDuration bornerait une accélération, ce qui rendrait le
        /// gain imprévisible d'un Script à l'autre.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        public const double OverdriveYieldMultiplier = 50d;

        /// <summary>
        /// Multiplicateur appliqué à la génération BRUTE de Trace pendant l'Exploit, en plus de
        /// l'extinction des Proxies (tranché par le GD le 2026-08-27).
        ///
        /// ⚠️ C'est le réglage le plus sensible de la mécanique : la dissipation valant zéro, le
        /// temps de survie depuis une jauge vide vaut `100 / (génération × ce facteur)`. À 10, il
        /// faut générer moins de 0,33 Trace/s pour tenir les 30 secondes — soit moins qu'un seul
        /// Script de niveau 1. Voir la note d'équilibrage du backlog.
        /// TODO (BalancingConfigSO) : cette constante doit rejoindre les autres réglages.
        /// </summary>
        public const float OverdriveTraceMultiplier = 10f;

        private readonly UpgradeManager _upgradeManager;

        private readonly ReactiveProperty<float> _chargeSeconds = new(0f);
        private readonly ReactiveProperty<bool> _isOverdriveActive = new(false);
        private readonly ReactiveProperty<float> _overdriveRemaining = new(0f);

        /// <summary>Charge accumulée, en secondes d'excédent. Bornée par <see cref="CapacitySeconds"/>.</summary>
        public ReadOnlyReactiveProperty<float> ChargeSeconds => _chargeSeconds;

        /// <summary>Le Zéro-Day Exploit tourne-t-il en ce moment.</summary>
        public ReadOnlyReactiveProperty<bool> IsOverdriveActive => _isOverdriveActive;

        /// <summary>Secondes restantes d'Exploit. Vaut 0 hors Overdrive.</summary>
        public ReadOnlyReactiveProperty<float> OverdriveRemaining => _overdriveRemaining;

        /// <summary>Charge disponible et Exploit à l'arrêt : le bouton est actionnable.</summary>
        public bool IsReady => _chargeSeconds.CurrentValue >= CapacitySeconds
                               && !_isOverdriveActive.CurrentValue;

        /// <summary>Avancement de la charge, de 0 à 1. Pour la jauge de la vue.</summary>
        public float NormalizedCharge => _chargeSeconds.CurrentValue / CapacitySeconds;

        public GhostCacheSystem(UpgradeManager upgradeManager)
        {
            _upgradeManager = upgradeManager;
        }

        /// <summary>
        /// Capte une frame d'excédent. Appelé par le SimulationTicker, et par lui seul : c'est
        /// lui qui tient le calcul du débit et sait donc si la dissipation dépasse la génération.
        ///
        /// Sans effet une fois la jauge pleine : le GD a tranché un plafond d'UNE charge, pour
        /// forcer le joueur à consommer la mécanique plutôt qu'à la thésauriser.
        /// </summary>
        public void Accumulate(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (_isOverdriveActive.CurrentValue) return;

            float current = _chargeSeconds.CurrentValue;
            if (current >= CapacitySeconds) return;

            _chargeSeconds.Value = Mathf.Min(CapacitySeconds, current + deltaTime);
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

            _chargeSeconds.Value = 0f;
            _overdriveRemaining.Value = OverdriveDurationSeconds;
            _isOverdriveActive.Value = true;

            _upgradeManager.SetGlobalYieldMultiplier(OverdriveYieldMultiplier);

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
            _chargeSeconds.Value = Mathf.Clamp(seconds, 0f, CapacitySeconds);
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
