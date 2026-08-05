using System;

namespace Onikiri.Progression
{
    /**
     * @brief 초당 체력 회복 곡선.
     *
     * **곱연산이다.** 가산으로 두면 8단계의 공격속도와 같은 이유로 죽는다 -
     * 회복이 유효체력에 기여하는 양은 회복량에 정비례하는데, 그 값이 선형으로
     * 자라면 비율 기여가 0으로 수렴하고 비용은 계속 지수로 오른다.
     *
     * **체력과 어떻게 다른가.** 유효체력 기준으로 보면 둘은 거의 같은 일을 한다 -
     * 제한 시간이 고정이라 회복 1/초는 그 시간만큼의 체력과 같다. 그래서 두 축을
     * 완전히 구분되는 것으로 포장하지 않았다. 실제로 갈리는 지점은 하나다:
     *
     *   회복은 **전투가 길수록** 값어치가 커진다.
     *
     * 챕터 보스는 등장 워크인이 있어 전투가 짧고(때릴 수 있는 24.7초), 일반
     * 스테이지 보스는 워크인이 없어 길다(30초). 회복 축은 일반 보스에서 더
     * 유리하고 체력 축은 어디서나 같다. 그 차이가 지금 이 게임에서 두 축을
     * 나누는 전부이며, 보고서에 그대로 적었다.
     *
     * step을 체력(1.10)보다 조금 높게 잡은 것은 그 위에 역할 차이를 하나 더
     * 얹기 위해서다. 초반에는 체력이, 후반에는 회복이 유효체력의 큰 쪽을 맡는다.
     * 대신 그만큼 두 축의 효율 비율이 레벨을 따라 움직이므로, 비용으로 가운데를
     * 맞춰 1~200 전체가 밴드 안에 들어가게 했다. SurvivalEfficiency 참고.
     */
    public static class HealthRegenCurve
    {
        /** 레벨 1의 초당 회복량 */
        public const double BaseValue = 1d;

        public const double Step = 1.115d;

        /**
         * @brief 비용.
         *
         * 체력(9)보다 비싸다. step이 높아 후반 유효체력 기여가 체력을 앞지르므로
         * (Lv.200에서 +9.39% 대 +1.84%) 그만큼을 비용으로 되돌려 놓는다.
         * 두 축의 효율 비율은 Lv.1에서 3.86배(체력 우위), Lv.100 근처에서 1.01배,
         * Lv.200에서 3.83배(회복 우위)로 대칭적인 사발 모양을 그린다.
         */
        public const double BaseCost = 12d;
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
