using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 구운 지면 스트립의 표면 아래를 흙 질감으로 다시 칠한다 (41단계).
     *
     * ## 왜 필요한가
     *
     * 봄숲·가을숲 팩에는 흙 타일이 없어서(각 빌더 주석 참조) 표면 타일의
     * 바위 줄을 되풀이해 채우는데, 그 바위가 문제다 - 봄숲은 거의 검은
     * 남색(#1D1D3E대)이고, 하단 UI 바탕(UiSkin.PanelInk #221D30)과 휘도차가
     * 0.02뿐이라 **풀 아래부터 탭바까지가 한 덩어리 검정**으로 읽힌다.
     * 미완성 화면의 인상 대부분이 여기서 왔다.
     *
     * 틴트로는 못 고친다 - 지역 틴트는 곱연산이라 밝게 만들 수 없다.
     * 그래서 굽는 시점에 픽셀로 다시 칠한다. 보스 림 라이트(YokaiSheetBaker)와
     * 같은 결정이다.
     *
     * ## 어떻게 칠하는가
     *
     * 원본 바위의 명암 변화를 질감으로 **재사용**한다 - 밝기를 [0,1]로 펴서
     * 어두운 흙과 밝은 흙 사이를 고르게 하므로, 팩이 그려둔 무늬의 자리가
     * 그대로 남고 색과 대비만 바뀐다. 여기에 셋을 얹는다:
     *
     *   - 세로 그라디언트: 표면 바로 아래가 가장 밝고 아래로 잠긴다. 바닥이
     *     UI 먹빛에 자연스럽게 이어져 "의도된 어두운 지면"이 된다
     *   - 결정론적 디더: 해시 노이즈로 픽셀을 흩는다. 평평한 띠가 흙이
     *     되는 것은 이 요철이다 (Random은 안 쓴다 - 구울 때마다 그림이
     *     바뀌면 커밋 디프가 소음이 된다)
     *   - 드문 자갈: 약 2% 픽셀을 한 단 밝게. 5배 확대에서 5px 돌알이 된다
     */
    public static class DirtTextureBaker
    {
        /**
         * @brief 스트립의 표면 아래(y < surfaceFromBottom)를 다시 칠한다.
         *
         * fillGapsAboveSurface를 켜면 표면 위쪽(풀 지대)의 **풀이 아닌**
         * 픽셀도 가장 밝은 깊이로 칠한다 - 봄숲은 풀 포기 사이가 바위색
         * 그대로라 "풀 사이 검은 구멍"으로 보였다. 풀 판정은 봄숲 빌더의
         * 실측 규칙(채도 0.3+, 색상 70~160도)과 같다.
         */
        public static void Apply(Color[] pixels, int width, int height, int surfaceFromBottom,
                                 Color dark, Color light, Color pebble,
                                 bool fillGapsAboveSurface)
        {
            // 원본 바위의 명암 범위를 실측한다. 봄숲(0.11~0.27)과 가을숲
            // (0.29~0.36)이 달라서, 고정 범위를 쓰면 한쪽은 질감이 펴지고
            // 다른 쪽은 전부 한 끝에 붙어 도로 평평해진다
            float minValue = 1f, maxValue = 0f;
            for (int y = 0; y < surfaceFromBottom; y++)
                for (int x = 0; x < width; x++)
                {
                    var c = pixels[y * width + x];
                    if (c.a < 0.999f) continue;
                    float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    if (v < minValue) minValue = v;
                    if (v > maxValue) maxValue = v;
                }
            if (maxValue - minValue < 0.01f) maxValue = minValue + 0.01f;

            for (int y = 0; y < height; y++)
            {
                bool belowSurface = y < surfaceFromBottom;
                if (!belowSurface && !fillGapsAboveSurface) continue;

                // 표면 위의 틈은 깊이 0(가장 밝은 흙)으로 취급한다
                float depth01 = belowSurface
                    ? 1f - (y + 0.5f) / surfaceFromBottom
                    : 0f;

                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    var source = pixels[index];
                    if (source.a < 0.999f) continue;
                    if (!belowSurface && IsGrass(source)) continue;

                    // 원본 명암을 실측 범위로 편다 - 팩이 그려둔 무늬가
                    // 자리 그대로 대비만 살아난다
                    float value = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
                    float texture = Mathf.InverseLerp(minValue, maxValue, value);

                    int h = Hash(x, y);
                    float dither = ((h & 15) / 15f - 0.5f) * 0.16f;

                    // 줄마다 미세하게 다른 지층. 가로로 이어지는 결이 흙을
                    // "쌓인 것"으로 읽게 한다
                    float strata = (((Hash(913, y) >> 3) & 7) / 7f - 0.5f) * 0.10f;

                    float surfaceLight = Mathf.Lerp(0.95f, 0.30f, depth01);
                    float shade = Mathf.Clamp01(
                        surfaceLight * (0.45f + 0.55f * texture) + dither + strata);

                    var color = Color.Lerp(dark, light, shade);

                    // 자갈. 표면 바로 밑과 최심부는 피한다 - 표면 밑은 풀
                    // 뿌리 자리고, 최심부는 UI로 잠기는 어둠이어야 한다
                    if (((h >> 8) & 255) < 5 && depth01 > 0.12f && depth01 < 0.85f)
                        color = Color.Lerp(color, pebble, 0.8f);

                    color.a = 1f;
                    pixels[index] = color;
                }
            }
        }

        /** 봄숲 빌더의 풀 실측과 같은 규칙. 두 곳이 다르면 경계가 어긋난다 */
        private static bool IsGrass(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(new Color(c.r, c.g, c.b), out h, out s, out v);
            return s > 0.3f && h * 360f > 70f && h * 360f < 160f;
        }

        /** 좌표 해시. 시드가 코드에 박혀 있어 언제 구워도 같은 그림이다 */
        private static int Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return h ^ (h >> 16);
            }
        }
    }
}
