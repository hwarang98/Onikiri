using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Onikiri.Battle;
using Onikiri.Core;

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

        /** 처치음 뱅크. 팩에 사망음이 하나뿐이고 그것이 정답이다 */
        private static readonly string[] KillClipPaths =
        {
            SfxRoot + "10_Battle_SFX/69_Enemy_death_01.wav"
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

            foreach (var tier in Tiers) BuildEnemyDefinition(tier);
            BuildEnemyPrefab();
            BuildSlashPrefab();
            BuildDamageNumberPrefab();

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
            var shake = WireCameraShake();
            var hitAudio = WireHitAudio();
            WireWalletAndHud();
            var damageNumbers = WireDamageNumbers();

            var spawner = WireSpawner(definitions, enemyPrefab);
            WirePlayerCombat(spawner, slashPrefab, shake, hitAudio, damageNumbers);

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
            rect.sizeDelta = new Vector2(260f, 60f);

            // 그림자를 먼저 만들어 뒤에 그려지게 하고 화면 픽셀 몇 개만큼 밀어둔다.
            // 비트맵 폰트는 SDF 아웃라인을 쓸 수 없고, 그림자가 없으면 흰 참격 위에
            // 올라가는 순간 숫자를 읽을 수 없다
            var shadow = CreateDamageLabel(root.transform, font, "Shadow", new Vector2(3f, -3f));
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
            var band = canvas.transform.Find("BattleArea");
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

        /** Battle 루트에 지갑을, 상단 바에 골드 표시를 붙인다 */
        private static void WireWalletAndHud()
        {
            var battle = GameObject.Find("Battle");
            if (battle.GetComponent<Onikiri.Progression.PlayerWallet>() == null)
                battle.AddComponent<Onikiri.Progression.PlayerWallet>();

            var canvas = GameObject.Find("UI Canvas");
            var topBar = canvas.transform.Find("TopBar");
            if (topBar == null) return;

            var existing = topBar.Find("GoldLabel");
            TMPro.TextMeshProUGUI label;
            if (existing != null)
            {
                label = existing.GetComponent<TMPro.TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject("GoldLabel", typeof(RectTransform));
                go.transform.SetParent(topBar, false);
                label = go.AddComponent<TMPro.TextMeshProUGUI>();

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(48f, 0f);
                rect.offsetMax = new Vector2(-48f, -40f);
            }

            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(GalmuriFontPath);
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            label.text = "골드 0";
            // 33은 아틀라스를 구운 크기다. 글리프가 비트맵과 1:1로 그려진다
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriSmall;
            label.alignment = TMPro.TextAlignmentOptions.Left;
            label.color = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
            label.raycastTarget = false;

            var hud = topBar.GetComponent<Onikiri.UI.HUDCurrency>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.HUDCurrency>();

            var so = new SerializedObject(hud);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("prefix").stringValue = "골드 ";
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
            so.FindProperty("attacksPerSecond").floatValue = 1.15f;
            SetBigDouble(so.FindProperty("damage"), 5d);
            // 사무라이 아트에는 7프레임 중 5~6번에 이미 흰 검격 궤적이 그려져 있다.
            // 임팩트를 그 프레임에 맞춰서 그려진 궤적, 참격 이펙트, 피격 플래시, 정지가
            // 순차가 아니라 동시에 일어나게 한다
            so.FindProperty("impactPoint").floatValue = 4f / 7f;
            so.FindProperty("hitStopSeconds").floatValue = 0.07f;
            so.FindProperty("hitStopBudgetPerSecond").floatValue = 0.3f;
            so.FindProperty("shakeSeconds").floatValue = 0.1f;
            so.FindProperty("shakeBudgetPerSecond").floatValue = 0.4f;
            so.FindProperty("shakePixels").floatValue = 3f;
            so.FindProperty("slashFrameRate").floatValue = 22f;
            // 요괴의 렌더링된 중심을 기준으로 재므로, 피벗 위치를 보정할 필요 없이
            // 호를 칼 쪽으로 조금 당기기만 하면 된다
            so.FindProperty("slashOffset").vector2Value = new Vector2(-0.3f, 0f);

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(string.Format("[Onikiri] Player combat: idle={0} attack={1} slash={2} frames.",
                OrderedSprites(SamuraiIdle).Count,
                OrderedSprites(SamuraiAttack).Count,
                OrderedSprites(SlashSheet).Count));
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
