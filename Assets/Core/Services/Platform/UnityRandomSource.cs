using UnityEngine;

namespace Core.Services.Platform
{
    /// <summary>
    /// La source de hasard du jeu réel : le générateur d'Unity. Sans état, sans allocation.
    /// </summary>
    public sealed class UnityRandomSource : IRandomSource
    {
        /// <summary>
        /// <c>Random.value</c> rend [0 ; 1] BORNES INCLUSES : le 1 exact est ramené juste en
        /// dessous, pour qu'une chance de 100 % reste certaine et qu'une chance de 0 % ne
        /// réussisse jamais avec un test strict.
        /// </summary>
        public float NextFloat01()
        {
            float value = Random.value;
            return value >= 1f ? 0.99999994f : value;
        }
    }
}
