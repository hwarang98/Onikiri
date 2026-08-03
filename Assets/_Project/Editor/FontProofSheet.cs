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
        private const string GalmuriPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";
        private const string ThaleahPath = "Assets/_Project/Art/Fonts/ThaleahFat SDF.asset";
        private const string OutputPath = "Assets/Screenshots/font_proof.png";

        private struct ProofLine
        {
            public string Text;
            public bool UseThaleah;
            public float Size;
        }

        private static readonly ProofLine[] Lines =
        {
            new ProofLine { Text = "귀참 키우기",  UseThaleah = false, Size = 33f },
            new ProofLine { Text = "공격력 강화",  UseThaleah = false, Size = 33f },
            new ProofLine { Text = "12.3K",        UseThaleah = false, Size = 33f },
            new ProofLine { Text = "12.3K  4.7M",  UseThaleah = true,  Size = 48f }
        };

        [MenuItem("Onikiri/Art/Render Font Proof Sheet")]
        public static void Render()
        {
            var galmuri = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriPath);
            var thaleah = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaleahPath);
            if (galmuri == null) { Debug.LogError("[Onikiri] Font asset missing: " + GalmuriPath); return; }

            const int width = 460;
            const int height = 260;
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
            Debug.Log("[Onikiri] Font proof rendered (Galmuri 33px, Thaleah 48px), magnified "
                      + zoom + "x -> " + OutputPath);
        }
    }
}
