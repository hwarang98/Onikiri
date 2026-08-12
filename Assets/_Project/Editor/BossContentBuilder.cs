using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 보스(다크 사무라이) 정의와 보스전 UI를 만들고 배선한다.
     *
     * 보스는 별도의 클래스가 아니라 Enemy 하나다. 하는 일이 잡몹과 완전히 같기
     * 때문이다 - 걸어와서 멈추고 맞고 죽는다. 다른 것은 아트와 스탯과 연출뿐이고,
     * 그중 스탯은 스폰 시점에 BossFight가 확정해서 넘긴다. 그래서 여기서 만드는
     * 것은 아트가 들어간 EnemyDefinition 하나와 화면 몇 장이다.
     */
    public static class BossContentBuilder
    {
        private const string BossSpriteFolder = "Assets/ThirdParty/Characters/Demon_Samurai/Sprites";
        private const string BossIdleSheet = BossSpriteFolder + "/IDLE.png";

        /** 이 팩은 걷기가 아니라 달리기다. 걸어 들어오는 자리에 그대로 쓴다 */
        private const string BossWalkSheet = BossSpriteFolder + "/RUN.png";

        private const string BossHurtSheet = BossSpriteFolder + "/HURT.png";
        private const string BossDeathSheet = BossSpriteFolder + "/DEATH.png";

        /**
         * @brief 보스 공격 클립.
         *
         * 팩에 이미 있다 - ATTACK 1 이 7프레임이다. 대체 연출을 만들 필요가 없었다.
         * ATTACK 2(5프레임)와 3(7프레임)도 있지만 1을 쓴다. 2는 너무 짧아
         * 2초 주기에서 예비 동작이 보이지 않고, 3은 점프가 섞여 제자리에 선
         * 보스의 동작으로 읽히지 않는다.
         */
        private const string BossAttackSheet = BossSpriteFolder + "/ATTACK 1.png";

        private const string DataFolder = "Assets/_Project/Data";
        private const string BossDefinitionPath = DataFolder + "/Enemy_DarkSamurai.asset";
        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        // Demon_Samurai 아트의 화면 이름. 12단계부터 "다크 사무라이"였는데 35단계
        // 후속에서 이름이 Inimig(9) 보스와 서로 바뀌었다 - BossConfigBuilder의
        // darkSamurai 씨앗 주석 참고. 이 상수는 로스터 없는 폴백 경로에만 쓰인다
        public const string BossDisplayName = "붉은눈 요괴";

        private static readonly Color DimColor = new Color32(0x0A, 0x08, 0x10, 0xE0);
        private static readonly Color PanelColor = new Color32(0x3A, 0x35, 0x50, 0xF0);
        private static readonly Color TextColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        private static readonly Color BossNameColor = new Color32(0xE8, 0x6A, 0x6A, 0xFF);
        private static readonly Color ButtonColor = new Color32(0x8C, 0x3A, 0x4E, 0xFF);
        private static readonly Color HealthColor = new Color32(0xC8, 0x3A, 0x46, 0xFF);
        private static readonly Color HealthBackColor = new Color32(0x1A, 0x16, 0x24, 0xD0);

        /**
         * @brief 확대판 보스의 틴트.
         *
         * 크기만 두 배로 키우면 "같은 놈이 커진 것"으로 읽힌다. 붉게 물들여
         * 우두머리라는 신호를 하나 더 얹는다. 완전히 다른 색으로 칠하지 않는
         * 이유는 그러면 어느 잡몹의 우두머리인지가 사라지기 때문이다.
         *
         * **밝은 값이어야 한다.** SpriteRenderer.color는 곱연산이라 어두운 틴트는
         * 스프라이트를 더 어둡게만 만든다. 처음에 #C87890을 썼다가 원래도 어두운
         * 요괴 아트가 검은 덩어리가 됐다. 빨강을 255로 두고 초록·파랑만 낮추면
         * 밝기를 유지한 채 색조만 붉은 쪽으로 민다.
         */
        private static readonly Color StageBossTint = new Color32(0xFF, 0xC0, 0xC8, 0xFF);

        /** 플레이어 체력 바 색. 보스 체력(붉은색)과 구분되는 초록 계열 */
        private static readonly Color PlayerHealthColor = new Color32(0x6E, 0xC8, 0x7A, 0xFF);

        private const string DataFolderForMobs = "Assets/_Project/Data";

        /**
         * @brief 일반 스테이지 보스로 쓸 잡몹 정의들. 씬에 굳는 것은 지역 1 풀이다.
         *
         * 36단계부터 잡몹이 지역별 풀로 갈라졌다. 씬에는 스포너와 같은 지역 1
         * 한 벌만 굳고, 지역이 넘어가면 RegionMobSwitcher가 런타임에 바꾼다 -
         * 스포너와 이 배열이 다른 풀이면 잡몹은 새 지역인데 확대판 보스만 옛
         * 지역 몹이 된다.
         */
        private static List<EnemyDefinition> LoadMobDefinitions()
        {
            var set = AssetDatabase.LoadAssetAtPath<RegionMobSet>(BattleContentBuilder.RegionMobSetPath(1));
            if (set != null && set.mobs != null && set.mobs.Length > 0)
                return new List<EnemyDefinition>(set.mobs);

            // 풀 애셋이 아직 없을 때(빌드 순서가 꼬인 옛 프로젝트)의 폴백.
            // 가중치 0(보스)만 빼고 전부 줍는 12단계까지의 방식이다
            var definitions = new List<EnemyDefinition>();

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { DataFolderForMobs }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.spawnWeight <= 0f) continue;
                definitions.Add(definition);
            }

            definitions.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return definitions;
        }

        // ---------------------------------------------------------------- 정의 에셋

        /**
         * @brief 보스 정의 에셋.
         *
         * 체력과 골드는 여기 적지 않는다. 잡몹 평균에 스테이지 배수와 보스 배수를
         * 곱한 값이라 스테이지마다 다르고, BossFight가 스폰 시점에 계산해서 넘긴다.
         * 에셋에 그럴듯한 숫자를 적어두면 언젠가 그 값이 실제 밸런스라고 오해받는다.
         */
        /**
         * @brief 배치 애셋을 깔고, 그 안의 시트형 보스를 전부 빌드한다.
         *
         * 13단계 이전에는 이 자리에서 다크 사무라이 하나를 하드코딩된 경로로
         * 만들었다. 이제 만들 대상은 로스터가 정한다 - 보스를 추가하려면
         * BossConfig 애셋을 하나 더 만들어 지역에 꽂으면 되고 여기는 그대로다.
         */
        public static BossRoster BuildBosses()
        {
            var roster = BossConfigBuilder.EnsureDefaultAssets();
            if (roster == null) return null;

            // 요괴 팩에서 뜯은 이펙트를 다시 굽는다. 보스 시트를 굽는 것과 같은
            // 자리다 - 둘 다 같은 aseprite에서 나오므로, 한쪽만 새로 구우면
            // 보스와 그 보스가 뿜는 참격의 붉은색이 어긋난다
            if (YokaiVfxBaker.BakeAll()) YokaiVfxBaker.BuildLibrary();

            int built = 0;
            foreach (var config in ConfigsIn(roster))
            {
                if (config.kind != BossConfig.ArtKind.Sheets) continue;

                // 발밑 여백을 매번 다시 잰다. 시트를 갈아 끼웠는데 옛 값이 남아
                // 있으면 보스가 공중에 뜨거나 땅에 박히고, 그것은 플레이해 봐야
                // 안다. 재는 비용이 그것보다 훨씬 싸다
                string report;
                int measured = BossConfigBuilder.MeasureFeetPadding(config, out report);
                if (measured >= 0 && measured != config.feetPadding)
                {
                    Debug.Log(string.Format("[Onikiri] {0} feet padding {1}px -> {2}px  [{3}]",
                        config.name, config.feetPadding, measured, report));
                    config.feetPadding = measured;
                    EditorUtility.SetDirty(config);
                }

                if (BossConfigBuilder.Build(config) != null) built++;
            }

            Debug.Log("[Onikiri] Boss roster built: " + built + " sheet boss(es).");
            return roster;
        }

        /** 로스터가 참조하는 모든 BossConfig. 중복 없이 */
        public static List<BossConfig> ConfigsIn(BossRoster roster)
        {
            var configs = new List<BossConfig>();
            if (roster == null || roster.regions == null) return configs;

            foreach (var region in roster.regions)
            {
                if (region == null) continue;
                AddUnique(configs, region.chapterBoss);
                AddUnique(configs, region.finaleBoss);
                AddUnique(configs, region.normalBossOverride);
            }
            return configs;
        }

        private static void AddUnique(List<BossConfig> into, BossConfig config)
        {
            if (config != null && !into.Contains(config)) into.Add(config);
        }

        /**
         * @brief 12단계까지 쓰던 하드코딩 경로. 폴백으로만 남는다.
         *
         * BossFight가 로스터 없이 돌 때를 위한 것이고, 새 보스를 추가할 때
         * 여기를 고칠 일은 없다.
         */
        public static EnemyDefinition BuildDefinition()
        {
            // 자동 슬라이싱이 만든 타이트 렉트를 그대로 두면 프레임마다 피벗이 달라
            // 보스가 제자리에서 떨린다. 격자로 다시 자른다
            CharacterSpriteSlicer.SliceBoss();

            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BossDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, BossDefinitionPath);
            }

            definition.displayName = BossDisplayName;
            definition.idleFrames = OrderedSprites(BossIdleSheet).ToArray();
            definition.walkFrames = OrderedSprites(BossWalkSheet).ToArray();
            definition.hurtFrames = OrderedSprites(BossHurtSheet).ToArray();
            definition.deathFrames = OrderedSprites(BossDeathSheet).ToArray();
            definition.attackFrames = OrderedSprites(BossAttackSheet).ToArray();
            definition.attackInterval = (float)BossCurve.AttackIntervalSeconds;
            definition.attackImpactPoint = 0.55f;

            // 잡몹보다 느린 12fps다. 보스는 26프레임짜리 사망 연출을 들고 있어서
            // 잡몹과 같은 속도로 돌리면 죽는 데 2초가 넘게 걸린다. 그 길이가
            // 보상 순간으로는 적당하다고 판단해 그대로 둔다
            definition.frameRate = 12f;

            // 잡몹 추첨에 절대 끼면 안 된다. 스포너의 definitions 배열에도 넣지
            // 않지만, 가중치 0을 함께 적어 의도를 에셋에서도 읽히게 한다
            definition.spawnWeight = 0f;

            // 스폰 시점에 덮어쓰인다. 0으로 두면 혹시 이 값으로 스폰되는 경로가
            // 생겼을 때 즉사하는 보스로 곧바로 드러난다
            definition.maxHealth = BigDouble.Zero;
            definition.goldReward = BigDouble.Zero;

            // 잡몹보다 느리게 걸어 들어온다. 무게가 속도로 읽힌다
            definition.moveSpeed = 0.85f;
            definition.queueSpacing = 2.0f;
            definition.hoverHeight = 0f;

            // 잡몹과 달리 0이다. 잡몹은 Aseprite 캔버스 피벗을 쓰기 때문에 아트까지의
            // 거리를 런타임에 빼줘야 하지만, 보스는 SliceBoss가 피벗을 아예 발밑
            // (아래에서 12px)에 찍는다. 여기서 또 빼면 공중에 뜬다
            definition.artBottomOffset = 0f;

            EditorUtility.SetDirty(definition);

            Debug.Log(string.Format(
                "[Onikiri] Boss '{0}': idle={1} hurt={2} death={3} attack={4} frames, " +
                "attack every {5}s.",
                definition.displayName, definition.idleFrames.Length,
                definition.hurtFrames.Length, definition.deathFrames.Length,
                definition.attackFrames.Length, definition.attackInterval));

            return definition;
        }

        // ---------------------------------------------------------------- 씬 배선

        /**
         * @brief 보스전 컴포넌트와 화면을 씬에 붙인다.
         *
         * BattleContentBuilder가 스포너·강화·세션을 배선한 뒤에 부른다.
         */
        public static BossFight Wire(EnemySpawner spawner)
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BossDefinitionPath);
            if (definition == null)
            {
                Debug.LogError("[Onikiri] Boss definition missing at " + BossDefinitionPath);
                return null;
            }

            var battle = GameObject.Find("Battle");
            if (battle == null) { Debug.LogError("[Onikiri] Battle root missing."); return null; }

            var fight = battle.GetComponent<BossFight>();
            if (fight == null) fight = battle.AddComponent<BossFight>();

            var panel = MainSceneBuilder.FindBand("GrowthPanel");

            var samurai = GameObject.Find("Samurai");

            var fightSo = new SerializedObject(fight);
            fightSo.FindProperty("spawner").objectReferenceValue = spawner;
            fightSo.FindProperty("progress").objectReferenceValue = battle.GetComponent<StageProgress>();
            fightSo.FindProperty("playerHealth").objectReferenceValue =
                samurai != null ? samurai.GetComponent<PlayerHealth>() : null;
            fightSo.FindProperty("bossDefinition").objectReferenceValue = definition;

            // 배치 애셋. 있으면 BossFight가 이쪽 말만 듣는다
            fightSo.FindProperty("roster").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BossRoster>(BossConfigBuilder.RosterPath);

            // 보스가 휘두를 때 앞에 뜨는 참격. 요괴 팩에서 뜯어낸 조각을 돌린다
            fightSo.FindProperty("attackVfx").objectReferenceValue = WireAttackVfx(battle);

            /**
             * @brief 공용 기본 참격은 **없다.** 빌더가 매번 비운다.
             *
             * C# 필드 기본값을 바꾸는 것으로는 부족하다 - 그 값은 컴포넌트를
             * 처음 붙일 때만 쓰이고, 이미 씬에 직렬화된 값은 그대로 남는다.
             * 실제로 기본값을 지운 뒤에도 씬에는 `crescent`가 남아 있어서
             * 다섯 보스가 계속 요괴의 참격을 뿜었다.
             *
             * 그래서 씬에 굳는 값을 여기서 못박는다. 참격은 BossConfig가
             * 보스마다 정하는 것이고(BossConfig.attackVfxId), 공용 자리에
             * 어느 요괴의 서명을 놓아서도 안 된다.
             */
            fightSo.FindProperty("defaultAttackVfxId").stringValue = string.Empty;

            // 토리이 관문. BattleStageBuilder가 지면 앵커 아래에 만들어두고
            // 숨겨둔 것을 찾아 연결한다
            fightSo.FindProperty("gate").objectReferenceValue =
                Object.FindFirstObjectByType<BossGate>(FindObjectsInactive.Include);
            fightSo.FindProperty("advance").objectReferenceValue = battle.GetComponent<StageAdvance>();

            // 일반 스테이지 보스로 쓸 잡몹들. 스테이지로 하나를 고른다
            var stageBosses = LoadMobDefinitions();
            var stageBossArray = fightSo.FindProperty("stageBossDefinitions");
            stageBossArray.arraySize = stageBosses.Count;
            for (int i = 0; i < stageBosses.Count; i++)
                stageBossArray.GetArrayElementAtIndex(i).objectReferenceValue = stageBosses[i];

            // 확대 배율은 **정수만** 쓴다.
            //
            // Pixel Perfect Camera가 아트 픽셀 하나를 화면 픽셀 N개로 늘린다
            // (1080 폭에서 N = 5). 스프라이트를 s배로 키우면 아트 픽셀 하나가
            // 화면에서 s x N 픽셀이 되는데, 이 값이 정수가 아니면 어떤 픽셀은
            // 7개, 어떤 픽셀은 8개로 그려져 격자가 눈에 띄게 일그러진다.
            //
            // 1.5배는 1080에서 7.5px이라 탈락이다. 1.2/1.4/1.6은 1080에서는
            // 정수가 되지만(6/7/8) 배율 N은 기기 해상도에 따라 달라지므로
            // 그 기기에서만 맞는 값이다. **어떤 N에서도 정수인 것은 정수 배율뿐이다.**
            fightSo.FindProperty("stageBossScale").floatValue = 2f;
            fightSo.FindProperty("stageBossTint").colorValue = StageBossTint;
            fightSo.FindProperty("upgrades").objectReferenceValue =
                panel != null ? panel.GetComponent<UpgradeSystem>() : null;
            // 전체 등장 연출은 챕터 보스 전용이다. 일반 스테이지 보스는 0.4초만
            // 스친다 - 매 스테이지 6초씩 반복되면 연출이 대기 시간이 된다
            fightSo.FindProperty("introSeconds").floatValue = 1f;
            fightSo.FindProperty("stageBossIntroSeconds").floatValue = 0.4f;
            fightSo.FindProperty("failSeconds").floatValue = 3f;
            fightSo.FindProperty("bossName").stringValue = BossDisplayName;
            fightSo.ApplyModifiedPropertiesWithoutUndo();

            BuildHud(fight);
            return fight;
        }

        /**
         * @brief 보스전 화면 네 장.
         *
         * 등장 연출만 안전 영역 루트에 두어 화면 전체를 덮는다. 나머지 셋은 전투
         * 밴드 안이다 - 시계와 도전 버튼은 전투를 가리면 안 되고, 실패 문구는
         * 뒤의 강화 버튼이 계속 눌려야 한다. 실패 직후에 해야 할 일이 정확히
         * 그 버튼을 누르는 것이기 때문이다.
         */
        private static void BuildHud(BossFight fight)
        {
            var safeArea = UpgradePanelBuilder.EnsureSafeArea();
            var band = MainSceneBuilder.FindBand("BattleArea");
            if (safeArea == null || band == null)
            {
                Debug.LogError("[Onikiri] Cannot build the boss HUD without SafeArea/BattleArea.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            Replace(safeArea, "BossIntro");
            Replace(band, "BossChallenge");
            Replace(band, "BossQuota");
            Replace(band, "BossFightHud");
            Replace(band, "BossResult");

            // --- 등장 연출: 화면 전체를 덮는 어두운 판 + 이름
            var intro = new GameObject("BossIntro", typeof(RectTransform));
            intro.transform.SetParent(safeArea, false);
            intro.transform.SetAsLastSibling();
            Stretch((RectTransform)intro.transform);
            // 판 자체가 레이캐스트를 먹어 연출 중 강화 버튼이 눌리는 것을 막는다
            intro.AddComponent<Image>().color = DimColor;

            var introLabel = CreateLabel(intro.transform, font, "Name", PixelFontSizesLarge, TextAlignmentOptions.Center);
            // 화면 정중앙이 아니라 전투 밴드의 한가운데다. 안전 영역 기준 중앙은 지면선
            // 바로 위라, 이름이 사무라이와 지면 풀에 겹쳐 읽힌다. 밴드는 45~90% 구간이고
            // 그 중심은 화면 중심에서 위로 22.5% - 1920 기준 432px이다
            CenterBox((RectTransform)introLabel.transform, new Vector2(1000f, 160f),
                      new Vector2(0f, DisplayConfig.DesignHeight * 0.225f));
            introLabel.color = BossNameColor;
            introLabel.text = BossDisplayName;

            // --- 도전 버튼: 전투 밴드 위쪽. 보스 체력 바와 같은 자리다.
            //
            // 처음에는 밴드 아래쪽에 뒀는데, 그 자리가 지면선이라 버튼이 사무라이의
            // 하반신과 풀을 덮었다. 보스가 열린 동안은 계속 떠 있는 버튼이라 그동안
            // 전투가 가려진다.
            //
            // 위쪽은 하늘이라 비어 있고, 무엇보다 이 슬롯은 보스 체력 바가 쓰는 자리와
            // 같다. 두 화면은 배타적이므로 겹칠 일이 없고, "보스에 관한 것은 여기 뜬다"가
            // 한 자리로 유지된다.
            var challenge = new GameObject("BossChallenge", typeof(RectTransform));
            challenge.transform.SetParent(band, false);
            var challengeRect = (RectTransform)challenge.transform;
            challengeRect.anchorMin = challengeRect.anchorMax = new Vector2(0.5f, 1f);
            challengeRect.pivot = new Vector2(0.5f, 1f);
            challengeRect.sizeDelta = new Vector2(440f, 124f);
            challengeRect.anchoredPosition = new Vector2(0f, -24f);

            var challengeImage = challenge.AddComponent<Image>();
            UiSkin.ApplyPanel(challengeImage, UiSkin.Panel, UiSkin.Danger);
            var challengeButton = challenge.AddComponent<Button>();
            UiSkin.ApplyButton(challengeButton, challengeImage);
            challengeButton.targetGraphic = challengeImage;

            var challengeLabel = CreateLabel(challenge.transform, font, "Label",
                                             PixelFontSizesSmall, TextAlignmentOptions.Center);
            Stretch((RectTransform)challengeLabel.transform);
            challengeLabel.text = "보스 도전";

            // 해골 글리프(38단계 아이콘화). "보스"라는 글자에 심볼이 얹혀야
            // 버튼이 위협으로 읽힌다 - 글자를 밀지 않게 왼쪽 여백에 세운다
            var skull = new GameObject("Skull", typeof(RectTransform));
            skull.transform.SetParent(challenge.transform, false);
            var skullRect = (RectTransform)skull.transform;
            skullRect.anchorMin = skullRect.anchorMax = new Vector2(0f, 0.5f);
            skullRect.pivot = new Vector2(0f, 0.5f);
            skullRect.sizeDelta = new Vector2(48f, 48f);
            skullRect.anchoredPosition = new Vector2(28f, 0f);
            var skullImage = skull.AddComponent<Image>();
            skullImage.sprite = UiGlyphBuilder.Load(UiGlyphBuilder.Skull);
            skullImage.color = new Color(1f, 0.92f, 0.92f, 1f);
            skullImage.raycastTarget = false;

            // --- 처치 할당량: 도전 버튼과 같은 자리, 배타로 뜬다 (37단계).
            //
            // 상단 바 재배치에서 스테이지 문구에 붙어 있던 카운터가 이리로 왔다.
            // 카운터가 차면 같은 자리가 도전 버튼으로 바뀌므로 "채우면 무슨 일이
            // 생기는가"가 한 자리에서 이어진다. 버튼보다 작고 눌리지 않는다
            var quota = new GameObject("BossQuota", typeof(RectTransform));
            quota.transform.SetParent(band, false);
            var quotaRect = (RectTransform)quota.transform;
            quotaRect.anchorMin = quotaRect.anchorMax = new Vector2(0.5f, 1f);
            quotaRect.pivot = new Vector2(0.5f, 1f);
            quotaRect.sizeDelta = new Vector2(320f, 76f);
            quotaRect.anchoredPosition = new Vector2(0f, -24f);

            var quotaImage = quota.AddComponent<Image>();
            UiSkin.ApplyPanel(quotaImage, UiSkin.Inlay, UiSkin.InlayTint);
            quotaImage.raycastTarget = false;

            var quotaLabel = CreateLabel(quota.transform, font, "Label",
                                         PixelFontSizesSmall, TextAlignmentOptions.Center);
            Stretch((RectTransform)quotaLabel.transform);
            // 카운터는 보조 정보다(38단계 위계) - 도전 버튼(44pt)보다 한 단 작다
            UiFonts.Demote(quotaLabel);
            quotaLabel.text = "처치 0/10";

            // --- 전투 HUD: 밴드 위쪽에 체력 바, 그 아래 시계
            var fightHud = new GameObject("BossFightHud", typeof(RectTransform));
            fightHud.transform.SetParent(band, false);
            Stretch((RectTransform)fightHud.transform);

            var healthBack = new GameObject("HealthBack", typeof(RectTransform));
            healthBack.transform.SetParent(fightHud.transform, false);
            var backRect = (RectTransform)healthBack.transform;
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(1f, 1f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.offsetMin = new Vector2(60f, -66f);
            backRect.offsetMax = new Vector2(-60f, -24f);
            UiSkin.ApplyPanel(healthBack.AddComponent<Image>(), UiSkin.Inlay, UiSkin.InlayTint);

            var healthFill = new GameObject("HealthFill", typeof(RectTransform));
            healthFill.transform.SetParent(healthBack.transform, false);
            Stretch((RectTransform)healthFill.transform);
            var fillImage = healthFill.AddComponent<Image>();
            fillImage.color = HealthColor;
            // **민짜 채움이다(38b 규칙).** Filled+내장 UISprite는 둥근 소프트
            // 가장자리가 바에서 그라데이션으로 보인다 - 게이지는 처음부터
            // 끝까지 균일하게 차야 한다(사용자 지적 두 번째). EXP 스트립
            // (LevelHud)과 같은 방식으로, sprite 없는 판을 BossHud가
            // anchorMax.x로 민다
            fillImage.sprite = null;
            fillImage.type = Image.Type.Simple;
            fillImage.raycastTarget = false;

            // --- 플레이어 체력 바: 전투 밴드 아래쪽. 보스 바와 화면 반대편에 둔다.
            //     둘이 붙어 있으면 어느 쪽이 내 체력인지 매번 확인해야 한다
            var playerBack = new GameObject("PlayerHealthBack", typeof(RectTransform));
            playerBack.transform.SetParent(fightHud.transform, false);
            var playerBackRect = (RectTransform)playerBack.transform;
            // 보스 바(밴드 위쪽)와 같은 하늘 영역에 두되 시계 아래로 한 칸 띄운다.
            //
            // 처음에는 밴드 아래쪽에 뒀는데, 그 자리가 지면선이라 체력 바와 숫자가
            // 사무라이의 발과 풀 위에 겹쳤다. 전투 중 계속 떠 있는 표시라 그동안
            // 싸움이 가려진다. 위쪽은 비어 있고, 무엇보다 두 체력 바를 같은 영역에
            // 두면 "누구 체력인가"를 색으로만 구분하면 된다 - 붉은색이 보스,
            // 초록색이 나다
            playerBackRect.anchorMin = new Vector2(0f, 1f);
            playerBackRect.anchorMax = new Vector2(1f, 1f);
            playerBackRect.pivot = new Vector2(0.5f, 1f);
            playerBackRect.offsetMin = new Vector2(60f, -196f);
            playerBackRect.offsetMax = new Vector2(-60f, -162f);
            UiSkin.ApplyPanel(playerBack.AddComponent<Image>(), UiSkin.Inlay, UiSkin.InlayTint);

            var playerFillGo = new GameObject("PlayerHealthFill", typeof(RectTransform));
            playerFillGo.transform.SetParent(playerBack.transform, false);
            Stretch((RectTransform)playerFillGo.transform);
            var playerFill = playerFillGo.AddComponent<Image>();
            playerFill.color = PlayerHealthColor;
            // 보스 바와 같은 민짜 채움(38b 규칙). 위 주석 참고
            playerFill.sprite = null;
            playerFill.type = Image.Type.Simple;
            playerFill.raycastTarget = false;

            var playerHealthLabel = CreateLabel(fightHud.transform, font, "PlayerHealthLabel",
                                                PixelFontSizesSmall, TextAlignmentOptions.Center);
            var playerLabelRect = (RectTransform)playerHealthLabel.transform;
            playerLabelRect.anchorMin = new Vector2(0.5f, 1f);
            playerLabelRect.anchorMax = new Vector2(0.5f, 1f);
            playerLabelRect.pivot = new Vector2(0.5f, 1f);
            playerLabelRect.sizeDelta = new Vector2(500f, 70f);
            playerLabelRect.anchoredPosition = new Vector2(0f, -200f);
            playerHealthLabel.text = "100 / 100";

            var timerLabel = CreateLabel(fightHud.transform, font, "Timer",
                                         PixelFontSizesSmall, TextAlignmentOptions.Center);
            var timerRect = (RectTransform)timerLabel.transform;
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.sizeDelta = new Vector2(400f, 80f);
            timerRect.anchoredPosition = new Vector2(0f, -76f);
            timerLabel.text = "30초";

            // --- 실패/결과 문구: 밴드 가운데. 레이캐스트를 먹지 않는다
            var result = new GameObject("BossResult", typeof(RectTransform));
            result.transform.SetParent(band, false);
            var resultRect = (RectTransform)result.transform;
            resultRect.anchorMin = resultRect.anchorMax = new Vector2(0.5f, 0.5f);
            resultRect.pivot = new Vector2(0.5f, 0.5f);
            resultRect.sizeDelta = new Vector2(960f, 300f);
            resultRect.anchoredPosition = Vector2.zero;

            var resultImage = result.AddComponent<Image>();
            UiSkin.ApplyPanel(resultImage, UiSkin.Row);
            resultImage.raycastTarget = false;

            var resultLabel = CreateLabel(result.transform, font, "Message",
                                          PixelFontSizesSmall, TextAlignmentOptions.Center);
            Stretch((RectTransform)resultLabel.transform);
            // 두 줄이 들어간다. 실패 문구는 "무슨 일이 있었는가"와 "무엇을 하면
            // 되는가"로 나뉘고, 한 줄로 붙이면 둘 다 흘려 읽힌다
            resultLabel.textWrappingMode = TextWrappingModes.Normal;
            resultLabel.text = "화력이 1.4배 모자란다.\n공격력 강화를 올리고 다시 도전하라";

            // --- 클리어 배너: 결과 문구와 같은 자리, 두 줄
            //
            // 위의 넷과 달리 이것만 Replace를 빼먹고 있었다. 빌더를 돌릴 때마다
            // 배너가 하나씩 더 쌓여 27개가 됐고, 컴포넌트는 마지막 것만 참조하므로
            // **화면상으로는 아무 이상이 없었다.** 이번 폰트 축소에서 드러났다 -
            // 새로 만든 것만 44/88이고 나머지 26개는 55/110에 멈춰 있었다
            Replace(band, "ClearBanner");

            var banner = new GameObject("ClearBanner", typeof(RectTransform));
            banner.transform.SetParent(band, false);
            var bannerRect = (RectTransform)banner.transform;
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.sizeDelta = new Vector2(960f, 300f);
            bannerRect.anchoredPosition = Vector2.zero;

            var bannerImage = banner.AddComponent<Image>();
            UiSkin.ApplyPanel(bannerImage, UiSkin.Row);
            bannerImage.raycastTarget = false;

            var bannerTitle = CreateLabel(banner.transform, font, "Title",
                                          PixelFontSizesLarge, TextAlignmentOptions.Center);
            var titleRect = (RectTransform)bannerTitle.transform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(20f, 0f);
            titleRect.offsetMax = new Vector2(-20f, -20f);
            bannerTitle.text = "클리어";

            var bannerDetail = CreateLabel(banner.transform, font, "Detail",
                                           PixelFontSizesSmall, TextAlignmentOptions.Center);
            var detailRect = (RectTransform)bannerDetail.transform;
            detailRect.anchorMin = new Vector2(0f, 0f);
            detailRect.anchorMax = new Vector2(1f, 0.5f);
            detailRect.offsetMin = new Vector2(20f, 20f);
            detailRect.offsetMax = new Vector2(-20f, 0f);
            bannerDetail.color = TextColor;
            bannerDetail.text = "지역 1 · 1/10   +0";

            var bannerComponent = band.GetComponent<Onikiri.UI.ClearBanner>();
            if (bannerComponent == null)
                bannerComponent = band.gameObject.AddComponent<Onikiri.UI.ClearBanner>();

            var bannerSo = new SerializedObject(bannerComponent);
            bannerSo.FindProperty("fight").objectReferenceValue = fight;
            bannerSo.FindProperty("root").objectReferenceValue = banner;
            bannerSo.FindProperty("titleLabel").objectReferenceValue = bannerTitle;
            bannerSo.FindProperty("detailLabel").objectReferenceValue = bannerDetail;
            bannerSo.ApplyModifiedPropertiesWithoutUndo();

            banner.SetActive(false);

            var hud = band.GetComponent<Onikiri.UI.BossHud>();
            if (hud == null) hud = band.gameObject.AddComponent<Onikiri.UI.BossHud>();

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("fight").objectReferenceValue = fight;
            hudSo.FindProperty("challengeRoot").objectReferenceValue = challenge;
            hudSo.FindProperty("challengeButton").objectReferenceValue = challengeButton;
            hudSo.FindProperty("challengeLabel").objectReferenceValue = challengeLabel;
            hudSo.FindProperty("quotaRoot").objectReferenceValue = quota;
            hudSo.FindProperty("quotaLabel").objectReferenceValue = quotaLabel;
            hudSo.FindProperty("progress").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.Progression.StageProgress>();
            hudSo.FindProperty("introRoot").objectReferenceValue = intro;
            hudSo.FindProperty("introLabel").objectReferenceValue = introLabel;
            hudSo.FindProperty("fightRoot").objectReferenceValue = fightHud;
            hudSo.FindProperty("timerLabel").objectReferenceValue = timerLabel;
            hudSo.FindProperty("healthFill").objectReferenceValue = fillImage;

            var samuraiForHud = GameObject.Find("Samurai");
            hudSo.FindProperty("playerHealth").objectReferenceValue =
                samuraiForHud != null ? samuraiForHud.GetComponent<PlayerHealth>() : null;
            hudSo.FindProperty("playerHealthFill").objectReferenceValue = playerFill;
            hudSo.FindProperty("playerHealthLabel").objectReferenceValue = playerHealthLabel;
            hudSo.FindProperty("resultRoot").objectReferenceValue = result;
            hudSo.FindProperty("resultLabel").objectReferenceValue = resultLabel;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // 넷 다 꺼진 채로 저장한다. 켜진 채로 저장되면 게임을 켜자마자 보스
            // 이름이 화면을 덮은 상태로 시작한다. OfflineRewardPopup에서 같은
            // 실수를 한 적이 있다
            intro.SetActive(false);
            challenge.SetActive(false);
            fightHud.SetActive(false);
            result.SetActive(false);
        }

        /**
         * @brief 보스 참격 재생기를 씬에 붙이고 라이브러리를 물린다.
         *
         * 사무라이의 오의(`SkillPerformer`)와 **같은 프리팹, 같은 VFX 루트**를
         * 쓴다. 참격을 재생하는 일은 양쪽이 똑같고, 다른 것은 어떤 클립을 어느
         * 자리에 놓느냐뿐이라 프리팹을 하나 더 만들 이유가 없다.
         *
         * 라이브러리가 아직 안 구워졌으면 여기서 굽는다. 배선만 하고 넘어가면
         * 참조가 빈 채로 씬에 굳고, 그 상태는 게임을 켜서 보스를 잡아 봐야 안다.
         */
        private static VfxBurst WireAttackVfx(GameObject battle)
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(YokaiVfxBaker.LibraryPath);
            if (library == null)
            {
                if (!YokaiVfxBaker.BakeAll()) return null;
                library = YokaiVfxBaker.BuildLibrary();
                if (library == null) return null;
            }

            var burst = battle.GetComponent<VfxBurst>();
            if (burst == null) burst = battle.AddComponent<VfxBurst>();

            var slashPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                SkillPanelBuilder.SlashPrefabPath);

            var so = new SerializedObject(burst);
            so.FindProperty("library").objectReferenceValue = library;
            so.FindProperty("slashPrefab").objectReferenceValue =
                slashPrefabRoot != null ? slashPrefabRoot.GetComponent<PackSlash>() : null;
            so.FindProperty("vfxParent").objectReferenceValue = battle.transform.Find("VFX");
            so.ApplyModifiedPropertiesWithoutUndo();

            return burst;
        }

        // ---------------------------------------------------------------- 도구

        private const float PixelFontSizesSmall = Onikiri.UI.PixelFontSizes.GalmuriSmall;
        private const float PixelFontSizesLarge = Onikiri.UI.PixelFontSizes.GalmuriLarge;

        /**
         * @brief 같은 이름의 자식을 **전부** 지운다.
         *
         * Find는 하나만 찾는다. 그래서 이미 중복이 쌓인 씬에서는 빌더를 돌려도 하나씩만
         * 줄어들고, 27개가 쌓여 있으면 27번 돌려야 깨끗해진다. 빌더는 몇 번을 돌리든
         * 같은 결과가 나와야 하므로 여기서 다 치운다.
         */
        private static void Replace(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name == name) Object.DestroyImmediate(child.gameObject);
            }
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, TMP_FontAsset font, string name,
                                                   float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            // 아틀라스를 구운 크기와 1:1. 다른 값을 쓰면 비트맵이 리샘플되어 흐려진다
            label.fontSize = size;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void CenterBox(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static List<Sprite> OrderedSprites(string sheetPath)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return sprites;
        }

        private static int IndexOf(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            int value;
            if (underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out value)) return value;
            return 0;
        }
    }
}
