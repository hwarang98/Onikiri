namespace Onikiri.UI
{
    /**
     * @brief 픽셀 폰트에 허용되는 표시 크기.
     *
     * 래스터 폰트는 아틀라스를 구운 크기이거나 그 정수배일 때만 선명하다. 그 외에는
     * 비트맵을 리샘플하게 되고, 래스터 아틀라스를 선택한 이유였던 흐림이 그대로 돌아온다.
     *
     * Galmuri11은 11px 폰트지만 아틀라스는 11이 아니라 33으로 굽는다. 외곽선을 11로
     * 래스터하면 TMP가 쿼드를 만드는 메트릭보다 1px 작은 비트맵이 나와서, 모든 글리프가
     * 늘어나고 글자 형태가 눈에 띄게 깨진다. 측정값은 PixelFontAssetBuilder 참고.
     *
     * 실질적인 결론: 쓸 수 있는 크기는 11의 배수가 아니라 33의 배수다. 44는 아틀라스를
     * 1.33배 리샘플하는 것이라 흐려 보인다.
     *
     * UI 코드는 숫자를 직접 적지 말고 여기서 크기를 가져다 쓸 것.
     */
    public static class PixelFontSizes
    {
        /** Galmuri11 폰트가 그려진 크기 */
        public const int GalmuriDesignSize = 11;

        /** 아틀라스를 구운 크기. 선명하게 나오는 최소 표시 크기 */
        public const int GalmuriAtlasSize = 33;

        /** HUD와 본문 텍스트. 아틀라스와 1:1 */
        public const float GalmuriSmall = GalmuriAtlasSize;        // 33

        /** 제목용. 아틀라스 픽셀 하나가 2x2 블록이 된다 */
        public const float GalmuriLarge = GalmuriAtlasSize * 2;    // 66

        /** Thaleah 폰트가 그려진 크기. 동봉된 레거시 비트맵 폰트에서 확인 */
        public const int ThaleahDesignSize = 16;

        /** Thaleah 아틀라스를 구운 크기 */
        public const int ThaleahAtlasSize = 48;

        /** 데미지 팝업용. 라틴 디스플레이 서체, 아틀라스와 1:1 */
        public const float ThaleahDamage = ThaleahAtlasSize;       // 48

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
