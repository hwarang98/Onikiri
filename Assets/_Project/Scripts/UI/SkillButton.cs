using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 스킬 패널 한 줄. 오의 하나의 상태를 보여주고 누르면 레벨을 올린다.
     *
     * UpgradeButton과 형제이고 규칙도 같다 - 이벤트로만 갱신하고, 잠긴 줄은
     * 레벨과 값을 감추며, 상한에 닿으면 회색이 아니라 금색(MASTER)이 된다.
     * 다른 것은 보여줄 것이 하나 더 있다는 점이다: **쿨다운.**
     *
     * ## 쿨다운을 아랫줄에 함께 적는 이유
     *
     * 이 축의 값어치는 배율 하나로 읽히지 않는다. 귀참 x7.04와 연참 x1.26을
     * 나란히 놓으면 귀참이 여섯 배 좋아 보이지만 실제 기여는 쿨다운으로 나눈
     * 값이고(0.32 대 0.18) 두 배도 차이가 나지 않는다. 배율만 보여주면 화면이
     * 거짓말을 한다.
     *
     * 그래서 아랫줄이 `x1.26 -> x1.41  ·  7.0초`다. 초당 환산값까지 적지는
     * 않았는데, 그것은 이 화면이 아니라 밸런스가 보는 숫자이고 한 줄에 넷째
     * 숫자가 들어오면 아무것도 안 읽힌다.
     */
    public sealed class SkillButton : MonoBehaviour
    {
        [SerializeField] private SkillSystem system;
        [SerializeField] private int slotIndex;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Image rowBackground;
        [SerializeField] private Image icon;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color masteredColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        [SerializeField] private Color masteredRowTint = new Color(0.62f, 0.55f, 0.42f, 1f);
        [SerializeField] private Color lockedColor = new Color32(0x5A, 0x51, 0x6B, 0xFF);
        [SerializeField] private Color lockedRowTint = new Color(0.34f, 0.36f, 0.55f, 1f);
        [SerializeField] private string masteredLabel = "MASTER";

        /**
         * @brief 잠긴 줄의 아이콘 틴트.
         *
         * 아이콘까지 죽여야 잠금이 읽힌다. 글자만 어둡게 하고 아이콘을 원래
         * 밝기로 두면, 목록에서 눈에 먼저 들어오는 것이 아이콘이라 잠긴 줄이
         * 오히려 도드라진다.
         */
        [SerializeField] private Color lockedIconTint = new Color(0.42f, 0.40f, 0.48f, 1f);

        [SerializeField] private Color iconTint = new Color(0.82f, 0.80f, 0.86f, 1f);

        private PlayerWallet wallet;
        private CharacterLevel character;

        private void Start()
        {
            wallet = PlayerWallet.Instance;
            character = CharacterLevel.Instance;

            if (button != null) button.onClick.AddListener(OnClick);
            if (system != null) system.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;

            // 해금은 레벨업으로 일어난다. 골드 이벤트만 듣고 있으면 잠긴 줄이
            // 조건에 닿아도 다음 구매가 있을 때까지 잠긴 채로 남는다 -
            // UpgradeButton이 스테이지 이벤트를 함께 듣는 것과 같은 이유다
            if (character != null) character.Changed += Refresh;

            Refresh();
        }

        /**
         * @brief 켜질 때마다 다시 그린다.
         *
         * 스킬 패널은 꺼진 채로 씬에 저장된다. 꺼진 오브젝트의 Start는 처음
         * 켜진 다음 프레임에 돌아서, 그 한 프레임 동안 빌더가 적어둔 자리표시가
         * 그대로 보인다. LockedTab·StatPointButton과 같은 처리다.
         */
        private void OnEnable()
        {
            if (wallet == null) wallet = PlayerWallet.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
            if (system != null) system.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (character != null) character.Changed -= Refresh;
        }

        private void OnGoldChanged(BigDouble gold)
        {
            Refresh();
        }

        private void OnClick()
        {
            if (system != null) system.TryPurchase(slotIndex);
        }

        private void Refresh()
        {
            if (system == null) return;

            var slot = system.GetSlot(slotIndex);
            if (slot == null) return;

            if (!system.IsUnlocked(slotIndex))
            {
                // 잠긴 줄은 이름만 남긴다. 레벨과 배율을 보여주면 이미 갖고 있는
                // 것으로 읽힌다 - UpgradeButton의 잠긴 줄과 같은 규칙이다
                if (nameLabel != null) { nameLabel.text = slot.displayName; nameLabel.color = lockedColor; }
                if (valueLabel != null) { valueLabel.text = FormatCooldown(slot); valueLabel.color = lockedColor; }
                if (costLabel != null) { costLabel.text = "Lv." + slot.unlockLevel; costLabel.color = lockedColor; }
                if (rowBackground != null) rowBackground.color = lockedRowTint;
                if (icon != null) icon.color = lockedIconTint;
                if (button != null) button.interactable = false;
                return;
            }

            if (icon != null) icon.color = iconTint;

            bool capped = system.IsMaxed(slotIndex);

            if (nameLabel != null)
            {
                nameLabel.text = slot.displayName + "  Lv." + slot.level;
                nameLabel.color = capped ? masteredColor : affordableColor;
            }

            if (valueLabel != null)
            {
                string value = capped
                    ? Multiplier(system.MultiplierOf(slotIndex))
                    : Multiplier(system.MultiplierOf(slotIndex)) + " → "
                      + Multiplier(system.NextMultiplierOf(slotIndex));

                valueLabel.text = value + "   " + FormatCooldown(slot);
                valueLabel.color = unaffordableColor;
            }

            bool affordable = wallet != null && wallet.CanAfford(system.CostOf(slotIndex));

            if (costLabel != null)
            {
                costLabel.text = capped ? masteredLabel : NumberFormatter.Format(system.CostOf(slotIndex));
                costLabel.color = capped ? masteredColor
                                : affordable ? affordableColor : unaffordableColor;
            }

            if (rowBackground != null)
                rowBackground.color = capped ? masteredRowTint : normalRowTint;

            if (button != null) button.interactable = !capped && affordable;
        }

        /** 빌더가 적어둔 평상시 행 색. 잠금·완성에서 되돌아올 자리가 필요하다 */
        [SerializeField] private Color normalRowTint = new Color(1f, 1f, 1f, 1f);

        private static string Multiplier(double value)
        {
            return "×" + value.ToString("F2");
        }

        /** "7.0초" - 쿨다운은 성장하지 않으므로 화살표가 없다 */
        private static string FormatCooldown(SkillSystem.Slot slot)
        {
            return slot.cooldownSeconds.ToString("F1") + "초";
        }
    }
}
