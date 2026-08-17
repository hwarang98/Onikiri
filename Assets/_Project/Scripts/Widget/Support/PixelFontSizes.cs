namespace Onikiri.UI
{
    /**
     * @brief 픽셀 폰트에 허용되는 표시 크기.
     *
     * 래스터 폰트는 아틀라스를 구운 크기이거나 그 정수배일 때만 선명하다. 그 외에는
     * 비트맵을 리샘플하게 되고, 래스터 아틀라스를 선택한 이유였던 흐림이 그대로 돌아온다.
     *
     * **아틀라스를 굽는 쪽(PixelFontAssetBuilder)이 여기 값을 읽는다.** 두 곳에 배수를
     * 따로 적어두면 "55pt로 굽고 44pt로 그리는" 상태가 조용히 만들어지고, 그것은
     * 에러가 아니라 흐릿한 글자로만 나타난다. 크기를 바꾸려면 이 파일만 고치고
     * 폰트를 다시 구우면 된다.
     *
     * ## 왜 아트 배율(5)보다 작은가
     *
     * 22단계까지 두 폰트 모두 아트와 같은 5배로 구웠다. 화면에 존재하는 픽셀 크기를
     * 한 종류로 유지하려는 것이었고, 그 자체는 맞는 목표다. 그런데 결과가 너무 컸다 -
     * 1080 폭에서 본문 한 줄에 한글 19자밖에 들어가지 않아 강화 행의 값이 잘렸고,
     * 데미지 팝업(80pt, 치명타는 160pt)은 전투 영역을 통째로 덮었다.
     *
     * 그래서 배율을 낮췄다. 글자의 픽셀 블록이 아트보다 작아지는 것은 감수한다 -
     * **읽히지 않는 글자보다 한 단계 고운 글자가 낫다.** 정수배는 그대로 지키므로
     * 글리프 자체는 여전히 선명하고, 계단의 크기만 아트보다 한 칸 작아진다.
     *
     * 두 폰트의 배율이 다른 것도 같은 이유다. Thaleah의 설계 크기(16)는 Galmuri(11)의
     * 1.45배라, 같은 배수로 구우면 데미지 숫자가 UI 본문보다 45% 크게 나온다. 데미지
     * 팝업은 전체 자릿수 표기(NumberFormatter.FormatFull)로 바뀌면서 가장 길어진
     * 문자열이기도 해서, 가장 많이 줄여야 하는 쪽이 가장 크게 나오고 있었다.
     *
     * UI 코드는 숫자를 직접 적지 말고 여기서 크기를 가져다 쓸 것.
     */
    public static class PixelFontSizes
    {
        /**
         * @brief 아트의 화면 픽셀 배율. Pixel Perfect Camera가 1080 폭에서 쓰는 값이다.
         *
         * 폰트는 더 이상 이 값을 쓰지 않는다(위 주석 참고). 그림자 오프셋처럼
         * "아트 픽셀 하나"를 뜻하는 자리에만 남는다.
         */
        public const int ArtPixelScale = 5;

        /** Galmuri11 폰트가 그려진 크기 */
        public const int GalmuriDesignSize = 11;

        /** Galmuri 아틀라스를 굽는 정수 배수 */
        public const int GalmuriScale = 4;

        /** 아틀라스를 구운 크기. 선명하게 나오는 최소 표시 크기 */
        public const int GalmuriAtlasSize = GalmuriDesignSize * GalmuriScale;   // 44

        /** HUD와 본문 텍스트. 아틀라스와 1:1 */
        public const float GalmuriSmall = GalmuriAtlasSize;        // 44

        /** 제목용. 아틀라스 픽셀 하나가 2x2 블록이 된다 */
        public const float GalmuriLarge = GalmuriAtlasSize * 2;    // 88

        /**
         * @brief 보조 정보용 캡션 크기 (38단계 폰트 위계).
         *
         * 38단계까지 UI 전체가 44pt 단일 크기였다(씬 실측 44pt x311 / 88pt x5).
         * 크기가 하나면 위계가 없고, 위계가 없으면 모든 글자가 소리를 지른다 -
         * "올드해 보인다"의 뿌리가 이것이었다.
         *
         * 33 = 11 x 3. 별도 아틀라스("Galmuri11 Caption SDF")를 3배로 구워
         * 1:1로 그린다 - 44 아틀라스를 0.75배로 그리면 Point 샘플링이 행을
         * 버려 글자가 깨진다. 같은 서체의 다른 배수라 형태는 44와 동일하다.
         *
         * 쓰는 곳: 항목 라벨·레벨·비용·전후값·탭 라벨·배지. 큰 것(재화·헤더·
         * 핵심 수치·데미지)은 44/48/88에 남는다.
         */
        public const int GalmuriCaptionScale = 3;
        public const int GalmuriCaptionSize = GalmuriDesignSize * GalmuriCaptionScale;  // 33
        public const float GalmuriCaption = GalmuriCaptionSize;    // 33

        /** Thaleah 폰트가 그려진 크기. 동봉된 레거시 비트맵 폰트에서 확인 */
        public const int ThaleahDesignSize = 16;

        /**
         * @brief Thaleah 아틀라스를 굽는 정수 배수.
         *
         * 3이었다가 2로 내렸다. 데미지 표기에서 자릿수 상한을 없애면서
         * (NumberFormatter.FormatFull) 숫자가 길어졌기 때문이다 - 10^16대 타격은
         * 17자리 + 쉼표 5개 = 22글자이고, 치명타는 그 두 배 크기로 뜬다.
         *
         * **비트맵 서체라 크기를 줄이려면 다시 굽는 수밖에 없다.** fontSize만
         * 낮추면 아틀라스와 1:1이 깨져 글자가 흐려진다 - 이 프로젝트가 폰트를
         * 정수배로만 쓰는 이유 그대로다. 그래서 상수를 내리고
         * `Onikiri/Art/Build Pixel Font Assets`로 아틀라스를 다시 굽는다.
         *
         * Thaleah는 데미지 팝업 전용이라(SkillNameFlash 주석 - 한글이 없어 다른
         * 곳에 못 쓴다) 이 값을 내려도 다른 화면은 안 움직인다.
         */
        public const int ThaleahScale = 2;

        /** Thaleah 아틀라스를 구운 크기 */
        public const int ThaleahAtlasSize = ThaleahDesignSize * ThaleahScale;   // 32

        /** 데미지 팝업용. 라틴 디스플레이 서체, 아틀라스와 1:1 */
        public const float ThaleahDamage = ThaleahAtlasSize;       // 32

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
