using Core.Models.Console;
using Core.Services.Localization;
using Core.Services.Simulation;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Pilote le bouton du Ghost Cache et raconte le Zéro-Day Exploit dans la console.
    ///
    /// Trois états, et la jauge change de SENS entre eux : elle se remplit pendant
    /// l'accumulation, puis se vide pendant l'Exploit. C'est volontaire — le joueur lit la même
    /// barre comme une réserve puis comme un compte à rebours, et la couleur marque la bascule.
    /// </summary>
    public class GhostCachePresenter : IStartable, IDisposable
    {
        private readonly GhostCacheView _view;
        private readonly GhostCacheSystem _ghostCache;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private DisposableBag _disposables;

        public GhostCachePresenter(
            GhostCacheView view,
            GhostCacheSystem ghostCache,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _view = view;
            _ghostCache = ghostCache;
            _console = console;
            _loc = loc;
        }

        public void Start()
        {
            _view.OnOverdriveClicked += HandleClicked;

            // Les trois sources sont ramenées à un ENTIER avant d'atteindre le presenter. Sans
            // ces filtres, le libellé serait reconstruit — donc une chaîne allouée — à chaque
            // frame pendant les cinq minutes de charge puis les trente secondes d'Exploit.
            _ghostCache.ChargeSeconds
                .Select(seconds => Mathf.RoundToInt(seconds / GhostCacheSystem.CapacitySeconds * 100f))
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            _ghostCache.OverdriveRemaining
                .Select(Mathf.CeilToInt)
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // Seul abonnement qui fait autre chose que rafraîchir : c'est lui qui porte la
            // narration. Il couvre les deux bords de l'Exploit, y compris une fin provoquée par
            // un wipe — le joueur doit savoir que ses Proxies sont revenus en ligne.
            _ghostCache.IsOverdriveActive
                .Skip(1)
                .Subscribe(HandleOverdriveToggled)
                .AddTo(ref _disposables);

            Refresh();
        }

        private void HandleOverdriveToggled(bool isActive)
        {
            _console.Log(
                _loc.GetText(isActive ? "LOG_OVERDRIVE_START" : "LOG_OVERDRIVE_END"),
                ConsoleLogType.Narrative);

            Refresh();
        }

        private void Refresh()
        {
            if (_ghostCache.IsOverdriveActive.CurrentValue)
            {
                float remaining = _ghostCache.OverdriveRemaining.CurrentValue;

                _view.ApplyState(
                    _loc.GetText("UI_GHOSTCACHE_ACTIVE", Mathf.CeilToInt(remaining)),
                    interactable: false,
                    fillAmount: remaining / GhostCacheSystem.OverdriveDurationSeconds,
                    fillColor: _view.OverdriveColor);

                return;
            }

            if (_ghostCache.IsReady)
            {
                _view.ApplyState(
                    _loc.GetText("UI_GHOSTCACHE_READY", Mathf.RoundToInt(GhostCacheSystem.OverdriveDurationSeconds)),
                    interactable: true,
                    fillAmount: 1f,
                    fillColor: _view.ReadyColor);

                return;
            }

            float normalized = _ghostCache.NormalizedCharge;

            _view.ApplyState(
                _loc.GetText("UI_GHOSTCACHE_CHARGING", Mathf.RoundToInt(normalized * 100f)),
                interactable: false,
                fillAmount: normalized,
                fillColor: _view.ChargingColor);
        }

        private void HandleClicked()
        {
            // Le retour est ignoré à dessein : le bouton est déjà grisé hors état prêt, et
            // l'abonnement à IsOverdriveActive se charge d'annoncer un déclenchement réussi.
            _ghostCache.TryTriggerOverdrive();
        }

        public void Dispose()
        {
            _view.OnOverdriveClicked -= HandleClicked;
            _disposables.Dispose();
        }
    }
}
