using System;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 방치(오프라인) 보상 계산.
     *
     * 순수 함수로 분리한 이유는, 이 계산이 틀렸을 때 알아채는 방법이 "몇 시간 앱을
     * 꺼놨다가 켜보기"밖에 없기 때문이다. 테스트로 못 박아두는 편이 훨씬 싸다.
     *
     * 초당 수입을 스탯에서 직접 유도한다. 실제 플레이 중 획득량을 평균 내는 방법도
     * 있지만, 그러면 방금 강화한 것이 반영되기까지 시간이 걸리고 앱을 켜자마자 끄면
     * 표본이 없다.
     */
    public static class IdleIncome
    {
        /** 방치 효율. 실제 플레이보다 벌이가 좋으면 게임을 켜둘 이유가 없어진다 */
        public const double Efficiency = 0.5d;

        /** 보상이 쌓이는 최대 시간 */
        public static readonly TimeSpan MaxAccrual = TimeSpan.FromHours(8d);

        /**
         * @brief 현재 스탯에서 기대되는 초당 골드.
         *
         * 두 가지 상한 중 낮은 쪽이 실제 처치 속도가 된다:
         *  - 공격 속도 / 처치당 타격 수  (플레이어가 얼마나 빨리 죽일 수 있는가)
         *  - 1 / 스폰 간격                (필드가 얼마나 빨리 채워지는가)
         *
         * 두 번째를 빼면 안 된다. 공격력을 크게 올린 상태에서는 요괴 공급이 실제
         * 병목이라, 이것을 무시하면 방치 보상이 실제 플레이보다 훨씬 후해진다.
         */
        public static double GoldPerSecond(BigDouble damage, float attacksPerSecond,
                                           BigDouble averageHealth, BigDouble averageGold,
                                           float spawnInterval)
        {
            if (attacksPerSecond <= 0f) return 0d;
            if (damage <= BigDouble.Zero) return 0d;

            int hitsToKill = StageCurve.HitsToKill(averageHealth, damage);
            if (hitsToKill <= 0) return 0d;

            double killsFromDamage = attacksPerSecond / (double)hitsToKill;
            double killsFromSupply = spawnInterval > 0f ? 1d / spawnInterval : double.MaxValue;
            double killsPerSecond = Math.Min(killsFromDamage, killsFromSupply);

            double gold = averageGold.ToDouble();
            if (double.IsInfinity(gold) || double.IsNaN(gold)) return 0d;

            return killsPerSecond * gold;
        }

        /** 보상이 쌓인 시간. 경과 시간을 0 ~ 8시간으로 자른다 */
        public static TimeSpan AccruedTime(DateTime lastQuitUtc, DateTime nowUtc)
        {
            var elapsed = nowUtc - lastQuitUtc;

            // 기기 시계를 뒤로 돌리면 경과가 음수로 나온다. 보상을 주지 않을 뿐,
            // 오류로 다루지는 않는다
            if (elapsed < TimeSpan.Zero) return TimeSpan.Zero;

            return elapsed > MaxAccrual ? MaxAccrual : elapsed;
        }

        /** 지급할 골드. 경과 시간 x 초당 골드 x 효율 */
        public static BigDouble Reward(double goldPerSecond, TimeSpan accrued)
        {
            if (goldPerSecond <= 0d || accrued <= TimeSpan.Zero) return BigDouble.Zero;

            double amount = goldPerSecond * accrued.TotalSeconds * Efficiency;
            if (double.IsInfinity(amount) || double.IsNaN(amount)) return BigDouble.Zero;

            return BigDouble.FromDouble(Math.Floor(amount));
        }

        /** 상한에 걸렸는지. 팝업이 "상한 도달"을 띄울지 결정한다 */
        public static bool IsCapped(DateTime lastQuitUtc, DateTime nowUtc)
        {
            return nowUtc - lastQuitUtc >= MaxAccrual;
        }
    }
}
