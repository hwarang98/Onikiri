using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Pure geometry for placing world objects inside the UI BattleArea band.
    ///
    /// The Pixel Perfect Camera runs with CropFrame.None, so a taller phone does not get
    /// letterboxed — it simply reveals more world vertically. That means the world height
    /// the camera shows is NOT constant across devices, and anything positioned relative
    /// to the camera centre drifts out of the battle band on 9:19.5 and 9:21 screens.
    ///
    /// Everything here is therefore driven by the band's screen-space rect instead. No
    /// Unity object state is touched, so the math is directly unit-testable at any
    /// resolution without a device.
    /// </summary>
    public static class BattleLayout
    {
        /// <summary>A vertical slice of the world, in world units.</summary>
        public struct Band
        {
            public float Bottom;
            public float Top;

            public float Height { get { return Top - Bottom; } }
            public float Center { get { return (Bottom + Top) * 0.5f; } }

            public bool Contains(float y)
            {
                return y >= Bottom && y <= Top;
            }

            /// <summary>True when the whole span [lower, upper] fits inside the band.</summary>
            public bool ContainsSpan(float lower, float upper)
            {
                return lower >= Bottom && upper <= Top;
            }

            public override string ToString()
            {
                return string.Format("Band[{0:F4} .. {1:F4}] h={2:F4}", Bottom, Top, Height);
            }
        }

        /// <summary>
        /// Integer zoom the Pixel Perfect Camera settles on: the largest whole multiple of
        /// the reference resolution that still fits on screen, never below 1.
        /// </summary>
        public static int PixelRatio(int screenWidth, int screenHeight, int referenceWidth, int referenceHeight)
        {
            int horizontal = screenWidth / referenceWidth;
            int vertical = screenHeight / referenceHeight;
            return Mathf.Max(1, Mathf.Min(horizontal, vertical));
        }

        /// <summary>World-space height the camera shows at a given screen height and zoom.</summary>
        public static float CameraWorldHeight(int screenHeight, int pixelRatio, int pixelsPerUnit)
        {
            return screenHeight / (float)(pixelRatio * pixelsPerUnit);
        }

        /// <summary>Maps a screen-space Y (pixels, 0 at the bottom) to a world Y.</summary>
        public static float ScreenYToWorldY(float screenY, float screenHeight, float cameraWorldHeight, float cameraCenterY)
        {
            return cameraCenterY + (screenY / screenHeight - 0.5f) * cameraWorldHeight;
        }

        public static Band ComputeBand(
            float bottomScreenY,
            float topScreenY,
            float screenHeight,
            float cameraWorldHeight,
            float cameraCenterY)
        {
            Band band;
            band.Bottom = ScreenYToWorldY(bottomScreenY, screenHeight, cameraWorldHeight, cameraCenterY);
            band.Top = ScreenYToWorldY(topScreenY, screenHeight, cameraWorldHeight, cameraCenterY);
            return band;
        }

        /// <summary>
        /// Band derived purely from screen size and the canvas anchors — the form used by
        /// tests and by any code that needs the answer before the UI exists.
        /// </summary>
        public static Band ComputeBandFromAnchors(
            int screenWidth,
            int screenHeight,
            int referenceWidth,
            int referenceHeight,
            int pixelsPerUnit,
            float anchorMinY,
            float anchorMaxY,
            float cameraCenterY)
        {
            int ratio = PixelRatio(screenWidth, screenHeight, referenceWidth, referenceHeight);
            float worldHeight = CameraWorldHeight(screenHeight, ratio, pixelsPerUnit);
            return ComputeBand(
                anchorMinY * screenHeight,
                anchorMaxY * screenHeight,
                screenHeight,
                worldHeight,
                cameraCenterY);
        }

        /// <summary>
        /// World Y of the surface characters stand on, given a background whose bottom edge
        /// sits on the band floor and whose drawn ground surface is <paramref name="groundSurfacePixels"/>
        /// above that edge.
        /// </summary>
        public static float GroundY(Band band, float groundSurfacePixels, int pixelsPerUnit)
        {
            return band.Bottom + groundSurfacePixels / pixelsPerUnit;
        }
    }
}
