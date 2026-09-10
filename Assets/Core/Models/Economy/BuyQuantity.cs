namespace Core.Models.Economy
{
    /// <summary>
    /// Le mode d'achat multiple sélectionné par le joueur, partagé par les trois onglets.
    ///
    /// Valeurs explicites et JAMAIS réordonnées : cet entier part dans le fichier de sauvegarde
    /// (<c>SaveData.BuyQuantityMode</c>). Insérer une valeur au milieu redéfinirait
    /// silencieusement la préférence de tous les joueurs déjà installés — même piège que celui
    /// déjà rencontré sur <c>PrestigeBonusType</c>, dont les nouveaux cas ont dû être appendus
    /// en fin d'enum. Toute nouvelle quantité s'ajoute APRÈS <see cref="Max"/>… en pensant à
    /// corriger la borne de <c>IsDefined</c> dans le sélecteur.
    /// </summary>
    public enum BuyQuantity
    {
        X1 = 0,
        X10 = 1,
        X100 = 2,

        /// <summary>Autant de niveaux que le solde permet d'en payer d'affilée.</summary>
        Max = 3
    }
}
