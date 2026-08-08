using System.Collections.Generic;
using UnityEditor;
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

        // 레이어 목록·속도·틴트는 21단계에 데이터로 이사했다.
        // RegionBackgroundBuilder(씨앗)와 RegionBackgroundSet(애셋)을 볼 것.
        //
        // 여기 남은 상수들은 **지역 1 기준의 레이아웃 값**이다. 밴드에 맞추는
        // 배율과 지면선은 아직 이 빌더가 계산하고, 그 계산은 배경 세트가
        // 바뀌어도 같은 식을 쓴다.

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

        /**
         * @brief 레이어별 스크롤 상대 속도. 이 값의 차이가 깊이를 만든다.
         *
         * 하늘은 아예 안 움직인다(0). 원경이 조금이라도 흐르면 그 속도가 곧
         * "저것이 얼마나 먼가"를 말하는데, 하늘은 무한히 멀어야 한다.
         *
         * 지면(Ground/Gras)은 1.0이다 - 사무라이의 발이 닿는 면이라 여기가
         * 기준이고, 이 속도와 달리기 클립이 맞아야 발이 안 미끄러진다.
         *
         * 목록에 없으면 중경 기본값을 쓴다. 팩을 바꿔 새 레이어가 들어와도
         * 스크롤은 일단 돌고, 어색하면 그때 값을 적어 넣으면 된다.
         */
        /**
         * @brief 패럴랙스 폭을 25:1에서 6.7:1로 **압축했다**.
         *
         * 처음에는 구름 0.04 ~ 지면 1.00으로 잡았다. 깊이는 잘 나왔는데 화면이
         * 느려 보였고, 재보니 이유가 분명했다.
         *
         *   레이어가 화면에 칠하는 면적 vs 그 속도
         *     Clouds   49.6%  ->  0.13 u/s
         *     Trees    45.0%  ->  1.98 u/s
         *     Ground   13.9%  ->  3.20 u/s
         *
         * **빠른 것은 화면의 14%뿐**이고 눈을 채우는 큰 형체는 거의 안 움직였다.
         * 면적으로 가중한 평균 속도가 0.94 u/s라, 지면만 보면 화면 한 폭에
         * 2.1초인데 체감은 7.2초였다.
         *
         * 압축 후 가중 평균 1.60 u/s = 4.2초. 깊이 순서는 그대로 남으므로
         * 원경/근경 구분은 유지된다 - 깊이를 만드는 것은 비율의 크기가 아니라
         * 순서다.
         *
         * 기준 속도(3.2)를 올리지 않은 이유는 지면 속도가 달리기 클립과 맞물려
         * 있기 때문이다. 더 올리면 발이 미끄러진다.
         */
        private const float DefaultScrollSpeed = 0.45f;

        private static float ScrollSpeedFor(string layerName)
        {
            switch (layerName)
            {
                case "Sky": return 0f;
                case "Clouds": return 0.15f;
                case "Fuji": return 0.20f;

                // 탑은 **랜드마크**다. 근경 속도(0.80)로 두면 11 units마다 같은
                // 탑이 지나가고, 달리는 내내 "왜 같은 탑이 계속 나오지?"가 된다.
                // 먼 축에 얹고 사본 간격을 넓혀 반복 주기를 늘린다
                case "Shrine_Single": return 0.22f;

                case "Mountain_Back": return 0.34f;
                case "Mountain_Middle": return 0.48f;
                case "Mountain_Front": return 0.62f;
                case "BackgroundTrees": return 0.76f;
                case "Trees": return 0.88f;
                case "Ground": return 1f;
                case "Gras": return 1f;
                default: return DefaultScrollSpeed;
            }
        }

        /**
         * @brief 랜드마크가 다시 나오기까지의 목표 시간.
         *
         * 반복 주기는 거리가 아니라 **시간**으로 잡아야 한다. 랜드마크가 "가끔
         * 지나가는 것"으로 읽히는 조건은 몇 미터마다인지가 아니라 몇 분마다인지다.
         */
        private const float LandmarkRepeatSeconds = 125f;

        /** StageAdvance를 못 찾았을 때 쓸 전진 속도. 그 필드의 기본값과 같아야 한다 */
        private const float FallbackAdvanceSpeed = 4.5f;

        /**
         * @brief 레이어 사본 사이의 간격 배수.
         *
         * 기본은 1 - 스프라이트 폭 그대로 이어 붙여 빈틈없는 배경을 만든다.
         *
         * 랜드마크는 다르다. 탑은 그림의 왼쪽 일부만 차지하고 나머지가 투명이라,
         * 사본을 넓게 벌려도 배경에 구멍이 나지 않는다. 벌린 만큼 같은 탑이
         * 다시 나오기까지의 거리가 길어진다.
         *
         * **이 배수를 손으로 적지 않는다.** 전진 속도가 3.2 -> 8.0 -> 16.0으로
         * 바뀔 때마다 주기가 그만큼 짧아져 8 -> 20 -> 40으로 따라 고쳤는데,
         * 세 번 다 같은 산수였고 세 번째에야 그것을 알아차렸다. 고쳐야 할 것이
         * 규칙적으로 반복되면 그것은 상수가 아니라 계산이다.
         *
         *   간격 = 목표 주기 x 이 레이어의 실제 속도 / 조각 폭
         *
         * 전진 속도는 씬의 StageAdvance에서 읽는다 - 그것이 유일한 출처이고,
         * 여기에 사본을 두면 다음에 또 어긋난다.
         */
        private static float ScrollSpacingFor(string layerName, float pieceWidth)
        {
            if (layerName != "Shrine_Single") return 1f;
            if (pieceWidth <= 0.001f) return 1f;

            float layerSpeed = ScrollSpeedFor(layerName) * SceneAdvanceSpeed();
            if (layerSpeed <= 0.001f) return 1f;

            // 최소 1배. 계산이 1 밑으로 내려가면 사본이 겹쳐 배경에 구멍이 난다
            return Mathf.Max(1f, LandmarkRepeatSeconds * layerSpeed / pieceWidth);
        }

        /** 씬에 설정된 전진 속도. 빌드 순서상 아직 없을 수 있으므로 폴백을 둔다 */
        private static float SceneAdvanceSpeed()
        {
            var advance = Object.FindFirstObjectByType<Onikiri.Battle.StageAdvance>();
            if (advance == null) return FallbackAdvanceSpeed;
            return advance.ScrollSpeed > 0.001f ? advance.ScrollSpeed : FallbackAdvanceSpeed;
        }

        /**
         * @brief 무한 스크롤에 쓰는 사본 수. **폭에서 계산한다.**
         *
         * 지역 1은 전부 353px = 11.03 units 이라 두 장이면 충분했고, 그래서
         * 21단계까지 이 값이 상수 2였다. 예전 주석도 "좁은 레이어가 들어오면
         * 세 장이 필요하다"고 적어두기만 했다.
         *
         * 가을숲 지면이 정확히 그 경우다 - 타일 순환에서 구운 스트립이라 한 장이
         * 화면(6.75 units)보다 좁을 수 있고, 그러면 한 장이 왼쪽으로 빠지는
         * 동안 오른쪽 끝에 **빈 구멍**이 생긴다. 배경에 구멍이 나면 카메라
         * 클리어 색이 그대로 비쳐서 "배경이 깜빡인다"로 보인다.
         *
         * 적어둔 조건을 상수로 두면 다음 팩에서 또 걸린다. 계산으로 옮긴다.
         */
        private static int ScrollPiecesFor(float spanWorld)
        {
            if (spanWorld <= 0.001f) return 2;

            // 화면을 덮는 데 필요한 장수 + 1. 여분 한 장이 스크롤 중에 오른쪽을
            // 미리 채운다
            float visible = Onikiri.Core.DisplayConfig.CameraWorldHeight
                            * Onikiri.Core.DisplayConfig.ReferenceWidth
                            / Onikiri.Core.DisplayConfig.ReferenceHeight;

            return Mathf.Max(2, Mathf.CeilToInt(visible / spanWorld) + 1);
        }

        // 첫 프레임을 렌더러의 기본 스프라이트로 쓰기 위해서만 읽는다. 실제 동작은
        // SpriteAnimator가 돌리므로 여기서 클립이나 Animator 컨트롤러는 만들지 않는다
        private const string IdleSheet = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/IDLE.png";

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

            var skyFill = BuildBackground(backgroundRoot, battle.transform, camera);
            BuildProps(groundAnchor);

            // idle 클립과 Animator 컨트롤러는 더 만들지 않는다. 스프라이트 동작은
            // 전부 SpriteAnimator가 돌리고(BattleContentBuilder), Mecanim은 오히려
            // 그것을 덮어써서 "다리가 안 움직인다"를 만들었다. BuildSamurai 참고
            BuildSamurai(player);

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
        /**
         * @brief 배경을 세운다. **생성은 런타임 컴포넌트가 한다.**
         *
         * 21단계 이전에는 이 함수가 레이어 배열과 switch로 직접 만들었다. 지역이
         * 둘이 되면서 생성이 런타임에도 필요해졌고(지역 전환), 그때 코드가 두 벌로
         * 갈리면 조용히 어긋난다 - 보스 체력이 두 곳에 있던 것과 같은 사고다.
         *
         * 그래서 이 함수는 배선만 한다. 무엇을 만들지는 데이터(RegionBackgroundSet),
         * 어떻게 만들지는 런타임(BackgroundStage)이 안다.
         */
        private static SpriteRenderer BuildBackground(Transform root, Transform battle, Camera camera)
        {
            // 예전 빌더가 Battle 아래에 남긴 하늘 사본을 쓸어낸다. 리빌드를 반복할수록
            // 조용히 쌓여 서로 겹쳐 그려졌다
            for (int i = battle.childCount - 1; i >= 0; i--)
            {
                var child = battle.GetChild(i);
                if (child.name == SkyFillName || child.name == SkyLayerName)
                    Object.DestroyImmediate(child.gameObject);
            }

            RegionBackgroundBuilder.EnsureDefaultAssets();

            var stage = battle.GetComponent<Onikiri.Battle.BackgroundStage>();
            if (stage == null) stage = battle.gameObject.AddComponent<Onikiri.Battle.BackgroundStage>();

            var so = new SerializedObject(stage);
            so.FindProperty("layerRoot").objectReferenceValue = root;
            so.FindProperty("skyParent").objectReferenceValue = battle;
            so.FindProperty("advance").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.Battle.StageAdvance>();
            so.FindProperty("targetCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();

            var set = AssetDatabase.LoadAssetAtPath<Onikiri.Battle.RegionBackgroundSet>(
                RegionBackgroundBuilder.Region1Path);
            if (set == null)
            {
                Debug.LogError("[Onikiri] Region 1 background set missing.");
                return null;
            }

            // force. 에디터 빌드는 씬을 통째로 다시 세우는 자리라, "같은 세트면
            // 건드리지 않는다"는 런타임 규칙이 여기서는 옛 레이어를 남긴다
            stage.Apply(set, true);

            WireRegionSwitcher(battle, stage);

            Debug.Log("[Onikiri] Background: " + set.displayName + ", "
                      + set.layers.Length + " layers (sky fill " + (stage.SkyFill != null) + ")");
            return stage.SkyFill;
        }

        /**
         * @brief 지역이 바뀔 때 배경을 갈아끼우는 컴포넌트를 배선한다.
         *
         * 배경 세트를 지역 config에 물려주는 것도 여기서 한다. 두 애셋이 서로를
         * 모르면 스위처가 무엇을 걸어야 할지 알 수 없고, 그 연결을 손으로 두면
         * 지역을 추가할 때마다 잊는다 - 13단계에 보스 등급 배수를 선언만 하고
         * 연결을 잊은 적이 있다.
         */
        private static void WireRegionSwitcher(Transform battle, Onikiri.Battle.BackgroundStage stage)
        {
            var roster = BossConfigBuilder.EnsureDefaultAssets();
            if (roster == null || roster.regions == null) return;

            // 순서가 곧 지역 번호다. 24단계 톤 아크: 봄 여명 -> 가을 -> 자줏빛 밤
            string[] setPaths =
            {
                RegionBackgroundBuilder.Region1Path,
                RegionBackgroundBuilder.Region2Path,
                RegionBackgroundBuilder.Region3Path
            };

            for (int i = 0; i < roster.regions.Length; i++)
            {
                var region = roster.regions[i];
                if (region == null || region.background != null) continue;

                // 배경 세트가 모자라면 마지막 것을 쓴다. 지역이 배경보다 먼저
                // 늘어나는 것이 정상이고, 그때 화면이 비어서는 안 된다
                string path = setPaths[Mathf.Min(i, setPaths.Length - 1)];
                region.background = AssetDatabase.LoadAssetAtPath<
                    Onikiri.Battle.RegionBackgroundSet>(path);

                if (region.background != null) EditorUtility.SetDirty(region);
            }

            var switcher = battle.GetComponent<Onikiri.Battle.RegionBackgroundSwitcher>();
            if (switcher == null)
                switcher = battle.gameObject.AddComponent<Onikiri.Battle.RegionBackgroundSwitcher>();

            var so = new SerializedObject(switcher);
            so.FindProperty("roster").objectReferenceValue = roster;
            so.FindProperty("backgroundStage").objectReferenceValue = stage;
            so.FindProperty("layout").objectReferenceValue =
                battle.GetComponent<Onikiri.Battle.BattleStageLayout>();
            so.FindProperty("progress").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.Progression.StageProgress>();
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
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

            // 피벗이 중앙이므로 절반 높이만큼 올려 기둥 밑동이 지면선에 닿게 한다.
            // x는 BossGate가 정한다 - 여기 값은 배율을 곱할 기준 높이만 남긴다
            go.transform.localPosition = new Vector3(ToriiX, sprite.bounds.extents.y, 0f);

            // **관문으로 만든다.** 17단계 보강에서 배경 소품에서 사건으로 바뀌었다.
            // 반복 스크롤에 섞이면 계속 다시 나오고, 스크롤에서 빼면 화면에
            // 붙박여 따라온다 - 둘 다 "왜 토리이가 계속 나오지?"가 된다.
            // 이제 보스에게 달려가는 동안 한 번만 지나간다
            var gate = go.AddComponent<Onikiri.Battle.BossGate>();
            renderer.enabled = false;

            // 값을 빌더가 적는다. 스크립트 기본값에 맡기면 컴포넌트가 이미 씬에
            // 있는 순간부터 코드를 고쳐도 반영되지 않는다 - 강화 곡선을 빌더가
            // 적는 것과 같은 이유이고, 실제로 배율을 2에서 1로 내렸을 때
            // 씬의 옛 값이 그대로 남아 한 번 겪었다
            var gateSo = new SerializedObject(gate);
            gateSo.FindProperty("sprite").objectReferenceValue = renderer;

            // 피날레 관문은 **크기가 아니라 색**으로 무게를 준다. 세로로 키우면
            // 전투 밴드(화면 45~90%)를 넘어 보스 체력 바와 겹친다
            gateSo.FindProperty("finaleScale").intValue = 1;
            gateSo.FindProperty("finaleTint").colorValue = new Color32(0xFF, 0x5A, 0x4A, 0xFF);
            gateSo.FindProperty("normalTint").colorValue = TintFor(ToriiName);
            gateSo.FindProperty("passFraction").floatValue = 0.45f;
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(string.Format("[Onikiri] Boss gate '{0}' built ({1:F2} x {2:F2} units, ppu {3}). " +
                "Hidden during farming.",
                ToriiName, sprite.bounds.size.x, sprite.bounds.size.y, sprite.pixelsPerUnit));
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
        private static void BuildSamurai(Transform player)
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

            // **Mecanim Animator를 붙이지 않는다. 오히려 지운다.**
            //
            // 예전에는 여기서 idle 컨트롤러를 달았고, BattleContentBuilder가
            // 그것을 지운 뒤 SpriteAnimator를 얹었다(전투는 임팩트 시점을 프레임
            // 단위로 제어해야 한다). 두 빌더가 같은 컴포넌트를 두고 반대로
            // 움직였고, 그래서 **실행 순서가 결과를 갈랐다** - Build Battle Stage를
            // 마지막에 돌리면 Animator가 되살아난다.
            //
            // 되살아나면 조용히 틀린다. Animator는 Update 다음의 애니메이션
            // 단계에서 sprite를 덮어쓰므로, SpriteAnimator는 정상 동작하는데
            // 화면만 idle 루프에 머문다. 17단계에서 실제로 걸렸고, 증상은
            // "달리는데 다리가 안 움직인다"였다 - 코드도 데이터도 멀쩡해 보여서
            // 원인이 애니메이션 쪽이라는 신호가 어디에도 없었다.
            //
            // 순서에 기대는 대신 방향을 하나로 맞춘다. 두 빌더 모두 지우는 쪽이면
            // 어느 것을 마지막에 돌리든 결과가 같다.
            var legacy = go.GetComponent<Animator>();
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy, true);
                Debug.Log("[Onikiri] Removed a leftover Mecanim Animator from Samurai - "
                          + "sprite animation is driven by SpriteAnimator.");
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

        /**
         * @brief 전투 밴드를 찾는다.
         *
         * 캔버스 직속으로만 찾으면 안 된다. 7단계에서 안전 영역 루트가 생기면서 밴드가
         * 그 아래로 내려갔고, 그때부터 이 빌더를 **두 번째로 실행할 때만** 참조가 null로
         * 기록됐다. 첫 실행에서는 안전 영역이 아직 없어 우연히 찾아지기 때문이다.
         *
         * 그리고 null이 기록돼도 아무 에러가 나지 않았다. BattleStageLayout이 조용히
         * 조기 반환하면서 배경과 지면 앵커가 마지막으로 성공했을 때의 좌표에 그대로
         * 머물렀고, 화면에서는 배경이 UI 밴드보다 122px 아래까지 그려지는 것으로만
         * 드러났다.
         */
        private static RectTransform FindBattleArea()
        {
            return MainSceneBuilder.FindBand("BattleArea") as RectTransform;
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
