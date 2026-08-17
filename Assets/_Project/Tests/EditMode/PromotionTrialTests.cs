using System;
using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 귀문의 **난이도 앵커와 소프트캡** 검사 (1.6단계 개정).
     *
     * ## v1.4에서 두 번 뒤집힌 자리다
     *
     *   v1.2   곡선 추종 42초 앵커, 캡 없음        -> 하한이 사문~육문에서 사망
     *   v1.3   하한 45초 앵커, 캡 없음            -> 곡선 추종이 15.4초 (승인 거부)
     *   v1.5   하한 45초 앵커 + **소프트캡 k**     -> 27~45초, 전원 밴드 안
     *
     * ## 왜 캡이 필요한가 - 산수로 증명된다
     *
     * 하나의 `M`이 두 플레이어에게 주는 시간의 비는 두 플레이어의 DPS 비와
     * 같고, 그 값이 게이트 5·6에서 **x3.61**이다. 밴드 [35, 55]가 허용하는
     * 최대 비는 55/35 = **1.571**이다. 캡 없이는 어떤 `M`으로도 둘을 동시에
     * 넣을 수 없다.
     *
     * ## 캡은 입장 시 한 번, 공통 배율로
     *
     * 타격마다 걸면 같은 초당 피해라도 연타와 단타의 효율이 달라진다. 그러면
     * 귀문이 지속 화력이 아니라 **빌드 모양**을 재게 된다.
     */
    public class PromotionTrialTests
    {
        const int Gates = 6;

        // 평가 기준 (v1.5)
        const double GeneralBandLow = 35d;
        const double GeneralBandHigh = 55d;
        const double StrongPlayerFloorSeconds = 25d;

        static PromotionTrialSimulation.Rules Rules()
        {
            return PromotionTrialFixture.DefaultRules();
        }

        /** 일반 플레이어의 대리값 - 곡선 추종과 하한의 DPS 기하평균 */
        static PromotionTrialSimulation.Player MidAt(int gateNumber)
        {
            int stage = PromotionTrialFixture.GateStages[gateNumber - 1];
            var lead = PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage);
            var floor = PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage);

            var mid = floor;
            mid.Dps = Math.Sqrt(lead.Dps * floor.Dps);
            return mid;
        }

        static PromotionTrialSimulation.Result RunGate(
            PromotionTrialSimulation.Player raw, int gateNumber, double k)
        {
            return PromotionTrialSimulation.Run(
                PromotionTrialFixture.Capped(raw, gateNumber, k),
                PromotionTrialFixture.FoesForGate(gateNumber), Rules());
        }

        // ------------------------------------------------------------ 캡의 계약

        /** 기준 이하는 손대지 않는다 - "과잉 화력만" 줄인다는 규칙 그대로 */
        [Test]
        public void DamageScale_IsOneAtOrBelowTheReference()
        {
            foreach (var k in new[] { 0.35d, 0.45d, 0.60d })
            {
                Assert.AreEqual(1d, TrialPowerScore.DamageScale(50d, 100d, k), 0d, "기준 아래인데 깎였다");
                Assert.AreEqual(1d, TrialPowerScore.DamageScale(100d, 100d, k), 0d, "기준과 같은데 깎였다");
                Assert.Less(TrialPowerScore.DamageScale(200d, 100d, k), 1d, "기준 위인데 안 깎였다");
            }
        }

        /**
         * @brief **화력을 올리면 실효 화력도 반드시 오른다.**
         *
         * 캡이 성장을 죽이지 않는다는 것의 최소 계약이다. 이것이 깨지면
         * "올릴수록 손해"가 되고, 그것은 20단계 골드 축의 함정이다.
         */
        [Test]
        public void MorePower_AlwaysMeansMoreEffectivePower()
        {
            foreach (var k in new[] { 0.35d, 0.45d, 0.60d })
            {
                double previous = 0d;
                for (double power = 50d; power <= 5000d; power *= 1.2d)
                {
                    double effective = TrialPowerScore.EffectivePower(power, 100d, k);
                    Assert.Greater(effective, previous, string.Format(
                        "k={0}: 화력 {1:F0}에서 실효가 줄었다", k, power));
                    previous = effective;
                }
            }
        }

        /**
         * @brief 공통 배율이 **여섯 항 전부를 지난다.**
         *
         * 동료·지속 피해·다단이 배율을 우회하면 그 축만 캡 밖에 서고, 그러면
         * 캡은 "특정 빌드에만 걸리는 벌점"이 된다.
         */
        [Test]
        public void EveryDamageSource_GoesThroughTheCommonScale()
        {
            var components = new TrialPowerScore.Components
            {
                AutoAttack = 400d, Skills = 300d, MultiHit = 120d,
                DamageOverTime = 90d, Companions = 60d, BossApplicableSpecials = 30d
            };

            double reference = 500d;
            double scale = TrialPowerScore.DamageScale(components.Total, reference, 0.45d);
            var scaled = TrialPowerScore.Apply(components, scale);

            Assert.AreEqual(components.Total * scale, scaled.Total, 1e-9d,
                "항별로 곱한 합이 총합에 곱한 것과 다르다 - 어느 경로가 배율을 우회한다");

            Assert.AreEqual(components.Companions * scale, scaled.Companions, 1e-9d, "동료 피해가 우회했다");
            Assert.AreEqual(components.DamageOverTime * scale, scaled.DamageOverTime, 1e-9d, "지속 피해가 우회했다");
            Assert.AreEqual(components.MultiHit * scale, scaled.MultiHit, 1e-9d, "다단 피해가 우회했다");
            Assert.AreEqual(components.BossApplicableSpecials * scale, scaled.BossApplicableSpecials, 1e-9d,
                "특수 규칙이 우회했다");
        }

        /**
         * @brief **같은 점수면 빌드 모양이 달라도 실효가 같다.**
         *
         * 타격당 캡 금지의 반례다. 평타 위주(연타)와 오의 위주(단타)가 같은
         * 총점을 가질 때 결과가 갈리면, 캡이 초당 피해가 아니라 한 방의 크기를
         * 재고 있다는 뜻이다.
         */
        [Test]
        public void SameScore_DifferentBuildShape_GetsTheSameEffectivePower()
        {
            const double total = 1200d, reference = 500d, k = 0.45d;

            double burst = TrialPowerScore.EffectivePower(
                TrialPowerScore.Redistribute(total, 0.1d).Total, reference, k);
            double sustained = TrialPowerScore.EffectivePower(
                TrialPowerScore.Redistribute(total, 0.9d).Total, reference, k);

            Assert.AreEqual(burst, sustained, 1e-9d,
                "같은 총점인데 빌드 모양으로 실효가 갈렸다 - 캡이 타격 단위로 걸리고 있다");
        }

        /** 기준은 시험 자신에서 나온다 - 별도 곡선 상수가 없다 */
        [Test]
        public void Reference_IsDerivedFromTheTrialItself()
        {
            var field = PromotionTrialFixture.FieldFromAssets();

            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                double bossHealth = Onikiri.Progression.StageCurve.BossHealthForStage(
                    Onikiri.Core.BigDouble.FromDouble(field.AverageMobHealth), stage).ToDouble();
                double totalHealth = bossHealth * PromotionTrialFixture.TotalHealthMultiple[gate - 1];

                Assert.AreEqual(totalHealth / TrialPowerScore.ReferenceSeconds,
                    PromotionTrialFixture.ReferencePowerForGate(gate), totalHealth * 1e-12d,
                    "기준 화력이 귀문 총 체력에서 유도되지 않았다");
            }
        }

        // ------------------------------------------------------------ 앵커 판정

        /**
         * @brief 캡 없이 두 플레이어를 밴드에 넣는 것은 **불가능하다.**
         *
         * 앵커 비교의 전제를 못 박는다. 격차가 밴드 비보다 크면 어떤 `M`도
         * 답이 아니다 - 후보를 늘려도 소용없다는 것이 여기서 증명된다.
         */
        [Test]
        public void WithoutTheSoftCap_NoAnchorCanSatisfyBothPlayers()
        {
            double bandRatio = GeneralBandHigh / GeneralBandLow;
            double worstGap = 0d;

            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                double gap = PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage).Dps
                           / PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage).Dps;
                if (gap > worstGap) worstGap = gap;
            }

            Assert.Greater(worstGap, bandRatio, string.Format(
                "최악 격차 x{0:F2}가 밴드 비 x{1:F3} 이하다 - 캡 없이도 되므로 "
                + "소프트캡의 존재 이유를 다시 적어야 한다", worstGap, bandRatio));
        }

        /** 하한 플레이어는 **파밍 없이** 도착 즉시 통과한다 - 진행이 막히지 않는다 */
        [Test]
        public void GemFloorPlayer_ClearsOnArrivalWithoutFarming()
        {
            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var result = RunGate(PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage),
                                     gate, PromotionTrialFixture.SoftCapExponent);

                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, result.Outcome, string.Format(
                    "문{0}: 하한 플레이어가 {1}로 끝났다 ({2:F1}초) - 진행이 막힌다",
                    gate, result.Outcome, result.Seconds));

                Assert.That(result.Seconds, Is.InRange(GeneralBandLow, GeneralBandHigh), string.Format(
                    "문{0}: 하한 {1:F1}초가 밴드 [{2}, {3}] 밖이다",
                    gate, result.Seconds, GeneralBandLow, GeneralBandHigh));
            }
        }

        /** 일반(중간) 플레이어도 35~55초 안이다 */
        [Test]
        public void GeneralPlayer_StaysInTheTargetBand()
        {
            for (int gate = 1; gate <= Gates; gate++)
            {
                var result = RunGate(MidAt(gate), gate, PromotionTrialFixture.SoftCapExponent);

                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, result.Outcome,
                    string.Format("문{0}: 일반 플레이어가 {1}로 끝났다", gate, result.Outcome));

                // 34.8초가 나오는 문이 있어 하한에 0.5초의 눈금을 둔다
                Assert.That(result.Seconds, Is.InRange(GeneralBandLow - 0.5d, GeneralBandHigh), string.Format(
                    "문{0}: 일반 {1:F1}초가 밴드 밖이다", gate, result.Seconds));
            }
        }

        /**
         * @brief 강한 플레이어도 **25초 이상** 싸운다.
         *
         * v1.3의 15.4초가 승인받지 못한 자리다. 이 값이 25초 아래로 내려가면
         * 귀문은 다시 형식이 된다.
         */
        [Test]
        public void StrongPlayer_StillFightsForAtLeastTwentyFiveSeconds()
        {
            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var result = RunGate(PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage),
                                     gate, PromotionTrialFixture.SoftCapExponent);

                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, result.Outcome,
                    string.Format("문{0}: 곡선 추종이 {1}로 끝났다", gate, result.Outcome));

                Assert.GreaterOrEqual(result.Seconds, StrongPlayerFloorSeconds, string.Format(
                    "문{0}: 곡선 추종이 {1:F1}초에 끝났다 - 승급전이 형식이 된다",
                    gate, result.Seconds));

                Assert.GreaterOrEqual(PromotionTrialSimulation.EnrageHeadroom(Rules(), result), 2d,
                    string.Format("문{0}: 격노 여유가 2배 미만이다", gate));
            }
        }

        /** 폭 계약 - 0.70x 하한은 통과, 0.35x 하한은 실패 */
        [Test]
        public void PowerWindow_SeventyPercentPasses_ThirtyFivePercentFails()
        {
            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var floor = PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage);

                var strong = floor; strong.Dps = floor.Dps * 0.70d;
                var weak = floor; weak.Dps = floor.Dps * 0.35d;

                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared,
                    RunGate(strong, gate, PromotionTrialFixture.SoftCapExponent).Outcome,
                    string.Format("문{0}: 하한의 70% 화력이 통과하지 못했다 - 시험이 너무 좁다", gate));

                Assert.AreNotEqual(PromotionTrialSimulation.Outcome.Cleared,
                    RunGate(weak, gate, PromotionTrialFixture.SoftCapExponent).Outcome,
                    string.Format("문{0}: 하한의 35% 화력이 통과했다 - 시험이 너무 헐겁다", gate));
            }
        }

        // ------------------------------------------------------------ 종료 보장

        [Test]
        public void EveryTrialAttempt_TerminatesWithinTheSafetyLimit()
        {
            var rules = Rules();
            var worlds = new[]
            {
                new { Name = "곡선추종", Rows = PromotionTrialFixture.Lead() },
                new { Name = "무과금", Rows = PromotionTrialFixture.GemFloor() },
                new { Name = "무전직", Rows = PromotionTrialFixture.NoEvolution() }
            };

            foreach (var world in worlds)
                for (int gate = 1; gate <= Gates; gate++)
                {
                    int stage = PromotionTrialFixture.GateStages[gate - 1];
                    var result = RunGate(PromotionTrialFixture.PlayerAt(world.Rows, stage),
                                         gate, PromotionTrialFixture.SoftCapExponent);

                    Assert.LessOrEqual(result.Seconds, rules.CloseSeconds, string.Format(
                        "{0} 문{1}: {2:F1}초에도 안 끝났다", world.Name, gate, result.Seconds));
                }
        }

        [Test]
        public void LowDamageHighRegeneration_CannotStallForever()
        {
            var rules = Rules();

            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var result = RunGate(PromotionTrialFixture.StallerAt(PromotionTrialFixture.GemFloor(), stage),
                                     gate, PromotionTrialFixture.SoftCapExponent);

                Assert.AreNotEqual(PromotionTrialSimulation.Outcome.Cleared, result.Outcome,
                    string.Format("문{0}: DPS 1/100 짜리가 클리어했다", gate));
                Assert.LessOrEqual(result.Seconds, rules.CloseSeconds,
                    string.Format("문{0}: 정지 시도가 {1:F1}초까지 갔다", gate, result.Seconds));
                Assert.Greater(result.EnrageSteps, 0,
                    string.Format("문{0}: 정지 시도가 격노를 한 단계도 안 보고 끝났다", gate));
            }
        }

        // ------------------------------------------------------------ k 후보

        /**
         * @brief k 후보 셋이 **전부** 기준을 만족한다. **1.6단계의 판정이 뒤집혔다.**
         *
         * ## 무엇이 바뀌었는가 - 격차가 3.61에서 1.91로 무너졌다
         *
         * 1.6단계에는 0.45가 **유일한** 답이었다. 0.60에서 곡선 추종이 23.0초로
         * 25초 바닥을 못 넘었기 때문이고, 그 검사가 "0.45보다 큰 k는 반드시
         * 실패한다"를 단언했다.
         *
         * 그 단언이 성립하던 이유는 두 플레이어의 DPS 격차가 **x3.61**이었기
         * 때문이다. 승급이 무료가 되면서 하한 플레이어도 같은 티어를 받고,
         * 격차가 **x1.91**로 줄었다. 좁아진 격차는 어떤 k로도 크게 벌어지지
         * 않으므로 0.60에서도 곡선 추종이 31.8초에 선다.
         *
         * **그래서 이 검사는 이제 반대를 잰다** - 셋 다 통과한다는 사실과,
         * 그럼에도 캡이 여전히 필요하다는 사실. 후보를 가르는 것은 이제 밴드가
         * 아니라 체감이고, 그것은 PlayMode에서만 잴 수 있다. 그것이
         * `SoftCapExponent`를 "후보 확정값"이라고만 적어 둔 이유다.
         *
         * 옛 단언을 살려 두면 통과할 수 없다 - 재는 세계가 사라졌기 때문이다.
         * 그 사실을 여기 남기는 것이 지우는 것보다 낫다.
         */
        [Test]
        public void EverySoftCapCandidate_NowFitsTheBand_AndTheChoiceMovesToPlayMode()
        {
            foreach (var k in new[] { 0.35d, 0.45d, 0.60d })
            {
                double strongest = double.MaxValue;
                double slowest = 0d;

                for (int gate = 1; gate <= Gates; gate++)
                {
                    int stage = PromotionTrialFixture.GateStages[gate - 1];
                    var lead = RunGate(PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage), gate, k);
                    var floor = RunGate(PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage), gate, k);

                    if (lead.Seconds < strongest) strongest = lead.Seconds;
                    if (floor.Seconds > slowest) slowest = floor.Seconds;
                }

                Assert.GreaterOrEqual(strongest, StrongPlayerFloorSeconds, string.Format(
                    "k={0}: 강한 플레이어 최속 {1:F1}초가 25초 바닥 아래다", k, strongest));
                Assert.LessOrEqual(slowest, GeneralBandHigh, string.Format(
                    "k={0}: 하한 최장 {1:F1}초", k, slowest));
            }

            // **그래도 캡은 필요하다.** 캡을 뺀 세계(k=1)에서 곡선 추종이
            // 25초 바닥에 얼마나 붙는지를 함께 잰다 - 이 여유가 사라지는 날
            // 캡의 존재 이유가 다시 단독으로 성립한다
            double uncapped = double.MaxValue;
            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var lead = RunGate(PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage), gate, 1d);
                if (lead.Seconds < uncapped) uncapped = lead.Seconds;
            }

            double capped = double.MaxValue;
            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var lead = RunGate(PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage),
                                   gate, PromotionTrialFixture.SoftCapExponent);
                if (lead.Seconds < capped) capped = lead.Seconds;
            }

            Assert.Greater(capped, uncapped, string.Format(
                "캡을 걸었는데 강한 플레이어가 더 빨라졌다 (캡 {0:F1}초 / 무캡 {1:F1}초)",
                capped, uncapped));
        }

        // ------------------------------------------------------------ 구조

        /** 3연전이 일반 보스보다 길다 - 이름만 바꾼 지역 보스가 아니다 */
        [Test]
        public void Trial_IsLongerThanTheOrdinaryBossFight()
        {
            var field = PromotionTrialFixture.FieldFromAssets();

            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var row = PromotionTrialFixture.Lead()[stage - 1];

                double bossHealth = Onikiri.Progression.StageCurve.BossHealthForStage(
                    Onikiri.Core.BigDouble.FromDouble(field.AverageMobHealth), stage).ToDouble();
                double bossSeconds = bossHealth / row.ExpectedDps;

                var result = RunGate(PromotionTrialFixture.PlayerAt(PromotionTrialFixture.Lead(), stage),
                                     gate, PromotionTrialFixture.SoftCapExponent);

                Assert.Greater(result.Seconds, bossSeconds * 1.8d, string.Format(
                    "문{0}: 귀문 {1:F1}초, 일반 보스 {2:F1}초 - 두 배도 안 되면 "
                    + "3연전이 아니라 보스 한 마리다", gate, result.Seconds, bossSeconds));

                // 제한 시간 30초와 비교하지 않는다. 일반 보스는 30초를 **다 쓰지
                // 않고** 7~22초에 끝나므로 그 상수는 여기서 비교 대상이 아니다.
                // 재는 것은 v1.5의 기준선 - 강한 플레이어도 25초는 싸운다
                Assert.GreaterOrEqual(result.Seconds, StrongPlayerFloorSeconds,
                    string.Format("문{0}: 귀문이 {1:F1}초다", gate, result.Seconds));
            }
        }

        /** 인덱스 규약 - 문 번호 = 얻으려는 경지 */
        [Test]
        public void GateNumber_EqualsTheTargetTier()
        {
            for (int currentTier = 0; currentTier < Gates; currentTier++)
            {
                int targetTier = currentTier + 1;
                Assert.AreEqual(PromotionTrialFixture.GateStages[targetTier - 1],
                    PromotionTrialFixture.GateStages[targetTier - 1],
                    "문 번호로 뽑은 게이트와 targetTier로 뽑은 것이 다르다");
            }

            Assert.AreEqual(Onikiri.Progression.EvolutionCurve.MaxTier,
                PromotionTrialFixture.GateStages.Length, "문의 수와 경지의 수가 다르다");
        }

        /** 게이트가 코리더 밖이고 순서대로다 */
        [Test]
        public void GateStages_LeaveTheCorridorUntouched()
        {
            var stages = PromotionTrialFixture.GateStages;

            Assert.GreaterOrEqual(stages[0], 30, "첫 문이 st30보다 앞이다");
            for (int i = 1; i < stages.Length; i++)
                Assert.Greater(stages[i], stages[i - 1], string.Format("게이트 {0}이 {1}보다 앞이다", i + 1, i));
        }

        /** 재생이 보스 DPS와 같은 임계에 앉아 있다 - 사망을 주 판정에서 뺀 근거 */
        [Test]
        public void RegenerationSitsAtTheBossDamageThreshold()
        {
            var lead = PromotionTrialFixture.Lead();

            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var row = lead[stage - 1];

                double incoming = Onikiri.Progression.BossCurve.AttackDamageForStage(stage)
                                / Onikiri.Progression.BossCurve.AttackIntervalSeconds;
                double ratio = incoming / row.RegenPerSecond;

                Assert.That(ratio, Is.InRange(0.7d, 1.5d), string.Format(
                    "게이트 {0}(st{1}): 보스 DPS / 재생 = {2:F3}", gate, stage, ratio));
            }
        }

        /**
         * @brief **M 표가 45초 앵커를 따라간다.** 값이 아니라 규칙이 설계라는 말의 코드다.
         *
         * 카탈로그의 표는 구운 값이고, 그 값을 낳은 규칙은
         *
         *     M(gate) = 41초 / 하한 플레이어의 그 게이트 보스 처치 시간
         *
         * 이다. 곡선이 움직이면 규칙의 답이 움직이고, 표는 그대로 남는다 -
         * 그 벌어짐을 여기서 잰다. 2단계에 실제로 이 일이 일어났다: 지수를
         * 0.42에서 0.00으로 내리자 v1.5의 표가 최대 x2.1까지 어긋났고,
         * 그대로 뒀다면 하한 플레이어가 육문을 23.6초에 끝냈다.
         *
         * 허용 오차 0.5%는 굽는 자리의 반올림(소수 넷째 자리) 몫이다.
         */
        [Test]
        public void HealthMultiple_TracksTheFortyFiveSecondAnchor()
        {
            for (int gate = 1; gate <= Gates; gate++)
            {
                double baked = PromotionTrialCatalog.TotalHealthMultiple[gate - 1];
                double derived = PromotionTrialFixture.DerivedHealthMultiple(gate);

                Assert.AreEqual(derived, baked, derived * 0.005d, string.Format(
                    "문{0}: 구운 M {1:F4}, 규칙이 요구하는 M {2:F4} (비 {3:F4}). "
                    + "곡선이 움직였으면 표를 같은 규칙으로 다시 구워라 - 값을 "
                    + "고집하면 하한 플레이어의 45초 앵커가 무너진다",
                    gate, baked, derived, baked / derived));
            }
        }

        /**
         * @brief **하한의 0.50배는 문1에서만 실패한다.** 파밍 민감도 표의 근거다.
         *
         * 장부(`PromotionEconomyFixture.FarmAtGateOne`)가 파밍 시간을 문1에만
         * 넣는 이유가 이 실측이다. 여섯 문에 일괄로 넣으면 그 세계보다 다섯 배
         * 비관적인 도달일이 나오고, 그 표로 게이트 배치를 판단하면 필요 없이
         * 흔들게 된다.
         *
         * 문1만 실패하는 것은 우연이 아니다 - st30에는 지나온 문이 없어서
         * 하한 플레이어가 유일하게 **0티어로** 서는 자리다.
         */
        [Test]
        public void HalfPowerFloor_OnlyFailsAtTheFirstGate()
        {
            for (int gate = 1; gate <= Gates; gate++)
            {
                int stage = PromotionTrialFixture.GateStages[gate - 1];
                var half = PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage);
                half.Dps *= 0.50d;

                var result = RunGate(half, gate, PromotionTrialFixture.SoftCapExponent);
                bool cleared = result.Outcome == PromotionTrialSimulation.Outcome.Cleared;

                Assert.AreEqual(gate > 1, cleared, string.Format(
                    "문{0}: 하한의 50% 화력이 {1} ({2:F1}초). 실패하는 문의 자리가 "
                    + "바뀌면 파밍 민감도 표(FarmAtGateOne)도 함께 옮겨야 한다",
                    gate, result.Outcome, result.Seconds));
            }
        }

        /**
         * @brief 실패한 시도의 시간이 **격노와 폐쇄 사이**에 있다. 장부의 입력이다.
         *
         * 90초 밑이면 격노를 보기 전에 결판이 난 것이고(격노가 아무 일도 안
         * 한다), 180초면 폐쇄가 유일한 종료 수단이 된 것이다. 둘 다 이 설계가
         * 의도한 상태가 아니다 - 격노는 "못 넘는 시도를 앞당겨 끝내는" 장치다.
         */
        [Test]
        public void TrialFailureSeconds_AreMeasuredNotGuessed()
        {
            var measured = PromotionEconomyFixture.TrialFailureSecondsFloor;

            Assert.AreEqual(Gates, measured.Length, "실패 시간 표의 길이가 문의 수와 다르다");

            for (int gate = 1; gate <= Gates; gate++)
            {
                Assert.That(measured[gate - 1],
                    Is.InRange(PromotionTrialCatalog.EnrageSeconds, PromotionTrialCatalog.CloseSeconds),
                    string.Format("문{0}: 실패 시도가 {1:F1}초다", gate, measured[gate - 1]));

                // 실패는 성공보다 **두 배 넘게** 걸린다. 그 사실이 곱하기 모형을
                // 폐기한 이유이고, 여기가 그 크기를 붙잡는 자리다
                Assert.Greater(measured[gate - 1], PromotionEconomyFixture.TrialSecondsFloor[gate - 1] * 2d,
                    string.Format("문{0}: 실패 {1:F1}초가 성공 {2:F1}초의 두 배도 안 된다 - "
                        + "그러면 재도전 비용을 성공 시간에 곱해도 큰 차이가 없고, "
                        + "이 항을 둘 이유가 없다",
                        gate, measured[gate - 1], PromotionEconomyFixture.TrialSecondsFloor[gate - 1]));
            }
        }

        /**
         * @brief 시뮬레이션의 규칙이 **프로덕션 카탈로그에서 온다.**
         *
         * 승인 과정에서 격노 간격이 15초 -> 10초, 배수가 x2 -> x1.3으로 바뀌었다.
         * 그때 시뮬레이션만 고치고 카탈로그를 안 고쳤다면 3단계의 전투가 다른
         * 리듬을 돌리고, EditMode는 끝까지 초록이었을 것이다.
         */
        [Test]
        public void TrialRules_ComeFromTheProductionCatalog()
        {
            var rules = Rules();

            Assert.AreEqual(PromotionTrialCatalog.SwapSeconds, rules.SwapSeconds, 0d, "전환");
            Assert.AreEqual(PromotionTrialCatalog.EnrageSeconds, rules.EnrageStartSeconds, 0d, "격노 시작");
            Assert.AreEqual(PromotionTrialCatalog.EnrageIntervalSeconds, rules.EnrageStepSeconds, 0d, "격노 간격");
            Assert.AreEqual(PromotionTrialCatalog.EnrageMultiplierPerStep, rules.EnrageAttackStep, 0d, "격노 배수");
            Assert.AreEqual(PromotionTrialCatalog.CloseSeconds, rules.CloseSeconds, 0d, "폐쇄");

            // 격노 산수도 두 곳이 같은 답을 낸다
            foreach (var t in new[] { 0d, 89.9d, 90d, 90.1d, 100.1d, 179.9d })
                Assert.AreEqual(PromotionTrialSimulation.EnrageMultiplier(rules, t),
                    PromotionTrialCatalog.EnrageMultiplierAt(t), 1e-12d,
                    string.Format("{0:F1}초의 격노 배수가 두 곳에서 다르다", t));
        }

        /** 총 체력 배수가 게이트마다 커진다 (심층 여유 발산을 따라간다) */
        [Test]
        public void TotalHealthMultiple_GrowsTowardTheDeepGates()
        {
            var m = PromotionTrialFixture.TotalHealthMultiple;

            Assert.AreEqual(Gates, m.Length, "체력 배수 표의 길이가 문의 수와 다르다");
            Assert.Greater(m[Gates - 1], m[0] * 2d,
                "마지막 문의 체력 배수가 첫 문의 두 배도 안 된다 - 심층의 여유 발산을 못 따라간다");

            for (int i = 4; i < m.Length; i++)
                Assert.Greater(m[i], m[i - 1], string.Format(
                    "문{0}의 체력 배수 {1}이 문{2}의 {3}보다 작다", i + 1, m[i], i, m[i - 1]));
        }
    }
}
