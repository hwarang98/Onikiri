using System.Collections.Generic;
using System.Linq;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief BossConfig 애셋을 읽어 스폰에 쓸 EnemyDefinition을 만든다.
     *
     * 방향이 중요하다. **빌더는 설정 애셋을 읽기만 한다.** 12단계까지는 반대여서,
     * 인스펙터에서 `Enemy_DarkSamurai.asset`을 고쳐도 Build Combat Content 한 번에
     * 날아갔다. 이제 손으로 고치는 것(BossConfig)과 생성되는 것(EnemyDefinition)이
     * 갈라져 있고, 덮어써도 되는 쪽만 덮어쓴다.
     */
    public static class BossConfigBuilder
    {
        /** 생성된 정의가 들어가는 곳. 손으로 고칠 이유가 없는 폴더다 */
        public const string GeneratedFolder = "Assets/_Project/Data/Generated";

        public const string ConfigFolder = "Assets/_Project/Data/Bosses";

        public const string DarkSamuraiPath = ConfigFolder + "/Boss_DarkSamurai.asset";
        public const string CyclopsLanternPath = ConfigFolder + "/Boss_CyclopsLantern.asset";

        /** 챕터 관문. 그 스테이지 잡몹의 확대판이라 지역이 바뀌어도 그대로 쓴다 */
        public const string ElitePath = ConfigFolder + "/Boss_Elite.asset";

        /** 지역 2 피날레 */
        public const string ExecutionerPath = ConfigFolder + "/Boss_Executioner.asset";
        public const string Region1Path = ConfigFolder + "/Region_1.asset";
        public const string Region2Path = ConfigFolder + "/Region_2.asset";
        public const string RosterPath = ConfigFolder + "/BossRoster.asset";

        private const string DemonSpriteFolder = "Assets/ThirdParty/Characters/Demon_Samurai/Sprites";
        private const string ExecutionerSpriteFolder = "Assets/ThirdParty/Characters/Executioner/Sprites";
        private const string ChochinDefinitionPath = "Assets/_Project/Data/Enemy_Chochin.asset";

        /**
         * @brief 지역 1 배치를 씨앗으로 깔아둔다. **있으면 건드리지 않는다.**
         *
         * 덮어쓰지 않는 것이 핵심이다. 이 애셋들은 손으로 고치라고 만든 것이고,
         * 빌드할 때마다 기본값으로 되돌아가면 하드코딩을 걷어낸 의미가 없다.
         * 빌더가 만드는 것은 EnemyDefinition(생성물)뿐이다.
         *
         * 처음 실행할 때만 일이 있고, 그 뒤로는 로그 한 줄도 남기지 않는다.
         */
        public static BossRoster EnsureDefaultAssets()
        {
            EnsureFolder(ConfigFolder);

            var darkSamurai = LoadOrCreate<BossConfig>(DarkSamuraiPath, config =>
            {
                config.displayName = "다크 사무라이";
                config.kind = BossConfig.ArtKind.Sheets;

                // 지역 피날레만 전체 연출을 쓴다. 5의 배수마다 돌던 것을 여기로
                // 옮긴 것이 13단계 배치의 핵심이다
                config.fullIntro = true;

                config.idleSheet = Sheet(DemonSpriteFolder + "/IDLE.png");
                config.walkSheet = Sheet(DemonSpriteFolder + "/RUN.png");
                config.hurtSheet = Sheet(DemonSpriteFolder + "/HURT.png");
                config.deathSheet = Sheet(DemonSpriteFolder + "/DEATH.png");
                config.attackSheet = Sheet(DemonSpriteFolder + "/ATTACK 1.png");

                config.cellWidth = 128;
                config.cellHeight = 108;

                // 아래의 자동 측정이 덮어쓴다. 여기 적는 값은 측정이 실패했을
                // 때의 폴백이고, 12단계까지 손으로 세던 그 값이다
                config.feetPadding = 12;

                config.frameRate = 12f;
                config.moveSpeed = 0.85f;
            });

            var lantern = LoadOrCreate<BossConfig>(CyclopsLanternPath, config =>
            {
                config.displayName = "외눈 등롱";
                config.kind = BossConfig.ArtKind.ScaledMob;

                // 21단계에 챕터 관문에서 **지역 피날레로 승격**했다. 라인업이
                // 지역마다 다른 피날레를 세우는 쪽으로 확정되면서(랜턴 -> 처형인
                // -> 다크사무라이 -> 요괴), 지역 1의 얼굴이 이쪽이 됐다.
                //
                // 그래서 전체 연출을 켠다. 피날레만 암전을 쓰는 규칙은 그대로다
                config.fullIntro = true;

                config.baseMob = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ChochinDefinitionPath);
                config.scale = 2;

                // 밝은 틴트여야 한다. 곱연산이라 어두운 값은 원래도 어두운 요괴
                // 아트를 검은 덩어리로 만든다
                config.tint = new Color32(0xFF, 0xC0, 0xC8, 0xFF);
            });

            /**
             * 챕터 관문. 랜턴이 피날레로 올라가면서 빈 자리를 메운다.
             *
             * **baseMob을 비워둔다.** 그러면 BossFight가 그 스테이지의 잡몹을
             * 가져다 쓴다 - "이 스테이지의 우두머리"라는 인상은 방금까지 베던
             * 놈이라야 생기고, 그것은 지역 설정이 아니라 스테이지가 정한다.
             *
             * 그냥 chapterBoss를 null로 두면 안 된다. 그 경우 BossFight가
             * 12단계까지의 옛 경로로 떨어져 **다크 사무라이가 챕터로 되돌아온다.**
             * 비우는 것과 "잡몹 확대를 쓰라고 말하는 것"은 다르다.
             */
            var elite = LoadOrCreate<BossConfig>(ElitePath, config =>
            {
                config.displayName = "정예";
                config.kind = BossConfig.ArtKind.ScaledMob;
                config.fullIntro = false;
                config.baseMob = null;
                config.scale = 2;

                // 피날레(분홍)와 다른 색이어야 한다. 같은 틴트면 두 관문이
                // 화면에서 구분되지 않고, 피날레가 "더 큰 챕터 보스"로 읽힌다.
                // 푸른 쪽으로 밀어 냉기 도는 정예로 세운다
                config.tint = new Color32(0xA8, 0xD8, 0xFF, 0xFF);
            });

            var region = LoadOrCreate<RegionConfig>(Region1Path, config =>
            {
                config.displayName = "지역 1";
                config.stageCount = BossCurve.RegionLength;
                config.chapterEvery = BossCurve.ChapterEvery;
                config.chapterBoss = elite;
                config.finaleBoss = lantern;
                config.normalBossOverride = null;
            });

            /**
             * @brief 지역 2 피날레. 라인업의 두 번째 자리다.
             *
             * 다크 사무라이와 같은 전용 시트형이고 셀 규격만 다르다 - 실측
             * **130x92**(다크 사무라이는 128x108). 프레임 수는 IDLE 12 / HURT 6 /
             * DEATH 11 / ATTACK 11로, 시트 폭들의 최대공약수에서 나왔다.
             *
             * 걷기 시트(WALK, 12프레임)도 쓴다. 21단계에 붙이지 않고 넘겼다가
             * **선 자세로 미끄러져 온다**는 지적을 받고 되돌아왔다 - 잡몹은
             * 작고 빨라서 넘어갔지만 이 덩치에서는 곧바로 보인다.
             */
            var executioner = LoadOrCreate<BossConfig>(ExecutionerPath, config =>
            {
                config.displayName = "처형인";
                config.kind = BossConfig.ArtKind.Sheets;
                config.fullIntro = true;

                config.idleSheet = Sheet(ExecutionerSpriteFolder + "/IDLE.png");
                config.walkSheet = Sheet(ExecutionerSpriteFolder + "/WALK.png");
                config.hurtSheet = Sheet(ExecutionerSpriteFolder + "/HURT.png");
                config.deathSheet = Sheet(ExecutionerSpriteFolder + "/DEATH.png");
                config.attackSheet = Sheet(ExecutionerSpriteFolder + "/ATTACK 1.png");

                config.cellWidth = 130;
                config.cellHeight = 92;

                // 이 팩은 **왼쪽을 보고** 그려져 있다. 다른 팩들과 반대라,
                // 뒤집으면 플레이어에게 등을 돌린다
                config.artFacesLeft = true;

                // 아래의 자동 측정이 덮어쓴다. 여기 값은 측정이 실패했을 때의 폴백
                config.feetPadding = 10;

                config.frameRate = 12f;
                config.moveSpeed = 0.85f;
            });

            /**
             * 지역 2. 21단계에 배경(가을숲)이 준비되면서 생겼다.
             *
             * 피날레가 처형인이다. 지역마다 다른 얼굴이 서는 것이 라인업의
             * 요점이고(랜턴 -> 처형인 -> 다크사무라이 -> 요괴), 배경만 바뀌고
             * 보스가 같으면 "같은 곳을 다시 도는" 인상이 남는다.
             */
            var region2 = LoadOrCreate<RegionConfig>(Region2Path, config =>
            {
                config.displayName = "지역 2";
                config.stageCount = BossCurve.RegionLength;
                config.chapterEvery = BossCurve.ChapterEvery;
                config.chapterBoss = elite;
                config.finaleBoss = executioner;
                config.normalBossOverride = null;
            });

            var roster = LoadOrCreate<BossRoster>(RosterPath, config =>
            {
                config.regions = new[] { region, region2 };
            });

            int backfilled = 0;
            if (BackfillWalkSheet(darkSamurai, DemonSpriteFolder + "/RUN.png")) backfilled++;
            if (BackfillWalkSheet(executioner, ExecutionerSpriteFolder + "/WALK.png")) backfilled++;

            // 저장하지 않으면 SetDirty가 다음 리로드에 날아가고, 채움이 매 빌드마다
            // 다시 돈다. "한 번만 하는 일"이라고 로그에 적어놓고 매번 도는 것은
            // 그 자체로 틀린 신호다
            if (backfilled > 0) AssetDatabase.SaveAssets();

            return roster;
        }

        /**
         * @brief 없던 칸이 생겼을 때 한 번만 채운다. **덮어쓰기가 아니다.**
         *
         * 씨앗은 애셋을 만들 때만 돌기 때문에, 나중에 필드를 추가하면 이미 있는
         * 애셋에서는 영원히 비어 있다. 처형인 config가 정확히 그 상태였다 -
         * 걷기 시트를 씨앗에 적어도 이미 만들어진 애셋은 그것을 못 본다.
         *
         * **비어 있을 때만** 채운다. 손으로 비워둔 것과 구분이 안 되는 것은
         * 사실이지만, 이 칸의 빈 값은 "idle로 걷는다"는 옛 동작이지 누가 고른
         * 설정이 아니다. 손으로 비우고 싶으면 그때 이 줄을 지우는 것이 맞다.
         */
        private static bool BackfillWalkSheet(BossConfig config, string sheetPath)
        {
            if (config == null || config.kind != BossConfig.ArtKind.Sheets) return false;
            if (config.walkSheet != null) return false;

            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (sheet == null) return false;

            config.walkSheet = sheet;
            EditorUtility.SetDirty(config);
            Debug.Log("[Onikiri] " + config.name + ": walkSheet 채움 -> " + sheetPath);
            return true;
        }

        private static Texture2D Sheet(string path)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (sheet == null) Debug.LogError("[Onikiri] Boss sheet missing: " + path);
            return sheet;
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> seed) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            seed(created);
            AssetDatabase.CreateAsset(created, path);
            EditorUtility.SetDirty(created);

            Debug.Log("[Onikiri] Seeded " + path + " (edit it by hand from now on - "
                      + "the builder will not overwrite it).");
            return created;
        }

        /**
         * @brief 시트형 보스의 아트를 자르고 정의를 갱신한다.
         *
         * 확대형은 아무것도 만들지 않는다 - 쓸 정의가 이미 잡몹 쪽에 있다.
         *
         * @return 갱신된 정의. 확대형이거나 실패하면 null
         */
        public static EnemyDefinition Build(BossConfig config)
        {
            if (config == null) return null;
            if (config.kind != BossConfig.ArtKind.Sheets) return null;

            if (config.idleSheet == null || config.deathSheet == null)
            {
                Debug.LogError("[Onikiri] BossConfig '" + config.name + "' has no IDLE/DEATH sheet.");
                return null;
            }

            EnsureFolder(GeneratedFolder);

            // 피벗을 발밑에 고정해 격자로 다시 자른다. 셀 크기는 애셋이 들고 있고,
            // 그 값이 틀리면 SliceGrid가 나누어떨어지지 않아 false를 낸다
            var pivot = new Vector2(0.5f, config.feetPadding / (float)Mathf.Max(1, config.cellHeight));

            int sliced = 0, failed = 0;
            foreach (var sheet in Sheets(config))
            {
                string path = AssetDatabase.GetAssetPath(sheet);
                if (string.IsNullOrEmpty(path)) continue;

                if (CharacterSpriteSlicer.SliceGrid(path, config.cellWidth, config.cellHeight, pivot)) sliced++;
                else failed++;
            }

            if (failed > 0)
            {
                Debug.LogError(string.Format(
                    "[Onikiri] BossConfig '{0}': {1} sheet(s) do not divide by {2}x{3} - " +
                    "check the cell size on the asset.",
                    config.name, failed, config.cellWidth, config.cellHeight));
                return null;
            }

            string definitionPath = GeneratedFolder + "/Enemy_" + config.name + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            definition.displayName = config.displayName;
            definition.idleFrames = OrderedSprites(config.idleSheet);
            definition.walkFrames = OrderedSprites(config.walkSheet);
            definition.hurtFrames = OrderedSprites(config.hurtSheet);
            definition.deathFrames = OrderedSprites(config.deathSheet);
            definition.attackFrames = OrderedSprites(config.attackSheet);
            definition.attackInterval = (float)BossCurve.AttackIntervalSeconds;
            definition.attackImpactPoint = 0.55f;
            definition.frameRate = config.frameRate;
            definition.moveSpeed = config.moveSpeed;
            definition.queueSpacing = 2.0f;
            definition.hoverHeight = 0f;

            // 잡몹 추첨에 절대 끼면 안 된다. 스포너의 배열에도 넣지 않지만,
            // 가중치 0을 함께 적어 의도를 에셋에서도 읽히게 한다
            definition.spawnWeight = 0f;

            // 스폰 시점에 덮어쓰인다. 0으로 두면 혹시 이 값으로 스폰되는 경로가
            // 생겼을 때 즉사하는 보스로 곧바로 드러난다
            definition.maxHealth = BigDouble.Zero;
            definition.goldReward = BigDouble.Zero;

            // 잡몹과 달리 0이다. 잡몹은 Aseprite 캔버스 피벗을 쓰므로 아트까지의
            // 거리를 런타임에 빼야 하지만, 보스는 위에서 피벗을 아예 발밑에 찍었다
            definition.artBottomOffset = 0f;

            // 팩마다 그려진 방향이 다르다. Enemy가 이 값을 보고 뒤집을지 정한다
            definition.artFacesLeft = config.artFacesLeft;

            config.generatedDefinition = definition;

            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(config);

            Debug.Log(string.Format(
                "[Onikiri] Boss '{0}': idle={1} walk={2} hurt={3} death={4} attack={5} frames, " +
                "cell {6}x{7}, feet {8}px, {9} sheets sliced.",
                definition.displayName, definition.idleFrames.Length, definition.walkFrames.Length,
                definition.hurtFrames.Length, definition.deathFrames.Length,
                definition.attackFrames.Length,
                config.cellWidth, config.cellHeight, config.feetPadding, sliced));

            return definition;
        }

        /**
         * @brief 네 시트에서 발밑 여백을 재고, 셋 이상이 일치하는지 확인한다.
         *
         * 시트마다 다르면 **가장 작은 값**을 쓴다. 큰 값에 맞추면 아트가 더 아래로
         * 내려간 프레임이 땅에 박힌다.
         *
         * @param report 시트별 측정값. 보고용
         * @return 쓸 값. 하나도 못 재면 -1
         */
        public static int MeasureFeetPadding(BossConfig config, out string report)
        {
            report = string.Empty;
            if (config == null) return -1;

            var lines = new List<string>();
            int idleMeasured = -1;
            int fallback = int.MaxValue;

            foreach (var sheet in Sheets(config))
            {
                string path = AssetDatabase.GetAssetPath(sheet);
                if (string.IsNullOrEmpty(path)) continue;

                int measured = CharacterSpriteSlicer.MeasureFeetPadding(
                    path, config.cellWidth, config.cellHeight);

                lines.Add(sheet.name + "=" + (measured < 0 ? "실패" : measured + "px"));

                if (sheet == config.idleSheet) idleMeasured = measured;
                if (measured >= 0 && measured < fallback) fallback = measured;
            }

            report = string.Join(", ", lines.ToArray());

            /**
             * **idle 기준으로 잡는다.** 예전에는 네 시트의 최소값을 썼는데, 그것은
             * "어떤 시트도 땅에 박히지 않게"라는 뜻이지만 그 대가로 **나머지 전부가
             * 뜬다.**
             *
             * 처형인에서 드러났다 - IDLE 16 / HURT 16 / DEATH 14 / ATTACK 5 라
             * 최소값 5를 쓰면 서 있는 내내 11px(0.34u) 공중에 떠 있었다. 공격
             * 프레임만 다리를 아래로 뻗기 때문인데, 그 한 시트에 맞추느라 기본
             * 자세를 희생한 셈이다.
             *
             * 보스는 대부분의 시간을 idle로 보낸다. 공격 순간 발이 살짝 지면에
             * 묻히는 것은 오히려 딛는 동작으로 읽힌다.
             */
            if (idleMeasured >= 0) return idleMeasured;
            return fallback == int.MaxValue ? -1 : fallback;
        }

        private static IEnumerable<Texture2D> Sheets(BossConfig config)
        {
            if (config.idleSheet != null) yield return config.idleSheet;
            // 걷기도 같은 피벗으로 잘라야 한다. 여기서 빠지면 걷기 프레임만
            // 캔버스 피벗을 쓰게 되어, 걷다가 멈추는 순간 발밑이 튄다
            if (config.walkSheet != null) yield return config.walkSheet;
            if (config.hurtSheet != null) yield return config.hurtSheet;
            if (config.deathSheet != null) yield return config.deathSheet;
            if (config.attackSheet != null) yield return config.attackSheet;
        }

        /**
         * @brief 시트에서 잘린 스프라이트를 이름 끝의 번호순으로.
         *
         * AssetDatabase가 돌려주는 순서는 보장되지 않는다. 이름순 문자열 정렬도
         * 안 된다 - "_10"이 "_2"보다 앞에 온다.
         */
        private static Sprite[] OrderedSprites(Texture2D sheet)
        {
            if (sheet == null) return new Sprite[0];

            string path = AssetDatabase.GetAssetPath(sheet);
            if (string.IsNullOrEmpty(path)) return new Sprite[0];

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => TrailingNumber(s.name))
                .ToArray();
        }

        private static int TrailingNumber(string name)
        {
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            if (i >= name.Length) return 0;

            int value;
            return int.TryParse(name.Substring(i), out value) ? value : 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
