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

        private const string JapanFolder = "Assets/ThirdParty/Backgrounds/TinyPixelJapan";
        private const string AutumnFolder = "Assets/ThirdParty/Backgrounds/AutumnForest";

        // ---------------------------------------------------------------- 지역 1 색

        private static readonly Color FarTint = new Color32(0x6E, 0x68, 0xA0, 0xFF);
        private static readonly Color MidTint = new Color32(0x8B, 0x82, 0xB5, 0xFF);
        private static readonly Color NearTint = new Color32(0xC8, 0xA4, 0xB8, 0xFF);
        private static readonly Color JapanClear = new Color32(0x2A, 0x27, 0x40, 0xFF);

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
         * @brief 카메라 전체를 덮는 채움. 밴드 밖(UI 뒤)까지 이 색이 깔린다.
         *
         * 밴드 안에 보이는 같은 그림보다 어둡다. 어색해 보일 것 같지만 그렇지
         * 않다 - 밴드 안에는 이 위에 스크롤하는 안개 레이어가 한 장 더 깔려서,
         * 실제로 눈에 닿는 것은 밝은 쪽이다. 어두운 채움은 UI 뒤에서만 드러난다.
         */
        private static readonly Color AutumnSkyTint = new Color32(0x5E, 0x63, 0x8C, 0xFF);

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
            EnsureRegion1();
            EnsureRegion2();
        }

        // ---------------------------------------------------------------- 지역 1

        private static RegionBackgroundSet EnsureRegion1()
        {
            return LoadOrCreate(Region1Path, set =>
            {
                set.displayName = "지역 1 · 사쿠라 밤";
                set.clearColor = JapanClear;
                set.groundSurfacePixels = 24f;
                set.backgroundPixelHeight = 180f;

                set.layers = new[]
                {
                    Layer("Sky", JapanFolder + "/Sky.png", 0f, FarTint, sky: true),
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
                    Layer("Mist_Far", AutumnFolder + "/Background/3.png", 0f, AutumnSkyTint, sky: true),
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
                sitsOnGround = onGround
            };
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
