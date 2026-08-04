using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 아트가 처음 임포트될 때 PixelArtImportSettings를 찍어준다.
     *
     * Assets/ThirdParty에 새 팩을 넣어도 조용히 PPU 100 + 이중선형 필터로 들어오는
     * 일이 없게 한다.
     *
     * 첫 임포트(importSettingsMissing)에만 손댄다. 아니면 재임포트마다 수동 슬라이싱과
     * 피벗 조정이 날아간다. 강제로 다시 찍으려면 Onikiri 메뉴를 쓸 것.
     */
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
