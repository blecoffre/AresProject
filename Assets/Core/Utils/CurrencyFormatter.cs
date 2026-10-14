using System;

namespace Core.Utils
{
    /// <summary>
    /// Formate les montants du jeu (K, M, B, T).
    ///
    /// <b>La culture est un paramètre, et c'est celle de la langue du jeu</b> :
    /// <c>ILocalizationService.Culture</c>. Jusqu'au 2026-10-14 ce formateur forçait la culture
    /// invariante (« 1.5K ») pendant que les durées suivaient celle de Windows (« 1,5 s ») : deux
    /// séparateurs sur le même écran. Désormais un joueur francophone lit « 1,5K » et « 1,5 s », un
    /// anglophone « 1.5K » et « 1.5 s », quel que soit son système.
    /// </summary>
    public static class CurrencyFormatter
    {
        /// <summary>
        /// Précision affichée au-delà du millier : trois décimales, soit « 1,234K ».
        /// </summary>
        private const double SubUnitFactor = 1000d;

        /// <summary>
        /// Le bruit de virgule flottante à absorber avant d'arrondir à la hausse.
        ///
        /// Les coûts sortent d'un `Math.Pow` : un prix qui vaut exactement 100 peut s'y stocker
        /// en 100,000000000000014. Plafonner tel quel afficherait « 101 » — un mensonge dans
        /// l'autre sens, et plus déroutant encore que celui qu'on corrige.
        /// </summary>
        private const int NoiseDigits = 6;

        /// <summary>
        /// Formate un SOLDE ou un GAIN (K, M, B, T). Tronque vers le bas : on n'annonce jamais
        /// au joueur plus d'argent qu'il n'en a.
        ///
        /// <b>Ne pas utiliser pour un prix</b> — voir <see cref="FormatCost"/>.
        /// </summary>
        public static string Format(double value, IFormatProvider culture)
        {
            if (value >= 1_000_000_000_000d)
                return (value / 1_000_000_000_000d).ToString("0.###", culture) + "T";
            if (value >= 1_000_000_000d)
                return (value / 1_000_000_000d).ToString("0.###", culture) + "B";
            if (value >= 1_000_000d)
                return (value / 1_000_000d).ToString("0.###", culture) + "M";
            if (value >= 1_000d)
                return (value / 1_000d).ToString("0.###", culture) + "K";

            // En dessous de 1000, on affiche l'entier
            return Math.Floor(value).ToString("0", culture);
        }

        /// <summary>
        /// Formate un PRIX ou un SEUIL À ATTEINDRE. Arrondit à la hausse, à la précision
        /// réellement affichée.
        ///
        /// La troncature de <see cref="Format"/> est correcte pour un solde et FAUSSE pour un
        /// prix : `SCR_01` au niveau 1 coûte 10,7 et s'affichait « Coût: 10 ». Un joueur avec
        /// exactement 10 Datas lisait le prix, voyait le bouton rester gris, et concluait que le
        /// jeu était cassé. Un prix affiché doit être un montant qui SUFFIT toujours.
        /// </summary>
        public static string FormatCost(double value, IFormatProvider culture)
        {
            if (value >= 1_000_000_000_000d)
                return CeilSubUnit(value / 1_000_000_000_000d, culture) + "T";
            if (value >= 1_000_000_000d)
                return CeilSubUnit(value / 1_000_000_000d, culture) + "B";
            if (value >= 1_000_000d)
                return CeilSubUnit(value / 1_000_000d, culture) + "M";
            if (value >= 1_000d)
                return CeilSubUnit(value / 1_000d, culture) + "K";

            return Math.Ceiling(Math.Round(value, NoiseDigits)).ToString("0", culture);
        }

        /// <summary>
        /// Plafonne à la troisième décimale, celle que « 0.### » affichera. Plafonner à l'unité
        /// ne suffirait pas : « 1,234K » masque déjà 999 unités, et arrondir vers le bas cette
        /// décimale-là reproduirait exactement le défaut d'origine, un cran plus haut.
        /// </summary>
        private static string CeilSubUnit(double scaledValue, IFormatProvider culture)
        {
            double ceiled = Math.Ceiling(Math.Round(scaledValue * SubUnitFactor, NoiseDigits)) / SubUnitFactor;

            return ceiled.ToString("0.###", culture);
        }
    }
}
