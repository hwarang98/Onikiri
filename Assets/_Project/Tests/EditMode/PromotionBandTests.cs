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

        /**
         * @brief **심층 지수 후보** (2.1.1단계에 아래로 넓혔다).
         *
         * 전역 보정은 지워졌으므로(A-1) 이 배열이 훑는 것은
         * `StageCurve.DeepPromotionConvergenceExponent`의 후보다. 문1~4는 어떤
         * 값에서도 안 움직이고, 갈리는 것은 문5·6과 심층 밴드뿐이다.
         *
         * 2.1단계에는 0.55에서 시작했고, 그래서 "0.55가 가장 낮은 통과값"이라는
         * 결론이 **배열의 시작점 때문에** 증명되지 않은 상태였다. 아래로
         * 넓혀 실제 하한을 찾았다 - 수학적 최소는 **0.42**이고(Affinity 시대
         * 이득이 binding, 정확한 교차점 e ~ 0.4139), 채택값 0.55는 그보다 높다.
         * 그 선택의 근거는 회귀 여유이고 §보고서에 있다.
         */
        static readonly double[] Candidates =
            { 0.00d, 0.30d, 0.38d, 0.42d, 0.45d, 0.50d, 0.55d, 0.60d, 0.65d, 1.00d };

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
         * 기본 정책의 실측 여유를, 중립 정책에 그 정책이 실제로 가진 배수를
         * 얹어 재현한다. 재현이 안 되면 아래 후보 비교는 전부 근거를 잃는다.
         *
         * ## 2단계에 세계가 하나로 합쳐졌다
         *
         * 1.6단계에는 프로덕션이 지수 0.42였으므로 이 검사가 `A^0.58`을 얹어
         * 재현해야 했고, st37~47에서는 **전직 골드 도약 비용**(누적 7.41e12)
         * 때문에 재현이 안 됐다 - 그래서 검사 구간이 st51부터였다.
         *
         * 이제 승급이 무료라 그 항이 없고, 보정도 없어서 지수가 0이다. 즉
         * 오버레이가 곱하는 것은 `A^1` 하나뿐이고, 그것은 시뮬레이션이 실제로
         * 곱하는 값과 **같은 수**다. 그래서 구간을 첫 문 뒤(st31)부터로 넓혔다 -
         * 좁혀 둘 이유였던 항이 사라졌으므로 좁혀 두면 검사가 덜 재게 된다.
         *
         * 남는 오차는 하나뿐이다. **생존은 목표값이라 체력 배수가 공짜로 오면
         * 강화 레벨을 덜 사고, 아낀 골드가 화력으로 간다** - 그 되먹임이
         * 오버레이에 없다. 아래 `SurvivalMargin_...`이 그 크기를 따로 잰다.
         */
        [Test]
        public void OverlayModel_MatchesTheSimulation()
        {
            var field = PromotionTrialFixture.FieldFromAssets();
            var actual = StageSimulation.Run(Horizon, field);
            var overlay = PromotionBandModel.Overlay(neutralLead, PromotionBandModel.RecommendedExponent);

            for (int stage = 31; stage <= 200; stage++)
            {
                var a = actual[stage - 1];
                double predicted = overlay[stage - 1].BossMargin;
                double error = predicted / a.BossMargin;

                // 오버레이가 실제로 그 스테이지의 배수를 쓰고 있는지부터 본다.
                // 티어가 어긋난 채 비만 맞으면 아무것도 못 재는 검사가 된다
                Assert.AreEqual(a.EvolutionTier, overlay[stage - 1].Tier, string.Format(
                    "stage {0}: 오버레이 티어 {1}, 실측 {2} - 게이트 표가 두 벌이 됐다",
                    stage, overlay[stage - 1].Tier, a.EvolutionTier));

                // 실측 오차가 st31~200에서 **1.0000**이다. 지수가 0이라
                // 오버레이가 곱하는 것과 시뮬레이션이 곱하는 것이 같은
                // 수이기 때문이고, 그래서 자를 0.95~1.02에서 조였다 -
                // 느슨하게 두면 되먹임이 새로 생겨도 안 잡힌다
                Assert.That(error, Is.InRange(0.995d, 1.005d), string.Format(
                    "stage {0}: 오버레이 예측 {1:F3}, 실측 {2:F3} (비 {3:F4}). "
                    + "모형이 시뮬레이션을 재현하지 못하면 지수 후보 비교가 근거를 잃는다",
                    stage, predicted, a.BossMargin, error));
            }
        }

        /**
         * @brief 생존 여유는 **거의 목표값이다** - 체력 배수를 대부분 되먹는다.
         *
         * 생존 루프가 `EffectiveHealth >= 보스 피해 x 1.15`까지만 사므로, 체력
         * 배수가 공짜로 와도 강화 레벨을 덜 사서 같은 목표에 닿는다. 그것이
         * 오버레이가 생존을 "그대로" 두는 근거다.
         *
         * ## 2단계에 자를 넓혔다 - 배수가 **계단**이 됐기 때문이다
         *
         * 1.6단계에는 비가 0.97~1.03이었다. 그때 티어는 2스테이지에 한 칸씩
         * 올라 되먹임이 매끄럽게 따라갈 수 있었다. 이제 티어는 문에서만
         * 오르므로 체력 배수가 st101에서 x1.10 계단으로 뛰고, 그 직후 구간은
         * **아직 되먹이기 전**이다 - 실측 최대 1.1000이 정확히 한 칸(x1.10)이고
         * 자리도 문 직후(st152)다.
         *
         * 방향이 안전한 쪽이라는 것이 중요하다. 오버레이는 생존을 **과소평가**
         * 하므로(실제가 더 여유롭다) 바닥 계약이 이 오차로 뚫릴 수 없다.
         */
        [Test]
        public void SurvivalMargin_DoesNotRespondToTheEvolutionMultiplier()
        {
            var field = PromotionTrialFixture.FieldFromAssets();
            var actual = StageSimulation.Run(Horizon, field);

            for (int stage = 31; stage <= 200; stage++)
            {
                double ratio = actual[stage - 1].SurvivalMargin / neutralLead[stage - 1].SurvivalMargin;

                // 상한이 체력 스텝 하나(x1.10)에 눈금 2%를 얹은 값이다.
                // 두 칸이 쌓이면(x1.21) 여기서 걸린다 - 그것은 되먹임이
                // 실제로 멈췄다는 뜻이고, 오버레이를 다시 세워야 한다
                Assert.That(ratio, Is.InRange(0.98d, 1.12d), string.Format(
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
            {
                Assert.AreEqual(1d, PromotionBandModel.DeepSteppedAtStage(30), 0d,
                    "코리더에 심층 게이트가 새어 들어왔다");

                // 프로덕션 경로도 같은 답을 낸다. 모형만 1이고 곡선이 아니면
                // 코리더 비트 불변이 검사되지 않은 채로 통과한다
                for (int stage = 1; stage <= 30; stage++)
                    Assert.AreEqual(1d, StageCurve.DeepPromotionCompensation(stage), 0d,
                        string.Format("stage {0}: 코리더에 심층 보정이 걸렸다 (지수 {1})", stage, e));
            }
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
