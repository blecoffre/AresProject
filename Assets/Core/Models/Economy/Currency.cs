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

        public void Add(double value)
        {
            _amount.Value += value;
        }

        public bool TryRemove(double value)
        {
            if (value < 0) return false;

            if (_amount.CurrentValue >= value)
            {
                _amount.Value -= value;
                return true;
            }

            return false;
        }

        public void Reset(double value = 0d)
        {
            _amount.Value = Math.Max(0d, value);
        }

        public void Dispose()
        {
            _amount.Dispose();
        }
    }
}
