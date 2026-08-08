using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 화면의 판때기와 색을 한 곳에서 정한다.
     *
     * 13단계까지 UI는 단색 사각형이었다. 색상값이 빌더 세 곳에 흩어져 있었고
     * (`RowColor`, `PanelColor`, `PopupBoxColor`가 전부 같은 #3A3550이다),
     * 하나를 바꾸려면 셋을 찾아야 했다.
     *
     * ## 왜 흰 판을 물들이는가
     *
     * Kenney 팩에는 갈색/황갈색 세트가 있고 그대로 쓰면 나무 질감이 된다. 그런데
     * 이 게임의 화면은 자주색 밤(#3A3550)에 크림색 글자(#F6E5BF)이고, 배경은
     * 벚꽃과 토리이다. 따뜻한 갈색 패널을 얹으면 **배경과 UI가 서로 다른 게임처럼**
     * 보인다.
     *
     * 그래서 중립적인 흰 판(Ancient/white)을 가져와 지금 쓰는 색으로 물들인다.
     * 얻는 것은 색이 아니라 **테두리와 모서리 장식, 그리고 눌림 상태**다.
     * 단색 사각형에는 그 셋이 전부 없었다.
     *
     * 나무 질감으로 바꾸고 싶으면 {@link PanelSet}과 {@link PanelName}만 갈면 된다.
     */
    public static class UiSkin
    {
        // ---------------------------------------------------------------- 팩

        /**
         * @brief 쓸 세트.
         *
         * Ancient는 테두리에 못 자국이 있고 질감이 있다. Colored/Outline은 평평한
         * 만화풍이라 이 게임의 픽셀 배경과 붙지 않는다.
         */
        public const string PanelSet = "Ancient";

        /**
         * @brief 쓸 판.
         *
         * **brown을 쓴다. 팩에서 유일하게 테두리가 안쪽보다 밝은 판이다**
         * (테두리 0.57 / 안쪽 0.44). 나머지는 전부 반대라서, 어두운 색으로
         * 물들이면 테두리가 사라진다.
         *
         * 처음에 white를 지금 팔레트(#3A3550)로 물들였다가 실패했다. white의
         * 자체 대비는 0.10인데, 곱연산 틴트는 그 차이도 함께 줄인다:
         * 0.90 x 0.21 = 0.19, 0.80 x 0.21 = 0.17. 남는 차이가 0.02라 눈에
         * 보이지 않는다. 보스 확대판 틴트에서 겪은 것과 같은 함정이다.
         *
         * brown은 안쪽이 이미 어두워서(0.44) 크림색 글자가 그대로 읽히고,
         * 물들이지 않으므로 테두리와 못 자국이 살아 있다.
         */
        public const string PanelName = "brown";
        public const string PanelPressedName = "brown_pressed";

        /** 안쪽으로 파인 판. 바의 배경처럼 "여기는 눌리지 않는다"를 말한다 */
        public const string InlayName = "brown_inlay";

        /** 아트 그대로. 나무색이 필요할 때만 */
        public static readonly Color Untinted = Color.white;

        public static Sprite Panel { get { return PixelUiBuilder.Panel(PanelSet, PanelName); } }
        public static Sprite PanelPressed { get { return PixelUiBuilder.Panel(PanelSet, PanelPressedName); } }
        public static Sprite Inlay { get { return PixelUiBuilder.Panel(PanelSet, InlayName); } }

        // ---------------------------------------------------------------- 색

        /**
         * @brief 성장 패널의 행.
         *
         * 나무 판을 **자줏빛으로 눌러** 13단계까지 쓰던 #3A3550 근처로 되돌린다.
         * 나무색 그대로 쓰면 따뜻한 황갈색이 자주색 밤 배경과 부딪히고, 화면의
         * 절반이 다른 게임처럼 보인다.
         *
         * 어둡게 눌러도 테두리가 살아남는 이유가 brown을 고른 이유다 - 테두리가
         * 안쪽보다 **밝아서**(0.57 대 0.44) 곱셈이 그 관계를 뒤집지 못한다.
         * white는 반대라서 같은 처리에 테두리가 사라졌다.
         */
        public static readonly Color Row = new Color(0.46f, 0.50f, 0.94f, 1f);

        /**
         * @brief 상단 바·탭바처럼 배경 역할을 하는 판.
         *
         * 행보다 어두워야 한다. 같은 밝기면 상단 바가 또 하나의 행으로 읽힌다.
         */
        public static readonly Color Chrome = new Color(0.34f, 0.36f, 0.68f, 1f);

        /** 살 수 없는 행. 눌리지 않는다는 것이 색으로 먼저 읽혀야 한다 */
        public static readonly Color RowDisabled = new Color(0.38f, 0.40f, 0.72f, 1f);

        /**
         * @brief 안으로 파인 판을 쓸 때의 틴트.
         *
         * inlay는 원래 어두워서 행과 같은 틴트를 곱하면 검게 죽는다. 처음에
         * 잠긴 탭이 그렇게 새까맣게 나왔다. 밝은 쪽에서 눌러야 파인 모양이 남는다.
         */
        public static readonly Color InlayTint = new Color(0.78f, 0.80f, 1.00f, 1f);

        public static readonly Color Text = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        public static readonly Color TextDim = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        /**
         * @brief 동작 버튼의 색.
         *
         * 이 값들은 **최종 색이 아니라 나무 판에 곱할 틴트**다. 팩의 Colored 세트
         * (주황·라임)를 쓰면 색은 선명하지만 만화풍이라 이 게임의 배경과 붙지
         * 않는다. 나무 판을 붉은 쪽/초록 쪽으로 밀면 물들인 나무가 되고, 재질이
         * 나머지 UI와 같은 채로 색만 갈린다.
         *
         * 한 채널을 1.0 근처에 두는 것이 중요하다. 세 채널을 모두 낮추면 그냥
         * 어두워질 뿐이고 테두리가 사라진다 - white를 물들였다가 실패한 것과
         * 같은 이유다.
         */
        public static readonly Color Danger = new Color(1.00f, 0.42f, 0.42f, 1f);

        /** 레벨업. 초록 쪽은 이득을 뜻한다 */
        public static readonly Color Good = new Color(0.55f, 1.00f, 0.62f, 1f);

        public static readonly Color BossHealth = new Color32(0xC8, 0x3A, 0x46, 0xFF);
        public static readonly Color PlayerHealth = new Color32(0x6E, 0xC8, 0x7A, 0xFF);
        public static readonly Color Exp = new Color32(0x7C, 0xC5, 0x9A, 0xFF);

        /** 바의 빈 부분. 채움색이 무엇이든 그 아래는 같은 어둠이다 */
        public static readonly Color BarTrack = new Color32(0x18, 0x14, 0x24, 0xFF);

        // ---------------------------------------------------------------- 배율

        /**
         * @brief 아트 픽셀 하나가 캔버스 픽셀 몇 개가 되는가.
         *
         * 정수여야 한다. 픽셀 아트를 정수가 아닌 배율로 늘리면 어떤 픽셀은 두 칸,
         * 어떤 픽셀은 세 칸이 되어 테두리가 울퉁불퉁해진다. 보스 확대 배율을
         * 정수로 묶어둔 것과 같은 이유다.
         *
         * 2배를 쓴다. 16px 테두리가 캔버스 32px이 되고, 그것이 1080 폭에서
         * 행 높이(144)의 약 1/4이다. 3배(48px)는 행 안쪽이 너무 좁아진다.
         */
        public const int PixelScale = 2;

        /**
         * @brief Image.pixelsPerUnitMultiplier 에 넣을 값.
         *
         * 캔버스의 referencePixelsPerUnit(100)과 스프라이트 PPU(32)에서 유도한다.
         * 이 값을 안 건드리면 테두리가 100/32 = 3.125배로 그려지고, 정수가
         * 아니라서 못 자국이 뭉갠다.
         */
        public static float PixelsPerUnitMultiplier
        {
            get { return 100f / (Onikiri.Core.DisplayConfig.PixelsPerUnit * (float)PixelScale); }
        }

        // ---------------------------------------------------------------- 적용

        /**
         * @brief Image 하나를 9-슬라이스 판으로 만든다.
         *
         * 스프라이트가 없으면 색만 칠하고 끝낸다. 팩이 빠진 프로젝트에서도 화면이
         * 13단계와 같은 모양으로 뜨고, 그 상태가 "UI가 깨졌다"로 보이지 않는다.
         */
        public static void ApplyPanel(Image image, Sprite sprite, Color color)
        {
            if (image == null) return;

            image.color = color;
            image.sprite = sprite;

            if (sprite == null)
            {
                image.type = Image.Type.Simple;
                return;
            }

            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;

            // 가운데를 그린다. Sliced에서 이것을 끄면 테두리만 남고 안이 비어,
            // 행 위의 글자가 배경 없이 떠 있게 된다
            image.fillCenter = true;
        }

        public static void ApplyPanel(Image image, Color color)
        {
            ApplyPanel(image, Panel, color);
        }

        /**
         * @brief 버튼에 눌림 상태를 붙인다.
         *
         * 13단계까지 버튼은 눌러도 아무 일이 없었다 - 색조차 바뀌지 않았다.
         * 모바일에서 그것은 "안 눌렸나?" 로 읽히고, 플레이어가 같은 자리를 두 번
         * 누른다.
         *
         * SpriteSwap을 쓴다. ColorTint로 어둡게만 해도 되지만, 팩이 안쪽으로
         * 파인 눌림 스프라이트를 이미 갖고 있어서 그것을 쓰는 편이 훨씬 분명하다.
         */
        public static void ApplyButton(Button button, Image target)
        {
            if (button == null || target == null) return;

            button.targetGraphic = target;

            var pressed = PanelPressed;
            if (pressed == null)
            {
                button.transition = Selectable.Transition.ColorTint;
                return;
            }

            button.transition = Selectable.Transition.SpriteSwap;

            var state = button.spriteState;
            state.pressedSprite = pressed;
            state.selectedSprite = target.sprite;
            state.highlightedSprite = target.sprite;

            // 비활성 스프라이트는 지정하지 않는다. Button은 interactable이 꺼지면
            // disabledSprite로 갈아끼우는데, 여기서 눌림 판을 주면 "살 수 없는 행"이
            // "지금 눌린 행"과 같아 보인다. 색(RowDisabled)이 그 역할을 한다
            button.spriteState = state;
        }
    }
}
