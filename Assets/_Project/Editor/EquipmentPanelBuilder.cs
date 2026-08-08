using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 장비 화면(대장간)과 그것을 구동하는 EquipmentSystem을 세운다.
     *
     * ## 자리는 성장·스킬·퀘스트와 같은 띠다
     *
     * 화면 10~45%. 넷이 동시에 보일 일이 없기 때문이고, 같은 자리를 쓰면
     * "아래쪽 절반은 목록"이라는 화면의 문법이 유지된다.
     *
     * ## 진입점은 하단 탭이다 - 대장간을 누르게 하지 않았다
     *
     * 사양이 "대장간을 상호작용 진입점으로"라고 했고, 기존 내비와 일관되게
     * 판단해서 보고하라고 열어 뒀다. **하단 탭을 골랐다.** 이유가 셋이다:
     *
     *   1. **대장간은 지역 1에만 있다.** 배경 세트의 랜드마크라
     *      (RegionBackgroundBuilder), 지역 2(가을숲)로 넘어가면 화면에서
     *      사라진다. 진입점이 사라지는 화면은 진입점이 아니다.
     *   2. **패럴랙스로 흘러간다.** 0.60 계수로 계속 움직이므로 누를 수 있는
     *      순간과 없는 순간이 있고, 그 차이를 플레이어가 알 방법이 없다.
     *   3. **하단 탭이 이 게임의 "어디로 갈지"다.** 상단 바는 지금 얼마인지를
     *      말하고 하단 탭은 화면을 연다(QuestPanelBuilder 주석). 같은 것을
     *      두 곳에서 열면 어느 쪽이 진짜인지 알 수 없다.
     *
     * 대장간이 하는 일은 **이름과 톤**이다 - 화면 제목이 "대장간"이고, 판의
     * 색이 그 건물의 먹빛·적을 따라간다. 랜드마크가 화면의 출처라는 것은
     * 그렇게 남는다.
     */
    public static class EquipmentPanelBuilder
    {
        public const string PanelName = "EquipmentPanel";

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        private const float SidePadding = 48f;
        private const float TopPadding = 12f;

        private const float HeaderHeight = 60f;
        private const float HeaderGap = 10f;

        /**
         * @brief 슬롯 카드 하나의 높이.
         *
         * 네 줄이 들어간다 - 등급 이름 / 슬롯·장착 / 단련 Lv / 버튼 둘.
         * 44pt 글자에 62px 줄이 세 개(186)와 버튼 120, 위아래 여백 24로 272다.
         *
         * 빌더가 검산한다(VerifyPanelFits). 17~18단계에서 글자 폭 어림이 세 번
         * 틀린 뒤로 이 프로젝트는 화면 크기 주장을 빌드가 확인한다.
         */
        private const float CardHeight = 272f;
        private const float CardGap = 14f;

        private const float LineHeight = 62f;
        private const float ButtonHeight = 120f;

        private const float IconLeft = 24f;
        private const float TextLeft = IconLeft + UiIcons.Size + 20f;

        private static readonly Color TextColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        private static readonly Color DimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        /**
         * @brief 등급업 버튼의 틴트. **보석 쪽 색이다.**
         *
         * 골드 버튼(단련)은 화면의 나머지와 같은 자주색 판을 쓰고 이쪽만 청으로
         * 민다. 두 재화가 섞이지 않게 하는 세 장치 중 하나이고(EquipmentRow
         * 주석), 색을 고른 근거는 상단 바의 보석 아이콘이 파란 다이아라는 것이다 -
         * 화면에 이미 있는 연결을 쓰는 것이 새 규칙을 하나 만드는 것보다 싸다.
         */
        private static readonly Color GemButtonTint = new Color(0.42f, 0.56f, 1.00f, 1f);

        [MenuItem("Onikiri/Build Equipment Panel")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static EquipmentSystem Build()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogError("[Onikiri] Battle root missing - run Build Combat Content first.");
                return null;
            }

            EquipmentIconSlicer.Slice();

            var system = EnsureSystem(battle);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - run Build Main Scene first.");
                return null;
            }

            var panel = EnsurePanel(safeArea);
            BuildHeader(panel, font);

            for (int i = 0; i < EquipmentCatalog.Count; i++)
                BuildCard(panel, system, font, i);

            VerifyPanelFits();

            // 판은 꺼진 채로 저장된다. 하단 탭이 켠다 - 스킬·퀘스트와 같은 규칙
            panel.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Equipment panel built: {0} slots, {1:F0}px (band {2:F0}px). "
                + "무기 상한 x{3:F2} / 방어구 상한 x{4:F2}, 해금 st{5}.",
                EquipmentCatalog.Count, PanelContentHeight, BandHeight,
                EquipmentCatalog.Slots[0].Ceiling, EquipmentCatalog.Slots[1].Ceiling,
                EquipmentCurve.UnlockStage));

            return system;
        }

        // ---------------------------------------------------------------- 시스템

        /**
         * @brief EquipmentSystem을 세우고 슬롯 값을 **카탈로그에서 옮겨 적는다.**
         *
         * 컴포넌트가 이미 씬에 있으면 스크립트 기본값을 고쳐도 반영되지 않으므로,
         * 빌더가 단일 출처로서 씬에 명시적으로 기록한다. SkillPanelBuilder가
         * 오의 슬롯에 하는 일과 같고, 테스트가 둘이 같은지 검사한다.
         *
         * **등급과 단련 레벨은 덮어쓰지 않는다.** 여기서 1로 되돌리면 빌더를
         * 한 번 돌릴 때마다 플레이 중인 세이브의 장비가 사라진다 - 계수만
         * 갱신하고 진행은 남긴다.
         */
        private static EquipmentSystem EnsureSystem(GameObject battle)
        {
            var system = battle.GetComponent<EquipmentSystem>();
            if (system == null) system = battle.AddComponent<EquipmentSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("upgrades").objectReferenceValue =
                Object.FindFirstObjectByType<UpgradeSystem>(FindObjectsInactive.Include);
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("stage").objectReferenceValue = battle.GetComponent<StageProgress>();

            var slots = so.FindProperty("slots");
            slots.arraySize = EquipmentCatalog.Count;

            for (int i = 0; i < EquipmentCatalog.Count; i++)
            {
                var spec = EquipmentCatalog.Slots[i];
                var element = slots.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("slotName").stringValue = spec.SlotName;
                element.FindPropertyRelative("stat").enumValueIndex = (int)spec.Stat;
                element.FindPropertyRelative("temperBaseCost").doubleValue = spec.TemperBaseCost;
                element.FindPropertyRelative("temperStep").doubleValue = spec.TemperStep;
                element.FindPropertyRelative("gradeStep").doubleValue = spec.GradeStep;

                var names = element.FindPropertyRelative("gradeNames");
                names.arraySize = spec.GradeNames.Length;
                for (int g = 0; g < spec.GradeNames.Length; g++)
                    names.GetArrayElementAtIndex(g).stringValue = spec.GradeNames[g];

                // 등급/레벨은 건드리지 않는다. 위 주석 참고 - 새 슬롯이면
                // 직렬화 기본값 1이 그대로 들어간다
                var grade = element.FindPropertyRelative("grade");
                if (grade.intValue < 1) grade.intValue = 1;
                var level = element.FindPropertyRelative("level");
                if (level.intValue < 1) level.intValue = 1;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // 세션이 세이브를 넘겨줘야 한다. 배선이 빠지면 장비가 매번 1등급에서
            // 시작하고, 증상은 "등급업했는데 껐다 켜면 사라진다"로 나온다 -
            // 보석을 쓴 뒤라 골드보다 손해가 크다
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null)
            {
                var sessionSo = new SerializedObject(session);
                sessionSo.FindProperty("equipment").objectReferenceValue = system;
                sessionSo.ApplyModifiedPropertiesWithoutUndo();
            }

            return system;
        }

        // ---------------------------------------------------------------- 판

        private static float BandHeight
        {
            get
            {
                return DisplayConfig.DesignHeight
                       * (DisplayConfig.GrowthPanelTop - DisplayConfig.BottomTabBarTop);
            }
        }

        private static float PanelContentHeight
        {
            get
            {
                return TopPadding + HeaderHeight + HeaderGap
                       + EquipmentCatalog.Count * (CardHeight + CardGap);
            }
        }

        /**
         * @brief 목록이 띠 안에 들어가는지 빌드가 검산한다.
         *
         * 스크롤을 두지 않았다. 둘뿐이라 필요가 없고, 스크롤이 있으면 "더
         * 있나?" 하고 끌어보게 된다 - 없는 것을 찾게 만드는 UI다. 대신 셋째
         * 슬롯(액세서리)이 생기는 순간 여기서 걸린다.
         */
        private static void VerifyPanelFits()
        {
            if (PanelContentHeight <= BandHeight) return;

            Debug.LogWarning(string.Format(
                "[Onikiri] Equipment panel needs {0:F0}px but the band is {1:F0}px - the last card "
                + "is cut off. Add a ScrollRect or shrink the card. {2} slots x {3:F0}px.",
                PanelContentHeight, BandHeight, EquipmentCatalog.Count, CardHeight + CardGap));
        }

        private static RectTransform EnsurePanel(Transform safeArea)
        {
            var existing = safeArea.Find(PanelName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(PanelName, typeof(RectTransform));
            go.transform.SetParent(safeArea, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, DisplayConfig.BottomTabBarTop);
            rect.anchorMax = new Vector2(1f, DisplayConfig.GrowthPanelTop);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = go.AddComponent<Image>();
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTextureBuilder.WashiPath);
            backdrop.type = Image.Type.Tiled;
            backdrop.color = UiSkin.PanelInk;

            // 뒤의 성장 패널이 드래그를 받지 않게 막는다
            backdrop.raycastTarget = true;

            return rect;
        }

        /**
         * @brief 제목 + 보석 잔액.
         *
         * 보석을 여기 한 번 더 적는다. 상단 바에 이미 있지만(31단계), 등급업
         * 버튼이 "보석 40"을 요구하는 화면에서 **잔액이 같은 화면 안에 없으면**
         * 눈이 위아래를 오간다. 상단 바는 전투를 보는 눈높이이고 이 판은
         * 목록을 보는 눈높이다.
         */
        private static void BuildHeader(RectTransform panel, TMP_FontAsset font)
        {
            var go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, HeaderHeight);
            rect.anchoredPosition = new Vector2(0f, -TopPadding);

            var title = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Left);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = Vector2.zero;
            title.text = "대장간";
            title.color = DimColor;

            var balance = CreateLabel(go.transform, font, "GemBalance", TextAlignmentOptions.Right);
            var balanceRect = (RectTransform)balance.transform;
            balanceRect.anchorMin = new Vector2(0.4f, 0f);
            balanceRect.anchorMax = new Vector2(1f, 1f);
            balanceRect.offsetMin = Vector2.zero;
            balanceRect.offsetMax = new Vector2(-24f, 0f);
            balance.text = "0";
            balance.color = DimColor;

            var gemIcon = new GameObject("GemIcon", typeof(RectTransform));
            gemIcon.transform.SetParent(go.transform, false);
            var gemRect = (RectTransform)gemIcon.transform;
            gemRect.anchorMin = new Vector2(1f, 0.5f);
            gemRect.anchorMax = new Vector2(1f, 0.5f);
            gemRect.pivot = new Vector2(1f, 0.5f);
            gemRect.sizeDelta = new Vector2(48f, 48f);
            gemRect.anchoredPosition = new Vector2(-24f, 0f);
            var gemImage = gemIcon.AddComponent<Image>();
            gemImage.sprite = UiIcons.LoadItem(UiIcons.GemSprite);
            gemImage.color = UiIcons.Tint;
            gemImage.raycastTarget = false;

            // 아이콘이 오른쪽 끝을 먹으므로 글자는 그 앞에서 끝난다
            balanceRect.offsetMax = new Vector2(-24f - 48f - 10f, 0f);

            // 상단 바와 **같은 컴포넌트**를 쓴다. 숫자를 적는 규칙(축약하지
            // 않는다)이 한 곳에만 있어야 두 자리가 갈리지 않는다
            var hud = go.AddComponent<Onikiri.UI.HUDGems>();
            var so = new SerializedObject(hud);
            so.FindProperty("label").objectReferenceValue = balance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 카드

        private static void BuildCard(RectTransform panel, EquipmentSystem system,
                                      TMP_FontAsset font, int index)
        {
            var spec = EquipmentCatalog.Slots[index];

            var go = new GameObject("Slot" + index, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, CardHeight);
            rect.anchoredPosition = new Vector2(0f,
                -(TopPadding + HeaderHeight + HeaderGap + index * (CardHeight + CardGap)));

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            var icon = CreateIcon(go.transform, UiIcons.LoadItem(spec.IconSprite));

            // 윗줄: 등급 이름 (왼쪽) + 지금 배수 (오른쪽)
            var gradeLabel = CreateLabel(go.transform, font, "Grade", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)gradeLabel.transform, TextLeft, 340f, 12f, LineHeight);
            gradeLabel.text = spec.GradeName(1);

            var statLabel = CreateLabel(go.transform, font, "Stat", TextAlignmentOptions.Right);
            PlaceStretched((RectTransform)statLabel.transform, TextLeft, 24f, 12f, LineHeight);
            statLabel.color = DimColor;
            statLabel.text = "×1.00";

            // 둘째 줄: 슬롯 이름 · 장착 중 (왼쪽) + 단련 Lv (오른쪽)
            //
            // 처음에 단련 Lv을 **셋째 줄**로 뒀다가 물렸다. 카드 272px에서
            // 세 줄(12 + 62x3 = 198)과 버튼(아래 16 + 120 = 136에서 시작)이
            // 정확히 겹쳐 글자가 버튼 뒤로 사라졌다 - 퀘스트 행이 보상 문구로
            // 겪은 것과 같은 종류이고, 그때처럼 **짧은 것을 오른쪽으로** 올려
            // 줄을 하나 없앴다
            var slotLabel = CreateLabel(go.transform, font, "Slot", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)slotLabel.transform, TextLeft, 300f,
                           12f + LineHeight, LineHeight);
            slotLabel.color = DimColor;
            slotLabel.text = spec.SlotName + " · 장착 중";

            var levelLabel = CreateLabel(go.transform, font, "Level", TextAlignmentOptions.Right);
            PlaceStretched((RectTransform)levelLabel.transform, TextLeft, 24f,
                           12f + LineHeight, LineHeight);
            levelLabel.color = DimColor;
            levelLabel.text = "단련 Lv.1 / " + EquipmentCurve.LevelsPerGrade;

            // 버튼 두 개. 왼쪽 골드(단련), 오른쪽 보석(등급업) - 자리가 고정이다
            TMP_Text temperTitle, temperCost, gradeTitle, gradeCost;
            Image temperImage, gradeImage;

            var temperButton = BuildActionButton(go.transform, font, "Temper", 0, UiSkin.Chrome,
                                                 "단련", "골드 0", out temperTitle, out temperCost,
                                                 out temperImage);
            var gradeButton = BuildActionButton(go.transform, font, "GradeUp", 1, GemButtonTint,
                                                "등급업", "보석 0 · 골드 0", out gradeTitle, out gradeCost,
                                                out gradeImage);

            var row = go.AddComponent<Onikiri.UI.EquipmentRow>();
            var so = new SerializedObject(row);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("slotIndex").intValue = index;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("rowBackground").objectReferenceValue = background;
            so.FindProperty("gradeLabel").objectReferenceValue = gradeLabel;
            so.FindProperty("slotLabel").objectReferenceValue = slotLabel;
            so.FindProperty("statLabel").objectReferenceValue = statLabel;
            so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            so.FindProperty("temperButton").objectReferenceValue = temperButton;
            so.FindProperty("temperBackground").objectReferenceValue = temperImage;
            so.FindProperty("temperTitle").objectReferenceValue = temperTitle;
            so.FindProperty("temperCost").objectReferenceValue = temperCost;
            so.FindProperty("gradeButton").objectReferenceValue = gradeButton;
            so.FindProperty("gradeBackground").objectReferenceValue = gradeImage;
            so.FindProperty("gradeTitle").objectReferenceValue = gradeTitle;
            so.FindProperty("gradeCost").objectReferenceValue = gradeCost;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("normalRowTint").colorValue = UiSkin.Row;
            so.FindProperty("iconTint").colorValue = UiIcons.Tint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 두 줄짜리 동작 버튼. side 0은 왼쪽 절반, 1은 오른쪽 절반 */
        private static Button BuildActionButton(Transform parent, TMP_FontAsset font, string name,
                                                int side, Color tint, string title, string cost,
                                                out TMP_Text titleLabel, out TMP_Text costLabel,
                                                out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(side * 0.5f, 0f);
            rect.anchorMax = new Vector2((side + 1) * 0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(side == 0 ? 24f : 8f, 16f);
            rect.offsetMax = new Vector2(side == 0 ? -8f : -24f, 16f + ButtonHeight);

            image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, tint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Center);
            PlaceStretched((RectTransform)titleLabel.transform, 8f, 8f, 8f, 52f);
            titleLabel.text = title;

            costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Center);
            PlaceStretched((RectTransform)costLabel.transform, 8f, 8f, 8f + 52f, 52f);
            costLabel.color = DimColor;
            costLabel.text = cost;

            return button;
        }

        // ---------------------------------------------------------------- 조각

        private static Image CreateIcon(Transform parent, Sprite sprite)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(UiIcons.Size, UiIcons.Size);
            rect.anchoredPosition = new Vector2(IconLeft, -16f);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = UiIcons.Tint;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            // 스프라이트가 없으면 자홍색 사각형이 남는다. 강화·스킬 행과 같은
            // 규칙 - 빠진 아이콘이 눈에도 드러나야 한다
            if (sprite == null) image.color = new Color(1f, 0f, 1f, 0.35f);

            return image;
        }

        private static void PlaceStretched(RectTransform rect, float left, float right,
                                           float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
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
