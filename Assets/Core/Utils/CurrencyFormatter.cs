using System;
using System.Globalization;

namespace Core.Utils
{
    public static class CurrencyFormatter
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Convertit un double en format lisible (K, M, B, T).
        /// </summary>
        public static string Format(double value)
        {
            if (value >= 1_000_000_000_000d)
                return (value / 1_000_000_000_000d).ToString("0.###", Culture) + "T";
            if (value >= 1_000_000_000d)
                return (value / 1_000_000_000d).ToString("0.###", Culture) + "B";
            if (value >= 1_000_000d)
                return (value / 1_000_000d).ToString("0.###", Culture) + "M";
            if (value >= 1_000d)
                return (value / 1_000d).ToString("0.###", Culture) + "K";

            // En dessous de 1000, on affiche l'entier
            return Math.Floor(value).ToString("0", Culture);
        }
    }
}