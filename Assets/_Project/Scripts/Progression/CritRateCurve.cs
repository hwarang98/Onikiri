using System;
using Onikiri.Battle;

namespace Onikiri.Progression
{
    /**
     * @brief 치명타 확률 강화 곡선.
     *
     * **가산이다.** 확률은 곱연산으로 키울 수 없다 - 1을 넘는 순간 의미를 잃고,
     * 상한에 부딪히기 전까지의 구간이 곡선의 대부분을 차지하게 된다.
     *
     * 8단계의 공격속도가 가산이라 죽었던 것과 무엇이 다른가? 그때는 **DPS 기여도
     * 가산**이었다. 공격속도 +0.12는 DPS를 정확히 그만큼 올리고, 그 비율은
     * 레벨이 오를수록 0으로 수렴했다.
     *
     * 치명타율은 다르다. DPS 기여가 rate x (mult - 1) 이라서, 치명타 피해 축이
     * 함께 자라면 확률 한 칸의 값어치도 함께 자란다. 두 축이 서로를 살린다.
     * 실제로 레벨당 DPS 증가율은 Lv.1에서 0.45%, Lv.50에서 1.0%, Lv.200에서
     * 0.45%로 완만한 언덕을 그린다 - 0으로 수렴하지 않는다.
     *
     * 이 관계가 깨지는 조건은 하나다. **치명타 피해를 아무도 사지 않는 경우.**
     * 그래서 두 축의 비용을 비슷하게 두어 한쪽만 사는 것이 최적이 되지 않게 했다.
     * UpgradeEfficiencyTests가 네 축 모든 쌍을 검사한다.
     */
    public static class CritRateCurve
    {
        /** 레벨 1의 확률. CombatBaseline의 기본값과 같아야 한다 */
        public const double BaseValue = CombatBaseline.CritChance;

        /** 레벨당 가산 (확률 포인트) */
        public const double Step = 0.005d;

        /**
         * @brief 확률 상한.
         *
         * 100%로 두지 않는 이유는 그 지점에서 치명타가 치명타가 아니게 되기
         * 때문이다. 모든 타격이 치명타면 그것은 그냥 공격력이고, 금색 숫자가
         * 화면을 가득 채워 강조의 의미도 사라진다.
         */
        public const double Ceiling = 0.60d;

        /**
         * @brief 비용.
         *
         * 공격력(10)보다 훨씬 싸다. 레벨 하나가 주는 DPS가 그만큼 작기 때문이다 -
         * Lv.1에서 확률 +0.5%p는 기대 DPS를 0.45%밖에 올리지 않는다(공격력은 12%).
         * 같은 값을 매기면 아무도 누르지 않는 죽은 버튼이 된다.
         *
         * 정수 비용 중 네 축이 5배 안에 들어가는 조합은 (치명타율 1, 치명타피해 2)
         * 하나뿐이었다. 후보와 탈락 이유는 10단계 보고서에 기록했다.
         *
         * 비용 증가율은 다른 축과 반드시 같아야 한다 - 다르면 효율 비율이 레벨을
         * 따라 벌어지고 언젠가 한쪽이 죽는다.
         */
        public const double BaseCost = 1d;
        public const double CostGrowth = 1.15d;

        public static double ValueAtLevel(int level)
        {
            return BaseValue + Step * Math.Max(0, level - 1);
        }

        public static double CappedValueAtLevel(int level)
        {
            return Math.Min(Ceiling, ValueAtLevel(level));
        }

        public static double CostAtLevel(int level)
        {
            return BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1));
        }

        /** 상한을 넘지 않는 마지막 레벨 */
        public static int MaxLevel
        {
            get { return (int)Math.Floor((Ceiling - BaseValue) / Step) + 1; }
        }
    }
}
