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

        /**
         * @brief 장착 버튼 (49단계). 줄 본체와 **다른 일을 한다.**
         *
         *   줄 본체   골드를 써서 레벨을 올린다 (26단계 그대로)
         *   이 버튼   자리에 끼운다 (재화가 안 든다)
         *
         * 두 조작을 한 버튼에 겹치지 않는 이유는 되돌릴 수 있는 정도가 다르기
         * 때문이다. 장착은 언제든 되돌릴 수 있고 레벨업은 골드가 사라진다 -
         * 실수로 눌렀을 때의 대가가 다르면 버튼도 달라야 한다.
         *
         * **빼는 것은 여기가 아니라 슬롯 칩이다**(SkillSlotChip). 한쪽 조작에
         * 두 뜻을 주지 않는다는 규칙이고, 그 이유는 그쪽 주석에 있다.
         */
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;

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

        /** 장착된 줄의 바탕. 네 자리가 목록 어디에 있는지 한눈에 읽혀야 한다 */
        [SerializeField] private Color equippedRowTint = new Color(0.52f, 0.46f, 0.72f, 1f);

        private PlayerWallet wallet;
        private CharacterLevel character;

        private void Start()
        {
            wallet = PlayerWallet.Instance;
            character = CharacterLevel.Instance;

            if (button != null) button.onClick.AddListener(OnClick);
            if (equipButton != null) equipButton.onClick.AddListener(OnEquip);
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
            if (equipButton != null) equipButton.onClick.RemoveListener(OnEquip);
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

        /**
         * @brief **빈 자리에만** 끼운다. 차 있으면 아무 일도 하지 않는다.
         *
         * 자동으로 무언가를 밀어내지 않는 것이 요점이다. 밀어낸다면 무엇을
         * 밀어낼지를 코드가 정하게 되는데, 그 결정이 곧 이 스텝이 만든 질문
         * ("네 자리에 무엇을 두는가")이고 그것은 플레이어의 것이다.
         *
         * 자리가 없으면 버튼이 아예 안 눌린다(Refresh) - 눌러도 아무 일이
         * 없는 버튼을 두는 것이 이 프로젝트가 가장 싫어하는 상태다.
         */
        private void OnEquip()
        {
            if (system == null) return;

            for (int slot = 0; slot < system.SlotCapacity; slot++)
            {
                if (system.EquippedAt(slot) >= 0) continue;
                system.Equip(slot, slotIndex);
                return;
            }
        }

        /**
         * @brief 잠긴 줄이 적는 조건. **게이트 종류마다 다른 말을 한다.**
         *
         * 50단계에 세 번째 종류가 생겼고, 그때 이 함수가 실기에서 거짓말을
         * 했다 - 혈조의 잠긴 줄이 **"41스테이지"**로 떴다. 그 값은 게이트가
         * 아니라 골드 비용의 기준점인데(SkillSpec.GachaGated 주석), 게이트를
         * `unlockLevel <= 0`으로만 갈랐으므로 스테이지 게이트로 읽혔다.
         *
         * 화면에 나온 증상이 특히 나쁘다: **st41을 이미 지난 플레이어에게
         * "41스테이지 필요"라고 적는다.** 조건을 이미 만족했는데 잠겨 있으니
         * 버그로 읽히고, 실제로는 조건 자체가 다른 것이다.
         *
         * 뽑기 게이트를 **가장 먼저** 본다. 순서가 반대면 unlockStage가
         * 있는 한 언제나 그쪽이 이긴다 - SkillSystem.IsUnlocked가 같은
         * 순서를 쓰는 것과 같은 이유다.
         */
        private static string GateText(SkillSystem.Slot slot)
        {
            if (slot.gachaGated) return "뽑기";

            return slot.unlockLevel > 0
                ? "Lv." + slot.unlockLevel
                : slot.unlockStage + "스테이지";
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
                if (costLabel != null) { costLabel.text = GateText(slot); costLabel.color = lockedColor; }
                if (rowBackground != null) rowBackground.color = lockedRowTint;
                if (icon != null) icon.color = lockedIconTint;
                if (button != null) button.interactable = false;
                ShowEquip(false, false, "장착");
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

            // 49단계: 장착이 완성(MASTER)보다 위다. 네 자리가 목록 어디에
            // 있는지가 이 화면에서 가장 먼저 읽혀야 하는 사실이기 때문이다 -
            // 금색 MASTER는 그 오의 하나의 사정이고, 장착은 지금 나가는 것이
            // 무엇인지를 말한다
            bool equipped = system.IsEquipped(slotIndex);

            if (rowBackground != null)
                rowBackground.color = equipped ? equippedRowTint
                                    : capped ? masteredRowTint : normalRowTint;

            if (button != null) button.interactable = !capped && affordable;

            bool hasRoom = false;
            for (int open = 0; open < system.SlotCapacity; open++)
                if (system.EquippedAt(open) < 0) { hasRoom = true; break; }

            ShowEquip(true, !equipped && hasRoom, equipped ? "장착 중" : "장착");
        }

        /**
         * @brief 장착 버튼의 상태를 한 자리에서 적는다.
         *
         * @param visible     잠긴 줄에서는 아예 감춘다 - 못 끼우는 것에 버튼이
         *                    떠 있으면 잠금이 조건이 아니라 실패로 읽힌다
         * @param interactable 빈 자리가 있고 아직 안 끼운 줄에서만 눌린다
         */
        private void ShowEquip(bool visible, bool interactable, string text)
        {
            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(visible);
                equipButton.interactable = interactable;
            }
            if (equipLabel != null)
            {
                equipLabel.text = text;
                equipLabel.color = interactable ? affordableColor : unaffordableColor;
            }
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
