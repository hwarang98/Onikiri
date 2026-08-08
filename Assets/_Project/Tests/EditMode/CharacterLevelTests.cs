using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 경험치 곡선, 스탯 포인트, 그리고 v5 세이브.
     *
     * 여기서 못 박는 것은 값 자체가 아니라 **관계**다. 경험치 획득이 골드보다
     * 느리게 자란다는 것, 초과분이 사라지지 않는다는 것, 상수를 선언만 하고
     * 쓰지 않는 상태가 아니라는 것.
     *
     * 마지막 항목은 11단계에서 실제로 겪었다. ChapterHealthMultiplier가 선언돼
     * 있고 "1보다 크다"는 테스트까지 있었는데 값 흐름 어디에도 연결되지 않아서,
     * 값을 바꿔도 게임이 달라지지 않았다. 상수는 선언이 아니라 **끝단에서**
     * 재야 한다.
     */
    public class CharacterLevelTests
    {
        // ------------------------------------------------------------ 경험치 곡선

        [Test]
        public void RequiredExp_GrowsWithLevel()
        {
            for (int level = 1; level < 200; level++)
            {
                var here = ExpCurve.RequiredForLevel(level);
                var next = ExpCurve.RequiredForLevel(level + 1);
                Assert.Greater(next.ToDouble(), here.ToDouble(),
                    "Lv." + level + " -> Lv." + (level + 1) + " must cost more");
            }
        }

        [Test]
        public void MobExp_GrowsWithStage()
        {
            Assert.AreEqual(ExpCurve.MobBaseExp, ExpCurve.MobExp(1).ToDouble(), 1e-9);

            for (int stage = 1; stage < 100; stage++)
                Assert.Greater(ExpCurve.MobExp(stage + 1).ToDouble(), ExpCurve.MobExp(stage).ToDouble());
        }

        /**
         * @brief 경험치는 골드보다 **느리게** 자라야 한다.
         *
         * 이것이 12단계 밸런스의 뼈대다. 둘이 같은 속도로 자라면 레벨은 골드를
         * 다시 표시한 값에 지나지 않고, 경험치가 더 빠르면 스탯 포인트가 골드
         * 강화를 밀어내 게임이 "레벨만 올리면 되는 것"이 된다.
         *
         * 스테이지 하나를 건너뛸 때의 배수로 잰다 - 두 재화 모두 스테이지당
         * 지수라 이 비교가 곧 전 구간의 비교다.
         */
        [Test]
        public void ExpGrowth_IsSlowerThanGoldGrowth()
        {
            Assert.Less(ExpCurve.ExpStageGrowth, StageCurve.GoldGrowth,
                "exp must grow slower than gold or stat points overtake the gold axes");

            // 끝단에서도 확인한다. 상수만 보면 두 값이 서로 다른 식에 들어가
            // 있을 가능성이 남는다
            double goldRatio = StageCurve.GoldMultiplier(21).ToDouble()
                               / StageCurve.GoldMultiplier(1).ToDouble();
            double expRatio = ExpCurve.MobExp(21).ToDouble() / ExpCurve.MobExp(1).ToDouble();

            Assert.Less(expRatio, goldRatio,
                "over 20 stages gold must outgrow exp (gold x" + goldRatio + ", exp x" + expRatio + ")");
        }

        [Test]
        public void BossExp_IsWorthAStageOfFarming()
        {
            // 일반 스테이지 보스. 할당량(10마리)보다 후해야 파밍보다 보스가 낫다
            var mob = ExpCurve.MobExp(2);
            var boss = ExpCurve.BossExp(2);

            Assert.IsFalse(BossCurve.IsChapterBoss(2), "stage 2 must be a normal boss for this test");
            Assert.Greater(boss.ToDouble(), mob.ToDouble() * StageCurve.KillsPerStage,
                "a boss must give more than the whole kill quota");
        }

        /** 챕터 배수가 선언만 되어 있지 않고 실제로 곱해지는지 */
        [Test]
        public void ChapterExpMultiplier_IsActuallyApplied()
        {
            Assert.IsTrue(BossCurve.IsChapterBoss(5), "stage 5 is expected to be a chapter boss");
            Assert.IsFalse(BossCurve.IsChapterBoss(4));

            double normal = ExpCurve.BossExp(4).ToDouble() / ExpCurve.MobExp(4).ToDouble();
            double chapter = ExpCurve.BossExp(5).ToDouble() / ExpCurve.MobExp(5).ToDouble();

            Assert.AreEqual(ExpCurve.BossExpMultiplier, normal, 1e-6);
            Assert.AreEqual(ExpCurve.BossExpMultiplier * ExpCurve.ChapterExpMultiplier, chapter, 1e-6,
                "chapter bosses must actually pay the chapter multiplier");
        }

        // ------------------------------------------------------------ 초과분

        [Test]
        public void LevelsAffordable_CountsMultipleLevels()
        {
            var exactlyOne = ExpCurve.RequiredForLevel(1);
            Assert.AreEqual(1, ExpCurve.LevelsAffordable(1, exactlyOne));

            var oneShort = exactlyOne * BigDouble.FromDouble(0.999d);
            Assert.AreEqual(0, ExpCurve.LevelsAffordable(1, oneShort));

            var two = ExpCurve.RequiredForLevel(1) + ExpCurve.RequiredForLevel(2);
            Assert.AreEqual(2, ExpCurve.LevelsAffordable(1, two));
        }

        /**
         * @brief 초과분은 다음 레벨로 넘어간다.
         *
         * 방치형에서 자리를 비운 사이 여러 레벨분이 쌓이는 것은 정상이고, 그때
         * 초과분을 버리면 오래 비울수록 손해가 되어 게임이 자리를 지키라고
         * 요구하게 된다.
         */
        [Test]
        public void LevelUp_KeepsTheOverflow()
        {
            var need = ExpCurve.RequiredForLevel(1);
            var held = need * BigDouble.FromDouble(2.5d);

            int gained = ExpCurve.LevelsAffordable(1, held);
            Assert.GreaterOrEqual(gained, 1);

            // 올린 뒤 남는 양을 직접 계산해 확인한다
            var remaining = held;
            for (int i = 0; i < gained; i++)
                remaining = remaining - ExpCurve.RequiredForLevel(1 + i);

            Assert.Greater(remaining.ToDouble(), 0d, "overflow must survive the level-ups");
            Assert.Less(remaining.ToDouble(), ExpCurve.RequiredForLevel(1 + gained).ToDouble(),
                "if the leftover still affords a level, LevelsAffordable under-counted");
        }

        // ------------------------------------------------------------ 스탯 포인트

        [Test]
        public void StatPoints_CompoundAndCap()
        {
            Assert.AreEqual(1d, StatPointCurve.Multiplier(0), 1e-9);
            Assert.AreEqual(StatPointCurve.PerPoint, StatPointCurve.Multiplier(1), 1e-9);

            // 상한 위로 넣어도 상한에서 멈춘다
            Assert.AreEqual(StatPointCurve.Multiplier(StatPointCurve.MaxPoints),
                            StatPointCurve.Multiplier(StatPointCurve.MaxPoints + 1000), 1e-9);
        }

        /**
         * @brief 증폭이 골드 축을 대체하지 않는 크기인가.
         *
         * 상한까지 다 찍은 증폭이 골드 축 몇 레벨에 해당하는지로 잰다. 이 값이
         * 너무 크면 레벨만 올리는 것이 최적 전략이 되고, 너무 작으면 스탯
         * 포인트를 찍을 이유가 없다.
         */
        [Test]
        public void MaxedStatPoints_StayASideAxis()
        {
            double amp = StatPointCurve.Multiplier(StatPointCurve.MaxPoints);

            // 같은 배수를 공격력 강화로 내려면 몇 레벨이 필요한가
            double levels = System.Math.Log(amp) / System.Math.Log(AttackPowerCurve.Step);

            Assert.Greater(levels, 20d, "maxed stat points are too weak to be worth spending");
            Assert.Less(levels, 120d, "maxed stat points would overshadow the gold axes");
        }

        [Test]
        public void TotalPoints_StartsAtZeroOnLevelOne()
        {
            Assert.AreEqual(0, StatPointCurve.TotalPointsAtLevel(1));
            Assert.AreEqual(StatPointCurve.PointsPerLevel, StatPointCurve.TotalPointsAtLevel(2));
            Assert.AreEqual(9 * StatPointCurve.PointsPerLevel, StatPointCurve.TotalPointsAtLevel(10));
        }

        // ------------------------------------------------------------ 세이브 v5

        static SaveData V4Save()
        {
            var data = SaveData.NewGame();
            data.version = 4;
            data.stage = 7;
            data.gold = BigDouble.FromDouble(1234d);
            data.upgradeIds = new[] { UpgradeSystem.AttackPowerId };
            data.upgradeLevels = new[] { 15 };

            // v4에는 이 필드들이 없었다. JsonUtility가 기본값으로 채워 넣는 상태를
            // 그대로 흉내낸다 - 0은 유효한 레벨이 아니다
            data.characterLevel = 0;
            data.exp = BigDouble.Zero;
            return data;
        }

        [Test]
        public void Migrate_V4_StartsAtLevelOne()
        {
            var data = V4Save();
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(1, data.characterLevel, "level 0 would render as 'Lv.0'");
            Assert.AreEqual(0, data.attackPoints);
            Assert.AreEqual(0, data.healthPoints);

            // 나머지 진행은 그대로여야 한다. 형식을 올리는 것이 진행을 지우는
            // 일이 되면 업데이트마다 플레이어가 처음부터 시작한다
            Assert.AreEqual(7, data.stage);
            Assert.AreEqual(15, data.upgradeLevels[0]);
        }

        /** 저장 실패 후 재시도 같은 경로에서 실제로 두 번 돈다 */
        [Test]
        public void Migrate_IsIdempotent()
        {
            var data = V4Save();
            Assert.IsTrue(SaveData.Migrate(data));

            data.characterLevel = 12;
            data.exp = BigDouble.FromDouble(500d);
            data.attackPoints = 7;

            Assert.IsTrue(SaveData.Migrate(data), "a v5 save must migrate cleanly");
            Assert.AreEqual(12, data.characterLevel, "a second pass must not reset progress");
            Assert.AreEqual(500d, data.exp.ToDouble(), 1e-9);
            Assert.AreEqual(7, data.attackPoints);
        }

        [Test]
        public void Migrate_RejectsFutureVersions()
        {
            var data = SaveData.NewGame();
            data.version = SaveData.CurrentVersion + 1;

            Assert.IsFalse(SaveData.Migrate(data),
                "a save from a newer build must not be silently reinterpreted");
        }

        [Test]
        public void NewGame_IsCurrentVersionWithLevelOne()
        {
            var data = SaveData.NewGame();
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(1, data.characterLevel);
            Assert.AreEqual(0d, data.exp.ToDouble(), 1e-9);
        }
    }
}
