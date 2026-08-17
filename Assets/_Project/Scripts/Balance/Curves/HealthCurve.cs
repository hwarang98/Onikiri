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
         * 회복(12)보다 싸다. 초반 유효체력 기여가 회복보다 크기 때문이 아니라
         * 그 반대다 - 기여가 크므로(Lv.1에서 +7.69% EHP 대 +2.65%) 같은 값이면
         * 회복이 죽는다. 정수 조합을 훑어 1~200 전 구간 최악 비율이 가장 작아지는
         * 지점(3.86배)으로 잡았다. SurvivalEfficiency 참고.
         */
        /** E-3 수정: 전 축 일괄 상향 + 정수화. 규칙과 배율은 UpgradeCost가 단일 출처다 */
        public const double BaseCost = 1.37455d * UpgradeCost.RaiseScale;

        /** 다른 모든 축과 같아야 한다. 다르면 효율 비율이 레벨을 따라 벌어진다 */
        public const double CostGrowth = 1.0176225d;

        public static double ValueAtLevel(int level)
        {
            return BaseValue * Math.Pow(Step, Math.Max(0, level - 1));
        }

        public static double CostAtLevel(int level)
        {
            return UpgradeCost.Quantize(BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1)));
        }
    }
}
