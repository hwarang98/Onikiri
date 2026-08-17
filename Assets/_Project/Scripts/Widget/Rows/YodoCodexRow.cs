using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 요도 도감의 한 줄. 누를 것이 없고, 무엇을 갖고 있는지만 말한다.
     *
     * ## 세 상태다 - 미발견 / 발견 / 봉인
     *
     *   미발견  실루엣(아이콘을 판 색으로 눌러 형태만 남긴다) + "○○ 처치 시 해금"
     *   발견    혼은 받았지만 아직 안 벼렸다. 이름과 아이콘이 켜지고 "봉인 대기"
     *   봉인    요도 이름 + 티어 + 배수
     *
     * 가운데 상태가 있는 것이 이 화면의 요점이다. 41단계의 잠긴 미리보기는
     * "잠김/열림" 둘뿐이었는데, 여기는 **혼을 받은 순간과 칼이 된 순간이
     * 다른 사건**이라 그 사이가 화면에 있어야 한다 - 그 한 줄이 요도 탭으로
     * 가라는 뜻이다.
     *
     * ## 실루엣은 알파가 아니라 RGB로 만든다
     *
     * 41단계에 확정한 규칙이다(Color에 스칼라를 곱하면 알파도 눌려 뒤가
     * 비친다). 아이콘을 판 색 근처까지 눌러 형태만 남긴다 - 뒤가 비치는
     * 반투명은 "잠김"이 아니라 "덜 그려짐"으로 읽힌다.
     */
    public sealed class YodoCodexRow : MonoBehaviour
    {
        [SerializeField] private YodoSystem system;

        [Tooltip("카탈로그 인덱스. -1이면 오니키리 완성 줄")]
        [SerializeField] private int bladeIndex;

        /**
         * @brief 전설 妖刀 인덱스 (47단계). -1이면 이 줄은 전설이 아니다.
         *
         * bladeIndex에 음수를 더 파서 겸용하지 않는 이유는 -1이 이미
         * "오니키리 줄"이라는 뜻을 갖고 있기 때문이다. 한 필드에 세 가지
         * 뜻을 담으면 읽는 쪽이 매번 표를 봐야 하고, 그 표는 주석에만 산다.
         */
        [SerializeField] private int legendaryIndex = -1;

        [Header("표시")]
        [SerializeField] private Image icon;
        [SerializeField] private Image rowBackground;

        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private TMP_Text valueLabel;

        [Header("색")]
        [SerializeField] private Color textColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color goldColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        [SerializeField] private Color lockedColor = new Color32(0x5A, 0x51, 0x6B, 0xFF);

        [SerializeField] private Color normalRowTint = Color.white;
        [SerializeField] private Color lockedRowTint = new Color(0.34f, 0.36f, 0.55f, 1f);
        [SerializeField] private Color completeRowTint = new Color(0.62f, 0.55f, 0.42f, 1f);

        [Tooltip("발견 뒤의 아이콘 틴트. 카탈로그의 IconTint를 빌더가 적어준다")]
        [SerializeField] private Color iconTint = Color.white;

        [Tooltip("미발견 실루엣. 판 색 근처로 눌러 형태만 남긴다")]
        [SerializeField] private Color silhouetteTint = new Color(0.24f, 0.22f, 0.34f, 1f);

        private void Start()
        {
            if (system == null) system = YodoSystem.Instance;
            if (system != null) system.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (system == null) system = YodoSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (system != null) system.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (system == null) return;

            if (legendaryIndex >= 0) { DrawLegendary(); return; }
            if (bladeIndex < 0) { DrawOnikiri(); return; }

            var blade = system.GetBlade(bladeIndex);
            if (blade == null) return;

            if (!blade.discovered) { DrawUndiscovered(blade); return; }

            if (icon != null) icon.color = iconTint;
            if (rowBackground != null) rowBackground.color = normalRowTint;

            if (blade.Sealed)
            {
                // 이름 옆에 혼격을 별로 붙인다(47단계). 색까지 등급으로
                // 바꾸지는 않는다 - 도감의 이름 열은 "무엇을 갖고 있는가"의
                // 열이고, 그 열이 자루마다 다른 색이면 목록이 안 훑어진다.
                // 등급 색은 뽑는 순간(GachaResultPopup)의 것이다
                Set(nameLabel, blade.bladeName + YodoRow.RaritySuffix(blade.rarity), textColor);

                // **도감의 둘째 줄이 티어에서 상성으로 바뀌었다.** 티어는
                // 요도 탭이 이미 크게 적고, 도감이 답해야 하는 질문은
                // "이 자루가 무엇을 하는가"다 - 45단계에 그 답이 생겼다.
                // 티어는 값 줄 옆으로 접어 넣는다
                Set(stateLabel, YodoRow.AffinitySkillName(bladeIndex) + " 강화 ×"
                    + YodoAffinityCurve.ValueAt(bladeIndex, blade.tier, blade.rarity).ToString("F3")
                    + " · 영체 ×"
                    + YodoSpiritCurve.MultiplierAtTier(blade.tier, blade.rarity).ToString("F1"),
                    dimColor);

                Set(valueLabel, "봉인 " + blade.tier + " · ×"
                    + blade.Multiplier.ToString("F3"), dimColor);
                return;
            }

            // 혼은 있는데 칼이 없다. 요도 탭으로 가라는 뜻이고, 그것을
            // 문장으로 적는다 - 도감에는 버튼이 없으므로 다음 행동은 글자로만
            // 전달된다
            Set(nameLabel, blade.soulName, textColor);

            // 무엇이 열리는지를 적는다. 발견 상태의 이 줄이 "요도 탭으로
            // 가라"는 뜻인데, 45단계부터는 **왜 가야 하는지**까지 말할 수
            // 있다 - 봉인의 대가가 배수 하나가 아니라 오의 강화와 영체다
            Set(stateLabel, "봉인 대기 · " + YodoRow.AffinitySkillName(bladeIndex)
                + " 강화 + 영체 해금", dimColor);
            Set(valueLabel, "요도 탭에서 봉인", dimColor);
        }

        private void DrawUndiscovered(YodoSystem.Blade blade)
        {
            if (icon != null) icon.color = silhouetteTint;
            if (rowBackground != null) rowBackground.color = lockedRowTint;

            // **이름을 감춘다.** 발견 전에 "등롱도"라고 적으면 이미 아는
            // 물건이 되고, 도감의 그 줄이 갖고 싶은 것이 아니라 목록의 한
            // 칸이 된다. 대신 어디서 나오는지는 적는다 - 갖고 싶으려면
            // 어디로 가야 하는지는 알아야 한다
            Set(nameLabel, "???", lockedColor);
            Set(stateLabel, blade.bossName + " 처치 시 해금", lockedColor);
            Set(valueLabel, YodoCurve.UnlockStage + "스테이지 이후", lockedColor);
        }

        /**
         * @brief 마지막 줄. 넷을 다 봉인해야 켜진다.
         *
         * 이 줄이 도감에 있는 이유는 세트 보너스가 **줄 넷의 합이 아니라
         * 별개의 물건**이기 때문이다(YodoCurve.SetBonusAt의 넷째 칸만 큰
         * 이유). 목록 아래에 요약으로 적으면 "합계"로 읽히고, 한 줄을
         * 내주면 도감이 향하는 곳이 된다.
         */
        private void DrawOnikiri()
        {
            int sealedCount = system.SealedCount;
            bool complete = system.IsOnikiriComplete;

            if (icon != null) icon.color = complete ? goldColor : silhouetteTint;
            if (rowBackground != null)
                rowBackground.color = complete ? completeRowTint : lockedRowTint;

            Set(nameLabel, complete ? YodoCatalog.OnikiriName : "???",
                complete ? goldColor : lockedColor);

            Set(stateLabel,
                complete ? "네 대요괴의 혼을 모두 봉인했다"
                         : "요도 " + sealedCount + " / " + YodoCatalog.Count + " 봉인",
                complete ? textColor : lockedColor);

            Set(valueLabel, "세트 ×" + system.SetBonus.ToString("F2"),
                complete ? goldColor : lockedColor);
        }

        /**
         * @brief 전설 妖刀 한 줄 (47단계). **오니키리 줄 아래에 선다.**
         *
         * ## 왜 도감의 아래쪽인가 - 세로축과 가로축이 갈리는 자리
         *
         * 위의 다섯 줄(요도 넷 + 오니키리)은 **완성의 세로축**이고 끝이
         * 있다. 전설은 **수집의 가로축**이고 끝이 없다
         * (LegendaryYodoSpec 머리 주석). 오니키리 줄이 그 사이의 선이라,
         * 목록을 위에서 아래로 훑으면 "여기까지가 완성, 여기부터가 수집"이
         * 배치로 읽힌다.
         *
         * 세트 보너스에 안 들어가는 것도 같은 사실의 다른 얼굴이다 - 전설을
         * 둘 다 모아도 오니키리 줄은 안 켜진다.
         *
         * ## 미보유는 이름을 감추지 않는다 - 보스 요도와 반대다
         *
         * 보스 요도는 미발견에 "???"를 적는다(DrawUndiscovered - 발견 전에
         * 이름을 적으면 이미 아는 물건이 된다). 전설은 반대로 **이름을
         * 적는다.** 조건이 다르기 때문이다: 보스 요도의 조건은 "저 요괴를
         * 베라"라서 이름 대신 요괴를 가리키면 되지만, 전설의 조건은 확률이라
         * 가리킬 대상이 뽑기밖에 없다. 무엇을 뽑으려 하는지를 감추면 뽑을
         * 이유가 사라진다 - 확률표를 공개하는 것과 같은 판단이다.
         */
        private void DrawLegendary()
        {
            var blade = system.GetLegendary(legendaryIndex);
            if (blade == null) return;

            var spec = LegendaryYodoCatalog.Blades[legendaryIndex];

            if (icon != null) icon.color = blade.Owned ? iconTint : silhouetteTint;
            if (rowBackground != null)
                rowBackground.color = blade.Owned ? completeRowTint : lockedRowTint;

            Set(nameLabel, blade.bladeName, blade.Owned ? goldColor : lockedColor);

            if (!blade.Owned)
            {
                Set(stateLabel, spec.Flavor, lockedColor);
                Set(valueLabel, "뽑기 " + GachaCurve.GradeNames[(int)GachaCurve.Grade.Legendary],
                    lockedColor);
                return;
            }

            Set(stateLabel, LegendaryYodoCurve.SkillName(legendaryIndex) + " 강화 ×"
                + LegendaryYodoCurve.AffinityAt(legendaryIndex, blade.copies).ToString("F3")
                + " · 영체 ×" + LegendaryYodoCurve.SpiritAt(blade.copies).ToString("F2"),
                dimColor);

            Set(valueLabel, "돌파 " + blade.Breakthrough + " / "
                + (LegendaryYodoCurve.MaxCopies - 1) + " · ×"
                + blade.Multiplier.ToString("F3"), dimColor);
        }

        private static void Set(TMP_Text label, string text, Color color)
        {
            if (label == null) return;
            label.text = text;
            label.color = color;
        }
    }
}
