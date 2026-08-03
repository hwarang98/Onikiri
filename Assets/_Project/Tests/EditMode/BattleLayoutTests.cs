using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;

namespace Onikiri.Tests
{
    /// <summary>
    /// The stage must stay inside the BattleArea band on every phone shape we target.
    /// CropFrame.None means taller screens show MORE world, so this is the regression
    /// net for the whole "don't anchor to the camera centre" decision.
    /// </summary>
    public class BattleLayoutTests
    {
        const int RefW = DisplayConfig.ReferenceWidth;    // 216
        const int RefH = DisplayConfig.ReferenceHeight;   // 384
        const int PPU = DisplayConfig.PixelsPerUnit;      // 32

        const float BandMin = DisplayConfig.GrowthPanelTop;  // 0.45
        const float BandMax = DisplayConfig.BattleAreaTop;   // 0.90

        // Measured from the art: the walkable dirt surface sits 24px above the background's
        // bottom edge (modal column height in Ground.png, not its tallest mound), and the
        // samurai's tallest idle frame is 34px.
        const float GroundSurfacePixels = 24f;
        const float CharacterPixelHeight = 34f;
        const float BackgroundPixelHeight = 180f;

        // The three phone shapes named in the brief.
        static readonly int[][] Screens =
        {
            new[] { 1080, 1920 },   // 9:16
            new[] { 1080, 2340 },   // 9:19.5
            new[] { 1080, 2520 }    // 9:21
        };

        static BattleLayout.Band BandFor(int w, int h)
        {
            return BattleLayout.ComputeBandFromAnchors(w, h, RefW, RefH, PPU, BandMin, BandMax, 0f);
        }

        [Test]
        public void PixelRatio_IsWidthLimitedOnEveryTargetPhone()
        {
            // 1080 / 216 = 5 exactly; the height would allow 6 on tall screens, so the
            // width is what pins the zoom. This is why world width stays constant.
            foreach (var s in Screens)
            {
                Assert.AreEqual(5, BattleLayout.PixelRatio(s[0], s[1], RefW, RefH),
                    "unexpected zoom at " + s[0] + "x" + s[1]);
            }
        }

        [Test]
        public void CameraWorldHeight_GrowsWithScreenHeight()
        {
            Assert.AreEqual(12.000f, BattleLayout.CameraWorldHeight(1920, 5, PPU), 1e-4f);
            Assert.AreEqual(14.625f, BattleLayout.CameraWorldHeight(2340, 5, PPU), 1e-4f);
            Assert.AreEqual(15.750f, BattleLayout.CameraWorldHeight(2520, 5, PPU), 1e-4f);
        }

        [Test]
        public void WorldWidth_IsIdenticalOnAllThreePhones()
        {
            // Confirms the background never has to widen: 1080/(5*32) = 6.75 units always.
            foreach (var s in Screens)
            {
                int ratio = BattleLayout.PixelRatio(s[0], s[1], RefW, RefH);
                float worldWidth = s[0] / (float)(ratio * PPU);
                Assert.AreEqual(6.75f, worldWidth, 1e-4f, "at " + s[0] + "x" + s[1]);
            }
        }

        [Test]
        public void Band_MatchesHandComputedValues()
        {
            var b16 = BandFor(1080, 1920);
            Assert.AreEqual(-0.600f, b16.Bottom, 1e-4f);
            Assert.AreEqual(4.800f, b16.Top, 1e-4f);

            var b195 = BandFor(1080, 2340);
            Assert.AreEqual(-0.73125f, b195.Bottom, 1e-4f);
            Assert.AreEqual(5.85000f, b195.Top, 1e-4f);

            var b21 = BandFor(1080, 2520);
            Assert.AreEqual(-0.78750f, b21.Bottom, 1e-4f);
            Assert.AreEqual(6.30000f, b21.Top, 1e-4f);
        }

        [Test]
        public void GroundY_MatchesHandComputedValues()
        {
            // Pins the ground calibration itself. Getting this wrong does not fail any
            // "is it inside the band" check - it just floats the character above the dirt,
            // which is only visible by eye. So assert the numbers directly.
            Assert.AreEqual(0.15000f, BattleLayout.GroundY(BandFor(1080, 1920), GroundSurfacePixels, PPU), 1e-4f);
            Assert.AreEqual(0.01875f, BattleLayout.GroundY(BandFor(1080, 2340), GroundSurfacePixels, PPU), 1e-4f);
            Assert.AreEqual(-0.03750f, BattleLayout.GroundY(BandFor(1080, 2520), GroundSurfacePixels, PPU), 1e-4f);
        }

