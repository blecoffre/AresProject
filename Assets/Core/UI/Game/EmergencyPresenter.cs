using Core.Models.Console;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Security;
using Core.UI.Theme;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Pilote le bouton du Data Wiper et raconte la purge dans la console.
    ///
    /// Trois états depuis la refonte « Time Attack » (lot 2) : verrouillé (nœud non acheté), prêt
    /// (il reste des usages), épuisé (le sacrifice a atteint son plafond). La jauge du bouton
    /// montre les usages restants ; épuisé, elle prend la couleur du sacrifice, qui court jusqu'à
    /// la fin de la run.
    /// </summary>
    public class EmergencyPresenter : IStartable, IDisposable
    {
        private readonly EmergencyView _view;
        private readonly EmergencyProtocolSystem _emergency;
        private readonly PrestigeManager _prestige;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;
        private readonly IPaletteService _palette;

        private DisposableBag _disposables;

        public EmergencyPresenter(
            EmergencyView view,
            EmergencyProtocolSystem emergency,
            PrestigeManager prestige,
            ConsolePresenter console,
            ILocalizationService loc,
            IPaletteService palette)
        {
            _view = view;
            _emergency = emergency;
            _prestige = prestige;
            _console = console;
            _loc = loc;
            _palette = palette;
        }

        public void Start()
        {
            _view.OnEmergencyClicked += HandleClicked;

            // Le compteur d'usages est la seule chose qui bouge en jeu : le sacrifice, les usages
            // restants et l'état du bouton en découlent. Il émet aussi au wipe de fin de run.
            _emergency.Uses
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            _prestige.IsEmergencyUnlocked
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // Mode daltonien changé à chaud : la jauge se repeint.
            _palette.Current
                .Skip(1)
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // Langue changée à chaud : le libellé se recompose. Skip(1) : le Refresh() ci-dessous
            // fait déjà le premier rendu.
            _loc.CurrentLanguage
                .Skip(1)
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            Refresh();
        }

        private void Refresh()
        {
            UiPaletteSO palette = _palette.Current.CurrentValue;

            if (!_emergency.IsUnlocked)
            {
                _view.ApplyState(
                    _loc.GetText("UI_EMERGENCY_LOCKED"),
                    interactable: false,
                    fillAmount: 0f,
                    fillColor: palette.EmergencyUnavailable);

                return;
            }

            if (_emergency.IsExhausted)
            {
                _view.ApplyState(
                    _loc.GetText("UI_EMERGENCY_EXHAUSTED", Mathf.RoundToInt(_emergency.SacrificedFraction * 100f)),
                    interactable: false,
                    fillAmount: 1f,
                    fillColor: palette.EmergencyBlocked);

                return;
            }

            // Les nombres viennent des réglages et de l'état, jamais du texte : sinon un
            // rééquilibrage laisserait le bouton mentir au joueur.
            int maxUses = _emergency.MaxUses;
            int remaining = _emergency.RemainingUses;

            _view.ApplyState(
                _loc.GetText(
                    "UI_EMERGENCY_READY",
                    _emergency.TraceRemoval,
                    Mathf.RoundToInt(_emergency.NextSacrificedFraction * 100f),
                    remaining),
                interactable: true,
                fillAmount: maxUses > 0 ? (float)remaining / maxUses : 0f,
                fillColor: palette.EmergencyReady);
        }

        private void HandleClicked()
        {
            float wiped = _emergency.TryTriggerEmergency();
            if (wiped < 0f) return;

            _console.Log(
                _loc.GetText(
                    "LOG_EMERGENCY_WIPE",
                    wiped,
                    Mathf.RoundToInt(_emergency.SacrificedFraction * 100f)),
                ConsoleLogType.Narrative);

            // Le compteur d'usages vient d'émettre : la vue est déjà à jour.
        }

        public void Dispose()
        {
            _view.OnEmergencyClicked -= HandleClicked;
            _disposables.Dispose();
        }
    }
}
