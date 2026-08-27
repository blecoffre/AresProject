using Core.Models.Economy;

namespace Core.Services.Economy
{
    /// <summary>
    /// Résultat d'un clic d'Overclock. Struct readonly : la vue en reçoit un par clic, et un type
    /// référence allouerait à chaque appui.
    /// </summary>
    public readonly struct OverclockResult
    {
        /// <summary>Secondes de progression accordées aux cycles déjà en cours.</summary>
        public readonly float SecondsGained;

        /// <summary>Scripts démarrés par le « réveil ». Vaut toujours 0 sans le nœud de prestige.</summary>
        public readonly int ScriptsWoken;

        public OverclockResult(float secondsGained, int scriptsWoken)
        {
            SecondsGained = secondsGained;
            ScriptsWoken = scriptsWoken;
        }
    }

    /// <summary>
    /// Le clic manuel.
    ///
    /// Par défaut, ce n'est PAS un démarreur de cycle : c'est un coup de fouet qui avance d'un
    /// coup tous les cycles déjà en cours. Lancer à la main les Scripts à l'arrêt fait partie de
    /// l'expérience du début et du milieu de partie — décision du 2026-08-27, qui revient sur
    /// celle du 26/08.
    ///
    /// Le « réveil » se débloque par un nœud de prestige tardif (`P_OVERCLOCK_AWAKE`) : à ce
    /// stade, relancer quinze Scripts à la main est devenu une corvée plutôt qu'un choix.
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

        /// <summary>Le nœud de réveil a-t-il été acheté. Exposé pour l'affichage, pas pour la garde.</summary>
        public bool WakesScripts => _prestigeManager.OverclockWakesScripts.CurrentValue;

        /// <summary>
        /// Déclenche l'overclock.
        ///
        /// L'ordre compte : on avance D'ABORD les cycles en cours, on réveille ENSUITE. Le GDD dit
        /// que le clic avance « ceux qui tournaient déjà » — réveiller en premier offrirait au
        /// passage une demi-seconde d'avance aux Scripts qui viennent tout juste de démarrer.
        /// </summary>
        public OverclockResult TriggerManualOverclock()
        {
            float seconds = CurrentWarpSeconds;
            _cycleRunner.AdvanceAllActive(seconds);

            int woken = WakesScripts ? _cycleRunner.StartAllIdle() : 0;

            return new OverclockResult(seconds, woken);
        }
    }
}
