using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 스테이지가 오를 때 요괴 체력과 골드 보상이 자라는 규칙.
     *
     * 이것이 존재하는 이유는 방치형에서 성장이 죽는 방식이 하나이기 때문이다.
     * 요괴 체력이 고정이면 공격력을 올리는 순간 요괴가 한 방에 죽고, 그 뒤로는
     * 공격력을 아무리 더 올려도 수입이 1원도 늘지 않는다. 처치 속도가 공격력이
     * 아니라 요괴 공급에 묶이기 때문이다.
     *
     * 해결은 튜닝이 아니라 구조다. 스테이지마다 체력을 함께 올려 **처치당 타격 수**를
     * 일정 범위로 유지하면, 공격력과 공격속도가 둘 다 끝까지 의미를 갖는다.
     *
     * 골드 성장률을 체력 성장률보다 조금 높게 잡은 것은 의도적이다. 같으면 수입과
     * 강화 비용이 정확히 평형을 이뤄 진행이 멈춘 것처럼 느껴진다.
     */
    public static class StageCurve
    {
        /** 다음 스테이지로 넘어가는 데 필요한 처치 수 */
        public const int KillsPerStage = 10;

        /** 스테이지당 요괴 체력 배수 */
        public const double HealthGrowth = 1.55d;

        /**
         * @brief 스테이지당 골드 보상 배수.
         *
         * 임의로 고른 값이 아니라 다른 세 곡선에서 유도된 값이다. 체력을 따라잡으려면
         * 스테이지마다 공격력 레벨이 이만큼 필요하다:
         *
         *     레벨/스테이지 = ln(HealthGrowth) / ln(공격력 배수)
         *                   = ln(1.55) / ln(1.12) = 3.87
         *
         * 강화 비용은 레벨마다 1.15배이므로, 그만큼 레벨을 올리는 데 드는 비용은
         * 스테이지마다 1.15^3.87 = 1.72배가 된다. 골드가 그보다 느리게 자라면
         * 스테이지가 오를수록 살 수 있는 레벨이 줄어들고, 처치당 타격 수가 서서히
         * 불어나 후반이 늘어진다. 반대로 빠르면 요괴가 한 방에 죽는 상태로 돌아간다.
         *
         * 처음에 1.62로 잡았다가 시뮬레이션에서 50스테이지쯤 뒤 타격 수가 계속 늘어나는
         * 것을 확인하고 고쳤다. StageProgressionTests가 이 관계를 못 박는다.
         */
        public const double GoldGrowth = 1.72d;

        // ---------------------------------------------------------------- 보스

        /**
         * @brief 보스 체력 = 해당 스테이지 잡몹 체력 x 이 값.
         *
         * 8이라는 숫자의 뜻은 "잡몹 여덟 마리 분량"이 아니라 **제한 시간의 압박**이다.
         * 잡몹은 큐로 한 마리씩 들어오므로 처치 속도가 스폰 간격에 묶여 있지만,
         * 보스는 처음부터 전부 나와 있어서 순수하게 DPS로만 깎인다. 그래서 이 값은
         * 곧 "30초 안에 초당 얼마를 넣어야 하는가"를 정한다.
         *
         * HitsToKill이 스테이지 내내 5~12대 범위를 유지하므로, 보스는 40~96대다.
         * 지금 공격속도(1.15~3.88회/초)에서 11~35초 - 제한 시간 30초를 아슬아슬하게
         * 걸치도록 의도한 값이다. 강화가 뒤처지면 실패하고, 따라가면 통과한다.
         */
        public const double BossHealthMultiplier = 8d;

        /**
         * @brief 보스 골드 = 해당 스테이지 잡몹 골드 x 이 값.
         *
         * 체력 배수(8)보다 크게 잡은 것은 의도적이다. 같으면 보스는 "체력만 많은 잡몹"과
         * 골드 효율이 똑같아서, 30초 제한과 실패 위험을 감수할 이유가 사라진다.
         * 12/8 = 1.5배의 웃돈이 도전의 대가다.
         */
        public const double BossGoldMultiplier = 12d;

        /** 보스전 제한 시간 (초). 초과하면 스테이지 실패 - 패널티는 없다 */
        public const float BossTimeLimitSeconds = 30f;

        public static BigDouble BossHealth(BigDouble stageMobHealth)
        {
            return stageMobHealth * BigDouble.FromDouble(BossHealthMultiplier);
        }

        public static BigDouble BossGold(BigDouble stageMobGold)
        {
            return stageMobGold * BigDouble.FromDouble(BossGoldMultiplier);
        }

        /**
         * @brief 1스테이지 기준 잡몹 평균에서 이 스테이지 보스의 체력/보상을 낸다.
         *
         * BossFight가 스폰할 때 부르는 함수이고, StageSimulation이 난이도를 잴 때도
         * 같은 것을 부른다. 두 곳이 각자 곱셈을 하고 있으면 언젠가 한쪽만 고쳐지고,
         * 그때 "계산상으로는 통과하는데 실제로는 실패하는" 상태가 만들어진다.
         */
        public static BigDouble BossHealthForStage(BigDouble averageMobHealth, int stage)
        {
            return BossHealth(averageMobHealth * HealthMultiplier(stage));
        }

        public static BigDouble BossGoldForStage(BigDouble averageMobGold, int stage)
        {
            return BossGold(averageMobGold * GoldMultiplier(stage));
        }

        /** stage(1부터)의 체력 배수. 1스테이지는 1배 */
        public static BigDouble HealthMultiplier(int stage)
        {
            return BigDouble.Pow(BigDouble.FromDouble(HealthGrowth), StepsFrom(stage));
        }

        /** stage(1부터)의 골드 배수. 1스테이지는 1배 */
        public static BigDouble GoldMultiplier(int stage)
        {
            return BigDouble.Pow(BigDouble.FromDouble(GoldGrowth), StepsFrom(stage));
        }

        private static int StepsFrom(int stage)
        {
            return stage > 1 ? stage - 1 : 0;
        }

        /**
         * @brief 이 체력을 이 데미지로 처치하는 데 필요한 타격 수.
         *
         * 밸런싱의 핵심 지표다. 이 값이 1로 내려앉으면 공격력 강화가 무의미해지고,
         * 너무 커지면 한 마리 잡는 데 지루해진다.
         */
        public static int HitsToKill(BigDouble health, BigDouble damage)
        {
            if (damage <= BigDouble.Zero) return int.MaxValue;

            BigDouble hits = health / damage;
            double value = hits.ToDouble();
            if (double.IsInfinity(value) || value > int.MaxValue) return int.MaxValue;

            int rounded = (int)value;
            // 나머지가 남으면 한 대 더 때려야 한다
            return BigDouble.FromDouble(rounded) < hits ? rounded + 1 : (rounded < 1 ? 1 : rounded);
        }
    }
}
