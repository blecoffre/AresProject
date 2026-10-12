using System;
using VContainer.Unity;

namespace Core.Services.Audio
{
    /// <summary>
    /// Joue les sons d'interface demandés sur le <see cref="UiSoundChannelSO"/> : le clic d'un
    /// bouton actif, le refus d'un bouton grisé.
    ///
    /// Quatre sources en rotation, routées vers le groupe SFX : elles suivent l'option « Effets
    /// sonores » sans jamais la lire. Le clic n'alloue rien.
    /// </summary>
    public sealed class UiSoundPlayer : IStartable, IDisposable
    {
        private const int VoiceCount = 4;

        // Priorité Unity par défaut : sous la limite de voix, un clic cède devant la musique et
        // les sons d'événements.
        private const int Priority = 128;

        private readonly AudioLibrarySO _library;
        private readonly UiSoundChannelSO _channel;
        private readonly AudioHost _host;

        private SoundSetVoices _voices;
        private bool _isHooked;

        public UiSoundPlayer(AudioLibrarySO library, UiSoundChannelSO channel, AudioHost host)
        {
            _library = library;
            _channel = channel;
            _host = host;
        }

        public void Start()
        {
            _voices = new SoundSetVoices(_host, "UI", _library.SfxGroup, VoiceCount, Priority);

            // L'asset survit au Play Mode dans l'éditeur : les abonnements doivent être retirés au
            // Dispose, sinon le Play suivant appellerait un lecteur détruit.
            _channel.ClickRequested += HandleClickRequested;
            _channel.RefusalRequested += HandleRefusalRequested;
            _isHooked = true;
        }

        private void HandleClickRequested(SoundSetSO setOverride)
        {
            // Une famille propre encore vide retombe sur la famille par défaut : un bouton préparé
            // pour son son à venir garde sa frappe en attendant, au lieu de devenir muet.
            // Test explicite plutôt que « ?? » : l'opérateur ignore l'égalité surchargée de Unity
            // et laisserait passer un asset détruit.
            SoundSetSO set = setOverride != null && setOverride.Count > 0 ? setOverride : _library.DefaultClickSet;
            _voices.Play(set);
        }

        /// <summary>
        /// Pas de repli ici : un refus qui sonnerait comme une frappe dirait « c'est fait » au
        /// moment où rien ne s'est passé. Famille vide = refus muet, comme avant.
        /// </summary>
        private void HandleRefusalRequested() => _voices.Play(_library.RefusalSet);

        public void Dispose()
        {
            if (!_isHooked) return;

            _channel.ClickRequested -= HandleClickRequested;
            _channel.RefusalRequested -= HandleRefusalRequested;
            _isHooked = false;
        }
    }
}
