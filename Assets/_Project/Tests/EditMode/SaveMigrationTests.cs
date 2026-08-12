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
            Assert.AreEqual(18, SaveData.CurrentVersion, "버전이 또 올랐으면 이 테스트도 함께 봐야 한다");

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

            SaveData.Migrate(data);

            // 43단계부터 심화 축 두 칸이 **뒤에 붙고**, 기존 레벨은 미세화
            // 격자로 **가치 등가 환산**된다(새 = (옛-1) x 계수 + 1). 숫자가
            // 바뀌는 것이 맞다 - 안 바뀌면 공격력 Lv.52의 배수가 x1.0143^51로
            // 쪼그라들어 진행이 무너진다. 공격속도(아트 상한)와 골드 획득
            // (밴드 손잡이)만 곡선이 그대로라 환산도 없다
            Assert.AreEqual(9, data.upgradeLevels.Length,
                "마이그레이션 뒤 칸 수가 기존 7 + 심화 2가 아니다");

            Assert.AreEqual(409, data.upgradeLevels[0], "공격력 52 -> (51x8)+1");
            Assert.AreEqual(32, data.upgradeLevels[1], "공격속도는 환산 없음");
            Assert.AreEqual(336, data.upgradeLevels[2], "치명타 확률 60 -> round(59x5.676)+1");
            Assert.AreEqual(449, data.upgradeLevels[3], "치명타 피해 57 -> (56x8)+1");
            Assert.AreEqual(233, data.upgradeLevels[4], "체력 30 -> (29x8)+1");
            Assert.AreEqual(153, data.upgradeLevels[5], "회복 20 -> (19x8)+1");
            Assert.AreEqual(13, data.upgradeLevels[6], "골드 획득은 환산 없음");

            // 환산이 실제로 가치 등가인지 - 옛 Lv.52 공격력 배수와 새 Lv.409의
            // 배수가 같아야 한다 (반 칸 오차 허용)
            double oldValue = 5d * System.Math.Pow(1.12d, 51);
            double newValue = AttackPowerCurve.ValueAtLevel(409);
            Assert.AreEqual(1d, newValue / oldValue, 0.02d, "환산이 가치를 보존하지 않는다");

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

        // ---------------------------------------------------------------- v9 -> v10

        /** 33단계 이전의 세이브. 전직 칸이 아예 없다 (JsonUtility 기본값 0) */
        static SaveData V9Save()
        {
            var data = V8Save();
            data.version = 9;
            data.equipmentIds = new[] { EquipmentCatalog.WeaponId, EquipmentCatalog.ArmorId };
            data.equipmentGrades = new[] { 4, 3 };
            data.equipmentLevels = new[] { 8, 5 };
            data.characterLevel = 74;
            return data;
        }

        /**
         * @brief v9이 **0티어(로닌)**로 올라온다.
         *
         * 티어 0의 배수가 정확히 1배라 이 마이그레이션은 예전 플레이어의 스탯을
         * 바꾸지 않는다. 장비 v8 -> v9와 같은 성질이다.
         *
         * **레벨을 넘겼어도 소급하지 않는다.** V9Save가 Lv.74인 것이 그 검사다 -
         * 해금 조건(Lv.30)을 오래전에 넘긴 플레이어도 티어는 0에서 시작한다.
         * 진화는 재화를 내고 오르는 사다리이지 레벨의 부록이 아니다.
         */
        [Test]
        public void V9_MigratesToV10AtTierZero()
        {
            var data = V9Save();
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0, data.evolutionTier, "v9 -> v10이 티어를 소급해 줬다");

            // **0티어의 배수가 1배여야 마이그레이션이 무해하다**
            Assert.AreEqual(1d, EvolutionCurve.AttackMultiplierAt(0), 1e-12d,
                "0티어의 공격 배수가 1배가 아니다 - 마이그레이션이 밸런스를 바꾼다");
            Assert.AreEqual(1d, EvolutionCurve.HealthMultiplierAt(0), 1e-12d,
                "0티어의 체력 배수가 1배가 아니다");
        }

        /** 장비·보석·퀘스트 진행은 그대로 남는다 */
        [Test]
        public void V9_KeepsEquipmentAndGems()
        {
            var data = V9Save();
            SaveData.Migrate(data);

            Assert.AreEqual(260L, data.gems, "v9 -> v10이 보석을 지웠다");
            Assert.AreEqual(4, data.equipmentGrades[0], "v9 -> v10이 장비 등급을 지웠다");
            Assert.AreEqual(74, data.characterLevel, "v9 -> v10이 레벨을 지웠다");
        }

        /**
         * @brief 두 번 돌려도 티어가 되돌아가지 않는다.
         *
         * 장비 멱등성과 같은 이유로 손해가 크다 - 티어는 보석 최대 1,200개짜리
         * 구매다. 저장 실패 후 재시도 한 번에 그것이 사라지면 안 된다.
         */
        [Test]
        public void V9_MigrationIsIdempotent()
        {
            var data = V9Save();
            SaveData.Migrate(data);

            data.evolutionTier = 4;
            SaveData.Migrate(data);

            Assert.AreEqual(4, data.evolutionTier, "두 번째 마이그레이션이 티어를 되돌렸다");
        }

        // ---------------------------------------------------------------- v10 -> v11

        /** 펫 스텝 이전의 세이브. 펫 칸이 아예 없다 */
        static SaveData V10Save()
        {
            var data = V9Save();
            data.version = 10;
            data.evolutionTier = 3;
            data.stage = 41;
            return data;
        }

        /**
         * @brief v10이 **세 마리 전부 잠금 + Lv.1, 액티브 없음**으로 올라온다.
         *
         * 잠긴 펫의 기여가 정확히 0이라 이 마이그레이션은 예전 플레이어의
         * DPS를 바꾸지 않는다. 장비 1등급(배수 1배)·전직 0티어와 같은 성질이다.
         *
         * **스테이지를 넘겼어도 소급하지 않는다.** V10Save가 st41인 것이 그
         * 검사다 - 해금 게이트(st31)를 오래전에 넘긴 플레이어도 펫은 잠금에서
         * 시작한다. 동료는 보석을 내고 여는 문이지 스테이지의 부록이 아니다.
         */
        [Test]
        public void V10_MigratesToV11WithAllPetsLocked()
        {
            var data = V10Save();
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(PetCatalog.Count, data.petIds.Length,
                "v10 -> v11이 펫 칸을 다 만들지 않았다");
            Assert.AreEqual("", data.activePetId, "v10 -> v11이 액티브 펫을 세워줬다");

            foreach (var pet in PetCatalog.Pets)
            {
                int index = System.Array.IndexOf(data.petIds, pet.Id);
                Assert.GreaterOrEqual(index, 0, "'" + pet.Name + "'이 세이브에 없다");
                Assert.AreEqual(0, data.petUnlocked[index], "'" + pet.Name + "'이 해금돼 있다 - 소급이다");
                Assert.AreEqual(1, data.petLevels[index], "'" + pet.Name + "'이 Lv.1이 아니다");
            }
        }

        /** 전직·장비·보석은 그대로 남는다 */
        [Test]
        public void V10_KeepsEvolutionAndGems()
        {
            var data = V10Save();
            SaveData.Migrate(data);

            Assert.AreEqual(260L, data.gems, "v10 -> v11이 보석을 지웠다");
            Assert.AreEqual(3, data.evolutionTier, "v10 -> v11이 전직 티어를 지웠다");
            Assert.AreEqual(4, data.equipmentGrades[0], "v10 -> v11이 장비 등급을 지웠다");
        }

        /**
         * @brief 두 번 돌려도 해금이 되돌아가지 않는다.
         *
         * 장비·전직 멱등성과 같은 이유로 손해가 크다 - 해금은 보석 최대
         * 400개짜리 구매다. 저장 실패 후 재시도 한 번에 사라지면 안 된다.
         */
        [Test]
        public void V10_MigrationIsIdempotent()
        {
            var data = V10Save();
            SaveData.Migrate(data);

            int wolf = System.Array.IndexOf(data.petIds, PetCatalog.WolfId);
            data.petUnlocked[wolf] = 1;
            data.petLevels[wolf] = 12;
            data.activePetId = PetCatalog.WolfId;

            SaveData.Migrate(data);

            Assert.AreEqual(PetCatalog.Count, data.petIds.Length, "두 번째 마이그레이션이 칸을 늘렸다");
            Assert.AreEqual(1, data.petUnlocked[wolf], "두 번째 마이그레이션이 해금을 되돌렸다");
            Assert.AreEqual(12, data.petLevels[wolf], "두 번째 마이그레이션이 레벨을 되돌렸다");
            Assert.AreEqual(PetCatalog.WolfId, data.activePetId, "두 번째 마이그레이션이 액티브를 비웠다");
        }

        /** 미래 버전은 거부한다. 모르는 형식을 억지로 읽으면 조용히 망가진다 */
        // ---------------------------------------------------------------- v11 -> v12

        /** 재선택 이전의 세이브. 최전선 칸이 없다 */
        static SaveData V11Save()
        {
            var data = V10Save();
            data.version = 11;
            return data;
        }

        /**
         * @brief 최전선은 지금 서 있는 스테이지다.
         *
         * v11까지는 스테이지가 내려가는 경로가 없었으므로 "현재 위치 = 최고
         * 도달"이 참이다. 이 마이그레이션은 아무 상태도 지어내지 않고, 해금
         * 판정(장비 st11·동료 st31)은 마이그레이션 전후로 같은 답을 낸다.
         */
        [Test]
        public void V11_MigratesToV12WithFrontierAtCurrentStage()
        {
            var data = V11Save();
            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(data.stage, data.maxStageReached,
                "v11 -> v12의 최전선은 현재 스테이지여야 한다 - 다른 값은 지어낸 상태다");
            Assert.GreaterOrEqual(data.maxStageReached, 1);
        }

        /**
         * @brief 재선택으로 내려간 상태를 다시 마이그레이션해도 최전선이 안 깎인다.
         *
         * 마이그레이션이 두 번 돌 때(저장 실패 후 재시도) stage로 최전선을
         * 덮어쓰면, 클리어한 지역에서 파밍하다 재시도가 난 플레이어의 도달
         * 기록이 사라지고 장비·동료가 다시 잠긴다.
         */
        [Test]
        public void V11_MigrationIsIdempotent()
        {
            var data = V11Save();
            SaveData.Migrate(data);

            // 재선택으로 내려가 있는 v12 상태를 흉내낸다
            int frontier = data.maxStageReached;
            data.stage = 3;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(frontier, data.maxStageReached,
                "두 번째 마이그레이션이 최전선을 현재 스테이지로 되돌렸다");
        }

        // ---------------------------------------------------------------- v12 -> v13

        /**
         * @brief 심화 축 둘이 Lv.1(배수 1 / 확률 0)로 생긴다 - 무보정 승격.
         *
         * 해금 상태는 저장하지 않는다. 치명타 확률 트랙의 상한 도달에서
         * 유도되는 값이라, 구세이브의 치명타가 60% 상한(옛 Lv.97)에 서
         * 있으면 새 상한(100%) 기준으로는 잠긴 것이 맞고 실제로 그렇게
         * 판정된다 - 지어내는 상태가 없다.
         */
        [Test]
        public void V12_MigratesToV13WithMasteryAtLevelOne()
        {
            var data = V11Save();
            SaveData.Migrate(data);

            Assert.AreEqual(SaveData.CurrentVersion, data.version);

            int transcend = System.Array.IndexOf(data.upgradeIds, UpgradeSystem.TranscendId);
            int combo = System.Array.IndexOf(data.upgradeIds, UpgradeSystem.ComboId);
            Assert.GreaterOrEqual(transcend, 0, "초월 치명타 칸이 안 생겼다");
            Assert.GreaterOrEqual(combo, 0, "연격 칸이 안 생겼다");
            Assert.AreEqual(1, data.upgradeLevels[transcend], "레벨 1이 아니면 밸런스가 움직인다");
            Assert.AreEqual(1, data.upgradeLevels[combo]);

            // 레벨 1의 값이 실제로 무보정인지 - 곡선 쪽 사실도 함께 못 박는다
            Assert.AreEqual(1d, TranscendCurve.MultiplierAtLevel(1), 1e-12d);
            Assert.AreEqual(0d, ComboCurve.ChanceAtLevel(1), 1e-12d);
        }

        [Test]
        public void V12_MigrationIsIdempotent()
        {
            var data = V11Save();
            SaveData.Migrate(data);

            // 심화 축을 올려둔 v13 상태를 흉내낸다
            int transcend = System.Array.IndexOf(data.upgradeIds, UpgradeSystem.TranscendId);
            data.upgradeLevels[transcend] = 9;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(9, data.upgradeLevels[transcend],
                "두 번째 마이그레이션이 심화 축 레벨을 되돌렸다");
        }

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
            Assert.AreEqual(0, data.evolutionTier,
                "사슬이 v9 -> v10에서 끊겼거나 티어를 소급해 줬다");
            Assert.AreEqual(PetCatalog.Count, data.petIds.Length,
                "사슬이 v10 -> v11에서 끊겼다 - v1 플레이어만 펫 없이 남는다");
            Assert.AreEqual(YodoCatalog.Count, data.yodoIds.Length,
                "사슬이 v13 -> v14에서 끊겼다 - v1 플레이어만 요도 없이 남는다");
            Assert.AreEqual(0, data.gachaPity,
                "사슬이 v14 -> v15에서 끊겼다 - v1 플레이어만 뽑기 없이 남는다");
            Assert.AreEqual(LegendaryYodoCatalog.Count, data.legendaryYodoIds.Length,
                "사슬이 v15 -> v16에서 끊겼다 - v1 플레이어만 전설 칸 없이 남는다");

            // v1에는 보스가 없었다. 지나온 스테이지 수를 보스 처치 수로 친다
            Assert.AreEqual(3, data.bossKillCount);
        }

        // ---------------------------------------------------------------- v13 -> v14

        /**
         * @brief 요도 넷이 미봉인(티어 0)으로 생긴다 - 무보정 승격.
         *
         * **소급하지 않는 것이 이 마이그레이션의 전부다.** 이미 st200인
         * 플레이어는 지나온 순환에서 대요괴를 열 번 넘게 벴지만, 그 처치를
         * 혼으로 쳐주면 접속하자마자 오니키리가 완성된다 - 도감이 채워지는
         * 과정 전체가 사라지고, 그것이 이 스텝이 만든 것의 전부다.
         */
        [Test]
        public void V13_MigratesToV14WithEveryBladeUnsealed()
        {
            var data = V11Save();
            data.stage = 200;
            data.maxStageReached = 200;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);

            Assert.AreEqual(YodoCatalog.Count, data.yodoIds.Length, "요도 칸이 안 생겼다");
            Assert.AreEqual(YodoCatalog.Count, data.yodoSouls.Length);
            Assert.AreEqual(YodoCatalog.Count, data.yodoTiers.Length);
            Assert.AreEqual(YodoCatalog.Count, data.yodoDiscovered.Length);

            foreach (var blade in YodoCatalog.Blades)
            {
                int index = System.Array.IndexOf(data.yodoIds, blade.Id);
                Assert.GreaterOrEqual(index, 0, "'" + blade.BladeName + "'이 세이브에 없다");
                Assert.AreEqual(0, data.yodoTiers[index],
                    "티어를 소급해 줬다 - 도감이 채워지는 과정이 통째로 사라진다");
                Assert.AreEqual(0L, data.yodoSouls[index]);
                Assert.AreEqual(0, data.yodoDiscovered[index]);
            }

            Assert.AreEqual(0L, data.yodoShards);

            // 승격이 밸런스를 안 바꾼다는 것도 함께 못 박는다 - 티어 0의
            // 배수와 0자루 세트 보너스가 정확히 1이어야 한다
            Assert.AreEqual(1d, YodoCurve.TierValue(0), 1e-12d);
            Assert.AreEqual(1d, YodoCurve.SetBonusAt(0), 1e-12d);
        }

        [Test]
        public void V13_MigrationIsIdempotent()
        {
            var data = V11Save();
            SaveData.Migrate(data);

            // 한 바퀴 돌아 등롱도를 3티어까지 올린 v14 상태를 흉내낸다
            int lantern = System.Array.IndexOf(data.yodoIds, YodoCatalog.LanternId);
            data.yodoTiers[lantern] = 3;
            data.yodoSouls[lantern] = 2L;
            data.yodoDiscovered[lantern] = 1;
            data.yodoShards = 77L;

            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(3, data.yodoTiers[lantern],
                "두 번째 마이그레이션이 요도 티어를 되돌렸다 - 한 바퀴가 통째로 사라진다");
            Assert.AreEqual(2L, data.yodoSouls[lantern]);
            Assert.AreEqual(1, data.yodoDiscovered[lantern]);
            Assert.AreEqual(77L, data.yodoShards);
        }

        // ---------------------------------------------------------------- v14 -> v15

        /**
         * @brief 뽑기가 **미사용** 상태로 생긴다 - 무보정 승격.
         *
         * 소급하지 않는 것이 여기서도 전부다. 지나온 날수만큼 무료 뽑기를
         * 쌓아주면 접속하자마자 200회가 돌아가고, 그것은 천장을 여섯 번
         * 지나는 양이라 요도가 통째로 리드 상한까지 올라간다.
         *
         * 대신 **오늘치 하나**는 곧바로 쓸 수 있다. 날짜가 0이면 "아직 한
         * 번도 안 썼다"이므로 마이그레이션이 아무것도 안 하는 것만으로
         * 그렇게 된다 - v7 -> v8 업적이 "곧바로 받을 수 있는 상태로 열린다"
         * 였던 것과 같은 결이다.
         */
        [Test]
        public void V14_MigratesToV15WithTheGachaUntouched()
        {
            var data = V11Save();
            data.stage = 200;
            data.maxStageReached = 200;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(18, SaveData.CurrentVersion, "세이브 버전이 v18이 아니다");

            Assert.AreEqual(0, data.gachaPity, "천장 카운터를 소급해 줬다");
            Assert.AreEqual(0, data.gachaTotalPulls);
            Assert.AreEqual(0L, data.gachaFreePullDayTicks,
                "무료 뽑기 날짜가 채워졌다 - 승격 직후 무료 뽑기를 못 쓴다");
        }

        [Test]
        public void V14_MigrationIsIdempotent()
        {
            var data = V11Save();
            SaveData.Migrate(data);

            // 천장 직전까지 돌린 v15 상태를 흉내낸다
            data.gachaPity = GachaCurve.PityPulls - 1;
            data.gachaTotalPulls = 137;
            data.gachaFreePullDayTicks = 638000000000000000L;

            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(GachaCurve.PityPulls - 1, data.gachaPity,
                "두 번째 마이그레이션이 천장 카운터를 되돌렸다 - 플레이어가 지불한 "
                + (GachaCurve.PityPulls - 1) + "회가 몰수된다");
            Assert.AreEqual(137, data.gachaTotalPulls);
            Assert.AreEqual(638000000000000000L, data.gachaFreePullDayTicks,
                "무료 뽑기 쿨이 초기화됐다 - 같은 날 두 번 뽑힌다");
        }

        // ---------------------------------------------------------------- v15 -> v16

        /**
         * @brief 희귀도 사다리가 **빈 채로** 생긴다 - 무보정 승격.
         *
         * 소급하지 않는 것이 여기서도 전부다. 이미 뽑기를 200회 돌린
         * 플레이어에게 그 회수만큼 ★4·★5를 나눠 주면 접속 즉시 혼격이
         * 상한까지 차오르고, 그것은 이 스텝이 판 재고를 통째로 지우는
         * 일이다 - 지나온 뽑기는 그때의 표로 이미 값을 받았다.
         *
         * **천장 카운터는 그대로 둔다.** 지키는 대상이 ★3에서 ★4+로
         * 승격했지만 카운터의 뜻("마지막 보장 뒤로 몇 번 돌렸는가")은 같고,
         * 0으로 되돌리면 29회에서 승격을 맞은 플레이어의 지불이 몰수된다.
         */
        [Test]
        public void V15_MigratesToV16WithAnEmptyLadder()
        {
            var data = V11Save();
            data.stage = 300;
            data.maxStageReached = 300;

            // 뽑기를 한참 돌린 v15 상태를 흉내낸다
            SaveData.Migrate(data);
            data.version = 15;
            data.gachaPity = 17;
            data.gachaTotalPulls = 240;
            data.yodoRarities = new int[0];
            data.legendaryYodoIds = new string[0];
            data.legendaryYodoCopies = new int[0];

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);

            Assert.AreEqual(data.yodoIds.Length, data.yodoRarities.Length,
                "혼격 칸이 요도 칸과 갈렸다");
            foreach (int rarity in data.yodoRarities)
                Assert.AreEqual(0, rarity, "혼격을 소급해 줬다 - 재고가 통째로 사라진다");

            Assert.AreEqual(LegendaryYodoCatalog.Count, data.legendaryYodoIds.Length,
                "전설 칸이 안 생겼다");
            foreach (int copies in data.legendaryYodoCopies)
                Assert.AreEqual(0, copies, "240회를 돌렸다고 전설을 나눠 줬다");

            Assert.AreEqual(17, data.gachaPity,
                "천장 카운터가 0으로 돌아갔다 - 승격이 몰수가 됐다");
            Assert.AreEqual(240, data.gachaTotalPulls);
        }

        /**
         * @brief v16 -> v17. **장착 구성은 지어내지 않고 구조가 낸다.**
         *
         * v16에는 장착이라는 개념이 없었다 - 오의 셋이 전부 상시 발동이었고,
         * 그것은 "셋이 세 자리에 끼워져 있다"와 같은 말이다. 그래서 이
         * 마이그레이션은 **아무것도 안 적는다**: 빈 배열로 두면 SkillSystem이
         * 기준 구성으로 메우고, st51 아래에서는 열린 자리도 열린 오의도 그
         * 셋뿐이라 답이 하나다.
         *
         * 값을 지어내지 않는 것이 요점이다. 여기서 id 셋을 적어 두면 표가
         * 바뀌는 날 마이그레이션이 없는 오의를 가리키게 된다.
         */
        [Test]
        public void V16_MigratesToV17WithTheLoadoutLeftToTheSystem()
        {
            var data = V11Save();
            data.stage = 300;
            data.maxStageReached = 300;

            SaveData.Migrate(data);
            data.version = 16;
            data.skillEquipped = new string[0];

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);

            Assert.IsNotNull(data.skillEquipped, "장착 칸이 null이다");
            Assert.AreEqual(0, data.skillEquipped.Length,
                "마이그레이션이 장착 구성을 지어냈다 - 표가 바뀌는 날 없는 오의를 가리킨다");

            // 신규 오의의 레벨도 소급하지 않는다. v6 -> v7이 만든 칸이 그대로 1이다
            foreach (var skill in SkillCatalog.Skills)
            {
                if (!skill.StageGated) continue;

                int index = System.Array.IndexOf(data.skillIds, skill.Id);
                Assert.GreaterOrEqual(index, 0, "'" + skill.DisplayName + "'의 칸이 없다");
                Assert.AreEqual(1, data.skillLevels[index],
                    "'" + skill.DisplayName + "'을 소급해 올려 줬다 - 심층 플레이어가 "
                    + "접속 즉시 상한을 받는다");
            }
        }

        /** 두 번 돌려도 같다. 저장 실패 후 재시도 같은 경로에서 실제로 두 번 돈다 */
        [Test]
        public void V16_MigrationIsIdempotent()
        {
            var data = V11Save();
            SaveData.Migrate(data);
            data.version = 16;

            Assert.IsTrue(SaveData.Migrate(data));
            var once = data.skillEquipped;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreSame(once, data.skillEquipped, "두 번째 마이그레이션이 장착 칸을 다시 만들었다");
        }

        /**
         * @brief v17 -> v18. **아무것도 주지 않고, 이미 준 것은 안 뺏는다.**
         *
         * 두 규칙이 한 블록에서 갈린다.
         *
         * **소급 없음** - XP 0, 천장 0, 무료 뽑기 미사용. 지나온 날수만큼
         * 무료 뽑기를 쌓아주면 접속 즉시 천장을 여러 번 지나 오의 둘이
         * 통째로 열리고, 그것은 이 스텝이 판 재고를 지우는 일이다.
         *
         * **보존** - 그런데 혈폭·혈조는 v17에서 **최전선 st51의 스테이지
         * 게이트**였다. 50단계가 그 게이트를 뽑기로 옮기므로, 손대지 않으면
         * 이미 심층에 있던 플레이어가 갖고 있던 오의 둘을 잃는다. 소급이
         * 아니라 보존이고, 기준이 st51인 것은 그것이 v17의 게이트 그 자체이기
         * 때문이다 - 다른 값을 고르면 마이그레이션이 v17 세계의 사실이 아니라
         * 새 판단을 지어내는 것이 된다.
         */
        [Test]
        public void V17_MigratesToV18WithoutGivingAnythingAway()
        {
            var data = V11Save();
            data.stage = 30;
            data.maxStageReached = 30;

            SaveData.Migrate(data);
            data.version = 17;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);

            Assert.AreEqual(0L, data.skillXp, "마이그레이션이 스킬 XP를 지어냈다");
            Assert.AreEqual(0, data.skillGachaPity, "천장 카운터가 소급됐다");
            Assert.AreEqual(0, data.skillGachaTotalPulls, "누적 횟수가 소급됐다");
            Assert.AreEqual(0L, data.skillGachaFreePullDayTicks,
                "무료 뽑기가 쓰인 것으로 들어왔다 - 오늘치 하나는 곧바로 쓸 수 있어야 한다");

            Assert.IsNotNull(data.gachaSkillIds, "보유 칸이 null이다");
            Assert.AreEqual(0, data.gachaSkillIds.Length,
                "st30 플레이어에게 가챠 몫이 들어왔다 - v17에서도 갖고 있지 않던 것이다");
        }

        /**
         * @brief v17에서 **이미 갖고 있던** 가챠 몫은 유지된다.
         *
         * st51을 넘긴 세이브는 v17의 규칙대로 혈폭·혈조가 열려 있었다.
         * 게이트의 출처가 바뀌었다고 그것을 도로 뺏으면 그것은 마이그레이션이
         * 아니라 몰수다 - 47단계가 천장 카운터를 안 건드린 이유와 같다.
         */
        [Test]
        public void V17_KeepsTheSkillsThatWorldHadAlreadyGiven()
        {
            var data = V11Save();
            data.stage = 300;
            data.maxStageReached = 300;

            SaveData.Migrate(data);
            data.version = 17;
            data.gachaSkillIds = new string[0];

            Assert.IsTrue(SaveData.Migrate(data));

            foreach (var id in SkillGachaCurve.UnlockOrder)
                Assert.Contains(id, data.gachaSkillIds, string.Format(
                    "심층(st{0}) 플레이어가 v17에서 갖고 있던 '{1}'을 잃었다", data.maxStageReached, id));

            // 그래도 레벨은 그대로 1이다. 갖고 있던 것을 지키는 것과
            // 소급해 올려 주는 것은 다른 일이다
            foreach (var id in SkillGachaCurve.UnlockOrder)
            {
                int index = System.Array.IndexOf(data.skillIds, id);
                Assert.GreaterOrEqual(index, 0, "'" + id + "'의 칸이 없다");
                Assert.AreEqual(1, data.skillLevels[index], "'" + id + "'을 소급해 올려 줬다");
            }
        }

        /** 두 번 돌려도 같다 - 뽑은 오의와 모은 XP가 두 번째에 사라지면 안 된다 */
        [Test]
        public void V17_MigrationIsIdempotent()
        {
            var data = V11Save();
            data.stage = 300;
            data.maxStageReached = 300;
            SaveData.Migrate(data);

            // 실제로 뽑고 모은 v18 상태를 흉내낸다
            data.skillXp = 244L;
            data.skillGachaPity = 19;
            data.skillGachaTotalPulls = 71;

            Assert.IsTrue(SaveData.Migrate(data));

            Assert.AreEqual(244L, data.skillXp, "두 번째 마이그레이션이 XP를 지웠다");
            Assert.AreEqual(19, data.skillGachaPity,
                "두 번째 마이그레이션이 천장을 되돌렸다 - 지불한 열아홉 회가 몰수된다");
            Assert.AreEqual(71, data.skillGachaTotalPulls, "누적 횟수가 지워졌다");
        }

        [Test]
        public void V15_MigrationIsIdempotent()
        {
            var data = V11Save();
            SaveData.Migrate(data);

            // 사다리를 실제로 올린 v16 상태를 흉내낸다
            for (int i = 0; i < data.yodoRarities.Length; i++) data.yodoRarities[i] = 2;
            data.legendaryYodoCopies[0] = 3;

            Assert.IsTrue(SaveData.Migrate(data));

            foreach (int rarity in data.yodoRarities)
                Assert.AreEqual(2, rarity,
                    "두 번째 마이그레이션이 혼격을 지웠다 - 뽑은 ★4가 몰수된다");
            Assert.AreEqual(3, data.legendaryYodoCopies[0],
                "두 번째 마이그레이션이 전설 사본을 지웠다 - 200회에 한 번이 사라진다");
            Assert.AreEqual(LegendaryYodoCatalog.Count, data.legendaryYodoIds.Length,
                "전설 칸이 두 번 생겼다");
        }

        /**
         * @brief 미래 버전은 거부한다. 사슬의 반대쪽 끝이다.
         */
        [Test]
        public void FutureVersion_IsRejectedAtV16()
        {
            var data = V11Save();
            data.version = SaveData.CurrentVersion + 1;
            Assert.IsFalse(SaveData.Migrate(data),
                "모르는 버전의 세이브를 그대로 읽는다 - 부분 마이그레이션이 된다");
        }
    }
}
