using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 뽑기 확률표 팝업 (69단계). **배너가 접은 표를 여기서 공개한다.**
     *
     * 46~68단계에는 확률표가 배너 본문에 펼쳐져 있었다("숨기면 공개한 것이
     * 아니다"). 69단계에 배너가 행 하나(제목 · 버튼 둘 · 소환 레벨 바)로 줄면서
     * 표가 들어갈 자리가 없어졌고, 그 표를 제목 줄의 "확률" 버튼 한 번 뒤로
     * 옮겼다 - 확률형 아이템의 확률은 게임 안에서 늘 찾을 수 있어야 한다.
     *
     * 표의 값은 **그 배너의 지금 소환 레벨** 표(SummonLevelCurve.ChancesAt)다.
     * 다음 뽑기가 실제로 굴리는 표가 그것이라(GachaCurve.Roll · SkillGachaCurve.Roll),
     * Lv.1 표를 보여 주면 레벨이 오른 플레이어에게 틀린 확률을 공개하는 셈이다.
     * 빌더는 칸만 세우고, 숫자와 아랫줄은 열 때마다 여기서 채운다.
     *
     * 판 자체는 PopupBuilder의 뼈대다(딤 · 창 · X, 층위 20).
     */
    public sealed class GachaRatePopup : MonoBehaviour
    {
        [SerializeField] private GameObject yodoTable;
        [SerializeField] private GameObject skillTable;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text footnoteLabel;

        /** 결과(GachaCurve.Outcome) 순서의 확률 칸. 빌더가 잇는다 */
        [SerializeField] private TMP_Text[] yodoChances;
        [SerializeField] private TMP_Text[] skillChances;

        public const string YodoTitle = "요도 뽑기 확률";
        public const string SkillTitle = "오의 뽑기 확률";

        /** 표 아래 한 줄. 이 표가 어느 레벨의 표인지, 레벨이 그것을 바꾼다는 것 */
        public static string FootnoteFor(int level)
        {
            return "소환 Lv." + Mathf.Max(1, level) + " 기준 · 레벨이 오르면 상위 등급 확률이 오른다";
        }

        /** 확률 칸 하나의 글자 */
        public static string PercentText(double chance)
        {
            return (chance * 100d).ToString("0.0") + "%";
        }

        /** 지금 켜진 표가 오의 쪽인가 */
        public bool ShowingSkill { get; private set; }

        /** 지금 표가 말하는 소환 레벨 */
        public int ShownLevel { get; private set; }

        /** 지금 켜진 표의 칸 글자 (검사용) */
        public string ChanceText(int outcome)
        {
            var cells = ShowingSkill ? skillChances : yodoChances;
            if (cells == null || outcome < 0 || outcome >= cells.Length || cells[outcome] == null) return null;
            return cells[outcome].text;
        }

        public string FootnoteText { get { return footnoteLabel != null ? footnoteLabel.text : null; } }

        public void Open(bool skill, int summonLevel)
        {
            ShowingSkill = skill;
            ShownLevel = Mathf.Max(1, summonLevel);

            if (yodoTable != null) yodoTable.SetActive(!skill);
            if (skillTable != null) skillTable.SetActive(skill);
            if (titleLabel != null) titleLabel.text = skill ? SkillTitle : YodoTitle;

            var chances = SummonLevelCurve.ChancesAt(ShownLevel);
            var cells = skill ? skillChances : yodoChances;
            if (cells != null)
                for (int i = 0; i < cells.Length && i < chances.Length; i++)
                    if (cells[i] != null) cells[i].text = PercentText(chances[i]);

            if (footnoteLabel != null) footnoteLabel.text = FootnoteFor(ShownLevel);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
    }
}
