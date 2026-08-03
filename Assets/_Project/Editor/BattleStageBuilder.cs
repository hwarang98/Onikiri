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

        /// <summary>
        /// Height of the walkable dirt surface above the background's bottom edge, in
        /// source pixels.
        ///
        /// Measured as the most common per-column surface height in Ground.png (209 of 353
        /// columns), NOT the topmost opaque pixel. The highest dirt mound reaches 30px but
        /// only 8 columns are that tall, so calibrating to it left the character floating
        /// 6px above the ground everyone else walks on.
        /// </summary>
        private const float GroundSurfacePixels = 24f;
        private const float BackgroundPixelHeight = 180f;

        // ------------------------------------------------------------------ colour grade
        //
        // The background packs ship bright and warm, which flattens the scene and lets the
        // sakura compete with the fighters. Tinting the sprite renderers by depth pushes the
        // distance darker and cooler and leaves the near ground lightest, so the untinted
        // characters read as the closest, brightest thing on screen.
        //
        // Characters and enemies are deliberately NOT tinted - that separation is the whole
        // point of the grade.

        /// <summary>Far layers: sky, clouds, Fuji.</summary>
        private static readonly Color FarTint = new Color32(0x6E, 0x68, 0xA0, 0xFF);

        /// <summary>Mid layers: mountains and tree bands.</summary>
        private static readonly Color MidTint = new Color32(0x8B, 0x82, 0xB5, 0xFF);

        /// <summary>Near layers: shrine, ground, grass.</summary>
        private static readonly Color NearTint = new Color32(0xA8, 0x9E, 0xCB, 0xFF);

        /// <summary>Camera clear colour, behind the tinted sky fill.</summary>
        private static readonly Color ClearColor = new Color32(0x2A, 0x27, 0x40, 0xFF);

        private static readonly string[] FarLayers = { "Sky", "Clouds", "Fuji" };
        private static readonly string[] NearLayers = { "Shrine_Single", "Shrine_Multiple", "House", "Ground", "Gras" };

        /// <summary>Depth tint for a background layer. Anything unlisted is treated as mid.</summary>
        public static Color TintFor(string layerName)
        {
            foreach (var name in FarLayers) if (name == layerName) return FarTint;
            foreach (var name in NearLayers) if (name == layerName) return NearTint;
            return MidTint;
        }

        private const string IdleSheet = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/IDLE.png";
        private const string AnimationFolder = "Assets/_Project/Animation";
        private const string IdleClipPath = AnimationFolder + "/Samurai_Idle.anim";
        private const string ControllerPath = AnimationFolder + "/Samurai.controller";

        private const float IdleFrameRate = 10f;

        /// <summary>
        /// Where the samurai is dropped on the FIRST build only. After that his transform
        /// is authored in the Scene view and rebuilds preserve it, so this is a seed value
        /// rather than the live setting. Visible world width is 6.75 units on every target
        /// phone, so the usable range is roughly -3.375 .. 3.375; he sits left of centre
        /// because enemies walk in from the right, but far enough right to clear the pagoda
        /// that sits on the left edge of the background.
        /// </summary>
        public const float PlayerX = -1.2f;

        private const string SkyLayerName = "Sky";
        private const string SkyFillName = "SkyFill";
        private const string GroundCoverLayerName = "Gras";

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
            camera.backgroundColor = ClearColor;

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

            var skyFill = BuildBackground(backgroundRoot, battle.transform);

            var clip = BuildIdleClip();
            var controller = BuildController(clip);
            BuildSamurai(player, controller);

            WireLayout(battle, camera, backgroundRoot, groundAnchor, skyFill);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Onikiri] Battle stage built: background + ground anchor + samurai idle.");
        }

        // ---------------------------------------------------------------- background

        /// <summary>
        /// Builds every background layer and returns the sky fill renderer.
        ///
        /// The sky is deliberately NOT a child of the background root. The other layers are
        /// bottom-anchored to the battle band, whereas the sky is stretched over the whole
        /// camera by <see cref="BattleStageLayout"/>, so it lives beside them under Battle.
        /// Both jobs are done here rather than split across two builders - when the combat
        /// builder also reparented the sky, rebuilds left a second stale copy behind and
        /// mutating the hierarchy mid-iteration silently skipped a layer's sorting order.
        /// </summary>
        private static SpriteRenderer BuildBackground(Transform root, Transform battle)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);

            // Sweep every prior sky, not just the first match. An earlier version of this
            // builder left the sky parented under Battle, so repeated rebuilds silently
            // stacked up copies that all rendered on top of each other.
            for (int i = battle.childCount - 1; i >= 0; i--)
            {
                var child = battle.GetChild(i);
                if (child.name == SkyFillName || child.name == SkyLayerName)
                    Object.DestroyImmediate(child.gameObject);
            }

            int order = SortingOrders.BackgroundBase;
            int built = 0;
            SpriteRenderer skyFill = null;

            foreach (var layerName in BackgroundLayers)
            {
                string path = BackgroundFolder + "/" + layerName + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning("[Onikiri] Background layer not found, skipping: " + path);
                    continue;
                }

                if (layerName == SkyLayerName)
                {
                    var skyObject = new GameObject(SkyFillName);
                    skyObject.transform.SetParent(battle, false);

                    skyFill = skyObject.AddComponent<SpriteRenderer>();
                    skyFill.sprite = sprite;
                    skyFill.color = TintFor(layerName);
                    skyFill.sortingOrder = SortingOrders.SkyFill;
                    built++;
                    continue;
                }

                var go = new GameObject(layerName);
                go.transform.SetParent(root, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = TintFor(layerName);
                // Grass jumps in front of the fighters so it crosses their feet; everything
                // else stacks back to front in list order.
                renderer.sortingOrder = layerName == GroundCoverLayerName
                    ? SortingOrders.GroundCover
                    : order++;

                // Sprites import with a centre pivot, so lift each layer by half its height
                // to put its bottom edge on the root's origin (which sits on the band floor).
                go.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y, 0f);
                built++;
            }

            Debug.Log("[Onikiri] Background layers built: " + built + " (sky fill " + (skyFill != null) + ")");
            return skyFill;
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

        /// <summary>
        /// Creates the samurai, or refreshes the existing one in place.
        ///
        /// The transform is deliberately left alone when the object already exists: where
        /// the character stands is an art decision made by dragging him in the Scene view,
        /// and a rebuild must not throw that away. <see cref="PlayerX"/> is only a starting
        /// position for the very first build.
        /// </summary>
        private static void BuildSamurai(Transform player, AnimatorController controller)
        {
            var existing = player.Find("Samurai");
            bool isNew = existing == null;

            GameObject go;
            if (isNew)
            {
                go = new GameObject("Samurai");
                go.transform.SetParent(player, false);
                // Local Y of zero: the ground anchor supplies the world height, and the
                // sprite pivot is already on the character's feet.
                go.transform.localPosition = new Vector3(PlayerX, 0f, 0f);
            }
            else
            {
                go = existing.gameObject;
            }

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();

            var sprites = LoadOrderedSprites(IdleSheet);
            if (sprites.Count > 0) renderer.sprite = sprites[0];
            renderer.sortingOrder = SortingOrders.Player;

            if (controller != null)
            {
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            Debug.Log(isNew
                ? "[Onikiri] Samurai created at x=" + PlayerX + "."
                : "[Onikiri] Samurai refreshed, keeping position " + go.transform.localPosition + ".");
        }

        // ---------------------------------------------------------------- wiring

        private static void WireLayout(GameObject battle, Camera camera, Transform backgroundRoot,
                                       Transform groundAnchor, SpriteRenderer skyFill)
        {
            var layout = battle.GetComponent<BattleStageLayout>();
            if (layout == null) layout = battle.AddComponent<BattleStageLayout>();

            var so = new SerializedObject(layout);
            so.FindProperty("targetCamera").objectReferenceValue = camera;
            so.FindProperty("battleArea").objectReferenceValue = FindBattleArea();
            so.FindProperty("backgroundRoot").objectReferenceValue = backgroundRoot;
            so.FindProperty("groundAnchor").objectReferenceValue = groundAnchor;
            so.FindProperty("skyFill").objectReferenceValue = skyFill;
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
