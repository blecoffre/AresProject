using System;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;

namespace Core.Services.Audio
{
    /// <summary>
    /// Le GameObject qui porte toutes les sources audio du jeu, et l'unique <see cref="AudioListener"/>.
    ///
    /// Il est rattaché au <c>RootLifetimeScope</c>, donc à la RootScene qui ne se décharge jamais.
    /// Un <c>new GameObject</c> nu atterrirait dans la scène ACTIVE — le menu ou la partie — et
    /// disparaîtrait avec elle au premier changement de scène, coupant la musique.
    ///
    /// <b>L'écouteur vit ici et non sur les caméras des scènes :</b> <c>SceneLoader</c> décharge
    /// une scène avant de charger la suivante. Avec un écouteur par caméra, la transition
    /// laisserait quelques frames sans aucun écouteur — la musique sauterait — et Unity le
    /// signalerait dans la console. Tous les sons du jeu sont en 2D : sa position est sans effet.
    /// </summary>
    public sealed class AudioHost : IDisposable
    {
        private readonly Transform _parent;
        private GameObject _root;

        // Rien n'est créé ici : un constructeur appelé par le conteneur ne doit pas avoir d'effet
        // de bord. L'hôte naît à la première source demandée, depuis le Start d'un lecteur.
        public AudioHost(LifetimeScope rootScope)
        {
            _parent = rootScope.transform;
        }

        /// <summary>
        /// Crée une source 2D routée vers le groupe donné. Réservé à l'initialisation des lecteurs.
        /// </summary>
        public AudioSource CreateSource(string name, AudioMixerGroup output)
        {
            if (_root == null)
            {
                _root = new GameObject("[Audio]");
                _root.transform.SetParent(_parent, false);
                _root.AddComponent<AudioListener>();
            }

            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = output;

            if (output == null)
            {
                Debug.LogWarning($"[AUDIO] La source « {name} » n'a pas de groupe de mixer : " +
                                 "elle ignore les volumes des options. Renseigner l'AudioLibrary.");
            }

            return source;
        }

        public void Dispose()
        {
            // L'application se ferme, ou le Play Mode s'arrête : la scène est déjà en train de
            // tout détruire, le test évite seulement de détruire deux fois.
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
