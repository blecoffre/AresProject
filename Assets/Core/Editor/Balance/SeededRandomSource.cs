using Core.Services.Platform;
using System;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Le hasard d'une campagne simulée : graine fixe, donc deux lancements identiques rendent
    /// exactement la même campagne. Sans ça, comparer deux réglages mêlerait leur effet au bruit
    /// des tirages (aujourd'hui, le seul tirage du jeu est le coup critique de l'Overclock).
    /// </summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SeededRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public float NextFloat01() => (float)_random.NextDouble();
    }
}
