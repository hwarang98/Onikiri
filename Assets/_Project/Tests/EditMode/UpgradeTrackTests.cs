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
        const double SpeedStep = 0.12d;
        const int SpeedMaxLevel = 60;

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
                                    BigDouble.FromDouble(25d), 1.35d,
                                    UpgradeTrack.Curve.Additive,
                                    BigDouble.FromDouble(SpeedBaseValue), SpeedStep,
                                    SpeedMaxLevel);
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
            var track = AttackSpeed();
            Assert.AreEqual(SpeedBaseValue, track.ValueAtLevel(1).ToDouble(), 1e-6d);
            Assert.AreEqual(SpeedBaseValue + SpeedStep * 9, track.ValueAtLevel(10).ToDouble(), 1e-6d);
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
            Assert.AreEqual(SpeedBaseValue + SpeedStep * (SpeedMaxLevel - 1), atMax, 1e-6d);
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

        [Test]
        public void SetLevel_ClampsToTheAllowedRange()
        {
            var track = AttackSpeed();

            track.SetLevel(0);
            Assert.AreEqual(1, track.Level, "level 0 would make the base value unreachable");

            track.SetLevel(SpeedMaxLevel + 50);
            Assert.AreEqual(SpeedMaxLevel, track.Level);
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
