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

        /**
         * @brief 굽는 크기는 표시 크기에서 온다. 여기에 배수를 따로 적지 않는다.
         *
         * 예전에는 설계 크기와 배수를 이 파일에도 복사해 두었다. 굽는 쪽과 그리는 쪽에
         * 같은 숫자가 두 벌 있으면 언젠가 한쪽만 바뀌고, 그 결과는 컴파일 에러가 아니라
         * **80pt로 구운 아틀라스를 48pt로 다운스케일해 그리는 흐릿한 글자**다. 화면을
         * 들여다보기 전까지 아무 신호도 없다.
         *
         * 이제 크기의 단일 출처는 {@link Onikiri.UI.PixelFontSizes} 하나다. 아래 두 값은
         * 정확히 표시 크기와 같고, 그래서 다운스케일이 구조적으로 불가능하다.
         */
        private static int GalmuriSamplingSize
        {
            get { return Onikiri.UI.PixelFontSizes.GalmuriAtlasSize; }
        }

        /**
         * @brief Thaleah 폰트가 그려진 크기.
         *
         * 함께 배포되는 레거시 비트맵 폰트에서 확인했다 (ThaleahFat.fontsettings 의
         * m_FontSize: 16).
         */
        private static int ThaleahSamplingSize
        {
            get { return Onikiri.UI.PixelFontSizes.ThaleahAtlasSize; }
        }

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

            // 설계 크기가 아니라 그 정수배로 샘플링한다.
            //
            // 외곽선을 정확히 11로 래스터하면 TMP가 쿼드를 만드는 메트릭보다 1픽셀 작은
            // 글리프 비트맵이 나온다. 그러면 모든 글리프가 11/10으로 늘어나고 Point
            // 샘플링이 행을 복제해 글자 형태가 눈에 띄게 깨진다. 픽셀 폰트의 외곽선은
            // 축 정렬 사각형이므로 정수배로 래스터하면 정확히 NxN 블록이 나오고 반올림
            // 오차가 무시할 수준이 된다. 표시 크기는 샘플링 크기와 1:1로 맞춘다.
            //
            // 배수를 얼마로 잡을지는 PixelFontSizes가 정한다. 여기서는 그 값을 그대로
            // 받아쓸 뿐이라, 구운 크기와 그리는 크기가 어긋날 여지가 없다.
            Build(GalmuriSourcePath, "Galmuri11", GalmuriSamplingSize, charset);

            // 캡션 아틀라스 (38단계 폰트 위계). 같은 서체를 3배(33pt)로 한 벌 더
            // 굽는다 - 44 아틀라스를 0.75배로 그리는 것은 Point 샘플링에서 글자가
            // 깨지므로, 작은 크기는 작은 아틀라스와 1:1이어야 한다
            Build(GalmuriSourcePath, "Galmuri11 Caption",
                  Onikiri.UI.PixelFontSizes.GalmuriCaptionSize, charset);

            Build(ThaleahSourcePath, "ThaleahFat", ThaleahSamplingSize, NumberCharset);

            BuildNameFont();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /** 남이 지은 이름을 그리는 한 벌. 경로가 곧 단일 출처다 */
        public const string NameFontPath = OutputFolder + "/Galmuri11 Name SDF.asset";

        /**
         * @brief **이 한 벌만 동적이다** (54단계 리더보드).
         *
         * 나머지 아틀라스가 정적인 것은 계약이다 - 화면에 나오는 모든 글자가
         * UIStrings.txt에 적혀 있고, 안 적힌 글자는 빌드 검사(VerifyGlyphCoverage)가
         * 잡는다. 그 계약이 성립하는 이유는 **문구를 우리가 쓰기 때문**이다.
         *
         * 랭킹표에는 남이 지은 이름이 뜬다. 한글 음절만 11,172자이고, 그중 무엇이
         * 올지는 우리가 알 수 없다 - 정적 아틀라스로는 원리상 덮을 수 없는 유일한
         * 자리다. 안 덮으면 증상이 특히 나쁘다: **남의 이름이 빈 네모로 보인다.**
         *
         * 그래서 이름 라벨만 이 폰트를 쓴다. 작성한 UI는 여전히 정적이고 검사도
         * 그대로 돈다 - 동적으로 새는 것은 "우리가 안 쓴 문자열"뿐이다.
         *
         * 대가: 원본 TTF가 빌드에 실린다(정적 아틀라스는 안 실린다). 폰트 하나
         * 값이고, 그 대가 없이 남의 이름을 그릴 방법이 없다.
         */
        public static TMP_FontAsset BuildNameFont()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(GalmuriSourcePath);
            if (sourceFont == null)
            {
                Debug.LogError("[Onikiri] Galmuri11.ttf missing - name font not built.");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NameFontPath);
            if (existing != null) AssetDatabase.DeleteAsset(NameFontPath);

            // 캡션 크기로 굽는다. 이름이 나오는 자리(랭킹 줄·내 이름)가 전부
            // 캡션 티어라, 다른 크기로 구우면 1:1이 깨져 이름만 흐릿해진다
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                Onikiri.UI.PixelFontSizes.GalmuriCaptionSize,
                AtlasPadding,
                GlyphRenderMode.RASTER_HINTED,
                AtlasWidth, AtlasHeight,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError("[Onikiri] CreateFontAsset failed for the name font.");
                return null;
            }

            fontAsset.name = "Galmuri11 Name SDF";

            AssetDatabase.CreateAsset(fontAsset, NameFontPath);
            AttachSubAssets(fontAsset, fontAsset);

            // Static으로 고정하지 **않는다** - 이 폰트의 존재 이유가 런타임
            // 래스터다. 위 Build()의 마지막 줄과 정확히 반대이고, 그 차이가
            // 이 에셋이 따로 있는 이유 전부다
            ApplyPointFiltering(fontAsset);
            ApplyBitmapShader(fontAsset);
            fontAsset.ReadFontAssetDefinition();

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            Debug.Log("[Onikiri] Name font (dynamic) built at "
                      + Onikiri.UI.PixelFontSizes.GalmuriCaptionSize + "pt -> " + NameFontPath);
            return fontAsset;
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

            string outputPath = OutputFolder + "/" + fontName + " SDF.asset";
            // 이름에 SDF가 붙는 것은 TMP 도구가 기대하는 관례일 뿐이고,
            // 내용물은 래스터 아틀라스다

            // 이미 있으면 **그 에셋을 그대로 쓴다.** 새로 만들어 갈아끼우지 않는다
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
            bool created = false;

            if (fontAsset == null)
            {
                fontAsset = CreateEmpty(sourceFont, fontName, samplingPointSize);
                if (fontAsset == null) return null;

                AssetDatabase.CreateAsset(fontAsset, outputPath);
                AttachSubAssets(fontAsset, fontAsset);
                created = true;
            }
            else if (NeedsRecreate(fontAsset, sourceFont, samplingPointSize))
            {
                // 샘플링 크기나 원본 폰트가 바뀌면 아틀라스 자체를 다시 만들어야
                // 한다. 이때는 GUID를 지킬 방법이 없으므로 경고를 남긴다 -
                // 씬을 다시 빌드해야 한다는 신호다
                Debug.LogWarning("[Onikiri] " + fontName + " changed sampling size or source font;"
                                 + " recreating the asset. Re-run Build Combat Content.");

                AssetDatabase.DeleteAsset(outputPath);
                fontAsset = CreateEmpty(sourceFont, fontName, samplingPointSize);
                if (fontAsset == null) return null;

                AssetDatabase.CreateAsset(fontAsset, outputPath);
                AttachSubAssets(fontAsset, fontAsset);
                created = true;
            }

            // 글리프를 **제자리에서** 다시 채운다.
            //
            // 여기가 이번 수정의 핵심이다. 예전에는 매번 CreateFontAsset으로 새
            // 오브젝트를 만들어 파일을 갈아끼웠고, 그러면 폰트 에셋과 그 머티리얼의
            // GUID/fileID가 전부 바뀌어 씬의 TMP 참조가 끊겼다. 11단계에서
            // CopySerialized로 내용만 옮겨봤지만 그때는 글리프 테이블과 아틀라스가
            // 어긋나 글자가 뒤섞였다 - 셋(테이블·아틀라스·머티리얼)이 한 벌인데
            // 그중 일부만 옮겼기 때문이다.
            //
            // TMP에는 그 셋을 한꺼번에 다루는 API가 이미 있다. 지우고 다시 채우면
            // 오브젝트 정체성은 그대로 두고 내용만 바뀐다.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.ClearFontAssetData(true);

            string missing;
            bool allAdded = fontAsset.TryAddCharacters(charset, out missing);
            if (!allAdded && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("[Onikiri] " + fontName + " is missing glyphs for: " + missing);

            // 고정시킨다. 정적 에셋은 런타임에 새 글리프를 몰래 래스터하지 않는다.
            // 그렇지 않으면 위의 모든 설정을 우회하게 된다
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            ApplyPointFiltering(fontAsset);
            ApplyBitmapShader(fontAsset);

            // 테이블을 다시 채운 뒤에는 조회용 사전을 다시 세워야 한다. 이것을
            // 빼먹으면 에디터가 옛 매핑을 들고 있다가 다음 도메인 리로드에서야
            // 정상으로 돌아온다
            fontAsset.ReadFontAssetDefinition();

            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.material != null) EditorUtility.SetDirty(fontAsset.material);
            foreach (var texture in fontAsset.atlasTextures)
                if (texture != null) EditorUtility.SetDirty(texture);

            AssetDatabase.SaveAssets();

            int glyphs = fontAsset.glyphTable != null ? fontAsset.glyphTable.Count : 0;
            int pages = fontAsset.atlasTextures != null ? fontAsset.atlasTextures.Length : 0;
            Debug.Log(string.Format(
                "[Onikiri] Font '{0}': {1} glyphs, {2} atlas page(s) at {3}pt, RASTER_HINTED, padding {4} -> {5}{6}",
                fontAsset.name, glyphs, pages, samplingPointSize, AtlasPadding, outputPath,
                created ? "  (new asset)" : "  (in place, GUID kept)"));

            // 페이지가 늘어나면 머티리얼도 늘어나고 표시 크기 규칙이 조용히 깨진다.
            // 아틀라스를 키워야 한다는 신호이므로 눈에 띄게 남긴다
            if (pages > 1)
                Debug.LogWarning("[Onikiri] " + fontAsset.name + " needed " + pages +
                                 " atlas pages - raise AtlasWidth/AtlasHeight.");

            return fontAsset;
        }

        /** 글리프가 비어 있는 폰트 에셋 하나. 채우는 것은 호출부가 한다 */
        private static TMP_FontAsset CreateEmpty(Font sourceFont, string fontName, int samplingPointSize)
        {
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize,
                AtlasPadding,
                GlyphRenderMode.RASTER_HINTED,
                AtlasWidth, AtlasHeight,
                AtlasPopulationMode.Dynamic,
                false);

            if (fontAsset == null)
            {
                Debug.LogError("[Onikiri] CreateFontAsset failed for " + fontName);
                return null;
            }

            fontAsset.name = fontName + " SDF";
            return fontAsset;
        }

        /**
         * @brief 제자리 갱신으로는 못 바꾸는 것이 바뀌었는가.
         *
         * 샘플링 크기와 원본 폰트는 아틀라스를 굽는 조건 자체라 다시 만들어야 한다.
         * 문자셋이 바뀌는 것은 여기 해당하지 않는다 - 그것이 제자리 갱신으로
         * 처리되어야 하는 정확히 그 경우다.
         */
        private static bool NeedsRecreate(TMP_FontAsset fontAsset, Font sourceFont, int samplingPointSize)
        {
            // sourceFontFile은 비교하지 않는다.
            //
            // TMP는 atlasPopulationMode를 Static으로 바꾸는 순간 이 참조를 null로
            // 만든다 - 정적 아틀라스는 원본 TTF 없이 동작하므로 빌드에 폰트 파일을
            // 딸려 보내지 않기 위해서다. 그래서 "원본이 달라졌는가"로 쓰면 항상
            // 참이 되고, 매번 에셋을 다시 만들게 된다. 실제로 그렇게 동작하고 있었다.
            //
            // 출력 경로 하나에 원본 하나가 대응하는 구조라 원본이 바뀌는 경우는
            // 코드를 고칠 때뿐이고, 그때는 아래 조건들이 함께 바뀐다.
            if (Mathf.Abs(fontAsset.faceInfo.pointSize - samplingPointSize) > 0.01f) return true;
            if (fontAsset.atlasPadding != AtlasPadding) return true;
            if (fontAsset.atlasWidth != AtlasWidth || fontAsset.atlasHeight != AtlasHeight) return true;
            return false;
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
         *
         * **기존 에셋이 있으면 그 안을 덮어쓴다. 지우고 다시 만들지 않는다.**
         *
         * 예전에는 DeleteAsset -> CreateAsset 이었다. 그러면 같은 경로에 새 GUID가
         * 생기고, 씬과 프리팹의 TMP 컴포넌트가 들고 있던 폰트 참조가 전부 끊긴다.
         * 증상이 고약하다 - 라틴 문자는 TMP 기본 폴백으로 멀쩡히 나오고 한글만
         * □가 되어, 폰트 참조가 끊긴 것이 아니라 글리프를 안 구운 것처럼 보인다.
         * 9단계에서 이것 때문에 "폰트를 다시 구우면 반드시 씬을 다시 빌드할 것"이라는
         * 규칙을 문서에 적었는데, 규칙으로 남길 문제가 아니라 고칠 문제였다.
         *
         * EditorUtility.CopySerialized로 내용만 옮기면 파일도 GUID도 그대로다.
         */
        /**
         * @brief 아틀라스와 머티리얼을 폰트 에셋의 자식으로 매단다.
         *
         * source는 방금 구운 것, owner는 파일에 실제로 존재하는 에셋이다. 재빌드에서는
         * 둘이 다르다 - 내용은 새로 구운 쪽에서 오고 파일 정체성은 기존 쪽이 갖는다.
         */
        private static void AttachSubAssets(TMP_FontAsset source, TMP_FontAsset owner)
        {
            if (source.atlasTextures != null)
            {
                for (int i = 0; i < source.atlasTextures.Length; i++)
                {
                    var texture = source.atlasTextures[i];
                    if (texture == null) continue;
                    texture.name = owner.name + " Atlas" + (i > 0 ? " " + i : string.Empty);
                    AssetDatabase.AddObjectToAsset(texture, owner);
                }
            }

            if (source.material != null)
            {
                source.material.name = owner.name + " Material";
                AssetDatabase.AddObjectToAsset(source.material, owner);
            }

            // 복사된 owner가 source의 서브에셋을 가리키도록 다시 연결한다.
            // CopySerialized는 참조를 그대로 복사하지만, 위에서 이름과 소유자를
            // 바꿨으므로 여기서 한 번 더 못 박아 둔다
            owner.atlasTextures = source.atlasTextures;
            owner.material = source.material;
        }

    }
}
