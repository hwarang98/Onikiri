namespace Onikiri.Core
{
    /**
     * @brief 세로 화면 표시 규격의 단일 출처.
     *
     * 픽셀 아트 임포터, Pixel Perfect Camera, UI 캔버스가 모두 여기를 참조한다.
     * 값이 서로 어긋나는 것을 막는 것이 목적이다.
     */
    public static class DisplayConfig
    {
        /** UI를 그리는 기준 해상도 (9:16) */
        public const int DesignWidth = 1080;
        public const int DesignHeight = 1920;

        /**
         * @brief 모든 아트 팩에 공통 적용하는 PPU.
         *
         * 팩마다 캔버스 크기는 다르지만(사무라이 96x96, 적 92x92, 보스 184x184)
         * 그 안에 그려진 아트는 전부 32~46px 수준이라 단일 PPU로 크기가 맞는다.
         * 이 값을 바꾸면 전체 아트를 재임포트해야 한다.
         */
        public const int PixelsPerUnit = 32;

        /**
         * @brief Pixel Perfect Camera 기준 해상도.
         *
         * 216x384는 1080x1920의 정확히 5배 정수배라 픽셀 격자가 깨지지 않고,
         * 34px 캐릭터가 전투 영역 높이의 약 20%를 차지한다.
         */
        public const int ReferenceWidth = 216;
        public const int ReferenceHeight = 384;

        /** 사양서의 화면 세로 분할. 캔버스 기준 아래에서 위 방향 */
        public const float BottomTabBarTop = 0.10f;
        public const float GrowthPanelTop = 0.45f;
        public const float BattleAreaTop = 0.90f;

        /** 카메라가 보여주는 월드 높이 (units) */
        public const float CameraWorldHeight = (float)ReferenceHeight / PixelsPerUnit; // 12 units
    }
}
