using System;

namespace Onikiri.Progression
{
    /**
     * @brief 초당 체력 회복 곡선. **최대 체력에 비례한다.**
     *
     * 값은 절대 회복량이 아니라 "초당 최대 체력의 몇 %"다.
     *
     * ## 왜 비례로 바꿨는가
     *
     * 처음에는 절대량이었다(1.0/s에서 x1.115). 형태는 건강했고 EHP 밴드도
     * 통과했지만, **20스테이지까지 한 번도 팔리지 않았다.** 체력이 100에서
     * 시작해 곱연산으로 자라는데 회복은 1에서 시작하니, 유효체력 기여가
     *
     *     체력  100 x 1.10^n
     *     회복    1 x 1.115^n x 30초 = 30 x 1.115^n
     *
     * 로 갈려서 골드당 이득이 체력 쪽으로 계속 기울었다. 두 곡선이 만나는
     * 지점은 Lv.100 근처인데 20스테이지에서 체력은 Lv.28이다. 그 버튼은
     * 곡선상으로는 살아 있고 화면에서는 죽어 있었다 - 8단계 공격속도와
     * 같은 결말이고, 원인만 다르다.
     *
     * 비례로 두면 그 경주가 사라진다. 유효체력이
     *
     *     EHP = 최대체력 x (1 + 비율 x 전투시간)
     *
     * 이 되어 체력의 기여율은 레벨과 무관하게 일정해지고(= step - 1),
     * 회복의 기여율은 비율이 자랄수록 그쪽으로 수렴한다. 두 축이 서로를
     * 밀어내지 않고 곱해진다 - 체력을 사면 회복의 절대량도 함께 오른다.
     *
     * ## 그래서 두 축이 어떻게 다른가
     *
     * 체력은 **버티는 총량**, 회복은 **버티는 시간당 효율**이다. 전투가
     * 길수록 회복이 유리하다는 원래 성질은 그대로 남는다. 달라진 것은
     * 회복이 더 이상 체력과 자릿수 경주를 하지 않는다는 점이다.
     */
    public static class HealthRegenCurve
    {
        /**
         * @brief 레벨 1의 초당 회복 비율.
         *
         * 최대 체력의 1%/초. 기본 체력 100에서 1.0/s이므로 예전 절대량과
         * 시작점이 같다 - 세이브를 건드리지 않아도 체감이 이어진다.
         */
        public const double BaseValue = 0.01d;

        /** 체력과 같은 step. 두 축이 같은 속도로 자라야 비율이 안정된다 */
        /**
         * 43단계에 여덟 배 미세화됐다(값 등가 재스케일). 옛 한 레벨이 새
         * 여덟 칸이다: step^8 = 옛 step, growth^8 = 1.15, 기준 비용은 누적
         * 골드가 같아지는 연속체 등가값((g-1)/0.15 배). 골드와 파워의 관계가
         * 보존되므로 밸런스는 그대로이고, 레벨 숫자만 슬레이어처럼 깊어진다 -
         * "곧 끝난다" 느낌이 없는 수천 레벨이 이 스텝의 목적이다.
                  * 기준 비용에는 이산 보정 x1.3이 얹혀 있다(연속체 등가값의 1.3배).
         * 등가 수식(누적 골드 동일)은 완벽한데도 미세 칸은 지갑을 끝전까지
         * 즉시 소진해 복리를 앞당긴다 - 실측으로 코리더 여유가 +27%까지
         * 떠서 10곳이 밴드를 깼고, 이 보정이 앵커를 +-3%로 되돌린다.
         */
        public const double Step = 1.0119850d;

