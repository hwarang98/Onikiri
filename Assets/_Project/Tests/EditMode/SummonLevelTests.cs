using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 68단계 소환 레벨. **천장이 보장이었다면 이것은 성장이다.**
     *
     * 다섯 묶음이다:
     *
     *   식        정규화(합 1) · 단조(레벨↑ ★5↑ ★1↓) · 상한 없음 · 요구 XP 정수·단조
     *   세이브    v22 -> v23 (소환 경험치 = 누적 뽑기, 천장 카운터 폐기)
     *   잔재      천장 식별자가 소스에 남지 않았다
     *   밴드      (a) 무과금 30일 · (b) st500 ★5 배수 · (c) 초반 레벨업 간격
     *   전후      옛 천장 세계의 기대와의 비교가 숫자로 남는다
     */
    public class SummonLevelTests
    {
        static StageSimulation.Field Field()
        {
            return new StageSimulation.Field
            {
                AverageMobHealth = 14.222d,
                AverageMobGold = 5.444d,
                SpawnInterval = 1.1d
            };
        }

        /**
         * @brief 47단계 천장 세계의 ★4+ 실효 대기. **(1 - q^30) / p, p = 2.9%.**
         *
         * 천장 코드는 사라졌으므로 비교군을 식 그대로 적는다 - 20.221회다.
         */
        static double Pre68EpicOrBetterPulls
        {
            get
            {
                double p = GachaCurve.EpicOrBetterChance;
                return (1d - Math.Pow(1d - p, 30)) / p;
            }
        }

        // ---------------------------------------------------------------- 식

        [Test]
        public void Chances_SumToOneAtEveryLevel()
        {
            foreach (int level in new[] { 1, 2, 5, 10, 20, 50, 100, 1000, 100000 })
            {
                var chances = SummonLevelCurve.ChancesAt(level);
                double sum = 0d;
                foreach (double c in chances)
                {
                    Assert.IsFalse(double.IsNaN(c) || double.IsInfinity(c), "Lv." + level + " 확률이 숫자가 아니다");
                    Assert.GreaterOrEqual(c, 0d);
                    sum += c;
                }
                Assert.AreEqual(1d, sum, 1e-12d, "Lv." + level + " 확률의 합이 1이 아니다");
            }
        }

        [Test]
        public void LevelOne_IsThePublishedTable()
        {
            var chances = SummonLevelCurve.ChancesAt(1);
            for (int i = 0; i < GachaCurve.OutcomeCount; i++)
                Assert.AreEqual(GachaCurve.Chances[i], chances[i], 1e-12d,
                    "Lv.1이 공개 확률표와 다르다 - 신규 계정에게 화면이 거짓말을 한다");
        }

        /**
         * @brief 레벨이 오르면 ★5는 오르고 ★1은 내린다 - **어느 레벨에서도.**
         *
         * g★5가 가장 커야 성립한다(SummonLevelCurve.GradeGrowth 주석). 처음 값
         * (★4 1.35 > ★5 1.25)은 고레벨에서 ★4가 ★5를 잡아먹어 이 검사를 깼다.
         */
        [Test]
        public void HigherLevels_RaiseTheFifthStarAndLowerTheFirst()
        {
            var previous = SummonLevelCurve.ChancesAt(1);
            for (int level = 2; level <= 400; level++)
            {
                var now = SummonLevelCurve.ChancesAt(level);
                Assert.Greater(now[(int)GachaCurve.Outcome.LegendaryBlade],
                               previous[(int)GachaCurve.Outcome.LegendaryBlade],
                               "Lv." + level + "에서 ★5가 줄었다");
                Assert.LessOrEqual(now[(int)GachaCurve.Outcome.ShardSmall],
                            previous[(int)GachaCurve.Outcome.ShardSmall],
                            "Lv." + level + "에서 ★1이 늘었다");
                previous = now;
            }

            Assert.Greater(SummonLevelCurve.ChanceAt(5000, (int)GachaCurve.Outcome.LegendaryBlade), 0.99d,
                "아주 높은 레벨에서도 ★5가 1로 다가가지 않는다 - 성장이 어딘가에서 멈췄다");
        }

        /**
         * @brief **상한이 없다.** 최대 레벨·clamp 상수가 곡선에 없다 (확정 4·5).
         *
         * 리플렉션으로 곡선의 상수 이름을 훑는다. 상한이 다시 생기는 날 그것은
         * 이름부터 Max·Cap·Limit일 것이고, 여기서 걸린다.
         */
        [Test]
        public void TheCurve_HasNoCap()
        {
            foreach (var field in typeof(SummonLevelCurve).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                         | System.Reflection.BindingFlags.Static))
            {
                StringAssert.DoesNotMatch("(?i)max|cap|limit|ceil", field.Name,
                    "소환 레벨에 상한처럼 보이는 상수가 생겼다: " + field.Name);
            }

            // 레벨은 경험치만큼 끝없이 오른다
            Assert.Greater(SummonLevelCurve.LevelFor(1000000000L), 100,
                "10억 XP가 레벨 100에 못 닿는다 - 어딘가에서 멈췄다");
        }

        [Test]
        public void XpToNext_IsAWholeNumberAndGrows()
        {
            long previous = 0L;
            for (int level = 1; level <= 200; level++)
            {
                long need = SummonLevelCurve.XpToNext(level);
                Assert.GreaterOrEqual(need, 1L);
                Assert.GreaterOrEqual(need, previous, "Lv." + level + "의 다음 칸이 앞 칸보다 싸다");
                Assert.AreEqual(Math.Ceiling(SummonLevelCurve.XpBase
                                             * Math.Pow(SummonLevelCurve.XpGrowth, level - 1) - 1e-9d),
                                (double)need, 0d, "Lv." + level + " 요구 XP가 식과 다르다");
                previous = need;
            }
        }

        [Test]
        public void LevelFor_AndXpIntoLevel_WalkTheSameStairs()
        {
            long total = 0L;
            for (int level = 1; level <= 30; level++)
            {
                Assert.AreEqual(level, SummonLevelCurve.LevelFor(total));
                Assert.AreEqual(0L, SummonLevelCurve.XpIntoLevel(total));
                long need = SummonLevelCurve.XpToNext(level);
                Assert.AreEqual(level, SummonLevelCurve.LevelFor(total + need - 1L), "한 칸 모자란데 올랐다");
                total += need;
            }
            Assert.AreEqual(1, SummonLevelCurve.LevelFor(-5L), "음수 경험치가 Lv.1이 아니다");
        }

        [Test]
        public void LevelText_ComesFromTheCurve()
        {
            Assert.AreEqual("소환 Lv.1  ·  0 / 20", SummonLevelCurve.LevelTextFor(0L));
            Assert.AreEqual("소환 Lv.2  ·  3 / 23", SummonLevelCurve.LevelTextFor(23L));
            Assert.AreEqual("소환 Lv.4 달성", SummonLevelCurve.LevelUpText(4));
            Assert.AreEqual("파편 6  ·  소환 Lv.4 달성", Onikiri.UI.GachaResultPopup.WithLevelUp("파편 6", 4));
            Assert.AreEqual("파편 6", Onikiri.UI.GachaResultPopup.WithLevelUp("파편 6", 0),
                "레벨이 안 올랐는데 제목에 꼬리가 붙었다");
        }

        // ---------------------------------------------------------------- 세이브 v23

        [Test]
        public void V22_MigratesToV23WithPullsAsSummonXp()
        {
            var data = SaveData.NewGame();
            data.version = 22;
            data.gachaTotalPulls = 101;
            data.skillGachaTotalPulls = 40;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(23, data.version);
            Assert.AreEqual(101L, data.yodoSummonXp, "요도 누적 뽑기가 경험치로 안 옮겨졌다");
            Assert.AreEqual(40L, data.skillSummonXp, "오의 누적 뽑기가 경험치로 안 옮겨졌다");

            // 개발 세이브(요도 101 · 오의 40)의 시작 레벨 - 보고서 §구현 전 4
            Assert.AreEqual(5, SummonLevelCurve.LevelFor(data.yodoSummonXp));
            Assert.AreEqual(2, SummonLevelCurve.LevelFor(data.skillSummonXp));
        }

        [Test]
        public void V22_Migration_IsIdempotentAndRefusesTheFuture()
        {
            var data = SaveData.NewGame();
            data.version = 22;
            data.gachaTotalPulls = 7;
            SaveData.Migrate(data);
            data.yodoSummonXp = 500L;       // 그 뒤로 더 뽑았다

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(500L, data.yodoSummonXp, "두 번째 마이그레이션이 경험치를 되돌렸다");

            var future = SaveData.NewGame();
            future.version = SaveData.CurrentVersion + 1;
            Assert.IsFalse(SaveData.Migrate(future), "v24 세이브를 받아들였다");
        }

        [Test]
        public void V23_SaveRoundTripsThroughJson()
        {
            var data = SaveData.NewGame();
            data.yodoSummonXp = 123456789012L;
            data.skillSummonXp = 77L;

            var back = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(data));
            Assert.AreEqual(123456789012L, back.yodoSummonXp);
            Assert.AreEqual(77L, back.skillSummonXp);

            // v22 JSON의 옛 천장 칸은 그냥 무시된다 - 필드가 없어졌을 뿐이다
            string old = "{\"version\":22,\"gachaPity\":17,\"gachaTotalPulls\":30,"
                       + "\"skillGachaPity\":9,\"skillGachaAwakenPity\":41,\"skillGachaTotalPulls\":12}";
            var migrated = UnityEngine.JsonUtility.FromJson<SaveData>(old);
            Assert.IsTrue(SaveData.Migrate(migrated));
            Assert.AreEqual(30L, migrated.yodoSummonXp);
            Assert.AreEqual(12L, migrated.skillSummonXp);
        }

        // ---------------------------------------------------------------- 잔재

        /**
         * @brief **천장 식별자가 소스에 없다** (확정 1). 테스트 코드는 뺀다.
         *
         * 주석과 문자열은 지운 뒤에 잰다 - 역사 주석("47단계의 천장")은 남아도
         * 되고, 남아야 한다. 걸리는 것은 코드가 아직 천장을 **부르는** 자리다.
         */
        [Test]
        public void NoPityIdentifierIsLeft()
        {
            var offenders = new System.Collections.Generic.List<string>();
            foreach (string root in new[] { "Assets/_Project/Scripts", "Assets/_Project/Editor",
                                            "Assets/_Project/DevTools" })
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string code = StripCommentsAndStrings(File.ReadAllText(path));
                    var hit = Regex.Match(code, @"\b\w*(Pity|pity)\w*\b");
                    if (hit.Success) offenders.Add(Path.GetFileName(path) + ": " + hit.Value);
                }
            }

            Assert.IsEmpty(offenders, "천장 식별자가 남았다: " + string.Join(", ", offenders));
        }

        static string StripCommentsAndStrings(string source)
        {
            // 블록 주석 · 줄 주석 · 문자열(일반·축자) · 문자 리터럴
            return Regex.Replace(source,
                @"/\*.*?\*/|//[^\n]*|@""(?:[^""]|"""")*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'",
                " ", RegexOptions.Singleline);
        }

        // ---------------------------------------------------------------- 밴드

        /**
         * @brief (a) 반씩 나눈 무과금이 **30일 안에** 옛 천장의 ★4+ 실효에 닿는다.
         *
         * 장부(PromotionEconomyLedger)가 무과금의 하루를 걷는다 - 일일 보석 55 +
         * 진행 보석, 핵심 축(장비·동료)이 끝난 뒤 10연. 그 뽑기의 절반이 요도
         * 배너로 가고, 일일 무료 하나가 날마다 더해진다. 68단계 결정 1이다.
         */
        [Test]
        public void Band_A_FreeToPlay_CatchesThePityRhythmWithinThirtyDays()
        {
            int reached = -1;
            for (int day = 1; day <= 30 && reached < 0; day++)
            {
                var input = PromotionEconomyFixture.Build(PromotionEconomyFixture.FreePromotion, 0.40d, day);
                var result = PromotionEconomyLedger.Run(input);

                long ledgerPulls = result.SpentGacha / GachaCurve.TenPullCostGems * GachaCurve.TenPullCount;
                long xp = day * GachaCurve.FreePullsPerDay + ledgerPulls / 2;
                int level = SummonLevelCurve.LevelFor(xp);

                if (GachaCurve.ExpectedPullsPerEpic(level) <= Pre68EpicOrBetterPulls) reached = day;
            }

            Assert.Greater(reached, 0, string.Format(
                "반씩 나눈 무과금이 30일 안에 ★4+ 하나에 {0:F1}회(옛 천장)에 못 닿는다",
                Pre68EpicOrBetterPulls));
        }

        /**
         * @brief (b) st500 추종 레벨의 ★5가 Lv.1의 **3배 이상 10배 이하**.
         *
         * 체감은 있되 전설이 흔해지지 않게 - 확정 (b)의 출발 범위다. 추종의
         * 뽑기 수는 시뮬레이션이 낸다(재고가 닫히면 멈춘다).
         */
        [Test]
        public void Band_B_TheFifthStarGrowsButStaysRare()
        {
            var rows = StageSimulation.Run(500, Field());
            int level = SummonLevelCurve.LevelFor((long)rows[499].GachaPulls);
            double ratio = GachaCurve.LegendaryChanceAt(level) / GachaCurve.LegendaryChance;

            Assert.GreaterOrEqual(ratio, 3d, string.Format(
                "st500 추종(Lv.{0}, {1:F0}회)의 ★5가 Lv.1의 x{2:F2} - 성장이 안 느껴진다",
                level, rows[499].GachaPulls, ratio));
            Assert.LessOrEqual(ratio, 10d, string.Format(
                "st500 추종(Lv.{0})의 ★5가 Lv.1의 x{1:F2} - 전설이 흔해졌다", level, ratio));
        }

        /** (c) 초반은 10연 두세 번에 한 레벨, 뒤로 갈수록 멀어진다 */
        [Test]
        public void Band_C_EarlyLevelsComeEveryTwoOrThreeTenPulls()
        {
            for (int level = 1; level <= 3; level++)
            {
                long need = SummonLevelCurve.XpToNext(level);
                Assert.That(need, Is.InRange(2L * GachaCurve.TenPullCount, 3L * GachaCurve.TenPullCount),
                    "Lv." + level + " -> " + (level + 1) + "이 " + need + "회다 - 10연 두세 번이 아니다");
            }

            Assert.Greater(SummonLevelCurve.XpToNext(15), 3L * SummonLevelCurve.XpToNext(1),
                "후반 레벨이 초반보다 충분히 멀지 않다 - 비용 게이트가 아니다");
        }

        /**
         * @brief 파편이 무너지는 레벨은 **재고가 닫힌 한참 뒤**다. 촉매는 어느 레벨에서도 이긴다.
         *
         * 레벨이 오르면 파편 등급(★1·★2)의 몫이 위로 옮겨 가 기대 파편이 준다 -
         * Lv.20에서 Lv.1의 5%, Lv.50에서 0이다. 그것이 문제가 되려면 추종이 **재고가
         * 열린 채로** 그 레벨에 가야 한다. 실측(68단계): st1~1000에서 마지막 뽑기가
         * st640 · 246회 · Lv.8이고, 거기서 파편은 Lv.1의 70%다. Lv.20(1775회)은 그
         * 일곱 배 뒤다. 그래서 두 가지를 못 박는다 - 재고가 닫히는 레벨에서 파편이
         * 절반 위이고, 촉매(확정·저렴)가 보석당 파편에서 모든 레벨을 이긴다.
         */
        [Test]
        public void Shards_CollapseOnlyAfterTheStockCloses_AndTheCatalystAlwaysWins()
        {
            double catalyst = (double)YodoCurve.ShardPackShards / YodoCurve.ShardPackGems;
            double previous = double.PositiveInfinity;
            foreach (int level in new[] { 1, 5, 10, 20, 50 })
            {
                double shards = GachaCurve.ExpectedShardsPerPull(level);
                Assert.LessOrEqual(shards, previous, "Lv." + level + "에서 파편 기대가 늘었다");
                Assert.Greater(catalyst, shards / GachaCurve.PullCostGems, string.Format(
                    "Lv.{0}에서 뽑기가 보석당 파편을 촉매보다 많이 준다", level));
                previous = shards;
            }

            var rows = StageSimulation.Run(1000, Field());
            double lastPulls = 0d;
            int lastAt = 0;
            foreach (var row in rows)
                if (row.GachaPulls > lastPulls) { lastPulls = row.GachaPulls; lastAt = row.Stage; }

            int level8 = SummonLevelCurve.LevelFor((long)lastPulls);
            double ratio = GachaCurve.ExpectedShardsPerPull(level8) / GachaCurve.ExpectedShardsPerPull(1);
            Assert.GreaterOrEqual(ratio, 0.5d, string.Format(
                "추종이 재고가 열린 채로 Lv.{0}(st{1}, {2:F0}회)까지 가서 파편이 Lv.1의 {3:P0}로 "
                + "무너졌다 - 파편이 필요한 동안 뽑기가 파편을 안 준다",
                level8, lastAt, lastPulls, ratio));
        }

        // ---------------------------------------------------------------- 전후

        /**
         * @brief 레벨이 오를수록 ★4+ 대기가 짧아지고, **Lv.3에서 옛 천장을 지난다.**
         *
         * 천장 세계는 첫 뽑기부터 20.2회였다. 소환 레벨은 34.5회(Lv.1)에서 출발해
         * 따라잡는다 - 그 교차점이 반씩 나눈 무과금의 28일차다.
         */
        [Test]
        public void EpicOrBetter_OvertakesThePityRhythmByLevelThree()
        {
            Assert.Greater(GachaCurve.ExpectedPullsPerEpic(1), Pre68EpicOrBetterPulls,
                "Lv.1이 이미 천장보다 짧다 - 천장을 걷어낸 대가가 없다는 것은 표가 바뀌었다는 뜻이다");
            Assert.LessOrEqual(GachaCurve.ExpectedPullsPerEpic(3), Pre68EpicOrBetterPulls,
                "Lv.3이 아직 옛 천장보다 길다 - (a)의 28일이 무너졌다");
        }
    }
}
