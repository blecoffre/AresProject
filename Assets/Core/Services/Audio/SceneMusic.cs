using VContainer.Unity;

namespace Core.Services.Audio
{
    /// <summary>
    /// Annonce l'ambiance musicale d'une scène dès que son scope démarre. Chaque scope de scène
    /// l'enregistre avec sa piste (<c>WithParameter</c>) : la scène dit où l'on est, sans que le
    /// lecteur ait à connaître les scènes ni le <c>SceneLoader</c>.
    /// </summary>
    public sealed class SceneMusic : IStartable
    {
        private readonly IMusicPlayer _music;
        private readonly MusicTrack _track;

        public SceneMusic(IMusicPlayer music, MusicTrack track)
        {
            _music = music;
            _track = track;
        }

        public void Start() => _music.Play(_track);
    }
}
