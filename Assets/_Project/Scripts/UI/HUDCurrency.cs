using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 상단 바의 골드 표시.
     *
     * Update에서 폴링하지 않고 이벤트로 갱신한다. 잔액은 처치할 때만 바뀌는데,
     * 거의 변하지 않는 숫자를 매 프레임 BigDouble에서 포맷하는 것은 폰에서 낭비다.
     */
    public sealed class HUDCurrency : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "G ";

        private PlayerWallet wallet;

        private void Start()
        {
            wallet = PlayerWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[Onikiri] HUDCurrency found no PlayerWallet.");
                return;
            }

            wallet.GoldChanged += OnGoldChanged;
            OnGoldChanged(wallet.Gold);
        }

        private void OnDestroy()
        {
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
        }

        private void OnGoldChanged(BigDouble gold)
        {
            if (label != null) label.text = prefix + NumberFormatter.Format(gold);
        }
    }
}
