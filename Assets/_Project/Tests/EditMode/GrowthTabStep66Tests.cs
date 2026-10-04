using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Onikiri.Tests.EditMode
{
    /**
     * @brief 66단계 - 성장탭 세 축(경험치·골드·방치), 보석 초기화, EXP % 둘째 자리.
     *
     * 재는 것은 넷이다.
     *
     *   축       배수·시간이 계수대로 나오는가, 상한은 다섯 축 공통 200인가
     *   단일 출처 세 배수가 각자 **한 자리에서만** 곱해지는가(소스 검사)
     *   초기화   무료 1회 -> 유료, 포인트 전부 미배분, 보석은 한 문으로 한 번
     *   밴드     시뮬레이션이 새 축을 굴리는가, 귀문 통과 시점이 그대로인가
     */
    public class GrowthTabStep66Tests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        static StageSimulation.Field Field()
        {
            return new StageSimulation.Field
            {
                AverageMobHealth = 128d / 9d,
                AverageMobGold = 49d / 9d,
                SpawnInterval = 1.1d
            };
        }

        /** 남은 포인트가 최소 points인 캐릭터. 포인트는 레벨에서만 나온다(PolishBatchTests와 같은 길) */
        CharacterLevel LevelWith(int points)
        {
            var go = new GameObject("~Step66Level");
            spawned.Add(go);
            var character = go.AddComponent<CharacterLevel>();

            int level = 1;
            while (StatPointCurve.TotalPointsAtLevel(level) < points && level < 10000) level++;
            character.Restore(level, BigDouble.Zero, 0, 0);
            return character;
        }

        GemWallet WalletWith(long gems)
        {
            var go = new GameObject("~Step66Gems");
            spawned.Add(go);
            var wallet = go.AddComponent<GemWallet>();
            wallet.SetBalance(gems);
            return wallet;
        }

        static string RuntimeSource(string fileName)
        {
            var hits = Directory.GetFiles("Assets/_Project/Scripts", fileName, SearchOption.AllDirectories);
            Assert.AreEqual(1, hits.Length, fileName + "을(를) " + hits.Length + "개 찾았다 - 하나여야 한다");
            return File.ReadAllText(hits[0]);
        }

        /** 이 문자열을 품은 런타임 소스 파일 이름들 */
        static List<string> FilesContaining(string needle)
        {
            var found = new List<string>();
            foreach (var path in Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
                if (File.ReadAllText(path).Contains(needle)) found.Add(Path.GetFileName(path));
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        /** from 다음부터 to 앞까지. 메서드 하나의 몸통을 자르는 데 쓴다 */
        static string Between(string source, string from, string to)
        {
            int start = source.IndexOf(from, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "'" + from + "'이(가) 없다");
            int end = source.IndexOf(to, start + from.Length, StringComparison.Ordinal);
            Assert.Greater(end, start, "'" + to + "'이(가) '" + from + "' 뒤에 없다");
            return source.Substring(start, end - start);
        }

        // ---------------------------------------------------------------- 축

        [Test]
        public void ExistingAxes_KeepTheirCoefficientAndCap()
        {
            // 확정 2: 기존 두 축 무수정, 새 상한 없음
            Assert.AreEqual(1.025d, StatPointCurve.PerPoint, 0d);
            Assert.AreEqual(200, StatPointCurve.MaxPoints);
            Assert.AreEqual(StatPointCurve.Multiplier(37),
                StatPointCurve.Multiplier(CharacterLevel.AttackAmpId, 37), 1e-12);
            Assert.AreEqual(StatPointCurve.Multiplier(37),
                StatPointCurve.Multiplier(CharacterLevel.HealthAmpId, 37), 1e-12);
        }

        [Test]
        public void NewAxes_MultiplyByTheirOwnCoefficient()
        {
            var character = LevelWith(60);
            Assert.AreEqual(20, character.TrySpendPoints(CharacterLevel.ExpAmpId, 20));
            Assert.AreEqual(20, character.TrySpendPoints(CharacterLevel.GoldAmpId, 20));
            Assert.AreEqual(20, character.TrySpendPoints(CharacterLevel.IdleAmpId, 20));

            Assert.AreEqual(Math.Pow(StatPointCurve.ExpPerPoint, 20), character.ExpGainMultiplier, 1e-12);
            Assert.AreEqual(Math.Pow(StatPointCurve.GoldPerPoint, 20), character.GoldGainMultiplier, 1e-12);
            Assert.AreEqual(TimeSpan.FromMinutes(100), character.IdleAccrualBonus);

            // 방치 축은 배수 자리로 새지 않는다
            Assert.AreEqual(1d, StatPointCurve.Multiplier(CharacterLevel.IdleAmpId, 200), 0d);

            // 공격력·체력은 손대지 않았다
            Assert.AreEqual(1d, character.AttackMultiplier, 0d);
            Assert.AreEqual(1d, character.HealthMultiplier, 0d);
        }

        [Test]
        public void NewAxes_StopAtTheSharedCapOf200()
        {
            var character = LevelWith(StatPointCurve.MaxPoints * 3 + 10);

            foreach (var axis in new[] { CharacterLevel.ExpAmpId, CharacterLevel.GoldAmpId, CharacterLevel.IdleAmpId })
            {
                Assert.AreEqual(StatPointCurve.MaxPoints, character.TrySpendPoints(axis, int.MaxValue), axis);
                Assert.IsTrue(character.IsAxisMaxed(axis), axis);
                Assert.AreEqual(0, character.SpendableInto(axis, 0), axis);
            }

            Assert.AreEqual(10, character.UnspentPoints, "상한에 닿은 축이 포인트를 삼켰다");
        }

        [Test]
        public void UnspentPoints_CountsAllFiveAxes()
        {
            var character = LevelWith(50);
            int before = character.UnspentPoints;

            foreach (var axis in CharacterLevel.AxisIds) character.TrySpendPoints(axis, 3);

            Assert.AreEqual(15, character.SpentPoints);
            Assert.AreEqual(before - 15, character.UnspentPoints);
        }

        /** 2-2: 경험치 축은 Lv.15(일섬 해금)에 열린다 - 상한이 아니라 등장 시점 */
        [Test]
        public void ExpAxis_OpensAtTheFlashSkillLevel()
        {
            int flash = -1;
            foreach (var skill in SkillCatalog.Skills)
                if (skill.Id == SkillCatalog.FlashId) flash = skill.UnlockLevel;
            Assert.AreEqual(flash, StatPointCurve.ExpUnlockLevel, "경험치 축이 일섬과 다른 레벨에 열린다");

            var early = LevelWith(20);   // Lv.11
            Assert.Less(early.Level, StatPointCurve.ExpUnlockLevel);
            Assert.IsFalse(early.IsAxisUnlocked(CharacterLevel.ExpAmpId));
            Assert.AreEqual(0, early.SpendableInto(CharacterLevel.ExpAmpId, 0));
            Assert.AreEqual(0, early.TrySpendPoints(CharacterLevel.ExpAmpId, 5), "잠긴 축에 찍혔다");
            Assert.IsTrue(early.IsAxisUnlocked(CharacterLevel.GoldAmpId), "게이트는 경험치 축 하나뿐이다");

            var late = LevelWith(StatPointCurve.TotalPointsAtLevel(StatPointCurve.ExpUnlockLevel));
            Assert.AreEqual(StatPointCurve.ExpUnlockLevel, late.Level);
            Assert.IsTrue(late.IsAxisUnlocked(CharacterLevel.ExpAmpId));
            Assert.AreEqual(5, late.TrySpendPoints(CharacterLevel.ExpAmpId, 5));
        }

        /** 2-2: 게이트 뒤 일섬·귀참이 비용이 가정한 스테이지에 열린다(시뮬레이션 실측) */
        [Test]
        public void ExpAxisGate_KeepsTheFlashSkillAtStage15()
        {
            var rows = StageSimulation.Run(30, Field());
            int lv15 = -1;
            foreach (var row in rows) if (row.CharacterLevel >= 15) { lv15 = row.Stage; break; }
            Assert.AreEqual(15, lv15, "게이트가 있어도 Lv.15가 st15가 아니다");

            // 게이트 전에는 경험치 축에 한 점도 없다
            foreach (var row in rows)
                if (row.CharacterLevel < StatPointCurve.ExpUnlockLevel)
                    Assert.AreEqual(0, row.ExpPoints, "st" + row.Stage + " 잠긴 축에 찍혔다");
        }

        [Test]
        public void PointsPerLevel_IsTwo()
        {
            Assert.AreEqual(2, StatPointCurve.PointsPerLevel);
            Assert.AreEqual(18, StatPointCurve.TotalPointsAtLevel(10));
        }

        // ---------------------------------------------------------------- 방치 축 = 시간

        [Test]
        public void IdleAxis_ExtendsMaxAccrualLinearly()
        {
            Assert.AreEqual(TimeSpan.FromHours(8), IdleIncome.MaxAccrualFor(0));
            Assert.AreEqual(TimeSpan.FromHours(8) + TimeSpan.FromMinutes(5), IdleIncome.MaxAccrualFor(1));
            Assert.AreEqual(new TimeSpan(24, 40, 0), IdleIncome.MaxAccrualFor(200), "200점 = 8h + 16h40m");
            Assert.AreEqual(IdleIncome.MaxAccrualFor(200), IdleIncome.MaxAccrualFor(500), "상한(200)을 넘겨도 멈춘다");

            // 선형: 이웃한 두 점의 차가 늘 같다
            for (int p = 0; p < 200; p += 17)
                Assert.AreEqual(TimeSpan.FromMinutes(StatPointCurve.IdleMinutesPerPoint),
                    IdleIncome.MaxAccrualFor(p + 1) - IdleIncome.MaxAccrualFor(p));

            var quit = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
            var now = quit.AddHours(30);
            var max = IdleIncome.MaxAccrualFor(200);
            Assert.AreEqual(max, IdleIncome.AccruedTime(quit, now, max));
            Assert.IsTrue(IdleIncome.IsCapped(quit, now, max));
            Assert.IsFalse(IdleIncome.IsCapped(quit, quit.AddHours(20), max));
        }

        /**
         * 방치 축은 배율 축이 아니다 - 200점에서도 초당 수입 x 0.5 그대로. 효율과
         * 초당 수입을 만드는 자리에 방치 축이 닿지 않는 것을 소스로 못 박는다
         */
        [Test]
        public void IdleAxis_NeverTouchesTheRateOrTheEfficiency()
        {
            Assert.AreEqual(0.5d, IdleIncome.Efficiency, 0d);
            Assert.AreEqual(1800d, IdleIncome.Reward(1d, TimeSpan.FromHours(1)).ToDouble(), 0d,
                "초당 1 x 3600초 x 0.5");

            string idle = RuntimeSource("IdleIncome.cs");
            string reward = Between(idle, "public static BigDouble Reward(", "public static bool IsCapped(");
            string rate = Between(idle, "public static double GoldPerSecond(", "@brief 방치 보상 증폭(66단계)까지");
            foreach (var body in new[] { reward, rate })
            {
                StringAssert.DoesNotContain("StatPointCurve", body);
                StringAssert.DoesNotContain("idle", body.ToLowerInvariant().Replace("idleincome", string.Empty));
            }

            string session = RuntimeSource("GameSession.cs");
            string goldRate = Between(session, "public double EstimateGoldPerSecond(", "public double EstimateExpPerSecond(");
            string expRate = Between(session, "public double EstimateExpPerSecond(", "public void DeleteSaveAndReload(");
            foreach (var body in new[] { goldRate, expRate })
            {
                StringAssert.DoesNotContain("IdlePoints", body);
                StringAssert.DoesNotContain("idlePoints", body);
                StringAssert.DoesNotContain("MaxAccrualFor", body);
            }
        }

        // ---------------------------------------------------------------- 단일 출처

        /** 경험치 증폭: 처치 입구(AddKillExp)와 방치 경험치 추정, 두 자리뿐. 업적은 받지 않는다 */
        [Test]
        public void ExpAmp_IsAppliedOnlyAtKillAndIdleEstimate()
        {
            CollectionAssert.AreEquivalent(new[] { "CharacterLevel.cs", "GameSession.cs" },
                FilesContaining("ExpGainMultiplier"));

            string level = RuntimeSource("CharacterLevel.cs");
            string addExp = Between(level, "public void AddExp(", "public BigDouble AddKillExp(");
            StringAssert.DoesNotContain("ExpGainMultiplier", addExp, "AddExp에서 곱하면 업적·방치 경험치까지 받는다");

            string spawner = RuntimeSource("EnemySpawner.cs");
            StringAssert.Contains("AddKillExp(", spawner);
            StringAssert.DoesNotContain(".AddExp(", spawner, "처치 경험치가 증폭 없는 입구로 들어간다");

            string quests = RuntimeSource("QuestSystem.cs");
            StringAssert.DoesNotContain("AddKillExp", quests, "업적 경험치에 증폭이 붙었다(31단계 faucet 금지)");

            string session = RuntimeSource("GameSession.cs");
            string grant = Between(session, "private void GrantOfflineReward(", "public bool Save()");
            StringAssert.DoesNotContain("AddKillExp", grant, "방치 경험치에 두 번 곱한다 - 추정 쪽에서 이미 곱했다");
        }

        [Test]
        public void KillExp_IsMultipliedOnce()
        {
            var character = LevelWith(40);   // Lv.21 - 경험치 축은 Lv.15에 열린다
            Assert.AreEqual(10, character.TrySpendPoints(CharacterLevel.ExpAmpId, 10));
            var before = character.Exp;

            var granted = character.AddKillExp(BigDouble.FromDouble(100d));

            Assert.AreEqual(100d * Math.Pow(StatPointCurve.ExpPerPoint, 10), granted.ToDouble(), 1e-9);
            Assert.AreEqual((before + granted).ToDouble(), character.Exp.ToDouble(), 1e-9);

            // 업적·방치 입구(AddExp)는 그대로 넣는다
            var plain = character.Exp;
            character.AddExp(BigDouble.FromDouble(100d));
            Assert.AreEqual(plain.ToDouble() + 100d, character.Exp.ToDouble(), 1e-9);
        }

        /** 골드 증폭: UpgradeSystem.CurrentGoldGain 한 줄. 강화 탭의 골드 행은 곡선 값만 */
        [Test]
        public void GoldAmp_IsReadOnlyByCurrentGoldGain()
        {
            CollectionAssert.AreEquivalent(new[] { "CharacterLevel.cs", "UpgradeSystem.cs" },
                FilesContaining("CurrentGoldGainAmp"));

            string upgrades = RuntimeSource("UpgradeSystem.cs");
            string current = Between(upgrades, "public static double CurrentGoldGain", "public static UpgradeSystem Instance");
            StringAssert.Contains("CharacterLevel.CurrentGoldGainAmp", current);

            // 인스턴스 쪽(강화 탭이 읽는 곡선 값)에는 스탯 포인트가 없다
            string curveOnly = Between(upgrades, "public double GoldGainMultiplier", "public static double CurrentGoldGain");
            StringAssert.DoesNotContain("CharacterLevel", curveOnly);

            // 강화 행은 트랙 값을 그대로 적는다 - 합산 배수를 읽지 않는다
            string button = RuntimeSource("UpgradeButton.cs");
            StringAssert.DoesNotContain("CurrentGoldGain", button, "강화 탭 골드 행에 스탯 포인트 배수가 섞였다");
            StringAssert.DoesNotContain("GoldGainMultiplier", button);

            // 업적 골드·장비 환불은 증폭을 받지 않는다
            StringAssert.DoesNotContain("CurrentGoldGain", RuntimeSource("QuestSystem.cs"));
            StringAssert.DoesNotContain("CurrentGoldGain", RuntimeSource("EquipmentSystem.cs").Replace("UpgradeSystem.CurrentGoldGain과 같은 규칙", string.Empty));
        }

        /** 방치 축: 최대 누적 시간을 읽는 곳은 GrantOfflineReward 하나 */
        [Test]
        public void IdleAmp_IsReadOnlyWhereTheAccrualIsClamped()
        {
            CollectionAssert.AreEquivalent(new[] { "GameSession.cs", "IdleIncome.cs" },
                FilesContaining("MaxAccrualFor("));

            string session = RuntimeSource("GameSession.cs");
            string grant = Between(session, "private void GrantOfflineReward(", "public bool Save()");
            StringAssert.Contains("IdleIncome.MaxAccrualFor(data.idlePoints)", grant);
            StringAssert.Contains("IsCapped(lastQuit.Value, now, maxAccrual)", grant,
                "팝업의 상한 표시가 다른 상한을 본다");

            // 59단계 계약: 방치 보상은 부팅 적용에서 한 번만, 지급 자리 그대로
            string boot = Between(session, "public void ApplyBoot(", "Debug.Log(\"[Onikiri] Boot apply #\"");
            Assert.AreEqual(1, CountOf(boot, "GrantOfflineReward(data)"));
        }

        static int CountOf(string text, string needle)
        {
            int count = 0, at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { count++; at += needle.Length; }
            return count;
        }

        // ---------------------------------------------------------------- 초기화

        [Test]
        public void Reset_FirstIsFree_ThenCostsGemsOnce()
        {
            var character = LevelWith(30);
            var gems = WalletWith(400);
            int total = character.UnspentPoints;

            int gemEvents = 0, spendEvents = 0;
            long last = gems.Gems;
            gems.GemsChanged += balance =>
            {
                gemEvents++;
                if (balance < last) spendEvents++;
                last = balance;
            };

            foreach (var axis in CharacterLevel.AxisIds) character.TrySpendPoints(axis, 4);
            Assert.IsTrue(character.IsNextResetFree);
            Assert.AreEqual(0, StatPointReset.NextCost(character));

            // 1회: 무료
            Assert.AreEqual(StatPointReset.Result.ResetFree, StatPointReset.TryReset(character, gems));
            Assert.AreEqual(0, character.SpentPoints);
            Assert.AreEqual(total, character.UnspentPoints, "포인트가 전부 미배분으로 돌아오지 않았다");
            Assert.AreEqual(400L, gems.Gems, "무료 초기화가 보석을 뺐다");
            Assert.AreEqual(0, gemEvents);
            Assert.AreEqual(1, character.ResetCount);

            // 2회: 유료 150, 보석 이벤트 한 번
            foreach (var axis in CharacterLevel.AxisIds) character.TrySpendPoints(axis, 2);
            Assert.AreEqual(StatPointCurve.ResetGemCost, StatPointReset.NextCost(character));
            Assert.AreEqual(StatPointReset.Result.ResetPaid, StatPointReset.TryReset(character, gems));
            Assert.AreEqual(0, character.SpentPoints);
            Assert.AreEqual(total, character.UnspentPoints);
            Assert.AreEqual(400L - StatPointCurve.ResetGemCost, gems.Gems);
            Assert.AreEqual(1, spendEvents, "보석 차감이 한 번이 아니다");
            Assert.AreEqual(2, character.ResetCount);
        }

        [Test]
        public void Reset_WithoutEnoughGems_ChangesNothing()
        {
            var character = LevelWith(10);
            var gems = WalletWith(StatPointCurve.ResetGemCost - 1);
            character.TrySpendPoints(CharacterLevel.AttackAmpId, 5);
            Assert.IsTrue(character.ResetAllPoints(true));     // 무료를 써 둔다
            character.TrySpendPoints(CharacterLevel.GoldAmpId, 5);

            Assert.AreEqual(StatPointReset.Result.NotEnoughGems, StatPointReset.TryReset(character, gems));
            Assert.AreEqual(5, character.GoldPoints, "보석이 모자란데 포인트가 돌아왔다");
            Assert.AreEqual(StatPointCurve.ResetGemCost - 1, gems.Gems);
            Assert.AreEqual(1, character.ResetCount);
        }

        [Test]
        public void Reset_WithNothingSpent_KeepsTheFreeOne()
        {
            var character = LevelWith(10);
            var gems = WalletWith(1000);

            Assert.AreEqual(StatPointReset.Result.NothingToReset, StatPointReset.TryReset(character, gems));
            Assert.IsTrue(character.IsNextResetFree, "빈 초기화가 무료 1회를 먹었다");
            Assert.AreEqual(1000L, gems.Gems);
        }

        [Test]
        public void Reset_FreeFlagCannotBeUsedTwice()
        {
            var character = LevelWith(10);
            character.TrySpendPoints(CharacterLevel.GoldAmpId, 3);
            Assert.IsTrue(character.ResetAllPoints(true));

            character.TrySpendPoints(CharacterLevel.GoldAmpId, 3);
            Assert.IsFalse(character.ResetAllPoints(true), "무료가 두 번 나갔다");
            Assert.AreEqual(3, character.GoldPoints);
        }

        /** 보석은 GemWallet 한 문으로만 빠진다 - 60단계 urgent 동기화가 그 문을 듣는다 */
        [Test]
        public void Reset_SpendsGemsOnlyThroughGemWallet()
        {
            string reset = RuntimeSource("StatPointReset.cs");
            StringAssert.Contains("gems.TrySpend(StatPointCurve.ResetGemCost)", reset);
            StringAssert.DoesNotContain("SetBalance", reset);

            // 보석 차감이 포인트 되돌림보다 먼저다 - 뒤집히면 공짜 초기화가 생긴다
            string paid = reset.Substring(reset.IndexOf("if (gems == null)", StringComparison.Ordinal));
            Assert.Less(paid.IndexOf("TrySpend", StringComparison.Ordinal),
                        paid.IndexOf("ResetAllPoints(false)", StringComparison.Ordinal));

            StringAssert.DoesNotContain("GemWallet", RuntimeSource("CharacterLevel.cs").Replace("GemWallet 한 문", string.Empty));
            StringAssert.Contains("StatPointReset.TryReset", RuntimeSource("StatPointResetButton.cs"));
        }

        // ---------------------------------------------------------------- 세이브 v22

        [Test]
        public void SaveV22_RoundTripsTheNewFields()
        {
            // 68단계(v23 소환 경험치)가 한 칸 더 올렸다 - 이 검사가 재는 것은 v22의 새 칸이다
            Assert.AreEqual(23, SaveData.CurrentVersion);

            var data = SaveData.NewGame();
            data.characterLevel = 40;
            data.attackPoints = 11;
            data.healthPoints = 7;
            data.expPoints = 13;
            data.goldPoints = 17;
            data.idlePoints = 19;
            data.statResetCount = 3;

            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            Assert.IsTrue(SaveData.Migrate(back));
            Assert.AreEqual(13, back.expPoints);
            Assert.AreEqual(17, back.goldPoints);
            Assert.AreEqual(19, back.idlePoints);
            Assert.AreEqual(3, back.statResetCount);

            var character = LevelWith(0);
            character.Restore(back.characterLevel, back.exp, back.attackPoints, back.healthPoints,
                back.expPoints, back.goldPoints, back.idlePoints, back.statResetCount);
            Assert.AreEqual(13, character.ExpPoints);
            Assert.AreEqual(17, character.GoldPoints);
            Assert.AreEqual(19, character.IdlePoints);
            Assert.AreEqual(3, character.ResetCount);
            Assert.IsFalse(character.IsNextResetFree);
        }

        [Test]
        public void SaveV21_MigratesWithTheNewFieldsAtZero()
        {
            var data = SaveData.NewGame();
            data.version = 21;
            data.characterLevel = 50;
            data.attackPoints = 30;
            data.healthPoints = 19;

            string json = JsonUtility.ToJson(data);
            // v21 파일에는 새 칸이 아예 없다
            foreach (var field in new[] { "expPoints", "goldPoints", "idlePoints", "statResetCount" })
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"" + field + "\":-?\\d+", string.Empty);
            var old = JsonUtility.FromJson<SaveData>(json);

            Assert.IsTrue(SaveData.Migrate(old));
            Assert.AreEqual(23, old.version);
            Assert.AreEqual(0, old.expPoints);
            Assert.AreEqual(0, old.goldPoints);
            Assert.AreEqual(0, old.idlePoints);
            Assert.AreEqual(0, old.statResetCount);
            Assert.AreEqual(30, old.attackPoints, "옛 포인트를 건드렸다");

            // 레벨당 2점이 되면서 옛 세이브는 늘어난 몫을 미배분으로 갖는다(소급 지급 없음)
            var character = LevelWith(0);
            character.Restore(old.characterLevel, old.exp, old.attackPoints, old.healthPoints,
                old.expPoints, old.goldPoints, old.idlePoints, old.statResetCount);
            Assert.AreEqual(StatPointCurve.TotalPointsAtLevel(50) - 49, character.UnspentPoints);
            Assert.IsTrue(character.IsNextResetFree);
        }

        [Test]
        public void Restore_TrimsOverflowFromTheBottomOfTheTab()
        {
            var character = LevelWith(0);
            // Lv.3 = 4점인데 열을 들고 온 손상 세이브
            character.Restore(3, BigDouble.Zero, 2, 2, 2, 2, 2, 0);

            Assert.AreEqual(4, character.SpentPoints);
            Assert.AreEqual(0, character.IdlePoints, "방치(맨 아래)부터 깎여야 한다");
            Assert.AreEqual(0, character.GoldPoints);
            Assert.AreEqual(0, character.ExpPoints);
            Assert.AreEqual(2, character.AttackPoints, "전투 축이 먼저 깎였다");
        }

        // ---------------------------------------------------------------- 시뮬레이션

        [Test]
        public void Simulation_SpendsIntoTheNewAxes()
        {
            var rows = StageSimulation.Run(100, Field());
            var row = rows[99];

            Assert.Greater(row.ExpPoints, 0, "시뮬레이션이 경험치 축에 찍지 않는다");
            Assert.Greater(row.GoldPoints, 0, "시뮬레이션이 골드 축에 찍지 않는다");
            Assert.AreEqual(Math.Pow(StatPointCurve.ExpPerPoint, row.ExpPoints), row.ExpAmp, 1e-9);
            Assert.AreEqual(Math.Pow(StatPointCurve.GoldPerPoint, row.GoldPoints), row.GoldAmp, 1e-9);

            // 수령 자리 셋(잡몹·보스·클리어)이 증폭을 곱한다 - 소스로도 본다
            string sim = RuntimeSource("StageSimulation.cs");
            Assert.AreEqual(3, CountOf(sim, "* levels.GoldIncome"), "골드 수령 세 자리 중 증폭이 빠진 곳이 있다");
            Assert.AreEqual(2, CountOf(sim, "* levels.ExpAmp)"), "처치 경험치 두 자리(잡몹·보스) 중 증폭이 빠진 곳이 있다");
        }

        /**
         * 죽은 축이 아닌가. 새 세계는 레벨이 빨리 오르고(경험치 축), **심층에서**
         * 앞선다(골드 축이 강화 전부에 복리로 번진다 - 벽이 밀린다).
         *
         * st100~300에서는 오히려 조금 뒤진다(66 보고서 §5) - 화력 몫을 셋이 나눠
         * 공격력 증폭이 덜 찍히기 때문이고, 그것이 코리더·심층 천장을 지키는 값이다.
         * 그래서 st100의 여유가 아니라 레벨과 st700(옛 벽 너머)의 여유로 잰다
         */
        [Test]
        public void Simulation_NewAxesMoveTheWorld()
        {
            var now = StageSimulation.Run(700, Field());
            var pre = StageSimulation.Run(700, Field(), new StageSimulation.Policy { StatPointsPre66 = true });

            Assert.Greater(now[99].CharacterLevel, pre[99].CharacterLevel, "경험치 축이 레벨을 앞당기지 않는다");
            Assert.Greater(now[699].BossMargin, pre[699].BossMargin * 2d, "골드 축이 심층을 밀지 않는다");
            Assert.AreEqual(0, pre[99].ExpPoints);
            Assert.AreEqual(0, pre[99].GoldPoints);
        }

        /** 귀문 통과 시점 무변 (T1@31 ... T6@151) */
        [Test]
        public void Simulation_TrialGateTimingIsUnchanged()
        {
            var now = StageSimulation.Run(160, Field());
            var pre = StageSimulation.Run(160, Field(), new StageSimulation.Policy { StatPointsPre66 = true });

            for (int i = 0; i < 160; i++)
                Assert.AreEqual(pre[i].EvolutionTier, now[i].EvolutionTier, "st" + (i + 1) + " 경지가 달라졌다");

            int[] firsts = { 31, 41, 51, 71, 101, 151 };
            for (int tier = 1; tier <= firsts.Length; tier++)
            {
                Assert.AreEqual(tier - 1, now[firsts[tier - 1] - 2].EvolutionTier);
                Assert.AreEqual(tier, now[firsts[tier - 1] - 1].EvolutionTier, "T" + tier);
            }
        }

        // ---------------------------------------------------------------- EXP %

        [Test]
        public void ExpPercent_ShowsTwoDecimalsAndNeverRoundsUpTo100()
        {
            Assert.AreEqual("0.37%", Onikiri.UI.LevelHud.ExpPercentText(0.0037f));
            Assert.AreEqual("0.00%", Onikiri.UI.LevelHud.ExpPercentText(0f));
            Assert.AreEqual("29.00%", Onikiri.UI.LevelHud.ExpPercentText(0.29f));
            Assert.AreEqual("72.37%", Onikiri.UI.LevelHud.ExpPercentText(0.7237f));
            Assert.AreEqual("99.99%", Onikiri.UI.LevelHud.ExpPercentText(0.99999f), "반올림하면 100%로 보인다");
            Assert.AreEqual("100.00%", Onikiri.UI.LevelHud.ExpPercentText(1f));
        }

        // ---------------------------------------------------------------- 문구

        /** 런타임에만 조립되는 문구의 글자가 아틀라스에 있는가 (LeaderboardTests와 같은 자) */
        [Test]
        public void Step66Strings_AreInTheBakedCharset()
        {
            string charsetPath = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                                              "Assets/_Project/Data/FontCharset.txt");
            string charset = File.ReadAllText(charsetPath);
            string uiStrings = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath),
                                                             "Assets/_Project/Data/UIStrings.txt"));

            // 빌더(에디터 어셈블리)의 문구 상수를 소스에서 읽는다 - 이 어셈블리는
            // 에디터 코드를 참조하지 않고, 문구를 여기 다시 적으면 둘이 갈린다
            string builder = File.ReadAllText("Assets/_Project/Editor/UpgradePanelBuilder.cs");
            var constants = System.Text.RegularExpressions.Regex.Matches(builder,
                "public const string (Reset[A-Za-z]+|StatLockedFormat) = \"([^\"]*)\";");
            Assert.GreaterOrEqual(constants.Count, 8, "초기화 문구 상수를 못 찾았다");

            var messages = new List<string>
            {
                "경험치 획득 증폭", "골드 획득 증폭", "방치 보상 증폭",
                UpgradeTrack.FormatAs(UpgradeTrack.Display.Duration, BigDouble.FromDouble(1000d)),
                UpgradeTrack.FormatAs(UpgradeTrack.Display.Duration, BigDouble.FromDouble(0d)),
                UpgradeTrack.FormatAs(UpgradeTrack.Display.BonusPercent, BigDouble.FromDouble(1.0075d)),
                "레벨 98 · EXP " + Onikiri.UI.LevelHud.ExpPercentText(0.0037f),
            };
            foreach (System.Text.RegularExpressions.Match m in constants)
                messages.Add(string.Format(m.Groups[2].Value, 1234, 150));

            foreach (var message in messages)
                foreach (char c in message)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    Assert.IsTrue(charset.IndexOf(c) >= 0, string.Format(
                        "'{0}'이 아틀라스에 없다 (문구 \"{1}\")", c, message));
                    Assert.IsTrue(uiStrings.IndexOf(c) >= 0, string.Format(
                        "'{0}'이 UIStrings.txt에 없다 (문구 \"{1}\")", c, message));
                }

            Assert.AreEqual("+16시간 40분", UpgradeTrack.FormatAs(UpgradeTrack.Display.Duration, BigDouble.FromDouble(1000d)));
            Assert.AreEqual("+0분", UpgradeTrack.FormatAs(UpgradeTrack.Display.Duration, BigDouble.Zero));
        }
    }
}
