using System;
using System.Collections.Generic;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief `EvolutionMarginExponent` 후보를 재유도하는 테스트 전용 오버레이 모형.
     *
     * ## 왜 오버레이인가 - 프로덕션 상수를 못 만지기 때문만이 아니다
     *
     * `StageCurve.EvolutionMarginExponent`는 상수이고 `StageSimulation`이 그것을
     * 통해서만 보정을 건다. 후보 일곱을 재려면 상수를 일곱 번 바꿔 돌려야 하는데,
     * 1.6단계는 프로덕션을 건드리지 않는다.
     *
     * 그런데 그것보다 나은 이유가 있다. **보정은 구매 궤적에 되먹이지 않는다.**
     * `Buy()`가 보는 것은 골드 수입·강화 비용·생존 목표(`보스 피해 x 1.15`)뿐이고
     * 셋 다 보스 **체력**과 무관하다. 그래서 보정을 걷어낸 세계 하나만 돌려 두면
     * 어떤 지수의 세계든 곱셈 한 번으로 정확히 재구성된다 - 일곱 번 돌린 것과
     * 같은 답이 나오면서 계산이 일곱 배 싸다.
     *
     * ## 모형의 식
     *
     *   보스 여유   neutral x A^(1-e)
     *   보스 처치   neutral x A^(e-1)
     *   생존 여유   neutral **그대로**
     *   잡몹 시간   neutral 그대로 (st11부터 스폰 하한에 붙어 있어 화력과 무관)
     *
     * `A`는 그 스테이지의 게이트 기반 누적 공격 배수다. v1.4에서는 전원이 귀문을
     * 돌파해 이 값을 무료로 가지므로, 정책과 무관하게 같은 `A`를 얹는다.
     *
     * ## 생존이 "그대로"인 이유 - 목표값이기 때문이다
     *
     * 생존 루프는 `EffectiveHealth >= 보스 피해 x 1.15`까지만 산다. 체력 배수가
     * 공짜로 오면 강화 레벨을 덜 사서 같은 목표에 닿고, 아낀 골드는 화력으로
     * 간다. 실측이 그것을 확인했다 - 기본 정책과 중립 정책의 생존 여유 비가
     * st50 이후 **0.999~1.006**이다.
     *
     * ## 모형 오차는 실측으로 재 뒀다
     *
     * 기본 정책(지수 0.42)과 중립 정책의 실제 여유 비를 `A^0.58`로 나눈 값이
     * **st51 이후 1.02~1.03**이다. 즉 이 모형은 그 구간에서 2~3% 보수적이다.
     * st37~47에서는 0.74~0.94로 더 크게 어긋나는데, 그것은 전직 골드 도약
     * 비용(누적 7.41e12)이 지갑을 비우는 효과다 - **v1.4에서 전직 비용이 0이
     * 되므로 그 항이 사라진다.** `PromotionBandTests.OverlayModel_MatchesTheSimulation`
     * 이 이 오차를 못 박는다.
     */
    public static class PromotionBandModel
    {
        /** 귀문 여섯의 게이트 스테이지. 경지는 그 다음 스테이지부터 유효하다 */
        public static readonly int[] GateStages = { 30, 40, 50, 70, 100, 150 };

        /**
         * @brief **1.6단계의 권장 지수.** 재유도 결과다.
         *
         * 후보 일곱(0.00 ~ 1.00)이 밴드와 도달층 계약을 **전부** 통과하므로,
         * 고를 근거는 승급 체감 하나뿐이다. 0.00이 그것을 최대로 만든다 -
         * 문 하나가 +10~20%이고, 지수를 올릴수록 그 몫이 보정으로 상쇄된다.
         *
         * 33단계가 0.42를 고른 근거("무과금은 1티어, 과금은 여섯")는 v1.5에서
         * 전원이 같은 티어를 무료로 받으면서 사라졌다. 값을 유지할 이유가
         * 남아 있지 않다.
         *
         * 0.00은 **보정항 자체를 지운다**는 뜻이기도 하다. `BossHealthForStage`의
         * 곱이 하나 줄고, 코리더 불변이 계수가 아니라 "항이 없음"으로 지켜진다.
         */
        public const double RecommendedExponent = 0.00d;

        /** 마일스톤이 되려면 문 하나가 최소 이만큼은 올려야 한다 */
        public const double MinimumFeltGain = 0.05d;

        /** 누적 공격 배수. 인덱스 = 티어 (0 = 로닌) */
        public static readonly double[] AttackAt =
        {
            1d,
            1.10d,
            1.10d * 1.10d,
            1.10d * 1.10d * 1.12d,
            1.10d * 1.10d * 1.12d * 1.12d,
            1.10d * 1.10d * 1.12d * 1.12d * 1.15d,
            1.10d * 1.10d * 1.12d * 1.12d * 1.15d * 1.20d
        };

        /** 이 스테이지에서 전원이 갖는 티어. 게이트를 지났으면 그 티어다 */
        public static int TierAtStage(int stage)
        {
            int tier = 0;
            for (int g = 0; g < GateStages.Length; g++)
                if (stage > GateStages[g]) tier = g + 1;
            return tier;
        }

        public static double AttackMultiplierAtStage(int stage)
        {
            return AttackAt[TierAtStage(stage)];
        }

        /** 이 문을 돌파한 직후의 **실질** 전투력 상승률 (보정을 뺀 뒤 남는 몫) */
        public static double FeltGainAtGate(int gateNumber, double exponent)
        {
            double step = AttackAt[gateNumber] / AttackAt[gateNumber - 1];
            return Math.Pow(step, 1d - exponent) - 1d;
        }

        // ---------------------------------------------------------------- 오버레이

        public struct Row
        {
            public int Stage;
            public double BossMargin;
            public double SurvivalMargin;
            public double StageSeconds;
            public int Tier;
            public BossCurve.Tier Class;
        }

        /**
         * @brief 중립 런에 지수 `e`의 세계를 얹는다.
         *
         * `neutral`은 반드시 `Policy.NeutralizeEvolution = true`로 돌린 것이어야
         * 한다 - 전직 배수도 보정도 없는 세계라야 오버레이가 이중으로 곱해지지
         * 않는다.
         */
        public static List<Row> Overlay(List<StageSimulation.StageResult> neutral, double exponent)
        {
            var rows = new List<Row>(neutral.Count);
            double fixedSeconds = StageSimulation.BossIntroSeconds + StageSimulation.BossWalkInSeconds;

            for (int i = 0; i < neutral.Count; i++)
            {
                var n = neutral[i];
                int tier = TierAtStage(n.Stage);
                double a = AttackAt[tier];

                rows.Add(new Row
                {
                    Stage = n.Stage,
                    BossMargin = n.BossMargin * Math.Pow(a, 1d - exponent),
                    SurvivalMargin = n.SurvivalMargin,
                    StageSeconds = n.MobSeconds + fixedSeconds
                                 + n.BossKillSeconds * Math.Pow(a, exponent - 1d),
                    Tier = tier,
                    Class = BossCurve.TierOf(n.Stage)
                });
            }
            return rows;
        }

        // ---------------------------------------------------------------- 도달층

        /** `from`부터 `to`까지 가는 데 드는 전투 시간 */
        public static double CombatSeconds(List<Row> rows, int from, int to)
        {
            double total = 0d;
            for (int i = from - 1; i < to && i < rows.Count; i++) total += rows[i].StageSeconds;
            return total;
        }

        /** 이 예산으로 `from`부터 몇 층까지 가는가 */
        public static int ReachedStage(List<Row> rows, double budget, int from)
        {
            double spent = 0d;
            int reached = from - 1;
            for (int i = from - 1; i < rows.Count; i++)
            {
                spent += rows[i].StageSeconds;
                if (spent > budget) break;
                reached = rows[i].Stage;
            }
            return reached;
        }

        // ---------------------------------------------------------------- 요약

        public struct BandSummary
        {
            public double NormalMin, NormalMax;
            public double ChapterMin, ChapterMax;
            public double FinaleMin, FinaleMax;
            public double SurvivalMin;
        }

        public static BandSummary Summarize(List<Row> rows, int from, int to)
        {
            var s = new BandSummary
            {
                NormalMin = double.MaxValue, ChapterMin = double.MaxValue,
                FinaleMin = double.MaxValue, SurvivalMin = double.MaxValue
            };

            for (int i = from - 1; i < to && i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.SurvivalMargin < s.SurvivalMin) s.SurvivalMin = r.SurvivalMargin;

                switch (r.Class)
                {
                    case BossCurve.Tier.Finale:
                        if (r.BossMargin < s.FinaleMin) s.FinaleMin = r.BossMargin;
                        if (r.BossMargin > s.FinaleMax) s.FinaleMax = r.BossMargin;
                        break;
                    case BossCurve.Tier.Chapter:
                        if (r.BossMargin < s.ChapterMin) s.ChapterMin = r.BossMargin;
                        if (r.BossMargin > s.ChapterMax) s.ChapterMax = r.BossMargin;
                        break;
                    default:
                        if (r.BossMargin < s.NormalMin) s.NormalMin = r.BossMargin;
                        if (r.BossMargin > s.NormalMax) s.NormalMax = r.BossMargin;
                        break;
                }
            }
            return s;
        }
    }
}
