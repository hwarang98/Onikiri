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

            // 현재 위치 한 줄. 오른쪽 절반은 스테이지 단위 이동 버튼이다 (#8)
            var currentRow = EnsureRow(panel, "Current", 0);
            currentRow.GetComponent<Image>().enabled = false;
            var current = CreateLabel(currentRow, font, "Label", TextAlignmentOptions.Left);
            UiFonts.Demote(current);
            // 왼쪽 44%까지만. 나머지는 버튼 넷의 자리다 - 라벨이 폭 전체를
            // 차지하면 "현재 172 스테이지 (최전선)"이 버튼 밑으로 흘러 들어간다
            StretchInside(current, 24f, 0.44f);
            current.color = DimColor;
            current.text = "현재 1 스테이지";

            // 한 칸씩 옮기는 버튼 넷 (#8). 지역 줄은 큰 이동이고 이쪽이 미세
            // 조정이다 - 별도 행을 쓰지 않는 이유는 행 하나를 더 넣으면 이 패널이
            // 밴드 높이(672px)를 10px 넘기 때문이다. 현재 위치 줄의 오른쪽은
            // 원래 비어 있었다
            Button minusTen, minusOne, plusOne, plusTen;
            BuildStageSteppers(currentRow, font,
                               out minusTen, out minusOne, out plusOne, out plusTen);

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
            so.FindProperty("minusTenButton").objectReferenceValue = minusTen;
            so.FindProperty("minusOneButton").objectReferenceValue = minusOne;
            so.FindProperty("plusOneButton").objectReferenceValue = plusOne;
            so.FindProperty("plusTenButton").objectReferenceValue = plusTen;

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

        /**
         * @brief 스테이지 단위 이동 버튼 넷 (#8). 현재 위치 줄의 오른쪽에 선다.
         *
         * 순서는 화면의 방향과 같다: -10 -1 +1 +10. 왼쪽이 뒤로, 오른쪽이
         * 앞으로다 - 목록이 위에서 아래로 자라는 것과 같은 종류의 약속이라
         * 화살표 글리프 없이도 읽힌다.
         *
         * 폭은 넷이 같다. "+10"이 "-1"보다 한 글자 길지만 글자에 맞춰 재면
         * 넷의 크기가 제각각이 되고, 그러면 어느 것을 눌렀는지가 위치로 안
         * 잡힌다(배수 줄과 같은 규칙 - UpgradePanelBuilder.BuildBatchRow).
         */
        private static void BuildStageSteppers(RectTransform row, TMP_FontAsset font,
                                               out Button minusTen, out Button minusOne,
                                               out Button plusOne, out Button plusTen)
        {
            string[] names = { "-10", "-1", "+1", "+10" };
            var made = new Button[names.Length];

            // 오른쪽 절반(0.46~1.0)을 넷으로 나눈다. 라벨이 0.44까지 쓰므로
            // 그 사이 0.02가 둘을 가르는 여백이다
            const float Left = 0.46f;
            float slice = (1f - Left) / names.Length;

            for (int i = 0; i < names.Length; i++)
            {
                var go = new GameObject("Step" + names[i], typeof(RectTransform));
                go.transform.SetParent(row, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(Left + i * slice, 0f);
                rect.anchorMax = new Vector2(Left + (i + 1) * slice, 1f);
                rect.offsetMin = new Vector2(4f, 2f);
                rect.offsetMax = new Vector2(-4f, -2f);

                var image = go.AddComponent<Image>();
                UiSkin.ApplyPanel(image, UiSkin.Row);

                var button = go.AddComponent<Button>();
                UiSkin.ApplyButton(button, image);

                var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
                UiFonts.Demote(label);
                StretchInside(label, 0f, 1f);
                label.text = names[i];

                made[i] = button;
            }

            minusTen = made[0];
            minusOne = made[1];
            plusOne = made[2];
            plusTen = made[3];
        }

        // ---------------------------------------------------------------- 설정

        /**
         * @brief 설정 = 계정의 상시 거처 (인트로 스텝).
         *
         * 타이틀은 진입 순간의 선택만 맡고(연동 유저는 그 화면을 다시 안
         * 본다), 게임 도중의 계정 관리는 전부 여기다: 상태 한 줄 · [구글 연동]
         * (게스트의 뒤늦은 주 경로) · 닉네임 변경(54단계 랭킹 것을 상시
         * 자리로 모음 - 랭킹 첫 진입 입력은 그대로 남는다).
         *
         * 연동 버튼은 GoogleLinkButton 한 벌이다 - 타이틀 CTA와 같은 컴포넌트,
         * 같은 숨김 규칙(에디터·이미 연동이면 버튼째 사라진다).
         */
        /**
         * @brief 설정은 이제 **팝업**이다 (#1). 내용은 한 줄도 안 바뀐다.
         *
         * 높이는 내용에서 나온다: 제목 + 여섯 줄(음소거·계정·이름·입력·상태·
         * 버전) = 484px. 0.30(576px)이면 그 위아래로 숨 쉴 자리가 남는다.
         * 랭킹처럼 크게 띄우면 여섯 줄 아래가 텅 빈 창이 된다.
         */
        private static void BuildSettingsPanel(Transform safeArea, TMP_FontAsset font)
        {
            RectTransform root;
            var panel = PopupBuilder.Ensure(safeArea, SettingsPanelName, font, 0.30f, out root);
            BuildTitle(panel, font, "설정");

            var muteRow = EnsureRow(panel, "Mute", 0);
            var muteImage = muteRow.GetComponent<Image>();
            var muteButton = muteRow.gameObject.GetComponent<Button>();
            if (muteButton == null) muteButton = muteRow.gameObject.AddComponent<Button>();
            UiSkin.ApplyButton(muteButton, muteImage);

            var muteLabel = CreateLabel(muteRow, font, "Label", TextAlignmentOptions.Center);
            StretchInside(muteLabel, 0f, 1f);
            muteLabel.text = "효과음  켜짐";

            // -- 계정 상태 + 연동. 상태 라벨은 동적 폰트다 - 연동되면 남이
            //    지은 구글 표시 이름이 들어온다 (LeaderboardPanelBuilder와 같은 규칙)
            var accountRow = EnsureRow(panel, "Account", 1);
            var accountLabel = CreateLabel(accountRow, font, "State", TextAlignmentOptions.Left);
            StretchInside(accountLabel, 24f, 0.7f);
            UseNameFont(accountLabel);
            accountLabel.text = "계정 확인 중...";

            var linkButton = CreateRowButton(accountRow, font, "LinkButton", "구글 연동", 240f);
            var link = linkButton.gameObject.AddComponent<Onikiri.UI.GoogleLinkButton>();
            var linkSo = new SerializedObject(link);
            linkSo.FindProperty("button").objectReferenceValue = linkButton;
            linkSo.FindProperty("hideRoot").objectReferenceValue = linkButton.gameObject;
            linkSo.ApplyModifiedPropertiesWithoutUndo();

            // -- 이름 한 줄 + 변경
            var nameRow = EnsureRow(panel, "Name", 2);
            var nameLabel = CreateLabel(nameRow, font, "Name", TextAlignmentOptions.Left);
            StretchInside(nameLabel, 24f, 0.62f);
            UseNameFont(nameLabel);
            nameLabel.text = Onikiri.Progression.PlayerProfile.DefaultName;

            var editButton = CreateRowButton(nameRow, font, "EditButton", "이름 변경", 200f);

            // -- 이름 입력 줄. 기본은 꺼져 있다 (자리는 남긴다 - 아래 줄이 안 뛴다)
            var editRow = EnsureRow(panel, "NameEdit", 3);
            var input = BuildNameInput(editRow, font);
            var confirmButton = CreateRowButton(editRow, font, "Confirm", "확인", 200f);
            editRow.gameObject.SetActive(false);

            // -- 상태줄 (이름 확정·연동 진행의 피드백)
            var statusRow = EnsureRow(panel, "Status", 4);
            statusRow.GetComponent<Image>().enabled = false;
            var status = CreateLabel(statusRow, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(status);
            StretchInside(status, 0f, 1f);
            status.color = DimColor;
            status.gameObject.SetActive(false);

            var versionRow = EnsureRow(panel, "Version", 5);
            versionRow.GetComponent<Image>().enabled = false;
            var version = CreateLabel(versionRow, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(version);
            StretchInside(version, 0f, 1f);
            version.color = DimColor;
            version.text = "버전 0.0";

            // 컴포넌트는 **루트**에 붙는다. 여는 쪽(HudScreenButton)이 켜고 끄는
            // 것이 루트이므로, 창에 붙이면 OnEnable이 팝업이 열릴 때 안 돈다
            var settings = root.gameObject.GetComponent<Onikiri.UI.SettingsPanel>();
            if (settings == null) settings = root.gameObject.AddComponent<Onikiri.UI.SettingsPanel>();

            var so = new SerializedObject(settings);
            so.FindProperty("muteButton").objectReferenceValue = muteButton;
            so.FindProperty("muteLabel").objectReferenceValue = muteLabel;
            so.FindProperty("versionLabel").objectReferenceValue = version;
            so.FindProperty("accountLabel").objectReferenceValue = accountLabel;
            so.FindProperty("googleLink").objectReferenceValue = link;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("nameEditButton").objectReferenceValue = editButton;
            so.FindProperty("nameEditGroup").objectReferenceValue = editRow.gameObject;
            so.FindProperty("nameInput").objectReferenceValue = input;
            so.FindProperty("nameConfirmButton").objectReferenceValue = confirmButton;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
        }

        /**
         * @brief 이름 입력칸. LeaderboardPanelBuilder의 것과 같은 구조다.
         *
         * 입력칸 글자도 동적 폰트다 - 자기 이름을 치는 동안 글자가 네모로
         * 보이면 그 이름을 못 쓴다고 읽는다.
         */
        private static TMP_InputField BuildNameInput(RectTransform row, TMP_FontAsset font)
        {
            var fieldObject = new GameObject("Input", typeof(RectTransform));
            fieldObject.transform.SetParent(row, false);
            var fieldRect = (RectTransform)fieldObject.transform;
            fieldRect.anchorMin = new Vector2(0f, 0f);
            fieldRect.anchorMax = new Vector2(0.62f, 1f);
            fieldRect.offsetMin = new Vector2(16f, 8f);
            fieldRect.offsetMax = new Vector2(-8f, -8f);

            var fieldImage = fieldObject.AddComponent<Image>();
            // 입력칸은 눌리는 것이 아니라 파인 것이다 - 재화 트레이와 같은 언어
            fieldImage.color = UiSkin.BarTrack;

            var text = CreateLabel(fieldObject.transform, font, "Text", TextAlignmentOptions.Left);
            UseNameFont(text);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);
            text.text = string.Empty;

            var placeholder = CreateLabel(fieldObject.transform, font, "Placeholder",
                                          TextAlignmentOptions.Left);
            UiFonts.Demote(placeholder);
            var placeholderRect = (RectTransform)placeholder.transform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(12f, 0f);
            placeholderRect.offsetMax = new Vector2(-12f, 0f);
            placeholder.color = DimColor;
            placeholder.text = "이름 (" + Onikiri.Progression.PlayerProfile.MaxLength + "자까지)";

            var input = fieldObject.AddComponent<TMP_InputField>();
            input.textViewport = fieldRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = Onikiri.Progression.PlayerProfile.MaxLength;
            // 줄바꿈이 이름에 끼면 랭킹 한 줄이 두 줄이 된다
            input.lineType = TMP_InputField.LineType.SingleLine;

            return input;
        }

        /** 행 오른쪽에 붙는 버튼. LeaderboardPanelBuilder.CreateButton과 같은 규칙 */
        private static Button CreateRowButton(RectTransform row, TMP_FontAsset font,
                                              string name, string text, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(row, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, 44f);
            rect.anchoredPosition = new Vector2(-16f, 0f);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Chrome);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(label);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -8f);
            labelRect.offsetMax = new Vector2(0f, 8f);
            label.text = text;

            return button;
        }

        /**
         * @brief 남이 지은 글자가 오는 라벨만 동적 폰트로 (LeaderboardPanelBuilder와 같은 규칙).
         */
        private static void UseNameFont(TMP_Text label)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                PixelFontAssetBuilder.NameFontPath);
            if (label == null || font == null) return;

            label.font = font;
            label.fontSharedMaterial = font.material;
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriCaption;
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

            // 개선안 v2: 초상은 **캐릭터 화면(홈)**을 연다 - 덮고 있는 다른
            // 화면들을 닫아 바탕(GrowthPanel)을 드러낸다. 레벨업 알림 점이
            // 초상에 붙으므로(LevelUpNoticeBadge) 신호와 입구가 같은 자리다.
            // 스탯 창 입구는 성장 패널 헤더의 "Lv · EXP" 라벨이 물려받았다 -
            // "정확한 값이 필요한 사람은 레벨 칩을 누른다"(LevelHud)의 그
            // 칩이 패널 안으로 들어간 것이다
            WireHomeButton(topBar, "PortraitButton");

            var growthPanel = MainSceneBuilder.FindBand("GrowthPanel");
            if (growthPanel != null
                && growthPanel.Find(UpgradePanelBuilder.LevelHeaderName) != null)
                WireScreenButton(growthPanel, UpgradePanelBuilder.LevelHeaderName,
                                 safeArea, StatsPanelName, false);
            else
                Debug.LogWarning("[Onikiri] LevelHeader missing - run Build Combat Content "
                                 + "so the stats screen keeps an entrance.");

            WireScreenButton(topBar, "SettingsButton", safeArea, SettingsPanelName, false);
        }

        /** 홈 버튼 배선. 화면 참조 없이 다른 화면 닫기만 한다(HudScreenButton.homeButton) */
        private static void WireHomeButton(Transform topBar, string buttonName)
        {
            var buttonObject = topBar.Find(buttonName);
            if (buttonObject == null)
            {
                Debug.LogError("[Onikiri] Cannot wire home button " + buttonName);
                return;
            }

            var control = buttonObject.GetComponent<Onikiri.UI.HudScreenButton>();
            if (control == null)
                control = buttonObject.gameObject.AddComponent<Onikiri.UI.HudScreenButton>();

            var so = new SerializedObject(control);
            so.FindProperty("button").objectReferenceValue = buttonObject.GetComponent<Button>();
            so.FindProperty("screen").objectReferenceValue = null;
            so.FindProperty("homeButton").boolValue = true;
            so.FindProperty("needsReselect").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            // 홈 모드는 WireHomeButton만 켠다. 초상이 스탯 창을 열던 세대의
            // 값이 남지 않게 화면을 여는 배선은 항상 끈다
            so.FindProperty("homeButton").boolValue = false;
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