        /**
         * @brief 비용.
         *
         * 체력(9)보다 싸다. 초반 유효체력 기여가 체력보다 작기 때문이다 -
         * Lv.1에서 비율 1%/초 x 30초 = 0.3이라 EHP의 23%만 담당한다.
         * 정수 조합을 훑어 밴드와 생존성을 동시에 만족하는 값으로 잡았다.
         */
        /** E-3 수정: 전 축 일괄 상향 + 정수화. 규칙과 배율은 UpgradeCost가 단일 출처다 */
        public const double BaseCost = 0.61091d * UpgradeCost.RaiseScale;
        public const double CostGrowth = 1.0176225d;

        /**
         * @brief 초당 회복 비율의 상한.
         *
         * ## 왜 상한이 필요한가
         *
         * 비율 곱연산에는 천장이 없다. 15단계 실측에서 Lv.54의 회복이
         * **156.2%/s**였다 - 초당 최대 체력의 1.5배를 회복하면 어떤 피해도 다음
         * 프레임에 지워지고, 11단계에서 만든 체력 게이트가 통째로 무력화된다.
         *
         * 비율로 바꾼 것 자체는 옳았다(위 주석 참고). 놓친 것은 **100%를 넘을 수
         * 있다는 사실**이다. 절대량이던 시절에는 체력이 함께 자라서 저절로
         * 억제됐는데, 비율이 되면서 그 억제가 사라졌다.
         *
         * ## 값의 근거
         *
         * 30%/s면 30초 보스전 동안 최대 체력의 9배를 회복한다. 커 보이지만
         * 유효체력 기준으로는 EHP = 최대체력 x (1 + 0.3 x 30) = 10배이고,
         * 그 정도는 보스 공격력 곡선(스테이지당 x1.12)이 몇 스테이지 만에
         * 따라잡는다. 무적이 아니라 "한 챕터를 벌어주는" 크기다.
         *
         * 이보다 낮으면(20%/s = 7배) 회복 축이 상한에 너무 일찍 닿아 죽고,
         * 높으면(40%/s = 13배) 체력 게이트가 다시 헐거워진다. 시뮬레이션에서
         * 생존 여유가 밴드에 남는 구간으로 잡았다.
         */
        public const double Ceiling = 0.30d;

        /**
         * @brief 상한을 무시한 곡선값.
         *
         * 효율 지표(SurvivalEfficiency)가 쓴다. 그쪽이 재는 것은 곡선의 **형태**이지
         * 지금 낼 수 있는 값이 아니다 - 상한 위에서 이득이 0이 되면 비율이 무한으로
         * 발산해 지표가 무너진다. 9단계 공격속도에서 같은 이유로 갈라놨다.
         */
        public static double ValueAtLevel(int level)
        {
            return BaseValue * Math.Pow(Step, Math.Max(0, level - 1));
        }

        /** 전투가 실제로 쓰는 값. 상한에서 멈춘다 */
        public static double CappedValueAtLevel(int level)
        {
            return Math.Min(Ceiling, ValueAtLevel(level));
        }

        /**
         * @brief 상한에 처음 닿는 레벨.
         *
         * UpgradeTrack의 maxLevel로 쓴다. 이 위로는 팔지 않는다 - 값이 안 오르는
         * 버튼에 골드를 받으면 그건 판매가 아니라 함정이다.
         */
        public static int MaxLevel
        {
            get
            {
                // BaseValue x Step^(n-1) >= Ceiling 을 만족하는 최소 n
                int level = 1 + (int)Math.Ceiling(Math.Log(Ceiling / BaseValue) / Math.Log(Step));
                return Math.Max(1, level);
            }
        }

        /**
         * @brief 이 레벨에서 최대 체력이 maxHealth일 때의 초당 절대 회복량.
         *
         * 상한이 적용된 값이다. 전투와 시뮬레이션이 함께 부르는 유일한 입구라
         * 여기서 자르면 두 곳이 갈릴 수 없다.
         */
        public static double PerSecondAt(int level, double maxHealth)
        {
            return maxHealth * CappedValueAtLevel(level);
        }

        public static double CostAtLevel(int level)
        {
            return UpgradeCost.Quantize(BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1)));
        }
    }
}
