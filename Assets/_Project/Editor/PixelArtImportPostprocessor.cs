using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Stamps <see cref="PixelArtImportSettings"/> onto art the first time it is imported,
    /// so dropping a new pack into Assets/ThirdParty never silently lands at PPU 100 with
    /// bilinear filtering.
    ///
    /// Only first import is touched (importSettingsMissing), otherwise every reimport would
    /// wipe manual sprite slicing and pivot tweaks. Use the Onikiri menu to force a re-stamp.
    /// </summary>
    public sealed class PixelArtImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!PixelArtImportSettings.IsInScope(assetPath)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null || !importer.importSettingsMissing) return;

            PixelArtImportSettings.Apply(importer);
        }

        void OnPreprocessAsset()
        {
            if (!PixelArtImportSettings.IsInScope(assetPath)) return;

            var importer = assetImporter as AsepriteImporter;
            if (importer == null || !importer.importSettingsMissing) return;

            PixelArtImportSettings.Apply(importer);
        }
    }

    public static class PixelArtImportMenu
    {
        [MenuItem("Onikiri/Art/Reapply Pixel Art Import Settings")]
        public static void ReapplyAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D t:Sprite", PixelArtImportSettings.ScopedRoots);
            var aseGuids = AssetDatabase.FindAssets("t:Object", PixelArtImportSettings.ScopedRoots);

            int textures = 0, aseprites = 0;
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;
                    PixelArtImportSettings.Apply(importer);
                    importer.SaveAndReimport();
                    textures++;
                }

                foreach (var guid in aseGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".aseprite", System.StringComparison.OrdinalIgnoreCase) &&
                        !path.EndsWith(".ase", System.StringComparison.OrdinalIgnoreCase)) continue;
                    var importer = AssetImporter.GetAtPath(path) as AsepriteImporter;
                    if (importer == null) continue;
                    PixelArtImportSettings.Apply(importer);
                    importer.SaveAndReimport();
                    aseprites++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log(string.Format(
                "[Onikiri] Pixel art import settings reapplied: {0} textures, {1} aseprite files (PPU {2}, Point, uncompressed).",
                textures, aseprites, PixelArtImportSettings.PixelsPerUnit));
        }
    }
}
