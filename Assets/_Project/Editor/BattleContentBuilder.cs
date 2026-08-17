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

            // 랭킹도 상단 바에서 열리는 화면이라 같은 자리다(54단계). 여기서
            // 함께 짓지 않으면 상단 바를 다시 만들 때마다 랭킹 칩의 배선만
            // 죽는다 - 칩은 새로 생기는데 그것을 물려줄 빌더가 안 돌기 때문이다
            LeaderboardPanelBuilder.Build();

            // 상호 배타 배선은 **모든 화면이 만들어진 뒤** 마지막이다. 패널
            // 빌더들이 자기 판을 지우고 다시 만들므로, 먼저 배선하면 죽은
            // 참조가 남는다
            WireScreenExclusivity();

            // 세션은 마지막이다. 강화·스테이지·전투가 전부 자리를 잡은 뒤라야
            // 세이브를 복원할 대상을 찾을 수 있다
            var offlinePopup = WireOfflinePopup();
            WireRegionTransition();
            WireSession(spawner, offlinePopup, skills, quests, equipment);

            // 부팅 오버레이는 세션 다음이다 - 로딩 게이트가 GameSession.IsLoaded를
            // 보므로 그 참조를 물릴 대상이 씬에 있어야 한다. 상호 배타 목록에는
            // 안 들어간다(성장 띠 화면이 아니라 전체 화면 일회성 덮개다)
            IntroScreenBuilder.Build();

            // 퀘스트 아이콘의 판 참조 (#16). **여기서 한 번 더 물린다** -
            // 아이콘은 상단 바와 함께(=퀘스트 판보다 먼저) 만들어지고,
            // 판 빌더가 부르는 RelinkScreenTabs는 그 시점에 하단 탭 수가
            // 아직 옛 세대라 통째로 조기 반환할 수 있다. 그 경로에 기대면
            // 아이콘이 아무것도 안 여는 채로 저장된다
            RelinkQuestButton();

            // 층위를 확정한다. **모든 화면이 만들어진 뒤**여야 한다 - 팝업들은
            // 각자의 빌더가 세우므로 그전에 올리면 아직 없는 것을 못 올린다
            // (상호 배타 배선이 마지막인 것과 같은 이유)
            RaisePopupLayer();

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
                new[] { "SettingsButton", HudScreensBuilder.SettingsPanelName },
                new[] { "RankingButton", LeaderboardPanelBuilder.PanelName }
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

            // 초상은 홈 버튼이다(개선안 v2). 화면 참조 없이 다른 화면들만
            // 닫아야 하고, 옛 세대(스탯 창)의 참조가 남아 있으면 초상을
            // 누를 때 스탯 창이 열려 알림 점의 안내(캐릭터 패널)와 어긋난다
            var portraitObject = topBar.Find("PortraitButton");
            var portraitControl = portraitObject != null
                ? portraitObject.GetComponent<Onikiri.UI.HudScreenButton>() : null;
            if (portraitControl == null)
                problems.Add("PortraitButton has no HudScreenButton - it would do nothing");
            else
            {
                var portraitSo = new SerializedObject(portraitControl);
                if (!portraitSo.FindProperty("homeButton").boolValue)
                    problems.Add("PortraitButton is not in home mode - run Build HUD Screens");
                if (portraitSo.FindProperty("screen").objectReferenceValue != null)
                    problems.Add("PortraitButton still opens a screen - the stats entrance "
                                 + "moved to the growth panel's LevelHeader");
            }

            // 스탯 창 입구가 초상에서 성장 패널 헤더로 옮겨갔다. 입구가 어디에도
            // 없으면 정확한 경험치·스탯을 볼 자리가 게임에서 사라진다
            var growthBand = MainSceneBuilder.FindBand("GrowthPanel");
            var headerEntry = growthBand != null
                ? growthBand.Find(UpgradePanelBuilder.LevelHeaderName) : null;
            var headerControl = headerEntry != null
                ? headerEntry.GetComponent<Onikiri.UI.HudScreenButton>() : null;
            var headerScreen = headerControl != null
                ? new SerializedObject(headerControl).FindProperty("screen")
                      .objectReferenceValue as GameObject
                : null;
            if (headerScreen == null || headerScreen.name != HudScreensBuilder.StatsPanelName)
                problems.Add("LevelHeader is not wired to " + HudScreensBuilder.StatsPanelName
                             + " - the stats screen would have no entrance");

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
                if (spec.Area == Onikiri.Progression.SkillArea.Pierce && !usesSlash && !usesStreak)
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

            // 2행에서 스테이지 칩과 랭킹 칩이 다투지 않는지 (54단계). 1행의
            // 트레이/톱니 검사와 같은 자, 같은 이유다.
            //
            // 퀘스트는 이 줄에 없다 - 잠깐 여기 세웠다가 전투 화면으로
            // 되돌렸다(#16은 "플레이 화면에 떠 있는 아이콘"을 요구했고
            // 상단 바는 그 화면이 아니다). BuildQuestButton 주석 참고
            float stageChipRight = ContentLeft + StageChipWidth;
            float rankingLeft = DisplayConfig.DesignWidth - SideMargin - RankingChipWidth;
            if (stageChipRight + 12f > rankingLeft)
                problems.Add(string.Format(
                    "Top bar row 2 overlaps: the stage chip ends at {0:F0}px but the "
                    + "ranking chip starts at {1:F0}px", stageChipRight, rankingLeft));

            // 랭킹 칩의 글자 폭 검사는 사라졌다 (#15) - 글자가 없다. 대신
            // 트로피 글리프가 실제로 붙었는지를 본다. 스프라이트가 없으면
            // (글리프를 안 구웠으면) 빈 칩이 되고, 그건 화면에서 "누를 것이
            // 없다"로 읽힌다
            var rankingGlyph = topBar.Find("RankingButton/Icon");
            if (rankingGlyph == null)
                problems.Add("Ranking chip has no Icon - #15 trophy glyph missing");
            else if (rankingGlyph.GetComponent<UnityEngine.UI.Image>().sprite == null)
                problems.Add("Ranking chip icon has no sprite"
                             + " - run Onikiri/Art/Build UI Glyphs to bake the trophy");

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

            // ---- 레벨 헤더와 레벨업 버튼 (개선안 v2: 캐릭터 패널 안)

            var growthPanel = MainSceneBuilder.FindBand("GrowthPanel");
            var hudLayer = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);

            // 전투 화면에 버튼이 남아 있으면 개편 전 세대의 잔재다 - 버튼이
            // 두 벌이 되고, "전투 화면에서 레벨업 버튼을 뺀다"가 무효가 된다
            if (hudLayer != null && hudLayer.Find("LevelUpButton") != null)
                problems.Add("LevelUpButton is still under SafeArea - it moved into the "
                             + "growth panel's LevelHeader. Run Build Combat Content.");

            var levelHeader = growthPanel != null
                ? growthPanel.Find(UpgradePanelBuilder.LevelHeaderName) : null;
            var levelUp = levelHeader != null ? levelHeader.Find("LevelUpButton") : null;
            if (levelHeader == null)
                problems.Add("LevelHeader missing under GrowthPanel - run Build Combat Content");
            else if (levelUp == null)
                problems.Add("LevelUpButton missing under LevelHeader - run Build Combat Content");
            else
            {
                CheckLabelFits(growthPanel,
                               UpgradePanelBuilder.LevelHeaderName + "/LevelUpButton/Label",
                               "레벨업 99", LevelUpWidth, problems);

                // 헤더 라벨의 최악값. 좌우 여백 48x2, 안쪽 16, 버튼 몫을 뺀
                // 상자에 들어가야 한다(BuildExpRow의 배치 그대로)
                CheckLabelFits(growthPanel,
                               UpgradePanelBuilder.LevelHeaderName + "/Label",
                               "Lv.999 · EXP 100%",
                               DisplayConfig.DesignWidth - 48f * 2f - 16f - (LevelUpWidth + 24f),
                               problems);

                // 패널 직속에 남은 더 옛 세대의 버튼
                if (growthPanel.Find("LevelUpButton") != null)
                    problems.Add("A stale LevelUpButton is still directly under GrowthPanel");
                if (levelUp.gameObject.activeSelf)
                    problems.Add("LevelUpButton is saved active - it must start hidden and "
                                 + "only appear when a level-up is pending (LevelHud)");
            }

            // 경험치 스트립(2a 후속). 상단 바가 아니라 성장 패널 최상단에 있다.
            // 채움은 앵커 폭 방식이라, 빈 상태(anchorMax.x=0)와 민짜 사각형
            // (스프라이트 없음 - UISprite의 소프트 가장자리는 얇은 줄에서
            // 그라데이션으로 읽힌다)을 빌드가 대조한다
            var stripFill = hudLayer != null
                ? hudLayer.Find(UpgradePanelBuilder.ExpStripName + "/Fill") as RectTransform : null;
            var stripImage = stripFill != null
                ? stripFill.GetComponent<UnityEngine.UI.Image>() : null;
            if (stripImage == null)
                problems.Add("Exp strip missing under SafeArea - run Build Combat Content");
            else if (growthPanel != null
                     && growthPanel.Find(UpgradePanelBuilder.ExpStripName) != null)
                problems.Add("A stale exp strip is still under GrowthPanel (#2)");
            else
            {
                if (stripImage.sprite != null)
                    problems.Add("Exp strip fill has a sprite - soft edges read as a gradient "
                                 + "on a 10px line. It must be a plain quad.");
                if (stripFill.anchorMax.x > 0f)
                    problems.Add("Exp strip fill is not empty in the built scene - "
                                 + "LevelHud drives anchorMax.x at runtime and starts from 0");
            }

            VerifyGuideCard(hudLayer, problems);
        }

        /**
         * @brief 가이드 카드가 제 칸을 지키는지 (슬레이어식 우측 패널).
         *
         * 재는 것은 둘이다: 폭이 요구 상한(화면의 40%)을 안 넘는지, 자리가
         * 퀘스트 아이콘 바로 아래인지(아이콘과 겹치면 카드가 입구를 가린다).
         * 문구 검사는 그대로 표 전체를 돌린다 - 최악의 문자열을 손으로
         * 고르면 표에 줄이 늘어나는 순간 낡는다.
         */
        private static void VerifyGuideCard(Transform hudLayer, List<string> problems)
        {
            var card = hudLayer != null ? hudLayer.Find(GuideCardName) as RectTransform : null;
            if (card == null)
            {
                problems.Add("GuideQuestCard missing under SafeArea - run Build Combat Content");
                return;
            }

            if (card.GetComponent<Onikiri.UI.GuideQuestCard>() == null)
                problems.Add("GuideQuestCard has no GuideQuestCard component - the card would "
                             + "sit on screen showing the builder's placeholder text forever");

            // 폭 상한: 화면의 40%(요구 범위 35~40%). 넘으면 전투 가운데로
            // 밀고 들어와 사무라이·요괴를 덮기 시작한다
            if (GuideCardWidth > DisplayConfig.DesignWidth * 0.40f + 0.5f)
                problems.Add(string.Format(
                    "The guide card is {0:F0}px wide - past 40% of the screen ({1:F0}px)",
                    GuideCardWidth, DisplayConfig.DesignWidth * 0.40f));

            /**
             * @brief 위치 검사는 **씬에 실제로 서 있는 자리**를 잰다.
             *
             * 위치가 에디터 소유가 되면서(BuildGuideQuestCard 주석) 상수로는
             * 잴 수 없다 - 사람이 어떤 앵커·피벗으로 옮겨놨든 월드 코너를
             * SafeArea 좌표로 되돌리면 같은 틀에서 비교할 수 있다. 밴드는
             * 비율로, 칩·버튼은 px로 서 있으므로 아래 상수들은 화면 높이가
             * 달라도 유효하다.
             */
            Canvas.ForceUpdateCanvases();
            var safeRect = hudLayer as RectTransform;
            var cardCorners = new Vector3[4];
            card.GetWorldCorners(cardCorners);
            Vector2 cardBL = safeRect.InverseTransformPoint(cardCorners[0]);
            Vector2 cardTR = safeRect.InverseTransformPoint(cardCorners[2]);
            float cardLeft = cardBL.x - safeRect.rect.xMin;
            float cardRightEdge = cardTR.x - safeRect.rect.xMin;
            float cardBottom = cardBL.y - safeRect.rect.yMin;
            float cardTop = cardTR.y - safeRect.rect.yMin;

            float safeW = safeRect.rect.width;
            float battleTop = safeRect.rect.height * DisplayConfig.BattleAreaTop;
            float battleBottom = safeRect.rect.height * DisplayConfig.GrowthPanelTop;

            // 화면·전투 영역 밖으로 나갔는가
            if (cardLeft < 0f || cardRightEdge > safeW || cardTop > battleTop)
                problems.Add(string.Format(
                    "The guide card left the battle area (L{0:F0} R{1:F0} T{2:F0} "
                    + "vs width {3:F0}, battle top {4:F0})",
                    cardLeft, cardRightEdge, cardTop, safeW, battleTop));

            // 퀘스트 아이콘(오른쪽 여백 32, 96px, 전투 윗변 -16~-112)을 가리는가
            float iconLeft = safeW - SideMargin - QuestButtonSize;
            float iconBottom = battleTop - 16f - QuestButtonSize;
            if (cardRightEdge > iconLeft && cardTop > iconBottom)
                problems.Add(string.Format(
                    "The guide card top ({0:F0}) overlaps the quest icon (bottom {1:F0}) "
                    + "- move it down in the editor", cardTop, iconBottom));

            // 보스 도전 버튼(가운데 폭 440, 전투 윗변 -24~-148 -
            // BossContentBuilder)을 무는가. 가로로 겹칠 때만 문제다
            float bossLeft = safeW * 0.5f - 220f;
            float bossRight = safeW * 0.5f + 220f;
            float bossBottom = battleTop - 24f - 124f;
            if (cardTop > bossBottom && cardLeft < bossRight && cardRightEdge > bossLeft)
                problems.Add(string.Format(
                    "The guide card top ({0:F0}) bites the boss challenge button "
                    + "(bottom {1:F0}) - move it down in the editor", cardTop, bossBottom));

            // 지면 근접은 **에러가 아니라 경고**다. 위치가 에디터 소유가 된
            // 뒤로 지면 가까이 두는 것은 사람의 선택일 수 있다(옛 하단 바가
            // 서 있던 죽은 영역이 정확히 그 근처다). 다만 카드가 지면선
            // 위로 올라온 만큼은 오른쪽 끝을 걷는 요괴의 발과 겹칠 수
            // 있으므로, 어디까지 올라와 있는지는 알려준다
            float groundLine = battleBottom + 120f;
            float aboveGround = cardTop - groundLine;
            if (cardBottom < groundLine && aboveGround > 0f)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Guide card straddles the ground line - its top rises {0:F0}px "
                    + "into the monster strip. Fine if intended (editor-owned position).",
                    aboveGround));

            /**
             * @brief 표에 실제로 적힌 문구가 다 들어가는지.
             *
             * 최악의 문자열을 손으로 골라 적지 않고 **표 전체를 돌린다.**
             * 손으로 고른 값은 표에 줄이 하나 늘어나는 순간 낡고, 그 낡음은
             * 에러가 아니라 실기 화면에서 잘린 글자로만 나타난다 - 가이드
             * 표가 인덱스를 손으로 안 적는 것과 같은 이유다
             * (GuideStep.Index 주석).
             */
            float tagBox = GuideCardWidth - GuideCardPad * 2f;
            // 완료 상태의 가운데 줄이 가장 좁다 - 받기 버튼이 폭을 나눠 쓴다.
            // 진행 중에는 같은 목표명이 카드 폭 전부를 쓰므로 이것만 재면 된다
            float detailBoxClaim = GuideCardWidth - GuideCardPad * 2f - GuideClaimWidth - 8f;
            // 보상 줄에서 아이콘과 화살표를 뺀 글자 칸. 보석 수(왼쪽)와
            // 진행도(오른쪽)가 나눠 쓰므로 둘을 합쳐 잰다
            float rewardBox = GuideCardWidth - GuideCardPad * 2f
                              - GuideRewardIconSize - 8f - GuideArrowWidth - 8f;

            foreach (var step in Onikiri.Progression.GuideQuestCatalog.Steps)
            {
                // 퀘스트를 안 가리키는 칸은 이 검산의 대상이 아니다. 완료를
                // 세이브에서 직접 읽으므로(GuideGate) 가리킬 퀘스트가 없고,
                // 카드에 뜨는 제목·보상도 GuideQuestLine이 직접 적는다
                if (step.Gate != Onikiri.Progression.GuideGate.Quest)
                {
                    // 카드에 뜨는 것은 행동 문구와 **완료 조건**이다. 둘 다
                    // 재는 이유는 둘 다 실제로 그려지기 때문이다
                    CheckCardLabelFits(card, "Detail",
                        Onikiri.Progression.GuideQuestCatalog.IntroTitle,
                        detailBoxClaim, problems);
                    continue;
                }

                if (step.Index < 0)
                {
                    problems.Add("Guide step '" + step.QuestId + "' points at a quest that is "
                                 + "not in QuestCatalog - that card would be skipped silently");
                    continue;
                }

                var spec = Onikiri.Progression.QuestCatalog.Of(step.Kind)[step.Index];

                CheckCardLabelFits(card, "Detail", spec.Title, detailBoxClaim, problems);

                // 진행도가 가장 길어지는 순간은 목표를 다 채웠을 때다. 보석
                // 수와 한 줄을 나눠 쓰므로 그 몫(+간격 24)을 미리 뺀다
                string worstProgress =
                    Onikiri.Progression.GuideQuestLine.FormatCount(spec.Target)
                    + "/"
                    + Onikiri.Progression.GuideQuestLine.FormatCount(spec.Target);
                CheckCardLabelFits(card, "RewardRow/Progress", worstProgress,
                                   rewardBox - 24f, problems);

                CheckCardLabelFits(card, "RewardRow/Reward", spec.Gems.ToString() + worstProgress,
                                   rewardBox - 24f, problems);
            }

            // 태그 줄의 세 문구. 상태가 바뀌어도 같은 칸을 쓴다
            CheckCardLabelFits(card, "Action", "가이드", tagBox, problems);
            CheckCardLabelFits(card, "Action", "가이드 완료", tagBox, problems);
            CheckCardLabelFits(card, "Action", "보상 수령 완료", tagBox, problems);
            CheckCardLabelFits(card, "Claim/Label", "받기", GuideClaimWidth, problems);

            // 보석 아이콘이 빠지면 보상 줄이 숫자만 남아 무슨 수인지 알 수 없다
            var rewardIconObject = card.Find("RewardRow/Icon");
            var rewardIconImage = rewardIconObject != null
                ? rewardIconObject.GetComponent<UnityEngine.UI.Image>() : null;
            if (rewardIconImage == null || rewardIconImage.sprite == null)
                problems.Add("Guide card reward icon is missing its gem sprite");

            // 받기 버튼이 없으면 완료 가능 상태에서 카드가 안내만 하고 수령이
            // 안 된다 - 개선안이 요구한 "즉시 받기"의 핵심 부품이다
            var claimObject = card.Find("Claim");
            if (claimObject == null)
                problems.Add("Guide card has no Claim button - run Build Combat Content");
            else if (claimObject.gameObject.activeSelf)
                problems.Add("Guide card Claim button is saved active - it must start hidden "
                             + "and only appear in the claimable state (GuideQuestCard)");
        }

        private static void CheckCardLabelFits(Transform card, string path, string worst,
                                               float boxWidth, List<string> problems)
        {
            var found = card.Find(path);
            var label = found != null ? found.GetComponent<TMPro.TMP_Text>() : null;
            if (label == null)
            {
                problems.Add("Guide card label '" + path + "' is missing");
                return;
            }

            var needed = label.GetPreferredValues(worst);
            if (needed.x > boxWidth)
                problems.Add(string.Format(
                    "Guide card '{0}' overflows: \"{1}\" needs {2:F0}px but its box is {3:F0}px. "
                    + "Shorten the guide line's wording (GuideQuestCatalog) - the atlas size "
                    + "is baked and the card cannot grow without hitting the progress line.",
                    path, worst, needed.x, boxWidth));

            /**
             * @brief 세로도 잰다. **가로만 재다가 실기에서 물린 자리다.**
             *
             * 아랫줄 상자를 38px로 잡았는데 33pt 한 줄이 38.5px을 요구했다.
             * 0.5px이 모자라자 Ellipsis가 **줄을 통째로 지웠다** -
             * `characterCount = 0`이라 잘린 흔적조차 없고, 화면에는 그 줄이
             * 그냥 없었다. 가로 넘침은 말줄임표로 보이기라도 하는데 세로
             * 부족은 아무 단서를 안 남긴다. 그래서 이쪽이 더 급하다.
             */
            float boxHeight = ((RectTransform)label.transform).rect.height;
            if (needed.y > boxHeight)
                problems.Add(string.Format(
                    "Guide card '{0}' is too short: one line needs {1:F1}px but its box is "
                    + "{2:F1}px tall. TMP's Ellipsis DELETES the whole line when the box is "
                    + "short - it does not clip. Raise GuideRowHeight.",
                    path, needed.y, boxHeight));
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

                // 패널 헤더의 "Lv · EXP" 라벨(개선안 v2). 빠지면 헤더가
                // 빌더의 자리표시 문구를 영영 보여준다
                RequireReference(hudSo, "headerLabel", problems);

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

                // 동적 폰트는 이 검사에서 빠진다 (54단계).
                //
                // 정적 아틀라스에서 "글리프가 없다"는 화면에 □가 뜬다는 뜻이지만,
                // 동적 폰트에서는 **아직 안 구웠다**는 뜻일 뿐이다 - 그리는 순간
                // 래스터된다. 이름 폰트가 그런 한 벌이고(남이 지은 이름은 미리
                // 알 수 없다, PixelFontAssetBuilder.BuildNameFont), 여기서
                // 걸러내지 않으면 빌드 검사가 영원히 빨간불이다
                if (label.font.atlasPopulationMode == TMPro.AtlasPopulationMode.Dynamic) continue;

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
            // #2 뒤로 스트립은 SafeArea 직속이다. 자리는 그대로라 흡수 연출의
            // 목적지도 그대로지만, 찾는 곳은 바뀌었다
            var hudLayer = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            var expStrip = hudLayer != null
                ? hudLayer.Find(UpgradePanelBuilder.ExpStripName) : null;
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

            // ---- 랭킹 칩 (2행 우측, 54단계). 스테이지 칩과 같은 줄에 서고
            // 오른쪽 끝에 붙는다 - 2행의 오른쪽 절반은 스테이지 칩(330px)이
            // 끝난 뒤로 계속 비어 있던 자리다.
            //
            // 글자에서 **트로피 글리프**로 (#15). 54단계에는 "뜻이 맞는 기존
            // 글리프가 없다"가 이유였는데, 그건 있는 것 중에 고르려 했기
            // 때문이다 - 없으면 굽는다는 것이 이 프로젝트의 UI 규칙이고
            // (UiGlyphBuilder 머리 주석), 트로피는 16px에서 읽히는 기호다.
            //
            // 상단 바에서 글자를 쓰는 것은 재화 수치뿐이라(38단계 아이콘화)
            // "랭킹" 두 글자는 그 줄에 마지막으로 남은 예외였다.
            //
            // 금빛이다(UiSkin.Gold). 이 게임에서 금색은 보상과 완성의 색이고
            // (UpgradeButton.masteredColor), 순위표가 정확히 그 계열이다
            var ranking = EnsureImage(topBar, "RankingButton", UiSkin.InkChip);
            var rankingRect = (RectTransform)ranking.transform;
            rankingRect.anchorMin = rankingRect.anchorMax = new Vector2(1f, 1f);
            rankingRect.pivot = new Vector2(1f, 1f);
            rankingRect.sizeDelta = new Vector2(RankingChipWidth, StageChipHeight);
            rankingRect.anchoredPosition = new Vector2(-SideMargin, -Row2Top);
            ranking.sprite = null;
            ranking.type = UnityEngine.UI.Image.Type.Simple;

            var rankingButton = ranking.GetComponent<UnityEngine.UI.Button>();
            if (rankingButton == null)
                rankingButton = ranking.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyFlatButton(rankingButton, ranking);

            var rankingIcon = EnsureImage(ranking.transform, "Icon", UiSkin.Gold);
            var rankingIconRect = (RectTransform)rankingIcon.transform;
            rankingIconRect.anchorMin = rankingIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            rankingIconRect.pivot = new Vector2(0.5f, 0.5f);
            rankingIconRect.sizeDelta = new Vector2(RankingIconSize, RankingIconSize);
            rankingIconRect.anchoredPosition = Vector2.zero;
            rankingIcon.sprite = UiGlyphBuilder.Load(UiGlyphBuilder.Trophy);
            rankingIcon.type = UnityEngine.UI.Image.Type.Simple;
            rankingIcon.raycastTarget = false;

            // 글자 칸은 지운다. 남겨두면 빈 라벨이 칩 안에서 레이캐스트와
            // 폭 검사(CheckLabelFits)를 계속 붙들고, 그 검사는 이제 잴 글자가
            // 없어서 항상 통과하는 검사가 된다
            var staleRankingLabel = ranking.transform.Find("Label");
            if (staleRankingLabel != null) Object.DestroyImmediate(staleRankingLabel.gameObject);

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

            // 레벨업 알림 점 (개선안 v2). 레벨업 버튼이 캐릭터 패널로 들어가
            // 전투 화면에서 안 보이는 동안, "올릴 것이 있다"는 초상의 점이
            // 말한다 - 초상을 누르면 그 패널이 열리므로(홈 버튼) 신호와
            // 입구가 같은 자리다. 퀘스트 아이콘 배지와 같은 부품·같은 규칙이다
            var noticeDot = QuestPanelBuilder.BuildBadge(frame.transform, null);
            var notice = frame.GetComponent<Onikiri.UI.LevelUpNoticeBadge>();
            if (notice == null)
                notice = frame.gameObject.AddComponent<Onikiri.UI.LevelUpNoticeBadge>();
            var noticeSo = new SerializedObject(notice);
            noticeSo.FindProperty("badge").objectReferenceValue = noticeDot.gameObject;
            noticeSo.ApplyModifiedPropertiesWithoutUndo();
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

        /**
         * @brief 랭킹 칩(2행 우측, 54단계). 이제 트로피 글리프 하나다 (#15).
         *
         * 글자가 빠졌는데도 폭을 안 줄인다. 160px은 이 캔버스(1080px = 360dp)
         * 에서 약 53dp라 손가락 표적의 최소치(48dp)를 넘고, 96px로 줄이면
         * 32dp가 되어 그 아래로 떨어진다 - 아이콘이 작아졌다고 누를 자리까지
         * 작아질 이유는 없다.
         */
        private const float RankingChipWidth = 160f;

        /** 랭킹 트로피. 스테이지 칩 글리프(40)보다 한 단 크다 - 혼자 서기 때문 */
        private const float RankingIconSize = 48f;

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
         * @brief 경험치 스트립 + 레벨 헤더(성장 패널) + LevelHud 배선.
         *
         * ## 개선안 v2 - 레벨업 버튼이 전투 화면을 떠났다
         *
         * #2에서 SafeArea로 올라가 스트립 위 "손잡이"로 서 있던 버튼이 다시
         * 패널 안으로 들어온다. 이번에는 전투 화면에서 완전히 빠지는 것이
         * 목적이다 - 하단에 가이드 카드가 서면서, 빨간 버튼까지 전투 띠에
         * 남아 있으면 세 가지 색이 동시에 소리를 지른다.
         *
         * 슬레이어의 구성을 따른다: 상단 HUD는 상태(초상 배지 + 스트립)만,
         * 실제 레벨업 조작은 캐릭터 패널의 "Lv / EXP / 레벨업" 헤더 한 줄이다.
         *
         *   - 패널 직속(스크롤 밖)·서브탭 위라 강화/성장/전직 어디서든 보인다
         *   - 눌리면 스탯 포인트가 생기고, 그 포인트를 쓰는 성장 탭 배지가
         *     바로 아래 줄이다 - "레벨업 -> 포인트 쓰기"가 시선 한 줄로 이어진다
         *   - 패널이 다른 화면(스킬·장비 등)에 덮이는 동안의 신호는 버튼이
         *     아니라 알림 점이 맡는다(LevelUpNoticeBadge - 초상·캐릭터 탭)
         *
         * 올릴 수 있을 때만 나타나는 규칙과 스트립 맥동(38b)은 그대로다.
         * 헤더 라벨("Lv.56 · EXP 72%")은 눌리면 스탯 창을 연다 - 초상이
         * 홈 버튼이 되면서 넘겨받은 입구다(HudScreensBuilder가 배선한다).
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

            var safeAreaForRow = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            var panel = MainSceneBuilder.FindBand("GrowthPanel");

            // 옛 세대를 치운다: SafeArea의 손잡이 버튼(#2~41단계 세대)과
            // 패널 직속의 더 옛 버튼. 남으면 같은 버튼이 두 겹이 된다
            if (safeAreaForRow != null)
            {
                var staleHandle = safeAreaForRow.Find("LevelUpButton");
                if (staleHandle != null) Object.DestroyImmediate(staleHandle.gameObject);
            }
            if (panel != null)
            {
                var stalePanelButton = panel.Find("LevelUpButton");
                if (stalePanelButton != null) Object.DestroyImmediate(stalePanelButton.gameObject);
            }

            UnityEngine.UI.Image levelUpRoot = null;
            UnityEngine.UI.Button button = null;
            TMPro.TMP_Text levelUpLabel = null;
            TMPro.TMP_Text headerLabel = null;
            if (panel != null)
            {
                /**
                 * @brief 레벨 헤더 행. 스트립 바로 아래, 서브탭 줄 위.
                 *
                 * 판 전체가 스탯 창 입구 버튼이다(라벨 = "Lv.56 · EXP 72%",
                 * 배선은 HudScreensBuilder). 오른쪽 끝의 레벨업 버튼은 자식
                 * 버튼이라 레이캐스트가 먼저 먹는다 - 카드 안의 받기 버튼과
                 * 같은 구조다.
                 */
                var header = EnsureImage(panel, UpgradePanelBuilder.LevelHeaderName, UiSkin.InkChip);
                var headerRect = (RectTransform)header.transform;
                headerRect.anchorMin = new Vector2(0f, 1f);
                headerRect.anchorMax = new Vector2(1f, 1f);
                headerRect.pivot = new Vector2(0.5f, 1f);
                // 좌우 여백은 서브탭 줄(UpgradePanelBuilder.SidePadding=48)과 맞춘다
                headerRect.offsetMin = new Vector2(48f, 0f);
                headerRect.offsetMax = new Vector2(-48f, 0f);
                headerRect.sizeDelta = new Vector2(-96f, UpgradePanelBuilder.LevelHeaderHeight);
                headerRect.anchoredPosition = new Vector2(0f, -UpgradePanelBuilder.ExpStripStride);
                header.sprite = null;
                header.type = UnityEngine.UI.Image.Type.Simple;

                var headerButton = header.GetComponent<UnityEngine.UI.Button>();
                if (headerButton == null)
                    headerButton = header.gameObject.AddComponent<UnityEngine.UI.Button>();
                UiSkin.ApplyFlatButton(headerButton, header);

                headerLabel = EnsureHudLabel(header.transform, "Label",
                                             TMPro.TextAlignmentOptions.Left,
                                             new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
                var headerLabelRect = (RectTransform)headerLabel.transform;
                headerLabelRect.anchorMin = Vector2.zero;
                headerLabelRect.anchorMax = Vector2.one;
                headerLabelRect.offsetMin = new Vector2(16f, 0f);
                headerLabelRect.offsetMax = new Vector2(-(LevelUpWidth + 24f), 0f);
                headerLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                headerLabel.text = "Lv.1 · EXP 0%";

                // 동작 버튼이므로 Ancient 판 그대로다. 틴트는 붉은색 - 이
                // 화면에서 붉은색은 이미 "네 차례"다(성장 탭의 남은 포인트
                // 배지가 같은 UiSkin.Danger를 쓰고, 레벨업이 만드는 것이
                // 정확히 그 포인트다)
                levelUpRoot = EnsureImage(header.transform, "LevelUpButton", UiSkin.Danger);
                var buttonRect = (RectTransform)levelUpRoot.transform;
                buttonRect.anchorMin = new Vector2(1f, 0.5f);
                buttonRect.anchorMax = new Vector2(1f, 0.5f);
                buttonRect.pivot = new Vector2(1f, 0.5f);
                buttonRect.sizeDelta = new Vector2(LevelUpWidth, LevelUpHeight);
                buttonRect.anchoredPosition = new Vector2(-6f, 0f);
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

                // 올릴 수 있을 때만 나타난다(LevelHud). 헤더 행 자체는 늘
                // 서 있으므로 "없다가 나타나는" 신호는 버튼만 만든다
                levelUpRoot.gameObject.SetActive(false);
            }

            // 완료 토스트(QuestProgressHud) 세대의 잔재를 치운다. 완료 신호는
            // 이제 가이드 카드(가이드 칸)와 퀘스트 아이콘 배지(그 외 전부)
            // 둘로 충분하다는 판단으로 걷어냈다(사용자 결정)
            if (safeAreaForRow != null)
            {
                var staleToast = safeAreaForRow.Find("QuestProgressHud");
                if (staleToast != null) Object.DestroyImmediate(staleToast.gameObject);
            }

            BuildGuideQuestCard();
            BuildQuestButton();

            var hud = topBar.GetComponent<Onikiri.UI.LevelHud>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.LevelHud>();

            var so = new SerializedObject(hud);
            so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            so.FindProperty("expFill").objectReferenceValue = fill;
            so.FindProperty("levelUpRoot").objectReferenceValue =
                levelUpRoot != null ? levelUpRoot.gameObject : null;
            so.FindProperty("levelUpButton").objectReferenceValue = button;
            so.FindProperty("levelUpLabel").objectReferenceValue = levelUpLabel;
            so.FindProperty("headerLabel").objectReferenceValue = headerLabel;
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
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - the exp strip has nowhere to go.");
                return null;
            }

            /**
             * @brief 스트립은 이제 **패널이 아니라 SafeArea의 자식**이다 (#2).
             *
             * 자리는 한 픽셀도 안 움직인다 - 성장 패널 띠의 윗변, 풀폭 10px 그대로.
             * 바뀐 것은 **누구의 자식인가**뿐이고, 그것이 이 항목의 전부다.
             *
             * 왜냐면 성장 패널은 이 띠의 **바탕층**이고, 스킬·퀘스트·장비·동료
             * 화면은 같은 띠를 통째로 덮는 형제 판이기 때문이다(LockedTab의
             * 상호 배타 규칙). 스트립이 바탕 패널 안에 있으면 그 넷 중 하나만
             * 열려도 함께 덮인다 - "캐릭터 탭에서만 보인다"의 정체가 이것이다.
             *
             * SafeArea 직속으로 올리고 형제 맨 뒤에 세우면 어느 화면이 열려
             * 있든 그 위에 그려진다. 경험치는 화면의 소유물이 아니라 **상태**이고,
             * 상태는 무엇을 보고 있든 같은 자리에 있어야 한다.
             */
            var stripImage = EnsureImage(safeArea, UpgradePanelBuilder.ExpStripName, UiSkin.PanelEdge);
            var stripRect = (RectTransform)stripImage.transform;
            stripRect.anchorMin = new Vector2(0f, DisplayConfig.GrowthPanelTop);
            stripRect.anchorMax = new Vector2(1f, DisplayConfig.GrowthPanelTop);
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.sizeDelta = new Vector2(0f, UpgradePanelBuilder.ExpStripHeight);
            stripRect.anchoredPosition = Vector2.zero;
            stripImage.raycastTarget = false;

            // 형제 순서가 아니라 **층위 값**으로 올린다. 형제 순서로 올리면
            // 나중에 도는 빌더(스킬·퀘스트·랭킹 패널)가 자기 판을 뒤에 붙이는
            // 순간 도로 파묻힌다 - 그림이 빌더 실행 순서에 의존하게 된다
            RaiseToLayer(stripImage.transform, DisplayConfig.SortingHud, false);

            // 패널 안에 남아 있는 옛 스트립을 치운다. 남겨두면 덮이지 않는
            // 새 스트립 **아래에** 같은 그림이 하나 더 있고, 캐릭터 탭에서만
            // 두 겹으로 그려진다
            var panel = MainSceneBuilder.FindBand("GrowthPanel");
            if (panel != null)
            {
                var stale = panel.Find(UpgradePanelBuilder.ExpStripName);
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }

            var strip = stripImage;

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

        // 퀘스트 완료 토스트(QuestProgressHud)는 여기 있다가 통째로 빠졌다.
        // #13의 상시 진행 줄 → 개선안 v2의 1.2초 토스트를 거쳐, 완료 신호가
        // 가이드 카드(가이드 칸)와 퀘스트 아이콘 배지(그 외 전부)로 충분해
        // 지면서 셋째 신호는 소음이라는 판단으로 제거됐다(사용자 결정).
        // 옛 씬 오브젝트는 BuildExpRow가 치운다.

        /**
         * @brief 폭 430 = 화면의 40%. 요구 범위(35~40%)의 위쪽 끝을 쓴다.
         *
         * 아래쪽 끝(380)이 아닌 이유는 가운데 줄이다 - 가장 긴 "강화 총합
         * 1200  1200/1200"(33pt)이 잘리지 않는 폭이 여기서 갈린다. 빌드
         * 검사가 표 전체를 대조하므로, 문구가 더 길어지면 폭이 아니라
         * 문구를 줄여야 한다.
         *
         * 높이 146 = 캡션(33pt, 실측 38.5px) 세 줄 + 여백. 세로를 아끼면
         * Ellipsis가 줄을 통째로 지운다(이 프로젝트가 두 번 밟은 함정).
         */
        private const float GuideCardWidth = 430f;
        private const float GuideCardHeight = 146f;
        private const float GuideCardPad = 14f;

        /** 글자 한 줄 상자. 38.5px 실측에 여유를 얹은 값 */
        private const float GuideRowHeight = 44f;

        /** 카드 윗변에서 태그 줄까지 / 밑변에서 보상 줄까지 */
        private const float GuideRowInset = 6f;

        /**
         * @brief 반투명 바탕. 검정~남색 55~65% 요구의 가운데 값.
         *
         * 카드가 전투 배경(나무 높이) 위에 뜨므로 통짜 판이면 배경을 자르고,
         * 너무 옅으면 단풍 위에서 글자가 안 읽힌다. 테두리 판(UiSkin.Panel)을
         * 안 쓰는 것도 같은 이유다 - 슬레이어의 카드처럼 민짜 어둠 한 장이다.
         */
        private const float GuideCardAlpha = 0.6f;

        /**
         * @brief 받기 버튼(완료 가능 상태). 카드 오른쪽 끝, 세로 가운데.
         * "받기" 두 글자(44pt)가 드는 최소치다.
         */
        private const float GuideClaimWidth = 130f;
        private const float GuideClaimHeight = 56f;
        private const float GuideArrowWidth = 36f;

        /** 보상 줄의 보석 아이콘. 16px 아트 정수배가 아니면 픽셀이 운다 */
        private const float GuideRewardIconSize = 32f;

        /**
         * @brief 퀘스트 아이콘 밑변에서 카드 윗변까지의 **기본** 간격.
         *
         * 12로 뒀다가 넓혔다. 아이콘 밑변은 -112인데 **보스 도전 버튼의
         * 밑변이 -148**이라(가운데 앵커 폭 440, 카드와 가로로 142px 겹친다),
         * 아이콘에만 맞추면 카드의 왼쪽 위 모서리가 그 버튼을 문다. 48이면
         * 카드 윗변이 -160 - 버튼 아래로 12px 여유다. 빌드 검사가 잰다.
         */
        private const float GuideCardTopGap = 48f;

        /**
         * @brief 기본 간격에서 **전투 영역 높이의 15%**만큼 더 내린다.
         *
         * 아이콘 바로 아래(-160)는 나무 우듬지 높이라 카드가 배경의 가장
         * 밝은 덩어리 위에 앉았다. 한 단 내리면 우측 **중하단** - 나무
         * 줄기 사이의 어두운 띠다. 요괴·사무라이는 지면(밴드 아래 120px)
         * 을 걷고 카드 밑변은 그 위로 300px 넘게 남으므로 전투는 여전히
         * 안 가린다. 값의 상한이 15%인 이유: 더 내리면 큰 요괴(카사오바케
         * 계열)의 머리와 데미지 숫자 띠에 닿기 시작한다.
         */
        private const float GuideCardDropRatio = 0.15f;

        /**
         * @brief 가이드 퀘스트 카드. **전투 화면 오른쪽의 소형 반투명 패널** (슬레이어식).
         *
         * ## 하단 풀폭 바를 떠났다
         *
         * 풀폭 바는 전투 화면 밑변을 통째로 차지했고, 강한 테두리와 긴 금색
         * 진행 바가 배경 위에서 혼자 소리를 질렀다. 슬레이어 키우기처럼
         * 줄인다 - 퀘스트 책 아이콘(#16) 바로 아래, 화면 폭 40%의 민짜
         * 반투명 판. 진행 바는 없고 진행도는 "4/100" 텍스트가 말한다.
         *
         * 자리의 근거: 아이콘과 세로로 이어져 "퀘스트에 관한 것"이 한
         * 기둥으로 읽히고, 카드 탭 = 퀘스트 화면이라 신호와 입구도 같다.
         * 위 가운데의 보스 도전 버튼과도, 지면을 걷는 요괴·사무라이와도
         * 안 겹친다(카드는 나무 높이에 뜬다).
         *
         * ## 눌린다
         *
         * 카드 전체가 퀘스트 화면으로 가는 문이다(HudScreenButton - 퀘스트
         * 아이콘과 같은 부품). 진행 중에는 오른쪽 끝 화살표가 그것을 말한다.
         * 완료 가능 상태에서는 오른쪽 끝에 받기 버튼이 서고, 그 버튼은
         * 퀘스트 화면의 받기와 같은 경로(QuestSystem.TryClaim)로 그 자리에서
         * 수령한다 - 새 지급 경로가 아니다.
         */
        private static void BuildGuideQuestCard()
        {
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null) return;

            /**
             * @brief **위치는 에디터 소유다.** 빌더는 처음 세울 때만 잡는다.
             *
             * 이 씬의 거의 모든 것은 빌더가 소유하지만, 이 카드의 자리는
             * 눈으로 보며 고르는 값이라 손 배치가 더 빠르다(사용자 결정).
             * 그래서 카드가 씬에 이미 있으면 앵커·위치를 **건드리지 않는다** -
             * 하이어라키에서 SafeArea/GuideQuestCard를 옮기고 저장하면 그
             * 자리가 그대로 남는다.
             *
             * 빌더가 계속 소유하는 것: 크기(430x146)·디자인·자식 배선·검증.
             * 잘못 놓으면(보스 버튼·아이콘·지면 침범) 빌드 검사가 실제
             * 씬 위치를 재서 알려준다(VerifyGuideCard).
             */
            bool fresh = safeArea.Find(GuideCardName) == null;

            var rootImage = EnsureImage(safeArea, GuideCardName, UiSkin.InkChip);
            var rect = (RectTransform)rootImage.transform;
            rect.sizeDelta = new Vector2(GuideCardWidth, GuideCardHeight);

            if (fresh)
            {
                // 기본 자리: 전투 영역 오른쪽 가운데(Right Center) 앵커,
                // 퀘스트 아이콘 열(-SideMargin), 아이콘 밑변에서 기본 간격
                // + 전투 높이의 15%만큼 내려간 우측 중하단(GuideCardDropRatio)
                float battleHeight = DisplayConfig.DesignHeight
                                     * (DisplayConfig.BattleAreaTop - DisplayConfig.GrowthPanelTop);
                float topFromBattleTop = 16f + QuestButtonSize + GuideCardTopGap
                                         + battleHeight * GuideCardDropRatio;
                rect.anchorMin = rect.anchorMax = new Vector2(
                    1f, (DisplayConfig.GrowthPanelTop + DisplayConfig.BattleAreaTop) * 0.5f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(
                    -SideMargin, battleHeight * 0.5f - topFromBattleTop);
            }
            rootImage.sprite = null;
            rootImage.type = UnityEngine.UI.Image.Type.Simple;
            // 카드 전체가 눌린다. 안 보일 때의 터치는 CanvasGroup이 막는다
            // (GuideQuestCard.Show가 blocksRaycasts를 가시성에 연동한다)
            rootImage.raycastTarget = true;

            var cardButton = rootImage.GetComponent<UnityEngine.UI.Button>();
            if (cardButton == null)
                cardButton = rootImage.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyFlatButton(cardButton, rootImage);

            // 반투명은 판의 색으로만 한다. CanvasGroup 알파는 숨김(0/1)이
            // 쓰고 있어서, 여기에 0.6을 곱하면 글자까지 같이 흐려진다
            var translucent = UiSkin.InkChip;
            translucent.a = GuideCardAlpha;
            rootImage.color = translucent;

            // 카드 탭 = 퀘스트 화면. 퀘스트 아이콘과 같은 부품·같은 대상이라
            // 신호(카드)와 입구(화면)가 어긋나지 않는다. 판이 다시 만들어지면
            // RelinkScreenTabs가 참조를 다시 물린다
            var questScreen = safeArea.Find(QuestPanelBuilder.PanelName);
            var opener = rootImage.GetComponent<Onikiri.UI.HudScreenButton>();
            if (opener == null)
                opener = rootImage.gameObject.AddComponent<Onikiri.UI.HudScreenButton>();
            var openerSo = new SerializedObject(opener);
            openerSo.FindProperty("button").objectReferenceValue = cardButton;
            openerSo.FindProperty("screen").objectReferenceValue =
                questScreen != null ? questScreen.gameObject : null;
            openerSo.FindProperty("needsReselect").boolValue = false;
            openerSo.FindProperty("otherScreens").arraySize = 0;
            openerSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 태그 줄. "가이드" / "가이드 완료" - 맨 위, 보조 정보라 흐린 색
            var action = EnsureHudLabel(rootImage.transform, "Action",
                                        TMPro.TextAlignmentOptions.Left,
                                        new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            UiFonts.Demote(action);
            var actionRect = (RectTransform)action.transform;
            actionRect.anchorMin = Vector2.zero;
            actionRect.anchorMax = Vector2.one;
            actionRect.offsetMin = new Vector2(GuideCardPad,
                                               GuideCardHeight - GuideRowInset - GuideRowHeight);
            actionRect.offsetMax = new Vector2(-GuideCardPad, -GuideRowInset);
            action.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            action.color = UiSkin.TextDim;
            action.text = "가이드";

            // ---- 목표명 + 진행도("4/100"). 가운데 줄, 카드의 주인 글자
            var detail = EnsureHudLabel(rootImage.transform, "Detail",
                                        TMPro.TextAlignmentOptions.Left,
                                        new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            UiFonts.Demote(detail);
            var detailRect = (RectTransform)detail.transform;
            detailRect.anchorMin = Vector2.zero;
            detailRect.anchorMax = Vector2.one;
            detailRect.offsetMin = new Vector2(GuideCardPad,
                                               GuideRowInset + GuideRowHeight);
            detailRect.offsetMax = new Vector2(-GuideCardPad,
                                               -(GuideRowInset + GuideRowHeight));
            detail.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            detail.color = UiSkin.Text;
            detail.text = "5스테이지 도달  0/5";

            // ---- 보상 줄. 보석 아이콘 + 숫자 - 맨 아래 왼쪽. 진행 중에만
            //      보이므로(GuideQuestCard) 루트 하나로 묶어 함께 끈다
            var rewardRow = rootImage.transform.Find("RewardRow");
            if (rewardRow == null)
            {
                var rowObject = new GameObject("RewardRow", typeof(RectTransform));
                rowObject.transform.SetParent(rootImage.transform, false);
                rewardRow = rowObject.transform;
            }
            var rewardRowRect = (RectTransform)rewardRow;
            rewardRowRect.anchorMin = Vector2.zero;
            rewardRowRect.anchorMax = new Vector2(1f, 0f);
            rewardRowRect.pivot = new Vector2(0.5f, 0f);
            rewardRowRect.offsetMin = new Vector2(GuideCardPad, GuideRowInset);
            rewardRowRect.offsetMax = new Vector2(-GuideCardPad, GuideRowInset + GuideRowHeight);

            var rewardIcon = EnsureImage(rewardRow, "Icon", Color.white);
            var rewardIconRect = (RectTransform)rewardIcon.transform;
            rewardIconRect.anchorMin = rewardIconRect.anchorMax = new Vector2(0f, 0.5f);
            rewardIconRect.pivot = new Vector2(0f, 0.5f);
            rewardIconRect.sizeDelta = new Vector2(GuideRewardIconSize, GuideRewardIconSize);
            rewardIconRect.anchoredPosition = Vector2.zero;
            // 재화 트레이·상점 탭이 쓰는 그 보석이다. 같은 것은 같은 심볼로
            rewardIcon.sprite = UiIcons.LoadItem(UiIcons.GemSprite);
            rewardIcon.type = UnityEngine.UI.Image.Type.Simple;
            rewardIcon.preserveAspect = true;
            rewardIcon.raycastTarget = false;

            var reward = EnsureHudLabel(rewardRow, "Reward",
                                        TMPro.TextAlignmentOptions.Left,
                                        new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            UiFonts.Demote(reward);
            var rewardRect = (RectTransform)reward.transform;
            rewardRect.anchorMin = Vector2.zero;
            rewardRect.anchorMax = Vector2.one;
            rewardRect.offsetMin = new Vector2(GuideRewardIconSize + 8f, 0f);
            rewardRect.offsetMax = new Vector2(-(GuideArrowWidth + 8f), 0f);
            reward.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            reward.color = UiSkin.TextDim;
            reward.text = "25";

            // 진행도("4/100"). 같은 줄의 오른쪽 끝, 화살표 앞. 목표명 옆에
            // 세우면 가장 긴 목표에서 반드시 잘린다(GuideQuestCard.Paint 주석)
            var progress = EnsureHudLabel(rewardRow, "Progress",
                                          TMPro.TextAlignmentOptions.Right,
                                          new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            UiFonts.Demote(progress);
            var progressRect = (RectTransform)progress.transform;
            progressRect.anchorMin = Vector2.zero;
            progressRect.anchorMax = Vector2.one;
            progressRect.offsetMin = new Vector2(GuideRewardIconSize + 8f, 0f);
            progressRect.offsetMax = new Vector2(-(GuideArrowWidth + 8f), 0f);
            progress.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            progress.color = UiSkin.TextDim;
            progress.text = "0/5";

            // ---- 진행 바 세대의 잔재. 이 판형에는 바가 없다 - 진행도는
            //      "4/100" 텍스트가 말한다(요구: 긴 진행도 바 제거)
            var staleTrack = rootImage.transform.Find("Track");
            if (staleTrack != null) Object.DestroyImmediate(staleTrack.gameObject);

            // ---- 받기 버튼. 완료 가능 상태에서만 켜진다(GuideQuestCard).
            //      퀘스트 화면의 받기 버튼과 같은 판(Chrome)·같은 글자라
            //      "같은 동작"이 형태로 읽힌다
            var claim = EnsureImage(rootImage.transform, "Claim", Color.white);
            var claimRect = (RectTransform)claim.transform;
            claimRect.anchorMin = new Vector2(1f, 0.5f);
            claimRect.anchorMax = new Vector2(1f, 0.5f);
            claimRect.pivot = new Vector2(1f, 0.5f);
            claimRect.sizeDelta = new Vector2(GuideClaimWidth, GuideClaimHeight);
            claimRect.anchoredPosition = new Vector2(-GuideCardPad, 0f);
            UiSkin.ApplyPanel(claim, UiSkin.Chrome);

            var claimButton = claim.GetComponent<UnityEngine.UI.Button>();
            if (claimButton == null)
                claimButton = claim.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyButton(claimButton, claim);

            var claimLabel = EnsureHudLabel(claim.transform, "Label",
                                            TMPro.TextAlignmentOptions.Center,
                                            new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            var claimLabelRect = (RectTransform)claimLabel.transform;
            claimLabelRect.anchorMin = Vector2.zero;
            claimLabelRect.anchorMax = Vector2.one;
            claimLabelRect.offsetMin = Vector2.zero;
            claimLabelRect.offsetMax = Vector2.zero;
            claimLabel.text = "받기";

            claim.gameObject.SetActive(false);

            // ---- 화살표. 보상 줄 오른쪽 끝에 서서 "카드가 눌린다"를
            //      말한다. 아틀라스에 '〉'가 없어 ASCII '>'를 쓴다
            var arrow = EnsureHudLabel(rootImage.transform, "Arrow",
                                       TMPro.TextAlignmentOptions.Center,
                                       new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero);
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 0f);
            arrowRect.pivot = new Vector2(1f, 0f);
            arrowRect.sizeDelta = new Vector2(GuideArrowWidth, GuideRowHeight);
            arrowRect.anchoredPosition = new Vector2(-GuideCardPad, GuideRowInset);
            UiFonts.Demote(arrow);
            arrow.color = UiSkin.TextDim;
            arrow.raycastTarget = false;
            arrow.text = ">";

            var component = rootImage.GetComponent<Onikiri.UI.GuideQuestCard>();
            if (component == null)
                component = rootImage.gameObject.AddComponent<Onikiri.UI.GuideQuestCard>();

            // 숨김은 알파로 한다 - 오브젝트를 끄면 이 컴포넌트의 구독이 끊겨
            // 다시 켜질 수 없다(GuideQuestCard.group 주석)
            var group = rootImage.GetComponent<CanvasGroup>();
            if (group == null) group = rootImage.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var so = new SerializedObject(component);
            so.FindProperty("actionLabel").objectReferenceValue = action;
            so.FindProperty("rewardLabel").objectReferenceValue = reward;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("progressLabel").objectReferenceValue = progress;
            so.FindProperty("rewardRoot").objectReferenceValue = rewardRow.gameObject;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("claimRoot").objectReferenceValue = claim.gameObject;
            so.FindProperty("claimButton").objectReferenceValue = claimButton;
            so.FindProperty("arrowLabel").objectReferenceValue = arrow;
            so.FindProperty("normalColor").colorValue = UiSkin.Text;
            so.FindProperty("dimColor").colorValue = UiSkin.TextDim;
            so.FindProperty("readyColor").colorValue = UiSkin.Gold;

            // 귀문 대기 중에는 이 카드가 문 입구가 된다 (4단계 §3).
            // 셋을 함께 물린다 - 셋 중 하나라도 비면 카드는 평소처럼만 굴러
            // 조용히 실패한다(LockedTab.screen이 비었을 때와 같은 종류의 사고)
            so.FindProperty("fight").objectReferenceValue =
                Object.FindFirstObjectByType<BossFight>();
            so.FindProperty("cardButton").objectReferenceValue = cardButton;
            so.FindProperty("cardScreenButton").objectReferenceValue = opener;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 상시 HUD 층. 경험치 줄과 같은 층이라 어느 하단 탭을 열어도
            // 보이고, 팝업(20) 아래라 딤을 안 뚫는다. 눌리는 것이 됐으므로
            // 자기 레이캐스터가 필요하다(중첩 캔버스는 부모의 것을 안 물려받는다)
            RaiseToLayer(rect, DisplayConfig.SortingHud, true);
        }

        /** 가이드 카드의 오브젝트 이름. 검사와 테스트 패널이 같은 이름을 읽는다 */
        public const string GuideCardName = "GuideQuestCard";

        /**
         * @brief 퀘스트 진입 아이콘 (#16). **전투 화면의 오른쪽 위**.
         *
         * ## 자리를 두 번 옮겼다
         *
         *   처음  진행 줄 바로 위(전투 화면 오른쪽 **아래**)
         *         → 그 자리가 요괴가 지나다니는 길이라 아이콘이 요괴 위에 겹쳤다
         *   다음  상단 바 2행(랭킹 트로피 옆)
         *         → 상태 바로 올라가면서 **플레이 화면의 아이콘**이 아니게 됐다.
         *           #16이 요구한 것은 "하단 탭이 아니라 플레이 화면에 떠 있는
         *           아이콘"이고, 상단 바는 그 화면이 아니라 그 위의 띠다
         *   지금  전투 화면의 오른쪽 **위** 모서리
         *
         * 여기가 셋을 다 만족한다. 전투 화면 안이고(플레이 화면의 아이콘),
         * 요괴가 걷는 지면은 화면 아래쪽이라 겹치지 않으며, 위쪽 가운데는
         * 보스 도전 버튼이 쓰지만 오른쪽 끝은 늘 비어 있다.
         *
         * 진행 줄(#13)은 아래에 그대로 둔다 - 그것은 문이 아니라 **상태**이고,
         * 방치 중에 흘깃 보는 자리는 화면 아래가 맞다.
         *
         * 잠기지 않는다. 31단계가 이 화면을 잠그지 않기로 한 이유("신규
         * 플레이어에게 다음에 무엇을 할지 알려주는 것")가 그대로다.
         */
        private const float QuestButtonSize = 96f;

        private static void BuildQuestButton()
        {
            var topBar = MainSceneBuilder.FindBand("TopBar");
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null) return;

            // 옛 세대(상단 바에 있던 칩)를 치운다. 부모가 바뀌었으므로 이름만
            // 같고 자리가 다른 유령이 남는다
            if (topBar != null)
            {
                var stale = topBar.Find("QuestButton");
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }

            var chip = EnsureImage(safeArea, "QuestButton", UiSkin.InkChip);
            var rect = (RectTransform)chip.transform;
            // 전투 영역의 윗변에서 아래로 매달린다. 상단 바 **밑**이다
            rect.anchorMin = rect.anchorMax = new Vector2(1f, DisplayConfig.BattleAreaTop);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(QuestButtonSize, QuestButtonSize);
            rect.anchoredPosition = new Vector2(-SideMargin, -16f);
            chip.sprite = null;
            chip.type = UnityEngine.UI.Image.Type.Simple;

            var button = chip.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = chip.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyFlatButton(button, chip);

            var icon = EnsureImage(chip.transform, "Icon", UiIcons.Tint);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            // 전투 화면에 홀로 뜨는 아이콘이라 상단 바 심볼보다 크다.
            // 그 줄의 칩들은 서로 크기를 맞춰야 하지만 이것은 비교 대상이 없다
            iconRect.sizeDelta = new Vector2(56f, 56f);
            iconRect.anchoredPosition = Vector2.zero;
            // 하단 탭이 쓰던 그 두루마리다 - 같은 화면의 입구이므로 심볼도 같다
            icon.sprite = UiIcons.LoadItem(UiIcons.QuestSprite);
            icon.type = UnityEngine.UI.Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // 알림 점. 하단 탭에 있던 그 배지가 여기로 따라온다 (#4의 원)
            var badge = QuestPanelBuilder.BuildBadge(chip.transform, null);
            badge.gameObject.SetActive(false);

            var tabBadge = chip.GetComponent<Onikiri.UI.QuestTabBadge>();
            if (tabBadge == null) tabBadge = chip.gameObject.AddComponent<Onikiri.UI.QuestTabBadge>();
            var badgeSo = new SerializedObject(tabBadge);
            badgeSo.FindProperty("badge").objectReferenceValue = badge.gameObject;
            badgeSo.FindProperty("label").objectReferenceValue = null;
            badgeSo.ApplyModifiedPropertiesWithoutUndo();

            // 여는 것은 HudScreenButton이다 - 상단 바의 스탯·설정과 같은 부품
            // (누르면 열리고 다시 누르면 닫힌다). 팝업이라 다른 화면은 안 닫는다
            var screen = safeArea.Find(QuestPanelBuilder.PanelName);
            var control = chip.GetComponent<Onikiri.UI.HudScreenButton>();
            if (control == null) control = chip.gameObject.AddComponent<Onikiri.UI.HudScreenButton>();

            var so = new SerializedObject(control);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("screen").objectReferenceValue = screen != null ? screen.gameObject : null;
            so.FindProperty("needsReselect").boolValue = false;
            so.FindProperty("otherScreens").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (screen == null)
                Debug.LogWarning("[Onikiri] QuestButton has no panel yet"
                                 + " - Build Quest Panel runs later and RelinkScreenTabs re-wires it.");

            // 상시 HUD 층. 전투 화면 위에 뜨지만 팝업(20) 아래다 -
            // 진행 줄·경험치 줄과 같은 층이다
            RaiseToLayer(rect, DisplayConfig.SortingHud, true);
        }

        /**
         * @brief 화면을 가로막는 것들을 팝업 층으로 올린다 (#2의 뒷정리).
         *
         * 경험치 줄이 HUD 층(10)으로 올라가면서 **딤 배경 위에 그려지는**
         * 문제가 생겼다. 오프라인 보상이나 보스 등장 연출은 화면을 통째로
         * 어둡게 덮는데, 그 위에 옥색 줄 하나가 남아 있으면 딤이 뚫린 것으로
         * 읽힌다.
         *
         * 이름으로 찾는다. 이 목록에 없는 팝업은 그냥 예전 층(0)에 남으므로,
         * 새 팝업을 만들 때 여기 이름을 더하는 것을 잊으면 경험치 줄이 그
         * 위에 뜬다 - 눈에 바로 보이는 종류의 누락이다.
         */
        private static void RaisePopupLayer()
        {
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null) return;

            string[] popups =
            {
                "OfflinePopup", "BossIntro", "IntroOverlay",
                // #1 팝업화로 옮겨온 둘. 이제 화면이 아니라 팝업이다
                HudScreensBuilder.SettingsPanelName,
                LeaderboardPanelBuilder.PanelName,
                // #16으로 하단 탭에서 내려온 퀘스트
                QuestPanelBuilder.PanelName,
                SkillPanelBuilder.SkillPopupName,
            };

            foreach (var name in popups)
            {
                // 부팅 오버레이는 SafeArea가 아니라 캔버스 직속이다(전체 화면
                // 덮개라 세이프 영역에 갇히면 노치 옆이 뚫린다). 두 곳을 다
                // 보는 이유가 그것이다 - 여기서 놓치면 타이틀 화면 위에
                // 경험치 줄이 떠 있게 된다
                var found = safeArea.Find(name);
                if (found == null && safeArea.parent != null) found = safeArea.parent.Find(name);
                if (found == null) continue;

                // 팝업 안에는 반드시 누를 것이 있다(최소한 닫기 버튼)
                RaiseToLayer(found, DisplayConfig.SortingPopup, true);
            }
        }

        /**
         * @brief 이 오브젝트를 정해진 층위로 올린다 (DisplayConfig.SortingHud 등).
         *
         * 중첩 Canvas + overrideSorting이다. 형제 순서를 바꾸지 않으므로
         * 계층 구조는 그대로 읽히고, 그림 순서만 값으로 못 박힌다.
         *
         * `needsRaycaster`는 그 안에 **눌리는 것이 있을 때** 켠다. 중첩 캔버스는
         * 부모의 GraphicRaycaster 아래로 들어가지 않아서, 없으면 버튼이 보이는데
         * 안 눌리는 상태가 된다 - 화면상 아무 단서가 없는 종류의 고장이다.
         */
        public static void RaiseToLayer(Transform target, int order, bool needsRaycaster)
        {
            if (target == null) return;

            /**
             * @brief **꺼진 오브젝트에는 overrideSorting이 안 붙는다.**
             *
             * 실기에서 물렸다. 경험치 줄(층 10)이 오프라인 보상 팝업(층 20)
             * **위에** 그려졌다 - 층 값은 제대로 적혔는데 `overrideSorting`이
             * false로 남아서, 그 캔버스가 층을 아예 안 쓰고 형제 순서로
             * 떨어진 것이다.
             *
             * 이유는 팝업들이 **꺼진 채로 저장되기** 때문이다. Unity는 비활성
             * 캔버스에 켠 overrideSorting을 지운다. 설정·랭킹처럼 만들어질
             * 때 켜져 있던 것만 살아남았고, 오프라인 보상·보스 등장처럼
             * 이미 꺼져 있던 것에는 안 붙었다 - 그래서 증상이 팝업마다
             * 갈렸고 그것이 원인을 가렸다.
             *
             * 잠깐 켜서 적고 원래 상태로 되돌린다.
             */
            bool wasActive = target.gameObject.activeSelf;
            if (!wasActive) target.gameObject.SetActive(true);

            var canvas = target.GetComponent<Canvas>();
            if (canvas == null) canvas = target.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;

            if (needsRaycaster && target.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                target.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            if (!wasActive) target.gameObject.SetActive(false);

            // 되돌린 뒤에도 값이 남았는지 확인한다. 이 검사가 없었으면
            // 위의 함정을 실기 캡처로만 발견할 수 있었다
            if (!canvas.overrideSorting)
                Debug.LogWarning("[Onikiri] " + target.name
                    + " lost overrideSorting - it will fall back to sibling order.");
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

            // ⚠️ **퀘스트는 이 줄에 없다** (#16). 31단계에 여기 있었고
            // "신규 플레이어에게 다음에 무엇을 할지 알려주는 화면이라 첫
            // 화면부터 열려 있어야 한다"는 이유로 잠그지도 않았는데, 그
            // 이유가 오히려 이 줄에서 내려보낸 근거가 됐다:
            //
            // 하단 탭은 **키우는 것들**의 줄이다(캐릭터·스킬·장비·동료·상점 -
            // 전부 들어가서 사고 끼우는 화면). 퀘스트는 받고 나오는 화면이라
            // 층위가 다르고, 열어야 할 순간은 "받을 것이 생겼을 때"다.
            //
            // 그래서 진입점이 플레이 화면의 아이콘으로 갔다(BuildQuestButton).
            // 알림 점이 그 아이콘에 붙으므로 신호와 입구가 같은 자리가 되고,
            // 진행도 줄(#13)이 바로 옆이라 "지금 뭘 하는 중인가 → 받으러
            // 간다"가 이어진다. 다섯 칸이 된 이 줄은 칸당 180 -> 216px로
            // 넓어진다(폭은 개수로 나누므로 저절로 따라온다).

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
                bool wantsLevelBadge = spec.HomeTab;

                var staleQuest = background.GetComponent<Onikiri.UI.QuestTabBadge>();
                if (staleQuest != null && !wantsQuestBadge) Object.DestroyImmediate(staleQuest);
                var staleEquip = background.GetComponent<Onikiri.UI.EquipmentTabBadge>();
                if (staleEquip != null && !wantsEquipBadge) Object.DestroyImmediate(staleEquip);
                var stalePet = background.GetComponent<Onikiri.UI.PetTabBadge>();
                if (stalePet != null && !wantsPetBadge) Object.DestroyImmediate(stalePet);
                var staleLevel = background.GetComponent<Onikiri.UI.LevelUpNoticeBadge>();
                if (staleLevel != null && !wantsLevelBadge) Object.DestroyImmediate(staleLevel);

                if (!wantsQuestBadge && !wantsEquipBadge && !wantsPetBadge && !wantsLevelBadge)
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
                else if (spec.HomeTab)
                {
                    // 캐릭터 탭의 레벨업 알림 점 (개선안 v2). 레벨업 버튼이
                    // 이 탭의 화면(캐릭터 패널) 안으로 들어갔으므로, 다른
                    // 화면을 보고 있을 때의 신호는 이 점이 맡는다 - 초상의
                    // 점(BuildPortraitAnchor)과 같은 부품이다. 숫자는 안
                    // 적는다: 대기 수는 헤더의 레벨업 버튼이 이미 적는다
                    var badge = QuestPanelBuilder.BuildBadge(background.transform, null);

                    var badgeComponent = background.GetComponent<Onikiri.UI.LevelUpNoticeBadge>();
                    if (badgeComponent == null)
                        badgeComponent = background.gameObject.AddComponent<Onikiri.UI.LevelUpNoticeBadge>();

                    var badgeSo = new SerializedObject(badgeComponent);
                    badgeSo.FindProperty("badge").objectReferenceValue = badge.gameObject;
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

            // 퀘스트는 하단 탭이 아니라 플레이 화면의 아이콘이 연다 (#16).
            // 그 아이콘도 판이 다시 만들어지면 참조가 죽으므로 함께 물린다 -
            // 이 함수가 도는 이유가 정확히 그것이다
            RelinkQuestButton();

            WireScreenExclusivity();
        }

        /**
         * @brief 퀘스트 판을 여는 것들을 다시 물린다 (#16). 전투 화면 위에 산다.
         *
         * 아이콘(QuestButton)과 가이드 카드 - 둘 다 같은 판을 여는
         * HudScreenButton이라 판이 다시 만들어지면 함께 참조가 죽는다.
         */
        private static void RelinkQuestButton()
        {
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null) return;

            var screen = safeArea.Find(QuestPanelBuilder.PanelName);
            if (screen == null) return;

            foreach (var name in new[] { "QuestButton", GuideCardName })
            {
                var chip = safeArea.Find(name);
                var control = chip != null
                    ? chip.GetComponent<Onikiri.UI.HudScreenButton>() : null;
                if (control == null) continue;

                var so = new SerializedObject(control);
                so.FindProperty("screen").objectReferenceValue = screen.gameObject;
                // 팝업이라 아무것도 안 닫는다. 여기서도 비워둔다 - 상호 배타
                // 배선이 이 버튼들을 훑지 않으므로(하단 탭도 상단 바도 아니다)
                // 옛 세대의 목록이 남아 있으면 퀘스트를 열 때 보던 화면이 닫힌다
                so.FindProperty("otherScreens").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
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
            // #1 뒤로 **설정과 랭킹은 이 표에 없다.** 둘은 화면이 아니라
            // 팝업이 됐고, 팝업은 아래를 닫지 않는다 - 스킬 목록을 보다가
            // 랭킹을 열면 랭킹이 그 위에 뜨고, 닫으면 보던 스킬 목록으로
            // 돌아와야 한다. 그것이 화면과 팝업을 가르는 지점이다.
            //
            // 아래 화면이 눌릴 걱정은 없다. 딤이 화면 전체를 덮고 있어
            // 바깥을 누르면 팝업이 닫힐 뿐이다(PopupPanel).
            string[] names =
            {
                SkillPanelBuilder.PanelName, EquipmentPanelBuilder.PanelName,
                PetPanelBuilder.PanelName,
                ShopPanelBuilder.PanelName,
                HudScreensBuilder.StatsPanelName, HudScreensBuilder.RegionSelectPanelName
            };

            // 이 이름들이 켜질 때는 아무것도 닫지 않는다(팝업이므로).
            // 퀘스트가 여기로 옮겨왔다 (#16) - 이제 띠를 덮는 화면이 아니라
            // 그 위에 뜨는 팝업이고, 보던 화면을 닫지 않는다
            var popupNames = new List<string>
            {
                HudScreensBuilder.SettingsPanelName,
                LeaderboardPanelBuilder.PanelName,
                QuestPanelBuilder.PanelName
            };

            var popups = new List<GameObject>();
            foreach (var name in popupNames)
            {
                var found = safeArea.Find(name);
                if (found != null) popups.Add(found.gameObject);
            }

            var screens = new List<GameObject>();
            foreach (var name in names)
            {
                var found = safeArea.Find(name);
                if (found != null) screens.Add(found.gameObject);
            }

            if (bar != null)
                foreach (var tab in bar.GetComponentsInChildren<Onikiri.UI.LockedTab>(true))
                    FillOtherScreens(new SerializedObject(tab), screens, popups);

            if (topBar != null)
                foreach (var hudButton in topBar.GetComponentsInChildren<Onikiri.UI.HudScreenButton>(true))
                    FillOtherScreens(new SerializedObject(hudButton), screens, popups);

            // 성장 패널 헤더의 스탯 창 입구(개선안 v2 - 초상이 홈 버튼이
            // 되면서 넘겨받은 문)도 같은 상호 배타를 따른다
            var growth = MainSceneBuilder.FindBand("GrowthPanel");
            if (growth != null)
                foreach (var hudButton in growth.GetComponentsInChildren<Onikiri.UI.HudScreenButton>(true))
                    FillOtherScreens(new SerializedObject(hudButton), screens, popups);
        }

        /** own screen을 뺀 나머지를 otherScreens 배열에 적는다 */
        private static void FillOtherScreens(SerializedObject so, List<GameObject> screens,
                                             List<GameObject> popups)
        {
            var own = so.FindProperty("screen").objectReferenceValue as GameObject;

            // 팝업을 여는 버튼은 아무것도 닫지 않는다 (#1)
            if (own != null && popups.Contains(own))
            {
                so.FindProperty("otherScreens").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

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
