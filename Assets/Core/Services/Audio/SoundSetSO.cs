using UnityEngine;

namespace Core.Services.Audio
{
    /// <summary>
    /// Une famille de sons dont une variante est tirée au hasard à chaque lecture : clics
    /// d'interface (« Frappe », « Validation »…) comme sons d'événements (saisie, victoire,
    /// paliers de Trace). Varier le son évite la lassitude d'un son identique répété ; une
    /// famille d'un seul fichier à hauteur fixe fait un son « signature », toujours reconnaissable.
    ///
    /// Ajouter une famille ne demande aucun code : créer l'asset, puis le désigner sur
    /// l'<see cref="AudioLibrarySO"/> ou sur le <c>UiClickSound</c> d'un bouton.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundSet", menuName = "Core/Audio/Sound Set")]
    public sealed class SoundSetSO : ScriptableObject
    {
        [Tooltip("Les variantes. Le lecteur n'enchaîne jamais deux fois la même.")]
        [SerializeField] private AudioClip[] _clips = new AudioClip[0];

        [Tooltip("Variation aléatoire de hauteur, de part et d'autre de la hauteur d'origine " +
                 "(0,05 = ±5 %). Multiplie les variantes perçues sans ajouter de fichier.")]
        [SerializeField, Range(0f, 0.2f)] private float _pitchVariation = 0.05f;

        [Tooltip("Volume de la famille, pour l'équilibrer avec les autres familles et la musique.")]
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        public int Count => _clips.Length;
        public AudioClip GetClip(int index) => _clips[index];
        public float PitchVariation => _pitchVariation;
        public float Volume => _volume;
    }
}
