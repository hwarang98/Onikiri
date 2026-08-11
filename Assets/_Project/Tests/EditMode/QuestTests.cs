using System;
using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 퀘스트 표와 밸런스 편입을 검사한다.
     *
     * ## 무엇을 검사하는가
     *
     * 시스템 자체(카운터·수령)는 MonoBehaviour라 EditMode에서 돌리기 어렵고,
     * 그쪽은 화면과 테스트 패널이 잡는다. 여기서 지키는 것은 **표의 성질**과
     * **밸런스 편입**이다 - 둘 다 코드로만 확인할 수 있고, 틀려도 화면에서는
     * "조금 후한 것 같다" 정도로만 보인다.
     */
    public class QuestTests
    {
        static StageSimulation.Field Field()
        {
            // GoldGainAxisTests와 같은 필드값. EnemyTier 표의 가중 평균이다
            return new StageSimulation.Field
            {
                AverageMobHealth = 128d / 9d,
                AverageMobGold = 49d / 9d,
                SpawnInterval = 1.2f
            };
        }

        // ---------------------------------------------------------------- 표

        [Test]
        public void Catalog_IdsAreUnique()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (var kind in new[] { QuestKind.Daily, QuestKind.Repeat, QuestKind.Achievement })
                foreach (var spec in QuestCatalog.Of(kind))
                {
                    // id가 겹치면 세이브가 엉킨다. 수령 상태를 id로 적기 때문에
                    // (SaveData.questIds) 같은 id 둘은 서로의 상태를 덮어쓴다
                    Assert.IsTrue(seen.Add(spec.Id), "중복된 퀘스트 id: " + spec.Id);
                }

            Assert.AreEqual(QuestCatalog.TotalCount, seen.Count);
        }

        [Test]
        public void Catalog_TargetsArePositive()
        {
            foreach (var kind in new[] { QuestKind.Daily, QuestKind.Repeat, QuestKind.Achievement })
                foreach (var spec in QuestCatalog.Of(kind))
                {
                    // 목표가 0이면 진행률이 0으로 나눠지고, 반복은 티어가 무한히
                    // 열린다. 화면에는 "받기를 눌러도 계속 받아진다"로 나온다
                    Assert.Greater(spec.Target, 0d, spec.Id + "의 목표가 0 이하다");
                    Assert.Greater(spec.Gems, 0, spec.Id + "의 보석 보상이 0이다");
                }
        }

        /**
         * @brief **일일과 반복은 골드/경험치를 주지 않는다.** 31단계의 밸런스 원칙.
         *
         * 그 둘은 매일 반복해서 들어오므로, 골드를 주면 파밍 곡선 자체가
         * 이동한다 - 일회성인 업적과 성격이 다르다. 표에 실수로 값을 적어 넣으면
         * StageSimulation은 그것을 모르고(업적만 편입한다) 밴드가 조용히 어긋난다.
         */
        [Test]
        public void OnlyAchievements_GrantGoldOrExp()
        {
            foreach (var spec in QuestCatalog.Daily)
            {
                Assert.AreEqual(0d, spec.GoldMobs, spec.Id + "이 골드를 준다");
                Assert.AreEqual(0d, spec.ExpBosses, spec.Id + "이 경험치를 준다");
            }

            foreach (var spec in QuestCatalog.Repeat)
            {
                Assert.AreEqual(0d, spec.GoldMobs, spec.Id + "이 골드를 준다");
                Assert.AreEqual(0d, spec.ExpBosses, spec.Id + "이 경험치를 준다");
            }

            bool anyAchievementPays = false;
            foreach (var spec in QuestCatalog.Achievement)
                if (spec.GoldMobs > 0d || spec.ExpBosses > 0d) anyAchievementPays = true;

            // 하나도 안 주면 시뮬레이션 편입이 뜻을 잃는다
            Assert.IsTrue(anyAchievementPays, "업적이 하나도 골드/경험치를 주지 않는다");
        }

        /**
         * @brief 업적이 **경험치를 주지 않는다.**
         *
         * 31단계에 두 번 깎고도 오의 해금이 한 칸 당겨져서 통째로 뺐다.
         * 레벨은 오의 해금과 스탯 포인트를 동시에 좌우하는 축이라 일회성
         * faucet이 건드릴 자리가 아니다 - QuestCatalog.Achievement 주석 참고.
         *
         * 필드는 남겨 뒀으므로 누군가 다시 켤 수 있다. 그때 이 테스트가 먼저
         * 깨져서 **왜 0이었는지를 읽게 하는 것**이 이 검사의 목적이다.
         */
        [Test]
        public void Achievements_GrantNoExp()
        {
            foreach (var spec in QuestCatalog.Achievement)
                Assert.AreEqual(0d, spec.ExpBosses,
                    spec.Id + "이 경험치를 준다 - 레벨 곡선이 움직이면 오의 해금 "
                    + "스테이지가 밀리고 그 오의의 가격 가정이 깨진다");
        }

        /**
         * @brief 보상이 **받는 스테이지에 비례**한다.
         *
         * 처음에 업적마다 기준 스테이지를 손으로 적었다가 물렸다 - 추정이
         * 빗나가 st11 여유가 2.79에서 18.73으로 튀었다. 지금은 받는 순간의
         * 스테이지를 넘기므로 빗나갈 수가 없고, 이 테스트가 그 성질을 잠근다.
         */
        [Test]
        public void AchievementGold_ScalesWithTheStageItIsClaimedAt()
        {
            var spec = QuestCatalog.Achievement[0];
            var mobGold = BigDouble.FromDouble(Field().AverageMobGold);

            double atFive = QuestCatalog.AchievementGold(spec, mobGold, 5).ToDouble();
            double atTwenty = QuestCatalog.AchievementGold(spec, mobGold, 20).ToDouble();

            Assert.Greater(atTwenty, atFive, "늦게 받을수록 커야 한다");

            // 그 구간의 골드 배수 비율과 정확히 같아야 한다. 다르면 보상이
            // 스테이지가 아니라 다른 것에 비례하고 있다는 뜻이다
            double expected = StageCurve.GoldMultiplier(20).ToDouble()
                            / StageCurve.GoldMultiplier(5).ToDouble();
            Assert.AreEqual(expected, atTwenty / atFive, expected * 0.001d);
        }

        /**
         * @brief 업적 하나가 **보스 두 마리를 넘지 않는다.**
         *
         * 크기의 상한을 코드로 잠근다. 보스 골드는 잡몹 골드의 10.8배
         * (StageCurve.BossGoldMultiplier)이므로 21.6마리분이 두 마리다.
         * 이 선을 넘으면 얇은 밴드(st11 / st20)가 흔들린다는 것을 실측으로
         * 확인했고, 그때는 4~8마리분에서도 일반 밴드 천장을 0.06 넘겼다.
         */
        [Test]
        public void AchievementGold_StaysUnderTwoBossKills()
        {
            double twoBosses = StageCurve.BossGoldMultiplier * 2d;

            foreach (var spec in QuestCatalog.Achievement)
                Assert.LessOrEqual(spec.GoldMobs, twoBosses,
                    spec.Id + "의 골드가 보스 두 마리분을 넘는다");
        }

        // ---------------------------------------------------------------- 밸런스

        /**
         * @brief 업적을 넣어도 **보스 여유 밴드가 유지된다.**
         *
         * 31단계가 새 faucet을 넣으면서 지켜야 하는 유일한 밸런스 조건이다.
         * 보석은 DPS로 환산되지 않아 무관하고, 골드/경험치만 여기에 걸린다.
         */
        [Test]
        public void Achievements_KeepBossMarginsInsideTheBands()
        {
            var results = StageSimulation.Run(30, Field());

            foreach (var r in results)
            {
                bool finale = r.Stage % 10 == 0;
                bool chapter = !finale && r.Stage % 5 == 0;

                double floor = finale ? 1.15d : chapter ? 1.3d : 1.5d;
                double ceiling = finale ? 1.7d : chapter ? 2.0d : 3.0d;

                Assert.GreaterOrEqual(r.BossMargin, floor,
                    string.Format("st{0} 여유 {1:0.00}이 바닥 {2:0.00} 아래다", r.Stage, r.BossMargin, floor));
                Assert.LessOrEqual(r.BossMargin, ceiling,
                    string.Format("st{0} 여유 {1:0.00}이 천장 {2:0.00} 위다", r.Stage, r.BossMargin, ceiling));
            }
        }

        /**
         * @brief 업적이 **진행을 빠르게 만들되 뒤집지는 않는다.**
         *
         * 비교군(SkipAchievements)과의 차이를 잠근다. 차이가 0이면 편입이 아무
         * 일도 하지 않는다는 뜻이고(표에 골드를 적어놓고 시뮬레이션이 안 읽는
         * 상태가 그렇다), 너무 크면 밸런스를 흔든 것이다.
         */
        [Test]
        public void Achievements_ChangeProgressButOnlySlightly()
        {
            var field = Field();

            var without = StageSimulation.Policy.Default;
            without.SkipAchievements = true;

            double baseline = StageSimulation.TotalSeconds(StageSimulation.Run(30, field, without));
            double withThem = StageSimulation.TotalSeconds(StageSimulation.Run(30, field));

            Assert.AreNotEqual(baseline, withThem,
                "업적을 넣으나 빼나 결과가 같다 - 시뮬레이션이 보상을 읽지 않고 있다");

            // 5% 안쪽. 일회성 보상이 진행 시간을 그 이상 흔들면 절제한 것이 아니다
            double drift = System.Math.Abs(withThem - baseline) / baseline;
            Assert.Less(drift, 0.05d,
                string.Format("업적이 30스테이지 진행을 {0:P1} 바꿨다", drift));
        }

        /**
         * @brief 온보딩(1~5스테이지)을 **단축하지 않는다.**
         *
         * 사양이 명시한 조건이다. 현재 기준이 175초이고, 일일 퀘스트는 보석만
         * 주므로 여기 영향이 없어야 하며 업적도 그 구간에서는 작아야 한다.
         */
        [Test]
        public void Achievements_DoNotDistortOnboarding()
        {
            var field = Field();

            var without = StageSimulation.Policy.Default;
            without.SkipAchievements = true;

            double baseline = StageSimulation.TotalSeconds(StageSimulation.Run(5, field, without));
            double withThem = StageSimulation.TotalSeconds(StageSimulation.Run(5, field));

            double drift = System.Math.Abs(withThem - baseline) / baseline;
            Assert.Less(drift, 0.05d,
                string.Format("업적이 온보딩 1~5를 {0:P1} 바꿨다 ({1:0.0}초 -> {2:0.0}초)",
                    drift, baseline, withThem));
        }

        // ---------------------------------------------------------------- 일일 경계

        /**
         * @brief 일일 리셋 경계는 **KST 새벽 4시**다 (39단계, UTC 자정에서 이동).
         *
         * KST 04:00 = UTC 전날 19:00. 고정 오프셋이지 기기 시간대가 아니다 -
         * 로컬 자정을 쓰면 시간대를 넘나드는 플레이어에게 리셋이 두 번 오거나
         * 건너뛴다는 원칙은 그대로다.
         */
        [Test]
        public void DailyReset_RollsAtFourAmKst()
        {
            // UTC 18:59:59 = KST 03:59:59 - 아직 같은 퀘스트일
            var justBefore = new DateTime(2026, 8, 9, 18, 59, 59, DateTimeKind.Utc);
            // UTC 19:00:00 = KST 04:00:00 - 새 퀘스트일
            var boundary = new DateTime(2026, 8, 9, 19, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(QuestSystem.QuestDayOf(justBefore).AddDays(1),
                            QuestSystem.QuestDayOf(boundary),
                "KST 새벽 4시(UTC 19시)에 퀘스트일이 정확히 한 칸 넘어가야 한다");

            // KST 자정(UTC 15:00)은 경계가 아니다 - 자정 넘어서까지 한 판은 "오늘"이다
            var kstMidnight = new DateTime(2026, 8, 9, 15, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(QuestSystem.QuestDayOf(justBefore), QuestSystem.QuestDayOf(kstMidnight));
        }

        /** 타이머는 다음 KST 새벽 4시까지를 잰다 */
        [Test]
        public void DailyReset_MeasuresToNextFourAmKst()
        {
            // UTC 정오 = KST 21:00. 다음 KST 04:00까지 7시간
            var noon = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(TimeSpan.FromHours(7d), QuestSystem.NextResetUtc(noon) - noon);

            // 경계 1초 전
            var justBefore = new DateTime(2026, 8, 9, 18, 59, 59, DateTimeKind.Utc);
            Assert.AreEqual(TimeSpan.FromSeconds(1d),
                            QuestSystem.NextResetUtc(justBefore) - justBefore);
        }

        /**
         * @brief 예전 세이브(UTC 자정으로 자른 날짜)가 들어와도 리셋이 **건너뛰지
         * 않는다.** 옛 마커의 퀘스트일은 실제 마지막 리셋의 퀘스트일보다 뒤일 수
         * 없다 - 자정+5시간은 같은 날짜이기 때문이다. QuestSystem.RollDailyIfNeeded
         * 주석의 경계 이동 논증을 식으로 고정한다.
         */
        [Test]
        public void DailyReset_LegacyMidnightMarkerNeverSkips()
        {
            for (int hour = 0; hour < 24; hour++)
            {
                var writeTime = new DateTime(2026, 8, 9, hour, 0, 0, DateTimeKind.Utc);
                var legacyMarker = writeTime.Date;   // 옛 저장 형식

                Assert.LessOrEqual(QuestSystem.QuestDayOf(legacyMarker),
                                   QuestSystem.QuestDayOf(writeTime),
                    "옛 마커의 퀘스트일이 저장 시각의 퀘스트일을 앞지르면 리셋이 건너뛴다"
                    + " (hour=" + hour + ")");
            }
        }
    }
}
