using System.Collections.Generic;
using System.IO;
using Onikiri.Battle;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief Pozac 팩(Beat 'em Up Combat Effects 5)의 낱장 프레임을 **혈(血) 계열로
     *        되물들여** 오의용 시트로 굽는다.
     *
     * ## 왜 되물들이는가 - 이 게임의 오의는 발도(拔刀)다
     *
     * 팩의 서른두 이펙트는 흰·금·청·녹이 섞여 있다. 그대로 쓰면 화면에 여섯째
     * 색 계열이 생기고, 그것은 오의가 아니라 "다른 게임의 이펙트"로 읽힌다.
     * 이 게임의 오의 이펙트는 48단계에 요괴에게서 뜯어낸 조각(혈참·혈조·혈파)이
     * 기준이고, 그 팔레트는 일곱 색 중 붉은 셋이다:
     *
     *     #4D0A29   #90133B   #B32849
     *
     * 신규 오의가 그 셋과 나란히 떠야 하므로, 팩의 색을 버리고 **밝기만 남겨**
     * 이 램프에 다시 얹는다.
     *
     * ## 왜 곱연산 틴트가 아니라 램프인가
     *
     * 35단계가 배경 재틴트에서 확정한 규칙이다 - **곱연산은 원본에 없는 채널을
     * 못 만든다.** 흰 링(E30)은 곱연산으로 붉어지지만 청록 소용돌이(E22)는
     * 그렇지 않다: 청록은 R이 거의 0이라 무엇을 곱해도 붉어지지 않고 어두워지기만
     * 한다. 실제로 확인했다.
     *
     * 밝기(luminance) 하나로 접었다가 램프로 펴면 **원본 색과 무관하게** 같은
     * 계열이 나온다. 원본의 명암 구조(중심이 밝고 가장자리가 어두운)는 밝기에
     * 그대로 남으므로 입체감을 잃지 않는다 - DirtTextureBaker가 흙을 재도색할 때
     * 쓴 것과 같은 수법이다(원본 명암을 질감으로 재사용).
     *
     * ## 감마 1.6이 하는 일
     *
     * 램프에 넣기 전에 밝기를 `l^1.6`으로 누른다. 안 누르면 폭발(E31)처럼 밝은
     * 픽셀이 대부분인 그림이 통째로 램프 꼭대기에 몰려 **평평한 분홍 덩어리**가
     * 된다 - 실기 비교로 확인했다(감마 1.0 / 1.6 / 2.2 세 벌을 나란히 굽고 골랐다).
     * 1.6이면 가장자리가 심홍으로 내려앉고 핵만 연분홍으로 남아, 원본이 흰 핵과
     * 주황 가장자리로 만들던 깊이가 그대로 옮겨온다.
     *
     * **구운 뒤 하베스트와 나란히 놓고 1.8로 한 칸 더 눌렀다.** 1.6에서는 링과
     * 폭발의 핵이 연분홍(#F2B0B8)이라, 같은 화면에 뜨는 48단계 조각(가장 밝은
     * 곳이 #B32849)과 **다른 계열로 읽혔다** - 이 스텝이 램프를 만든 이유가
     * 계열을 하나로 묶는 것이었으므로 그것은 실패다. 램프 꼭대기도 #E8657F로
     * 내려 하베스트와의 거리를 좁혔다. 핵이 여전히 가장 밝은 것은 남겨 뒀다 -
     * 터지는 그림에서 중심이 안 빛나면 그것은 폭발이 아니라 얼룩이다.
     *
     * ## 배율은 굽지 않는다
     *
     * YokaiVfxBaker와 같은 규칙이다 - 시트는 1배로 굽고 화면 크기는 부르는 쪽이
     * 정한다(PackSlash.Play의 scale). 구운 배율과 런타임 배율이 둘 다 있으면
     * 2배가 두 번 곱해진다.
     */
    public static class PozacVfxBaker
    {
        /**
         * 팩의 원본은 낱장 PNG 203장이다. **유니티 임포트를 거치지 않고 파일에서
         * 직접 읽는다** - 임포터가 스프라이트로 자르든 말든 결과가 같아야 하고,
         * 낱장 203장의 임포트 설정에 굽기가 의존하면 그 설정이 바뀌는 날 조용히
         * 다른 그림이 나온다.
         */
        public const string SourceFolder = "Assets/ThirdParty/VFX/Pozac5/PNG";

        /** 대소문자가 기존 폴더와 정확히 같아야 한다(`VFX`) - YokaiVfxBaker 주석 참고 */
        public const string OutputFolder = "Assets/_Project/Art/VFX/Pozac";

        public const string LibraryPath = "Assets/_Project/Data/VfxLibrary_Pozac.asset";

        // ---------------------------------------------------------------- 혈 램프

        /**
         * @brief 밝기를 누르는 지수. 위 주석의 "감마 1.6이 하는 일" 참고.
         */
        private const float LuminanceGamma = 1.8f;

        /**
         * @brief 혈 램프의 다섯 마디.
         *
         * 가운데 셋이 48단계 하베스트의 실제 팔레트다(#4D0A29 계열 → #6E0E31,
         * #B32849). 양끝 둘만 이 파일이 더한다 - 아래는 그림자, 위는 핵의
         * 하이라이트다. 하이라이트가 없으면 폭발의 중심이 가장자리와 같은 값이
         * 되어 "빛나는 것"으로 안 읽힌다.
         */
        private static readonly float[] RampStops = { 0.00f, 0.35f, 0.70f, 0.90f, 1.00f };

        private static readonly Color32[] RampColors =
        {
            new Color32(0x2A, 0x06, 0x16, 255),
            new Color32(0x6E, 0x0E, 0x31, 255),
            new Color32(0xB3, 0x28, 0x49, 255),
            new Color32(0xC9, 0x45, 0x5C, 255),
            new Color32(0xE8, 0x65, 0x7F, 255)
        };

        /** 오의 이펙트로 쓰는 세 조각의 이름. 카탈로그(SkillCatalog)가 이 값을 가리킨다 */
        public const string WaveRingId = "pozac_wave_ring";
        public const string BurstId = "pozac_burst";
        public const string VortexId = "pozac_vortex";

        /**
         * 무기 티어 스파크 두 조각 (51단계). SkillPerformer의 상수와 같아야
         * 한다 - 갈리면 스파크 겹이 조용히 안 뜬다(테스트가 잡는다).
         */
        public const string SparkBurstId = "pozac_spark_burst";
        public const string SparkRayId = "pozac_spark_ray";

        /**
         * @brief 뜯어 쓸 이펙트 하나.
         *
         * 서른둘 중 셋만 쓴다. 나머지를 안 쓰는 이유는 자리가 없어서지 그림이
         * 나빠서가 아니다 - 다음에 필요해지면 이 표에 줄을 하나 더 적으면 된다.
         * (팩 전체는 `Assets/ThirdParty/VFX/Pozac5`에 원본 그대로 남아 있다.)
         */
        private struct Recipe
        {
            public string Id;
            public string FileName;

            /** 팩의 번호. `Effect (N)1.png` ~ `Effect (N)M.png` */
            public int Effect;

            /** 재생 기본값. 라이브러리 애셋의 씨앗이다 */
            public float FrameRate;
            public float Scale;
            public float Angle;
            public float ForwardOffset;
            public float HeightOffset;

            /**
             * @brief 혈 램프 대신 **은백**으로 굽는다 (51단계 무기 티어 스파크).
             *
             * 곱연산 틴트가 임의 색을 내려면 바탕이 은색이어야 한다(47단계
             * 규칙). 티어 색은 다섯인데 시트를 다섯 벌 굽는 대신, 밝기만 남긴
             * 한 벌을 PackSlash가 재생할 때 물들인다. 혈 램프를 지나면 붉은
             * 계열 밖의 색(티어1 청, 티어2 보라)을 영영 못 낸다.
             */
            public bool Silver;
        }

        /**
         * @brief 세 조각. 전부 낱장을 눈으로 확인하고 골랐다.
         *
         * - **파동 링(E30, 8프레임)**: 한 점에서 겹겹의 링이 퍼져 나간다. 지면
         *   파동으로 쓰기에 유일하게 맞는 그림이다 - 다른 링(E24·E26)은 점선이라
         *   1080 화면에서 흩어져 보인다.
         * - **폭발(E31, 6프레임)**: 흰 핵 + 주황 구름. 팩에서 가장 큰 단발이고,
         *   램프를 지나면 심홍 구름에 연분홍 핵이 된다.
         * - **소용돌이(E22, 8프레임)**: 청록 회오리 두 겹이 서로 반대로 감긴다.
         *   회전 다타의 그림이고, **곱연산으로는 절대 붉게 만들 수 없는 그림**이라
         *   램프 방식의 필요성을 증명하는 자리이기도 하다.
         */
        private static readonly Recipe[] Recipes =
        {
            /**
             * @brief 지면 파동. 발밑에서 퍼지므로 앞으로 밀지 않고 낮게 깐다.
             *
             * **배율 1이다.** 구운 셀이 126x120이라 32 PPU에서 이미 3.94u이고,
             * 화면 폭이 6.75u다 - 2배면 7.88u로 화면을 통째로 덮는다. 48단계가
             * 초승달(114px)에서 배율 3을 1로 내릴 수밖에 없었던 것과 같은 산수다.
             * 이 팩의 128px 캔버스는 참격 팩(64px)의 두 배라 "정수 배율" 규칙의
             * 하한이 곧 정답이 된다.
             */
            new Recipe
            {
                Id = WaveRingId, FileName = "WAVE_RING.png", Effect = 30,
                FrameRate = 18f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0f, HeightOffset = 0.45f
            },

            // 단발 버스트. 요괴 몸통 높이에서 터진다.
            // 앞으로 1.6u - 요괴 열이 대략 x 0.8~3.7에 서므로 첫 줄 언저리다
            new Recipe
            {
                Id = BurstId, FileName = "BURST.png", Effect = 31,
                FrameRate = 16f, Scale = 1f, Angle = 0f,
                ForwardOffset = 1.6f, HeightOffset = 1.05f
            },

            // 회전 다타. 플레이어를 감싸므로 앞으로 밀지 않는다.
            // 2.94u는 사무라이 키(3u)와 거의 같다 - 감싸는 그림이라 그래야 맞다
            new Recipe
            {
                Id = VortexId, FileName = "VORTEX.png", Effect = 22,
                FrameRate = 16f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0f, HeightOffset = 1.0f
            },

            // ------------------------------------------------ 51단계: 무기 티어 스파크
            //
            // 자리(forward/height)를 0으로 두는 것이 위 셋과 다르다 - 이 둘은
            // 오의처럼 정해진 자리에 서는 조각이 아니라 SkillPerformer가 참격
            // 앵커 기준으로 겹마다 자리를 계산해 띄우는 조각이다. 라이브러리의
            // 자리 값은 읽히지 않고, 배율·속도만 씨앗으로 쓰인다.

            // 버스트 스파크 (E28, 6프레임). 금색 원본 -> 은백. 짧고 둥글게 터진다
            new Recipe
            {
                Id = SparkBurstId, FileName = "SPARK_BURST.png", Effect = 28,
                FrameRate = 20f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0f, HeightOffset = 0f, Silver = true
            },

            // 방사 스파크 (E1, 6프레임). 시안 원본 -> 은백. 가느다란 침이 사방으로
            new Recipe
            {
                Id = SparkRayId, FileName = "SPARK_RAY.png", Effect = 1,
                FrameRate = 20f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0f, HeightOffset = 0f, Silver = true
            },

            // ------------------------------------------------ 15종 재설계: 신규 일곱
            //
            // **전부 은백으로 굽는다.** 위의 세 오의 조각은 혈 램프를 지나 붉게
            // 구워져 있는데, 그것은 그 셋이 전부 혈식이라 성립한 선택이었다.
            // 신규 일곱은 검식 셋(청)과 귀오의 넷(금)이라 한 램프로 못 덮는다.
            //
            // 그래서 47단계의 규칙을 그대로 빌린다 - **밝기만 남긴 한 벌을 굽고
            // 재생할 때 물들인다**(Recipe.Silver 주석). 물들이는 색은 각 오의의
            // SlashRgba이고, 안무의 slashTint가 그 값을 나른다. 기존 여덟의
            // slashTint는 흰색이라 한 픽셀도 안 바뀐다.

            // 심격 - 세로로 길게 뻗는 창 (E5, 6프레임). 찌르기의 그림이다
            new Recipe
            {
                Id = Onikiri.Progression.SkillCatalog.SkillVfx.Thrust, FileName = "THRUST.png", Effect = 5,
                FrameRate = 18f, Scale = 1f, Angle = 0f,
                ForwardOffset = 1.4f, HeightOffset = 1.0f, Silver = true
            },

            // 회월참 - 휘어 도는 초승달 궤적 (E20, 5프레임).
            // 시전자를 감싸는 원이라 앞으로 안 민다
            new Recipe
            {
                Id = Onikiri.Progression.SkillCatalog.SkillVfx.MoonArc, FileName = "MOON_ARC.png", Effect = 20,
                FrameRate = 18f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0f, HeightOffset = 1.0f, Silver = true
            },

            // 검진 - 흩뿌려 남는 검기 (E18, 12프레임).
            // **프레임이 가장 많은 조각을 고른 이유가 지속이다** - 12프레임을
            // 8fps로 재생하면 1.5초이고, 그것이 장판의 수명과 같다
            new Recipe
            {
                Id = Onikiri.Progression.SkillCatalog.SkillVfx.SwordField, FileName = "SWORD_FIELD.png", Effect = 18,
                FrameRate = 8f, Scale = 1f, Angle = 0f,
                ForwardOffset = 1.8f, HeightOffset = 0.7f, Silver = true
            },

            // 귀신난무 - 사방으로 터지는 침 (E15, 4프레임).
            // 다섯 타격마다 한 번씩 뜨므로 짧아야 한다
            new Recipe
            {
                Id = Onikiri.Progression.SkillCatalog.SkillVfx.OniDance, FileName = "ONI_DANCE.png", Effect = 15,
                FrameRate = 20f, Scale = 1f, Angle = 0f,
                ForwardOffset = 1.6f, HeightOffset = 1.05f, Silver = true
            },

            // 나락인력 - 빨아들이는 구체 (E9, 8프레임).
            // 도착점(시전자 +1.6u)에 뜨므로 그 자리에 맞춘다
            new Recipe
            {
                Id = Onikiri.Progression.SkillCatalog.SkillVfx.AbyssPull, FileName = "ABYSS_PULL.png", Effect = 9,
                FrameRate = 16f, Scale = 1f, Angle = 0f,
                ForwardOffset = 1.6f, HeightOffset = 1.0f, Silver = true
            },

            // 참수 - 처형의 섬광 (E32, 8프레임). 최근접 하나에 떨어진다
            new Recipe
            {
                Id = Onikiri.Progression.SkillCatalog.SkillVfx.Decapitate, FileName = "DECAPITATE.png", Effect = 32,
                FrameRate = 16f, Scale = 1f, Angle = 0f,
                ForwardOffset = 1.4f, HeightOffset = 1.1f, Silver = true
            },

            // 귀왕강림 - 금빛 강림진 (E27, 16프레임).
            // 팩에서 가장 긴 조각이다. 최장 쿨(19초)의 오의라 여기가 그 자리다
            new Recipe
            {
                Id = Onikiri.Progression.SkillCatalog.SkillVfx.OniAdvent, FileName = "ONI_ADVENT.png", Effect = 27,
                FrameRate = 14f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0f, HeightOffset = 1.2f, Silver = true
            },
        };

        /** 구운 조각의 셀 크기. 굽고 나서 채워진다 */
        public static readonly Dictionary<string, Vector2Int> BakedCells =
            new Dictionary<string, Vector2Int>();

        public static IEnumerable<string> Ids
        {
            get { foreach (var recipe in Recipes) yield return recipe.Id; }
        }

        public static string PathFor(string id)
        {
            foreach (var recipe in Recipes)
                if (recipe.Id == id) return OutputFolder + "/" + recipe.FileName;
            return null;
        }

        [MenuItem("Onikiri/Art/Bake Pozac VFX")]
        public static void BakeMenu()
        {
            if (!BakeAll()) return;

            var library = BuildLibrary();
            Debug.Log(string.Format("[Onikiri] Pozac VFX baked: {0} clip(s), library {1}.",
                BakedCells.Count, library != null ? "written" : "FAILED"));
        }

        // ---------------------------------------------------------------- 굽기

        public static bool BakeAll()
        {
            BakedCells.Clear();
            EnsureFolder(OutputFolder);

            foreach (var recipe in Recipes)
                if (!BakeOne(recipe)) return false;

            AssetDatabase.Refresh();
            return true;
        }

        private static bool BakeOne(Recipe recipe)
        {
            var frames = LoadFrames(recipe.Effect);
            if (frames == null || frames.Count == 0)
            {
                Debug.LogError("[Onikiri] Pozac effect " + recipe.Effect + " has no frames under "
                               + SourceFolder);
                return false;
            }

            // 낱장은 전부 같은 캔버스에 그려져 있다(64 또는 128 정사각). 그래서
            // 하베스트와 달리 피벗 정렬이 필요 없다 - 캔버스가 곧 정렬이다.
            // 다만 크기가 섞이면 셀 계산이 어긋나므로 확인한다
            int w = frames[0].Width, h = frames[0].Height;
            foreach (var frame in frames)
            {
                if (frame.Width == w && frame.Height == h) continue;

                Debug.LogError(string.Format(
                    "[Onikiri] Pozac effect {0}: frame sizes differ ({1}x{2} vs {3}x{4}). "
                    + "The pack is expected to draw every frame on one canvas.",
                    recipe.Effect, w, h, frame.Width, frame.Height));
                return false;
            }

            foreach (var frame in frames)
            {
                if (recipe.Silver) RecolorSilver(frame.Pixels);
                else Recolor(frame.Pixels);
            }

            // 팩의 캔버스는 그림보다 넉넉하다. 여백을 남기면 셀 한가운데 피벗이
            // 그림의 한가운데가 아니게 되고, 그 어긋남이 화면에서 "이펙트가
            // 한쪽으로 쏠려 뜬다"로 나온다 (48d가 스프라이트 칸으로 물린 함정과
            // 같은 종류다 - 칸은 위치 기준이 못 된다)
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            foreach (var frame in frames)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (frame.Pixels[y * w + x].a <= 0f) continue;
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
                }
            }

            if (x1 < 0)
            {
                Debug.LogError("[Onikiri] Pozac effect " + recipe.Effect + " is fully transparent.");
                return false;
            }

            // **좌우·상하로 대칭이 되게 넓힌다.** 잘라낸 상자의 한가운데가 곧
            // 피벗인데, 원본의 그림이 캔버스 한가운데를 조금 벗어나 있으면 그
            // 상자의 중심도 벗어난다. 링과 소용돌이는 "퍼져 나가는 중심"이
            // 그림의 뜻이라, 그 중심이 반 픽셀만 밀려도 프레임 사이에서 흔들린다
            int cx0 = Mathf.Min(x0, w - 1 - x1);
            int cy0 = Mathf.Min(y0, h - 1 - y1);
            x0 = cx0; x1 = w - 1 - cx0;
            y0 = cy0; y1 = h - 1 - cy0;

            int cellW = x1 - x0 + 1;
            int cellH = y1 - y0 + 1;

            // 한 줄 가로 스트립. 여덟 장 x 128px = 1024px이라 4096 상한과 멀다
            int sheetW = cellW * frames.Count;
            var sheet = new Color[sheetW * cellH];

            for (int i = 0; i < frames.Count; i++)
            {
                var pixels = frames[i].Pixels;
                for (int y = 0; y < cellH; y++)
                {
                    for (int x = 0; x < cellW; x++)
                        sheet[y * sheetW + i * cellW + x] = pixels[(y0 + y) * w + x0 + x];
                }
            }

            string path = OutputFolder + "/" + recipe.FileName;
            WritePng(path, sheet, sheetW, cellH);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // 피벗은 셀 한가운데다. 참격과 같은 규칙 - 허공에 놓이는 그림이고
            // 부르는 쪽이 중심으로 자리를 잡는다
            if (!CharacterSpriteSlicer.SliceGrid(path, cellW, cellH, new Vector2(0.5f, 0.5f)))
            {
                Debug.LogError("[Onikiri] Pozac VFX '" + recipe.Id + "' failed to slice at "
                               + cellW + "x" + cellH + ".");
                return false;
            }

            BakedCells[recipe.Id] = new Vector2Int(cellW, cellH);

            Debug.Log(string.Format(
                "[Onikiri] Pozac VFX '{0}': E{1} {2} frames, cell {3}x{4} -> {5}",
                recipe.Id, recipe.Effect, frames.Count, cellW, cellH, path));

            return true;
        }

        // ---------------------------------------------------------------- 재색

        /**
         * @brief 밝기만 남겨 혈 램프에 다시 얹는다. 알파는 손대지 않는다.
         *
         * 알파를 그대로 두는 것이 요점이다 - 팩의 이펙트는 가장자리를 알파로
         * 흐리게 그리는데(픽셀 아트지만 반투명 가장자리를 쓴다), 그 알파가
         * 사라지면 링이 종이를 오린 것처럼 딱딱해진다.
         */
        private static void Recolor(Color[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.a <= 0f) continue;

                float luminance = 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
                var color = Ramp(Mathf.Pow(Mathf.Clamp01(luminance), LuminanceGamma));
                pixels[i] = new Color(color.r, color.g, color.b, pixel.a);
            }
        }

        /**
         * @brief 밝기만 남긴다 - 은백 굽기 (51단계). 알파는 혈 램프와 같은
         *        이유로 손대지 않는다.
         *
         * 감마를 안 누른다. 혈 램프의 1.8은 밝은 픽셀이 램프 꼭대기(연분홍)에
         * 몰리는 것을 막는 값인데, 은백에는 꼭대기가 없다 - 여기서 눌러버리면
         * 틴트를 곱한 뒤의 그림이 통째로 어두워져, 티어 색이 "빛나는 스파크"가
         * 아니라 "칙칙한 얼룩"이 된다.
         */
        private static void RecolorSilver(Color[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.a <= 0f) continue;

                float luminance = 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
                pixels[i] = new Color(luminance, luminance, luminance, pixel.a);
            }
        }

        /** 램프 한 점. 마디 사이를 선형으로 잇는다 */
        public static Color Ramp(float t)
        {
            t = Mathf.Clamp01(t);

            for (int i = 0; i < RampStops.Length - 1; i++)
            {
                if (t > RampStops[i + 1] && i + 1 < RampStops.Length - 1) continue;

                float span = RampStops[i + 1] - RampStops[i];
                float f = span <= 0f ? 0f : Mathf.Clamp01((t - RampStops[i]) / span);
                return Color.Lerp(RampColors[i], RampColors[i + 1], f);
            }

            return RampColors[RampColors.Length - 1];
        }

        // ---------------------------------------------------------------- 원본 읽기

        private sealed class Frame
        {
            public int Width;
            public int Height;
            public Color[] Pixels;
        }

        /**
         * @brief `Effect (N)1.png` 부터 번호가 끊길 때까지 읽는다.
         *
         * 파일 이름의 숫자가 곧 순서다. 디렉터리 나열 순서를 믿으면 10이 2보다
         * 앞에 오는 문자열 정렬에 걸린다 - 실제로 이 팩은 프레임이 열둘까지 있다.
         */
        private static List<Frame> LoadFrames(int effect)
        {
            var frames = new List<Frame>();

            for (int index = 1; ; index++)
            {
                string path = string.Format("{0}/Effect ({1}){2}.png", SourceFolder, effect, index);
                if (!File.Exists(path)) break;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    Debug.LogError("[Onikiri] Pozac frame failed to decode: " + path);
                    Object.DestroyImmediate(texture);
                    return null;
                }

                frames.Add(new Frame
                {
                    Width = texture.width,
                    Height = texture.height,
                    Pixels = texture.GetPixels()
                });
                Object.DestroyImmediate(texture);
            }

            return frames;
        }

        // ---------------------------------------------------------------- 라이브러리

        /**
         * @brief 구운 시트를 읽어 `VfxLibrary` 애셋을 다시 쓴다.
         *
         * YokaiVfxBaker.BuildLibrary와 같은 규칙이고 같은 이유다 - 손으로 적을
         * 것이 없으므로 매번 덮어쓰고, `SaveAssets`까지 밀어야 디스크에 닿는다.
         */
        public static VfxLibrary BuildLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var so = new SerializedObject(library);
            var array = so.FindProperty("clips");
            array.arraySize = Recipes.Length;

            for (int i = 0; i < Recipes.Length; i++)
            {
                var recipe = Recipes[i];
                string path = OutputFolder + "/" + recipe.FileName;
                var frames = OrderedSprites(path);
                if (frames.Length == 0)
                {
                    Debug.LogError("[Onikiri] Pozac VFX sheet has no sprites: " + path);
                    return null;
                }

                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = recipe.Id;
                element.FindPropertyRelative("frameRate").floatValue = recipe.FrameRate;
                element.FindPropertyRelative("scale").floatValue = recipe.Scale;
                element.FindPropertyRelative("angle").floatValue = recipe.Angle;
                element.FindPropertyRelative("forwardOffset").floatValue = recipe.ForwardOffset;
                element.FindPropertyRelative("heightOffset").floatValue = recipe.HeightOffset;

                var slots = element.FindPropertyRelative("frames");
                slots.arraySize = frames.Length;
                for (int f = 0; f < frames.Length; f++)
                    slots.GetArrayElementAtIndex(f).objectReferenceValue = frames[f];

                Debug.Log(string.Format("[Onikiri] VFX clip '{0}': {1} frames @{2}fps, scale {3}.",
                    recipe.Id, frames.Length, recipe.FrameRate, recipe.Scale));
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            return library;
        }

        /** 시트에서 잘린 스프라이트를 시트 순서대로. YokaiVfxBaker와 같은 규칙 */
        private static Sprite[] OrderedSprites(string sheetPath)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return sprites.ToArray();
        }

        private static int IndexOf(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            int value;
            if (underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out value)) return value;
            return 0;
        }

        private static void WritePng(string path, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
