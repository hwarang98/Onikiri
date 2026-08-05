using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 캐릭터 시트를 격자로 슬라이싱한다.
     *
     * 중요한 것은 피벗이다. FULL_Samurai의 모든 프레임은 96x96 셀이지만 캐릭터는 그
     * 가운데만 차지하고, 발밑에 일정하게 15px의 빈 공간이 있다. 그래서 단순
     * Bottom-Center 피벗은 사무라이를 지면선 위 약 0.47 units 띄운다. 실제 기준선을
     * 한 번 측정해 거기에 피벗을 두면, 지면 Y에 배치한 캐릭터의 발이 모든 애니메이션에서
     * 지면에 닿는다. 클립마다 보정 오프셋을 넣을 필요가 없다.
     */
    public static class CharacterSpriteSlicer
    {
        public const int SamuraiCell = 96;

        /** FULL_Samurai 모든 프레임에서 발밑에 있는 빈 줄 수 */
        public const int SamuraiFeetPadding = 15;

        private const string SamuraiSpriteFolder = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites";

        /**
         * @brief 다크 사무라이(보스) 시트의 셀. 128x108이고 한 줄로 늘어서 있다.
         *
         * FULL_Samurai와 규격이 다르므로 따로 잰다. 팩이 다르면 셀 크기도 다르다는
         * 것을 상수 이름으로 남겨둔다.
         */
        public const int BossCellWidth = 128;
        public const int BossCellHeight = 108;

        /**
         * @brief 다크 사무라이 프레임에서 발밑에 있는 빈 줄 수.
         *
         * IDLE / HURT / DEATH 세 시트를 픽셀로 훑어 셋 다 12px로 일치하는 것을
         * 확인했다. 자동 슬라이싱이 만든 타이트 렉트를 쓰면 프레임마다 피벗이 달라져
         * 보스가 제자리에서 떨리는데, 격자로 자르고 피벗을 이 값에 고정하면 모든
         * 프레임이 같은 발밑을 공유한다.
         */
        public const int BossFeetPadding = 12;

        private const string BossSpriteFolder = "Assets/ThirdParty/Characters/Demon_Samurai/Sprites";

        [MenuItem("Onikiri/Art/Slice Dark Samurai (Boss) Sheets")]
        public static void SliceBoss()
        {
            var pivot = new Vector2(0.5f, BossFeetPadding / (float)BossCellHeight);
            var paths = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BossSpriteFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            int sliced = 0, skipped = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in paths)
                {
                    if (SliceGrid(path, BossCellWidth, BossCellHeight, pivot)) sliced++;
                    else skipped++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log(string.Format(
                "[Onikiri] Sliced {0} dark samurai sheets at {1}x{2}, pivot ({3}, {4:F5}). Skipped {5}.",
                sliced, BossCellWidth, BossCellHeight, pivot.x, pivot.y, skipped));
        }

        /**
         * @brief 참격 시트는 64x64의 5x2 격자다.
         *
         * 128 세트가 아니라 64 세트를 쓰는 이유는, 128이 같은 아트의 단순 2배
         * 업스케일이기 때문이다. PPU 32에서 64 프레임은 사무라이 키의 1.24배로 읽혀
         * 평타에 적절하고, 128은 2.4배라 보스/필살기 이펙트로 남겨두는 편이 낫다.
         */
        public const int SlashCell = 64;

        private static readonly string[] SlashFolders =
        {
            "Assets/ThirdParty/VFX/Slashes",
            "Assets/_Project/Art/VFX"
        };

        [MenuItem("Onikiri/Art/Slice Samurai Sheets")]
        public static void SliceSamurai()
        {
            var pivot = new Vector2(0.5f, SamuraiFeetPadding / (float)SamuraiCell);
            var paths = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SamuraiSpriteFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            int sliced = 0, skipped = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var path in paths)
                {
                    if (SliceGrid(path, SamuraiCell, SamuraiCell, pivot)) sliced++;
                    else skipped++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log(string.Format(
                "[Onikiri] Sliced {0} samurai sheets at {1}x{1}, pivot ({2}, {3:F5}). Skipped {4}.",
                sliced, SamuraiCell, pivot.x, pivot.y, skipped));
        }

        [MenuItem("Onikiri/Art/Slice Slash VFX (64px)")]
        public static void SliceSlashes()
        {
            int sliced = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", SlashFolders))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    // 128px 세트는 건드리지 않는다. 직접 색을 바꾼 시트는 이미 64px이다
                    if (!path.Contains("64x64") && !path.StartsWith("Assets/_Project/Art/VFX")) continue;

                    // 그려진 호는 64x64 셀 한가운데에 있지 않다. 그래서 단순 중앙 피벗은
                    // 이펙트를 떨어져야 할 지점에서 벗어나게 한다. 대신 아트의 중심에
                    // 피벗을 둔다. 시트 전체를 합쳐 측정하므로 애니메이션의 모든 프레임이
                    // 같은 기준점을 공유한다
                    var pivot = MeasureArtCentrePivot(path, SlashCell, SlashCell);
                    if (SliceGrid(path, SlashCell, SlashCell, pivot)) sliced++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log("[Onikiri] Sliced " + sliced + " slash sheets at " + SlashCell + "x" + SlashCell + ".");
        }

        /**
         * @brief 텍스처 하나를 왼쪽에서 오른쪽, 위에서 아래 순서의 격자로 자른다.
         *
         * 텍스처가 셀 크기의 정확한 배수가 아니면 false를 반환한다.
         */
        public static bool SliceGrid(string assetPath, int cellWidth, int cellHeight, Vector2 pivot)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null) return false;
            if (texture.width % cellWidth != 0 || texture.height % cellHeight != 0) return false;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            int columns = texture.width / cellWidth;
            int rows = texture.height / cellHeight;
            string baseName = Path.GetFileNameWithoutExtension(assetPath).Replace(' ', '_');

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // 임포트된 텍스처는 CPU에서 읽을 수 없으므로 원본 파일을 직접 읽는다.
            // 덕분에 격자가 남기는 여백 셀을 버릴 수 있다. 참격 시트는 5x2지만 10칸 중
            // 9칸만 그려져 있고, 끝에 빈 프레임이 남으면 이펙트 마지막이 끊겨 보인다
            var pixels = LoadReadableCopy(assetPath);

            // 이미 잘려 있던 스프라이트의 GUID를 이름으로 찾아둔다.
            //
            // 매번 GUID.Generate()를 부르면 슬라이싱할 때마다 .meta의 spriteID가
            // 전부 새로 찍힌다. 참조가 깨지지는 않는다 - 실제로 쓰이는 것은
            // internalID이고 그쪽은 이름 표(ISpriteNameFileIdDataProvider)가
            // 유지해준다. 하지만 빌더를 돌릴 때마다 의미 없는 diff가 수십 줄씩
            // 쌓이고, 그 안에 진짜 변경이 섞이면 알아볼 수 없게 된다.
            //
            // 빌더는 몇 번을 돌려도 같은 결과여야 한다.
            var existingIds = new Dictionary<string, GUID>();
            foreach (var rect in provider.GetSpriteRects())
            {
                if (rect != null && !string.IsNullOrEmpty(rect.name))
                    existingIds[rect.name] = rect.spriteID;
            }

            var rects = new List<SpriteRect>();
            int index = 0;
            int skipped = 0;

            // 텍스처 좌표의 원점은 좌하단이지만 시트는 위에서 아래로 읽는다.
            // 프레임 0이 시트의 첫 프레임이 되도록 행을 역순으로 훑는다
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < columns; column++)
                {
                    var cell = new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight);

                    if (pixels != null && IsCellEmpty(pixels, cell))
                    {
                        skipped++;
                        continue;
                    }

                    string spriteName = baseName + "_" + index;

                    GUID id;
                    if (!existingIds.TryGetValue(spriteName, out id)) id = GUID.Generate();

                    var spriteRect = new SpriteRect
                    {
                        name = spriteName,
                        rect = cell,
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        spriteID = id
                    };
                    rects.Add(spriteRect);
                    index++;
                }
            }

            if (pixels != null) Object.DestroyImmediate(pixels);
            if (skipped > 0)
                Debug.Log("[Onikiri] " + System.IO.Path.GetFileName(assetPath) + ": skipped " + skipped + " empty cell(s).");

            provider.SetSpriteRects(rects.ToArray());

            // Unity 2021 이상은 이름 -> file id 표를 유지한다. 이것이 없으면 재슬라이싱
            // 때마다 스프라이트 GUID가 바뀌어 기존 참조가 전부 깨진다
            var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>();
                foreach (var rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
                nameProvider.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /**
         * @brief 시트의 모든 셀을 합친 그려진 아트의 중심을 정규화 피벗으로 반환한다.
         *
         * 파일을 읽을 수 없으면 셀 중앙으로 폴백한다.
         */
        private static Vector2 MeasureArtCentrePivot(string assetPath, int cellWidth, int cellHeight)
        {
            var texture = LoadReadableCopy(assetPath);
            if (texture == null) return new Vector2(0.5f, 0.5f);

            int columns = texture.width / cellWidth;
            int rows = texture.height / cellHeight;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    var pixels = texture.GetPixels(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if (pixels[i].a <= 0.03f) continue;
                        int x = i % cellWidth;
                        int y = i / cellWidth;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            Object.DestroyImmediate(texture);
            if (maxX < 0) return new Vector2(0.5f, 0.5f);

            float centreX = (minX + maxX + 1) * 0.5f / cellWidth;
            float centreY = (minY + maxY + 1) * 0.5f / cellHeight;
            return new Vector2(centreX, centreY);
        }

        /**
         * @brief 디스크의 PNG를 임시 읽기 가능 텍스처로 디코드한다.
         *
         * 실제 에셋의 isReadable을 켜는 방식을 피한다. 그러면 재임포트가 강제되고
         * 프로젝트가 모든 스프라이트 시트의 CPU 사본을 들고 다니게 된다.
         */
        private static Texture2D LoadReadableCopy(string assetPath)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(assetPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    Object.DestroyImmediate(texture);
                    return null;
                }
                return texture;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Onikiri] Could not read " + assetPath + " for empty-cell detection: " + e.Message);
                return null;
            }
        }

        private static bool IsCellEmpty(Texture2D texture, Rect cell)
        {
            int x0 = Mathf.Clamp((int)cell.x, 0, texture.width);
            int y0 = Mathf.Clamp((int)cell.y, 0, texture.height);
            int w = Mathf.Clamp((int)cell.width, 0, texture.width - x0);
            int h = Mathf.Clamp((int)cell.height, 0, texture.height - y0);
            if (w <= 0 || h <= 0) return true;

            var pixels = texture.GetPixels(x0, y0, w, h);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0.03f) return false;
            }
            return true;
        }
    }
}
