using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 강화 곡선 검증.
     *
     * 방치형에서 밸런스가 깨지는 방식은 대개 조용하다. 비용이 충분히 빨리 오르지 않으면
     * 플레이어가 남은 레벨을 한 번에 전부 사버리고 성장할 것이 사라지는데, 그것을
     * 알아채려면 몇 시간을 방치해봐야 한다. 곡선은 여기서 못 박는다.
     */
    public class UpgradeTrackTests
    {
        // UpgradePanelBuilder가 실제로 기록하는 값
        const double PowerBaseCost = 10d;
        const double PowerCostGrowth = 1.15d;
        const double PowerBaseValue = 5d;
        const double PowerStep = 1.12d;

        const double SpeedBaseValue = 1.15d;
        const double SpeedStep = 1.04d;
        const int SpeedMaxLevel = 51;

        private static UpgradeTrack AttackPower()
        {
            return new UpgradeTrack(UpgradeSystem.AttackPowerId, "공격력 강화",
                                    BigDouble.FromDouble(PowerBaseCost), PowerCostGrowth,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(PowerBaseValue), PowerStep);
        }

        private static UpgradeTrack AttackSpeed()
        {
            return new UpgradeTrack(UpgradeSystem.AttackSpeedId, "공격속도 강화",
                                    BigDouble.FromDouble(4d), 1.15d,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(SpeedBaseValue), SpeedStep,
                                    SpeedMaxLevel);
        }

        /**
         * @brief 합연산 곡선 자체를 검사하기 위한 표본.
         *
         * 게임에서 쓰는 축은 이제 둘 다 곱연산이지만, UpgradeTrack이 두 형태를 모두
         * 지원하므로 합연산 쪽도 계속 검사한다.
         */
        private static UpgradeTrack AdditiveSample()
        {
            return new UpgradeTrack("additive_sample", "합연산 표본",
                                    BigDouble.FromDouble(25d), 1.35d,
                                    UpgradeTrack.Curve.Additive,
                                    BigDouble.FromDouble(1.15d), 0.12d,
                                    60);
        }

        private static PlayerWallet WalletWith(double gold)
        {
            var go = new GameObject("~TestWallet");
            var wallet = go.AddComponent<PlayerWallet>();
            wallet.SetBalance(BigDouble.FromDouble(gold), BigDouble.FromDouble(gold));
            return wallet;
        }

        [Test]
        public void FirstPurchase_CostsTheBasePrice()
        {
            var track = AttackPower();
            Assert.AreEqual(1, track.Level);
            Assert.AreEqual(PowerBaseCost, track.Cost.ToDouble(), 1e-6d);
        }

        [Test]
        public void Cost_GrowsExponentially()
        {
            var track = AttackPower();
            var wallet = WalletWith(1e9d);

            for (int level = 1; level <= 20; level++)
            {
                double expected = PowerBaseCost * System.Math.Pow(PowerCostGrowth, level - 1);
                Assert.AreEqual(expected, track.Cost.ToDouble(), expected * 1e-6d,
                    "cost wrong at level " + level);
                Assert.IsTrue(track.TryPurchase(wallet));
            }

            Object.DestroyImmediate(wallet.gameObject);
        }

        [Test]
        public void Value_MultiplicativeTrackCompounds()
        {
            var track = AttackPower();
            Assert.AreEqual(PowerBaseValue, track.ValueAtLevel(1).ToDouble(), 1e-6d);
            Assert.AreEqual(PowerBaseValue * PowerStep, track.ValueAtLevel(2).ToDouble(), 1e-6d);

            double expected = PowerBaseValue * System.Math.Pow(PowerStep, 29);
            Assert.AreEqual(expected, track.ValueAtLevel(30).ToDouble(), expected * 1e-6d);
        }

        [Test]
        public void Value_AdditiveTrackStaysLinear()
        {
            var track = AdditiveSample();
            Assert.AreEqual(1.15d, track.ValueAtLevel(1).ToDouble(), 1e-6d);
            Assert.AreEqual(1.15d + 0.12d * 9, track.ValueAtLevel(10).ToDouble(), 1e-6d);
        }

        [Test]
        public void AttackSpeedAtMaxLevel_ExceedsTheAnimationCap()
        {
            // 공격 애니메이션 원본은 7프레임 14fps = 0.5초라, 압축이 없으면 실제 공격은
            // 초당 2회에서 멈춘다. 상한 레벨의 값이 그보다 한참 높아야 스윙 압축이
            // 동작하는지 여부가 플레이 중에 드러난다
            var track = AttackSpeed();
            double atMax = track.ValueAtLevel(SpeedMaxLevel).ToDouble();

            Assert.Greater(atMax, 2d, "max attack speed no longer exceeds the animation cap");
            Assert.AreEqual(SpeedBaseValue * System.Math.Pow(SpeedStep, SpeedMaxLevel - 1), atMax, 1e-6d);
        }

        [Test]
        public void Purchase_DeductsExactlyTheCost()
        {
            var track = AttackPower();
            var wallet = WalletWith(100d);

            var cost = track.Cost;
            Assert.IsTrue(track.TryPurchase(wallet));

            Assert.AreEqual(100d - cost.ToDouble(), wallet.Gold.ToDouble(), 1e-6d);
            Assert.AreEqual(2, track.Level);

            Object.DestroyImmediate(wallet.gameObject);
        }

        [Test]
        public void Purchase_WithoutEnoughGold_ChangesNothing()
        {
            var track = AttackPower();
            var wallet = WalletWith(PowerBaseCost - 1d);

            Assert.IsFalse(track.TryPurchase(wallet));
            Assert.AreEqual(1, track.Level, "level moved without paying");
            Assert.AreEqual(PowerBaseCost - 1d, wallet.Gold.ToDouble(), 1e-6d, "gold was spent on a failed purchase");

            Object.DestroyImmediate(wallet.gameObject);
        }

        [Test]
        public void MaxedTrack_RefusesFurtherPurchases()
        {
            var track = AttackSpeed();
            var wallet = WalletWith(1e30d);

            track.SetLevel(SpeedMaxLevel);
            Assert.IsTrue(track.IsMaxed);
            Assert.IsFalse(track.TryPurchase(wallet));
            Assert.AreEqual(SpeedMaxLevel, track.Level);

            // 잔액이 남아돌아도 상한을 넘지 않는다
            Assert.AreEqual(1e30d, wallet.Gold.ToDouble(), 1e24d);

            Object.DestroyImmediate(wallet.gameObject);
        }

        /**
         * @brief SetLevel은 아래로만 막는다. 위로는 막지 않는다.
         *
         * 9단계에서 바뀐 동작이다. 예전에는 maxLevel로도 잘랐는데, 그러면 상한이
         * 내려간 업데이트에서 플레이어가 산 레벨이 영구히 사라진다 - 공격속도 상한을
         * 51에서 32로 낮췄을 때 Lv.44 세이브가 정확히 그렇게 됐다.
         *
         * 이제 레벨은 남고 효과만 valueCeiling에서 막힌다. 구매는 여전히 IsMaxed가
         * 막으므로 UI 동작은 달라지지 않는다.
         */
        [Test]
        public void SetLevel_ClampsBelowOneButKeepsLevelsPastTheCap()
        {
            var track = AttackSpeed();

            track.SetLevel(0);
            Assert.AreEqual(1, track.Level, "level 0 would make the base value unreachable");

            track.SetLevel(SpeedMaxLevel + 50);
            Assert.AreEqual(SpeedMaxLevel + 50, track.Level,
                "저장된 레벨이 잘렸다. 상한이 내려간 업데이트에서 플레이어가 산 것이 사라진다");
            Assert.IsTrue(track.IsMaxed, "상한 위인데 더 살 수 있는 상태로 보인다");
        }

        [Test]
        public void UnboundedTrack_HasNoCeiling()
        {
            var track = AttackPower();
            Assert.AreEqual(0, track.MaxLevel);

            track.SetLevel(5000);
            Assert.AreEqual(5000, track.Level);
            Assert.IsFalse(track.IsMaxed);
        }
    }
}
