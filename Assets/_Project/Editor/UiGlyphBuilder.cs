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

            AssetDatabase.Refresh();
            Debug.Log("[Onikiri] UI glyphs built: gear/lock/skull/flag/person/paw -> " + Folder);
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
