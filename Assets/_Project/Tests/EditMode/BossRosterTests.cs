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
         * 다른 피날레를 세우는 쪽으로 확정되면서(랜턴 -> 처형인 -> 요괴
         * -> 다크사무라이) 첫 지역의 얼굴이 더 가벼운 쪽이 됐다.
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
         * @brief "붉은눈 요괴"(Demon_Samurai 아트)가 **지역 3의 피날레**로 서 있는지.
         *
         * ## 애셋 파일명과 화면 이름이 다르다
         *
         * Boss_DarkSamurai.asset은 12단계부터 "다크 사무라이"로 살았는데, 35단계
         * 후속에서 화면 이름이 Inimig(9) 보스와 서로 바뀌었다 - 갓 쓴 검은 도포의
         * Inimig(9) 쪽이 화면에서 "다크 사무라이"로 읽히고, 이쪽 아트(붉은 오니
         * 가면)는 "붉은눈 요괴"가 어울린다. 사용자가 화면을 보고 확정했다.
         * 파일명은 GUID 참조 때문에 그대로다.
         */
        [Test]
        public void Region3Finale_IsTheRedEyeYokai()
        {
            // 경로를 문자열로 적는다. 테스트 어셈블리는 에디터 빌더를 참조하지
            // 않으므로 BossConfigBuilder.DarkSamuraiPath를 가져올 수 없다
            var oni = UnityEditor.AssetDatabase.LoadAssetAtPath<BossConfig>(
                "Assets/_Project/Data/Bosses/Boss_DarkSamurai.asset");

            Assert.IsNotNull(oni, "Demon_Samurai 아트 config가 사라졌다 - 지역 3 피날레용이다");
            Assert.AreEqual("붉은눈 요괴", oni.displayName,
                "화면 이름이 다르다 - 이름 대응은 이 테스트의 주석 참고");
            Assert.AreEqual(BossConfig.ArtKind.Sheets, oni.kind);
            Assert.IsNotNull(oni.idleSheet, "아트 참조가 끊겼다");
            Assert.IsNotNull(oni.attackSheet, "공격 시트 참조가 끊겼다");

            var roster = LoadRoster();
            Assert.GreaterOrEqual(roster.regions.Length, 3);
            Assert.AreSame(oni, roster.regions[2].finaleBoss,
                "지역 3의 피날레가 붉은눈 요괴가 아니다");
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
            // 붙었다"로 잘못 보고된다 - 실제로 그렇게 실패했다.
            //
            // E-3 수정의 비용 상향 완화(CostRaiseRelief)도 같은 이유로 되돌린다 -
            // 나누는 항이라 곱해서 걷어낸다
            System.Func<int, double> tierOnly = stage =>
                StageCurve.BossHealthForStage(mobHealth, stage).ToDouble()
                * StageCurve.CostRaiseRelief(stage)
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
         * @brief 정의된 지역을 지나면 **처음부터 다시 도는가** (42단계 무한 구간).
         *
         * 41단계까지는 "마지막 지역 반복"이었다 - 지역이 하나뿐이던 시절의
         * 임시 처방이 네 지역이 다 들어온 뒤에도 남아, 무한 구간을 요괴 소굴
         * 한 판에 가두고 지역 전환 연출을 st40에서 정지시켰다.
         *
         * 이제 한 바퀴(40스테이지)를 접어 순환한다. st41은 지역 1(봄숲)의
         * 첫 스테이지 배치와 같고, 두 바퀴째의 피날레들도 첫 바퀴와 같은
         * 순서로 돈다. 지역 수를 상수로 적지 않고 배열에서 합산하는 이유는
         * 예전 그대로다 - 지역이 늘 때마다 테스트를 고쳐야 하면 곧 지워진다.
         */
        [Test]
        public void PastTheLastRegion_TheWholeWorldCycles()
        {
            var roster = LoadRoster();
            var first = roster.regions[0];
            var last = roster.regions[roster.regions.Length - 1];

            int totalStages = 0;
            foreach (var region in roster.regions) totalStages += region.stageCount;

            // 두 바퀴째 첫 지역: 피날레와 챕터가 지역 1의 배치 그대로
            Assert.AreSame(first.finaleBoss, roster.BossForStage(totalStages + first.stageCount),
                "정의된 지역을 지나면 지역 1부터 다시 돌아야 한다 - 피날레가 다르다");
            Assert.AreSame(first.chapterBoss,
                roster.BossForStage(totalStages + first.chapterEvery));

            // 두 바퀴째 마지막 지역: 한 바퀴 끝의 피날레(다크 사무라이)도 그대로
            Assert.AreSame(last.finaleBoss, roster.BossForStage(totalStages * 2),
                "두 바퀴째의 끝 피날레가 첫 바퀴와 다르다");

            // 깊은 바퀴에서도 같은 자리에는 같은 지역이 선다 (st241 = 일곱 바퀴째의 st1)
            int stageInRegion;
            Assert.AreSame(first, roster.RegionForStage(totalStages * 6 + 1, out stageInRegion));
            Assert.AreEqual(1, stageInRegion);
        }

        /**
         * @brief "다크 사무라이"(Inimig 9 아트, 림 라이트 구움)가 **마지막 지역의
         * 피날레**로 서 있는가.
         *
         * 라인업의 끝이다(랜턴 -> 처형인 -> 붉은눈 요괴 -> **다크사무라이**).
         * 무한 구간은 세계를 순환하므로(42단계) 이 보스가 매 바퀴의 끝
         * (st40, 80, 120...)을 지킨다 - 여기가 빠지면 바퀴의 매듭이 사라진다.
         * 애셋 파일명(Boss_RedEyeYokai)과 화면 이름의 대응은
         * Region3Finale_IsTheRedEyeYokai 주석 참고.
         */
        [Test]
        public void LastRegionFinale_IsTheDarkSamurai()
        {
            var roster = LoadRoster();
            Assert.GreaterOrEqual(roster.regions.Length, 4,
                "지역이 4개 미만이다 - 요괴 소굴(지역 4)이 로스터에 없다");

            var shadow = AssetDatabase.LoadAssetAtPath<BossConfig>(
                "Assets/_Project/Data/Bosses/Boss_RedEyeYokai.asset");
            Assert.IsNotNull(shadow, "Inimig(9) 보스 config가 없다");
            Assert.AreEqual("다크 사무라이", shadow.displayName,
                "화면 이름이 다르다 - 갓 쓴 검은 도포 아트가 다크 사무라이로 읽힌다(사용자 확정)");

            var last = roster.regions[roster.regions.Length - 1];
            Assert.AreSame(shadow, last.finaleBoss, "마지막 지역의 피날레가 다크 사무라이가 아니다");

            Assert.AreEqual(BossConfig.ArtKind.Sheets, shadow.kind);
            Assert.IsTrue(shadow.fullIntro, "피날레인데 전체 연출이 꺼져 있다");
            Assert.IsNotNull(shadow.idleSheet, "구운 시트 참조가 끊겼다");
            Assert.IsNotNull(shadow.deathSheet, "사망 시트 참조가 끊겼다");
        }

        /**
         * @brief 요괴 시트에 림 라이트가 **실제로 구워져 있는가.**
         *
         * 베이커가 돌지 않아도(림 코드가 죽어도) 시트는 존재할 수 있다 - 굽기
         * 없이 복사만 된 시트와 화면에서 구분하려면 st40까지 가야 한다. 그래서
         * 픽셀을 직접 센다.
         *
         * 림 색(#FF9A7A 쪽으로 78% 블렌드)은 원본 팔레트가 만들 수 없는 영역이다:
         * 원본의 밝은 색은 채도 높은 적(G가 낮다)이고 몸통은 어두운 자회색(R가
         * 낮다)이라, "R도 G도 함께 높은" 픽셀은 림뿐이다.
         */
        [Test]
        public void YokaiSheets_CarryTheBakedRimLight()
        {
            string path = "Assets/_Project/Art/Bosses/RedEyeYokai/IDLE.png";
            Assert.IsTrue(System.IO.File.Exists(path), "구운 IDLE 시트가 없다: " + path);

            // 임포터 설정과 무관하게 파일을 직접 읽는다. 아틀라스가 읽기 금지라도
            // PNG 바이트는 언제나 읽을 수 있다
            var texture = new UnityEngine.Texture2D(2, 2);
            UnityEngine.ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(path));

            int rim = 0;
            var pixels = texture.GetPixels32();
            foreach (var pixel in pixels)
            {
                if (pixel.a < 128) continue;
                if (pixel.r > 190 && pixel.g > 105 && pixel.b > 80) rim++;
            }
            UnityEngine.Object.DestroyImmediate(texture);

            Assert.Greater(rim, 100,
                "림 색 픽셀이 없다 - 시트가 림 라이트 없이 구워졌다 (배경에 실루엣이 묻힌다)");
        }

        /**
         * @brief 지역 4가 **자기 배경**을 갖고 있는가.
         *
         * 배경이 비면 스위처가 지역 3의 자줏빛 밤을 그대로 쓴다 - 게임은 돌지만
         * "요괴 소굴에 왔다"가 보스 하나로만 남는다. 지역 3까지의 톤 아크가
         * 마지막 계단에서 끊기는 것이므로 애셋 존재부터 검사한다.
         */
        [Test]
        public void Region4_HasItsOwnBackgroundSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<RegionBackgroundSet>(
                "Assets/_Project/Data/Backgrounds/Background_Region4.asset");
            Assert.IsNotNull(set, "지역 4 배경 세트가 없다");
            Assert.Greater(set.layers.Length, 0, "레이어가 비었다");

            var region3Set = AssetDatabase.LoadAssetAtPath<RegionBackgroundSet>(
                "Assets/_Project/Data/Backgrounds/Background_Region3.asset");
            Assert.AreNotSame(region3Set, set, "지역 4가 지역 3 배경을 그대로 쓴다");

            // 21단계 배경 규칙 두 가지가 새 세트에도 서 있는지
            bool hasSkyFill = false, hasRooted = false;
            foreach (var layer in set.layers)
            {
                if (layer.isSkyFill) hasSkyFill = true;
                if (layer.sitsOnGround) hasRooted = true;
            }
            Assert.IsTrue(hasSkyFill, "하늘 채움이 없다 - 세로 커버(B-0)가 뚫린다");
            Assert.IsTrue(hasRooted, "땅에 서는 레이어가 없다 - 나무가 공중에 뜬다");

            var roster = LoadRoster();
            var last = roster.regions[roster.regions.Length - 1];
            Assert.AreSame(set, last.background, "지역 4 config에 배경이 물리지 않았다");
        }

        /**
         * @brief 지역 1 하늘이 줄무늬 없는 구운 사본을 쓰는가 (2b 후속).
         *
         * 원본 layer_1은 49~51행에 미아 줄무늬(위쪽 하늘의 짙은 파랑 세 줄)가
         * 있어 화면에서 전폭 가로선으로 읽혔다. SpringForestBuilder.BuildSkySheet가
         * 지운 사본을 굽고 배경 세트가 그쪽을 무는데, 그 배선은 애셋에만 있어
         * 코드만 봐서는 깨져도 모른다 - 원본으로 되돌아가면 줄이 되살아난다.
         *
         * 검사는 디스크의 PNG 바이트로 한다. 임포터 설정(isReadable)을 건드리지
         * 않고 읽을 수 있는 유일한 길이다.
         */
        [Test]
        public void Region1_SkyIsTheBakedSheetWithoutTheStripe()
        {
            var set = AssetDatabase.LoadAssetAtPath<RegionBackgroundSet>(
                "Assets/_Project/Data/Backgrounds/Background_Region1.asset");
            Assert.IsNotNull(set, "지역 1 배경 세트가 없다");

            RegionBackgroundSet.Layer sky = null;
            foreach (var layer in set.layers)
                if (layer != null && layer.name == "Sky") sky = layer;

            Assert.IsNotNull(sky, "지역 1에 Sky 레이어가 없다");
            Assert.IsNotNull(sky.sprite, "Sky 레이어의 스프라이트가 비었다");

            const string bakedPath = "Assets/_Project/Art/Backgrounds/SpringSky.png";
            Assert.AreEqual(bakedPath, AssetDatabase.GetAssetPath(sky.sprite),
                "지역 1 하늘이 구운 사본이 아니다 - 원본 layer_1의 줄무늬가 화면에 되살아난다");

            var bytes = System.IO.File.ReadAllBytes(bakedPath);
            var texture = new UnityEngine.Texture2D(2, 2);
            Assert.IsTrue(UnityEngine.ImageConversion.LoadImage(texture, bytes),
                "구운 하늘 PNG를 읽지 못했다");

            try
            {
                var stray = new UnityEngine.Color32(123, 219, 255, 255);
                int found = 0;
                for (int rowFromTop = 49; rowFromTop <= 51; rowFromTop++)
                {
                    int y = texture.height - 1 - rowFromTop;
                    for (int x = 0; x < texture.width; x++)
                    {
                        UnityEngine.Color32 p = texture.GetPixel(x, y);
                        if (p.r == stray.r && p.g == stray.g && p.b == stray.b) found++;
                    }
                }
                Assert.AreEqual(0, found,
                    "구운 하늘 49~51행에 줄무늬 파랑이 " + found + "픽셀 남아 있다 - "
                    + "Build Spring Forest Pieces를 다시 돌려야 한다");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /**
         * @brief 지역 1 하늘 채움이 렌더 실측값인가 (2b 후속).
         *
         * 채움 색은 계산이 아니라 화면 실측에서 나온다(25단계 규칙). 옛 값
         * (#92CCD7)은 여명 안개 재배치 이후의 화면과 어긋나 9:21에서 단차
         * 0.145의 가로선을 그었다. 새 값은 아트 첫 줄의 렌더색을 채움 시트
         * 그라디언트로 나눈 것이고, 애셋에만 사는 값이라 여기서 지킨다.
         */
        [Test]
        public void Region1_SkyFillTintIsTheMeasuredValue()
        {
            var set = AssetDatabase.LoadAssetAtPath<RegionBackgroundSet>(
                "Assets/_Project/Data/Backgrounds/Background_Region1.asset");
            Assert.IsNotNull(set, "지역 1 배경 세트가 없다");

            RegionBackgroundSet.Layer fill = null;
            foreach (var layer in set.layers)
                if (layer != null && layer.isSkyFill) fill = layer;

            Assert.IsNotNull(fill, "지역 1에 하늘 채움 레이어가 없다");
            Assert.AreEqual((UnityEngine.Color32)new UnityEngine.Color32(0x6B, 0xBD, 0xD8, 0xFF),
                (UnityEngine.Color32)fill.tint,
                "지역 1 채움 틴트가 실측값(#6BBDD8)이 아니다 - 하늘 끝선에 가로 이음매가 선다");
        }

        /**
         * @brief 지역마다 피날레가 다른가.
         *
         * 라인업의 요점이다(랜턴 -> 처형인 -> 요괴 -> 다크사무라이). 배경만
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
