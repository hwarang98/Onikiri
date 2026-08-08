using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 세이브 마이그레이션. **진행을 지우지 않는 것이 전부다.**
     *
     * 8단계까지는 형식이 바뀌면 새 게임으로 되돌렸고, 그것은 실제 배포에서
     * "업데이트했더니 처음부터"가 된다. 그 뒤로 Migrate가 버전을 하나씩 올린다.
     *
     * 여기서 지키는 성질은 셋이다.
     *
     *   멱등     두 번 돌아도 레벨이 초기화되지 않는다 (저장 실패 후 재시도 경로)
     *   무해     새 축이 생겼다고 예전 진행의 밸런스가 달라지지 않는다
     *   거부     모르는(더 높은) 버전은 올리지 않는다
     */
    public class SaveMigrationTests
    {
        /** 26단계 이전의 세이브. 오의 칸이 아예 없다 */
        static SaveData V6Save()
        {
            return new SaveData
            {
                version = 6,
                stage = 12,
                characterLevel = 18,
                upgradeIds = new[]
                {
                    UpgradeSystem.AttackPowerId, UpgradeSystem.AttackSpeedId,
                    UpgradeSystem.CritRateId, UpgradeSystem.CritDamageId,
                    UpgradeSystem.HealthId, UpgradeSystem.HealthRegenId,
                    UpgradeSystem.GoldGainId
                },
                upgradeLevels = new[] { 52, 32, 60, 57, 30, 20, 13 },

                // JsonUtility가 없는 필드를 채우는 방식 그대로 흉내낸다.
                // 배열은 빈 배열, bool은 false다 - 그 false가 문제의 자리다
                skillIds = new string[0],
                skillLevels = new int[0],
                skillAutoCast = false
            };
        }

        [Test]
        public void V6_MigratesToV7WithEverySkillAtLevelOne()
        {
            var data = V6Save();
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(9, SaveData.CurrentVersion, "버전이 또 올랐으면 이 테스트도 함께 봐야 한다");

            Assert.AreEqual(SkillCatalog.Count, data.skillIds.Length,
                "v6 -> v7이 오의 칸을 다 만들지 않았다");

            foreach (var skill in SkillCatalog.Skills)
            {
                int index = System.Array.IndexOf(data.skillIds, skill.Id);
                Assert.GreaterOrEqual(index, 0, "'" + skill.DisplayName + "'이 세이브에 없다");
                Assert.AreEqual(1, data.skillLevels[index],
                    "'" + skill.DisplayName + "'이 레벨 1이 아니다");
            }
        }

        /** 31단계 이전의 세이브. 퀘스트·보석 칸이 아예 없다 */
        static SaveData V7Save()
        {
            var data = V6Save();
            data.version = 7;
            data.skillAutoCast = true;
            data.skillIds = new[] { SkillCatalog.ChainSlashId, SkillCatalog.FlashId, SkillCatalog.OniCleaveId };
            data.skillLevels = new[] { 5, 3, 2 };
            return data;
        }

        /**
         * @brief v7이 **퀘스트 0진행 · 보석 0**으로 올라온다.
         *
         * 소급하지 않는 것이 요점이다. 지금까지 잡은 요괴를 누적 카운터에 넣어주면
         * 예전 플레이어가 접속하자마자 반복 티어 수십 개를 한꺼번에 받는데,
         * 그것은 리텐션(내일 또 올 이유)을 첫날에 소진시키고 업적 골드를 한
         * 스테이지에 몰아 떨어뜨린다. v4 -> v5가 레벨을 소급하지 않은 것과
         * 같은 판단이다.
         */
        [Test]
        public void V7_MigratesToV8WithNoQuestProgressAndNoGems()
        {
            var data = V7Save();
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0L, data.gems, "보석을 소급해 줬다");

            Assert.AreEqual(0d, data.questTotalMobKills, "누적 처치를 소급해 줬다");
            Assert.AreEqual(0d, data.questTotalBossKills);
            Assert.AreEqual(0d, data.questTotalSkillCasts);
            Assert.AreEqual(0d, data.questTotalUpgrades);
            Assert.IsTrue(data.questTotalGold.IsZero);

            Assert.AreEqual(0d, data.questTodayMobKills, "오늘치를 소급해 줬다");
            Assert.IsTrue(data.questTodayGold.IsZero);

            Assert.AreEqual(0, data.questClaims.Length, "수령 상태가 미리 채워져 있다");

            // 0이어야 QuestSystem이 첫 프레임에 오늘 날짜를 적는다. UtcNow를
            // 마이그레이션에서 읽으면 테스트가 시각을 넘겨줄 수 없다
            Assert.AreEqual(0L, data.lastDailyResetUtcTicks);
        }

        /** 오의 진행은 그대로 남는다. 새 칸을 만드는 것과 지우는 것은 다른 일이다 */
        [Test]
        public void V7_KeepsSkillLevels()
        {
            var data = V7Save();
            SaveData.Migrate(data);

            int index = System.Array.IndexOf(data.skillIds, SkillCatalog.ChainSlashId);
            Assert.GreaterOrEqual(index, 0);
            Assert.AreEqual(5, data.skillLevels[index], "v7 -> v8이 오의 레벨을 지웠다");
        }

        /** 두 번 돌려도 같다. 저장 실패 후 재시도 같은 경로에서 실제로 두 번 돈다 */
        [Test]
        public void V7_MigrationIsIdempotent()
        {
            var data = V7Save();
            SaveData.Migrate(data);

            data.gems = 120L;
            data.questTotalMobKills = 340d;

            SaveData.Migrate(data);

            Assert.AreEqual(120L, data.gems, "두 번째 마이그레이션이 보석을 지웠다");
            Assert.AreEqual(340d, data.questTotalMobKills, "두 번째 마이그레이션이 카운터를 지웠다");
        }

        /**
         * @brief 자동 시전이 **켜진 채로** 올라온다.
         *
         * JsonUtility는 없는 bool을 false로 채운다. 명시하지 않으면 v6 플레이어가
         * 오의가 꺼진 채로 올라오고, 그것은 마이그레이션이 아니라 밸런스 변경이다 -
         * 화면에서는 "업데이트했더니 스킬이 안 나간다"로 나타난다.
         */
        [Test]
        public void V6_TurnsAutoCastOn()
        {
            var data = V6Save();
            Assert.IsFalse(data.skillAutoCast, "전제 확인 - v6에는 이 필드가 없어 false로 들어온다");

            SaveData.Migrate(data);

            Assert.IsTrue(data.skillAutoCast,
                "v6 세이브가 오의가 꺼진 채로 올라온다 - 마이그레이션이 밸런스를 바꿨다");
        }

        /** 기존 강화 레벨은 그대로여야 한다. 새 축이 생겼다고 예전 진행이 달라지면 안 된다 */
        [Test]
        public void V6_LeavesTheExistingProgressAlone()
        {
            var data = V6Save();
            var before = (int[])data.upgradeLevels.Clone();

            SaveData.Migrate(data);

            Assert.AreEqual(before, data.upgradeLevels, "강화 레벨이 바뀌었다");
            Assert.AreEqual(12, data.stage);
            Assert.AreEqual(18, data.characterLevel);
        }

        /**
         * @brief 두 번 돌려도 레벨이 초기화되지 않는다.
         *
         * 저장 실패 후 재시도 같은 경로에서 실제로 두 번 돌 수 있다.
         */
        [Test]
        public void Migration_IsIdempotent()
        {
            var data = V6Save();
            SaveData.Migrate(data);

            int index = System.Array.IndexOf(data.skillIds, SkillCatalog.ChainSlashId);
            data.skillLevels[index] = 9;

            SaveData.Migrate(data);

            Assert.AreEqual(SkillCatalog.Count, data.skillIds.Length, "두 번째 마이그레이션이 칸을 늘렸다");
            Assert.AreEqual(9, data.skillLevels[index], "두 번째 마이그레이션이 레벨을 되돌렸다");
        }

        // ---------------------------------------------------------------- v8 -> v9

        /** 32단계 이전의 세이브. 장비 칸이 아예 없다 */
        static SaveData V8Save()
        {
            var data = V7Save();
            data.version = 8;
            data.gems = 260L;
            data.questTotalMobKills = 540d;
            data.questIds = new[] { QuestCatalog.Achievement[0].Id };
            data.questClaims = new[] { 1 };
            return data;
        }

        /**
         * @brief v8이 **두 슬롯 1등급 Lv.1**로 올라온다.
         *
         * 그 상태의 배수가 정확히 1배라 이 마이그레이션은 예전 플레이어의 스탯을
         * 바꾸지 않는다. v5 -> v6이 골드 획득 축을 레벨 1(x1배)로 넣은 것과 같은
         * 성질이고, 그것이 "새 축이 생겼다고 예전 진행의 밸런스가 달라지지
         * 않는다"의 뜻이다.
         */
        [Test]
        public void V8_MigratesToV9WithBothSlotsAtGradeOne()
        {
            var data = V8Save();
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(EquipmentCatalog.Count, data.equipmentIds.Length,
                "v8 -> v9가 장비 칸을 다 만들지 않았다");

            foreach (var slot in EquipmentCatalog.Slots)
            {
                int index = System.Array.IndexOf(data.equipmentIds, slot.Id);
                Assert.GreaterOrEqual(index, 0, "'" + slot.SlotName + "'이 세이브에 없다");
                Assert.AreEqual(1, data.equipmentGrades[index], "'" + slot.SlotName + "'이 1등급이 아니다");
                Assert.AreEqual(1, data.equipmentLevels[index], "'" + slot.SlotName + "'이 Lv.1이 아니다");

                // **배수가 1배여야 마이그레이션이 무해하다.** 여기가 1이 아니면
                // 예전 플레이어의 공격력이 접속하는 순간 달라진다
                Assert.AreEqual(1d,
                    EquipmentCurve.ValueAt(slot.GradeStep, slot.TemperStep, 1, 1), 1e-12d,
                    "'" + slot.SlotName + "' 1등급 Lv.1의 배수가 1배가 아니다");
            }
        }

        /** 보석과 퀘스트 진행은 그대로 남는다. 칸을 만드는 것과 지우는 것은 다른 일이다 */
        [Test]
        public void V8_KeepsGemsAndQuestProgress()
        {
            var data = V8Save();
            SaveData.Migrate(data);

            Assert.AreEqual(260L, data.gems, "v8 -> v9가 보석을 지웠다");
            Assert.AreEqual(540d, data.questTotalMobKills, "v8 -> v9가 퀘스트 카운터를 지웠다");
            Assert.AreEqual(1, data.questClaims[0], "v8 -> v9가 수령 상태를 지웠다");
        }

        /**
         * @brief 두 번 돌려도 등급이 되돌아가지 않는다.
         *
         * 여기가 깨지면 저장 실패 후 재시도 한 번에 **보석으로 산 등급이
         * 사라진다.** 다른 축의 멱등성보다 손해가 크다 - 골드는 다시 벌지만
         * 보석은 퀘스트를 다시 해야 한다.
         */
        [Test]
        public void V8_MigrationIsIdempotent()
        {
            var data = V8Save();
            SaveData.Migrate(data);

            int index = System.Array.IndexOf(data.equipmentIds, EquipmentCatalog.WeaponId);
            data.equipmentGrades[index] = 4;
            data.equipmentLevels[index] = 8;

            SaveData.Migrate(data);

            Assert.AreEqual(EquipmentCatalog.Count, data.equipmentIds.Length,
                "두 번째 마이그레이션이 칸을 늘렸다");
            Assert.AreEqual(4, data.equipmentGrades[index], "두 번째 마이그레이션이 등급을 되돌렸다");
            Assert.AreEqual(8, data.equipmentLevels[index], "두 번째 마이그레이션이 단련 레벨을 되돌렸다");
        }

        /** 미래 버전은 거부한다. 모르는 형식을 억지로 읽으면 조용히 망가진다 */
        [Test]
        public void FutureVersion_IsRejected()
        {
            var data = V6Save();
            data.version = SaveData.CurrentVersion + 1;

            Assert.IsFalse(SaveData.Migrate(data));
        }

        /**
         * @brief 아주 오래된 세이브(v1)도 오의까지 한 번에 올라온다.
         *
         * 각 단계가 자기 다음 버전만 알면 되도록 사슬로 짜여 있다. 그 사슬이
         * 어디선가 끊기면 v1 플레이어만 조용히 오의 없이 남는다.
         */
        [Test]
        public void V1_ClimbsAllTheWay()
        {
            var data = new SaveData
            {
                version = 1,
                stage = 4,
                upgradeIds = new[] { UpgradeSystem.AttackPowerId },
                upgradeLevels = new[] { 7 }
            };

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(SkillCatalog.Count, data.skillIds.Length);
            Assert.IsTrue(data.skillAutoCast);
            Assert.AreEqual(EquipmentCatalog.Count, data.equipmentIds.Length,
                "사슬이 v8 -> v9에서 끊겼다 - v1 플레이어만 장비 없이 남는다");

            // v1에는 보스가 없었다. 지나온 스테이지 수를 보스 처치 수로 친다
            Assert.AreEqual(3, data.bossKillCount);
        }
    }
}
