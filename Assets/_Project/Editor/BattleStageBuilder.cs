using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

using Onikiri.Battle;
using Onikiri.Core;

namespace Onikiri.EditorTools
{
    /**
     * @brief Main.unity에 전투 스테이지를 구성한다.
     *
     * 패럴랙스 배경, 지면 앵커, idle 루프를 도는 사무라이까지.
     *
     * 재실행 가능하므로 스테이지가 어떻게 조립되는지에 대한 문서 역할도 한다.
     */
    public static class BattleStageBuilder
    {
        // Tiny Pixel Japan은 사양서가 지정한 배경 팩이다. 숲 팩들과 달리 실제로 평평한
        // 지면이 있고, 요구사항에 있는 벚꽃 팔레트도 갖췄다. 스테이지를 바꾸려면 이
        // 블록을 교체하면 된다.
        private const string BackgroundFolder = "Assets/ThirdParty/Backgrounds/TinyPixelJapan";

        /** 뒤에서 앞 순서. 없는 레이어는 빌드를 실패시키지 않고 건너뛴다 */
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

        /**
         * @brief 배경 밑단에서 걸을 수 있는 흙 표면까지의 높이 (원본 픽셀).
         *
         * Ground.png의 열별 표면 높이 중 최빈값으로 측정했다(353개 열 중 209개).
         * 가장 높은 불투명 픽셀이 아니다. 가장 높은 흙더미는 30px까지 올라가지만 그
         * 높이인 열은 8개뿐이라, 거기에 맞추면 캐릭터가 나머지 지면 위로 6px 떠버린다.
         */
        private const float GroundSurfacePixels = 24f;
        private const float BackgroundPixelHeight = 180f;

        // ------------------------------------------------------------------ 색 그레이드
        //
        // 배경 팩은 밝고 따뜻하게 나오는데, 그러면 씬이 평면적으로 보이고 벚꽃이
        // 파이터와 시선을 다툰다. 스프라이트 렌더러를 깊이별로 틴트하면 원경이 어둡고
        // 차가워지고 근경 지면이 가장 밝게 남아, 틴트하지 않은 캐릭터가 화면에서 가장
        // 가깝고 밝은 것으로 읽힌다.
        //
        // 캐릭터와 적은 의도적으로 틴트하지 않는다. 그 분리가 이 그레이드의 핵심이다.

        /** 원경 레이어: 하늘, 구름, 후지산 */
        private static readonly Color FarTint = new Color32(0x6E, 0x68, 0xA0, 0xFF);

        /** 중경 레이어: 산과 나무 띠 */
        private static readonly Color MidTint = new Color32(0x8B, 0x82, 0xB5, 0xFF);

        /**
         * @brief 근경 레이어: 신사, 지면, 풀, 지면 소품.
         *
         * 처음에는 #A89ECB 였는데 적색 채널을 파랑보다 35 낮게 눌러서, 화면 전체가
         * 보라 단색조가 되고 사양서의 먹빛-적-벚꽃 팔레트에서 적이 사라졌다.
         *
         * 지금은 적색을 파랑에 가깝게 되돌렸다. 대기 원근(멀수록 차갑고 푸르게)은
         * 원경/중경 틴트가 그대로 유지하고 있으므로, 근경만 따뜻해지면 깊이는 오히려
         * 더 분명해진다.
         */
        private static readonly Color NearTint = new Color32(0xC8, 0xA4, 0xB8, 0xFF);

        /** 카메라 클리어 색. 틴트된 하늘 뒤에 깔린다 */
        private static readonly Color ClearColor = new Color32(0x2A, 0x27, 0x40, 0xFF);

        private static readonly string[] FarLayers = { "Sky", "Clouds", "Fuji" };
        private static readonly string[] NearLayers = { "Shrine_Single", "Shrine_Multiple", "House", "Ground", "Gras", ToriiName };

        /** 배경 레이어의 깊이 틴트. 목록에 없으면 중경으로 취급한다 */
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

