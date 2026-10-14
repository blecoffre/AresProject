using Core.Models.Economy;
using Core.Services.Economy;
using R3;
using System;
using UnityEngine;

namespace Core.Services.Security
{
    /// <summary>
    /// Le Bouton d'Urgence : « Data Wiper ».
    ///
    /// <b>Refonte « Time Attack » (lot 2, 2026-10-14).</b> Plus de délai, plus de palier de TFlops,
    /// plus de contrecoup qui se dissipe : chaque usage retire <b>2 500 UT</b> de la jauge et
    /// <b>sacrifie 30 % des TFlops nettes jusqu'à la fin de la run</b>, dans la limite stricte de
    /// 90 % — trois usages. Effacer ses traces, c'est envoyer un ver dans les serveurs fédéraux, et
    /// la puissance qu'il mobilise ne revient pas.
    ///
    /// Le sacrifice frappe les TFlops NETTES, celles de l'économie : les Scripts ralentissent et
    /// rapportent moins, le capteur du brouillard s'aveugle. La vitesse de l'A.M.I., qui lit les
    /// TFlops BRUTES, ne baisse jamais : sans cette règle du GD, le bouton deviendrait un soin
    /// gratuit au lieu d'un choix punitif.
    ///
    /// Plus rien ne s'écoule dans le temps : ce système n'est plus un ITickable.
    /// </summary>
    public class EmergencyProtocolSystem : IDisposable
    {
        private readonly ThreatManager _threatManager;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;
        private readonly BalancingConfigSO _balancing;

        private readonly ReactiveProperty<int> _timesUsedInCurrentRun = new(0);

        /// <summary>Nombre d'usages sur la run, observable. C'est lui qui pilote la vue.</summary>
        public ReadOnlyReactiveProperty<int> Uses => _timesUsedInCurrentRun;

        /// <summary>
        /// Nombre d'usages sur la run en cours. Fait partie de l'état sauvegardé : c'est de lui que
        /// découle le sacrifice, qui ne doit pas s'effacer en fermant le jeu.
        /// </summary>
        public int UsesInCurrentRun => _timesUsedInCurrentRun.CurrentValue;

        /// <summary>Le nœud de prestige a-t-il été acheté.</summary>
        public bool IsUnlocked => _prestigeManager.IsEmergencyUnlocked.CurrentValue;

        /// <summary>
        /// Usages permis par run : le plafond du sacrifice divisé par son pas. La marge absorbe
        /// l'erreur du float — 0,9 / 0,3 vaut 2,9999998 en simple précision, et trois usages
        /// deviendraient deux.
        /// </summary>
        public int MaxUses => _balancing.EmergencySacrificeStep <= 0f
            ? 0
            : Mathf.FloorToInt(_balancing.EmergencyMaxSacrifice / _balancing.EmergencySacrificeStep + 1e-4f);

        /// <summary>Usages encore possibles sur cette run.</summary>
        public int RemainingUses => Mathf.Max(0, MaxUses - UsesInCurrentRun);

        /// <summary>Part des TFlops nettes sacrifiée en ce moment, de 0 au plafond.</summary>
        public float SacrificedFraction => SacrificeFor(UsesInCurrentRun);

        /// <summary>Part qui sera sacrifiée APRÈS le prochain usage, pour que la vue l'annonce.</summary>
        public float NextSacrificedFraction => SacrificeFor(UsesInCurrentRun + 1);

        /// <summary>UT retirées par un usage, pour l'affichage.</summary>
        public float TraceRemoval => _balancing.EmergencyTraceRemoval;

        public bool IsExhausted => RemainingUses <= 0;

        public EmergencyProtocolSystem(
            ThreatManager threatManager,
            UpgradeManager upgradeManager,
            PrestigeManager prestigeManager,
            BalancingConfigSO balancing)
        {
            _threatManager = threatManager;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _balancing = balancing;
        }

        /// <summary>
        /// Déclenche la purge. Deux conditions : le nœud acheté, et un usage restant. Utilisable à
        /// tout moment, même jauge vide — l'excédent est alors perdu, comme pour un Proxy.
        ///
        /// Retourne les UT réellement retirées, ou une valeur négative si l'usage est refusé ; la
        /// vue grise le bouton, mais elle n'est pas la garde.
        /// </summary>
        public float TryTriggerEmergency()
        {
            if (!IsUnlocked || IsExhausted) return -1f;

            float wiped = _threatManager.Wipe(_balancing.EmergencyTraceRemoval);

            _timesUsedInCurrentRun.Value++;
            _upgradeManager.SetTFlopsSacrificedFraction(SacrificedFraction);

            Debug.Log("[EMERGENCY] Data Wiper lancé. Jauge −" + wiped + " UT, TFlops nettes sacrifiées à "
                      + (SacrificedFraction * 100f) + " %.");
            return wiped;
        }

        /// <summary>
        /// Restaure l'état depuis une sauvegarde : le compteur d'usages, et le sacrifice qui en
        /// découle. À appeler APRÈS la restauration des générateurs, dont il ampute les TFlops.
        /// </summary>
        public void Restore(int uses)
        {
            _timesUsedInCurrentRun.Value = Mathf.Clamp(uses, 0, MaxUses);
            _upgradeManager.SetTFlopsSacrificedFraction(SacrificedFraction);
        }

        /// <summary>Remet le système à neuf. Appelé par le wipe : tout ceci est de l'état de run.</summary>
        public void ResetSystem()
        {
            _timesUsedInCurrentRun.Value = 0;
            _upgradeManager.SetTFlopsSacrificedFraction(0f);
        }

        private float SacrificeFor(int uses)
        {
            return Mathf.Min(_balancing.EmergencyMaxSacrifice, _balancing.EmergencySacrificeStep * uses);
        }

        public void Dispose()
        {
            _timesUsedInCurrentRun.Dispose();
        }
    }
}
