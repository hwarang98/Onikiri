using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 배치 애셋과 곡선 상수가 같은 것을 말하는지.
     *
     * 13단계에서 보스 배치가 코드에서 애셋으로 나갔다. 그 순간부터 **같은 사실이
     * 두 곳에 적히게 된다** - 지역 길이는 `Region_1.asset`에도 있고
     * `BossCurve.RegionLength`에도 있다. 시뮬레이션이 순수 함수라 애셋을 읽을 수
     * 없어서 생긴 사본이고, 사본은 언젠가 갈린다.
     *
     * 11단계의 ChapterHealthMultiplier가 정확히 이 종류였다. 상수는 있는데 값
     * 흐름에 연결되지 않아, 바꿔도 게임이 달라지지 않았다. 여기서는 반대 방향의
     * 같은 위험이다 - 애셋을 바꿨는데 시뮬레이션이 옛 배치로 밸런스를 잰다.
     */
    public class BossRosterTests
    {
        const string RosterPath = "Assets/_Project/Data/Bosses/BossRoster.asset";

        static BossRoster LoadRoster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<BossRoster>(RosterPath);
            Assert.IsNotNull(roster, "보스 로스터가 없다: " + RosterPath);
            return roster;
        }

        [Test]
        public void Roster_MatchesTheCurveConstants()
        {
            var roster = LoadRoster();
            Assert.IsNotNull(roster.regions);
            Assert.Greater(roster.regions.Length, 0, "지역이 하나도 없다");

            var region = roster.regions[0];
            Assert.IsNotNull(region, "지역 1 슬롯이 비어 있다");

            Assert.AreEqual(BossCurve.RegionLength, region.stageCount,
                "지역 길이가 곡선 상수와 다르다 - 시뮬레이션이 다른 스테이지를 피날레로 본다");
            Assert.AreEqual(BossCurve.ChapterEvery, region.chapterEvery,
                "챕터 주기가 곡선 상수와 다르다");
        }

        [Test]
        public void Region1_HasBothBossSlotsFilled()
        {
            var region = LoadRoster().regions[0];

            Assert.IsNotNull(region.chapterBoss, "챕터 보스 슬롯이 비었다");
            Assert.IsNotNull(region.finaleBoss, "피날레 슬롯이 비었다");
            Assert.AreNotSame(region.chapterBoss, region.finaleBoss,
                "챕터와 피날레가 같은 보스다 - 지역에 같은 놈이 두 번 나오면 피날레의 무게가 사라진다");
        }

        /**
         * @brief 피날레 보스는 **피날레에만** 나온다.
         *
         * 12단계까지는 같은 보스가 5의 배수마다 나왔고, 그래서 10스테이지의 등장이
         * 5스테이지와 다를 이유가 없었다. 이 테스트가 그 배치로 되돌아가는 것을 막는다.
         *
         * **아트 종류는 검사하지 않는다.** 21단계에 지역 1 피날레가 다크 사무라이
         * (전용 시트)에서 외눈 등롱(잡몹 확대)으로 바뀌었다 - 라인업이 지역마다
         * 다른 피날레를 세우는 쪽으로 확정되면서(랜턴 -> 처형인 -> 다크사무라이
         * -> 요괴) 첫 지역의 얼굴이 더 가벼운 쪽이 됐다.
         *
         * 피날레를 특별하게 만드는 것은 아트 종류가 아니라 **전용 배치와 전체
         * 연출**이다. 그 둘을 검사한다.
         */
        [Test]
        public void FinaleBoss_AppearsOnlyAtTheRegionFinale()
        {
            var roster = LoadRoster();
            var region = roster.regions[0];
            var finale = region.finaleBoss;

            Assert.IsTrue(finale.fullIntro,
                "피날레가 전체 연출을 안 쓴다 - 챕터 관문과 등장이 같아진다");

            // **첫 지역 안에서만** 본다. 21단계에 지역이 둘이 되면서 지역 2의
            // 피날레는 다른 보스이고, 그것이 라인업의 설계다 - 여기서 두 지역을
            // 한꺼번에 훑으면 "지역 2 피날레가 지역 1 피날레가 아니다"로 실패한다
            for (int stage = 1; stage <= region.stageCount; stage++)
            {
                var boss = roster.BossForStage(stage);
                bool isFinaleStage = stage == region.stageCount;

                if (isFinaleStage)
                    Assert.AreSame(finale, boss, "stage " + stage + "은 피날레인데 피날레 보스가 아니다");
                else
                    Assert.AreNotSame(finale, boss,
                        "stage " + stage + "에 피날레 보스가 나온다 - 피날레 전용이어야 한다");
            }
        }

        /**
         * @brief 다크 사무라이 애셋이 **보존**돼 있는지.
         *
         * 21단계에 지역 1에서 빠졌지만 지역 3 피날레로 예정돼 있다. 배치에서
         * 빠졌다고 애셋을 지우면 그때 아트·측정값을 다시 만들어야 하고, 그것은
         * 12~13단계에 한 작업이다.
         *
         * "지금 아무도 안 쓰니까 지워도 되겠지"를 막는 것이 이 테스트의 전부다.
         */
        [Test]
        public void DarkSamurai_IsPreservedForALaterRegion()
        {
            // 경로를 문자열로 적는다. 테스트 어셈블리는 에디터 빌더를 참조하지
            // 않으므로 BossConfigBuilder.DarkSamuraiPath를 가져올 수 없다
            var samurai = UnityEditor.AssetDatabase.LoadAssetAtPath<BossConfig>(
                "Assets/_Project/Data/Bosses/Boss_DarkSamurai.asset");

            Assert.IsNotNull(samurai, "다크 사무라이 config가 사라졌다 - 지역 3 피날레용이다");
            Assert.AreEqual(BossConfig.ArtKind.Sheets, samurai.kind);
            Assert.IsNotNull(samurai.idleSheet, "아트 참조가 끊겼다");
            Assert.IsNotNull(samurai.attackSheet, "공격 시트 참조가 끊겼다");
        }

        /**
         * @brief 시트형 보스의 **걷기·공격 아트가 실제로 물려 있는지.**
         *
         * 두 칸 다 비어 있어도 게임은 돈다 - 걷기가 없으면 idle로 미끄러지고,
         * 공격이 없으면 아무 동작 없이 피해만 들어간다. 조용히 나빠지는 종류라
         * 여기서 잡는다.
         *
         * 처형인이 정확히 그랬다. `walkSheet`이라는 칸 자체가 나중에 생겨서,
         * 이미 만들어진 config에는 영원히 비어 있었다 - 씨앗은 애셋을 만들 때만
         * 돌기 때문이다.
         */
        [Test]
        public void SheetBosses_HaveWalkAndAttackArt()
        {
            var roster = LoadRoster();

            foreach (var config in Configs(roster))
            {
                if (config.kind != BossConfig.ArtKind.Sheets) continue;

                Assert.IsNotNull(config.walkSheet,
                    config.name + ": 걷기 시트가 비었다 - 선 자세로 미끄러져 들어온다");
                Assert.IsNotNull(config.attackSheet,
                    config.name + ": 공격 시트가 비었다");

                var definition = config.generatedDefinition;
                Assert.IsNotNull(definition, config.name + ": 생성된 정의가 없다");
                Assert.IsNotEmpty(definition.walkFrames,
                    config.name + ": 시트는 물렸는데 걷기 프레임이 잘리지 않았다");
                Assert.IsNotEmpty(definition.attackFrames,
                    config.name + ": 공격 프레임이 잘리지 않았다");
            }
        }

        /** 로스터가 참조하는 모든 보스 config. 중복 없이 */
        static System.Collections.Generic.List<BossConfig> Configs(BossRoster roster)
        {
            var configs = new System.Collections.Generic.List<BossConfig>();
            foreach (var region in roster.regions)
            {
                foreach (var config in new[] { region.chapterBoss, region.finaleBoss,
                                               region.normalBossOverride })
                    if (config != null && !configs.Contains(config)) configs.Add(config);
            }
            return configs;
        }

        [Test]
        public void ChapterStages_GetTheEliteMob()
        {
            var roster = LoadRoster();
            var region = roster.regions[0];

            // 5스테이지는 챕터 관문. 확대형이어야 한다 - 전용 아트를 여기 쓰면
            // 피날레와 구분되지 않는다
            var boss = roster.BossForStage(BossCurve.ChapterEvery);
            Assert.AreSame(region.chapterBoss, boss);
            Assert.AreEqual(BossConfig.ArtKind.ScaledMob, boss.kind);
            Assert.IsFalse(boss.fullIntro, "챕터 관문이 전체 연출을 쓰면 피날레와 같아 보인다");
        }

        [Test]
        public void NormalStages_HaveNoConfiguredBoss()
        {
            var roster = LoadRoster();

            // null이 "그 스테이지 잡몹의 확대판"이라는 뜻이다. 일반 보스가
            // 우두머리로 읽히려면 방금까지 베던 잡몹이어야 한다
            foreach (int stage in new[] { 1, 2, 3, 4, 6, 7, 8, 9, 11 })
                Assert.IsNull(roster.BossForStage(stage),
                    "stage " + stage + "에 보스가 지정돼 있다 - 일반 스테이지는 잡몹 확대판이다");
        }

        [Test]
        public void ScaledMobBoss_UsesAnIntegerScaleAndBrightTint()
        {
            var boss = LoadRoster().regions[0].chapterBoss;

            // 정수 배율만. Pixel Perfect가 아트 픽셀을 화면 픽셀 N개로 늘리는데,
            // 배율이 정수가 아니면 격자가 일그러진다 (11단계 규칙)
            Assert.GreaterOrEqual(boss.scale, 1, "확대 배율은 1 이상의 정수여야 한다");

            // 곱연산이라 어두운 틴트는 요괴를 검은 덩어리로 만든다
            float brightness = UnityEngine.Mathf.Max(boss.tint.r,
                UnityEngine.Mathf.Max(boss.tint.g, boss.tint.b));
            Assert.GreaterOrEqual(brightness, 0.75f, "틴트가 어두워 아트가 뭉개진다");
        }

        // ------------------------------------------------------------ 등급 배수

        [Test]
        public void Tiers_AreOrderedByWeight()
        {
            // 피날레가 가장 무겁고 일반이 가장 가볍다. 순서가 뒤집히면 지역의
            // 마지막이 그 앞의 관문보다 쉬워진다
            Assert.Greater(BossCurve.FinaleHealthMultiplier, BossCurve.ChapterHealthMultiplier);
            Assert.Greater(BossCurve.ChapterHealthMultiplier, 1d);

            Assert.Greater(BossCurve.FinaleAttackMultiplier, BossCurve.ChapterAttackMultiplier);

            /*
             * 보상은 골드가 아니라 **경험치** 쪽이 크다.
             *
             * 16단계에서 갈라놨다. 피날레가 골드를 크게 주면 그 골드가 즉시
             * 화력으로 바뀌어 다음 스테이지 보스 여유가 천장을 뚫는다 - 실제로
             * st11이 3.00까지 올라갔다. 경험치는 레벨을 거쳐 들어오고 증폭은
             * 포인트당 상한이 있어 그렇게 튀지 않는다.
             *
             * 그래서 "피날레가 더 준다"는 유지하되 주는 재화를 옮겼다.
             */
            Assert.GreaterOrEqual(BossCurve.FinaleGoldMultiplier, BossCurve.ChapterGoldMultiplier,
                "피날레가 챕터보다 골드를 적게 주면 도전할 이유가 줄어든다");
            Assert.Greater(ExpCurve.FinaleExpMultiplier, ExpCurve.ChapterExpMultiplier,
                "피날레의 추가 보상이 경험치 쪽에 없다 - 골드를 낮춘 만큼 여기가 커야 한다");
        }

        /**
         * @brief 등급 배수가 선언만 되어 있지 않고 값 흐름 끝단에 도달하는가.
         *
         * 11단계에서 ChapterHealthMultiplier가 선언되고 "1보다 큰가" 테스트까지
         * 있었는데 아무 데도 연결되지 않았다. 상수의 존재는 연결의 증거가 아니다.
         */
        [Test]
        public void TierMultipliers_ReachTheEndOfTheValueFlow()
        {
            var mobHealth = Onikiri.Core.BigDouble.FromDouble(100d);
            var mobGold = Onikiri.Core.BigDouble.FromDouble(10d);

            // 9(일반) / 5(챕터) / 10(피날레) 를 같은 곡선 위에서 비교한다.
            // 스테이지가 다르면 기본 배수도 다르므로, 등급 배수만 떼어내기 위해
            // 등급 없는 값과의 비율을 본다.
            //
            // 20단계의 골드축 보정도 함께 나눈다. 그것은 등급이 아니라 플레이어의
            // 골드 성장을 따라가는 항이라, 여기 섞이면 "일반 보스에 등급 배수가
            // 붙었다"로 잘못 보고된다 - 실제로 그렇게 실패했다
            System.Func<int, double> tierOnly = stage =>
                StageCurve.BossHealthForStage(mobHealth, stage).ToDouble()
                / (StageCurve.BossHealth(mobHealth * StageCurve.HealthMultiplier(stage), stage).ToDouble()
                   * StageCurve.GoldAxisCompensation(stage));

            double normal9 = tierOnly(9);
            double chapter5 = tierOnly(5);
            double finale10 = tierOnly(10);

            Assert.AreEqual(1d, normal9, 1e-6, "일반 보스에 등급 배수가 붙었다");
            Assert.AreEqual(BossCurve.ChapterHealthMultiplier, chapter5, 1e-6,
                "챕터 배수가 체력에 도달하지 않는다");
            Assert.AreEqual(BossCurve.FinaleHealthMultiplier, finale10, 1e-6,
                "피날레 배수가 체력에 도달하지 않는다");

            // 골드도 같은 방식으로
            double goldFinale = StageCurve.BossGoldForStage(mobGold, 10).ToDouble()
                                / StageCurve.BossGold(mobGold * StageCurve.GoldMultiplier(10)).ToDouble();
            Assert.AreEqual(BossCurve.FinaleGoldMultiplier, goldFinale, 1e-6,
                "피날레 골드 배수가 도달하지 않는다");

            // 공격력
            double plainAttack = BossCurve.BaseAttackDamage
                                 * System.Math.Pow(BossCurve.AttackGrowth, 9);
            Assert.AreEqual(BossCurve.FinaleAttackMultiplier,
                BossCurve.AttackDamageForStage(10) / plainAttack, 1e-6,
                "피날레 공격력 배수가 도달하지 않는다");

            // 경험치
            Assert.AreEqual(ExpCurve.FinaleExpMultiplier, ExpCurve.MultiplierFor(10), 1e-9);
            Assert.AreEqual(ExpCurve.ChapterExpMultiplier, ExpCurve.MultiplierFor(5), 1e-9);
            Assert.AreEqual(1d, ExpCurve.MultiplierFor(9), 1e-9);
        }

        [Test]
        public void FullIntro_IsFinaleOnly()
        {
            Assert.IsTrue(BossCurve.UsesFullIntro(BossCurve.RegionLength));
            Assert.IsFalse(BossCurve.UsesFullIntro(BossCurve.ChapterEvery),
                "챕터 관문이 전체 연출을 쓰면 지역 하나에 같은 암전이 두 번 나온다");
            Assert.IsFalse(BossCurve.UsesFullIntro(1));
        }

        /**
         * @brief 정의된 지역을 지나도 진행이 멈추지 않는가.
         *
         * 지역 3·4는 아직 없다. 마지막 지역을 지난 뒤에 보스가 아예 없으면 게임이
         * 거기서 멈추므로 **마지막 지역의 배치를 반복한다.**
         *
         * 21단계에 지역이 둘이 되면서 반복하는 대상도 지역 2로 옮겨갔다. 지역
         * 인덱스를 상수로 적지 않고 배열 끝에서 가져오는 이유가 그것이다 -
         * 지역이 늘어날 때마다 이 테스트를 고쳐야 하면 곧 지워진다.
         */
        [Test]
        public void PastTheLastRegion_ThePatternRepeats()
        {
            var roster = LoadRoster();
            var last = roster.regions[roster.regions.Length - 1];

            // 정의된 지역 전체를 지난 뒤의 첫 피날레
            int totalStages = 0;
            foreach (var region in roster.regions) totalStages += region.stageCount;

            Assert.AreSame(last.finaleBoss, roster.BossForStage(totalStages + last.stageCount),
                "정의된 지역을 지나면 피날레가 사라진다 - 진행이 멈춘다");
            Assert.AreSame(last.chapterBoss,
                roster.BossForStage(totalStages + last.chapterEvery));
        }

        /**
         * @brief 지역마다 피날레가 다른가.
         *
         * 라인업의 요점이다(랜턴 -> 처형인 -> 다크사무라이 -> 요괴). 배경만
         * 바뀌고 보스가 같으면 "같은 곳을 다시 도는" 인상이 남는다 - 12단계에
         * 다크 사무라이가 5의 배수마다 나와서 챕터가 특별하지 않았던 것과
         * 같은 종류의 문제다.
         */
        [Test]
        public void EachRegion_HasItsOwnFinale()
        {
            var roster = LoadRoster();
            if (roster.regions.Length < 2) Assert.Ignore("지역이 아직 하나뿐이다");

            for (int i = 0; i < roster.regions.Length; i++)
                for (int j = i + 1; j < roster.regions.Length; j++)
                    Assert.AreNotSame(roster.regions[i].finaleBoss, roster.regions[j].finaleBoss,
                        string.Format("{0}과 {1}의 피날레가 같다",
                            roster.regions[i].displayName, roster.regions[j].displayName));
        }
    }
}
