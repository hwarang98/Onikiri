using Onikiri.Battle;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 지역별 배경 세트 애셋을 만든다. **있으면 건드리지 않는다.**
     *
     * 보스 배치(BossConfigBuilder)와 같은 규칙이다 - 이 애셋들은 손으로 다듬으라고
     * 만든 것이고, 빌드할 때마다 기본값으로 되돌아가면 데이터로 옮긴 의미가 없다.
     *
     * ## 지역 1은 예전 하드코딩을 그대로 옮긴 것이다
     *
     * 레이어 순서·속도·틴트가 21단계 이전 `BattleStageBuilder`의 배열과 switch에
     * 있던 값과 같다. 옮기면서 값을 바꾸면 "데이터로 옮겼더니 화면이 달라졌다"가
     * 되고, 그때 원인이 이사인지 값인지 알 수 없다.
     */
    public static class RegionBackgroundBuilder
    {
        public const string Folder = "Assets/_Project/Data/Backgrounds";

        public const string Region1Path = Folder + "/Background_Region1.asset";
        public const string Region2Path = Folder + "/Background_Region2.asset";
        public const string Region3Path = Folder + "/Background_Region3.asset";

        private const string JapanFolder = "Assets/ThirdParty/Backgrounds/TinyPixelJapan";
        private const string AutumnFolder = "Assets/ThirdParty/Backgrounds/AutumnForest";
        private const string SpringFolder = "Assets/ThirdParty/Backgrounds/SpringForest";

        /**
         * ## 톤 아크 — 지역이 갈수록 어두워진다
         *
         *     지역 1  봄숲 + 여명    맑고 밝다. 온보딩의 첫인상
         *     지역 2  가을숲         붉은 단풍. 중반
         *     지역 3  사쿠라 밤      자줏빛 황혼. 후반
         *
         * 23단계까지 자줏빛 밤이 지역 1이었다. 첫 화면이 가장 어두운 것은 아크가
         * 거꾸로 선 것이고, 새 플레이어가 가장 읽기 어려운 화면에서 시작한다는
         * 뜻이기도 했다. 애셋을 옮겼을 뿐 내용은 그대로다 - 이름만 Region3으로
         * 바뀌었고 GUID는 지켰다.
         */

        // ---------------------------------------------------------------- 지역 3 색 (옛 지역 1)

        private static readonly Color FarTint = new Color32(0x6E, 0x68, 0xA0, 0xFF);
        private static readonly Color MidTint = new Color32(0x8B, 0x82, 0xB5, 0xFF);
        private static readonly Color NearTint = new Color32(0xC8, 0xA4, 0xB8, 0xFF);
        private static readonly Color JapanClear = new Color32(0x2A, 0x27, 0x40, 0xFF);

        /**
         * @brief 지역 3 하늘 채움. 예전 색(Sky.png 단색 × 옛 틴트)을 그대로 낸다.
         *
         * 채움 시트가 팩 그림에서 흰 면으로 바뀌었으므로, 그림이 갖고 있던 색을 틴트로
         * 옮겨야 화면이 안 달라진다. 계산해서 넣은 값이다.
         */
        private static readonly Color JapanSkyFillTint = new Color32(0x6A, 0x5D, 0x78, 0xFF);

        // ---------------------------------------------------------------- 지역 1 색 (봄숲)
        //
        // 봄숲은 **지금까지의 두 팩과 반대 문제**를 갖고 있다. 가을숲은 어두워서
        // 누르면 죽었고, 봄숲은 너무 밝다 - 하늘 밝기 실측 0.914에 시안(hue 183)이라,
        // 원본 그대로 두면 흰 사무라이가 하늘에 묻힌다. 지역 2 단풍에서 겪은 것과
        // 같은 문제이고 방향만 반대다.
        //
        // 그래서 전체적으로 눌러 내린다. 다만 **따뜻함은 곱연산으로 만들 수 없다** -
        // 시안은 R이 가장 낮은 채널이라 어떤 색을 곱해도 R이 올라가지 않고, 따뜻해지는
        // 대신 탁한 청록이 된다. 여명은 곱연산이 아니라 얹는 안개가 만든다
        // (SpringForestBuilder.BuildDawnHaze).

        /**
         * @brief 하늘 채움. **밴드 안의 하늘보다 훨씬 어둡다.**
         *
         * 이것이 카메라 전체를 덮으므로 전투 밴드 밖, 즉 **성장 패널 뒤에 비치는 색**이
         * 곧 이 값이다. 패널은 반투명이다 - 지역 1·3이 자줏빛 밤이던 동안에는 뒤가
         * 어두워서 패널이 불투명해 보였을 뿐이다.
         *
         * 봄숲 하늘을 그대로 깔았더니 패널이 하늘색으로 비쳐 글자가 떠 보였다. 가을숲이
         * 같은 이유로 채움만 어둡게 눌러둔 것과 같다(AutumnSkyTint 주석 참고).
         *
         * 25단계에 성장 패널이 자기 배경을 갖게 되면서 "패널 뒤에 비치는 색"이라는 역할이
         * 사라졌다. 남은 역할은 **하늘 장 위쪽을 이어 그리는 것** 하나뿐이라, 가을숲과
         * 같은 규칙으로 끝선 아래의 화면 색을 실측해 이어받는다.
         *
         * 여기서는 **완전한 0을 낼 수 없다.** 끝선 바로 아래에 있는 것이 흐르는 구름이라
         * 그 높이의 색이 매 프레임 달라지기 때문이다. 하늘 장의 맨 윗줄(구름이 닿지 않는
         * 평평한 하늘)에 맞추면 남는 차이는 구름 자체이고, 그것은 이음매가 아니라 내용이다.
         * 실측 단차 0.0339 - 눈에 보이는 기준(0.05)의 아래다.
         */
        private static readonly Color SpringSkyFillTint = new Color32(0x92, 0xCC, 0xD7, 0xFF);

        /** 밴드 안의 하늘. 채움보다 밝다 */
        private static readonly Color SpringSkyTint = new Color32(0xD8, 0xD4, 0xD0, 0xFF);

        /** 설산. 원본이 가장 채도 높은 시안이라 가장 많이 누른다 */
        private static readonly Color SpringFarTint = new Color32(0xC4, 0xC2, 0xC6, 0xFF);

        /** 침엽수 띠 */
        private static readonly Color SpringMidTint = new Color32(0xB8, 0xBA, 0xB4, 0xFF);

        /** 앞쪽 덤불. 캐릭터 바로 뒤라 가장 어두워야 발이 읽힌다 */
        private static readonly Color SpringNearTint = new Color32(0x9E, 0xA4, 0x9A, 0xFF);

        /**
         * @brief 여명 안개의 색과 세기.
         *
         * 알파가 곧 세기다. 시트에는 알파의 **모양**(지평선이 짙고 위로 갈수록 맑음)만
         * 구워져 있고, 얼마나 얹을지는 여기서 정한다 - 화면을 보고 고치는 값이라
         * 시트를 다시 구울 일이 없어야 한다.
         *
         * 세게 걸면 원본 디테일이 뭉개진다. 0.59로 올렸더니 지평선은 여명이 됐지만
         * **설산이 통째로 사라졌다** - 프롬프트가 경고한 그것이다. 0.44는 하늘의 시안기가
         * 살구빛으로 넘어오면서도 설산 능선과 침엽수 실루엣이 남는 지점이다.
         */
        private static readonly Color SpringHazeTint = new Color32(0xFF, 0xC1, 0x8A, 0x70);

        /** 벚꽃. 팔레트의 벚꽃색과 같은 계열이라 거의 누르지 않는다 */
        private static readonly Color SpringSakuraTint = new Color32(0xF0, 0xDC, 0xDC, 0xFF);

        /**
         * @brief 지면. 거의 누르지 않는다.
         *
         * 이 팩의 바위가 이미 검은 남색이라 더 누를 여지가 없다 - 검게 만드는 것이
         * 아니라 검은 것을 어떻게 다루느냐가 문제였고, 그건 색이 아니라 **두께**로
         * 풀었다(SpringForestBuilder.StripHeight 48 -> 32). 풀의 채도만 살짝 내린다.
         */
        private static readonly Color SpringGroundTint = new Color32(0xC8, 0xBC, 0xAC, 0xFF);

        /** 대장간. 랜드마크라 살짝 따뜻하게 남긴다 */
        private static readonly Color SpringLandmarkTint = new Color32(0xC8, 0xB8, 0xAC, 0xFF);

        /** 배경이 못 덮는 곳에 비치는 색. 여명 하늘의 어두운 쪽이다 */
        private static readonly Color SpringClear = new Color32(0x3A, 0x38, 0x40, 0xFF);

        // ---------------------------------------------------------------- 지역 2 색
        //
        // 가을숲은 원본이 이미 남색·주황이다. 지역 1에 쓴 자줏빛 틴트를 그대로
        // 곱하면 먼 레이어가 검은 덩어리가 된다 - 원본이 밝고 따뜻한 팩이라
        // 눌러도 형태가 남았던 것이지, 어두운 팩에는 같은 값을 쓸 수 없다.
        //
        // 그래서 가을숲은 **거의 누르지 않는다.** 깊이는 원본이 이미 갖고 있다
        // (실측 밝기 far 0.664 -> near 0.207). 틴트가 하는 일은 UI 팔레트와
        // 맞추는 미세 조정뿐이다.

        /**
         * @brief 채움. **아트가 끝나는 높이의 화면 색을 그대로 이어받는다.**
         *
         * 24단계까지는 일부러 더 어둡게 눌렀다. 채움이 성장 패널 뒤까지 깔려서, 밝으면
         * 반투명 패널이 떠 보였기 때문이다. 25단계에 패널이 자기 배경을 갖게 되면서
         * 그 제약이 사라졌고, 채움의 역할은 **먼 배경 위쪽을 이어 그리는 것** 하나만
         * 남았다.
         *
         * 안개 시트들은 180px에서 끝나는데 전투 밴드는 9:19.5에서 211px이다. 그 위
         * 30px을 채움이 그리므로, 색이 다르면 시트가 끝나는 높이에 가로선이 그어진다.
         *
         * ## 한 레이어의 색이 아니라 **합성 결과**다
         *
         * 처음에 가장 먼 시트(3.png) 맨 윗줄 × 그 틴트 = #7382A8로 잡았다가 **오히려
         * 나빠졌다**(단차 0.0562 -> 0.2993). 그 높이에는 3.png만 있는 것이 아니라
         * 2.png와 1.png의 반투명 실루엣이 겹쳐 있어서, 실제 화면 색은 훨씬 어두운
         * #292E4D였다. 계산으로 맞출 수 있는 값이 아니라 **찍어서 재야 하는 값**이다.
         *
         * 렌더된 화면에서 끝선 아래 10px을 실측하고(가운데 UI를 피해 좌우 가장자리만)
         * 채움 그라디언트(0.96)로 나눈 값이다. 결과 단차 0.0027.
         */
        private static readonly Color AutumnSkyTint = new Color32(0x2C, 0x31, 0x51, 0xFF);

        /** 가장 먼 안개. 살짝 푸르게 밀어 거리감을 준다 */
        private static readonly Color AutumnFarTint = new Color32(0xC8, 0xCE, 0xE8, 0xFF);
        private static readonly Color AutumnMidTint = new Color32(0xD8, 0xDC, 0xF0, 0xFF);

        /**
         * @brief 가까운 실루엣. 원본이 이미 어두운 남색이라 그대로 둔다.
         */
        private static readonly Color AutumnNearTint = Color.white;

        /**
         * @brief 단풍과 지면. **원본을 눌러야 한다.**
         *
         * 처음에 원본 그대로(흰색) 뒀더니 채도 높은 주황이 화면을 가득 채워
         * **사무라이가 그 안에 묻혔다.** 지역 1이 깊이 틴트로 푼 문제와 같은
         * 것이고, 그 주석이 적어둔 규칙도 같다 - 캐릭터와 적은 틴트하지 않으므로
         * 배경이 눌린 만큼 그 둘이 화면에서 가장 밝고 가까운 것이 된다.
         *
         * 지역 1 근경(#C8A4B8)만큼 자줏빛으로 밀지는 않는다. 그러면 가을숲이
         * 사쿠라 밤의 재탕이 되고, 지역이 바뀐 것이 색에서 안 읽힌다. 밝기만
         * 20%쯤 낮추고 색상은 남긴다.
         */
        private static readonly Color AutumnFoliageTint = new Color32(0xCC, 0xAA, 0xB4, 0xFF);

        private static readonly Color AutumnClear = new Color32(0x2A, 0x2C, 0x48, 0xFF);

        [MenuItem("Onikiri/Scene/Build Region Backgrounds")]
        public static void BuildMenu()
        {
            EnsureDefaultAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("[Onikiri] Region background sets ready.");
        }

        public static void EnsureDefaultAssets()
        {
            EnsureFolder(Folder);

            // 굽는 것이 먼저다. 세트를 만들 때 스프라이트를 집어가므로, 없으면
            // 레이어가 빈 참조로 굳는다
            BackdropTextureBuilder.BuildAll();
            SpringForestBuilder.BuildAll();

            EnsureRegion1();
            EnsureRegion2();
            EnsureRegion3();

            BackfillArtBottom();
        }

        /**
         * @brief 땅에 서는 레이어의 밑동 여백을 다시 재서 채운다.
         *
         * 씨앗은 애셋을 **만들 때만** 돈다. 그래서 나중에 필드를 추가하면 이미 있는
         * 애셋에서는 영원히 0으로 남는다 - 처형인 config의 걷기 시트가 그랬고
         * (`BossConfigBuilder.BackfillWalkSheet`), 이 필드도 같은 처지다.
         *
         * 실측값이라 덮어써도 안전하다. 손으로 고를 값이 아니라 아트에서 나오는 값이고,
         * 아트가 그대로면 결과도 그대로다.
         */
        public static void BackfillArtBottom()
        {
            string[] paths = { Region1Path, Region2Path, Region3Path };
            int changed = 0;

            foreach (var path in paths)
            {
                var set = AssetDatabase.LoadAssetAtPath<RegionBackgroundSet>(path);
                if (set == null || set.layers == null) continue;

                foreach (var layer in set.layers)
                {
                    if (layer == null || !layer.sitsOnGround) continue;

                    float measured = MeasureArtBottom(layer.sprite);
                    if (Mathf.Approximately(measured, layer.artBottomPixels)) continue;

                    Debug.Log(string.Format("[Onikiri] {0} / {1}: 밑동 여백 {2}px -> {3}px",
                        set.name, layer.name, layer.artBottomPixels, measured));
                    layer.artBottomPixels = measured;
                    changed++;
                }

                if (changed > 0) EditorUtility.SetDirty(set);
            }

            if (changed > 0) AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- 지역 1 (봄숲)

        /**
         * @brief 봄숲. **레이어 순서가 파일 이름과 맞는 드문 경우다.**
         *
         * 가을숲에서 이름을 믿었다가 순서가 반대였던 적이 있어 이번에도 실측했다.
         * 결과는 이름과 같았다:
         *
         *     layer_1  불투명 100%  밝기 0.914  <- 구름 낀 하늘. 채움도 겸한다
         *     layer_2  불투명  60%  밝기 0.732  <- 설산
         *     layer_3  불투명  41%  밝기 0.628  <- 침엽수 띠
         *     layer_4  불투명  27%  밝기 0.393  <- 앞쪽 덤불, 가장 어둡다
         *
         * 불투명도와 밝기가 나란히 단조 감소한다. 100% 불투명한 장이 가장 뒤에
         * 있어야 하는 이유는 가을숲과 같다 - 그 뒤에는 그릴 것이 없다.
         *
         * 좌우 이음새도 함께 쟀다: layer_1 0.0010 / layer_2 0.0008 / layer_3 0.0073 /
         * layer_4 0.0030. 넷 다 지역 1 옛 실측 범위(0.000~0.046) 안이라 그대로
         * 이어 붙는다. 벚꽃 나무는 좌우 끝 열이 완전히 투명해 비교할 행이 0개다 -
         * 가을 단풍과 같고, 만나는 픽셀이 없으니 이음매도 없다.
         */
        private static RegionBackgroundSet EnsureRegion1()
        {
            return LoadOrCreate(Region1Path, set =>
            {
                set.displayName = "지역 1 · 봄숲 여명";
                set.clearColor = SpringClear;

                // 굽는 쪽이 실측과 대조해 검증하는 값이다. 손으로 적지 않는다
                set.groundSurfacePixels = SpringForestBuilder.SurfaceFromBottom;

                // 이 팩은 216px이다(가을·사쿠라는 180px). 세로 커버 판정이
                // 이 값으로 이뤄지므로 팩을 따라가야 한다
                set.backgroundPixelHeight = 216f;

                set.layers = new[]
                {
                    // 채움은 팩 시트가 아니라 **그림 없는 면**이다. 시트를 쓰면 세로
                    // 타일링에서 내용이 반복돼 화면 중턱에 가로선이 생긴다.
                    // BackdropTextureBuilder.SkyFillPath 주석 참고
                    Layer("Sky_Fill", BackdropTextureBuilder.SkyFillPath, 0f,
                          SpringSkyFillTint, sky: true),
                    Layer("Sky", SpringFolder + "/Background/layer_1.png", 0.12f, SpringSkyTint),
                    Layer("Mountains", SpringFolder + "/Background/layer_2.png", 0.30f, SpringFarTint),

                    /**
                     * 벚꽃은 **먼 배경이다.**
                     *
                     * 24단계에는 0.88(지면 바로 뒤)이었다. 큰 나무가 캐릭터·대장간과
                     * 같은 깊이에 서서 화면 앞을 막았다 - 지역 1의 첫인상이 "답답하다"
                     * 였던 이유다.
                     *
                     * 0.38로 밀어 설산과 침엽수 사이에 세운다. 크기는 그대로지만
                     * **침엽수·덤불·대장간이 앞에 서면서** 줄기가 가려지고, 위로 뻗은
                     * 꽃 덩어리만 남아 원경의 벚꽃 숲으로 읽힌다. 느리게 흐르는 것도
                     * 거리로 읽힌다.
                     *
                     * 뒤로 가도 **심어져 있어야 한다** - artBottomPixels 보정은 그대로다.
                     */
                    Layer("Sakura", SpringFolder + "/Trees/Color 1.png", 0.38f, SpringSakuraTint,
                          onGround: true),

                    Layer("Conifers", SpringFolder + "/Background/layer_3.png", 0.52f, SpringMidTint),

                    // 대장간은 벚꽃보다 앞이다. 랜드마크가 나무에 가리면 존재감이 없다
                    LandmarkLayer("Blacksmith", SpringForestBuilder.BlacksmithFrame(), 0.60f,
                                  SpringLandmarkTint, 140f),

                    /**
                     * 여명 안개는 **덤불 뒤**다.
                     *
                     * 24단계에는 덤불 앞이었다. 대기 원근은 먼 것에만 끼는 것이므로
                     * 원래도 그게 맞았는데, 벚꽃이 앞에 있던 동안에는 덤불이 나무에
                     * 가려 티가 안 났다. 벚꽃을 뒤로 보내자 덤불이 화면의 가장 넓은
                     * 면이 되었고, 그 위에 안개가 덮이니 **중경 전체가 평평한 갈색
                     * 덩어리**가 됐다.
                     *
                     * 뒤로 옮기면 하늘·설산·벚꽃·침엽수·대장간까지만 흐려지고, 캐릭터
                     * 바로 뒤의 덤불은 대비를 지킨다.
                     */
                    Layer("DawnHaze", SpringForestBuilder.HazePath, 0.10f, SpringHazeTint),

                    Layer("Brush", SpringFolder + "/Background/layer_4.png", 0.72f, SpringNearTint),

                    /**
                     * 여명 안개. **먼 레이어 앞, 가까운 것 뒤**에 선다.
                     *
                     * 대기 원근이 하는 일이 정확히 그것이다 - 멀리 있는 것에만
                     * 공기가 낀다. 벚꽃과 지면 앞에 두면 발밑까지 뿌옇게 되어
                     * 캐릭터가 안개 속에 잠긴다.
                     *
                     * 속도 0이 아니라 0.10이다. 0이면 사본이 제자리에 서서 화면
                     * 왼쪽(음수 x)이 비는데, 이 시트는 가로로 완전히 균일해서
                     * 흘려도 보이지 않는다. 흘리는 쪽이 안전하다
                     */
                    // 타일셋에서 구운 스트립(SpringForestBuilder)
                    Layer("Ground", SpringForestBuilder.GroundPath, 1f, SpringGroundTint)
                };
            });
        }

        // ---------------------------------------------------------------- 지역 3 (옛 지역 1)

        /**
         * @brief 사쿠라 밤. 23단계까지 지역 1이던 그 세트다.
         *
         * 애셋 파일은 이름만 바꿔 옮겼다(GUID 유지). 여기 씨앗은 **처음부터 다시
         * 만들 때를 위한 것**이고, 값은 옛 지역 1과 한 글자도 다르지 않다 - 옮기면서
         * 값을 바꾸면 화면이 달라졌을 때 원인이 이사인지 값인지 알 수 없다. 21단계에
         * 배경을 데이터로 옮길 때와 같은 규칙이다.
         */
        private static RegionBackgroundSet EnsureRegion3()
        {
            return LoadOrCreate(Region3Path, set =>
            {
                set.displayName = "지역 3 · 사쿠라 밤";
                set.clearColor = JapanClear;
                set.groundSurfacePixels = 24f;
                set.backgroundPixelHeight = 180f;

                set.layers = new[]
                {
                    // 채움만 그림 없는 면으로 바꿨다(25단계). **이 지역은 원래 이음매가
                    // 없었다** - Sky.png는 63,540픽셀이 전부 같은 색인 완전 단색이라
                    // 세로로 반복해도 표가 나지 않는다. 그래도 같은 면을 쓰게 한 이유는,
                    // 채움이 지역마다 다른 규칙을 따르면 다음 지역에서 또 틀리기 때문이다.
                    // 틴트는 예전 색(단색 x 옛 틴트)을 그대로 재현한다
                    Layer("Sky", BackdropTextureBuilder.SkyFillPath, 0f, JapanSkyFillTint, sky: true),
                    Layer("Clouds", JapanFolder + "/Clouds.png", 0.15f, FarTint),
                    Layer("Fuji", JapanFolder + "/Fuji.png", 0.20f, FarTint),
                    Layer("Mountain_Back", JapanFolder + "/Mountain_Back.png", 0.34f, MidTint),
                    Layer("Mountain_Middle", JapanFolder + "/Mountain_Middle.png", 0.48f, MidTint),
                    Layer("Mountain_Front", JapanFolder + "/Mountain_Front.png", 0.62f, MidTint),
                    Layer("BackgroundTrees", JapanFolder + "/BackgroundTrees.png", 0.76f, MidTint),
                    Layer("Trees", JapanFolder + "/Trees.png", 0.88f, MidTint),

                    // 탑은 랜드마크다. 근경 속도로 두면 11 units마다 같은 탑이
                    // 지나가고 "왜 같은 탑이 계속 나오지?"가 된다
                    Layer("Shrine_Single", JapanFolder + "/Shrine_Single.png", 0.22f, NearTint,
                          landmarkSeconds: 125f),

                    Layer("Ground", JapanFolder + "/Ground.png", 1f, NearTint),
                    Layer("Gras", JapanFolder + "/Gras.png", 1f, NearTint, groundCover: true)
                };
            });
        }

        // ---------------------------------------------------------------- 지역 2

        /**
         * @brief 가을숲. **레이어 순서를 실측으로 정했다.**
         *
         * 파일 이름이 1/2/3이라 순서를 말해주지 않는다. 각 장의 불투명도와 밝기를
         * 재서 far -> near를 확정했다:
         *
         *     3.png  불투명 100%  밝기 0.664  <- 배경을 통째로 칠한다 = 가장 멀다
         *     2.png  불투명  73%  밝기 0.448
         *     1.png  불투명  57%  밝기 0.207  <- 가지·뿌리까지 뚜렷한 어두운 실루엣
         *     Trees  불투명  31%  밝기 0.461  <- 주황 단풍, 가장 앞
         *
         * 이름 순서(1->2->3)와 **반대**다. 100% 불투명한 장이 뒤에 있어야 하는
         * 이유는 단순하다 - 그 뒤에는 아무것도 그릴 필요가 없기 때문이다.
         *
         * 좌우 이음새도 함께 쟀다: 3=0.0007, 2=0.0000, 1=0.0120. 지역 1 실측
         * (0.000~0.046) 범위 안이라 두 장 교대로 맞물린다.
         *
         * Trees는 0.0822로 그보다 나쁘지만 **랜드마크로 쓴다** - 나무 세 그루가
         * 그림의 일부만 차지하고 나머지가 투명이라, 사본을 벌리면 이음새가
         * 만나는 지점 자체가 화면에 없다. 탑과 같은 처리다.
         */
        private static RegionBackgroundSet EnsureRegion2()
        {
            return LoadOrCreate(Region2Path, set =>
            {
                set.displayName = "지역 2 · 가을숲";
                set.clearColor = AutumnClear;

                /**
                 * **손으로 적지 않는다.** 이 값은 지면 스트립을 어떻게 구웠는지에
                 * 딸린 값이라, 여기에 숫자를 적어두면 스트립을 다시 구울 때마다
                 * 어긋난다. 실제로 세 번 어긋났다 - 64(낙엽 끝)로 뒀다가 캐릭터가
                 * 잎 위에 떴고, 32(타일 경계)로 뒀다가 흙 속에 박혔다.
                 *
                 * 굽는 쪽이 낙엽/흙 경계를 실측하고 그 값과 대조해 에러를 내므로,
                 * 여기서는 그 상수를 그대로 읽기만 한다.
                 */
                set.groundSurfacePixels = AutumnGroundBuilder.SurfaceFromBottom;
                set.backgroundPixelHeight = 180f;

                set.layers = new[]
                {
                    // 3이 가장 멀다. 불투명 100%라 하늘 채움도 겸한다 -
                    // 이 팩에는 별도 하늘 장이 없다.
                    //
                    // 채움은 **더 어둡게** 눌러야 한다. 카메라 전체를 덮으므로
                    // 전투 밴드 밖(UI 뒤)까지 이 색이 깔리는데, 원본 그대로면
                    // 화면 아래쪽이 밝은 회색이 되어 자주색 UI가 떠 보인다.
                    // 지역 1의 Sky가 자주색 밤이라 그 문제가 없었다
                    // 채움은 팩 시트가 아니라 그림 없는 면이다(25단계). 3.png를 세로로
                    // 타일링하면 시트 아래쪽 나무 줄기 띠가 화면 중턱에 다시 나타나
                    // 배경이 가로로 갈라져 보였다. BackdropTextureBuilder 주석 참고
                    Layer("Mist_Far", BackdropTextureBuilder.SkyFillPath, 0f, AutumnSkyTint, sky: true),
                    Layer("Mist_Far_Scroll", AutumnFolder + "/Background/3.png", 0.18f, AutumnFarTint),
                    Layer("Mist_Mid", AutumnFolder + "/Background/2.png", 0.40f, AutumnMidTint),
                    Layer("Silhouette", AutumnFolder + "/Background/1.png", 0.66f, AutumnNearTint),

                    // 단풍은 **랜드마크가 아니라 숲 그 자체**다. 처음에 탑처럼
                    // 95초 간격으로 벌렸더니 화면에 한 번도 안 나왔다 - 가을숲인데
                    // 단풍이 없는 화면이 됐다.
                    //
                    // 이어 붙여도 이음새가 없다. 좌우 끝 열이 **둘 다 완전히
                    // 투명**이라 만나는 픽셀 자체가 없기 때문이다. 앞서 잰 0.0822는
                    // 보이지 않는 투명 픽셀의 RGB 차이였고, 알파를 빼고 다시 재니
                    // 비교할 행이 0개였다
                    // **땅에 선다.** 밴드 바닥에 밑단을 두면 지면(2u)이 줄기를
                    // 통째로 먹고 잎 덩어리만 공중에 남는다
                    Layer("Maples", AutumnFolder + "/Trees/Trees.png", 0.88f, AutumnFoliageTint,
                          onGround: true),

                    // 타일셋에서 구운 스트립(AutumnGroundBuilder)
                    Layer("Ground", AutumnGroundBuilder.OutputPath, 1f, AutumnFoliageTint)
                };
            });
        }

        // ---------------------------------------------------------------- 도구

        /**
         * @brief 스프라이트를 경로가 아니라 참조로 받는 레이어.
         *
         * 시트에서 잘라낸 한 장은 경로로 집을 수 없다 - `LoadAssetAtPath<Sprite>`는
         * 여러 장으로 잘린 시트에서 무엇을 돌려줄지 보장하지 않는다. 대장간이 그렇다
         * (13프레임 시트에서 첫 장만 쓴다).
         */
        private static RegionBackgroundSet.Layer LandmarkLayer(
            string name, Sprite sprite, float speed, Color tint, float landmarkSeconds)
        {
            if (sprite == null)
                Debug.LogWarning("[Onikiri] Background landmark sprite missing: " + name);

            return new RegionBackgroundSet.Layer
            {
                name = name,
                sprite = sprite,
                scrollSpeed = speed,
                tint = tint,
                landmarkRepeatSeconds = landmarkSeconds
            };
        }

        private static RegionBackgroundSet.Layer Layer(
            string name, string spritePath, float speed, Color tint,
            bool sky = false, bool groundCover = false, float landmarkSeconds = 0f,
            bool onGround = false)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                Debug.LogWarning("[Onikiri] Background layer sprite missing: " + spritePath);

            return new RegionBackgroundSet.Layer
            {
                name = name,
                sprite = sprite,
                scrollSpeed = speed,
                tint = tint,
                isSkyFill = sky,
                isGroundCover = groundCover,
                landmarkRepeatSeconds = landmarkSeconds,
                sitsOnGround = onGround,

                // 땅에 서는 레이어만 재면 된다. 나머지는 밴드 바닥에 붙으므로
                // 캔버스 안에서 아트가 어디서 시작하든 상관이 없다
                artBottomPixels = onGround ? MeasureArtBottom(sprite) : 0f
            };
        }

        /**
         * @brief 캔버스 바닥에서 그려진 밑동까지의 빈 줄 수를 **픽셀로 실측한다.**
         *
         * `sprite.bounds`로는 알 수 없다. 배경 시트는 FullRect로 임포트되므로 bounds가
         * 언제나 캔버스 전체이고, 투명 여백이 몇 줄인지는 거기 안 담긴다.
         *
         * 읽기를 잠깐 켰다 되돌린다 - 원래 꺼져 있는 것이 맞고(메모리), 이 값은 굽는
         * 시점에 한 번만 필요하다. AutumnGroundBuilder가 타일셋을 읽을 때와 같은 방식이다.
         */
        private static float MeasureArtBottom(Sprite sprite)
        {
            if (sprite == null) return 0f;

            string path = AssetDatabase.GetAssetPath(sprite);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return 0f;

            bool wasReadable = importer.isReadable;
            if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

            try
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) return 0f;

                var rect = sprite.rect;
                int x0 = (int)rect.x, y0 = (int)rect.y;
                int w = (int)rect.width, h = (int)rect.height;

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (texture.GetPixel(x0 + x, y0 + y).a > 0.35f) return y;

                return 0f;
            }
            finally
            {
                if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
            }
        }

        private static RegionBackgroundSet LoadOrCreate(string path, System.Action<RegionBackgroundSet> seed)
        {
            var existing = AssetDatabase.LoadAssetAtPath<RegionBackgroundSet>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<RegionBackgroundSet>();
            seed(created);
            AssetDatabase.CreateAsset(created, path);
            Debug.Log("[Onikiri] Created " + path);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
