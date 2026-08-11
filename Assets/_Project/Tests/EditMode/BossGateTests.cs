using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 보스 게이트의 순수 로직 - 실패 안내와 세이브 마이그레이션.
     *
     * 둘 다 화면을 봐야만 틀린 것을 알 수 있는 종류의 코드다. 실패 문구가 엉뚱한
     * 축을 가리켜도 게임은 정상으로 돌고, 마이그레이션이 잘못돼도 진행이 사라진
     * 뒤에야 드러난다.
     */
    public class BossGateTests
    {
        static UpgradeTrack Power(int level)
        {
            var track = new UpgradeTrack(UpgradeSystem.AttackPowerId, "공격력 강화",
                                         BigDouble.FromDouble(10d), 1.15d,
                                         UpgradeTrack.Curve.Multiplicative,
                                         BigDouble.FromDouble(5d), 1.12d);
            track.SetLevel(level);
            return track;
        }

        static UpgradeTrack Speed(int level)
        {
            var track = new UpgradeTrack(UpgradeSystem.AttackSpeedId, "공격속도 강화",
                                         BigDouble.FromDouble(AttackSpeedCurve.BaseCost),
                                         AttackSpeedCurve.CostGrowth,
                                         UpgradeTrack.Curve.Multiplicative,
                                         BigDouble.FromDouble(AttackSpeedCurve.BaseValue),
                                         AttackSpeedCurve.Step,
                                         AttackSpeedCurve.MaxLevel);
            track.SetLevel(level);
            return track;
        }

        // ---------------------------------------------------------------- 실패 안내

        [Test]
        public void ShortfallFactor_IsTheRatioOfHealthToDamageDealt()
        {
            // 30초 동안 체력의 절반을 깎았으면 두 배가 필요했다
            Assert.AreEqual(2d, BossFailureAdvice.ShortfallFactor(
                BigDouble.FromDouble(1000d), BigDouble.FromDouble(500d)), 1e-9d);

            Assert.AreEqual(4d, BossFailureAdvice.ShortfallFactor(
                BigDouble.FromDouble(1000d), BigDouble.FromDouble(250d)), 1e-9d);
        }

        [Test]
        public void ShortfallFactor_IsZeroWhenNoDamageLanded()
        {
            // 한 대도 못 때린 경우. 나눗셈이 무한대가 되므로 문구가 배율을 말하면 안 된다
            Assert.AreEqual(0d, BossFailureAdvice.ShortfallFactor(
                BigDouble.FromDouble(1000d), BigDouble.Zero));
        }

        [Test]
        public void BestAxis_SkipsMaxedTracks()
        {
            // 공격속도가 상한이면 "공격속도를 올려라"는 실행할 수 없는 지시가 된다
            var maxed = Speed(AttackSpeedCurve.MaxLevel);
            Assert.IsTrue(maxed.IsMaxed);

            var best = BossFailureAdvice.BestAxis(new[] { Power(20), maxed });
            Assert.AreEqual(UpgradeSystem.AttackPowerId, best.Id);
        }

        /**
         * @brief 레벨이 낮아 싼 쪽을 고른다.
         *
         * 두 축의 골드당 효율은 같은 레벨에서 1.2배 차이로 일정하지만, 레벨이
         * 다르면 비용이 달라 역전된다. 공격력이 훨씬 앞서 있으면 다음 1레벨은
         * 공격속도가 압도적으로 싸고, 안내도 그쪽을 가리켜야 한다.
         */
        [Test]
        public void BestAxis_FollowsWhicheverIsCheaperRightNow()
        {
            var laggingSpeed = BossFailureAdvice.BestAxis(new[] { Power(40), Speed(2) });
            Assert.AreEqual(UpgradeSystem.AttackSpeedId, laggingSpeed.Id);

            var laggingPower = BossFailureAdvice.BestAxis(new[] { Power(2), Speed(25) });
            Assert.AreEqual(UpgradeSystem.AttackPowerId, laggingPower.Id);
        }

        [Test]
        public void Message_NamesTheShortfallAndTheAxis()
        {
            var message = BossFailureAdvice.Message(
                BigDouble.FromDouble(1000d), BigDouble.FromDouble(500d),
                new[] { Power(2), Speed(25) });

            StringAssert.Contains("2.0배", message);
            StringAssert.Contains("공격력 강화", message);
            // 두 줄이라야 "무슨 일이 있었나"와 "무엇을 하나"가 나뉜다
            StringAssert.Contains("\n", message);
        }

        [Test]
        public void Message_StaysUsefulWhenNothingLanded()
        {
            var message = BossFailureAdvice.Message(
                BigDouble.FromDouble(1000d), BigDouble.Zero, new[] { Power(2) });

            StringAssert.DoesNotContain("배", message.Split('\n')[0]);
            StringAssert.Contains("공격력 강화", message);
        }

        // ---------------------------------------------------------------- 세이브

        [Test]
        public void Migrate_LiftsAV1SaveWithoutLosingProgress()
        {
            var data = new SaveData
            {
                version = 1,
                gold = BigDouble.FromDouble(12345d),
                lifetimeGold = BigDouble.FromDouble(99999d),
                stage = 7,
                killsThisStage = 4,
                upgradeIds = new[] { UpgradeSystem.AttackPowerId },
                upgradeLevels = new[] { 31 }
            };

            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(7, data.stage, "the stage moved during migration");
            Assert.AreEqual(4, data.killsThisStage);
            Assert.AreEqual(12345d, data.gold.ToDouble(), 1e-6d);
            Assert.AreEqual(241, data.upgradeLevels[0],
                "미세화 환산(43단계): 옛 Lv.31 = 새 (30x8)+1 = 241 - 가치 보존이 곧 유지다");

            // v1에는 보스가 없었다. 넘어온 스테이지 수를 보스 처치 수로 친다
            Assert.AreEqual(6, data.bossKillCount);
        }

        [Test]
        public void Migrate_IsIdempotent()
        {
            var data = SaveData.NewGame();
            data.stage = 3;
            data.bossKillCount = 2;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(2, data.bossKillCount, "a second migration recomputed a value it should not touch");
        }

        [Test]
        public void Migrate_RefusesAFutureVersion()
        {
            var data = SaveData.NewGame();
            data.version = SaveData.CurrentVersion + 1;

            Assert.IsFalse(SaveData.Migrate(data),
                "a save from a newer build must not be read - its fields mean something else");
        }

        [Test]
        public void Migrate_HandlesAFreshStageOneSave()
        {
            var data = new SaveData { version = 1, stage = 1, killsThisStage = 0 };

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(0, data.bossKillCount, "stage 1 means no boss has been cleared");
        }
    }
}
