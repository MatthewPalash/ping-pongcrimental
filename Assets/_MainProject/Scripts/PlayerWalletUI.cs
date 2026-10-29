using TMPro;
using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerWalletUI : MonoBehaviour
    {
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private string prefix = "Монеты: ";

        private void OnEnable()
        {
            if (wallet != null) wallet.MatchEarningsChanged += UpdateBalance;
            Refresh();
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.MatchEarningsChanged -= UpdateBalance;
        }

        public void SetWallet(PlayerWallet playerWallet)
        {
            if (isActiveAndEnabled && wallet != null) wallet.MatchEarningsChanged -= UpdateBalance;
            wallet = playerWallet;
            if (isActiveAndEnabled && wallet != null) wallet.MatchEarningsChanged += UpdateBalance;
            Refresh();
        }

        public void Refresh() => UpdateBalance(wallet != null ? wallet.MatchEarnings : 0);

        private void UpdateBalance(int balance)
        {
            if (balanceText != null) balanceText.text = prefix + balance;
        }
    }
}
