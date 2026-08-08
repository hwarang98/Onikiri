using System.IO;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 가을숲 타일셋에서 이어지는 지면 스트립 한 장을 굽는다.
     *
     * ## 왜 굽는가
     *
     * 지역 1의 지면은 `Ground.png` 한 장이라 그대로 두 장 교대로 스크롤하면
     * 됐다. 가을숲은 **타일셋**이다 - 32px 조각들이 격자로 들어 있고, 그중
     * 어느 것을 어떻게 이어 붙일지는 데이터에 적혀 있지 않다.
     *
     * 런타임에 타일을 깔 수도 있지만 그러면 스크롤 코드가 두 벌이 된다(한 장짜리
     * 레이어용과 타일용). 대신 **굽는 시점에 한 장으로 만들어** 지역 1과 완전히
     * 같은 경로를 태운다. ParallaxScroller는 자기가 무엇을 스크롤하는지 몰라도 된다.
     *
     * ## 이 타일셋에는 "흙" 타일이 없다
     *
     * 처음에 격자를 흙 타일 창고로 읽고 위 줄은 표면, 아래 줄은 흙으로 깔았다.
     * 틀렸다. 이것은 **플랫포머 지형용 9-슬라이스**다 - 격자에 들어 있는 것은
     * 흙 조각이 아니라 블록의 *모서리와 변*이고, 안쪽은 지나갈 일이 없으니
     * 단색으로 비워져 있다.
     *
     *     r1c2   rgb(57,31,33) 100%          <- 블록 안쪽(빈 칸) 그 자체
     *     r1c6   cave 52%, 바깥쪽만 바위     <- 왼쪽 변
     *     r1c7   cave 52%, 바깥쪽만 바위     <- 오른쪽 변
     *     r2~r4  대부분 잎 쐐기가 박힌 경사 타일
     *
     * 아래 줄에 무엇을 깔든 블록 안쪽이 화면에 노출된다. 지면 아래가 뻥 뚫린
     * 것처럼 보였던 이유다.
     *
     * **그래서 격자에서 가져오는 것은 표면 타일(r0) 한 줄뿐이고, 그 아래는
     * 표면 타일 자신의 바위 부분을 되풀이해 채운다.** 이 팩이 온전한 지면 띠로
     * 그려둔 곳은 거기 하나다.
     *
     * ## rgb(57,31,33)을 흙으로 치환하지 않는다
     *
     * 한 번 그렇게 고쳤다가 되돌렸다. 이 색은 "빈 칸 색"이 아니라 **팔레트의
     * 최암부**다 - 표면 타일에도 24%가 들어 있고, 그게 바위와 낙엽의 윤곽선이다.
     * 치환하면 아트가 통째로 뭉개진다. 안쪽이 비어 보이는 문제는 색이 아니라
     * **어느 타일을 쓰느냐**로 푼다.
     *
     * ## 어느 열을 고르는가 — 실측
     *
     * 표면 타일은 r0의 c1~c9인데 좌우가 서로 맞물리는 것은 일부뿐이라, 각
     * 조합의 이음새(끝 열 색차)를 재서 골랐다.
     *
     *     단일 타일 반복    c2 0.018 / c9 0.052 / c4,c6,c7 ~0.08
     *                       c1,c3,c5,c8 ~0.21  <- 블록 가장자리라 안 맞물린다
     *     연속 c1..c9 반복  0.226              <- 그대로 이어 붙이면 실패
     *
     *     **c6 -> c7 -> (c6)   c6->c7 0.034 / c7->c6 0.012**
     *
     * 지역 1 실측 범위(0.000~0.046) 안이고, 두 타일을 돌리므로 무늬 주기가
     * 64px이 된다.
     */
    public static class AutumnGroundBuilder
    {
        public const string TilesetPath =
            "Assets/ThirdParty/Backgrounds/AutumnForest/Tileset/Tileset.png";

        /** 구워진 결과. 지역 1의 Ground.png와 같은 자리에 선다 */
        public const string OutputPath =
            "Assets/_Project/Art/Backgrounds/AutumnGround.png";

        private const int TileSize = 32;

        /** 격자에서 가져오는 유일한 줄. 이 팩이 지면 띠로 그려둔 곳은 여기뿐이다 */
        private const int SurfaceGridRow = 0;

        /**
         * @brief 가로로 이어 붙일 표면 타일의 열 번호. 실측으로 고른 순서다.
         *
         * 순서가 곧 이음새다. 같은 셋이라도 뒤집어 돌리면 값이 달라진다 -
         * 각 타일의 왼쪽 끝과 오른쪽 끝이 다르기 때문이다.
         */
        private static readonly int[] SurfaceColumns = { 6, 7 };

        /**
         * @brief 그 순환을 몇 번 이어 붙일 것인가.
         *
         * 이음새와 무관한 값이다(같은 순환을 반복하므로). 순전히 **한 장이 화면보다
         * 넓어야** 하기 때문이다 - 64px = 2 units 인데 가시 폭이 6.75 units이라,
         * 한 장이 왼쪽으로 빠지기 전에 다음 장이 오른쪽을 덮지 못한다.
         *
         * 배경 빌더가 사본 수를 폭에서 계산하므로 이 값이 부족해도 화면이 비지는
         * 않지만, 넓은 한 장이 사본 수를 줄여 드로우 콜이 적다.
         */
        private const int Repeats = 5;

        /**
         * @brief 스트립 높이. **이 값이 곧 지면 두께다.**
         *
         * 지역 1과 구조가 다르다. `Ground.png`는 180px 높이인데 실제 흙이 아래
         * 24px뿐이고 나머지가 투명이라, 밑단을 밴드 바닥에 붙이면 표면이 자연히
         * 0.75u 위에 온다.
         *
         * 구운 스트립은 **전체가 지면**이다. 그래서 스트립 높이가 그대로 지면
         * 두께가 된다. 96px(3u)로 구웠더니 흙이 전투 밴드의 절반을 먹었고,
         * 64px(2u)이 지역 1(0.75u)보다는 두껍지만 낙엽 띠 아래로 바위가 두 겹
         * 들어가는 최소 높이라 여기가 하한이다.
         */
        private const int StripHeight = 64;

        /**
         * @brief 캐릭터가 서야 할 선. 스트립 **바닥에서** 잰 픽셀이다.
         *
         * `RegionBackgroundSet.groundSurfacePixels`에 그대로 들어간다. 세 번
         * 틀린 값이라 여기에 상수로 박고, 굽는 동안 실측과 대조해 어긋나면
         * 에러를 낸다 - 열 구성을 바꾸면 이 값도 바뀌는데, 그걸 사람이 기억하는
         * 방식은 이미 세 번 실패했다.
         *
         *     낙엽    타일 위에서 y0~y15 (y15에서 12/32로 끝난다)
         *     바위흙  y16~y27  전 줄 완전 불투명
         *     y28~y31 투명 1~3px  <- 스트립 안쪽에서 되풀이하면 진짜 구멍이 된다
         *
         * 64 - 16 = 48. **낙엽과 흙이 만나는 선**이고, 발이 낙엽 띠 밑동에
         * 닿아 낙엽 속에 선 그림이 된다. 낙엽 끝(64)으로 잡으면 잎 위에 뜨고,
         * 타일 경계(32)로 잡으면 흙 속에 박힌다. 둘 다 해봤다.
         */
        public const int SurfaceFromBottom = 48;

        [MenuItem("Onikiri/Art/Build Autumn Ground Strip")]
        public static void Build()
        {
            var importer = AssetImporter.GetAtPath(TilesetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Onikiri] Autumn tileset not found: " + TilesetPath);
                return;
            }

            // 읽기를 잠깐 켠다. 원래 꺼져 있는 것이 맞고(메모리), 끝나면 되돌린다
            bool wasReadable = importer.isReadable;
            if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

            try
            {
                var tileset = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetPath);
                if (tileset == null)
                {
                    Debug.LogError("[Onikiri] Autumn tileset failed to load.");
                    return;
                }

                int gridRows = tileset.height / TileSize;
                int tileCount = SurfaceColumns.Length * Repeats;
                int width = tileCount * TileSize;
                var pixels = new Color[width * StripHeight];

                int leafRows = -1, rockRows = -1;

                for (int i = 0; i < tileCount; i++)
                {
                    int column = SurfaceColumns[i % SurfaceColumns.Length];

                    // 격자의 r0(표면)이 텍스처에서는 가장 위 = 가장 큰 y다
                    int sourceRow = gridRows - 1 - SurfaceGridRow;
                    var tile = tileset.GetPixels(column * TileSize, sourceRow * TileSize,
                                                 TileSize, TileSize);

                    int leaves = MeasureLeafRows(tile);
                    int rocks = MeasureRockRows(tile, leaves);
                    if (leafRows < 0) { leafRows = leaves; rockRows = rocks; }

                    WriteColumn(pixels, width, i, tile, leaves, rocks);
                }

                if (StripHeight - leafRows != SurfaceFromBottom)
                {
                    Debug.LogError(string.Format(
                        "[Onikiri] 지면선이 어긋난다. 실측 {0}px, 상수 {1}px. " +
                        "SurfaceColumns를 바꿨다면 SurfaceFromBottom과 " +
                        "Background_Region2.groundSurfacePixels를 함께 고쳐야 한다.",
                        StripHeight - leafRows, SurfaceFromBottom));
                    return;
                }

                var strip = new Texture2D(width, StripHeight, TextureFormat.RGBA32, false);
                strip.SetPixels(pixels);
                strip.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                File.WriteAllBytes(OutputPath, strip.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(strip);

                AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
                ApplyImportSettings();

                Debug.Log(string.Format(
                    "[Onikiri] Autumn ground strip baked: {0}x{1} from r0[{2}] " +
                    "(낙엽 {3}px + 바위 {4}px 반복) 지면선 {5}px -> {6}",
                    width, StripHeight,
                    string.Join(",", System.Array.ConvertAll(SurfaceColumns, c => "c" + c)),
                    leafRows, rockRows, SurfaceFromBottom, OutputPath));
            }
            finally
            {
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }

        /**
         * @brief 표면 타일 한 칸을 스트립의 한 열로 쓴다.
         *
         * 위쪽은 낙엽을 그대로 얹고, 그 아래는 **같은 타일의 바위 부분**을
         * 되풀이해 채운다. 홀수 번째 되풀이는 위아래를 뒤집고 가로로 반 칸
         * 밀었다 - 그냥 이어 붙이면 같은 바위 덩어리가 세로로 줄지어 서서
         * 반복이 눈에 띈다.
         */
        private static void WriteColumn(Color[] pixels, int width, int tileIndex,
                                        Color[] tile, int leafRows, int rockRows)
        {
            // tile/pixels 모두 index = y * 폭 + x 이고 y=0 이 아래다
            int rockTop = TileSize - 1 - leafRows;   // 낙엽 바로 아래 줄
            int rockBottom = rockTop - (rockRows - 1);
            int baseX = tileIndex * TileSize;

            // 낙엽: 타일 위 leafRows 줄을 스트립 위 leafRows 줄에 그대로
            for (int r = 0; r < leafRows; r++)
            {
                int sourceY = TileSize - 1 - r;
                int targetY = StripHeight - 1 - r;
                for (int x = 0; x < TileSize; x++)
                    pixels[targetY * width + baseX + x] = tile[sourceY * TileSize + x];
            }

            // 바위: 낙엽 아래를 전부 채운다
            for (int targetY = 0; targetY < StripHeight - leafRows; targetY++)
            {
                int depth = (StripHeight - leafRows) - targetY;   // 1 = 낙엽 바로 아래
                int band = (depth - 1) / rockRows;
                int inBand = (depth - 1) % rockRows;

                int sourceY = (band % 2 == 0)
                    ? rockTop - inBand            // 위에서 아래로
                    : rockBottom + inBand;        // 뒤집어서 아래에서 위로
                int shift = (band % 2 == 0) ? 0 : TileSize / 2;

                for (int x = 0; x < TileSize; x++)
                {
                    int sourceX = (x + shift) % TileSize;
                    pixels[targetY * width + baseX + x] = tile[sourceY * TileSize + sourceX];
                }
            }
        }

        /**
         * @brief 타일 위에서 낙엽이 몇 줄인가.
         *
         * 낙엽은 채도 높은 주황이고 바위흙은 갈색이라 색으로 갈린다. 아래로
         * 내려가며 주황이 하나도 없는 첫 줄이 경계다 - 마지막 낙엽 줄은
         * 12/32 처럼 성글게 끝나므로 "전부 주황"으로 재면 4px 위를 짚는다.
         */
        private static int MeasureLeafRows(Color[] tile)
        {
            for (int r = 0; r < TileSize; r++)
            {
                int sourceY = TileSize - 1 - r;
                bool anyLeaf = false;
                for (int x = 0; x < TileSize; x++)
                {
                    var c = tile[sourceY * TileSize + x];
                    if (c.a < 0.5f) continue;
                    if (c.r > 140f / 255f && (c.r - c.g) > 60f / 255f) { anyLeaf = true; break; }
                }
                if (!anyLeaf) return r;
            }
            return TileSize;
        }

        /**
         * @brief 낙엽 아래로 **완전히 불투명한** 바위 줄이 몇 줄인가.
         *
         * 타일 맨 아래 네 줄은 투명 픽셀이 1~3개씩 있다 - 블록의 바깥 실루엣이라
         * 원본에서는 맞지만, 그 줄을 스트립 안쪽에서 되풀이하면 지면 한가운데
         * 진짜 구멍이 뚫린다. 그래서 불투명한 줄까지만 쓴다.
         */
        private static int MeasureRockRows(Color[] tile, int leafRows)
        {
            int rows = 0;
            for (int r = leafRows; r < TileSize; r++)
            {
                int sourceY = TileSize - 1 - r;
                for (int x = 0; x < TileSize; x++)
                    if (tile[sourceY * TileSize + x].a < 0.999f) return rows;
                rows++;
            }
            return rows;
        }

        /**
         * @brief 구운 PNG를 다른 배경과 같은 규격으로 임포트한다.
         *
         * PPU와 필터가 어긋나면 이 레이어만 다른 배율로 그려져 픽셀 격자가 깨진다.
         * PixelArtImportPostprocessor가 폴더 규칙으로 잡아주긴 하지만, 구운 직후
         * 명시적으로 맞춰두면 그 규칙이 바뀌어도 이 파일은 흔들리지 않는다.
         */
        private static void ApplyImportSettings()
        {
            var importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Onikiri.Core.DisplayConfig.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            // 중앙 피벗. 배경 빌더가 각 장을 자기 높이의 절반만큼 올려
            // 밑단을 밴드 바닥에 맞추는데, 그 계산이 중앙 피벗을 전제한다
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
