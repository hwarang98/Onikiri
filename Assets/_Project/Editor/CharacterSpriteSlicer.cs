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

            var rects = new List<SpriteRect>();
            int index = 0;

            // Texture space has its origin at the bottom-left, but sheets read top-down,
            // so walk rows in reverse to keep frame 0 as the sheet's first frame.
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < columns; column++)
                {
                    var spriteRect = new SpriteRect
                    {
                        name = baseName + "_" + index,
                        rect = new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        spriteID = GUID.Generate()
                    };
                    rects.Add(spriteRect);
                    index++;
                }
            }

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
    }
}
