using Core.Models.Settings;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Settings
{
    public interface ISettingsService
    {
        ReadOnlyReactiveProperty<DisplayModeSetting> DisplayMode { get; }
        ReadOnlyReactiveProperty<Vector2Int> Resolution { get; }
        ReadOnlyReactiveProperty<bool> VSync { get; }
        ReadOnlyReactiveProperty<int> FrameRateCap { get; }
        ReadOnlyReactiveProperty<int> UiScalePercent { get; }
        ReadOnlyReactiveProperty<ColorVisionMode> ColorVision { get; }

        ReadOnlyReactiveProperty<bool> MasterMuted { get; }
        ReadOnlyReactiveProperty<bool> AudioInBackground { get; }
        ReadOnlyReactiveProperty<int> MusicVolumePercent { get; }
        ReadOnlyReactiveProperty<bool> MusicMuted { get; }
        ReadOnlyReactiveProperty<int> SfxVolumePercent { get; }
        ReadOnlyReactiveProperty<bool> SfxMuted { get; }

        /// <summary>Pas des sélecteurs de volume, en points de pourcentage.</summary>
        int VolumeStepPercent { get; }

        /// <summary>Résolutions fenêtrées proposées, de la plus petite à la plus grande.</summary>
        IReadOnlyList<Vector2Int> AvailableResolutions { get; }

        /// <summary>Plafonds de cadence proposés, croissants.</summary>
        IReadOnlyList<int> FrameRateCaps { get; }

        /// <summary>Langue choisie par le joueur, vide s'il n'a pas encore choisi.</summary>
        string Language { get; }

        /// <summary>Faux tant que l'écran de premier lancement n'a pas été validé.</summary>
        bool HasChosenLanguage { get; }

        int UiScaleMinPercent { get; }
        int UiScaleMaxPercent { get; }
        int UiScaleStepPercent { get; }

        void SetDisplayMode(DisplayModeSetting mode);
        void SetResolution(Vector2Int resolution);
        void SetVSync(bool enabled);
        void SetFrameRateCap(int cap);
        void SetUiScalePercent(int percent);

        /// <summary>
        /// Mémorise le choix. Ne charge RIEN : c'est <c>ILocalizationService.LoadLanguageAsync</c>
        /// qui change la langue affichée ; ce service ne fait que s'en souvenir.
        /// </summary>
        void SetLanguage(string languageCode);

        void SetColorVision(ColorVisionMode mode);

        void SetMasterMuted(bool muted);
        void SetAudioInBackground(bool enabled);
        void SetMusicVolumePercent(int percent);
        void SetMusicMuted(bool muted);
        void SetSfxVolumePercent(int percent);
        void SetSfxMuted(bool muted);
    }

    /// <summary>
    /// Source unique des réglages du joueur. Chaque modification est validée, publiée, écrite
    /// sur disque, et — pour l'affichage — appliquée sur-le-champ.
    ///
    /// Ce service n'applique QUE le mode d'affichage et la résolution. Les autres réglages
    /// sont appliqués par ceux qui en ont la charge, en s'abonnant :
    /// <list type="bullet">
    /// <item>V-Sync et plafond de FPS → <c>FrameRateGovernor</c>, seul à écrire dans
    /// <c>QualitySettings.vSyncCount</c> et <c>Application.targetFrameRate</c> ;</item>
    /// <item>échelle d'interface → <c>UiScalePresenter</c>, un par scène, sur son CanvasScaler.</item>
    /// </list>
    /// </summary>
    public sealed class SettingsService : ISettingsService, IStartable, IDisposable
    {
        private static readonly int[] FrameRateCapValues = { 30, 60, 120, 144 };

        private const int MinWindowWidth = 1280;
        private const int MinWindowHeight = 720;

        private readonly SettingsStore _store;
        private readonly GameSettings _settings;
        private readonly List<Vector2Int> _availableResolutions = new();

        private readonly ReactiveProperty<DisplayModeSetting> _displayMode;
        private readonly ReactiveProperty<Vector2Int> _resolution;
        private readonly ReactiveProperty<bool> _vSync;
        private readonly ReactiveProperty<int> _frameRateCap;
        private readonly ReactiveProperty<int> _uiScalePercent;
        private readonly ReactiveProperty<ColorVisionMode> _colorVision;
        private readonly ReactiveProperty<bool> _masterMuted;
        private readonly ReactiveProperty<bool> _audioInBackground;
        private readonly ReactiveProperty<int> _musicVolumePercent;
        private readonly ReactiveProperty<bool> _musicMuted;
        private readonly ReactiveProperty<int> _sfxVolumePercent;
        private readonly ReactiveProperty<bool> _sfxMuted;

        public ReadOnlyReactiveProperty<DisplayModeSetting> DisplayMode => _displayMode;
        public ReadOnlyReactiveProperty<Vector2Int> Resolution => _resolution;
        public ReadOnlyReactiveProperty<bool> VSync => _vSync;
        public ReadOnlyReactiveProperty<int> FrameRateCap => _frameRateCap;
        public ReadOnlyReactiveProperty<int> UiScalePercent => _uiScalePercent;
        public ReadOnlyReactiveProperty<ColorVisionMode> ColorVision => _colorVision;
        public ReadOnlyReactiveProperty<bool> MasterMuted => _masterMuted;
        public ReadOnlyReactiveProperty<bool> AudioInBackground => _audioInBackground;
        public ReadOnlyReactiveProperty<int> MusicVolumePercent => _musicVolumePercent;
        public ReadOnlyReactiveProperty<bool> MusicMuted => _musicMuted;
        public ReadOnlyReactiveProperty<int> SfxVolumePercent => _sfxVolumePercent;
        public ReadOnlyReactiveProperty<bool> SfxMuted => _sfxMuted;

        public int VolumeStepPercent => 10;

        public IReadOnlyList<Vector2Int> AvailableResolutions => _availableResolutions;
        public IReadOnlyList<int> FrameRateCaps => FrameRateCapValues;

        public string Language => _settings.Language;
        public bool HasChosenLanguage => !string.IsNullOrEmpty(_settings.Language);

        public int UiScaleMinPercent => 80;
        public int UiScaleMaxPercent => 150;
        public int UiScaleStepPercent => 10;

        /// <summary>
        /// Le fichier est lu dans le constructeur, et non dans <see cref="Start"/> : le
        /// <c>FrameRateGovernor</c> s'abonne à la V-Sync dans SON Start, et doit y trouver la
        /// valeur du joueur, pas la valeur par défaut suivie d'une correction une frame plus tard.
        /// </summary>
        public SettingsService(SettingsStore store)
        {
            _store = store;

            BuildResolutionList();

            GameSettings loaded = _store.Load();
            _settings = loaded ?? CreateFirstLaunchDefaults();
            Sanitize(_settings);

            _displayMode = new ReactiveProperty<DisplayModeSetting>(_settings.DisplayMode);
            _resolution = new ReactiveProperty<Vector2Int>(new Vector2Int(_settings.ResolutionWidth, _settings.ResolutionHeight));
            _vSync = new ReactiveProperty<bool>(_settings.VSync);
            _frameRateCap = new ReactiveProperty<int>(_settings.FrameRateCap);
            _uiScalePercent = new ReactiveProperty<int>(_settings.UiScalePercent);
            _colorVision = new ReactiveProperty<ColorVisionMode>(_settings.ColorVision);
            _masterMuted = new ReactiveProperty<bool>(_settings.MasterMuted);
            _audioInBackground = new ReactiveProperty<bool>(_settings.AudioInBackground);
            _musicVolumePercent = new ReactiveProperty<int>(_settings.MusicVolumePercent);
            _musicMuted = new ReactiveProperty<bool>(_settings.MusicMuted);
            _sfxVolumePercent = new ReactiveProperty<int>(_settings.SfxVolumePercent);
            _sfxMuted = new ReactiveProperty<bool>(_settings.SfxMuted);

            // Premier lancement : on écrit tout de suite, pour que le fichier existe et documente
            // les valeurs choisies.
            if (loaded == null) _store.Save(_settings);
        }

        public void Start()
        {
            ApplyDisplay();
        }

        public void SetDisplayMode(DisplayModeSetting mode)
        {
            if (_settings.DisplayMode == mode) return;

            _settings.DisplayMode = mode;
            _displayMode.Value = mode;
            ApplyDisplay();
            _store.Save(_settings);
        }

        public void SetResolution(Vector2Int resolution)
        {
            if (!_availableResolutions.Contains(resolution)) return;
            if (_settings.ResolutionWidth == resolution.x && _settings.ResolutionHeight == resolution.y) return;

            _settings.ResolutionWidth = resolution.x;
            _settings.ResolutionHeight = resolution.y;
            _resolution.Value = resolution;
            ApplyDisplay();
            _store.Save(_settings);
        }

        public void SetVSync(bool enabled)
        {
            if (_settings.VSync == enabled) return;

            _settings.VSync = enabled;
            _vSync.Value = enabled;
            _store.Save(_settings);
        }

        public void SetFrameRateCap(int cap)
        {
            if (Array.IndexOf(FrameRateCapValues, cap) < 0) return;
            if (_settings.FrameRateCap == cap) return;

            _settings.FrameRateCap = cap;
            _frameRateCap.Value = cap;
            _store.Save(_settings);
        }

        public void SetUiScalePercent(int percent)
        {
            percent = Mathf.Clamp(percent, UiScaleMinPercent, UiScaleMaxPercent);
            if (_settings.UiScalePercent == percent) return;

            _settings.UiScalePercent = percent;
            _uiScalePercent.Value = percent;
            _store.Save(_settings);
        }

        public void SetMasterMuted(bool muted)
        {
            if (_settings.MasterMuted == muted) return;

            _settings.MasterMuted = muted;
            _masterMuted.Value = muted;
            _store.Save(_settings);
        }

        public void SetAudioInBackground(bool enabled)
        {
            if (_settings.AudioInBackground == enabled) return;

            _settings.AudioInBackground = enabled;
            _audioInBackground.Value = enabled;
            _store.Save(_settings);
        }

        public void SetMusicVolumePercent(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (_settings.MusicVolumePercent == percent) return;

            _settings.MusicVolumePercent = percent;
            _musicVolumePercent.Value = percent;
            _store.Save(_settings);
        }

        public void SetMusicMuted(bool muted)
        {
            if (_settings.MusicMuted == muted) return;

            _settings.MusicMuted = muted;
            _musicMuted.Value = muted;
            _store.Save(_settings);
        }

        public void SetSfxVolumePercent(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (_settings.SfxVolumePercent == percent) return;

            _settings.SfxVolumePercent = percent;
            _sfxVolumePercent.Value = percent;
            _store.Save(_settings);
        }

        public void SetSfxMuted(bool muted)
        {
            if (_settings.SfxMuted == muted) return;

            _settings.SfxMuted = muted;
            _sfxMuted.Value = muted;
            _store.Save(_settings);
        }

        public void SetColorVision(ColorVisionMode mode)
        {
            if (!Enum.IsDefined(typeof(ColorVisionMode), mode) || _settings.ColorVision == mode) return;

            _settings.ColorVision = mode;
            _colorVision.Value = mode;
            _store.Save(_settings);
        }

        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || _settings.Language == languageCode) return;

            _settings.Language = languageCode;
            _store.Save(_settings);
        }

        /// <summary>
        /// En plein écran, la résolution est celle du moniteur : le fenêtré sans bordure ne sait
        /// rien faire d'autre, et une résolution inférieure serait de toute façon étirée. La
        /// résolution choisie par le joueur ne vaut donc que pour le fenêtré, et elle est
        /// conservée pendant qu'il est en plein écran pour être retrouvée au retour.
        /// </summary>
        private void ApplyDisplay()
        {
            if (_settings.DisplayMode == DisplayModeSetting.Fullscreen)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(_settings.ResolutionWidth, _settings.ResolutionHeight, FullScreenMode.Windowed);
            }
        }

        /// <summary>
        /// Résolutions distinctes du moniteur principal (les fréquences multiples d'une même
        /// taille sont fusionnées), bornées par le minimum lisible et par la taille de l'écran.
        /// Jamais vide : dans l'éditeur, ou sur un pilote qui ne déclare rien, il reste 1280×720.
        /// </summary>
        private void BuildResolutionList()
        {
            int maxWidth = Display.main.systemWidth;
            int maxHeight = Display.main.systemHeight;

            foreach (UnityEngine.Resolution r in Screen.resolutions)
            {
                if (r.width < MinWindowWidth || r.height < MinWindowHeight) continue;
                if (r.width > maxWidth || r.height > maxHeight) continue;

                var size = new Vector2Int(r.width, r.height);
                if (!_availableResolutions.Contains(size)) _availableResolutions.Add(size);
            }

            var fallback = new Vector2Int(MinWindowWidth, MinWindowHeight);
            if (!_availableResolutions.Contains(fallback)) _availableResolutions.Add(fallback);

            _availableResolutions.Sort(CompareResolutions);
        }

        private static int CompareResolutions(Vector2Int a, Vector2Int b)
        {
            int byWidth = a.x.CompareTo(b.x);
            return byWidth != 0 ? byWidth : a.y.CompareTo(b.y);
        }

        /// <summary>
        /// Valeurs du tout premier lancement. Plein écran d'office sur un écran de 1280 px de
        /// large ou moins — le Steam Deck en tête : une fenêtre 1280×720 dans un écran 1280×800
        /// n'y aurait aucun sens.
        /// </summary>
        private static GameSettings CreateFirstLaunchDefaults()
        {
            var settings = new GameSettings();
            if (Display.main.systemWidth <= MinWindowWidth) settings.DisplayMode = DisplayModeSetting.Fullscreen;
            return settings;
        }

        /// <summary>
        /// Un fichier édité à la main, ou écrit sur un autre moniteur, peut contenir n'importe
        /// quoi. On ramène chaque valeur dans ce que le jeu sait proposer, plutôt que d'ouvrir
        /// une fenêtre plus grande que l'écran.
        /// </summary>
        private void Sanitize(GameSettings s)
        {
            if (!Enum.IsDefined(typeof(DisplayModeSetting), s.DisplayMode)) s.DisplayMode = DisplayModeSetting.Windowed;

            var resolution = new Vector2Int(s.ResolutionWidth, s.ResolutionHeight);
            if (!_availableResolutions.Contains(resolution))
            {
                Vector2Int largestFitting = _availableResolutions[0];
                for (int i = 0; i < _availableResolutions.Count; i++)
                {
                    Vector2Int candidate = _availableResolutions[i];
                    if (candidate.x <= resolution.x && candidate.y <= resolution.y) largestFitting = candidate;
                }

                s.ResolutionWidth = largestFitting.x;
                s.ResolutionHeight = largestFitting.y;
            }

            if (Array.IndexOf(FrameRateCapValues, s.FrameRateCap) < 0) s.FrameRateCap = 60;

            // Une langue inconnue (fichier retiré entre deux versions) n'est pas corrigée ici :
            // ce service ne connaît pas les langues disponibles. Le GameBootstrapper retombe sur
            // la langue du système dans ce cas.
            s.Language ??= string.Empty;

            if (!Enum.IsDefined(typeof(ColorVisionMode), s.ColorVision)) s.ColorVision = ColorVisionMode.Standard;

            // Volumes ramenés sur la grille des pas, comme l'échelle d'interface.
            s.MusicVolumePercent = SnapVolume(s.MusicVolumePercent);
            s.SfxVolumePercent = SnapVolume(s.SfxVolumePercent);

            // Ramené sur la grille des pas : 117 devient 120.
            int step = UiScaleStepPercent;
            int snapped = Mathf.RoundToInt(s.UiScalePercent / (float)step) * step;
            s.UiScalePercent = Mathf.Clamp(snapped, UiScaleMinPercent, UiScaleMaxPercent);
        }

        private int SnapVolume(int percent) =>
            Mathf.Clamp(Mathf.RoundToInt(percent / (float)VolumeStepPercent) * VolumeStepPercent, 0, 100);

        public void Dispose()
        {
            _displayMode.Dispose();
            _resolution.Dispose();
            _vSync.Dispose();
            _frameRateCap.Dispose();
            _uiScalePercent.Dispose();
            _colorVision.Dispose();
            _masterMuted.Dispose();
            _audioInBackground.Dispose();
            _musicVolumePercent.Dispose();
            _musicMuted.Dispose();
            _sfxVolumePercent.Dispose();
            _sfxMuted.Dispose();
        }
    }
}
