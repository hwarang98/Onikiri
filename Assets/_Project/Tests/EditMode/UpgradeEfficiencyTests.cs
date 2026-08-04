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
     * 9단계에서 치명타와 골드 획득량을 추가할 때 같은 함정을 반복하지 않기 위해,
     * 새 축이 목록에 들어오는 순간 이 테스트가 형태를 검사한다.
     */
    public class UpgradeEfficiencyTests
    {
        // UpgradePanelBuilder가 실제로 기록하는 값
        static UpgradeTrack AttackPower()
        {
            return new UpgradeTrack(UpgradeSystem.AttackPowerId, "공격력 강화",
                                    BigDouble.FromDouble(10d), 1.15d,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(5d), 1.12d);
        }

        static UpgradeTrack AttackSpeed()
        {
            return new UpgradeTrack(UpgradeSystem.AttackSpeedId, "공격속도 강화",
                                    BigDouble.FromDouble(4d), 1.15d,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(1.15d), 1.04d,
                                    51);
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

        /**
         * @brief 허용하는 축 간 효율 차이.
         *
         * 3배까지는 설계상의 의도로 볼 수 있다. 그 이상 벌어지면 낮은 쪽은 최적
         * 플레이에서 영영 선택되지 않는다.
         */
        const double MaxRatio = 3d;

        [Test]
        public void AxesStayWithinTheAllowedRatio()
        {
            var power = AttackPower();
            var speed = AttackSpeed();

            int[] levels = { 1, 5, 10, 20, 30, 40, 50 };
            foreach (var level in levels)
            {
                double ratio = UpgradeEfficiency.Ratio(power, speed, level);
                Assert.LessOrEqual(ratio, MaxRatio, string.Format(
                    "Lv.{0}에서 축 효율이 {1:F1}배 벌어짐\n  {2}\n  {3}",
                    level, ratio,
                    UpgradeEfficiency.Describe(power, level),
                    UpgradeEfficiency.Describe(speed, level)));
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
            double atFifty = UpgradeEfficiency.Ratio(power, speed, 50);

            Assert.AreEqual(atOne, atFifty, atOne * 0.01d,
                "효율 비율이 레벨에 따라 " + atOne.ToString("F2") + " -> " + atFifty.ToString("F2") +
                " 로 움직임. 두 축의 비용 증가율이 다르면 낮은 쪽은 결국 죽는다");
        }

        [Test]
        public void MultiplicativeTrack_KeepsItsRelativeGain()
        {
            // 곱연산의 정의. 레벨이 아무리 올라도 "몇 % 오르는가"는 그대로다
            var power = AttackPower();
            Assert.AreEqual(0.12d, UpgradeEfficiency.RelativeGain(power, 1), 1e-9d);
            Assert.AreEqual(0.12d, UpgradeEfficiency.RelativeGain(power, 100), 1e-9d);

            Assert.IsFalse(UpgradeEfficiency.DecaysStructurally(power, 1, 100));
        }

        /**
         * @brief 8단계까지의 곡선이 실제로 죽어 있었음을 확인한다.
         *
         * 이 테스트가 실패하면 회귀가 아니라 이 파일이 틀린 것이다. 지표가 문제를
         * 실제로 잡아내는지를 증명하는 대조군이다.
         */
        [Test]
        public void TheOldLinearCurve_WouldFailThisSameCheck()
        {
            var power = AttackPower();
            var dead = DeadAttackSpeed();

            Assert.IsTrue(UpgradeEfficiency.DecaysStructurally(dead, 1, 30),
                "선형 값 + 지수 비용은 구조적으로 죽어야 한다");

            double ratioAtOne = UpgradeEfficiency.Ratio(power, dead, 1);
            double ratioAtThirty = UpgradeEfficiency.Ratio(power, dead, 30);

            Assert.Greater(ratioAtThirty, ratioAtOne * 5d,
                "옛 곡선은 레벨이 오를수록 비율이 벌어졌어야 한다");
            Assert.Greater(ratioAtThirty, MaxRatio,
                "옛 곡선이 허용 범위를 넘지 않는다면 이 테스트의 기준이 너무 느슨하다");
        }

        [Test]
        public void AttackSpeedCeiling_MatchesTheAuthoredCap()
        {
            var speed = AttackSpeed();
            double ceiling = speed.ValueAtLevel(speed.MaxLevel).ToDouble();

            // 8단계까지의 상한 8.23과 사실상 같아야 한다. 여기가 크게 달라지면
            // 스윙 압축과 참격 예산의 검증 기준도 함께 흔들린다
            Assert.AreEqual(8.23d, ceiling, 0.15d,
                "공격속도 상한이 " + ceiling.ToString("F2") + " 로 바뀌었다");
        }

        /**
         * @brief 새 축을 추가할 때 이 목록에 넣으면 자동으로 검사된다.
         *
         * 9단계의 치명타 확률·피해, 골드 획득량이 여기로 온다.
         */
        [Test]
        public void EveryPairOfAxesIsComparable()
        {
            var tracks = new[] { AttackPower(), AttackSpeed() };

            for (int i = 0; i < tracks.Length; i++)
            {
                for (int j = i + 1; j < tracks.Length; j++)
                {
                    for (int level = 1; level <= 40; level += 13)
                    {
                        double ratio = UpgradeEfficiency.Ratio(tracks[i], tracks[j], level);
                        Assert.LessOrEqual(ratio, MaxRatio, string.Format(
                            "'{0}' 와 '{1}' 이 Lv.{2}에서 {3:F1}배 차이",
                            tracks[i].DisplayName, tracks[j].DisplayName, level, ratio));
                    }
                }
            }
        }
    }
}
