using Core.Services.Settings;
using R3;
using System;

namespace Core.UI.Theme
{
    public interface IPaletteService
    {
        /// <summary>
        /// La palette du mode de vision choisi. Émet à chaque changement d'option, et sa valeur
        /// courante à l'abonnement : comme pour la langue, l'abonnement sert de premier rendu.
        /// </summary>
        ReadOnlyReactiveProperty<UiPaletteSO> Current { get; }
    }

    /// <summary>
    /// Traduit le réglage <c>ColorVision</c> en palette. Vit dans le Root : les presenters de
    /// toutes les scènes s'y abonnent pour repeindre leurs états à chaud.
    /// </summary>
    public sealed class PaletteService : IPaletteService, IDisposable
    {
        private readonly ReactiveProperty<UiPaletteSO> _current;
        private readonly IDisposable _subscription;

        public ReadOnlyReactiveProperty<UiPaletteSO> Current => _current;

        public PaletteService(UiPaletteCatalogSO catalog, ISettingsService settings)
        {
            _current = new ReactiveProperty<UiPaletteSO>(catalog.Resolve(settings.ColorVision.CurrentValue));
            _subscription = settings.ColorVision.Subscribe(mode => _current.Value = catalog.Resolve(mode));
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _current.Dispose();
        }
    }
}
