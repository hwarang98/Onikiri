using System.IO;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 뒤를 받치는 두 장을 굽는다 — 하늘 채움과 성장 패널 종이.
     *
     * 둘 다 **내용이 없는 바탕**이라는 공통점이 있다. 그림이 아니라 다른 것을 올려놓기
     * 위한 면이고, 그래서 흰색으로 굽고 색은 런타임 틴트가 정한다(벚꽃잎·타격 불꽃과
     * 같은 방식). 지역마다 다른 하늘색을 한 시트로 쓸 수 있는 이유다.
     */
    public static class BackdropTextureBuilder
    {
        private const string Folder = "Assets/_Project/Art/Backgrounds";

        public const string SkyFillPath = Folder + "/SkyFill.png";
        public const string WashiPath = Folder + "/WashiPaper.png";
        public const string SakuraSilhouettePath = Folder + "/SakuraSilhouette.png";

        // ================================================================ 하늘 채움

        /**
         * @brief 하늘 채움 한 장. **세로로 절대 반복되지 않을 만큼 길다.**
         *
         * ## 왜 새로 굽는가 — 배경이 가로로 갈라져 보이던 원인
         *
         * 24단계까지 하늘 채움은 그 지역의 **먼 배경 시트를 그대로** 썼고,
         * `BattleStageLayout`이 그것을 카메라 크기로 타일링했다. 타일링은 세로로도
         * 반복된다 - 가을숲 3.png(180px)는 9:19.5 화면에서 **세로로 3.44번** 깔렸다.
         *
         * 그 시트에는 아래쪽에 창백한 나무 줄기 띠가 그려져 있다. 세로로 반복되면
         * **그 줄기 띠가 화면 중턱에 다시 나타나** 배경이 위아래로 갈린 것처럼 보인다.
         * 이음매의 색은 맞았다(맨 윗줄과 맨 아랫줄 색차 0.0000) - 색 단차가 아니라
         * **내용의 반복**이었다.
         *
         * 그래서 채움은 그림이 아니라 **면**이어야 한다. 내용이 없으면 반복해도 이음매가
         * 없다. 여기에 더해 지원 비율(9:21, 카메라 17.36u + overscan 19.36u)보다 길게
         * 구워서 애초에 반복이 일어나지 않게 한다.
         */
        private const int SkyWidth = 64;

        /** 22u. 가장 긴 지원 비율의 카메라(19.36u)보다 길어 세로 반복이 없다 */
        private const int SkyHeight = 704;

        /**
         * @brief 위로 갈수록 어두워지는 정도.
         *
         * 완전히 평평하면 하늘이 아니라 색종이로 보인다. 실제 하늘도 천정에 가까울수록
         * 짙다. 다만 아주 약하게 - 이 면 위에 스크롤하는 하늘 장이 한 겹 더 깔리므로
         * 여기서 강한 그라디언트를 주면 두 겹이 어긋나 보인다.
         */
        private const float SkyTopScale = 0.94f;

        // ================================================================ 성장 패널 종이

        /**
         * @brief 화지(和紙) 질감. 64x64 타일이다.
         *
         * 성장 패널 바탕이 **라이브 씬을 비추지 않게** 막는 것이 첫째 목적이고, 그
         * 위에 종이 결을 얹는 것이 둘째다. 아무 무늬 없는 단색은 UI가 아니라 구멍처럼
         * 보인다.
         *
         * 값 범위를 0.90~1.00으로 좁게 잡는다. 곱연산으로 먹빛을 입히므로 원본의 대비가
         * 그대로 증폭되는데, 여기서 세게 잡으면 **행 카드와 아이콘 경계가 질감에 묻힌다** -
         * 프롬프트가 경고한 그 함정이다. 글씨를 방해하지 않는 것이 질감보다 우선이다.
         */
        private const int WashiSize = 64;

        /** 결정론적으로 굽는다. 다시 구울 때마다 무늬가 바뀌면 diff가 무의미해진다 */
        private const int WashiSeed = 24;

        [MenuItem("Onikiri/Art/Build Backdrop Textures")]
        public static void BuildAll()
        {
            BuildSkyFill();
            BuildWashi();
            BuildSakuraSilhouette();
            AssetDatabase.SaveAssets();
        }

        public static void BuildSkyFill()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(SkyFillPath) != null) return;

            var pixels = new Color[SkyWidth * SkyHeight];

            for (int y = 0; y < SkyHeight; y++)
            {
                float up = y / (float)(SkyHeight - 1);
                float v = Mathf.Lerp(1f, SkyTopScale, up);
                var color = new Color(v, v, v, 1f);
                for (int x = 0; x < SkyWidth; x++) pixels[y * SkyWidth + x] = color;
            }

            Write(SkyFillPath, SkyWidth, SkyHeight, pixels);
            Debug.Log("[Onikiri] Sky fill baked: " + SkyWidth + "x" + SkyHeight + " -> " + SkyFillPath);
        }

        public static void BuildWashi()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(WashiPath) != null) return;

            var state = Random.state;
            Random.InitState(WashiSeed);

            var pixels = new Color[WashiSize * WashiSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;

            // 결이 굵은 종이라 얼룩부터 깐다. 값은 0.955~1.0
            for (int y = 0; y < WashiSize; y++)
            for (int x = 0; x < WashiSize; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.11f + 3.7f, y * 0.11f + 8.2f);
                float v = Mathf.Lerp(0.955f, 1f, n);
                pixels[y * WashiSize + x] = new Color(v, v, v, 1f);
            }

            // 화지의 특징은 결(섬유)이다. 가로로 긴 실이 드문드문 지나간다.
            // 타일 경계를 넘기지 않으려면 가로줄은 폭 전체를 지나가야 한다
            for (int i = 0; i < 7; i++)
            {
                int y = Random.Range(0, WashiSize);
                float v = 0.90f + Random.value * 0.05f;
                for (int x = 0; x < WashiSize; x++)
                {
                    // 실이 균일하면 줄무늬로 보인다. 중간중간 끊는다
                    if (Random.value < 0.22f) continue;
                    Darken(pixels, x, y, v);
                }
            }

            // 세로 결은 더 드물다
            for (int i = 0; i < 3; i++)
            {
                int x = Random.Range(0, WashiSize);
                float v = 0.93f + Random.value * 0.05f;
                for (int y = 0; y < WashiSize; y++)
                {
                    if (Random.value < 0.35f) continue;
                    Darken(pixels, x, y, v);
                }
            }

            Random.state = state;

            Write(WashiPath, WashiSize, WashiSize, pixels);
            Debug.Log("[Onikiri] Washi paper baked: " + WashiSize + "x" + WashiSize + " -> " + WashiPath);
        }

        // ================================================================ 벚꽃 실루엣

        /**
         * @brief 화지 위에 얹는 벚꽃 가지 실루엣 (39단계, 25단계에 보류했던 것).
         *
         * ## 왜 tone-on-tone인가
         *
         * 하단 UI의 언어는 "고정 먹빛 화지"다. 여기에 밝은 장식을 얹으면 장식이
         * 내용과 경쟁한다 - 목록 화면에서 눈이 가야 할 곳은 행 카드와 숫자다.
         * 그래서 **흰색으로 굽고 배치할 때 먹빛보다 반 톤 밝은 색으로 틴트**한다.
         * 화지 결(10% 요철)보다 약간 진한 정도라, 시선이 머물면 보이고 훑을 때는
         * 사라진다.
         *
         * ## 왜 오른쪽 위 모서리인가
         *
         * 패널 왼쪽 위는 제목("대장간"·"퀘스트")의 자리다. 가지가 오른쪽 위에서
         * 드리우면 텍스트가 없는 여백을 따라 내려오고, 행 카드가 그 위를 덮는
         * 부분은 그냥 안 보인다 - 배경 모티프는 가려져도 거짓말이 되지 않는다.
         * 전면에 깔지 않는 이유는 25단계에 적어뒀다(가독성).
         *
         * 결정론적으로 굽는다(화지와 같은 이유 - diff가 의미를 가져야 한다).
         */
        private const int SilhouetteWidth = 240;
        private const int SilhouetteHeight = 100;
        private const int SilhouetteSeed = 39;

        /** 배치 틴트. PanelInk(#221D30)에서 반 톤 들어올린 먹빛 */
        public static readonly Color SilhouetteTint = new Color32(0x31, 0x2A, 0x44, 0xFF);

        public static void BuildSakuraSilhouette()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(SakuraSilhouettePath) != null) return;

            var state = Random.state;
            Random.InitState(SilhouetteSeed);

            var pixels = new Color[SilhouetteWidth * SilhouetteHeight];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 1f, 0f);

            // 본가지: 오른쪽 위 모서리에서 왼쪽 아래로 드리우는 곡선.
            // 굵기는 뿌리 쪽 4px에서 끝 1px로 가늘어진다
            var root = new Vector2(SilhouetteWidth - 1, SilhouetteHeight - 6);
            var control = new Vector2(SilhouetteWidth * 0.55f, SilhouetteHeight * 0.95f);
            var tip = new Vector2(SilhouetteWidth * 0.10f, SilhouetteHeight * 0.38f);

            for (int step = 0; step <= 240; step++)
            {
                float t = step / 240f;
                var p = Bezier(root, control, tip, t);
                StampDisc(pixels, p, Mathf.Lerp(4f, 1.2f, t));
            }

            // 잔가지 + 꽃송이. 잔가지는 본가지에서 아래로 처지고, 끝에 꽃이 모인다
            float[] forks = { 0.22f, 0.40f, 0.58f, 0.76f, 0.92f };
            foreach (var at in forks)
            {
                var from = Bezier(root, control, tip, at);
                // 아래로 드리운다 - 벚가지는 처지는 것이 인상의 절반이다
                var dir = new Vector2(-(0.4f + Random.value * 0.5f), -(0.5f + Random.value * 0.6f)).normalized;
                float length = 14f + Random.value * 14f;

                var end = from + dir * length;
                for (int step = 0; step <= 24; step++)
                {
                    float t = step / 24f;
                    // 살짝 휘어진다. 직선 잔가지는 철사로 보인다
                    var p = Vector2.Lerp(from, end, t) + new Vector2(0f, -6f * t * t);
                    StampDisc(pixels, p, Mathf.Lerp(1.8f, 0.9f, t));
                }

                StampBlossomCluster(pixels, end + new Vector2(0f, -6f));
            }

            // 본가지 끝에도 한 송이 - 가지가 허공에서 끊기지 않는다
            StampBlossomCluster(pixels, tip);

            Random.state = state;

            Write(SakuraSilhouettePath, SilhouetteWidth, SilhouetteHeight, pixels);
            Debug.Log("[Onikiri] Sakura silhouette baked: " + SilhouetteWidth + "x"
                      + SilhouetteHeight + " -> " + SakuraSilhouettePath);
        }

        /** 꽃송이 = 중심을 비운 다섯 꽃잎. 원 하나면 열매로 읽힌다 */
        private static void StampBlossomCluster(Color[] pixels, Vector2 center)
        {
            int petals = 4 + (int)(Random.value * 3f);
            for (int i = 0; i < petals; i++)
            {
                float angle = (i / (float)petals + Random.value * 0.08f) * Mathf.PI * 2f;
                float distance = 3.2f + Random.value * 1.6f;
                var petal = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                StampDisc(pixels, petal, 2.1f + Random.value * 0.8f);
            }
        }

        private static void StampDisc(Color[] pixels, Vector2 center, float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(SilhouetteWidth - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(SilhouetteHeight - 1, Mathf.CeilToInt(center.y + radius));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                if (dx * dx + dy * dy > radius * radius) continue;
                pixels[y * SilhouetteWidth + x] = Color.white;
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /**
         * @brief 패널 바탕에 가지 실루엣 하나를 앉힌다. 모든 하단 패널이 이 헬퍼
         * 하나를 지나야 "한 앱"으로 읽힌다.
         *
         * 형제 순서상 바탕 바로 다음(맨 앞)에 넣는다 - 행 카드·헤더·글자는 전부
         * 그 뒤에 만들어져 실루엣을 덮는다. 실루엣은 흰색으로 구워졌으므로 틴트가
         * 그대로 최종 색이다(멀티플라이 함정 없음 - 곱할 바탕이 흰색이다).
         */
        public const string BranchName = "SakuraBranch";

        public static void AddSakuraBranch(Transform panel)
        {
            if (panel == null) return;

            BuildSakuraSilhouette();

            var existing = panel.Find(BranchName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(BranchName, typeof(RectTransform));
            go.transform.SetParent(panel, false);
            go.transform.SetAsFirstSibling();

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            // 2배 정수 배율(UiSkin.PixelScale과 같은 규칙). 240x100 아트가
            // 480x200 캔버스 픽셀 - 1080 폭의 44%, 오른쪽 위 여백에 걸친다
            rect.sizeDelta = new Vector2(SilhouetteWidth * 2f, SilhouetteHeight * 2f);
            rect.anchoredPosition = Vector2.zero;

            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SakuraSilhouettePath);
            image.color = SilhouetteTint;
            image.raycastTarget = false;
        }

        private static void Darken(Color[] pixels, int x, int y, float value)
        {
            int i = y * WashiSize + x;
            float v = Mathf.Min(pixels[i].r, value);
            pixels[i] = new Color(v, v, v, 1f);
        }

        private static void Write(string path, int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Onikiri.Core.DisplayConfig.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;   // Tiled 드로우가 요구한다
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
