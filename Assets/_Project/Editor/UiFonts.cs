using TMPro;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 빌더들이 쓰는 폰트 에셋 두 벌 (38단계 폰트 위계).
     *
     * 크기 티어는 PixelFontSizes가 정하고, 여기는 그 크기에 맞는 아틀라스를
     * 찾아줄 뿐이다. 44pt 글자에 33pt 아틀라스를 물리는(또는 그 반대) 실수를
     * 빌더마다 반복하지 않도록 로더를 한 곳에 둔다 - 비트맵 폰트는 표시 크기와
     * 아틀라스가 1:1이어야 하고, 어긋나면 에러가 아니라 흐린 글자로만 나타난다.
     */
    public static class UiFonts
    {
        public const string PrimaryPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";
        public const string CaptionPath = "Assets/_Project/Art/Fonts/Galmuri11 Caption SDF.asset";

        /** 본문/재화/헤더. 44pt 아틀라스 */
        public static TMP_FontAsset Primary
        {
            get { return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryPath); }
        }

        /** 항목 라벨·레벨·비용·전후값·탭·배지. 33pt 아틀라스 */
        public static TMP_FontAsset Caption
        {
            get { return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CaptionPath); }
        }

        /** 라벨을 캡션 티어로 내린다. 폰트와 크기를 함께 바꿔야 1:1이 유지된다 */
        public static void Demote(TMP_Text label)
        {
            var caption = Caption;
            if (label == null || caption == null) return;

            label.font = caption;
            label.fontSharedMaterial = caption.material;
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriCaption;
        }
    }
}
