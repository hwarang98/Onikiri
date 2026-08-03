using Onikiri.Core;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Project-wide pixel-art import standard.
    ///
    /// Every art pack we imported ships at a different canvas size (samurai 96x96,
    /// enemies 92x92, boss 184x184, slashes 64/128), but the *drawn* art inside those
    /// canvases is all at roughly the same scale (34px samurai vs 32-46px enemies).
    /// So a single PPU makes every pack line up on screen with no per-pack rescaling.
    ///
    /// PPU 32 pairs with a 216x384 Pixel Perfect reference resolution, which is an
    /// exact 5x integer upscale of the 1080x1920 design resolution.
    /// </summary>
    public static class PixelArtImportSettings
    {
        public const int PixelsPerUnit = DisplayConfig.PixelsPerUnit;

        /// <summary>Roots this standard applies to.</summary>
        public static readonly string[] ScopedRoots =
        {
            "Assets/ThirdParty/",
            "Assets/_Project/Art/"
        };

        public static bool IsInScope(string assetPath)
        {
            foreach (var root in ScopedRoots)
            {
                if (assetPath.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Side-view characters pivot at their feet so a 92px and a 184px frame still
        /// stand on the same ground line. Everything else pivots at center.
        /// </summary>
        public static SpriteAlignment PivotFor(string assetPath)
        {
            if (assetPath.Contains("/Characters/") || assetPath.Contains("/Enemies/"))
                return SpriteAlignment.BottomCenter;
            return SpriteAlignment.Center;
        }

        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;

            // Never demote an already-sliced sheet back to a single sprite. Doing so
            // deletes every SpriteRect, which silently breaks every prefab and animation
            // clip referencing those sprites. Only sheets that have never been sliced get
            // the Single default.
            bool alreadySliced = importer.spriteImportMode == SpriteImportMode.Multiple;
            if (!alreadySliced) importer.spriteImportMode = SpriteImportMode.Single;

            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Sheets run up to ~1560px wide; the 2048 default would silently downscale
            // anything larger, which destroys pixel alignment.
            importer.maxTextureSize = 4096;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // On a sliced sheet the pivot lives per-SpriteRect, and those were authored by
            // the slicer against measured art. Leave them alone.
            if (!alreadySliced) settings.spriteAlignment = (int)PivotFor(importer.assetPath);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            // Block-compression on mobile smears pixel art, so force uncompressed RGBA.
            ApplyPlatform(importer, "Android");
            ApplyPlatform(importer, "iPhone");
        }

        static void ApplyPlatform(TextureImporter importer, string platform)
        {
            var ps = importer.GetPlatformTextureSettings(platform);
            ps.overridden = true;
            ps.format = TextureImporterFormat.RGBA32;
            ps.maxTextureSize = 4096;
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(ps);
        }

        /// <summary>
        /// Height of the lowest drawn pixel above the bottom of the Feudal Japan enemy
        /// canvas (92px). Measured across the hover cycle, which only bobs by 1px.
        /// Without this the enemies float 20px (0.625 units) above the ground, because the
        /// importer pivots on the canvas edge rather than the art.
        /// </summary>
        public const float EnemyArtBottomPixels = 20f;
        public const float EnemyCanvasPixels = 92f;

        public static void Apply(AsepriteImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.spriteMeshType = SpriteMeshType.FullRect;
            importer.spriteExtrude = 0;

            if (importer.assetPath.Contains("/Enemies/"))
            {
                // Canvas space keeps every trimmed frame aligned to the original 92x92
                // artboard, so one pivot value holds for the whole animation.
                importer.pivotSpace = PivotSpaces.Canvas;
                importer.pivotAlignment = SpriteAlignment.Custom;
                importer.customPivotPosition =
                    new Vector2(0.5f, EnemyArtBottomPixels / EnemyCanvasPixels);
            }
            else
            {
                importer.pivotAlignment = PivotFor(importer.assetPath);
            }
            // Aseprite frame tags become animation clips, which is why we import the
            // .aseprite sources for enemies instead of the untagged loose PNG frames.
            importer.generateAnimationClips = true;
            importer.generateModelPrefab = false;
        }
    }
}
