using Onikiri.Core;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 프로젝트 전체의 픽셀 아트 임포트 기준.
     *
     * 임포트한 아트 팩은 캔버스 크기가 제각각이지만(사무라이 96x96, 적 92x92,
     * 보스 184x184, 참격 64/128), 그 캔버스 안에 실제로 그려진 아트는 대략 같은
     * 스케일이다(사무라이 34px 대 적 32~46px). 그래서 단일 PPU만으로 팩별 재스케일
     * 없이 화면에서 크기가 맞는다.
     *
     * PPU 32는 Pixel Perfect 기준 해상도 216x384와 짝을 이루며, 이는 설계 해상도
     * 1080x1920의 정확한 5배 정수배다.
     */
    public static class PixelArtImportSettings
    {
        public const int PixelsPerUnit = DisplayConfig.PixelsPerUnit;

        /** 이 기준이 적용되는 루트 경로 */
        public static readonly string[] ScopedRoots =
        {
            "Assets/ThirdParty/",
            "Assets/_Project/Art/"
        };

        public static bool IsInScope(string assetPath)
        {
            foreach (var root in ScopedRoots)
            {
                if (assetPath.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /**
         * @brief 사이드뷰 캐릭터는 발에 피벗을 둔다.
         *
         * 그래야 92px 프레임과 184px 프레임이 같은 지면선에 선다. 그 외에는 중앙 피벗.
         */
        public static SpriteAlignment PivotFor(string assetPath)
        {
            if (assetPath.Contains("/Characters/") || assetPath.Contains("/Enemies/"))
                return SpriteAlignment.BottomCenter;
            return SpriteAlignment.Center;
        }

        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;

            // 이미 슬라이싱된 시트를 단일 스프라이트로 되돌리는 일은 절대 없어야 한다.
            // 그러면 모든 SpriteRect가 삭제되고, 그 스프라이트를 참조하던 프리팹과
            // 애니메이션 클립이 조용히 전부 깨진다. 슬라이싱된 적 없는 시트에만
            // Single 기본값을 준다
            bool alreadySliced = importer.spriteImportMode == SpriteImportMode.Multiple;
            if (!alreadySliced) importer.spriteImportMode = SpriteImportMode.Single;

            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // 시트는 가로 약 1560px까지 간다. 기본값 2048이면 그보다 큰 것을 조용히
            // 축소해 픽셀 정렬이 깨진다
            importer.maxTextureSize = 4096;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // 슬라이싱된 시트에서는 피벗이 SpriteRect마다 따로 있고, 그것은 슬라이서가
            // 실측한 아트를 기준으로 지정한 값이다. 건드리지 않는다
            if (!alreadySliced) settings.spriteAlignment = (int)PivotFor(importer.assetPath);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            // 모바일의 블록 압축은 픽셀 아트를 뭉갠다. 무압축 RGBA를 강제한다
            ApplyPlatform(importer, "Android");
            ApplyPlatform(importer, "iPhone");
        }

        static void ApplyPlatform(TextureImporter importer, string platform)
        {
            var ps = importer.GetPlatformTextureSettings(platform);
            ps.overridden = true;
            ps.format = TextureImporterFormat.RGBA32;
            ps.maxTextureSize = 4096;
            ps.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(ps);
        }

        public static void Apply(AsepriteImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.spriteMeshType = SpriteMeshType.FullRect;
            importer.spriteExtrude = 0;

            if (importer.assetPath.Contains("/Enemies/"))
            {
                // 캔버스 공간을 쓰면 트리밍된 모든 프레임이 원본 아트보드 기준으로
                // 정렬되므로, 애니메이션 전체에 피벗 하나가 통한다.
                //
                // 이 피벗은 아트가 아니라 캔버스 가장자리다. 캔버스 안에서 아트가 놓이는
                // 위치는 요괴마다 다르다(등롱은 20px 위, 도깨비불은 34px). 그래서 발
                // 기준선을 임포트에 굽는 방식은 파일마다 다른 값을 요구한다. 대신
                // EnemyDefinition이 임포트된 스프라이트에서 오프셋을 측정하고 Enemy가
                // 스폰 시점에 보정한다
                importer.pivotSpace = PivotSpaces.Canvas;
                importer.pivotAlignment = SpriteAlignment.BottomCenter;
            }
            else
            {
                importer.pivotAlignment = PivotFor(importer.assetPath);
            }
            // Aseprite 프레임 태그가 애니메이션 클립이 된다. 적을 낱장 PNG가 아니라
            // .aseprite 원본으로 임포트하는 이유가 이것이다
            importer.generateAnimationClips = true;
            importer.generateModelPrefab = false;
        }
    }
}
