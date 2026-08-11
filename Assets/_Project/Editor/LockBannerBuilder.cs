using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 잠긴 패널의 헤더를 덮는 해금 조건 배너 (41단계).
     *
     * LockedTab이 잠긴 화면도 열어주게 되면서(미리보기), 스킬·장비·동료
     * 세 패널이 같은 안내를 필요로 한다. 세 빌더가 각자 만들면 판·색·문구
     * 형식이 갈리므로 한 곳에서 만든다 - UiSkin이 판때기를 한 곳에 모은
     * 것과 같은 이유다.
     *
     * 배너는 헤더 위에 겹쳐 서고 자리를 새로 먹지 않는다. 잠긴 동안 헤더가
     * 말하던 것(제목·보석 잔고·자동 시전)은 어차피 쓸 수 없는 것들이고,
     * 해금되면 배너가 사라져 헤더가 그대로 드러난다 - 목록을 밀거나 빈
     * 자리를 남기지 않는 유일한 배치다.
     *
     * 판은 전직 안내(BuildAwakenPage)와 같은 파인 판이다. "여기는 아직
     * 잠겨 있다"를 말하는 형태가 화면마다 다르면 그것은 두 규칙이다.
     */
    public static class LockBannerBuilder
    {
        public const string RootName = "LockBanner";

        /** 자물쇠 글리프 한 변. 캡션 글자(33px)보다 조금 큰 정도가 배지답다 */
        private const float GlyphSize = 44f;

        /**
         * @brief 헤더를 덮는 배너를 세운다. 문구는 완성형으로 받는다.
         *
         * 조건 문구를 여기서 조립하지 않는 이유: 탭(LockedTab.Requirement)과
         * 배너가 같은 조건을 다른 문장으로 말하면 안 되므로, 형식의 선택은
         * 호출하는 빌더가 한 번만 한다. 새 문구는 UIStrings.txt에 등재할 것.
         *
         * 컴포넌트는 뿌리(빈 RectTransform, 항상 켜짐)에 살고 자식 판만
         * 켜고 끈다 - 자기를 끄면 해금 이벤트를 받을 몸이 없다.
         */
        public static void Build(Transform header, TMP_FontAsset font, string message,
                                 int requiredLevel, int requiredStage)
        {
            if (header == null) return;

            var stale = header.Find(RootName);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(header, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // 헤더의 다른 것(제목·잔고·토글)보다 위에 그려져야 덮는다
            root.transform.SetAsLastSibling();

            var visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(root.transform, false);

            var visualRect = (RectTransform)visual.transform;
            visualRect.anchorMin = Vector2.zero;
            visualRect.anchorMax = Vector2.one;
            visualRect.offsetMin = Vector2.zero;
            visualRect.offsetMax = Vector2.zero;

            var image = visual.AddComponent<Image>();

            // 전직 안내와 같은 톤(InlayTint의 0.7배)인데 **알파는 1로 되돌린다.**
            // Color에 스칼라를 곱하면 알파도 함께 눌려 판이 반투명해지고,
            // 덮었어야 할 헤더 글자(보석 잔고)가 비쳐 보였다 - 전직 안내는
            // 빈 페이지 위라 같은 실수가 눈에 안 띄었을 뿐이다
            var tint = UiSkin.InlayTint * 0.7f;
            tint.a = 1f;
            UiSkin.ApplyPanel(image, UiSkin.Inlay, tint);

            // 레이캐스트를 받는다 - 배너가 덮은 헤더의 조작(자동 시전 토글)이
            // 잠긴 동안 눌리면 안 된다. 파인 판이라 눌릴 것으로 보이지도 않는다
            image.raycastTarget = true;

            var glyph = new GameObject("Glyph", typeof(RectTransform));
            glyph.transform.SetParent(visual.transform, false);

            var glyphRect = (RectTransform)glyph.transform;
            glyphRect.anchorMin = new Vector2(0f, 0.5f);
            glyphRect.anchorMax = new Vector2(0f, 0.5f);
            glyphRect.pivot = new Vector2(0f, 0.5f);
            glyphRect.sizeDelta = new Vector2(GlyphSize, GlyphSize);
            glyphRect.anchoredPosition = new Vector2(24f, 0f);

            var glyphImage = glyph.AddComponent<Image>();
            glyphImage.sprite = UiGlyphBuilder.Load(UiGlyphBuilder.Lock);
            glyphImage.color = UiSkin.TextDim;
            glyphImage.raycastTarget = false;
            glyphImage.enabled = glyphImage.sprite != null;

            var label = QuestPanelBuilder.CreateLabel(visual.transform, font, "Label",
                                                      TextAlignmentOptions.Center);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            // 글리프 몫만큼 왼쪽을 비운다. 문구는 남는 폭의 가운데다
            labelRect.offsetMin = new Vector2(24f + GlyphSize, 0f);
            labelRect.offsetMax = new Vector2(-24f, 0f);
            label.text = message;
            label.color = UiSkin.Text;

            var banner = root.AddComponent<Onikiri.UI.PanelLockBanner>();
            var so = new SerializedObject(banner);
            so.FindProperty("requiredLevel").intValue = requiredLevel;
            so.FindProperty("requiredStage").intValue = requiredStage;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 잠긴 세이브가 다수이므로 켜진 채로 저장한다. 해금된 세이브에서는
            // PanelLockBanner.OnEnable이 첫 프레임에 끈다
            visual.SetActive(true);
        }
    }
}
