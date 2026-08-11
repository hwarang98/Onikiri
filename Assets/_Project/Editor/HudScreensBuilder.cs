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
     * @brief 상단 바에서 열리는 화면 셋 (37단계): 스탯 / 스테이지 재선택 / 설정.
     *
     * 하단 탭 화면들(스킬·퀘스트·장비·동료)과 같은 자리 규칙이다 - SafeArea
     * 직속, 성장 패널 띠를 덮고, 꺼진 채 저장되고, 여는 버튼이 닫는다
     * (HudScreenButton). 다른 점은 진입점이 하단 탭이 아니라 상단 바라는 것뿐.
     *
     * 한 파일에 셋이 사는 이유: 전부 "라벨 몇 줄 + 버튼 한둘"짜리 읽기 전용
     * 화면이라 골격이 같고, 상단 바 버튼과의 배선(WireTopBarButtons)이 셋을
     * 한 번에 알아야 하기 때문이다.
     */
    public static class HudScreensBuilder
    {
        public const string StatsPanelName = "StatsPanel";
        public const string RegionSelectPanelName = "RegionSelectPanel";
        public const string SettingsPanelName = "SettingsPanel";

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        private const float SidePadding = 48f;
        private const float TopPadding = 16f;
        private const float HeaderHeight = 64f;
        private const float RowHeight = 60f;
        private const float RowGap = 6f;

        // 39단계 톤 통일: 팔레트의 단일 출처는 UiSkin이다
        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        [MenuItem("Onikiri/Build Hud Screens")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Build()
        {
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - run Build Main Scene first.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            BuildStatsPanel(safeArea, font);
            BuildRegionSelectPanel(safeArea, font);
            BuildSettingsPanel(safeArea, font);
            WireTopBarButtons(safeArea);
            WireSoundApplier();

            Debug.Log("[Onikiri] Hud screens built: stats / region select / settings.");
        }

        // ---------------------------------------------------------------- 스탯

        /** 라벨 왼쪽 이름 + 오른쪽 값 여덟 줄. 읽기 전용이라 스크롤이 없다 */
        private static void BuildStatsPanel(Transform safeArea, TMP_FontAsset font)
        {
            var panel = EnsurePanel(safeArea, StatsPanelName);
            BuildTitle(panel, font, "스탯");

            // 경험치 수치. 얇은 스트립(2a 후속)에는 숫자가 없어서 정확한 값의
            // 자리는 이 창이다(레벨 칩이 입구니까 동선도 그대로다). 행으로
            // 세우면 아홉 줄 + 내역이 패널 높이(672px)를 넘는다 - 머리글 오른쪽
            // 빈 자리에 캡션으로 얹는다. 제목 "스탯"은 왼쪽 두 글자뿐이라
            // 오른쪽 절반은 늘 비어 있다
            var exp = CreateLabel(panel, font, "ExpValue", TextAlignmentOptions.Right);
            UiFonts.Demote(exp);
            var expRect = (RectTransform)exp.transform;
            expRect.anchorMin = new Vector2(0f, 1f);
            expRect.anchorMax = new Vector2(1f, 1f);
            expRect.pivot = new Vector2(0.5f, 1f);
            expRect.sizeDelta = new Vector2(-SidePadding * 2f, HeaderHeight);
            expRect.anchoredPosition = new Vector2(0f, -TopPadding);
            exp.color = DimColor;
            exp.text = "경험치  0 / 30";

            string[] names =
            {
                "총 공격력", "공격 속도", "치명타", "최대 체력",
                "초당 회복", "골드 획득", "동료 지원", "추정 DPS"
            };

            var values = new TMP_Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var row = EnsureRow(panel, "Stat" + i, i);

                var name = CreateLabel(row, font, "Name", TextAlignmentOptions.Left);
                UiFonts.Demote(name);
                StretchInside(name, 24f, 0.55f);
                name.text = names[i];
                name.color = DimColor;

                var value = CreateLabel(row, font, "Value", TextAlignmentOptions.Right);
                StretchInside(value, 24f, 1f);
                value.text = "-";
                values[i] = value;
            }

            // 배수 내역 한 줄. 최종 값의 출처를 묻는 화면이 없어서 생긴 창이므로
            // 출처도 한 줄은 보여준다
            var detailRow = EnsureRow(panel, "Detail", names.Length);
            detailRow.GetComponent<Image>().enabled = false;
            var detail = CreateLabel(detailRow, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(detail);
            StretchInside(detail, 0f, 1f);
            detail.color = DimColor;
            detail.text = "공격 배수  증폭 x1.00 · 장비 x1.00 · 전직 x1.00";

            var stats = panel.gameObject.GetComponent<Onikiri.UI.StatsPanel>();
            if (stats == null) stats = panel.gameObject.AddComponent<Onikiri.UI.StatsPanel>();

            var so = new SerializedObject(stats);
            so.FindProperty("combat").objectReferenceValue =
                Object.FindFirstObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            so.FindProperty("health").objectReferenceValue =
                Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            so.FindProperty("expValue").objectReferenceValue = exp;
            so.FindProperty("damageValue").objectReferenceValue = values[0];
            so.FindProperty("attackSpeedValue").objectReferenceValue = values[1];
            so.FindProperty("critValue").objectReferenceValue = values[2];
            so.FindProperty("healthValue").objectReferenceValue = values[3];
            so.FindProperty("regenValue").objectReferenceValue = values[4];
            so.FindProperty("goldGainValue").objectReferenceValue = values[5];
            so.FindProperty("petValue").objectReferenceValue = values[6];
            so.FindProperty("dpsValue").objectReferenceValue = values[7];
            so.FindProperty("multiplierDetail").objectReferenceValue = detail;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- 재선택

        private static void BuildRegionSelectPanel(Transform safeArea, TMP_FontAsset font)
        {
            var panel = EnsurePanel(safeArea, RegionSelectPanelName);
            BuildTitle(panel, font, "스테이지 선택");

            var roster = AssetDatabase.LoadAssetAtPath<BossRoster>(BossConfigBuilder.RosterPath);
            if (roster == null || roster.regions == null || roster.regions.Length == 0)
            {
                Debug.LogError("[Onikiri] BossRoster missing - cannot build the region list.");
                return;
            }

            // 현재 위치 한 줄
            var currentRow = EnsureRow(panel, "Current", 0);
            currentRow.GetComponent<Image>().enabled = false;
            var current = CreateLabel(currentRow, font, "Label", TextAlignmentOptions.Left);
            UiFonts.Demote(current);
            StretchInside(current, 24f, 1f);
            current.color = DimColor;
            current.text = "현재 1 스테이지";

            // 지역 줄들 + 무한 구간 한 줄(42단계)
            var rowData = new Onikiri.UI.RegionSelectPanel.Row[roster.regions.Length + 1];
            int firstStage = 1;
            for (int i = 0; i < roster.regions.Length; i++)
            {
                var region = roster.regions[i];
                int stageCount = region != null ? region.stageCount : 10;
                int lastStage = firstStage + stageCount - 1;

                var row = EnsureRow(panel, "Region" + (i + 1), i + 1);
                var background = row.GetComponent<Image>();

                var button = row.gameObject.GetComponent<Button>();
                if (button == null) button = row.gameObject.AddComponent<Button>();
                UiSkin.ApplyButton(button, background);

                var name = CreateLabel(row, font, "Name", TextAlignmentOptions.Left);
                UiFonts.Demote(name);
                StretchInside(name, 24f, 0.6f);
                name.text = "지역 " + (i + 1) + "  (" + firstStage + "~" + lastStage + ")";

                var state = CreateLabel(row, font, "State", TextAlignmentOptions.Right);
                UiFonts.Demote(state);
                StretchInside(state, 24f, 1f);
                state.color = DimColor;
                state.text = "잠김";

                rowData[i] = new Onikiri.UI.RegionSelectPanel.Row
                {
                    root = row.gameObject,
                    nameLabel = name,
                    stateLabel = state,
                    button = button,
                    background = background,
                    firstStage = firstStage,
                    lastStage = lastStage
                };

                firstStage += stageCount;
            }

            // 무한 구간(42단계). st41부터는 세계가 순환하며 끝없이 이어진다 -
            // 정적 지역 줄로는 "지금 어디인가"가 표시될 자리가 없었다(최전선
            // 41+면 네 줄 전부 "클리어"만 남는다). 열린 구간 한 줄이 그 자리다.
            //
            // lastStage = int.MaxValue가 요점이다. RegionSelectPanel.Refresh의
            // 기존 규칙(cleared = frontier > last)이 이 줄에서는 영원히 거짓이라
            // "클리어"가 되지 않고, 현재 위치/진행 중/잠김 셋만 오간다 -
            // 런타임 코드는 한 줄도 안 바뀐다
            {
                int deepFirst = firstStage;   // 마지막 지역 다음 칸 = 41
                var row = EnsureRow(panel, "RegionDeep", roster.regions.Length + 1);
                var background = row.GetComponent<Image>();

                var button = row.gameObject.GetComponent<Button>();
                if (button == null) button = row.gameObject.AddComponent<Button>();
                UiSkin.ApplyButton(button, background);

                var name = CreateLabel(row, font, "Name", TextAlignmentOptions.Left);
                UiFonts.Demote(name);
                StretchInside(name, 24f, 0.6f);
                name.text = "무한 구간  (" + deepFirst + "~)";

                var state = CreateLabel(row, font, "State", TextAlignmentOptions.Right);
                UiFonts.Demote(state);
                StretchInside(state, 24f, 1f);
                state.color = DimColor;
                state.text = "잠김";

                rowData[roster.regions.Length] = new Onikiri.UI.RegionSelectPanel.Row
                {
                    root = row.gameObject,
                    nameLabel = name,
                    stateLabel = state,
                    button = button,
                    background = background,
                    firstStage = deepFirst,
                    lastStage = int.MaxValue
                };
            }

            // 최전선 복귀. 목록보다 눈에 띄어야 한다 - 재선택의 기본값은
            // 언제나 "돌아오는 것"이다
            var frontierRow = EnsureRow(panel, "Frontier", roster.regions.Length + 2);
            var frontierImage = frontierRow.GetComponent<Image>();
            UiSkin.ApplyPanel(frontierImage, UiSkin.Panel, UiSkin.Good);
            var frontierButton = frontierRow.gameObject.GetComponent<Button>();
            if (frontierButton == null) frontierButton = frontierRow.gameObject.AddComponent<Button>();
            UiSkin.ApplyButton(frontierButton, frontierImage);

            var frontierLabel = CreateLabel(frontierRow, font, "Label", TextAlignmentOptions.Center);
            StretchInside(frontierLabel, 0f, 1f);
            frontierLabel.text = "최전선으로";

            // 안내 한 줄. 되돌아간 스테이지의 규칙(보스 잠김, 경험치 없음)은
            // 화면 어딘가에 적혀 있어야 한다 - 숨은 규칙은 버그로 읽힌다
            var noteRow = EnsureRow(panel, "Note", roster.regions.Length + 3);
            noteRow.GetComponent<Image>().enabled = false;
            var note = CreateLabel(noteRow, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(note);
            StretchInside(note, 0f, 1f);
            note.color = DimColor;
            // 984px 상자에 44pt 한 줄로 들어가는 길이여야 한다. 원래 "보스와
            // 경험치는"이었는데 26자라 잘렸다 - 보스 잠김은 상단 바의 "클리어"와
            // 도전 버튼 부재가 이미 말하므로 경험치만 남긴다
            note.text = "클리어한 지역은 골드만 - 경험치는 최전선에";

            var select = panel.gameObject.GetComponent<Onikiri.UI.RegionSelectPanel>();
            if (select == null) select = panel.gameObject.AddComponent<Onikiri.UI.RegionSelectPanel>();

            var battle = GameObject.Find("Battle");
            var so = new SerializedObject(select);
            so.FindProperty("progress").objectReferenceValue =
                battle != null ? battle.GetComponent<StageProgress>() : null;
            so.FindProperty("fight").objectReferenceValue =
                battle != null ? battle.GetComponent<BossFight>() : null;
            so.FindProperty("spawner").objectReferenceValue =
                Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            so.FindProperty("currentLabel").objectReferenceValue = current;
            so.FindProperty("frontierButton").objectReferenceValue = frontierButton;
            so.FindProperty("frontierLabel").objectReferenceValue = frontierLabel;

            var rowsProperty = so.FindProperty("rows");
            rowsProperty.arraySize = rowData.Length;
            for (int i = 0; i < rowData.Length; i++)
            {
                var element = rowsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = rowData[i].root;
                element.FindPropertyRelative("nameLabel").objectReferenceValue = rowData[i].nameLabel;
                element.FindPropertyRelative("stateLabel").objectReferenceValue = rowData[i].stateLabel;
                element.FindPropertyRelative("button").objectReferenceValue = rowData[i].button;
                element.FindPropertyRelative("background").objectReferenceValue = rowData[i].background;
                element.FindPropertyRelative("firstStage").intValue = rowData[i].firstStage;
                element.FindPropertyRelative("lastStage").intValue = rowData[i].lastStage;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- 설정

        private static void BuildSettingsPanel(Transform safeArea, TMP_FontAsset font)
        {
            var panel = EnsurePanel(safeArea, SettingsPanelName);
            BuildTitle(panel, font, "설정");

            var muteRow = EnsureRow(panel, "Mute", 0);
            var muteImage = muteRow.GetComponent<Image>();
            var muteButton = muteRow.gameObject.GetComponent<Button>();
            if (muteButton == null) muteButton = muteRow.gameObject.AddComponent<Button>();
            UiSkin.ApplyButton(muteButton, muteImage);

            var muteLabel = CreateLabel(muteRow, font, "Label", TextAlignmentOptions.Center);
            StretchInside(muteLabel, 0f, 1f);
            muteLabel.text = "효과음  켜짐";

            var versionRow = EnsureRow(panel, "Version", 1);
            versionRow.GetComponent<Image>().enabled = false;
            var version = CreateLabel(versionRow, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(version);
            StretchInside(version, 0f, 1f);
            version.color = DimColor;
            version.text = "버전 0.0";

            var settings = panel.gameObject.GetComponent<Onikiri.UI.SettingsPanel>();
            if (settings == null) settings = panel.gameObject.AddComponent<Onikiri.UI.SettingsPanel>();

            var so = new SerializedObject(settings);
            so.FindProperty("muteButton").objectReferenceValue = muteButton;
            so.FindProperty("muteLabel").objectReferenceValue = muteLabel;
            so.FindProperty("versionLabel").objectReferenceValue = version;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- 배선

        /**
         * @brief 상단 바 버튼 셋에 화면을 물린다.
         *
         * 버튼 자체는 BattleContentBuilder가 세운다(상단 바의 주인이 그쪽이다).
         * 화면 참조만 여기서 잇는 이유는 화면이 이 빌더의 생성물이라, 만든 쪽이
         * 물리는 것이 참조 끊김을 빌드 순서 문제로 좁혀주기 때문이다.
         */
        private static void WireTopBarButtons(Transform safeArea)
        {
            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar == null) return;

            WireScreenButton(topBar, "StageButton", safeArea, RegionSelectPanelName, true);
            // 2b: 스탯 창 입구가 레벨 칩에서 캐릭터 초상으로 바뀌었다. 경로는
            // 그대로다 - "캐릭터에 관한 것"의 입구가 캐릭터 그림인 것이 더 곧다
            WireScreenButton(topBar, "PortraitButton", safeArea, StatsPanelName, false);
            WireScreenButton(topBar, "SettingsButton", safeArea, SettingsPanelName, false);
        }

        private static void WireScreenButton(Transform topBar, string buttonName,
                                             Transform safeArea, string screenName, bool needsReselect)
        {
            var buttonObject = topBar.Find(buttonName);
            var screen = safeArea.Find(screenName);
            if (buttonObject == null || screen == null)
            {
                Debug.LogError("[Onikiri] Cannot wire " + buttonName + " -> " + screenName);
                return;
            }

            var control = buttonObject.GetComponent<Onikiri.UI.HudScreenButton>();
            if (control == null)
                control = buttonObject.gameObject.AddComponent<Onikiri.UI.HudScreenButton>();

            var so = new SerializedObject(control);
            so.FindProperty("button").objectReferenceValue = buttonObject.GetComponent<Button>();
            so.FindProperty("screen").objectReferenceValue = screen.gameObject;
            so.FindProperty("needsReselect").boolValue = needsReselect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 저장된 음소거를 씬 시작에 적용하는 컴포넌트. 항상 켜져 있는 곳에 산다 */
        private static void WireSoundApplier()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null) return;
            if (battle.GetComponent<Onikiri.UI.SoundPrefsApplier>() == null)
                battle.AddComponent<Onikiri.UI.SoundPrefsApplier>();
        }

        // ---------------------------------------------------------------- 조각

        private static RectTransform EnsurePanel(Transform safeArea, string name)
        {
            var existing = safeArea.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(name, typeof(RectTransform));
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
            backdrop.raycastTarget = true;

            // 화지 위의 벚가지 (39단계). 하단 탭 패널들과 같은 자리 - 상단 바에서
            // 여는 화면(스탯·재선택·설정)도 같은 화지 언어를 쓴다
            BackdropTextureBuilder.AddSakuraBranch(rect);

            return rect;
        }

        private static void BuildTitle(RectTransform panel, TMP_FontAsset font, string title)
        {
            var label = CreateLabel(panel, font, "Title", TextAlignmentOptions.Left);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, HeaderHeight);
            rect.anchoredPosition = new Vector2(0f, -TopPadding);
            label.text = title;
            label.color = DimColor;
        }

        /** index번째 행. 머리글 아래에서 위에서 아래로 쌓인다 */
        private static RectTransform EnsureRow(RectTransform panel, string name, int index)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, RowHeight);
            rect.anchoredPosition = new Vector2(0f,
                -(TopPadding + HeaderHeight + 8f + index * (RowHeight + RowGap)));

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            return rect;
        }

        private static void StretchInside(TMP_Text label, float sidePadding, float anchorMaxX)
        {
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(anchorMaxX, 1f);
            // 세로를 넓힌다 - Ellipsis의 세로 잘림 방지 (ExpLabel과 같은 이유)
            rect.offsetMin = new Vector2(sidePadding, -14f);
            rect.offsetMax = new Vector2(-sidePadding, 14f);
            label.overflowMode = TextOverflowModes.Ellipsis;
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
            label.raycastTarget = false;
            return label;
        }
    }
}
