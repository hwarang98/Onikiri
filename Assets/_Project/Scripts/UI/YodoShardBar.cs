using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 요도 화면 맨 위의 파편 줄. 잔량을 적고, 보석으로 한 묶음을 판다.
     *
     * ## 왜 행이 아니라 줄인가
     *
     * 파편은 요도별이 아니라 **하나의 지갑**이다(YodoSystem.shards). 자루
     * 넷의 행마다 잔량을 적으면 같은 숫자가 네 번 뜨고, 그중 하나를 쓰면
     * 넷이 동시에 바뀐다 - 화면이 "각자의 재료"라고 거짓말한다.
     *
     * ## 왜 보석 버튼이 여기 있는가
     *
     * 촉매의 성격 때문이다(YodoCurve.ShardPackGems 주석). 파는 것은 특정
     * 요도의 티어가 아니라 **모자란 재료**이고, 그래서 자루가 아니라 지갑
     * 옆에 선다. 자루의 행에 두면 "이 칼에만 쓰는 보석"으로 읽힌다.
     */
    public sealed class YodoShardBar : MonoBehaviour
    {
        [SerializeField] private YodoSystem system;

        [Tooltip("\"파편 128\"")]
        [SerializeField] private TMP_Text shardLabel;

        [Tooltip("\"봉인한 요도 2 / 4\"")]
        [SerializeField] private TMP_Text progressLabel;

        [SerializeField] private Button buyButton;
        [SerializeField] private Image buyBackground;
        [SerializeField] private TMP_Text buyTitle;
        [SerializeField] private TMP_Text buyCost;

        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color buyButtonTint = new Color(0.42f, 0.56f, 1.00f, 1f);

        private GemWallet gems;

        private void Start()
        {
            if (system == null) system = YodoSystem.Instance;
            gems = GemWallet.Instance;

            if (buyButton != null) buyButton.onClick.AddListener(OnBuy);
            if (system != null) system.Changed += Refresh;
            if (gems != null) gems.GemsChanged += OnGemsChanged;

            Refresh();
        }

        private void OnEnable()
        {
            if (system == null) system = YodoSystem.Instance;
            if (gems == null) gems = GemWallet.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (buyButton != null) buyButton.onClick.RemoveListener(OnBuy);
            if (system != null) system.Changed -= Refresh;
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
        }

        private void OnGemsChanged(long balance) { Refresh(); }

        private void OnBuy()
        {
            if (system != null) system.TryBuyShards();
        }

        private void Refresh()
        {
            if (system == null) return;

            if (shardLabel != null) shardLabel.text = "파편 " + system.Shards;

            if (progressLabel != null)
            {
                progressLabel.text = system.IsOnikiriComplete
                    ? YodoCatalog.OnikiriName + " 완성"
                    : "봉인 " + system.SealedCount + " / " + YodoCatalog.Count;
                progressLabel.color = unaffordableColor;
            }

            bool can = system.CanBuyShards;

            if (buyTitle != null)
            {
                buyTitle.text = "파편 조달";
                buyTitle.color = can ? affordableColor : unaffordableColor;
            }
            if (buyCost != null)
            {
                buyCost.text = system.IsUnlocked
                    ? "보석 " + YodoCurve.ShardPackGems + " → 파편 " + YodoCurve.ShardPackShards
                    : YodoCurve.UnlockStage + "스테이지부터";
                buyCost.color = can ? affordableColor : unaffordableColor;
            }
            if (buyButton != null) buyButton.interactable = can;

            // 판까지 죽인다(41b 규칙). interactable=false만으로는 SpriteSwap
            // 버튼의 판이 평소처럼 밝게 남아 활성처럼 보인다
            if (buyBackground != null)
                buyBackground.color = can ? buyButtonTint : Dimmed(buyButtonTint);
        }

        private static Color Dimmed(Color c)
        {
            return new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, c.a);
        }
    }
}
