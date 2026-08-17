using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 요괴 봉인 검(妖刀). 혼 드랍 -> 봉인·합성 -> 도감 세트.
     *
     * 이 축이 다른 축과 다른 점 하나가 검사의 절반을 정한다 - **골드로 사지
     * 않는다.** 속도를 정하는 것이 지갑이 아니라 한 바퀴(40스테이지)이므로,
     * "얼마에 파는가"가 아니라 **"언제 무엇이 떨어지는가"**가 밸런스다.
     * 그래서 여기서 가장 많이 검사하는 것은 드랍 일정이 애셋·로스터·곡선
     * 셋에서 같은 답을 내는가다.
     */
    public class YodoTests
    {
        const string RosterPath = "Assets/_Project/Data/Bosses/BossRoster.asset";

        static BossRoster LoadRoster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<BossRoster>(RosterPath);
            Assert.IsNotNull(roster, "보스 로스터가 없다: " + RosterPath);
            return roster;
        }

        static StageSimulation.Field Field()
        {
            return new StageSimulation.Field
            {
                AverageMobHealth = 14.222d,
                AverageMobGold = 5.444d,
                SpawnInterval = 1.1d
            };
        }

        static double TotalSeconds(List<StageSimulation.StageResult> rows, int from, int to)
        {
            double total = 0d;
            for (int i = from - 1; i < to && i < rows.Count; i++)
                total += rows[i].MobSeconds + StageSimulation.BossIntroSeconds
                       + StageSimulation.BossWalkInSeconds + rows[i].BossKillSeconds;
            return total;
        }

        // ---------------------------------------------------------------- 표

        [Test]
        public void Catalog_HasUniqueIdsAndNames()
        {
            var ids = new HashSet<string>();
            foreach (var blade in YodoCatalog.Blades)
            {
                Assert.IsFalse(string.IsNullOrEmpty(blade.Id), "id가 비었다");
                Assert.IsTrue(ids.Add(blade.Id), "id가 겹친다: " + blade.Id);

                Assert.IsFalse(string.IsNullOrEmpty(blade.SoulName));
                Assert.IsFalse(string.IsNullOrEmpty(blade.BladeName));
                Assert.IsFalse(string.IsNullOrEmpty(blade.BossName));
                Assert.IsFalse(string.IsNullOrEmpty(blade.IconSprite), "아이콘 이름이 비었다");
            }
        }

        /**
         * @brief 완성된 칼의 이름이 장비 등급 이름과 겹치지 않는다.
         *
         * 44단계에 장비 5등급을 "오니키리"에서 "명공검"으로 옮긴 이유다 -
         * 대장간 한 화면에 같은 이름의 다른 물건이 둘 서면 그것은 두
         * 시스템이 아니라 버그로 읽힌다. 되돌리면 여기서 걸린다.
         */
        [Test]
        public void OnikiriName_DoesNotCollideWithEquipmentGrades()
        {
            foreach (var slot in EquipmentCatalog.Slots)
                foreach (var grade in slot.GradeNames)
                    Assert.AreNotEqual(YodoCatalog.OnikiriName, grade,
                        "장비 등급 이름이 오니키리와 겹친다 - 대장간 한 화면에 같은 이름 둘");
        }

        // ---------------------------------------------------------------- 애셋 대조

        /**
         * @brief 세계 한 바퀴의 길이가 로스터와 같은가.
         *
         * 곡선은 카탈로그 수 x 지역 길이로 유도하고(YodoCurve.CycleLength),
         * 로스터는 지역 애셋의 stageCount 합이다. 지역을 하나 더하는 날 둘이
         * 갈리면 혼이 엉뚱한 스테이지에서 떨어진다.
         */
        [Test]
        public void CycleLength_MatchesTheRoster()
        {
            var roster = LoadRoster();

            int total = 0;
            foreach (var region in roster.regions)
                if (region != null) total += region.stageCount;

            Assert.AreEqual(total, YodoCurve.CycleLength,
                "순환 길이가 로스터와 다르다 - 혼 일정이 실제 보스와 어긋난다");
            Assert.AreEqual(YodoCatalog.Count, roster.regions.Length,
                "혼 종류 수와 지역 수가 다르다 - 대요괴 하나가 혼을 못 남기거나 " +
                "없는 요괴의 혼이 표에 있다");
        }

        /**
         * @brief 곡선의 혼 일정과 **애셋이 실제로 세우는 보스**가 같은가.
         *
         * 드랍은 런타임에서 BossConfig.soulId로 가고(단일 출처), 보정과
         * 시뮬레이션은 YodoCurve.SoulIndexDroppedAt으로 간다. 둘이 갈리면
         * "등롱을 벴는데 흑야의 혼이 나온다" - 42단계 세계 순환이 만든
         * 위험이고, 여기서만 잡을 수 있다.
         */
        [Test]
        public void SoulSchedule_MatchesTheRoster()
        {
            var roster = LoadRoster();

            for (int stage = 1; stage <= 200; stage++)
            {
                var config = roster.BossForStage(stage);
                string assetSoul = config != null ? config.soulId : null;

                int index = YodoCurve.SoulIndexDroppedAt(stage);

                if (!YodoCurve.IsUnlockedAt(stage))
                {
                    Assert.AreEqual(-1, index, "stage " + stage + ": 해금 전에 혼이 떨어진다");
                    continue;
                }

                if (index < 0)
                {
                    // 곡선이 "혼 없음"이라고 했으면 애셋도 그래야 한다.
                    // 정예(챕터)와 일반 보스가 여기 온다
                    Assert.IsTrue(string.IsNullOrEmpty(assetSoul),
                        "stage " + stage + ": 애셋은 혼을 남기는데 곡선은 아니라고 한다 ("
                        + assetSoul + ")");
                    continue;
                }

                Assert.AreEqual(YodoCatalog.Blades[index].Id, assetSoul,
                    "stage " + stage + ": 곡선과 애셋의 혼이 다르다");
            }
        }

        /** 도감의 "○○ 처치 시 해금"이 실제 보스 이름과 같은가 */
        [Test]
        public void Catalog_MatchesTheBossAssets()
        {
            var roster = LoadRoster();

            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                // i번 혼은 순환 위치 (i+1) x 10 에서 떨어진다
                int stage = YodoCurve.FirstDropStage(i);
                var config = roster.BossForStage(stage);

                Assert.IsNotNull(config, "stage " + stage + "에 보스 애셋이 없다");
                Assert.AreEqual(config.displayName, YodoCatalog.Blades[i].BossName,
                    "도감의 보스 이름이 화면 이름과 다르다 - 플레이어가 대조할 수 없다");
            }
        }

        /** 정예는 혼을 남기지 않는다. 남기면 지역 넷이 같은 혼을 준다 */
        [Test]
        public void EliteBoss_LeavesNoSoul()
        {
            var roster = LoadRoster();

            foreach (var region in roster.regions)
            {
                Assert.IsNotNull(region.chapterBoss);
                Assert.IsTrue(string.IsNullOrEmpty(region.chapterBoss.soulId),
                    "정예가 혼을 남긴다 - 네 지역이 같은 애셋을 물고 있어 " +
                    "도감의 한 줄이 어느 지역인지 가리키지 못한다");
            }
        }

        // ---------------------------------------------------------------- 곡선

        /**
         * @brief 조율 구간(st1~50)에 이 축도 보정도 **존재하지 않는다.**
         *
         * 계수가 아니라 구조가 지키는 불변이다 - 첫 혼이 st50 피날레에
         * 떨어지고 그 힘은 st51부터 붙는다. 장비(st11)·동료(st31)가 각자
         * 자기 구간 밖에 선 것과 같은 검사이고, 여기는 코리더뿐 아니라
         * **가속 구간까지** 덮는다.
         */
        [Test]
        public void Yodo_IsAbsentFromTheTunedBands()
        {
            for (int stage = 1; stage <= 50; stage++)
            {
                Assert.AreEqual(1d, YodoCurve.ExpectedMultiplierAtStage(stage), 1e-12d,
                    "stage " + stage + ": 조율 구간에 요도 배수가 있다");
                Assert.AreEqual(1d, StageCurve.YodoCompensation(stage), 1e-12d,
                    "stage " + stage + ": 조율 구간에 요도 보정이 걸린다 - " +
                    "21단계 골드 축의 사고(축은 없는데 보정만)가 재현된다");
            }

            Assert.Greater(YodoCurve.ExpectedMultiplierAtStage(51), 1d,
                "st51에 요도가 없다 - st50 피날레의 혼이 안 들어왔다");
        }

        [Test]
        public void TierValue_IsExactlyOneWhenUnsealed()
        {
            Assert.AreEqual(1d, YodoCurve.TierValue(0), 1e-12d);
            Assert.AreEqual(1d, YodoCurve.TierValue(-3), 1e-12d);

            // 상한 위는 자른다 - 세이브의 티어를 깎지 않는 규칙의 값 쪽
            Assert.AreEqual(YodoCurve.TierValue(YodoCurve.MaxTier),
                            YodoCurve.TierValue(YodoCurve.MaxTier + 5), 1e-12d);
        }

        /** 봉인 한 칸이 티어 한 칸보다 크다 - 없던 칼이 생기는 일이기 때문 */
        [Test]
        public void Sealing_IsBiggerThanATierStep()
        {
            Assert.Greater(YodoCurve.SealStep, YodoCurve.TierStep);
            Assert.Greater(YodoCurve.SealStep, 1d);
        }

        /** 세트 보너스의 마지막 칸(오니키리)만 도약이다 */
        [Test]
        public void SetBonus_JumpsOnlyAtCompletion()
        {
            double prev = YodoCurve.SetBonusAt(0);
            double biggestBefore = 0d;

            for (int n = 1; n < YodoCatalog.Count; n++)
            {
                double step = YodoCurve.SetBonusAt(n) / prev;
                Assert.Greater(step, 1d, "세트 보너스 " + n + "칸이 안 오른다");
                biggestBefore = Mathf.Max((float)biggestBefore, (float)step);
                prev = YodoCurve.SetBonusAt(n);
            }

            // 배수의 **초과분**끼리 비교한다. 1.25/1.09 = 1.147 과
            // 1.09/1.05 = 1.038 은 비로 보면 가깝지만, 실제로 얹히는 몫은
            // 0.147 대 0.038 로 네 배 가까이 다르다 - 화면에서 읽히는 것도
            // 그 몫이다
            double last = YodoCurve.SetBonusAt(YodoCatalog.Count) / prev;
            Assert.Greater(last - 1d, (biggestBefore - 1d) * 2d,
                "오니키리 완성이 앞의 칸과 같은 크기다 - 완성이 진척의 연장으로 읽힌다");

            Assert.IsTrue(YodoCurve.IsComplete(YodoCatalog.Count));
            Assert.IsFalse(YodoCurve.IsComplete(YodoCatalog.Count - 1));
        }

        /** 혼은 한 바퀴에 한 번씩만 들어온다. 그것이 이 축의 속도다 */
        [Test]
        public void Souls_ArriveOncePerCycle()
        {
            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                int first = YodoCurve.FirstDropStage(i);
                Assert.AreEqual(i, YodoCurve.SoulIndexDroppedAt(first));
                Assert.AreEqual(i, YodoCurve.SoulIndexDroppedAt(first + YodoCurve.CycleLength));

                // 첫 드랍은 해금(st41) 뒤 첫 피날레부터다
                Assert.GreaterOrEqual(first, YodoCurve.UnlockStage);

                // 그 혼의 힘은 **다음 스테이지부터**다. 이 한 칸이 가속 구간의
                // 마지막(st50)을 지킨다
                Assert.AreEqual(0, YodoCurve.SoulsBeforeStage(i, first));
                Assert.AreEqual(1, YodoCurve.SoulsBeforeStage(i, first + 1));
            }
        }

        // ---------------------------------------------------------------- 시뮬레이션

        [Test]
        public void Simulation_HasASlotForEveryBlade()
        {
            Assert.LessOrEqual(YodoCatalog.Count, StageSimulation.YodoSlotCapacity,
                "카탈로그가 시뮬레이션의 칸보다 많다 - 뒤쪽 요도가 조용히 무시된다");
        }

        /**
         * @brief 조율 구간(st1~50)이 **비트 단위로** 요도 이전과 같은가.
         *
         * 기본 정책과 "요도도 보정도 없는 세계"를 나란히 돌려 부동소수점
         * 값까지 대조한다. 42단계 심층 램프가 st50 이하 불변을 같은 방식으로
         * 지켰고, 이 스텝은 코리더뿐 아니라 가속 구간까지 그래야 한다.
         */
        [Test]
        public void TunedBands_AreBitIdenticalWithoutYodo()
        {
            var field = Field();
            var with = StageSimulation.Run(50, field);
            var without = StageSimulation.Run(50, field,
                new StageSimulation.Policy { NeutralizeYodo = true });

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(without[i].BossMargin, with[i].BossMargin,
                    "stage " + (i + 1) + ": 요도가 조율 구간의 보스 여유를 움직였다");
                Assert.AreEqual(without[i].MobSeconds, with[i].MobSeconds,
                    "stage " + (i + 1) + ": 요도가 조율 구간의 파밍 시간을 움직였다");
            }
        }

        /**
         * @brief 기대 곡선이 **촉매 없는 무과금 경로**를 그대로 따라가는가.
         *
         * 요도만 기대 곡선이 무과금 쪽이다(YodoCurve.ExpectedStateAtStage 주석) -
         * 보정이 따라가는 선이 곧 아무도 그 아래로 떨어지지 않아야 하는
         * 선이기 때문이다. 둘이 갈리면 무과금이 없는 이득을 상쇄당한다.
         */
        [Test]
        public void ExpectedCurve_TracksTheSimulation()
        {
            var rows = StageSimulation.Run(300, Field(),
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true });

            for (int stage = YodoCurve.UnlockStage; stage < 300; stage++)
            {
                // 표의 한 줄은 **그 스테이지를 끝낸 뒤**의 상태이므로 기대
                // 곡선의 다음 칸과 맞춘다
                Assert.AreEqual(YodoCurve.ExpectedMultiplierAtStage(stage + 1),
                                rows[stage - 1].YodoMultiplier, 1e-9d,
                    "stage " + stage + ": 기대 곡선이 실측과 갈렸다 - " +
                    "보정이 무과금을 따라가지 못한다");
            }
        }

        /**
         * @brief 벼리지 않으면 손해인가. **죽은 버튼 검사의 한쪽.**
         *
         * 보정은 걷어내지 않는다 - 스테이지의 함수라 플레이어를 구분하지
         * 못하고, 그래서 이 비교군이 재는 것은 "안 벼리면 손해인가"다.
         * 이 축은 골드가 안 드니 "안 산다"가 성립하지 않는다 - 정확히
         * **봉인 버튼을 한 번도 안 누른 플레이어**를 잰다.
         */
        [Test]
        public void Forging_IsNotADeadButton()
        {
            var field = Field();
            var forged = StageSimulation.Run(200, field);
            var skipped = StageSimulation.Run(200, field,
                new StageSimulation.Policy { SkipYodo = true });

            double gain = TotalSeconds(skipped, 51, 200) / TotalSeconds(forged, 51, 200) - 1d;

            Assert.Greater(gain, 0.04d,
                string.Format("봉인·합성의 이득이 {0:P1}뿐이다 - 20단계 골드 축의 함정"
                    + "(지표는 사라는데 실제로는 손해)이 재현되고 있다", gain));
        }

        /**
         * @brief 이 축을 게임에 넣은 것이 이득이었는가. **비교군의 다른 쪽.**
         *
         * Policy.SkipYodo와 다른 질문이다 - 저쪽은 "안 벼리면 손해인가"이고
         * 이쪽은 축과 보정을 **둘 다** 걷어낸 43단계 세계와 비교한다.
         * 20단계가 두 답을 섞어 읽어 "사면 손해"라는 결론을 냈다.
         */
        [Test]
        public void Yodo_IsWorthAddingAtAll()
        {
            var field = Field();
            var withYodo = StageSimulation.Run(200, field);
            var before = StageSimulation.Run(200, field,
                new StageSimulation.Policy { NeutralizeYodo = true });

            double gain = TotalSeconds(before, 51, 200) / TotalSeconds(withYodo, 51, 200) - 1d;

            Assert.Greater(gain, 0.04d,
                string.Format("요도를 넣은 이득이 {0:P1}뿐이다 - 보정 지수"
                    + "(StageCurve.YodoMarginExponent {1})가 너무 높다",
                    gain, StageCurve.YodoMarginExponent));
        }

        /**
         * @brief 보석 촉매(파편 조달)가 실제로 시간을 앞당기는가.
         *
         * 이 스텝의 새 보석 소비처이고, 그것이 죽은 버튼이면 보석은 다시
         * 갈 곳을 잃는다. 창은 **파편이 모자라기 시작한 뒤**에 열린다 -
         * 초반 바퀴는 정예 드랍만으로 남으므로(YodoCurve.ShardsPerElite 주석)
         * 여기서 st200 이전을 재면 0%가 나온다. 실제로 25로 뒀을 때 그랬다.
         *
         * **46단계에 두 세계 다 SkipGacha가 됐다.** 뽑기도 파편을 주므로
         * (겹치는 두 상품) 뽑기가 켜진 세계에서는 촉매를 빼도 0%가 나온다 -
         * 촉매가 죽은 것이 아니라 **이 자로는 잴 수 없게** 된 것이다. 그래서
         * 여기는 44단계의 질문("촉매가 이 축의 파편 병목을 푸는가")을 그
         * 세계에서 그대로 묻고, 46단계의 질문("두 상품이 겹쳐도 각자 이득인가")은
         * GachaTests.Catalyst_IsStillWorthAddingAtAll이 둘 다 없는 세계를
         * 기준으로 따로 잰다. 20단계가 SkipGoldGain과 NeutralizeGoldAxis를
         * 나눈 것과 같은 처리다.
         */
        [Test]
        public void ShardPack_BuysTime()
        {
            var field = Field();
            var withPacks = StageSimulation.Run(450, field,
                new StageSimulation.Policy { SkipGacha = true });
            var without = StageSimulation.Run(450, field,
                new StageSimulation.Policy { SkipShardPacks = true, SkipGacha = true });

            double gain = TotalSeconds(without, 200, 450) / TotalSeconds(withPacks, 200, 450) - 1d;

            Assert.Greater(gain, 0.04d,
                string.Format("보석 파편 묶음의 이득이 {0:P1}뿐이다 - 파편이 남아서 "
                    + "살 이유가 없다(ShardsPerElite {1})", gain, YodoCurve.ShardsPerElite));
        }

        /**
         * @brief 무과금은 촉매 없이도 요도가 오르는가. **f2p 바닥의 안전선.**
         *
         * ShardPack_BuysTime과 짝이다. 촉매가 이득이려면 파편이 모자라야
         * 하는데, 너무 모자라면 무과금의 축이 통째로 멈춘다 - 그러면 보정만
         * 걸리고 이득이 없는 21단계의 사고가 된다.
         */
        [Test]
        public void FreeToPlay_KeepsForgingWithoutTheCatalyst()
        {
            var rows = StageSimulation.Run(300, Field(),
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true });

            Assert.AreEqual(YodoCatalog.Count, rows[79].YodoSealed,
                "무과금이 st80까지 오니키리를 완성하지 못했다 - 봉인에는 파편이 " +
                "들지 않으므로 여기가 막히면 드랍 일정이 어긋난 것이다");

            foreach (int stage in new[] { 120, 200, 300 })
            {
                var tiers = rows[stage - 1].YodoTiers;
                foreach (int tier in tiers)
                    Assert.GreaterOrEqual(tier, 2,
                        "st" + stage + ": 무과금의 요도가 티어 2에서 멈췄다 - " +
                        "파편(ShardsPerElite " + YodoCurve.ShardsPerElite + ")이 너무 적다");
            }
        }

        /**
         * @brief 버려지는 드랍이 0인가.
         *
         * 티어가 남아 있는 동안 혼은 그 자리에서 쓰이고(SoulsPerTier = 1),
         * 상한에 닿은 뒤에는 파편으로 바뀐다. 그 사이에 혼이 쌓인 채로
         * 멈추는 구간이 있으면 그것이 "죽은 드랍"이다.
         */
        [Test]
        public void NoSoulIsWasted()
        {
            var rows = StageSimulation.Run(500, Field());

            // 곡선 추종(촉매 있음) 플레이어는 언제나 혼에만 막히므로 손에 든
            // 혼이 쌓이지 않는다 - 상한에 닿은 뒤에도 파편으로 바뀐다
            for (int stage = YodoCurve.UnlockStage; stage <= 500; stage++)
                Assert.AreEqual(0, rows[stage - 1].SoulsHeld,
                    "stage " + stage + ": 쓰이지 못한 혼이 손에 남아 있다");

            // 상한 뒤에도 파편은 계속 는다 - 대요괴가 파편 공장이 된다
            Assert.Greater(rows[499].Shards, rows[439].Shards,
                "상한 뒤 대요괴의 혼이 아무것도 되지 않는다");
        }

        // ---------------------------------------------------------------- 시스템

        /**
         * @brief 해금된 상태의 YodoSystem 하나. 빌더가 씬에 하는 일과 같다.
         *
         * StageProgress를 함께 세우는 이유는 해금이 **최전선**으로 걸리기
         * 때문이다(YodoSystem.StageNow). 없으면 최전선이 1로 읽혀 봉인
         * 버튼이 통째로 잠긴다 - 실제로 한 번 물렸다.
         */
        static YodoSystem BuildSystem()
        {
            var go = new GameObject("YodoTestSystem");

            var progress = go.AddComponent<StageProgress>();
            progress.SetProgress(YodoCurve.UnlockStage, 0, 0, YodoCurve.UnlockStage);

            var system = go.AddComponent<YodoSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("stage").objectReferenceValue = progress;
            var blades = so.FindProperty("blades");
            blades.arraySize = YodoCatalog.Count;

            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                var spec = YodoCatalog.Blades[i];
                var element = blades.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("soulName").stringValue = spec.SoulName;
                element.FindPropertyRelative("bladeName").stringValue = spec.BladeName;
                element.FindPropertyRelative("bossName").stringValue = spec.BossName;
                element.FindPropertyRelative("souls").longValue = 0L;
                element.FindPropertyRelative("tier").intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }

        static BossConfig LanternConfig()
        {
            var roster = AssetDatabase.LoadAssetAtPath<BossRoster>(RosterPath);
            return roster.BossForStage(YodoCurve.FirstDropStage(0));
        }

        /**
         * @brief 해금 전(st1~40)에는 대요괴를 베어도 아무것도 남지 않는다.
         *
         * 조율 구간의 밴드가 이 한 줄에 걸려 있다 - 되살아난 요괴만 혼을
         * 남긴다는 설정이 곧 밸런스 게이트다.
         */
        [Test]
        public void NothingDropsBeforeUnlock()
        {
            var system = BuildSystem();
            try
            {
                system.ReportBossDefeated(LanternConfig(), 10);
                system.ReportBossDefeated(LanternConfig(), 40);

                Assert.AreEqual(0L, system.Shards);
                Assert.AreEqual(0, system.GetBlade(0).tier);
                Assert.AreEqual(0L, system.GetBlade(0).souls);
                Assert.IsFalse(system.GetBlade(0).discovered,
                    "해금 전 처치가 도감을 켰다 - 잠긴 미리보기가 사라진다");
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        [Test]
        public void SealingCostsASoulAndNoShards()
        {
            var system = BuildSystem();
            try
            {
                system.ReportBossDefeated(LanternConfig(), YodoCurve.FirstDropStage(0));

                Assert.AreEqual(1L, system.GetBlade(0).souls, "혼이 안 떨어졌다");
                Assert.IsTrue(system.GetBlade(0).discovered);
                Assert.AreEqual(0, system.ShardCostOf(0), "봉인에 파편이 든다");

                Assert.IsTrue(system.TryForge(0), "파편 0인데 봉인이 막혔다");
                Assert.AreEqual(1, system.GetBlade(0).tier);
                Assert.AreEqual(0L, system.GetBlade(0).souls);

                // 티어업부터 재료가 붙는다
                Assert.Greater(system.ShardCostOf(0), 0);
                Assert.IsFalse(system.CanForge(0), "혼도 파편도 없는데 합성이 열려 있다");
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /** 정예는 파편만 남긴다. 혼은 하나도 안 는다 */
        [Test]
        public void EliteDropsShardsOnly()
        {
            var system = BuildSystem();
            try
            {
                // 순환 안의 정예 자리 (해금 뒤 첫 챕터 관문)
                int elite = YodoCurve.UnlockStage + BossCurve.ChapterEvery - 1;
                Assert.IsTrue(YodoCurve.DropsShardsAt(elite), "st" + elite + "이 정예가 아니다");

                system.ReportBossDefeated(null, elite);

                Assert.AreEqual(YodoCurve.ShardsPerElite, system.Shards);
                for (int i = 0; i < system.BladeCount; i++)
                    Assert.AreEqual(0L, system.GetBlade(i).souls);
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /** 상한 요도에게 온 혼은 파편이 된다 - 버려지는 드랍 0 */
        [Test]
        public void OverflowSoulBecomesShards()
        {
            var system = BuildSystem();
            try
            {
                for (int i = 0; i < YodoCurve.MaxTier; i++) system.DebugForge(0);
                Assert.IsTrue(system.IsMaxed(0));

                long before = system.Shards;
                system.ReportBossDefeated(LanternConfig(), YodoCurve.FirstDropStage(0));

                Assert.AreEqual(0L, system.GetBlade(0).souls, "상한 요도에 혼이 쌓였다");
                Assert.AreEqual(before + YodoCurve.ShardsPerOverflowSoul, system.Shards,
                    "상한 뒤의 혼이 파편이 되지 않았다 - 죽은 드랍이다");
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /** 배수가 요도 없이 정확히 1이다 - 전투 전용 씬이 공격력을 잃지 않게 */
        [Test]
        public void MultiplierIsOneWithoutAnyBlade()
        {
            Assert.AreEqual(1d, YodoSystem.CurrentAttackMultiplier, 1e-12d);

            var system = BuildSystem();
            try
            {
                Assert.AreEqual(1d, system.AttackMultiplier, 1e-12d);
                Assert.AreEqual(0, system.SealedCount);
                Assert.IsFalse(system.IsOnikiriComplete);
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /** 세이브 복원이 티어를 자르지 않는다 (상한이 내려간 업데이트 대비) */
        [Test]
        public void Restore_DoesNotClampTiers()
        {
            var system = BuildSystem();
            try
            {
                var ids = new string[YodoCatalog.Count];
                var souls = new long[YodoCatalog.Count];
                var tiers = new int[YodoCatalog.Count];
                var discovered = new int[YodoCatalog.Count];

                for (int i = 0; i < YodoCatalog.Count; i++)
                {
                    ids[i] = YodoCatalog.Blades[i].Id;
                    tiers[i] = YodoCurve.MaxTier + 3;
                }

                system.Restore(ids, souls, tiers, discovered, 42L);

                Assert.AreEqual(YodoCurve.MaxTier + 3, system.GetBlade(0).tier,
                    "복원이 티어를 잘랐다 - 플레이어가 모은 바퀴가 사라진다");
                Assert.AreEqual(YodoCurve.TierValue(YodoCurve.MaxTier),
                                system.GetBlade(0).Multiplier, 1e-12d,
                    "값은 상한에서 잘려야 한다");
                Assert.AreEqual(42L, system.Shards);

                // 봉인한 적이 있으면 도감도 켜져 있어야 한다 (발견 플래그 없는
                // 마이그레이션 직후 세이브)
                Assert.IsTrue(system.GetBlade(0).discovered);
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }
    }
}
