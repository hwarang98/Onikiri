using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 상단 바의 보석 표시. HUDCurrency와 같은 규칙이다.
     *
     * 폴링하지 않고 이벤트로 갱신한다. 보석은 골드보다도 드물게 바뀌므로
     * (퀘스트를 받을 때만) 매 프레임 다시 그릴 이유가 더 없다.
     *
     * ## 0이어도 숨기지 않는다
     *
     * 잔액이 0이면 감추는 방법도 있지만 그러지 않는다. 신규 플레이어가
     * **보석이라는 것이 있다**는 사실을 알아야 퀘스트를 열어볼 이유가 생긴다 -
     * 처음 하나를 받은 뒤에 나타나면 그때는 이미 늦다.
     */
    public sealed class HUDGems : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private GemWallet wallet;

        private void Start()
        {
            wallet = GemWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[Onikiri] HUDGems found no GemWallet.");
                return;
            }

            wallet.GemsChanged += OnGemsChanged;
            OnGemsChanged(wallet.Gems);
        }

        private void OnDestroy()
        {
            if (wallet != null) wallet.GemsChanged -= OnGemsChanged;
        }

        /**
         * @brief 축약하지 않고 그대로 적는다.
         *
         * 골드는 자릿수가 계속 늘어 "1.2K"가 필요하지만, 보석은 퀘스트로만
         * 들어오므로 오래 해도 네 자리 근처다. 축약하면 **정확한 개수를 알 수
         * 없게 되는데**, 상점이 붙는 순간 "지금 몇 개인가"가 곧 살 수 있는지다.
         */
        private void OnGemsChanged(long gems)
        {
            if (label != null) label.text = gems.ToString("N0");
        }
    }
}
