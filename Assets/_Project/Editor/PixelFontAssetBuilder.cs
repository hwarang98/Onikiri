using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Onikiri.EditorTools
{
    /**
     * @brief 픽셀 폰트용 TMP 폰트 에셋을 굽는다.
     *
     * TMP는 기본이 SDF 렌더링인데 여기서는 정확히 틀린 선택이다. SDF는 글리프 경계를
     * 해석적으로 복원하며 안티에일리어싱을 넣기 때문에, 11px 비트맵 서체가 흐릿하게
     * 나오고 주변 픽셀 아트와 톤이 어긋난다. 중요한 설정은 다음과 같다:
     *
     *   샘플링 크기   = 설계 크기의 정수배 (자동 계산 금지)
     *   패딩          = 1  (아래 AtlasPadding 주석 참고)
     *   렌더 모드     = RASTER_HINTED (픽셀 격자에 힌팅된 1비트 커버리지)
     *   아틀라스 필터 = Point (이중선형이면 그리는 시점에 다시 뭉갠다)
     *   머티리얼 셰이더 = TextMeshPro/Bitmap. Distance Field 계열이 아니다
     *
     * 표시 크기는 아틀라스를 구운 크기의 정수배여야 한다. 아니면 글리프 격자가 화면
     * 픽셀 사이에 놓여 선명함이 사라진다. Onikiri.UI.PixelFontSizes 참고.
     */
    public static class PixelFontAssetBuilder
    {
        private const string FontFolder = "Assets/_Project/Art/Fonts";
        private const string OutputFolder = "Assets/_Project/Art/Fonts";

        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        /**
         * @brief 글리프마다 투명 여백 1픽셀.
         *
         * 래스터 아틀라스에는 퍼짐 공간이 필요한 distance field가 없으니 패딩 0이
         * 직관적인 선택이다. 그런데 TMP는 폰트 메트릭으로 글리프 쿼드 크기를 정하면서
         * 패킹된 rect를 샘플링한다. 패딩 0에서는 이 둘이 1픽셀 어긋난다
         * (메트릭 10x11인 글리프가 9x10 rect로 패킹된다). 그러면 모든 글리프가 약 10/9로
         * 늘어나고, Point 샘플링이 픽셀 행을 복제하거나 누락시켜 글자가 깨지고 겹쳐 보인다.
         *
         * 패딩 1이면 rect = 메트릭 + 2 가 되어 매핑이 정확해진다. 글리프마다 투명 여백
         * 1픽셀을 쓸 뿐 흐려지는 것은 없다.
         */
        private const int AtlasPadding = 1;

        /** Galmuri11 폰트가 그려진 크기 */
        public const int GalmuriDesignSize = 11;

        /**
         * @brief 아틀라스를 래스터하는 정수 배수.
         *
         * 표시 크기는 이 값(33)이거나 그 배수여야 한다.
         */
        public const int GalmuriSampleMultiple = 3;

        /**
         * @brief Thaleah 폰트가 그려진 크기.
         *
         * 함께 배포되는 레거시 비트맵 폰트에서 확인했다 (ThaleahFat.fontsettings 의
         * m_FontSize: 16).
         */
        public const int ThaleahDesignSize = 16;
        public const int ThaleahSampleMultiple = 3;

        /**
         * @brief Thaleah는 데미지 팝업에만 쓰는 라틴 디스플레이 서체다.
         *
         * 숫자, 구분자, NumberFormatter가 내는 자릿수 접미사만 있으면 된다. 전체 UI
         * 문자셋을 먹이면 없는 한글 글리프마다 경고만 쌓인다.
         */
        private const string NumberCharset =
            "0123456789.,+-x" +
            "KMBT" +
            "abcdefghijklmnopqrstuvwxyz";

        [MenuItem("Onikiri/Art/Build Pixel Font Assets")]
        public static void BuildAll()
        {
            FontCharsetBuilder.Rebuild();
            var charset = FontCharsetBuilder.LoadCharset();

            // 설계 크기가 아니라 그 3배로 샘플링한다.
            //
            // 외곽선을 정확히 11로 래스터하면 TMP가 쿼드를 만드는 메트릭보다 1픽셀 작은
            // 글리프 비트맵이 나온다. 그러면 모든 글리프가 11/10으로 늘어나고 Point
            // 샘플링이 행을 복제해 글자 형태가 눈에 띄게 깨진다. 픽셀 폰트의 외곽선은
            // 축 정렬 사각형이므로 정수배로 래스터하면 정확히 NxN 블록이 나오고 반올림
            // 오차가 무시할 수준이 된다. 표시 크기는 샘플링 크기와 1:1로 맞춘다.
            Build(GalmuriSourcePath, "Galmuri11",
                  GalmuriDesignSize * GalmuriSampleMultiple, charset);

            Build(ThaleahSourcePath, "ThaleahFat",
                  ThaleahDesignSize * ThaleahSampleMultiple, NumberCharset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private const string GalmuriSourcePath = FontFolder + "/Galmuri11.ttf";

        /** Asset Store 패키지에서 임포트된 위치 그대로 둔 경로 */
        private const string ThaleahSourcePath =
            "Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF.ttf";

        /**
         * @brief 폰트 하나를 굽는다.
         *
         * samplingPointSize는 폰트가 그려진 크기의 정수배여야 하고, 표시 크기는 그것과
         * 같거나 그 배수여야 한다.
         */
        public static TMP_FontAsset Build(string sourcePath, string fontName,
                                          int samplingPointSize, string charset)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                Debug.LogError("[Onikiri] Font not found: " + sourcePath);
                return null;
            }

            int designPointSize = samplingPointSize;
            string outputPath = OutputFolder + "/" + fontName + " SDF.asset";
            // 이름에 SDF가 붙는 것은 TMP 도구가 기대하는 관례일 뿐이고,
            // 내용물은 래스터 아틀라스다

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                designPointSize,
                AtlasPadding,
                GlyphRenderMode.RASTER_HINTED,
                AtlasWidth, AtlasHeight,
                AtlasPopulationMode.Dynamic,         // dynamic while we add glyphs
                false);

            if (fontAsset == null)
            {
                Debug.LogError("[Onikiri] CreateFontAsset failed for " + fontName);
                return null;
            }

            fontAsset.name = fontName + " SDF";

            string missing;
            bool allAdded = fontAsset.TryAddCharacters(charset, out missing);
            if (!allAdded && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("[Onikiri] " + fontName + " is missing glyphs for: " + missing);

            // 고정시킨다. 정적 에셋은 런타임에 새 글리프를 몰래 래스터하지 않는다.
            // 그렇지 않으면 위의 모든 설정을 우회하게 된다
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            ApplyPointFiltering(fontAsset);
            ApplyBitmapShader(fontAsset);

            SaveWithSubAssets(fontAsset, outputPath);

            int glyphs = fontAsset.glyphTable != null ? fontAsset.glyphTable.Count : 0;
            int pages = fontAsset.atlasTextures != null ? fontAsset.atlasTextures.Length : 0;
            Debug.Log(string.Format(
                "[Onikiri] Font '{0}': {1} glyphs, {2} atlas page(s) at {3}pt, RASTER_HINTED, padding 0 -> {4}",
                fontAsset.name, glyphs, pages, designPointSize, outputPath));

            return fontAsset;
        }

        private static void ApplyPointFiltering(TMP_FontAsset fontAsset)
        {
            if (fontAsset.atlasTextures == null) return;

            foreach (var texture in fontAsset.atlasTextures)
            {
                if (texture == null) continue;
                texture.filterMode = FilterMode.Point;
                texture.anisoLevel = 0;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
        }

        private static void ApplyBitmapShader(TMP_FontAsset fontAsset)
        {
            var shader = Shader.Find("TextMeshPro/Bitmap");
            if (shader == null)
            {
                Debug.LogWarning("[Onikiri] TextMeshPro/Bitmap shader not found; leaving default material.");
                return;
            }

            // 새로 만들지 않고 TMP가 이미 만들어 둔 머티리얼의 셰이더만 교체한다.
            // 새 Material은 _TextureWidth / _TextureHeight / _GradientScale 이 기본값으로
            // 시작하는데, TMP는 그 값들로 글리프 UV를 계산한다. 값이 틀리면 글리프가
            // 밀려 그려지고 옆 글리프가 번져 들어온다
            var material = fontAsset.material;
            if (material == null)
            {
                material = new Material(shader);
                fontAsset.material = material;
            }
            else
            {
                material.shader = shader;
            }

            material.name = fontAsset.name + " Material";

            // ShaderUtilities.ID_* 가 아니라 프로퍼티 이름을 쓴다. 그 캐시된 ID들은
            // 지연 초기화라 여기서는 아직 0이고, SetFloat(0, ...) 은 조용히 아무것도
            // 쓰지 않아 아틀라스 크기가 0으로 남는다. 글리프가 깨지는 원인이 바로 이것이다
            material.SetTexture("_MainTex", fontAsset.atlasTexture);
            material.SetFloat("_TextureWidth", fontAsset.atlasWidth);
            material.SetFloat("_TextureHeight", fontAsset.atlasHeight);
            material.SetFloat("_GradientScale", fontAsset.atlasPadding + 1);
        }

        /**
         * @brief 아틀라스 텍스처와 머티리얼을 폰트 에셋 안에 중첩해 저장한다.
         *
         * 폰트 전체가 파일 하나가 되어 옮기거나 지우기 쉬워진다.
         */
        private static void SaveWithSubAssets(TMP_FontAsset fontAsset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(fontAsset, path);

            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var texture = fontAsset.atlasTextures[i];
                    if (texture == null) continue;
                    texture.name = fontAsset.name + " Atlas" + (i > 0 ? " " + i : string.Empty);
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
