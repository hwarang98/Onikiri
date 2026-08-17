using System;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 격노·폐쇄 시계 정책의 후보 비교 (승급 5.0단계 §4).
     *
     * ## 지금 무엇인가
     *
     * `BossFight.UpdateTrial`이 `Time.deltaTime`으로 돈다 - **scaled**다. 격노와
     * 폐쇄가 같은 `trialClock` 하나를 읽고, 프로젝트에서 `timeScale`을 흔드는 곳은
     * `HitStop` 하나뿐이다.
     *
     * 3단계 실기 실측: **게임 142초 = 실시간 202초, 비율 0.70.** 그래서 화면의
     * 「180초」는 전투 중 실시간 약 257초(4분 17초)다.
     *
     * ## 후보 넷과 **결론**
     *
     *   A  scaled 180 (5.0단계 전의 값)
     *   B  scaled 150   <- **채택**
     *   C  scaled 120
     *   D  unscaled     <- 기각 (아래 `GoingUnscaled_…`)
     *
     * B를 고른 근거는 `PromotionTrialCatalog.CloseSeconds` 주석에 있다 - 실측된
     * 모든 시도(최대 132초)를 그대로 담는 가장 작은 값이고, 결과를 한 칸도 바꾸지
     * 않으면서 벽시계를 43초 줄인다.
     *
     * D는 시뮬레이션으로 재지 못한다 - `HitStop`이 프레임과 타격 수에 달린
     * 런타임 현상이라 순수 계산에 그런 항이 없다. 대신 **환산**한다: unscaled 180은
     * 조작할 수 없는 시간(비율 0.30)을 제한 시간에서 빼는 구조이므로 실효 게임 시간이
     * 180 x 0.70 = **126 게임초**이고, 그 크기를 이 검사가 못 박는다.
     *
     * 그리고 그 구조는 공정성 위험이다 - 빼는 양이 **플레이어가 얼마나 많이 때렸는가**에
     * 비례한다. 다단·연격 빌드가 시간을 더 잃고, 그것은 소프트캡이 하려는 일
     * ("과잉 화력을 완만하게 줄인다")과 정반대로 **빌드 모양**에 벌점을 매기는 일이다
     * (`TrialPowerScore` 머리 주석이 타격당 캡을 금지한 것과 같은 이유).
     *
     * ## 이 스텝은 상수를 바꾸지 않는다
     *
     * 검사가 재는 것은 "후보를 골랐을 때 무엇이 깨지는가"이고, 마지막 검사가
     * 현행 상수를 그대로 못 박는다 - 5.1이 바꿀 때 그 검사가 먼저 걸려서
     * 변경이 **의도된 것**임을 적게 만든다.
     */
    public class TrialClockPolicyTests
    {
        const int Gates = 6;

        /** 후보 셋(scaled). D는 환산으로 다룬다 */
        static readonly double[] CloseCandidates = { 180d, 150d, 120d };

        /** 3단계 Android 실측: 게임 142초 = 실시간 202초 */
        const double MeasuredGamePerRealSecond = 142d / 202d;

        static PromotionTrialSimulation.Rules RulesWith(double close)
        {
            var rules = PromotionTrialFixture.DefaultRules();
            rules.CloseSeconds = close;
            return rules;
        }

        static PromotionTrialSimulation.Player MidAt(int gate)
        {
            int stage = PromotionTrialFixture.GateStages[gate - 1];
            var lead = PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage);
            var floor = PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage);

            var mid = floor;
            mid.Dps = Math.Sqrt(lead.Dps * floor.Dps);
            return mid;
        }

        static PromotionTrialSimulation.Result Run(
            PromotionTrialSimulation.Player raw, int gate, double close)
        {
            return PromotionTrialSimulation.Run(
                PromotionTrialFixture.Capped(raw, gate, PromotionTrialFixture.SoftCapExponent),
                PromotionTrialFixture.FoesForGate(gate), RulesWith(close));
        }

        static PromotionTrialSimulation.Player Scaled(
            System.Collections.Generic.List<StageSimulation.StageResult> rows, int gate, double share)
        {
            var player = PromotionTrialFixture.PlayerAt(rows, PromotionTrialFixture.GateStages[gate - 1]);
            player.Dps *= share;
            return player;
        }

        // ------------------------------------------------------------ 결과 계약

        /**
         * @brief 후보 셋 **전부**가 통과·실패 계약을 그대로 지킨다.
         *
         * 폐쇄 시간을 줄이는 것이 위험한 이유는 "통과해야 하는 플레이어가 시간에
         * 걸리는" 경우인데, 실측된 가장 느린 통과가 0.70x의 62.6초라 120초에도
         * 58초의 여유가 남는다. 그 사실을 여기서 확인한다.
         */
        [Test]
        public void EveryCloseCandidate_KeepsThePassAndFailContracts()
        {
            var floor = PromotionTrialFixture.GemFloor();
            var lead = PromotionTrialFixture.Lead();

            foreach (var close in CloseCandidates)
                for (int gate = 1; gate <= Gates; gate++)
                {
                    Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared,
                        Run(PromotionTrialFixture.PlayerAt(floor, PromotionTrialFixture.GateStages[gate - 1]), gate, close).Outcome,
                        string.Format("폐쇄 {0:F0} 문{1}: 하한이 통과하지 못했다", close, gate));

                    Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared,
                        Run(MidAt(gate), gate, close).Outcome,
                        string.Format("폐쇄 {0:F0} 문{1}: 중간 대리값이 통과하지 못했다", close, gate));

                    Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared,
                        Run(PromotionTrialFixture.PlayerAt(lead, PromotionTrialFixture.GateStages[gate - 1]), gate, close).Outcome,
                        string.Format("폐쇄 {0:F0} 문{1}: 추종이 통과하지 못했다", close, gate));

                    Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared,
                        Run(Scaled(floor, gate, 0.70d), gate, close).Outcome,
                        string.Format("폐쇄 {0:F0} 문{1}: 하한의 70%가 통과하지 못했다 - 시험이 좁아진다", close, gate));

                    Assert.AreNotEqual(PromotionTrialSimulation.Outcome.Cleared,
                        Run(Scaled(floor, gate, 0.35d), gate, close).Outcome,
                        string.Format("폐쇄 {0:F0} 문{1}: 하한의 35%가 통과했다", close, gate));

                    // 0.50x는 문1에서만 실패한다 - 파밍 민감도 표의 근거
                    bool halfCleared = Run(Scaled(floor, gate, 0.50d), gate, close).Outcome
                                       == PromotionTrialSimulation.Outcome.Cleared;
                    Assert.AreEqual(gate > 1, halfCleared, string.Format(
                        "폐쇄 {0:F0} 문{1}: 하한의 50%가 {2} - 실패하는 문의 자리가 바뀌면 "
                        + "FarmAtGateOne도 함께 옮겨야 한다", close, gate, halfCleared ? "통과했다" : "실패했다"));
                }
        }

        /**
         * @brief 정지 시도는 **어떤 후보에서도** 끝나고 격노를 본다.
         *
         * 종료 보장이 폐쇄 하나에 걸려 있지 않다는 것이 요점이다 - 격노가 먼저
         * 밀어내므로 세 후보 모두에서 유한 시간에 닫힌다.
         */
        [Test]
        public void TheStaller_TerminatesUnderEveryCandidate_AndSeesEnrage()
        {
            var floor = PromotionTrialFixture.GemFloor();

            foreach (var close in CloseCandidates)
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var result = Run(
                        PromotionTrialFixture.StallerAt(floor, PromotionTrialFixture.GateStages[gate - 1]),
                        gate, close);

                    Assert.AreNotEqual(PromotionTrialSimulation.Outcome.Cleared, result.Outcome,
                        string.Format("폐쇄 {0:F0} 문{1}: DPS 1/100 짜리가 통과했다", close, gate));
                    Assert.LessOrEqual(result.Seconds, close, string.Format(
                        "폐쇄 {0:F0} 문{1}: 정지 시도가 {2:F1}초까지 갔다", close, gate, result.Seconds));
                    Assert.Greater(result.EnrageSteps, 0, string.Format(
                        "폐쇄 {0:F0} 문{1}: 격노를 한 단계도 안 보고 끝났다", close, gate));
                }
        }

        /**
         * @brief 180 -> 120에서 결과가 바뀌는 것은 **정지 시도 문1 하나뿐이다.**
         *
         * 폐쇄 시간을 줄이는 결정의 크기가 정확히 이것이다 - 실제 플레이어의
         * 어떤 시도도 120초를 넘지 않고(가장 긴 실패가 0.35x의 98.6초), 유일하게
         * 잘리는 것이 지어낸 정지 시도의 132초다. 그 시도는 132초에 **죽던** 것이
         * 120초에 **폐쇄로** 끝난다 - 둘 다 실패이고 플레이어의 손실은 같다.
         */
        [Test]
        public void ShorteningTheCloseTo120_ChangesOnlyTheSyntheticStaller()
        {
            var floor = PromotionTrialFixture.GemFloor();
            int changed = 0;

            for (int gate = 1; gate <= Gates; gate++)
            {
                var cases = new[]
                {
                    PromotionTrialFixture.PlayerAt(floor, PromotionTrialFixture.GateStages[gate - 1]),
                    MidAt(gate),
                    PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), PromotionTrialFixture.GateStages[gate - 1]),
                    Scaled(floor, gate, 0.70d),
                    Scaled(floor, gate, 0.50d),
                    Scaled(floor, gate, 0.35d)
                };

                foreach (var player in cases)
                {
                    var atOneEighty = Run(player, gate, 180d);
                    var atOneTwenty = Run(player, gate, 120d);

                    Assert.AreEqual(atOneEighty.Outcome, atOneTwenty.Outcome, string.Format(
                        "문{0}: 실제 플레이어의 결과가 폐쇄 단축으로 바뀌었다 ({1} -> {2}, {3:F1}초)",
                        gate, atOneEighty.Outcome, atOneTwenty.Outcome, atOneEighty.Seconds));
                    Assert.AreEqual(atOneEighty.Seconds, atOneTwenty.Seconds, 1e-9d, string.Format(
                        "문{0}: 실제 플레이어의 시간이 폐쇄 단축으로 바뀌었다", gate));
                }

                var staller = PromotionTrialFixture.StallerAt(floor, PromotionTrialFixture.GateStages[gate - 1]);
                if (Run(staller, gate, 180d).Outcome != Run(staller, gate, 120d).Outcome) changed++;
            }

            Assert.AreEqual(1, changed, string.Format(
                "폐쇄 180 -> 120에서 결과가 바뀐 정지 시도가 {0}개다 - 5.0단계 실측은 "
                + "문1 하나(132초 사망 -> 120초 폐쇄)였다", changed));
        }

        // ------------------------------------------------------------ 격노 창

        /**
         * @brief 폐쇄를 줄이면 **격노가 일할 구간도 줄어든다.** 그 크기를 못 박는다.
         *
         *   폐쇄 180   단계 10  최대 x13.79  창 90초
         *   폐쇄 150   단계  7  최대 x6.27   창 60초
         *   폐쇄 120   단계  4  최대 x2.86   창 30초
         *
         * 30초·네 단계가 남는 것이 "격노가 의미 있게 작동한다"의 하한이다 -
         * 실측된 정지 시도가 격노 1~5단계에서 죽으므로 네 단계면 그 일을 한다.
         */
        [Test]
        public void ShorteningTheClose_ShrinksTheEnrageWindow_ButNeverBelowFourSteps()
        {
            double previousSteps = double.MaxValue;

            foreach (var close in CloseCandidates)
            {
                int steps = PromotionTrialCatalog.EnrageStepsAt(close);
                double window = close - PromotionTrialCatalog.EnrageSeconds;

                Assert.Greater(window, 0d, string.Format(
                    "폐쇄 {0:F0}: 격노가 시작되기 전에 문이 닫힌다 - 격노가 아무 일도 못 한다", close));
                Assert.GreaterOrEqual(steps, 4, string.Format(
                    "폐쇄 {0:F0}: 격노 단계가 {1}개뿐이다 - 실측 정지 시도가 1~5단계에서 "
                    + "죽으므로 네 단계는 남아야 한다", close, steps));
                Assert.Less(steps, previousSteps, "폐쇄를 줄였는데 격노 단계가 늘었다");
                previousSteps = steps;
            }
        }

        // ------------------------------------------------------------ unscaled 환산

        /**
         * @brief unscaled는 **이름보다 작은 예산**이고, 그 뺄셈이 빌드 모양에 달려 있다.
         *
         * 실측 비율 0.70이므로 unscaled로 바꾸면 같은 숫자가 게임 시계로는 70%만
         * 돈다 - 출시값 150에서 **105 게임초**다. 즉 후보 D는 이름을 그대로 두고도
         * 예산을 3분의 1 가까이 깎고, **그 깎이는 양이 기기의 프레임 성능과
         * 플레이어의 타격 수에 달려 있다.**
         *
         * 그 의존이 D를 기각한 이유다 - 같은 빌드가 기기에 따라 다른 제한 시간을
         * 받고, 많이 때리는 빌드(다단·연격)가 더 많이 잃는다. `TrialPowerScore`
         * 머리 주석이 타격당 캡을 금지한 이유와 같은 종류의 결함이다.
         */
        [Test]
        public void GoingUnscaled_WouldCutTheBudgetByTheHitStopShare()
        {
            double close = PromotionTrialCatalog.CloseSeconds;
            double effective = close * MeasuredGamePerRealSecond;

            Assert.Less(effective, close, string.Format(
                "unscaled 예산 {0:F1}초가 scaled 예산 {1:F0}초보다 작지 않다 - "
                + "히트스톱이 시계를 멈추지 않는다는 뜻이고, 실측 비율을 다시 재야 한다",
                effective, close));

            Assert.That(effective, Is.InRange(close * 0.68d, close * 0.72d), string.Format(
                "unscaled 실효 예산이 {0:F1}초다 - 3단계 실측 비율 0.70에서 {1:F1}초여야 한다",
                effective, close * 0.70d));

            // 화면의 숫자가 실제로 얼마인가. 보고서가 이 값을 싣는다
            double wallClock = close / MeasuredGamePerRealSecond;
            Assert.That(wallClock, Is.InRange(208d, 220d), string.Format(
                "scaled {0:F0}의 실시간 길이가 {1:F0}초다 - 비율 0.70에서 약 214초(3분 34초)다",
                close, wallClock));
        }

        // ------------------------------------------------------------ 현행 고정

        /**
         * @brief **확정된 시계 상수.** 5.0단계가 고정한 값이다.
         *
         *   격노 90초 / 10초 간격 / x1.3   유지
         *   폐쇄 **150 게임초**            180에서 내렸다
         *   소프트캡 k **0.45**            유지 (0.35 채택 안 함 · 0.60 기각)
         *   시계 기준                      `Time.deltaTime` (scaled) 유지
         *
         * 값을 지키는 검사가 아니라 **결정을 기록하게 만드는** 검사다. 이 셋 중
         * 하나라도 움직이면 여기서 먼저 걸리고, 그때 변경이 의도된 것임을 이
         * 주석에 적어야 한다.
         */
        [Test]
        public void TheShippedClockConstants_AreTheOnesStepFiveFixed()
        {
            Assert.AreEqual(90d, PromotionTrialCatalog.EnrageSeconds, 0d, "격노 시작");
            Assert.AreEqual(10d, PromotionTrialCatalog.EnrageIntervalSeconds, 0d, "격노 간격");
            Assert.AreEqual(1.3d, PromotionTrialCatalog.EnrageMultiplierPerStep, 0d, "격노 배수");
            Assert.AreEqual(150d, PromotionTrialCatalog.CloseSeconds, 0d, "폐쇄 (5.0단계에 180 -> 150)");
            Assert.AreEqual(2d, PromotionTrialCatalog.SwapSeconds, 0d, "전환");
            Assert.AreEqual(0.45d, PromotionTrialCatalog.SoftCapExponent, 0d, "소프트캡 지수");
        }

        /**
         * @brief 출시값 150에서 **압박 구간이 60게임초 · 격노 일곱 단계** 남는다.
         *
         * 폐쇄를 내린 대가가 정확히 이것이다. 격노가 90초에 시작하므로 남는 창이
         * `150 - 90 = 60초`이고, 실측 정지 시도가 격노 1~5단계에서 죽으므로 일곱
         * 단계는 그 일을 하고도 남는다.
         *
         * 120을 고르지 않은 이유가 이 숫자다 - 그쪽은 30초·네 단계로 여유가 없다.
         */
        [Test]
        public void TheShippedClose_LeavesSixtySecondsOfEnragePressure()
        {
            double window = PromotionTrialCatalog.CloseSeconds - PromotionTrialCatalog.EnrageSeconds;

            Assert.AreEqual(60d, window, 1e-9d, "격노 압박 구간(게임초)");
            Assert.AreEqual(7, PromotionTrialCatalog.EnrageStepsAt(PromotionTrialCatalog.CloseSeconds),
                "폐쇄 시각까지 오르는 격노 단계 수");
            Assert.That(PromotionTrialCatalog.EnrageMultiplierAt(PromotionTrialCatalog.CloseSeconds),
                Is.InRange(6.2d, 6.3d), "폐쇄 시각의 적 공격 배수 (x1.3^7 = 6.27)");
        }

        /**
         * @brief 출시값 150에서 **다섯 계약이 그대로 성립한다.** §4의 구현 조건이다.
         *
         * 위 `EveryCloseCandidate_…`가 후보 셋을 함께 재는 비교표라면 이쪽은
         * **출시값 하나**를 정면으로 재는 계약이다. 후보 목록이 나중에 정리돼도
         * 이 검사는 남는다.
         */
        [Test]
        public void AtTheShippedClose_EveryContractHolds()
        {
            var floor = PromotionTrialFixture.GemFloor();
            var lead = PromotionTrialFixture.Lead();
            double close = PromotionTrialCatalog.CloseSeconds;

            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];

                // 1. 정상 시도의 결과가 180일 때와 같다
                foreach (var rows in new[] { floor, lead })
                {
                    var atShipped = Run(PromotionTrialFixture.PlayerAt(rows, stage), gate, close);
                    var atOldValue = Run(PromotionTrialFixture.PlayerAt(rows, stage), gate, 180d);

                    Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, atShipped.Outcome,
                        string.Format("문{0}: 정상 시도가 통과하지 못했다", gate));
                    Assert.AreEqual(atOldValue.Seconds, atShipped.Seconds, 1e-9d, string.Format(
                        "문{0}: 폐쇄를 내리자 정상 시도의 시간이 움직였다", gate));
                }

                // 2. 0.70x 통과
                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared,
                    Run(Scaled(floor, gate, 0.70d), gate, close).Outcome,
                    string.Format("문{0}: 하한의 70%가 통과하지 못했다", gate));

                // 3. 0.35x 실패
                Assert.AreNotEqual(PromotionTrialSimulation.Outcome.Cleared,
                    Run(Scaled(floor, gate, 0.35d), gate, close).Outcome,
                    string.Format("문{0}: 하한의 35%가 통과했다", gate));

                // 4. 정지 시도도 반드시 종료
                var staller = Run(PromotionTrialFixture.StallerAt(floor, stage), gate, close);
                Assert.AreNotEqual(PromotionTrialSimulation.Outcome.Cleared, staller.Outcome,
                    string.Format("문{0}: 정지 시도가 통과했다", gate));
                Assert.LessOrEqual(staller.Seconds, close, string.Format(
                    "문{0}: 정지 시도가 폐쇄를 넘겼다 ({1:F1}초)", gate, staller.Seconds));

                // 5. 격노가 그 시도를 실제로 밀어냈다
                Assert.Greater(staller.EnrageSteps, 0, string.Format(
                    "문{0}: 정지 시도가 격노를 한 단계도 안 보고 끝났다", gate));
            }
        }
    }
}
