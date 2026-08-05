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
        // 흰 참격. 팩의 적색 시트를 max 채널로 무채화해 생성했다. 무기 등급을
        // 흰색 -> 적색 -> 금색으로 설계했으므로 기본 칼날은 흰색이어야 하고,
        // 유채색 시트는 업그레이드용으로 남겨둔다.
        private const string SlashSheet = "Assets/_Project/Art/VFX/Slash_White.png";
        private const string SamuraiIdle = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/IDLE.png";
        private const string SamuraiAttack = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/ATTACK 1.png";

        private const string DataFolder = "Assets/_Project/Data";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string EnemyPrefabPath = PrefabFolder + "/Enemy.prefab";
        private const string SlashPrefabPath = PrefabFolder + "/SlashVfx.prefab";

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

        /**
         * @brief 임팩트 이펙트로 쓰는 참격 시트의 첫 프레임 (0-기반).
         *
         * 시트의 아홉 프레임은 예비 동작(0~4)과 굵은 호(5~8)로 나뉜다.
         */
        private const int SlashImpactFirstFrame = 5;

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
            BuildEnemyPrefab();
            BuildSlashPrefab();
            BuildDamageNumberPrefab();
            SakuraContentBuilder.BuildArt();

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
            var slashPrefab = LoadPrefabComponent<SlashVfx>(SlashPrefabPath);

            if (definitions.Count == 0 || enemyPrefab == null || slashPrefab == null)
            {
                Debug.LogError("[Onikiri] Combat assets missing after build: definitions=" + definitions.Count
                               + " enemy=" + (enemyPrefab != null) + " slash=" + (slashPrefab != null));
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
            WirePlayerCombat(spawner, slashPrefab, shake, hitAudio, damageNumbers);

            // 강화는 PlayerCombat이 씬에 있어야 배선할 수 있다
            UpgradePanelBuilder.Build();

            // 보스전은 강화 다음이다. 실패 문구가 "어느 축을 올려라"를 고르려면
            // UpgradeSystem이 이미 씬에 있어야 한다
            BossContentBuilder.Wire(spawner);

            // 세션은 마지막이다. 강화·스테이지·전투가 전부 자리를 잡은 뒤라야
            // 세이브를 복원할 대상을 찾을 수 있다
            var offlinePopup = WireOfflinePopup();
            WireSession(spawner, offlinePopup);

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
            RequireReference(combatSo, "slashPrefab", problems);
            RequireReference(combatSo, "cameraShake", problems);
            RequireReference(combatSo, "hitAudio", problems);
            RequireArray(combatSo, "idleFrames", problems);
            RequireArray(combatSo, "attackFrames", problems);
            RequireArray(combatSo, "slashFrames", problems);

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
                    RequireArray(upgradeSo, "tracks", problems);
                }
            }

            if (Object.FindFirstObjectByType<Onikiri.UI.SafeAreaFitter>() == null)
                problems.Add("No SafeAreaFitter - UI will run under the notch");

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
            }

            if (problems.Count > 0)
            {
                Debug.LogError("[Onikiri] Combat wiring incomplete:\n  " + string.Join("\n  ", problems.ToArray()));
                return false;
            }
            return true;
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
            // 오프셋 5는 아트 픽셀 하나다(Pixel Perfect 배율과 같은 값). 그보다 작으면
            // 그림자가 픽셀 격자 사이에 놓여 글자 가장자리가 지저분해진다
            var shadow = CreateDamageLabel(root.transform, font, "Shadow", new Vector2(5f, -5f));
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

        private static SlashVfx BuildSlashPrefab()
        {
            var root = new GameObject("SlashVfx");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingOrders.Vfx;
            root.AddComponent<SpriteAnimator>();
            root.AddComponent<SlashVfx>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, SlashPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<SlashVfx>();
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

            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar == null) return;

            var goldLabel = EnsureHudLabel(topBar, "GoldLabel", TMPro.TextAlignmentOptions.Left,
                                           new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -46f));
            goldLabel.text = "골드 0";

            var stageLabel = EnsureHudLabel(topBar, "StageLabel", TMPro.TextAlignmentOptions.Right,
                                            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -46f));
            stageLabel.text = "스테이지 1  0/10";

            var hud = topBar.GetComponent<Onikiri.UI.HUDCurrency>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.HUDCurrency>();

            var currencySo = new SerializedObject(hud);
            currencySo.FindProperty("label").objectReferenceValue = goldLabel;
            currencySo.FindProperty("prefix").stringValue = "골드 ";
            currencySo.ApplyModifiedPropertiesWithoutUndo();

            var stageHud = topBar.GetComponent<Onikiri.UI.HUDStage>();
            if (stageHud == null) stageHud = topBar.gameObject.AddComponent<Onikiri.UI.HUDStage>();

            var stageSo = new SerializedObject(stageHud);
            stageSo.FindProperty("label").objectReferenceValue = stageLabel;
            stageSo.FindProperty("prefix").stringValue = "스테이지 ";
            stageSo.ApplyModifiedPropertiesWithoutUndo();
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

        /**
         * @brief 방치 보상 팝업.
         *
         * 안전 영역 루트의 마지막 자식이라 다른 밴드 위에 그려진다. 전체를 덮는
         * 어두운 판이 뒤의 강화 버튼 입력을 막는 역할도 한다 - 팝업이 떠 있는데
         * 뒤가 눌리면 보상을 확인하기 전에 강화가 되어버린다.
         */
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
            boxRect.sizeDelta = new Vector2(920f, 560f);
            boxRect.anchoredPosition = Vector2.zero;
            box.AddComponent<UnityEngine.UI.Image>().color = PopupBoxColor;

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
            buttonImage.color = new Color32(0x6E, 0x68, 0xA0, 0xFF);
            var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = buttonImage;

            var buttonLabel = CreatePopupLabel(buttonObject.transform, font, "Label",
                                               Onikiri.UI.PixelFontSizes.GalmuriSmall, -20f);
            buttonLabel.text = "받기";

            var popup = root.AddComponent<Onikiri.UI.OfflineRewardPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("durationLabel").objectReferenceValue = duration;
            so.FindProperty("amountLabel").objectReferenceValue = amount;
            so.FindProperty("claimButton").objectReferenceValue = button;
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
        private static void WireSession(EnemySpawner spawner, Onikiri.UI.OfflineRewardPopup popup)
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
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("offlinePopup").objectReferenceValue = popup;
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

        private static void WirePlayerCombat(EnemySpawner spawner, SlashVfx slashPrefab,
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
            so.FindProperty("slashPrefab").objectReferenceValue = slashPrefab;
            so.FindProperty("vfxParent").objectReferenceValue = vfxRoot;
            so.FindProperty("cameraShake").objectReferenceValue = shake;
            so.FindProperty("hitAudio").objectReferenceValue = hitAudio;
            so.FindProperty("damageNumbers").objectReferenceValue = damageNumbers;

            AssignSprites(so.FindProperty("idleFrames"), OrderedSprites(SamuraiIdle));
            AssignSprites(so.FindProperty("attackFrames"), OrderedSprites(SamuraiAttack));

            // 참격 시트의 뒤쪽 절반만 쓴다. 앞 다섯 프레임은 타격으로 이어지는 가느다란
            // 예비 동작이고 6~9번이 굵은 호다. 히트스톱은 임팩트 순간 표시 중인 프레임에서
            // 멈추므로, 이펙트는 가장 강한 프레임에서 시작해야 한다. 아니면 정지 화면이
            // 흐릿한 얼룩이 된다
            var slash = OrderedSprites(SlashSheet);
            AssignSprites(so.FindProperty("slashFrames"), slash.GetRange(
                Mathf.Min(SlashImpactFirstFrame, slash.Count - 1),
                Mathf.Max(1, slash.Count - SlashImpactFirstFrame)));

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
            so.FindProperty("slashFrameRate").floatValue = 22f;
            so.FindProperty("slashBudgetPerSecond").floatValue = 0.45f;
            // 요괴의 렌더링된 중심을 기준으로 재므로, 피벗 위치를 보정할 필요 없이
            // 호를 칼 쪽으로 조금 당기기만 하면 된다
            so.FindProperty("slashOffset").vector2Value = new Vector2(-0.3f, 0f);

            // 꽃잎은 참격이 지나간 방향으로 흩어진다. 사무라이의 발도는 오른쪽 위로
            // 향하고, 그 방향이 아트의 흰 궤적과 같아야 참격과 꽃잎이 한 동작으로 읽힌다
            var sakura = SakuraContentBuilder.Wire(vfxRoot);
            so.FindProperty("sakura").objectReferenceValue = sakura;
            so.FindProperty("slashDirection").vector2Value = new Vector2(1f, 0.45f);

            so.ApplyModifiedPropertiesWithoutUndo();

            WirePlayerHealth(samurai, animator, sakura);

            Debug.Log(string.Format("[Onikiri] Player combat: idle={0} attack={1} slash={2} frames.",
                OrderedSprites(SamuraiIdle).Count,
                OrderedSprites(SamuraiAttack).Count,
                OrderedSprites(SlashSheet).Count));
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