        /**
         * @brief 최초 빌드에서만 사무라이를 놓을 위치.
         *
         * 이후에는 Scene 뷰에서 트랜스폼을 직접 잡고 리빌드가 그것을 보존하므로, 이
         * 값은 실제 설정이 아니라 시드값이다. 가시 월드 폭은 대상 기기 전부에서
         * 6.75 units이라 쓸 수 있는 범위는 대략 -3.375 ~ 3.375 다. 적이 오른쪽에서
         * 오므로 중앙보다 왼쪽에 서되, 배경 왼쪽 끝의 오층탑과 겹치지 않을 만큼은
         * 오른쪽에 둔다.
         */
        public const float PlayerX = -1.2f;

        private const string SkyLayerName = "Sky";
        private const string SkyFillName = "SkyFill";
        private const string GroundCoverLayerName = "Gras";

        // ------------------------------------------------------------------ 지면 소품
        //
        // 깊이 틴트를 넣은 뒤 화면에 채도 높은 적이 하나도 남지 않았다. 틴트를 되돌리는
        // 것만으로는 부족하다. 배경 팩의 원본 색이 갈색과 분홍이라, 곱하기로는 없는 적을
        // 만들어낼 수 없기 때문이다. 주홍 도리이는 팔레트에 적을 되돌려 놓으면서 무대가
        // 어디인지도 한 번에 말해준다.
        //
        // SpringForest 팩에서 가져왔지만 같은 픽셀 스케일이고 색이 이 씬에 그대로 맞는다.

        private const string ToriiName = "Torii";
        private const string ToriiSheet = "Assets/ThirdParty/Backgrounds/SpringForest/Props/Torii gate.png";

        /**
         * @brief 도리이의 가로 위치 (world units).
         *
         * 요괴는 오른쪽에서 들어오므로 문을 그쪽에 세운다. 폭이 3 units이고 화면
         * 오른쪽 끝이 3.375이므로, 중심 1.8이면 0.3~3.3으로 화면 안에 정확히 들어온다.
         */
        private const float ToriiX = 1.8f;

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

            // 파이터를 지면 앵커의 자식으로 둬서 지면선을 그냥 상속받게 한다. 캐릭터마다
            // 레이아웃 코드를 짤 필요가 없고, 나중에 추가되는 적도 따라온다.
            // 기존 루트를 먼저 옮긴 다음에 생성해야 한다. 순서가 반대면 리빌드 때
            // 각각 두 개씩 생긴다
            Reparent(battle.transform, groundAnchor, "Player");
            Reparent(battle.transform, groundAnchor, "Enemies");
            var player = EnsureChild(groundAnchor, "Player");
            EnsureChild(groundAnchor, "Enemies");

            var skyFill = BuildBackground(backgroundRoot, battle.transform);
            BuildProps(groundAnchor);

            var clip = BuildIdleClip();
            var controller = BuildController(clip);
            BuildSamurai(player, controller);

