using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 오의 하나의 팝업 (#14). **정보가 먼저, 그다음이 행동이다.**
     *
     * ## 왜 바로 강화하지 않는가
     *
     * 그전에는 목록의 줄을 누르는 것이 곧 구매였다(SkillButton.OnClick →
     * TryPurchase). 강화 목록과 같은 문법인데, 오의는 강화 축과 성격이
     * 다르다:
     *
     *   강화 축   무엇을 하는지 이름이 다 말한다("공격력 강화")
     *   오의     이름만으로는 **무엇을 하는지 모른다**("혈파동")
     *
     * 화면에 있던 것은 배율과 쿨다운뿐이라("x2.88 · 16.0초") 그 오의가
     * 한 대상을 때리는지 화면 전체를 쓸어버리는지 알 방법이 없었다. 값은
     * 있는데 **거동**이 없다.
     *
     * 그래서 한 겹을 넣는다. 줄을 누르면 팝업이 열리고, 거기서 무엇을 하는
     * 오의인지 읽은 다음 강화하거나 장착한다. 골드가 나가는 조작이 실수로
     * 일어나지 않는다는 것은 덤이다.
     *
     * ## 로직은 하나도 안 옮겨왔다
     *
     * 강화는 SkillSystem.TryPurchase, 장착은 SkillSystem.Equip이다. 목록
     * 줄이 부르던 그 함수를 그대로 부른다 - 팝업은 **표현**이고, 규칙은
     * 여전히 시스템에 있다(잠금·비용·자리 판정 전부).
     */
    public sealed class SkillInfoPopup : MonoBehaviour
    {
        [SerializeField] private SkillSystem system;

        [Header("머리")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Image icon;

        /**
         * @brief 오의별 아이콘. **빌더가 카탈로그 순서대로 옮겨 적는다.**
         *
         * 팝업 하나가 모든 오의를 그리므로 아이콘도 열 때마다 바뀌어야 한다.
         * 목록 줄은 자기 아이콘 하나를 빌드 시점에 박아두면 되지만 이쪽은
         * 그럴 수 없다.
         *
         * 배선이 비면 아이콘 칸을 **끈다**. 실기에서 흰 사각형이 떴는데,
         * 스프라이트 없는 Image가 정확히 그 모양이다 - 이 프로젝트가
         * "없는 것보다 나쁘다"고 적어둔 상태다(QuestPanelBuilder.AddRewardIcon).
         */
        [SerializeField] private Sprite[] icons = new Sprite[0];

        [Tooltip("무엇을 하는 오의인가. 거동 + 배율 + 주기")]
        [SerializeField] private TMP_Text descriptionLabel;

        [Header("탭")]
        [SerializeField] private Button upgradeTab;
        [SerializeField] private TMP_Text upgradeTabLabel;
        [SerializeField] private Image upgradeTabBackground;
        [SerializeField] private GameObject upgradePage;

        [SerializeField] private Button equipTab;
        [SerializeField] private TMP_Text equipTabLabel;
        [SerializeField] private Image equipTabBackground;
        [SerializeField] private GameObject equipPage;

        [Header("강화 탭")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeButtonLabel;

        [Header("장착 탭")]
        [SerializeField] private TMP_Text equipStateLabel;
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipButtonLabel;

        [Header("색")]
        [SerializeField] private Color textColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color masteredColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        [SerializeField] private Color selectedTint = new Color(0.62f, 0.66f, 1.00f, 1f);
        [SerializeField] private Color unselectedTint = new Color(0.40f, 0.42f, 0.70f, 1f);

        /** 지금 보고 있는 오의. -1이면 아무것도 안 열려 있다 */
        private int index = -1;

        /** 0 = 강화, 1 = 장착 */
        private int tab;

        private PlayerWallet wallet;

        private void Start()
        {
            wallet = PlayerWallet.Instance;

            if (upgradeTab != null) upgradeTab.onClick.AddListener(() => SelectTab(0));
            if (equipTab != null) equipTab.onClick.AddListener(() => SelectTab(1));
            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgrade);
            if (equipButton != null) equipButton.onClick.AddListener(OnEquip);

            if (system != null) system.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
        }

        private void OnDestroy()
        {
            if (system != null) system.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
        }

        private void OnGoldChanged(BigDouble gold)
        {
            Refresh();
        }

        /**
         * @brief 이 오의로 팝업을 연다. 목록 줄이 부른다.
         *
         * 탭은 **강화에서 시작한다.** 이 팝업을 여는 이유의 대부분이 그것이고,
         * 장착은 자리가 빈 사람만 쓰는 문이다. 지난번에 장착 탭을 보고 닫았다고
         * 다음 오의도 장착 탭으로 열면, 강화하려던 사람이 매번 한 번 더 누른다.
         */
        public void Open(int slotIndex)
        {
            index = slotIndex;
            tab = 0;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            Refresh();
        }

        private void SelectTab(int value)
        {
            tab = value;
            Refresh();
        }

        private void OnUpgrade()
        {
            if (system == null || index < 0) return;

            // 목록 줄이 부르던 그 함수다. 잠금·비용 판정은 전부 그 안에 있다
            system.TryPurchase(index);
            // Changed 이벤트가 Refresh를 부른다 - 여기서 또 부르면 두 번 그린다
        }

        private void OnEquip()
        {
            if (system == null || index < 0) return;

            // 빈 자리 중 첫 칸에 끼운다 (SkillButton.OnEquip과 같은 규칙)
            for (int slot = 0; slot < system.SlotCapacity; slot++)
            {
                if (system.IsSlotLocked(slot)) continue;
                if (system.EquippedAt(slot) >= 0) continue;
                system.Equip(slot, index);
                return;
            }
        }

        private void Refresh()
        {
            if (system == null || index < 0) return;

            var slot = system.GetSlot(index);
            if (slot == null) return;

            bool unlocked = system.IsUnlocked(index);
            bool maxed = system.IsMaxed(index);

            if (nameLabel != null) nameLabel.text = slot.displayName;

            if (icon != null)
            {
                var sprite = icons != null && index < icons.Length ? icons[index] : null;
                icon.sprite = sprite;
                // 스프라이트가 없으면 칸을 끈다 - 켜두면 흰 사각형이 뜬다
                icon.enabled = sprite != null;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = SkillDescription.Of(slot.id, system.MultiplierOf(index),
                                                            slot.cooldownSeconds);
                descriptionLabel.color = dimColor;
            }

            // ---- 탭 색
            if (upgradeTabBackground != null)
                upgradeTabBackground.color = tab == 0 ? selectedTint : unselectedTint;
            if (equipTabBackground != null)
                equipTabBackground.color = tab == 1 ? selectedTint : unselectedTint;
            if (upgradeTabLabel != null) upgradeTabLabel.color = tab == 0 ? textColor : dimColor;
            if (equipTabLabel != null) equipTabLabel.color = tab == 1 ? textColor : dimColor;

            if (upgradePage != null) upgradePage.SetActive(tab == 0);
            if (equipPage != null) equipPage.SetActive(tab == 1);

            // ---- 강화 탭
            if (levelLabel != null)
            {
                levelLabel.text = unlocked ? "Lv." + slot.level : "잠김";
                levelLabel.color = maxed ? masteredColor : textColor;
            }

            if (valueLabel != null)
            {
                double now = system.MultiplierOf(index);
                valueLabel.text = maxed || !unlocked
                    ? "x" + NumberFormatter.FormatStat(BigDouble.FromDouble(now), 2)
                    : "x" + NumberFormatter.FormatStat(BigDouble.FromDouble(now), 2)
                      + " → x" + NumberFormatter.FormatStat(
                          BigDouble.FromDouble(system.NextMultiplierOf(index)), 2);
                valueLabel.color = dimColor;
            }

            var cost = system.CostOf(index);
            bool affordable = wallet != null && wallet.CanAfford(cost);

            if (costLabel != null)
            {
                costLabel.text = maxed ? "MASTER" : NumberFormatter.Format(cost);
                costLabel.color = maxed ? masteredColor : affordable ? textColor : dimColor;
            }

            if (upgradeButton != null) upgradeButton.interactable = unlocked && !maxed && affordable;
            if (upgradeButtonLabel != null)
                upgradeButtonLabel.color = unlocked && !maxed && affordable ? textColor : dimColor;

            // ---- 장착 탭
            bool equipped = system.IsEquipped(index);

            bool hasRoom = false;
            for (int s = 0; s < system.SlotCapacity; s++)
            {
                if (system.IsSlotLocked(s)) continue;
                if (system.EquippedAt(s) < 0) { hasRoom = true; break; }
            }

            if (equipStateLabel != null)
            {
                equipStateLabel.text = !unlocked ? "아직 열리지 않은 오의다"
                    : equipped ? (system.SlotOf(index) + 1) + "번 자리에 장착 중"
                    : hasRoom ? "빈 자리에 끼울 수 있다"
                    : "자리가 가득 찼다 - 슬롯 칩을 눌러 빼낸다";
                equipStateLabel.color = dimColor;
            }

            if (equipButton != null) equipButton.interactable = unlocked && !equipped && hasRoom;
            if (equipButtonLabel != null)
            {
                equipButtonLabel.text = equipped ? "장착 중" : "장착";
                equipButtonLabel.color = unlocked && !equipped && hasRoom ? textColor : dimColor;
            }
        }
    }
}
