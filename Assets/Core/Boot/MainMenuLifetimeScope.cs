using Core.Services.Audio;
using Core.UI.MainMenu;
using Core.UI.Options;
using Core.UI.Theme;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Core.Boot
{
    /// <summary>
    /// Scope de la MainMenuScene, rattaché au Root par <c>SceneLoader</c> (EnqueueParent) comme
    /// celui de la GameScene. N'enregistre que de l'affichage : tout le métier est déjà dans le
    /// Root, chargé et restauré avant même que le menu n'apparaisse.
    /// </summary>
    public class MainMenuLifetimeScope : LifetimeScope
    {
        [SerializeField] private MainMenuView _mainMenuView;
        [SerializeField] private OptionsPanelView _optionsPanelView;

        [Tooltip("Écran de choix de langue, affiché par-dessus le menu au tout premier lancement.")]
        [SerializeField] private LanguagePickerView _languagePickerView;

        [Tooltip("Le CanvasScaler de la scène, sur lequel s'applique l'échelle d'interface.")]
        [SerializeField] private CanvasScaler _canvasScaler;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_mainMenuView);
            builder.RegisterComponent(_optionsPanelView);
            builder.RegisterComponent(_languagePickerView);
            builder.RegisterComponent(_canvasScaler);

            builder.Register<OptionsPanelPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.Register<LanguagePickerPresenter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<UiScalePresenter>();
            builder.RegisterEntryPoint<MainMenuPresenter>();
            builder.RegisterEntryPoint<SceneMusic>().WithParameter(MusicTrack.MainMenu);
        }
    }
}
