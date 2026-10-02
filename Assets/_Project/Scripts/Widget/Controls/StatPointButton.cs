using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 스탯 포인트 축 한 줄. 강화 행과 같은 모양으로 만든다.
     *
     * 재화가 골드가 아니라 포인트일 뿐 하는 일은 UpgradeButton과 같다. 그래서
     * 같은 자리에 같은 모양으로 선다 - 두 축이 다르게 생기면 플레이어는 이것이
     * 다른 종류의 조작이라고 배우고, 실제로는 아니다.
     *
     * 다른 점 하나는 되돌리기가 비싸다는 것이다. 골드는 다시 벌 수 있지만
     * 포인트는 레벨을 다시 올려야 나온다. 66단계부터 보석 초기화가 있지만
     * (StatPointReset - 첫 1회 무료, 이후 150 보석) 사흘치 보석이라, 찍기 전에
     * 결과를 보여주는 규칙(배수 표시)은 그대로다.
     */
    public sealed class StatPointButton : MonoBehaviour
    {
        [SerializeField] private string axisId = CharacterLevel.AttackAmpId;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text costLabel;

        [SerializeField] private string displayName = "공격력 증폭";

        /**
         * @brief 값의 표시 방식 (66단계).
         *
         *   Multiplier     공격력·체력 증폭. 예전 그대로 "×1.025" (소수 셋째 자리)
         *   BonusPercent   경험치·골드 획득 증폭. "+0.8%" - 강화 탭의 골드 획득
         *                  행과 같은 모양(UpgradeTrack.FormatAs)
         *   Duration       방치 보상 증폭. "+16시간 40분" - 배수가 아니다
         */
        [SerializeField] private UpgradeTrack.Display display = UpgradeTrack.Display.Multiplier;

        /**
         * @brief 잠긴 축의 문구 (66단계). "Lv.15 해금" - {0}이 해금 레벨이다.
         *
         * 경험치 축만 게이트가 있다(StatPointCurve.ExpUnlockLevel). 잠긴 줄도
         * 목록에 서 있는 것은 강화 행(UpgradeButton.lockedLabel)과 같은 규칙이다 -
         * 줄이 없으면 그 축이 나중에 온다는 것을 알 길이 없다. 빌더가 넣는다
         */
        [SerializeField] private string lockedFormat = string.Empty;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        private CharacterLevel character;

        /**
         * @brief 켜질 때마다 다시 그린다.
         *
         * Start만으로는 모자란다. 18단계에서 성장 축이 자기 탭을 갖게 되면서
         * 이 행은 **꺼진 채로 씬에 저장된다.** 꺼져 있는 오브젝트의 Start는 처음
         * 켜진 다음 프레임에 도는데, 그동안 화면에는 빌더가 넣어둔 자리표시
         * "Name" / "Cost" / "Value"가 그대로 보인다 - 성장 탭을 처음 누른 사람이
         * 정확히 그 글자를 한 프레임 본다.
         *
         * OnEnable은 그 첫 활성화에서 Start보다 먼저 돌고, CharacterLevel은
         * 자기 Awake에서 Instance를 세우므로 여기서 이미 찾을 수 있다.
         */
        private void OnEnable()
        {
            if (character == null) character = CharacterLevel.Instance;
            Refresh();
        }

        private void Start()
        {
            if (character == null) character = CharacterLevel.Instance;

            if (button != null) button.onClick.AddListener(OnClick);
            if (character != null) character.Changed += Refresh;

            // 배수가 바뀌면 이 줄의 대가와 증가폭이 통째로 달라진다
            UpgradeBatchSelector.Changed += Refresh;

            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
            if (character != null) character.Changed -= Refresh;
            UpgradeBatchSelector.Changed -= Refresh;
        }

        /**
         * @brief 이번에 찍을 점 수. 강화 목록과 **같은 배수 줄**을 읽는다 (#3 후속).
         *
         * 성장 탭에도 배수가 필요한 이유는 강화와 같다 - 포인트는 레벨업으로
         * 쌓이고 레벨업은 방치로 밀리므로, 오래 안 열어본 사람의 화면에는
         * 수십 점이 쌓여 있다.
         *
         * 배수 줄을 하나 더 만들지 않고 그것을 그대로 읽는다. 두 줄이 있으면
         * "지금 ×100인 것이 어느 쪽인가"를 화면에서 확인해야 하고, 그 확인이
         * 배수를 고르는 값보다 비싸다(UpgradeBatchSelector 머리 주석의
         * "배수는 목록 전체의 모드"가 여기서도 그대로다).
         *
         * 남은 포인트가 모자라면 거기까지 줄인다 - 화면에 뜬 수와 실제로
         * 나가는 포인트가 같아야 한다.
         */
        private int PlannedPoints()
        {
            if (character == null) return 0;
            return character.SpendableInto(axisId, UpgradeBatchSelector.Current);
        }

        private void OnClick()
        {
            if (character == null) return;

            int planned = PlannedPoints();

            // 한 점이면 예전 경로 그대로. 배수 줄이 없는 씬도 여기로 떨어진다
            if (planned <= 1) { character.TrySpendPoint(axisId); return; }

            character.TrySpendPoints(axisId, planned);
        }

        private void Refresh()
        {
            if (character == null) return;

            if (!character.IsAxisUnlocked(axisId))
            {
                ShowLocked();
                return;
            }

            int points = character.PointsIn(axisId);
            bool maxed = character.IsAxisMaxed(axisId);

            if (nameLabel != null) nameLabel.text = displayName + "  Lv." + points;

            // 이번에 찍을 점 수 (#3 후속). 배수가 ×1이면 예전과 같은 한 점이다
            int planned = maxed ? 0 : PlannedPoints();

            if (valueLabel != null)
            {
                // 강화 행과 같은 "지금 → 다음" 모양. 포인트당 0.5%는 한 줄로 보면
                // 작지만, 이 표시가 없으면 찍었을 때 무엇이 달라졌는지 화면 어디에도
                // 나타나지 않는다 - 증폭은 다른 축의 값에 곱해져 들어가기 때문이다
                //
                // 배수로 찍을 때는 **그만큼 간 값**을 보여준다. 되돌릴 수 없는
                // 재화라 누르기 전에 결과가 화면에 있어야 한다
                string now = Format(points);
                valueLabel.text = maxed
                    ? now
                    : now + " → " + Format(points + Mathf.Max(1, planned))
                      + (planned > 1 ? "   +" + planned : string.Empty);
            }

            bool affordable = character.UnspentPoints > 0;

            if (costLabel != null)
            {
                // 값은 이번에 나가는 **포인트 수**다. 예전에는 늘 "1"이었는데,
                // 배수가 붙으면 그 숫자가 곧 대가의 크기다
                costLabel.text = maxed ? "MAX" : Mathf.Max(1, planned).ToString();
                costLabel.color = maxed || affordable ? affordableColor : unaffordableColor;
            }

            if (button != null) button.interactable = !maxed && affordable;
        }

        /**
         * @brief 소수 셋째 자리까지.
         *
         * 둘째 자리로는 이 축이 보여줘야 할 변화가 정확히 그 자리에서 사라진다 -
         * 포인트당 0.5%라 Lv.1의 "×1.005 → ×1.010"이 둘 다 "×1.01"로 뭉개진다.
         * 화면에서는 눌러도 아무것도 바뀌지 않는 것으로 보인다.
         *
         * UpgradeButton이 Format 대신 FormatStat을 쓰는 것과 같은 이유이고,
         * 같은 실수를 한 번 더 한 것이다.
         */
        private void ShowLocked()
        {
            if (nameLabel != null) nameLabel.text = displayName;
            if (valueLabel != null)
                valueLabel.text = string.Format(lockedFormat, StatPointCurve.UnlockLevelFor(axisId));
            if (costLabel != null)
            {
                costLabel.text = string.Empty;
                costLabel.color = unaffordableColor;
            }
            if (button != null) button.interactable = false;
        }

        private string Format(int points)
        {
            if (display == UpgradeTrack.Display.Duration)
                return UpgradeTrack.FormatAs(display,
                    Onikiri.Core.BigDouble.FromDouble(StatPointCurve.IdleExtraAccrual(points).TotalMinutes));

            double multiplier = StatPointCurve.Multiplier(axisId, points);
            if (display == UpgradeTrack.Display.BonusPercent)
                return UpgradeTrack.FormatAs(display, Onikiri.Core.BigDouble.FromDouble(multiplier));

            return "×" + multiplier.ToString("0.000");
        }
    }
}
