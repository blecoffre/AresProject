using System;

namespace Core.Models.Economy
{
    /// <summary>
    /// Ce qu'un clic sur « Acheter » coûterait et rapporterait MAINTENANT, pour le mode d'achat
    /// courant. Struct readonly : la vue en demande une à chaque variation du lot, sur les
    /// quarante-cinq générateurs affichés.
    ///
    /// <see cref="Levels"/> est ce qu'on AFFICHE, <see cref="IsAffordable"/> ce qu'on autorise.
    /// Les deux se séparent dans un seul cas, et il est voulu : en MAX sans le sou, on annonce
    /// quand même le prix d'un niveau — un bouton gris sans montant ne dit pas au joueur ce qui
    /// lui manque.
    /// </summary>
    public readonly struct PurchaseQuote : IEquatable<PurchaseQuote>
    {
        /// <summary>Nombre de niveaux que le lot affiché représente. Toujours ≥ 1.</summary>
        public readonly int Levels;

        /// <summary>Coût cumulé de ces niveaux, somme géométrique déjà résolue.</summary>
        public readonly double TotalCost;

        /// <summary>Vrai si le lot ENTIER est payable. En x10 et x100, c'est tout ou rien.</summary>
        public readonly bool IsAffordable;

        public PurchaseQuote(int levels, double totalCost, bool isAffordable)
        {
            Levels = levels;
            TotalCost = totalCost;
            IsAffordable = isAffordable;
        }

        // IEquatable explicite et non le comparateur par défaut : sans lui, un
        // DistinctUntilChanged sur ce type passerait par ObjectEqualityComparer, donc par un
        // boxing par émission — sur un flux réveillé à chaque versement de cycle.
        public bool Equals(PurchaseQuote other) =>
            Levels == other.Levels
            && TotalCost.Equals(other.TotalCost)
            && IsAffordable == other.IsAffordable;

        public override bool Equals(object obj) => obj is PurchaseQuote other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Levels, TotalCost, IsAffordable);
    }
}
