using System;
using Onikiri.Battle;

namespace Onikiri.Progression
{
    /**
     * @brief 공격속도 강화 곡선과, 아트가 정하는 그 상한.
     *
     * 이 파일이 따로 있는 이유는 상한이 **밸런스 숫자가 아니라 계산 결과**이기
     * 때문이다. 8단계까지는 상한 8.23회/초를 손으로 적어뒀는데, 그 값에는 근거가
     * 없었다. 실제로 그 속도에서 무슨 일이 벌어지는지는 이렇다:
     *
     *   사무라이 스윙은 7프레임 / 14fps = 0.5초로 그려져 있다.
     *   초당 8.23회면 공격 간격이 0.121초이고, 스윙은 그 안에 들어가야 하므로
     *   4.11배속으로 재생된다. 프레임 하나가 17ms - 60fps 화면에서 한 프레임이다.
     *   그 시점에 화면에 보이는 것은 발도 동작이 아니라 스프라이트 노이즈다.
     *
     * 그래서 상한을 곡선이 아니라 클립에서 유도한다. 애니메이션은 2배속까지만
     * 허용하고(CombatFeel.MaxAnimationSpeed), 스윙이 공격 간격 안에 들어가야
     * 하므로 최대 공격속도는 2 / 클립길이로 정해진다. 클립을 교체하면 상한도
     * 함께 움직이고, 아무도 숫자를 다시 손으로 맞출 필요가 없다.
     *
     * 비용 곡선(x1.15)은 그대로 둔다. 공격력과 같은 증가율이라야 두 축의 골드당
     * 효율 비율이 레벨과 무관하게 일정해진다. UpgradeEfficiency 참고.
     */
    public static class AttackSpeedCurve
    {
        /** 레벨 1의 공격속도. PlayerCombat의 시작 스탯과 같아야 한다 */
        public const double BaseValue = 1.15d;

        /** 레벨당 배수. 공격력의 1.12보다 낮은 것은 의도적이다 */
        public const double Step = 1.04d;

        public const double BaseCost = 4d;
        public const double CostGrowth = 1.15d;

        /** 사무라이 공격 클립의 프레임 수와 프레임레이트. BattleContentBuilder가 쓰는 값 */
        public const int AttackFrameCount = 7;
        public const float AttackFrameRate = 14f;

        /** 압축 전 스윙 길이 (초) */
        public static float SwingDuration
        {
            get { return AttackFrameCount / AttackFrameRate; }
        }

        /** 애니메이션을 당기지 않은 자연 속도에서의 공격속도 */
        public static float NaturalAttacksPerSecond
        {
            get { return 1f / SwingDuration; }
        }

        /** 2배속까지 허용했을 때의 공격속도 상한 */
        public static float Ceiling
        {
            get { return CombatFeel.MaxAttacksPerSecond(SwingDuration); }
        }

        public static double ValueAtLevel(int level)
        {
            return BaseValue * Math.Pow(Step, Math.Max(0, level - 1));
        }

        /** 상한에서 잘린 실제 공격속도. 전투가 쓰는 값과 같아야 한다 */
        public static double CappedValueAtLevel(int level)
        {
            return Math.Min(Ceiling, ValueAtLevel(level));
        }

        public static double CostAtLevel(int level)
        {
            return BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1));
        }

        /**
         * @brief 상한을 넘지 않는 마지막 레벨.
         *
         * UpgradePanelBuilder가 이것으로 maxLevel을 기록한다. 상한을 값 자체가 아니라
         * 레벨로 옮겨야 UI가 "MAX"를 표시하고 구매를 막을 수 있다. 값에서만 자르면
         * 버튼은 계속 팔리는데 스탯이 안 오르는, 8단계의 죽은 버튼과 같은 상태가 된다.
         */
        public static int MaxLevelWithin(double ceiling)
        {
            if (ceiling <= BaseValue) return 1;
            return (int)Math.Floor(Math.Log(ceiling / BaseValue) / Math.Log(Step)) + 1;
        }

        /** 지금 아트 기준의 강화 상한 레벨 */
        public static int MaxLevel
        {
            get { return MaxLevelWithin(Ceiling); }
        }
    }
}
