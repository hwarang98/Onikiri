using Onikiri.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 팝업의 뼈대 (#1). 딤 + 가운데 창 + 닫기.
     *
     * 설정·랭킹·뽑기·스킬 정보가 전부 이것을 쓴다. 한곳에 두는 이유는
     * 팝업이 화면과 달리 **형태 자체가 약속**이기 때문이다 - 어두워지고,
     * 가운데 창이 뜨고, 오른쪽 위에 X가 있고, 바깥을 누르면 닫힌다. 네 팝업이
     * 각자 그 약속을 다시 만들면 그중 하나는 반드시 조금 다르게 만들어진다.
     *
     * ## 창의 내용은 이 클래스가 모른다
     *
     * `Ensure`는 **창 rect를 돌려주고 끝난다.** 부르는 쪽은 그 안에 예전과
     * 똑같이 내용을 세운다 - 팝업화가 "컨테이너만 교체"인 이유가 이것이다.
     * 설정 화면의 계정 절도, 랭킹의 목록도 코드가 그대로다.
     */
    public static class PopupBuilder
    {
        /** 딤. 뒤가 비쳐야 게임이 계속 돈다는 것이 보인다 - 완전 불투명은 화면이다 */
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.62f);

        /** 창의 좌우 여백. 전체 폭(1080)에서 이만큼씩 물러난다 */
        private const float SideMargin = 36f;

        public const float CloseSize = 72f;

        /**
         * @brief 팝업 하나를 세우고 **창 rect**를 돌려준다.
         *
         * @param heightFraction 창 높이 / 디자인 높이. 내용에 맞춰 부르는 쪽이
         *        정한다 - 여덟 줄짜리 설정과 스무 줄짜리 랭킹이 같은 크기로
         *        뜨면 한쪽은 텅 비고 한쪽은 답답하다
         * @param dimIsInert 딤을 눌러도 안 닫히게 (뽑기 연출용)
         */
        public static RectTransform Ensure(Transform safeArea, string name, TMP_FontAsset font,
                                           float heightFraction, out RectTransform root,
                                           bool dimIsInert = false)
        {
            var existing = safeArea.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // ---- 루트 = 화면 전체. 딤이 여기 깔린다
            var rootObject = new GameObject(name, typeof(RectTransform));
            rootObject.transform.SetParent(safeArea, false);

            root = (RectTransform)rootObject.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var dim = rootObject.AddComponent<Image>();
            dim.color = DimColor;
            dim.sprite = null;
            dim.raycastTarget = true;

            // 딤 자체가 버튼이다. 창 바깥을 누르면 닫힌다 - 별도의 투명 판을
            // 깔지 않는 이유는 판이 둘이면 어느 쪽이 위인지가 또 하나의 규칙이
            // 되기 때문이다
            var dimButton = rootObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dim;

            // ---- 창 = 가운데 상자
            var windowObject = new GameObject("Window", typeof(RectTransform));
            windowObject.transform.SetParent(rootObject.transform, false);

            var window = (RectTransform)windowObject.transform;
            float half = Mathf.Clamp01(heightFraction) * 0.5f;
            window.anchorMin = new Vector2(0f, 0.5f - half);
            window.anchorMax = new Vector2(1f, 0.5f + half);
            window.offsetMin = new Vector2(SideMargin, 0f);
            window.offsetMax = new Vector2(-SideMargin, 0f);

            var backdrop = windowObject.AddComponent<Image>();
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTextureBuilder.WashiPath);
            backdrop.type = Image.Type.Tiled;
            backdrop.color = UiSkin.PanelInk;
            // 창을 누른 것이 딤까지 내려가면 안 된다. 내용을 만지려다 닫힌다
            backdrop.raycastTarget = true;

            BackdropTextureBuilder.AddSakuraBranch(window);

            // ---- 닫기 X. 창의 오른쪽 위 모서리에 걸친다
            var closeObject = new GameObject("Close", typeof(RectTransform));
            closeObject.transform.SetParent(windowObject.transform, false);

            var closeRect = (RectTransform)closeObject.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(CloseSize, CloseSize);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);

            var closeImage = closeObject.AddComponent<Image>();
            closeImage.color = UiSkin.InkChip;
            closeImage.sprite = null;

            var closeButton = closeObject.AddComponent<Button>();
            UiSkin.ApplyFlatButton(closeButton, closeImage);

            var closeLabel = new GameObject("Label", typeof(RectTransform));
            closeLabel.transform.SetParent(closeObject.transform, false);
            var text = closeLabel.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            text.fontSize = Onikiri.UI.PixelFontSizes.GalmuriSmall;
            text.alignment = TextAlignmentOptions.Center;
            text.color = UiSkin.Text;
            text.raycastTarget = false;
            // 픽셀 폰트에 X 글리프가 있다(FontCharset). 스프라이트를 굽지 않는
            // 이유는 이것이 기호가 아니라 글자여도 읽히기 때문이다
            text.text = "X";
            var textRect = (RectTransform)closeLabel.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0f, -8f);
            textRect.offsetMax = new Vector2(0f, 8f);

            // ---- 컴포넌트
            var popup = rootObject.AddComponent<Onikiri.UI.PopupPanel>();
            var so = new SerializedObject(popup);
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("dimButton").objectReferenceValue = dimButton;
            so.FindProperty("dimIsInert").boolValue = dimIsInert;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 팝업 층. 상시 HUD(경험치 줄)보다 위다 - 딤 위에 옥색 줄이 남으면
            // 딤이 뚫린 것으로 읽힌다
            BattleContentBuilder.RaiseToLayer(root, DisplayConfig.SortingPopup, true);

            return window;
        }

        /** 창 높이(px). 내용 배치가 이 값을 기준으로 자리를 잡는다 */
        public static float WindowHeight(float heightFraction)
        {
            return DisplayConfig.DesignHeight * Mathf.Clamp01(heightFraction);
        }
    }
}
