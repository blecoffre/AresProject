using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Audio
{
    public interface IMusicPlayer
    {
        /// <summary>
        /// Passe à l'ambiance demandée par un fondu enchaîné. Sans effet si elle joue déjà : la
        /// GameScene rechargée en fin de run ne relance pas sa musique au début.
        /// </summary>
        void Play(MusicTrack track);
    }

    /// <summary>
    /// La musique de fond, d'une scène à l'autre. Vit dans le Root : elle survit aux changements
    /// de scène et enchaîne d'une ambiance à l'autre au lieu de couper net.
    ///
    /// Deux sources jouent en alternance : l'une monte pendant que l'autre descend. Le fondu agit
    /// sur le volume des SOURCES, jamais sur le mixer, dont l'<see cref="AudioVolumeApplier"/>
    /// garde l'exclusivité : les deux se multiplient sans jamais se marcher dessus.
    ///
    /// La pause n'interrompt pas la musique (décision du 2026-10-02) : rien ici ne lit l'horloge
    /// de simulation.
    /// </summary>
    public sealed class MusicPlayer : IMusicPlayer, IStartable, IDisposable
    {
        private readonly AudioLibrarySO _library;
        private readonly AudioHost _host;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private AudioSource _current;
        private AudioSource _other;
        private CancellationTokenSource _fade;

        // Une demande peut arriver avant le Start du lecteur : le scope de la première scène
        // démarre avant que le Root n'ait fini les siens. Elle est mémorisée, puis rejouée.
        private MusicTrack _requested = MusicTrack.None;
        private bool _isStarted;

        public MusicPlayer(AudioLibrarySO library, AudioHost host)
        {
            _library = library;
            _host = host;
        }

        public void Start()
        {
            _current = CreateMusicSource("Music A");
            _other = CreateMusicSource("Music B");
            _isStarted = true;

            CrossfadeTo(_requested);
        }

        public void Play(MusicTrack track)
        {
            if (track == _requested && _isStarted) return;

            _requested = track;
            if (_isStarted) CrossfadeTo(track);
        }

        private AudioSource CreateMusicSource(string name)
        {
            AudioSource source = _host.CreateSource(name, _library.MusicGroup);
            source.loop = true;
            source.volume = 0f;
            // Priorité maximale : sous la limite de voix de Unity, c'est un clic qui doit céder,
            // jamais la musique.
            source.priority = 0;
            return source;
        }

        private void CrossfadeTo(MusicTrack track)
        {
            AudioClip clip = _library.GetMusic(track);

            // La source qui joue déjà ce morceau (y compris celle qui était en train de s'éteindre,
            // si le joueur revient en arrière pendant un fondu) remonte depuis son volume actuel,
            // sans repartir du début.
            if (clip != null && _other.clip == clip && _other.isPlaying)
            {
                Swap();
            }
            else if (clip == null || _current.clip != clip || !_current.isPlaying)
            {
                Swap();
                _current.Stop();
                _current.clip = clip;
                _current.volume = 0f;
                if (clip != null) _current.Play();
            }

            _fade?.Cancel();
            _fade?.Dispose();
            _fade = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);

            FadeAsync(_current, _other, _library.MusicCrossfadeSeconds, _fade.Token).Forget();
        }

        private void Swap() => (_current, _other) = (_other, _current);

        /// <summary>
        /// Monte <paramref name="incoming"/> jusqu'au plein volume et éteint <paramref name="outgoing"/>,
        /// chacune depuis son volume actuel : un fondu interrompu reprend sans saut.
        /// </summary>
        private static async UniTaskVoid FadeAsync(AudioSource incoming, AudioSource outgoing,
                                                   float duration, CancellationToken ct)
        {
            float incomingFrom = incoming.volume;
            float outgoingFrom = outgoing.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // Temps non mis à l'échelle : la musique ne dépend pas de la simulation.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                incoming.volume = Mathf.Lerp(incomingFrom, 1f, t);
                outgoing.volume = Mathf.Lerp(outgoingFrom, 0f, t);

                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
            }

            incoming.volume = 1f;
            outgoing.volume = 0f;
            outgoing.Stop();
            outgoing.clip = null;
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _fade?.Dispose();
        }
    }

    /// <summary>
    /// Repli quand l'AudioLibrary n'est pas renseignée : les scènes demandent leur musique sans
    /// avoir à savoir qu'il n'y en a pas.
    /// </summary>
    public sealed class SilentMusicPlayer : IMusicPlayer
    {
        public void Play(MusicTrack track) { }
    }
}
