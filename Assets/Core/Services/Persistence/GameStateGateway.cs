using Core.Models;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Security;
using Core.Services.Simulation;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Services.Persistence
{
    /// <summary>
    /// Point unique de traduction entre l'état vivant du jeu et le fichier de sauvegarde.
    ///
    /// Les deux sens (Restore et Capture) sont volontairement dans la même classe et se lisent
    /// champ pour champ : c'est la seule façon de garantir qu'une donnée ajoutée d'un côté ne soit
    /// pas oubliée de l'autre. C'est exactement ce qui était arrivé au prestige, hydraté nulle part
    /// alors que le champ existait dans SaveData depuis le début.
    /// </summary>
    public class GameStateGateway
    {
        /// <summary>
        /// On ne restaure jamais une jauge pleine. Sinon la partie reprend en Game Over immédiat,
        /// avant même que l'écran de fin ne soit abonné et prêt à l'afficher. Le joueur reprend
        /// au bord du gouffre, ce qui est aussi plus juste que de le punir d'avoir fermé le jeu.
        /// </summary>
        private const float MaxRestorableThreat = 0.99f;

        private readonly UserCurrencies _currencies;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestigeManager;
        private readonly ThreatManager _threatManager;
        private readonly EmergencyProtocolSystem _emergencyProtocol;
        private readonly GhostCacheSystem _ghostCache;
        private readonly GameSessionManager _sessionManager;

        // Instance et tampons réutilisés d'une capture à l'autre : un autosave ne doit rien allouer
        // en dehors de la chaîne JSON elle-même.
        private readonly SaveData _buffer = SaveData.CreateNew();
        private readonly Dictionary<string, int> _upgradeLevels = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _prestigeLevels = new Dictionary<string, int>();

        /// <summary>
        /// Faux tant que Restore n'a pas eu lieu. Le SaveScheduler s'en sert comme verrou : écrire
        /// avant la restauration remplacerait la sauvegarde du joueur par un état vide.
        /// </summary>
        public bool HasRestored { get; private set; }

        public GameStateGateway(
            UserCurrencies currencies,
            UpgradeManager upgradeManager,
            PrestigeManager prestigeManager,
            ThreatManager threatManager,
            EmergencyProtocolSystem emergencyProtocol,
            GhostCacheSystem ghostCache,
            GameSessionManager sessionManager)
        {
            _currencies = currencies;
            _upgradeManager = upgradeManager;
            _prestigeManager = prestigeManager;
            _threatManager = threatManager;
            _emergencyProtocol = emergencyProtocol;
            _ghostCache = ghostCache;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Hydrate tous les systèmes depuis une sauvegarde. Appelé une seule fois, au démarrage.
        /// </summary>
        public void Restore(SaveData data)
        {
            data.EnsureValid();

            // 1. Monnaies et compteurs cumulés
            _currencies.LoadFromSave(
                data.Money,
                data.CpuCycles,
                data.TotalMoney,
                data.RunMoney,
                data.TotalCpuCycles,
                data.TotalDetections);

            // 2. Générateurs de la run
            FillLevels(data.Upgrades, _upgradeLevels);
            _upgradeManager.InitializeFromSave(_upgradeLevels);

            // 3. Méta-progression
            FillLevels(data.PrestigeUpgrades, _prestigeLevels);
            _prestigeManager.InitializeFromSave(_prestigeLevels);

            // 4. État de menace de la run
            _threatManager.RestoreThreat(Mathf.Min(data.NormalizedThreat, MaxRestorableThreat));
            _emergencyProtocol.Restore(
                data.EmergencyUsesInRun,
                data.EmergencyBlockRemainingSeconds,
                data.EmergencyCooldownRemainingSeconds);
            _ghostCache.RestoreCharge(data.GhostCacheSeconds);
            _sessionManager.RestoreElapsed(data.RunElapsedSeconds);

            HasRestored = true;
        }

        /// <summary>
        /// Reconstitue une SaveData depuis l'état vivant.
        /// Retourne un tampon interne réutilisé : l'appelant doit le sérialiser immédiatement
        /// et ne pas le conserver au-delà de l'appel.
        /// </summary>
        public SaveData Capture()
        {
            // Les TFlops ne sont pas capturées : elles se recalculent depuis les niveaux
            // d'Upgrades, qui le sont. Les écrire créerait une seconde source de vérité.
            _buffer.Money = _currencies.Money.Amount.CurrentValue;
            _buffer.CpuCycles = _currencies.CpuCycles.Amount.CurrentValue;

            _buffer.TotalMoney = _currencies.TotalMoneyGenerated.CurrentValue;
            _buffer.RunMoney = _currencies.RunMoneyGenerated.CurrentValue;
            _buffer.TotalCpuCycles = _currencies.TotalCpuCyclesGenerated.CurrentValue;
            _buffer.TotalDetections = _currencies.TotalNumberOfDetections.CurrentValue;

            _buffer.NormalizedThreat = _threatManager.NormalizedThreat.CurrentValue;
            _buffer.EmergencyUsesInRun = _emergencyProtocol.UsesInCurrentRun;
            _buffer.EmergencyBlockRemainingSeconds = _emergencyProtocol.BlockRemaining.CurrentValue;
            _buffer.EmergencyCooldownRemainingSeconds = _emergencyProtocol.CooldownRemaining.CurrentValue;
            _buffer.GhostCacheSeconds = _ghostCache.ChargeSeconds.CurrentValue;
            _buffer.RunElapsedSeconds = _sessionManager.RunElapsedSeconds;

            _upgradeManager.CaptureLevelsInto(_buffer.Upgrades);
            _prestigeManager.CaptureLevelsInto(_buffer.PrestigeUpgrades);

            return _buffer;
        }

        private static void FillLevels(List<UpgradeSaveEntry> source, Dictionary<string, int> target)
        {
            target.Clear();

            // Boucle indexée plutôt que foreach : List<T> expose un énumérateur struct, mais on
            // reste explicite ici pour ne laisser aucune ambiguïté sur l'absence d'allocation.
            for (int i = 0; i < source.Count; i++)
            {
                UpgradeSaveEntry entry = source[i];
                if (string.IsNullOrEmpty(entry.Id)) continue;

                target[entry.Id] = entry.Level;
            }
        }
    }
}
