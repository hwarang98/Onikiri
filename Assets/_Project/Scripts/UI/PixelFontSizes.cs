namespace Onikiri.UI
{
    /**
     * @brief 픽셀 폰트에 허용되는 표시 크기.
     *
     * 래스터 폰트는 아틀라스를 구운 크기이거나 그 정수배일 때만 선명하다. 그 외에는
     * 비트맵을 리샘플하게 되고, 래스터 아틀라스를 선택한 이유였던 흐림이 그대로 돌아온다.
     *
     * 아틀라스는 설계 크기의 5배로 굽는다. 게임 아트가 PPU 32 + 기준 해상도 216x384라
     * 1080 폭 기기에서 Pixel Perfect Camera가 정확히 5배로 확대하기 때문이다. 폰트도 같은
     * 배율이어야 화면에 존재하는 픽셀 크기가 한 종류로 유지된다. 3배로 구우면 글자만
     * 더 잘게 계단이 지면서 해상도가 섞인 화면이 된다.
     *
     * 설계 크기 그대로 굽지 않는 이유는 별개다. 11로 래스터하면 글리프 비트맵이 TMP가
     * 쿼드를 만드는 메트릭보다 1px 작게 나와 모든 글자가 늘어난다. PixelFontAssetBuilder 참고.
     *
     * UI 코드는 숫자를 직접 적지 말고 여기서 크기를 가져다 쓸 것.
     */
    public static class PixelFontSizes
    {
        /** 화면 픽셀 배율. Pixel Perfect Camera가 1080 폭에서 쓰는 값과 같다 */
        public const int PixelScale = 5;

        /** Galmuri11 폰트가 그려진 크기 */
        public const int GalmuriDesignSize = 11;

        /** 아틀라스를 구운 크기. 선명하게 나오는 최소 표시 크기 */
        public const int GalmuriAtlasSize = GalmuriDesignSize * PixelScale;   // 55

        /** HUD와 본문 텍스트. 아틀라스와 1:1 */
        public const float GalmuriSmall = GalmuriAtlasSize;        // 55

        /** 제목용. 아틀라스 픽셀 하나가 2x2 블록이 된다 */
        public const float GalmuriLarge = GalmuriAtlasSize * 2;    // 110

        /** Thaleah 폰트가 그려진 크기. 동봉된 레거시 비트맵 폰트에서 확인 */
        public const int ThaleahDesignSize = 16;

        /** Thaleah 아틀라스를 구운 크기 */
        public const int ThaleahAtlasSize = ThaleahDesignSize * PixelScale;   // 80

        /** 데미지 팝업용. 라틴 디스플레이 서체, 아틀라스와 1:1 */
        public const float ThaleahDamage = ThaleahAtlasSize;       // 80

        /** desired 이하에서 가장 가까운 허용 크기 */
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
