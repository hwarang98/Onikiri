using System;
using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;
using UnityEngine.TestTools;

namespace Onikiri.Tests
{
    /**
     * @brief 방치 보상 계산 검증.
     *
     * 이 계산이 틀렸을 때 알아채는 유일한 방법이 "몇 시간 앱을 꺼놨다가 켜보기"라서
     * 테스트가 특히 중요하다. 너무 후하면 게임을 켤 이유가 사라지고, 너무 박하면
     * 방치형이 아니게 된다.
     */
    public class IdleIncomeTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void AccruedTime_CapsAtEightHours()
        {
            Assert.AreEqual(TimeSpan.FromHours(3d), IdleIncome.AccruedTime(Noon, Noon.AddHours(3d)));
            Assert.AreEqual(IdleIncome.MaxAccrual, IdleIncome.AccruedTime(Noon, Noon.AddHours(30d)));
            Assert.AreEqual(TimeSpan.FromHours(8d), IdleIncome.MaxAccrual);
        }

        [Test]
        public void ClockMovedBackwards_PaysNothing()
        {
            // 기기 시계를 뒤로 돌리는 것은 흔한 일이고(시간대 이동, 수동 변경),
            // 음수 경과가 그대로 흘러가면 보상이 음수가 되거나 예외가 난다
            Assert.AreEqual(TimeSpan.Zero, IdleIncome.AccruedTime(Noon, Noon.AddHours(-5d)));
            Assert.AreEqual(BigDouble.Zero, IdleIncome.Reward(100d, TimeSpan.FromHours(-1d)));
        }

        [Test]
        public void Reward_IsRateTimesTimeTimesEfficiency()
        {
            // 초당 10골드로 1시간 = 36000, 효율 0.5 -> 18000
            var reward = IdleIncome.Reward(10d, TimeSpan.FromHours(1d));
            Assert.AreEqual(18000d, reward.ToDouble(), 1d);
            Assert.AreEqual(0.5d, IdleIncome.Efficiency, 1e-9d);
        }

        [Test]
        public void IsCapped_OnlyBeyondTheLimit()
        {
            Assert.IsFalse(IdleIncome.IsCapped(Noon, Noon.AddHours(7.9d)));
            Assert.IsTrue(IdleIncome.IsCapped(Noon, Noon.AddHours(8.1d)));
        }

        [Test]
        public void GoldPerSecond_IsLimitedByEnemySupply()
        {
            // 공격력이 터무니없이 높아도 요괴는 스폰 간격보다 빨리 들어오지 않는다.
            // 이 상한을 빼면 방치 보상이 실제 플레이보다 훨씬 후해진다
            const float spawnInterval = 1.1f;

            double huge = IdleIncome.GoldPerSecond(
                BigDouble.FromDouble(1e9d), 30f,
                BigDouble.FromDouble(14.2d), BigDouble.FromDouble(5.4d), spawnInterval);

            double supplyCap = 5.4d / spawnInterval;
            Assert.AreEqual(supplyCap, huge, 1e-6d);
        }

        [Test]
        public void GoldPerSecond_ScalesWithDamageBelowTheSupplyCap()
        {
            // 스폰 간격을 짧게 잡아 공급 상한을 초당 2마리로 밀어둔다. 두 표본 모두
            // 상한 아래에 있어야 이 테스트가 이름대로 "데미지에 비례해서 오른다"를
            // 검증한다. 상한이 먼저 걸리면 무엇을 재는지 알 수 없는 테스트가 된다.
            // 상한 자체는 GoldPerSecond_IsLimitedByEnemySupply가 따로 못 박는다
            const float spawnInterval = 0.5f;

            // 처치당 5타 -> 초당 1회 공격이면 0.2마리/초
            double slow = IdleIncome.GoldPerSecond(
                BigDouble.FromDouble(3d), 1f,
                BigDouble.FromDouble(15d), BigDouble.FromDouble(5d), spawnInterval);

            Assert.AreEqual(0.2d * 5d, slow, 1e-6d);

            // 데미지를 5배로 올리면 처치당 1타가 되어 수입도 정확히 5배가 된다.
            // 공격력 강화가 끝까지 의미를 갖는다는 것이 이 한 줄이다
            double fast = IdleIncome.GoldPerSecond(
                BigDouble.FromDouble(15d), 1f,
                BigDouble.FromDouble(15d), BigDouble.FromDouble(5d), spawnInterval);

            Assert.AreEqual(1d * 5d, fast, 1e-6d);
            Assert.AreEqual(5d * slow, fast, 1e-6d);
        }

        [Test]
        public void ZeroDamageOrSpeed_PaysNothing()
        {
            // 세이브가 적용되기 전이나 손상된 상태에서 0으로 나누지 않아야 한다
            Assert.AreEqual(0d, IdleIncome.GoldPerSecond(
                BigDouble.Zero, 1f, BigDouble.FromDouble(10d), BigDouble.FromDouble(5d), 1.1f));
            Assert.AreEqual(0d, IdleIncome.GoldPerSecond(
                BigDouble.FromDouble(5d), 0f, BigDouble.FromDouble(10d), BigDouble.FromDouble(5d), 1.1f));
        }

        // ---------------------------------------------------------------- 세이브

        [Test]
        public void SaveData_SurvivesAJsonRoundTrip()
        {
            var original = SaveData.NewGame();
            original.gold = BigDouble.FromDouble(1.234e40d);
            original.lifetimeGold = BigDouble.FromDouble(9.9e41d);
            original.upgradeIds = new[] { UpgradeSystem.AttackPowerId, UpgradeSystem.AttackSpeedId };
            original.upgradeLevels = new[] { 37, 12 };
            original.stage = 23;
            original.killsThisStage = 4;
            original.lastQuitUtcTicks = Noon.Ticks;
            original.goldPerSecond = 1234.5d;

            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original));

            // 큰 수가 double로 뭉개지지 않는지가 핵심이다. 방치형 세이브는 금방
            // 1e40을 넘긴다
            Assert.AreEqual(original.gold.Mantissa, restored.gold.Mantissa, 1e-12d);
            Assert.AreEqual(original.gold.Exponent, restored.gold.Exponent);
            Assert.AreEqual(original.lifetimeGold.Exponent, restored.lifetimeGold.Exponent);

            Assert.AreEqual(original.upgradeIds, restored.upgradeIds);
            Assert.AreEqual(original.upgradeLevels, restored.upgradeLevels);
            Assert.AreEqual(23, restored.stage);
            Assert.AreEqual(4, restored.killsThisStage);
            Assert.AreEqual(Noon, restored.LastQuitUtc);
            Assert.AreEqual(1234.5d, restored.goldPerSecond, 1e-9d);
        }

        [Test]
        public void NewGame_HasNoQuitTimeSoNoOfflineReward()
        {
            // 첫 실행에 방치 보상이 나오면 안 된다. 기준 시각이 없으면 경과가
            // 1970년부터가 되어 항상 상한을 받는다
            var data = SaveData.NewGame();
            Assert.IsNull(data.LastQuitUtc);
        }

        [Test]
        public void OutOfRangeTimestamp_IsIgnoredInsteadOfThrowing()
        {
            var data = SaveData.NewGame();
            data.lastQuitUtcTicks = long.MaxValue;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("out-of-range"));
            Assert.IsNull(data.LastQuitUtc);
        }
    }
}
