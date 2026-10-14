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

        /// <summary>Appelé par la Vue (bouton) et par la barre d'espace.</summary>
        public void OnClickActionTriggered()
        {
            OverclockResult result = _overclockSystem.TriggerManualOverclock();

            switch (result.Outcome)
            {
                // On annonce ce qui a été gagné ET ce que ça a coûté : l'Overclock est un pari,
                // le joueur doit voir les deux plateaux de la balance.
                case OverclockOutcome.Applied:
                    _consolePresenter.Log(
                        _loc.GetText(
                            "LOG_MANUAL_OVERCLOCK",
                            result.SecondsGained,
                            result.TracePenalty),
                        ConsoleLogType.Success);
                    break;

                // Rien ne tournait : l'appui n'a rien fait et rien coûté. Le dire, sinon le
                // joueur croit la touche cassée.
                case OverclockOutcome.NoActiveCycle:
                    _consolePresenter.Log(_loc.GetText("LOG_MANUAL_OVERCLOCK_IDLE"), ConsoleLogType.Standard);
                    break;

                // Trop rapproché du précédent : silence, une rafale ne doit pas noyer la console.
                case OverclockOutcome.Throttled:
                    break;
            }
        }
    }
}
