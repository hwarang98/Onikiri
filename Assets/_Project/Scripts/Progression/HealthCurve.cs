using System;

namespace Onikiri.Progression
{
    /**
     * @brief 체력 강화 곡선.
     *
     * 곱연산이고 상한이 없다. 보스 공격력이 스테이지마다 지수로 오르므로 체력도
     * 같은 형태여야 따라간다 - 공격력/요괴 체력 관계와 같은 구조다.
     *
     * 기준값 100은 임의로 고른 값이 아니라 1스테이지 보스가 무강화 플레이어를
     * 죽이지 못하게 하는 값이다. 보스는 2초마다 6을 때리므로 때릴 수 있는 24.7초
     * 동안 12번, 총 72의 피해를 낸다. 첫 보스는 화력으로도 체력으로도 벽이
     * 아니어야 한다 - 게이트를 배우는 자리다.
     */
    public static class HealthCurve
    {
        public const double BaseValue = 100d;
        public const double Step = 1.10d;

        /**
         * @brief 비용.
         *
         * 회복(12)보다 싸다. 초반 유효체력 기여가 회복보다 크기 때문이 아니라
         * 그 반대다 - 기여가 크므로(Lv.1에서 +7.69% EHP 대 +2.65%) 같은 값이면
         * 회복이 죽는다. 정수 조합을 훑어 1~200 전 구간 최악 비율이 가장 작아지는
         * 지점(3.86배)으로 잡았다. SurvivalEfficiency 참고.
         */
        public const double BaseCost = 9d;

        /** 다른 모든 축과 같아야 한다. 다르면 효율 비율이 레벨을 따라 벌어진다 */
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
