using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 발도 오의 화면과 그것을 구동하는 SkillSystem을 세운다.
     *
     * ## 왜 성장 패널의 탭이 아닌가
     *
     * 18단계에 성장 패널의 최상위 탭이 **재화**로 정리됐다(강화=골드 / 성장=포인트
     * / 전직=잠금). 스킬을 그 줄에 얹으면 "이 줄은 캐릭터 성장"이라는 규칙이
     * 깨진다 - 오의는 캐릭터의 스탯이 아니라 별개 시스템이고, 그래서 12단계부터
     * 하단 탭바에 자기 자리를 갖고 있었다.
     *
     * 그 결정이 `BattleContentBuilder.LockedTabs` 주석에 적혀 있고, 이 빌더는
     * 그 자리에 실제 화면을 채운다.
     *
     * ## 성장 패널과 같은 띠를 쓴다
     *
     * 화면 10~45%를 그대로 덮는다. 새 자리를 만들지 않는 이유는 두 화면이
     * **동시에 보일 일이 없기** 때문이다 - 하단 탭이 둘 사이를 오간다. 같은
     * 자리를 쓰면 "아래쪽 절반은 목록"이라는 화면의 문법이 유지된다.
     *
     * 자기 바탕을 깔아야 한다. 뒤의 성장 패널 행이 비쳐 보이면 두 목록이 겹친
     * 것으로 읽힌다 - 11단계의 유령 텍스트와 같은 그림이다.
     */
    public static class SkillPanelBuilder
    {
        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        /** 참격 재생 프리팹. 요괴 이펙트(VfxBurst)도 같은 것을 돌려 쓴다 */
        public const string SlashPrefabPath = PrefabFolder + "/PackSlash.prefab";
        private const string StreakPrefabPath = PrefabFolder + "/DashStreak.prefab";
        private const string AfterimagePrefabPath = PrefabFolder + "/Afterimage.prefab";
        private const string StreakTexturePath = "Assets/_Project/Art/VFX/DashStreak.png";

        /**
         * @brief 참격 팩. 128x128 열 장짜리 시트가 모양 3종 x 색 5종으로 있다.
         *
         * 시트는 640x256 = 가로 5칸 x 세로 2칸이고, 프레임은 왼쪽 위부터
         * 오른쪽으로 읽는다.
         */
        private const string SlashPackFolder = "Assets/ThirdParty/VFX/Slashes/";
        private const int SlashFrameSize = 128;
        private const int SlashSheetColumns = 5;
        private const int SlashSheetRows = 2;

        public const string PanelName = "SkillPanel";

        private const float SidePadding = 48f;
        private const float LineHeight = 62f;
        private const float RowHeight = LineHeight * 2f + 20f;
        private const float RowGap = 14f;
        private const float TopPadding = 12f;
        private const float CostWidth = 300f;
        private const float IconLeft = 24f;
        private const float TextLeft = IconLeft + UiIcons.Size + 20f;

        /** 머리글(제목 + 자동 시전) 한 줄. 행보다 얇다 */
        private const float HeaderHeight = 76f;
        private const float HeaderGap = 14f;

        // ------------------------------------------------------------ 49단계: 장착

        /**
         * @brief 장착 슬롯 줄. **스크롤 밖에 고정된다.**
         *
         * 대장간의 서브탭 줄과 같은 처리다(EquipmentPanelBuilder.BuildTabs) -
         * 목록이 움직여도 네 자리는 제자리에 있어야 한다. 이 화면에서 "지금
         * 나가는 것이 무엇인가"는 목록을 어디까지 굴렸는지와 무관한 사실이고,
         * 그것이 함께 굴러가면 여덟 줄 중 넷을 눈으로 찾아야 한다.
         */
        private const float SlotRowHeight = 138f;
        private const float SlotRowGap = 14f;
        private const float SlotGap = 12f;

        /** 칩 아이콘. 16px 아트의 정수배여야 한다(UiIcons.Size와 같은 규칙) */
        private const float SlotIconSize = 80f;

        /** 목록 아래 여백. 마지막 줄이 띠 끝에 붙어 잘린 것처럼 보이지 않게 */
        private const float BottomPadding = 12f;

        public const string SlotRowName = "Slots";
        public const string ViewportName = "Viewport";
        public const string ContentName = "Content";

        // 39단계 톤 통일: 자기 색을 갖지 않는다. 팔레트의 단일 출처는 UiSkin이다
        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        /**
         * @brief 화면과 시스템을 세우고 SkillSystem을 돌려준다.
         *
         * PlayerCombat이 씬에 있어야 한다 - 오의가 그것을 통해 벤다.
         */
        public static SkillSystem Build()
        {
            var safeArea = UpgradePanelBuilder.EnsureSafeArea();
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - run Rebuild Main Scene first.");
                return null;
            }

            var samurai = GameObject.Find("Samurai");
            var combat = samurai != null ? samurai.GetComponent<PlayerCombat>() : null;
            if (combat == null)
            {
                Debug.LogError("[Onikiri] PlayerCombat missing - run Build Combat Content first.");
                return null;
            }

            var system = samurai.GetComponent<SkillSystem>();
            if (system == null) system = samurai.AddComponent<SkillSystem>();

            var panel = EnsurePanel(safeArea);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            // 팝업이 목록보다 먼저다 (#14) - 줄들이 이 팝업을 참조로 들고
            // 있어야 하고, 참조를 물리려면 대상이 이미 있어야 한다
            BuildSkillPopup(safeArea, system, font);

            BuildHeader(panel, system, font);
            BuildSlotRow(panel, system, font);

            var content = BuildScroll(panel);
            for (int i = 0; i < SkillCatalog.Count; i++) BuildRow(content, system, font, i);

            PruneStrays(panel);
            VerifyPanelFits();

            // 씬에는 꺼진 채로 저장된다. 하단 탭이 켠다 - 처음 켠 플레이어가
            // 강화 목록 대신 잠긴 오의 셋을 먼저 보는 화면이 되면 안 된다
            panel.gameObject.SetActive(false);

            // 27단계의 화면 연출. 안무보다 먼저 세워야 참조를 넘길 수 있다
            var nameFlash = BuildNameFlash(safeArea, font);
            var screenFlash = BuildScreenFlash();

            var performer = WirePerformer(samurai, combat, nameFlash, screenFlash);
            WriteSlots(system, combat, performer);

            Debug.Log(string.Format(
                "[Onikiri] Skill panel built: {0} skills / {1} slots, list {2:F0}px scrolling in "
                + "a {3:F0}px viewport (band {4:F0}px).",
                SkillCatalog.Count, SkillCurve.MaxSlots, PanelContentHeight, ViewportHeight,
                BandHeight));

            return system;
        }

        // ---------------------------------------------------------------- 정보 팝업 (#14)

        public const string SkillPopupName = "SkillInfoPopup";

        /**
         * @brief 오의 정보 팝업. **정보 한 덩이 + 탭 둘(강화/장착).**
         *
         * 공용 팝업 뼈대를 쓴다(PopupBuilder) - 딤, 가운데 창, X, 바깥 탭으로
         * 닫기가 설정·랭킹과 같은 약속이다. 이 빌더가 아는 것은 창 안의
         * 배치뿐이다.
         *
         * 높이 0.34(653px)는 내용에서 나온다: 이름 72 + 설명 120 + 탭 줄 72 +
         * 페이지(두 줄 124 + 버튼 88) + 여백. 처음에 0.42로 잡았다가 실기에서
         * 아래가 200px 넘게 비어 "덜 만든 창"으로 보였다 - 44단계의 규칙대로
         * **상자를 내용에 맞춘다.**
         *
         * 강화 탭과 장착 탭 중 **긴 쪽**에 맞춘다. 탭마다 창 크기가 달라지면
         * 손가락이 있던 자리에 다른 버튼이 온다.
         */
        private static void BuildSkillPopup(Transform safeArea, SkillSystem system,
                                            TMP_FontAsset font)
        {
            RectTransform root;
            var window = PopupBuilder.Ensure(safeArea, SkillPopupName, font, 0.34f, out root);

            // ---- 머리: 아이콘 + 이름
            var iconObject = new GameObject("Icon", typeof(RectTransform));
            iconObject.transform.SetParent(window, false);
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.sizeDelta = new Vector2(72f, 72f);
            iconRect.anchoredPosition = new Vector2(28f, -24f);
            var icon = iconObject.AddComponent<Image>();
            icon.color = UiIcons.Tint;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var nameLabel = CreateLabel(window, font, "Name", TextAlignmentOptions.Left);
            var nameRect = (RectTransform)nameLabel.transform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.offsetMin = new Vector2(112f, 0f);
            nameRect.offsetMax = new Vector2(-(PopupBuilder.CloseSize + 20f), 0f);
            nameRect.sizeDelta = new Vector2(nameRect.sizeDelta.x, 72f);
            nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, -24f);
            nameLabel.text = "오의";

            // ---- 설명. 두 줄이라 높이를 넉넉히 준다
            var description = CreateLabel(window, font, "Description", TextAlignmentOptions.TopLeft);
            UiFonts.Demote(description);
            var descRect = (RectTransform)description.transform;
            descRect.anchorMin = new Vector2(0f, 1f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0.5f, 1f);
            descRect.offsetMin = new Vector2(28f, 0f);
            descRect.offsetMax = new Vector2(-28f, 0f);
            descRect.sizeDelta = new Vector2(descRect.sizeDelta.x, 120f);
            descRect.anchoredPosition = new Vector2(0f, -112f);
            description.color = DimColor;
            description.text = "";

            // ---- 탭 둘
            Button upgradeTab, equipTab;
            TMP_Text upgradeTabLabel, equipTabLabel;
            Image upgradeTabBg, equipTabBg;
            BuildPopupTab(window, font, "강화", 0f, 0.5f, out upgradeTab, out upgradeTabLabel, out upgradeTabBg);
            BuildPopupTab(window, font, "장착", 0.5f, 1f, out equipTab, out equipTabLabel, out equipTabBg);

            // ---- 강화 페이지
            var upgradePage = BuildPopupPage(window, "UpgradePage");

            var levelLabel = CreateLabel(upgradePage, font, "Level", TextAlignmentOptions.Left);
            PlacePopupLine(levelLabel, 0);
            var valueLabel = CreateLabel(upgradePage, font, "Value", TextAlignmentOptions.Left);
            UiFonts.Demote(valueLabel);
            PlacePopupLine(valueLabel, 1);
            var costLabel = CreateLabel(upgradePage, font, "Cost", TextAlignmentOptions.Right);
            PlacePopupLine(costLabel, 1);

            var upgradeButton = BuildPopupAction(upgradePage, font, "강화", UiSkin.Good);

            // ---- 장착 페이지
            var equipPage = BuildPopupPage(window, "EquipPage");

            var equipState = CreateLabel(equipPage, font, "State", TextAlignmentOptions.Left);
            UiFonts.Demote(equipState);
            PlacePopupLine(equipState, 0);

            var equipAction = BuildPopupAction(equipPage, font, "장착", UiSkin.GemAction);

            // ---- 컴포넌트
            var popup = root.gameObject.AddComponent<Onikiri.UI.SkillInfoPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("icon").objectReferenceValue = icon;

            // 오의별 아이콘. 카탈로그 순서 그대로 - 팝업이 index로 집는다
            var iconSprites = SkillIcons();
            var iconArray = so.FindProperty("icons");
            iconArray.arraySize = iconSprites.Length;
            for (int i = 0; i < iconSprites.Length; i++)
                iconArray.GetArrayElementAtIndex(i).objectReferenceValue = iconSprites[i];

            so.FindProperty("descriptionLabel").objectReferenceValue = description;
            so.FindProperty("upgradeTab").objectReferenceValue = upgradeTab;
            so.FindProperty("upgradeTabLabel").objectReferenceValue = upgradeTabLabel;
            so.FindProperty("upgradeTabBackground").objectReferenceValue = upgradeTabBg;
            so.FindProperty("upgradePage").objectReferenceValue = upgradePage.gameObject;
            so.FindProperty("equipTab").objectReferenceValue = equipTab;
            so.FindProperty("equipTabLabel").objectReferenceValue = equipTabLabel;
            so.FindProperty("equipTabBackground").objectReferenceValue = equipTabBg;
            so.FindProperty("equipPage").objectReferenceValue = equipPage.gameObject;
            so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            so.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            so.FindProperty("costLabel").objectReferenceValue = costLabel;
            so.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
            so.FindProperty("upgradeButtonLabel").objectReferenceValue =
                upgradeButton.GetComponentInChildren<TMP_Text>(true);
            so.FindProperty("equipStateLabel").objectReferenceValue = equipState;
            so.FindProperty("equipButton").objectReferenceValue = equipAction;
            so.FindProperty("equipButtonLabel").objectReferenceValue =
                equipAction.GetComponentInChildren<TMP_Text>(true);
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
        }

        /** 팝업 탭 하나. 설명 아래, 페이지 위 */
        private static void BuildPopupTab(RectTransform window, TMP_FontAsset font, string text,
                                          float left, float right,
                                          out Button button, out TMP_Text label, out Image background)
        {
            var go = new GameObject("Tab_" + text, typeof(RectTransform));
            go.transform.SetParent(window, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(left, 1f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(28f + (left > 0f ? 6f : 0f), 0f);
            rect.offsetMax = new Vector2(-(28f + (right < 1f ? 6f : 0f)), 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, PopupTabHeight);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -PopupTabTop);

            background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, background);

            label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(label);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -6f);
            labelRect.offsetMax = new Vector2(0f, 6f);
            label.text = text;
        }

        /** 탭이 켜고 끄는 페이지. 둘이 같은 자리에 겹친다 */
        private static RectTransform BuildPopupPage(RectTransform window, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(window, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(28f, 24f);
            rect.offsetMax = new Vector2(-28f, -(PopupTabTop + PopupTabHeight + 16f));
            return rect;
        }

        /** 페이지 안의 한 줄. 위에서 아래로 쌓인다 */
        private static void PlacePopupLine(TMP_Text label, int line)
        {
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, PopupLineHeight);
            rect.anchoredPosition = new Vector2(0f, -line * PopupLineHeight);
        }

        /** 페이지 아래쪽에 붙는 동작 버튼 하나 */
        private static Button BuildPopupAction(RectTransform page, TMP_FontAsset font,
                                               string text, Color tint)
        {
            var go = new GameObject("Action", typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, PopupActionHeight);
            rect.anchoredPosition = Vector2.zero;

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Panel, tint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -8f);
            labelRect.offsetMax = new Vector2(0f, 8f);
            label.text = text;

            return button;
        }

        private const float PopupTabTop = 244f;
        private const float PopupTabHeight = 72f;
        private const float PopupLineHeight = 62f;
        private const float PopupActionHeight = 88f;

        // ---------------------------------------------------------------- 화면 연출

        public const string NameFlashName = "SkillNameFlash";
        public const string ScreenFlashName = "ScreenFlash";
        public const string VignettePath = "Assets/_Project/Art/VFX/Vignette.png";

        /**
         * @brief 오의 이름이 뜨는 자리.
         *
         * 전투 영역 밴드 안, 가로 가운데, **사무라이 머리 위**에 둔다.
         *
         * 처음에 지면선 살짝 위(밴드 바닥 +210)에 뒀다가 스크린샷을 보고 올렸다 -
         * 글자가 사무라이 발밑과 지면 띠에 겹쳐서, 이름과 캐릭터가 서로를 가렸다.
         * +460이면 사무라이(키 1u = 화면 약 190px) 위의 빈 하늘에 선다.
         *
         * 상단 바에 두지 않은 이유는 시선이다. 오의가 터지는 곳은 전투 영역이고,
         * 이름이 상단 바에 뜨면 그 둘을 눈이 따로 봐야 한다.
         */
        private static Onikiri.UI.SkillNameFlash BuildNameFlash(Transform safeArea, TMP_FontAsset font)
        {
            var existing = safeArea.Find(NameFlashName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(NameFlashName, typeof(RectTransform));
            go.transform.SetParent(safeArea, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, DisplayConfig.GrowthPanelTop);
            rect.anchorMax = new Vector2(0.5f, DisplayConfig.GrowthPanelTop);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(520f, 120f);
            rect.anchoredPosition = new Vector2(0f, 460f);

            // 그림자를 먼저 만든다. 형제 순서가 곧 그리는 순서이므로 뒤에 있어야
            // 라벨에 덮인다 - 순서를 뒤집으면 그림자가 글자를 지운다
            var shadow = CreateFlashLabel(go.transform, font, "Shadow");
            var shadowRect = (RectTransform)shadow.transform;
            // 아트 픽셀 하나만큼 오른쪽 아래로. 데미지 팝업과 같은 규칙이다
            shadowRect.anchoredPosition = new Vector2(
                Onikiri.UI.PixelFontSizes.ArtPixelScale, -Onikiri.UI.PixelFontSizes.ArtPixelScale);
            shadow.color = new Color(0f, 0f, 0f, 0.75f);

            var label = CreateFlashLabel(go.transform, font, "Label");

            var flash = go.AddComponent<Onikiri.UI.SkillNameFlash>();
            var so = new SerializedObject(flash);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("rect").objectReferenceValue = rect;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 꺼진 채로 저장한다. 켜둔 채 저장하면 에디터에서 오의 이름이 항상
            // 화면에 떠 있고, 그것이 "버그"로 읽힌다
            go.SetActive(false);
            return flash;
        }

        /** 이름 플래시의 라벨 하나. 라벨과 그림자가 같은 크기·정렬이어야 한다 */
        private static TMP_Text CreateFlashLabel(Transform parent, TMP_FontAsset font, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            // 아틀라스의 정수배. 제목 크기(88)를 쓴다 - 본문(44)이면 이펙트에 묻히고,
            // 그 사이 값은 비트맵이 리샘플되어 흐려진다(PixelFontSizes)
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriLarge;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = SkillCatalog.Skills[0].DisplayName;
            return label;
        }

        /**
         * @brief 귀참의 화면 연출. **가장자리가 닫혔다 열린다.**
         *
         * 27단계의 흰 풀스크린을 걷어냈다. 이유는 ScreenFlash 주석에 적어뒀다 -
         * 가운데를 덮으면 무엇이 죽었는지 볼 수 없고, 먹빛 톤에서 흰 화면만
         * 다른 게임처럼 보인다.
         *
         * 두 겹 다 **비네트 텍스처를 쓴다.** 모양이 같아야 "테두리에서 빛이
         * 터지고 그 뒤를 어둠이 조인다"가 한 동작으로 읽힌다 - 하나는 원형이고
         * 하나는 사각형이면 두 사건으로 보인다.
         *
         * **안전 영역이 아니라 캔버스 직속이다.** 노치 옆까지 덮어야 한다 -
         * 화면 일부만 반응하면 그 경계선이 보이고, 경계선이 보이는 순간 연출이
         * 아니라 UI가 된다. 형제 순서의 맨 뒤에 두어 UI 위에도 덮인다.
         */
        private static Onikiri.UI.ScreenFlash BuildScreenFlash()
        {
            var canvas = GameObject.Find("UI Canvas");
            if (canvas == null) return null;

            var existing = canvas.transform.Find(ScreenFlashName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(ScreenFlashName, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();
            Stretch((RectTransform)go.transform);

            var vignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BuildVignetteTexture());

            // 어둠이 먼저(뒤에), 빛이 나중(앞에). 순서를 뒤집으면 어둠이 빛을 덮어
            // 엣지 번쩍이 통째로 사라진다
            var vignette = CreateFullScreenImage(go.transform, "Vignette", vignetteSprite,
                new Color(0f, 0f, 0f, 0f));

            // 붉은 흰빛. 순백으로 두면 먹빛 톤에서 다시 튀고, 순적이면 피격
            // 플래시와 섞인다. 사이의 살굿빛이 "베인 자리에서 튀는 불티"로 읽힌다
            var edge = CreateFullScreenImage(go.transform, "EdgeFlash", vignetteSprite,
                new Color32(0xFF, 0x8A, 0x6A, 0x00));

            var flash = go.AddComponent<Onikiri.UI.ScreenFlash>();
            var so = new SerializedObject(flash);
            so.FindProperty("edgeFlash").objectReferenceValue = edge;
            so.FindProperty("vignette").objectReferenceValue = vignette;
            // 0.85로 두면 모서리가 완전히 살굿빛으로 막힌다. 번쩍은 "있었다"만
            // 남기면 되고, 무게는 히트스톱과 셰이크가 낸다
            so.FindProperty("edgePeak").floatValue = 0.6f;
            so.FindProperty("edgeSeconds").floatValue = 0.10f;
            so.FindProperty("vignettePeak").floatValue = 0.88f;
            so.FindProperty("vignetteSeconds").floatValue = 0.34f;
            so.FindProperty("vignetteCloseSeconds").floatValue = 0.05f;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return flash;
        }

        private static Image CreateFullScreenImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;

            // 레이캐스트를 먹으면 번쩍이 도는 0.3초 동안 모든 버튼이 죽는다.
            // 화면을 덮는 판에서 가장 흔한 사고다
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /**
         * @brief 비네트 텍스처를 굽는다. 가운데 투명, 가장자리 검정.
         *
         * 코드로 굽는다. 팩 아트를 찾지 않는 이유는 불꽃·꽃잎과 같다 - 필요한 것이
         * 방사형 알파 그라디언트 하나이고, 그 크기에서는 그림 파일을 관리하는
         * 비용이 아트보다 크다.
         *
         * 128px으로 굽고 화면 전체로 늘린다. 비네트는 부드러운 그라디언트라
         * 픽셀 격자를 지킬 이유가 없는 유일한 이펙트다 - 오히려 정수배로 늘리면
         * 계단이 보인다.
         */
        private static string BuildVignetteTexture()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath) != null) return VignettePath;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 중심에서의 정규화 거리. 코너가 1을 넘으므로 코너가 가장 어둡다
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);

                    // **0.80까지 완전 투명, 1.35에서 최대.** 처음에 0.45~1.05로
                    // 잡았는데 그건 비네트가 아니라 화면을 통째로 덮는 판이었다 -
                    // 검은 비네트일 때는 "어둡다"로 넘어갔지만, 같은 모양으로
                    // 살굿빛 엣지 번쩍을 켜자 화면 전체가 살구색이 됐다. 걷어내려던
                    // 흰 풀스크린과 같은 것을 색만 바꿔 다시 만든 셈이다.
                    //
                    // 이 값이면 각 변의 한가운데(r=1.0)가 0.30, 네 귀퉁이(r=1.41)가
                    // 1.0이다. 무게가 모서리에 몰리고 가운데는 그대로 보인다.
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.80f, 1.35f, r));

                    // **RGB를 흰색으로 굽는다. 검정이 아니다.**
                    //
                    // 이 스프라이트는 두 장이 나눠 쓴다 - 비네트는 검게, 엣지
                    // 번쩍은 살굿빛으로 틴트한다. 그런데 Image의 색은 스프라이트에
                    // **곱해지므로**, 검게 구우면 살굿빛을 곱해도 검정이다.
                    // 실제로 28단계에 그렇게 구워 두고 "엣지 번쩍"을 넣었는데,
                    // 화면 가장자리 픽셀을 재 보니 밝아지기는커녕 더 어두워졌다
                    // (t=0.017에서 하늘 0.49 -> 0.29, 붉은 쪽으로 치우침 없음).
                    // 흰색으로 구워야 틴트가 그대로 색이 된다.
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            System.IO.File.WriteAllBytes(VignettePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(VignettePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(VignettePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // 비네트만 필터를 켠다. 그라디언트라 Point로 두면 128px 계단이 화면
            // 전체로 늘어나 띠가 보인다
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            Debug.Log("[Onikiri] Baked vignette " + size + "px -> " + VignettePath);
            return VignettePath;
        }

        // ---------------------------------------------------------------- 시스템

        /**
         * @brief SkillCatalog의 값을 씬 컴포넌트에 옮겨 적는다.
         *
         * 스크립트 기본값에 맡기지 않는 이유는 이 프로젝트의 규칙이다 - 컴포넌트가
         * 이미 씬에 있으면 코드의 기본값을 바꿔도 반영되지 않고, 그러면 빌더가
         * 단일 출처 역할을 못 한다.
         *
         * **레벨은 덮어쓰지 않는다.** 곡선을 손볼 때마다 플레이 진행이 초기화되면
         * 밸런싱을 눈으로 확인할 수가 없다(WriteTracks와 같은 처리).
         */
        private static void WriteSlots(SkillSystem system, PlayerCombat combat, SkillPerformer performer)
        {
            var so = new SerializedObject(system);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("performer").objectReferenceValue = performer;

            // **최전선.** 4번 자리와 신규 오의의 게이트라, 이 참조가 비면 시스템이
            // 최전선을 1로 보고(SkillSystem.FrontierNow의 폴백) 다섯이 영원히
            // 잠긴다 - 실기에서 실제로 그랬다. 게이트가 없는 것이 아니라 게이트가
            // 늘 닫혀 있는 상태라 화면에는 "st51에 갔는데 아무 일도 안 일어난다"로
            // 나온다. EquipmentPanelBuilder가 같은 참조를 같은 방식으로 넘긴다
            var battle = GameObject.Find("Battle");
            so.FindProperty("stage").objectReferenceValue =
                battle != null ? battle.GetComponent<StageProgress>() : null;

            var slots = so.FindProperty("slots");
            int previousCount = slots.arraySize;
            slots.arraySize = SkillCatalog.Count;

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                var element = slots.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("displayName").stringValue = spec.DisplayName;
                element.FindPropertyRelative("baseMultiplier").doubleValue = spec.BaseMultiplier;
                element.FindPropertyRelative("cooldownSeconds").floatValue = (float)spec.CooldownSeconds;
                element.FindPropertyRelative("unlockLevel").intValue = spec.UnlockLevel;
                element.FindPropertyRelative("unlockStage").intValue = spec.UnlockStage;

                // 50단계: 게이트의 **종류**도 옮겨 적는다. 이 플래그가 없으면
                // 가챠 몫이 unlockStage(비용 기준점 st41)를 게이트로 읽어
                // 상점 해금과 함께 열린다 - 뽑지 않은 오의가 공짜로 열리는
                // 것이고, 그 사고는 밸런스가 어긋난 뒤에야 드러난다
                element.FindPropertyRelative("gachaGated").boolValue = spec.GachaGated;
                element.FindPropertyRelative("baseCost").doubleValue = spec.BaseCost;
                element.FindPropertyRelative("slashTint").colorValue = RgbaToColor(spec.SlashRgba);

                if (i >= previousCount) element.FindPropertyRelative("level").intValue = 1;
                if (element.FindPropertyRelative("level").intValue < 1)
                    element.FindPropertyRelative("level").intValue = 1;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 0xRRGGBBAA -> Color. 밸런스 표에 Color 구조체를 섞지 않기 위한 변환 */
        public static Color RgbaToColor(uint rgba)
        {
            return new Color32(
                (byte)((rgba >> 24) & 0xFF),
                (byte)((rgba >> 16) & 0xFF),
                (byte)((rgba >> 8) & 0xFF),
                (byte)(rgba & 0xFF));
        }

        // ---------------------------------------------------------------- 참격 / 안무

        /**
         * @brief 사무라이 클립. 27단계에 오의마다 고유 모션이 붙었다.
         *
         * 팩에 이미 있는 것을 쓴다. 새 아트를 만들지 않는 것이 이 프로젝트의
         * 규칙이고(불꽃·꽃잎을 코드로 구운 것과 같은 판단), 마침 필요한 세 가지가
         * 전부 있었다 - 연속 베기 셋, 돌진, 큰 피니셔.
         */
        private const string SamuraiSprites = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/";

        /**
         * @brief 클립별 타격 프레임. **실측값이다.**
         *
         * 23단계의 방법을 그대로 썼다 - 프레임마다 잉크의 오른쪽 끝을 재면 칼이
         * 뻗는 프레임에서만 값이 튄다. 그 프레임이 원화가가 참격을 그려 넣은
         * 자리이고, 타격은 거기서 나야 한다.
         *
         * ```
         * ATTACK 1 (7f)  f4  reach 1.38u  ink 808   <- 타격
         * ATTACK 2 (7f)  f3  reach 1.22u  ink 870   <- 타격
         * ATTACK 3 (6f)  f2  reach 1.34u  ink 749   <- 타격
         * SPECIAL  (14f) f5  reach 1.00u  ink 1140  <- 타격 (f0~4 준비, f6~13 마무리)
         * DASH     (8f)  f1~3 reach 0.72u          <- 자세만, 칼을 뻗지 않는다
         * ```
         *
         * DASH에 타격 프레임이 없는 것이 일섬의 설계를 정했다. 돌진 클립은 베는
         * 그림이 아니므로 **베는 것은 참격 애니가 맡는다** - 사무라이가 돌진 자세로
         * 지나가고 팩 참격이 일렬을 가른다.
         */
        private const int Attack1ImpactFrame = 4;
        private const int Attack2ImpactFrame = 3;
        private const int Attack3ImpactFrame = 2;
        private const int SpecialImpactFrame = 5;

        /**
         * @brief 일섬의 타격 프레임. **일부러 늦다.**
         *
         * 3번(0.115초)이 아니라 6번(0.231초)이다. 거합은 지나가는 것이 먼저고
         * 베이는 것이 나중이라, 지나가는 그 프레임에 숫자가 뜨면 "지나가며 벴다"가
         * 아니라 "부딪쳤다"로 읽힌다. 반 박자 뒤에 요괴들이 한꺼번에 반응해야 한다.
         *
         * DASH는 원래 칼을 뻗지 않는 자세 클립이라 "그려진 타격 프레임"이 없다.
         * 그래서 23단계의 실측 규칙(원화가가 그린 참격 프레임에 맞춘다)이 여기서는
         * 적용되지 않고, 연출 타이밍이 기준이 된다.
         *
         * **타격 횟수는 1회 그대로**이므로 총 데미지와 밴드는 움직이지 않는다.
         */
        private const int DashLateHitFrame = 6;

        /**
         * @brief 참격 프리팹을 굽고 안무를 배선한다.
         *
         * 29단계에 메시 트레일(`SlashTrail`)이 사라지고 **팩 참격 애니**가 들어왔다.
         *
         * 27단계에는 64px 참격 한 장을 3.4배로 키웠고(픽셀이 네모가 됐다),
         * 28단계에는 그 뭉개짐을 고치려고 같은 크기의 메시를 그렸다(뭉개짐은
         * 사라졌지만 여전히 화면을 덮는 덩어리였다). **두 번 다 크기가 원인이었지
         * 표현 방식이 원인이 아니었다.**
         *
         * 그래서 크기를 줄이고 팩으로 돌아온다. 팩 시트는 128x128 열 장짜리 실제
         * 베는 모션이라, 작아도 "벴다"로 읽힌다. 배율은 정수 2 이하로 묶는다.
         */
        private static SkillPerformer WirePerformer(GameObject samurai, PlayerCombat combat,
                                                    Onikiri.UI.SkillNameFlash nameFlash,
                                                    Onikiri.UI.ScreenFlash screenFlash)
        {
            var performer = samurai.GetComponent<SkillPerformer>();
            if (performer == null) performer = samurai.AddComponent<SkillPerformer>();

            var slashPrefab = BuildSlashPrefab();
            var streakPrefab = BuildStreakPrefab();
            var afterimagePrefab = BuildAfterimagePrefab();
            var vfxRoot = GameObject.Find("Battle").transform.Find("VFX");

            var so = new SerializedObject(performer);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("samuraiRenderer").objectReferenceValue = samurai.GetComponent<SpriteRenderer>();
            so.FindProperty("vfxParent").objectReferenceValue = vfxRoot;
            so.FindProperty("slashPrefab").objectReferenceValue = slashPrefab;
            so.FindProperty("streakPrefab").objectReferenceValue = streakPrefab;
            so.FindProperty("afterimagePrefab").objectReferenceValue = afterimagePrefab;
            so.FindProperty("nameFlash").objectReferenceValue = nameFlash;
            so.FindProperty("screenFlash").objectReferenceValue = screenFlash;

            // 무기 티어 스파크가 든 라이브러리 (51단계). 은백 굽기는
            // PozacVfxBaker가 같은 애셋에 넣는다
            so.FindProperty("glowLibrary").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<VfxLibrary>(PozacVfxBaker.LibraryPath);

            // 3 -> 6 (51단계). 참격 한 장 위에 티어 스파크가 최대 세 겹
            // 얹히므로, 귀참 클립(0.42초)과 스파크(0.4초 안팎)가 겹치는 순간
            // 넷이 동시에 떠 있다. 남은 둘은 오의가 겹칠 때의 여유다
            so.FindProperty("slashPrewarm").intValue = 6;
            so.FindProperty("streakPrewarm").intValue = 2;
            so.FindProperty("afterimagePrewarm").intValue = 4;
            // 알파 0.5로 뒀더니 실측에서 첫 장이 0.17까지 내려가 화면에 안 보였다.
            // 0.8이면 가장 옅은 장도 0.36에서 시작한다
            so.FindProperty("afterimageTint").colorValue = new Color(0.78f, 0.86f, 1f, 0.8f);

            // ------------------------------------------------------------ 안무 셋

            var chain = new List<Sprite>();
            var attack1 = OrderedSprites(SamuraiSprites + "ATTACK 1.png");
            var attack2 = OrderedSprites(SamuraiSprites + "ATTACK 2.png");
            var attack3 = OrderedSprites(SamuraiSprites + "ATTACK 3.png");
            chain.AddRange(attack1);
            chain.AddRange(attack2);
            chain.AddRange(attack3);

            // 이어 붙인 클립의 타격 프레임. 각 시트의 실측 타격 프레임에 앞 시트의
            // 길이를 더한다 - 숫자를 손으로 적으면 시트가 한 프레임 늘어나는 날
            // 타격이 조용히 그림에서 떨어진다
            var chainHits = new[]
            {
                Attack1ImpactFrame,
                attack1.Count + Attack2ImpactFrame,
                attack1.Count + attack2.Count + Attack3ImpactFrame
            };

            var dash = OrderedSprites(SamuraiSprites + "DASH.png");
            var special = OrderedSprites(SamuraiSprites + "SPECIAL ATTACK.png");

            var list = so.FindProperty("choreographies");
            list.arraySize = SkillCatalog.Count;

            // 연참 - 제자리 연속 베기. **참격을 얹지 않는다. 27/28/29단계 모두 그대로다.**
            //
            // 클립에 원화가가 그린 궤적이 세 번 나오므로, 그 위에 무엇을 얹든
            // 23단계가 걷어낸 이중 참격이다. 27단계 플레이 소감에서 연참만 좋다고
            // 나온 이유이기도 하다 - 그린 사람이 그린 참격이 언제나 가장 잘 맞는다.
            var noSlash = new SlashSpec();
            WriteChoreography(list.GetArrayElementAtIndex(0), SkillCatalog.ChainSlashId,
                chain, 40f, chainHits,
                usesSlash: false, slash: noSlash,
                pierceRange: 0f, pierceHeight: 0f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 1.4f, shake: 1.1f, perHitShake: 0.55f,
                numberSize: 1, flash: false);

            // 일섬 - **거합 돌진.** 팩 참격을 쓰지 않는다.
            //
            // ## 왜 팩을 뺐는가
            //
            // 29단계에는 Slash2를 썼는데 그 시트가 직선 돌진과 두 군데서 어긋났다:
            //
            //   굽은 사선     돌진은 직선인데 이펙트가 휘어 경로가 둘로 읽힌다
            //   큰 흰 폭발    맞은 자리를 덮어 무엇이 몇 대 맞았는지 안 보인다
            //
            // 팩에는 얇은 수평 섬광이 없다. 그래서 일섬만 섬광을 구워 쓴다
            // (`DashStreak`). 귀참은 팩 그대로다.
            //
            // 큰 흰 폭발이 사라진 자리는 **23단계 붉은 스파크**가 채운다 -
            // `PlayerCombat.DeliverSkillHit`가 맞은 요괴마다 이미 하나씩 낸다.
            // 오의 전용 폭발을 따로 두지 않는 것이 원래 규칙이었다.
            //
            // ## 거합 타이밍
            //
            //   0.00~0.09  돌진. 섬광이 칼끝을 따라 자란다. 아직 아무도 안 베인다
            //   0.09~0.30  복귀. 섬광이 잠시 머물다 옅어진다
            //   0.231      **타격.** 지나가고 반 박자 뒤에 요괴들이 한꺼번에
            //              번쩍이고 숫자가 뜬다. 히트스톱도 여기서 짧게
            //
            // 타격을 DASH 3번(0.115초) -> 6번(0.231초) 프레임으로 옮긴 것이 전부다.
            // **타격 횟수는 그대로 1회**이므로 총 데미지와 밴드가 움직이지 않는다.
            WriteChoreography(list.GetArrayElementAtIndex(1), SkillCatalog.FlashId,
                dash, 26f, new[] { DashLateHitFrame },
                usesSlash: false, slash: new SlashSpec(),
                pierceRange: 4.6f, pierceHeight: 2.6f,
                // **돌진을 1.9 -> 4.2u로 늘렸다.** 요괴 열이 대략 x 0.8~3.7에 서므로
                // 1.9로는 그 앞에서 멈춘다 - "적 사이로 지나간다"가 되려면 통과해야
                // 한다. 앵커(-1.2)에서 4.2면 3.0까지 나간다.
                //
                // 복귀 0.21초는 클립 길이(8f/26fps = 0.308초)에 맞춘 값이다.
                // 0.09+0.21 = 0.30 <= 0.308 - 넘기면 클립이 먼저 끝나 시전이
                // 해제되고, LungeOffsetX가 0으로 튀면서 사무라이가 순간이동한다
                lunge: 4.2f, lungeOut: 0.09f, lungeBack: 0.21f, afterimages: 3,
                // 히트스톱을 1.9 -> 1.2로 줄였다. 거합의 무게는 긴 정지가 아니라
                // **늦게 오는 타격**이 낸다
                hitStop: 1.2f, shake: 1.9f, perHitShake: 0f,
                numberSize: 2, flash: false,
                streak: new StreakSpec
                {
                    used = true,
                    thickness = 0.18f,      // 얇게. 굵으면 다시 덩어리다
                    height = -0.42f,        // 요괴 몸통 (지상 -0.16~0.71 / 도깨비불 0.19~1.03)
                    color = Color.white,    // 색은 텍스처에 구워져 있다 (흰 심 + 붉은 옆면)
                    reveal = 0.09f,         // 돌진이 나가는 시간과 같아야 머리가 칼끝에 붙는다
                    hold = 0.08f,
                    fade = 0.10f
                });

            // 귀참 - 화면 광역. 가장 무거운 한 방.
            // 팩 Slash3(큰 초승달) color2(흑적 - 검은 심 + 붉은 테두리).
            //
            // **Slash1이 아니라 Slash3을 골랐다.** 사양에서 예로 든 Slash1의 ">" 호는
            // 7~8번 프레임이 큰 단색 부채꼴이라, 크기를 줄여도 화면에 뜨는 것이
            // 궤적이 아니라 면이다 - 피하려던 "덩어리"가 작아진 채로 그대로 온다.
            // Slash3은 열 프레임 내내 초승달의 두께만 변해서 **베고 지나간 자국**으로
            // 읽힌다. 색은 사양대로 color2다 - 검은 심 덕분에 밝은 벚꽃 배경에서도,
            // 붉은 테두리 덕분에 밤 배경에서도 형태가 남는다.
            //
            // 배율 2는 셋 중 가장 크지만 원본 픽셀의 두 배다. 128px 프레임이
            // 32 PPU에서 4u이므로 2배면 8u 칸이고, 그 안에 실제로 그려진 초승달은
            // 약 5u다 - **사무라이 키(3u)의 1.7배**, 화면 폭(8.03u)의 62%.
            // 화면을 덮지 않는다.
            //
            // 화면 전체 타격은 그림이 아니라 `DeliverAll`이 한다. 이펙트가 맞는
            // 것들을 물리적으로 덮을 필요가 없다는 것이 이번 정정의 핵심이다.
            WriteChoreography(list.GetArrayElementAtIndex(2), SkillCatalog.OniCleaveId,
                special, 24f, new[] { SpecialImpactFrame },
                usesSlash: true,
                slash: new SlashSpec
                {
                    frames = SliceSlashSheet("Slash3", 2),
                    frameRate = 24f,        // SPECIAL 클립(14f/24fps = 0.58초) 안에서 끝난다
                    scale = 2f,
                    // 원본 초승달은 "위로 볼록"이다. -90도면 "오른쪽으로 볼록"인
                    // 곧은 세로 호가 되는데, 그러면 5.0u가 통째로 세로로 서서
                    // 전투 띠(약 3.5u)를 위아래로 넘친다. -55도로 눕히면 세로
                    // 폭이 5.0*cos35 = 4.1u로 줄고, 모양도 "가로막는 벽"이 아니라
                    // **비스듬히 내려긋는 베기**가 된다
                    angle = -55f,
                    // **앵커는 호의 곡률 중심이지 그림의 자리가 아니다.**
                    //
                    // 피벗을 절정 프레임의 잉크 경계 한가운데로 잡았는데, 초승달은
                    // 굽은 선이라 그 경계 한가운데가 **오목한 안쪽의 빈 곳**이다.
                    // 실측으로 앵커를 0.92에 두자 붉은 호는 2.25에 떴다 - 1.3u
                    // 차이가 곧 호의 반지름이다.
                    //
                    // 그래서 앵커를 사무라이 바로 앞에 두고 호가 앞으로 부풀게
                    // 한다. 칼을 쥔 손이 중심이고 궤적이 뻗어 나가는 모양이라,
                    // 우연히도 이쪽이 실제 베기와 더 닮았다.
                    forward = 0.8f, height = 0.15f
                },
                pierceRange: 0f, pierceHeight: 0f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 3.0f, shake: 2.8f, perHitShake: 0f,
                numberSize: 2, flash: true);

            // 귀참의 무기 티어 변형 (51단계). 팩의 5색이 이미 있으므로 같은
            // Slash3 시트를 색만 바꿔 다섯 벌 자른다 - slashFrames(위의
            // color2)는 티어 배선이 빠진 씬의 폴백으로 남는다
            WriteTierSlashes(list.GetArrayElementAtIndex(2), "Slash3");

            // ------------------------------------------------------------ 49단계: 신규 다섯
            //
            // ## 몸은 있는 클립을 돌려 쓰고, 갈리는 것은 이펙트다
            //
            // 사무라이에게 있는 공격 클립은 다섯 벌(ATTACK 1/2/3 · DASH ·
            // SPECIAL)뿐이고, 오의는 이제 여덟이다. 새 몸 애니를 그리지 않는 것이
            // 이 스텝의 전제이므로(값싼 콘텐츠 원칙 - 35단계 지역 4와 같은 판단)
            // 몸은 겹칠 수밖에 없다.
            //
            // 겹쳐도 되는 이유는 **화면에서 읽히는 것이 몸이 아니기 때문**이다.
            // 23단계 이후 오의를 가르는 신호는 이펙트·이름 플래시·데미지 숫자
            // 셋이고, 사무라이의 팔 각도를 보고 오의를 구분하는 사람은 없다.
            // 실제로 그것이 27단계가 "색만 다른 같은 아크"를 문제 삼은 이유이기도
            // 하다 - 그때 같았던 것은 몸이 아니라 **이펙트**였다.
            //
            // 자리와 배율은 여기서 정하지 않는다. VfxLibrary가 클립마다 들고
            // 있고(굽는 시점에 정해진 값), 이 아래는 그것을 그대로 읽는다 -
            // 같은 조각이 부르는 곳마다 다른 크기로 뜨는 것을 막는 장치다.

            // 혈파동 - 발밑에서 퍼지는 지면 파동. 화면 전체(Screen).
            // 귀참과 같은 SPECIAL 몸을 쓰지만 그림이 정반대다 - 저쪽은 앞을
            // 가르는 초승달이고 이쪽은 발밑에서 사방으로 퍼지는 고리다
            WriteChoreography(list.GetArrayElementAtIndex(3), SkillCatalog.BloodWaveId,
                special, 24f, new[] { SpecialImpactFrame },
                usesSlash: true, slash: VfxSlash(SkillCatalog.SkillVfx.WaveRing),
                pierceRange: 0f, pierceHeight: 0f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 2.0f, shake: 2.2f, perHitShake: 0f,
                numberSize: 2, flash: false);

            // 낙혈 - 전방 일렬 관통. 48단계 하베스트의 핏빛 파도가 앞으로 솟는다.
            // 일섬과 같은 Pierce지만 **돌진이 없다** - 저쪽은 지나가며 베고
            // 이쪽은 서서 앞으로 밀어낸다. 사거리도 조금 짧다
            WriteChoreography(list.GetArrayElementAtIndex(4), SkillCatalog.BloodFallId,
                attack2, 22f, new[] { Attack2ImpactFrame },
                usesSlash: true, slash: VfxSlash(SkillCatalog.SkillVfx.Wave),
                pierceRange: 4.0f, pierceHeight: 2.6f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 1.6f, shake: 1.6f, perHitShake: 0f,
                numberSize: 2, flash: false);

            // 혈륜 - 플레이어를 감고 도는 회오리. 다섯 번 때린다.
            //
            // **타격 프레임을 회오리에 맞춘다.** 다른 오의는 몸 클립의 그려진
            // 참격에 맞추는데(23단계 규칙), 이 오의는 화면에 그려진 것이 몸이
            // 아니라 회오리다 - 여덟 프레임이 두 겹으로 감기고, 그 감김마다
            // 한 대씩이다. chain 클립(20f @40fps = 0.50초)이 회오리(8f @16fps
            // = 0.50초)와 길이가 같아서 프레임을 그대로 나눠 걸 수 있다
            WriteChoreography(list.GetArrayElementAtIndex(5), SkillCatalog.BloodWheelId,
                chain, 40f, new[] { 2, 6, 10, 14, 18 },
                usesSlash: true, slash: VfxSlash(SkillCatalog.SkillVfx.Vortex),
                pierceRange: 0f, pierceHeight: 0f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 1.3f, shake: 1.0f, perHitShake: 0.45f,
                numberSize: 1, flash: false);

            // 혈폭 - 단발 최대. 요괴 몸통에서 터진다.
            //
            // 셋 중 가장 무겁다(무게 2, 귀참과 같은 등급). 광역이 아니라
            // 단일 대상이므로 보스전에서 가장 크게 뜨는 숫자가 이것이고,
            // 화면 번쩍임도 준다 - 귀참 말고 번쩍이는 유일한 오의다
            WriteChoreography(list.GetArrayElementAtIndex(6), SkillCatalog.BloodBurstId,
                special, 24f, new[] { SpecialImpactFrame },
                usesSlash: true, slash: VfxSlash(SkillCatalog.SkillVfx.Burst),
                pierceRange: 0f, pierceHeight: 0f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 3.0f, shake: 2.6f, perHitShake: 0f,
                numberSize: 2, flash: true);

            // 혈조 - 멀리 뻗는 채찍. 여덟 중 쿨이 가장 짧다(6초).
            //
            // 낙혈과 같은 Pierce인데 **사거리가 더 길고 세로가 얇다.** 채찍은
            // 한 줄로 뻗는 그림이라 그 얇음이 곧 거동이고, 그래서 지면의 요괴는
            // 다 걸리지만 높이 뜬 도깨비불은 놓친다 - 6초 쿨의 대가다
            WriteChoreography(list.GetArrayElementAtIndex(7), SkillCatalog.BloodWhipId,
                attack3, 24f, new[] { Attack3ImpactFrame },
                usesSlash: true, slash: VfxSlash(SkillCatalog.SkillVfx.Whip),
                pierceRange: 5.4f, pierceHeight: 1.6f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 1.0f, shake: 0.9f, perHitShake: 0f,
                numberSize: 1, flash: false);

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(string.Format(
                "[Onikiri] Skill choreography: 연참 chain {0}f hits [{1}] (참격 없음) / "
                + "일섬 DASH {2}f + 돌진 섬광 4.2u (타격 f6) / 귀참 SPECIAL {3}f + Slash3_color2 x2 / "
                + "49단계 다섯: 혈파동·낙혈·혈륜·혈폭·혈조 (VfxLibrary 클립).",
                chain.Count, string.Join(",", System.Array.ConvertAll(chainHits, h => h.ToString())),
                dash.Count, special.Count));

            return performer;
        }

        /**
         * @brief VfxLibrary의 클립 하나를 안무의 참격 칸으로 옮긴다.
         *
         * **배율·각도·자리를 여기서 다시 적지 않는다.** 라이브러리가 굽는
         * 시점에 정한 값을 들고 있고(VfxLibrary 머리 주석), 부르는 쪽마다
         * 값을 다시 적으면 같은 조각이 화면마다 다른 크기로 뜬다 - 그것을
         * 막으려고 라이브러리를 애셋으로 만든 것이다.
         *
         * 두 라이브러리를 차례로 본다. 48단계 하베스트(요괴에게서 뜯은 것)와
         * 49단계 Pozac(팩에서 구운 것)이고, 이름은 둘을 합쳐 유일하다
         * (SkillShapeTests.EverySkillEffect_BelongsToExactlyOneSkill).
         */
        private static SlashSpec VfxSlash(string clipId)
        {
            var clip = FindVfxClip(clipId);
            if (clip == null)
            {
                Debug.LogError("[Onikiri] VFX clip '" + clipId + "' missing - run "
                               + "Onikiri/Art/Harvest Yokai VFX and Onikiri/Art/Bake Pozac VFX.");
                return new SlashSpec();
            }

            return new SlashSpec
            {
                frames = clip.frames,
                frameRate = clip.frameRate,
                scale = clip.scale,
                angle = clip.angle,
                forward = clip.forwardOffset,

                // **라이브러리의 높이는 발밑 기준이고 안무의 높이는 그려진 중심
                // 기준이다.** 두 원점이 다르다 - 48d가 "스프라이트 칸은 위치
                // 기준이 못 된다"로 정리한 자리에서 요괴 쪽은 발밑으로 옮겼는데,
                // 플레이어 안무(SkillPerformer.SpawnSlash)는 27단계부터
                // samuraiRenderer.bounds.center를 쓴다.
                //
                // 사무라이의 그려진 중심은 발밑에서 약 0.85u다(96px 셀에 그려진
                // 키가 약 54px, 32 PPU). 그만큼 빼야 라이브러리가 말하는 높이에
                // 실제로 놓인다
                height = clip.heightOffset - SamuraiCenterHeight
            };
        }

        /**
         * @brief 사무라이 스프라이트의 **중심이 발보다 얼마나 위인가** (월드 단위).
         *
         * ## 상수로 안 적고 스프라이트에서 잰다
         *
         * 눈대중으로 0.85를 적었다가 실기에서 0.18u 어긋났다 - 48c가 초승달에서
         * 두 번 틀린 자리를 그대로 반복할 뻔한 자리다. 실측값은 1.031이고,
         * 그 값은 어림이 아니라 **피벗에서 나온다**:
         *
         *     중심 - 발 = (칸 높이 / 2 - 피벗 y) / PPU
         *              = (96 / 2 - 15) / 32 = 1.031
         *
         * 피벗은 슬라이서가 그려진 발선을 실측해 넣은 값이므로(CharacterSpriteSlicer),
         * 여기서 다시 재면 아트가 바뀌어도 따라간다. 상수로 박으면 그때 조용히 어긋난다.
         */
        private static float SamuraiCenterHeight
        {
            get
            {
                var idle = AssetDatabase.LoadAssetAtPath<Sprite>(SamuraiSprites + "IDLE.png");
                if (idle == null) return 1.031f;   // 아트가 없으면 실측값으로 떨어진다

                return (idle.rect.height * 0.5f - idle.pivot.y) / idle.pixelsPerUnit;
            }
        }

        private static VfxLibrary.Clip FindVfxClip(string clipId)
        {
            string[] libraries = { YokaiVfxBaker.LibraryPath, PozacVfxBaker.LibraryPath };

            foreach (var path in libraries)
            {
                var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(path);
                if (library == null) continue;

                var clip = library.Find(clipId);
                if (clip != null && clip.frames != null && clip.frames.Length > 0) return clip;
            }
            return null;
        }

        /** 참격 값 묶음. 인자 목록이 스물을 넘어가 읽을 수 없어서 뽑았다 */
        private struct SlashSpec
        {
            public Sprite[] frames;
            public float frameRate;
            public float scale;
            public float angle;
            public float forward;
            public float height;
        }

        /** 돌진 섬광 값 묶음. 일섬만 쓴다 */
        private struct StreakSpec
        {
            public bool used;
            public float thickness;
            public float height;
            public Color color;
            public float reveal;
            public float hold;
            public float fade;
        }

        private static void WriteChoreography(SerializedProperty element, string id,
                                              List<Sprite> clip, float frameRate, int[] hitFrames,
                                              bool usesSlash, SlashSpec slash,
                                              float pierceRange, float pierceHeight,
                                              float lunge, float lungeOut, float lungeBack,
                                              int afterimages,
                                              float hitStop, float shake, float perHitShake,
                                              int numberSize, bool flash,
                                              StreakSpec streak = default(StreakSpec))
        {
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("clipFrameRate").floatValue = frameRate;
            AssignSprites(element.FindPropertyRelative("clip"), clip);

            var hits = element.FindPropertyRelative("hitFrames");
            hits.arraySize = hitFrames.Length;
            for (int i = 0; i < hitFrames.Length; i++)
                hits.GetArrayElementAtIndex(i).intValue = hitFrames[i];

            element.FindPropertyRelative("usesSlash").boolValue = usesSlash;

            var slashFrames = element.FindPropertyRelative("slashFrames");
            int frameCount = slash.frames != null ? slash.frames.Length : 0;
            slashFrames.arraySize = frameCount;
            for (int i = 0; i < frameCount; i++)
                slashFrames.GetArrayElementAtIndex(i).objectReferenceValue = slash.frames[i];

            // 티어 변형은 기본 0이다. 쓰는 안무(귀참)만 WriteTierSlashes가
            // 뒤에서 채운다 - 여기서 지워둬야 다시 돌릴 때 옛 배선이 안 남는다
            element.FindPropertyRelative("slashTierFrames").arraySize = 0;

            element.FindPropertyRelative("slashFrameRate").floatValue = slash.frameRate;
            element.FindPropertyRelative("slashScale").floatValue = slash.scale;
            element.FindPropertyRelative("slashAngle").floatValue = slash.angle;
            element.FindPropertyRelative("slashForwardOffset").floatValue = slash.forward;
            element.FindPropertyRelative("slashHeightOffset").floatValue = slash.height;

            element.FindPropertyRelative("usesStreak").boolValue = streak.used;
            element.FindPropertyRelative("streakThickness").floatValue = streak.thickness;
            element.FindPropertyRelative("streakHeightOffset").floatValue = streak.height;
            element.FindPropertyRelative("streakColor").colorValue = streak.color;
            element.FindPropertyRelative("streakRevealSeconds").floatValue = streak.reveal;
            element.FindPropertyRelative("streakHoldSeconds").floatValue = streak.hold;
            element.FindPropertyRelative("streakFadeSeconds").floatValue = streak.fade;
            element.FindPropertyRelative("pierceRange").floatValue = pierceRange;
            element.FindPropertyRelative("pierceHeight").floatValue = pierceHeight;
            element.FindPropertyRelative("lungeDistance").floatValue = lunge;
            element.FindPropertyRelative("lungeOutSeconds").floatValue = lungeOut;
            element.FindPropertyRelative("lungeBackSeconds").floatValue = lungeBack;
            element.FindPropertyRelative("afterimageCount").intValue = afterimages;
            element.FindPropertyRelative("hitStopMultiplier").floatValue = hitStop;
            element.FindPropertyRelative("shakeMultiplier").floatValue = shake;
            element.FindPropertyRelative("perHitShakeMultiplier").floatValue = perHitShake;
            element.FindPropertyRelative("numberSizeMultiple").intValue = numberSize;
            element.FindPropertyRelative("screenFlash").boolValue = flash;
        }

        /**
         * @brief 안무 하나에 무기 티어(1~5)별 참격 프레임을 채운다 (51단계).
         *
         * 색 번호는 여기서 정하지 않는다 - WeaponVfxTier.SlashColorOf가 단일
         * 출처다(청 -> 보라 -> 주황 -> 흑적 x2). 빌더가 색을 따로 적으면
         * 런타임의 스파크 램프와 참격 램프가 갈리는 날이 온다.
         */
        private static void WriteTierSlashes(SerializedProperty element, string shape)
        {
            var tiers = element.FindPropertyRelative("slashTierFrames");
            tiers.arraySize = WeaponVfxTier.MaxTier;

            for (int tier = WeaponVfxTier.MinTier; tier <= WeaponVfxTier.MaxTier; tier++)
            {
                var frames = SliceSlashSheet(shape, WeaponVfxTier.SlashColorOf(tier));
                var slot = tiers.GetArrayElementAtIndex(tier - 1)
                                .FindPropertyRelative("frames");

                slot.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    slot.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
        }

        private static void AssignSprites(SerializedProperty array, List<Sprite> sprites)
        {
            array.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static List<Sprite> OrderedSprites(string sheetPath)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return sprites;
        }

        /**
         * @brief 참격 프리팹. 스프라이트 렌더러 하나뿐이다.
         *
         * 28단계의 메시 프리팹(서브메시 둘 + 전용 셰이더 + 재료 둘)이 통째로
         * 사라진 자리다. 팩 애니를 재생하는 데는 렌더러 하나면 된다.
         */
        private static PackSlash BuildSlashPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlashPrefabPath);
            if (existing == null)
            {
                var root = new GameObject("PackSlash");
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrders.Vfx;
                root.AddComponent<PackSlash>();

                existing = PrefabUtility.SaveAsPrefabAsset(root, SlashPrefabPath);
                Object.DestroyImmediate(root);
            }

            var slash = existing.GetComponent<PackSlash>();

            var so = new SerializedObject(slash);
            so.FindProperty("spriteRenderer").objectReferenceValue = existing.GetComponent<SpriteRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);

            return slash;
        }

        /**
         * @brief 팩 시트 한 장을 열 프레임으로 자르고 순서대로 돌려준다.
         *
         * 팩은 Single 스프라이트로 임포트되어 있어서 그대로는 정적인 한 장이다.
         * **여기서 잘라야 애니가 된다** - 자르지 않고 쓰는 것이 27단계에 한 장을
         * 키워 쓴 실수의 출발점이었다.
         *
         * 프레임 순서는 왼쪽 위 -> 오른쪽, 그다음 아랫줄이다. Unity 텍스처는
         * 좌하단이 원점이라 **행을 뒤집어야** 시트에 그려진 순서와 맞는다.
         *
         * 이름을 `Slash3_color2_00` 처럼 0을 채워 붙이는 이유는 정렬 때문이다 -
         * `_1`, `_10`, `_2` 순으로 읽히면 애니가 뒤섞인다.
         *
         * 45b에 요도 빌더에게도 열었다. 영체가 같은 팩 시트를 쓰는데
         * (YodoPanelBuilder의 시그니처 연출) 자르는 코드를 한 벌 더 두면
         * 피벗 계산이 두 곳에 살고, 두 곳이 갈리는 날 참격이 영체 옆에서
         * 어긋난다. 같은 시트를 두 번 자르는 것도 아니다 - 임포터 설정을
         * 고치는 일이라 두 번째부터는 그대로 읽는다.
         */
        public static Sprite[] SliceSlashSheet(string shape, int color)
        {
            string path = SlashPackFolder + "Slash_128x128_" + shape + "_color" + color + ".png";

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Onikiri] Slash pack sheet missing: " + path);
                return new Sprite[0];
            }

            int frames = SlashSheetColumns * SlashSheetRows;
            string prefix = shape + "_color" + color + "_";

            // 잉크가 실제로 놓인 자리를 재서 피벗을 잡는다. 아래 주석 참고
            var pivot = ContentPivot(path, importer);

            var sheet = new SpriteMetaData[frames];
            for (int i = 0; i < frames; i++)
            {
                int column = i % SlashSheetColumns;
                int row = i / SlashSheetColumns;

                sheet[i] = new SpriteMetaData
                {
                    name = prefix + i.ToString("00"),
                    // 행 뒤집기. 위 주석 참고
                    rect = new Rect(column * SlashFrameSize,
                                    (SlashSheetRows - 1 - row) * SlashFrameSize,
                                    SlashFrameSize, SlashFrameSize),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot
                };
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = sheet;
            // 게임 아트와 같은 격자에 얹혀야 톤이 맞는다. 32 PPU에서
            // 128px 프레임 = 4 월드 단위다
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var ordered = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var sprite = asset as Sprite;
                if (sprite != null) ordered.Add(sprite);
            }
            ordered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            if (ordered.Count != frames)
                Debug.LogWarning("[Onikiri] " + shape + "_color" + color + " sliced into "
                                 + ordered.Count + " frames, expected " + frames);

            return ordered.ToArray();
        }

        /**
         * @brief 피벗 = **잉크가 가장 많은 프레임**의 잉크 한가운데.
         *
         * ## 왜 프레임 중심(0.5, 0.5)을 못 쓰는가
         *
         * 팩 프레임 안에서 그림은 가운데 있지 않다. 피벗을 프레임 중심에 두고
         * -90도 돌리면 **회전축과 그림 사이의 거리만큼 그림이 날아간다.** 실측으로
         * 참격이 앵커(0.40, 0.97) 대신 화면 오른쪽 끝으로 밀려 잘렸고, 배율 2배가
         * 그 어긋남까지 2배로 키웠다.
         *
         * ## 왜 열 장의 합집합도 못 쓰는가
         *
         * 처음에 그렇게 했다. 열 장을 겹치면 프레임을 거의 다 덮어서 결과가
         * (65, 69.5)/128 - **프레임 중심과 사실상 같았고, 아무것도 고쳐지지
         * 않았다.** 합집합은 "어디에 그려졌나"가 아니라 "어디까지 갔나"를 잰다.
         *
         * ## 왜 프레임마다 따로 구하지 않는가
         *
         * 팩은 **프레임 안에서 그림을 옮기고 키우면서** 애니메이션한다. 프레임마다
         * 제 잉크 중심으로 피벗을 잡으면 그 움직임이 통째로 상쇄되어, 참격이
         * 지나가지 않고 제자리에서 커졌다 작아진다.
         *
         * 그래서 **한 프레임을 대표로 뽑아 그 중심을 열 장이 공유한다.** 잉크가
         * 가장 많은 프레임이 곧 플레이어가 "참격"으로 보는 그림이므로, 그것을
         * 앵커에 맞추면 나머지는 그 주위에서 자란다.
         *
         * 읽기를 잠깐 켰다 되돌린다 - 픽셀을 읽으려면 필요하고, 켠 채로 두면
         * 빌드에 텍스처 사본이 하나 더 들어간다.
         */
        private static Vector2 ContentPivot(string path, TextureImporter importer)
        {
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pixels = texture.GetPixels();
            int width = texture.width;

            int frames = SlashSheetColumns * SlashSheetRows;
            var counts = new int[frames];
            var minX = new int[frames]; var maxX = new int[frames];
            var minY = new int[frames]; var maxY = new int[frames];
            for (int i = 0; i < frames; i++)
            {
                minX[i] = int.MaxValue; minY[i] = int.MaxValue;
                maxX[i] = int.MinValue; maxY[i] = int.MinValue;
            }

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a <= 0.004f) continue;

                    int column = x / SlashFrameSize;
                    // 텍스처는 좌하단 원점이라 위 줄이 row 0이다
                    int row = SlashSheetRows - 1 - (y / SlashFrameSize);
                    int frame = row * SlashSheetColumns + column;
                    if (frame < 0 || frame >= frames) continue;

                    int localX = x % SlashFrameSize;
                    int localY = y % SlashFrameSize;

                    counts[frame]++;
                    if (localX < minX[frame]) minX[frame] = localX;
                    if (localX > maxX[frame]) maxX[frame] = localX;
                    if (localY < minY[frame]) minY[frame] = localY;
                    if (localY > maxY[frame]) maxY[frame] = localY;
                }
            }

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            int peak = 0;
            for (int i = 1; i < frames; i++)
                if (counts[i] > counts[peak]) peak = i;

            if (counts[peak] == 0) return new Vector2(0.5f, 0.5f);

            var pivot = new Vector2(
                (minX[peak] + maxX[peak] + 1) * 0.5f / SlashFrameSize,
                (minY[peak] + maxY[peak] + 1) * 0.5f / SlashFrameSize);

            Debug.Log(string.Format(
                "[Onikiri] {0}: peak frame {1} ({2}px ink) spans {3}..{4} x {5}..{6} "
                + "-> pivot ({7:0.000}, {8:0.000}), ink {9}x{10}px",
                System.IO.Path.GetFileNameWithoutExtension(path),
                peak, counts[peak], minX[peak], maxX[peak], minY[peak], maxY[peak],
                pivot.x, pivot.y,
                maxX[peak] - minX[peak] + 1, maxY[peak] - minY[peak] + 1));

            return pivot;
        }

        /**
         * @brief 돌진 섬광 프리팹 + 그 텍스처.
         *
         * 팩에 얇은 수평 섬광이 없어서 이것만 굽는다. 귀참은 팩 그대로다.
         */
        private static DashStreak BuildStreakPrefab()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BuildStreakTexture());

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StreakPrefabPath);
            if (existing == null)
            {
                var root = new GameObject("DashStreak");
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrders.Vfx;
                root.AddComponent<DashStreak>();

                existing = PrefabUtility.SaveAsPrefabAsset(root, StreakPrefabPath);
                Object.DestroyImmediate(root);
            }

            var streak = existing.GetComponent<DashStreak>();
            var spriteRenderer = existing.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = SortingOrders.Vfx;

            var so = new SerializedObject(streak);
            so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);

            return streak;
        }

        /**
         * @brief 섬광 텍스처를 굽는다. 흰 심 + 붉은 옆면.
         *
         * ## 왜 색을 틴트로 주지 않는가
         *
         * 사양이 "흰/적"인데 Image·SpriteRenderer의 색은 **한 겹**이라 틴트
         * 하나로는 심과 옆면을 다르게 만들 수 없다. 28단계에 비네트를 검게 구워
         * 두고 살굿빛 틴트를 씌웠다가 검정 x 주황 = 검정이 나온 것과 같은 자리다.
         * 그래서 RGB를 텍스처에 굽고 틴트는 흰색으로 둔다.
         *
         * ## 왜 Bilinear인가
         *
         * 이 스프라이트만 픽셀 아트가 아니라 **그라디언트**다. 길이에 맞춰 가로로
         * 늘여 쓰므로 Point로 두면 늘어난 만큼 계단이 커진다. 게임 아트가 아니라
         * 빛이므로 격자에 얹힐 이유도 없다(비네트와 같은 판단).
         */
        private static string BuildStreakTexture()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(StreakTexturePath) != null) return StreakTexturePath;

            const int width = 256;
            const int height = 16;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                // 가운데에서의 거리. 0이 심, 1이 위아래 끝
                float d = Mathf.Abs((y + 0.5f) / height * 2f - 1f);

                // 심은 희고 옆면으로 갈수록 붉다. 얇은 선에서 색을 읽히게 하는
                // 유일한 방법이다 - 29단계 일섬에서 배웠다(얇은 것에 테두리로
                // 색을 입힐 수는 없다)
                var rgb = Color.Lerp(Color.white, new Color(1f, 0.20f, 0.12f),
                                     Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, d)));

                // **가운데 45%는 알파 1로 꽉 채운다.** 처음에 (1-d)^1.6으로
                // 부드럽게 떨어뜨렸더니 화면에 회색 실선 하나가 지나가는 것으로
                // 보였다 - 두께가 0.12u(약 19픽셀)뿐인데 그중 불투명한 것은
                // 가운데 몇 줄이라, 얇은 것을 더 얇게 만든 셈이었다.
                // 얇은 선은 **또렷해야** 보인다.
                float across = d <= 0.45f
                    ? 1f
                    : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.45f, 1f, d));

                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;

                    // 꼬리(0쪽)는 길게 옅어지고 머리(1쪽)는 짧게 끊긴다.
                    // 반대로 두면 날아가는 것으로 보인다
                    float tail = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.55f, u));
                    float head = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.94f, u));

                    var color = rgb;
                    color.a = across * tail * head;
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            EnsureFolder("Assets/_Project/Art/VFX");
            System.IO.File.WriteAllBytes(StreakTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(StreakTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(StreakTexturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Bilinear;   // 위 주석 참고
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Debug.Log("[Onikiri] Baked dash streak " + width + "x" + height + " -> " + StreakTexturePath);
            return StreakTexturePath;
        }

        /**
         * @brief 돌진 잔상 프리팹. 스프라이트 렌더러 하나뿐이다.
         *
         * 스프라이트는 런타임에 사무라이의 현재 프레임을 복사해 넣는다 - 새 아트를
         * 만들지 않는다는 규칙 그대로다.
         */
        private static Afterimage BuildAfterimagePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AfterimagePrefabPath);
            if (existing == null)
            {
                var root = new GameObject("Afterimage");
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrders.Player - 1;
                root.AddComponent<Afterimage>();

                existing = PrefabUtility.SaveAsPrefabAsset(root, AfterimagePrefabPath);
                Object.DestroyImmediate(root);
            }

            var ghost = existing.GetComponent<Afterimage>();

            var so = new SerializedObject(ghost);
            so.FindProperty("spriteRenderer").objectReferenceValue = existing.GetComponent<SpriteRenderer>();
            so.FindProperty("lifetime").floatValue = 0.16f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);

            return ghost;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }


        // ---------------------------------------------------------------- 화면

        private static float BandHeight
        {
            get
            {
                return DisplayConfig.DesignHeight
                       * (DisplayConfig.GrowthPanelTop - DisplayConfig.BottomTabBarTop);
            }
        }

        /** 스크롤 안쪽 목록의 높이. 여덟 줄이라 뷰포트보다 길다 */
        private static float PanelContentHeight
        {
            get { return SkillCatalog.Count * (RowHeight + RowGap); }
        }

        /** 스크롤 창의 높이. 띠에서 머리글과 슬롯 줄을 뺀 나머지 */
        private static float ViewportHeight
        {
            get
            {
                return BandHeight - TopPadding - HeaderHeight - HeaderGap
                       - SlotRowHeight - SlotRowGap - BottomPadding;
            }
        }

        /**
         * @brief 고정 부분(머리글 + 슬롯 줄)이 띠를 넘지 않는지 빌드가 검산한다.
         *
         * ## 49단계에 스크롤이 생겼다
         *
         * 26단계 주석은 "스크롤이 있으면 '더 있나?' 하고 끌어보게 된다 - 없는
         * 것을 찾게 만드는 UI다"라고 스크롤을 거부했고, 그 판단은 **오의가
         * 셋일 때** 옳았다. 여덟이 되면 목록이 1264px이라 720px 띠에 어떤
         * 배치로도 안 들어간다.
         *
         * 그리고 이제는 끌어볼 것이 실제로 있다 - 스크롤이 거짓말을 하지 않는다.
         * 대장간이 44단계에 같은 자리에서 같은 판단을 했다.
         *
         * 검산 대상이 바뀌었다: 목록 길이가 아니라 **뷰포트가 남는가**다.
         * 머리글과 슬롯 줄은 스크롤 밖에 고정되므로 그 둘이 띠를 다 먹으면
         * 목록이 0px가 되고, 그것은 화면에서 "스킬 탭이 비어 있다"로 나온다.
         */
        private static void VerifyPanelFits()
        {
            // 두 줄(행 하나 + 다음 행의 머리)은 보여야 목록으로 읽힌다.
            // 한 줄만 보이면 스크롤이 아니라 '한 칸짜리 창'이다
            float minimum = RowHeight + RowGap + RowHeight * 0.4f;
            if (ViewportHeight >= minimum) return;

            Debug.LogWarning(string.Format(
                "[Onikiri] Skill list viewport is only {0:F0}px (needs {1:F0}px). The header "
                + "({2:F0}px) and the slot row ({3:F0}px) have eaten the {4:F0}px band - "
                + "shrink one of them.",
                ViewportHeight, minimum, HeaderHeight, SlotRowHeight, BandHeight));
        }

        // ---------------------------------------------------------------- 스크롤

        /**
         * @brief 뷰포트와 목록을 세우고 목록의 RectTransform을 돌려준다.
         *
         * 대장간(EquipmentPanelBuilder)과 같은 값이다. 스크롤 감각은 화면마다
         * 다르면 안 되는 것 중 하나라 - 같은 손가락이 같은 속도로 움직여야 한다.
         */
        private static RectTransform BuildScroll(RectTransform panel)
        {
            var viewportObject = new GameObject(ViewportName, typeof(RectTransform));
            viewportObject.transform.SetParent(panel, false);

            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.sizeDelta = new Vector2(0f, ViewportHeight);
            viewport.anchoredPosition = new Vector2(0f,
                -(TopPadding + HeaderHeight + HeaderGap + SlotRowHeight + SlotRowGap));

            // 마스크가 있어야 목록이 슬롯 줄 위로 삐져 나오지 않는다.
            // RectMask2D는 이미지를 요구하지 않아 바탕을 한 겹 덜 그린다
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject(ContentName, typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);

            var content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, PanelContentHeight);
            content.anchoredPosition = Vector2.zero;

            var scroll = panel.gameObject.GetComponent<ScrollRect>();
            if (scroll == null) scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;

            return content;
        }

        // ---------------------------------------------------------------- 장착 슬롯 줄

        /**
         * @brief 네 칸. 지금 나가는 오의가 무엇인지 말하는 유일한 자리다.
         *
         * 칸 수는 SkillCurve.MaxSlots이고 그중 몇 개가 열려 있는지는 런타임이
         * 정한다(SkillSlotChip이 잠긴 칸을 자물쇠로 그린다). 빌더가 열린 수만큼만
         * 세우지 않는 이유는 41단계의 잠긴 미리보기 규칙이다 - 열릴 것이
         * 있다는 사실 자체가 진행의 이유이므로 감추지 않는다.
         */
        private static void BuildSlotRow(RectTransform panel, SkillSystem system, TMP_FontAsset font)
        {
            var existing = panel.Find(SlotRowName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(SlotRowName, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, SlotRowHeight);
            rect.anchoredPosition = new Vector2(0f, -(TopPadding + HeaderHeight + HeaderGap));

            var icons = SkillIcons();
            var lockGlyph = UiGlyphBuilder.Load(UiGlyphBuilder.Lock);

            float width = (DisplayConfig.DesignWidth - SidePadding * 2f
                           - SlotGap * (SkillCurve.MaxSlots - 1)) / SkillCurve.MaxSlots;

            for (int slot = 0; slot < SkillCurve.MaxSlots; slot++)
                BuildSlotChip(rect, system, font, slot, width, icons, lockGlyph);
        }

        private static void BuildSlotChip(RectTransform row, SkillSystem system, TMP_FontAsset font,
                                          int slot, float width, Sprite[] icons, Sprite lockGlyph)
        {
            var go = new GameObject("Slot" + slot, typeof(RectTransform));
            go.transform.SetParent(row, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(slot * (width + SlotGap), 0f);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var icon = CreateIcon(go.transform, null);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(SlotIconSize, SlotIconSize);
            iconRect.anchoredPosition = new Vector2(0f, -10f);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(label);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, 44f);
            labelRect.anchoredPosition = Vector2.zero;

            /**
             * @brief 쿨타임 표시 (슬레이어식). 아이콘과 같은 자리에 얹는다.
             *
             * 칩(SkillSlotChip)에 직접 넣지 않고 별도 컴포넌트로 두는 이유:
             * 칩은 이벤트(Changed)로만 다시 그리는데 쿨다운은 매 프레임
             * 변하는 값이라 갱신 리듬이 다르다. 섞으면 칩 전체가 폴링으로
             * 끌려간다. 아이콘 색을 만지지 않는 것도 같은 이유다 - 두
             * 컴포넌트가 한 색을 다투면 마지막에 쓴 쪽이 이긴다. 어둡게는
             * 아이콘 위의 그늘 판이 한다.
             *
             * 세 그림 다 시작은 꺼진 상태로 저장한다. 씬을 연 첫 프레임에
             * 쿨다운이 돌고 있을 리 없고, 돌기 시작하면 오버레이가 켠다.
             */
            var cool = new GameObject("Cooldown", typeof(RectTransform));
            cool.transform.SetParent(go.transform, false);
            var coolRect = (RectTransform)cool.transform;
            coolRect.anchorMin = new Vector2(0.5f, 1f);
            coolRect.anchorMax = new Vector2(0.5f, 1f);
            coolRect.pivot = new Vector2(0.5f, 1f);
            coolRect.sizeDelta = new Vector2(SlotIconSize, SlotIconSize);
            coolRect.anchoredPosition = new Vector2(0f, -10f);

            var shadeObject = new GameObject("Shade", typeof(RectTransform));
            shadeObject.transform.SetParent(cool.transform, false);
            var shadeRect = (RectTransform)shadeObject.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;
            var shade = shadeObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.38f);
            shade.raycastTarget = false;
            shade.enabled = false;

            var maskObject = new GameObject("Mask", typeof(RectTransform));
            maskObject.transform.SetParent(cool.transform, false);
            var maskRect = (RectTransform)maskObject.transform;
            // 폭만 잡아 두고 높이(anchorMin.y)는 런타임이 남은 비율로 민다
            maskRect.anchorMin = new Vector2(0f, 1f);
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = Vector2.zero;
            maskRect.offsetMax = Vector2.zero;
            var maskImage = maskObject.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.6f);
            maskImage.raycastTarget = false;
            maskImage.enabled = false;

            var time = CreateLabel(cool.transform, font, "Time", TextAlignmentOptions.Center);
            // 캡션 티어(33pt)로 내린다. 44pt는 80px 아이콘 위에서 그림을 반쯤
            // 덮었다 - 숫자는 확인하는 값이지 칸의 주인공이 아니다. 크기는
            // 아틀라스에 구운 단만 쓸 수 있다(PixelFontSizes)
            UiFonts.Demote(time);
            var timeRect = (RectTransform)time.transform;
            timeRect.anchorMin = Vector2.zero;
            timeRect.anchorMax = Vector2.one;
            timeRect.offsetMin = Vector2.zero;
            timeRect.offsetMax = Vector2.zero;
            // 상자는 아이콘 폭(80). "16.7"(33pt)도 그 안에 들지만, 폰트 단이
            // 바뀌어도 잘리지 않게 흘린다 - 가운데 정렬이라 양옆으로 균등하게
            // 넘치고, 칩 폭(약 240) 안에는 넉넉히 든다
            time.overflowMode = TextOverflowModes.Overflow;
            time.color = TextColor;
            time.raycastTarget = false;
            time.enabled = false;

            var overlay = cool.AddComponent<Onikiri.UI.SkillCooldownOverlay>();
            var overlaySo = new SerializedObject(overlay);
            overlaySo.FindProperty("system").objectReferenceValue = system;
            overlaySo.FindProperty("slot").intValue = slot;
            overlaySo.FindProperty("shade").objectReferenceValue = shade;
            overlaySo.FindProperty("mask").objectReferenceValue = maskRect;
            overlaySo.FindProperty("maskImage").objectReferenceValue = maskImage;
            overlaySo.FindProperty("label").objectReferenceValue = time;
            overlaySo.ApplyModifiedPropertiesWithoutUndo();

            var chip = go.AddComponent<Onikiri.UI.SkillSlotChip>();
            var so = new SerializedObject(chip);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("slot").intValue = slot;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("background").objectReferenceValue = image;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("filledTint").colorValue = UiSkin.Row;
            so.FindProperty("textColor").colorValue = TextColor;
            so.FindProperty("dimColor").colorValue = DimColor;
            so.FindProperty("iconTint").colorValue = UiIcons.Tint;
            so.FindProperty("lockGlyph").objectReferenceValue = lockGlyph;

            // 빈 칸에는 그림을 안 넣는다. 자물쇠를 재활용하면 "잠김"과 "비었음"이
            // 같은 그림이 되는데, 하나는 못 쓰는 것이고 하나는 지금 쓸 수 있는
            // 자리라 뜻이 정반대다. SkillSlotChip이 null이면 아이콘을 끈다
            so.FindProperty("emptyGlyph").objectReferenceValue = null;

            var iconList = so.FindProperty("skillIcons");
            iconList.arraySize = icons.Length;
            for (int i = 0; i < icons.Length; i++)
                iconList.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 카탈로그 순서의 오의 아이콘. 칩이 인덱스로 꺼내 쓴다 */
        private static Sprite[] SkillIcons()
        {
            var icons = new Sprite[SkillCatalog.Count];
            for (int i = 0; i < icons.Length; i++)
                icons[i] = UiIcons.For(SkillCatalog.Skills[i].Id);
            return icons;
        }

        private static RectTransform EnsurePanel(Transform safeArea)
        {
            var existing = safeArea.Find(PanelName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(PanelName, typeof(RectTransform));
            go.transform.SetParent(safeArea, false);

            // 성장 패널과 같은 띠. 하단 탭바 위, 전투 영역 아래
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, DisplayConfig.BottomTabBarTop);
            rect.anchorMax = new Vector2(1f, DisplayConfig.GrowthPanelTop);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 자기 바탕. 뒤의 성장 패널 행이 비쳐 보이면 두 목록이 겹친 것으로
            // 읽힌다. 하단 UI 바탕과 같은 먹빛이라 화면의 언어가 하나로 남는다
            var backdrop = go.AddComponent<Image>();
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTextureBuilder.WashiPath);
            backdrop.type = Image.Type.Tiled;
            backdrop.color = UiSkin.PanelInk;

            // 뒤의 성장 패널이 드래그를 받지 않게 막는다. 이 판이 레이캐스트를
            // 먹지 않으면 스킬 화면 위에서 끈 손가락이 뒤의 강화 목록을 스크롤한다
            backdrop.raycastTarget = true;

            // 화지 위의 벚가지 (39단계). 행이 덮는 부분은 안 보이고, 여백에만 남는다
            BackdropTextureBuilder.AddSakuraBranch(rect);

            return rect;
        }

        /** 빌더가 만들지 않은 자식은 이전 세대의 잔재다. 11단계 유령 텍스트와 같은 종류 */
        private static void PruneStrays(RectTransform panel)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                var child = panel.GetChild(i);
                if (child.name == "Header") continue;
                if (child.name == SlotRowName) continue;
                if (child.name == ViewportName) continue;
                if (child.name == BackdropTextureBuilder.BranchName) continue;
                if (child.name.StartsWith("Skill")) continue;
                Object.DestroyImmediate(child.gameObject);
            }
        }

        /**
         * @brief 제목 + 자동 시전 토글.
         *
         * 제목을 두는 이유는 이 화면에 탭 줄이 없기 때문이다. 성장 패널은 탭이
         * "지금 무엇을 보고 있는가"를 말해주지만, 여기는 화면 전체가 하나라서
         * 그 자리가 비어 있다.
         */
        private static void BuildHeader(RectTransform panel, SkillSystem system, TMP_FontAsset font)
        {
            var existing = panel.Find("Header");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, HeaderHeight);
            rect.anchoredPosition = new Vector2(0f, -TopPadding);

            var title = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Left);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            // 아래 22px은 예고 줄(49b) 자리다
            titleRect.offsetMin = new Vector2(24f, 22f);
            titleRect.offsetMax = Vector2.zero;
            title.text = "발도 오의";
            title.color = DimColor;

            // 토글은 오른쪽 절반. 자기 판을 갖는다 - 제목은 글자뿐이고 이쪽은
            // 눌리는 것이라, 그 차이가 형태에서 먼저 읽혀야 한다
            var toggleObject = new GameObject("AutoCast", typeof(RectTransform));
            toggleObject.transform.SetParent(go.transform, false);

            var toggleRect = (RectTransform)toggleObject.transform;
            toggleRect.anchorMin = new Vector2(0.44f, 0f);
            toggleRect.anchorMax = new Vector2(1f, 1f);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;

            var toggleImage = toggleObject.AddComponent<Image>();
            UiSkin.ApplyPanel(toggleImage, UiSkin.Chrome);

            var toggleButton = toggleObject.AddComponent<Button>();
            UiSkin.ApplyButton(toggleButton, toggleImage);

            var toggleLabel = CreateLabel(toggleObject.transform, font, "Label", TextAlignmentOptions.Center);
            var toggleLabelRect = (RectTransform)toggleLabel.transform;
            toggleLabelRect.anchorMin = Vector2.zero;
            toggleLabelRect.anchorMax = Vector2.one;
            toggleLabelRect.offsetMin = new Vector2(0f, -10f);
            toggleLabelRect.offsetMax = new Vector2(0f, 10f);
            toggleLabel.text = "자동 시전  ON";

            // 잠긴 미리보기의 해금 조건 배너(41단계). 헤더를 덮으므로 자동
            // 시전 토글도 잠긴 동안 함께 막힌다 - 토글은 상태 변경이다.
            // 조건은 탭(LockedTab)과 같은 출처에서 끌어온다
            LockBannerBuilder.Build(go.transform, font,
                                    "Lv." + SkillCatalog.PanelUnlockLevel + " 도달 시 해금",
                                    SkillCatalog.PanelUnlockLevel, 0);

            // 다음 해금 예고 (49b). 제목 아래 한 줄 - 제목이 "발도 오의"라는
            // 사실을 말하고 이 줄이 "다음에 무엇이 오는가"를 말한다
            var next = CreateLabel(go.transform, font, "NextUnlock", TextAlignmentOptions.Left);
            UiFonts.Demote(next);
            var nextRect = (RectTransform)next.transform;
            nextRect.anchorMin = new Vector2(0f, 0f);
            nextRect.anchorMax = new Vector2(0.44f, 0f);
            nextRect.pivot = new Vector2(0f, 0f);
            nextRect.offsetMin = new Vector2(24f, -6f);
            nextRect.offsetMax = new Vector2(0f, 0f);
            nextRect.sizeDelta = new Vector2(nextRect.sizeDelta.x, 40f);
            next.color = DimColor;
            next.text = "다음  혈파동  12스테이지";

            var preview = go.AddComponent<Onikiri.UI.SkillNextUnlock>();
            var previewSo = new SerializedObject(preview);
            previewSo.FindProperty("system").objectReferenceValue = system;
            previewSo.FindProperty("label").objectReferenceValue = next;
            previewSo.ApplyModifiedPropertiesWithoutUndo();

            var toggle = toggleObject.AddComponent<Onikiri.UI.SkillAutoCastToggle>();
            var so = new SerializedObject(toggle);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("button").objectReferenceValue = toggleButton;
            so.FindProperty("label").objectReferenceValue = toggleLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRow(RectTransform content, SkillSystem system, TMP_FontAsset font, int index)
        {
            var spec = SkillCatalog.Skills[index];
            string rowName = "Skill" + index;

            var existing = content.Find(rowName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(rowName, typeof(RectTransform));
            go.transform.SetParent(content, false);

            // 목록 안이라 자리는 스크롤 원점 기준이다. 머리글·슬롯 줄은 스크롤
            // 밖에 있으므로 여기 계산에 안 들어간다
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -(index * (RowHeight + RowGap)));

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var icon = CreateIcon(go.transform, UiIcons.For(spec.Id));

            // 행 글자는 전부 캡션 크기(39단계 - 캐릭터 화면과 같은 위계). 이
            // 화면에서 44pt로 남는 것은 머리글("발도 오의")과 자동 시전 버튼뿐이다
            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            UiFonts.Demote(nameLabel);
            PlaceStretched((RectTransform)nameLabel.transform, TextLeft, CostWidth + 24f, 10f, LineHeight);
            nameLabel.text = spec.DisplayName;

            var costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Right);
            UiFonts.Demote(costLabel);
            PlaceRight((RectTransform)costLabel.transform, 24f, 10f, LineHeight);
            costLabel.color = DimColor;
            costLabel.text = spec.StageGated ? spec.UnlockStage + "스테이지" : "Lv." + spec.UnlockLevel;

            // 장착 버튼. 아랫줄 오른쪽 - 비용(윗줄 오른쪽) 아래이고, 둘 다
            // 오른쪽 끝에 서므로 "이 줄에서 누를 수 있는 것"이 한 열에 모인다
            var equipButton = BuildEquipButton(go.transform, font);

            var valueLabel = CreateLabel(go.transform, font, "Value", TextAlignmentOptions.Left);
            UiFonts.Demote(valueLabel);
            PlaceStretched((RectTransform)valueLabel.transform, TextLeft,
                           24f + EquipWidth + 16f, 10f + LineHeight, LineHeight);
            valueLabel.color = DimColor;
            valueLabel.text = spec.CooldownSeconds.ToString("F1") + "초";

            var skillButton = go.AddComponent<Onikiri.UI.SkillButton>();
            var so = new SerializedObject(skillButton);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("slotIndex").intValue = index;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            so.FindProperty("costLabel").objectReferenceValue = costLabel;
            so.FindProperty("rowBackground").objectReferenceValue = image;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("equipButton").objectReferenceValue = equipButton;
            so.FindProperty("equipLabel").objectReferenceValue =
                equipButton.GetComponentInChildren<TMP_Text>();
            so.FindProperty("equippedRowTint").colorValue = EquippedRowTint;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("normalRowTint").colorValue = UiSkin.Row;
            so.FindProperty("iconTint").colorValue = UiIcons.Tint;

            // 줄을 누르면 여는 정보 팝업 (#14). 팝업은 패널보다 먼저 세워지므로
            // 여기서 이미 찾을 수 있다 - 비어 있으면 SkillButton이 예전처럼
            // 곧바로 강화한다
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            var popupObject = safeArea != null ? safeArea.Find(SkillPopupName) : null;
            so.FindProperty("popup").objectReferenceValue = popupObject != null
                ? popupObject.GetComponent<Onikiri.UI.SkillInfoPopup>() : null;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 장착 버튼 하나. 값은 SkillButton이 매 갱신마다 다시 쓴다 */
        private static Button BuildEquipButton(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Equip", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(EquipWidth, LineHeight);
            rect.anchoredPosition = new Vector2(-24f, -(10f + LineHeight));

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Chrome);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(label);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.text = "장착";

            return button;
        }

        /** 장착 버튼의 폭. 두 글자 + 여백 */
        private const float EquipWidth = 168f;

        /**
         * @brief 장착된 줄의 바탕. **새 색이 아니라 행 색을 밝힌 것이다.**
         *
         * UiSkin의 강조색 셋(GemAction·Gold·Danger) 규칙을 지키려면 여기에
         * 넷째 색을 만들면 안 된다. 같은 색의 밝기만 올리면 "같은 줄인데
         * 켜져 있다"로 읽히고, 그것이 정확히 말하려는 바다.
         *
         * **RGB만 곱한다.** Color에 스칼라를 곱하면 알파도 함께 눌리는 것이
         * 41단계에서 물린 함정이다
         */
        private static readonly Color EquippedRowTint =
            new Color(UiSkin.Row.r * 1.34f, UiSkin.Row.g * 1.30f, UiSkin.Row.b * 1.02f, 1f);

        // ---------------------------------------------------------------- 조각

        private static Image CreateIcon(Transform parent, Sprite sprite)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(UiIcons.Size, UiIcons.Size);
            rect.anchoredPosition = new Vector2(IconLeft, 0f);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = UiIcons.Tint;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            // 스프라이트가 없으면 자홍색 사각형이 남는다. 강화 행과 같은 규칙 -
            // 빠진 아이콘이 눈에도 드러나야 한다
            if (sprite == null) image.color = new Color(1f, 0f, 1f, 0.35f);

            return image;
        }

        private static void PlaceStretched(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void PlaceRight(RectTransform rect, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(CostWidth, height);
            rect.anchoredPosition = new Vector2(-right, -top);
        }

        private static TMP_Text CreateLabel(Transform parent, TMP_FontAsset font, string name,
                                            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriSmall;
            label.alignment = alignment;
            label.color = TextColor;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = name;
            return label;
        }
    }
}
