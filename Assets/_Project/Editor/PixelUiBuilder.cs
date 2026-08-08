using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief Kenney Pixel UI 팩의 9-슬라이스 테두리를 찍는다.
     *
     * `PixelArtImportPostprocessor`가 포인트 필터와 무압축까지는 해주지만
     * **테두리(spriteBorder)는 못 찍는다.** 그 값이 비어 있으면
     * Image.type = Sliced 가 아무 일도 하지 않고, 패널을 늘릴 때 테두리와
     * 모서리 장식이 통째로 늘어난다 - 픽셀 아트에서 그것은 곧 흐려짐이다.
     *
     * 팩 원본을 고치지 않고 임포트 단계에서 해결한다. 원본을 손대면 팩을 다시
     * 받았을 때 무엇을 고쳤는지 알 수 없다.
     *
     * **스프라이트시트(16x16 타일)는 자르지 않는다.** 체크박스·화살표·슬라이더
     * 조각들인데, 이번 UI 개편이 필요한 것은 패널과 버튼이다. 400칸을 전부 잘라
     * 두면 쓰지 않을 스프라이트가 애셋 목록을 채우고 필요한 것을 찾는 일이 더
     * 비싸진다. 화살표가 필요해지면 그때 자른다.
     */
    public static class PixelUiBuilder
    {
        public const string PackRoot = "Assets/ThirdParty/UI/KenneyPixelUI";
        private const string NineSliceFolder = PackRoot + "/9-Slice";

        /**
         * @brief 48x48 패널의 테두리.
         *
         * 48 = 16 + 16 + 16. 가운데 16x16만 늘어나고 모서리 16x16 넷은 그대로
         * 남는다. 모서리 장식(못 자국)이 정확히 그 안에 들어간다.
         */
        public const int PanelBorder = 16;

        /** 44x44 인레이. 44 = 14 + 16 + 14 */
        public const int InlayBorder = 14;

        [MenuItem("Onikiri/Art/Build Pixel UI Pack")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(NineSliceFolder))
            {
                Debug.LogError("[Onikiri] Kenney Pixel UI pack not found at " + NineSliceFolder);
                return;
            }

            int done = 0, skipped = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { NineSliceFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (importer == null || texture == null) { skipped++; continue; }

                    // 인레이는 44x44라 테두리가 다르다. 크기로 가른다 - 파일 이름에
                    // 기대면 팩이 이름을 바꿨을 때 조용히 틀린 값이 들어간다
                    int border = texture.width >= 48 ? PanelBorder : InlayBorder;

                    if (border * 2 >= texture.width || border * 2 >= texture.height)
                    {
                        Debug.LogError(string.Format(
                            "[Onikiri] {0} is {1}x{2}, too small for a {3}px border",
                            path, texture.width, texture.height, border));
                        skipped++;
                        continue;
                    }

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spriteBorder = new Vector4(border, border, border, border);

                    // 늘어나는 UI라 메시가 사각형이어야 한다. Tight면 9-슬라이스가
                    // 어긋난다
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteGenerateFallbackPhysicsShape = false;
                    importer.SetTextureSettings(settings);

                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    done++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log("[Onikiri] Pixel UI pack: " + done + " nine-slice panels bordered, "
                      + skipped + " skipped.");
        }

        /** 팩 안의 패널 하나. 없으면 null이고 호출부가 폴백을 쓴다 */
        public static Sprite Panel(string set, string name)
        {
            string path = NineSliceFolder + "/" + set + "/" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning("[Onikiri] Pixel UI panel missing: " + path);
            return sprite;
        }
    }
}
