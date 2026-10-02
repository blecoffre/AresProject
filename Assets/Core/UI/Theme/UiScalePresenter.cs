using Core.Services.Settings;
using R3;
using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace Core.UI.Theme
{
    /// <summary>
    /// Applique l'échelle d'interface choisie par le joueur au CanvasScaler de la scène. Un par
    /// scope de scène, puisque chaque scène a son propre Canvas.
    ///
    /// Le scaler reste en « Scale With Screen Size » ; on ne touche qu'à sa résolution de
    /// RÉFÉRENCE. La diviser par 1,3 fait croire au scaler que l'écran est 1,3 fois plus grand
    /// que prévu, et il grossit tout d'autant — textes, boutons, marges — sans rien changer à
    /// son adaptation aux différentes tailles d'écran. C'est ce qui rend le texte lisible sur
    /// Steam Deck, où le facteur naturel tombe autour de 0,70.
    /// </summary>
    public sealed class UiScalePresenter : IStartable, IDisposable
    {
        private readonly CanvasScaler _scaler;
        private readonly ISettingsService _settings;

        private Vector2 _baseReferenceResolution;
        private IDisposable _subscription;

        public UiScalePresenter(CanvasScaler scaler, ISettingsService settings)
        {
            _scaler = scaler;
            _settings = settings;
        }

        public void Start()
        {
            // Mémorisée UNE fois : relire la référence après l'avoir modifiée composerait les
            // échelles à chaque changement.
            _baseReferenceResolution = _scaler.referenceResolution;
            _subscription = _settings.UiScalePercent.Subscribe(Apply);
        }

        private void Apply(int percent)
        {
            _scaler.referenceResolution = _baseReferenceResolution * (100f / percent);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
