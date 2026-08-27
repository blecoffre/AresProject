using Core.Models.Economy;

namespace Core.Services.Economy
{
    /// <summary>
    /// Le clic manuel. Ce n'est pas un démarreur de cycle : c'est un coup de fouet qui avance
    /// d'un coup TOUS les cycles déjà en cours.
    /// </summary>
    public class OverclockSystem
    {
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly PrestigeManager _prestigeManager;
        private readonly BalancingConfigSO _balancing;

        public OverclockSystem(
            ScriptCycleRunner cycleRunner,
            PrestigeManager prestigeManager,
            BalancingConfigSO balancing)
        {
            _cycleRunner = cycleRunner;
            _prestigeManager = prestigeManager;
            _balancing = balancing;
        }

        /// <summary>Secondes qu'un clic ferait gagner en l'état. Exposé pour que l'UI dise la vérité.</summary>
        public float CurrentWarpSeconds => _balancing.OverclockWarpSeconds * _prestigeManager.ClickPowerMultiplier.CurrentValue;

        /// <summary>Déclenche l'overclock et retourne le nombre de secondes réellement accordé.</summary>
        public float TriggerManualOverclock()
        {
            float seconds = CurrentWarpSeconds;
            _cycleRunner.AdvanceAllActive(seconds);
            return seconds;
        }
    }
}
