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

        /**
         * @brief 화면 세로 분할. 캔버스 기준 아래에서 위 방향.
         *
         * 탭바는 사양서의 10%에서 7.5%로 줄었다(41단계). 192px 탭은 아이콘
         * 64 + 캡션을 넣고도 위아래가 남는 두께였고, 매 화면에 상주하는
         * 띠가 두꺼울수록 그만큼 목록이 짧아진다. 줄인 몫은 전부 성장
         * 패널이 가져간다 - 전투 영역(0.45~0.90)은 카메라 밴드 산식과
         * 물려 있어 건드리지 않는다.
         *
         * 밴드 앵커는 씬에 저장되므로 이 값을 바꾸면 Build Combat Content가
         * MainSceneBuilder.ReassertBands로 씬을 따라오게 한다.
         */
        public const float BottomTabBarTop = 0.075f;
        public const float GrowthPanelTop = 0.45f;
        public const float BattleAreaTop = 0.90f;

        /** 카메라가 보여주는 월드 높이 (units) */
        public const float CameraWorldHeight = (float)ReferenceHeight / PixelsPerUnit; // 12 units

        /**
         * @brief UI 층위 (#2 · #1 팝업화).
         *
         * ## 왜 형제 순서로는 안 되는가
         *
         * 그전까지 그리기 순서는 전부 형제 순서였다. 그것이 무너진 것이
         * 경험치 줄을 공용 층으로 올릴 때다 - 줄은 전투 콘텐츠 빌더가 세우는데
         * 화면 판들(스킬·퀘스트·장비·랭킹...)은 각자의 빌더가 **나중에**
         * 붙이므로, 세우는 순간 맨 뒤였던 것이 빌드가 끝나면 중간에 파묻힌다.
         * 빌더 실행 순서에 그림이 의존하는 구조다.
         *
         * 그래서 층위를 값으로 적는다. Canvas.overrideSorting을 켠 오브젝트는
         * 형제 순서와 무관하게 이 수로 정렬되고, 그 수는 빌더가 언제 도는지와
         * 상관없다.
         *
         *   0   기본 - 밴드 판들(성장 패널, 하단 탭 화면들)
         *   10  상시 HUD - 경험치 줄, 레벨업 버튼. 어느 화면 위에도 보인다
         *   20  팝업 - 딤 배경을 깔고 화면을 가로막는 것들(설정·랭킹·뽑기·
         *       스킬 정보·오프라인 보상). HUD보다 위다: 팝업이 떠 있는 동안
         *       경험치 줄이 그 위에 그려지면 딤이 뚫린 것으로 읽힌다
         */
        public const int SortingHud = 10;
        public const int SortingPopup = 20;
    }
}