            WireLayout(battle, camera, backgroundRoot, groundAnchor, skyFill);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Onikiri] Battle stage built: background + ground anchor + samurai idle.");
        }

        // ---------------------------------------------------------------- 배경

        /**
         * @brief 배경 레이어를 전부 만들고 하늘 렌더러를 반환한다.
         *
         * 하늘은 의도적으로 배경 루트의 자식이 아니다. 다른 레이어는 전투 밴드에 밑단이
         * 고정되지만 하늘은 BattleStageLayout이 카메라 전체 크기로 늘리므로, Battle 아래에
         * 나란히 둔다.
         *
         * 두 작업을 두 빌더로 나누지 않고 여기서 함께 처리한다. 결합 빌더도 하늘을
         * 재부모화하던 시절에는 리빌드마다 낡은 사본이 하나씩 남았고, 순회 도중 계층을
         * 바꾸는 바람에 레이어 하나가 정렬 순서 배정을 조용히 건너뛰었다.
         */
        private static SpriteRenderer BuildBackground(Transform root, Transform battle)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);

            // 첫 번째 일치만이 아니라 이전 하늘을 전부 쓸어낸다. 이 빌더의 예전 버전이
            // 하늘을 Battle 아래에 남겨두는 바람에, 리빌드를 반복할수록 사본이 조용히
            // 쌓여 서로 겹쳐 그려졌다
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
                // 풀은 파이터 앞으로 올려 발을 가로지르게 한다. 나머지는 목록 순서대로
                // 뒤에서 앞으로 쌓인다
                renderer.sortingOrder = layerName == GroundCoverLayerName
                    ? SortingOrders.GroundCover
                    : order++;

                // 스프라이트는 중앙 피벗으로 임포트되므로, 각 레이어를 자기 높이의 절반만큼
                // 올려 밑단이 루트 원점(밴드 바닥)에 오게 한다
                go.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y, 0f);
                built++;
            }

            Debug.Log("[Onikiri] Background layers built: " + built + " (sky fill " + (skyFill != null) + ")");
            return skyFill;
        }

        // ---------------------------------------------------------------- 지면 소품

        /**
         * @brief 지면선 위에 서는 소품을 배치한다.
         *
         * 배경 레이어가 아니라 지면 앵커의 자식이다. 배경은 밴드 바닥에 밑단이 붙고
         * 소품은 걸을 수 있는 흙 표면에 서야 하는데, 그 둘은 0.75 units 차이가 난다.
         * 앵커에 붙이면 그 차이를 여기서 다시 계산할 필요가 없다.
         */
        private static void BuildProps(Transform groundAnchor)
        {
            var props = EnsureChild(groundAnchor, "Props");

            for (int i = props.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(props.GetChild(i).gameObject);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ToriiSheet);
            if (sprite == null)
            {
                Debug.LogWarning("[Onikiri] Prop not found, skipping: " + ToriiSheet);
                return;
            }

            // 이 소품은 다른 배경 팩에서 왔다. 임포트 기준이 적용되기 전에 들어온
            // 파일이면 PPU가 100(유니티 기본값)일 수 있고, 그러면 도리이만 1/3 크기로
            // 나온다. 조용히 어긋나는 대신 여기서 잡는다
            if (!Mathf.Approximately(sprite.pixelsPerUnit, DisplayConfig.PixelsPerUnit))
            {
                var importer = AssetImporter.GetAtPath(ToriiSheet) as TextureImporter;
                if (importer != null)
                {
                    PixelArtImportSettings.Apply(importer);
                    importer.SaveAndReimport();
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ToriiSheet);
                }
            }

            var go = new GameObject(ToriiName);
            go.transform.SetParent(props, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = TintFor(ToriiName);
            renderer.sortingOrder = SortingOrders.BackgroundProp;

            // 피벗이 중앙이므로 절반 높이만큼 올려 기둥 밑동이 지면선에 닿게 한다
            go.transform.localPosition = new Vector3(ToriiX, sprite.bounds.extents.y, 0f);

            Debug.Log(string.Format("[Onikiri] Prop '{0}' placed at x={1} ({2:F2} x {3:F2} units, ppu {4}).",
                ToriiName, ToriiX, sprite.bounds.size.x, sprite.bounds.size.y, sprite.pixelsPerUnit));
        }

        // ---------------------------------------------------------------- 애니메이션

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

        /** "NAME_0", "NAME_1" 형태의 스프라이트를 숫자 접미사 순으로 정렬해 반환 */
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

        // ---------------------------------------------------------------- 사무라이

        /**
         * @brief 사무라이를 생성하거나, 이미 있으면 제자리에서 갱신한다.
         *
         * 오브젝트가 이미 있으면 트랜스폼은 의도적으로 건드리지 않는다. 캐릭터를 어디에
         * 세울지는 Scene 뷰에서 드래그해 정하는 아트 결정이고, 리빌드가 그것을 날려서는
         * 안 된다. PlayerX는 최초 빌드에서만 쓰는 시작 위치다.
         */
        private static void BuildSamurai(Transform player, AnimatorController controller)
        {
            var existing = player.Find("Samurai");
            bool isNew = existing == null;

            GameObject go;
            if (isNew)
            {
                go = new GameObject("Samurai");
                go.transform.SetParent(player, false);
                // 로컬 Y는 0. 월드 높이는 지면 앵커가 공급하고, 스프라이트 피벗은
                // 이미 캐릭터의 발에 있다
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

        // ---------------------------------------------------------------- 배선

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

        // ---------------------------------------------------------------- 헬퍼

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
