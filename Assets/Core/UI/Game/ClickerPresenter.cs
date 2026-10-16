using Core.Models.Console;
using Core.Services.Economy;
using Core.Services.Localization;

namespace Core.UI.Game
{
    public class ClickerPresenter
    {
        private readonly OverclockSystem _overclockSystem;
        private readonly ConsolePresenter _consolePresenter;
        private readonly PrestigeManager _prestige;
        private readonly ILocalizationService _loc;

        /// <summary>
        /// L'Espace verrouillé a déjà été expliqué pendant cette run. Le presenter vit avec la
        /// scène, rechargée à chaque run : le rappel revient une fois par run, pas à chaque appui.
        /// </summary>
        private bool _spacebarLockExplained;

        public ClickerPresenter(OverclockSystem overclockSystem, ConsolePresenter consolePresenter,
                                PrestigeManager prestige, ILocalizationService loc)
        {
            _overclockSystem = overclockSystem;
            _consolePresenter = consolePresenter;
            _prestige = prestige;
            _loc = loc;
        }

        /// <summary>
        /// La barre d'Espace. Verrouillée tant que « Saisie Multiplexée » n'est pas installée
        /// (GD, 2026-10-16) : le bouton à l'écran, lui, reste libre dès le départ. Un appui
        /// verrouillé est expliqué une fois, sinon le joueur croit la touche cassée.
        /// </summary>
        public void OnKeyboardOverclockRequested()
        {
            if (_prestige.IsSpacebarUnlocked.CurrentValue)
            {
                OnClickActionTriggered();
                return;
            }

            if (_spacebarLockExplained) return;
            _spacebarLockExplained = true;

            _consolePresenter.Log(_loc.GetText("LOG_SPACEBAR_LOCKED"), ConsoleLogType.Standard);
        }

        /// <summary>Appelé par la Vue (bouton), et par la barre d'Espace une fois déverrouillée.</summary>
        public void OnClickActionTriggered()
        {
            OverclockResult result = _overclockSystem.TriggerManualOverclock();

            switch (result.Outcome)
            {
                // On annonce ce qui a été gagné ET ce que ça a coûté : l'Overclock est un pari,
                // le joueur doit voir les deux plateaux de la balance. Le critique, lui, ne coûte
                // rien : sa ligne le dit, sous l'étiquette verte « CRITICAL BYPASS ».
                case OverclockOutcome.Applied:
                    if (result.IsCritical)
                    {
                        _consolePresenter.Log(
                            _loc.GetText("LOG_MANUAL_OVERCLOCK_CRITICAL", result.SecondsGained),
                            ConsoleLogType.Critical);
                    }
                    else
                    {
                        _consolePresenter.Log(
                            _loc.GetText(
                                "LOG_MANUAL_OVERCLOCK",
                                result.SecondsGained,
                                result.TracePenalty),
                            ConsoleLogType.Success);
                    }
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
