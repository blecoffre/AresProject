using Core.Models.Console;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Security;
using Core.Utils;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Pilote le bouton du Data Wiper et raconte la purge dans la console.
    ///
    /// Quatre états, dans un ordre de priorité qui n'est pas arbitraire : verrouillé d'abord,
    /// puis le délai de recharge, puis la puissance manquante, et prêt en dernier. Un joueur
    /// qui vient de purger doit lire « en recharge » et non « puissance insuffisante », alors
    /// que les deux sont vrais — c'est le contrecoup qui vient de lui prendre ses TFlops.
    /// </summary>
    public class EmergencyPresenter : IStartable, IDisposable
    {
        private readonly EmergencyView _view;
        private readonly EmergencyProtocolSystem _emergency;
        private readonly UpgradeManager _upgradeManager;
        private readonly PrestigeManager _prestige;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private DisposableBag _disposables;

        public EmergencyPresenter(
            EmergencyView view,
            EmergencyProtocolSystem emergency,
            UpgradeManager upgradeManager,
            PrestigeManager prestige,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _view = view;
            _emergency = emergency;
            _upgradeManager = upgradeManager;
            _prestige = prestige;
            _console = console;
            _loc = loc;
        }

        public void Start()
        {
            _view.OnEmergencyClicked += HandleClicked;

            // Le délai est ramené à un ENTIER de seconde : sans ce filtre, le libellé serait
            // reconstruit — donc une chaîne allouée — à chaque frame pendant cinq minutes.
            _emergency.CooldownRemaining
                .Select(Mathf.CeilToInt)
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // Le contrecoup fait chuter puis remonter les TFlops : les deux bords changent l'état
            // affiché, puisque la puissance disponible conditionne la disponibilité du bouton.
            _emergency.BlockRemaining
                .Select(Mathf.CeilToInt)
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // On écoute le COMPTEUR d'usages et non le palier : celui-ci est désormais calculé
            // à la volée depuis le BalancingConfigSO, pour suivre un réglage à chaud.
            _emergency.Uses
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // Franchir le palier requis doit rendre le bouton cliquable à l'achat du Hardware,
            // pas à la seconde suivante. On filtre sur le SEUIL et non sur la valeur : les
            // TFlops bougent à chaque achat, l'état du bouton bien plus rarement.
            _upgradeManager.TotalTFlops
                .Select(_ => _emergency.HasEnoughPower)
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            _prestige.IsEmergencyUnlocked
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            Refresh();
        }

        private void Refresh()
        {
            if (!_emergency.IsUnlocked)
            {
                _view.ApplyState(
                    _loc.GetText("UI_EMERGENCY_LOCKED"),
                    interactable: false,
                    fillAmount: 0f,
                    fillColor: _view.UnavailableColor);

                return;
            }

            if (_emergency.IsOnCooldown)
            {
                float remaining = _emergency.CooldownRemaining.CurrentValue;
                float blockRemaining = _emergency.BlockRemaining.CurrentValue;

                // Tant que les TFlops sont immobilisées, la jauge le dit par sa couleur : c'est
                // la fenêtre où le joueur est le plus vulnérable, et il doit la voir passer.
                _view.ApplyState(
                    _loc.GetText("UI_EMERGENCY_COOLDOWN", Mathf.CeilToInt(remaining)),
                    interactable: false,
                    fillAmount: 1f - remaining / _emergency.TotalCooldownSeconds,
                    fillColor: blockRemaining > 0f ? _view.BlockedColor : _view.UnavailableColor);

                return;
            }

            if (!_emergency.HasEnoughPower)
            {
                _view.ApplyState(
                    _loc.GetText(
                        "UI_EMERGENCY_INSUFFICIENT",
                        CurrencyFormatter.Format(_upgradeManager.TotalTFlops.CurrentValue),
                        CurrencyFormatter.Format(_emergency.RequiredTFlops)),
                    interactable: false,
                    fillAmount: (float)(_upgradeManager.TotalTFlops.CurrentValue
                                        / _emergency.RequiredTFlops),
                    fillColor: _view.UnavailableColor);

                return;
            }

            // Les trois nombres viennent des constantes et de l'état, jamais du texte : sinon un
            // rééquilibrage laisserait le bouton mentir au joueur.
            _view.ApplyState(
                _loc.GetText(
                    "UI_EMERGENCY_READY",
                    Mathf.RoundToInt(_emergency.TraceReduction * 100f),
                    Mathf.RoundToInt(_emergency.NextBlockedFraction * 100f),
                    Mathf.RoundToInt(_emergency.BlockDuration)),
                interactable: true,
                fillAmount: 1f,
                fillColor: _view.ReadyColor);
        }

        private void HandleClicked()
        {
            if (!_emergency.TryTriggerEmergency()) return;

            _console.Log(_loc.GetText("LOG_EMERGENCY_WIPE"), ConsoleLogType.Narrative);
            Refresh();
        }

        public void Dispose()
        {
            _view.OnEmergencyClicked -= HandleClicked;
            _disposables.Dispose();
        }
    }
}
