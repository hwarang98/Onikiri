using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;

namespace Onikiri.Tests
{
    /**
     * @brief 스테이지는 대상 기기 전부에서 BattleArea 밴드 안에 있어야 한다.
     *
     * CropFrame.None 때문에 세로가 긴 화면일수록 월드를 더 보여준다. 이 테스트가
     * "카메라 중심을 기준으로 잡지 않는다"는 결정 전체의 회귀 방지망이다.
     */
    public class BattleLayoutTests
    {
        const int RefW = DisplayConfig.ReferenceWidth;    // 216
        const int RefH = DisplayConfig.ReferenceHeight;   // 384
        const int PPU = DisplayConfig.PixelsPerUnit;      // 32

        const float BandMin = DisplayConfig.GrowthPanelTop;  // 0.45
        const float BandMax = DisplayConfig.BattleAreaTop;   // 0.90

        // 아트에서 실측한 값. 걸을 수 있는 흙 표면은 배경 밑단에서 24px 위에 있고
        // (Ground.png 열별 높이의 최빈값이지 가장 높은 흙더미가 아니다),
        // 사무라이의 가장 큰 idle 프레임은 34px 이다
        const float GroundSurfacePixels = 24f;
        const float CharacterPixelHeight = 34f;
        const float BackgroundPixelHeight = 180f;

        // 요구사항에 명시된 세 가지 기기 비율
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
            // 1080 / 216 = 정확히 5. 세로가 긴 화면에서는 높이 기준으로 6까지 가능하므로
            // 배율을 결정하는 것은 폭이다. 월드 가로 폭이 일정한 이유가 이것이다
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
            // 배경을 넓힐 필요가 없음을 확인한다. 1080/(5*32) = 항상 6.75 units
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
            // 지면 보정값 자체를 못 박는다. 이걸 틀려도 "밴드 안에 있는가" 검사는
            // 통과한다. 캐릭터가 흙 위에 떠 있을 뿐이고 그건 눈으로만 보인다.
            // 그래서 숫자를 직접 단언한다
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

        // ---- 요구사항 본체 ----------------------------------------------------------

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
            // 사이드뷰 스테이지는 파이터 아래가 아니라 위에 여유 공간이 있어야 한다
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
            // 실제 한계를 문서화한다. 배경 아트가 180px(5.625 units)뿐이라 9:19.5
            // 이상에서는 밴드가 아트보다 높다. 그 여백은 하늘 톤으로 맞춰둔 카메라
            // 클리어 색이 덮는다
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
            // 아주 작은 에디터 게임 뷰에서도 배율이 0이나 음수가 되면 안 된다
            Assert.AreEqual(1, BattleLayout.PixelRatio(100, 100, RefW, RefH));
        }
    }
}
