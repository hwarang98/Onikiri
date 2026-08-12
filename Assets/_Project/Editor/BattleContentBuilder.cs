using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.EditorTools
{
    /**
     * @brief 전투 콘텐츠를 구성한다.
     *
     * 적 정의 에셋, 적/참격 프리팹을 만들고, 스포너와 사무라이의 전투를 Main.unity에
     * 배선한다.
     *
     * 재실행 가능하며 Onikiri/Scene/Build Battle Stage 다음에 실행한다.
     */
    public static class BattleContentBuilder
    {
        /**
         * @brief 적 티어 하나. 크기는 34px 사무라이를 기준으로 실측한 idle 프레임 아트다.
         *
         * 구성은 높이만이 아니라 시각적 존재감으로 짰다. 히토다마는 사무라이와 키가
         * 거의 같지만(32px) 폭이 20px뿐이라 가느다란 불꽃으로 읽히고, Inimig(7)은 29px로
         * 더 작지만 폭이 32px라 훨씬 묵직하다. 가중치로 작은 종류를 흔하게, 정예를
         * 드물게 만든다.
         */
        private struct EnemyTier
        {
            public string Aseprite;
            public string AssetName;
            public string DisplayName;
            public string IdleClip;
            public string DeathClip;

            /**
             * @brief 걷기·피격·공격 태그. **없는 요괴는 비운다.**
             *
             * 이 세 칸은 48단계에 생겼다. 그전까지는 팩에서 idle과 death만
             * 꺼내 썼고 나머지 태그는 임포트만 되고 아무도 안 읽었다 - 여덟
             * 파일에 그런 태그가 열둘이었다.
             *
             * "안 쓰던 것을 마저 쓴다"는 것이 이 칸들의 전부다. **어느 것도
             * 밸런스를 건드리지 않는다** - 이동 속도·체력·골드·공격 주기는
             * 그대로이고, 바뀌는 것은 같은 시간 동안 화면에 뜨는 그림이다.
             *
             * 비어 있어도 되는 칸이다. Enemy는 걷기가 없으면 idle로 걷고,
             * 피격이 없으면 흰 플래시로 대신하며, 공격이 없으면 자세를 유지한다.
             */
            public string WalkClip;
            public string HurtClip;
            public string AttackClip;

            /** 태그가 없는 구간을 이름으로 집는 폴백. Idle/Death와 같은 규칙 */
            public string[] AttackNames;

            /**
             * @brief 클립 대신 스프라이트 이름으로 프레임을 집는 폴백.
             *
             * Inimig (2)는 태그가 하나뿐이라(43프레임 중 12프레임만 태그) 사망이
             * 클립으로 안 나온다. 미태그 프레임도 스프라이트로는 전부 임포트되므로
             * 화면 실측으로 확정한 이름 범위를 직접 적는다. IdleClip/DeathClip이
             * 비어 있을 때만 쓴다.
             */
            public string[] IdleNames;
            public string[] DeathNames;

            /** 이 몹이 속한 지역(1~4). 0이면 어느 풀에도 안 들어가는 보스 전용 */
            public int Region;

            public float SpawnWeight;
            public float Health;
            public float MoveSpeed;
            public float QueueSpacing;
            public float HoverHeight;
            public double Gold;
        }

        /**
         * @brief 지역별 잡몹 배분 (36단계). 이 표가 "어느 지역에 어떤 몹이 나오나"의
         * 단일 출처다.
         *
         * ## 스탯 구조는 모든 지역이 같다
         *
         * 주력(w5, HP12, 골드5) + 부몹(w4, HP17, 골드6). 가중 평균이 정확히
         * 128/9 = 14.222, 49/9 = 5.444로, 8단계부터 쓰던 3종(w3/5/1, HP 8/14/34,
         * 골드 2/5/18) 시절의 필드 평균과 같다. 보스 체력과 방치 보상이 스포너의
         * 가중 평균에서 유도되므로, **평균이 같으면 풀을 바꿔도 밸런스가 한 치도
         * 안 움직인다.** 시뮬레이션·밴드 테스트가 전부 그대로인 이유다.
         *
         * 지역 3과 4는 같은 두 종을 주력만 바꿔 쓴다(의도된 겹침 - 팩에 남는 몹이
         * 없다). 같은 에셋에 가중치를 지역마다 다르게 줄 수 없어서(가중치가 정의
         * 안에 있다) 지역 4는 _Den 접미사의 별도 에셋이다. 엔드게임 전용 몹 팩을
         * 사면 지역 4의 두 줄만 갈아끼우면 된다.
         *
         * 제외: Inimig (1) 초롱은 지역 1 피날레(외눈 등롱)의 원본이라 잡몹으로
         * 쓰지 않는다 - 보스와 잡몹이 같은 그림이면 피날레의 무게가 사라진다.
         * Inimig (9)는 다크 사무라이 보스 시트다.
         */
        private static readonly EnemyTier[] Tiers =
        {
            // ---- 지역 1 (봄숲 여명): 밝은 숲의 장난스러운 것들
            new EnemyTier {
                // 태그가 둘뿐이다(대기 12f, 사망 6f). 걷기·공격·피격은 팩에 없다 -
                // 이 요괴는 등껍질을 지고 기어다니는 그림 두 벌이 전부다
                Aseprite = "Inimig (7)", AssetName = "Enemy_Kourin", DisplayName = "Mossback",
                IdleClip = "Tag", DeathClip = "Tag_0", Region = 1,
                SpawnWeight = 5f, Health = 12f, MoveSpeed = 1.0f,
                QueueSpacing = 1.25f, HoverHeight = 0f, Gold = 5d
            },
            new EnemyTier {
                Aseprite = "Inimig (8)", AssetName = "Enemy_Kinoko", DisplayName = "Kinoko-obake",
                IdleClip = "Tag", WalkClip = "Tag_0", DeathClip = "Tag_1", Region = 1,
                SpawnWeight = 4f, Health = 17f, MoveSpeed = 1.15f,
                QueueSpacing = 1.0f, HoverHeight = 0f, Gold = 6d
            },

            // ---- 지역 2 (가을숲): 흙빛 장난 요괴
            new EnemyTier {
                // 팩에서 가장 많이 그려진 개체다 - 다섯 태그가 전부 다른 동작이다.
                // 공격(Tag_2)은 몸을 둥글게 말았다 터뜨리는 덮치기이고,
                // 피격(Tag_3)은 납작하게 눌린 자세다. 눈으로 확인해 갈랐다
                Aseprite = "Inimig (6)", AssetName = "Enemy_Kedama", DisplayName = "Kedama",
                IdleClip = "Tag", WalkClip = "Tag_1", DeathClip = "Tag_0",
                AttackClip = "Tag_2", HurtClip = "Tag_3", Region = 2,
                SpawnWeight = 5f, Health = 12f, MoveSpeed = 1.05f,
                QueueSpacing = 1.1f, HoverHeight = 0f, Gold = 5d
            },
            new EnemyTier {
                Aseprite = "Inimig (2)", AssetName = "Enemy_Kasaobake", DisplayName = "Kasa-obake",
                // 태그가 하나뿐인 개체다. idle은 첫 줄의 깡충 뛰기(F0~7)를,
                // 사망은 팩 공통 규칙(하양 플래시 -> 붕괴 -> 먼지)과 같은 모양의
                // 미태그 구간(F28~33)을 이름으로 집는다. 화면 실측으로 확정했다
                IdleNames = new[] { "Frame_0", "Frame_1", "Frame_2", "Frame_3",
                                    "Frame_4", "Frame_5", "Frame_6", "Frame_7" },
                DeathNames = new[] { "Frame_28", "Frame_29", "Frame_30",
                                     "Frame_31", "Frame_32", "Frame_33" },
                // **그 하나뿐인 태그가 공격이다.** 12프레임짜리 물어뜯기로,
                // 입을 벌리고(F41~44) 달려들었다가 제자리로 돌아온다. 지금까지
                // 이 파일에서 유일하게 태그가 붙은 구간이 아무 데도 안 걸려
                // 있었다 - 태그가 하나뿐이라 idle일 것이라고 지나쳤던 자리다
                AttackClip = "Tag",
                Region = 2,
                SpawnWeight = 4f, Health = 17f, MoveSpeed = 0.9f,
                QueueSpacing = 1.1f, HoverHeight = 0f, Gold = 6d
            },

            // ---- 지역 3 (자줏빛 밤): 유령과 악귀
            new EnemyTier {
                Aseprite = "Inimig (4)", AssetName = "Enemy_Hitodama", DisplayName = "Hitodama",
                IdleClip = "Tag", WalkClip = "Tag_0", DeathClip = "Tag_1", Region = 3,
                SpawnWeight = 5f, Health = 12f, MoveSpeed = 1.25f,
                QueueSpacing = 1.0f, HoverHeight = 0.35f, Gold = 5d
            },
            new EnemyTier {
                Aseprite = "Inimig (3)", AssetName = "Enemy_Onigashira", DisplayName = "Onigashira",
                IdleClip = "Tag", WalkClip = "Tag_0", DeathClip = "Tag_2",
                HurtClip = "Tag_1", Region = 3,
                SpawnWeight = 4f, Health = 17f, MoveSpeed = 1.2f,
                QueueSpacing = 1.0f, HoverHeight = 0.4f, Gold = 6d
            },

            // ---- 지역 4 (요괴 소굴): 지역 3 재활용, 주력만 오니 두상으로 역전.
            // 틴트는 걸지 않는다 - 적 틴트 금지는 가독성 규칙이다(배경만 틴트)
            new EnemyTier {
                Aseprite = "Inimig (3)", AssetName = "Enemy_Onigashira_Den", DisplayName = "Onigashira",
                IdleClip = "Tag", WalkClip = "Tag_0", DeathClip = "Tag_2",
                HurtClip = "Tag_1", Region = 4,
                SpawnWeight = 5f, Health = 12f, MoveSpeed = 1.2f,
                QueueSpacing = 1.0f, HoverHeight = 0.4f, Gold = 5d
            },
            new EnemyTier {
                Aseprite = "Inimig (4)", AssetName = "Enemy_Hitodama_Den", DisplayName = "Hitodama",
                IdleClip = "Tag", WalkClip = "Tag_0", DeathClip = "Tag_1", Region = 4,
                SpawnWeight = 4f, Health = 17f, MoveSpeed = 1.25f,
                QueueSpacing = 1.0f, HoverHeight = 0.35f, Gold = 6d
            },

            // ---- 보스 전용: 외눈 등롱(지역 1 피날레)의 원본. 가중치 0이라
            // 어느 풀에도, 어느 평균에도 안 들어간다 - 스폰 경로는 잃지만
            // Boss_CyclopsLantern이 baseMob으로 계속 참조한다
            new EnemyTier {
                // 여섯 태그 중 넷(Tag/Tag_0/Tag_3/Tag_4)이 **거의 같은 대기 루프**다 -
                // 넷을 나란히 재어보면 폭·픽셀 수·무게중심이 소수점까지 겹친다.
                // 그래서 걷기 칸은 비워 둔다. 그중 하나를 걷기라고 적으면 표에는
                // 걷기가 생기지만 화면에서는 아무 일도 일어나지 않고, 다음 사람이
                // "걷기가 있는데 왜 안 보이지"를 다시 확인하게 된다.
                // 이 요괴는 다리가 없으니 떠다니는 것이 맞다.
                //
                // 진짜로 새로 붙는 것은 피격(Tag_1) 하나다. 확대판 피날레 보스로
                // 서는 개체라 오래 얻어맞고, 그동안 움찔하는 그림이 생긴다
                Aseprite = "Inimig (1)", AssetName = "Enemy_Chochin", DisplayName = "Chochin-obake",
                IdleClip = "Tag", DeathClip = "Tag_2", HurtClip = "Tag_1", Region = 0,
                SpawnWeight = 0f, Health = 34f, MoveSpeed = 0.8f,
                QueueSpacing = 1.5f, HoverHeight = 0.1f, Gold = 18d
            }
        };

        private const string EnemyFolder = "Assets/ThirdParty/Enemies/FeudalJapan/";
        // 참격 시트 상수는 없다. **참격은 아래 ATTACK 시트 안에 그려져 있다** -
        // 별도 아크를 얹지 않는 이유는 PlayerCombat.SpawnSpark에 적어뒀다.
        private const string SamuraiIdle = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/IDLE.png";
        private const string SamuraiAttack = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/ATTACK 1.png";

        /** 사거리가 비었을 때 도는 클립. 17단계에서 전진감을 만드는 자리 */
        private const string SamuraiRun = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/RUN.png";

        private const string DataFolder = "Assets/_Project/Data";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string EnemyPrefabPath = PrefabFolder + "/Enemy.prefab";

        /**
         * @brief 손으로 넣은 추가 사운드를 이 폴더들에서 주워온다.
         *
         * 아래의 선별된 뱅크에 더해진다.
         */
        private const string HitAudioFolder = "Assets/_Project/Audio/Hits";
        private const string KillAudioFolder = "Assets/_Project/Audio/Kills";

        private const string SfxRoot = "Assets/Leohpaz/RPG_Essentials_Free/";

        /**
         * @brief RPG Essentials 팩에서 고른 타격음 뱅크.
         *
         * 폴더를 스캔하지 않고 손으로 골랐다. 어떤 소리가 "카타나가 요괴를 벤다"로
         * 읽히는지는 설계 판단이고, 같은 팩에 메뉴 클릭과 발소리가 섞여 있어 절대
         * 여기 들어오면 안 되기 때문이다. 길이를 섞어둔 것(1.3초 참격 하나에
         * 0.7초 임팩트 셋)은 피치 랜덤 위에 자연스러운 변화를 하나 더 얹는다.
         */
        private static readonly string[] HitClipPaths =
        {
            SfxRoot + "10_Battle_SFX/22_Slash_04.wav",
            SfxRoot + "10_Battle_SFX/15_Impact_flesh_02.wav",
            SfxRoot + "10_Battle_SFX/77_flesh_02.wav",
            SfxRoot + "12_Player_Movement_SFX/61_Hit_03.wav"
        };

        /**
         * @brief 처치음 뱅크.
         *
         * 팩에 사망음이 하나뿐이고 그것이 정답이지만, 원본 2.667초는 이 게임에 너무 길다.
         * 처치 간격이 그보다 짧아지면 보이스가 잔향으로 계속 차 있어서 새 처치음이
         * 밀려난다. SfxTrimBuilder가 구워둔 0.35초 사본을 쓴다.
         */
        private static readonly string[] KillClipPaths =
        {
            SfxTrimBuilder.DeathShortPath
        };

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        /** 사양서에 따라 데미지 팝업에 쓰는 라틴 디스플레이 서체 */
        private const string ThaleahFontPath = "Assets/_Project/Art/Fonts/ThaleahFat SDF.asset";
        private const string DamagePrefabPath = PrefabFolder + "/DamageNumber.prefab";

        [MenuItem("Onikiri/Scene/Build Combat Content")]
        public static void Build()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);

            // 잘라낸 효과음이 없으면 배선 단계에서 클립을 못 찾는다. 빌더가 스스로
            // 만들어두게 해서 메뉴 실행 순서를 외우지 않아도 되게 한다
            SfxTrimBuilder.Rebuild();

            // UI 글리프(톱니·자물쇠·해골 등)도 같은 이유로 스스로 굽는다 (38단계)
            UiGlyphBuilder.Build();

            foreach (var tier in Tiers) BuildEnemyDefinition(tier);
            BossContentBuilder.BuildDefinition();
            BossContentBuilder.BuildBosses();

            // 지역 몹 풀은 보스 빌드 다음이다. Region_N 애셋이 그쪽(EnsureDefaultAssets)
            // 에서 만들어지고, 풀을 물릴 자리가 있어야 물릴 수 있다
            BuildRegionMobSets();
            BuildEnemyPrefab();
            BuildDamageNumberPrefab();
            SakuraContentBuilder.BuildArt();
            ImpactSparkBuilder.BuildArt();

            var scene = EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath, OpenSceneMode.Single);

            // 씬을 연 '뒤에' 경로로 에셋을 다시 로드한다. 씬 로드 전에 만든 오브젝트
            // 참조는 그때 발생하는 재임포트로 무효화될 수 있고, 무효한 UnityEngine.Object를
            // SerializedProperty에 대입하면 아무 에러 없이 null이 기록된다.
            // 스포너에 프리팹이 비어 있던 원인이 정확히 이것이었다
            // 스포너의 시작 풀은 지역 1이다. 지역이 넘어가면 RegionMobSwitcher가
            // 런타임에 바꾸므로, 씬에 굳는 것은 첫 지역 한 벌뿐이다
            var definitions = new List<EnemyDefinition>();
            foreach (var tier in Tiers)
            {
                if (tier.Region != 1) continue;
                var loaded = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath(tier));
                if (loaded != null) definitions.Add(loaded);
            }
            var enemyPrefab = LoadPrefabComponent<Enemy>(EnemyPrefabPath);
            var sparkPrefab = ImpactSparkBuilder.BuildPrefab();

            if (definitions.Count == 0 || enemyPrefab == null || sparkPrefab == null)
            {
                Debug.LogError("[Onikiri] Combat assets missing after build: definitions=" + definitions.Count
                               + " enemy=" + (enemyPrefab != null) + " spark=" + (sparkPrefab != null));
                return;
            }

            EnsureAudioListener();
            // 안전 영역 루트를 먼저 세운다. 아래의 배선이 밴드를 찾아 쓰는데, 그 사이에
            // 밴드의 부모가 바뀌면 방금 연결한 참조가 가리키는 계층이 달라진다
            UpgradePanelBuilder.EnsureSafeArea();

            // 밴드 앵커를 DisplayConfig에 다시 맞춘다(41단계 탭바 7.5%).
            // 씬에 저장된 옛 비율이 남으면 코드의 배치 계산과 씬이 어긋난다
            MainSceneBuilder.ReassertBands();

            var shake = WireCameraShake();
            var hitAudio = WireHitAudio();
            WireWalletAndHud();
            var damageNumbers = WireDamageNumbers();

            var spawner = WireSpawner(definitions, enemyPrefab);
            WirePlayerCombat(spawner, sparkPrefab, shake, hitAudio, damageNumbers);

            // 강화는 PlayerCombat이 씬에 있어야 배선할 수 있다.
            // 성장 패널이 증폭 행을 만들려면 CharacterLevel과 LevelHud가 이미
            // 씬에 있어야 하는데, 그것은 WireWalletAndHud가 세웠다
            var upgrades = UpgradePanelBuilder.Build();

            // 반대 방향 참조는 여기서 잇는다. CharacterLevel이 포인트가 바뀔 때마다
            // 강화 적용을 다시 돌려야 증폭이 스탯에 들어간다
            WireCharacterToUpgrades(upgrades);

            // 스킬 화면은 잠긴 탭보다 먼저다. 탭이 켤 대상을 참조로 들고 있어야
            // 하는데(LockedTab.screen), 없으면 조용히 예전처럼 안 눌리는 탭이 된다
            var skills = SkillPanelBuilder.Build();

            // 퀘스트 화면도 잠긴 탭보다 먼저다. 스킬과 같은 이유 - 탭이 켤 대상을
            // 참조로 들고 있어야 한다. 스킬 다음인 것은 업적이 오의 총 레벨을
            // 읽으므로 SkillSystem이 이미 씬에 있어야 하기 때문이다
            var quests = QuestPanelBuilder.Build();

            // 장비 화면은 퀘스트 다음이다. 두 가지가 이미 씬에 있어야 한다 -
            // GemWallet(퀘스트가 세운다)과 UpgradeSystem(장비 배수가 그쪽을
            // 통해 스탯에 도달한다). 잠긴 탭보다 먼저인 것은 앞의 둘과 같은 이유다
            var equipment = EquipmentPanelBuilder.Build();

            // 상점은 대장간 다음이다(46단계). 뽑기가 요도의 상태를 읽어
            // 혼 정수의 상한을 판정하므로(YodoSystem.TryTakeEssence)
            // YodoSystem이 이미 씬에 있어야 하고, 그것을 세우는 것은 대장간
            // 빌더다. 잠긴 탭보다 먼저인 것은 앞의 셋과 같은 이유다
            ShopPanelBuilder.Build();

            WireLockedTabs();
            WireStageAdvance();

            // 보스전은 강화 다음이다. 실패 문구가 "어느 축을 올려라"를 고르려면
            // UpgradeSystem이 이미 씬에 있어야 한다
            BossContentBuilder.Wire(spawner);

            // 지역 몹 스위처는 보스전 다음이다. 확대판 보스도 그 지역의 잡몹이어야
            // 하므로 BossFight가 씬에 있어야 물릴 수 있다
            WireRegionMobSwitcher(spawner);

            // 상단 바에서 열리는 화면 셋(스탯/재선택/설정)도 보스전 다음이다 -
            // 재선택 화면이 BossFight(파밍 중 가드)와 스포너를 참조한다
            HudScreensBuilder.Build();

            // 상호 배타 배선은 **모든 화면이 만들어진 뒤** 마지막이다. 패널
            // 빌더들이 자기 판을 지우고 다시 만들므로, 먼저 배선하면 죽은
            // 참조가 남는다
            WireScreenExclusivity();

            // 세션은 마지막이다. 강화·스테이지·전투가 전부 자리를 잡은 뒤라야
            // 세이브를 복원할 대상을 찾을 수 있다
            var offlinePopup = WireOfflinePopup();
            WireRegionTransition();
            WireSession(spawner, offlinePopup, skills, quests, equipment);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (!VerifyWiring()) return;
            Debug.Log("[Onikiri] Combat content built.");
        }

        // ---------------------------------------------------------------- 적 데이터

        private static string DefinitionPath(EnemyTier tier)
        {
            return DataFolder + "/" + tier.AssetName + ".asset";
        }

        private const string MobSetFolder = DataFolder + "/Mobs";

        /** 지역 N의 잡몹 풀 애셋 경로. BossContentBuilder가 지역 1 풀을 배선에 쓴다 */
        public static string RegionMobSetPath(int region)
        {
            return MobSetFolder + "/RegionMobs_" + region + ".asset";
        }

        /**
         * @brief 지역별 잡몹 풀 애셋을 만들고 Region_N에 물린다.
         *
         * 풀의 구성원은 Tiers 표의 Region 칸에서 나온다. Region_N 애셋은 씨앗이
         * 한 번만 도는 물건이라(LoadOrCreate), mobs 칸은 여기서 매번 덮어쓴다 -
         * 풀은 손으로 배치하는 데이터가 아니라 빌더 생성물이기 때문이다.
         */
        private static void BuildRegionMobSets()
        {
            EnsureFolder(MobSetFolder);

            string[] regionPaths =
            {
                BossConfigBuilder.Region1Path, BossConfigBuilder.Region2Path,
                BossConfigBuilder.Region3Path, BossConfigBuilder.Region4Path
            };

            for (int region = 1; region <= regionPaths.Length; region++)
            {
                var mobs = new List<EnemyDefinition>();
                foreach (var tier in Tiers)
                {
                    if (tier.Region != region) continue;
                    var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath(tier));
                    if (definition != null) mobs.Add(definition);
                }

                if (mobs.Count == 0)
                {
                    Debug.LogError("[Onikiri] Region " + region + " has no mobs in the tier table.");
                    continue;
                }

                string setPath = RegionMobSetPath(region);
                var set = AssetDatabase.LoadAssetAtPath<RegionMobSet>(setPath);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<RegionMobSet>();
                    AssetDatabase.CreateAsset(set, setPath);
                }

                set.mobs = mobs.ToArray();
                EditorUtility.SetDirty(set);

                var regionConfig = AssetDatabase.LoadAssetAtPath<RegionConfig>(regionPaths[region - 1]);
                if (regionConfig == null)
                {
                    Debug.LogError("[Onikiri] Region config missing at " + regionPaths[region - 1]);
                    continue;
                }

                if (regionConfig.mobs != set)
                {
                    regionConfig.mobs = set;
                    EditorUtility.SetDirty(regionConfig);
                }

                Debug.Log(string.Format("[Onikiri] Region {0} mobs: {1}",
                    region, string.Join(", ", mobs.ConvertAll(m => m.displayName).ToArray())));
            }

            AssetDatabase.SaveAssets();
        }

        /**
         * @brief 지역이 바뀔 때 스폰 풀을 갈아끼우는 컴포넌트를 배선한다.
         *
         * RegionBackgroundSwitcher(BattleStageBuilder)와 같은 GameObject에 산다.
         * 이 배선이 빠지면 전 지역이 지역 1 몹으로 돈다 - 화면은 멀쩡해 보이고
         * 지역 2부터만 조용히 틀린다.
         */
        private static void WireRegionMobSwitcher(EnemySpawner spawner)
        {
            var battle = GameObject.Find("Battle");
            if (battle == null) return;

            var switcher = battle.GetComponent<RegionMobSwitcher>();
            if (switcher == null) switcher = battle.AddComponent<RegionMobSwitcher>();

            var so = new SerializedObject(switcher);
            so.FindProperty("roster").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BossRoster>(BossConfigBuilder.RosterPath);
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("bossFight").objectReferenceValue = battle.GetComponent<BossFight>();
            so.FindProperty("progress").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.Progression.StageProgress>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnemyDefinition BuildEnemyDefinition(EnemyTier tier)
        {
            string path = DefinitionPath(tier);
            string aseprite = EnemyFolder + tier.Aseprite + ".aseprite";

            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.displayName = tier.DisplayName;
            definition.idleFrames = Frames(aseprite, tier.IdleClip, tier.IdleNames);

            // 걸어 들어오는 동안 도는 클립. 없는 요괴는 지금까지처럼 idle로 걷는다.
            // 떠다니는 것들(도깨비불·등롱)에는 애초에 걷기가 없고 있을 이유도 없다
            definition.walkFrames = Frames(aseprite, tier.WalkClip, null);

            // 피격 태그가 있는 요괴만 채운다. 없으면 Enemy가 흰 플래시로 대체하고,
            // 이 스프라이트 크기에서는 그것으로도 충분히 읽힌다
            definition.hurtFrames = Frames(aseprite, tier.HurtClip, null);

            definition.deathFrames = Frames(aseprite, tier.DeathClip, tier.DeathNames);
            definition.frameRate = 12f;

            // 이 정의는 잡몹으로도, 그 스테이지의 '거대' 보스로도 쓰인다.
            // 공격 주기를 적어두지만 **잡몹은 이것으로 공격하지 않는다** -
            // Enemy는 스폰 시점에 받은 공격력이 0이면 주기를 아예 돌리지 않고,
            // 잡몹 스폰 경로는 0을 넘긴다. 보스로 스폰될 때만 깨어나는 값이다
            definition.attackInterval = (float)BossCurve.AttackIntervalSeconds;
            definition.attackImpactPoint = 0.5f;

            // 공격 태그가 있는 요괴만 채운다. 여덟 파일 중 둘뿐이다
            // (케다마의 덮치기, 카사오바케의 물어뜯기).
            //
            // 잡몹으로 스폰될 때는 이 배열이 있어도 재생되지 않는다 - Enemy는
            // 공격력 0을 받으면 주기를 아예 돌리지 않고, 잡몹 스폰 경로는 0을
            // 넘긴다. 깨어나는 것은 이 정의가 확대판 보스로 설 때뿐이다
            definition.attackFrames = Frames(aseprite, tier.AttackClip, tier.AttackNames);

            // 공격 그림이 없는 요괴가 확대판 보스로 설 때, 참격 이펙트를 타격보다
            // 이만큼 먼저 띄운다. **피해량과 주기는 안 움직인다** - 옮기는 것은
            // 그림이 뜨는 시각뿐이다(Enemy.UpdateAttack).
            //
            // 0.35초인 이유는 사람이 "예고"로 읽는 하한이 대략 그 언저리이고,
            // 2초 주기 안에서 그만큼 먼저 떠도 다음 주기와 겹치지 않기 때문이다
            definition.attackTelegraphSeconds = 0.35f;

            definition.spawnWeight = tier.SpawnWeight;
            definition.maxHealth = BigDouble.FromDouble(tier.Health);
            definition.moveSpeed = tier.MoveSpeed;
            definition.queueSpacing = tier.QueueSpacing;
            definition.hoverHeight = tier.HoverHeight;
            definition.goldReward = BigDouble.FromDouble(tier.Gold);

            // 캔버스 안에서 아트가 실제로 시작하는 위치를 측정한다. bounds.min.y는
            // 피벗(캔버스 하단)에서 가장 아래 그려진 픽셀까지의 거리이고, 이것이 바로
            // Enemy가 요괴를 지면에 앉히기 위해 필요한 보정값이다
            definition.artBottomOffset = definition.idleFrames.Length > 0
                ? definition.idleFrames[0].bounds.min.y
                : 0f;

            EditorUtility.SetDirty(definition);

            Debug.Log(string.Format(
                "[Onikiri] Enemy '{0}': idle={1} walk={2} hurt={3} attack={4} death={5} frames, " +
                "weight={6}, hp={7}, gold={8}, artBottom={9:F4}u",
                definition.displayName, definition.idleFrames.Length, definition.walkFrames.Length,
                definition.hurtFrames.Length, definition.attackFrames.Length,
                definition.deathFrames.Length,
                definition.spawnWeight, definition.maxHealth, tier.Gold, definition.artBottomOffset));

            return definition;
        }

        /** 클립 이름이 있으면 클립에서, 없으면 스프라이트 이름 범위에서 프레임을 뽑는다 */
        private static Sprite[] Frames(string assetPath, string clipName, string[] spriteNames)
        {
            if (!string.IsNullOrEmpty(clipName)) return FramesFromClip(assetPath, clipName);
            if (spriteNames != null && spriteNames.Length > 0) return FramesByName(assetPath, spriteNames);
            return new Sprite[0];
        }

        /**
         * @brief 스프라이트 이름으로 프레임을 집는다. 태그가 없는 구간용 폴백.
         *
         * Inimig (2)처럼 태그가 안 붙은 프레임은 클립이 없지만 스프라이트로는
         * 임포트된다(임포터가 Frame_N으로 이름 붙인다. 중복 프레임은 건너뛰므로
         * 번호에 구멍이 있을 수 있다 - 그래서 범위가 아니라 이름을 하나씩 적는다).
         */
        private static Sprite[] FramesByName(string assetPath, string[] spriteNames)
        {
            var byName = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) byName[sprite.name] = sprite;
            }

            var frames = new List<Sprite>();
            foreach (var name in spriteNames)
            {
                Sprite sprite;
                if (byName.TryGetValue(name, out sprite)) frames.Add(sprite);
                else Debug.LogWarning("[Onikiri] Sprite '" + name + "' not found in " + assetPath);
            }
            return frames.ToArray();
        }

        /**
         * @brief 임포트된 Aseprite 애니메이션 클립에서 순서대로 스프라이트 목록을 뽑는다.
         *
         * 어떤 프레임이 어느 태그에 속하는지 아는 것은 클립뿐이므로, 인덱스로 프레임
         * 범위를 추측하지 않고 키프레임을 읽는다.
         */
        private static Sprite[] FramesFromClip(string assetPath, string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return new Sprite[0];

            AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                var candidate = asset as AnimationClip;
                if (candidate != null && candidate.name == clipName) { clip = candidate; break; }
            }

            if (clip == null)
            {
                Debug.LogWarning("[Onikiri] Clip '" + clipName + "' not found in " + assetPath);
                return new Sprite[0];
            }

            var frames = new List<Sprite>();
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                for (int i = 0; i < keys.Length; i++)
                {
                    var sprite = keys[i].value as Sprite;
                    // Aseprite는 마지막 프레임을 유지하려고 끝에 중복 키를 하나 더 쓴다
                    if (sprite == null) continue;
                    if (frames.Count > 0 && frames[frames.Count - 1] == sprite && i == keys.Length - 1) continue;
                    frames.Add(sprite);
                }
            }
            return frames.ToArray();
        }

        // ---------------------------------------------------------------- 프리팹

        /**
         * @brief 저장된 씬에서 배선된 참조를 전부 다시 읽어 확인한다.
         *
         * SerializedProperty 대입은 무효한 오브젝트를 받으면 조용히 실패한다. 그래서
         * "빌더가 에러 없이 돌았다"는 것만으로는 아무것도 증명하지 못한다. 이 검사가
         * 그런 종류의 버그를 플레이 중 널 참조가 아니라 빌드 시점 에러로 바꿔준다.
         */
        private static bool VerifyWiring()
        {
            var problems = new List<string>();

            // 검사 비용은 싸지만 코드만 봐서는 절대 알아챌 수 없다. 씬에 리스너가 없어도
            // AudioSource는 스스로 재생 중이라고 보고한다
            if (Object.FindFirstObjectByType<AudioListener>() == null)
                problems.Add("Scene has no AudioListener - all audio will be silent");

            // 레이아웃 참조가 빠지면 배경과 지면선이 마지막으로 성공했을 때의 좌표에
            // 얼어붙는다. 플레이는 정상으로 보이고 배경만 UI 밴드와 어긋난 채 남는다
            var layoutObject = GameObject.Find("Battle");
            var layout = layoutObject != null ? layoutObject.GetComponent<BattleStageLayout>() : null;
            if (layout == null) problems.Add("Battle has no BattleStageLayout");
            else
            {
                var layoutSo = new SerializedObject(layout);
                RequireReference(layoutSo, "targetCamera", problems);
                RequireReference(layoutSo, "battleArea", problems);
                RequireReference(layoutSo, "backgroundRoot", problems);
                RequireReference(layoutSo, "groundAnchor", problems);
                RequireReference(layoutSo, "skyFill", problems);
            }

            var spawnerObject = GameObject.Find("EnemySpawner");
            if (spawnerObject == null) { Debug.LogError("[Onikiri] EnemySpawner missing from scene."); return false; }

            var spawnerSo = new SerializedObject(spawnerObject.GetComponent<EnemySpawner>());
            RequireReference(spawnerSo, "stage", problems);
            RequireReference(spawnerSo, "enemyPrefab", problems);
            RequireReference(spawnerSo, "enemyParent", problems);
            var definitions = spawnerSo.FindProperty("definitions");
            if (definitions.arraySize == 0 || definitions.GetArrayElementAtIndex(0).objectReferenceValue == null)
                problems.Add("EnemySpawner.definitions is empty");

            // 씬에 굳는 시작 풀은 지역 1이어야 한다. 다른 풀이 굳어 있으면 첫
            // 프레임 스폰과 세이브 복원 직후의 평균이 잠깐 다른 풀에서 나온다
            var region1Set = AssetDatabase.LoadAssetAtPath<RegionMobSet>(RegionMobSetPath(1));
            if (region1Set == null || region1Set.mobs == null)
                problems.Add("Region-1 mob set missing at " + RegionMobSetPath(1));
            else if (definitions.arraySize != region1Set.mobs.Length)
                problems.Add("EnemySpawner.definitions is not the region-1 pool");
            else
            {
                for (int i = 0; i < region1Set.mobs.Length; i++)
                    if (definitions.GetArrayElementAtIndex(i).objectReferenceValue != region1Set.mobs[i])
                        problems.Add("EnemySpawner.definitions[" + i + "] differs from the region-1 pool");
            }

            var samurai = GameObject.Find("Samurai");
            if (samurai == null) { Debug.LogError("[Onikiri] Samurai missing from scene."); return false; }

            var combatSo = new SerializedObject(samurai.GetComponent<PlayerCombat>());
            RequireReference(combatSo, "spawner", problems);
            RequireReference(combatSo, "animator", problems);
            RequireReference(combatSo, "sparkPrefab", problems);
            RequireReference(combatSo, "cameraShake", problems);
            RequireReference(combatSo, "hitAudio", problems);
            RequireArray(combatSo, "idleFrames", problems);
            RequireArray(combatSo, "attackFrames", problems);
            RequireArray(combatSo, "sparkFrames", problems);

            // 공격속도 상한이 이 두 값에서 유도된다. 아트 팩을 바꾸거나 프레임레이트를
            // 손대면 강화 상한 레벨도 함께 움직여야 하는데, 그 사실은 코드 어디에도
            // 드러나지 않는다. 여기서 잡지 않으면 상한만 예전 값으로 남는다
            // 치명타가 어긋나면 화면에서는 아무 문제가 없어 보이고, 밸런스 계산만
            // 조용히 틀린다
            if (!Mathf.Approximately(combatSo.FindProperty("critChance").floatValue, CombatBaseline.CritChance) ||
                !Mathf.Approximately(combatSo.FindProperty("critMultiplier").floatValue, CombatBaseline.CritMultiplier))
            {
                problems.Add("PlayerCombat crit values differ from CombatBaseline"
                             + " - StageSimulation would compute the wrong DPS");
            }

            int attackFrames = combatSo.FindProperty("attackFrames").arraySize;
            float attackFrameRate = combatSo.FindProperty("attackFrameRate").floatValue;
            if (attackFrames != AttackSpeedCurve.AttackFrameCount ||
                !Mathf.Approximately(attackFrameRate, AttackSpeedCurve.AttackFrameRate))
            {
                problems.Add(string.Format(
                    "Attack clip is {0} frames @ {1}fps but AttackSpeedCurve assumes {2} @ {3}fps"
                    + " - the attack-speed cap would be wrong",
                    attackFrames, attackFrameRate,
                    AttackSpeedCurve.AttackFrameCount, AttackSpeedCurve.AttackFrameRate));
            }

            // 강화는 눌러봐야만 알 수 있는 종류의 실패를 낸다. 트랙이 비었거나 combat
            // 참조가 없으면 버튼은 정상으로 보이고 아무 일도 일어나지 않는다
            var panel = MainSceneBuilder.FindBand("GrowthPanel");
            if (panel == null) problems.Add("GrowthPanel band missing");
            else
            {
                var system = panel.GetComponent<Onikiri.Progression.UpgradeSystem>();
                if (system == null) problems.Add("GrowthPanel has no UpgradeSystem");
                else
                {
                    var upgradeSo = new SerializedObject(system);
                    RequireReference(upgradeSo, "combat", problems);
                    RequireReference(upgradeSo, "health", problems);
                    RequireArray(upgradeSo, "tracks", problems);

                    // 행 수가 빌더가 만들기로 한 수와 다르면 화면에 유령 텍스트가 남는다.
                    //
                    // 11단계에서 행의 부모를 Viewport/Content로 옮기면서 패널 직속에
                    // 있던 옛 행 넷이 지워지지 않고 남았다. 뒤에 그려지기 때문에
                    // 새 행 사이의 틈으로 옛 수치가 비쳐 보였고, 화면에서는
                    // "글자가 겹친다"로만 보여 원인을 찾기 어려웠다.
                    //
                    // 18단계에서 행의 부모가 **또** 바뀌었다(Content -> 페이지 루트).
                    // 같은 사고가 날 자리라 세는 단위도 페이지로 옮긴다 - Content
                    // 직속을 세던 예전 식을 그냥 두면 페이지 셋만 세고 그 안의
                    // 잔재는 못 본다
                    var content = panel.Find("Viewport/Content");
                    if (content == null) problems.Add("GrowthPanel has no Viewport/Content");
                    else
                    {
                        if (content.childCount != UpgradePanelBuilder.PageNames.Length)
                            problems.Add(string.Format(
                                "GrowthPanel has {0} children under Content but the builder makes {1} pages"
                                + " - stale rows from the pre-18 flat list would show through",
                                content.childCount, UpgradePanelBuilder.PageNames.Length));

                        foreach (var pageName in UpgradePanelBuilder.PageNames)
                        {
                            var page = content.Find(pageName);
                            if (page == null) { problems.Add("GrowthPanel has no " + pageName); continue; }

                            int expected = UpgradePanelBuilder.RowsInPage(pageName);
                            if (page.childCount != expected)
                                problems.Add(string.Format(
                                    "GrowthPanel page '{0}' has {1} rows but the builder makes {2}",
                                    pageName, page.childCount, expected));
                        }
                    }

                    // 패널 직속에는 Viewport와 탭 줄 둘뿐이어야 한다. 그 외는 잔재다.
                    // 탭 줄은 목록이 스크롤돼도 제자리에 있어야 해서 Viewport 밖에
                    // 산다. 18단계에서 재화 탭 줄이 위에 붙어 둘이 됐다
                    for (int i = 0; i < panel.childCount; i++)
                    {
                        var name = panel.GetChild(i).name;
                        if (System.Array.IndexOf(UpgradePanelBuilder.PanelChildNames, name) >= 0) continue;
                        problems.Add("GrowthPanel has a stray child '" + panel.GetChild(i).name
                                     + "' outside the scroll viewport - it renders behind the rows");
                    }
                }
            }

            if (Object.FindFirstObjectByType<Onikiri.UI.SafeAreaFitter>() == null)
                problems.Add("No SafeAreaFitter - UI will run under the notch");

            VerifyCharacterLevel(problems);
            VerifyBossRoster(problems);
            VerifyRowIcons(problems);

            // 꽃잎은 없어도 전투가 도므로 플레이 중에는 아무 문제가 없어 보인다.
            // 배선이 빠진 것과 "아직 안 만든 것"이 화면에서 구분되지 않는다
            var sakura = samurai.GetComponent<SakuraBurst>();
            if (sakura == null) problems.Add("Samurai has no SakuraBurst");
            else
            {
                var sakuraSo = new SerializedObject(sakura);
                RequireReference(sakuraSo, "petalPrefab", problems);
                RequireArray(sakuraSo, "petalSprites", problems);
            }

            // 보스전이 배선되지 않으면 10마리를 잡은 뒤에야 알게 된다. 그 시점에
            // 도전 버튼이 없으면 진행이 영원히 멈춘다 - 스테이지가 오르는 경로가
            // 보스뿐이기 때문이다
            var bossFight = layoutObject != null ? layoutObject.GetComponent<BossFight>() : null;
            if (bossFight == null) problems.Add("Battle has no BossFight - stages can never advance");
            else
            {
                var bossSo = new SerializedObject(bossFight);
                RequireReference(bossSo, "spawner", problems);
                RequireReference(bossSo, "progress", problems);
                RequireReference(bossSo, "bossDefinition", problems);
                RequireReference(bossSo, "upgrades", problems);
                // 이것이 없으면 보스가 때려도 아무 일도 일어나지 않는다.
                // 화면에서는 정상으로 보이고 플레이어는 영원히 죽지 않는다
                RequireReference(bossSo, "playerHealth", problems);
                RequireArray(bossSo, "stageBossDefinitions", problems);

                var bossDef = bossSo.FindProperty("bossDefinition").objectReferenceValue as EnemyDefinition;
                if (bossDef != null && (bossDef.idleFrames == null || bossDef.idleFrames.Length == 0))
                    problems.Add("Boss definition has no idle frames - the boss would be invisible");
                if (bossDef != null && (bossDef.attackFrames == null || bossDef.attackFrames.Length == 0))
                    problems.Add("Boss definition has no attack frames - the boss would hit without telegraphing");

                // 확대 배율이 정수가 아니면 픽셀 격자가 어긋난다. BossContentBuilder 참고
                float stageBossScale = bossSo.FindProperty("stageBossScale").floatValue;
                if (!Mathf.Approximately(stageBossScale, Mathf.Round(stageBossScale)))
                    problems.Add("stageBossScale " + stageBossScale + " is not an integer"
                                 + " - the pixel grid would break at some device scales");
            }

            // 지역 잡몹 스위처. 빠지면 전 지역이 지역 1 몹으로 돈다 - 화면은
            // 멀쩡해 보이고 지역 2부터만 조용히 틀린다
            var mobSwitcher = layoutObject != null ? layoutObject.GetComponent<RegionMobSwitcher>() : null;
            if (mobSwitcher == null)
                problems.Add("Battle has no RegionMobSwitcher - every region would spawn region-1 mobs");
            else
            {
                var mobSwitcherSo = new SerializedObject(mobSwitcher);
                RequireReference(mobSwitcherSo, "roster", problems);
                RequireReference(mobSwitcherSo, "spawner", problems);
                RequireReference(mobSwitcherSo, "bossFight", problems);
            }

            VerifyRegionMobPools(problems);
            VerifyHudScreens(problems);

            // 플레이어 체력이 없으면 보스전에서 아무도 죽지 않는다
            var playerHealthComponent = samurai.GetComponent<PlayerHealth>();
            if (playerHealthComponent == null) problems.Add("Samurai has no PlayerHealth");
            else
            {
                var healthSo = new SerializedObject(playerHealthComponent);
                RequireArray(healthSo, "hurtFrames", problems);
                RequireArray(healthSo, "deathFrames", problems);
                RequireReference(healthSo, "sakura", problems);
            }

            var bossHud = Object.FindFirstObjectByType<Onikiri.UI.BossHud>();
            if (bossHud == null) problems.Add("No BossHud - the challenge button would never appear");
            else
            {
                var hudSo = new SerializedObject(bossHud);
                RequireReference(hudSo, "fight", problems);
                RequireReference(hudSo, "challengeButton", problems);
                RequireReference(hudSo, "introRoot", problems);
                RequireReference(hudSo, "fightRoot", problems);
                RequireReference(hudSo, "healthFill", problems);
                RequireReference(hudSo, "resultRoot", problems);
                RequireReference(hudSo, "resultLabel", problems);
            }

            // 세이브가 배선되지 않으면 플레이 중에는 아무 문제가 없어 보이고,
            // 앱을 껐다 켠 뒤에야 진행이 사라진 것을 알게 된다
            var battle = GameObject.Find("Battle");
            var session = battle != null ? battle.GetComponent<Onikiri.Progression.GameSession>() : null;
            if (session == null) problems.Add("Battle has no GameSession - nothing will be saved");
            else
            {
                var sessionSo = new SerializedObject(session);
                RequireReference(sessionSo, "upgrades", problems);
                RequireReference(sessionSo, "stage", problems);
                RequireReference(sessionSo, "combat", problems);
                RequireReference(sessionSo, "spawner", problems);
                RequireReference(sessionSo, "offlinePopup", problems);
                RequireReference(sessionSo, "quests", problems);
            }

            VerifySkillChoreography(samurai, problems);
            VerifyGlyphCoverage(problems);
            VerifyExpRowFits(problems);
            VerifyNoMecanimAnimators(problems);

            if (problems.Count > 0)
            {
                Debug.LogError("[Onikiri] Combat wiring incomplete:\n  " + string.Join("\n  ", problems.ToArray()));
                return false;
            }
            return true;
        }

        /**
         * @brief 상단 바 버튼 셋과 그 화면들이 배선됐는지 (37단계).
         *
         * HudScreenButton.screen이 비면 버튼은 정상으로 보이고 눌러도 아무 일도
         * 일어나지 않는다 - LockedTab의 "구현 안 된 탭"과 같은 종류의 실패인데,
         * 이쪽은 잠금 표시도 없어서 화면에서 구분할 방법이 없다.
         */
        private static void VerifyHudScreens(List<string> problems)
        {
            var topBar = MainSceneBuilder.FindBand("TopBar");
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (topBar == null || safeArea == null) return;

            string[][] pairs =
            {
                new[] { "StageButton", HudScreensBuilder.RegionSelectPanelName },
                new[] { "PortraitButton", HudScreensBuilder.StatsPanelName },
                new[] { "SettingsButton", HudScreensBuilder.SettingsPanelName }
            };

            foreach (var pair in pairs)
            {
                var buttonObject = topBar.Find(pair[0]);
                if (buttonObject == null)
                {
                    problems.Add("Top bar has no " + pair[0]);
                    continue;
                }

                var control = buttonObject.GetComponent<Onikiri.UI.HudScreenButton>();
                if (control == null)
                {
                    problems.Add(pair[0] + " has no HudScreenButton - it would do nothing");
                    continue;
                }

                var so = new SerializedObject(control);
                var screen = so.FindProperty("screen").objectReferenceValue as GameObject;
                if (screen == null || screen.name != pair[1])
                    problems.Add(pair[0] + " is not wired to " + pair[1]);
                else if (screen.activeSelf)
                    problems.Add(pair[1] + " is saved active - it would cover the growth panel on boot");
            }

            // 스탯 창의 값 라벨이 전부 물려 있는지. 하나라도 비면 그 줄만 "-"로 남는다
            var statsObject = safeArea.Find(HudScreensBuilder.StatsPanelName);
            var stats = statsObject != null
                ? statsObject.GetComponent<Onikiri.UI.StatsPanel>() : null;
            if (stats == null) problems.Add("StatsPanel component missing");
            else
            {
                var statsSo = new SerializedObject(stats);
                RequireReference(statsSo, "combat", problems);
                RequireReference(statsSo, "health", problems);
                RequireReference(statsSo, "damageValue", problems);
                RequireReference(statsSo, "dpsValue", problems);
                RequireReference(statsSo, "multiplierDetail", problems);
                // 경험치 수치는 이제 이 창에만 있다(스트립에는 숫자가 없다).
                // 빠지면 정확한 경험치를 볼 자리가 게임 어디에도 없어진다
                RequireReference(statsSo, "expValue", problems);
            }

            var selectObject = safeArea.Find(HudScreensBuilder.RegionSelectPanelName);
            var select = selectObject != null
                ? selectObject.GetComponent<Onikiri.UI.RegionSelectPanel>() : null;
            if (select == null) problems.Add("RegionSelectPanel component missing");
            else
            {
                var selectSo = new SerializedObject(select);
                RequireReference(selectSo, "progress", problems);
                RequireReference(selectSo, "fight", problems);
                RequireReference(selectSo, "spawner", problems);
                RequireReference(selectSo, "frontierButton", problems);
                RequireArray(selectSo, "rows", problems);
            }

            // 처치 할당량이 보스 자리로 옮겨왔는지. 상단 바에서 뺐는데 여기도
            // 없으면 진행 신호가 화면에서 통째로 사라진다
            var band = MainSceneBuilder.FindBand("BattleArea");
            if (band != null && band.Find("BossQuota") == null)
                problems.Add("BossQuota missing - the kill counter left the top bar and never landed");
        }

        /**
         * @brief 지역 1~4의 잡몹 풀이 전부 서 있고, 각 몹이 화면에 나올 수 있는지.
         *
         * idle이 비면 투명한 요괴가, death가 비면 죽는 순간 뚝 사라지는 요괴가
         * 나온다. 특히 Kasa-obake는 클립이 아니라 이름 범위로 프레임을 집으므로
         * (태그가 없는 개체다), 임포터가 이름 규칙을 바꾸면 여기서 잡혀야 한다.
         */
        private static void VerifyRegionMobPools(List<string> problems)
        {
            string[] regionPaths =
            {
                BossConfigBuilder.Region1Path, BossConfigBuilder.Region2Path,
                BossConfigBuilder.Region3Path, BossConfigBuilder.Region4Path
            };

            for (int region = 1; region <= regionPaths.Length; region++)
            {
                var config = AssetDatabase.LoadAssetAtPath<RegionConfig>(regionPaths[region - 1]);
                if (config == null)
                {
                    problems.Add("Region config missing at " + regionPaths[region - 1]);
                    continue;
                }

                if (config.mobs == null || config.mobs.mobs == null || config.mobs.mobs.Length == 0)
                {
                    problems.Add("Region " + region + " has no mob pool - it would inherit the previous region's mobs");
                    continue;
                }

                foreach (var definition in config.mobs.mobs)
                {
                    if (definition == null)
                    {
                        problems.Add("Region " + region + " mob pool has a null entry");
                        continue;
                    }
                    if (definition.idleFrames == null || definition.idleFrames.Length == 0)
                        problems.Add(definition.name + " has no idle frames - it would be invisible");
                    if (definition.deathFrames == null || definition.deathFrames.Length == 0)
                        problems.Add(definition.name + " has no death frames - it would vanish without dying");
                    if (definition.spawnWeight <= 0f)
                        problems.Add(definition.name + " has zero spawn weight but sits in the region " + region + " pool");
                }
            }
        }

        /**
         * @brief 스프라이트를 돌리는 주체가 하나뿐인지.
         *
         * Mecanim Animator와 SpriteAnimator가 같은 SpriteRenderer에 붙으면
         * **Animator가 이긴다.** Animator는 Update 다음의 애니메이션 단계에서
         * sprite를 덮어쓰므로, SpriteAnimator는 프레임을 정상적으로 넘기는데
         * 화면만 컨트롤러의 클립에 머문다.
         *
         * 17단계에서 걸렸고 증상은 "달리는데 다리가 안 움직인다"였다. 코드도
         * 데이터도 멀쩡했다 - runFrames 16개가 배정돼 있었고, 재생 속도도 맞았고,
         * IsPlaying도 true였다. 어디에도 신호가 없었고, 렌더러의 sprite와
         * SpriteAnimator의 frameIndex가 서로 다른 클립을 가리키는 것을 보고서야
         * 드러났다.
         *
         * 원인은 두 빌더가 이 컴포넌트를 두고 반대로 움직인 것이었다. 지금은
         * 양쪽 다 지우는 쪽으로 맞췄지만, 방향이 같아졌다는 사실 자체를 빌드가
         * 확인해야 다음에 누가 한쪽만 되돌려도 조용히 지나가지 않는다.
         */
        /**
         * @brief 오의 안무가 배선됐고 카탈로그와 어긋나지 않는지.
         *
         * 27단계에 생겼다. 안무가 없으면 `SkillPerformer.Cast`가 거절하므로 화면에는
         * "오의가 안 나간다"로만 나타나고, 그것은 26단계의 "나가는지 모르겠다"와
         * 구분되지 않는다 - 그때 실제로는 나가고 있었기 때문이다. 같은 증상에 원인이
         * 둘이면 빌드가 하나를 걷어내야 한다.
         *
         * 타격 프레임 수를 카탈로그와 대조하는 것이 핵심이다. 다타의 총 데미지가
         * 그 수로 나뉘므로(SkillCatalog.HitDamageShare), 둘이 어긋나면 **총 데미지가
         * 조용히 바뀐다** - 셋으로 나눈 것을 두 번만 때리면 3분의 1이 사라진다.
         */
        private static void VerifySkillChoreography(GameObject samurai, List<string> problems)
        {
            var performer = samurai.GetComponent<Onikiri.Battle.SkillPerformer>();
            if (performer == null)
            {
                problems.Add("Samurai has no SkillPerformer - 오의가 하나도 나가지 않는다");
                return;
            }

            var so = new SerializedObject(performer);
            RequireReference(so, "combat", problems);
            RequireReference(so, "slashPrefab", problems);
            RequireReference(so, "streakPrefab", problems);
            RequireReference(so, "afterimagePrefab", problems);
            RequireReference(so, "nameFlash", problems);
            RequireReference(so, "screenFlash", problems);

            var list = so.FindProperty("choreographies");
            if (list.arraySize != Onikiri.Progression.SkillCatalog.Count)
            {
                problems.Add(string.Format(
                    "SkillPerformer has {0} choreographies but the catalog has {1} skills",
                    list.arraySize, Onikiri.Progression.SkillCatalog.Count));
                return;
            }

            for (int i = 0; i < Onikiri.Progression.SkillCatalog.Count; i++)
            {
                var spec = Onikiri.Progression.SkillCatalog.Skills[i];
                var element = list.GetArrayElementAtIndex(i);

                string id = element.FindPropertyRelative("id").stringValue;
                if (id != spec.Id)
                {
                    problems.Add(string.Format(
                        "Choreography {0} is '{1}' but the catalog says '{2}' - 배율과 안무가 " +
                        "다른 오의를 가리킨다", i, id, spec.Id));
                    continue;
                }

                if (element.FindPropertyRelative("clip").arraySize == 0)
                    problems.Add("'" + spec.DisplayName + "'의 클립이 비었다 - 모션 없이 데미지만 들어간다");

                int hitFrames = element.FindPropertyRelative("hitFrames").arraySize;
                int expected = Onikiri.Progression.SkillCatalog.HitsPerCast(i);
                if (hitFrames != expected)
                    problems.Add(string.Format(
                        "'{0}'의 타격 프레임이 {1}개인데 카탈로그는 {2}회를 가정한다 - " +
                        "총 데미지가 {3:P0}만 들어간다",
                        spec.DisplayName, hitFrames, expected,
                        expected > 0 ? hitFrames / (float)expected : 0f));

                // 연참은 참격을 얹지 않는 것이 설계다. 켜지면 23단계가 걷어낸
                // 이중 참격이 그대로 재발한다 - 그 클립에만 궤적이 **세 번**
                // 있다(ATTACK 1/2/3을 이어 붙였다).
                //
                // ## 49단계에 조건을 고쳤다 - Shape가 아니라 이 오의 하나다
                //
                // 27단계에는 `Shape == MultiHit`으로 적혀 있었다. 그때는 다타
                // 오의가 연참 하나뿐이라 두 조건이 구분되지 않았고, 짧은 쪽이
                // 일반 규칙처럼 보였을 뿐이다. 49단계에 다타가 둘 더 생기면서
                // 그 일반화가 거짓이라는 것이 드러났다.
                //
                // **실측으로 확인했다**: 사무라이의 공격 클립 넷(ATTACK 1/2/3 ·
                // SPECIAL)은 **전부** 흰 궤적을 한 번씩 그린다. 그래도 귀참은
                // 27단계부터 SPECIAL 위에 Slash3을 얹고 있고 그것이 이 게임의
                // 오의 연출이다 - "몸의 칼 궤적 + 그 위의 오의 이펙트"는 겹침이
                // 아니라 층이다.
                //
                // 겹침이 되는 것은 **같은 종류의 그림이 여러 번** 겹칠 때이고,
                // 그 조건에 걸리는 것은 궤적이 셋인 연참뿐이다.
                bool usesSlash = element.FindPropertyRelative("usesSlash").boolValue;
                if (spec.Id == Onikiri.Progression.SkillCatalog.ChainSlashId && usesSlash)
                    problems.Add("'" + spec.DisplayName + "'이 팩 참격을 쓴다 - 클립에 이미 "
                                 + "궤적이 세 번 그려져 있어 23단계의 이중 참격이 된다");

                bool usesStreak = element.FindPropertyRelative("usesStreak").boolValue;

                // 관통(일섬)은 눈에 보이는 것이 있어야 한다. 팩 참격이든 돌진
                // 섬광이든 둘 중 하나는 켜져 있어야, 데미지만 들어가고 화면에는
                // 아무것도 안 나오는 상태를 빌드가 잡는다
                if (spec.Shape == Onikiri.Progression.SkillShape.Pierce && !usesSlash && !usesStreak)
                    problems.Add("'" + spec.DisplayName + "'에 참격도 섬광도 없다 - "
                                 + "데미지는 들어가는데 화면에는 아무것도 안 나온다");

                // 섬광은 돌진이 지나간 자리다. 돌진이 0이면 길이도 0이라 안 보인다
                if (usesStreak)
                {
                    if (element.FindPropertyRelative("lungeDistance").floatValue <= 0f)
                        problems.Add("'" + spec.DisplayName + "'이 돌진 섬광을 쓰는데 "
                                     + "돌진 거리가 0이다 - 길이가 0인 선이 된다");

                    if (element.FindPropertyRelative("streakThickness").floatValue <= 0f)
                        problems.Add("'" + spec.DisplayName + "'의 섬광 두께가 0이다");

                    // 섬광은 돌진과 함께 자란다. 다 자라는 시간이 돌진이 나가는
                    // 시간과 다르면 머리가 칼끝에서 떨어진다
                    float reveal = element.FindPropertyRelative("streakRevealSeconds").floatValue;
                    float lungeOut = element.FindPropertyRelative("lungeOutSeconds").floatValue;
                    if (Mathf.Abs(reveal - lungeOut) > 0.001f)
                        problems.Add(string.Format(
                            "'{0}'의 섬광 성장 시간({1:0.###}초)이 돌진 시간({2:0.###}초)과 다르다 - "
                            + "섬광의 머리가 칼끝에서 떨어진다",
                            spec.DisplayName, reveal, lungeOut));
                }

                if (!usesSlash) continue;

                // 참격을 쓰는데 프레임이 비었으면 화면에 아무것도 안 나온다.
                // 데미지는 그대로 들어가므로 "이펙트만 사라진" 상태가 되고,
                // 그 증상은 로그에도 콘솔에도 남지 않는다
                if (element.FindPropertyRelative("slashFrames").arraySize == 0)
                    problems.Add("'" + spec.DisplayName + "'의 참격 프레임이 비었다 - "
                                 + "데미지는 들어가는데 화면에는 아무것도 안 나온다");

                // 한 장짜리면 자르기가 실패한 것이다. 정적인 한 장을 키워 쓰던
                // 27단계로 조용히 되돌아가는 경로가 정확히 이것이다
                if (element.FindPropertyRelative("slashFrames").arraySize == 1)
                    problems.Add("'" + spec.DisplayName + "'의 참격이 한 장뿐이다 - "
                                 + "시트 자르기가 실패했다. 정적인 그림이 뜬다");

                // 배율 상한. 원본 픽셀의 2.5배를 넘기면 아트 픽셀 하나가 화면에서
                // 10px 넘는 네모가 되고, 그것이 27단계의 덩어리다
                float scale = element.FindPropertyRelative("slashScale").floatValue;
                if (scale > 2.5f)
                    problems.Add(string.Format(
                        "'{0}'의 참격 배율이 {1:0.##}배다 - 2.5배를 넘으면 소스 픽셀이 "
                        + "화면에서 네모로 보인다", spec.DisplayName, scale));
            }
        }

        private static void VerifyNoMecanimAnimators(List<string> problems)
        {
            foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (animator.GetComponent<SpriteAnimator>() == null) continue;

                problems.Add("'" + animator.name + "' has both a Mecanim Animator and a SpriteAnimator. "
                             + "The Animator overwrites the sprite after Update, so the SpriteAnimator's "
                             + "frames never reach the screen. Remove the Animator.");
            }
        }

        /**
         * @brief 상단 바 라벨들이 상자에 들어가는지를 빌드가 직접 잰다.
         *
         * 이 바는 두 번 잘렸고 두 번 다 원인이 같았다 - **글자 폭을 눈으로
         * 어림했기 때문이다.** 16단계에서 28px/글자로 잡았지만 55pt Galmuri의
         * 실측은 33.7px이었고, 20%의 오차가 레벨 41에서 터졌다. 어림이 틀렸다는
         * 신호는 스크린샷뿐이었다.
         *
         * 그래서 어림을 걷어내고 TMP에게 직접 묻는다. 라벨마다 최악 문자열을
         * 실제 폰트로 재서 상자와 비교한다. 여기서 걸리면 빌드가 실패하므로,
         * 다음에 이 바에 무언가를 더 넣는 사람은 스크린샷이 아니라 에러로
         * 알게 된다. 행 아이콘 검사, 글리프 검사와 같은 계열이다.
         *
         * 최악 문자열의 근거:
         *   "Lv.999"        3자리 레벨. 방치형 수명 안에 반드시 닿는다
         *   "999.9aa"       골드 축약의 최악(단위 두 글자). 보석은 미축약 "99,999"
         *   "지역 4 · 10/10" 마지막 지역의 마지막 스테이지
         *   "레벨업 99"      쌓인 레벨업 개수. 쓸어담기 전까지 두 자리가 될 수 있다
         *
         * 경험치 숫자("999aa/999aa")는 2a 후속에서 화면을 떠났다 - 바가 얇은
         * 스트립이 되면서 숫자는 스탯 창으로 갔고, 그쪽은 값 폭이 행 폭이라
         * 잘릴 수 없다.
         */
        private static void VerifyExpRowFits(List<string> problems)
        {
            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar == null) return;

            // 캡션 아틀라스가 없으면 Demote가 조용히 아무것도 안 해서 위계가
            // 사라진다 - 에러가 아니라 "전부 44pt인 화면"으로만 나타난다
            if (UiFonts.Caption == null)
                problems.Add("Caption font missing at " + UiFonts.CaptionPath
                             + " - run Onikiri/Art/Build Pixel Font Assets");

            // ---- 2b 상단 바: 초상 배지 / 재화 트레이 / 스테이지 칩

            CheckLabelFits(topBar, "PortraitButton/LevelBadge/Label", "Lv.999",
                           LevelBadgeWidth - 8f, problems);
            CheckLabelFits(topBar, "CurrencyTray/GoldLabel", "999.9aa", GoldLabelWidth, problems);
            CheckLabelFits(topBar, "CurrencyTray/GemLabel", "99,999", GemLabelWidth, problems);

            // 최악 문구가 짧아졌다 - "클리어"가 빠졌다(2b 압축). 상자는 깃발
            // 글리프 자리만큼 줄어 있다.
            // 42단계부터 지역 번호가 무한히 오른다(무한 구간). 두 자리(지역
            // 10~99 = st91~989)까지가 현실적인 최악이고, 세 자리는 st991부터라
            // 그때 다시 잰다
            CheckLabelFits(topBar, "StageButton/StageLabel", "지역 99 · 10/10",
                           StageChipWidth - (StageChipIconLeft + StageChipIconSize + 8f + 14f),
                           problems);

            // 1행에서 트레이와 설정 톱니가 다투지 않는지. 상자끼리의 검사라
            // 폰트와 무관하게 상수에서 바로 나온다
            float trayRight = ContentLeft + CurrencyTrayWidth;
            float settingsLeft = DisplayConfig.DesignWidth - SideMargin - SettingsSize;
            if (trayRight + 12f > settingsLeft)
                problems.Add(string.Format(
                    "Top bar row 1 overlaps: the currency tray ends at {0:F0}px but the "
                    + "settings button starts at {1:F0}px", trayRight, settingsLeft));

            // 컨텐츠가 바 밴드(디자인 높이의 10% = 192px)를 넘지 않는지. 초상
            // 배지가 가장 아래다 - 여기가 넘치면 배지가 전투 화면에 걸린다
            float badgeBottom = PortraitTop + PortraitSize + LevelBadgeOverhang;
            float bandHeight = DisplayConfig.DesignHeight * (1f - DisplayConfig.BattleAreaTop);
            if (badgeBottom > bandHeight - TopBarEdgeHeight)
                problems.Add(string.Format(
                    "Top bar content overflows its band: the level badge ends at {0:F0}px "
                    + "but the band is {1:F0}px tall", badgeBottom, bandHeight));

            // 초상 버튼이 스탯 창 입구 노릇을 하려면 실제로 서 있어야 한다
            if (topBar.Find("PortraitButton/Window/PortraitMask/TierPortrait") == null)
                problems.Add("Top bar portrait is missing - run Build Combat Content "
                             + "after Build Evolution Content");

            // ---- 레벨업 버튼 (2b: 성장 패널 헤더로 내려왔다)

            var growthPanel = MainSceneBuilder.FindBand("GrowthPanel");
            var levelUp = growthPanel != null ? growthPanel.Find("LevelUpButton") : null;
            if (levelUp == null)
                problems.Add("LevelUpButton missing under GrowthPanel - run Build Combat Content");
            else
            {
                CheckLabelFits(growthPanel, "LevelUpButton/Label", "레벨업 99",
                               LevelUpWidth, problems);
                if (levelUp.gameObject.activeSelf)
                    problems.Add("LevelUpButton is saved active - it must start hidden and "
                                 + "only appear when a level-up is pending (LevelHud)");

                // 41단계 재배치의 회귀 가드: 밑변이 패널 상단(스트립 윗변)
                // 아래로 내려오면 스트립을 덮고 탭 줄을 압박한다 - 그 모양이
                // 정확히 "어색하게 걸친 상자"였다. pivot(1,1)이므로 밑변 =
                // anchoredPosition.y - 높이다
                var levelUpRect = levelUp as RectTransform;
                if (levelUpRect != null
                    && levelUpRect.anchoredPosition.y - levelUpRect.sizeDelta.y < 0f)
                    problems.Add(string.Format(
                        "LevelUpButton dips {0:F0}px into the growth panel - its bottom must "
                        + "sit on the exp strip line, not cover it",
                        levelUpRect.sizeDelta.y - levelUpRect.anchoredPosition.y));
            }

            // 경험치 스트립(2a 후속). 상단 바가 아니라 성장 패널 최상단에 있다.
            // 채움은 앵커 폭 방식이라, 빈 상태(anchorMax.x=0)와 민짜 사각형
            // (스프라이트 없음 - UISprite의 소프트 가장자리는 얇은 줄에서
            // 그라데이션으로 읽힌다)을 빌드가 대조한다
            var stripFill = growthPanel != null
                ? growthPanel.Find(UpgradePanelBuilder.ExpStripName + "/Fill") as RectTransform : null;
            var stripImage = stripFill != null
                ? stripFill.GetComponent<UnityEngine.UI.Image>() : null;
            if (stripImage == null)
                problems.Add("Exp strip missing under GrowthPanel - run Build Combat Content");
            else
            {
                if (stripImage.sprite != null)
                    problems.Add("Exp strip fill has a sprite - soft edges read as a gradient "
                                 + "on a 10px line. It must be a plain quad.");
                if (stripFill.anchorMax.x > 0f)
                    problems.Add("Exp strip fill is not empty in the built scene - "
                                 + "LevelHud drives anchorMax.x at runtime and starts from 0");
            }
        }

        private static void CheckLabelFits(Transform topBar, string path, string worst,
                                           float boxWidth, List<string> problems)
        {
            var found = topBar.Find(path);
            var label = found != null ? found.GetComponent<TMPro.TMP_Text>() : null;
            if (label == null)
            {
                problems.Add("Top bar label '" + path + "' is missing - cannot check its width");
                return;
            }

            float needed = label.GetPreferredValues(worst).x;
            if (needed > boxWidth)
                problems.Add(string.Format(
                    "Top bar '{0}' overflows: \"{1}\" needs {2:F0}px but its box is {3:F0}px. "
                    + "Widen it, take the space from a neighbour, or shorten the text - "
                    + "55pt is the baked atlas size and cannot shrink.",
                    path, worst, needed, boxWidth));
        }

        /**
         * @brief 모든 성장 행에 아이콘이 배정됐는지.
         *
         * 축을 하나 추가하면 트랙 표(UpgradePanelBuilder.Specs)에는 넣지만
         * 아이콘 표(UiIcons.Map)에는 넣는 것을 잊기 쉽다. 그러면 그 행만 빈
         * 사각형으로 나가는데, **여덟 줄 중 하나라서 눈에 잘 안 띈다.**
         *
         * 14단계의 유령 텍스트, 9-슬라이스 테두리 검사와 같은 계열이다 -
         * 화면을 훑어 찾을 것이 아니라 빌드가 세어야 한다.
         */
        private static void VerifyRowIcons(List<string> problems)
        {
            var panel = MainSceneBuilder.FindBand("GrowthPanel");
            var content = panel != null ? panel.Find("Viewport/Content") : null;
            if (content == null) return;   // 행 수 검사가 이미 보고했다

            int missing = 0, checkedRows = 0;

            // 행은 이제 Content 직속이 아니라 페이지 루트 아래에 있다(18단계).
            // 한 겹 더 들어가지 않으면 이 검사가 조용히 0행을 세게 된다
            for (int p = 0; p < content.childCount; p++)
            {
                var page = content.GetChild(p);

                for (int i = 0; i < page.childCount; i++)
                {
                    var row = page.GetChild(i);

                    // 머리글("남은 포인트")과 전직 잠금 안내는 행이 아니다.
                    // 버튼이 없는 것으로 가른다
                    if (row.GetComponent<UnityEngine.UI.Button>() == null) continue;

                    checkedRows++;

                    var icon = row.Find("Icon");
                    if (icon == null)
                    {
                        problems.Add("Growth row '" + row.name + "' has no Icon object");
                        missing++;
                        continue;
                    }

                    var image = icon.GetComponent<UnityEngine.UI.Image>();
                    if (image == null || image.sprite == null)
                    {
                        problems.Add("Growth row '" + row.name + "' has an empty icon - "
                                     + "add its track id to UiIcons.Map");
                        missing++;
                    }
                }
            }

            if (checkedRows == 0)
                problems.Add("No growth rows found to check icons on");

            // 재화 아이콘도 같이 본다. 상단 바만 심볼이 없으면 두 화면이 서로
            // 다른 규칙으로 읽힌다. 경험치 별은 바와 함께 화면에서 빠졌다(2a 후속)
            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar != null)
            {
                // 재화 아이콘은 트레이 안에 있다(2b)
                foreach (var name in new[] { "CurrencyTray/GoldIcon", "CurrencyTray/GemIcon" })
                {
                    var found = topBar.Find(name);
                    var image = found != null ? found.GetComponent<UnityEngine.UI.Image>() : null;
                    if (image == null || image.sprite == null)
                        problems.Add("Top bar '" + name + "' is missing its sprite");
                }
            }

            if (missing == 0 && checkedRows > 0)
                Debug.Log("[Onikiri] Row icons: " + checkedRows + " rows, all assigned.");
        }

        /**
         * @brief 보스 배치 애셋의 무결성.
         *
         * 이 계통이 틀리면 **해당 스테이지에 도달해야만** 드러난다. 10스테이지
         * 피날레의 참조가 비어 있어도 1~9스테이지는 멀쩡히 돌고, 그때까지는
         * 아무 신호가 없다. 배치는 눈으로 볼 것이 아니라 빌드가 세어야 한다.
         */
        private static void VerifyBossRoster(List<string> problems)
        {
            var roster = AssetDatabase.LoadAssetAtPath<Onikiri.Battle.BossRoster>(
                BossConfigBuilder.RosterPath);

            if (roster == null)
            {
                problems.Add("No BossRoster at " + BossConfigBuilder.RosterPath);
                return;
            }

            if (roster.regions == null || roster.regions.Length == 0)
            {
                problems.Add("BossRoster has no regions - every stage would fall back to a scaled mob");
                return;
            }

            var fight = Object.FindFirstObjectByType<BossFight>();
            if (fight != null)
            {
                var fightSo = new SerializedObject(fight);
                RequireReference(fightSo, "roster", problems);
            }

            foreach (var region in roster.regions)
            {
                if (region == null) { problems.Add("BossRoster has an empty region slot"); continue; }

                /**
                 * @brief 피날레는 **어느 지역도** 비울 수 없다. 특히 마지막 지역이 그렇다.
                 *
                 * 24단계에 한 번 봐주려다 되돌렸다. 지역 3을 배경만 확정하고 피날레를
                 * 비워둔 채 로스터에 넣었는데, `BossRosterTests`가 잡았다 - 정의된 지역을
                 * 다 지나면 **마지막 지역의 배치가 무한히 반복되므로**, 마지막 지역에
                 * 피날레가 없으면 후반 전체에서 피날레가 사라진다. 10스테이지마다 정예
                 * 관문만 도는 화면이 된다.
                 *
                 * "만들다 만 지역은 봐준다"는 규칙이 하필 가장 봐주면 안 되는 자리를
                 * 봐주고 있었다.
                 */

                if (region.stageCount <= 0)
                    problems.Add(region.name + ": stageCount must be positive");

                // 곡선과 애셋이 갈리면 시뮬레이션이 실제와 다른 밸런스를 잰다.
                // 11단계의 ChapterHealthMultiplier가 정확히 이 종류의 사고였다
                if (region.stageCount != Onikiri.Progression.BossCurve.RegionLength)
                    problems.Add(string.Format(
                        "{0}: stageCount {1} != BossCurve.RegionLength {2} - the simulation would " +
                        "put the finale on a different stage than the game does",
                        region.name, region.stageCount, Onikiri.Progression.BossCurve.RegionLength));

                if (region.chapterEvery != Onikiri.Progression.BossCurve.ChapterEvery)
                    problems.Add(string.Format(
                        "{0}: chapterEvery {1} != BossCurve.ChapterEvery {2}",
                        region.name, region.chapterEvery, Onikiri.Progression.BossCurve.ChapterEvery));

                if (region.finaleBoss == null)
                    problems.Add(region.name + ": finale boss slot is empty");
                if (region.chapterBoss == null)
                    problems.Add(region.name + ": chapter boss slot is empty");
            }

            foreach (var config in BossContentBuilder.ConfigsIn(roster))
                VerifyBossConfig(config, problems);
        }

        private static void VerifyBossConfig(Onikiri.Battle.BossConfig config, List<string> problems)
        {
            if (string.IsNullOrEmpty(config.displayName))
                problems.Add(config.name + ": displayName is empty");

            if (config.kind == Onikiri.Battle.BossConfig.ArtKind.ScaledMob)
            {
                // 확대 배율은 정수만. Pixel Perfect가 아트 픽셀 하나를 화면 픽셀
                // N개로 늘리는데, 배율이 정수가 아니면 어떤 픽셀은 7개 어떤 픽셀은
                // 8개로 그려져 격자가 눈에 띄게 일그러진다 (11단계 규칙)
                if (config.scale < 1)
                    problems.Add(config.name + ": scale " + config.scale + " must be an integer >= 1");

                float brightness = Mathf.Max(config.tint.r, Mathf.Max(config.tint.g, config.tint.b));
                if (brightness < 0.75f)
                    problems.Add(config.name + ": tint is too dark - SpriteRenderer.color multiplies, "
                                 + "so a dark tint turns the mob into a black blob");
                return;
            }

            // 시트형: 정의가 생성됐는지, 프레임이 실제로 잘렸는지
            if (config.generatedDefinition == null)
            {
                problems.Add(config.name + ": no generated definition - run Build Combat Content");
                return;
            }

            var definition = config.generatedDefinition;

            if (definition.idleFrames == null || definition.idleFrames.Length == 0)
                problems.Add(config.name + ": idle clip has no frames");
            if (definition.deathFrames == null || definition.deathFrames.Length == 0)
                problems.Add(config.name + ": death clip has no frames");

            // 공격 프레임이 없으면 보스가 때리는 시늉만 하고 피해가 안 들어간다.
            // 11단계에서 실제로 겪은 것이라 그때 검사를 넣었고, 여기로 옮긴다
            if (definition.attackFrames == null || definition.attackFrames.Length == 0)
                problems.Add(config.name + ": attack clip has no frames - the boss would never hit");

            // 격자 칸 수와 프레임 수를 대조한다. 자른 결과가 격자와 안 맞으면 셀
            // 크기가 틀린 것이고, 화면에서는 프레임이 반씩 잘려 나온다.
            //
            // 등호가 아니라 범위인 이유: 시트가 여러 줄일 수 있고(요괴 - 임포트
            // 상한 4096px 때문에 줄바꿈해 굽는다) SliceGrid가 끝의 빈 칸을
            // 버리므로, 프레임 수는 "마지막 줄에 하나 이상"과 "전체 칸 이하"
            // 사이에 있어야 한다. 한 줄 시트에서는 이 범위가 등호와 같다
            if (config.idleSheet != null && config.cellWidth > 0 && config.cellHeight > 0)
            {
                if (config.idleSheet.width % config.cellWidth != 0)
                    problems.Add(string.Format("{0}: IDLE sheet {1}px does not divide by cell width {2}",
                        config.name, config.idleSheet.width, config.cellWidth));
                else if (definition.idleFrames != null)
                {
                    int columns = config.idleSheet.width / config.cellWidth;
                    int gridRows = Mathf.Max(1, config.idleSheet.height / config.cellHeight);
                    int capacity = columns * gridRows;
                    int frames = definition.idleFrames.Length;

                    if (frames > capacity || frames <= capacity - columns)
                        problems.Add(string.Format(
                            "{0}: idle clip has {1} frames but the {2}x{3} grid holds {4} - cell size is wrong",
                            config.name, frames, columns, gridRows, capacity));
                }
            }

            if (config.feetPadding < 0 || config.feetPadding >= config.cellHeight)
                problems.Add(config.name + ": feetPadding " + config.feetPadding
                             + " is outside the cell height " + config.cellHeight);
        }

        /**
         * @brief 레벨/경험치 계통의 배선.
         *
         * 이 계통은 **비어 있어도 게임이 돈다.** 경험치가 안 쌓이고 레벨업 버튼이
         * 안 뜰 뿐 전투도 강화도 그대로다. 그래서 배선이 빠진 것과 "아직 레벨이
         * 낮아서 안 보이는 것"이 플레이 화면에서 구분되지 않는다 - 꽃잎이 없는
         * 것을 눈으로 못 잡는 것과 같은 종류다.
         */
        private static void VerifyCharacterLevel(List<string> problems)
        {
            var character = Object.FindFirstObjectByType<Onikiri.Progression.CharacterLevel>();
            if (character == null)
            {
                problems.Add("No CharacterLevel - kills would grant no experience");
                return;
            }

            var characterSo = new SerializedObject(character);
            RequireReference(characterSo, "upgrades", problems);

            var hud = Object.FindFirstObjectByType<Onikiri.UI.LevelHud>();
            if (hud == null)
            {
                problems.Add("No LevelHud - the level-up button would never appear");
            }
            else
            {
                var hudSo = new SerializedObject(hud);
                RequireReference(hudSo, "levelLabel", problems);
                RequireReference(hudSo, "expFill", problems);
                RequireReference(hudSo, "levelUpRoot", problems);
                RequireReference(hudSo, "levelUpButton", problems);
                RequireReference(hudSo, "levelUpLabel", problems);

                // 남은 포인트 라벨은 성장 패널이 만들고 여기에 꽂아준다. 빠지면
                // 포인트가 쌓이는데 화면 어디에도 그 숫자가 없는 상태가 된다
                RequireReference(hudSo, "pointsLabel", problems);
            }

            // 흡수 연출의 목표가 비면 팝업이 화면 구석으로 날아간다. 그런데 그것은
            // 처치 순간에만 0.55초 스쳐 지나가는 연출이라 눈으로 잡기 어렵다
            var numbers = Object.FindFirstObjectByType<Onikiri.UI.DamageNumberSpawner>();
            if (numbers != null)
            {
                var numbersSo = new SerializedObject(numbers);
                RequireReference(numbersSo, "expTarget", problems);
            }

            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                var spawnerSo = new SerializedObject(spawner);
                RequireReference(spawnerSo, "damageNumbers", problems);
            }

            var stats = Object.FindObjectsByType<Onikiri.UI.StatPointButton>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (stats.Length != 2)
                problems.Add("Expected 2 stat point axes but found " + stats.Length);

            // 하단 바의 잠긴 탭 + 성장 패널의 전직 안내. 18단계에 전직이 하단에서
            // 성장 패널 탭으로 옮겨가면서 LockedTab이 두 밴드에 나뉘어 산다.
            // 씬 전체로 세면 옮긴 것인지 복사한 것인지 구분이 안 되므로 밴드별로 센다
            const int AwakenNotices = 1;
            var tabs = Object.FindObjectsByType<Onikiri.UI.LockedTab>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (tabs.Length != LockedTabs.Length + AwakenNotices)
                problems.Add("Expected " + (LockedTabs.Length + AwakenNotices)
                             + " locked placeholders but found " + tabs.Length);

            var bottomBar = MainSceneBuilder.FindBand("BottomTabBar");
            if (bottomBar != null)
            {
                int bottomTabs = bottomBar.GetComponentsInChildren<Onikiri.UI.LockedTab>(true).Length;
                if (bottomTabs != LockedTabs.Length)
                    problems.Add("BottomTabBar has " + bottomTabs + " locked tabs but the builder makes "
                                 + LockedTabs.Length + " - a stale '전직' tab would duplicate"
                                 + " the growth panel's 전직 tab");
            }

            // 세션이 레벨을 저장하지 않으면 앱을 껐다 켤 때마다 Lv.1로 돌아간다.
            // 그런데 켠 직후 화면은 정상이라(경험치가 다시 쌓이므로) 며칠 뒤에야
            // "레벨이 안 오른다"로 발견된다
            var session = Object.FindFirstObjectByType<Onikiri.Progression.GameSession>();
            if (session != null)
            {
                var sessionSo = new SerializedObject(session);
                RequireReference(sessionSo, "character", problems);
            }
        }

        /**
         * @brief 씬의 모든 TMP 문자열이 폰트 아틀라스에 구워져 있는지.
         *
         * 새 문구를 UIStrings.txt에 적는 것을 잊으면 화면에 □가 뜬다. 9단계에서
         * 보스 이름이, 11단계에서 사망 문구가 그렇게 깨졌다. 두 번 다 스크린샷을
         * 찍고 나서야 알았다.
         *
         * 눈으로 찾을 것이 아니라 빌드가 대조해야 한다. 씬에 적힌 글자를 전부
         * 모아 구워진 문자셋과 비교한다.
         *
         * **한계**: 코드가 런타임에 조립하는 문구(실패 안내 등)는 씬에 없으므로
         * 여기서 잡히지 않는다. 그것들은 UIStrings.txt에 손으로 적어두는 수밖에
         * 없고, 이 검사는 빌더가 씬에 써넣는 라벨을 담당한다.
         */
        private static void VerifyGlyphCoverage(List<string> problems)
        {
            // 대조 상대는 FontCharset.txt가 아니라 **폰트 애셋의 글리프 표**다.
            // 문자셋 파일은 아틀라스와 따로 갱신될 수 있어서, 파일에는 글자가
            // 있는데 아틀라스에는 없는 상태가 존재한다. 화면에 □가 뜨는지를
            // 실제로 결정하는 것은 아틀라스 쪽이다.
            //
            // 라벨마다 자기 폰트에 물어보므로 한글용/숫자용 폰트가 섞여 있어도
            // 각자 맞는 표를 본다.
            var missingByFont = new SortedDictionary<string, SortedSet<char>>();

            foreach (var label in Object.FindObjectsByType<TMPro.TMP_Text>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label == null || string.IsNullOrEmpty(label.text)) continue;

                if (label.font == null)
                {
                    problems.Add("TMP label has no font: " + label.name);
                    continue;
                }

                foreach (var c in label.text)
                {
                    if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
                    if (label.font.HasCharacter(c, true)) continue;

                    SortedSet<char> set;
                    if (!missingByFont.TryGetValue(label.font.name, out set))
                    {
                        set = new SortedSet<char>();
                        missingByFont[label.font.name] = set;
                    }
                    set.Add(c);
                }
            }

            foreach (var entry in missingByFont)
            {
                var builder = new System.Text.StringBuilder();
                foreach (var c in entry.Value) builder.Append(c);

                problems.Add("Font '" + entry.Key + "' is missing glyphs used in the scene: \""
                             + builder + "\" - add them to " + FontCharsetBuilder.StringsPath
                             + " and run Onikiri/Art/Build Pixel Font Assets");
            }

            VerifyDataAssetNames(problems);
        }

        /**
         * @brief 씬에 없고 **데이터 애셋에만 있는** 이름도 대조한다.
         *
         * 위 검사는 자기 한계를 주석에 적어두고 있었다 - 씬에 적힌 라벨만 본다.
         * 보스 이름은 씬에 없다. `BossConfig`에 있고 등장 연출이 런타임에
         * 읽어가므로, 새 보스를 추가하면 이 검사를 **통과한 채로 □가 뜬다.**
         *
         * 21단계 처형인이 정확히 그랬다. 9단계와 11단계에도 같은 자리에서
         * 깨졌으니 세 번째다. 한계를 적어두는 것으로는 아무것도 막지 못한다.
         *
         * 대조 상대는 한글 라벨이 실제로 쓰는 폰트다. 보스 이름이 다른 폰트로
         * 뜨는 경로는 없다.
         */
        private static void VerifyDataAssetNames(List<string> problems)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(GalmuriFontPath);
            if (font == null) { problems.Add("Galmuri font asset missing: " + GalmuriFontPath); return; }

            var missing = new SortedSet<char>();
            foreach (var name in FontCharsetBuilder.DisplayNames())
            {
                if (string.IsNullOrEmpty(name)) continue;
                foreach (var c in name)
                {
                    if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
                    if (!font.HasCharacter(c, true)) missing.Add(c);
                }
            }

            if (missing.Count == 0) return;

            var builder = new System.Text.StringBuilder();
            foreach (var c in missing) builder.Append(c);

            problems.Add("Font '" + font.name + "' is missing glyphs used by data asset names: \""
                         + builder + "\" - run Onikiri/Art/Rebuild Font Charset "
                         + "then Onikiri/Art/Build Pixel Font Assets");
        }

        private static void RequireReference(SerializedObject so, string field, List<string> problems)
        {
            var property = so.FindProperty(field);
            if (property == null) { problems.Add(so.targetObject.GetType().Name + "." + field + " does not exist"); return; }
            if (property.objectReferenceValue == null) problems.Add(so.targetObject.GetType().Name + "." + field + " is null");
        }

        private static void RequireArray(SerializedObject so, string field, List<string> problems)
        {
            var property = so.FindProperty(field);
            if (property == null || property.arraySize == 0)
                problems.Add(so.targetObject.GetType().Name + "." + field + " is empty");
        }

        private static T LoadPrefabComponent<T>(string path) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null ? go.GetComponent<T>() : null;
        }

        private static Enemy BuildEnemyPrefab()
        {
            var root = new GameObject("Enemy");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingOrders.EnemyBase;
            root.AddComponent<SpriteAnimator>();
            root.AddComponent<Enemy>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Enemy>();
        }

        /**
         * @brief 데미지 팝업 프리팹.
         *
         * 래스터 폰트를 설계 크기의 정확한 정수배로 써서 숫자가 아트와 같은 픽셀 격자를
         * 유지하게 한다.
         */
        private static Onikiri.UI.DamageNumber BuildDamageNumberPrefab()
        {
            // Galmuri가 아니라 Thaleah. 데미지 팝업은 숫자뿐이고 두꺼운 라틴 디스플레이
            // 서체가 어울린다. 한글 UI는 Galmuri를 계속 쓴다
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(ThaleahFontPath);

            var root = new GameObject("DamageNumber", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            // 처치와 치명타는 160pt로 뜬다. 평타(80pt) 기준으로 잡으면 강조 숫자가
            // 잘린다
            rect.sizeDelta = new Vector2(520f, 200f);

            // 그림자를 먼저 만들어 뒤에 그려지게 하고 화면 픽셀 몇 개만큼 밀어둔다.
            // 비트맵 폰트는 SDF 아웃라인을 쓸 수 없고, 그림자가 없으면 흰 참격 위에
            // 올라가는 순간 숫자를 읽을 수 없다.
            //
            // 오프셋은 **글자의 픽셀 하나**다. 아트 픽셀(5)이 아니라 Thaleah 아틀라스의
            // 배율을 쓴다 - 그림자는 글자를 따라가는 것이므로 글자의 격자 위에 놓여야
            // 하고, 다른 격자를 쓰면 그림자 가장자리가 글자 가장자리와 어긋나 지저분해진다
            const float shadowStep = Onikiri.UI.PixelFontSizes.ThaleahScale;
            var shadow = CreateDamageLabel(root.transform, font, "Shadow",
                                           new Vector2(shadowStep, -shadowStep));
            var label = CreateDamageLabel(root.transform, font, "Label", Vector2.zero);

            var popup = root.AddComponent<Onikiri.UI.DamageNumber>();
            var so = new SerializedObject(popup);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("rect").objectReferenceValue = rect;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DamagePrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Onikiri.UI.DamageNumber>();
        }

        private static TMPro.TextMeshProUGUI CreateDamageLabel(
            Transform parent, TMPro.TMP_FontAsset font, string name, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TMPro.TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            label.fontSize = Onikiri.UI.PixelFontSizes.ThaleahDamage;   // 1:1 with the atlas
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = "0";

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(offset.x, offset.y);
            rect.offsetMax = new Vector2(offset.x, offset.y);
            return label;
        }

        // ---------------------------------------------------------------- 씬 배선

        /**
         * @brief 카메라에 리스너가 추가되기 전에 만들어진 씬을 복구한다.
         *
         * 리스너가 없으면 모든 AudioSource가 정상 재생 중이라고 보고하면서도 아무 소리도
         * 나지 않는다.
         */
        private static void EnsureAudioListener()
        {
            var existing = Object.FindFirstObjectByType<AudioListener>();
            if (existing != null) return;

            var camera = Camera.main;
            if (camera == null) { Debug.LogError("[Onikiri] No main camera to attach an AudioListener to."); return; }

            camera.gameObject.AddComponent<AudioListener>();
            Debug.LogWarning("[Onikiri] Scene had no AudioListener - added one to the main camera.");
        }

        /** 카메라에 흔들림을 붙이고, 레이아웃이 그것을 무시하도록 알려준다 */
        private static ScreenShake WireCameraShake()
        {
            var camera = Camera.main;
            var shake = camera.GetComponent<ScreenShake>();
            if (shake == null) shake = camera.gameObject.AddComponent<ScreenShake>();

            var layout = GameObject.Find("Battle").GetComponent<BattleStageLayout>();
            var so = new SerializedObject(layout);
            so.FindProperty("cameraShake").objectReferenceValue = shake;
            so.ApplyModifiedPropertiesWithoutUndo();

            return shake;
        }

        /**
         * @brief 타격음 재생기를 만들고 클립을 채운다.
         *
         * 폴더가 비어 있어도 된다. 전투는 조건 없이 호출하고, 사운드 파일을 넣기 전까지는
         * 조용히 있으므로 나중에 다시 배선할 것이 없다.
         */
        private static HitAudio WireHitAudio()
        {
            var battle = GameObject.Find("Battle");
            var audio = battle.GetComponent<HitAudio>();
            if (audio == null) audio = battle.AddComponent<HitAudio>();

            var hits = LoadClipList(HitClipPaths);
            var kills = LoadClipList(KillClipPaths);

            // 오디오 폴더에 손으로 넣은 것은 선별된 세트에 더해진다
            hits.AddRange(LoadClips(HitAudioFolder));
            kills.AddRange(LoadClips(KillAudioFolder));

            ApplyOneShotImportSettings(hits);
            ApplyOneShotImportSettings(kills);

            var so = new SerializedObject(audio);
            AssignClips(so.FindProperty("hitClips"), hits);
            AssignClips(so.FindProperty("killClips"), kills);
            so.FindProperty("voices").intValue = 4;
            so.FindProperty("minHitInterval").floatValue = 0.04f;
            so.FindProperty("minKillInterval").floatValue = 0.02f;
            so.FindProperty("hitPitchRange").vector2Value = new Vector2(0.94f, 1.06f);
            so.FindProperty("killPitchRange").vector2Value = new Vector2(0.78f, 0.88f);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (hits.Count == 0)
                Debug.LogWarning("[Onikiri] No hit sounds found - combat is silent.");
            else
                Debug.Log("[Onikiri] Sounds wired: " + hits.Count + " hit, " + kills.Count + " kill" +
                          (kills.Count == 0 ? " (kills fall back to pitched-down hits)" : "") +
                          " - mono ADPCM, decompress on load.");

            return audio;
        }

        private static List<AudioClip> LoadClipList(string[] paths)
        {
            var clips = new List<AudioClip>();
            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) { Debug.LogWarning("[Onikiri] Sound not found: " + path); continue; }
                clips.Add(clip);
            }
            return clips;
        }

        /**
         * @brief 짧은 원샷 사운드를 위한 모바일 임포트 설정.
         *
         * 팩은 44.1kHz 스테레오 Vorbis로 배포되는데 이 용도에서는 셋 다 틀렸다.
         * spatialBlend 0으로 재생하므로 두 번째 채널은 버려지고, 1초 미만 클립에
         * Vorbis 디코드 비용은 낭비다. 모노 ADPCM + DecompressOnLoad가 표준 레시피이며
         * 메모리는 약 1/4, 재생 시점 디코드 비용은 없다. 후반에 초당 열 번씩 울릴 때
         * 이 차이가 의미를 갖는다.
         */
        private static void ApplyOneShotImportSettings(List<AudioClip> clips)
        {
            foreach (var clip in clips)
            {
                var path = AssetDatabase.GetAssetPath(clip);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                var settings = importer.defaultSampleSettings;
                bool changed = false;

                if (!importer.forceToMono) { importer.forceToMono = true; changed = true; }
                if (importer.loadInBackground) { importer.loadInBackground = false; changed = true; }

                // preloadAudioData는 플랫폼별 샘플 설정 쪽으로 옮겨졌다
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                    settings.compressionFormat != AudioCompressionFormat.ADPCM ||
                    !settings.preloadAudioData)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.ADPCM;
                    settings.preloadAudioData = true;
                    importer.defaultSampleSettings = settings;
                    changed = true;
                }

                if (changed) importer.SaveAndReimport();
            }
        }

        private static List<AudioClip> LoadClips(string folder)
        {
            var clips = new List<AudioClip>();
            if (!AssetDatabase.IsValidFolder(folder)) return clips;

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) clips.Add(clip);
            }
            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips;
        }

        private static void AssignClips(SerializedProperty array, List<AudioClip> clips)
        {
            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        /**
         * @brief 데미지 팝업은 오버레이 캔버스의 전투 밴드에 둔다.
         *
         * 그래야 전투 위, 상단 바 아래에 놓인다.
         */
        private static Onikiri.UI.DamageNumberSpawner WireDamageNumbers()
        {
            var canvas = GameObject.Find("UI Canvas");
            var band = MainSceneBuilder.FindBand("BattleArea");
            if (band == null) return null;

            var spawner = band.GetComponent<Onikiri.UI.DamageNumberSpawner>();
            if (spawner == null) spawner = band.gameObject.AddComponent<Onikiri.UI.DamageNumberSpawner>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamagePrefabPath);

            var so = new SerializedObject(spawner);
            so.FindProperty("prefab").objectReferenceValue =
                prefab != null ? prefab.GetComponent<Onikiri.UI.DamageNumber>() : null;
            so.FindProperty("container").objectReferenceValue = band;
            so.FindProperty("worldCamera").objectReferenceValue = Camera.main;
            so.FindProperty("canvas").objectReferenceValue = canvas.GetComponent<Canvas>();
            so.FindProperty("prewarm").intValue = 12;

            // 경험치가 빨려 들어갈 곳. 성장 패널 상단 경계의 얇은 스트립이고
            // (2a 후속 - 상단 바에서 내려왔다), WireWalletAndHud가 이미 세워뒀다 -
            // 순서가 뒤바뀌면 여기서 null이 기록된다. 처치 지점에서 **아래로**
            // 떨어지는 연출이 되는데, 흡수처가 화면 아래 패널이니 방향도 맞다
            var growthPanel = MainSceneBuilder.FindBand("GrowthPanel");
            var expStrip = growthPanel != null
                ? growthPanel.Find(UpgradePanelBuilder.ExpStripName) : null;
            so.FindProperty("expTarget").objectReferenceValue = expStrip;
            so.FindProperty("expColor").colorValue = ExpStripFillColor;

            so.ApplyModifiedPropertiesWithoutUndo();

            return spawner;
        }

        /**
         * @brief Battle 루트에 지갑/스테이지를, 상단 바에 초상·재화·스테이지를 세운다.
         *
         * ## 2b 재디자인 - 초상 앵커
         *
         * 37~38단계의 상단 바는 "칩들의 줄 두 개"였다: Ancient 나무 판 테두리의
         * 상자들이 좌우로 흩어져 있었고, 2행은 레벨 칩과 설정뿐이라 성겼다.
         * 좁은 바에서 두꺼운 테두리 상자가 겹겹이 서는 것이 촌스러움의 핵심이었다.
         *
         * 재배치의 축은 **왼쪽의 캐릭터 초상**이다(슬레이어 참고). 초상이 1.5행을
         * 채우고, 그 옆에 재화 트레이(1행)와 스테이지 칩(2행)이 붙는다. 레벨은
         * 텍스트 칩에서 초상 코너의 배지가 됐고, 레벨업 버튼은 성장 패널 헤더로
         * 내려갔다(BuildExpRow) - 상단 바는 "상태", 패널이 "행동"이다.
         *
         * 판은 전부 민짜다. 바탕은 성장 패널과 같은 화지+먹빛이고, 칩은 반 단
         * 밝은 톤온톤(UiSkin.InkChip) - 테두리 없이 색차가 윤곽이다.
         */
        private static void WireWalletAndHud()
        {
            var battle = GameObject.Find("Battle");
            if (battle.GetComponent<Onikiri.Progression.PlayerWallet>() == null)
                battle.AddComponent<Onikiri.Progression.PlayerWallet>();
            if (battle.GetComponent<Onikiri.Progression.StageProgress>() == null)
                battle.AddComponent<Onikiri.Progression.StageProgress>();
            if (battle.GetComponent<Onikiri.Progression.CharacterLevel>() == null)
                battle.AddComponent<Onikiri.Progression.CharacterLevel>();

            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar == null) return;

            // 칩 세대의 잔재. 재화는 트레이 안으로 들어갔고 레벨 칩은 초상
            // 배지가 됐다 - 옛 직속 자식이 남으면 유령 텍스트다(11단계 계열)
            foreach (var staleName in new[]
                     { "GoldIcon", "GoldLabel", "GemIcon", "GemLabel", "LevelButton", "StageLabel" })
            {
                var stale = topBar.Find(staleName);
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }

            BuildTopBarChrome(topBar);
            BuildPortraitAnchor(topBar);

            // ---- 재화 트레이. 골드와 보석이 **한 판** 위에 나란히 선다.
            // 개별 상자 둘이 아니라 얕은 먹빛 웰 하나 - 눌리지 않는 표시라
            // 바탕보다 어둡게 판다(BarTrack). 성장 행과 같은 심볼 언어다
            var tray = EnsureImage(topBar, "CurrencyTray", UiSkin.BarTrack);
            var trayRect = (RectTransform)tray.transform;
            trayRect.anchorMin = trayRect.anchorMax = new Vector2(0f, 1f);
            trayRect.pivot = new Vector2(0f, 1f);
            trayRect.sizeDelta = new Vector2(CurrencyTrayWidth, RowHeight2b);
            trayRect.anchoredPosition = new Vector2(ContentLeft, -RowTop);
            tray.sprite = null;
            tray.type = UnityEngine.UI.Image.Type.Simple;
            tray.raycastTarget = false;

            float x = TrayPad;
            EnsureTrayIcon(tray.transform, "GoldIcon", UiIcons.Load(UiIcons.GoldIcon), x);
            x += BarIconSize + 10f;
            var goldLabel = EnsureTrayLabel(tray.transform, "GoldLabel", x, GoldLabelWidth);
            goldLabel.text = "0";
            x += GoldLabelWidth + 14f;
            EnsureTrayIcon(tray.transform, "GemIcon", UiIcons.LoadItem(UiIcons.GemSprite), x);
            x += BarIconSize + 8f;
            var gemLabel = EnsureTrayLabel(tray.transform, "GemLabel", x, GemLabelWidth);
            gemLabel.text = "0";

            // ---- 스테이지 칩 (2행, 트레이 아래). 깃발 심볼 + "지역 1 · 1/10".
            // "클리어"는 뺐다(HUDStage 주석) - 압축이 이 칩의 2b 몫이다
            var stageButton = EnsureImage(topBar, "StageButton", UiSkin.InkChip);
            var stageButtonRect = (RectTransform)stageButton.transform;
            stageButtonRect.anchorMin = stageButtonRect.anchorMax = new Vector2(0f, 1f);
            stageButtonRect.pivot = new Vector2(0f, 1f);
            stageButtonRect.sizeDelta = new Vector2(StageChipWidth, StageChipHeight);
            stageButtonRect.anchoredPosition = new Vector2(ContentLeft, -Row2Top);
            stageButton.sprite = null;
            stageButton.type = UnityEngine.UI.Image.Type.Simple;

            var stageButtonControl = stageButton.GetComponent<UnityEngine.UI.Button>();
            if (stageButtonControl == null)
                stageButtonControl = stageButton.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyFlatButton(stageButtonControl, stageButton);

            var stageChipIcon = EnsureImage(stageButton.transform, "Icon", UiIcons.Tint);
            var stageChipIconRect = (RectTransform)stageChipIcon.transform;
            stageChipIconRect.anchorMin = stageChipIconRect.anchorMax = new Vector2(0f, 0.5f);
            stageChipIconRect.pivot = new Vector2(0f, 0.5f);
            stageChipIconRect.sizeDelta = new Vector2(StageChipIconSize, StageChipIconSize);
            stageChipIconRect.anchoredPosition = new Vector2(StageChipIconLeft, 0f);
            stageChipIcon.sprite = UiGlyphBuilder.Load(UiGlyphBuilder.Flag);
            stageChipIcon.raycastTarget = false;

            var stageLabel = EnsureHudLabel(stageButton.transform, "StageLabel",
                                            TMPro.TextAlignmentOptions.Center,
                                            new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            UiFonts.Demote(stageLabel);
            var stageLabelRect = (RectTransform)stageLabel.transform;
            stageLabelRect.anchorMin = Vector2.zero;
            stageLabelRect.anchorMax = Vector2.one;
            // 세로는 넓힌다. Ellipsis가 세로 부족으로 한 줄을 통째로 지우는
            // 것을 막는다 - 55pt 시절 경험치 숫자가 통째로 사라졌던 함정이다
            stageLabelRect.offsetMin = new Vector2(StageChipIconLeft + StageChipIconSize + 8f, -14f);
            stageLabelRect.offsetMax = new Vector2(-14f, 14f);
            stageLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            stageLabel.text = "지역 1 · 1/10";

            // ---- 설정 톱니. 우측 유지, 판만 민짜 칩으로
            var settings = EnsureImage(topBar, "SettingsButton", UiSkin.InkChip);
            var settingsRect = (RectTransform)settings.transform;
            settingsRect.anchorMin = settingsRect.anchorMax = new Vector2(1f, 1f);
            settingsRect.pivot = new Vector2(1f, 1f);
            settingsRect.sizeDelta = new Vector2(SettingsSize, RowHeight2b);
            settingsRect.anchoredPosition = new Vector2(-SideMargin, -RowTop);
            settings.sprite = null;
            settings.type = UnityEngine.UI.Image.Type.Simple;

            var settingsButton = settings.GetComponent<UnityEngine.UI.Button>();
            if (settingsButton == null)
                settingsButton = settings.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyFlatButton(settingsButton, settings);

            var staleSettingsLabel = settings.transform.Find("Label");
            if (staleSettingsLabel != null) Object.DestroyImmediate(staleSettingsLabel.gameObject);

            var settingsIcon = EnsureImage(settings.transform, "Icon", UiIcons.Tint);
            var settingsIconRect = (RectTransform)settingsIcon.transform;
            settingsIconRect.anchorMin = settingsIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            settingsIconRect.pivot = new Vector2(0.5f, 0.5f);
            settingsIconRect.sizeDelta = new Vector2(BarIconSize, BarIconSize);
            settingsIconRect.anchoredPosition = Vector2.zero;
            settingsIcon.sprite = UiGlyphBuilder.Load(UiGlyphBuilder.Gear);
            settingsIcon.raycastTarget = false;

            // ---- HUD 컴포넌트 배선
            var hud = topBar.GetComponent<Onikiri.UI.HUDCurrency>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.HUDCurrency>();

            var currencySo = new SerializedObject(hud);
            currencySo.FindProperty("label").objectReferenceValue = goldLabel;
            // "골드"라는 글자는 아이콘이 이미 말한다(15단계부터)
            currencySo.FindProperty("prefix").stringValue = string.Empty;
            currencySo.ApplyModifiedPropertiesWithoutUndo();

            var gemHud = topBar.GetComponent<Onikiri.UI.HUDGems>();
            if (gemHud == null) gemHud = topBar.gameObject.AddComponent<Onikiri.UI.HUDGems>();

            var gemSo = new SerializedObject(gemHud);
            gemSo.FindProperty("label").objectReferenceValue = gemLabel;
            gemSo.ApplyModifiedPropertiesWithoutUndo();

            var stageHud = topBar.GetComponent<Onikiri.UI.HUDStage>();
            if (stageHud == null) stageHud = topBar.gameObject.AddComponent<Onikiri.UI.HUDStage>();

            var stageSo = new SerializedObject(stageHud);
            stageSo.FindProperty("label").objectReferenceValue = stageLabel;
            stageSo.FindProperty("icon").objectReferenceValue = stageChipIcon;
            stageSo.FindProperty("flagSprite").objectReferenceValue =
                UiGlyphBuilder.Load(UiGlyphBuilder.Flag);
            stageSo.FindProperty("skullSprite").objectReferenceValue =
                UiGlyphBuilder.Load(UiGlyphBuilder.Skull);
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            BuildExpRow(topBar);
        }

        /**
         * @brief 상단 바 바탕. Ancient 나무 판을 벗고 성장 패널과 같은 화지+먹빛.
         *
         * 두꺼운 9-슬라이스 테두리가 2b가 지목한 촌스러움의 핵심이었다. 바탕이
         * 하단 UI와 같은 언어(화지 결 x PanelInk)를 쓰면 "위아래 프레임은 같은
         * 먹, 가운데만 씬"으로 화면이 선다. 전투와의 경계에는 하단 UI의
         * TopEdge와 같은 먹선 한 획을 긋는다.
         */
        private static void BuildTopBarChrome(Transform topBar)
        {
            var chrome = EnsureImage(topBar, "Chrome", UiSkin.PanelInk);
            var chromeRect = (RectTransform)chrome.transform;
            chromeRect.anchorMin = Vector2.zero;
            chromeRect.anchorMax = Vector2.one;
            chromeRect.offsetMin = Vector2.zero;
            chromeRect.offsetMax = Vector2.zero;
            chrome.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTextureBuilder.WashiPath);
            chrome.type = UnityEngine.UI.Image.Type.Tiled;
            chrome.color = UiSkin.PanelInk;
            chrome.raycastTarget = false;

            // 판은 라벨보다 뒤에 있어야 한다. 나중에 만든 자식이 위에 그려지므로
            // 맨 앞으로 보낸다
            chrome.transform.SetAsFirstSibling();

            var edge = EnsureImage(topBar, "BottomEdge", UiSkin.PanelEdge);
            var edgeRect = (RectTransform)edge.transform;
            edgeRect.anchorMin = new Vector2(0f, 0f);
            edgeRect.anchorMax = new Vector2(1f, 0f);
            edgeRect.pivot = new Vector2(0.5f, 0f);
            edgeRect.sizeDelta = new Vector2(0f, TopBarEdgeHeight);
            edgeRect.anchoredPosition = Vector2.zero;
            edge.sprite = null;
            edge.type = UnityEngine.UI.Image.Type.Simple;
            edge.raycastTarget = false;
            edge.transform.SetSiblingIndex(1);
        }

        /**
         * @brief 왼쪽의 캐릭터 초상 앵커: 액자 + 경지 초상 + Lv 배지.
         *
         * 초상은 캐릭터 탭(39단계)과 같은 물건이다 - EvolutionAppearance의 idle
         * 첫 프레임을 머리 기준으로 세우는 CharacterTabPortrait를 그대로 쓰고,
         * 칸이 크니 배율만 3배다. 측정이 두 벌이면 두 화면이 따로 논다.
         *
         * 배지가 옛 "레벨 74" 칩을 대체한다. 숫자의 출처는 그대로 LevelHud이고
         * (levelPrefix만 "Lv."로), 탭 = 스탯 창 진입도 옛 레벨 칩의 경로 그대로
         * 다 - 버튼 오브젝트 이름만 PortraitButton으로 바뀌었다(HudScreensBuilder).
         */
        private static void BuildPortraitAnchor(Transform topBar)
        {
            var frame = EnsureImage(topBar, "PortraitButton", UiSkin.InkChip);
            var frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0f, 1f);
            frameRect.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            frameRect.anchoredPosition = new Vector2(SideMargin, -PortraitTop);
            frame.sprite = null;
            frame.type = UnityEngine.UI.Image.Type.Simple;

            var frameButton = frame.GetComponent<UnityEngine.UI.Button>();
            if (frameButton == null)
                frameButton = frame.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyFlatButton(frameButton, frame);

            // 액자 안쪽. 트레이와 같은 어둠(BarTrack)이라 "창"으로 읽히고,
            // 초상의 크림색 옷이 그 위에서 가장 밝다
            var window = EnsureImage(frame.transform, "Window", UiSkin.BarTrack);
            var windowRect = (RectTransform)window.transform;
            windowRect.anchorMin = Vector2.zero;
            windowRect.anchorMax = Vector2.one;
            windowRect.offsetMin = new Vector2(PortraitBorder, PortraitBorder);
            windowRect.offsetMax = new Vector2(-PortraitBorder, -PortraitBorder);
            window.sprite = null;
            window.type = UnityEngine.UI.Image.Type.Simple;
            window.raycastTarget = false;

            WireTopBarPortrait(window);

            // Lv 배지. 초상 아래변에 겹쳐 "이 캐릭터의 레벨"로 붙는다.
            //
            // **가운데 정렬이다.** 처음에는 오른쪽 모서리 겹침(앵커 (1,0) +
            // 오른쪽으로 8px 내밈)이었는데, 배지(132px)가 액자(144px)와 거의
            // 같은 폭이라 모서리 배지가 아니라 "삐뚤어진 아래 띠"로 읽혔다 -
            // 왼쪽 틈 20px 대 오른쪽 내밈 8px의 비대칭이 그대로 보인다
            // (사용자 지적). 폭이 칸의 절반쯤일 때만 모서리 겹침이 성립한다
            var badge = EnsureImage(frame.transform, "LevelBadge", UiSkin.InkChip);
            var badgeRect = (RectTransform)badge.transform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0f);
            badgeRect.sizeDelta = new Vector2(LevelBadgeWidth, LevelBadgeHeight);
            badgeRect.anchoredPosition = new Vector2(0f, -LevelBadgeOverhang);
            badge.sprite = null;
            badge.type = UnityEngine.UI.Image.Type.Simple;
            badge.raycastTarget = false;

            var badgeLabel = EnsureHudLabel(badge.transform, "Label",
                                            TMPro.TextAlignmentOptions.Center,
                                            new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            UiFonts.Demote(badgeLabel);
            var badgeLabelRect = (RectTransform)badgeLabel.transform;
            badgeLabelRect.anchorMin = Vector2.zero;
            badgeLabelRect.anchorMax = Vector2.one;
            badgeLabelRect.offsetMin = new Vector2(4f, -14f);
            badgeLabelRect.offsetMax = new Vector2(-4f, 14f);
            badgeLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            badgeLabel.text = "Lv.1";
        }

        /**
         * @brief 액자 창 안에 경지 초상을 세운다. 캐릭터 탭(39단계)의 재사용이다.
         *
         * WireCharacterTabPortrait와 같은 재료(EvolutionAppearance idle 첫 프레임
         * + 전직 카드의 잉크 실측)를 같은 컴포넌트에 넣는다. 다른 것은 배율(3배 -
         * 칸이 136px로 탭의 두 배)과 머리 여백뿐이다. 초상이 아직 안 구워진
         * 씬에서는 인물 글리프가 자리를 지킨다.
         */
        private static void WireTopBarPortrait(UnityEngine.UI.Image window)
        {
            var stale = window.transform.Find("PortraitMask");
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var appearance = Object.FindFirstObjectByType<Onikiri.Battle.EvolutionAppearance>(
                FindObjectsInactive.Include);

            var maskObject = new GameObject("PortraitMask", typeof(RectTransform));
            maskObject.transform.SetParent(window.transform, false);
            var maskRect = (RectTransform)maskObject.transform;
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = Vector2.zero;
            maskRect.offsetMax = Vector2.zero;
            maskObject.AddComponent<UnityEngine.UI.RectMask2D>();

            var portraitObject = new GameObject("TierPortrait", typeof(RectTransform));
            portraitObject.transform.SetParent(maskObject.transform, false);
            var portraitRect = (RectTransform)portraitObject.transform;
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.pivot = new Vector2(0.5f, 1f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            var portraitImage = portraitObject.AddComponent<UnityEngine.UI.Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            if (appearance == null || appearance.TierCount == 0)
            {
                Debug.LogWarning("[Onikiri] Top bar portrait keeps the person glyph"
                                 + " - run Build Evolution Content first.");
                portraitImage.sprite = UiGlyphBuilder.Load(UiGlyphBuilder.Person);
                portraitImage.color = UiIcons.Tint;
                return;
            }

            var component = window.GetComponent<Onikiri.UI.CharacterTabPortrait>();
            if (component == null)
                component = window.gameObject.AddComponent<Onikiri.UI.CharacterTabPortrait>();

            var so = new SerializedObject(component);
            so.FindProperty("portrait").objectReferenceValue = portraitImage;
            so.FindProperty("pixelScale").floatValue = TopBarPortraitScale;
            so.FindProperty("topInset").floatValue = TopBarPortraitTopInset;

            var portraits = so.FindProperty("tierPortraits");
            var tops = so.FindProperty("tierInkTop");
            var centers = so.FindProperty("tierInkCenter");
            portraits.arraySize = appearance.TierCount;
            tops.arraySize = appearance.TierCount;
            centers.arraySize = appearance.TierCount;

            for (int t = 0; t < appearance.TierCount; t++)
            {
                var frames = appearance.GetTier(t);
                var portrait = frames != null && frames.idle != null && frames.idle.Length > 0
                    ? frames.idle[0] : null;

                portraits.GetArrayElementAtIndex(t).objectReferenceValue = portrait;

                var bounds = UpgradePanelBuilder.MeasurePortraitBounds(portrait);
                tops.GetArrayElementAtIndex(t).floatValue = bounds.z;
                centers.GetArrayElementAtIndex(t).floatValue = bounds.y;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 트레이 안의 재화 아이콘 하나. 세로 가운데 정렬이다 */
        private static void EnsureTrayIcon(Transform tray, string name, Sprite sprite, float x)
        {
            var image = EnsureImage(tray, name, UiIcons.Tint);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(BarIconSize, BarIconSize);
            rect.anchoredPosition = new Vector2(x, 0f);
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.color = sprite != null ? UiIcons.Tint : new Color(1f, 0f, 1f, 0.35f);
            image.raycastTarget = false;
        }

        /** 트레이 안의 재화 숫자 하나. 아이콘 오른쪽, 세로 가운데 */
        private static TMPro.TMP_Text EnsureTrayLabel(Transform tray, string name, float x, float width)
        {
            var label = EnsureHudLabel(tray, name, TMPro.TextAlignmentOptions.Left,
                                       new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(x, 0f);
            label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            return label;
        }

        /**
         * @brief 상단 바 아이콘의 한 변.
         *
         * 16px 아트의 정수배. 48 = 16 x 3. 성장 행(96 = 16 x 6)의 절반이라
         * "같은 심볼의 작은 판"으로 읽힌다.
         */
        private const float BarIconSize = 48f;

        /**
         * @brief 골드 라벨에 잡아두는 폭.
         *
         * 31단계에 보석이 그 오른쪽에 붙으면서 필요해졌다. 최악 문자열은
         * "999.9aa"(축약 단위 두 글자)이고, 55pt 실측 183px -> 44pt는 그 0.8배
         * 언저리다(32/37단계에서 두 번 줄인 역사와 그때의 실측 근거는 git에
         * 있다). 어림이 아니라는 보장은 VerifyExpRowFits가 빌드마다 현재
         * 폰트로 실측하는 것으로 유지된다.
         *
         * 2b: 재화가 트레이 한 판으로 묶이면서 이 폭이 곧 트레이 폭의 일부다.
         * 170으로 줄였다가 빌드 실측(183px)에 걸려 되돌렸다 - 검사가 일한 것이다.
         */
        private const float GoldLabelWidth = 190f;

        /** 보석 라벨 폭. "99,999" 실측 160 + 6. VerifyExpRowFits가 재검한다 */
        private const float GemLabelWidth = 166f;

        // ---------------------------------------------------------------- 2b 상단 바 기하

        /** 좌우 바깥 여백. 초상과 설정 톱니가 이 선에 선다 */
        private const float SideMargin = 32f;

        /** 1행(트레이·설정)의 윗선과 행 높이 */
        private const float RowTop = 32f;
        private const float RowHeight2b = 64f;

        /** 2행(스테이지 칩)의 윗선. 1행 + 간격 16 */
        private const float Row2Top = RowTop + RowHeight2b + 16f;
        private const float StageChipHeight = 56f;

        /**
         * @brief 초상 액자. 두 행(64+16+56=136) 높이에 맞춘 1.5행짜리 정사각이다.
         *
         * 상단 바 컨텐츠는 이 액자가 세로 기준이다 - 옛 2행(레벨 칩 줄)이
         * 빠지면서 남은 성김을 초상이 채우고, 바의 컨텐츠 높이가 192에서
         * 176(배지 끝)으로 조여진다.
         */
        private const float PortraitSize = 144f;
        private const float PortraitTop = 24f;
        private const float PortraitBorder = 4f;

        /** 초상 원본 1픽셀 = 캔버스 3픽셀. 탭(2배)보다 칸이 두 배라 한 단 키운다 */
        private const float TopBarPortraitScale = 3f;
        private const float TopBarPortraitTopInset = 8f;

        /**
         * @brief Lv 배지. "Lv.999"(캡션) 빌드 실측 118px + 안쪽 여백. 108로
         * 어림했다가 걸려 넓혔다. VerifyExpRowFits가 계속 실측한다.
         * 초상 모서리에서 8px 내민다.
         */
        private const float LevelBadgeWidth = 132f;
        private const float LevelBadgeHeight = 44f;
        private const float LevelBadgeOverhang = 8f;

        /** 재화 트레이가 초상 오른쪽에서 시작하는 x. 초상 끝 + 32 */
        private const float ContentLeft = SideMargin + PortraitSize + 32f;

        /** 트레이 안쪽 여백과 전체 폭. 폭은 내용물 산수의 합이다 */
        private const float TrayPad = 16f;
        private const float CurrencyTrayWidth =
            TrayPad + BarIconSize + 10f + GoldLabelWidth + 14f
            + BarIconSize + 8f + GemLabelWidth + TrayPad;

        /** 스테이지 칩. 폭은 "지역 4 · 10/10"(캡션) 실측 + 글리프 자리다 */
        private const float StageChipWidth = 330f;
        private const float StageChipIconSize = 40f;
        private const float StageChipIconLeft = 16f;

        /** 설정 톱니 칩. 정사각에 가까운 한 칸 */
        private const float SettingsSize = 64f;

        /** 전투와의 경계에 긋는 먹선. 하단 UI의 TopEdge와 같은 언어다 */
        private const float TopBarEdgeHeight = 6f;

        /** 레벨업 버튼(성장 패널 헤더)의 폭. "레벨업 99"가 들어간다 */
        private const float LevelUpWidth = 210f;
        private const float LevelUpHeight = 64f;

        /**
         * @brief 경험치 스트립의 채움색.
         *
         * 38단계까지의 채움(0x7CC59A)은 채도가 높아 먹빛 위에서 두툼한 초록
         * 덩어리로 읽혔다 - 그것이 바를 얇게 뺀 이유의 절반이다. 초록의 "경험치"
         * 연상은 유지하되 채도와 명도를 눌러 옥색으로 - 트랙(PanelEdge, 보라
         * 회색)과 붙어도 튀지 않고, 맥동(LevelHud)이 밝힐 여유는 남는 값이다.
         */
        private static readonly Color ExpStripFillColor = new Color32(0x5F, 0x9C, 0x83, 0xFF);


        /**
         * @brief 경험치 스트립 + 레벨업 버튼(성장 패널 헤더) + LevelHud 배선.
         *
         * ## 2b - 레벨업 버튼이 상단 바를 떠났다
         *
         * 상단 바는 "상태"(초상·재화·스테이지)만 남고, "행동"(레벨업)은 그 행동의
         * 결과가 보이는 곳 - 성장 패널 - 으로 갔다. 슬레이어도 레벨업은 패널의
         * EXP 바 옆이다. 자리는 패널 **헤더**의 EXP 스트립 라인 오른쪽 끝이다:
         *
         *   - 서브탭(강화/성장/전직) 어디에서도 보인다. 성장 서브탭 안에 묻으면
         *     다른 탭을 보다가 레벨업하려고 탭을 옮겨야 한다
         *   - 스크롤 밖(패널 직속)이라 목록이 움직여도 제자리다
         *   - 눌리면 스탯 포인트가 생기는데, 그 포인트를 쓰는 성장 탭 배지가
         *     바로 아래 줄이다 - "레벨업 -> 포인트 쓰기"가 시선 한 줄로 이어진다
         *
         * 올릴 수 있을 때만 나타나는 규칙과 스트립 맥동(38b)은 그대로다.
         */
        private static void BuildExpRow(Transform topBar)
        {
            // 옛 세대의 잔재: 상단 바의 경험치 바(2a 전), 레벨 칩 줄의 레벨업
            // 버튼(2b 전). 스트립과 패널 헤더 버튼이 각각 물려받았다
            foreach (var staleName in new[] { "ExpIcon", "ExpTrack", "LevelUpButton" })
            {
                var stale = topBar.Find(staleName);
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }

            var fill = BuildExpStrip();

            // 초상 배지의 레벨 라벨(BuildPortraitAnchor가 세웠다)
            var badgeLabelTransform = topBar.Find("PortraitButton/LevelBadge/Label");
            var levelLabel = badgeLabelTransform != null
                ? badgeLabelTransform.GetComponent<TMPro.TMP_Text>() : null;

            // 레벨업 버튼. 성장 패널 직속이라 PanelChildNames 화이트리스트에
            // 올라 있어야 패널 재빌드가 지우지 않는다
            var panel = MainSceneBuilder.FindBand("GrowthPanel");
            UnityEngine.UI.Image levelUpRoot = null;
            UnityEngine.UI.Button button = null;
            TMPro.TMP_Text levelUpLabel = null;
            if (panel != null)
            {
                levelUpRoot = EnsureImage(panel, "LevelUpButton", UiSkin.Danger);
                var buttonRect = (RectTransform)levelUpRoot.transform;
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
                buttonRect.pivot = new Vector2(1f, 1f);
                buttonRect.sizeDelta = new Vector2(LevelUpWidth, LevelUpHeight);

                // 스트립 **위에 올라선다** (41단계 재배치). 그전에는 라인에
                // 걸쳐 아래로 10px 내려왔는데, 그 10px이 스트립을 덮고 탭
                // 줄과의 여백을 2px까지 좁혀 "삐져나온 상자"로 읽혔다.
                // 이제 밑변이 스트립 윗변(패널 상단)과 정확히 맞닿는다 -
                // 버튼이 스트립에서 자라난 손잡이가 되고, 패널 안(스트립·탭
                // 줄)은 아무것도 덮지 않는다. 위쪽 공간은 전투 화면의 지면
                // 띠라 몬스터도 숫자도 오지 않는 죽은 영역이다
                buttonRect.anchoredPosition = new Vector2(-SideMargin, LevelUpHeight);

                // 동작 버튼이므로 Ancient 판 그대로다. 민짜로 바꾼 것은 상단
                // 바의 "상태" 칩들이지 행동 버튼이 아니다. 틴트는 초록(Good)
                // 대신 붉은색 - 밝은 초록은 판의 질감을 다 눌러 "기본 초록
                // 상자"로 보였고, 이 화면에서 붉은색은 이미 "네 차례"다(성장
                // 탭의 남은 포인트 배지가 같은 UiSkin.Danger를 쓰고, 레벨업이
                // 만드는 것이 정확히 그 포인트다)
                UiSkin.ApplyPanel(levelUpRoot, UiSkin.Panel, UiSkin.Danger);

                button = levelUpRoot.GetComponent<UnityEngine.UI.Button>();
                if (button == null)
                    button = levelUpRoot.gameObject.AddComponent<UnityEngine.UI.Button>();
                UiSkin.ApplyButton(button, levelUpRoot);

                levelUpLabel = EnsureHudLabel(levelUpRoot.transform, "Label",
                                              TMPro.TextAlignmentOptions.Center,
                                              new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
                var levelUpRect = (RectTransform)levelUpLabel.transform;
                levelUpRect.anchorMin = Vector2.zero;
                levelUpRect.anchorMax = Vector2.one;
                levelUpRect.offsetMin = Vector2.zero;
                levelUpRect.offsetMax = Vector2.zero;
                levelUpLabel.text = "레벨업";

                // 목록·탭보다 위에 그려져야 한다. 형제 순서가 곧 그리기 순서다
                levelUpRoot.transform.SetAsLastSibling();
                levelUpRoot.gameObject.SetActive(false);
            }

            var hud = topBar.GetComponent<Onikiri.UI.LevelHud>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.LevelHud>();

            var so = new SerializedObject(hud);
            so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            so.FindProperty("expFill").objectReferenceValue = fill;
            so.FindProperty("levelUpRoot").objectReferenceValue =
                levelUpRoot != null ? levelUpRoot.gameObject : null;
            so.FindProperty("levelUpButton").objectReferenceValue = button;
            so.FindProperty("levelUpLabel").objectReferenceValue = levelUpLabel;
            so.FindProperty("levelPrefix").stringValue = "Lv.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 경험치 스트립 (2a 후속). 성장 패널 최상단, 전투 화면과의 경계.
         *
         * 풀폭 몇 px - 구분선이 곧 진행 바다. 트랙을 바탕의 경계선(TopEdge)과
         * 같은 색으로 깔아서, 채움이 없으면 그냥 경계로 읽히고 채움이 차오르면
         * 경계가 옥색으로 물든다. 숫자는 없다 - 필요한 사람은 레벨 칩으로
         * 스탯 창을 연다.
         *
         * 자리 계산(탭 줄·뷰포트 내려앉음)은 UpgradePanelBuilder가 상수로 안다.
         * 패널 직속 자식 화이트리스트(PanelChildNames)에도 올라 있어 패널
         * 재빌드가 지우지 않는다.
         */
        private static UnityEngine.UI.Image BuildExpStrip()
        {
            var panel = MainSceneBuilder.FindBand("GrowthPanel");
            if (panel == null)
            {
                Debug.LogError("[Onikiri] GrowthPanel band missing - the exp strip has nowhere to go.");
                return null;
            }

            var strip = EnsureImage(panel, UpgradePanelBuilder.ExpStripName, UiSkin.PanelEdge);
            var stripRect = (RectTransform)strip.transform;
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.anchorMax = new Vector2(1f, 1f);
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.sizeDelta = new Vector2(0f, UpgradePanelBuilder.ExpStripHeight);
            stripRect.anchoredPosition = Vector2.zero;
            strip.raycastTarget = false;

            // 채움은 Image.Filled가 아니라 **앵커 폭**으로 그린다(LevelHud가
            // anchorMax.x = 진행률). Filled는 스프라이트가 있어야 도는데
            // (null이면 조용히 통짜 - 보스 체력 바에서 물린 함정), 내장
            // UISprite를 물렸더니 이번엔 둥근 소프트 가장자리가 10px 줄에서
            // 세로 그라데이션으로 읽혔다. 스프라이트 없는 민짜 사각형이
            // 픽셀 아트 위에서 가장 깨끗하고, 흉내낼 것도 없다
            var fill = EnsureImage(strip.transform, "Fill", ExpStripFillColor);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);   // 폭 0 = 빈 바. 런타임이 늘린다
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fill.type = UnityEngine.UI.Image.Type.Simple;
            fill.sprite = null;
            fill.raycastTarget = false;

            return fill;
        }

        private static UnityEngine.UI.Image EnsureImage(Transform parent, string name, Color color)
        {
            var existing = parent.Find(name);
            UnityEngine.UI.Image image;

            if (existing != null)
            {
                image = existing.GetComponent<UnityEngine.UI.Image>();
                if (image == null) image = existing.gameObject.AddComponent<UnityEngine.UI.Image>();
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                image = go.AddComponent<UnityEngine.UI.Image>();
            }

            image.color = color;
            return image;
        }

        private static void WireCharacterToUpgrades(Onikiri.Progression.UpgradeSystem upgrades)
        {
            var character = Object.FindFirstObjectByType<Onikiri.Progression.CharacterLevel>();
            if (character == null || upgrades == null) return;

            var so = new SerializedObject(character);
            so.FindProperty("upgrades").objectReferenceValue = upgrades;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 전진 판단자를 Battle 루트에 붙인다.
         *
         * 보스전 다음에 불러야 한다 - BossFight가 씬에 있어야 페이즈를 물어볼
         * 수 있고, 없으면 배경이 보스 등장 중에도 계속 흐른다.
         */
        private static void WireStageAdvance()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null) return;

            var advance = battle.GetComponent<StageAdvance>();
            if (advance == null) advance = battle.AddComponent<StageAdvance>();

            var samurai = GameObject.Find("Samurai");

            var so = new SerializedObject(advance);
            so.FindProperty("combat").objectReferenceValue =
                samurai != null ? samurai.GetComponent<PlayerCombat>() : null;
            so.FindProperty("bossFight").objectReferenceValue = battle.GetComponent<BossFight>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 하단 탭바에 세워두는 잠긴 탭. 구현은 없고 자리와 조건만 있다 */
        private struct LockedTabSpec
        {
            public string Name;
            public int RequiredLevel;

            /** 스테이지 조건. 0이면 레벨만 쓴다 (32단계의 장비가 이쪽) */
            public int RequiredStage;

            /** 열렸을 때 켤 화면. 비어 있으면 예전처럼 눌리지 않는다 */
            public string ScreenName;

            /**
             * @brief 홈 탭 (38단계 층위 분리). 화면을 토글하지 않고 다른
             * 화면들만 닫는다 - 바탕(GrowthPanel)이 드러나는 것이 곧 홈이다.
             */
            public bool HomeTab;
        }

        /**
         * @brief 하단 탭의 심볼 (38단계 아이콘화).
         *
         * 출처가 셋으로 갈린다: KURAI(스킬 - 오의와 같은 심볼), Kyrise(장비·
         * 퀘스트 - 그 화면이 이미 쓰는 아이콘), 코드 생성 글리프(캐릭터·동료 -
         * 팩에 인물·발자국 계열이 없다. UiGlyphBuilder 참고).
         * 탭과 화면이 같은 심볼을 써야 "이 탭이 그 화면"이 형태로 읽힌다.
         */
        private static Sprite TabIcon(string name)
        {
            switch (name)
            {
                case "캐릭터": return UiGlyphBuilder.Load(UiGlyphBuilder.Person);
                case "스킬": return UiIcons.Load("Icon076");
                case "장비": return UiIcons.LoadItem(Onikiri.Progression.UiSprites.WeaponSprite);
                case "동료": return UiGlyphBuilder.Load(UiGlyphBuilder.Paw);
                case "퀘스트": return UiIcons.LoadItem(UiIcons.QuestSprite);

                // 상점은 보석이다(46단계). 이 화면이 파는 것이 전부 보석으로
                // 사는 것이라, 탭과 상품이 같은 심볼을 쓰면 "저기 가면 보석을
                // 쓴다"가 형태로 읽힌다 - 다른 탭들이 자기 화면의 아이콘을
                // 쓰는 것과 같은 규칙이다
                case "상점": return UiIcons.LoadItem(UiIcons.GemSprite);

                default: return null;
            }
        }

        /**
         * @brief 하단에 남는 잠긴 탭.
         *
         * 18단계에서 **전직이 빠졌다.** 성장 패널의 최상위 탭이 재화로 갈리면서
         * (강화/성장/전직) 전직이 그 줄로 올라갔고, 여기 남겨두면 같은 것이 두
         * 곳에 서 있게 된다 - 둘 중 어느 쪽이 진짜 전직 화면인지 알 수 없다.
         *
         * 스킬은 남는다. 성장 패널의 탭이 아니라 **별개 시스템**이라서다. 전직은
         * 캐릭터 성장의 한 축(포인트/골드와 같은 층위)이지만 스킬은 그렇지 않고,
         * 성장 패널 탭 줄에 올리면 "이 줄은 캐릭터 성장"이라는 규칙이 깨진다.
         *
         * 26단계에 이 탭이 **실제로 눌리게 됐다.** 화면이 생겼기 때문이다
         * (SkillPanelBuilder). 잠금 조건은 코드에서 끌어온다 - 여기 10을 손으로
         * 적어두면 SkillCatalog의 해금 레벨과 갈릴 수 있고, 그러면 탭은 밝은데
         * 목록의 세 줄이 전부 잠긴 화면이 나온다.
         */
        private static readonly LockedTabSpec[] LockedTabs =
        {
            // 38단계의 캐릭터 홈 탭. 강화·성장·전직은 이 화면(GrowthPanel)의
            // **서브탭**이고, 하단 탭은 화면 전환만 한다 - 두 층위가 한 줄에
            // 섞여 보이던 것을 여기서 가른다. 조건 없음(첫 화면이 이곳이다)
            new LockedTabSpec {
                Name = "캐릭터",
                RequiredLevel = 1,
                ScreenName = "GrowthPanel",
                HomeTab = true
            },

            new LockedTabSpec {
                Name = "스킬",
                RequiredLevel = Onikiri.Progression.SkillCatalog.PanelUnlockLevel,
                ScreenName = SkillPanelBuilder.PanelName
            },

            // 32단계의 장비(대장간). **조건이 스테이지다** - 이 탭만 그렇다.
            // 대장간은 지역 1의 랜드마크이고, 화면에 서 있는 건물이 열리는
            // 조건은 "그 지역을 지나왔는가"여야 말이 된다
            // (EquipmentCurve.UnlockStage).
            //
            // 값은 코드에서 끌어온다. 여기 11을 손으로 적어두면 곡선의 해금
            // 스테이지와 갈릴 수 있고, 그러면 탭은 밝은데 두 줄이 전부 잠긴
            // 화면이 나온다 - 스킬 탭에서 같은 이유로 같은 처리를 했다
            new LockedTabSpec {
                Name = "장비",
                RequiredLevel = 1,
                RequiredStage = Onikiri.Progression.EquipmentCurve.UnlockStage,
                ScreenName = EquipmentPanelBuilder.PanelName
            },

            // 동료. **조건이 스테이지다** - 장비와 같은 결이다. 동료는
            // 캐릭터 자신의 성장(전직 Lv.30)이 아니라 여정에서 만나는 존재이고,
            // 지역 3 피날레(st30)를 넘긴 다음 칸(st31)에서 합류한다.
            //
            // st31은 가속 구간의 첫 칸이기도 하다 - 코리더(1~30)에 동료가
            // 구조적으로 없어야 기존 밴드가 무사하다(PetCurve.UnlockStage).
            new LockedTabSpec {
                Name = "동료",
                RequiredLevel = 1,
                RequiredStage = Onikiri.Progression.PetCurve.UnlockStage,
                ScreenName = PetPanelBuilder.PanelName
            },

            // 31단계의 퀘스트. **해금 레벨이 1이다** - 잠그지 않는다.
            //
            // 다른 탭은 "앞으로 무엇이 열리는가"를 보여주려고 잠가 뒀지만
            // 퀘스트는 반대다. 신규 플레이어에게 **다음에 무엇을 할지 알려주는
            // 것**이 이 화면의 목적이고, 그것이 필요한 시점은 레벨 10이 아니라
            // 첫 화면이다.
            new LockedTabSpec {
                Name = "퀘스트",
                RequiredLevel = 1,
                ScreenName = QuestPanelBuilder.PanelName
            },

            // 46단계의 상점. **조건이 스테이지다** - 장비·동료와 같은 결이고
            // 값도 코드에서 끌어온다(GachaCurve.UnlockStage = 요도 해금과
            // 같은 칸). 여기 41을 손으로 적으면 곡선과 갈릴 수 있고, 그러면
            // 탭은 밝은데 배너가 잠긴 화면이 나온다.
            //
            // **여섯째 칸이 된다.** 탭 하나가 216에서 180px로 좁아지는데,
            // 아이콘 48 + 캡션 두 글자("상점")는 그 폭에서도 남는다 -
            // 38단계가 "동료 31스테이지"를 크램하다 아이콘+캡션 2층으로
            // 바꾼 뒤로 탭의 글자는 언제나 두세 글자다.
            //
            // 잠긴 채로도 들어가진다(41단계 미리보기). 상점은 그 규칙이
            // 가장 필요한 화면이다 - 사용자가 "상점이 어디에도 안 보인다"고
            // 지적한 것이 이 스텝의 출발점이고, st41까지 자물쇠만 보여주는
            // 것은 그 지적에 절반만 답하는 것이다
            new LockedTabSpec {
                Name = "상점",
                RequiredLevel = 1,
                RequiredStage = Onikiri.Progression.GachaCurve.UnlockStage,
                ScreenName = ShopPanelBuilder.PanelName
            }
        };

        /**
         * @brief 하단 탭 심볼 크기. 16px 아트의 정수배 (48 = 16 x 3).
         *
         * 41단계에 64에서 한 단 줄였다. 밴드가 10%에서 7.5%로 얇아지면서
         * (DisplayConfig.BottomTabBarTop) 64는 캡션과 합쳐 칸을 꽉 채웠고,
         * 아이콘은 눌리는 영역이 아니라 이름표라 크기가 가독의 전부가
         * 아니다. 정수배 규칙은 그대로다 - 3배가 아닌 값은 픽셀이 운다.
         */
        private const float TabIconSize = 48f;

        /** 탭 판의 안쪽 여백. 밴드가 얇아진 만큼 여백도 한 단 줄인다 */
        private const float TabInset = 10f;

        /** 아이콘 윗변이 탭 판 위에서 내려오는 거리 */
        private const float TabIconTop = 12f;

        /** 캡션 줄의 바닥 여백과 높이. 캡션(33px)에 아래위 숨 쉴 자리다 */
        private const float TabLabelBottom = 6f;
        private const float TabLabelHeight = 40f;

        /**
         * @brief 탭이 하나만 남아도 반쪽 너비를 유지하기 위한 최소 칸 수.
         *
         * 전직이 빠져 탭이 하나가 됐다. 균등 분할 식을 그대로 두면 남은 탭이
         * 폭 1080을 통째로 먹는데, 그러면 "잠긴 탭 하나"가 아니라 **하단 바
         * 자체가 스킬 버튼**으로 보인다. 탭이 늘어날 자리라는 것이 형태에서
         * 사라진다.
         *
         * 두 칸을 최소로 잡고 남은 탭을 가운데에 둔다. 탭이 둘이던 때와 크기가
         * 같으므로 이번 변경으로 하단 바의 생김새는 그대로다.
         */
        private const int MinTabSlots = 2;

        /**
         * @brief 아직 없는 기능의 탭을 하단에 세운다.
         *
         * 강화 탭은 만들지 않는다. 지금 화면 전체가 강화이고, 탭이 하나뿐인
         * 탭바는 탭바가 아니다. 탭 전환은 12-3에서 화면이 여럿이 될 때 생긴다.
         */
        private static void WireLockedTabs()
        {
            var bar = MainSceneBuilder.FindBand("BottomTabBar");
            if (bar == null) return;

            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(GalmuriFontPath);

            // 탭이 줄어든 경우의 잔재를 지운다. 아래 루프는 자기가 만들 이름만
            // 건드리므로 Tab1(옛 전직)은 아무도 손대지 않고 화면에 남는다 -
            // 성장 패널의 유령 행과 정확히 같은 종류의 사고다
            for (int i = bar.childCount - 1; i >= 0; i--)
            {
                var child = bar.GetChild(i);
                if (!child.name.StartsWith("Tab")) continue;

                int index;
                if (int.TryParse(child.name.Substring(3), out index) && index >= LockedTabs.Length)
                    Object.DestroyImmediate(child.gameObject);
            }

            for (int i = 0; i < LockedTabs.Length; i++)
            {
                var spec = LockedTabs[i];

                var background = EnsureImage(bar, "Tab" + i, UiSkin.Row);

                // 잠긴 탭은 **안쪽으로 파인 판**을 쓴다. 색만 죽이면 "지금 못 누른다"와
                // "여기에 뭔가 있다"가 구분되지 않는데, 파인 모양은 그 자리가 아직
                // 비어 있다는 것을 형태로 말한다
                UiSkin.ApplyPanel(background, UiSkin.Inlay, UiSkin.InlayTint);

                var rect = (RectTransform)background.transform;

                // 폭을 균등 분할하되 칸은 최소 둘로 잡고, 실제 탭을 가운데에
                // 모은다. 나중에 탭이 늘어도 이 식은 그대로 쓴다
                float slice = 1f / Mathf.Max(LockedTabs.Length, MinTabSlots);
                float groupLeft = (1f - slice * LockedTabs.Length) * 0.5f;
                rect.anchorMin = new Vector2(groupLeft + i * slice, 0f);
                rect.anchorMax = new Vector2(groupLeft + (i + 1) * slice, 1f);
                rect.offsetMin = new Vector2(TabInset, TabInset);
                rect.offsetMax = new Vector2(-TabInset, -TabInset);

                var button = background.GetComponent<UnityEngine.UI.Button>();
                if (button == null) button = background.gameObject.AddComponent<UnityEngine.UI.Button>();
                UiSkin.ApplyButton(button, background);

                // 아이콘 + 라벨의 2층 구성(38단계). 심볼이 위에서 말하고 글자는
                // 아래에서 캡션 크기로 받친다 - "동료 31스테이지"를 한 줄에
                // 욱여넣던 시절의 넘침이 여기서 사라진다
                var tabIcon = EnsureImage(background.transform, "Icon", Color.white);
                var tabIconRect = (RectTransform)tabIcon.transform;
                tabIconRect.anchorMin = tabIconRect.anchorMax = new Vector2(0.5f, 1f);
                tabIconRect.pivot = new Vector2(0.5f, 1f);
                tabIconRect.sizeDelta = new Vector2(TabIconSize, TabIconSize);
                tabIconRect.anchoredPosition = new Vector2(0f, -TabIconTop);
                tabIcon.raycastTarget = false;
                tabIcon.type = UnityEngine.UI.Image.Type.Simple;

                var label = EnsureHudLabel(background.transform, "Label",
                                           TMPro.TextAlignmentOptions.Center,
                                           new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
                UiFonts.Demote(label);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.offsetMin = new Vector2(0f, TabLabelBottom);
                labelRect.offsetMax = new Vector2(0f, TabLabelBottom + TabLabelHeight);
                label.overflowMode = TMPro.TextOverflowModes.Overflow;
                label.text = spec.Name;

                var tab = background.GetComponent<Onikiri.UI.LockedTab>();
                if (tab == null) tab = background.gameObject.AddComponent<Onikiri.UI.LockedTab>();

                var so = new SerializedObject(tab);
                so.FindProperty("displayName").stringValue = spec.Name;
                so.FindProperty("requiredLevel").intValue = spec.RequiredLevel;
                so.FindProperty("requiredStage").intValue = spec.RequiredStage;
                so.FindProperty("button").objectReferenceValue = button;
                so.FindProperty("label").objectReferenceValue = label;
                so.FindProperty("background").objectReferenceValue = background;
                so.FindProperty("icon").objectReferenceValue = tabIcon;
                so.FindProperty("normalIcon").objectReferenceValue = TabIcon(spec.Name);
                so.FindProperty("lockedIcon").objectReferenceValue =
                    UiGlyphBuilder.Load(UiGlyphBuilder.Lock);
                so.FindProperty("homeTab").boolValue = spec.HomeTab;

                // 켤 화면. 없으면 null이 들어가고 탭은 예전처럼 잠긴 표시만 한다 -
                // 화면을 안 만들고 잠금만 푸는 실수가 성립하지 않는 것이 요점이다
                var screen = string.IsNullOrEmpty(spec.ScreenName)
                    ? null
                    : MainSceneBuilder.FindBand(spec.ScreenName);
                so.FindProperty("screen").objectReferenceValue =
                    screen != null ? screen.gameObject : null;

                if (!string.IsNullOrEmpty(spec.ScreenName) && screen == null)
                    Debug.LogWarning("[Onikiri] Locked tab '" + spec.Name + "' wants screen '"
                                     + spec.ScreenName + "' but it is not in the scene.");

                // 이 둘은 최종 색이 아니라 판에 곱해지는 틴트다. 빌더가 스킨에서
                // 가져와 적어야 팔레트를 바꿀 때 한 곳만 고치면 된다
                so.FindProperty("lockedBackground").colorValue = UiSkin.InlayTint * 0.7f;
                so.FindProperty("unlockedBackground").colorValue = UiSkin.InlayTint;
                so.ApplyModifiedPropertiesWithoutUndo();

                // 잔재 청소가 먼저다. 탭 배치가 바뀐 세대의 배지 컴포넌트가
                // 남아 있으면 엉뚱한 탭이 남의 수를 센다 - 실제로 스킬 탭이
                // 옛 QuestTabBadge를 물고 퀘스트 수령 가능 수를 표기했다.
                // 빌더는 그동안 추가만 하고 지운 적이 없었다
                bool wantsQuestBadge = spec.ScreenName == QuestPanelBuilder.PanelName;
                bool wantsEquipBadge = spec.ScreenName == EquipmentPanelBuilder.PanelName;
                bool wantsPetBadge = spec.ScreenName == PetPanelBuilder.PanelName;

                var staleQuest = background.GetComponent<Onikiri.UI.QuestTabBadge>();
                if (staleQuest != null && !wantsQuestBadge) Object.DestroyImmediate(staleQuest);
                var staleEquip = background.GetComponent<Onikiri.UI.EquipmentTabBadge>();
                if (staleEquip != null && !wantsEquipBadge) Object.DestroyImmediate(staleEquip);
                var stalePet = background.GetComponent<Onikiri.UI.PetTabBadge>();
                if (stalePet != null && !wantsPetBadge) Object.DestroyImmediate(stalePet);

                if (!wantsQuestBadge && !wantsEquipBadge && !wantsPetBadge)
                {
                    var staleBadge = background.transform.Find("Badge");
                    if (staleBadge != null) Object.DestroyImmediate(staleBadge.gameObject);
                }

                // 배지는 **퀘스트와 장비** 둘에 붙는다. 세는 것이 다르다 -
                // 퀘스트는 "받을 것", 장비는 "살 수 있는 것"이다. 규칙은 같다:
                // 탭 버튼의 자식이라 판을 열지 않아도 보이고, 개수를 숫자로 적는다
                if (spec.ScreenName == QuestPanelBuilder.PanelName)
                {
                    var badge = QuestPanelBuilder.BuildBadge(background.transform, font);

                    var badgeComponent = background.GetComponent<Onikiri.UI.QuestTabBadge>();
                    if (badgeComponent == null)
                        badgeComponent = background.gameObject.AddComponent<Onikiri.UI.QuestTabBadge>();

                    var badgeSo = new SerializedObject(badgeComponent);
                    badgeSo.FindProperty("badge").objectReferenceValue = badge.gameObject;
                    badgeSo.FindProperty("label").objectReferenceValue =
                        badge.GetComponentInChildren<TMPro.TMP_Text>(true);
                    badgeSo.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (spec.ScreenName == EquipmentPanelBuilder.PanelName)
                {
                    var badge = QuestPanelBuilder.BuildBadge(background.transform, font);

                    var badgeComponent = background.GetComponent<Onikiri.UI.EquipmentTabBadge>();
                    if (badgeComponent == null)
                        badgeComponent = background.gameObject.AddComponent<Onikiri.UI.EquipmentTabBadge>();

                    var badgeSo = new SerializedObject(badgeComponent);
                    badgeSo.FindProperty("badge").objectReferenceValue = badge.gameObject;
                    badgeSo.FindProperty("label").objectReferenceValue =
                        badge.GetComponentInChildren<TMPro.TMP_Text>(true);
                    badgeSo.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (spec.ScreenName == PetPanelBuilder.PanelName)
                {
                    // 동료 배지. 세는 것은 "누를 수 있는 버튼"(해금·레벨) -
                    // 장비 배지와 같은 자다(PetSystem.AffordableCount)
                    var badge = QuestPanelBuilder.BuildBadge(background.transform, font);

                    var badgeComponent = background.GetComponent<Onikiri.UI.PetTabBadge>();
                    if (badgeComponent == null)
                        badgeComponent = background.gameObject.AddComponent<Onikiri.UI.PetTabBadge>();

                    var badgeSo = new SerializedObject(badgeComponent);
                    badgeSo.FindProperty("badge").objectReferenceValue = badge.gameObject;
                    badgeSo.FindProperty("label").objectReferenceValue =
                        badge.GetComponentInChildren<TMPro.TMP_Text>(true);
                    badgeSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (spec.HomeTab) WireCharacterTabPortrait(background, tabIcon, so);
            }
        }

        /**
         * @brief 캐릭터 탭의 심볼을 현재 경지 초상으로 (39단계).
         *
         * 인물 글리프는 교체 1순위로 적어둔 자리였다(UiGlyphBuilder) - 경지가
         * 올라 사무라이의 모습이 바뀌면 탭의 "나"도 함께 바뀌어야 한다.
         *
         * LockedTab의 글리프 이미지를 뺏지 않고 **전용 이미지를 마스크 안에**
         * 따로 세운다 - 한 이미지를 두 컴포넌트가 서로 덮어쓰는 싸움을 구조로
         * 피한다(CharacterTabPortrait 주석). 글리프는 normalIcon을 비워 끈다.
         * 초상이 아직 안 구워진 씬(Build Evolution Content 전)에서는 글리프를
         * 그대로 둔다 - 심볼 없는 탭보다 낫다.
         */
        private static void WireCharacterTabPortrait(UnityEngine.UI.Image background,
                                                     UnityEngine.UI.Image glyphIcon,
                                                     SerializedObject lockedTabSo)
        {
            var appearance = Object.FindFirstObjectByType<Onikiri.Battle.EvolutionAppearance>(
                FindObjectsInactive.Include);

            // 잔재 정리는 먼저 - 초상이 없어 글리프로 남는 경우에도 옛 마스크가
            // 남아 있으면 안 된다
            var stale = background.transform.Find("PortraitMask");
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            if (appearance == null || appearance.TierCount == 0)
            {
                Debug.LogWarning("[Onikiri] Character tab keeps the person glyph"
                                 + " - run Build Evolution Content first for the portrait.");
                return;
            }

            // 글리프를 끈다. LockedTab.Refresh는 normalIcon이 null이면 이미지를
            // 비활성화한다 - 캐릭터 탭은 잠기지 않으므로 자물쇠로 돌아올 일도 없다
            lockedTabSo.FindProperty("normalIcon").objectReferenceValue = null;
            lockedTabSo.ApplyModifiedPropertiesWithoutUndo();

            // 글리프 아이콘과 같은 자리의 마스크 칸. 초상의 다리가 여기서 잘린다
            var maskObject = new GameObject("PortraitMask", typeof(RectTransform));
            maskObject.transform.SetParent(background.transform, false);

            var glyphRect = (RectTransform)glyphIcon.transform;
            var maskRect = (RectTransform)maskObject.transform;
            maskRect.anchorMin = glyphRect.anchorMin;
            maskRect.anchorMax = glyphRect.anchorMax;
            maskRect.pivot = glyphRect.pivot;
            maskRect.sizeDelta = glyphRect.sizeDelta;
            maskRect.anchoredPosition = glyphRect.anchoredPosition;
            maskObject.AddComponent<UnityEngine.UI.RectMask2D>();

            var portraitObject = new GameObject("TierPortrait", typeof(RectTransform));
            portraitObject.transform.SetParent(maskObject.transform, false);

            var portraitRect = (RectTransform)portraitObject.transform;
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.pivot = new Vector2(0.5f, 1f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            var portraitImage = portraitObject.AddComponent<UnityEngine.UI.Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            var component = background.GetComponent<Onikiri.UI.CharacterTabPortrait>();
            if (component == null)
                component = background.gameObject.AddComponent<Onikiri.UI.CharacterTabPortrait>();

            var so = new SerializedObject(component);
            so.FindProperty("portrait").objectReferenceValue = portraitImage;
            so.FindProperty("pixelScale").floatValue = 2f;
            so.FindProperty("topInset").floatValue = 4f;

            var portraits = so.FindProperty("tierPortraits");
            var tops = so.FindProperty("tierInkTop");
            var centers = so.FindProperty("tierInkCenter");
            portraits.arraySize = appearance.TierCount;
            tops.arraySize = appearance.TierCount;
            centers.arraySize = appearance.TierCount;

            for (int t = 0; t < appearance.TierCount; t++)
            {
                var frames = appearance.GetTier(t);
                var portrait = frames != null && frames.idle != null && frames.idle.Length > 0
                    ? frames.idle[0] : null;

                portraits.GetArrayElementAtIndex(t).objectReferenceValue = portrait;

                // 정렬 기준은 전직 카드와 같은 실측(그려진 픽셀)이다. 측정이
                // 두 벌이면 두 화면이 따로 논다 - 그쪽 헬퍼를 그대로 쓴다
                var bounds = UpgradePanelBuilder.MeasurePortraitBounds(portrait);
                tops.GetArrayElementAtIndex(t).floatValue = bounds.z;
                centers.GetArrayElementAtIndex(t).floatValue = bounds.y;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 하단 탭이 켜는 화면을 다시 물린다. **판을 새로 만든 쪽이 부른다.**
         *
         * ## 조용히 잠기는 탭
         *
         * 화면 판을 세우는 빌더들(대장간·동료·퀘스트·오의)은 자기 판을 지우고
         * 다시 만든다. 그러면 그 판을 가리키던 하단 탭의 참조가 끊기는데,
         * LockedTab은 참조가 없으면 **"아직 구현되지 않았다"로 읽고 탭을 잠근다**
         * (LockedTab.screen 주석 - 화면을 안 만들고 잠금만 푸는 실수를 막으려고
         * 일부러 그렇게 판정한다).
         *
         * 그래서 증상이 "빌더를 돌렸더니 장비 탭이 안 눌린다"로 나온다. 콘솔은
         * 조용하다 - 빌더 입장에서는 판을 세운 것이 성공이고, 끊어진 것은 남의
         * 참조다. 실제로 45c에 물렸다: Build Combat Content로 하단 바를 세운 뒤
         * Build Equipment Panel이 판을 새로 만들자 장비 탭이 잠긴 채 남았다.
         *
         * 판을 만든 쪽이 끝에서 이것을 부르면 **빌더 실행 순서에 대한 의존이
         * 사라진다.** 어느 것을 먼저 돌리든 마지막에 참조가 맞는다.
         *
         * 상호 배타(otherScreens)도 같은 이유로 함께 다시 쓴다 - 새로 만든 판은
         * 남들의 목록에도 없다.
         */
        public static void RelinkScreenTabs()
        {
            var bar = MainSceneBuilder.FindBand("BottomTabBar");
            if (bar == null) return;

            var tabs = bar.GetComponentsInChildren<Onikiri.UI.LockedTab>(true);

            // 표의 순서가 곧 탭의 순서다(BuildBottomTabs가 이 표로 만든다).
            // 수가 어긋나면 짝이 밀리므로 손대지 않고 알린다
            if (tabs.Length != LockedTabs.Length)
            {
                Debug.LogWarning(string.Format(
                    "[Onikiri] BottomTabBar has {0} tabs but the table has {1} - "
                    + "cannot relink screens. Run Build Combat Content.",
                    tabs.Length, LockedTabs.Length));
                return;
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                var spec = LockedTabs[i];
                if (string.IsNullOrEmpty(spec.ScreenName)) continue;

                var screen = MainSceneBuilder.FindBand(spec.ScreenName);
                if (screen == null) continue;

                var so = new SerializedObject(tabs[i]);
                so.FindProperty("screen").objectReferenceValue = screen.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            WireScreenExclusivity();
        }

        /**
         * @brief 같은 띠를 쓰는 화면들의 상호 배타를 배선한다 (38단계).
         *
         * 하단 탭 4화면 + 상단 바 3화면이 전부 성장 띠를 덮는다. 그전에는
         * 여럿이 동시에 켜질 수 있었고 형제 순서로 위의 것만 보였다 - 보이는
         * 상태와 켜진 상태가 달랐다. 이제 어느 하나를 열면 나머지를 닫는다.
         * GrowthPanel은 목록에 없다 - 바탕층이라 항상 켜져 있고, 모두 닫힌
         * 상태가 곧 "캐릭터 화면"이다.
         */
        private static void WireScreenExclusivity()
        {
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            var bar = MainSceneBuilder.FindBand("BottomTabBar");
            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (safeArea == null) return;

            // ⚠️ **새 화면을 이 표에 넣는 것을 잊지 말 것.** 46단계에 상점을
            // 빼먹고 실기에서 물렸다 - 증상이 "상점을 연 뒤 다른 탭이 안
            // 눌린다"였는데, 실제로는 눌렸고 켜진 화면이 상점 **뒤에**
            // 있었다(상점이 마지막에 만들어져 형제 순서상 위에 그려진다).
            // 38단계 주석이 경고한 "보이는 상태와 켜진 상태가 다르다"가
            // 정확히 재현된 것이고, 화면을 만든 빌더가 여기에 이름을 더하지
            // 않으면 그 화면만 영원히 안 닫힌다
            string[] names =
            {
                SkillPanelBuilder.PanelName, EquipmentPanelBuilder.PanelName,
                PetPanelBuilder.PanelName, QuestPanelBuilder.PanelName,
                ShopPanelBuilder.PanelName,
                HudScreensBuilder.StatsPanelName, HudScreensBuilder.RegionSelectPanelName,
                HudScreensBuilder.SettingsPanelName
            };

            var screens = new List<GameObject>();
            foreach (var name in names)
            {
                var found = safeArea.Find(name);
                if (found != null) screens.Add(found.gameObject);
            }

            if (bar != null)
                foreach (var tab in bar.GetComponentsInChildren<Onikiri.UI.LockedTab>(true))
                    FillOtherScreens(new SerializedObject(tab), screens);

            if (topBar != null)
                foreach (var hudButton in topBar.GetComponentsInChildren<Onikiri.UI.HudScreenButton>(true))
                    FillOtherScreens(new SerializedObject(hudButton), screens);
        }

        /** own screen을 뺀 나머지를 otherScreens 배열에 적는다 */
        private static void FillOtherScreens(SerializedObject so, List<GameObject> screens)
        {
            var own = so.FindProperty("screen").objectReferenceValue as GameObject;

            var others = new List<GameObject>();
            foreach (var screen in screens)
                if (screen != own) others.Add(screen);

            var array = so.FindProperty("otherScreens");
            array.arraySize = others.Count;
            for (int i = 0; i < others.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = others[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 상단 바 라벨 하나. 위 기준 앵커라 상단 바 높이가 바뀌어도 위치가 유지된다 */
        private static TMPro.TextMeshProUGUI EnsureHudLabel(
            Transform parent, string name, TMPro.TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
        {
            var existing = parent.Find(name);
            TMPro.TextMeshProUGUI label;

            if (existing != null)
            {
                label = existing.GetComponent<TMPro.TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                label = go.AddComponent<TMPro.TextMeshProUGUI>();
            }

            var rect = (RectTransform)label.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, 1f);
            rect.sizeDelta = new Vector2(560f, 72f);
            rect.anchoredPosition = anchoredPosition;

            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(GalmuriFontPath);
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }

            // 55는 아틀라스를 구운 크기다. 글리프가 비트맵과 1:1로 그려진다
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriSmall;
            label.alignment = alignment;
            label.color = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
            label.raycastTarget = false;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            return label;
        }

        private static readonly Color PopupDimColor = new Color32(0x10, 0x0E, 0x18, 0xD0);
        private static readonly Color PopupBoxColor = new Color32(0x3A, 0x35, 0x50, 0xFF);
        private static readonly Color PopupTextColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);

        /** 아직 못 누르는 것. 잠긴 탭과 같은 색이라 규칙이 화면 전체에서 하나다 */
        private static readonly Color PopupDimTextColor = new Color32(0x5A, 0x51, 0x6B, 0xFF);

        /**
         * @brief 방치 보상 팝업.
         *
         * 안전 영역 루트의 마지막 자식이라 다른 밴드 위에 그려진다. 전체를 덮는
         * 어두운 판이 뒤의 강화 버튼 입력을 막는 역할도 한다 - 팝업이 떠 있는데
         * 뒤가 눌리면 보상을 확인하기 전에 강화가 되어버린다.
         */
        /**
         * @brief 지역 전환 연출을 세우고 배경 스위처에 물린다.
         *
         * 캔버스의 **맨 위**에 둔다. 이 판이 가려야 할 것은 배경 교체인데,
         * 전투 밴드만 덮으면 상단 바와 성장 패널이 그대로 보인 채 그 사이만
         * 어두워져서 "화면이 고장났다"로 읽힌다. 화면 전체를 한 번에 가르는
         * 것이 참격의 뜻이기도 하다.
         *
         * 안전 영역 밖(캔버스 직속)인 이유도 같다 - 노치까지 덮어야 가림이
         * 완결된다.
         */
        private static void WireRegionTransition()
        {
            var canvas = GameObject.Find("UI Canvas");
            if (canvas == null) return;

            var existing = canvas.transform.Find("RegionTransition");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject("RegionTransition", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();
            Stretch((RectTransform)root.transform);

            // 커버. 이 판 하나가 교체를 가린다
            var cover = new GameObject("Cover", typeof(RectTransform));
            cover.transform.SetParent(root.transform, false);
            Stretch((RectTransform)cover.transform);

            var coverImage = cover.AddComponent<UnityEngine.UI.Image>();

            // 검정이 아니라 이 게임의 밤색이다. 순검정은 픽셀 팔레트에 없는
            // 색이라 암전 순간만 다른 게임처럼 보인다
            coverImage.color = new Color32(0x18, 0x14, 0x24, 0x00);

            // 전환 중에는 아래의 버튼을 막아야 한다. 가려진 화면을 누르면
            // 보이지 않는 것이 눌린다
            coverImage.raycastTarget = true;

            /**
             * 참격 띠. 회전한 흰 사각형이다.
             *
             * 전용 아트를 만들지 않는다 - 픽셀 아트에서 대각선 띠는 결국 계단이고,
             * 그것을 스프라이트로 구우면 회전 각도를 바꿀 때마다 다시 구워야 한다.
             * 흰 사각형을 돌리면 각도가 데이터로 남는다.
             */
            var slash = new GameObject("Slash", typeof(RectTransform));
            slash.transform.SetParent(root.transform, false);

            var slashRect = (RectTransform)slash.transform;
            slashRect.anchorMin = slashRect.anchorMax = new Vector2(0.5f, 0.5f);
            slashRect.pivot = new Vector2(0.5f, 0.5f);

            // 화면 대각선보다 길어야 회전해도 양 끝이 화면을 벗어난다
            slashRect.sizeDelta = new Vector2(120f, 4200f);
            slashRect.localRotation = Quaternion.Euler(0f, 0f, 28f);

            var slashImage = slash.AddComponent<UnityEngine.UI.Image>();
            slashImage.color = new Color32(0xFF, 0xFF, 0xFF, 0x00);
            slashImage.raycastTarget = false;

            var transition = root.AddComponent<Onikiri.UI.RegionTransition>();
            var so = new SerializedObject(transition);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("cover").objectReferenceValue = coverImage;
            so.FindProperty("slash").objectReferenceValue = slashRect;
            so.FindProperty("slashImage").objectReferenceValue = slashImage;
            so.ApplyModifiedPropertiesWithoutUndo();

            // **끄지 않는다.** 이 오브젝트에 RegionTransition이 붙어 있는데,
            // 비활성이면 StartCoroutine이 시작조차 되지 않아 전환이 통째로
            // 죽는다. 숨기는 것은 알파가 한다(RegionTransition.Awake)

            // 배경 스위처에 물린다. 이것이 없으면 연출은 있는데 아무도 안 부른다 -
            // 13단계에 보스 등급 배수를 선언만 하고 연결을 잊은 것과 같은 자리다
            var switcher = Object.FindFirstObjectByType<Onikiri.Battle.RegionBackgroundSwitcher>();
            if (switcher != null)
            {
                var switcherSo = new SerializedObject(switcher);
                switcherSo.FindProperty("transition").objectReferenceValue = transition;
                switcherSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Onikiri] RegionBackgroundSwitcher missing - "
                                 + "region transition will never play. Run Build Battle Stage first.");
            }
        }

        private static Onikiri.UI.OfflineRewardPopup WireOfflinePopup()
        {
            var safeArea = UpgradePanelBuilder.EnsureSafeArea();
            if (safeArea == null) return null;

            var existing = safeArea.Find("OfflinePopup");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(GalmuriFontPath);

            var root = new GameObject("OfflinePopup", typeof(RectTransform));
            root.transform.SetParent(safeArea, false);
            root.transform.SetAsLastSibling();
            Stretch((RectTransform)root.transform);

            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            Stretch((RectTransform)dim.transform);
            var dimImage = dim.AddComponent<UnityEngine.UI.Image>();
            dimImage.color = PopupDimColor;

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(root.transform, false);
            var boxRect = (RectTransform)box.transform;
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            // 20단계에 광고 자리가 한 줄 늘면서 560에서 키웠다. 그대로 두면
            // 그 버튼이 박스 아래로 66px 삐져나와 뒤의 강화 행과 겹친다 -
            // 화면에서는 "글자가 두 겹으로 보인다"로만 나타난다
            boxRect.sizeDelta = new Vector2(920f, 700f);
            boxRect.anchoredPosition = Vector2.zero;
            UiSkin.ApplyPanel(box.AddComponent<UnityEngine.UI.Image>(), UiSkin.Row);

            var title = CreatePopupLabel(box.transform, font, "Title", Onikiri.UI.PixelFontSizes.GalmuriLarge, -40f);
            title.text = "오프라인 보상";

            var duration = CreatePopupLabel(box.transform, font, "Duration", Onikiri.UI.PixelFontSizes.GalmuriSmall, -190f);
            duration.text = "자리를 비운 동안  0s";

            var amount = CreatePopupLabel(box.transform, font, "Amount", Onikiri.UI.PixelFontSizes.GalmuriSmall, -280f);
            amount.text = "0 획득했습니다";

            var buttonObject = new GameObject("ClaimButton", typeof(RectTransform));
            buttonObject.transform.SetParent(box.transform, false);
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = new Vector2(360f, 110f);
            buttonRect.anchoredPosition = new Vector2(0f, -400f);

            var buttonImage = buttonObject.AddComponent<UnityEngine.UI.Image>();
            UiSkin.ApplyPanel(buttonImage, UiSkin.Panel, UiSkin.Good);
            var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyButton(button, buttonImage);

            var buttonLabel = CreatePopupLabel(buttonObject.transform, font, "Label",
                                               Onikiri.UI.PixelFontSizes.GalmuriSmall, -20f);
            buttonLabel.text = "받기";

            // 광고 리워드 자리(20-3). 받기 버튼 **아래**에 둔다 - 위에 두면 먼저
            // 눌러야 할 것처럼 보이는데, 지금은 눌리지도 않는다
            var adObject = new GameObject("DoubleButton", typeof(RectTransform));
            adObject.transform.SetParent(box.transform, false);
            var adRect = (RectTransform)adObject.transform;
            adRect.anchorMin = adRect.anchorMax = new Vector2(0.5f, 1f);
            adRect.pivot = new Vector2(0.5f, 1f);
            adRect.sizeDelta = new Vector2(620f, 96f);
            adRect.anchoredPosition = new Vector2(0f, -545f);

            var adImage = adObject.AddComponent<UnityEngine.UI.Image>();

            // 안으로 파인 판. 잠긴 탭이 쓰는 것과 같은 판이라 "여기는 아직
            // 비어 있다"가 형태로 읽힌다
            UiSkin.ApplyPanel(adImage, UiSkin.Inlay, UiSkin.InlayTint * 0.7f);

            var adButton = adObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyButton(adButton, adImage);
            adButton.interactable = false;

            var adLabel = CreatePopupLabel(adObject.transform, font, "Label",
                                           Onikiri.UI.PixelFontSizes.GalmuriSmall, -18f);
            adLabel.text = "골드 2배 준비 중";
            adLabel.color = PopupDimTextColor;

            var popup = root.AddComponent<Onikiri.UI.OfflineRewardPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("durationLabel").objectReferenceValue = duration;
            so.FindProperty("amountLabel").objectReferenceValue = amount;
            so.FindProperty("claimButton").objectReferenceValue = button;
            so.FindProperty("doubleButton").objectReferenceValue = adButton;
            so.FindProperty("doubleLabel").objectReferenceValue = adLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 씬에서는 꺼둔 채로 저장한다. 켜진 채로 저장되면 방치 보상이 없는
            // 실행에서도 팝업이 화면을 가린 채 시작한다
            root.SetActive(false);

            return popup;
        }

        private static TMPro.TextMeshProUGUI CreatePopupLabel(
            Transform parent, TMPro.TMP_FontAsset font, string name, float size, float top)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(24f, top - size * 1.3f);
            rect.offsetMax = new Vector2(-24f, top);

            var label = go.AddComponent<TMPro.TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            label.fontSize = size;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = PopupTextColor;
            label.raycastTarget = false;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /** 세이브/로드와 방치 보상을 담당하는 세션 */
        private static void WireSession(EnemySpawner spawner, Onikiri.UI.OfflineRewardPopup popup,
                                        Onikiri.Progression.SkillSystem skills,
                                        Onikiri.Progression.QuestSystem quests,
                                        Onikiri.Progression.EquipmentSystem equipment)
        {
            var battle = GameObject.Find("Battle");
            var session = battle.GetComponent<Onikiri.Progression.GameSession>();
            if (session == null) session = battle.AddComponent<Onikiri.Progression.GameSession>();

            var samurai = GameObject.Find("Samurai");
            var panel = MainSceneBuilder.FindBand("GrowthPanel");

            var so = new SerializedObject(session);
            so.FindProperty("upgrades").objectReferenceValue =
                panel != null ? panel.GetComponent<Onikiri.Progression.UpgradeSystem>() : null;
            so.FindProperty("stage").objectReferenceValue =
                battle.GetComponent<Onikiri.Progression.StageProgress>();
            so.FindProperty("combat").objectReferenceValue =
                samurai != null ? samurai.GetComponent<PlayerCombat>() : null;
            so.FindProperty("character").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.Progression.CharacterLevel>();
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("offlinePopup").objectReferenceValue = popup;

            // 오의 레벨과 자동 시전 토글이 세이브에 들어간다(v7). 이 줄이 빠지면
            // 세이브 형식은 v7인데 스킬 칸이 매번 비어 있는 상태가 되고, 화면에서는
            // "껐다 켜면 오의 레벨이 1로 돌아간다"로만 나타난다
            so.FindProperty("skills").objectReferenceValue = skills;

            // 퀘스트 진행·수령·보석이 세이브에 들어간다(v8). 빠지면 형식은 v8인데
            // 퀘스트 칸이 매번 비어 있고, 화면에서는 "받았는데 껐다 켜면 다시
            // 받을 수 있다"로 나타난다 - 오의 줄과 같은 종류의 사고다
            so.FindProperty("quests").objectReferenceValue = quests;

            // 장비 등급·단련 레벨이 세이브에 들어간다(v9). 빠지면 등급업에 쓴
            // **보석이 사라진다** - 골드는 다시 벌지만 보석은 퀘스트를 다시
            // 해야 하므로 앞의 두 줄보다 손해가 크다
            so.FindProperty("equipment").objectReferenceValue = equipment;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnemySpawner WireSpawner(List<EnemyDefinition> definitions, Enemy enemyPrefab)
        {
            var battle = GameObject.Find("Battle");
            var enemies = battle.transform.Find("GroundAnchor/Enemies");

            var spawnerObject = battle.transform.Find("EnemySpawner");
            if (spawnerObject == null)
            {
                var go = new GameObject("EnemySpawner");
                go.transform.SetParent(battle.transform, false);
                spawnerObject = go.transform;
            }

            var spawner = spawnerObject.GetComponent<EnemySpawner>();
            if (spawner == null) spawner = spawnerObject.gameObject.AddComponent<EnemySpawner>();

            var so = new SerializedObject(spawner);
            so.FindProperty("stage").objectReferenceValue = battle.GetComponent<BattleStageLayout>();
            so.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            so.FindProperty("enemyParent").objectReferenceValue = enemies;
            so.FindProperty("damageNumbers").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.UI.DamageNumberSpawner>();

            var definitionsProperty = so.FindProperty("definitions");
            definitionsProperty.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
                definitionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];

            so.FindProperty("targetAlive").intValue = 4;
            so.FindProperty("spawnInterval").floatValue = 1.1f;
            // 사무라이가 -1.2에 서므로 큐의 선두도 함께 오른쪽으로 옮긴다.
            // 칼과 선두 요괴 사이에 읽히는 간격을 유지하기 위함
            so.FindProperty("frontLineX").floatValue = 0.05f;
            so.FindProperty("prewarm").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();

            // HitStop을 같은 오브젝트에 둬서 플레이 모드에 반드시 존재하도록 보장한다
            if (battle.GetComponent<HitStop>() == null) battle.AddComponent<HitStop>();

            return spawner;
        }

        private static void WirePlayerCombat(EnemySpawner spawner, ImpactSpark sparkPrefab,
                                             ScreenShake shake, HitAudio hitAudio,
                                             Onikiri.UI.DamageNumberSpawner damageNumbers)
        {
            var samurai = GameObject.Find("Samurai");
            if (samurai == null) { Debug.LogError("[Onikiri] Samurai not found."); return; }

            // 이전에는 idle을 Animator로 돌렸다. 전투는 임팩트 시점을 프레임 단위로
            // 제어해야 하므로, 사무라이도 적과 같은 SpriteAnimator로 옮긴다
            var legacyAnimator = samurai.GetComponent<Animator>();
            if (legacyAnimator != null) Object.DestroyImmediate(legacyAnimator, true);

            var renderer = samurai.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingOrders.Player;

            var animator = samurai.GetComponent<SpriteAnimator>();
            if (animator == null) animator = samurai.AddComponent<SpriteAnimator>();

            var combat = samurai.GetComponent<PlayerCombat>();
            if (combat == null) combat = samurai.AddComponent<PlayerCombat>();

            var vfxRoot = GameObject.Find("Battle").transform.Find("VFX");

            var so = new SerializedObject(combat);
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("sparkPrefab").objectReferenceValue = sparkPrefab;
            so.FindProperty("vfxParent").objectReferenceValue = vfxRoot;
            so.FindProperty("cameraShake").objectReferenceValue = shake;
            so.FindProperty("hitAudio").objectReferenceValue = hitAudio;
            so.FindProperty("damageNumbers").objectReferenceValue = damageNumbers;

            AssignSprites(so.FindProperty("idleFrames"), OrderedSprites(SamuraiIdle));
            AssignSprites(so.FindProperty("attackFrames"), OrderedSprites(SamuraiAttack));
            AssignSprites(so.FindProperty("runFrames"), OrderedSprites(SamuraiRun));

            // 달리기 클립의 재생 속도. **다리 회전 수가 곧 속도감이다.**
            //
            // 17단계에서 배경을 흐르게 만든 뒤 "이동속도가 왜 이리 느리냐"가 나왔고,
            // 처음에는 패럴랙스만 손봤다. 실제로 느렸던 것은 다리였다:
            //
            //   16프레임 @ 12fps = 한 사이클 1.333초
            //   픽셀 차이로 보면 봉우리가 둘(f3, f12)이라 한 사이클 = 두 걸음
            //   => 걸음당 0.667초.  사람 달리기는 0.30~0.35초 - 정확히 절반이다
            //
            // 배경을 아무리 빨리 흘려도 다리가 슬로모션이면 달리기로 안 읽힌다.
            // 24fps면 걸음당 0.333초로 사람 달리기 주기에 맞는다.
            //
            // 덤으로 발 미끄러짐도 준다. 그림 실측 키가 0.97u(96px 박스 안의 31px)라
            // 보폭 0.8키 기준 발이 감당하는 속도는 12fps에서 1.2 u/s인데 지면은
            // 3.2 u/s로 흘렀다 - **2.7배 앞서 있었다.** 24fps면 2.3 u/s가 되어
            // 1.4배로 좁혀진다. 남는 차이는 앞으로 기운 질주로 읽히는 범위다
            // 기준 스크롤이 8.0으로 올라가면서 24fps로는 발이 3.5배 앞섰다.
            // 32fps면 사이클 0.5초 = 초당 네 걸음으로, 사람 전력질주(초당 4~4.5걸음)의
            // 상한이고 미끄러짐도 2.6배로 좁는다. 16프레임 클립으로 갈 수 있는 끝이다
            so.FindProperty("runFrameRate").floatValue = 32f;

            // **참격 프레임은 배선하지 않는다.** 참격은 위의 attackFrames 안에 이미 있다 -
            // 원화가가 5번째 프레임에 칼 궤적을 그려 넣었고, 그것이 스프라이트의 일부라
            // 칼과 어긋날 수가 없다. 22단계까지 그 위에 별도 팩 아크를 한 장 더 얹고
            // 있었던 것이 "이펙트가 붕 뜬다"의 원인이었다.
            //
            // 이 자리에 남는 것은 맞은 지점의 작은 불꽃뿐이다
            AssignSprites(so.FindProperty("sparkFrames"), ImpactSparkBuilder.OrderedSprites());

            // 튜닝 값을 스크립트 기본값에 맡기지 않고 명시적으로 기록한다. 컴포넌트가
            // 이미 씬에 존재하므로 코드의 기본값을 바꿔도 전달되지 않고, 그러면 빌더가
            // 단일 출처 역할을 못 하게 된다
            so.FindProperty("attackRange").floatValue = 2.0f;
            so.FindProperty("attacksPerSecond").floatValue = (float)AttackSpeedCurve.BaseValue;

            // 프레임레이트를 명시적으로 기록한다. 이 값과 프레임 수가 곧 공격속도
            // 상한이므로(AttackSpeedCurve), 씬의 값이 코드가 가정한 값과 조용히
            // 어긋나면 강화 상한이 근거를 잃는다
            so.FindProperty("attackFrameRate").floatValue = AttackSpeedCurve.AttackFrameRate;
            SetBigDouble(so.FindProperty("damage"), 5d);
            // 사무라이 아트에는 7프레임 중 5~6번에 이미 흰 검격 궤적이 그려져 있다.
            // 임팩트를 그 프레임에 맞춰서 그려진 궤적, 참격 이펙트, 피격 플래시, 정지가
            // 순차가 아니라 동시에 일어나게 한다
            so.FindProperty("impactPoint").floatValue = 4f / 7f;
            // 치명타는 강화 곡선에 속하지 않지만 DPS 계산에는 들어간다. 시뮬레이션이
            // 쓰는 값과 씬의 값이 갈리면 밸런스 판정이 조용히 어긋난다. CombatBaseline 참고
            so.FindProperty("critChance").floatValue = CombatBaseline.CritChance;
            so.FindProperty("critMultiplier").floatValue = CombatBaseline.CritMultiplier;

            so.FindProperty("hitStopSeconds").floatValue = 0.07f;
            so.FindProperty("hitStopBudgetPerSecond").floatValue = 0.3f;
            so.FindProperty("shakeSeconds").floatValue = 0.1f;
            so.FindProperty("shakeBudgetPerSecond").floatValue = 0.4f;
            so.FindProperty("shakePixels").floatValue = 3f;
            // 불꽃은 네 프레임짜리라 30fps면 0.133초다. 걷어낸 아크(4프레임 22fps =
            // 0.18초)보다 짧다 - 아크는 '베였다'를 보여주는 것이라 눈이 따라갈 시간이
            // 필요했지만, 불꽃은 '여기 맞았다'를 찍는 것이라 짧을수록 날카롭다
            so.FindProperty("sparkFrameRate").floatValue = 30f;
            so.FindProperty("sparkBudgetPerSecond").floatValue = 0.45f;

            // 6 -> 12 (51단계). 무기 티어의 평타 오라가 타격마다 불꽃을 한 장 더
            // 꺼내므로 동시 사용량이 두 배다 - 실측으로 프리웜 6에서 성장이
            // 8회 잡혔다(SparkPoolGrowthCount). 성장은 한 번뿐이지만 그 한 번이
            // 전투 중의 Instantiate라, 미리 세워 두는 쪽이 규칙이다
            so.FindProperty("sparkPrewarm").intValue = 12;

            // 요괴 중심에서 앞면 쪽으로 0.22u. 잡몹의 그려진 반너비가 0.27~0.5u쯤이라
            // 몸 안쪽 가장자리에 얹힌다 - 칼이 몸에 박히는 자리다. 부호는 여기서 정하지
            // 않는다. Enemy.FacingDirection이 artFacesLeft에서 끌어온다
            so.FindProperty("sparkOffset").vector2Value = new Vector2(0.22f, 0f);

            // 꽃잎은 베는 방향으로 흩어진다. 사무라이의 발도는 오른쪽 위로 향하고,
            // 그 방향이 스프라이트에 그려진 흰 궤적과 같아야 한 동작으로 읽힌다
            var sakura = SakuraContentBuilder.Wire(vfxRoot);
            so.FindProperty("sakura").objectReferenceValue = sakura;
            so.FindProperty("slashDirection").vector2Value = new Vector2(1f, 0.45f);

            so.ApplyModifiedPropertiesWithoutUndo();

            WirePlayerHealth(samurai, animator, sakura);

            Debug.Log(string.Format(
                "[Onikiri] Player combat: idle={0} attack={1} (참격 내장) spark={2} frames.",
                OrderedSprites(SamuraiIdle).Count,
                OrderedSprites(SamuraiAttack).Count,
                ImpactSparkBuilder.OrderedSprites().Count));
        }

        private const string SamuraiHurt = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/HURT.png";
        private const string SamuraiDeath = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/DEATH.png";

        /**
         * @brief 플레이어의 체력. 보스만 이것을 깎는다.
         *
         * 피격/사망 애니메이션은 Mattz 팩에 이미 있다(HURT 4프레임, DEATH 9프레임).
         * 새 아트를 만들지 않았다.
         */
        private static void WirePlayerHealth(GameObject samurai, SpriteAnimator animator, SakuraBurst sakura)
        {
            var health = samurai.GetComponent<PlayerHealth>();
            if (health == null) health = samurai.AddComponent<PlayerHealth>();

            var so = new SerializedObject(health);
            so.FindProperty("spriteRenderer").objectReferenceValue = samurai.GetComponent<SpriteRenderer>();
            so.FindProperty("animator").objectReferenceValue = animator;

            AssignSprites(so.FindProperty("hurtFrames"), OrderedSprites(SamuraiHurt));
            AssignSprites(so.FindProperty("deathFrames"), OrderedSprites(SamuraiDeath));

            so.FindProperty("hurtFrameRate").floatValue = 14f;
            so.FindProperty("deathFrameRate").floatValue = 10f;
            so.FindProperty("hurtFlashSeconds").floatValue = 0.14f;

            // 쓰러짐 연출은 타격용 꽃잎 발생기를 그대로 쓴다. 새 이펙트를 만들지
            // 않고 있는 것을 겹쳐 쓰는 쪽이 화면의 언어를 하나로 유지한다
            so.FindProperty("sakura").objectReferenceValue = sakura;
            so.FindProperty("deathPetalBursts").intValue = 3;

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(string.Format("[Onikiri] Player health: hurt={0} death={1} frames.",
                OrderedSprites(SamuraiHurt).Count, OrderedSprites(SamuraiDeath).Count));
        }

        /**
         * @brief BigDouble을 직렬화된 가수부/지수부 필드로 기록한다.
         *
         * SerializedProperty는 이 구조체 자체를 알지 못하기 때문이다.
         */
        private static void SetBigDouble(SerializedProperty property, double value)
        {
            var big = BigDouble.FromDouble(value);
            property.FindPropertyRelative("m").doubleValue = big.Mantissa;
            property.FindPropertyRelative("e").longValue = big.Exponent;
        }

        private static void AssignSprites(SerializedProperty array, List<Sprite> sprites)
        {
            array.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static List<Sprite> OrderedSprites(string sheetPath)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            // 빈 격자 셀은 슬라이싱 시점에 걸러지므로, 여기 남은 것은 전부 그려진 아트다
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

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
