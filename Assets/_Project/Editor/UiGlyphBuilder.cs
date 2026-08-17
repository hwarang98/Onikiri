using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief UI 글리프(톱니·자물쇠·해골·깃발·인물·발자국)를 코드로 굽는다 (38단계).
     *
     * 보유 팩 전수 조사 결과(KURAI 11종 - 전부 스탯/스킬 심볼, Kyrise 133종 -
     * 전부 아이템, Kenney - 버튼/화살표) **UI 기호 계열이 하나도 없다.** 설정
     * 톱니와 잠금 자물쇠 없이는 아이콘화가 서지 않으므로, 화지 텍스처·타격
     * 불꽃·벚꽃과 같은 방식으로 코드에서 굽는다 - 단색 16px 기호라 팩 아트보다
     * 오히려 먹빛 미니멀에 맞는다.
     *
     * **구매 팩으로 교체 가능하다.** 여기서 만든 PNG를 같은 이름으로 덮으면
     * 배선은 그대로다. 교체 후보 목록은 38단계 보고서에 있다.
     *
     * 흰색으로 굽고 쓰는 쪽에서 Image.color로 물들인다(UiIcons.Tint 계열) -
     * 색을 구우면 틴트 규칙이 두 곳으로 갈라진다.
     */
    public static class UiGlyphBuilder
    {
        public const string Folder = "Assets/_Project/Art/UI/Glyphs";

        public static string PathOf(string name) { return Folder + "/" + name + ".png"; }

        public const string Gear = "glyph_gear";
        public const string Lock = "glyph_lock";
        public const string Skull = "glyph_skull";
        public const string Flag = "glyph_flag";
        public const string Person = "glyph_person";
        public const string Paw = "glyph_paw";

        /**
         * @brief 트로피 (#15). 상단 바의 랭킹 칩.
         *
         * 그전에는 "랭킹" 두 글자가 칩 안에 들어 있었다. 상단 바에서 글자를
         * 쓰는 것은 재화 수치뿐이고(38단계 아이콘화), 그 줄에서 유일하게
         * 남아 있던 글자 버튼이 이것이었다.
         *
         * 다른 글리프와 같은 규칙으로 흰색으로 굽는다 - 금빛은 쓰는 쪽이
         * Image.color로 입힌다(UiSkin.Gold). 색을 구워버리면 잠금·비활성
         * 상태에서 톤을 낮출 방법이 없다.
         */
        public const string Trophy = "glyph_trophy";

        /**
         * @brief 알림 점 (#4). **진짜 동그라미다.**
         *
         * 처음에는 9-슬라이스 판(UiSkin.Row 계열)을 정사각으로 눌러 썼는데,
         * 그 판은 모서리가 둥근 **사각형**이라 28px에서도 사각형으로 읽혔다.
         * 점은 점이어야 한다 - 알림의 표준 모양이고, 사각형은 "작은 버튼"으로
         * 보인다.
         *
         * 다른 글리프처럼 흰색으로 굽고 쓰는 쪽이 붉게 물들인다.
         */
        public const string Dot = "glyph_dot";

        [MenuItem("Onikiri/Art/Build UI Glyphs")]
        public static void Build()
        {
            EnsureFolder();

            WriteGlyph(Gear, GearPixels());
            WriteGlyph(Lock, FromRows(LockRows));
            WriteGlyph(Skull, FromRows(SkullRows));
            WriteGlyph(Flag, FromRows(FlagRows));
            WriteGlyph(Person, FromRows(PersonRows));
            WriteGlyph(Paw, FromRows(PawRows));
            WriteGlyph(Trophy, FromRows(TrophyRows));
            WriteGlyph(Dot, DotPixels());

            AssetDatabase.Refresh();
            Debug.Log("[Onikiri] UI glyphs built: gear/lock/skull/flag/person/paw/trophy -> " + Folder);
        }

        public static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PathOf(name));
            if (sprite == null)
                Debug.LogWarning("[Onikiri] UI glyph missing: " + name + " - run Onikiri/Art/Build UI Glyphs");
            return sprite;
        }

        // ---------------------------------------------------------------- 도형

        /**
         * @brief 톱니는 손으로 그리지 않고 수식으로 만든다.
         *
         * 8개 이빨의 각도 대칭을 문자열 그림으로 지키기가 오히려 어렵다.
         * 고리(반지름 2.6~5.1) + 45도 간격 이빨(±24도, 반지름 7.2까지).
         */
        private static bool[,] GearPixels()
        {
            var pixels = new bool[16, 16];
            const float center = 7.5f;

            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = x - center, dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 2.6f && r <= 5.1f) { pixels[y, x] = true; continue; }

                    if (r > 5.1f && r <= 7.2f)
                    {
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f;
                        float local = Mathf.Repeat(angle, 45f);
                        if (local <= 24f * 0.5f || local >= 45f - 24f * 0.5f)
                            pixels[y, x] = true;
                    }
                }

            return pixels;
        }

        /**
         * @brief 꽉 찬 원 (#4). 톱니처럼 **수식으로** 만든다.
         *
         * 문자열 그림으로 원을 그리면 대칭이 눈으로는 맞아 보여도 반드시
         * 한두 칸이 어긋나고, 그 어긋남이 28px로 줄면 한쪽이 눌린 타원으로
         * 보인다. 반지름 하나면 대칭이 공짜다.
         *
         * 7.2는 16칸 격자에 들어가는 가장 큰 원이다(중심 7.5, 가장자리까지
         * 7.5). 0.3칸을 남기는 이유는 픽셀 아트에서 원이 캔버스에 딱 붙으면
         * 사방 끝이 평평하게 잘려 다시 사각형처럼 읽히기 때문이다.
         */
        private static bool[,] DotPixels()
        {
            var pixels = new bool[16, 16];
            const float center = 7.5f;
            const float radius = 7.2f;

            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = x - center, dy = y - center;
                    pixels[y, x] = dx * dx + dy * dy <= radius * radius;
                }

            return pixels;
        }

        private static readonly string[] LockRows =
        {
            "................",
            "....XXXXXXXX....",
            "...XX......XX...",
            "...XX......XX...",
            "...XX......XX...",
            "...XX......XX...",
            ".XXXXXXXXXXXXXX.",
            ".XXXXXXXXXXXXXX.",
            ".XXXXXX..XXXXXX.",
            ".XXXXX....XXXXX.",
            ".XXXXX....XXXXX.",
            ".XXXXXX..XXXXXX.",
            ".XXXXXX..XXXXXX.",
            ".XXXXXXXXXXXXXX.",
            ".XXXXXXXXXXXXXX.",
            "................"
        };

        private static readonly string[] SkullRows =
        {
            "................",
            "....XXXXXXXX....",
            "...XXXXXXXXXX...",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXXX..",
            "..XX..XXXX..XX..",
            "..XX..XXXX..XX..",
            "..XXXXXXXXXXXX..",
            "..XXXXX..XXXXX..",
            "...XXXXXXXXXX...",
            "....XXXXXXXX....",
            "....XXXXXXXX....",
            "....X.XX.X.X....",
            "....XXXXXXXX....",
            "................",
            "................"
        };

        private static readonly string[] FlagRows =
        {
            "..XX............",
            "..XXXXXXXXXX....",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXX...",
            "..XXXXXXXXX.....",
            "..XXXXXX........",
            "..XX............",
            "..XX............",
            "..XX............",
            "..XX............",
            "..XX............",
            "..XX............",
            "..XX............",
            "..XX............",
            "................"
        };

        /** 상투 튼 사무라이 두상. 캐릭터 탭 */
        private static readonly string[] PersonRows =
        {
            ".......XX.......",
            "......XXXX......",
            ".......XX.......",
            ".....XXXXXX.....",
            "....XXXXXXXX....",
            "....XXXXXXXX....",
            "....XXXXXXXX....",
            ".....XXXXXX.....",
            "................",
            "....XXXXXXXX....",
            "..XXXXXXXXXXXX..",
            ".XXXXXXXXXXXXXX.",
            ".XXXXXXXXXXXXXX.",
            ".XXXXXXXXXXXXXX.",
            ".XXXXXXXXXXXXXX.",
            "................"
        };

        /** 짐승 발자국. 동료 탭 */
        private static readonly string[] PawRows =
        {
            "................",
            "..XX...XX...XX..",
            ".XXXX.XXXX.XXXX.",
            ".XXXX.XXXX.XXXX.",
            "..XX...XX...XX..",
            "................",
            "....XXXXXXXX....",
            "...XXXXXXXXXX...",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXXX..",
            "...XXXXXXXXXX...",
            "....XXXXXXXX....",
            "................",
            "................"
        };

        /**
         * @brief 우승컵 (#15). 손잡이 둘 · 잔 · 목 · 받침.
         *
         * 16px에서 트로피가 트로피로 읽히려면 **손잡이가 몸통에서 떨어져
         * 있어야** 한다. 붙여 그리면 잔이 그냥 넓어진 모양이 되고, 그때는
         * 성배·자루·모래시계와 구분되지 않는다. 그래서 3~5행에서 양옆 두 칸을
         * 비워 고리를 만든다.
         *
         * 받침은 두 단이다(넓은 바닥 + 좁은 목). 한 단이면 아래가 잘린 잔으로
         * 읽히는데, 이 칩은 하단 바가 아니라 상단 바에 있어서 아래쪽 여백이
         * 좁다 - 잘림과 디자인이 헷갈리는 자리다.
         */
        private static readonly string[] TrophyRows =
        {
            "................",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXXX..",
            "XX.XXXXXXXXXX.XX",
            "XX.XXXXXXXXXX.XX",
            "XX.XXXXXXXXXX.XX",
            "XX..XXXXXXXX..XX",
            ".XX..XXXXXX..XX.",
            "..XX.XXXXXX.XX..",
            "......XXXX......",
            "......XXXX......",
            "......XXXX......",
            "....XXXXXXXX....",
            "....XXXXXXXX....",
            "..XXXXXXXXXXXX..",
            "..XXXXXXXXXXXX.."
        };

        // ---------------------------------------------------------------- 굽기

        private static bool[,] FromRows(string[] rows)
        {
            var pixels = new bool[16, 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    pixels[y, x] = rows[y][x] == 'X';
            return pixels;
        }

        private static void WriteGlyph(string name, bool[,] pixels)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var colors = new Color32[16 * 16];

            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    // 문자열 그림은 위가 0행이고 텍스처는 아래가 0행이다
                    bool ink = pixels[15 - y, x];
                    colors[y * 16 + x] = ink
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }

            texture.SetPixels32(colors);
            texture.Apply();

            string path = PathOf(name);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/UI"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "UI");
            AssetDatabase.CreateFolder("Assets/_Project/Art/UI", "Glyphs");
        }
    }
}