        [Test]
        public void Band_IsAlways45PercentOfScreenHeight()
        {
            foreach (var s in Screens)
            {
                var band = BandFor(s[0], s[1]);
                int ratio = BattleLayout.PixelRatio(s[0], s[1], RefW, RefH);
                float expected = (BandMax - BandMin) * BattleLayout.CameraWorldHeight(s[1], ratio, PPU);
                Assert.AreEqual(expected, band.Height, 1e-4f, "at " + s[0] + "x" + s[1]);
            }
        }

        // ---- the actual requirement -------------------------------------------------

        [Test]
        public void GroundLine_IsInsideBandOnEveryPhone()
        {
            foreach (var s in Screens)
            {
                var band = BandFor(s[0], s[1]);
                float ground = BattleLayout.GroundY(band, GroundSurfacePixels, PPU);
                Assert.IsTrue(band.Contains(ground),
                    "ground " + ground + " escaped " + band + " at " + s[0] + "x" + s[1]);
            }
        }

        [Test]
        public void StandingCharacter_FitsEntirelyInsideBandOnEveryPhone()
        {
            foreach (var s in Screens)
            {
                var band = BandFor(s[0], s[1]);
                float feet = BattleLayout.GroundY(band, GroundSurfacePixels, PPU);
                float head = feet + CharacterPixelHeight / PPU;

                Assert.IsTrue(band.ContainsSpan(feet, head),
                    "character " + feet + ".." + head + " escaped " + band + " at " + s[0] + "x" + s[1]);
            }
        }

        [Test]
        public void GroundLine_SitsInLowerPortionOfBand()
        {
            // A side-view stage wants headroom above the fighters, not below them.
            foreach (var s in Screens)
            {
                var band = BandFor(s[0], s[1]);
                float ground = BattleLayout.GroundY(band, GroundSurfacePixels, PPU);
                float fraction = (ground - band.Bottom) / band.Height;
                Assert.Less(fraction, 0.25f, "ground too high in band at " + s[0] + "x" + s[1]);
            }
        }

        [Test]
        public void TallPhones_ExposeSkyAboveTheBackground()
        {
            // Documents a real limitation: the background art is only 180px (5.625 units)
            // tall, so on 9:19.5 and taller the band is taller than the art. That gap is
            // covered by the camera clear colour, which is set to the sky tone.
            var b16 = BandFor(1080, 1920);
            float top16 = b16.Bottom + BackgroundPixelHeight / PPU;
            Assert.GreaterOrEqual(top16, b16.Top, "9:16 should be fully covered by the art");

            foreach (var s in new[] { new[] { 1080, 2340 }, new[] { 1080, 2520 } })
            {
                var band = BandFor(s[0], s[1]);
                float backgroundTop = band.Bottom + BackgroundPixelHeight / PPU;
                Assert.Less(backgroundTop, band.Top,
                    "expected a sky gap at " + s[0] + "x" + s[1] + " - if this fails the art grew and the note is stale");
            }
        }

        [Test]
        public void ScreenYToWorldY_MapsCentreAndEdges()
        {
            const float worldHeight = 12f;
            Assert.AreEqual(0f, BattleLayout.ScreenYToWorldY(960f, 1920f, worldHeight, 0f), 1e-4f);
            Assert.AreEqual(-6f, BattleLayout.ScreenYToWorldY(0f, 1920f, worldHeight, 0f), 1e-4f);
            Assert.AreEqual(6f, BattleLayout.ScreenYToWorldY(1920f, 1920f, worldHeight, 0f), 1e-4f);
        }

        [Test]
        public void ScreenYToWorldY_RespectsCameraOffset()
        {
            Assert.AreEqual(3f, BattleLayout.ScreenYToWorldY(960f, 1920f, 12f, 3f), 1e-4f);
        }

        [Test]
        public void PixelRatio_NeverDropsBelowOne()
        {
            // A tiny editor game view must not produce a zero or negative zoom.
            Assert.AreEqual(1, BattleLayout.PixelRatio(100, 100, RefW, RefH));
        }
    }
}
