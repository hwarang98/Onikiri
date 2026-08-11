using System;

namespace Onikiri.Progression
{
    /**
     * @brief 공격력 강화 곡선.
     *
     * AttackSpeedCurve와 짝을 이룬다. 이 값들이 코드 세 곳에 흩어져 있었기 때문에
     * 따로 뽑았다 - UpgradePanelBuilder가 씬에 기록하고, 테스트가 시뮬레이션에
     * 쓰고, 보고서의 진행 시간 계산이 또 한 번 적었다. 셋이 조용히 어긋나면
     * "계산상으로는 통과하는데 실제로는 실패하는" 밸런스가 만들어진다.
     *
     * 상한이 없다. 요괴 체력이 스테이지마다 지수로 오르므로 공격력도 끝까지
     * 따라가야 하고, 공격속도와 달리 표시할 애니메이션이 없어서 아트가 정하는
     * 한계도 없다.
     */
    public static class AttackPowerCurve
    {
        /** 레벨 1의 공격력. PlayerCombat의 시작 데미지와 같아야 한다 */
        public const double BaseValue = 5d;

        /** 레벨당 배수. 스테이지 체력 성장(x1.55)을 3.87레벨로 따라잡는 값 */
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
        public const double Step = 1.0142669d;

        /** E-3 수정: 전 축 일괄 상향 + 정수화. 규칙과 배율은 UpgradeCost가 단일 출처다 */
        public const double BaseCost = 1.5273d * UpgradeCost.RaiseScale;

        /** 공격속도와 같아야 한다. 다르면 두 축의 효율 비율이 레벨을 따라 벌어진다 */
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
