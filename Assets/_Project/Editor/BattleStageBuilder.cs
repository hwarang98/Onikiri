using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

using Onikiri.Battle;
using Onikiri.Core;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Builds the step-3 battle stage into Main.unity: parallax background, ground anchor
    /// and the samurai with his idle loop.
    ///
    /// Re-runnable, so it also documents exactly how the stage is assembled.
    /// </summary>
    public static class BattleStageBuilder
    {
        // Tiny Pixel Japan is the background the handoff spec names, and unlike the forest
        // packs it has an actual flat ground surface plus the sakura palette the brief asks
        // for. Swap this block to change stages.
        private const string BackgroundFolder = "Assets/ThirdParty/Backgrounds/TinyPixelJapan";

        /// <summary>Back to front. Anything not found is skipped rather than failing the build.</summary>
        private static readonly string[] BackgroundLayers =
        {
            "Sky",
            "Clouds",
            "Fuji",
            "Mountain_Back",
            "Mountain_Middle",
            "Mountain_Front",
            "BackgroundTrees",
            "Trees",
            "Shrine_Single",
            "Ground",
            "Gras"
        };

        /// <summary>Ground surface height above the background's bottom edge, in source pixels.</summary>
        private const float GroundSurfacePixels = 30f;
        private const float BackgroundPixelHeight = 180f;

        /// <summary>Uniform sky tone of Sky.png; the camera clears to this so tall phones blend.</summary>
        private static readonly Color SkyColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);

        private const string IdleSheet = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/IDLE.png";
        private const string AnimationFolder = "Assets/_Project/Animation";
        private const string IdleClipPath = AnimationFolder + "/Samurai_Idle.anim";
        private const string ControllerPath = AnimationFolder + "/Samurai.controller";

        private const float IdleFrameRate = 10f;

        /// <summary>Player sits left of centre; enemies will walk in from the right.</summary>
        private const float PlayerX = -2.0f;

        private const int BackgroundSortingBase = -110;
        private const int PlayerSortingOrder = 0;

        [MenuItem("Onikiri/Scene/Build Battle Stage")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath, OpenSceneMode.Single);

            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogError("[Onikiri] 'Battle' root missing - run Onikiri/Scene/Rebuild Main Scene first.");
                return;
            }

            var camera = Camera.main;
            camera.backgroundColor = SkyColor;

            var backgroundRoot = EnsureChild(battle.transform, "Background");
            var groundAnchor = EnsureChild(battle.transform, "GroundAnchor");

            // Parent the fighters to the ground anchor so they inherit the ground line for
            // free - no per-character layout code, and enemies added later come along too.
            // Move existing roots across BEFORE creating any, or a rebuild ends up with two
            // of each.
            Reparent(battle.transform, groundAnchor, "Player");
            Reparent(battle.transform, groundAnchor, "Enemies");
            var player = EnsureChild(groundAnchor, "Player");
            EnsureChild(groundAnchor, "Enemies");

            BuildBackground(backgroundRoot);

            var clip = BuildIdleClip();
            var controller = BuildController(clip);
            BuildSamurai(player, controller);

            WireLayout(battle, camera, backgroundRoot, groundAnchor);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Onikiri] Battle stage built: background + ground anchor + samurai idle.");
        }

        // ---------------------------------------------------------------- background

        private static void BuildBackground(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);

            int order = BackgroundSortingBase;
            int built = 0;

            foreach (var layerName in BackgroundLayers)
            {
                string path = BackgroundFolder + "/" + layerName + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning("[Onikiri] Background layer not found, skipping: " + path);
                    continue;
                }

                var go = new GameObject(layerName);
                go.transform.SetParent(root, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = order++;

                // Sprites import with a centre pivot, so lift each layer by half its height
                // to put its bottom edge on the root's origin (which sits on the band floor).
                go.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y, 0f);
                built++;
            }

            Debug.Log("[Onikiri] Background layers built: " + built);
        }

        // ---------------------------------------------------------------- animation

        private static AnimationClip BuildIdleClip()
        {
            EnsureFolder(AnimationFolder);

            var sprites = LoadOrderedSprites(IdleSheet);
            if (sprites.Count == 0)
            {
                Debug.LogError("[Onikiri] No sprites in " + IdleSheet + " - run Onikiri/Art/Slice Samurai Sheets first.");
                return null;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, IdleClipPath);
            }

            clip.frameRate = IdleFrameRate;

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / IdleFrameRate,
                    value = sprites[i]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            Debug.Log("[Onikiri] Idle clip: " + sprites.Count + " frames @ " + IdleFrameRate + "fps");
            return clip;
        }

        private static AnimatorController BuildController(AnimationClip clip)
        {
            if (clip == null) return null;

            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) AssetDatabase.DeleteAsset(ControllerPath);

            return AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
        }

        /// <summary>Sprites named "NAME_0", "NAME_1"... sorted by their numeric suffix.</summary>
        private static List<Sprite> LoadOrderedSprites(string sheetPath)
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

        // ---------------------------------------------------------------- samurai

        private static void BuildSamurai(Transform player, AnimatorController controller)
        {
            for (int i = player.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(player.GetChild(i).gameObject);

            var go = new GameObject("Samurai");
            go.transform.SetParent(player, false);
            // Local Y of zero: the ground anchor supplies the world height, and the sprite
            // pivot is already on the character's feet.
            go.transform.localPosition = new Vector3(PlayerX, 0f, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            var sprites = LoadOrderedSprites(IdleSheet);
            if (sprites.Count > 0) renderer.sprite = sprites[0];
            renderer.sortingOrder = PlayerSortingOrder;

            if (controller != null)
            {
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        // ---------------------------------------------------------------- wiring

        private static void WireLayout(GameObject battle, Camera camera, Transform backgroundRoot, Transform groundAnchor)
        {
            var layout = battle.GetComponent<BattleStageLayout>();
            if (layout == null) layout = battle.AddComponent<BattleStageLayout>();

            var so = new SerializedObject(layout);
            so.FindProperty("targetCamera").objectReferenceValue = camera;
            so.FindProperty("battleArea").objectReferenceValue = FindBattleArea();
            so.FindProperty("backgroundRoot").objectReferenceValue = backgroundRoot;
            so.FindProperty("groundAnchor").objectReferenceValue = groundAnchor;
            so.FindProperty("groundSurfacePixels").floatValue = GroundSurfacePixels;
            so.FindProperty("backgroundPixelHeight").floatValue = BackgroundPixelHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform FindBattleArea()
        {
            var canvas = GameObject.Find("UI Canvas");
            if (canvas == null) return null;
            var band = canvas.transform.Find("BattleArea");
            return band as RectTransform;
        }

        // ---------------------------------------------------------------- helpers

        private static Transform EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Reparent(Transform from, Transform to, string childName)
        {
            var child = from.Find(childName);
            if (child == null || child.parent == to) return;
            child.SetParent(to, false);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
