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
     * 다른 점 하나는 되돌릴 수 없다는 것이다. 골드는 다시 벌 수 있지만 포인트는
     * 레벨을 다시 올려야 나온다. 승급(12단계 범위 밖)이 생기면 그때 초기화
     * 수단이 붙는다.
     */
    public sealed class StatPointButton : MonoBehaviour
    {
        [SerializeField] private string axisId = CharacterLevel.AttackAmpId;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private TMP_Text costLabel;

        [SerializeField] private string displayName = "공격력 증폭";

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

            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
            if (character != null) character.Changed -= Refresh;
        }

        private void OnClick()
        {
            if (character != null) character.TrySpendPoint(axisId);
        }

        private void Refresh()
        {
            if (character == null) return;

            int points = character.PointsIn(axisId);
            bool maxed = character.IsAxisMaxed(axisId);

            if (nameLabel != null) nameLabel.text = displayName + "  Lv." + points;

            if (valueLabel != null)
            {
                // 강화 행과 같은 "지금 → 다음" 모양. 포인트당 0.5%는 한 줄로 보면
                // 작지만, 이 표시가 없으면 찍었을 때 무엇이 달라졌는지 화면 어디에도
                // 나타나지 않는다 - 증폭은 다른 축의 값에 곱해져 들어가기 때문이다
                string now = Format(StatPointCurve.Multiplier(points));
                valueLabel.text = maxed
                    ? now
                    : now + " → " + Format(StatPointCurve.Multiplier(points + 1));
            }

            bool affordable = character.UnspentPoints > 0;

            if (costLabel != null)
            {
                costLabel.text = maxed ? "MAX" : "1";
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
        private static string Format(double multiplier)
        {
            return "×" + multiplier.ToString("0.000");
        }
    }
}
