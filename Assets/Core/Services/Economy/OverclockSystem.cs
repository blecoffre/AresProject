namespace Core.Services.Economy
{
    /// <summary>
    /// Le clic manuel. Ce n'est pas un démarreur de cycle : c'est un coup de fouet qui avance
    /// d'un coup TOUS les cycles déjà en cours.
    /// </summary>
    public class OverclockSystem
    {
        /// <summary>Secondes de progression offertes par clic, avant bonus de prestige.</summary>
        private const float BaseWarpSeconds = 0.5f;

        private readonly ScriptCycleRunner _cycleRunner;
        private readonly PrestigeManager _prestigeManager;

        public OverclockSystem(ScriptCycleRunner cycleRunner, PrestigeManager prestigeManager)
        {
            _cycleRunner = cycleRunner;
            _prestigeManager = prestigeManager;
        }

        /// <summary>Secondes qu'un clic ferait gagner en l'état. Exposé pour que l'UI dise la vérité.</summary>
        public float CurrentWarpSeconds => BaseWarpSeconds * _prestigeManager.ClickPowerMultiplier.CurrentValue;

        /// <summary>Déclenche l'overclock et retourne le nombre de secondes réellement accordé.</summary>
        public float TriggerManualOverclock()
        {
            float seconds = CurrentWarpSeconds;
            _cycleRunner.AdvanceAllActive(seconds);
            return seconds;
        }
    }
}
