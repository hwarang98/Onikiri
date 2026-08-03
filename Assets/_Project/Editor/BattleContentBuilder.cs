using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Onikiri.Battle;
using Onikiri.Core;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Builds the step-4 combat content: enemy definition asset, enemy and slash prefabs,
    /// then wires the spawner and the samurai's combat into Main.unity.
    ///
    /// Re-runnable. Run after Onikiri/Scene/Build Battle Stage.
    /// </summary>
    public static class BattleContentBuilder
    {
        /// <summary>
        /// One enemy tier. Sizes are the measured idle-frame art, against a 34px samurai.
        ///
        /// The mix is authored by visual mass rather than height alone: the hitodama is
        /// nearly as tall as the samurai (32px) but only 20px wide, so it reads as a thin
        /// wisp, while Inimig (7) is shorter at 29px yet 32px wide and carries far more
        /// presence. Weights make the small types common and the elite rare.
        /// </summary>
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
            // Filler: thin wisp, dies fast, keeps the screen busy.
            new EnemyTier {
                Aseprite = "Inimig (4)", AssetName = "Enemy_Hitodama", DisplayName = "Hitodama",
                IdleClip = "Tag", DeathClip = "Tag_1",
                SpawnWeight = 3f, Health = 8f, MoveSpeed = 1.25f,
                QueueSpacing = 1.0f, HoverHeight = 0.35f, Gold = 2d
            },
            // Common: the bulk of the encounter.
            new EnemyTier {
                Aseprite = "Inimig (7)", AssetName = "Enemy_Kourin", DisplayName = "Mossback",
                IdleClip = "Tag", DeathClip = "Tag_0",
                SpawnWeight = 5f, Health = 14f, MoveSpeed = 1.0f,
                QueueSpacing = 1.25f, HoverHeight = 0f, Gold = 5d
            },
            // Elite: the lantern out-sizes the samurai on purpose, so it stays rare.
            new EnemyTier {
                Aseprite = "Inimig (1)", AssetName = "Enemy_Chochin", DisplayName = "Chochin-obake",
                IdleClip = "Tag", DeathClip = "Tag_2",
                SpawnWeight = 1f, Health = 34f, MoveSpeed = 0.8f,
                QueueSpacing = 1.5f, HoverHeight = 0.1f, Gold = 18d
            }
        };

        private const string EnemyFolder = "Assets/ThirdParty/Enemies/FeudalJapan/";
        // White slash, generated from the pack's red sheet by collapsing hue to the max
        // channel. Weapon tiers are planned as white -> red -> gold, so the base blade has
        // to be white and the coloured sheets stay reserved for upgrades.
        private const string SlashSheet = "Assets/_Project/Art/VFX/Slash_White.png";
        private const string SamuraiIdle = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/IDLE.png";
        private const string SamuraiAttack = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/ATTACK 1.png";

        private const string DataFolder = "Assets/_Project/Data";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string EnemyPrefabPath = PrefabFolder + "/Enemy.prefab";
        private const string SlashPrefabPath = PrefabFolder + "/SlashVfx.prefab";

        /// <summary>Impact sounds are auto-wired from here once the files exist.</summary>
        private const string HitAudioFolder = "Assets/_Project/Audio/Hits";

        /// <summary>
        /// First frame of the slash sheet used for the impact effect (0-based). The sheet's
        /// nine frames are a wind-up (0-4) followed by the heavy arc (5-8).
        /// </summary>
        private const int SlashImpactFirstFrame = 5;

        [MenuItem("Onikiri/Scene/Build Combat Content")]
        public static void Build()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);

            foreach (var tier in Tiers) BuildEnemyDefinition(tier);
            BuildEnemyPrefab();
            BuildSlashPrefab();

            var scene = EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath, OpenSceneMode.Single);

            // Re-load the assets by path AFTER opening the scene. Object references created
            // before the scene load can be invalidated by the reimport it triggers, and
            // assigning a stale UnityEngine.Object to a SerializedProperty writes null
            // without any error - which is exactly how the spawner ended up prefab-less.
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

            var shake = WireCameraShake();
            var hitAudio = WireHitAudio();
            WireWalletAndHud();

            var spawner = WireSpawner(definitions, enemyPrefab);
            WirePlayerCombat(spawner, slashPrefab, shake, hitAudio);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (!VerifyWiring()) return;
            Debug.Log("[Onikiri] Combat content built.");
        }

        // ---------------------------------------------------------------- enemy data

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
            // None of these packs ship a hurt tag; Enemy falls back to a colour flash,
            // which reads fine at this sprite size.
            definition.hurtFrames = new Sprite[0];
            definition.deathFrames = FramesFromClip(aseprite, tier.DeathClip);
            definition.frameRate = 12f;
            definition.spawnWeight = tier.SpawnWeight;
            definition.maxHealth = tier.Health;
            definition.moveSpeed = tier.MoveSpeed;
            definition.queueSpacing = tier.QueueSpacing;
            definition.hoverHeight = tier.HoverHeight;
            definition.goldReward = BigDouble.FromDouble(tier.Gold);

            // Measure where the art actually starts inside its canvas. bounds.min.y is the
            // distance from the pivot (canvas bottom) to the lowest drawn pixel, which is
            // exactly the correction Enemy needs to sit the yokai on the ground.
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

        /// <summary>
        /// Pulls the ordered sprite list out of an imported Aseprite animation clip.
        /// The clip is the only thing that knows which frames belong to which tag, so we
        /// read the keyframes rather than guessing frame ranges by index.
        /// </summary>
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
                    // Aseprite writes a duplicate trailing key to hold the last frame.
                    if (sprite == null) continue;
                    if (frames.Count > 0 && frames[frames.Count - 1] == sprite && i == keys.Length - 1) continue;
                    frames.Add(sprite);
                }
            }
            return frames.ToArray();
        }

        // ---------------------------------------------------------------- prefabs

        /// <summary>
        /// Reads every wired reference back out of the saved scene.
        ///
        /// SerializedProperty assignment fails silently when handed a stale object, so
        /// "the builder ran without errors" proves nothing on its own. This turns that
        /// class of bug into a build-time error instead of a null reference at play time.
        /// </summary>
        private static bool VerifyWiring()
        {
            var problems = new List<string>();

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

        // ---------------------------------------------------------------- scene wiring

        /// <summary>
        /// Applies the explicit sorting order table and lifts the grass layer in front of
        /// the fighters so it crosses their feet.
        /// </summary>
        /// <summary>Puts the shake on the camera and tells the layout to ignore it.</summary>
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

        /// <summary>
        /// Creates the hit-sound player and fills it from <see cref="HitAudioFolder"/>.
        /// The folder is allowed to be empty - combat calls into it unconditionally and it
        /// stays silent until sound files are dropped in, so nothing has to be rewired later.
        /// </summary>
        private static HitAudio WireHitAudio()
        {
            var battle = GameObject.Find("Battle");
            var audio = battle.GetComponent<HitAudio>();
            if (audio == null) audio = battle.AddComponent<HitAudio>();

            var clips = new List<AudioClip>();
            if (AssetDatabase.IsValidFolder(HitAudioFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { HitAudioFolder }))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (clip != null) clips.Add(clip);
                }
                clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }

            var so = new SerializedObject(audio);
            var array = so.FindProperty("clips");
            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            if (clips.Count == 0)
                Debug.LogWarning("[Onikiri] No hit sounds in " + HitAudioFolder +
                                 " - combat will be silent. Drop .wav files there and rebuild.");
            else
                Debug.Log("[Onikiri] Hit sounds wired: " + clips.Count);

            return audio;
        }

        /// <summary>Wallet on the Battle root plus a gold readout in the top bar.</summary>
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

            // Placeholder styling: the pixel font (Thaleah) is not in the project yet, so
            // this uses TMP's default face purely so the value is visible and verifiable.
            label.text = "G 0";
            label.fontSize = 64f;
            label.alignment = TMPro.TextAlignmentOptions.Left;
            label.color = new Color32(0xF6, 0xE5, 0xBF, 0xFF);

            var hud = topBar.GetComponent<Onikiri.UI.HUDCurrency>();
            if (hud == null) hud = topBar.gameObject.AddComponent<Onikiri.UI.HUDCurrency>();

            var so = new SerializedObject(hud);
            so.FindProperty("label").objectReferenceValue = label;
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
            // Samurai now stands at -1.2, so the front of the queue moves right with him to
            // keep a readable gap between the blade and the leading yokai.
            so.FindProperty("frontLineX").floatValue = 0.05f;
            so.FindProperty("prewarm").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();

            // HitStop lives on the same object so it is guaranteed present in play mode.
            if (battle.GetComponent<HitStop>() == null) battle.AddComponent<HitStop>();

            return spawner;
        }

        private static void WirePlayerCombat(EnemySpawner spawner, SlashVfx slashPrefab,
                                             ScreenShake shake, HitAudio hitAudio)
        {
            var samurai = GameObject.Find("Samurai");
            if (samurai == null) { Debug.LogError("[Onikiri] Samurai not found."); return; }

            // Step 3 drove the idle with an Animator. Combat needs frame-level control of
            // the impact moment, so the samurai moves to the same SpriteAnimator the
            // enemies use.
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

            AssignSprites(so.FindProperty("idleFrames"), OrderedSprites(SamuraiIdle));
            AssignSprites(so.FindProperty("attackFrames"), OrderedSprites(SamuraiAttack));

            // Only the back half of the slash sheet. The first five frames are thin wisps
            // that build up to the strike; frames 6-9 are the heavy arc. Because a hitstop
            // freezes on whatever frame is showing at impact, the effect has to open on its
            // strongest frame or the held frame is a faint smear.
            var slash = OrderedSprites(SlashSheet);
            AssignSprites(so.FindProperty("slashFrames"), slash.GetRange(
                Mathf.Min(SlashImpactFirstFrame, slash.Count - 1),
                Mathf.Max(1, slash.Count - SlashImpactFirstFrame)));

            // Tuning is written explicitly rather than left to script defaults: the
            // component already exists in the scene, so changing a default in code would
            // never reach it and the builder would stop being the source of truth.
            so.FindProperty("attackRange").floatValue = 2.0f;
            so.FindProperty("attacksPerSecond").floatValue = 1.15f;
            so.FindProperty("damage").floatValue = 5f;
            // The samurai art already paints a white sword trail into attack frames 5-6 of
            // 7. Impact is timed to land on that frame so the painted arc, the slash effect,
            // the hit flash and the freeze all happen together instead of in sequence.
            so.FindProperty("impactPoint").floatValue = 4f / 7f;
            so.FindProperty("hitStopSeconds").floatValue = 0.07f;
            so.FindProperty("hitStopBudgetPerSecond").floatValue = 0.3f;
            so.FindProperty("shakeSeconds").floatValue = 0.1f;
            so.FindProperty("shakeBudgetPerSecond").floatValue = 0.4f;
            so.FindProperty("shakePixels").floatValue = 3f;
            so.FindProperty("slashFrameRate").floatValue = 22f;
            // Measured from the yokai's rendered centre, so it only needs to nudge the arc
            // back towards the blade rather than compensate for pivot placement.
            so.FindProperty("slashOffset").vector2Value = new Vector2(-0.3f, 0f);

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(string.Format("[Onikiri] Player combat: idle={0} attack={1} slash={2} frames.",
                OrderedSprites(SamuraiIdle).Count,
                OrderedSprites(SamuraiAttack).Count,
                OrderedSprites(SlashSheet).Count));
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
            // Empty grid cells are dropped at slice time, so whatever is here is drawn art.
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
