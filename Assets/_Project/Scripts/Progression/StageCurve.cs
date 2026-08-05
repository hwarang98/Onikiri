using System;
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
         * @brief 1스테이지 보스의 체력 배수 (해당 스테이지 잡몹 체력 기준).
         *
         * 이 값의 뜻은 "잡몹 몇 마리 분량"이 아니라 **제한 시간의 압박**이다.
         * 잡몹은 큐로 한 마리씩 들어오므로 처치 속도가 스폰 공급에 묶이지만,
         * 보스는 처음부터 나와 있어서 순수하게 DPS로만 깎인다.
         *
         * 8은 무강화 플레이어가 1스테이지 보스를 아슬아슬하게 잡는 값이다
         * (17.7초 / 때릴 수 있는 24.7초). 첫 보스는 벽이 아니라 튜토리얼이어야 한다.
         */
        public const double BossHealthMultiplierBase = 8d;

        /**
         * @brief 스테이지마다 체력 배수에 추가로 곱하는 값.
         *
         * 9단계에서는 배수가 8 고정이었고, 그 결과 곡선을 따라가는 플레이어의
         * 보스 여유가 2.1배에서 5.6배로 발산했다. 5스테이지부터 제한 시간이
         * 아무 일도 하지 않는다는 뜻이다. 10단계에서 치명타 두 축이 들어오면서
         * 그 발산은 20스테이지 기준 82배까지 커졌다.
         *
         * 원인은 단순하다. 잡몹 체력은 스테이지마다 1.55배로 자라는데 플레이어의
         * DPS는 골드(1.72배)로 사는 강화 레벨을 통해 그보다 빨리 자란다. 보스가
         * 잡몹과 같은 비율로만 자라면 그 차이가 그대로 여유가 된다.
         *
         * **고정 배율로는 안 된다.** 처음에 1.225 하나로 맞춰봤더니 1~20 스테이지는
         * 밴드에 들어갔지만 30스테이지에서 여유가 0.88로 떨어져 보스가 벽이 됐다.
         * 플레이어의 DPS 성장률이 일정하지 않기 때문이다 - 초반에는 치명타 두 축이
         * 바닥에서 자라며 빠르게 오르고, 공격속도가 Lv.32에서, 치명타율이 Lv.97에서
         * 상한에 닿으면 성장이 공격력과 치명타 피해 둘로 좁아져 느려진다.
         *
         * 그래서 배율도 같은 모양으로 **감쇠**시킨다. 초반 1.34에서 시작해
         * 후반 1.065로 수렴한다. 후자는 우연한 값이 아니라 관측값이다 - 상한에
         * 닿은 뒤 DPS는 스테이지마다 약 1.72배로 자라고 잡몹 체력은 1.55배로
         * 자라므로, 그 차이 1.72/1.55 = 1.11 근처가 평형이다.
         *
         * 계수는 시뮬레이션으로 찾았다. 1~20 여유의 최대/최소 비가 가장 작아지는
         * 지점이며(1.22배), 동시에 50스테이지까지 여유가 1 아래로 내려가지 않는다.
         */
        public const double BossHealthRampStart = 1.34d;
        public const double BossHealthRampFinal = 1.065d;

        /** 램프가 Start에서 Final로 내려오는 속도. 1에 가까울수록 천천히 */
        public const double BossHealthRampDecay = 0.93d;

        /**
         * @brief 이 스테이지 보스의 체력 배수.
         *
         * 닫힌 식이 없어 곱을 직접 돈다. 스테이지 수만큼의 반복이고 호출은
         * 보스 스폰과 시뮬레이션뿐이라 비용은 문제가 되지 않는다.
         */
        public static double BossHealthMultiplier(int stage)
        {
            double multiplier = BossHealthMultiplierBase;
            int steps = StepsFrom(stage);

            for (int k = 0; k < steps; k++)
            {
                double ramp = BossHealthRampFinal
                            + (BossHealthRampStart - BossHealthRampFinal) * Math.Pow(BossHealthRampDecay, k);
                multiplier *= ramp;
            }

            return multiplier;
        }

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

        public static BigDouble BossHealth(BigDouble stageMobHealth, int stage)
        {
            return stageMobHealth * BigDouble.FromDouble(BossHealthMultiplier(stage));
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
            var health = BossHealth(averageMobHealth * HealthMultiplier(stage), stage);

            // 챕터 보스는 더 무겁다. **이 한 줄이 빠져 있었다.**
            //
            // BossCurve에 상수를 선언하고 "1보다 큰가"만 검사하는 테스트를 뒀는데,
            // 그 값이 실제로 쓰이는지는 아무도 확인하지 않았다. 치명타 축에서
            // 같은 함정을 막으려고 CritAxes_FeedTheDpsFormula 를 만들어 놓고
            // 여기서는 그러지 않았다. 상수의 존재는 연결의 증거가 아니다
            return BossCurve.IsChapterBoss(stage)
                ? health * BigDouble.FromDouble(BossCurve.ChapterHealthMultiplier)
                : health;
        }

        public static BigDouble BossGoldForStage(BigDouble averageMobGold, int stage)
        {
            var gold = BossGold(averageMobGold * GoldMultiplier(stage));

            return BossCurve.IsChapterBoss(stage)
                ? gold * BigDouble.FromDouble(BossCurve.ChapterGoldMultiplier)
                : gold;
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
