using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief UI BattleArea 밴드 안에 월드 오브젝트를 배치하기 위한 순수 기하 계산.
     *
     * Pixel Perfect Camera가 CropFrame.None으로 동작하므로 세로가 긴 기기는 레터박스
     * 대신 월드를 더 보여준다. 즉 카메라가 보여주는 월드 높이는 기기마다 다르고,
     * 카메라 중심을 기준으로 배치한 것은 9:19.5 / 9:21 화면에서 전투 밴드를 벗어난다.
     *
     * 그래서 여기서는 전부 밴드의 화면 좌표 rect를 기준으로 계산한다. Unity 오브젝트
     * 상태를 건드리지 않으므로, 기기 없이 임의 해상도로 바로 단위 테스트할 수 있다.
     */
    public static class BattleLayout
    {
        /** 월드의 세로 구간 (world units) */
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

            /** [lower, upper] 구간 전체가 밴드 안에 들어가면 true */
            public bool ContainsSpan(float lower, float upper)
            {
                return lower >= Bottom && upper <= Top;
            }

            public override string ToString()
            {
                return string.Format("Band[{0:F4} .. {1:F4}] h={2:F4}", Bottom, Top, Height);
            }
        }

        /**
         * @brief Pixel Perfect Camera가 결정하는 정수 배율.
         *
         * 화면에 들어가는 가장 큰 기준 해상도의 정수배이며 1 아래로는 내려가지 않는다.
         */
        public static int PixelRatio(int screenWidth, int screenHeight, int referenceWidth, int referenceHeight)
        {
            int horizontal = screenWidth / referenceWidth;
            int vertical = screenHeight / referenceHeight;
            return Mathf.Max(1, Mathf.Min(horizontal, vertical));
        }

        /** 주어진 화면 높이와 배율에서 카메라가 보여주는 월드 높이 */
        public static float CameraWorldHeight(int screenHeight, int pixelRatio, int pixelsPerUnit)
        {
            return screenHeight / (float)(pixelRatio * pixelsPerUnit);
        }

        /** 화면 좌표 Y(픽셀, 아래가 0)를 월드 Y로 변환 */
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

        /**
         * @brief 화면 크기와 캔버스 앵커만으로 유도한 밴드.
         *
         * 테스트와, UI가 존재하기 전에 답이 필요한 코드가 쓰는 형태다.
         */
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

        /**
         * @brief 캐릭터가 서는 지면의 월드 Y.
         *
         * 배경의 밑단이 밴드 바닥에 놓이고, 그려진 지면 표면이 그 밑단에서
         * groundSurfacePixels 만큼 위에 있다는 전제로 계산한다.
         */
        public static float GroundY(Band band, float groundSurfacePixels, int pixelsPerUnit)
        {
            return band.Bottom + groundSurfacePixels / pixelsPerUnit;
        }
    }
}
