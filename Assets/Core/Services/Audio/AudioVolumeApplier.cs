using Core.Services.Settings;
using R3;
using System;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;

namespace Core.Services.Audio
{
    /// <summary>
    /// Applique les volumes des options à l'AudioMixer du jeu. C'est le SEUL endroit qui écrit
    /// les volumes : le futur système audio n'aura qu'à router ses AudioSources vers les groupes
    /// Music et SFX du mixer, sans jamais lire les réglages lui-même.
    ///
    /// Le mixer expose trois paramètres — <see cref="MasterVolumeParameter"/>,
    /// <see cref="MusicVolumeParameter"/> et <see cref="SfxVolumeParameter"/> — branchés sur le
    /// volume des groupes du même nom. Le Master ne fait que couper ou laisser passer : coupure
    /// générale, ou fenêtre inactive quand le joueur ne veut pas de son en arrière-plan.
    /// Renommer un paramètre exposé dans le mixer casse silencieusement ce lien : SetFloat rend
    /// alors faux, et c'est signalé une fois dans la console.
    ///
    /// Appliqué en Start et non dans le constructeur : Unity ignore les SetFloat posés sur un
    /// mixer avant le premier Start de la scène.
    /// </summary>
    public sealed class AudioVolumeApplier : IStartable, IDisposable
    {
        public const string MasterVolumeParameter = "MasterVolume";
        public const string MusicVolumeParameter = "MusicVolume";
        public const string SfxVolumeParameter = "SfxVolume";

        /// <summary>Plancher du mixer de Unity : -80 dB, c'est le silence.</summary>
        private const float SilenceDb = -80f;

        private readonly AudioMixer _mixer;
        private readonly ISettingsService _settings;

        private DisposableBag _disposables;
        private bool _hasWarned;

        // Même parti pris que le FrameRateGovernor : on part du principe que la fenêtre a le
        // focus au démarrage, plutôt que de lire Application.isFocused au premier frame, où il
        // peut répondre faux alors que la fenêtre s'apprête à le prendre.
        private bool _hasFocus = true;
        private bool _isHooked;

        public AudioVolumeApplier(AudioMixer mixer, ISettingsService settings)
        {
            _mixer = mixer;
            _settings = settings;
        }

        public void Start()
        {
            _disposables = new DisposableBag();

            // Six sources, trois sorties. Chaque abonnement émet sa valeur courante tout de
            // suite : le premier passage applique les réglages du joueur au démarrage.
            _settings.MusicVolumePercent.Subscribe(_ => ApplyMusic()).AddTo(ref _disposables);
            _settings.MusicMuted.Subscribe(_ => ApplyMusic()).AddTo(ref _disposables);
            _settings.SfxVolumePercent.Subscribe(_ => ApplySfx()).AddTo(ref _disposables);
            _settings.SfxMuted.Subscribe(_ => ApplySfx()).AddTo(ref _disposables);
            _settings.MasterMuted.Subscribe(_ => ApplyMaster()).AddTo(ref _disposables);
            _settings.AudioInBackground.Subscribe(_ => ApplyMaster()).AddTo(ref _disposables);

            Application.focusChanged += HandleFocusChanged;
            _isHooked = true;
        }

        private void HandleFocusChanged(bool hasFocus)
        {
            _hasFocus = hasFocus;
            ApplyMaster();
        }

        /// <summary>
        /// Le Master est à 0 dB ou coupé, sans intermédiaire : les volumes se règlent par canal.
        /// </summary>
        private void ApplyMaster()
        {
            bool silencedInBackground = !_hasFocus && !_settings.AudioInBackground.CurrentValue;
            bool muted = _settings.MasterMuted.CurrentValue || silencedInBackground;
            Apply(MasterVolumeParameter, muted ? 0 : 100);
        }

        private void ApplyMusic() =>
            Apply(MusicVolumeParameter, _settings.MusicMuted.CurrentValue ? 0 : _settings.MusicVolumePercent.CurrentValue);

        private void ApplySfx() =>
            Apply(SfxVolumeParameter, _settings.SfxMuted.CurrentValue ? 0 : _settings.SfxVolumePercent.CurrentValue);

        private void Apply(string parameter, int percent)
        {
            if (_mixer.SetFloat(parameter, ToDecibels(percent))) return;

            if (_hasWarned) return;
            _hasWarned = true;
            Debug.LogWarning($"[AUDIO] Le paramètre exposé « {parameter} » est introuvable sur le mixer " +
                             $"« {_mixer.name} » : le volume des options n'a aucun effet.");
        }

        /// <summary>
        /// Le volume perçu suit une échelle logarithmique : 50 % sur le sélecteur doit sonner
        /// « moitié moins fort », soit -6 dB, et non une atténuation linéaire du gain qui
        /// rendrait toute la moitié basse du réglage quasi muette.
        /// </summary>
        private static float ToDecibels(int percent) =>
            percent <= 0 ? SilenceDb : Mathf.Max(SilenceDb, 20f * Mathf.Log10(percent / 100f));

        public void Dispose()
        {
            // Événement statique de Unity : il survit au scope, le désabonnement est impératif.
            if (_isHooked)
            {
                Application.focusChanged -= HandleFocusChanged;
                _isHooked = false;
            }

            _disposables.Dispose();
        }
    }
}
