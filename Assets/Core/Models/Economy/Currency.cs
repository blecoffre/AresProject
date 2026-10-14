using R3;
using System;

namespace Core.Models.Economy
{
    public class Currency : IDisposable
    {
        private readonly ReactiveProperty<double> _amount;
        public ReadOnlyReactiveProperty<double> Amount => _amount;

        public Currency(double initialAmount = 0)
        {
            _amount = new ReactiveProperty<double>(initialAmount);
        }

        /// <summary>
        /// Ajoute un montant STRICTEMENT positif et fini ; tout le reste est ignoré. Les gardes
        /// vivaient seulement dans <c>UserCurrencies</c>, alors que <see cref="Currency"/> est
        /// exposé : un appel direct avec un négatif retirait de l'argent sans passer par
        /// <see cref="TryRemove"/>, et un NaN rendait le solde illisible pour toute la partie.
        /// </summary>
        public void Add(double value)
        {
            // !(value > 0) écarte aussi NaN, que toute comparaison rend fausse.
            if (!(value > 0d) || double.IsInfinity(value)) return;

            _amount.Value += value;
        }

        public bool TryRemove(double value)
        {
            // Même garde qu'Add : un NaN passait le test « < 0 » et ne retirait rien, mais par
            // chance ; un retrait se refuse explicitement s'il n'est pas un nombre fini positif.
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value)) return false;

            if (_amount.CurrentValue >= value)
            {
                _amount.Value -= value;
                return true;
            }

            return false;
        }

        public void Reset(double value = 0d)
        {
            // Math.Max(0, NaN) rend NaN : un solde de départ illisible est remis à zéro.
            _amount.Value = double.IsNaN(value) || double.IsInfinity(value) ? 0d : Math.Max(0d, value);
        }

        public void Dispose()
        {
            _amount.Dispose();
        }
    }
}
