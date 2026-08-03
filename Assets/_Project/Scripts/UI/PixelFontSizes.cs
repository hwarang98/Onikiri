namespace Onikiri.UI
{
    /// <summary>
    /// Legal display sizes for the pixel fonts.
    ///
    /// A raster font is crisp only at the size its atlas was rasterised at, or a whole
    /// multiple of it. Anything else resamples the bitmap and brings back exactly the
    /// blurring the raster atlas was chosen to avoid.
    ///
    /// Galmuri11 is an 11px face, but the atlas is baked at 33 rather than 11: rasterising
    /// the outline at 11 produces bitmaps one pixel shorter than the metrics TMP builds its
    /// quads from, so every glyph is stretched and the letterforms visibly break. See
    /// PixelFontAssetBuilder for the measurements.
    ///
    /// The practical consequence: usable sizes are multiples of 33, NOT of 11. 44 would be
    /// a 1.33x resample of the atlas and would look soft.
    ///
    /// UI code should take sizes from here rather than typing numbers.
    /// </summary>
    public static class PixelFontSizes
    {
        /// <summary>Size the Galmuri11 face was drawn at.</summary>
        public const int GalmuriDesignSize = 11;

        /// <summary>Size the atlas is rasterised at; the smallest crisp display size.</summary>
        public const int GalmuriAtlasSize = 33;

        /// <summary>HUD and body text, 1:1 with the atlas.</summary>
        public const float GalmuriSmall = GalmuriAtlasSize;        // 33

        /// <summary>Headings. Each atlas pixel becomes a 2x2 block.</summary>
        public const float GalmuriLarge = GalmuriAtlasSize * 2;    // 66

        /// <summary>Size the Thaleah face was drawn at (from its legacy bitmap font).</summary>
        public const int ThaleahDesignSize = 16;

        /// <summary>Size the Thaleah atlas is rasterised at.</summary>
        public const int ThaleahAtlasSize = 48;

        /// <summary>Damage popups. Latin display face, 1:1 with its atlas.</summary>
        public const float ThaleahDamage = ThaleahAtlasSize;       // 48

        /// <summary>Nearest legal size at or below <paramref name="desired"/>.</summary>
        public static float SnapToMultiple(float desired, int baseSize)
        {
            if (baseSize <= 0) return desired;
            int multiple = (int)(desired / baseSize);
            if (multiple < 1) multiple = 1;
            return multiple * baseSize;
        }

        public static bool IsLegal(float size, int baseSize)
        {
            if (baseSize <= 0) return false;
            float multiple = size / baseSize;
            return UnityEngine.Mathf.Approximately(multiple, UnityEngine.Mathf.Round(multiple));
        }
    }
}
