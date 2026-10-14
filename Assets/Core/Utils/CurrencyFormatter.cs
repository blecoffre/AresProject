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
    ///
    /// <b>Au-delà du millier, trois décimales</b> (« 1,234K ») : chaque palier s'affiche par pas
    /// d'un millième de son unité — 1 Data en K, 1 000 en M, un million en B, un milliard en T.
    /// </summary>
    public static class CurrencyFormatter
    {
        /// <summary>Nombre de pas affichés par unité : trois décimales.</summary>
        private const double StepsPerUnit = 1000d;

        /// <summary>
        /// Le bruit de virgule flottante qu'un PRIX absorbe avant d'être plafonné, en RELATIF.
        ///
        /// Les coûts sortent d'un `Math.Pow` : un prix qui vaut exactement 100 peut s'y stocker
        /// en 100,000000000000014. Plafonner tel quel afficherait « 101 » — un mensonge dans
        /// l'autre sens, et plus déroutant encore que celui qu'on corrige.
        ///
        /// Relatif et non plus en décimales fixes depuis le 2026-10-15 : six décimales du
        /// millième d'unité absorbaient 500 Datas au palier T, assez pour afficher « 1T » sur un
        /// prix de 1 000 000 000 400. Le bruit d'un double est de l'ordre de 1e-16 ; 1e-12 laisse
        /// quatre ordres de grandeur de marge aux sommes géométriques des achats par lot.
        /// </summary>
        private const double CostNoiseTolerance = 1e-12;

        /// <summary>Les paliers, du plus petit au plus grand.</summary>
        private static readonly string[] Suffixes = { "K", "M", "B", "T" };

        /// <summary>
        /// Formate un SOLDE ou un GAIN (K, M, B, T). Tronque vers le bas : on n'annonce jamais
        /// au joueur plus d'argent qu'il n'en a.
        ///
        /// La troncature porte sur la valeur BRUTE, sans marge de bruit : le bouton d'achat
        /// compare ce même double au prix, l'affichage ne doit donc jamais le dépasser. Jusqu'au
        /// 2026-10-15 « 0.### » ARRONDISSAIT — 1 234,5 Datas s'affichaient « 1,235K », 999 999,9
        /// « 1000K » —, et un joueur pouvait lire un solde égal au prix et voir le bouton gris.
        ///
        /// <b>Ne pas utiliser pour un prix</b> — voir <see cref="FormatCost"/>.
        /// </summary>
        public static string Format(double value, IFormatProvider culture)
        {
            if (value < 1_000d) return Math.Floor(value).ToString("0", culture);

            // Le palier se choisit sur la valeur brute : tronquer ne fait jamais monter d'un
            // palier, 999 999,9 reste « 999,999K ».
            double unit = 1_000d;
            int tier = 0;
            while (tier < Suffixes.Length - 1 && value >= unit * 1_000d)
            {
                unit *= 1_000d;
                tier++;
            }

            // Compté en pas entiers sur la valeur brute, et non « value / unit » remultiplié :
            // l'aller-retour pourrait rendre 1233,9999… pour 1 234 tout rond, et afficher un pas
            // de moins que ce que le joueur possède.
            double steps = Math.Floor(value / (unit / StepsPerUnit));

            return (steps / StepsPerUnit).ToString("0.###", culture) + Suffixes[tier];
        }

        /// <summary>
        /// Formate un PRIX ou un SEUIL À ATTEINDRE. Arrondit à la hausse, à la précision
        /// réellement affichée.
        ///
        /// La troncature de <see cref="Format"/> est correcte pour un solde et FAUSSE pour un
        /// prix : `SCR_01` au niveau 1 coûte 10,7 et s'affichait « Coût: 10 ». Un joueur avec
        /// exactement 10 Datas lisait le prix, voyait le bouton rester gris, et concluait que le
        /// jeu était cassé. Un prix affiché doit être un montant qui SUFFIT toujours.
        ///
        /// Plafonner au millième d'unité, et pas seulement à l'unité : « 1,234K » masque déjà 999
        /// Datas, et arrondir vers le bas cette décimale-là reproduirait le défaut un cran plus haut.
        /// </summary>
        public static string FormatCost(double value, IFormatProvider culture)
        {
            double whole = CeilAbsorbingNoise(value);
            if (whole < 1_000d) return whole.ToString("0", culture);

            // Le palier se choisit APRÈS le plafonnement, pas avant : plafonner peut faire passer
            // le montant au palier suivant. Choisi sur la valeur brute, 999,9 s'affichait « 1000 »
            // et 999 999,9 « 1000K » jusqu'au 2026-10-15, au lieu de « 1K » et « 1M ».
            double unit = 1_000d;
            int last = Suffixes.Length - 1;

            for (int tier = 0; tier < last; tier++, unit *= 1_000d)
            {
                double steps = CeilAbsorbingNoise(value / (unit / StepsPerUnit));
                if (steps < StepsPerUnit * 1_000d)
                    return (steps / StepsPerUnit).ToString("0.###", culture) + Suffixes[tier];
            }

            // Le dernier palier n'a pas de suivant : « 1500T » reste « 1500T ».
            double lastSteps = CeilAbsorbingNoise(value / (unit / StepsPerUnit));

            return (lastSteps / StepsPerUnit).ToString("0.###", culture) + Suffixes[last];
        }

        /// <summary>Plafonne à l'entier, après avoir retiré le bruit relatif d'un prix.</summary>
        private static double CeilAbsorbingNoise(double value)
        {
            return Math.Ceiling(value - Math.Abs(value) * CostNoiseTolerance);
        }
    }
}
