using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 실제 폰트 에셋으로 샘플 텍스트를 렌더해 디스크에 저장한다.
     *
     * 래스터 결과를 눈으로 확인하기 위한 것이다.
     *
     * 잡아내려는 실패는 구체적이다. 아틀라스가 SDF로 구워졌거나, 아틀라스 텍스처가
     * 이중선형 필터로 들어갔거나, 설계 크기의 정수배가 아닌 크기로 그리면 글리프
     * 가장자리가 뭉개진다. 인스펙터에서는 보이지 않고 확대 렌더에서는 명확하다.
     */
    public static class FontProofSheet
    {
        private const string GalmuriPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";
        private const string ThaleahPath = "Assets/_Project/Art/Fonts/ThaleahFat SDF.asset";
        private const string OutputPath = "Assets/Screenshots/font_proof.png";

        private struct ProofLine
        {
            public string Text;
            public bool UseThaleah;
            public float Size;
        }

        /**
         * @brief 검증용 표본.
         *
         * 크기는 PixelFontSizes에서 가져온다. 여기에 숫자를 직접 적으면 폰트를 다시
         * 구운 뒤 검증 시트만 옛 크기로 남아, 정작 확인해야 할 리샘플 흐림을
         * 이 시트가 스스로 만들어 보여주게 된다.
         */
        private static readonly ProofLine[] Lines =
        {
            new ProofLine { Text = "귀참 키우기",  UseThaleah = false, Size = Onikiri.UI.PixelFontSizes.GalmuriSmall },
            new ProofLine { Text = "공격력 강화",  UseThaleah = false, Size = Onikiri.UI.PixelFontSizes.GalmuriSmall },
            new ProofLine { Text = "12.3K",        UseThaleah = false, Size = Onikiri.UI.PixelFontSizes.GalmuriSmall },
            new ProofLine { Text = "12.3K  4.7M",  UseThaleah = true,  Size = Onikiri.UI.PixelFontSizes.ThaleahDamage }
        };

        [MenuItem("Onikiri/Art/Render Font Proof Sheet")]
        public static void Render()
        {
            var galmuri = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriPath);
            var thaleah = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaleahPath);
            if (galmuri == null) { Debug.LogError("[Onikiri] Font asset missing: " + GalmuriPath); return; }

            // 줄 높이는 가장 큰 표본에 맞춘다. 폰트를 키우면 시트도 함께 커져야
            // 글자가 잘리지 않는다
            int lineHeight = Mathf.CeilToInt(Onikiri.UI.PixelFontSizes.ThaleahDamage * 1.2f);
            int width = 640;
            int height = lineHeight * Lines.Length + 40;
            const int zoom = 2;        // 확대해서 픽셀 경계를 눈으로 확인한다

            var root = new GameObject("~FontProof");
            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(root.transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            var cameraObject = new GameObject("ProofCamera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x2A, 0x27, 0x40, 0xFF);
            camera.cullingMask = ~0;

            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;

            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(width, height);

            for (int i = 0; i < Lines.Length; i++)
            {
                var labelObject = new GameObject("Line" + i, typeof(RectTransform));
                labelObject.transform.SetParent(canvasObject.transform, false);

                var chosen = Lines[i].UseThaleah && thaleah != null ? thaleah : galmuri;

                var label = labelObject.AddComponent<TextMeshProUGUI>();
                label.font = chosen;
                label.fontSharedMaterial = chosen.material;
                label.fontSize = Lines[i].Size;
                label.text = Lines[i].Text;
                label.color = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
                label.alignment = TextAlignmentOptions.Left;

                var rect = (RectTransform)labelObject.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(width - 40, lineHeight);
                rect.anchoredPosition = new Vector2(20, -20 - i * lineHeight);
            }

            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            camera.targetTexture = renderTexture;
            camera.Render();

            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = previous;

            // 최근접 이웃으로 확대한다. 그래야 검사 과정 자체가 픽셀 경계를 뭉개지 않는다
            var zoomed = new Texture2D(width * zoom, height * zoom, TextureFormat.RGBA32, false);
            var source = readback.GetPixels32();
            var target = new Color32[width * zoom * height * zoom];
            for (int y = 0; y < height * zoom; y++)
            {
                int sy = y / zoom;
                for (int x = 0; x < width * zoom; x++)
                    target[y * width * zoom + x] = source[sy * width + x / zoom];
            }
            zoomed.SetPixels32(target);
            zoomed.Apply(false);

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, zoomed.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(zoomed);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);

            AssetDatabase.Refresh();
            Debug.Log(string.Format("[Onikiri] Font proof rendered (Galmuri {0}px, Thaleah {1}px), magnified {2}x -> {3}",
                Onikiri.UI.PixelFontSizes.GalmuriSmall, Onikiri.UI.PixelFontSizes.ThaleahDamage, zoom, OutputPath));
        }
    }
}
