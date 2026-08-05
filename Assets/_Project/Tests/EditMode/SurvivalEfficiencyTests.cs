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
        const int MaxLevelChecked = 200;

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
            return new UpgradeTrack(UpgradeSystem.HealthRegenId, "체력 회복 (나쁜 곡선)",
                                    BigDouble.FromDouble(11d), 1.15d,
                                    UpgradeTrack.Curve.Additive,
                                    BigDouble.FromDouble(1d), 0.05d);
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
         * 유효체력만 놓고 보면 체력과 회복은 거의 같은 일을 한다 - 제한 시간이
         * 고정이라 회복 1/초는 그 시간만큼의 체력과 같다. step을 다르게 둔 것이
         * 두 축에 역할을 주는 유일한 장치이므로, 그 차이가 실제로 존재하는지
         * 여기서 확인한다.
         */
        [Test]
        public void HealthLeadsEarly_RegenLeadsLate()
        {
            var health = Health();
            var regen = Regen();

            Assert.Greater(SurvivalEfficiency.RelativeGain(health, 1),
                           SurvivalEfficiency.RelativeGain(regen, 1),
                           "초반에는 체력이 유효체력의 큰 쪽을 맡아야 한다");

            Assert.Greater(SurvivalEfficiency.RelativeGain(regen, MaxLevelChecked),
                           SurvivalEfficiency.RelativeGain(health, MaxLevelChecked),
                           "후반에는 회복이 앞서야 한다. 아니면 두 축이 같은 버튼이다");
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

            Assert.IsTrue(SurvivalEfficiency.DecaysStructurally(dead, 1, 100),
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
            double ehp = SurvivalEfficiency.EffectiveHealth(100d, 2d);

            // 회복 2/초 x 30초 = 체력 60과 같다
            Assert.AreEqual(100d + 2d * StageCurve.BossTimeLimitSeconds, ehp, 1e-9d);
            Assert.AreEqual(StageCurve.BossTimeLimitSeconds, SurvivalEfficiency.ReferenceFightSeconds, 1e-9d);
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
