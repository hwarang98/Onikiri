namespace Onikiri.UI
{
    /**
     * @brief 데미지 팝업의 강조 단계.
     *
     * 평타는 의도적으로 작고 흐리게 둔다. 방치형 화면에는 초당 수십 개의 숫자가
     * 지나가므로 전부 크게 만들면 아무것도 강조되지 않는다. 대비가 강조를 만든다.
     *
     * 크기는 두 단계뿐이고 둘 다 아틀라스 크기의 정수배다(80 / 160). 그 사이 값은
     * 비트맵을 리샘플하게 되어 픽셀이 흐려진다. PixelFontSizes 참고.
     */
    public enum DamageStyle
    {
        Normal,
        Critical,
        Kill
    }
}
