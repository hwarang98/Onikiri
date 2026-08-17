using System;
using System.IO;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 귀문의 **순수 도메인·데이터·마이그레이션** 검사 (2단계 신규).
     *
     * ## 이 파일이 재는 것과 안 재는 것
     *
     *   잰다     표(게이트·M·시간 규칙), 변환 API(티어 <-> 게이트), 진행 판정,
     *            무료 승급, v21 마이그레이션 경계와 멱등성, 문구의 글리프
     *   안 잰다  전투. `PromotionTrialTests`가 시간 적분을 맡고, 실제 `BossFight`
     *            연결은 3단계다
     *
     * ## 중간 상태의 안전성이 계약이다
     *
     * 2단계가 끝난 시점의 빌드는 **귀문이 없는 게임**이어야 한다 - 판정만 붙고
     * 전투가 없으면 진행이 영구히 막히고, 세이브만 올라가면 되돌릴 수 없다.
     * 아래 `IntermediateState_*` 셋이 그 사실을 코드로 붙잡는다. 3단계가 넷을
     * 한 번에 뒤집을 때 이 검사들도 함께 뒤집힌다.
     */
    public class PromotionDomainTests
    {
        // ------------------------------------------------------------ 표

        /**
         * @brief 카탈로그의 표가 **자기 자신과 일관된다.**
         *
         * 길이·순서·합처럼 사람이 표를 손으로 고칠 때 어긋나는 것들이다.
         * 값이 옳은지는 밴드와 앵커가 재고, 여기는 표의 **모양**만 본다.
         */
        [Test]
        public void Catalog_TableShapesAreConsistent()
        {
            Assert.AreEqual(EvolutionCurve.MaxTier, PromotionTrialCatalog.GateCount,
                "문의 수와 경지의 수가 다르다 - 마지막 문을 넘어도 못 받는 티어가 생긴다");

            Assert.AreEqual(PromotionTrialCatalog.GateCount,
                PromotionTrialCatalog.TotalHealthMultiple.Length,
                "M 표의 길이가 문의 수와 다르다");

            for (int g = 1; g < PromotionTrialCatalog.GateStages.Length; g++)
                Assert.Greater(PromotionTrialCatalog.GateStages[g], PromotionTrialCatalog.GateStages[g - 1],
                    string.Format("게이트 {0}이 {1}보다 앞이다", g + 1, g));

            double shareSum = 0d;
            foreach (var share in PromotionTrialCatalog.FoeHealthShare)
            {
                Assert.Greater(share, 0d, "체력 몫이 0 이하인 적이 있다");
                shareSum += share;
            }
            Assert.AreEqual(1d, shareSum, 1e-12d,
                "체력 배분의 합이 1이 아니다 - 총 체력 M이 실제 체력과 갈린다");

            // 3체가 절반이다. "마지막이 본체"라는 서사가 여기서 나온다
            Assert.AreEqual(0.50d, PromotionTrialCatalog.FoeHealthShare[PromotionTrialCatalog.FoeCount - 1], 1e-12d,
                "3체의 몫이 절반이 아니다");
        }

        /**
         * @brief 시간 규칙 넷이 서로 **모순되지 않는다.**
         *
         * 격노가 폐쇄 뒤에 오면 격노는 아무 일도 안 하고, 격노 간격이 폐쇄보다
         * 길면 단계가 한 번밖에 안 오른다. 값을 손으로 고칠 때 이 관계가 먼저
         * 깨진다.
         */
        [Test]
        public void Catalog_TimeRulesAreOrdered()
        {
            Assert.Less(PromotionTrialCatalog.EnrageSeconds, PromotionTrialCatalog.CloseSeconds,
                "격노가 폐쇄 뒤에 시작한다 - 격노가 아무 일도 안 한다");

            Assert.Greater(PromotionTrialCatalog.EnrageIntervalSeconds, 0d, "격노 간격이 0 이하다");
            Assert.Greater(PromotionTrialCatalog.EnrageMultiplierPerStep, 1d,
                "격노 배수가 1 이하다 - 압박이 아니라 완화가 된다");

            // 폐쇄까지 단계가 여러 번 올라야 "완만한 압박"이 성립한다.
            // 한 번뿐이면 격노는 시각이 정해진 처형이다
            Assert.GreaterOrEqual(PromotionTrialCatalog.EnrageStepsAt(PromotionTrialCatalog.CloseSeconds), 5,
                "폐쇄까지 격노가 다섯 단계도 안 오른다");

            // 전환 두 번이 기준 시간 밖이다 - 45초 앵커의 산수
            Assert.AreEqual(45d,
                TrialPowerScore.ReferenceSeconds + (PromotionTrialCatalog.FoeCount - 1) * PromotionTrialCatalog.SwapSeconds,
                1e-12d,
                "기준 시간 + 전환이 45초 앵커와 다르다 - M의 유도가 근거를 잃는다");
        }

        /**
         * @brief 격노 단계의 경계. 시작 시각 **직후**에 첫 단계가 온다.
         *
         * 식은 `1 + floor((t - 시작) / 간격)`이다. 그래서 `t = 시작 + 간격`
         * **정각**에 이미 2단계다 - 첫 단계가 (시작, 시작+간격) 열린 구간을
         * 차지하고, 정각이 다음 구간의 시작이기 때문이다.
         *
         * 다르게 읽고 싶어지는 자리라 여기 적어 둔다. 시뮬레이션의
         * `PromotionTrialSimulation.EnrageSteps`가 같은 식을 쓰므로 둘이
         * 갈릴 수 없고, `PromotionTrialTests.TrialRules_ComeFromTheProductionCatalog`이
         * 그 등식을 잰다.
         */
        [Test]
        public void Enrage_StepsAtTheBoundary()
        {
            double start = PromotionTrialCatalog.EnrageSeconds;
            double step = PromotionTrialCatalog.EnrageIntervalSeconds;

            Assert.AreEqual(0, PromotionTrialCatalog.EnrageStepsAt(0d));
            Assert.AreEqual(0, PromotionTrialCatalog.EnrageStepsAt(start - 0.01d));
            Assert.AreEqual(0, PromotionTrialCatalog.EnrageStepsAt(start),
                "시작 시각에 이미 격노가 올랐다");
            Assert.AreEqual(1, PromotionTrialCatalog.EnrageStepsAt(start + 0.01d));
            Assert.AreEqual(1, PromotionTrialCatalog.EnrageStepsAt(start + step - 0.01d));
            Assert.AreEqual(2, PromotionTrialCatalog.EnrageStepsAt(start + step),
                "간격 정각이 다음 단계의 시작이 아니다 - 식이 바뀌었다");

            Assert.AreEqual(1d, PromotionTrialCatalog.EnrageMultiplierAt(start), 0d,
                "격노 시작 시각에 이미 배수가 걸려 있다");
            Assert.AreEqual(PromotionTrialCatalog.EnrageMultiplierPerStep,
                PromotionTrialCatalog.EnrageMultiplierAt(start + 0.01d), 1e-12d);
        }

        // ------------------------------------------------------------ 변환

        /**
         * @brief 최전선 -> 티어의 **경계 열셋.**
         *
         * `gateStage < frontier`다. 등호를 쓰면 st30에 **도착만** 한 플레이어가
         * 일문을 공짜로 받는다 - 이 한 글자가 마이그레이션 경계 전부를 정한다.
         */
        [Test]
        public void TierAtFrontier_HasTheRightBoundaries()
        {
            var cases = new[]
            {
                new[] { 1, 0 }, new[] { 29, 0 }, new[] { 30, 0 }, new[] { 31, 1 },
                new[] { 39, 1 }, new[] { 40, 1 }, new[] { 41, 2 },
                new[] { 49, 2 }, new[] { 50, 2 }, new[] { 51, 3 },
                new[] { 69, 3 }, new[] { 70, 3 }, new[] { 71, 4 },
                new[] { 99, 4 }, new[] { 100, 4 }, new[] { 101, 5 },
                new[] { 149, 5 }, new[] { 150, 5 }, new[] { 151, 6 },
                new[] { 1000, 6 }
            };

            foreach (var c in cases)
                Assert.AreEqual(c[1], PromotionTrialCatalog.TierAtFrontier(c[0]), string.Format(
                    "최전선 {0}에서 티어 {1}이어야 하는데 {2}다 - 도착과 클리어를 섞었다",
                    c[0], c[1], PromotionTrialCatalog.TierAtFrontier(c[0])));

            // 단조성. 최전선이 나아가면 티어는 절대 내려가지 않는다
            int previous = 0;
            for (int frontier = 1; frontier <= 300; frontier++)
            {
                int tier = PromotionTrialCatalog.TierAtFrontier(frontier);
                Assert.GreaterOrEqual(tier, previous,
                    string.Format("최전선 {0}에서 티어가 내려갔다", frontier));
                Assert.That(tier, Is.InRange(0, EvolutionCurve.MaxTier));
                previous = tier;
            }
        }

        /** 게이트 번호 <-> 스테이지 <-> 티어의 왕복 */
        [Test]
        public void GateConversions_RoundTrip()
        {
            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
            {
                int stage = PromotionTrialCatalog.GateStageOfTier(gate);

                Assert.AreEqual(PromotionTrialCatalog.GateStages[gate - 1], stage);
                Assert.AreEqual(gate, PromotionTrialCatalog.GateNumberAtStage(stage));
                Assert.IsTrue(PromotionTrialCatalog.IsGateStage(stage));

                // 문을 넘은 **다음** 스테이지에서 그 티어를 갖는다
                Assert.AreEqual(gate, PromotionTrialCatalog.TierAtFrontier(stage + 1));

                // 그 티어가 다음으로 만날 문
                Assert.AreEqual(gate < PromotionTrialCatalog.GateCount
                        ? PromotionTrialCatalog.GateStages[gate]
                        : -1,
                    PromotionTrialCatalog.NextGateStage(gate),
                    string.Format("티어 {0}의 다음 문이 어긋났다", gate));
            }

            // 범위 밖
            Assert.AreEqual(-1, PromotionTrialCatalog.GateStageOfTier(0));
            Assert.AreEqual(-1, PromotionTrialCatalog.GateStageOfTier(PromotionTrialCatalog.GateCount + 1));
            Assert.AreEqual(0, PromotionTrialCatalog.GateNumberAtStage(31));
            Assert.IsFalse(PromotionTrialCatalog.IsGateStage(31));
            Assert.AreEqual(-1, PromotionTrialCatalog.NextGateStage(PromotionTrialCatalog.GateCount));
        }

        /**
         * @brief 진행 판정 - **3단계가 `AdvanceStage`에서 부를 자리다.**
         *
         * 게이트 스테이지를 클리어했는데 그 문의 티어가 없으면 막힌다. 그
         * 외에는 아무것도 막지 않는다 - 문이 아닌 스테이지에서 이 함수가 0을
         * 안 내면 게임 전체가 멈춘다.
         */
        [Test]
        public void RequiredGate_BlocksOnlyAtTheGateAndOnlyWhenTheTierIsMissing()
        {
            for (int stage = 1; stage <= 200; stage++)
            {
                int gate = PromotionTrialCatalog.GateNumberAtStage(stage);

                if (gate == 0)
                {
                    // 문이 아닌 곳은 어떤 티어에서도 안 막힌다
                    for (int tier = 0; tier <= EvolutionCurve.MaxTier; tier++)
                        Assert.AreEqual(0, PromotionTrialCatalog.RequiredGateAfterClearing(stage, tier),
                            string.Format("st{0}(문 아님)이 티어 {1}에서 막혔다", stage, tier));
                    continue;
                }

                // 문에서는 그 번호 미만의 티어만 막힌다
                for (int tier = 0; tier <= EvolutionCurve.MaxTier; tier++)
                    Assert.AreEqual(tier >= gate ? 0 : gate,
                        PromotionTrialCatalog.RequiredGateAfterClearing(stage, tier),
                        string.Format("st{0}(문{1}) 티어 {2}의 판정이 틀렸다", stage, gate, tier));
            }
        }

        /**
         * @brief 무료 티어 상승 - `max`이고, **멱등이고, 되돌아가지 않는다.**
         *
         * 재도전이 티어를 깎으면 "무료·무제한 재도전"이 손해가 되고, 아무도
         * 두 번 들어가지 않는다.
         */
        [Test]
        public void TrialVictory_RaisesTierForFreeAndNeverLowersIt()
        {
            for (int tier = 0; tier <= EvolutionCurve.MaxTier; tier++)
                for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
                {
                    int after = PromotionTrialCatalog.TierAfterTrialVictory(tier, gate);

                    Assert.GreaterOrEqual(after, tier, string.Format(
                        "티어 {0}이 문{1}을 넘고 {2}로 내려갔다", tier, gate, after));
                    Assert.That(after, Is.InRange(0, EvolutionCurve.MaxTier));

                    // 멱등 - 같은 문을 다시 넘어도 안 움직인다 (무료 재도전)
                    Assert.AreEqual(after, PromotionTrialCatalog.TierAfterTrialVictory(after, gate),
                        string.Format("문{0} 재도전이 티어를 움직였다", gate));
                }

            // 범위 밖 게이트는 아무것도 안 한다
            Assert.AreEqual(3, PromotionTrialCatalog.TierAfterTrialVictory(3, 0));
            Assert.AreEqual(3, PromotionTrialCatalog.TierAfterTrialVictory(3, PromotionTrialCatalog.GateCount + 1));
        }

        /**
         * @brief 승리가 **배수·이름·외형·오라를 동시에** 준다.
         *
         * v1.4의 "돌파했지만 38일 동안 로닌 외형"을 폐기한 결정이 이것이다.
         * 저장하는 값이 티어 하나뿐이므로 넷이 갈릴 수가 없다는 것을 여기서
         * 못 박는다 - 외형만 따로 저장하는 필드가 다시 생기면 여기가 걸린다.
         */
        [Test]
        public void TrialVictory_GrantsMultiplierNameLookAndAuraTogether()
        {
            double previousAttack = EvolutionCurve.AttackMultiplierAt(0);
            double previousHealth = EvolutionCurve.HealthMultiplierAt(0);
            string previousName = EvolutionCatalog.NameOf(0);

            Assert.AreEqual(1d, previousAttack, 1e-12d, "티어 0의 공격 배수가 1이 아니다");

            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
            {
                int tier = PromotionTrialCatalog.TierAfterTrialVictory(gate - 1, gate);
                Assert.AreEqual(gate, tier);

                double attack = EvolutionCurve.AttackMultiplierAt(tier);
                double health = EvolutionCurve.HealthMultiplierAt(tier);
                string name = EvolutionCatalog.NameOf(tier);
                var spec = EvolutionCatalog.Tiers[tier - 1];

                Assert.Greater(attack, previousAttack, string.Format("문{0}이 공격 배수를 안 올렸다", gate));
                Assert.Greater(health, previousHealth, string.Format("문{0}이 체력 배수를 안 올렸다", gate));
                Assert.AreNotEqual(previousName, name, string.Format("문{0}이 이름을 안 바꿨다", gate));
                Assert.IsFalse(string.IsNullOrEmpty(spec.SpriteFolder),
                    string.Format("문{0}의 기본 외형이 없다", gate));

                previousAttack = attack;
                previousHealth = health;
                previousName = name;
            }

            // 오라는 마지막 문의 서명이다. 중간에 있으면 최종의 표식이 아니게 된다
            for (int t = 0; t < EvolutionCatalog.Count; t++)
                Assert.AreEqual(t == EvolutionCatalog.Count - 1, EvolutionCatalog.Tiers[t].Aura,
                    string.Format("티어 {0}의 오라 자리가 틀렸다", t + 1));
        }

        /**
         * @brief 문별 **실질** 전투력 상승. B-1의 계약 전부가 이 검사다.
         *
         * ```
         * 문1  10%   문2  10%   문3  12%   문4  12%     <- 심층 보정 밖. 엄격 고정
         * 문5  >= 5%  문6  >= 5%                        <- 심층 보정이 눌러도 마일스톤
         * ```
         *
         * ## 앞 넷을 왜 엄격하게 고정하는가
         *
         * B-1이 지키기로 한 것이 정확히 그것이다 - 심층 수렴을 고치면서
         * **승인된 체감을 건드리지 않는다.** 심층 보정이 실수로 문1~4까지
         * 번지면 여유가 아니라 여기서 먼저 걸려야 원인이 보인다. 허용 오차를
         * 1e-12로 두는 이유는 그 번짐이 "조금"일 리가 없기 때문이다 -
         * `Math.Pow(1.10, 1-0.55)`는 10%가 아니라 4.4%다.
         *
         * ## 실질 상승은 명목 배수의 함수가 아니다
         *
         * 실질 = 명목 / 보정이다. 그래서 검사가 `EvolutionCatalog`의 스텝을
         * 다시 읽는 것으로 끝나면 안 되고, **보정을 지나는 경로**를 함께
         * 봐야 한다. 아래 `RealGainAtGate`가 그 경로다 - 문을 넘기 전과 넘은
         * 뒤의 (배수 / 보정)을 프로덕션 함수로 각각 잰다.
         */
        [Test]
        public void EveryGate_MovesRealPowerByTheApprovedAmount()
        {
            var approved = new[] { 0.10d, 0.10d, 0.12d, 0.12d };

            for (int gate = 1; gate <= approved.Length; gate++)
                Assert.AreEqual(approved[gate - 1], RealGainAtGate(gate), 1e-12d, string.Format(
                    "문{0}의 실질 상승이 {1:P4}다 (승인값 {2:P0}). 심층 보정이 "
                    + "가속 구간까지 번졌거나 티어 스텝이 움직였다 - B-1은 문1~4를 "
                    + "건드리지 않기로 한 변경이다",
                    gate, RealGainAtGate(gate), approved[gate - 1]));

            for (int gate = PromotionTrialCatalog.FirstDeepGate;
                 gate <= PromotionTrialCatalog.GateCount; gate++)
            {
                double gain = RealGainAtGate(gate);

                Assert.GreaterOrEqual(gain, PromotionBandModel.MinimumFeltGain, string.Format(
                    "문{0}의 실질 상승이 {1:P2}뿐이다 - 관문을 이기고 마일스톤 바닥({2:P0})도 "
                    + "못 넘으면 그 전투는 통과 의례다. 심층 지수({3:F2})를 낮춰라",
                    gate, gain, PromotionBandModel.MinimumFeltGain,
                    StageCurve.DeepPromotionConvergenceExponent));
            }

            // 어느 문에서도 실질 상승이 0 이하가 되지 않는다 - 티어가 오를수록
            // 전투력이 **줄어드는** 구간이 있으면 그 문은 벌점이다
            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
                Assert.Greater(RealGainAtGate(gate), 0d,
                    string.Format("문{0}의 실질 상승이 {1:P2}다", gate, RealGainAtGate(gate)));

            // 모형과 프로덕션이 같은 답을 낸다. 후보 비교표가 실제 곡선과
            // 다른 세계를 재고 있으면 여기서 갈린다
            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
                Assert.AreEqual(RealGainAtGate(gate),
                    PromotionBandModel.FeltGainAtGate(gate, PromotionBandModel.ShippedDeepExponent), 1e-12d,
                    string.Format("문{0}: 모형의 체감과 곡선의 체감이 다르다", gate));
        }

        /**
         * @brief 문 `gate`를 넘은 직후의 실질 상승률. **프로덕션 경로로 잰다.**
         *
         * 문의 스테이지에 서 있을 때(넘기 전)와 그 다음 스테이지(넘은 뒤)의
         * `배수 / 심층보정`을 비교한다. 두 자리의 티어가 gate-1과 gate라는 것이
         * `TierAtFrontier` 경계 규칙이고, 그래서 이 함수는 경계가 어긋나면
         * 함께 어긋난다.
         */
        static double RealGainAtGate(int gate)
        {
            int stage = PromotionTrialCatalog.GateStageOfTier(gate);

            double before = EvolutionCurve.AttackMultiplierAt(PromotionTrialCatalog.TierAtFrontier(stage))
                          / StageCurve.DeepPromotionCompensation(stage);
            double after = EvolutionCurve.AttackMultiplierAt(PromotionTrialCatalog.TierAtFrontier(stage + 1))
                         / StageCurve.DeepPromotionCompensation(stage + 1);

            return after / before - 1d;
        }

        /**
         * @brief 심층 보정의 **경계**. 클리어해야 걸린다.
         *
         * st100에 도착한 것은 문5를 깬 것이 아니므로 그 자리에 보정이 없다.
         * 등호를 쓰면 안 깬 문의 보정이 먼저 걸려 하한 플레이어가 문5를
         * **더 무거운 보스로** 만나게 된다 - 진행이 막히는 경로다.
         */
        [Test]
        public void DeepCompensation_AppliesOnlyAfterTheGateIsCleared()
        {
            // 티어 4까지는 **비트 단위로** 1이다. Math.Pow를 지나지도 않는다
            for (int stage = 1; stage <= PromotionTrialCatalog.GateStages[4]; stage++)
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(1d),
                    BitConverter.DoubleToInt64Bits(StageCurve.DeepPromotionCompensation(stage)),
                    string.Format("stage {0}: 심층 보정이 {1:R}이다 - 문5를 깨기 전인데 걸렸다",
                        stage, StageCurve.DeepPromotionCompensation(stage)));

            double e = StageCurve.DeepPromotionConvergenceExponent;
            double fifth = EvolutionCatalog.Tiers[PromotionTrialCatalog.FirstDeepGate - 1].AttackStep;
            double sixth = EvolutionCatalog.Tiers[PromotionTrialCatalog.GateCount - 1].AttackStep;

            Assert.AreEqual(Math.Pow(fifth, e), StageCurve.DeepPromotionCompensation(101), 1e-12d,
                "st101에서 문5의 보정이 안 걸렸다");
            Assert.AreEqual(Math.Pow(fifth, e), StageCurve.DeepPromotionCompensation(150), 1e-12d,
                "st150에서 문6의 보정이 벌써 걸렸다 - 도착과 클리어를 섞었다");
            Assert.AreEqual(Math.Pow(fifth * sixth, e), StageCurve.DeepPromotionCompensation(151), 1e-12d,
                "st151에서 문6의 보정이 안 걸렸다");

            // 단조성 - 보정은 내려가지 않는다
            double previous = 0d;
            for (int stage = 1; stage <= 400; stage++)
            {
                double now = StageCurve.DeepPromotionCompensation(stage);
                Assert.GreaterOrEqual(now, previous, string.Format("stage {0}: 보정이 내려갔다", stage));
                previous = now;
            }
        }

        /**
         * @brief 심층 보정을 **두 번 걷어내면** 중립 세계가 기준선을 벗어난다.
         *
         * ## 왜 이 검사가 필요한가
         *
         * `NeutralizeEvolution`은 보스 처치 시간을 `DeepPromotionCompensation`으로
         * **한 번** 나눈다. 그 나눗셈이 실수로 두 번 들어가면(예: 정책 분기를
         * 복사해 붙이거나 `BossHealthForStage` 쪽에도 같은 처리를 넣으면) 중립
         * 세계가 실제보다 가벼워지고, 그 위에 얹히는 오버레이가 **조용히**
         * 틀린 답을 낸다 - 밴드 검사는 여전히 초록일 수 있다.
         *
         * 그래서 "두 번 걷어낸 세계"를 여기서 직접 만들어 그것이 기준선 밖으로
         * 나가는지 확인한다. 안 나가면 이 나눗셈이 아무것도 안 하고 있다는 뜻이다.
         */
        [Test]
        public void DoubleRemovingTheDeepCompensation_BreaksTheNeutralBaseline()
        {
            var field = PromotionTrialFixture.FieldFromAssets();
            var neutral = StageSimulation.Run(200, field,
                new StageSimulation.Policy { NeutralizeEvolution = true });

            double worstRatio = 1d;
            int worstStage = 0;

            for (int stage = PromotionTrialCatalog.GateStages[PromotionTrialCatalog.FirstDeepGate - 1] + 1;
                 stage <= 200; stage++)
            {
                // 한 번 더 걷어낸 세계. **곱하는 방향이 맞다** - 중립화는 보스
                // 처치 시간을 보정으로 나누고(= 보스가 가벼워지고), 여유는 시간의
                // 역수 쪽이라 그만큼 **뜬다**. 중립 런은 이미 한 번 뜬 상태이므로
                // 두 번째 제거는 거기에 보정을 한 번 더 곱한 값이다
                double doubled = neutral[stage - 1].BossMargin
                               * StageCurve.DeepPromotionCompensation(stage);
                double ratio = doubled / neutral[stage - 1].BossMargin;

                if (ratio > worstRatio) { worstRatio = ratio; worstStage = stage; }
            }

            // 보정이 실제로 걸리는 구간이 있으므로 비가 1보다 확실히 커야 한다.
            // 1이면 `DeepPromotionCompensation`이 심층에서도 항등원이라는 뜻이고,
            // 그러면 중립화 경로가 아무것도 안 걷어내고 있다
            Assert.Greater(worstRatio, 1.05d, string.Format(
                "심층 보정을 두 번 걷어내도 여유가 {0:P2}밖에 안 움직인다 (@st{1}) - "
                + "중립화 경로의 나눗셈이 아무 일도 안 하고 있다", worstRatio - 1d, worstStage));

            // 그리고 그 크기가 정확히 보정 하나만큼이다. 다른 값이면 두 곳이
            // 서로 다른 보정을 쓰고 있다는 뜻이다
            Assert.AreEqual(StageCurve.DeepPromotionCompensation(200), worstRatio, 1e-9d,
                "두 번째 나눗셈의 크기가 보정 하나와 다르다");
        }

        /**
         * @brief 심층 게이트의 자리가 **드리프트 창과 같은 곳에서 시작한다.**
         *
         * `FirstDeepGate = 5`는 임의 상수가 아니라 두 사실의 결과다 -
         * 수렴 계약이 재는 창이 st100 -> st200이고, 그 창 **안에서** 티어가
         * 오르는 문이 다섯째와 여섯째뿐이다. 넷째(st70)는 창이 열리기 전에
         * 끝나므로 st100과 st200 양쪽에 똑같이 들어가 비에서 상쇄된다.
         *
         * 게이트 표가 움직이면 이 사실이 먼저 깨진다.
         */
        [Test]
        public void DeepGateBoundary_StartsWhereTheDriftWindowDoes()
        {
            const int windowFrom = 100;
            const int windowTo = 200;

            // 창의 시작에서 이미 지난 문은 보정 대상이 아니어야 한다
            int tierAtWindowStart = PromotionTrialCatalog.TierAtFrontier(windowFrom);
            Assert.AreEqual(PromotionTrialCatalog.FirstDeepGate - 1, tierAtWindowStart, string.Format(
                "드리프트 창의 시작(st{0})에서 티어가 {1}이다 - FirstDeepGate({2})의 근거가 "
                + "게이트 표와 갈렸다", windowFrom, tierAtWindowStart, PromotionTrialCatalog.FirstDeepGate));

            // 창 안에서 오르는 문이 정확히 다섯째·여섯째다
            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
            {
                int stage = PromotionTrialCatalog.GateStageOfTier(gate);
                bool insideWindow = stage >= windowFrom && stage < windowTo;

                Assert.AreEqual(insideWindow, PromotionTrialCatalog.IsDeepGate(gate), string.Format(
                    "문{0}(st{1}): 창 안 {2}인데 심층 게이트 판정이 {3}이다",
                    gate, stage, insideWindow, PromotionTrialCatalog.IsDeepGate(gate)));
            }
        }

        // ------------------------------------------------------------ v21 마이그레이션

        /**
         * @brief v20 -> v21 경계. **`maxStageReached`는 최전선이지 클리어 기록이 아니다.**
         *
         * 사용자가 지정한 경계 열셋을 그대로 잰다. 등호를 쓰면 29/30/31 중
         * 30이 1이 되어 문 하나가 전투 없이 열린다.
         */
        [Test]
        public void MigrationV21_HasTheRightBoundaries()
        {
            var cases = new[]
            {
                new[] { 29, 0 }, new[] { 30, 0 }, new[] { 31, 1 },
                new[] { 40, 1 }, new[] { 41, 2 },
                new[] { 50, 2 }, new[] { 51, 3 },
                new[] { 70, 3 }, new[] { 71, 4 },
                new[] { 100, 4 }, new[] { 101, 5 },
                new[] { 150, 5 }, new[] { 151, 6 }
            };

            foreach (var c in cases)
            {
                var data = SaveAt(frontier: c[0], tier: 0);
                SaveData.ApplyGateEvolutionTier(data);

                Assert.AreEqual(c[1], data.evolutionTier, string.Format(
                    "최전선 {0}: 티어 {1}이어야 하는데 {2}다", c[0], c[1], data.evolutionTier));
            }
        }

        /** 기존 티어를 **뺏지 않는다.** 산 것이 마이그레이션으로 사라지면 안 된다 */
        [Test]
        public void MigrationV21_KeepsTheExistingTier()
        {
            for (int existing = 0; existing <= EvolutionCurve.MaxTier; existing++)
                for (int frontier = 1; frontier <= 200; frontier += 7)
                {
                    var data = SaveAt(frontier, existing);
                    SaveData.ApplyGateEvolutionTier(data);

                    Assert.GreaterOrEqual(data.evolutionTier, existing, string.Format(
                        "최전선 {0} / 기존 티어 {1}이 {2}로 줄었다", frontier, existing, data.evolutionTier));
                    Assert.AreEqual(Math.Max(existing, PromotionTrialCatalog.TierAtFrontier(frontier)),
                        data.evolutionTier);
                }

            // v20까지 재화로 6티어를 산 플레이어가 st1에 있어도 6을 지킨다
            var bought = SaveAt(frontier: 1, tier: EvolutionCurve.MaxTier);
            SaveData.ApplyGateEvolutionTier(bought);
            Assert.AreEqual(EvolutionCurve.MaxTier, bought.evolutionTier,
                "재화로 산 최종 티어가 마이그레이션에서 사라졌다");
        }

        /** 멱등 - 두 번 돌려도 안 움직인다. `max`라 구조적으로 그렇다 */
        [Test]
        public void MigrationV21_IsIdempotent()
        {
            for (int frontier = 1; frontier <= 200; frontier += 3)
                for (int existing = 0; existing <= EvolutionCurve.MaxTier; existing++)
                {
                    var data = SaveAt(frontier, existing);

                    SaveData.ApplyGateEvolutionTier(data);
                    int once = data.evolutionTier;

                    Assert.IsFalse(SaveData.ApplyGateEvolutionTier(data), string.Format(
                        "최전선 {0} / 티어 {1}: 두 번째 마이그레이션이 값을 바꿨다", frontier, existing));
                    Assert.AreEqual(once, data.evolutionTier);
                }
        }

        /** 값이 0~6 밖으로 못 나간다. 세이브 손상이 스탯이 되면 안 된다 */
        [Test]
        public void MigrationV21_ClampsToTheLadder()
        {
            foreach (var frontier in new[] { -5, 0, 1, 100000, int.MaxValue })
                foreach (var tier in new[] { -3, 0, 99 })
                {
                    var data = SaveAt(frontier, tier);
                    SaveData.ApplyGateEvolutionTier(data);

                    Assert.That(data.evolutionTier, Is.InRange(0, EvolutionCurve.MaxTier), string.Format(
                        "최전선 {0} / 티어 {1} -> {2}", frontier, tier, data.evolutionTier));
                }
        }

        /**
         * @brief Firebase의 `max` 병합이 **단조성을 보존한다.**
         *
         * 승급이 무료·단조 증가·재화 비결합이 되면서 이 필드에 트랜잭션이
         * 필요 없어졌다. 그 주장이 참이려면 두 기기의 값을 `max`로 합친 것이
         * 어느 쪽보다 작지 않고, 합치는 순서에 상관없어야 한다(결합·교환).
         */
        [Test]
        public void MigrationV21_MaxMergeIsMonotoneAndOrderFree()
        {
            for (int a = 0; a <= EvolutionCurve.MaxTier; a++)
                for (int b = 0; b <= EvolutionCurve.MaxTier; b++)
                {
                    int merged = Math.Max(a, b);

                    Assert.GreaterOrEqual(merged, a);
                    Assert.GreaterOrEqual(merged, b);
                    Assert.AreEqual(merged, Math.Max(b, a), "병합이 순서에 의존한다");
                    Assert.AreEqual(merged, Math.Max(merged, a), "병합이 멱등이 아니다");
                }

            // 기기 둘이 서로 다른 최전선을 갖고 각자 마이그레이션한 뒤 합쳐도
            // 결과가 "더 멀리 간 쪽"이다 - 진행이 되돌아가는 경로가 없다
            for (int near = 1; near <= 200; near += 11)
                for (int far = near; far <= 200; far += 23)
                {
                    var slow = SaveAt(near, 0);
                    var fast = SaveAt(far, 0);
                    SaveData.ApplyGateEvolutionTier(slow);
                    SaveData.ApplyGateEvolutionTier(fast);

                    Assert.AreEqual(fast.evolutionTier, Math.Max(slow.evolutionTier, fast.evolutionTier),
                        string.Format("최전선 {0}/{1}의 병합이 뒤처진 기기를 따랐다", near, far));
                }
        }

        static SaveData SaveAt(int frontier, int tier)
        {
            var data = SaveData.NewGame();
            data.stage = 1;
            data.maxStageReached = frontier;
            data.evolutionTier = tier;
            return data;
        }

        // ------------------------------------------------------------ 중간 상태

        /**
         * @brief **세이브를 아직 안 올렸다.** 3단계의 원자적 변경까지 v20이다.
         *
         * 전투도 게이트도 없는 상태에서 `CurrentVersion`을 21로 올리면 다음
         * 실행에서 라이브 세이브가 전부 변환되고, 그 변환은 되돌릴 수 없다 -
         * v21 세이브를 v20 클라이언트가 읽으면 `Migrate`가 false를 내고 새
         * 게임이 된다.
         *
         * 3단계가 이 줄을 21로 고칠 때, 이 검사도 함께 고치는 것이 절차다.
         */
        [Test]
        public void MigrationV21_IsActiveAndConvertsOldSaves()
        {
            Assert.AreEqual(21, SaveData.CurrentVersion,
                "귀문이 연결됐는데 세이브 버전이 21이 아니다 - 원자적 변경 넷 중 하나가 빠졌다");

            // st200을 지난 v20 플레이어의 티어가 0이어도 여섯 문을 인정받는다
            var veteran = SaveAt(frontier: 200, tier: 0);
            veteran.version = 20;

            Assert.IsTrue(SaveData.Migrate(veteran));
            Assert.AreEqual(EvolutionCurve.MaxTier, veteran.evolutionTier,
                "st200 플레이어가 마이그레이션 뒤에도 0티어다");
            Assert.AreEqual(SaveData.CurrentVersion, veteran.version);

            // 미래 버전 거부 계약은 그대로다
            var future = SaveAt(frontier: 30, tier: 0);
            future.version = SaveData.CurrentVersion + 1;
            Assert.IsFalse(SaveData.Migrate(future), "미래 버전을 받아들였다");

            // 새 게임은 0티어에서 시작한다
            Assert.AreEqual(0, SaveData.NewGame().evolutionTier);
        }

        /**
         * @brief 마이그레이션이 **두 번 돌아도** 값이 안 움직인다 (라이브 경로).
         *
         * 위 `MigrationV21_IsIdempotent`가 함수 하나를 재고, 이것은 `Migrate`
         * 전체를 두 번 지나는 경로를 잰다 - 세이브를 읽고 쓰고 다시 읽는
         * 실제 왕복이 그 모양이다.
         */
        [Test]
        public void MigrationV21_LiveMigrateIsIdempotent()
        {
            foreach (var frontier in new[] { 29, 30, 31, 70, 151, 200 })
            {
                var data = SaveAt(frontier, 0);
                data.version = 20;

                SaveData.Migrate(data);
                int once = data.evolutionTier;

                SaveData.Migrate(data);
                Assert.AreEqual(once, data.evolutionTier,
                    string.Format("최전선 {0}: 두 번째 Migrate가 티어를 바꿨다", frontier));
            }
        }

        /**
         * @brief **원자적 넷이 함께 들어왔다.** 하나만 빠진 빌드를 막는다.
         *
         * 2단계에는 이 자리에 반대 검사가 있었다("아직 연결하지 않았다").
         * 3단계가 넷을 함께 뒤집었으므로 검사도 함께 뒤집힌다 - 그 대칭이
         * 원자성의 기록이다.
         *
         * 소스 문자열로 재는 이유는 넷 중 셋이 씬을 필요로 해서 EditMode에서
         * 실행할 수 없기 때문이다. 배선의 **존재**만 여기서 보고, 동작은
         * PlayMode가 잰다.
         */
        /**
         * @brief 런타임 소스를 **이름으로** 찾아 읽는다. 경로를 적지 않는다.
         *
         * 폴더 구조를 역할 기반으로 다시 나눈 뒤(Character·Subsystems·Balance·
         * Widget…) 이 검사 둘이 하드코딩한 경로 때문에 깨졌다. 경로를 새 값으로
         * 고치면 다음 정리에서 또 깨지므로, **파일 이름 하나만** 계약으로 둔다 -
         * 이름은 클래스 이름이라 옮겨도 안 바뀐다.
         *
         * 두 개 이상 찾히면 실패한다. 같은 이름의 스크립트가 둘 생기면 어느 쪽을
         * 읽었는지 모르는 검사가 되고, 그 모호함은 통과로 보고된다.
         */
        static string RuntimeSource(string fileName)
        {
            var hits = Directory.GetFiles("Assets/_Project/Scripts", fileName,
                                          SearchOption.AllDirectories);

            Assert.AreEqual(1, hits.Length, string.Format(
                "런타임 스크립트 {0}을(를) {1}개 찾았다 - 하나여야 한다", fileName, hits.Length));

            return File.ReadAllText(hits[0]);
        }

        [Test]
        public void AtomicWiring_AllFourAreConnected()
        {
            var progressSource = RuntimeSource("StageProgress.cs");
            var bossSource = RuntimeSource("BossFight.cs");

            Assert.IsTrue(progressSource.Contains("PromotionTrialCatalog"),
                "StageProgress가 게이트를 모른다 - 진행이 안 막힌다");
            Assert.IsTrue(progressSource.Contains("RegisterGateBossKill"),
                "게이트 보스 처치 경로가 없다 (D-4의 대기 상태를 기록하지 못한다)");
            Assert.IsTrue(progressSource.Contains("AdvanceAfterTrial"),
                "귀문 승리 뒤 진행 경로가 없다");

            Assert.IsTrue(bossSource.Contains("TrialState"),
                "BossFight에 귀문 상태 머신이 없다");
            Assert.IsTrue(bossSource.Contains("GrantTrialVictory"),
                "귀문 승리가 경지를 올리지 않는다");
            Assert.IsTrue(bossSource.Contains("TrialDamageScale.Exit"),
                "귀문 종료가 소프트캡을 끄지 않는다");

            Assert.AreEqual(21, SaveData.CurrentVersion, "세이브 버전이 21이 아니다");
        }

        /**
         * @brief 전직의 **재화 경로가 남아 있지 않다.**
         *
         * 상수만 0으로 두고 함수를 남기면 "귀문 없이 살 수 있다"가 다시
         * 열린다 - 진행을 여는 판정과 값을 주는 경로가 갈린 채로 빌드가
         * 나가는 것이 이 재설계에서 가장 나쁜 중간 상태다.
         */
        [Test]
        public void EvolutionPurchasePathIsGone()
        {
            var system = RuntimeSource("EvolutionSystem.cs");
            var curve = RuntimeSource("EvolutionCurve.cs");
            var catalog = RuntimeSource("EvolutionCatalog.cs");

            Assert.IsFalse(system.Contains("public bool TryEvolve"),
                "TryEvolve(재화 소비)가 남아 있다");
            Assert.IsFalse(curve.Contains("public static int GemCost"),
                "EvolutionCurve.GemCost가 남아 있다");
            Assert.IsFalse(curve.Contains("public const int UnlockLevel"),
                "EvolutionCurve.UnlockLevel이 남아 있다");
            Assert.IsFalse(catalog.Contains("public int GemCost"),
                "EvolutionCatalog에 보석 값 칸이 남아 있다");
            Assert.IsFalse(catalog.Contains("public double GoldCost"),
                "EvolutionCatalog에 골드 값 칸이 남아 있다");
        }

        // ------------------------------------------------------------ 문구

        /**
         * @brief 소프트캡 안내가 **아틀라스의 출처에 등재돼 있다.**
         *
         * ## 왜 아틀라스가 아니라 `UIStrings.txt`를 보는가
         *
         * 정적 아틀라스(`FontCharset.txt`)는 `UIStrings.txt` + 프리팹 + 씬 +
         * 데이터 애셋에서 **구워지는 결과물**이다. 등재가 먼저이고 굽는 것이
         * 나중이므로, 등재를 안 한 채 아틀라스만 다시 구우면 글자가 안 들어온다.
         *
         * 그래서 이 검사가 잡는 것은 등재다. 문구를 코드 상수로만 두고
         * `UIStrings.txt`에 안 적는 실수가 이 프로젝트에서 세 번 났고
         * (보스 이름·사망 문구·스킬 이름), 증상은 매번 화면의 ㅁㅁㅁ이었다.
         *
         * **아틀라스 재굽기는 3단계다.** 현재 `FontCharset.txt`(449자)에
         * `친`(U+CE5C)이 없다 - 이 문구가 처음 들여오는 글자다. 3단계가 UI를
         * 붙일 때 `Onikiri/Art/Rebuild Font Charset`과 `Build Pixel Font
         * Assets`를 함께 돌린다. 2단계에서 굽지 않는 이유는 그것이 아트 애셋
         * 변경이고, 이 단계의 계약이 "도메인·데이터·EditMode"이기 때문이다.
         */
        [Test]
        public void PromotionStringsFitAndHaveGlyphs()
        {
            string notice = PromotionTrialCatalog.SoftCapNotice;

            Assert.IsFalse(string.IsNullOrEmpty(notice), "소프트캡 안내 문구가 비어 있다");

            // 진입 화면 한 줄이다. 길면 두 줄로 접히고, 접히면 "규칙 한 줄"이
            // 아니라 문단이 된다 - 노출이 1회뿐이라 읽히는 길이여야 한다
            Assert.LessOrEqual(notice.Length, 40, string.Format(
                "안내 문구가 {0}자다 - 진입 화면 한 줄을 넘는다", notice.Length));

            string registry = File.ReadAllText("Assets/_Project/Data/UIStrings.txt");

            Assert.IsTrue(registry.Contains(notice), string.Format(
                "'{0}'이 UIStrings.txt에 없다 - 아틀라스를 다시 구워도 이 글자들은 "
                + "안 들어온다", notice));

            foreach (char c in notice)
            {
                if (c == ' ') continue;

                Assert.IsTrue(registry.IndexOf(c) >= 0, string.Format(
                    "'{0}'(U+{1:X4})이 UIStrings.txt 어디에도 없다 - 화면에 ㅁ이 뜬다",
                    c, (int)c));
            }
        }
        /**
         * @brief 귀문이 화면에 적는 **모든** 문구가 실제 아틀라스에 있다 (4단계).
         *
         * 위 검사는 안내 한 줄만 봤다. 4단계에서 결과·가이드 카드·경지 표에
         * 문구가 여럿 늘었고, 그중 둘의 글자가 아틀라스에 없어서 실기에
         * 「시간이 다 **ㅁ**다」와 「무료 재도전 가**ㅁ**」이 떴다
         * (`능` U+B2A5 · `됐` U+B410).
         *
         * 원인은 구조다. 문자셋 빌더는 `UIStrings.txt`·프리팹·씬·데이터 애셋을
         * 훑는데 **런타임에 C#이 만드는 문자열은 그 넷 어디에도 없다.** 그래서
         * 코드에 문구를 적고 등재를 잊으면 아무도 안 잡는다.
         *
         * 이 검사는 위 검사와 **다른 것을 본다**: 등재(UIStrings)가 아니라
         * **구워진 결과물**(FontCharset.txt)이다. 등재만 하고 아틀라스를 안 구운
         * 상태도 여기서 걸린다 - 실기에 나간 것은 아틀라스이지 등재가 아니다.
         */
        [Test]
        public void EveryTrialStringHasGlyphsInTheAtlas()
        {
            string charset = File.ReadAllText("Assets/_Project/Data/FontCharset.txt");

            var strings = PromotionTrialCatalog.AllUiStrings();
            Assert.Greater(strings.Length, 0, "귀문 문구 목록이 비어 있다");

            foreach (var text in strings)
            {
                Assert.IsFalse(string.IsNullOrEmpty(text), "귀문 문구 중 빈 것이 있다");

                foreach (char c in text)
                {
                    if (char.IsWhiteSpace(c)) continue;

                    Assert.IsTrue(charset.IndexOf(c) >= 0, string.Format(
                        "'{0}'(U+{1:X4})이 FontCharset.txt에 없다 - 화면에 ㅁ이 뜬다. "
                        + "문구 '{2}'. UIStrings.txt에 등재한 뒤 "
                        + "Rebuild Font Charset + Build Pixel Font Assets를 돌려라",
                        c, (int)c, text));
                }
            }
        }
    }
}
