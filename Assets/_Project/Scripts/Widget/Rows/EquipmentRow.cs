using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 장비 화면의 슬롯 한 칸. 무기 또는 방어구 하나를 그리고 두 버튼을 준다.
     *
     * UpgradeButton·SkillButton과 형제이지만 다른 것이 하나 있다 - **버튼이
     * 둘이고 재화가 서로 다르다.**
     *
     * ## 두 재화가 화면에서 섞이지 않게 하는 규칙
     *
     * E-3이 요구한 것이고, 여기서는 세 가지로 지킨다.
     *
     *   자리   왼쪽 버튼은 언제나 골드(단련), 오른쪽은 언제나 보석(등급업)
     *   색     등급업 버튼만 보석 색(청)을 쓴다. 골드 버튼은 나머지 화면과 같다
     *   글자   비용 줄에 재화 이름을 적는다. 숫자만 있으면 어느 쪽인지 모른다
     *
     * ## "강화"라고 부르지 않는다
     *
     * 왼쪽 버튼은 **단련**이다. 일일 퀘스트 "강화 15회 구매"와 업적 "강화 총합"이
     * UpgradeSystem의 구매만 세는데(EquipmentSystem.TryTemper 주석), 화면에서
     * 같은 이름을 쓰면 세지 않는 것이 거짓말이 된다.
     */
    public sealed class EquipmentRow : MonoBehaviour
    {
        [SerializeField] private EquipmentSystem system;
        [SerializeField] private int slotIndex;

        [Header("표시")]
        [SerializeField] private Image icon;
        [SerializeField] private Image rowBackground;

        [Tooltip("등급 이름. \"요괴검\"")]
        [SerializeField] private TMP_Text gradeLabel;

        [Tooltip("슬롯 이름 + 장착 표시. \"무기 · 장착 중\"")]
        [SerializeField] private TMP_Text slotLabel;

        [Tooltip("지금 곱하고 있는 배수. \"공격력 x1.08\"")]
        [SerializeField] private TMP_Text statLabel;

        [Tooltip("\"단련 Lv.6 / 10\"")]
        [SerializeField] private TMP_Text levelLabel;

        [Header("단련 (골드)")]
        [SerializeField] private Button temperButton;
        [SerializeField] private Image temperBackground;
        [SerializeField] private TMP_Text temperTitle;
        [SerializeField] private TMP_Text temperCost;

        [Header("등급업 (보석 + 골드)")]
        [SerializeField] private Button gradeButton;
        [SerializeField] private Image gradeBackground;
        [SerializeField] private TMP_Text gradeTitle;
        [SerializeField] private TMP_Text gradeCost;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color masteredColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        [SerializeField] private Color lockedColor = new Color32(0x5A, 0x51, 0x6B, 0xFF);

        [Tooltip("평상시 행 틴트. 빌더가 스킨에서 적어준다")]
        [SerializeField] private Color normalRowTint = Color.white;
        [SerializeField] private Color masteredRowTint = new Color(0.62f, 0.55f, 0.42f, 1f);
        [SerializeField] private Color lockedRowTint = new Color(0.34f, 0.36f, 0.55f, 1f);

        [SerializeField] private Color iconTint = new Color(0.82f, 0.80f, 0.86f, 1f);
        [SerializeField] private Color lockedIconTint = new Color(0.42f, 0.40f, 0.48f, 1f);

        [Tooltip("버튼 판의 평상시 틴트. 잠긴 화면에서 되살릴 값이라 들고 있는다")]
        [SerializeField] private Color temperButtonTint = new Color(0.34f, 0.36f, 0.68f, 1f);
        [SerializeField] private Color gradeButtonTint = new Color(0.42f, 0.56f, 1.00f, 1f);

        private PlayerWallet wallet;
        private GemWallet gems;
        private StageProgress stage;

        private void Start()
        {
            wallet = PlayerWallet.Instance;
            gems = GemWallet.Instance;
            stage = Object.FindFirstObjectByType<StageProgress>();

            if (temperButton != null) temperButton.onClick.AddListener(OnTemper);
            if (gradeButton != null) gradeButton.onClick.AddListener(OnGradeUp);

            if (system != null) system.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
            if (gems != null) gems.GemsChanged += OnGemsChanged;

            // 해금이 **스테이지**로 걸린다. 골드 이벤트만 듣고 있으면 보스를
            // 잡아 st11이 된 순간에도 화면이 잠긴 채로 남는다 - SkillButton이
            // 캐릭터 레벨 이벤트를 함께 듣는 것과 같은 이유이고, 다른 것은
            // 이 축의 조건이 레벨이 아니라 스테이지라는 점뿐이다
            if (stage != null) stage.Changed += Refresh;

            Refresh();
        }

        /** 판이 꺼진 채로 저장된다. 켜질 때 다시 그린다 - SkillButton과 같은 처리 */
        private void OnEnable()
        {
            if (wallet == null) wallet = PlayerWallet.Instance;
            if (gems == null) gems = GemWallet.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (temperButton != null) temperButton.onClick.RemoveListener(OnTemper);
            if (gradeButton != null) gradeButton.onClick.RemoveListener(OnGradeUp);

            if (system != null) system.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
            if (stage != null) stage.Changed -= Refresh;
        }

        private void OnGoldChanged(BigDouble gold) { Refresh(); }
        private void OnGemsChanged(long balance) { Refresh(); }

        private void OnTemper()
        {
            if (system != null) system.TryTemper(slotIndex);
        }

        private void OnGradeUp()
        {
            if (system != null) system.TryUpgradeGrade(slotIndex);
        }

        private void Refresh()
        {
            if (system == null) return;

            var slot = system.GetSlot(slotIndex);
            if (slot == null) return;

            if (!system.IsUnlocked)
            {
                DrawLocked(slot);
                return;
            }

            if (icon != null) icon.color = iconTint;

            // 잠긴 미리보기에서 눌러뒀던 판을 되살린다 - DrawLocked를 지난
            // 카드가 해금 이벤트로 이 길로 돌아온다
            if (temperBackground != null) temperBackground.color = temperButtonTint;
            if (gradeBackground != null) gradeBackground.color = gradeButtonTint;

            bool maxed = system.IsMaxed(slotIndex);

            if (gradeLabel != null)
            {
                gradeLabel.text = slot.GradeName;
                gradeLabel.color = maxed ? masteredColor : affordableColor;
            }

            if (slotLabel != null)
            {
                // 장착 상태를 적는다. 인벤토리가 없어서 언제나 장착 중이지만,
                // 적지 않으면 "이것을 끼고 있는가"가 화면 어디에도 없다
                slotLabel.text = slot.slotName + " · 장착 중";
                slotLabel.color = unaffordableColor;
            }

            if (statLabel != null)
            {
                statLabel.text = StatName(slot.stat) + " " + Multiplier(system.MultiplierOf(slotIndex));
                statLabel.color = unaffordableColor;
            }

            if (levelLabel != null)
            {
                levelLabel.text = "단련 Lv." + slot.level
                                + " / " + EquipmentCurve.MaxLevelForGrade(slot.grade);
                levelLabel.color = unaffordableColor;
            }

            DrawTemper(slot);
            DrawGrade(slot);

            if (rowBackground != null) rowBackground.color = maxed ? masteredRowTint : normalRowTint;
        }

        /**
         * @brief 잠긴 화면. **등급도 배수도 감춘다.**
         *
         * UpgradeButton·SkillButton의 잠긴 줄과 같은 규칙이다 - 값을 보여주면
         * 이미 갖고 있는 것으로 읽힌다. 대신 **언제 열리는지**를 적는다.
         */
        private void DrawLocked(EquipmentSystem.Slot slot)
        {
            if (icon != null) icon.color = lockedIconTint;
            if (rowBackground != null) rowBackground.color = lockedRowTint;

            if (gradeLabel != null) { gradeLabel.text = slot.slotName; gradeLabel.color = lockedColor; }
            if (slotLabel != null) { slotLabel.text = "대장간 미개방"; slotLabel.color = lockedColor; }
            if (statLabel != null) { statLabel.text = string.Empty; }

            if (levelLabel != null)
            {
                levelLabel.text = EquipmentCurve.UnlockStage + "스테이지부터";
                levelLabel.color = lockedColor;
            }

            SetButton(temperButton, temperTitle, temperCost, "단련", string.Empty, false, lockedColor);
            SetButton(gradeButton, gradeTitle, gradeCost, "등급업", string.Empty, false, lockedColor);

            // 판까지 죽인다(41b - PetRow와 같은 원칙). interactable=false만으로는
            // SpriteSwap 버튼의 판이 평소처럼 밝게 남아 활성처럼 보인다.
            // 알파가 아니라 RGB를 누른다 - 알파는 뒤가 비친다
            if (temperBackground != null) temperBackground.color = Dimmed(temperButtonTint);
            if (gradeBackground != null) gradeBackground.color = Dimmed(gradeButtonTint);
        }

        private void DrawTemper(EquipmentSystem.Slot slot)
        {
            if (!system.CanTemper(slotIndex))
            {
                // 단련이 끝났으면 남은 일은 등급업뿐이다. 버튼을 지우지 않고
                // 무엇이 막고 있는지 적는다 - "왜 안 눌리지"가 화면에 남으면
                // 그것은 고장으로 읽힌다
                bool gradeOpen = system.CanUpgradeGrade(slotIndex);
                SetButton(temperButton, temperTitle, temperCost, "단련",
                          gradeOpen ? "등급업 필요" : "MASTER", false,
                          gradeOpen ? unaffordableColor : masteredColor);
                return;
            }

            var cost = system.TemperCostOf(slotIndex);
            bool affordable = wallet != null && wallet.CanAfford(cost);

            // **화살표의 왼쪽은 카드 오른쪽 위(statLabel)에 이미 있다.** 버튼마다
            // 다시 적으면 같은 숫자가 한 카드에 세 번 뜨고, 44pt에서 그 줄이
            // 버튼 폭을 넘는다 - 퀘스트 행에서 보상 문구로 겪은 자리다
            SetButton(temperButton, temperTitle, temperCost,
                      "단련 → " + Multiplier(system.NextTemperMultiplierOf(slotIndex)),
                      "골드 " + NumberFormatter.Format(cost),
                      affordable, affordable ? affordableColor : unaffordableColor);
        }

        private void DrawGrade(EquipmentSystem.Slot slot)
        {
            if (!system.CanUpgradeGrade(slotIndex))
            {
                bool last = slot.grade >= EquipmentCurve.GradeCount;
                SetButton(gradeButton, gradeTitle, gradeCost, "등급업",
                          last ? "최고 등급" : "단련 먼저", false,
                          last ? masteredColor : unaffordableColor);
                return;
            }

            var gold = system.GradeGoldCostOf(slotIndex);
            int gemCost = system.GradeGemCostOf(slotIndex);

            bool affordable = wallet != null && wallet.CanAfford(gold)
                           && gems != null && gems.CanAfford(gemCost);

            SetButton(gradeButton, gradeTitle, gradeCost,
                      "등급업 → " + Multiplier(system.NextGradeMultiplierOf(slotIndex)),
                      // **보석을 먼저 적는다.** 이 버튼을 가르는 것이 보석이고,
                      // 골드는 따라오는 값이다. 순서가 곧 무엇이 관문인지다
                      "보석 " + gemCost + " · 골드 " + NumberFormatter.Format(gold),
                      affordable, affordable ? affordableColor : unaffordableColor);
        }

        private void SetButton(Button button, TMP_Text title, TMP_Text cost,
                               string titleText, string costText, bool interactable, Color color)
        {
            if (title != null) { title.text = titleText; title.color = color; }
            if (cost != null) { cost.text = costText; cost.color = color; }
            if (button != null) button.interactable = interactable;
        }

        /** 판 죽이기. PetRow와 같은 값 - 두 화면의 "죽은 버튼"이 같은 어둠이어야 한다 */
        private static Color Dimmed(Color c)
        {
            return new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, c.a);
        }

        private static string StatName(EquipmentStat stat)
        {
            return stat == EquipmentStat.AttackPower ? "공격력" : "최대 체력";
        }

        /**
         * @brief 배수를 **소수 세 자리**로 적는다. 다른 화면은 두 자리다.
         *
         * 무기 단련 한 칸이 +1.2%라 두 자리에서는 x1.00 -> x1.01로 보이고,
         * 실제 폭(0.012)의 절반이 반올림에 먹힌다. 오의 배율(x1.26 -> x1.41)은
         * 두 자리로 충분했지만 이 축은 한 칸이 작아서 자릿수가 곧 정보다.
         *
         * 축이 작은 것 자체는 밴드가 정한 사실이고(EquipmentCatalog), 화면이
         * 그것을 반올림으로 감추면 "눌러도 안 움직인다"가 된다.
         */
        private static string Multiplier(double value)
        {
            return "×" + value.ToString("F3");
        }
    }
}
