using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 강화 한 줄의 버튼.
     *
     * 이름/레벨, 현재 효과, 다음 비용을 보여주고 누르면 구매한다.
     *
     * 갱신은 이벤트로만 한다. 골드는 처치할 때마다 바뀌므로 매 프레임 폴링하면 값이
     * 그대로인 프레임에도 BigDouble 포맷과 TMP 메시 재생성을 계속 돌리게 된다.
     */
    public sealed class UpgradeButton : MonoBehaviour
    {
        [SerializeField] private UpgradeSystem system;
        [SerializeField] private int trackIndex;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);

        [Tooltip("골드가 모자랄 때의 비용 글자색. 버튼을 숨기지 않고 색만 죽여서 " +
                 "다음 목표가 얼마인지 계속 보이게 한다")]
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        private PlayerWallet wallet;

        private void Start()
        {
            wallet = PlayerWallet.Instance;

            if (button != null) button.onClick.AddListener(OnClick);
            if (system != null) system.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;

            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
            if (system != null) system.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
        }

        private void OnGoldChanged(BigDouble gold)
        {
            Refresh();
        }

        private void OnClick()
        {
            if (system != null) system.TryPurchase(trackIndex);
        }

        private void Refresh()
        {
            if (system == null) return;

            var track = system.GetTrack(trackIndex);
            if (track == null) return;

            if (nameLabel != null)
                nameLabel.text = track.DisplayName + "  Lv." + track.Level;

            bool affordable = wallet != null && wallet.CanAfford(track.Cost);

            if (costLabel != null)
            {
                costLabel.text = track.IsMaxed ? "최대" : NumberFormatter.Format(track.Cost);
                costLabel.color = track.IsMaxed || affordable ? affordableColor : unaffordableColor;
            }

            if (button != null) button.interactable = !track.IsMaxed && affordable;
        }
    }
}
