using System;
using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerWallet : MonoBehaviour
    {
        public event Action<int> BalanceChanged;
        public event Action<int> MatchEarningsChanged;

        public int Balance { get; private set; }
        public int MatchEarnings { get; private set; }

        private void Awake() => Balance = 0;

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Balance = amount > int.MaxValue - Balance ? int.MaxValue : Balance + amount;
            BalanceChanged?.Invoke(Balance);
        }

        public void AddMatchCoins(int amount)
        {
            if (amount <= 0) return;
            int credited = Mathf.Min(amount, int.MaxValue - Balance);
            Balance += credited;
            MatchEarnings = credited > int.MaxValue - MatchEarnings ? int.MaxValue : MatchEarnings + credited;
            BalanceChanged?.Invoke(Balance);
            MatchEarningsChanged?.Invoke(MatchEarnings);
        }

        public void ResetMatchEarnings()
        {
            MatchEarnings = 0;
            MatchEarningsChanged?.Invoke(MatchEarnings);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Balance < amount) return false;
            Balance -= amount;
            BalanceChanged?.Invoke(Balance);
            return true;
        }
    }
}
