using System;
using Onikiri.Battle;

namespace Onikiri.Progression
{
    /**
     * @brief 치명타 피해 배수 강화 곡선.
     *
     * 곱연산이고 상한이 없다. 공격력과 함께 후반 DPS를 끝까지 끌고 가는 축이다.
     *
     * 이 축의 DPS 기여율은 특이하게 **레벨이 오를수록 커진다.** 배수가 커지면
     * 치명타가 DPS의 대부분을 차지하게 되고, 그 지점에서 배수를 3% 올리는 것은
     * DPS를 거의 3% 올리는 것과 같아지기 때문이다.
     *
     *   Lv.1   0.64%   (배수 2.0, 확률 12% - 치명타가 DPS의 11%뿐)
     *   Lv.50  2.49%
     *   Lv.200 3.0%에 수렴 (= step - 1)
     *
     * 그래서 초반에는 약하고 후반에 강한 축이다. 그 형태 자체는 문제가 아니지만,
     * 축 사이 효율 차이가 5배를 넘지 않아야 하므로 baseCost로 위치를 맞췄다.
     * 낮은 레벨에서 너무 비싸면 아무도 시작하지 않고, 너무 싸면 후반에 다른
     * 축을 전부 눌러버린다.
     */
    public static class CritDamageCurve
    {
        /** 레벨 1의 배수. CombatBaseline의 기본값과 같아야 한다 */
        public const double BaseValue = CombatBaseline.CritMultiplier;

        /** 레벨당 배수 */
        public const double Step = 1.03d;

        /**
         * @brief 비용.
         *
         * 치명타율(1)의 두 배다. 두 축의 DPS 기여가 레벨에 따라 반대로 움직이기
         * 때문에 이 비율이 좁게 정해진다 - 치명타피해는 레벨이 오를수록 세지고
         * 치명타율은 언덕을 그린 뒤 내려온다. 둘의 비용이 같으면 Lv.200에서
         * 6.7배까지 벌어지고, 3배면 Lv.1에서 5.6배로 벌어진다. 2배가 유일하게
         * 양쪽 끝을 모두 통과한다. 10단계 보고서 참고.
         */
        public const double BaseCost = 2d;
        public const double CostGrowth = 1.15d;

        public static double ValueAtLevel(int level)
        {
            return BaseValue * Math.Pow(Step, Math.Max(0, level - 1));
        }

        public static double CostAtLevel(int level)
        {
            return BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1));
        }
    }
}
