using System.Collections.Generic;
using System.IO;
using Onikiri.Battle;
using Onikiri.Core;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 타격 지점에 터지는 작은 불꽃의 시트와 프리팹을 굽는다.
     *
     * ## 왜 팩 아트를 안 쓰고 굽는가
     *
     * `Assets/ThirdParty/VFX/Slashes`에 다섯 색 x 세 종류가 있고, 그중 Slash2에는 실제로
     * 쓸 만한 임팩트 별이 들어 있다. 그런데 그 별은 **긴 찌르기 줄기와 한 프레임에 붙어
     * 있다.** 열별 잉크 분포를 재보면 프레임 2에서 줄기(x 10~26)와 별(x 26~56)이 이어져
     * 있어서, 별만 떼어내려면 좌표를 손으로 박아야 하고 그것은 팩을 갱신하는 순간 깨진다.
     *
     * 필요한 것은 12px짜리 가시 몇 개다. 그 크기에서는 그림 파일을 관리하는 비용이 아트
     * 자체보다 크고, 어떤 모양인지도 파일을 열어야 알 수 있다. 벚꽃잎과 같은 판단이다 -
     * 코드가 곧 아트 명세가 된다(SakuraContentBuilder 참고).
     *
     * ## 흰색으로 굽는다
     *
     * 색은 `ImpactSpark.tint`가 런타임에 입힌다. 팩 원본 color1은 초록(실측 hue 74도)이고
     * 먹빛·적·벚꽃 팔레트와 충돌하는데, 시트에 색을 구워 넣으면 그 실수를 다시 할 여지가
     * 남는다. 흰 시트에는 초록을 넣을 방법이 없다.
     */
    public static class ImpactSparkBuilder
    {
        private const string ArtFolder = "Assets/_Project/Art/VFX";
        private const string SparkSheetPath = ArtFolder + "/ImpactSpark.png";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string SparkPrefabPath = PrefabFolder + "/ImpactSpark.prefab";

        /**
         * @brief 셀 한 변 (px).
         *
         * PPU 32에서 0.375 유닛이다. 잡몹의 그려진 몸이 0.5~1.3 유닛이므로 몸의 3분의 1쯤
         * 되는 크기다 - "맞은 자리"를 찍기에 충분하고 몸을 덮지 않는다. 걷어낸 참격 아크는
         * 64px(2.0 유닛)이었고, 그것이 사무라이와 요괴를 통째로 가렸다.
         */
        private const int Cell = 12;

        /** 중심. 12px 셀이라 픽셀 사이(5.5)에 온다 - 가시가 상하좌우 대칭으로 뻗는다 */
        private const float Center = (Cell - 1) * 0.5f;

        /**
         * @brief 가시가 뻗는 여덟 방향. 상하좌우 넷 + 대각 넷.
         *
         * 대각은 짧게 뻗는다(DiagonalScale). 여덟 개가 전부 같은 길이면 별이 아니라
         * 동그라미로 읽힌다.
         */
        private const float DiagonalScale = 0.66f;

        /**
         * @brief 칼이 들어온 쪽으로 가시가 더 길게 뻗는 배수.
         *
         * 불꽃이 좌우 대칭이면 뒤집어도 같은 그림이라 방향이 없다. 한쪽을 길게 만들어야
         * "이쪽에서 칼이 들어왔다"가 읽히고, 그때 비로소 좌우 반전이 의미를 갖는다
         * (PlayerCombat이 Enemy.FacingDirection으로 뒤집는다).
         */
        private const float TrailScale = 1.4f;

        /**
         * @brief 프레임마다의 안쪽·바깥 반지름과 심 크기.
         *
         * 가운데가 비면서 바깥으로 나간다. 터진 것은 퍼지지 되돌아오지 않으므로, 심은
         * 첫 두 프레임에만 있고 마지막 두 프레임은 가시 끝만 남는다.
         */
        private struct Burst
        {
            public float Inner;
            public float Outer;
            public float Core;
        }

        private static readonly Burst[] Frames =
        {
            // 닿은 순간. 심만 있고 가시는 아직 짧다
            new Burst { Inner = 0f,   Outer = 2.1f, Core = 1.9f },
            // 가장 크게 벌어지는 프레임. 히트스톱이 멈춰 세우는 것이 보통 여기다
            new Burst { Inner = 0f,   Outer = 4.5f, Core = 1.3f },
            // 가운데가 비기 시작한다
            new Burst { Inner = 1.9f, Outer = 5.5f, Core = 0f },
            // 끝만 남는다
            new Burst { Inner = 3.7f, Outer = 5.8f, Core = 0f }
        };

        [MenuItem("Onikiri/Art/Build Impact Spark")]
        public static void BuildArt()
        {
            BuildSheet();
            BuildPrefab();
            AssetDatabase.SaveAssets();
        }

        /**
         * @brief 네 프레임을 가로로 늘어놓은 48x12 시트를 굽는다.
         *
         * 이미 있으면 다시 굽지 않는다. 재임포트가 일어나면 프리팹이 들고 있는 스프라이트
         * 참조가 끊길 수 있다. 모양을 고쳤으면 파일을 지우고 다시 돌린다.
         */
        public static void BuildSheet()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(SparkSheetPath) != null) return;

            EnsureFolder(ArtFolder);

            int width = Cell * Frames.Length;
            var texture = new Texture2D(width, Cell, TextureFormat.RGBA32, false);

            var pixels = new Color[width * Cell];
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            for (int frame = 0; frame < Frames.Length; frame++)
                DrawBurst(pixels, width, frame, Frames[frame]);

            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(SparkSheetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(SparkSheetPath, ImportAssetOptions.ForceUpdate);

            // 피벗은 셀 한가운데다. 불꽃은 타격 지점 '위에' 서는 것이 아니라 그 지점을
            // 중심으로 터지므로, 피벗이 모서리면 스폰 좌표를 매번 보정해야 한다
            CharacterSpriteSlicer.SliceGrid(SparkSheetPath, Cell, Cell, new Vector2(0.5f, 0.5f));

            Debug.Log("[Onikiri] Baked " + Frames.Length + " impact spark frames at " +
                      Cell + "x" + Cell + " -> " + SparkSheetPath);
        }

        /** 여덟 가시와 심을 한 셀에 찍는다 */
        private static void DrawBurst(Color[] pixels, int width, int frame, Burst burst)
        {
            int originX = frame * Cell;

            // 심: 중심에서 Core 이내를 채운다
            if (burst.Core > 0f)
            {
                for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    float dx = x - Center, dy = y - Center;
                    if (dx * dx + dy * dy <= burst.Core * burst.Core)
                        Plot(pixels, width, originX + x, y);
                }
            }

            // 가시: 여덟 방향으로 Inner에서 Outer까지 긋는다. 0.5px 간격으로 걸어야
            // 대각선에서 점선이 되지 않는다
            for (int spoke = 0; spoke < 8; spoke++)
            {
                float angle = spoke * 45f * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

                float length = burst.Outer;
                if (spoke % 2 == 1) length *= DiagonalScale;   // 대각은 짧게
                if (cos > 0.01f) length *= TrailScale;         // 칼이 들어온 쪽은 길게

                for (float r = burst.Inner; r <= length; r += 0.5f)
                {
                    int x = Mathf.RoundToInt(Center + cos * r);
                    int y = Mathf.RoundToInt(Center + sin * r);
                    if (x < 0 || x >= Cell || y < 0 || y >= Cell) continue;
                    Plot(pixels, width, originX + x, y);
                }
            }
        }

        private static void Plot(Color[] pixels, int width, int x, int y)
        {
            pixels[y * width + x] = Color.white;
        }

        /**
         * @brief 색은 튜닝 값이라 **이미 있는 프리팹에도 매번 다시 쓴다.**
         *
         * 프리팹 자체는 한 번 만들면 그만이지만(참조가 끊기면 안 된다), 색은 화면을 보고
         * 고치게 되는 값이다. 만들 때만 쓰면 코드의 기본값을 바꿔도 전달되지 않고, 그러면
         * 빌더가 단일 출처 역할을 못 한다 - 씬 컴포넌트에 값을 명시적으로 기록하는 것과
         * 같은 이유다.
         */
        public static ImpactSpark BuildPrefab()
        {
            EnsureFolder(PrefabFolder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SparkPrefabPath);
            if (existing == null)
            {
                var root = new GameObject("ImpactSpark");
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrders.Vfx;
                root.AddComponent<SpriteAnimator>();
                root.AddComponent<ImpactSpark>();

                existing = PrefabUtility.SaveAsPrefabAsset(root, SparkPrefabPath);
                Object.DestroyImmediate(root);
            }

            var spark = existing.GetComponent<ImpactSpark>();

            // 붉은색이다. 흰색은 사무라이 스프라이트에 그려진 참격에 묻히고, 팩 원본의
            // 초록(실측 hue 74도)은 먹빛·적·벚꽃 팔레트와 충돌한다. ImpactSpark 참고
            var so = new SerializedObject(spark);
            so.FindProperty("tint").colorValue = new Color32(0xFF, 0x45, 0x3A, 0xFF);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);

            return spark;
        }

        /** 시트에서 프레임 순서대로 스프라이트를 뽑는다 */
        public static List<Sprite> OrderedSprites()
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SparkSheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return sprites;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
