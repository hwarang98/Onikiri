using System.Collections.Generic;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 뽑기 결과를 한 판에 펼친다.
     *
     * ## 왜 한 판에 다 보여주는가 - 연출을 한 줄씩 넘기지 않는다
     *
     * 뽑기 연출의 표준은 하나씩 열어 보이는 것인데, 이 게임에서는 그것이
     * 맞지 않는다. 방치형이고 10연이 30초짜리 사건이 되면 **다음에 또 하고
     * 싶지 않아진다** - 26단계가 오의 쿨다운을 20초 안팎에 묶은 것과 같은
     * 기준(흘깃 볼 때 보여야 한다)이 여기에도 있다.
     *
     * 대신 크기로 말한다. 파편 등급 셋과 혼 정수가 색과 글자로 갈리고,
     * 잭팟과 정수만 금색이다 - 열 줄 중 금색이 몇 개인가가 이 판이 전하는
     * 유일한 정보이고, 그것은 한눈에 읽힌다.
     *
     * ## 판이 스스로 닫히지 않는다
     *
     * 자동으로 사라지면 10연의 결과가 **뭐였는지 모르는 채로** 지나간다.
     * 확인 버튼 하나를 두는 이유이고, 오프라인 보상 팝업(OfflineRewardPopup)이
     * 같은 규칙을 쓴다.
     */
    public sealed class GachaResultPopup : MonoBehaviour
    {
        [Tooltip("켜고 끄는 판. 이 컴포넌트는 항상 켜진 뿌리에 산다")]
        [SerializeField] private GameObject visual;

        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text[] lines;
        [SerializeField] private Button confirmButton;

        [SerializeField] private Color textColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color goldColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        /**
         * @brief 등급 다섯 칸의 색 (47단계). 빌더가 UiSkin.Grades에서 옮겨 적는다.
         *
         * 스크립트 기본값이 아니라 빌더가 쓰는 이유는 이 프로젝트의 규칙이다 -
         * 컴포넌트가 이미 씬에 있으면 스크립트 기본값을 고쳐도 반영되지 않는다.
         * 배열이 비어 있으면 46단계의 색으로 떨어진다(ColorFor).
         */
        [SerializeField] private Color[] gradeColors = new Color[0];

        /**
         * @brief 전설이 나왔을 때 판 전체가 물드는 색.
         *
         * ## 왜 연출이 이것 하나뿐인가
         *
         * 46단계가 정한 규칙("한 판에 다 보여준다 - 10연이 30초짜리 사건이
         * 되면 다음에 또 하고 싶지 않아진다")이 여기서도 그대로다. 전설
         * 하나 때문에 판을 한 줄씩 여는 연출로 바꾸면, 200회 중 199회는
         * 그 연출을 **전설 없이** 지나야 한다.
         *
         * 그래서 사건성은 **판의 색**에 싣는다. 여섯 줄 중 하나가 금색인
         * 것과 판 전체가 금테를 두른 것은 흘깃 볼 때 다른 화면이고, 그
         * 차이가 200회에 한 번만 나타난다.
         */
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color cardNormalTint = new Color(0.34f, 0.36f, 0.68f, 1f);
        [SerializeField] private Color cardLegendaryTint = new Color(0.62f, 0.50f, 0.22f, 1f);

        private void Start()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Close);
            Close();
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Close);
        }

        public void Close()
        {
            if (visual != null) visual.SetActive(false);
        }

        /**
         * @brief 결과 목록을 그린다. 목록은 **보관하지 않는다.**
         *
         * GachaSystem이 돌려 쓰는 목록이라 다음 뽑기가 덮어쓴다. 그 자리에서
         * 문자열로 옮기고 끝낸다.
         */
        public void Show(List<GachaSystem.PullResult> results, YodoSystem yodo)
        {
            if (visual == null || lines == null || results == null) return;

            int shards = 0;
            var best = GachaCurve.Grade.Common;
            var counts = new int[GachaCurve.GradeCount];

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;

                if (i >= results.Count)
                {
                    lines[i].text = string.Empty;
                    continue;
                }

                var result = results[i];
                shards += result.Shards;

                var grade = GachaCurve.GradeFor(result.Outcome);
                counts[(int)grade]++;
                if (grade > best) best = grade;

                lines[i].text = TextFor(result, yodo);
                lines[i].color = ColorFor(result);
            }

            if (titleLabel != null)
            {
                titleLabel.text = Headline(shards, counts);
                titleLabel.color = ColorOf(best);
            }

            // 판이 물드는 것은 전설에서만이다. ★4까지는 줄 하나의 색으로
            // 충분하고, 판까지 바뀌면 200회에 한 번의 사건이 20회에 한 번이
            // 된다 - 사건은 드물어야 사건이다
            if (cardBackground != null)
                cardBackground.color = best == GachaCurve.Grade.Legendary
                    ? cardLegendaryTint : cardNormalTint;

            visual.SetActive(true);
            visual.transform.SetAsLastSibling();
        }

        /**
         * @brief 머리글. **합계이고, 위쪽 등급만 센다.**
         *
         * 46단계는 "파편 54 · 혼 정수 1"이었다. 사다리가 다섯이 되면서
         * 등급을 다 세면 머리글이 다섯 조각이 되는데, 그러면 열 줄을 안
         * 세게 하려고 만든 줄이 그 자체로 세야 하는 줄이 된다.
         *
         * 그래서 **파편 합계 + ★3 이상만** 적는다. ★1·★2는 전부 파편이라
         * 이미 합계에 들어 있고, 위쪽 셋은 개수가 곧 사건이다.
         */
        private static string Headline(int shards, int[] counts)
        {
            string text = "파편 " + shards;

            for (int g = (int)GachaCurve.Grade.Rare; g < counts.Length; g++)
            {
                if (counts[g] <= 0) continue;
                text += " · " + GachaCurve.GradeNames[g] + " " + counts[g];
            }
            return text;
        }

        /**
         * @brief 한 줄의 문구.
         *
         * 혼 정수가 갈 곳이 있었는지를 반드시 적는다. 상한(리드)에 닿아
         * 파편이 된 경우에 "혼 정수"라고만 적으면 플레이어는 티어가 오를
         * 것을 기대하고 대장간에 갔다가 아무것도 못 찾는다 - 44단계가
         * "버려지는 드랍 0"을 값으로 지켰다면 여기서는 **문구로** 지킨다.
         */
        private static string TextFor(GachaSystem.PullResult result, YodoSystem yodo)
        {
            switch (result.Outcome)
            {
                case GachaCurve.Outcome.SoulEssence:
                    return EssenceText(result, yodo, string.Empty);

                case GachaCurve.Outcome.SoulRarity:
                    return RarityText(result, yodo);

                case GachaCurve.Outcome.LegendaryBlade:
                    return LegendaryText(result, yodo);

                default:
                    return "파편 " + result.Shards;
            }
        }

        private static string EssenceText(GachaSystem.PullResult result, YodoSystem yodo,
                                          string prefix)
        {
            if (result.BladeIndex < 0)
                return prefix + "혼 정수 → 파편 " + result.Shards;

            var blade = yodo != null ? yodo.GetBlade(result.BladeIndex) : null;
            string name = blade != null ? blade.soulName : "혼";
            return prefix + "혼 정수 → " + name;
        }

        /**
         * @brief ★4 한 줄. **미끄러진 것을 반드시 적는다.**
         *
         * 혼격이 꽉 차면 ★4는 ★3으로 내려간다(GachaSystem.GrantRarity).
         * 그때 "상위 혼"이라고만 적으면 플레이어는 대장간에 가서 혼격이 안
         * 오른 것을 보고, "혼 정수"라고만 적으면 등급이 내려간 것을 모른다.
         * 두 단어를 다 적어 **무엇이 무엇이 됐는지**를 그 자리에서 말한다 -
         * 46단계의 "혼 정수 → 파편 40"과 같은 문법이다.
         */
        private static string RarityText(GachaSystem.PullResult result, YodoSystem yodo)
        {
            string prefix = result.FromPity ? "천장! " : string.Empty;

            if (result.Downgraded)
                return EssenceText(result, yodo, prefix + "상위 혼 → ");

            var blade = yodo != null ? yodo.GetBlade(result.BladeIndex) : null;
            if (blade == null) return prefix + "상위 혼";

            // 혼격은 올라간 **뒤**의 값이다. 화면이 도감과 같은 숫자를 적어야
            // "뽑았는데 안 올랐다"가 안 나온다
            return prefix + blade.bladeName + " " + YodoRarityCurve.Stars(blade.rarity);
        }

        /**
         * @brief ★5 한 줄. 새 칼인가 돌파인가를 가른다.
         *
         * 두 사건의 크기가 다르다 - 새 칼은 도감에 줄이 하나 켜지는 일이고
         * 돌파는 있던 줄이 깊어지는 일이다. 같은 문구로 적으면 200회에 한
         * 번의 결과가 어느 쪽이었는지 판에서 읽히지 않는다.
         */
        private static string LegendaryText(GachaSystem.PullResult result, YodoSystem yodo)
        {
            if (result.LegendaryIndex < 0)
                return "전설 → 파편 " + result.Shards;

            var blade = yodo != null ? yodo.GetLegendary(result.LegendaryIndex) : null;
            if (blade == null) return "전설 요도";

            return blade.copies <= 1
                ? blade.bladeName + " 획득!"
                : blade.bladeName + " 돌파 " + blade.Breakthrough;
        }

        private Color ColorFor(GachaSystem.PullResult result)
        {
            return ColorOf(GachaCurve.GradeFor(result.Outcome));
        }

        /**
         * @brief 등급의 색. 배열이 안 배선된 씬에서는 46단계의 색으로 떨어진다.
         *
         * 폴백을 남기는 이유는 41단계 이후의 규칙이다 - 배선이 빠진 씬에서
         * 화면이 아예 안 뜨는 것보다 조용히 수수한 편이 낫다.
         */
        private Color ColorOf(GachaCurve.Grade grade)
        {
            int index = (int)grade;
            if (gradeColors != null && index >= 0 && index < gradeColors.Length)
                return gradeColors[index];

            return grade >= GachaCurve.Grade.Rare ? goldColor
                 : grade == GachaCurve.Grade.Uncommon ? textColor
                 : dimColor;
        }
    }
}
