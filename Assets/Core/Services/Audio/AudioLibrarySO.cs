using UnityEngine;
using UnityEngine.Audio;

namespace Core.Services.Audio
{
    /// <summary>
    /// Tout le contenu audio du jeu et sa sortie dans le mixer, en un seul asset glissé sur le
    /// <c>RootLifetimeScope</c>. Remplacer un son se fait ici, sans toucher au code ni aux scènes.
    ///
    /// Un clip laissé vide n'est pas une erreur : la musique correspondante se tait, le clic reste
    /// muet. C'est ce qui permet de livrer le système avant d'avoir choisi les sons.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Core/Audio/Audio Library")]
    public sealed class AudioLibrarySO : ScriptableObject
    {
        [Header("Sorties du mixer")]
        [Tooltip("Groupe Music du GameMixer : son volume suit l'option « Musique ».")]
        [SerializeField] private AudioMixerGroup _musicGroup;

        [Tooltip("Groupe SFX du GameMixer : son volume suit l'option « Effets sonores ».")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Header("Musiques")]
        [SerializeField] private AudioClip _mainMenuMusic;
        [SerializeField] private AudioClip _gameMusic;

        [Tooltip("Durée du fondu enchaîné entre deux musiques, en secondes.")]
        [SerializeField, Min(0f)] private float _musicCrossfadeSeconds = 1.5f;

        [Header("Interface")]
        [Tooltip("La famille de clics par défaut de tous les boutons. Un bouton peut en désigner " +
                 "une autre sur son composant UiClickSound.")]
        [SerializeField] private SoundSetSO _defaultClickSet;

        [Tooltip("Joué quand on clique un bouton grisé (« pas encore »). Doit rester très discret : " +
                 "on le déclenche souvent. Vide = refus muet.")]
        [SerializeField] private SoundSetSO _refusalSet;

        [Header("Événements de jeu")]
        [Tooltip("Saisie Fédérale : la Trace atteint 100 %.")]
        [SerializeField] private SoundSetSO _seizureSet;

        [Tooltip("Victoire de la campagne.")]
        [SerializeField] private SoundSetSO _victorySet;

        [Tooltip("Un palier d'extraction franchi sur la jauge RELEVÉE, un élément par palier dans " +
                 "l'ordre des CleanExitTiers (50 %, 75 %, 90 %). Des sons de plus en plus tendus " +
                 "font monter l'alerte ; s'il y a moins de familles que de paliers, la dernière sert " +
                 "pour les suivants.")]
        [SerializeField] private SoundSetSO[] _traceTierSets = new SoundSetSO[0];

        public AudioMixerGroup MusicGroup => _musicGroup;
        public AudioMixerGroup SfxGroup => _sfxGroup;
        public float MusicCrossfadeSeconds => _musicCrossfadeSeconds;
        public SoundSetSO DefaultClickSet => _defaultClickSet;
        public SoundSetSO RefusalSet => _refusalSet;
        public SoundSetSO SeizureSet => _seizureSet;
        public SoundSetSO VictorySet => _victorySet;

        /// <summary>La famille d'alerte du palier d'index donné, ou null si aucune n'est prévue.</summary>
        public SoundSetSO GetTraceTierSet(int tierIndex)
        {
            if (_traceTierSets.Length == 0) return null;
            return _traceTierSets[Mathf.Min(tierIndex, _traceTierSets.Length - 1)];
        }

        public AudioClip GetMusic(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.MainMenu: return _mainMenuMusic;
                case MusicTrack.Game: return _gameMusic;
                default: return null;
            }
        }
    }
}
