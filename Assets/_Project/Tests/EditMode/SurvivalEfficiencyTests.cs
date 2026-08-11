using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 생존 축들이 서로 비슷한 값어치를 갖는지 검증한다.
     *
     * UpgradeEfficiencyTests와 같은 구조이고 자만 다르다. 체력·회복은 DPS에
     * 기여하지 않으므로 %DPS로 재면 둘 다 0이 나오고, 그 자로는 어떤 곡선을
     * 넣어도 통과하거나 어떤 곡선도 통과하지 못한다.
     *
     * 자는 **골드 1당 유효체력 증가율(%)** 이다.
     *
     *     EHP = 최대체력 + 초당회복 x 제한시간
     *
     * 회복을 체력으로 환산하는 항이 핵심이고 환산율이 곧 전투 지속시간이다.
     */
    public class SurvivalEfficiencyTests
    {
        // 43단계 미세화: 두 생존 축 다 옛 한 레벨 = 새 여덟 칸이라, 옛 200레벨
        // 구간과 같은 값 범위를 보려면 여덟 배를 훑어야 한다
        const int MaxLevelChecked = 1600;

        /** 생존 축끼리 허용하는 효율 차이. DPS 축과 같은 기준이다 */
        const double MaxRatio = 5d;

        static UpgradeTrack Health()
        {
            return new UpgradeTrack(UpgradeSystem.HealthId, "체력 강화",
                                    BigDouble.FromDouble(HealthCurve.BaseCost), HealthCurve.CostGrowth,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(HealthCurve.BaseValue), HealthCurve.Step);
        }

        static UpgradeTrack Regen()
        {
            return new UpgradeTrack(UpgradeSystem.HealthRegenId, "체력 회복",
                                    BigDouble.FromDouble(HealthRegenCurve.BaseCost), HealthRegenCurve.CostGrowth,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(HealthRegenCurve.BaseValue), HealthRegenCurve.Step);
        }

        /**
         * @brief 일부러 나쁜 회복 곡선. 대조군이다.
         *
         * 가산 + 지수 비용. 8단계의 공격속도와 같은 형태이고, 유효체력 기준으로도
         * 같은 이유로 죽어야 한다 - 회복의 EHP 기여는 회복량에 정비례하는데
         * 그 값이 선형으로 자라면 비율 기여가 0으로 수렴한다.
         */
        static UpgradeTrack DeadRegen()
        {
            // 43단계 미세화에 맞춰 대조군도 같은 격자다(1/8 걸음). 실곡선만
            // 미세하고 대조군이 옛 격자면 같은 레벨 번호가 다른 지출 지점이
            // 되어 비교가 무의미해진다
            return new UpgradeTrack(UpgradeSystem.HealthRegenId, "체력 회복 (나쁜 곡선)",
                                    BigDouble.FromDouble(1.295d), 1.0176225d,
                                    UpgradeTrack.Curve.Additive,
                                    BigDouble.FromDouble(1d), 0.00625d);
        }

        static UpgradeTrack[] Axes()
        {
            return new[] { Health(), Regen() };
        }

        static void WorstRatio(UpgradeTrack a, UpgradeTrack b, out double worst, out int atLevel)
        {
            worst = 0d;
            atLevel = 1;

            for (int level = 1; level <= MaxLevelChecked; level++)
            {
                double ratio = SurvivalEfficiency.Ratio(a, b, level);
                if (ratio <= worst) continue;
                worst = ratio;
                atLevel = level;
            }
        }

        [Test]
        public void EveryPairOfSurvivalAxesStaysWithinTheAllowedRatio()
        {
            var axes = Axes();

            for (int i = 0; i < axes.Length; i++)
            {
                for (int j = i + 1; j < axes.Length; j++)
                {
                    double worst;
                    int level;
                    WorstRatio(axes[i], axes[j], out worst, out level);

                    Assert.LessOrEqual(worst, MaxRatio, string.Format(
                        "'{0}' 와 '{1}' 이 Lv.{2}에서 {3:F2}배 차이 (허용 {4}배)\n  {5}\n  {6}",
                        axes[i].DisplayName, axes[j].DisplayName, level, worst, MaxRatio,
                        SurvivalEfficiency.Describe(axes[i], level),
                        SurvivalEfficiency.Describe(axes[j], level)));
                }
            }
        }

        /**
         * @brief 두 축이 서로 다른 구간을 맡는지.
         *
         * 회복을 최대 체력 비례로 바꾸면서 **역할을 나누는 장치가 바뀌었다.**
         *
         * 예전에는 step이 달라서(1.10 대 1.115) 지분이 옮겨갔다. 이제 두 축의
         * step이 같으므로 순수 증가율은 둘 다 10%로 수렴한다 - 체력은 정확히
         * 10% 고정이고, 회복은 비율이 자랄수록 10%에 가까워진다.
         *
         * 역할을 나누는 것은 이제 **비용**이다. 회복이 더 싸므로(4 대 9) 골드당
         * 이득이 어느 레벨부터 회복 쪽으로 넘어간다. 그 교차점이 플레이어가
         * 실제로 도달하는 구간 안에 있어야 회복 버튼이 눌린다.
         */
        [Test]
        public void HealthLeadsEarly_RegenTakesOverByGoldValue()
        {
            var health = Health();
            var regen = Regen();

            // 순수 증가율은 초반에 체력이 앞선다
            Assert.Greater(SurvivalEfficiency.RelativeGain(health, 1),
                           SurvivalEfficiency.RelativeGain(regen, 1),
                           "초반에는 체력이 유효체력의 큰 쪽을 맡아야 한다");

            // 그리고 둘 다 step - 1 로 수렴한다
            Assert.AreEqual(HealthCurve.Step - 1d,
                            SurvivalEfficiency.RelativeGain(health, MaxLevelChecked), 1e-6d);
            Assert.AreEqual(HealthCurve.Step - 1d,
                            SurvivalEfficiency.RelativeGain(regen, MaxLevelChecked), 1e-3d);

            // 골드당으로 보면 어느 지점에서 회복이 앞선다. 그 지점이 없으면
            // 회복은 영원히 눌리지 않는 버튼이다
            int crossover = -1;
            for (int level = 1; level <= MaxLevelChecked; level++)
            {
                if (SurvivalEfficiency.GainPerGold(regen, level) >= SurvivalEfficiency.GainPerGold(health, level))
                {
                    crossover = level;
                    break;
                }
            }

            Assert.AreNotEqual(-1, crossover, "회복이 골드당으로 체력을 한 번도 앞서지 않는다");
            Assert.LessOrEqual(crossover, 240, string.Format(   // 옛 30레벨 = 새 240칸
                "회복이 Lv.{0}에서야 앞선다. 20스테이지 시점의 체력 레벨보다 뒤면 " +
                "플레이어는 그 버튼을 만나지 못한다", crossover));
        }

        /**
         * @brief 생존 축에서는 '기여가 줄어든다'가 곧 죽음이 아니다.
         *
         * DPS 축에서는 절대 기여율이 줄면 그 축은 죽는다. 축들이 서로 곱해지므로
         * 한 축의 %기여가 다른 축의 레벨과 무관하기 때문이다.
         *
         * 유효체력은 **합**이다. 체력과 회복이 EHP를 나눠 가지므로, step이 다르면
         * 한쪽의 지분이 줄어드는 것이 필연이다. 실제로 체력의 EHP 기여는
         * 7.69% -> 1.84%로 줄고 회복은 2.65% -> 9.39%로 는다. 그것 자체는 설계이지
         * 결함이 아니다 - 초반과 후반의 주인공을 나누기 위해 일부러 step을 다르게
         * 두었다.
         *
         * 죽었는지를 가르는 것은 지분이 아니라 **골드당 값어치**다. 지분이 줄어든
         * 만큼 비용이 싸면 여전히 살 이유가 있다. 그래서 여기서 지키는 것은
         * 절대 기여가 아니라 비율이고, 그것은 위의 밴드 검사가 이미 한다.
         *
         * 이 테스트는 그 사실을 명시적으로 못 박는다 - 지분은 줄어도 되지만
         * 골드당 효율은 밴드를 벗어나면 안 된다.
         */
        [Test]
        public void SurvivalShareMayShift_ButGoldValueMustNot()
        {
            var health = Health();
            var regen = Regen();

            // 지분은 실제로 옮겨간다. 그것이 두 축을 나누는 장치다
            Assert.Less(SurvivalEfficiency.RelativeGain(health, MaxLevelChecked),
                        SurvivalEfficiency.RelativeGain(health, 1),
                        "체력의 EHP 지분이 줄지 않으면 회복이 후반을 맡을 자리가 없다");

            Assert.Greater(SurvivalEfficiency.RelativeGain(regen, MaxLevelChecked),
                           SurvivalEfficiency.RelativeGain(regen, 1));

            // 그런데도 골드당 효율은 끝까지 밴드 안이다
            double worst;
            int level;
            WorstRatio(health, regen, out worst, out level);

            Assert.LessOrEqual(worst, MaxRatio, string.Format(
                "지분이 옮겨가는 것은 괜찮지만 골드당 효율이 Lv.{0}에서 {1:F2}배로 벌어졌다",
                level, worst));
        }

        /**
         * @brief 대조군. 나쁜 곡선이 실제로 이 검사에 걸리는지.
         *
         * 이것이 실패하면 회귀가 아니라 기준이 너무 느슨하다는 뜻이고, 그 상태의
         * 테스트는 통과해도 아무것도 보장하지 못한다.
         */
        [Test]
        public void AnAdditiveRegenCurve_WouldFailTheSameCheck()
        {
            var health = Health();
            var dead = DeadRegen();

            Assert.IsTrue(SurvivalEfficiency.DecaysStructurally(dead, 1, 800),   // 옛 100레벨 = 새 800칸
                "가산 회복 + 지수 비용은 구조적으로 죽어야 한다");

            double worst;
            int level;
            WorstRatio(health, dead, out worst, out level);

            Assert.Greater(worst, MaxRatio, string.Format(
                "나쁜 곡선이 허용 범위({0}배)를 넘지 않는다면 이 파일의 기준이 너무 느슨하다. " +
                "최악 지점 Lv.{1}에서 {2:F1}배", MaxRatio, level, worst));
        }

        // ------------------------------------------------------------ 자의 정의

        [Test]
        public void EffectiveHealth_ConvertsRegenByFightDuration()
        {
            // 회복은 **초당 최대 체력의 몇 %**다. 10%/s에 30초면 체력 세 배치를
            // 더 버티는 셈이므로 유효체력은 최대 체력의 4배가 된다
            double ehp = SurvivalEfficiency.EffectiveHealth(100d, 0.10d);

            Assert.AreEqual(100d * (1d + 0.10d * StageCurve.BossTimeLimitSeconds), ehp, 1e-9d);
            Assert.AreEqual(400d, ehp, 1e-9d);
            Assert.AreEqual(StageCurve.BossTimeLimitSeconds, SurvivalEfficiency.ReferenceFightSeconds, 1e-9d);
        }

        /**
         * @brief 비례이므로 체력을 올리면 회복의 절대량도 함께 오른다.
         *
         * 이것이 절대량 모델과 갈리는 지점이다. 예전에는 두 축이 자릿수 경주를
         * 했고 지는 쪽이 화면에서 죽었다. HealthRegenCurve 참고.
         */
        [Test]
        public void RaisingHealth_AlsoRaisesAbsoluteRegen()
        {
            double lowHealth = HealthRegenCurve.PerSecondAt(1, 100d);
            double highHealth = HealthRegenCurve.PerSecondAt(1, 400d);

            Assert.AreEqual(lowHealth * 4d, highHealth, 1e-9d,
                "체력을 네 배로 올렸는데 초당 회복량이 그대로다");
        }

        [Test]
        public void SurvivalAxes_AreNotMeasurableByTheDpsRuler()
        {
            // 생존 축을 DPS 자로 재면 0이 나온다. 그것이 이 파일이 존재하는 이유다
            foreach (var track in Axes())
            {
                Assert.IsFalse(CombatStats.FeedsDps(track.Id));
                Assert.AreEqual(0d, UpgradeEfficiency.RelativeGain(track, 10), 1e-12d);
                Assert.IsTrue(SurvivalEfficiency.FeedsSurvival(track.Id));
                Assert.Greater(SurvivalEfficiency.RelativeGain(track, 10), 0d);
            }
        }

        /** 반대 방향도. DPS 축을 생존 자로 재면 0이어야 한다 */
        [Test]
        public void DpsAxes_AreNotMeasurableByTheSurvivalRuler()
        {
            var power = new UpgradeTrack(UpgradeSystem.AttackPowerId, "공격력 강화",
                                         BigDouble.FromDouble(AttackPowerCurve.BaseCost),
                                         AttackPowerCurve.CostGrowth,
                                         UpgradeTrack.Curve.Multiplicative,
                                         BigDouble.FromDouble(AttackPowerCurve.BaseValue),
                                         AttackPowerCurve.Step);

            Assert.IsFalse(SurvivalEfficiency.FeedsSurvival(power.Id));
            Assert.AreEqual(0d, SurvivalEfficiency.RelativeGain(power, 10), 1e-12d);
        }
    }
}
