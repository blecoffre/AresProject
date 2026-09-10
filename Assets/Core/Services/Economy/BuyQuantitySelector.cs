using Core.Models.Economy;
using R3;
using System;

namespace Core.Services.Economy
{
    /// <summary>
    /// Le mode d'achat multiple courant (x1, x10, x100, MAX), partagé par les trois onglets.
    ///
    /// Service du scope RACINE et non état privé de l'UpgradePanelPresenter, pour deux raisons :
    /// il survit au rechargement de la GameScene comme le reste de la partie, et le
    /// GameStateGateway doit pouvoir le lire et l'écrire — c'est une préférence d'interface qui
    /// se sauvegarde, et la retrouver à x1 à chaque lancement agace en fin de partie, où on
    /// n'achète plus qu'en MAX.
    ///
    /// Ce n'est PAS un état de run : le wipe de fin de partie ne le touche pas, volontairement.
    /// Perdre son mode d'achat parce qu'on s'est fait repérer n'aurait aucun sens.
    /// </summary>
    public class BuyQuantitySelector : IDisposable
    {
        /// <summary>
        /// Dernière valeur valide de l'enum. Une borne plutôt qu'un Enum.IsDefined : celui-ci
        /// boxe son argument et passe par la réflexion, pour valider quatre entiers contigus.
        /// </summary>
        private const BuyQuantity LastDefined = BuyQuantity.Max;

        private readonly ReactiveProperty<BuyQuantity> _current = new(BuyQuantity.X1);

        /// <summary>
        /// Le mode courant. Le ReactiveProperty de R3 filtre déjà les écritures identiques :
        /// recliquer sur le bouton déjà actif ne réveille pas les quarante-cinq générateurs
        /// affichés, aucun DistinctUntilChanged n'est nécessaire côté abonné.
        /// </summary>
        public ReadOnlyReactiveProperty<BuyQuantity> Current => _current;

        public void Select(BuyQuantity quantity)
        {
            if (!IsDefined(quantity)) return;

            _current.Value = quantity;
        }

        /// <summary>
        /// Restaure la préférence depuis la sauvegarde. La valeur vient d'un fichier que le
        /// joueur peut avoir édité, ou d'un binaire plus ancien : tout ce qui n'est pas un mode
        /// connu retombe sur x1 plutôt que de propager un enum hors domaine dans les presenters.
        /// </summary>
        public void Restore(int savedValue)
        {
            var quantity = (BuyQuantity)savedValue;
            _current.Value = IsDefined(quantity) ? quantity : BuyQuantity.X1;
        }

        public int Capture() => (int)_current.CurrentValue;

        private static bool IsDefined(BuyQuantity quantity) =>
            quantity >= BuyQuantity.X1 && quantity <= LastDefined;

        public void Dispose()
        {
            _current.Dispose();
        }
    }
}
