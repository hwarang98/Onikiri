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
        private const string EnemyAseprite = "Assets/ThirdParty/Enemies/FeudalJapan/Inimig (1).aseprite";
        // color2 is the red variant. The pack's default (color1) is bright green, which
        // fights the ink/red/sakura palette the brief calls for.
        private const string SlashSheet = "Assets/ThirdParty/VFX/Slashes/Slash_64x64_Slash1_color2.png";
        private const string SamuraiIdle = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/IDLE.png";
        private const string SamuraiAttack = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/ATTACK 1.png";

        private const string DataFolder = "Assets/_Project/Data";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string EnemyDefinitionPath = DataFolder + "/Enemy_Chochin.asset";
        private const string EnemyPrefabPath = PrefabFolder + "/Enemy.prefab";
        private const string SlashPrefabPath = PrefabFolder + "/SlashVfx.prefab";

        /// <summary>
        /// First frame of the slash sheet used for the impact effect (0-based). The sheet's
        /// nine frames are a wind-up (0-4) followed by the heavy arc (5-8).
        /// </summary>
        private const int SlashImpactFirstFrame = 5;

        // Aseprite tags in this pack are unnamed, so clips arrive as Tag/Tag_0/Tag_1...
        // Identified by inspecting the frames: see the step 4 report.
        private const string IdleClip = "Tag";
        private const string HurtClip = "Tag_1";
        private const string DeathClip = "Tag_2";

        [MenuItem("Onikiri/Scene/Build Combat Content")]
        public static void Build()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);

            BuildEnemyDefinition();
            BuildEnemyPrefab();
            BuildSlashPrefab();

            var scene = EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath, OpenSceneMode.Single);

            // Re-load the assets by path AFTER opening the scene. Object references created
            // before the scene load can be invalidated by the reimport it triggers, and
            // assigning a stale UnityEngine.Object to a SerializedProperty writes null
            // without any error - which is exactly how the spawner ended up prefab-less.
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath);
            var enemyPrefab = LoadPrefabComponent<Enemy>(EnemyPrefabPath);
            var slashPrefab = LoadPrefabComponent<SlashVfx>(SlashPrefabPath);

            if (definition == null || enemyPrefab == null || slashPrefab == null)
            {
                Debug.LogError("[Onikiri] Combat assets missing after build: definition=" + (definition != null)
                               + " enemy=" + (enemyPrefab != null) + " slash=" + (slashPrefab != null));
                return;
            }

            WireSortingAndSky();
            var spawner = WireSpawner(definition, enemyPrefab);
            WirePlayerCombat(spawner, slashPrefab);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (!VerifyWiring()) return;
            Debug.Log("[Onikiri] Combat content built.");
        }

        // ---------------------------------------------------------------- enemy data

        private static EnemyDefinition BuildEnemyDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, EnemyDefinitionPath);
            }

            definition.displayName = "Chochin-obake";
            definition.idleFrames = FramesFromClip(EnemyAseprite, IdleClip);
            definition.hurtFrames = FramesFromClip(EnemyAseprite, HurtClip);
            definition.deathFrames = FramesFromClip(EnemyAseprite, DeathClip);
            definition.frameRate = 12f;
            definition.maxHealth = 12f;
            definition.moveSpeed = 1.1f;
            // The lantern art is 36px wide (1.125 units), so anything under that overlaps
            // the neighbour and the queue reads as one smear instead of separate yokai.
            definition.queueSpacing = 1.35f;
            definition.hoverHeight = 0.1f;

            EditorUtility.SetDirty(definition);

            Debug.Log(string.Format("[Onikiri] Enemy definition: idle={0} hurt={1} death={2} frames.",
                definition.idleFrames.Length, definition.hurtFrames.Length, definition.deathFrames.Length));

            return definition;
        }

        /// <summary>
        /// Pulls the ordered sprite list out of an imported Aseprite animation clip.
        /// The clip is the only thing that knows which frames belong to which tag, so we
        /// read the keyframes rather than guessing frame ranges by index.
        /// </summary>
        private static Sprite[] FramesFromClip(string assetPath, string clipName)
        {
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
        private static void WireSortingAndSky()
        {
            var background = GameObject.Find("Background");
            if (background == null) return;

            int order = SortingOrders.BackgroundBase;
            SpriteRenderer skyRenderer = null;

            foreach (Transform layer in background.transform)
            {
                var renderer = layer.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;

                if (layer.name == "Gras")
                {
                    renderer.sortingOrder = SortingOrders.GroundCover;
                    continue;
                }

                if (layer.name == "Sky")
                {
                    skyRenderer = renderer;
                    renderer.sortingOrder = SortingOrders.SkyFill;
                    // Detach from the band-anchored root: the sky is sized to the camera,
                    // not to the background's bottom edge.
                    layer.SetParent(background.transform.parent, true);
                    continue;
                }

                renderer.sortingOrder = order++;
            }

            var layout = GameObject.Find("Battle").GetComponent<BattleStageLayout>();
            var so = new SerializedObject(layout);
            so.FindProperty("skyFill").objectReferenceValue = skyRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnemySpawner WireSpawner(EnemyDefinition definition, Enemy enemyPrefab)
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

            var definitions = so.FindProperty("definitions");
            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = definition;

            so.FindProperty("targetAlive").intValue = 4;
            so.FindProperty("spawnInterval").floatValue = 1.1f;
            so.FindProperty("frontLineX").floatValue = -0.75f;
            so.FindProperty("prewarm").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();

            // HitStop lives on the same object so it is guaranteed present in play mode.
            if (battle.GetComponent<HitStop>() == null) battle.AddComponent<HitStop>();

            return spawner;
        }

        private static void WirePlayerCombat(EnemySpawner spawner, SlashVfx slashPrefab)
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
            so.FindProperty("slashFrameRate").floatValue = 22f;
            // Lands on the yokai rather than midway, at roughly its centre of mass.
            so.FindProperty("slashOffset").vector2Value = new Vector2(-0.2f, 0.45f);

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
