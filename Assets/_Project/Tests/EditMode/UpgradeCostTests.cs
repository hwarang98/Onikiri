using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 강화 비용의 두 규칙(E-3 수정)을 못 박는다 - **정수 골드, 최소 1골드.**
     *
     * 43단계 미세화가 남긴 0.2~1.5골드짜리 첫 칸이 이 규칙의 발단이다. 소수
     * 골드는 화폐의 문법에 없고, 표시(UpgradeButton)가 소수로 때우던 처리도
     * 규칙과 함께 사라졌다. 여기가 깨지면 화면에 "0골드" 버튼이나 소수 비용이
     * 되살아난다.
     *
     * 규칙의 단일 출처는 UpgradeCost다. 곡선 statics(시뮬레이션·효율 지표)와
     * UpgradeTrack(씬의 실제 버튼)이 같은 규칙을 지나는지도 여기서 대조한다 -
     * 두 곳이 갈리면 시뮬레이션이 화면과 다른 가격을 잰다.
     */
    public class UpgradeCostTests
    {
        /** double이 정수를 정확히 들 수 있는 경계. 이 위는 반올림이 항등이다 */
        const double IntegerLimit = 1e15d;

        // ---------------------------------------------------------------- 규칙 자체

        [Test]
        public void Quantize_FloorsAtOneGold()
        {
            Assert.AreEqual(1d, UpgradeCost.Quantize(0.19943d), "1골드 미만이 살아남았다");
            Assert.AreEqual(1d, UpgradeCost.Quantize(0.9999d));
            Assert.AreEqual(1d, UpgradeCost.Quantize(1.4d));
            Assert.AreEqual(1d, UpgradeCost.Quantize(BigDouble.FromDouble(0.23d)).ToDouble());
        }

        [Test]
        public void Quantize_RoundsHalfAwayFromZero()
        {
            Assert.AreEqual(2d, UpgradeCost.Quantize(1.5d));
            Assert.AreEqual(2d, UpgradeCost.Quantize(2.4999d));
            Assert.AreEqual(13d, UpgradeCost.Quantize(12.5d));
            Assert.AreEqual(13d, UpgradeCost.Quantize(BigDouble.FromDouble(12.5d)).ToDouble());
        }

        /** 10^15 위는 건드리지 않는다. 끝전이 표시 밖이고 반올림도 항등이다 */
        [Test]
        public void Quantize_LeavesHugeCostsAlone()
        {
            Assert.AreEqual(3.7e17d, UpgradeCost.Quantize(3.7e17d), 1e2d);

            var big = BigDouble.FromDouble(3.7e17d);
            Assert.AreEqual(big.ToDouble(), UpgradeCost.Quantize(big).ToDouble(), 1e2d);
        }

        // ---------------------------------------------------------------- 전 축 검사

        /** 축 하나의 비용 곡선. 이름은 실패 메시지에 쓴다 */
        struct Axis
        {
            public string Name;
            public System.Func<int, double> Cost;
            public int Levels;
        }

        /**
         * @brief 아홉 축 전부와 훑는 범위.
         *
         * 범위는 콘텐츠가 실제로 닿는 깊이다 - 치명타 확률·연격은 만렙(1000),
         * 공격속도는 아트 상한(32), 골드 획득은 상한(13), 나머지 무상한 축은
         * 심층 실측(st200)이 닿는 수천 레벨을 덮는 3000까지.
         */
        static Axis[] Axes()
        {
            return new[]
            {
                new Axis { Name = "공격력", Cost = AttackPowerCurve.CostAtLevel, Levels = 3000 },
                new Axis { Name = "공격속도", Cost = AttackSpeedCurve.CostAtLevel, Levels = AttackSpeedCurve.MaxLevel },
                new Axis { Name = "치명타 확률", Cost = CritRateCurve.CostAtLevel, Levels = CritRateCurve.MaxLevel },
                new Axis { Name = "치명타 피해", Cost = CritDamageCurve.CostAtLevel, Levels = 3000 },
                new Axis { Name = "체력", Cost = HealthCurve.CostAtLevel, Levels = 3000 },
                new Axis { Name = "체력 회복", Cost = HealthRegenCurve.CostAtLevel, Levels = HealthRegenCurve.MaxLevel },
                new Axis { Name = "골드 획득", Cost = GoldGainCurve.CostAtLevel, Levels = GoldGainCurve.MaxLevel },
                new Axis { Name = "초월 치명타", Cost = TranscendCurve.CostAtLevel, Levels = 3000 },
                new Axis { Name = "연격", Cost = ComboCurve.CostAtLevel, Levels = ComboCurve.MaxLevel }
            };
        }

        /**
         * @brief 소수·1골드 미만 0건 - 사용자 확정 스펙의 본체.
         *
         * "어떤 축, 어떤 레벨도 1 미만 없음"이므로 어떤 표본이 아니라 전 구간을
         * 돈다. 10^15 위는 double의 정수 해상도 밖이라 정수 판정 자체가 무의미해
         * 1골드 하한만 본다.
         */
        [Test]
        public void EveryAxis_EveryLevel_CostsWholeGoldAndAtLeastOne()
        {
            foreach (var axis in Axes())
            {
                for (int level = 1; level <= axis.Levels; level++)
                {
                    double cost = axis.Cost(level);

                    Assert.GreaterOrEqual(cost, 1d, string.Format(
                        "{0} Lv.{1}: 비용 {2}골드 - 1골드 미만이 되살아났다", axis.Name, level, cost));

                    if (cost >= IntegerLimit) continue;

                    Assert.AreEqual(System.Math.Round(cost), cost, 1e-9d, string.Format(
                        "{0} Lv.{1}: 비용 {2}골드 - 소수가 되살아났다", axis.Name, level, cost));
                }
            }
        }

        /**
         * @brief 비용이 뒤 레벨보다 비싸지지 않는 일이 없다 (단조 비감소).
         *
         * 원곡선이 단조 증가이고 반올림은 단조를 보존하므로 수학적으로는 자명한데,
         * 그 "자명"이 지키는 것은 구현이다 - 치명타 확률의 관문(두 구간 비용)처럼
         * 곡선이 두 식으로 갈리는 자리에서 한 칸이 어긋나면 여기서 걸린다.
         */
        [Test]
        public void EveryAxis_CostNeverStepsDown()
        {
            foreach (var axis in Axes())
            {
                double previous = axis.Cost(1);
                for (int level = 2; level <= axis.Levels; level++)
                {
                    double cost = axis.Cost(level);
                    Assert.GreaterOrEqual(cost, previous, string.Format(
                        "{0} Lv.{1}: {2} -> {3} 으로 계단이 내려간다", axis.Name, level, previous, cost));
                    previous = cost;
                }
            }
        }

        // ---------------------------------------------------------------- 트랙 대조

        /**
         * @brief 관문 축(치명타 확률)의 트랙이 곡선 statics와 같은 값을 낸다.
         *
         * SimulationCurves_MatchTheLiveUpgradeTracks는 공격력·공격속도만 60레벨까지
         * 대조한다. 정수화가 들어온 지금, 두 구간 비용이 갈리는 관문(Lv.545 언저리)과
         * 심층까지 같은 규칙을 지나는지는 여기서 못 박는다.
         */
        [Test]
        public void CritRateTrack_MatchesTheCurveAcrossTheWall()
        {
            var track = new UpgradeTrack(UpgradeSystem.CritRateId, "치명타 확률",
                BigDouble.FromDouble(CritRateCurve.BaseCost), CritRateCurve.CostGrowth,
                UpgradeTrack.Curve.Additive,
                BigDouble.FromDouble(CritRateCurve.BaseValue), CritRateCurve.Step,
                CritRateCurve.MaxLevel, CritRateCurve.Ceiling);
            track.SetWall(CritRateCurve.DeepPhaseLevel - 1,
                CritRateCurve.WallJump, CritRateCurve.DeepGrowth);

            for (int level = 1; level <= CritRateCurve.MaxLevel; level++)
            {
                double fromCurve = CritRateCurve.CostAtLevel(level);
                double fromTrack = track.CostAtLevel(level).ToDouble();

                Assert.AreEqual(fromCurve, fromTrack, fromCurve * 1e-9d,
                    "치명타 확률 비용: Lv." + level + "에서 트랙과 곡선이 갈라졌다");
            }
        }
    }
}
