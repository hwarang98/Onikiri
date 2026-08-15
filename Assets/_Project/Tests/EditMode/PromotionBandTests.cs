using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief `EvolutionMarginExponent` 재유도 (1.6단계).
     *
     * 33단계가 0.42를 고른 근거는 "무과금은 1티어에 머물고 과금은 여섯을 다
     * 오른다"였다. v1.4에서 **전원이 귀문 돌파로 같은 티어를 무료로** 받으므로
     * 그 근거가 통째로 사라졌다. 값을 유지할 이유가 없어졌으니 다시 유도한다.
     *
     * 여기서 재는 세계는 프로덕션이 아니라 `PromotionBandModel`의 오버레이다.
     * 그 모형이 실제 시뮬레이션과 같은 답을 낸다는 것을 먼저 증명하고
     * (`OverlayModel_MatchesTheSimulation`), 그 위에서 후보를 비교한다.
     */
    public class PromotionBandTests
    {
        // 밴드 (기존 계약 그대로 - 임의로 낮추지 않는다)
        const double AcceleratedNormalFloor = 1.40d;
        const double AcceleratedChapterFloor = 1.25d;
        const double AcceleratedFinaleFloor = 1.08d;
        const double AcceleratedNormalCeiling = 4.50d;
        const double AcceleratedChapterCeiling = 2.80d;
        const double AcceleratedFinaleCeiling = 3.10d;
        const double SurvivalFloor = 1.15d;
        const int ReachFloorStage = 260;
        const int ReachLeadCap = 240;

        const int Horizon = 500;

        static readonly double[] Candidates = { 0.00d, 0.20d, 0.30d, 0.42d, 0.50d, 0.60d, 1.00d };

        static List<StageSimulation.StageResult> neutralLead;
        static List<StageSimulation.StageResult> neutralFloor;

        [SetUp]
        public void Prepare()
        {
            if (neutralLead != null) return;

            var field = PromotionTrialFixture.FieldFromAssets();
            neutralLead = StageSimulation.Run(Horizon, field,
                new StageSimulation.Policy { NeutralizeEvolution = true });
            neutralFloor = StageSimulation.Run(Horizon, field,
                new StageSimulation.Policy { NeutralizeEvolution = true, GemsFromQuestsOnly = true });
        }

        // ------------------------------------------------------------ 모형의 정당성

        /**
         * @brief 오버레이가 **실제 시뮬레이션과 같은 답**을 낸다.
         *
         * 기본 정책(전직 있음, 지수 0.42)의 실측 여유를, 중립 정책에 그 정책이
         * 실제로 가진 배수를 얹어 재현한다. 재현이 안 되면 아래 후보 비교는
         * 전부 근거를 잃는다.
         *
         * 검사 구간이 st51부터인 것은 **전직 골드 도약 비용** 때문이다. st37~47
         * 에서는 기본 정책이 누적 7.41e12 골드를 전직에 쓰고 그만큼 화력이
         * 줄어드는데, 오버레이에는 그 항이 없다. v1.4에서 전직 비용이 0이 되면
         * 그 구간의 어긋남도 사라진다 - 지금 재현이 안 되는 것은 모형의 결함이
         * 아니라 **재현하려는 세계가 다르다**는 뜻이다.
         */
        [Test]
        public void OverlayModel_MatchesTheSimulation()
        {
            var field = PromotionTrialFixture.FieldFromAssets();
            var actual = StageSimulation.Run(Horizon, field);

            for (int stage = 51; stage <= 200; stage++)
            {
                var a = actual[stage - 1];
                var n = neutralLead[stage - 1];

                // 그 스테이지에서 기본 정책이 실제로 가진 배수로 오버레이한다
                double predicted = n.BossMargin
                    * System.Math.Pow(a.EvolutionAttack, 1d - StageCurve.EvolutionMarginExponent);
                double error = predicted / a.BossMargin;

                Assert.That(error, Is.InRange(0.95d, 1.02d), string.Format(
                    "stage {0}: 오버레이 예측 {1:F3}, 실측 {2:F3} (비 {3:F4}). "
                    + "모형이 시뮬레이션을 재현하지 못하면 지수 후보 비교가 근거를 잃는다",
                    stage, predicted, a.BossMargin, error));
            }
        }

        /**
         * @brief 생존 여유는 **목표값이라 배수에 반응하지 않는다.**
         *
         * 생존 루프가 `EffectiveHealth >= 보스 피해 x 1.15`까지만 사기 때문이다.
         * 이것이 성립하지 않으면 오버레이가 생존을 "그대로" 두는 것이 틀린
         * 가정이 되고, 지수 후보의 생존 판정이 전부 흔들린다.
         */
        [Test]
        public void SurvivalMargin_DoesNotRespondToTheEvolutionMultiplier()
        {
            var field = PromotionTrialFixture.FieldFromAssets();
            var actual = StageSimulation.Run(Horizon, field);

            for (int stage = 51; stage <= 200; stage++)
            {
                double ratio = actual[stage - 1].SurvivalMargin / neutralLead[stage - 1].SurvivalMargin;

                Assert.That(ratio, Is.InRange(0.97d, 1.03d), string.Format(
                    "stage {0}: 전직 체력 배수 x{1:F3}인데 생존 여유 비가 {2:F4}다. "
                    + "생존이 목표값이 아니라 파생값이면 오버레이 모형을 다시 세워야 한다",
                    stage, actual[stage - 1].EvolutionHealth, ratio));
            }
        }

        // ------------------------------------------------------------ 후보 비교

        /**
         * @brief 코리더는 **어떤 지수에서도** 티어 0이다.
         *
         * 첫 게이트가 st30이므로 st1~30의 배수가 1이고, 1의 어떤 거듭제곱도
         * 1이다. 지수가 코리더를 건드릴 수 있는 경로 자체가 없다는 것을
         * 못 박는다 - `EvolutionCharacterizationTests`의 비트 동일이 지수와
         * 무관하게 성립하는 근거다.
         */
        [Test]
        public void Corridor_IsTierZeroForEveryCandidate()
        {
            for (int stage = 1; stage <= 30; stage++)
            {
                Assert.AreEqual(0, PromotionBandModel.TierAtStage(stage),
                    string.Format("stage {0}: 코리더에 경지가 새어 들어왔다", stage));

                Assert.AreEqual(1d, PromotionBandModel.AttackMultiplierAtStage(stage), 0d,
                    string.Format("stage {0}: 코리더의 배수가 1이 아니다", stage));
            }

            foreach (var e in Candidates)
                Assert.AreEqual(1d, System.Math.Pow(PromotionBandModel.AttackMultiplierAtStage(30), 1d - e), 0d,
                    "지수 " + e + "에서 코리더 보정이 1이 아니다");
        }

        /**
         * @brief 후보 일곱 전부가 **가속 구간 밴드 안**이다.
         *
         * 바닥은 하한 플레이어로, 천장은 곡선 추종으로 잰다 - 33단계가 정한
         * 두 끝의 뜻 그대로다.
         */
        [Test]
        public void EveryCandidate_HoldsTheAcceleratedBand()
        {
            foreach (var e in Candidates)
            {
                var lead = PromotionBandModel.Summarize(
                    PromotionBandModel.Overlay(neutralLead, e), 31, 50);
                var floor = PromotionBandModel.Summarize(
                    PromotionBandModel.Overlay(neutralFloor, e), 31, 50);

                Assert.LessOrEqual(lead.NormalMax, AcceleratedNormalCeiling, Msg(e, "일반 천장", lead.NormalMax));
                Assert.LessOrEqual(lead.ChapterMax, AcceleratedChapterCeiling, Msg(e, "챕터 천장", lead.ChapterMax));
                Assert.LessOrEqual(lead.FinaleMax, AcceleratedFinaleCeiling, Msg(e, "피날레 천장", lead.FinaleMax));

                Assert.GreaterOrEqual(floor.NormalMin, AcceleratedNormalFloor, Msg(e, "일반 바닥", floor.NormalMin));
                Assert.GreaterOrEqual(floor.ChapterMin, AcceleratedChapterFloor, Msg(e, "챕터 바닥", floor.ChapterMin));
                Assert.GreaterOrEqual(floor.FinaleMin, AcceleratedFinaleFloor, Msg(e, "피날레 바닥", floor.FinaleMin));

                Assert.GreaterOrEqual(floor.SurvivalMin, SurvivalFloor, Msg(e, "생존 바닥", floor.SurvivalMin));
                Assert.GreaterOrEqual(lead.SurvivalMin, SurvivalFloor, Msg(e, "생존 바닥(추종)", lead.SurvivalMin));
            }
        }

        /** 심층 앞머리(st51~150)도 바닥 위에 선다 */
        [Test]
        public void EveryCandidate_HoldsTheDeepFloor()
        {
            foreach (var e in Candidates)
            {
                var floor = PromotionBandModel.Summarize(
                    PromotionBandModel.Overlay(neutralFloor, e), 51, 150);

                Assert.GreaterOrEqual(floor.NormalMin, AcceleratedNormalFloor, Msg(e, "심층 일반 바닥", floor.NormalMin));
                Assert.GreaterOrEqual(floor.ChapterMin, AcceleratedChapterFloor, Msg(e, "심층 챕터 바닥", floor.ChapterMin));
                Assert.GreaterOrEqual(floor.FinaleMin, AcceleratedFinaleFloor, Msg(e, "심층 피날레 바닥", floor.FinaleMin));
                Assert.GreaterOrEqual(floor.SurvivalMin, SurvivalFloor, Msg(e, "심층 생존", floor.SurvivalMin));
            }
        }

        /**
         * @brief 도달층 계약 - 바닥선과 리드 상한.
         *
         * 예산은 계약 그대로 **기준 플레이어가 st51에서 지평까지 가는 시간**이다.
         * v1.4에서는 기준 플레이어도 하한 플레이어도 같은 배수를 가지므로,
         * 남는 격차는 장비·동료·뽑기뿐이다.
         */
        [Test]
        public void EveryCandidate_HoldsTheReachContract()
        {
            foreach (var e in Candidates)
            {
                var lead = PromotionBandModel.Overlay(neutralLead, e);
                var floor = PromotionBandModel.Overlay(neutralFloor, e);

                double budget = PromotionBandModel.CombatSeconds(lead, 51, Horizon);
                int reached = PromotionBandModel.ReachedStage(floor, budget, 51);

                Assert.GreaterOrEqual(reached, ReachFloorStage, Msg(e, "무과금 도달층", reached));
                Assert.LessOrEqual(Horizon - reached, ReachLeadCap, Msg(e, "과금 리드(층)", Horizon - reached));
            }
        }

        /**
         * @brief **승급 체감** - 문을 돌파한 직후 실질 전투력이 얼마나 오르는가.
         *
         * 지수가 유일하게 갈리는 자리다. 밴드는 후보 일곱이 전부 통과하므로
         * (위 셋), 고를 근거는 이것뿐이다.
         *
         * 16단계의 `MinimumFeltGain`(1%)이 아니라 **5%**를 쓴다 - 귀문은 강화
         * 한 칸이 아니라 진행을 여는 관문이고, 그것을 이기고도 5%가 안 오르면
         * 그 전투는 통과 의례가 된다.
         */
        [Test]
        public void FeltGain_StaysAboveTheMilestoneFloor()
        {
            // 권장 지수는 반드시 넘어야 한다
            double worstRecommended = WorstFeltGain(PromotionBandModel.RecommendedExponent);

            Assert.GreaterOrEqual(worstRecommended, PromotionBandModel.MinimumFeltGain, string.Format(
                "권장 지수 {0:F2}: 최소 체감이 {1:P1}뿐이다. 관문을 이기고 이만큼도 "
                + "안 오르면 그 전투는 통과 의례다",
                PromotionBandModel.RecommendedExponent, worstRecommended));

            // 지수 1.00은 이득을 정확히 상쇄한다 - 체감이 0이어야 한다
            Assert.AreEqual(0d, WorstFeltGain(1d), 1e-12d,
                "지수 1.00에서 체감이 0이 아니다 - 보정의 정의가 깨졌다");

            // **이 기준이 실제로 후보를 가른다.** 전부 통과하면 자가 아니다
            int rejected = 0;
            foreach (var e in Candidates)
                if (WorstFeltGain(e) < PromotionBandModel.MinimumFeltGain) rejected++;

            Assert.Greater(rejected, 0,
                "체감 기준이 후보를 하나도 못 걸렀다 - 그 기준은 아무것도 안 재고 있다");
            Assert.Less(rejected, Candidates.Length,
                "체감 기준이 후보를 전부 걸렀다 - 기준이 너무 높다");
        }

        static double WorstFeltGain(double exponent)
        {
            double worst = double.MaxValue;
            for (int gate = 1; gate <= 6; gate++)
            {
                double gain = PromotionBandModel.FeltGainAtGate(gate, exponent);
                if (gain < worst) worst = gain;
            }
            return worst;
        }

        /**
         * @brief 권장 지수(0.00)가 **모든 계약을 통과한다.**
         *
         * 위 검사들은 후보 일곱을 훑고, 이것은 실제로 고른 값 하나를 못 박는다.
         * 권장이 바뀌면 `PromotionBandModel.RecommendedExponent` 한 줄만 고치고
         * 여기서 결과를 확인한다.
         */
        [Test]
        public void RecommendedExponent_PassesEveryContract()
        {
            double e = PromotionBandModel.RecommendedExponent;

            var lead = PromotionBandModel.Overlay(neutralLead, e);
            var floor = PromotionBandModel.Overlay(neutralFloor, e);

            var leadBand = PromotionBandModel.Summarize(lead, 31, 50);
            var floorBand = PromotionBandModel.Summarize(floor, 31, 50);
            var deepBand = PromotionBandModel.Summarize(floor, 51, 150);

            Assert.LessOrEqual(leadBand.NormalMax, AcceleratedNormalCeiling, "일반 천장");
            Assert.LessOrEqual(leadBand.ChapterMax, AcceleratedChapterCeiling, "챕터 천장");
            Assert.LessOrEqual(leadBand.FinaleMax, AcceleratedFinaleCeiling, "피날레 천장");

            Assert.GreaterOrEqual(floorBand.NormalMin, AcceleratedNormalFloor, "일반 바닥");
            Assert.GreaterOrEqual(floorBand.ChapterMin, AcceleratedChapterFloor, "챕터 바닥");
            Assert.GreaterOrEqual(floorBand.FinaleMin, AcceleratedFinaleFloor, "피날레 바닥");
            Assert.GreaterOrEqual(floorBand.SurvivalMin, SurvivalFloor, "생존 바닥");
            Assert.GreaterOrEqual(deepBand.FinaleMin, AcceleratedFinaleFloor, "심층 피날레 바닥");

            double budget = PromotionBandModel.CombatSeconds(lead, 51, Horizon);
            int reached = PromotionBandModel.ReachedStage(floor, budget, 51);

            Assert.GreaterOrEqual(reached, ReachFloorStage, "무과금 도달층 " + reached);
            Assert.LessOrEqual(Horizon - reached, ReachLeadCap, "과금 리드 " + (Horizon - reached));

            Assert.GreaterOrEqual(WorstFeltGain(e), PromotionBandModel.MinimumFeltGain,
                "권장 지수의 최소 체감이 바닥 아래다");
        }

        /**
         * @brief 지수가 낮을수록 체감이 크고 무과금 도달층이 높다 - **단조성.**
         *
         * 후보를 고를 때 "낮은 쪽이 유리하다"는 방향이 실제로 성립하는지
         * 확인한다. 뒤집히면 표를 다시 읽어야 한다.
         */
        [Test]
        public void LowerExponent_MeansMoreFeltGainAndMoreFloorReach()
        {
            double previousGain = double.MaxValue;
            int previousReach = int.MaxValue;

            foreach (var e in Candidates)
            {
                double gain = PromotionBandModel.FeltGainAtGate(6, e);
                Assert.Less(gain, previousGain, "지수 " + e + "에서 체감 단조성이 깨졌다");
                previousGain = gain;

                var lead = PromotionBandModel.Overlay(neutralLead, e);
                var floor = PromotionBandModel.Overlay(neutralFloor, e);
                double budget = PromotionBandModel.CombatSeconds(lead, 51, Horizon);
                int reached = PromotionBandModel.ReachedStage(floor, budget, 51);

                Assert.LessOrEqual(reached, previousReach, "지수 " + e + "에서 도달층 단조성이 깨졌다");
                previousReach = reached;
            }
        }

        static string Msg(double exponent, string what, double value)
        {
            return string.Format("지수 {0:F2}: {1} = {2:F3}", exponent, what, value);
        }

        static string Msg(double exponent, string what, int value)
        {
            return string.Format("지수 {0:F2}: {1} = {2}", exponent, what, value);
        }
    }
}
