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
        public const double Step = 1.12d;

        public const double BaseCost = 10d;

        /** 공격속도와 같아야 한다. 다르면 두 축의 효율 비율이 레벨을 따라 벌어진다 */
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
