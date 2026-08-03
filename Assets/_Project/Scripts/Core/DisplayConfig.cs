namespace Onikiri.Core
{
    /// <summary>
    /// Single source of truth for the portrait display standard.
    /// Referenced by the pixel-art importer, the Pixel Perfect Camera and the UI canvas
    /// so these numbers can never drift apart.
    /// </summary>
    public static class DisplayConfig
    {
        /// <summary>Design resolution the UI is authored against (9:16).</summary>
        public const int DesignWidth = 1080;
        public const int DesignHeight = 1920;

        /// <summary>
        /// Unified pixels-per-unit for every art pack. The packs ship different canvas
        /// sizes (96x96 samurai, 92x92 enemies, 184x184 boss) but the drawn art inside
        /// is all ~32-46px tall, so one PPU lines them all up without rescaling.
        /// Changing this requires reimporting all art.
        /// </summary>
        public const int PixelsPerUnit = 32;

        /// <summary>
        /// Pixel Perfect Camera reference resolution. 216x384 is an exact 5x integer
        /// upscale of 1080x1920, which keeps the pixel grid clean and puts a ~34px
        /// character at roughly 20% of the battle area's height.
        /// </summary>
        public const int ReferenceWidth = 216;
        public const int ReferenceHeight = 384;

        /// <summary>Vertical screen split from the handoff spec, bottom-up in canvas space.</summary>
        public const float BottomTabBarTop = 0.10f;
        public const float GrowthPanelTop = 0.45f;
        public const float BattleAreaTop = 0.90f;

        /// <summary>World-space height of the camera view, in units.</summary>
        public const float CameraWorldHeight = (float)ReferenceHeight / PixelsPerUnit; // 12 units
    }
}
