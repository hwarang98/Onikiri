using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Bakes TMP font assets for pixel fonts.
    ///
    /// TMP defaults to signed distance field rendering, which is exactly wrong here. SDF
    /// reconstructs glyph edges analytically and anti-aliases them, so an 11px bitmap face
    /// comes out soft and blurred - the letters stop matching the pixel art around them.
    /// The settings that matter:
    ///
    ///   sampling point size = the font's design size (11 for Galmuri11), never "auto"
    ///   padding             = 0        (padding only exists to give SDF spread room)
    ///   render mode         = RASTER_HINTED (1-bit coverage, hinted to the pixel grid)
    ///   atlas filter        = Point    (bilinear would smear it again at draw time)
    ///   material shader     = TextMeshPro/Bitmap, not any Distance Field variant
    ///
    /// Displayed sizes must then be integer multiples of the design size, or the glyph grid
    /// lands between screen pixels and the crispness is lost anyway. See
    /// <see cref="Onikiri.UI.PixelFontSizes"/>.
    /// </summary>
    public static class PixelFontAssetBuilder
    {
        private const string FontFolder = "Assets/_Project/Art/Fonts";
        private const string OutputFolder = "Assets/_Project/Art/Fonts";

        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        /// <summary>
        /// One transparent pixel of margin around each glyph.
        ///
        /// Zero padding is the intuitive choice for a raster atlas - there is no distance
        /// field that needs spread room - but TMP derives glyph quad size from the font's
        /// metrics while sampling the packed rect, and at padding 0 those disagree by one
        /// pixel (a glyph with 10x11 metrics packs into a 9x10 rect). Every glyph then gets
        /// stretched by ~10/9, and with point sampling that duplicates and drops pixel rows,
        /// which reads as broken, overlapping letterforms.
        ///
        /// Padding 1 makes rect = metrics + 2 and the mapping exact. It costs a pixel of
        /// transparent margin per glyph and softens nothing.
        /// </summary>
        private const int AtlasPadding = 1;

        /// <summary>Size the Galmuri11 face was drawn at.</summary>
        public const int GalmuriDesignSize = 11;

        /// <summary>
        /// Integer multiple the atlas is rasterised at. Display sizes must be this multiple
        /// (33) or a further multiple of the design size.
        /// </summary>
        public const int GalmuriSampleMultiple = 3;

        [MenuItem("Onikiri/Art/Build Pixel Font Assets")]
        public static void BuildAll()
        {
            FontCharsetBuilder.Rebuild();
            var charset = FontCharsetBuilder.LoadCharset();

            // Sampled at 3x the 11px design size, not at 11.
            //
            // Rasterising the outline at exactly 11 produces glyph bitmaps one pixel
            // shorter than the metrics TMP builds quads from, so every glyph gets stretched
            // by 11/10 and point sampling duplicates rows - the letterforms visibly break.
            // A pixel font's outlines are axis-aligned rectangles, so rasterising at an
            // integer multiple yields exact NxN blocks and the rounding error becomes
            // negligible. Display size then matches the sampling size 1:1.
            Build("Galmuri11", GalmuriDesignSize * GalmuriSampleMultiple, charset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Bakes one font. <paramref name="designPointSize"/> must be the size the face was
        /// drawn at - Galmuri11 is an 11px face, so sampling at anything else resamples the
        /// bitmap and destroys it.
        /// </summary>
        public static TMP_FontAsset Build(string fontName, int designPointSize, string charset)
        {
            string sourcePath = FontFolder + "/" + fontName + ".ttf";
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                Debug.LogError("[Onikiri] Font not found: " + sourcePath);
                return null;
            }

            string outputPath = OutputFolder + "/" + fontName + " SDF.asset";
            // Named "<font> SDF" only because that is the convention TMP tooling expects;
            // the contents are a raster atlas.

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                designPointSize,
                AtlasPadding,
                GlyphRenderMode.RASTER_HINTED,
                AtlasWidth, AtlasHeight,
                AtlasPopulationMode.Dynamic,         // dynamic while we add glyphs
                false);

            if (fontAsset == null)
            {
                Debug.LogError("[Onikiri] CreateFontAsset failed for " + fontName);
                return null;
            }

            fontAsset.name = fontName + " SDF";

            string missing;
            bool allAdded = fontAsset.TryAddCharacters(charset, out missing);
            if (!allAdded && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("[Onikiri] " + fontName + " is missing glyphs for: " + missing);

            // Freeze it: a static asset will not silently rasterise new glyphs at runtime,
            // which would bypass every setting above.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            ApplyPointFiltering(fontAsset);
            ApplyBitmapShader(fontAsset);

            SaveWithSubAssets(fontAsset, outputPath);

            int glyphs = fontAsset.glyphTable != null ? fontAsset.glyphTable.Count : 0;
            int pages = fontAsset.atlasTextures != null ? fontAsset.atlasTextures.Length : 0;
            Debug.Log(string.Format(
                "[Onikiri] Font '{0}': {1} glyphs, {2} atlas page(s) at {3}pt, RASTER_HINTED, padding 0 -> {4}",
                fontAsset.name, glyphs, pages, designPointSize, outputPath));

            return fontAsset;
        }

        private static void ApplyPointFiltering(TMP_FontAsset fontAsset)
        {
            if (fontAsset.atlasTextures == null) return;

            foreach (var texture in fontAsset.atlasTextures)
            {
                if (texture == null) continue;
                texture.filterMode = FilterMode.Point;
                texture.anisoLevel = 0;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
        }

        private static void ApplyBitmapShader(TMP_FontAsset fontAsset)
        {
            var shader = Shader.Find("TextMeshPro/Bitmap");
            if (shader == null)
            {
                Debug.LogWarning("[Onikiri] TextMeshPro/Bitmap shader not found; leaving default material.");
                return;
            }

            // Swap the shader on the material TMP already built, rather than creating a
            // fresh one. A new Material starts with default values for _TextureWidth /
            // _TextureHeight / _GradientScale, and TMP computes glyph UVs from those - get
            // them wrong and glyphs render displaced with neighbouring glyphs bleeding in.
            var material = fontAsset.material;
            if (material == null)
            {
                material = new Material(shader);
                fontAsset.material = material;
            }
            else
            {
                material.shader = shader;
            }

            material.name = fontAsset.name + " Material";

            // Property names, not ShaderUtilities.ID_*: those cached IDs are populated
            // lazily and are still 0 here, so SetFloat(0, ...) silently writes nothing and
            // the atlas dimensions stay zero - which is exactly what mangles the glyphs.
            material.SetTexture("_MainTex", fontAsset.atlasTexture);
            material.SetFloat("_TextureWidth", fontAsset.atlasWidth);
            material.SetFloat("_TextureHeight", fontAsset.atlasHeight);
            material.SetFloat("_GradientScale", fontAsset.atlasPadding + 1);
        }

        /// <summary>
        /// Writes the font asset with its atlas texture and material nested inside it, so
        /// the whole font is one file to move or delete.
        /// </summary>
        private static void SaveWithSubAssets(TMP_FontAsset fontAsset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(fontAsset, path);

            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var texture = fontAsset.atlasTextures[i];
                    if (texture == null) continue;
                    texture.name = fontAsset.name + " Atlas" + (i > 0 ? " " + i : string.Empty);
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
