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
        public const double Step = 1.10d;

        /**
         * @brief 비용.
         *
         * 체력(9)보다 싸다. 초반 유효체력 기여가 체력보다 작기 때문이다 -
         * Lv.1에서 비율 1%/초 x 30초 = 0.3이라 EHP의 23%만 담당한다.
         * 정수 조합을 훑어 밴드와 생존성을 동시에 만족하는 값으로 잡았다.
         */
        public const double BaseCost = 4d;
        public const double CostGrowth = 1.15d;

        /** 초당 회복 비율 (최대 체력 대비) */
        public static double ValueAtLevel(int level)
        {
            return BaseValue * Math.Pow(Step, Math.Max(0, level - 1));
        }

        /** 이 레벨에서 최대 체력이 maxHealth일 때의 초당 절대 회복량 */
        public static double PerSecondAt(int level, double maxHealth)
        {
            return maxHealth * ValueAtLevel(level);
        }

        public static double CostAtLevel(int level)
        {
            return BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1));
        }
    }
}
