using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 65단계 명중·회피.
     *
     * 확정 사항 여덟을 검사로 옮긴다 - 판정식(수치 대결)·st1 90%·상한 없음·
     * 영체/동료 면제·귀문 불변·결정론·죽은 버튼 아님·밴드. 수치의 출처는
     * 65단계 보고서이고 하네스 실측이다.
     */
    public class AccuracyEvasionTests
    {
        static StageSimulation.Field Field()
        {
            return PromotionTrialFixture.FieldFromAssets();
        }

        // ---------------------------------------------------------------- 판정식

        /**
         * @brief 판정식의 성질 - (0, 1) 안, 단조, 무한대에서 1로 수렴, 상대 0이면 정확히 1.
         *
         * 상한 없는 축이 안전한 근거가 이것이다. 수치가 아무리 커도 확률이 1을
         * 넘지 않으므로 축 값에 Ceiling이 필요 없다(확정 3·7).
         */
        [Test]
        public void RatingContest_StaysInsideZeroAndOne_AndConvergesToOne()
        {
            Assert.AreEqual(1d, RatingContest.Chance(90d, 0d), 0d, "상대 0이면 정확히 1이어야 한다");
            Assert.AreEqual(0d, RatingContest.Chance(0d, 10d), 0d);

            double previous = 0d;
            for (double rating = 1d; rating < 1e12d; rating *= 1.7d)
            {
                double chance = RatingContest.Chance(rating, 10d);
                Assert.Greater(chance, previous, "명중 수치가 오르는데 확률이 안 오른다");
                Assert.Less(chance, 1d, "확률이 1에 닿았다 - 수치 대결은 1에 수렴만 해야 한다");
                previous = chance;
            }
            Assert.Greater(previous, 1d - 1e-9d, "수치가 무한히 커지면 1로 수렴해야 한다");

            for (double opposing = 1d; opposing < 1e6d; opposing *= 2d)
                Assert.Less(RatingContest.Chance(90d, opposing * 2d), RatingContest.Chance(90d, opposing),
                    "상대 수치가 오르는데 확률이 안 내려간다");

            // 대칭: 내가 맞힐 확률 + 상대가 피할 확률 = 1
            Assert.AreEqual(1d, RatingContest.Chance(90d, 37d) + RatingContest.Chance(37d, 90d), 1e-12d);
        }

        /** 확정 4: st1, 강화 전 명중률은 정확히 90% */
        [Test]
        public void StageOne_UnupgradedHitChance_IsNinetyPercent()
        {
            Assert.AreEqual(0.90d, StageCurve.UnboughtMobHitChance(1), 1e-12d);
            Assert.AreEqual(0.90d, CombatStats.AtLevel(1).HitChanceAgainst(StageCurve.EnemyEvasionAtStage(1)), 1e-12d);
            Assert.AreEqual(CombatBaseline.Accuracy, AccuracyCurve.ValueAtLevel(1), 0d);
        }

        /**
         * @brief 적 회피는 단조 증가하고 보스는 배수, 보스 명중도 단조 증가.
         *
         * 명중을 안 산 플레이어의 명중률이 st500에서 몇 %인지를 못 박는다 -
         * 지시서 §F가 명시를 요구한 값이고, 이 축의 핵심 밸런스 변수다.
         */
        [Test]
        public void EnemyEvasion_GrowsMonotonically_AndTheUnboughtDeclineIsDocumented()
        {
            for (int stage = 1; stage < 2000; stage++)
            {
                Assert.Greater(StageCurve.EnemyEvasionAtStage(stage + 1), StageCurve.EnemyEvasionAtStage(stage));
                Assert.Greater(StageCurve.BossAccuracyAtStage(stage + 1), StageCurve.BossAccuracyAtStage(stage));
                Assert.AreEqual(StageCurve.EnemyEvasionAtStage(stage) * StageCurve.BossEvasionMultiplier,
                    StageCurve.BossEvasionAtStage(stage), 1e-12d);
            }

            // 보고서 §2의 표 - 잡몹 / 보스
            Assert.AreEqual(0.887d, StageCurve.UnboughtMobHitChance(10), 0.001d);
            Assert.AreEqual(0.777d, StageCurve.UnboughtMobHitChance(100), 0.001d);
            Assert.AreEqual(0.500d, StageCurve.UnboughtMobHitChance(500), 0.001d, "st500 강화 전 잡몹 명중률");
            Assert.AreEqual(0.400d, StageCurve.UnboughtBossHitChance(500), 0.001d, "st500 강화 전 보스 명중률");
        }

        // ---------------------------------------------------------------- 곡선

        /** 확정 7: 두 곡선에 Ceiling·MaxLevel·CappedValueAtLevel이 없다. 값은 끝없이 오른다 */
        [Test]
        public void Curves_HaveNoCeiling()
        {
            foreach (var type in new[] { typeof(AccuracyCurve), typeof(EvasionCurve) })
            {
                Assert.IsNull(type.GetField("Ceiling"), type.Name + "에 Ceiling이 있다");
                Assert.IsNull(type.GetProperty("MaxLevel"), type.Name + "에 MaxLevel이 있다");
                Assert.IsNull(type.GetField("MaxLevel"), type.Name + "에 MaxLevel이 있다");
                Assert.IsNull(type.GetMethod("CappedValueAtLevel"), type.Name + "에 CappedValueAtLevel이 있다");
            }

            for (int level = 1; level < 100000; level++)
            {
                Assert.Greater(AccuracyCurve.ValueAtLevel(level + 1), AccuracyCurve.ValueAtLevel(level));
                Assert.Greater(EvasionCurve.ValueAtLevel(level + 1), EvasionCurve.ValueAtLevel(level));
            }
        }

        /** Step 43 계약: 정수 골드, 1골드 이상. 10^15 위는 정수 해상도 밖이라 하한만 */
        [Test]
        public void Costs_AreWholeGoldAndAtLeastOne()
        {
            for (int level = 1; level <= 3000; level++)
            {
                foreach (var cost in new[] { AccuracyCurve.CostAtLevel(level), EvasionCurve.CostAtLevel(level) })
                {
                    if (double.IsInfinity(cost)) break;
                    Assert.GreaterOrEqual(cost, 1d, "Lv." + level + ": 1골드 미만");
                    if (cost < 1e15d)
                        Assert.AreEqual(Math.Round(cost), cost, 1e-9d, "Lv." + level + ": 소수 비용");
                }
            }
        }

        /** 강화 트랙이 곡선 statics와 같은 값을 낸다 - 빌더가 옮겨 적는 다섯 값의 대조 */
        [Test]
        public void Tracks_MatchTheCurves_AndNeverCap()
        {
            var accuracy = new UpgradeTrack(UpgradeSystem.AccuracyId, "명중",
                BigDouble.FromDouble(AccuracyCurve.BaseCost), AccuracyCurve.CostGrowth,
                UpgradeTrack.Curve.Additive, BigDouble.FromDouble(AccuracyCurve.BaseValue),
                AccuracyCurve.Step, 0, 0d, UpgradeTrack.Display.HitChance);
            var evasion = new UpgradeTrack(UpgradeSystem.EvasionId, "회피",
                BigDouble.FromDouble(EvasionCurve.BaseCost), EvasionCurve.CostGrowth,
                UpgradeTrack.Curve.Additive, BigDouble.FromDouble(EvasionCurve.BaseValue),
                EvasionCurve.Step, 0, 0d, UpgradeTrack.Display.DodgeChance);

            for (int level = 1; level <= 400; level++)
            {
                Assert.AreEqual(AccuracyCurve.CostAtLevel(level), accuracy.CostAtLevel(level).ToDouble(),
                    AccuracyCurve.CostAtLevel(level) * 1e-9d, "명중 비용 Lv." + level);
                Assert.AreEqual(AccuracyCurve.ValueAtLevel(level), accuracy.ValueAtLevel(level).ToDouble(), 1e-9d);
                Assert.AreEqual(EvasionCurve.CostAtLevel(level), evasion.CostAtLevel(level).ToDouble(),
                    EvasionCurve.CostAtLevel(level) * 1e-9d, "회피 비용 Lv." + level);
                Assert.AreEqual(EvasionCurve.ValueAtLevel(level), evasion.ValueAtLevel(level).ToDouble(), 1e-9d);
            }

            accuracy.SetLevel(5000);
            evasion.SetLevel(5000);
            Assert.IsFalse(accuracy.IsMaxed || accuracy.IsValueCapped, "명중에 MASTER가 뜬다");
            Assert.IsFalse(evasion.IsMaxed || evasion.IsValueCapped, "회피에 MASTER가 뜬다");
        }

        /**
         * @brief 표시는 수치가 아니라 확률이다 - 진행이 없는 씬에서는 st1 기준.
         *
         * 강화 전 명중 90은 "90.00%", 강화 전 회피 0은 "0.00%"로 읽혀야 한다.
         */
        [Test]
        public void Display_ReadsAsChancesAtTheCurrentStage()
        {
            var accuracy = new UpgradeTrack(UpgradeSystem.AccuracyId, "명중",
                BigDouble.FromDouble(AccuracyCurve.BaseCost), AccuracyCurve.CostGrowth,
                UpgradeTrack.Curve.Additive, BigDouble.FromDouble(AccuracyCurve.BaseValue),
                AccuracyCurve.Step, 0, 0d, UpgradeTrack.Display.HitChance);
            var evasion = new UpgradeTrack(UpgradeSystem.EvasionId, "회피",
                BigDouble.FromDouble(EvasionCurve.BaseCost), EvasionCurve.CostGrowth,
                UpgradeTrack.Curve.Additive, BigDouble.FromDouble(EvasionCurve.BaseValue),
                EvasionCurve.Step, 0, 0d, UpgradeTrack.Display.DodgeChance);

            Assume.That(StageProgress.Instance, Is.Null, "진행이 있는 씬에서는 기준 스테이지가 다르다");

            Assert.AreEqual("90.00%", accuracy.Format(accuracy.Value));
            Assert.AreEqual("0.00%", evasion.Format(evasion.Value));
            Assert.AreEqual("1.48%", evasion.Format(evasion.ValueAtLevel(2)), "첫 칸 = 1.5 / (1.5 + 100)");
        }

        // ---------------------------------------------------------------- 영체·동료·귀문

        /**
         * @brief 확정 5: 영체와 동료는 명중 판정을 받지 않는다.
         *
         * 기대 DPS 쪽: 영체만 있는 스탯은 적 회피와 무관하고, 동료 몫은 빗나가지
         * 않는 DPS 기준이다. 적 회피 0이면 ExpectedDps와 **비트 단위로** 같다.
         */
        [Test]
        public void SpiritAndCompanions_NeverMiss_InTheExpectedDps()
        {
            var spiritOnly = new CombatStats { Damage = 100d, SpiritRate = 2d, CritMultiplier = 2d };
            Assert.AreEqual(spiritOnly.ExpectedDps, spiritOnly.ExpectedDpsAgainst(1e6d),
                spiritOnly.ExpectedDps * 1e-12d, "영체가 빗나갔다");

            var stats = new CombatStats
            {
                Damage = 100d, AttacksPerSecond = 2d, SkillRate = 1d, SpiritRate = 0.5d,
                CritMultiplier = 2d, CritRate = 0.3d, PetBonus = 0.4d
            };
            double hit = stats.HitChanceAgainst(30d);
            double perHit = stats.Damage * stats.CritFactor;
            double expected = perHit * ((2d + 1d) * hit + 0.5d + (2d + 1d + 0.5d) * 0.4d);
            Assert.AreEqual(expected, stats.ExpectedDpsAgainst(30d), expected * 1e-12d);

            Assert.AreEqual(BitConverter.DoubleToInt64Bits(stats.ExpectedDps),
                BitConverter.DoubleToInt64Bits(stats.ExpectedDpsAgainst(0d)),
                "적 회피 0(귀문)에서 기대 DPS가 한 비트라도 달라졌다");
        }

        /**
         * @brief 확정 5·6의 런타임 쪽 - 판정을 넣은 자리를 소스로 못 박는다.
         *
         * PlayMode 없이 확인할 수 있는 것은 "어디에 넣었고 어디에 안 넣었는가"다:
         * 명중 굴림은 PlayerCombat에만 있고 동료(PetCombat)에는 없으며, 오의 경로는
         * 영체(Special)를 면제하고, 귀문 중에는 굴리지 않는다. 회피 굴림은 귀문에서도
         * 돈다 - 확정 6이 0으로 묶은 것은 귀문 **적의** 회피다(BossFight 주석).
         */
        [Test]
        public void RuntimeRolls_SkipSpiritCompanionsAndTheTrial()
        {
            string combat = RuntimeSource("PlayerCombat.cs");
            string pet = RuntimeSource("PetCombat.cs");
            string fight = RuntimeSource("BossFight.cs");
            string enemy = RuntimeSource("Enemy.cs");

            StringAssert.Contains("TrialDamageScale.IsActive) return true;", combat, "귀문 중 명중 면제가 빠졌다");
            StringAssert.Contains("TrialDamageScale.Source.Special", combat, "영체 면제가 빠졌다");
            Assert.IsFalse(pet.Contains("RollHit") || pet.Contains("Evasion"), "동료가 명중 판정을 받는다");
            StringAssert.Contains("playerHealth.TryDodge(StageCurve.BossAccuracyAtStage(CurrentStage))", fight,
                "보스 공격 경로에 회피 굴림이 없다");

            // 귀문의 적은 회피 0 - ConfigureAsTrialFoe가 명시적으로 비운다
            int trialFoe = enemy.IndexOf("public void ConfigureAsTrialFoe", StringComparison.Ordinal);
            Assert.Greater(trialFoe, 0);
            StringAssert.Contains("Evasion = 0d;", enemy.Substring(trialFoe, 600));
        }

        /** 귀문의 적은 회피 0이다 - 실제 컴포넌트로 */
        [Test]
        public void TrialFoe_HasZeroEvasion()
        {
            var go = new GameObject("TrialFoeProbe");
            try
            {
                var enemy = go.AddComponent<Enemy>();
                enemy.SetEvasion(42d);
                Assert.AreEqual(42d, enemy.Evasion);

                enemy.ConfigureAsTrialFoe(2f, 0.5f);
                Assert.AreEqual(0d, enemy.Evasion, 0d, "귀문의 적에게 회피가 남았다");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /**
         * @brief 확정 6: 귀문의 체력은 명중·회피 보정을 받지 않는다 - 비트 단위.
         *
         * 귀문은 BossHealthBeforeHitRating을 읽고, 그 값은 BossHealthForStage에서
         * **마지막 곱 하나**(HitRatingCompensation)만 뺀 것이다.
         */
        [Test]
        public void TrialHealth_IgnoresTheHitRatingCompensation()
        {
            var average = BigDouble.FromDouble(Field().AverageMobHealth);

            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
            {
                int stage = PromotionTrialCatalog.GateStages[gate - 1];
                var before = StageCurve.BossHealthBeforeHitRating(average, stage);

                Assert.AreEqual(
                    (before * BigDouble.FromDouble(PromotionTrialCatalog.TotalHealthMultiple[gate - 1])).ToDouble(),
                    PromotionTrialCatalog.TotalTrialHealth(average, gate).ToDouble(), 0d,
                    "문" + gate + ": 귀문 체력이 명중 보정을 받았다");

                Assert.AreEqual(
                    (before * BigDouble.FromDouble(StageCurve.HitRatingCompensation(stage))).ToDouble(),
                    StageCurve.BossHealthForStage(average, stage).ToDouble(),
                    StageCurve.BossHealthForStage(average, stage).ToDouble() * 1e-12d,
                    "일반 보스 체력 = 보정 전 x 명중 보정 이어야 한다");
            }
        }

        // ---------------------------------------------------------------- 시뮬레이션

        /** Step 52 리더보드 결정론 계약 - 난수 없음, 같은 입력 같은 비트 */
        [Test]
        public void Simulation_StaysDeterministic()
        {
            var a = StageSimulation.Run(200, Field());
            var b = StageSimulation.Run(200, Field());

            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(a[i].BossMargin),
                    BitConverter.DoubleToInt64Bits(b[i].BossMargin), "st" + (i + 1) + " 여유");
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(a[i].BossHitChance),
                    BitConverter.DoubleToInt64Bits(b[i].BossHitChance), "st" + (i + 1) + " 명중률");
                Assert.AreEqual(a[i].AccuracyLevel, b[i].AccuracyLevel);
                Assert.AreEqual(a[i].EvasionLevel, b[i].EvasionLevel);
            }
        }

        /**
         * @brief 체계를 걷어낸 세계가 64단계를 재현한다 - 49단계 규칙 ⓐ의 비교군.
         *
         * 값은 64단계 종료 시점의 실측(하네스)이다. 이 세계가 64단계와 같아야
         * 앞 스텝들의 앵커가 이 정책 한 줄로 살아남는다.
         */
        [Test]
        public void HitRatingNeutralized_ReproducesTheStep64World()
        {
            var rows = StageSimulation.Run(100, Field(), new StageSimulation.Policy { NeutralizeHitRating = true, StatPointsPre66 = true });

            Assert.AreEqual(1.527d, rows[9].BossMargin, 0.002d, "st10");
            Assert.AreEqual(1.407d, rows[19].BossMargin, 0.002d, "st20");
            Assert.AreEqual(1.564d, rows[29].BossMargin, 0.002d, "st30");
            Assert.AreEqual(2.236d, rows[49].BossMargin, 0.002d, "st50");
            Assert.AreEqual(14.369d, rows[99].BossMargin, 0.02d, "st100");

            foreach (var row in rows)
            {
                Assert.AreEqual(1, row.AccuracyLevel, "체계가 없는 세계에서 명중을 샀다");
                Assert.AreEqual(1, row.EvasionLevel, "체계가 없는 세계에서 회피를 샀다");
                Assert.AreEqual(1d, row.BossHitChance, 0d);
            }
        }

        /**
         * @brief 온보딩(st1~5)이 그대로다 - 강화 전 명중률을 잡몹·보스 체력이 되갚는다.
         *
         * 지시서가 짚은 "명중 90%면 소요 시간 +11%"를 체력 보정이 상쇄한다
         * (StageCurve.MobEvasionCompensation). 비트가 아니라 1% 안이다 - 체력에
         * 0.9를 곱하고 DPS에 0.9를 곱한 값은 원래 값과 마지막 자리에서 갈린다.
         */
        [Test]
        public void Onboarding_TakesTheStep64Time()
        {
            var now = StageSimulation.Run(5, Field());
            var then = StageSimulation.Run(5, Field(), new StageSimulation.Policy { NeutralizeHitRating = true });

            double a = StageSimulation.TotalSeconds(now), b = StageSimulation.TotalSeconds(then);
            Assert.AreEqual(b, a, b * 0.01d, string.Format("온보딩이 {0:F1}초 -> {1:F1}초", b, a));
        }

        /**
         * @brief 해금 자리: 둘 다 **게임 시작부터** (66.1단계 확정).
         *
         * 65단계에는 명중 st11 · 회피 st5였다. 게이트를 다시 넣어 밴드를 맞추는
         * 길은 닫혔다 - 밴드의 손잡이는 초반 잡몹 체력 하나다. 그래서 여기서는
         * 해금 값과 함께 "st1에 실제로 사는가"를 본다. 열려만 있고 아무도 안
         * 누르면 게이트가 있는 것과 같다.
         */
        [Test]
        public void Unlocks_FromTheFirstStage()
        {
            Assert.AreEqual(1, AccuracyCurve.UnlockStage, "명중에 스테이지 게이트가 다시 생겼다");
            Assert.AreEqual(1, EvasionCurve.UnlockStage, "회피에 스테이지 게이트가 다시 생겼다");
            Assert.IsTrue(AccuracyCurve.IsUnlockedAt(1));
            Assert.IsTrue(EvasionCurve.IsUnlockedAt(1));

            var rows = StageSimulation.Run(1, Field());
            Assert.Greater(rows[0].AccuracyLevel, 1, "명중이 st1부터 열렸는데 st1에 한 칸도 안 팔린다");
        }

        /**
         * @brief 기대 명중률(닫힌 식)이 시뮬레이션을 따라간다.
         *
         * 보스 체력 보정이 읽는 값이다(StageCurve.AccuracyAxisCompensation). 실측과
         * 갈리면 보정이 실제와 다른 축을 상쇄한다 - 26단계 StagesToCeiling 사고.
         */
        [Test]
        public void Accuracy_ExpectedCurve_TracksTheSimulation()
        {
            var rows = StageSimulation.Run(500, Field());

            for (int stage = AccuracyCurve.UnlockStage; stage <= 500; stage++)
            {
                double expected = RatingContest.Chance(AccuracyCurve.ExpectedValueAtStage(stage),
                                                       StageCurve.BossEvasionAtStage(stage));
                Assert.AreEqual(expected, rows[stage - 1].BossHitChance, 0.01d, string.Format(
                    "st{0}: 기대 명중률 {1:P2} vs 실측 {2:P2} (Lv.{3}) - ExpectedLevelAtUnlock/" +
                    "ExpectedLevelsPerStage를 실측에 맞춰라", stage, expected,
                    rows[stage - 1].BossHitChance, rows[stage - 1].AccuracyLevel));
            }
        }

        // ---------------------------------------------------------------- 죽은 버튼

        /**
         * @brief 명중이 진행을 움직인다 - 안 산 플레이어보다 빠르다.
         *
         * 보정 지수가 1이라(StageCurve.AccuracyMarginExponent 주석) 보스 여유의
         * 이득은 없고 이득은 파밍 속도로 남는다. 4% 기준은 무한 구간(st51~500)의
         * 전투 시간으로 잰다 - 실측 +4.4%. 조율 구간(st1~50)은 +3.9%로 기준
         * 아래이고, 3%를 하한으로 못 박는다(보고서 §8).
         *
         * 66단계: 성장탭 세 축이 분모를 키워 지금 세계의 무한 구간 이득이 3.86%로
         * 내려앉았다. 46·49단계 처방(ⓒ)대로 4%는 **이 축이 마지막 층이던 세계**
         * (StatPointsPre66)에서 재고, 지금 세계는 3% 하한을 못 박는다
         */
        [Test]
        public void Accuracy_IsNotADeadButton()
        {
            var with = StageSimulation.Run(500, Field());
            var without = StageSimulation.Run(500, Field(), new StageSimulation.Policy { SkipAccuracy = true });

            var with65 = StageSimulation.Run(500, Field(), new StageSimulation.Policy { StatPointsPre66 = true });
            var without65 = StageSimulation.Run(500, Field(),
                new StageSimulation.Policy { SkipAccuracy = true, StatPointsPre66 = true });
            double deep65 = StageSimulation.CombatSeconds(without65, 51, 500)
                          / StageSimulation.CombatSeconds(with65, 51, 500) - 1d;
            Assert.Greater(deep65, 0.04d, string.Format("무한 구간 이득(66 전 세계) {0:P2} - 4% 아래", deep65));

            double deep = StageSimulation.CombatSeconds(without, 51, 500) / StageSimulation.CombatSeconds(with, 51, 500) - 1d;
            Assert.Greater(deep, 0.03d, string.Format("무한 구간 이득(지금 세계) {0:P2} - 3% 아래", deep));

            double tuned = StageSimulation.TotalSeconds(without.GetRange(0, 50))
                         / StageSimulation.TotalSeconds(with.GetRange(0, 50)) - 1d;
            Assert.Greater(tuned, 0.03d, string.Format("조율 구간 이득 {0:P2} - 3% 아래", tuned));
        }

        /**
         * @brief 회피는 생존 축이다 - 사면 생존에 쓰는 골드가 준다.
         *
         * 회피의 값어치는 보스 생존 하나다(잡몹은 때리지 않는다). 그래서 진행 시간이
         * 아니라 **같은 생존 여유를 사는 데 든 체력 레벨**로 잰다 - 회피를 산
         * 플레이어는 체력을 덜 산다. 그리고 실제로 산다(죽은 버튼이 아니다).
         */
        [Test]
        public void Evasion_IsBoughtAndReplacesSomeHealth()
        {
            var with = StageSimulation.Run(200, Field());
            var without = StageSimulation.Run(200, Field(), new StageSimulation.Policy { SkipEvasion = true });

            Assert.Greater(with[19].EvasionLevel, 1, "st20까지 회피가 한 번도 안 팔렸다");
            Assert.Greater(with[199].DodgeChance, 0.2d, "st200 회피율이 20% 아래 - 축이 묻혔다");
            Assert.Less(with[199].HealthLevel, without[199].HealthLevel,
                "회피를 산 플레이어가 체력을 덜 사지 않는다 - 회피가 생존을 대신하지 못한다");

            foreach (var row in with)
                Assert.IsTrue(row.Survived, "st" + row.Stage + ": 회피를 사는 플레이어가 죽는다");
        }

        /**
         * @brief 명중을 안 산 플레이어도 조율 구간(st1~50)을 깬다.
         *
         * 보정이 기대 명중률을 전량 따라가므로 안 산 플레이어의 보스는 그만큼
         * 무겁다. 바닥은 골드 축의 안 산 플레이어와 같은 클리어 바닥이다
         * (1.35 / 1.15 / 1.0 - GoldGainAxisTests).
         */
        [Test]
        public void SkippingAccuracy_StillClearsTheTunedZone()
        {
            var rows = StageSimulation.Run(50, Field(), new StageSimulation.Policy { SkipAccuracy = true });

            foreach (var row in rows)
            {
                var tier = BossCurve.TierOf(row.Stage);
                double floor = tier == BossCurve.Tier.Finale ? 1.0d
                             : tier == BossCurve.Tier.Chapter ? 1.15d : 1.35d;
                Assert.GreaterOrEqual(row.BossMargin, floor, string.Format(
                    "st{0}: 명중을 안 산 플레이어의 여유 {1:F2} - 보정이 이 축을 필수로 만들었다",
                    row.Stage, row.BossMargin));
                Assert.IsTrue(row.Survived);
            }
        }

        // ---------------------------------------------------------------- 효율 밴드

        /**
         * @brief 회피와 체력의 효율이 곡선 추종 경로 위에서 5배 안이다.
         *
         * 실측 최악 x1.20(st223). 같은 생존 루프의 같은 저울(%EHP/골드)에서 잰다.
         */
        [Test]
        public void Evasion_StaysWithinFiveTimesOfHealth_AlongTheFollowerPath()
        {
            var rows = StageSimulation.Run(500, Field());
            for (int stage = EvasionCurve.UnlockStage; stage <= 500; stage++)
            {
                var row = rows[stage - 1];
                double accuracy = StageCurve.BossAccuracyAtStage(stage);
                double now = EvasionCurve.EffectiveHealthFactor(EvasionCurve.ValueAtLevel(row.EvasionLevel), accuracy);
                double next = EvasionCurve.EffectiveHealthFactor(EvasionCurve.ValueAtLevel(row.EvasionLevel + 1), accuracy);
                double evasion = (next / now - 1d) / EvasionCurve.CostAtLevel(row.EvasionLevel);
                double health = (HealthCurve.Step - 1d) / HealthCurve.CostAtLevel(row.HealthLevel);
                double ratio = evasion > health ? evasion / health : health / evasion;
                Assert.LessOrEqual(ratio, 5d, "st" + stage + ": 회피/체력 효율 x" + ratio.ToString("F2"));
            }
        }

        // ---------------------------------------------------------------- 도움

        static string RuntimeSource(string fileName)
        {
            var hits = Directory.GetFiles("Assets/_Project/Scripts", fileName, SearchOption.AllDirectories);
            Assert.AreEqual(1, hits.Length, fileName + "을(를) " + hits.Length + "개 찾았다 - 하나여야 한다");
            return File.ReadAllText(hits[0]);
        }
    }
}
