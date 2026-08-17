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
        /** 귀문 여섯의 게이트 스테이지. **표의 출처는 프로덕션이다** */
        public static int[] GateStages { get { return PromotionTrialCatalog.GateStages; } }

        /**
         * @brief **이 모형이 재는 지수가 2.1단계에 바뀌었다.**
         *
         *   1.6~2단계   전역 `EvolutionMarginExponent` - 여섯 문 전부에 걸린다
         *   2.1단계~    `StageCurve.DeepPromotionConvergenceExponent` -
         *               **문 다섯·여섯에만** 걸린다
         *
         * 전역 보정은 지워졌고(승인 A-1), 그 자리를 문제가 실제로 있는 구간에만
         * 작용하는 다른 항이 대신한다(승인 B-1). 그래서 이 모형의 `exponent`
         * 인자는 이제 **심층 지수**이고, 문1~4의 체감은 어떤 값에서도 안 움직인다 -
         * `FeltGainAtGate`가 그 구조를 그대로 담는다.
         *
         * 값은 프로덕션에서 읽는다. 두 벌을 두면 후보 비교표와 실제 곡선이
         * 갈리고, 그 갈림은 밴드가 아니라 심층에서만 나타난다.
         */
        public static double ShippedDeepExponent
        {
            get { return StageCurve.DeepPromotionConvergenceExponent; }
        }

        /** 예전 이름. 부르는 쪽을 한꺼번에 안 고치려고 남겨 둔 별칭이다 */
        public static double RecommendedExponent { get { return ShippedDeepExponent; } }

        /** 마일스톤이 되려면 문 하나가 최소 이만큼은 올려야 한다 */
        public const double MinimumFeltGain = 0.05d;

        /**
         * @brief 누적 공격 배수. 인덱스 = 티어 (0 = 로닌).
         *
         * 1.6단계에는 여기 곱셈이 손으로 적혀 있었다. 2단계에 `EvolutionCurve`를
         * 부르게 바꾼 이유는 카탈로그의 스텝이 움직이는 날 이 모형만 옛 배수를
         * 재는 것을 막기 위해서다 - 밴드 판정이 조용히 거짓이 되는 경로다.
         */
        public static readonly double[] AttackAt = BuildAttackTable();

        static double[] BuildAttackTable()
        {
            var table = new double[EvolutionCurve.MaxTier + 1];
            for (int tier = 0; tier < table.Length; tier++)
                table[tier] = EvolutionCurve.AttackMultiplierAt(tier);
            return table;
        }

        /**
         * @brief 이 스테이지에서 전원이 갖는 티어. **판정도 프로덕션이 한다.**
         *
         * `gateStage < stage`다. 등호가 아닌 이유는
         * `PromotionTrialCatalog.TierAtFrontier` 주석에 있다.
         */
        public static int TierAtStage(int stage)
        {
            return PromotionTrialCatalog.TierAtFrontier(stage);
        }

        public static double AttackMultiplierAtStage(int stage)
        {
            return AttackAt[TierAtStage(stage)];
        }

        /**
         * @brief 이 문을 돌파한 직후의 **실질** 전투력 상승률 (보정을 뺀 뒤 남는 몫).
         *
         * 문1~4는 심층 보정이 안 걸리므로 **명목 배수가 통째로 실질**이다 -
         * 지수가 어떤 값이어도 10/10/12/12%다. 문5·6만 `step^(1-e)`로 눌린다.
         *
         * 이 분기가 B-1의 전부이고, `PromotionDomainTests.EveryGate_MovesRealPower...`가
         * 앞 넷을 엄격한 허용 오차로 고정한다.
         */
        public static double FeltGainAtGate(int gateNumber, double deepExponent)
        {
            double step = AttackAt[gateNumber] / AttackAt[gateNumber - 1];

            if (!PromotionTrialCatalog.IsDeepGate(gateNumber)) return step - 1d;
            return Math.Pow(step, 1d - deepExponent) - 1d;
        }

        /**
         * @brief 이 스테이지에서 심층 보정이 잡는 배수 (지수를 얹기 **전**의 몫).
         *
         * 문 다섯째부터의 스텝만 곱한다. 티어 4 이하면 정확히 1이다 -
         * `StageCurve.DeepPromotionCompensation`과 같은 규칙, 같은 출처다.
         */
        public static double DeepSteppedAtStage(int stage)
        {
            int tier = TierAtStage(stage);
            if (tier < PromotionTrialCatalog.FirstDeepGate) return 1d;

            return EvolutionCurve.AttackMultiplierBetween(
                PromotionTrialCatalog.FirstDeepGate - 1, tier);
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
         * @brief 중립 런에 심층 지수 `e`의 세계를 얹는다.
         *
         * `neutral`은 반드시 `Policy.NeutralizeEvolution = true`로 돌린 것이어야
         * 한다 - 전직 배수도 심층 보정도 없는 세계라야 오버레이가 이중으로
         * 곱해지지 않는다.
         *
         * ## 곱이 둘로 갈렸다 (2.1단계)
         *
         *     여유   neutral x (전 티어 배수) / (심층 몫)^e
         *     시간   neutral 의 보스 시간 x (심층 몫)^e / (전 티어 배수)
         *
         * 앞의 곱은 문 여섯 전부의 배수이고 뒤의 나눗셈은 **문 다섯·여섯만**이다.
         * 하나의 `A^(1-e)`로 쓸 수 없게 된 것이 B-1의 모양 그대로다.
         */
        public static List<Row> Overlay(List<StageSimulation.StageResult> neutral, double deepExponent)
        {
            var rows = new List<Row>(neutral.Count);
            double fixedSeconds = StageSimulation.BossIntroSeconds + StageSimulation.BossWalkInSeconds;

            for (int i = 0; i < neutral.Count; i++)
            {
                var n = neutral[i];
                int tier = TierAtStage(n.Stage);

                double attack = AttackAt[tier];
                double deep = Math.Pow(DeepSteppedAtStage(n.Stage), deepExponent);

                rows.Add(new Row
                {
                    Stage = n.Stage,
                    BossMargin = n.BossMargin * attack / deep,
                    SurvivalMargin = n.SurvivalMargin,
                    StageSeconds = n.MobSeconds + fixedSeconds
                                 + n.BossKillSeconds * deep / attack,
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
