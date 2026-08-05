using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 강화 축들이 서로 비슷한 값어치를 갖는지 검증한다.
     *
     * 8단계까지 공격속도 강화는 죽은 버튼이었다. 값은 선형(+0.12/레벨)인데 비용은
     * 지수(x1.35)라 골드당 효율이 공격력의 1/89까지 벌어져 있었다. 화면에는 멀쩡한
     * 버튼으로 보이고, 플레이해봐도 "왠지 안 사게 된다" 정도로만 느껴진다.
     *
     * 효율의 정의는 **골드 1당 DPS 증가율(%)** 이다. 절대 증가량이 아니다.
     * DPS = 공격력 x 공격속도이므로 한 축의 절대 증가량은 다른 축의 현재 레벨에
     * 달려 있고, 그러면 축 사이를 비교할 때마다 상대 축의 레벨을 함께 정해야 한다.
     * 증가율로 재면 그 의존이 사라져 비교가 레벨 하나로 끝난다.
     *
     * 비교는 **같은 레벨끼리** 한다. 레벨 n에서 다음 1레벨을 살 때의 %DPS/골드다.
     *
     * 앞으로 치명타율·골드획득을 추가할 때 같은 함정을 반복하지 않기 위해, 새 축이
     * Axes()에 들어오는 순간 이 파일이 형태를 검사한다.
     */
    public class UpgradeEfficiencyTests
    {
        /**
         * @brief 검사 구간.
         *
         * 200까지 보는 이유는, 어느 한 레벨에서 통과하는 것은 아무것도 증명하지 않기
         * 때문이다. 선형+지수 조합은 낮은 레벨에서 멀쩡해 보이다가 서서히 죽는다.
         * 실제 상한이 32인 축도 이 구간 전체를 재는데, 검사 대상이 계수가 아니라
         * 곡선의 **형태**이고 형태는 상한과 무관하기 때문이다.
         */
        const int MaxLevelChecked = 200;

        /**
         * @brief 허용하는 축 간 효율 차이.
         *
         * 이 이상 벌어지면 낮은 쪽은 최적 플레이에서 영영 선택되지 않는다.
         * 지금 두 축의 실제 비율은 1.20배이고 레벨과 무관하게 일정하다.
         */
        const double MaxRatio = 5d;

        // UpgradePanelBuilder가 실제로 기록하는 값
        static UpgradeTrack AttackPower()
        {
            return new UpgradeTrack(UpgradeSystem.AttackPowerId, "공격력 강화",
                                    BigDouble.FromDouble(10d), 1.15d,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(5d), 1.12d);
        }

        /** 곡선 값은 AttackSpeedCurve에서 가져온다. 여기 숫자를 복사해두면 언젠가 어긋난다 */
        static UpgradeTrack AttackSpeed()
        {
            return new UpgradeTrack(UpgradeSystem.AttackSpeedId, "공격속도 강화",
                                    BigDouble.FromDouble(AttackSpeedCurve.BaseCost),
                                    AttackSpeedCurve.CostGrowth,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(AttackSpeedCurve.BaseValue),
                                    AttackSpeedCurve.Step,
                                    AttackSpeedCurve.MaxLevel,
                                    AttackSpeedCurve.Ceiling);
        }

        /** 8단계까지 쓰던 곡선. 무엇이 문제였는지를 테스트 안에 남겨둔다 */
        static UpgradeTrack DeadAttackSpeed()
        {
            return new UpgradeTrack(UpgradeSystem.AttackSpeedId, "공격속도 강화 (구)",
                                    BigDouble.FromDouble(25d), 1.35d,
                                    UpgradeTrack.Curve.Additive,
                                    BigDouble.FromDouble(1.15d), 0.12d,
                                    60);
        }

        static UpgradeTrack CritRate()
        {
            return new UpgradeTrack(UpgradeSystem.CritRateId, "치명타 확률",
                                    BigDouble.FromDouble(CritRateCurve.BaseCost),
                                    CritRateCurve.CostGrowth,
                                    UpgradeTrack.Curve.Additive,
                                    BigDouble.FromDouble(CritRateCurve.BaseValue),
                                    CritRateCurve.Step,
                                    CritRateCurve.MaxLevel, CritRateCurve.Ceiling);
        }

        static UpgradeTrack CritDamage()
        {
            return new UpgradeTrack(UpgradeSystem.CritDamageId, "치명타 피해",
                                    BigDouble.FromDouble(CritDamageCurve.BaseCost),
                                    CritDamageCurve.CostGrowth,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(CritDamageCurve.BaseValue),
                                    CritDamageCurve.Step);
        }

        /**
         * @brief 지금 게임에 있는 성장 축 전부.
         *
         * 새 축은 여기에만 추가하면 아래 검사가 전부 따라온다. 골드 획득량이
         * 다음 후보인데, 그 축은 DPS에 기여하지 않으므로 이 지표로는 잴 수 없다 -
         * 그때는 "골드당 골드"라는 별도 자가 필요하다.
         */
        static UpgradeTrack[] Axes()
        {
            return new[] { AttackPower(), AttackSpeed(), CritRate(), CritDamage() };
        }

        /** 구간 전체에서 두 축이 가장 크게 벌어지는 지점 */
        static void WorstRatio(UpgradeTrack a, UpgradeTrack b, out double worst, out int atLevel)
        {
            worst = 0d;
            atLevel = 1;

            for (int level = 1; level <= MaxLevelChecked; level++)
            {
                double ratio = UpgradeEfficiency.Ratio(a, b, level);
                if (ratio <= worst) continue;
                worst = ratio;
                atLevel = level;
            }
        }

        /**
         * @brief 9-2의 본체. 어떤 두 축도 어떤 레벨에서도 5배 넘게 벌어지지 않는다.
         *
         * 축이 늘어나면 조합도 늘어나므로 쌍을 전부 돈다. 두 축뿐일 때는 과해 보이지만,
         * 죽은 버튼은 축이 셋 이상일 때 훨씬 알아채기 어렵다 - 하나가 지배적이면
         * 나머지 둘은 서로 비슷해서 "둘 다 안 사게 되는" 상태가 정상처럼 보인다.
         */
        [Test]
        public void EveryPairOfAxesStaysWithinTheAllowedRatio()
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
                        "'{0}' 와 '{1}' 이 Lv.{2}에서 {3:F1}배 차이 (허용 {4}배)\n  {5}\n  {6}",
                        axes[i].DisplayName, axes[j].DisplayName, level, worst, MaxRatio,
                        UpgradeEfficiency.Describe(axes[i], level),
                        UpgradeEfficiency.Describe(axes[j], level)));
                }
            }
        }

        /**
         * @brief 비율이 레벨에 따라 벌어지지 않는지.
         *
         * 이것이 구조적 검사다. 어느 한 레벨에서 통과해도 비율이 레벨과 함께 커지면
         * 그 축은 언젠가 반드시 죽는다. 두 축이 같은 형태(곱연산)에 같은 비용
         * 증가율을 쓰면 비율은 상수가 된다.
         */
        [Test]
        public void RatioDoesNotDriftWithLevel()
        {
            var power = AttackPower();
            var speed = AttackSpeed();

            double atOne = UpgradeEfficiency.Ratio(power, speed, 1);
            double atTop = UpgradeEfficiency.Ratio(power, speed, MaxLevelChecked);

            Assert.AreEqual(atOne, atTop, atOne * 0.01d,
                "효율 비율이 레벨에 따라 " + atOne.ToString("F2") + " -> " + atTop.ToString("F2") +
                " 로 움직임. 두 축의 비용 증가율이 다르면 낮은 쪽은 결국 죽는다");
        }

        /** 어떤 축도 값의 증가율이 레벨과 함께 줄어들지 않아야 한다 */
        [Test]
        public void NoAxisDecaysStructurally()
        {
            foreach (var track in Axes())
            {
                Assert.IsFalse(UpgradeEfficiency.DecaysStructurally(track, 1, MaxLevelChecked),
                    "'" + track.DisplayName + "' 의 레벨당 증가율이 " +
                    UpgradeEfficiency.RelativeGain(track, 1).ToString("P2") + " -> " +
                    UpgradeEfficiency.RelativeGain(track, MaxLevelChecked).ToString("P2") +
                    " 로 줄어든다. 값이 합연산인데 비용이 지수면 이 축은 반드시 죽는다");
            }
        }

        [Test]
        public void MultiplicativeTrack_KeepsItsRelativeGain()
        {
            // 곱연산의 정의. 레벨이 아무리 올라도 "DPS가 몇 % 오르는가"는 그대로다.
            // 공격력은 DPS에 직접 곱해지므로 값 증가율이 곧 DPS 증가율이다
            var power = AttackPower();
            Assert.AreEqual(0.12d, UpgradeEfficiency.RelativeGain(power, 1), 1e-9d);
            Assert.AreEqual(0.12d, UpgradeEfficiency.RelativeGain(power, MaxLevelChecked), 1e-9d);
        }

        /**
         * @brief 치명타율은 가산이지만 죽지 않는다.
         *
         * 8단계의 공격속도와 형태가 같은데(가산 값 + 지수 비용) 결과가 다르다.
         * 차이는 DPS 기여 구조에 있다 - 확률의 기여는 rate x (배수 - 1) 이라,
         * 치명타 피해 축이 함께 자라면 확률 한 칸의 값어치도 함께 자란다.
         *
         * 이 테스트가 지키는 것은 계수가 아니라 **그 관계**다. 둘 중 하나를 빼거나
         * 비용 증가율을 다르게 두면 여기서 걸린다.
         */
        [Test]
        public void AdditiveCritRate_DoesNotDecay_BecauseCritDamageGrowsWithIt()
        {
            var rate = CritRate();

            double atOne = UpgradeEfficiency.RelativeGain(rate, 1);
            double atFifty = UpgradeEfficiency.RelativeGain(rate, 50);
            double atTop = UpgradeEfficiency.RelativeGain(rate, MaxLevelChecked);

            Assert.Greater(atOne, 0d);
            Assert.IsFalse(UpgradeEfficiency.DecaysStructurally(rate, 1, MaxLevelChecked),
                string.Format("치명타율의 DPS 기여가 {0:P2} -> {1:P2} 로 무너졌다. " +
                              "치명타 피해가 함께 자라지 않으면 이 축은 8단계의 공격속도가 된다",
                              atOne, atTop));

            // 언덕 모양이어야 한다. 중간이 양 끝보다 높다
            Assert.Greater(atFifty, atOne);
            Assert.Greater(atFifty, atTop);
        }

        /**
         * @brief 치명타 피해는 레벨이 오를수록 세진다.
         *
         * 배수가 커지면 치명타가 DPS의 대부분을 차지하게 되어, 배수를 3% 올리는
         * 것이 DPS를 거의 3% 올리는 일이 된다. 상한(step - 1 = 3%)에 수렴한다.
         */
        [Test]
        public void CritDamage_GainGrowsTowardItsStep()
        {
            var damage = CritDamage();

            double atOne = UpgradeEfficiency.RelativeGain(damage, 1);
            double atTop = UpgradeEfficiency.RelativeGain(damage, MaxLevelChecked);

            Assert.Less(atOne, atTop, "치명타 피해의 기여가 레벨과 함께 커지지 않는다");
            Assert.AreEqual(CritDamageCurve.Step - 1d, atTop, 0.002d,
                "Lv." + MaxLevelChecked + "에서 step - 1 에 수렴하지 않았다");
        }

        /** 치명타 축이 DPS 공식을 실제로 지나는지. 지나지 않으면 효율이 0으로 나온다 */
        [Test]
        public void CritAxes_FeedTheDpsFormula()
        {
            foreach (var track in Axes())
                Assert.IsTrue(CombatStats.FeedsDps(track.Id),
                    "'" + track.DisplayName + "' 이 DPS 공식에 연결돼 있지 않다. " +
                    "효율이 0으로 나와 비교에서 조용히 빠진다");
        }

        /**
         * @brief 8단계까지의 곡선이 실제로 이 검사에 걸리는지 확인한다.
         *
         * 대조군이다. 이것이 실패하면 회귀가 아니라 검사 기준이 너무 느슨하다는 뜻이고,
         * 그 상태의 테스트는 통과해도 아무것도 보장하지 못한다.
         *
         * 이 테스트가 있어야 위의 통과가 의미를 갖는다. "5배를 넘지 않았다"는 것은
         * 5배를 넘는 곡선을 실제로 잡아낼 수 있을 때만 성과다.
         */
        [Test]
        public void TheOldLinearCurve_WouldFailTheSameCheck()
        {
            var power = AttackPower();
            var dead = DeadAttackSpeed();

            Assert.IsTrue(UpgradeEfficiency.DecaysStructurally(dead, 1, 30),
                "선형 값 + 지수 비용은 구조적으로 죽어야 한다");

            double worst;
            int level;
            WorstRatio(power, dead, out worst, out level);

            Assert.Greater(worst, MaxRatio, string.Format(
                "옛 곡선이 허용 범위({0}배)를 넘지 않는다면 이 파일의 기준이 너무 느슨하다. " +
                "최악 지점 Lv.{1}에서 {2:F1}배", MaxRatio, level, worst));

            // 벌어지는 방식도 확인한다. 한 레벨에서만 튀는 것과 레벨을 따라 계속
            // 벌어지는 것은 전혀 다른 문제이고, 죽은 버튼은 후자다
            double ratioAtOne = UpgradeEfficiency.Ratio(power, dead, 1);
            double ratioAtThirty = UpgradeEfficiency.Ratio(power, dead, 30);
            Assert.Greater(ratioAtThirty, ratioAtOne * 5d,
                "옛 곡선은 레벨이 오를수록 비율이 벌어졌어야 한다");
        }

        // ---------------------------------------------------------------- 공격속도 상한

        /**
         * @brief 공격속도 상한이 아트에서 유도되는지.
         *
         * 8단계까지 상한 8.23은 손으로 적은 값이었고 근거가 없었다. 그 속도에서
         * 스윙은 4.1배속으로 재생되어 프레임 하나가 60fps 화면의 한 프레임에
         * 해당한다. 화면에 보이는 것은 발도가 아니라 깜빡임이다.
         */
        [Test]
        public void AttackSpeedCeiling_ComesFromTheClipLength()
        {
            // 7프레임 / 14fps = 0.5초. 자연 속도로는 초당 2회가 한계다
            Assert.AreEqual(0.5f, AttackSpeedCurve.SwingDuration, 1e-6f);
            Assert.AreEqual(2f, AttackSpeedCurve.NaturalAttacksPerSecond, 1e-6f);

            // 2배속까지 허용하면 정확히 두 배
            Assert.AreEqual(4f, AttackSpeedCurve.Ceiling, 1e-6f);
        }

        [Test]
        public void AttackSpeedMaxLevel_StopsJustBelowTheCeiling()
        {
            int max = AttackSpeedCurve.MaxLevel;

            Assert.LessOrEqual(AttackSpeedCurve.ValueAtLevel(max), AttackSpeedCurve.Ceiling,
                "상한 레벨의 공격속도가 이미 상한을 넘는다");

            Assert.Greater(AttackSpeedCurve.ValueAtLevel(max + 1), AttackSpeedCurve.Ceiling,
                "한 레벨을 더 팔 수 있는데 상한으로 막았다. 그만큼이 그냥 버려진다");

            // 지금 아트 기준의 실제 값. 여기가 바뀌면 보고서의 수치도 함께 바뀌어야 한다
            Assert.AreEqual(32, max);
            Assert.AreEqual(3.879d, AttackSpeedCurve.ValueAtLevel(max), 0.005d);
        }

        /**
         * @brief 상한 위의 세이브 레벨은 **유지되고 효과만 막힌다**.
         *
         * 처음에는 SetLevel이 레벨을 잘랐다. 그러면 9단계처럼 상한을 51에서 32로
         * 낮춘 업데이트에서 Lv.44 플레이어가 산 12레벨이 영구히 사라진다. 골드는
         * 이미 썼는데 되돌릴 방법이 없다.
         */
        [Test]
        public void AttackSpeedTrack_KeepsTheSavedLevelAndCapsOnlyTheValue()
        {
            var speed = AttackSpeed();
            speed.SetLevel(44);

            Assert.AreEqual(44, speed.Level, "저장된 레벨이 잘렸다");
            Assert.IsTrue(speed.IsMaxed, "상한 위인데 더 살 수 있는 상태로 보인다");
            Assert.IsTrue(speed.IsValueCapped, "효과가 막혀 있는데 그 사실이 드러나지 않는다");

            Assert.AreEqual(AttackSpeedCurve.Ceiling, speed.Value.ToDouble(), 1e-9d,
                "효과가 상한에서 정확히 멈추지 않는다");

            // 곡선 자체는 그대로 살아 있다. 상한이 오르면 이 값이 돌아온다
            Assert.Greater(speed.UncappedValueAtLevel(44).ToDouble(), AttackSpeedCurve.Ceiling);
        }

        /**
         * @brief 상한이 오르면 잠들어 있던 레벨이 스스로 깨어난다.
         *
         * "레벨을 유지한다"가 의미를 가지려면 되찾을 경로가 있어야 한다. 더 긴 공격
         * 클립으로 교체하거나 2배속 규칙을 바꾸면 상한이 오르고, 그때 Lv.44는
         * 다시 사지 않아도 제 값을 내야 한다.
         */
        [Test]
        public void RaisingTheCeiling_RestoresTheSleepingLevels()
        {
            var speed = AttackSpeed();
            speed.SetLevel(44);

            double cappedBefore = speed.Value.ToDouble();
            Assert.AreEqual(AttackSpeedCurve.Ceiling, cappedBefore, 1e-9d);

            // 클립이 두 배로 길어졌다고 치면 상한도 두 배가 된다
            double raised = AttackSpeedCurve.Ceiling * 2d;
            speed.SetValueCeiling(raised, AttackSpeedCurve.MaxLevelWithin(raised));

            Assert.AreEqual(speed.UncappedValueAtLevel(44).ToDouble(), speed.Value.ToDouble(), 1e-6d,
                "상한을 올렸는데 Lv.44가 제 값을 내지 않는다");
            Assert.Greater(speed.Value.ToDouble(), cappedBefore,
                "상한이 올랐는데 효과가 그대로다");
            Assert.IsFalse(speed.IsValueCapped, "더 이상 막혀 있지 않아야 한다");
            Assert.IsFalse(speed.IsMaxed, "상한이 올랐으므로 다시 살 수 있어야 한다");
        }

        /** 상한이 없는 축은 예전과 똑같이 동작한다 */
        [Test]
        public void UncappedTrack_IsUnaffectedByTheCeilingLogic()
        {
            var power = AttackPower();
            power.SetLevel(500);

            Assert.AreEqual(500, power.Level);
            Assert.IsFalse(power.IsMaxed);
            Assert.IsFalse(power.IsValueCapped);
            Assert.AreEqual(power.UncappedValueAtLevel(500).ToDouble(), power.Value.ToDouble(), 1e-6d);
        }
    }
}
