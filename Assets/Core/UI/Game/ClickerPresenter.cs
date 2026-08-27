using Core.Models.Console;
using Core.Services.Economy;
using Core.Services.Localization;

namespace Core.UI.Game
{
    public class ClickerPresenter
    {
        private readonly OverclockSystem _overclockSystem;
        private readonly ConsolePresenter _consolePresenter;
        private readonly ILocalizationService _loc;

        public ClickerPresenter(OverclockSystem overclockSystem, ConsolePresenter consolePresenter, ILocalizationService loc)
        {
            _overclockSystem = overclockSystem;
            _consolePresenter = consolePresenter;
            _loc = loc;
        }

        /// <summary>Appelé par la Vue lorsque le joueur clique sur le bouton d'overclock.</summary>
        public void OnClickActionTriggered()
        {
            OverclockResult result = _overclockSystem.TriggerManualOverclock();

            // On annonce la valeur réellement accordée. Elle augmente avec le nœud de prestige
            // de puissance de clic, ce qui rend ce bonus enfin visible pour le joueur.
            //
            // Deux messages plutôt qu'un : tant que le nœud de réveil n'est pas acheté, parler de
            // scripts réveillés n'aurait aucun sens, et une fois acheté, un clic qui n'a rien
            // trouvé à réveiller ne doit pas annoncer « 0 script relancé ».
            if (result.ScriptsWoken > 0)
            {
                _consolePresenter.Log(
                    _loc.GetText(
                        "LOG_MANUAL_OVERCLOCK_WAKE",
                        result.SecondsGained.ToString("0.#"),
                        result.ScriptsWoken),
                    ConsoleLogType.Success);

                return;
            }

            _consolePresenter.Log(
                _loc.GetText("LOG_MANUAL_OVERCLOCK", result.SecondsGained.ToString("0.#")),
                ConsoleLogType.Success);
        }
    }
}
