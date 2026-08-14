using System;

namespace Onikiri.Progression
{
    /**
     * @brief 오의가 **무엇을 하는가**를 한 문장으로 (#14).
     *
     * ## 왜 문장이 필요한가
     *
     * 화면에 있던 것은 배율과 쿨다운뿐이었다("x2.88 · 16.0초"). 그 둘은
     * 얼마나 센가를 말하지만 **어떻게 때리는가**는 말하지 않는다 - 한 대상을
     * 세 번 때리는 오의와 화면 전체를 한 번 쓸어버리는 오의가 같은 두 숫자로
     * 표시됐고, 잡몹 화면에서 그 차이가 곧 체감 전부다.
     *
     * ## 왜 문자열을 카탈로그에 안 적는가
     *
     * 거동은 이미 SkillCatalog에 **값으로** 있다(Shape, HitCount). 설명을
     * 손으로 적어 나란히 두면 둘이 갈라진다 - 타격 수를 3에서 5로 바꾼 날
     * 문장은 "3회"로 남고, 그 거짓말은 아무 테스트도 잡지 못한다.
     *
     * 그래서 값에서 문장을 만든다. 카탈로그가 바뀌면 문장이 따라온다.
     *
     * ## 배율은 퍼센트로 적는다
     *
     * 목록의 "x2.88"과 다른 표기인데, 여기서는 그것이 맞다. 팝업의 문장은
     * "공격력의 288%"라고 읽혀야 하고("슬레이어 키우기"의 화법이다),
     * 배수 표기는 강화 탭의 증가폭 줄이 계속 쓴다 - 같은 값을 두 자리에서
     * 다른 방식으로 보여주는 것이 아니라, 문장과 표는 원래 다른 화법이다.
     */
    public static class SkillDescription
    {
        /**
         * @param id         오의 id (SkillCatalog와 같은 문자열)
         * @param multiplier 지금 레벨의 총 배율
         * @param cooldown   시전 간격 (초)
         */
        public static string Of(string id, double multiplier, double cooldown)
        {
            int index = SkillCatalog.IndexOf(id);
            if (index < 0) return string.Empty;

            var spec = SkillCatalog.Skills[index];
            string percent = Percent(multiplier);

            string body;
            switch (spec.Shape)
            {
                case SkillShape.MultiHit:
                {
                    int hits = Math.Max(1, spec.HitCount);
                    // 한 대상을 여러 번 때린다. **총 배율을 나눠 갖는다**는
                    // 사실까지는 안 적는다 - 플레이어가 알아야 하는 것은
                    // 그 오의가 한 번에 넣는 총량이고, 나눗셈은 연출의 문제다
                    body = hits > 1
                        ? "가장 가까운 적을 공격력의 " + percent + "로 " + hits + "회 나눠 공격"
                        : "가장 가까운 적을 공격력의 " + percent + "로 공격";
                    break;
                }

                case SkillShape.Pierce:
                    // 경로의 모든 대상이 **각자** 총 배율을 받는다. 잡몹이
                    // 줄지어 선 화면에서 이것이 곧 위력이라 반드시 적는다
                    body = "전방 일렬의 적을 관통해 각각 공격력의 " + percent + "로 공격";
                    break;

                default:
                    body = "화면의 모든 적을 각각 공격력의 " + percent + "로 공격";
                    break;
            }

            return body + "\n" + cooldown.ToString("F1") + "초마다 자동 시전";
        }

        /**
         * @brief 배율 -> 퍼센트 문자열.
         *
         * 후반 배율은 네 자리를 넘는다(x70.4 = 7040%). 소수는 안 적는다 -
         * 이 문장이 답하는 질문은 "얼마나 센가"이지 "정확히 얼마인가"가
         * 아니고, 정확한 값은 바로 아래 강화 탭이 x 표기로 보여준다.
         */
        private static string Percent(double multiplier)
        {
            double percent = multiplier * 100d;

            // 만 단위를 넘으면 축약한다. "704000%"는 자릿수를 세게 만든다
            if (percent >= 100000d)
                return Onikiri.Core.NumberFormatter.Format(
                    Onikiri.Core.BigDouble.FromDouble(percent)) + "%";

            return Math.Round(percent).ToString("N0") + "%";
        }
    }
}
