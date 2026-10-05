using System.IO;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 뽑기 결과 타일 뒤의 빛 (69단계). **흰색 한 장을 굽고 색은 실행 때 곱한다.**
     *
     * ★4는 보라, ★5는 금색으로 물든다(GachaResultPopup.glowEpic / glowLegendary).
     * 색마다 텍스처를 따로 굽지 않는 이유는 곱연산 틴트가 흰 바탕에서는 어떤 색도
     * 만들기 때문이다(35단계 규칙의 쉬운 쪽 - 흰색이면 R도 G도 남아 있다).
     *
     * ## 왜 VfxBurst가 아닌가
     *
     * VfxBurst는 월드 좌표에 스프라이트 클립을 한 번 재생하고 풀로 돌리는 전투용
     * 장치다. 결과 판은 캔버스 위라 그것을 쓰려면 카메라·캔버스 좌표 변환이 끼고,
     * 열 타일이 동시에 빛나는 순간 풀이 열 장을 요구한다. 판에 필요한 것은
     * "타일 뒤에서 천천히 도는 빛" 하나라 Image 한 장이면 된다.
     *
     * 모양은 열두 갈래의 빛줄기 + 가운데에서 바깥으로 빠지는 알파다. 끝을 부드럽게
     * 빼므로 Bilinear로 가져온다 - 픽셀 아트 아이콘과 달리 이것은 빛이라, Point로
     * 2.4배 늘리면 계단이 생겨 빛이 아니라 도형으로 읽힌다.
     */
    public static class GachaGlowBaker
    {
        public const string Path = "Assets/_Project/Art/UI/GachaGlow.png";

        private const int Size = 256;
        private const int Rays = 12;

        [MenuItem("Onikiri/Art/Bake Gacha Glow")]
        public static void BakeMenu()
        {
            Bake();
        }

        /** 없으면 굽고, 있으면 그대로 돌려준다 - 빌더가 부른다 */
        public static Sprite Ensure()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path);
            return sprite != null ? sprite : Bake();
        }

        public static Sprite Bake()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            float half = Size * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    // 빛줄기: cos의 거듭제곱으로 갈래를 좁힌다. 가운데 원판이 그 위에 얹힌다
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * Rays * 0.5f)), 6f);
                    float falloff = Mathf.Clamp01(1f - r);
                    float core = Mathf.Clamp01(1f - r / 0.45f);

                    float alpha = Mathf.Clamp01(ray * falloff * falloff * 0.9f + core * core * 0.7f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            File.WriteAllBytes(Path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(Path);
        }
    }
}
