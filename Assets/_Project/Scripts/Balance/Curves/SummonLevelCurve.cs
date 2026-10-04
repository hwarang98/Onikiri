using System;

namespace Onikiri.Progression
{
    /**
     * @brief 소환 레벨 (68단계). **천장이 보장이었다면 이것은 성장이다.**
     *
     * ## 무엇을 대신하는가
     *
     * 47·50단계의 천장(요도 30회 ★4+ / 오의 30회 ★4 · 100회 ★5)을 통째로
     * 걷어내고 그 자리에 둔다. 천장은 "N회 안에 반드시"를 약속했지만 그
     * 약속은 매번 0으로 돌아갔다 - 뽑기가 쌓아 주는 것이 없었다. 소환 레벨은
     * 반대다. 뽑은 횟수가 경험치로 남고, 레벨이 오를수록 위쪽 등급의
     * 확률이 올라가며, **그 성장은 되돌아가지 않는다.**
     *
     * 배너별로 따로다(요도 / 오의). 두 배너는 파는 것이 다르고 지갑도
     * 나눠 쓰므로(PromotionEconomyLedger) 한쪽 뽑기가 다른 쪽을 키우면
     * "어느 배너를 돌려야 하는가"가 사라진다.
     *
     * ## 경험치 = 뽑기 횟수
     *
     * 1회 = 1 XP다. 무료 뽑기·온보딩 10연도 준다 - **무과금이 이 축에 닿는
     * 길이 바로 그 둘**이고(GachaCurve.FreePullsPerDay), 그들을 빼면 이 축은
     * 과금 전용이 된다.
     *
     * ## 상한이 없다 - 65단계 수치 대결과 같은 원칙
     *
     * 요구 XP는 지수로 자라는 비용 게이트이고(XpToNext) 최대 레벨이 없다.
     * 확률은 등급별 가중치를 키운 뒤 **합으로 나눈다**(ChancesAt). 그러면
     * 확률은 언제나 (0, 1) 안이고, 레벨이 끝없이 올라도 ★5의 비중이 1에
     * 다가갈 뿐 어디에서도 잘리지 않는다 - 하드캡·clamp가 필요 없는
     * 모양이다. 같은 구조가 RatingContest.Chance(a / (a + b))에 있다.
     */
    public static class SummonLevelCurve
    {
        // ---------------------------------------------------------------- 요구 XP

        /**
         * @brief Lv.1 -> 2에 드는 XP = 뽑기 수. **10연 둘에 레벨 하나.**
         *
         * 초반 간격이 10연 2~3회(20~30회)라는 것이 지시서의 (c)다. 20이면
         * 처음 세 레벨이 20 / 23 / 27회라 상점에 두세 번 들르는 사이에
         * 첫 레벨이 오른다 - 성장이 있다는 것을 첫 주에 보여 주는 크기다.
         */
        public const double XpBase = 20d;

        /**
         * @brief 레벨당 요구 XP 증가율. **후반은 점점 멀어진다.**
         *
         * 1.15면 Lv.11까지 411회, Lv.15까지 818회, Lv.21까지 2060회다.
         * 확률 가중치(GradeGrowth)가 레벨의 지수이므로 요구 XP도 지수여야
         * 뽑기 수에 대한 확률의 성장이 완만해진다 - 선형이면 레벨이 뽑기
         * 수의 제곱근으로만 느려져 ★5가 st500 전에 흔해진다(68 보고서 §2).
         */
        public const double XpGrowth = 1.15d;

        /**
         * @brief `level`에서 다음 레벨로 가는 데 드는 XP. 정수 올림, 최소 1.
         *
         * 화면이 "xp / next"를 정수로 적으므로 정수다. 상한은 없다 - 다만
         * long이 담을 수 없는 크기(레벨 약 290 위)는 long.MaxValue로
         * 적는다. 그 자리는 게임의 상한이 아니라 **숫자 표현의 끝**이고,
         * 그 레벨에 닿으려면 1e18회를 뽑아야 한다.
         */
        public static long XpToNext(int level)
        {
            if (level < 1) level = 1;

            double raw = Math.Ceiling(XpBase * Math.Pow(XpGrowth, level - 1) - 1e-9d);
            if (raw >= long.MaxValue) return long.MaxValue;
            return raw < 1d ? 1L : (long)raw;
        }

        /** 누적 XP가 닿은 레벨. 0이면 Lv.1 */
        public static int LevelFor(long totalXp)
        {
            int level = 1;
            long left = totalXp < 0L ? 0L : totalXp;

            for (;;)
            {
                long need = XpToNext(level);
                if (left < need) return level;
                left -= need;
                level++;
            }
        }

        /** 지금 레벨 안에서 채운 XP (게이지의 분자) */
        public static long XpIntoLevel(long totalXp)
        {
            int level = 1;
            long left = totalXp < 0L ? 0L : totalXp;

            for (;;)
            {
                long need = XpToNext(level);
                if (left < need) return left;
                left -= need;
                level++;
            }
        }

