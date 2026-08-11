using System.IO;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 봄숲(SpringForest) 팩이 쓰려면 먼저 구워야 하는 것들.
     *
     * 지역 1이 이 팩으로 바뀌면서 필요해졌다. 가을숲과 같은 종류의 준비가 필요한데,
     * 팩마다 격자 규격도 지면 구조도 달라서 값을 그대로 쓸 수 없다.
     *
     *   1. 지면 스트립  - 타일셋에서 한 장으로 굽는다 (AutumnGroundBuilder와 같은 이유)
     *   2. 여명 안개    - 곱연산으로는 만들 수 없는 따뜻함을 얹는 오버레이
     *   3. 대장간 분할  - 13프레임 애니 시트에서 한 장만 쓴다
     */
    public static class SpringForestBuilder
    {
        private const string PackFolder = "Assets/ThirdParty/Backgrounds/SpringForest";

        public const string TilesetPath = PackFolder + "/Tiles/Tileset.png";
        public const string BlacksmithPath = PackFolder + "/Props/Blacksmith.png";

        public const string GroundPath = "Assets/_Project/Art/Backgrounds/SpringGround.png";
        public const string HazePath = "Assets/_Project/Art/Backgrounds/DawnHaze.png";
        public const string SkyPath = "Assets/_Project/Art/Backgrounds/SpringSky.png";

        // ================================================================ 지면 스트립

        private const int TileSize = 16;

        /**
         * @brief 지면으로 쓸 타일. 격자에서 (행, 열), 행은 화면 위에서 아래.
         *
         * ## 어떻게 골랐는가 — 실측
         *
         * 이 팩의 타일셋도 가을숲과 마찬가지로 **플랫포머 지형용 9-슬라이스**다.
         * 19x11 격자에 들어 있는 것은 흙 창고가 아니라 블록 예시들이고, 블록
         * 안쪽은 지나갈 일이 없으니 어두운 남색으로 비어 있다. 그래서 격자에서
         * 가져올 수 있는 것은 **풀이 얹힌 표면 줄** 뿐이다.
         *
         * 표면 후보 중 "풀 아래가 완전히 불투명한" 것만 남기면 일곱 칸이 된다
         * (r1c2 r1c3 r1c8 r1c12 r4c2 r6c12 r6c13). 나머지는 블록 모서리라 투명
         * 픽셀이 섞여 있고, 그런 줄을 스트립 안쪽에서 되풀이하면 지면 한가운데
         * 진짜 구멍이 뚫린다 - 가을숲에서 겪은 것과 같다.
         *
         * 남은 일곱의 이음새(끝 열 색차)를 전수로 쟀다:
         *
         *     단일 반복   r6c13 0.0379  r4c2 0.0466  r1c2 0.0757  r1c12 0.0944
         *                 r1c3 / r1c8 0.1562
         *     2~4 타일 순환 최적도 0.0379 (= r6c13 단독)를 못 넘었다
         *
         * **r6c13 단독이 최선**이고, 지역 1 실측 범위(0.000~0.046) 안이다.
         */
        private static readonly Vector2Int SurfaceTile = new Vector2Int(6, 13);

        /**
         * @brief 한 칸 걸러 좌우를 뒤집는다. **이음새가 정확히 0이 된다.**
         *
         * 뒤집은 사본의 왼쪽 끝 열은 원본의 오른쪽 끝 열과 같은 픽셀이다. 그래서
         * 원본 -> 뒤집은 것의 이음매는 자기 자신과 만나고, 색차가 계산이 아니라
         * 정의상 0이다. 반대쪽 이음매도 같은 이유로 0이다.
         *
         * 덤으로 무늬 주기가 16px에서 32px로 늘어난다. 16px(0.5u) 주기는 화면
         * 하나에 열세 번 반복되어 눈에 띄는데, 뒤집기는 그것을 공짜로 절반으로
         * 줄인다.
         */
        private const int PeriodTiles = 2;

        /** 순환을 몇 번 이어 붙일 것인가. 한 장이 가시 폭(6.75u)보다 넓어야 한다 */
        private const int Repeats = 10;

        /**
         * @brief 스트립 높이. 이 값이 곧 지면 두께다.
         *
         * 가을숲은 64px(2u), 지역 3(옛 지역 1)은 24px(0.75u)이다. 봄숲은 32px(1u).
         *
         * 48px로 먼저 구웠다가 줄였다. **이 팩의 바위는 거의 검은 남색이다** - 가을숲의
         * 갈색 흙과 달리 화면에서 검은 띠로 읽히는데, 여명 하늘 아래에서 그 띠가
         * 두꺼우면 그림의 아래 6분의 1이 통째로 죽는다. 틴트로는 못 고친다. 곱연산은
         * 밝게 만들 수 없고 원본이 이미 어둡기 때문이다.
         *
         * 그래서 높이로 푼다. 32px이면 풀 8px 아래 바위가 24px(세 겹)이라 지면으로
         * 읽히면서도 띠가 얇다. 캐릭터가 서는 높이도 옛 지역 1과 같아진다.
         */
        private const int StripHeight = 32;

        /**
         * @brief 캐릭터가 서는 선. 스트립 **바닥에서** 잰 픽셀이다.
         *
         * 풀은 타일 위 8줄이고 그 아래가 바위다. 32 - 8 = 24가 **풀과 바위가 만나는
         * 선**이고, 발이 풀 밑동에 닿아 풀 속에 선 그림이 된다. 풀 끝(32)으로 잡으면
         * 풀 위에 뜨고, 타일 경계로 잡으면 바위 속에 박힌다 - 가을숲에서 둘 다 해봤다.
         *
         * 굽는 동안 실측과 대조해 어긋나면 에러를 낸다. 타일을 바꾸면 이 값도 바뀌는데,
         * 그걸 사람이 기억하는 방식은 이미 세 번 실패했다. **실제로 여기서 두 번 걸렸다** -
         * 처음엔 다른 표면 타일(r1c2~c4)의 풀이 7줄이라 그 값을 적었고(r6c13은 8줄),
         * 그다음엔 스트립 높이를 48에서 32로 줄이면서 이 값을 같이 안 고쳤다.
         */
        public const int SurfaceFromBottom = 24;

        // ================================================================ 여명 안개

        /**
         * @brief 안개 스프라이트 크기. 배경 레이어와 같은 규격이다.
         *
         * 가로로는 완전히 균일하다. 그래야 스크롤해도 이음매가 보이지 않고, 사본이
         * 어디서 만나든 상관이 없다.
         */
        private const int HazeWidth = 384;
        private const int HazeHeight = 216;

        /**
         * @brief 아래에서 위로 가는 알파. 지평선이 가장 짙다.
         *
         * 대기 원근이다 - 멀리 있는 것일수록, 그리고 지평선에 가까울수록 공기가
         * 두껍게 낀다. 위쪽 하늘은 맑아야 하늘로 읽힌다.
         */
        private const float HazeAlphaBottom = 0.95f;
        private const float HazeAlphaTop = 0.0f;

        /** 알파가 꺾이는 높이 비율. 이 위로는 빠르게 맑아진다 */
        private const float HazeHorizon = 0.42f;

        // ================================================================ 대장간

        /**
         * @brief 대장간 시트의 한 프레임 폭.
         *
         * 2288x112 한 장으로 보이지만 **13프레임 애니메이션**이다. 빈 열이 정확히
         * 176px 간격(171~179, 347~355, 523~531 ...)으로 나 있어서 확정했다.
         *
         * 배경 랜드마크로는 한 장이면 된다. 애니메이션(풀무 불꽃)은 먼 배경에서
         * 보이지도 않고, 13장을 도는 스크롤 레이어를 만들 이유가 없다.
         */
        public const int BlacksmithFrameWidth = 176;
        public const int BlacksmithFrameHeight = 112;

        [MenuItem("Onikiri/Art/Build Spring Forest Pieces")]
        public static void BuildAll()
        {
            BuildGroundStrip();
            BuildDawnHaze();
            BuildSkySheet();
            SliceBlacksmith();
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- 지면

        public static void BuildGroundStrip()
        {
            var importer = AssetImporter.GetAtPath(TilesetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Onikiri] Spring tileset not found: " + TilesetPath);
                return;
            }

            bool wasReadable = importer.isReadable;
            if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

            try
            {
                var tileset = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetPath);
                if (tileset == null) { Debug.LogError("[Onikiri] Spring tileset failed to load."); return; }

                // 격자의 행은 화면 위에서 아래지만 텍스처 y는 아래가 0이다
                int sourceY = tileset.height - (SurfaceTile.x + 1) * TileSize;
                var tile = tileset.GetPixels(SurfaceTile.y * TileSize, sourceY, TileSize, TileSize);

                int grassRows = MeasureGrassRows(tile);
                int rockRows = MeasureRockRows(tile, grassRows);

                if (StripHeight - grassRows != SurfaceFromBottom)
                {
                    Debug.LogError(string.Format(
                        "[Onikiri] 지면선이 어긋난다. 실측 {0}px, 상수 {1}px. " +
                        "SurfaceTile을 바꿨다면 SurfaceFromBottom과 " +
                        "Background_Region1.groundSurfacePixels를 함께 고쳐야 한다.",
                        StripHeight - grassRows, SurfaceFromBottom));
                    return;
                }
                if (rockRows <= 0)
                {
                    Debug.LogError("[Onikiri] 표면 타일 아래에 불투명한 바위 줄이 없다. 타일 선택이 틀렸다.");
                    return;
                }

                int tileCount = PeriodTiles * Repeats;
                int width = tileCount * TileSize;
                var pixels = new Color[width * StripHeight];

                var mirrored = MirrorHorizontally(tile);

                for (int i = 0; i < tileCount; i++)
                    WriteColumn(pixels, width, i, (i % 2 == 0) ? tile : mirrored, grassRows, rockRows);

                // 바위를 흙으로 다시 칠한다(41단계). 이 팩의 바위는 거의 검은
                // 남색이라(위 StripHeight 주석) 두께를 줄이는 것으로 버텼는데,
                // 남은 24px도 하단 UI 먹빛과 휘도가 겹쳐 한 덩어리 검정으로
                // 읽혔다. 색은 여명 팔레트의 남보라를 유지하고 밝기만 편다.
                // 풀 포기 사이의 틈(바위색)도 함께 메운다 - "풀 사이 검은
                // 구멍"의 정체가 그 틈이었다
                DirtTextureBaker.Apply(pixels, width, StripHeight, SurfaceFromBottom,
                                       new Color(0.137f, 0.125f, 0.235f, 1f),   // 어두운 흙 #23203C
                                       new Color(0.361f, 0.329f, 0.486f, 1f),   // 밝은 흙 #5C547C
                                       new Color(0.459f, 0.424f, 0.588f, 1f),   // 자갈 #756C96
                                       true);

                var strip = new Texture2D(width, StripHeight, TextureFormat.RGBA32, false);
                strip.SetPixels(pixels);
                strip.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(GroundPath));
                File.WriteAllBytes(GroundPath, strip.EncodeToPNG());
                Object.DestroyImmediate(strip);

                AssetDatabase.ImportAsset(GroundPath, ImportAssetOptions.ForceUpdate);
                ApplyBackgroundImportSettings(GroundPath);

                Debug.Log(string.Format(
                    "[Onikiri] Spring ground strip baked: {0}x{1} from r{2}c{3} " +
                    "(풀 {4}px + 바위 {5}px 반복, 한 칸 걸러 좌우 반전) 지면선 {6}px -> {7}",
                    width, StripHeight, SurfaceTile.x, SurfaceTile.y,
                    grassRows, rockRows, SurfaceFromBottom, GroundPath));
            }
            finally
            {
                if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
            }
        }

        private static Color[] MirrorHorizontally(Color[] tile)
        {
            var flipped = new Color[tile.Length];
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    flipped[y * TileSize + x] = tile[y * TileSize + (TileSize - 1 - x)];
            return flipped;
        }

        /**
         * @brief 타일 한 칸을 스트립의 한 열로 쓴다.
         *
         * 위쪽은 풀을 그대로 얹고, 그 아래는 **같은 타일의 바위 부분**을 되풀이해
         * 채운다. 홀수 번째 되풀이는 위아래를 뒤집고 가로로 반 칸 밀었다 - 그냥
         * 이어 붙이면 같은 바위 덩어리가 세로로 줄지어 서서 반복이 눈에 띈다.
         */
        private static void WriteColumn(Color[] pixels, int width, int tileIndex,
                                        Color[] tile, int grassRows, int rockRows)
        {
            int rockTop = TileSize - 1 - grassRows;
            int rockBottom = rockTop - (rockRows - 1);
            int baseX = tileIndex * TileSize;

            for (int r = 0; r < grassRows; r++)
            {
                int sourceY = TileSize - 1 - r;
                int targetY = StripHeight - 1 - r;
                for (int x = 0; x < TileSize; x++)
                    pixels[targetY * width + baseX + x] = tile[sourceY * TileSize + x];
            }

            for (int targetY = 0; targetY < StripHeight - grassRows; targetY++)
            {
                int depth = (StripHeight - grassRows) - targetY;
                int band = (depth - 1) / rockRows;
                int inBand = (depth - 1) % rockRows;

                int sourceY = (band % 2 == 0) ? rockTop - inBand : rockBottom + inBand;
                int shift = (band % 2 == 0) ? 0 : TileSize / 2;

                for (int x = 0; x < TileSize; x++)
                    pixels[targetY * width + baseX + x] =
                        tile[sourceY * TileSize + (x + shift) % TileSize];
            }
        }

        /** 타일 위에서 풀이 몇 줄인가. 풀은 채도 높은 초록이고 바위는 회남색이다 */
        private static int MeasureGrassRows(Color[] tile)
        {
            for (int r = 0; r < TileSize; r++)
            {
                int sourceY = TileSize - 1 - r;
                bool anyGrass = false;
                for (int x = 0; x < TileSize; x++)
                {
                    var c = tile[sourceY * TileSize + x];
                    if (c.a < 0.5f) continue;
                    float h, s, v;
                    Color.RGBToHSV(new Color(c.r, c.g, c.b), out h, out s, out v);
                    if (s > 0.3f && h * 360f > 70f && h * 360f < 160f) { anyGrass = true; break; }
                }
                if (!anyGrass) return r;
            }
            return TileSize;
        }

        /** 풀 아래로 **완전히 불투명한** 바위 줄이 몇 줄인가 */
        private static int MeasureRockRows(Color[] tile, int grassRows)
        {
            int rows = 0;
            for (int r = grassRows; r < TileSize; r++)
            {
                int sourceY = TileSize - 1 - r;
                for (int x = 0; x < TileSize; x++)
                    if (tile[sourceY * TileSize + x].a < 0.999f) return rows;
                rows++;
            }
            return rows;
        }

        // ---------------------------------------------------------------- 여명 안개

        /**
         * @brief 여명 오버레이 한 장. 흰색 + 세로 알파 그라디언트다.
         *
         * ## 왜 틴트만으로는 안 되는가
         *
         * 배경 틴트는 `SpriteRenderer.color`, 즉 **곱연산**이다. 곱셈은 채널을 줄일
         * 수만 있고 늘릴 수 없다. 봄숲 하늘은 시안(실측 hue 183, R이 가장 낮은 채널)이라,
         * 어떤 따뜻한 색을 곱해도 R이 올라가지 않는다 - 따뜻해지는 것이 아니라 그냥
         * 탁한 청록이 된다. 실제로 그렇게 나왔다.
         *
         * 그래서 **얹는다.** 알파 블렌딩은 더할 수 있다. 색은 런타임 틴트가 정하고
         * (초록 금지 같은 규칙을 한 곳에 두기 위해) 이 시트는 알파 모양만 갖는다.
         *
         * ## 왜 세게 걸지 않는가
         *
         * 프롬프트의 경고가 맞다 - 세게 걸면 원본 디테일이 뭉개진다. 여기서 굽는 것은
         * **알파의 모양**이고 세기는 레이어 틴트의 알파가 정하므로, 화면을 보고 그
         * 한 값만 조절하면 된다. 시트를 다시 구울 일이 없다.
         */
        public static void BuildDawnHaze()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(HazePath) != null) return;

            var pixels = new Color[HazeWidth * HazeHeight];

            for (int y = 0; y < HazeHeight; y++)
            {
                // y=0이 아래(지평선 쪽)다
                float up = y / (float)(HazeHeight - 1);

                // 지평선까지는 짙게 유지하다가 그 위로 부드럽게 걷힌다
                float t = up <= HazeHorizon
                    ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (up - HazeHorizon) / (1f - HazeHorizon));

                float alpha = Mathf.Lerp(HazeAlphaTop, HazeAlphaBottom, t);
                var color = new Color(1f, 1f, 1f, alpha);

                for (int x = 0; x < HazeWidth; x++) pixels[y * HazeWidth + x] = color;
            }

            var texture = new Texture2D(HazeWidth, HazeHeight, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(HazePath));
            File.WriteAllBytes(HazePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(HazePath, ImportAssetOptions.ForceUpdate);
            ApplyBackgroundImportSettings(HazePath);

            Debug.Log("[Onikiri] Dawn haze baked: " + HazeWidth + "x" + HazeHeight + " -> " + HazePath);
        }

        // ---------------------------------------------------------------- 하늘 시트

        /**
         * @brief 하늘 장(layer_1)의 미아 줄무늬를 지운 사본을 굽는다 (2b 후속).
         *
         * 원본 하늘은 위쪽이 짙은 파랑(#7BDBFF), 40행부터 옅은 파랑(#A1EEFF)인데,
         * **49~51행 세 줄만 다시 짙은 파랑이다.** 캔버스에서는 눈에 안 띄지만
         * 화면에서는 5배로 늘어나 15px 띠가 되고, 옅은 하늘 한가운데를 가로지르는
         * 전폭 가로선이라 "하늘이 중간에 짤린" 이음매로 읽힌다 - 1080x1920에서
         * 상단 바 바로 아래가 정확히 이 자리다(실측 단차 0.075, 기준 0.05 위).
         *
         * 픽셀 수술은 세 줄의 짙은 파랑을 옅은 파랑으로 바꾸는 것뿐이다. 구름
         * 픽셀은 색이 달라 건드리지 않고, 팔레트가 네 색뿐인 픽셀 아트라 정확
         * 일치로 안전하다. 원본(ThirdParty)은 손대지 않는다 - 지면 스트립과
         * 같은 규칙으로 _Project에 사본을 굽고 배경 세트가 그쪽을 문다.
         *
         * 40행 경계 자체(짙은 -> 옅은)는 지우지 않는다. 원본이 37~39행에 디더로
         * 그려 놓은 **의도된 하늘 층**이고, 그것까지 밀면 그림이 밋밋해진다.
         * 지우는 것은 층이 아니라 층에서 떨어져 나온 세 줄이다.
         */
        public static void BuildSkySheet()
        {
            const string sourcePath = PackFolder + "/Background/layer_1.png";

            var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Onikiri] Spring sky source not found: " + sourcePath);
                return;
            }

            bool wasReadable = importer.isReadable;
            if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

            try
            {
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                if (source == null) { Debug.LogError("[Onikiri] Spring sky failed to load."); return; }

                var pixels = source.GetPixels32();
                int width = source.width, height = source.height;

                // 줄무늬의 짙은 파랑과 그 자리의 원래 하늘색. 값은 팔레트 실측이다
                var stray = new Color32(123, 219, 255, 255);
                var sky = new Color32(161, 238, 255, 255);

                // 위에서 센 49~51행. 텍스처 y는 아래가 0이다
                int replaced = 0;
                for (int rowFromTop = 49; rowFromTop <= 51; rowFromTop++)
                {
                    int y = height - 1 - rowFromTop;
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        if (pixels[i].r == stray.r && pixels[i].g == stray.g
                            && pixels[i].b == stray.b && pixels[i].a == stray.a)
                        {
                            pixels[i] = sky;
                            replaced++;
                        }
                    }
                }

                // 자리를 못 찾으면 원본이 바뀐 것이다 - 조용히 원본 그대로 구우면
                // 줄무늬가 남은 채로 "고쳤다"가 되므로 에러로 말한다
                if (replaced == 0)
                {
                    Debug.LogError("[Onikiri] 하늘 줄무늬(49~51행, #7BDBFF)를 못 찾았다 - "
                                   + "원본이 바뀌었다면 자리를 다시 실측해야 한다.");
                    return;
                }

                var baked = new Texture2D(width, height, TextureFormat.RGBA32, false);
                baked.SetPixels32(pixels);
                baked.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(SkyPath));
                File.WriteAllBytes(SkyPath, baked.EncodeToPNG());
                Object.DestroyImmediate(baked);

                AssetDatabase.ImportAsset(SkyPath, ImportAssetOptions.ForceUpdate);
                ApplyBackgroundImportSettings(SkyPath);

                Debug.Log(string.Format(
                    "[Onikiri] Spring sky baked: {0}x{1}, 줄무늬 픽셀 {2}개 치환 -> {3}",
                    width, height, replaced, SkyPath));
            }
            finally
            {
                if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
            }
        }

        // ---------------------------------------------------------------- 대장간

        /**
         * @brief 애니 시트를 프레임 격자로 자른다. 배경은 그중 첫 장만 쓴다.
         *
         * 자르지 않으면 스프라이트 하나가 2288px(71.5u)이 되어, 랜드마크 간격 계산이
         * 통째로 무의미해진다 - 한 장이 화면의 열 배 넓다.
         */
        public static void SliceBlacksmith()
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(BlacksmithPath);
            int sprites = 0;
            foreach (var a in existing) if (a is Sprite) sprites++;
            if (sprites >= 13) return;

            CharacterSpriteSlicer.SliceGrid(BlacksmithPath, BlacksmithFrameWidth, BlacksmithFrameHeight,
                                            new Vector2(0.5f, 0.5f));

            Debug.Log("[Onikiri] Blacksmith sliced at " + BlacksmithFrameWidth + "x" + BlacksmithFrameHeight);
        }

        /** 배경 랜드마크로 쓸 한 장. 시트의 첫 프레임이다 */
        public static Sprite BlacksmithFrame()
        {
            SliceBlacksmith();

            Sprite first = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(BlacksmithPath))
            {
                var sprite = asset as Sprite;
                if (sprite == null) continue;
                if (first == null || EditorUtility.NaturalCompare(sprite.name, first.name) < 0)
                    first = sprite;
            }
            return first;
        }

        // ---------------------------------------------------------------- 도구

        private static void ApplyBackgroundImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Onikiri.Core.DisplayConfig.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
