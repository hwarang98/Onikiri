using Onikiri.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Builds Assets/_Project/Scenes/Main.unity from scratch: portrait Pixel Perfect
    /// camera, a 1080x1920 overlay canvas, and empty containers for the four screen
    /// bands described in the handoff spec.
    ///
    /// Re-runnable — it overwrites the scene, so it doubles as documentation of exactly
    /// how the scene is configured.
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        [MenuItem("Onikiri/Scene/Rebuild Main Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateCanvas();
            CreateEventSystem();
            CreateWorldRoots();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Debug.Log("[Onikiri] Built " + ScenePath +
                      " (Portrait, PPU " + DisplayConfig.PixelsPerUnit +
                      ", ref " + DisplayConfig.ReferenceWidth + "x" + DisplayConfig.ReferenceHeight + ").");
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            // Pixel Perfect Camera drives this at runtime; set it so the editor view matches.
            cam.orthographicSize = DisplayConfig.CameraWorldHeight * 0.5f;
            cam.nearClipPlane = -100f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.047f, 0.067f, 1f); // sumi ink
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            urp.renderShadows = false;

            var ppc = go.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = DisplayConfig.PixelsPerUnit;
            ppc.refResolutionX = DisplayConfig.ReferenceWidth;
            ppc.refResolutionY = DisplayConfig.ReferenceHeight;
            // No letter/pillarboxing: taller phones (9:21) simply reveal more world
            // instead of getting black bars.
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            // Snap rendering to the pixel grid without forcing an upscale render texture,
            // so VFX and damage numbers can still move smoothly.
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
        }

        static void CreateCanvas()
        {
            var go = new GameObject("UI Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DisplayConfig.DesignWidth, DisplayConfig.DesignHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Width priority (0). In portrait the width is the constrained axis: matching
            // width keeps UI at 1:1 on any 9:16-9:21 phone and lets the extra height become
            // free space. Matching height instead would overflow horizontally on tall phones.
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();

            // Four screen bands from the spec, bottom-up: tab bar 10%, growth 35%,
            // battle 45%, top bar 10%. Left empty on purpose - contents come later.
            CreateBand(go.transform, "BottomTabBar", 0f, DisplayConfig.BottomTabBarTop);
            CreateBand(go.transform, "GrowthPanel", DisplayConfig.BottomTabBarTop, DisplayConfig.GrowthPanelTop);
            CreateBand(go.transform, "BattleArea", DisplayConfig.GrowthPanelTop, DisplayConfig.BattleAreaTop);
            CreateBand(go.transform, "TopBar", DisplayConfig.BattleAreaTop, 1f);
        }

        static void CreateBand(Transform parent, string name, float anchorMinY, float anchorMaxY)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, anchorMinY);
            rt.anchorMax = new Vector2(1f, anchorMaxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static void CreateWorldRoots()
        {
            // Empty parents so later steps have obvious places to put things.
            new GameObject("--- WORLD ---").transform.position = Vector3.zero;
            var battle = new GameObject("Battle");
            new GameObject("Background").transform.SetParent(battle.transform, false);
            new GameObject("Player").transform.SetParent(battle.transform, false);
            new GameObject("Enemies").transform.SetParent(battle.transform, false);
            new GameObject("VFX").transform.SetParent(battle.transform, false);
        }

        static void RegisterInBuildSettings()
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
            {
                if (s.path == ScenePath) return;
            }

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            foreach (var s in existing)
            {
                if (s.path != ScenePath) list.Add(s);
            }
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