        // ---------------------------------------------------------------- 확률

        /**
         * @brief 등급별 레벨당 가중치 배율 g. **★1 = 1이 기준이다.**
         *
         *     w_i(L) = Chances_i x g_grade(i)^(L-1),  확률 = w_i / Σw
         *
         *   ★1·★2 1     파편. 표 그대로의 기준
         *   ★3 1.35     혼 정수 / 스킬 XP 240
         *   ★4 1.35     상위 혼 / 오의 해금. **무과금의 자**: 반씩 나눈 무과금이
         *               28일차(Lv.3)에 옛 천장의 ★4+ 실효(1/20.2회)에 닿는다
         *   ★5 1.36     전설 / 귀오의. **가장 커야 한다** - 그래야 레벨이 오를수록
         *               ★5가 줄지 않고 1로 수렴한다(단조). st500 추종 Lv.7에서 Lv.1의 x4.9
         *
         * ## ★3이 ★4와 같은 이유 - 추종의 뽑기는 재고가 멈춘다
         *
         * 시뮬레이션의 추종 플레이어는 혼 정수·혼격 재고가 닫힐 때까지 뽑는다
         * (StageSimulation.TryGacha). ★3·★4가 같은 비율로 크면 재고가 같은
         * 비율로 일찍 닫혀 뽑기 수가 줄고, 그 사이에 얹혀 오는 전설(★5) 기대가
         * 레벨이 없는 세계와 같아진다 - **이 축이 전설을 더 주지 않는다.**
         *
         * 처음 값(★3 1.03 / ★4 1.35 / ★5 1.25)은 둘 다 어겼다. ★4 > ★5라 고레벨에서
         * ★5가 줄었고(Lv.50 거의 0), ★3을 안 키워 뽑기 수가 그대로 남아 전설이
         * 더 쌓였다(68단계 보고서 §2).
         *
         * 요도와 오의는 같은 확률표를 쓰므로 같은 g를 쓴다 - 오의의 결과는
         * 같은 칸에 매핑된다(SkillGachaCurve.Outcome).
         */
        public static readonly double[] GradeGrowth = { 1d, 1d, 1.35d, 1.35d, 1.36d };

        /**
         * @brief 레벨 L의 결과별 확률. 합이 정확히 1이 되도록 나눈다.
         *
         * 지수를 로그로 더한 뒤 가장 큰 항을 빼고 되돌린다 - 레벨이 아주
         * 높아도 Math.Pow가 무한대로 넘치지 않게 하는 방법이고, 나눗셈이
         * 그 공통 인수를 지우므로 값은 같다. Lv.1이면 Chances 그대로다.
         */
        public static double[] ChancesAt(int level)
        {
            var table = GachaCurve.Chances;
            var weights = new double[table.Length];
            int steps = level < 1 ? 0 : level - 1;

            double top = double.NegativeInfinity;
            for (int i = 0; i < table.Length; i++)
            {
                double log = Math.Log(table[i])
                           + steps * Math.Log(GradeGrowth[(int)GachaCurve.GradeOf[i]]);
                weights[i] = log;
                if (log > top) top = log;
            }

            double sum = 0d;
            for (int i = 0; i < table.Length; i++)
            {
                weights[i] = Math.Exp(weights[i] - top);
                sum += weights[i];
            }

            for (int i = 0; i < table.Length; i++) weights[i] /= sum;
            return weights;
        }

        /** 레벨 L에서 결과 하나의 확률 */
        public static double ChanceAt(int level, int outcome)
        {
            var chances = ChancesAt(level);
            return outcome >= 0 && outcome < chances.Length ? chances[outcome] : 0d;
        }

        // ---------------------------------------------------------------- 문구

        /**
         * @brief 배너 아래 진행 줄. **빌더와 런타임이 같은 함수를 지난다.**
         *
         * 옛 천장 줄(GachaCurve.PityText)이 서던 자리이고 같은 규칙이다 -
         * 한쪽만 고치면 씬을 연 순간과 첫 갱신 사이에 문장이 바뀐다.
         * 게이지 바와 레벨업 연출은 69단계(UI 개편) 몫이다.
         */
        public static string LevelText(int level, long xp, long next)
        {
            return "소환 Lv." + level + "  ·  " + xp + " / " + next;
        }

        /** 누적 XP 하나로 진행 줄을 만든다 */
        public static string LevelTextFor(long totalXp)
        {
            int level = LevelFor(totalXp);
            return LevelText(level, XpIntoLevel(totalXp), XpToNext(level));
        }

        /** 결과 팝업 제목 줄에 붙는 레벨업 한 줄 */
        public static string LevelUpText(int level)
        {
            return "소환 Lv." + level + " 달성";
        }
    }
}
