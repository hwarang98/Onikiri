using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Renders sample text through the real font asset and writes it to disk, so the
    /// rasterisation can be checked by eye.
    ///
    /// The failure this catches is specific: if the atlas was baked as SDF, or the atlas
    /// texture ended up on bilinear filtering, or the font is drawn at a size that is not a
    /// whole multiple of its 11px design size, the glyph edges go soft. That is invisible
    /// in the inspector and obvious in a magnified render.
    /// </summary>
    public static class FontProofSheet
    {
        private const string FontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";
        private const string OutputPath = "Assets/Screenshots/font_proof.png";

        private static readonly string[] Lines =
        {
            "귀참 키우기",
            "공격력 강화",
            "12.3K"
        };

        [MenuItem("Onikiri/Art/Render Font Proof Sheet")]
        public static void Render()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) { Debug.LogError("[Onikiri] Font asset missing: " + FontPath); return; }

            const int fontSize = 33;   // 11 x 3
            const int width = 420;
            const int height = 200;
            const int zoom = 3;        // magnify afterwards to inspect the pixel edges

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

                var label = labelObject.AddComponent<TextMeshProUGUI>();
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.fontSize = fontSize;
                label.text = Lines[i];
                label.color = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
                label.alignment = TextAlignmentOptions.Left;

                var rect = (RectTransform)labelObject.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(width - 40, 56);
                rect.anchoredPosition = new Vector2(20, -20 - i * 56);
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

            // Nearest-neighbour magnify so the pixel edges survive the inspection itself.
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
            Debug.Log("[Onikiri] Font proof rendered at " + fontSize + "px, magnified " + zoom + "x -> " + OutputPath);
        }
    }
}
