using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Grid-slices character sheets.
    ///
    /// The pivot is the important part. Every FULL_Samurai frame is a 96x96 cell but the
    /// character only occupies the middle of it, with a consistent 15px of empty space
    /// below the feet. A plain Bottom-Center pivot would therefore float the samurai
    /// ~0.47 units above the ground line. Measuring the real baseline once and pivoting
    /// there means a character placed at ground Y has its feet on the ground, in every
    /// animation, with no per-clip fudge offsets.
    /// </summary>
    public static class CharacterSpriteSlicer
    {
        public const int SamuraiCell = 96;

        /// <summary>Rows of empty space beneath the feet in every FULL_Samurai frame.</summary>
        public const int SamuraiFeetPadding = 15;

        private const string SamuraiSpriteFolder = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites";

        /// <summary>
        /// Slash sheets are 5x2 grids of 64x64. We use the 64 set rather than the 128 set
        /// because the 128 export is a straight 2x upscale of the same art: at PPU 32 the
        /// 64 frames read 1.24x the samurai's height, which is right for a normal hit,
        /// while 128 reads 2.4x and is better saved for boss/finisher effects.
        /// </summary>
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
                    // Leave the 128px set untouched; our own recoloured sheets are already 64px.
                    if (!path.Contains("64x64") && !path.StartsWith("Assets/_Project/Art/VFX")) continue;

                    // The drawn arc does not sit in the middle of its 64x64 cell, so a plain
                    // centre pivot throws the effect away from the point it is meant to land
                    // on. Pivot on the centre of the art instead, measured across the whole
                    // sheet so every frame of the animation shares one anchor.
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

        /// <summary>
        /// Slices one texture into a left-to-right, top-to-bottom grid. Returns false when
        /// the texture is not an exact multiple of the cell size.
        /// </summary>
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

            // Read the source file directly rather than the imported texture, which is not
            // CPU-readable. This lets us drop the padding cells that grids leave behind -
            // the slash sheets are 5x2 but only 9 of the 10 cells are drawn, and a blank
            // trailing frame shows up as a hitch at the end of the effect.
            var pixels = LoadReadableCopy(assetPath);

            var rects = new List<SpriteRect>();
            int index = 0;
            int skipped = 0;

            // Texture space has its origin at the bottom-left, but sheets read top-down,
            // so walk rows in reverse to keep frame 0 as the sheet's first frame.
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

                    var spriteRect = new SpriteRect
                    {
                        name = baseName + "_" + index,
                        rect = cell,
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        spriteID = GUID.Generate()
                    };
                    rects.Add(spriteRect);
                    index++;
                }
            }

            if (pixels != null) Object.DestroyImmediate(pixels);
            if (skipped > 0)
                Debug.Log("[Onikiri] " + System.IO.Path.GetFileName(assetPath) + ": skipped " + skipped + " empty cell(s).");

            provider.SetSpriteRects(rects.ToArray());

            // Unity 2021+ keeps a name -> file id table; without it every reslice churns
            // sprite GUIDs and breaks anything already referencing these sprites.
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

        /// <summary>
        /// Normalized pivot at the centre of the drawn art, unioned across every cell in
        /// the sheet. Falls back to the cell centre if the file cannot be read.
        /// </summary>
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

        /// <summary>
        /// Decodes the PNG on disk into a throwaway readable texture. Avoids toggling
        /// isReadable on the real asset, which would force a reimport and leave the project
        /// carrying CPU copies of every sprite sheet.
        /// </summary>
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
