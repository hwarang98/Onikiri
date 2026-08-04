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
    /**
     * @brief Assets/_Project/Scenes/Main.unity 를 처음부터 만든다.
     *
     * 세로 Pixel Perfect 카메라, 1080x1920 오버레이 캔버스, 그리고 사양서가 정의한
     * 화면 4분할 컨테이너를 빈 채로 배치한다.
     *
     * 재실행 가능하며 씬을 덮어쓴다. 따라서 씬이 어떻게 구성돼 있는지에 대한 문서
     * 역할도 겸한다.
     */
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
            // 런타임에는 Pixel Perfect Camera가 이 값을 덮어쓴다. 에디터 뷰를 맞추기 위해 설정
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

            // Unity가 기본 생성하는 Main Camera에는 딸려 오지만, 손으로 만든 카메라에는
            // 없다. 씬에 리스너가 없으면 AudioSource.Play()는 성공하고 isPlaying도 true를
            // 반환한다. 소리만 출력에 도달하지 않으므로, 무음을 알아채기 전까지는
            // 모든 것이 정상으로 보인다
            go.AddComponent<AudioListener>();

            var ppc = go.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = DisplayConfig.PixelsPerUnit;
            ppc.refResolutionX = DisplayConfig.ReferenceWidth;
            ppc.refResolutionY = DisplayConfig.ReferenceHeight;
            // 레터/필러박스를 쓰지 않는다. 세로가 긴 기기(9:21)는 검은 띠 대신
            // 월드를 더 보여준다
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            // 업스케일 렌더 텍스처를 강제하지 않고 픽셀 격자에만 스냅한다.
            // 그래야 VFX와 데미지 숫자가 부드럽게 움직일 수 있다
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
            // 폭 우선(0). 세로 화면에서는 폭이 고정축이다. 폭에 맞추면 9:16~9:21 어느
            // 기기에서도 UI가 1:1로 유지되고 남는 높이는 여백이 된다. 높이에 맞추면
            // 세로가 긴 기기에서 가로로 넘친다
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();

            // 사양서의 화면 4분할. 아래에서 위로 탭바 10%, 성장 35%, 전투 45%, 상단 10%.
            // 내용물은 나중에 채우므로 의도적으로 비워 둔다
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
            // 이후 단계에서 무엇을 어디에 둘지 분명해지도록 빈 부모만 만들어 둔다
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
