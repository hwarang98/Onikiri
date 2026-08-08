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
            public float SpawnWeight;
            public float Health;
            public float MoveSpeed;
            public float QueueSpacing;
            public float HoverHeight;
            public double Gold;
        }

        private static readonly EnemyTier[] Tiers =
        {
            // 필러: 가느다란 불꽃, 빨리 죽고, 화면이 비지 않게 한다
            new EnemyTier {
                Aseprite = "Inimig (4)", AssetName = "Enemy_Hitodama", DisplayName = "Hitodama",
                IdleClip = "Tag", DeathClip = "Tag_1",
                SpawnWeight = 3f, Health = 8f, MoveSpeed = 1.25f,
                QueueSpacing = 1.0f, HoverHeight = 0.35f, Gold = 2d
            },
            // 일반: 전투의 대부분을 차지한다
            new EnemyTier {
                Aseprite = "Inimig (7)", AssetName = "Enemy_Kourin", DisplayName = "Mossback",
                IdleClip = "Tag", DeathClip = "Tag_0",
                SpawnWeight = 5f, Health = 14f, MoveSpeed = 1.0f,
                QueueSpacing = 1.25f, HoverHeight = 0f, Gold = 5d
            },
            // 정예: 등롱은 의도적으로 사무라이보다 크므로 드물게 유지한다
            new EnemyTier {
                Aseprite = "Inimig (1)", AssetName = "Enemy_Chochin", DisplayName = "Chochin-obake",
                IdleClip = "Tag", DeathClip = "Tag_2",
                SpawnWeight = 1f, Health = 34f, MoveSpeed = 0.8f,
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

            foreach (var tier in Tiers) BuildEnemyDefinition(tier);
            BossContentBuilder.BuildDefinition();
            BossContentBuilder.BuildBosses();
            BuildEnemyPrefab();
            BuildDamageNumberPrefab();
            SakuraContentBuilder.BuildArt();
            ImpactSparkBuilder.BuildArt();

            var scene = EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath, OpenSceneMode.Single);

            // 씬을 연 '뒤에' 경로로 에셋을 다시 로드한다. 씬 로드 전에 만든 오브젝트
            // 참조는 그때 발생하는 재임포트로 무효화될 수 있고, 무효한 UnityEngine.Object를
            // SerializedProperty에 대입하면 아무 에러 없이 null이 기록된다.
            // 스포너에 프리팹이 비어 있던 원인이 정확히 이것이었다
            var definitions = new List<EnemyDefinition>();
            foreach (var tier in Tiers)
            {
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

            WireLockedTabs();
            WireStageAdvance();

            // 보스전은 강화 다음이다. 실패 문구가 "어느 축을 올려라"를 고르려면
            // UpgradeSystem이 이미 씬에 있어야 한다
            BossContentBuilder.Wire(spawner);

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
            definition.idleFrames = FramesFromClip(aseprite, tier.IdleClip);
            // 이 팩들에는 피격 태그가 없다. Enemy가 색 플래시로 대체하며,
            // 이 스프라이트 크기에서는 충분히 읽힌다
            definition.hurtFrames = new Sprite[0];
            definition.deathFrames = FramesFromClip(aseprite, tier.DeathClip);
            definition.frameRate = 12f;

            // 이 정의는 잡몹으로도, 그 스테이지의 '거대' 보스로도 쓰인다.
            // 공격 주기를 적어두지만 **잡몹은 이것으로 공격하지 않는다** -
            // Enemy는 스폰 시점에 받은 공격력이 0이면 주기를 아예 돌리지 않고,
            // 잡몹 스폰 경로는 0을 넘긴다. 보스로 스폰될 때만 깨어나는 값이다
            definition.attackInterval = (float)BossCurve.AttackIntervalSeconds;
            definition.attackImpactPoint = 0.5f;
            // 잡몹 팩에는 공격 태그가 없다. 비워 두면 Enemy가 idle을 유지한 채
            // 주기만 돌리고, 타격은 피격 플래시와 데미지 숫자로 읽힌다
            definition.attackFrames = new Sprite[0];

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
                "[Onikiri] Enemy '{0}': idle={1} death={2} frames, weight={3}, hp={4}, gold={5}, artBottom={6:F4}u",
                definition.displayName, definition.idleFrames.Length, definition.deathFrames.Length,
                definition.spawnWeight, definition.maxHealth, tier.Gold, definition.artBottomOffset));

            return definition;
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
                // 이중 참격이 그대로 재발한다 - 클립에 궤적이 이미 세 번 있다
                bool usesSlash = element.FindPropertyRelative("usesSlash").boolValue;
                if (spec.Shape == Onikiri.Progression.SkillShape.MultiHit && usesSlash)
                    problems.Add("'" + spec.DisplayName + "'이 팩 참격을 쓴다 - 클립의 "
                                 + "그려진 참격과 겹쳐 23단계의 이중 참격이 된다");

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
         * @brief 상단 둘째 줄의 글자가 상자에 들어가는지를 빌드가 직접 잰다.
         *
         * 이 줄은 두 번 잘렸고 두 번 다 원인이 같았다 - **글자 폭을 눈으로
         * 어림했기 때문이다.** 16단계에서 28px/글자로 잡았지만 55pt Galmuri의
         * 실측은 33.7px이었고, 20%의 오차가 레벨 41에서 터졌다. 어림이 틀렸다는
         * 신호는 스크린샷뿐이었다.
         *
         * 그래서 어림을 걷어내고 TMP에게 직접 묻는다. 세 라벨의 최악 문자열을
         * 실제 폰트로 재서 상자와 비교한다. 여기서 걸리면 빌드가 실패하므로,
         * 다음에 이 줄에 무언가를 더 넣는 사람은 스크린샷이 아니라 에러로
         * 알게 된다. 행 아이콘 검사, 글리프 검사와 같은 계열이다.
         *
         * 최악 문자열의 근거:
         *   "레벨 999"     3자리 레벨. 방치형 수명 안에 반드시 닿는다
         *   "레벨업 99"    쌓인 레벨업 개수. 쓸어담기 전까지 두 자리가 될 수 있다
         *   "999aa/999aa"  경험치 필요량 48 x 1.26^n 이 10^17에 닿는 레벨 173 부근
         */
        private static void VerifyExpRowFits(List<string> problems)
        {
            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar == null) return;

            // 라벨 경로, 폭을 잴 대상, 최악 문자열.
            // 경험치 라벨만 상자가 막대에서 안쪽 여백만큼 줄어든다
            CheckLabelFits(topBar, "LevelLabel", "레벨 999", LevelLabelWidth, problems);
            CheckLabelFits(topBar, "LevelUpButton/Label", "레벨업 99", LevelUpWidth, problems);

            var track = topBar.Find("ExpTrack") as RectTransform;
            if (track != null)
                CheckLabelFits(topBar, "ExpTrack/ExpLabel", "999aa/999aa",
                               track.rect.width - ExpLabelInset * 2f, problems);
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
            // 다른 규칙으로 읽힌다
            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar != null)
            {
                foreach (var name in new[] { "GoldIcon", "ExpIcon" })
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

            // 시트 폭 / 셀 폭 = 프레임 수. 자른 결과가 그것과 다르면 셀 크기가
            // 틀린 것이고, 화면에서는 프레임이 반씩 잘려 나온다
            if (config.idleSheet != null && config.cellWidth > 0)
            {
                int expected = config.idleSheet.width / config.cellWidth;
                if (config.idleSheet.width % config.cellWidth != 0)
                    problems.Add(string.Format("{0}: IDLE sheet {1}px does not divide by cell width {2}",
                        config.name, config.idleSheet.width, config.cellWidth));
                else if (definition.idleFrames != null && definition.idleFrames.Length != expected)
                    problems.Add(string.Format(
                        "{0}: idle clip has {1} frames but the sheet holds {2} - cell size is wrong",
                        config.name, definition.idleFrames.Length, expected));
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
                RequireReference(hudSo, "expLabel", problems);
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

            // 경험치가 빨려 들어갈 곳. 상단 바의 경험치 바이고, WireWalletAndHud가
            // 이미 세워뒀다 - 순서가 뒤바뀌면 여기서 null이 기록된다
            var topBar = MainSceneBuilder.FindBand("TopBar");
            var expTrack = topBar != null ? topBar.Find("ExpTrack") : null;
            so.FindProperty("expTarget").objectReferenceValue = expTrack;
            so.FindProperty("expColor").colorValue = ExpFillColor;

            so.ApplyModifiedPropertiesWithoutUndo();

            return spawner;
        }

        /**
         * @brief Battle 루트에 지갑/스테이지를, 상단 바에 골드·스테이지 표시를 붙인다.
         *
         * 상단 바는 세로 192px밖에 안 되므로 두 표시를 좌우로 나눈다. 골드는 왼쪽에서
         * 자릿수가 계속 늘어나고, 스테이지는 오른쪽에 붙어 폭이 거의 변하지 않는다.
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

            // 재화 아이콘. 성장 행이 심볼을 쓰기 시작했으므로 상단 바도 같은
            // 언어를 써야 한다 - 한쪽만 아이콘이면 두 화면이 다른 규칙으로 읽힌다
            EnsureBarIcon(topBar, "GoldIcon", UiIcons.Load(UiIcons.GoldIcon), new Vector2(48f, -40f));

            var goldLabel = EnsureHudLabel(topBar, "GoldLabel", TMPro.TextAlignmentOptions.Left,
                                           new Vector2(0f, 1f), new Vector2(0f, 1f),
                                           new Vector2(48f + BarIconSize + 12f, -46f));
            goldLabel.text = "골드 0";

            var stageLabel = EnsureHudLabel(topBar, "StageLabel", TMPro.TextAlignmentOptions.Right,
                                            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -46f));

            // 지역 표시가 붙어 길어졌다. "지역 4 · 7/10  처치 3/10" 이 최악이고
            // 55pt에서 627px이다
            ((RectTransform)stageLabel.transform).sizeDelta = new Vector2(660f, 72f);
            stageLabel.text = "지역 1 · 1/10  처치 0/10";

            var hud = topBar.GetComponent<Onikiri.UI.HUDCurrency>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.HUDCurrency>();

            var currencySo = new SerializedObject(hud);
            currencySo.FindProperty("label").objectReferenceValue = goldLabel;

            // "골드"라는 글자를 뺀다. 15단계에서 코인 아이콘을 앞에 붙였으므로
            // 그 두 글자는 같은 말을 두 번 하는 것이고, 55pt에서 112px을 먹는다.
            // 17단계에서 스테이지 표시가 "지역 4 · 7/10  처치 3/10"으로 길어지면서
            // 그 112px이 실제로 모자랐다
            currencySo.FindProperty("prefix").stringValue = string.Empty;
            currencySo.ApplyModifiedPropertiesWithoutUndo();

            // 보석. **골드 오른쪽에 붙인다.**
            //
            // 상단 바는 세로 192px에 좌우로 골드와 스테이지가 이미 있다. 보석을
            // 오른쪽 줄에 두면 스테이지 표시("지역 4 · 7/10  처치 3/10", 최악 627px)와
            // 자리를 다투므로, 왼쪽 골드 옆에 이어 붙인다 - 둘 다 재화라 한 묶음으로
            // 읽히는 것이 오히려 맞다.
            //
            // 골드 라벨의 폭을 잡아 그 오른쪽에 놓는다. 골드는 자릿수가 늘어나므로
            // 고정 폭을 주고 그만큼 띄운다
            ((RectTransform)goldLabel.transform).sizeDelta = new Vector2(GoldLabelWidth, 72f);

            EnsureBarIcon(topBar, "GemIcon", UiIcons.LoadItem(UiIcons.GemSprite),
                          new Vector2(48f + BarIconSize + 12f + GoldLabelWidth + 16f, -40f));

            var gemLabel = EnsureHudLabel(topBar, "GemLabel", TMPro.TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(48f + BarIconSize + 12f + GoldLabelWidth + 16f + BarIconSize + 10f, -46f));
            ((RectTransform)gemLabel.transform).sizeDelta = new Vector2(180f, 72f);
            gemLabel.text = "0";

            var gemHud = topBar.GetComponent<Onikiri.UI.HUDGems>();
            if (gemHud == null) gemHud = topBar.gameObject.AddComponent<Onikiri.UI.HUDGems>();

            var gemSo = new SerializedObject(gemHud);
            gemSo.FindProperty("label").objectReferenceValue = gemLabel;
            gemSo.ApplyModifiedPropertiesWithoutUndo();

            var stageHud = topBar.GetComponent<Onikiri.UI.HUDStage>();
            if (stageHud == null) stageHud = topBar.gameObject.AddComponent<Onikiri.UI.HUDStage>();

            var stageSo = new SerializedObject(stageHud);
            stageSo.FindProperty("label").objectReferenceValue = stageLabel;
            stageSo.FindProperty("prefix").stringValue = "스테이지 ";
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            BuildExpRow(topBar);
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
         * 31단계에 보석이 그 오른쪽에 붙으면서 필요해졌다. 그전에는 라벨이
         * 내용에 맞춰 늘어나도 오른쪽이 비어 있어 상관없었지만, 이제 그쪽에
         * 아이콘이 서므로 **자리를 확정해야** 골드가 길어질 때 겹치지 않는다.
         *
         * ## 260 -> 200. 32단계에 실측으로 줄였다
         *
         * 260은 "999.9M까지 들어가는 폭"이라고 적혀 있었는데 **어림이었다.**
         * TMP 실측:
         *
         *   "999.9M"    168px
         *   "999.9aa"   183px   <- 축약 단위가 두 글자가 되는 최악
         *   "100.2M"    161px
         *
         * 92px이 놀고 있었고, 그 뒤에 선 보석 라벨이 그만큼 오른쪽으로 밀려
         * **스테이지 문구와 5px까지 붙어 있었다.** 보석 세 자리("860" 87px)에서
         * 이미 그 상태이고, 네 자리("9999" 116px)면 24px 겹친다.
         *
         * 31단계에는 보석이 두 자리였고(퀘스트 몇 개분) 소비처가 없어 자릿수가
         * 늘 이유도 없었다. 32단계가 그 이유를 만들었다 - 등급업 하나가 40~260개라
         * 네 자리가 정상 구간이 된다. **재화에 소비처가 생기면 그 재화의 자릿수
         * 가정도 다시 재야 한다.**
         *
         * 200 = 183 + 17. 이 값도 어림이 아니라 위 실측에서 나온 것이고, 넘치면
         * 축약 단위가 하나 올라가 자릿수가 다시 줄어든다(NumberFormatter).
         */
        private const float GoldLabelWidth = 200f;

        /** 상단 바 아이콘 하나. 위 기준 앵커라 바 높이가 바뀌어도 위치가 유지된다 */
        private static UnityEngine.UI.Image EnsureBarIcon(
            Transform parent, string name, Sprite sprite, Vector2 anchoredPosition)
        {
            var image = EnsureImage(parent, name, UiIcons.Tint);

            var rect = (RectTransform)image.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(BarIconSize, BarIconSize);
            rect.anchoredPosition = anchoredPosition;

            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.color = sprite != null ? UiIcons.Tint : new Color(1f, 0f, 1f, 0.35f);
            image.raycastTarget = false;
            return image;
        }

        /** 상단 바 둘째 줄의 세로 위치와 높이. 골드/스테이지 줄(46~118) 바로 아래다 */
        private const float ExpRowTop = 126f;
        private const float ExpRowHeight = 60f;

        /**
         * @brief 레벨 라벨과 레벨업 버튼의 폭.
         *
         * 16단계에서 한 번 줄였는데 그 계산이 틀렸다. **글자 폭을 28px로 어림했고,
         * 실측은 33.7px이었다.** 그래서 레벨 41에서 경험치 숫자가 다시 잘렸고
         * ("248.4K/496.…"), 더 나쁘게는 "레벨 999"(실측 236)가 상자 200을 이미
         * 넘고 있었다 - 3자리 레벨에 닿는 순간 터질 예정이었다.
         *
         * 어림은 두 번 틀렸으므로 이번 폭은 전부 TMP 실측값이다:
         *
         *   레벨 라벨   "레벨 999"      236  ->  240
         *   레벨업 버튼 "레벨업 99"     254  ->  260
         *   경험치 라벨 "999aa/999aa"   378  ->  382 (막대 402 - 안쪽 여백 20)
         *
         * 합이 1080에 4~6px 여유로 들어간다. 여유가 이만큼밖에 없다는 것은
         * 55pt 네 요소가 1080의 한계라는 뜻이고, 그래서 경험치 숫자에서 소수
         * 자리를 뗐다(LevelHud). 다시 틀리지 않도록 VerifyExpRowFits가 빌드에서
         * 최악의 문자열을 직접 재서 검사한다 - 어림은 더 쓰지 않는다.
         */
        private const float LevelLabelWidth = 240f;
        private const float LevelUpWidth = 260f;

        /** 경험치 숫자를 막대 테두리 안쪽으로 미는 좌우 여백. 9-슬라이스 테두리 두께 */
        private const float ExpLabelInset = 10f;

        private static readonly Color ExpTrackColor = new Color32(0x2A, 0x25, 0x3C, 0xFF);
        private static readonly Color ExpFillColor = new Color32(0x7C, 0xC5, 0x9A, 0xFF);
        private static readonly Color LevelUpColor = new Color32(0x4E, 0x7A, 0x5C, 0xFF);

        /**
         * @brief 상단 바 둘째 줄: 레벨, 경험치 바, 레벨업 버튼.
         *
         * 상단 바는 192px뿐이라 두 줄이 한계다. 그래서 레벨업 버튼을 성장 패널이
         * 아니라 여기에 둔다 - 패널은 스크롤이라 버튼이 화면 밖으로 밀려날 수 있고,
         * 레벨업은 "지금 누를 수 있다"가 보여야 의미가 있는 조작이다.
         *
         * 경험치 바 위에 숫자를 겹쳐 올린다. 바만 있으면 얼마나 남았는지 어림밖에
         * 안 되고, 숫자만 있으면 방치 중에 늘어나는 것이 눈에 걸리지 않는다.
         */
        private static void BuildExpRow(Transform topBar)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(GalmuriFontPath);

            var levelLabel = EnsureHudLabel(topBar, "LevelLabel", TMPro.TextAlignmentOptions.Left,
                                            new Vector2(0f, 1f), new Vector2(0f, 1f),
                                            new Vector2(48f, -ExpRowTop));
            ((RectTransform)levelLabel.transform).sizeDelta = new Vector2(LevelLabelWidth, ExpRowHeight);
            levelLabel.text = "레벨 1";

            // 바는 왼쪽 레벨 라벨과 오른쪽 버튼 사이를 채운다. 양쪽 앵커를 쓰면
            // 상단 바 폭이 바뀌어도(태블릿) 가운데가 알아서 늘어난다
            // 상단 바 전체에 어두운 판을 깐다. 배경 위에 글자만 떠 있으면 벚꽃의
            // 밝은 부분에서 골드 숫자가 읽히지 않는다
            var chrome = EnsureImage(topBar, "Chrome", UiSkin.Chrome);
            var chromeRect = (RectTransform)chrome.transform;
            chromeRect.anchorMin = Vector2.zero;
            chromeRect.anchorMax = Vector2.one;
            chromeRect.offsetMin = Vector2.zero;
            chromeRect.offsetMax = Vector2.zero;
            chrome.raycastTarget = false;
            UiSkin.ApplyPanel(chrome, UiSkin.Chrome);

            // 판은 라벨보다 뒤에 있어야 한다. 나중에 만든 자식이 위에 그려지므로
            // 맨 앞으로 보낸다
            chrome.transform.SetAsFirstSibling();

            // 경험치 별. 바 바로 왼쪽에 붙여 이 바가 무엇의 바인지 말한다
            float expIconX = 48f + LevelLabelWidth + 8f;
            EnsureBarIcon(topBar, "ExpIcon", UiIcons.Load(UiIcons.ExpIcon),
                          new Vector2(expIconX, -(ExpRowTop + (ExpRowHeight - BarIconSize) * 0.5f)));

            var track = EnsureImage(topBar, "ExpTrack", ExpTrackColor);
            // 안쪽으로 파인 판. "여기는 눌리지 않는다"가 모양으로 읽힌다
            UiSkin.ApplyPanel(track, UiSkin.Inlay, UiSkin.InlayTint);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0f, 1f);
            trackRect.anchorMax = new Vector2(1f, 1f);
            trackRect.pivot = new Vector2(0.5f, 1f);
            trackRect.offsetMin = new Vector2(expIconX + BarIconSize + 10f, -(ExpRowTop + ExpRowHeight));
            trackRect.offsetMax = new Vector2(-(48f + LevelUpWidth + 16f), -ExpRowTop);

            var fill = EnsureImage(track.transform, "Fill", ExpFillColor);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            var expLabel = EnsureHudLabel(track.transform, "ExpLabel", TMPro.TextAlignmentOptions.Center,
                                          new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            var expRect = (RectTransform)expLabel.transform;
            expRect.anchorMin = Vector2.zero;
            expRect.anchorMax = Vector2.one;
            // 가로는 테두리 안쪽으로 밀고, **세로는 오히려 넓힌다.**
            //
            // Ellipsis는 가로뿐 아니라 세로가 모자라도 잘라내는데, 잘릴 것이
            // 한 줄뿐이면 통째로 사라진다. 처음에 세로를 막대 높이(60)에
            // 맞췄다가 55pt 글자의 라인 높이가 그것을 넘어 **숫자가 화면에서
            // 아예 안 보였다.** 세로로 넘치는 것은 문제가 아니므로 풀어준다
            expRect.offsetMin = new Vector2(ExpLabelInset, -14f);
            expRect.offsetMax = new Vector2(-ExpLabelInset, 14f);
            expLabel.fontSize = Onikiri.UI.PixelFontSizes.GalmuriSmall;

            // 가로로 넘치면 막대 밖으로 새는 대신 안에서 잘린다. 위에서 폭을
            // 넉넉히 잡았지만 숫자는 자릿수가 계속 늘어나는 값이라(999.9aa)
            // 언젠가 다시 넘친다 - 그때 레벨업 버튼을 덮는 것보다 잘리는 편이 낫다
            expLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            expLabel.text = "0/30";

            // 레벨업 버튼은 올릴 수 있을 때만 켜진다. 루트를 따로 두는 이유는
            // 버튼과 라벨을 한 번에 껐다 켜기 위해서다
            var levelUpRoot = EnsureImage(topBar, "LevelUpButton", LevelUpColor);
            var buttonRect = (RectTransform)levelUpRoot.transform;
            buttonRect.anchorMin = new Vector2(1f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.sizeDelta = new Vector2(LevelUpWidth, ExpRowHeight);
            buttonRect.anchoredPosition = new Vector2(-48f, -ExpRowTop);

            UiSkin.ApplyPanel(levelUpRoot, UiSkin.Panel, UiSkin.Good);

            var button = levelUpRoot.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = levelUpRoot.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.ApplyButton(button, levelUpRoot);

            var levelUpLabel = EnsureHudLabel(levelUpRoot.transform, "Label",
                                              TMPro.TextAlignmentOptions.Center,
                                              new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            var levelUpRect = (RectTransform)levelUpLabel.transform;
            levelUpRect.anchorMin = Vector2.zero;
            levelUpRect.anchorMax = Vector2.one;
            levelUpRect.offsetMin = Vector2.zero;
            levelUpRect.offsetMax = Vector2.zero;
            levelUpLabel.text = "레벨업";

            levelUpRoot.gameObject.SetActive(false);

            var hud = topBar.GetComponent<Onikiri.UI.LevelHud>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.LevelHud>();

            var so = new SerializedObject(hud);
            so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            so.FindProperty("expFill").objectReferenceValue = fill;
            so.FindProperty("expLabel").objectReferenceValue = expLabel;
            so.FindProperty("levelUpRoot").objectReferenceValue = levelUpRoot.gameObject;
            so.FindProperty("levelUpButton").objectReferenceValue = button;
            so.FindProperty("levelUpLabel").objectReferenceValue = levelUpLabel;
            so.FindProperty("levelPrefix").stringValue = "레벨 ";
            so.ApplyModifiedPropertiesWithoutUndo();
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
            new LockedTabSpec {
                Name = "스킬",
                RequiredLevel = Onikiri.Progression.SkillCatalog.PanelUnlockLevel,
                ScreenName = SkillPanelBuilder.PanelName
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
            }
        };

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
                rect.offsetMin = new Vector2(12f, 12f);
                rect.offsetMax = new Vector2(-12f, -12f);

                var button = background.GetComponent<UnityEngine.UI.Button>();
                if (button == null) button = background.gameObject.AddComponent<UnityEngine.UI.Button>();
                UiSkin.ApplyButton(button, background);

                var label = EnsureHudLabel(background.transform, "Label",
                                           TMPro.TextAlignmentOptions.Center,
                                           new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                label.text = spec.RequiredStage > 0
                    ? spec.Name + " " + spec.RequiredStage + "스테이지"
                    : spec.Name + " Lv." + spec.RequiredLevel;

                var tab = background.GetComponent<Onikiri.UI.LockedTab>();
                if (tab == null) tab = background.gameObject.AddComponent<Onikiri.UI.LockedTab>();

                var so = new SerializedObject(tab);
                so.FindProperty("displayName").stringValue = spec.Name;
                so.FindProperty("requiredLevel").intValue = spec.RequiredLevel;
                so.FindProperty("requiredStage").intValue = spec.RequiredStage;
                so.FindProperty("button").objectReferenceValue = button;
                so.FindProperty("label").objectReferenceValue = label;
                so.FindProperty("background").objectReferenceValue = background;

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
            }
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
