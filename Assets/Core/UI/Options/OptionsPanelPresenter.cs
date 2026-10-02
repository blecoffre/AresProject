using Core.Models.Settings;
using Core.Services.Localization;
using Core.Services.Settings;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Options
{
    /// <summary>
    /// Relie le panneau d'options aux réglages. Enregistré dans le scope de CHAQUE scène qui
    /// pose le panneau (menu principal, partie) : la vue est locale à la scène, les réglages
    /// vivent dans le Root.
    ///
    /// Chaque changement s'applique immédiatement — pas de bouton « Appliquer ». Le panneau se
    /// contente de demander ; c'est <see cref="ISettingsService"/> qui valide, et ce sont ses
    /// propriétés réactives qui rafraîchissent l'affichage. Le presenter n'écrit donc jamais une
    /// valeur qu'il suppose acceptée.
    /// </summary>
    public class OptionsPanelPresenter : IStartable, IDisposable
    {
        private readonly OptionsPanelView _view;
        private readonly ISettingsService _settings;
        private readonly ILocalizationService _loc;

        // Ordre des onglets : celui des tableaux de la vue (et du prefab OptionsPanel).
        private const int DisplayTab = 0;
        private const int AudioTab = 1;
        private const int AccessibilityTab = 2;

        /// <summary>Délai pendant lequel « [ CONFIRMER ? ] » attend le second clic.</summary>
        private static readonly TimeSpan ResetConfirmWindow = TimeSpan.FromSeconds(3);

        private DisposableBag _disposables;
        private int _currentTab = DisplayTab;

        // Remise à zéro en deux temps : le premier clic arme, le second applique. Le jeton
        // annule le retour automatique à l'état désarmé.
        private bool _isResetArmed;
        private CancellationTokenSource _resetArmCts;
        private CancellationTokenSource _cts;
        private bool _isSwitchingLanguage;

        public OptionsPanelPresenter(OptionsPanelView view, ISettingsService settings, ILocalizationService loc)
        {
            _view = view;
            _settings = settings;
            _loc = loc;
        }

        public bool IsOpen => _view.IsVisible;

        public void Start()
        {
            _view.Hide();
            _cts = new CancellationTokenSource();

            _view.OnBackClicked += Close;
            _view.OnResetClicked += HandleResetClicked;
            _view.OnTabClicked += ShowTab;
            _view.ShowTab(_currentTab);

            _view.MasterMute.OnPrevious += ToggleMasterMute;
            _view.MasterMute.OnNext += ToggleMasterMute;
            _view.AudioInBackground.OnPrevious += ToggleAudioInBackground;
            _view.AudioInBackground.OnNext += ToggleAudioInBackground;
            _view.MusicVolume.OnPrevious += DecreaseMusicVolume;
            _view.MusicVolume.OnNext += IncreaseMusicVolume;
            _view.MusicMute.OnPrevious += ToggleMusicMute;
            _view.MusicMute.OnNext += ToggleMusicMute;
            _view.SfxVolume.OnPrevious += DecreaseSfxVolume;
            _view.SfxVolume.OnNext += IncreaseSfxVolume;
            _view.SfxMute.OnPrevious += ToggleSfxMute;
            _view.SfxMute.OnNext += ToggleSfxMute;

            _view.Language.OnPrevious += PreviousLanguage;
            _view.Language.OnNext += NextLanguage;

            _view.DisplayMode.OnPrevious += ToggleDisplayMode;
            _view.DisplayMode.OnNext += ToggleDisplayMode;
            _view.Resolution.OnPrevious += PreviousResolution;
            _view.Resolution.OnNext += NextResolution;
            _view.VSync.OnPrevious += ToggleVSync;
            _view.VSync.OnNext += ToggleVSync;
            _view.FrameRateCap.OnPrevious += PreviousFrameRateCap;
            _view.FrameRateCap.OnNext += NextFrameRateCap;
            _view.UiScale.OnPrevious += DecreaseUiScale;
            _view.UiScale.OnNext += IncreaseUiScale;
            _view.ColorVision.OnPrevious += PreviousColorVision;
            _view.ColorVision.OnNext += NextColorVision;

            // Chaque abonnement émet sa valeur courante tout de suite : c'est lui qui remplit
            // le panneau à l'ouverture de la scène. Le mode d'affichage commande aussi la ligne
            // Résolution, la V-Sync commande la ligne FPS.
            _disposables = new DisposableBag();

            // La langue en PREMIER : son émission initiale écrit les libellés fixes, et chaque
            // changement à chaud réécrit tout le panneau, valeurs comprises (« Fenêtré » se
            // traduit aussi).
            _loc.CurrentLanguage.Subscribe(_ => RenderAll()).AddTo(ref _disposables);

            _settings.DisplayMode.Subscribe(_ => RenderDisplay()).AddTo(ref _disposables);
            _settings.Resolution.Subscribe(_ => RenderDisplay()).AddTo(ref _disposables);
            _settings.VSync.Subscribe(_ => RenderFrameRate()).AddTo(ref _disposables);
            _settings.FrameRateCap.Subscribe(_ => RenderFrameRate()).AddTo(ref _disposables);
            _settings.UiScalePercent.Subscribe(_ => RenderUiScale()).AddTo(ref _disposables);
            _settings.ColorVision.Subscribe(_ => RenderColorVision()).AddTo(ref _disposables);
            _settings.MasterMuted.Subscribe(_ => RenderAudio()).AddTo(ref _disposables);
            _settings.AudioInBackground.Subscribe(_ => RenderAudio()).AddTo(ref _disposables);
            _settings.MusicVolumePercent.Subscribe(_ => RenderAudio()).AddTo(ref _disposables);
            _settings.MusicMuted.Subscribe(_ => RenderAudio()).AddTo(ref _disposables);
            _settings.SfxVolumePercent.Subscribe(_ => RenderAudio()).AddTo(ref _disposables);
            _settings.SfxMuted.Subscribe(_ => RenderAudio()).AddTo(ref _disposables);
        }

        public void Open() => _view.Show();

        public void Close()
        {
            DisarmReset();
            _view.Hide();
        }

        private void RenderAll()
        {
            RenderStaticTexts();
            RenderLanguage();
            RenderDisplay();
            RenderFrameRate();
            RenderUiScale();
            RenderColorVision();
            RenderAudio();
        }

        /// <summary>
        /// L'onglet affiché est conservé d'une ouverture à l'autre : un joueur qui règle le son
        /// revient au son.
        /// </summary>
        private void ShowTab(int index)
        {
            if (index < 0 || index >= _view.TabCount) return;

            // Un « [ CONFIRMER ? ] » armé sur l'Audio ne doit pas remettre à zéro l'Affichage.
            DisarmReset();

            _currentTab = index;
            _view.ShowTab(index);
        }

        private void RenderStaticTexts()
        {
            _view.SetTexts(_loc.GetText("OPTIONS_TITLE"), _loc.GetText("OPTIONS_BACK"));
            RenderResetButton();

            string previous = _loc.GetText("UI_SELECTOR_PREV");
            string next = _loc.GetText("UI_SELECTOR_NEXT");

            SetupRow(_view.Language, "OPTIONS_LANGUAGE", previous, next);
            SetupRow(_view.DisplayMode, "OPTIONS_DISPLAY_MODE", previous, next);
            SetupRow(_view.Resolution, "OPTIONS_RESOLUTION", previous, next);
            SetupRow(_view.VSync, "OPTIONS_VSYNC", previous, next);
            SetupRow(_view.FrameRateCap, "OPTIONS_FRAME_RATE", previous, next);
            SetupRow(_view.UiScale, "OPTIONS_UI_SCALE", previous, next);
            SetupRow(_view.ColorVision, "OPTIONS_COLOR_VISION", previous, next);
            SetupRow(_view.MasterMute, "OPTIONS_MASTER_MUTE", previous, next);
            SetupRow(_view.AudioInBackground, "OPTIONS_AUDIO_IN_BACKGROUND", previous, next);
            SetupRow(_view.MusicVolume, "OPTIONS_MUSIC_VOLUME", previous, next);
            SetupRow(_view.MusicMute, "OPTIONS_MUSIC_MUTE", previous, next);
            SetupRow(_view.SfxVolume, "OPTIONS_SFX_VOLUME", previous, next);
            SetupRow(_view.SfxMute, "OPTIONS_SFX_MUTE", previous, next);

            _view.SetTabLabel(DisplayTab, _loc.GetText("OPTIONS_TAB_DISPLAY"));
            _view.SetTabLabel(AudioTab, _loc.GetText("OPTIONS_TAB_AUDIO"));
            _view.SetTabLabel(AccessibilityTab, _loc.GetText("OPTIONS_TAB_ACCESSIBILITY"));
        }

        private void SetupRow(OptionSelectorView row, string labelKey, string previous, string next)
        {
            row.SetLabel(_loc.GetText(labelKey));
            row.SetGlyphs(previous, next);
        }

        // ---------- Rendu ----------

        private void RenderLanguage()
        {
            _view.Language.SetValue(_loc.GetText(LocalizationKeys.LanguageName(_loc.CurrentLanguage.CurrentValue)));

            bool canSwitch = _loc.AvailableLanguages.Count > 1 && !_isSwitchingLanguage;
            _view.Language.SetArrows(canSwitch, canSwitch);
        }

        private void RenderDisplay()
        {
            bool fullscreen = _settings.DisplayMode.CurrentValue == DisplayModeSetting.Fullscreen;
            _view.DisplayMode.SetValue(_loc.GetText(fullscreen ? "OPTIONS_DISPLAY_FULLSCREEN" : "OPTIONS_DISPLAY_WINDOWED"));
            _view.DisplayMode.SetArrows(true, true);

            Vector2Int resolution = _settings.Resolution.CurrentValue;
            int index = IndexOf(_settings.AvailableResolutions, resolution);
            _view.Resolution.SetValue(_loc.GetText("OPTIONS_RESOLUTION_VALUE", resolution.x, resolution.y));
            _view.Resolution.SetArrows(index > 0, index >= 0 && index < _settings.AvailableResolutions.Count - 1);

            // En plein écran, la fenêtre prend la taille du moniteur : la résolution choisie ne
            // s'applique qu'au fenêtré. La ligne reste visible pour qu'on la retrouve au retour.
            _view.Resolution.SetRowEnabled(!fullscreen);
        }

        private void RenderFrameRate()
        {
            bool vSync = _settings.VSync.CurrentValue;
            _view.VSync.SetValue(_loc.GetText(vSync ? "OPTIONS_ON" : "OPTIONS_OFF"));
            _view.VSync.SetArrows(true, true);

            int cap = _settings.FrameRateCap.CurrentValue;
            int index = IndexOf(_settings.FrameRateCaps, cap);
            _view.FrameRateCap.SetValue(_loc.GetText("OPTIONS_FRAME_RATE_VALUE", cap));
            _view.FrameRateCap.SetArrows(index > 0, index >= 0 && index < _settings.FrameRateCaps.Count - 1);

            // Sous V-Sync, Unity ignore le plafond : la ligne serait un réglage sans effet.
            _view.FrameRateCap.SetRowEnabled(!vSync);
        }

        private void RenderUiScale()
        {
            int percent = _settings.UiScalePercent.CurrentValue;
            _view.UiScale.SetValue(_loc.GetText("OPTIONS_UI_SCALE_VALUE", percent));
            _view.UiScale.SetArrows(percent > _settings.UiScaleMinPercent, percent < _settings.UiScaleMaxPercent);
        }

        /// <summary>
        /// Tout le son coupé : les lignes de canal et d'arrière-plan s'estompent — leurs
        /// réglages sont conservés, mais sans effet tant que le Master est coupé.
        /// </summary>
        private void RenderAudio()
        {
            bool masterMuted = _settings.MasterMuted.CurrentValue;

            _view.MasterMute.SetValue(_loc.GetText(masterMuted ? "OPTIONS_YES" : "OPTIONS_NO"));
            _view.MasterMute.SetArrows(true, true);

            _view.AudioInBackground.SetValue(_loc.GetText(_settings.AudioInBackground.CurrentValue ? "OPTIONS_YES" : "OPTIONS_NO"));
            _view.AudioInBackground.SetArrows(true, true);
            _view.AudioInBackground.SetRowEnabled(!masterMuted);

            RenderChannel(_view.MusicVolume, _view.MusicMute,
                          _settings.MusicVolumePercent.CurrentValue, _settings.MusicMuted.CurrentValue, masterMuted);
            RenderChannel(_view.SfxVolume, _view.SfxMute,
                          _settings.SfxVolumePercent.CurrentValue, _settings.SfxMuted.CurrentValue, masterMuted);
        }

        /// <summary>
        /// Un canal coupé estompe sa ligne de volume, comme la V-Sync estompe le plafond de FPS :
        /// le réglage est conservé, mais sans effet tant que le son est coupé.
        /// </summary>
        private void RenderChannel(OptionSelectorView volumeRow, OptionSelectorView muteRow, int percent, bool muted, bool masterMuted)
        {
            volumeRow.SetValue(_loc.GetText("OPTIONS_VOLUME_VALUE", percent));
            volumeRow.SetArrows(percent > 0, percent < 100);
            volumeRow.SetRowEnabled(!muted && !masterMuted);

            muteRow.SetValue(_loc.GetText(muted ? "OPTIONS_YES" : "OPTIONS_NO"));
            muteRow.SetArrows(true, true);
            muteRow.SetRowEnabled(!masterMuted);
        }

        private void RenderColorVision()
        {
            string key = _settings.ColorVision.CurrentValue switch
            {
                ColorVisionMode.Protanopia => "OPTIONS_COLOR_VISION_PROTANOPIA",
                ColorVisionMode.Deuteranopia => "OPTIONS_COLOR_VISION_DEUTERANOPIA",
                ColorVisionMode.Tritanopia => "OPTIONS_COLOR_VISION_TRITANOPIA",
                _ => "OPTIONS_COLOR_VISION_STANDARD",
            };

            _view.ColorVision.SetValue(_loc.GetText(key));
            _view.ColorVision.SetArrows(true, true);
        }

        private void RenderResetButton() =>
            _view.SetResetLabel(_loc.GetText(_isResetArmed ? "OPTIONS_RESET_CONFIRM" : "OPTIONS_RESET"));

        // ---------- Actions ----------

        /// <summary>
        /// Premier clic : arme, et le bouton demande confirmation pendant quelques secondes.
        /// Second clic dans ce délai : rétablit les valeurs par défaut de l'onglet affiché.
        /// Pas de fenêtre modale : le même bouton se pilote tel quel au clavier ou à la manette.
        /// </summary>
        private void HandleResetClicked()
        {
            if (!_isResetArmed)
            {
                ArmReset();
                return;
            }

            DisarmReset();
            ResetCurrentTab();
        }

        private void ArmReset()
        {
            _isResetArmed = true;
            RenderResetButton();

            _resetArmCts = new CancellationTokenSource();
            DisarmLaterAsync(_resetArmCts.Token).Forget();
        }

        /// <summary>
        /// Temps NON échelonné : le panneau s'ouvre aussi depuis la pause, et l'horloge doit
        /// tourner même si un jour Time.timeScale venait à changer.
        /// </summary>
        private async UniTaskVoid DisarmLaterAsync(CancellationToken ct)
        {
            bool cancelled = await UniTask.Delay(ResetConfirmWindow, DelayType.UnscaledDeltaTime, cancellationToken: ct)
                                          .SuppressCancellationThrow();
            if (cancelled) return;

            DisarmReset();
        }

        private void DisarmReset()
        {
            _resetArmCts?.Cancel();
            _resetArmCts?.Dispose();
            _resetArmCts = null;

            if (!_isResetArmed) return;
            _isResetArmed = false;
            RenderResetButton();
        }

        private void ResetCurrentTab()
        {
            switch (_currentTab)
            {
                case DisplayTab:
                    _settings.ResetDisplay();
                    break;

                case AudioTab:
                    _settings.ResetAudio();
                    break;

                // La langue n'est JAMAIS remise à zéro (décision de Bertrand, 2026-10-02) : c'est
                // un choix explicite du joueur, fait à l'écran de premier lancement, et la perdre
                // sur un clic destiné au mode daltonien serait une mauvaise surprise.
                case AccessibilityTab:
                    _settings.ResetAccessibility();
                    break;
            }
        }

        private void ToggleMasterMute() => _settings.SetMasterMuted(!_settings.MasterMuted.CurrentValue);

        private void ToggleAudioInBackground() => _settings.SetAudioInBackground(!_settings.AudioInBackground.CurrentValue);

        private void DecreaseMusicVolume() =>
            _settings.SetMusicVolumePercent(_settings.MusicVolumePercent.CurrentValue - _settings.VolumeStepPercent);

        private void IncreaseMusicVolume() =>
            _settings.SetMusicVolumePercent(_settings.MusicVolumePercent.CurrentValue + _settings.VolumeStepPercent);

        private void ToggleMusicMute() => _settings.SetMusicMuted(!_settings.MusicMuted.CurrentValue);

        private void DecreaseSfxVolume() =>
            _settings.SetSfxVolumePercent(_settings.SfxVolumePercent.CurrentValue - _settings.VolumeStepPercent);

        private void IncreaseSfxVolume() =>
            _settings.SetSfxVolumePercent(_settings.SfxVolumePercent.CurrentValue + _settings.VolumeStepPercent);

        private void ToggleSfxMute() => _settings.SetSfxMuted(!_settings.SfxMuted.CurrentValue);

        private void PreviousColorVision() => StepColorVision(-1);
        private void NextColorVision() => StepColorVision(+1);

        /// <summary>Boucle sur les quatre modes : comme les langues, ils n'ont pas d'ordre.</summary>
        private void StepColorVision(int direction)
        {
            const int count = 4;
            int next = ((int)_settings.ColorVision.CurrentValue + direction + count) % count;
            _settings.SetColorVision((ColorVisionMode)next);
        }

        private void PreviousLanguage() => StepLanguage(-1);
        private void NextLanguage() => StepLanguage(+1);

        /// <summary>
        /// La liste boucle : contrairement à une résolution, les langues n'ont pas d'ordre, et
        /// aucune n'est « la plus grande ».
        /// </summary>
        private void StepLanguage(int direction)
        {
            if (_isSwitchingLanguage) return;

            IReadOnlyList<string> languages = _loc.AvailableLanguages;
            if (languages.Count < 2) return;

            int index = IndexOf(languages, _loc.CurrentLanguage.CurrentValue);
            int nextIndex = (index + direction + languages.Count) % languages.Count;

            SwitchLanguageAsync(languages[nextIndex], _cts.Token).Forget();
        }

        /// <summary>
        /// Charge puis mémorise. Le panneau, comme tout le reste, se réécrit sur l'émission de
        /// <c>CurrentLanguage</c> : rien à rafraîchir ici. Le jeton est celui du presenter — la
        /// scène peut être déchargée pendant la lecture du fichier.
        /// </summary>
        private async UniTaskVoid SwitchLanguageAsync(string languageCode, CancellationToken ct)
        {
            _isSwitchingLanguage = true;
            RenderLanguage();

            try
            {
                await _loc.LoadLanguageAsync(languageCode, ct);
                _settings.SetLanguage(_loc.CurrentLanguage.CurrentValue);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OPTIONS] Échec du changement de langue : {ex}");
            }

            _isSwitchingLanguage = false;
            RenderLanguage();
        }

        private void ToggleDisplayMode()
        {
            bool fullscreen = _settings.DisplayMode.CurrentValue == DisplayModeSetting.Fullscreen;
            _settings.SetDisplayMode(fullscreen ? DisplayModeSetting.Windowed : DisplayModeSetting.Fullscreen);
        }

        private void PreviousResolution() => StepResolution(-1);
        private void NextResolution() => StepResolution(+1);

        private void StepResolution(int direction)
        {
            IReadOnlyList<Vector2Int> list = _settings.AvailableResolutions;
            int index = IndexOf(list, _settings.Resolution.CurrentValue) + direction;
            if (index < 0 || index >= list.Count) return;

            _settings.SetResolution(list[index]);
        }

        private void ToggleVSync() => _settings.SetVSync(!_settings.VSync.CurrentValue);

        private void PreviousFrameRateCap() => StepFrameRateCap(-1);
        private void NextFrameRateCap() => StepFrameRateCap(+1);

        private void StepFrameRateCap(int direction)
        {
            IReadOnlyList<int> caps = _settings.FrameRateCaps;
            int index = IndexOf(caps, _settings.FrameRateCap.CurrentValue) + direction;
            if (index < 0 || index >= caps.Count) return;

            _settings.SetFrameRateCap(caps[index]);
        }

        private void DecreaseUiScale() =>
            _settings.SetUiScalePercent(_settings.UiScalePercent.CurrentValue - _settings.UiScaleStepPercent);

        private void IncreaseUiScale() =>
            _settings.SetUiScalePercent(_settings.UiScalePercent.CurrentValue + _settings.UiScaleStepPercent);

        // Boucle explicite plutôt qu'IndexOf d'interface : IReadOnlyList n'en expose pas, et un
        // cast vers IList masquerait le type réel.
        private static int IndexOf<T>(IReadOnlyList<T> list, T value) where T : IEquatable<T>
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Equals(value)) return i;
            }

            return -1;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _disposables.Dispose();

            DisarmReset();

            _view.OnBackClicked -= Close;
            _view.OnResetClicked -= HandleResetClicked;
            _view.OnTabClicked -= ShowTab;

            _view.MasterMute.OnPrevious -= ToggleMasterMute;
            _view.MasterMute.OnNext -= ToggleMasterMute;
            _view.AudioInBackground.OnPrevious -= ToggleAudioInBackground;
            _view.AudioInBackground.OnNext -= ToggleAudioInBackground;
            _view.MusicVolume.OnPrevious -= DecreaseMusicVolume;
            _view.MusicVolume.OnNext -= IncreaseMusicVolume;
            _view.MusicMute.OnPrevious -= ToggleMusicMute;
            _view.MusicMute.OnNext -= ToggleMusicMute;
            _view.SfxVolume.OnPrevious -= DecreaseSfxVolume;
            _view.SfxVolume.OnNext -= IncreaseSfxVolume;
            _view.SfxMute.OnPrevious -= ToggleSfxMute;
            _view.SfxMute.OnNext -= ToggleSfxMute;

            _view.Language.OnPrevious -= PreviousLanguage;
            _view.Language.OnNext -= NextLanguage;

            _view.DisplayMode.OnPrevious -= ToggleDisplayMode;
            _view.DisplayMode.OnNext -= ToggleDisplayMode;
            _view.Resolution.OnPrevious -= PreviousResolution;
            _view.Resolution.OnNext -= NextResolution;
            _view.VSync.OnPrevious -= ToggleVSync;
            _view.VSync.OnNext -= ToggleVSync;
            _view.FrameRateCap.OnPrevious -= PreviousFrameRateCap;
            _view.FrameRateCap.OnNext -= NextFrameRateCap;
            _view.UiScale.OnPrevious -= DecreaseUiScale;
            _view.UiScale.OnNext -= IncreaseUiScale;
            _view.ColorVision.OnPrevious -= PreviousColorVision;
            _view.ColorVision.OnNext -= NextColorVision;
        }
    }
}
