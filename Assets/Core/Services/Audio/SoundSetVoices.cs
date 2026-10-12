using UnityEngine;
using UnityEngine.Audio;

namespace Core.Services.Audio
{
    /// <summary>
    /// Un petit groupe de sources qui jouent des <see cref="SoundSetSO"/> à tour de rôle. Chaque
    /// lecteur (interface, événements) possède le sien : une rafale de clics ne peut pas couper
    /// une alerte de Trace, et inversement.
    ///
    /// <b>Pourquoi plusieurs sources.</b> La hauteur se règle sur la source entière : la changer
    /// pendant qu'un son joue encore le déformerait. En rotation, une source n'est réutilisée
    /// qu'après toutes les autres.
    ///
    /// Créées une fois, à la construction (appelée depuis le Start d'un lecteur) : jouer n'alloue rien.
    /// </summary>
    public sealed class SoundSetVoices
    {
        private readonly AudioSource[] _voices;
        private int _nextVoice;

        // Le dernier son joué par ce groupe : deux lectures consécutives ne sonnent jamais pareil.
        // Le hasard pur répète régulièrement, et c'est ce que l'oreille repère.
        private AudioClip _lastClip;

        /// <param name="priority">Priorité Unity (0 = la plus haute, 128 = défaut). Sous la limite
        /// de voix, c'est la plus basse qui cède.</param>
        public SoundSetVoices(AudioHost host, string name, AudioMixerGroup output, int voiceCount, int priority)
        {
            _voices = new AudioSource[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                AudioSource source = host.CreateSource(name + " " + (i + 1), output);
                source.priority = priority;
                _voices[i] = source;
            }
        }

        /// <summary>Joue une variante de la famille. Rend faux si elle est vide.</summary>
        public bool Play(SoundSetSO set)
        {
            if (set == null || set.Count == 0) return false;

            AudioClip clip = PickClip(set);
            if (clip == null) return false;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            float variation = set.PitchVariation;
            voice.pitch = 1f + Random.Range(-variation, variation);
            voice.volume = set.Volume;
            voice.clip = clip;
            voice.Play();

            _lastClip = clip;
            return true;
        }

        /// <summary>
        /// Tire une variante au hasard, en sautant la dernière jouée : on tire parmi les autres
        /// puis on décale l'index, ce qui garde un tirage uniforme sans boucle de re-tirage.
        /// </summary>
        private AudioClip PickClip(SoundSetSO set)
        {
            int count = set.Count;
            if (count == 1) return set.GetClip(0);

            int lastIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (set.GetClip(i) == _lastClip)
                {
                    lastIndex = i;
                    break;
                }
            }

            if (lastIndex < 0) return set.GetClip(Random.Range(0, count));

            int index = Random.Range(0, count - 1);
            if (index >= lastIndex) index++;
            return set.GetClip(index);
        }
    }
}
