using Onikiri.Cloud;
using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 랭킹 화면 (54단계). 상단 바의 "랭킹" 칩이 연다.
     *
     * 자리 규칙은 스탯·재선택·설정과 같다(HudScreensBuilder 머리 주석) -
     * SafeArea 직속, 성장 패널 띠를 덮고, 꺼진 채 저장되고, 여는 버튼이 닫는다.
     * 다른 점은 목록이 길어 스크롤이 있다는 것뿐이라, 스크롤 구조는 퀘스트
     * 패널의 것을 그대로 따른다(Viewport(RectMask2D) + Content).
     *
     * ## 왜 하단 탭이 아니라 상단 바인가
     *
     * 하단 탭 여섯 칸이 이미 다 찼다(캐릭터·성장·대장간·동료·퀘스트·상점).
     * 그리고 랭킹은 그 여섯과 성질이 다르다 - 나머지는 전부 **내 것을 키우는**
     * 화면이고 랭킹은 **남과 견주는** 화면이라, 스탯·설정과 같은 줄에 서는
     * 편이 동선의 뜻에 맞는다.
     */
    public static class LeaderboardPanelBuilder
    {
        public const string PanelName = "LeaderboardPanel";
        public const string ButtonName = "RankingButton";

        /**
         * @brief 목록에 세우는 줄 수 = 상위 몇 위까지 보여주는가.
         *
         * 100인 이유는 이 숫자가 "목록으로 볼 수 있는 것"의 관례적 끝이라서다.
         * 그 밖의 사람에게는 목록이 아니라 **내 순위 한 줄**이 답이고, 그것은
         * 집계 쿼리가 따로 낸다(CloudScores.FetchRankAsync).
         *
         * 줄을 미리 100개 세워두고 남는 것을 꺼둔다. 실행 중에 만들면 첫
         * 조회에서 100개의 생성이 한 프레임에 몰린다.
         */
        public const int TopCount = 100;

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        private const float SidePadding = 48f;
        private const float TopPadding = 16f;
        private const float HeaderHeight = 64f;

        /**
         * @brief 계정 줄(55단계)의 높이. 이름 줄 **위**에 선다.
         *
         * 순서가 계정 → 이름인 이유는 둘의 관계가 상하이기 때문이다 - 계정은
         * "이 기록이 누구 것인가"이고 이름은 "그 사람이 뭐라고 불리는가"다.
         * 반대로 두면 이름을 정한 사람이 그 아래에서 "게스트"를 보게 된다.
         *
         * ⚠️ **목록이 66px 짧아졌다** (뷰포트 392 -> 326px, 보이는 줄 6.3 -> 5.2).
         * 판을 늘릴 수는 없다(성장 패널 띠가 고정 비율이다). 이 값을 치른
         * 이유는 복구의 결과 - 내 순위가 1층에서 원래 기록으로 돌아오는 것 -
         * 가 보이는 화면이 여기뿐이라서다. 설정 화면에 두면 성공 문구 한 줄만
         * 보고 나와야 한다.
         */
        private const float AccountRowHeight = 56f;

        private const float NameRowHeight = 60f;
        private const float EditRowHeight = 60f;
        private const float RowGap = 6f;
        private const float RowHeight = 56f;

        private const float FooterHeight = 56f;
        private const float StatusHeight = 36f;

        /** 줄 세 개가 시작하는 y. 아래 셋이 여기서부터 차례로 쌓인다 */
        private const float AccountRowTop = TopPadding + HeaderHeight + 8f;
        private const float NameRowTop = AccountRowTop + AccountRowHeight + RowGap;
        private const float NameEditTop = NameRowTop + NameRowHeight + RowGap;

        /** 목록 위쪽이 시작하는 y (패널 위에서부터). 제목 + 계정 + 이름 + 입력 */
        private const float ListTop = NameEditTop + EditRowHeight + RowGap;

        /** 목록 아래쪽이 끝나는 y (패널 밑에서부터). 상태줄 + 내 순위 줄 */
        private const float ListBottom = 8f + FooterHeight + 4f + StatusHeight + 4f;

        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        [MenuItem("Onikiri/Build Leaderboard Panel")]
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
            RectTransform root;
            var panel = EnsurePanel(safeArea, font, out root);

            BuildTitle(panel, font, "랭킹");

            var accountLabel = BuildAccountRow(panel, font, out Button accountButton,
                                               out TMP_Text accountButtonLabel);
            var nameLabel = BuildNameRow(panel, font, out Button editButton);
            var editGroup = BuildNameEditRow(panel, font, out TMP_InputField input,
                                             out Button confirmButton);
            var content = BuildScroll(panel);
            var rows = BuildRows(content, font);
            var status = BuildStatus(panel, font);
            var myRank = BuildFooter(panel, font, out Button refreshButton);

            // 컴포넌트는 창이 아니라 **루트**에 붙는다 (#1). 여는 쪽이 켜고
            // 끄는 것이 루트라, 창에 붙으면 OnEnable 새로고침이 안 돈다
            WirePanel(root, rows, status, myRank, refreshButton,
                      nameLabel, editButton, editGroup, input, confirmButton,
                      accountLabel, accountButton, accountButtonLabel);

            EnsureSubmitter();
            WireButton(safeArea);

            root.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Leaderboard popup built: {0}줄 (뷰포트 {1:F0}px, 목록 {2:F0}px).",
                TopCount, ViewportHeight, TopCount * (RowHeight + RowGap)));
        }

        // ---------------------------------------------------------------- 판

        /**
         * @brief 창 높이 / 디자인 높이 (#1 팝업화).
         *
         * 그전에는 성장 패널 띠(0.075~0.45 = 720px)에 갇혀 있었다. 55단계에서
         * 계정 줄이 들어오며 목록 뷰포트가 66px 짧아졌고, 순위 한 줄이
         * 62px이라 **그 한 줄이 통째로 사라진 것**과 같았다.
         *
         * 팝업은 띠에 묶이지 않는다. 0.62(1190px)면 55단계 이전보다 오히려
         * 넓고, 그러면서 위아래로 게임 화면이 남아 있어 "잠깐 열어본 것"으로
         * 읽힌다. 전체 화면으로 키우지 않는 이유가 그것이다 - 랭킹은 보고
         * 나오는 화면이지 머무는 화면이 아니다.
         */
        private const float WindowFraction = 0.62f;

        private static float BandHeight { get { return PopupBuilder.WindowHeight(WindowFraction); } }

        private static float ViewportHeight { get { return BandHeight - ListTop - ListBottom; } }

        private static RectTransform EnsurePanel(Transform safeArea, TMP_FontAsset font,
                                                 out RectTransform root)
        {
            // 팝업 뼈대(딤 + 창 + X)는 공용이다. 이 빌더는 창 안의 내용만 안다
            return PopupBuilder.Ensure(safeArea, PanelName, font, WindowFraction, out root);
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

        // ---------------------------------------------------------------- 계정 (55단계)

        /**
         * @brief 계정 상태 한 줄 + "구글 로그인" 버튼.
         *
         * ⚠️ 상태 라벨은 **동적 폰트**를 쓴다. 연동되면 여기에 구글 계정의
         * 표시 이름이 들어오는데, 그것은 54단계의 플레이어 이름과 똑같이
         * **남이 지은 글자**라 정적 아틀라스로 덮을 수 없다. 이름 라벨만
         * 동적이던 규칙에 이 자리가 두 번째로 추가된다.
         *
         * ⚠️ 버튼 폭 240px는 재 본 값이다. "구글 로그인"은 캡션 33pt에서
         * 한글 5자 + 공백 = 181px이라 200px 버튼에도 들어가지만, 공급자
         * 이름이 붙는 문구라 "Apple 로그인"(다음 스텝)까지 같은 버튼을 쓴다.
         */
        private static TMP_Text BuildAccountRow(RectTransform panel, TMP_FontAsset font,
                                                out Button accountButton,
                                                out TMP_Text accountButtonLabel)
        {
            var row = EnsureRow(panel, "AccountRow", AccountRowTop, AccountRowHeight);

            var label = CreateLabel(row, font, "State", TextAlignmentOptions.Left);
            StretchInside(label, 24f, 0.72f);
            UseNameFont(label);
            label.text = "계정 확인 중...";

            accountButton = CreateButton(row, font, "LinkButton", "구글 로그인", 240f);
            accountButtonLabel = accountButton.transform.Find("Label").GetComponent<TMP_Text>();

            return label;
        }

        // ---------------------------------------------------------------- 이름

        /** 내 이름 한 줄 + 변경 버튼. 목록에서 나를 찾는 첫 단서다 */
        private static TMP_Text BuildNameRow(RectTransform panel, TMP_FontAsset font,
                                             out Button editButton)
        {
            var row = EnsureRow(panel, "NameRow", NameRowTop, NameRowHeight);

            var name = CreateLabel(row, font, "Name", TextAlignmentOptions.Left);
            StretchInside(name, 24f, 0.62f);
            UseNameFont(name);
            name.text = PlayerProfile.DefaultName;

            var button = CreateButton(row, font, "EditButton", "이름 변경", 200f);
            editButton = button;

            return name;
        }

        /**
         * @brief 이름 입력 줄. 기본은 꺼져 있고, 이름을 안 정한 사람에게만 켜진 채 열린다.
         *
         * 자리를 **항상 비워둔다**(꺼져 있어도 목록이 위로 안 올라온다). 열고
         * 닫을 때마다 목록이 60px씩 뛰면 그 순간 읽던 줄을 놓친다.
         */
        private static GameObject BuildNameEditRow(RectTransform panel, TMP_FontAsset font,
                                                   out TMP_InputField input, out Button confirmButton)
        {
            var row = EnsureRow(panel, "NameEdit", NameEditTop, EditRowHeight);

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

            // 입력칸도 이름 자리다 - 자기 이름을 치는 동안 글자가 네모로
            // 보이면 그 이름을 못 쓴다고 읽는다
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
            placeholder.text = "이름 (" + PlayerProfile.MaxLength + "자까지)";

            input = fieldObject.AddComponent<TMP_InputField>();
            input.textViewport = fieldRect;
            input.textComponent = (TMP_Text)text;
            input.placeholder = placeholder;
            input.characterLimit = PlayerProfile.MaxLength;
            // 줄바꿈이 이름에 끼면 랭킹 한 줄이 두 줄이 된다. PlayerProfile이
            // 저장 직전에도 지우지만, 입력에서 애초에 못 들어오게 막는다
            input.lineType = TMP_InputField.LineType.SingleLine;

            confirmButton = CreateButton(row, font, "Confirm", "확인", 200f);

            // 자리는 남기고 내용만 끈다 - 목록이 뛰지 않는다(머리 주석)
            row.gameObject.SetActive(false);
            return row.gameObject;
        }

        // ---------------------------------------------------------------- 목록

        private static RectTransform BuildScroll(RectTransform panel)
        {
            var go = new GameObject("Viewport", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var viewport = (RectTransform)go.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(SidePadding, ListBottom);
            viewport.offsetMax = new Vector2(-SidePadding, -ListTop);

            go.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);

            var content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, TopCount * (RowHeight + RowGap));
            content.anchoredPosition = Vector2.zero;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
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

        private static Onikiri.UI.LeaderboardPanel.Row[] BuildRows(RectTransform content,
                                                                   TMP_FontAsset font)
        {
            var rows = new Onikiri.UI.LeaderboardPanel.Row[TopCount];

            for (int i = 0; i < TopCount; i++)
            {
                var go = new GameObject("Rank" + (i + 1), typeof(RectTransform));
                go.transform.SetParent(content, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, RowHeight);
                rect.anchoredPosition = new Vector2(0f, -i * (RowHeight + RowGap));

                var background = go.AddComponent<Image>();
                UiSkin.ApplyPanel(background, UiSkin.Row);

                // 순위 · 이름 · 도달층. 셋 다 캡션 크기다 - 목록은 훑는
                // 화면이고, 이 판에서 44pt로 남는 것은 제목과 버튼뿐이다
                var rank = CreateLabel(go.transform, font, "Rank", TextAlignmentOptions.Right);
                UiFonts.Demote(rank);
                PlaceStretched((RectTransform)rank.transform, 16f, 0f, 0.14f);
                rank.color = DimColor;
                rank.text = "-";

                var name = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
                UseNameFont(name);
                PlaceStretched((RectTransform)name.transform, 16f, 0.16f, 0.68f);
                name.text = "-";

                var stage = CreateLabel(go.transform, font, "Stage", TextAlignmentOptions.Right);
                UiFonts.Demote(stage);
                PlaceStretched((RectTransform)stage.transform, 16f, 0.68f, 1f);
                stage.text = "-";

                go.SetActive(false);

                rows[i] = new Onikiri.UI.LeaderboardPanel.Row
                {
                    root = go,
                    rankLabel = rank,
                    nameLabel = name,
                    stageLabel = stage,
                    background = background,
                };
            }

            return rows;
        }

        // ---------------------------------------------------------------- 바닥

        /** 상태 한 줄. 로딩·빈 목록·실패가 전부 여기 적힌다 */
        private static TMP_Text BuildStatus(RectTransform panel, TMP_FontAsset font)
        {
            var go = new GameObject("Status", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, StatusHeight);
            rect.anchoredPosition = new Vector2(0f, 8f + FooterHeight + 4f);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(label);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -8f);
            labelRect.offsetMax = new Vector2(0f, 8f);
            label.color = DimColor;
            label.text = "불러오는 중...";

            return label;
        }

        /** 내 순위 + 새로고침. 목록 밖의 사람에게는 이 줄이 유일한 답이다 */
        private static TMP_Text BuildFooter(RectTransform panel, TMP_FontAsset font,
                                            out Button refreshButton)
        {
            var go = new GameObject("Footer", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, FooterHeight);
            rect.anchoredPosition = new Vector2(0f, 8f);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            var label = CreateLabel(go.transform, font, "MyRank", TextAlignmentOptions.Left);
            UiFonts.Demote(label);
            PlaceStretched((RectTransform)label.transform, 24f, 0f, 0.6f);
            label.text = "내 순위  -";

            refreshButton = CreateButton(rect, font, "Refresh", "새로고침", 200f);

            return label;
        }

        // ---------------------------------------------------------------- 배선

        private static void WirePanel(RectTransform panel, Onikiri.UI.LeaderboardPanel.Row[] rows,
                                      TMP_Text status, TMP_Text myRank, Button refreshButton,
                                      TMP_Text nameLabel, Button editButton, GameObject editGroup,
                                      TMP_InputField input, Button confirmButton,
                                      TMP_Text accountLabel, Button accountButton,
                                      TMP_Text accountButtonLabel)
        {
            var component = panel.gameObject.GetComponent<Onikiri.UI.LeaderboardPanel>();
            if (component == null)
                component = panel.gameObject.AddComponent<Onikiri.UI.LeaderboardPanel>();

            var battle = GameObject.Find("Battle");

            var so = new SerializedObject(component);
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("myRankLabel").objectReferenceValue = myRank;
            so.FindProperty("refreshButton").objectReferenceValue = refreshButton;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("nameEditButton").objectReferenceValue = editButton;
            so.FindProperty("nameEditGroup").objectReferenceValue = editGroup;
            so.FindProperty("nameInput").objectReferenceValue = input;
            so.FindProperty("nameConfirmButton").objectReferenceValue = confirmButton;
            so.FindProperty("accountLabel").objectReferenceValue = accountLabel;
            so.FindProperty("accountButton").objectReferenceValue = accountButton;
            so.FindProperty("accountButtonLabel").objectReferenceValue = accountButtonLabel;
            so.FindProperty("progress").objectReferenceValue =
                battle != null ? battle.GetComponent<StageProgress>() : null;
            // 내 줄은 **새 색이 아니라 같은 판을 밝힌 것**이다(49단계 장착 행과
            // 같은 판단 - 강조색 셋 규칙 안에 머문다)
            so.FindProperty("myRowColor").colorValue = UiSkin.InlayTint;
            so.FindProperty("rowColor").colorValue = UiSkin.Row;

            var rowsProperty = so.FindProperty("rows");
            rowsProperty.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                var element = rowsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                element.FindPropertyRelative("rankLabel").objectReferenceValue = rows[i].rankLabel;
                element.FindPropertyRelative("nameLabel").objectReferenceValue = rows[i].nameLabel;
                element.FindPropertyRelative("stageLabel").objectReferenceValue = rows[i].stageLabel;
                element.FindPropertyRelative("background").objectReferenceValue = rows[i].background;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 자동 제출기를 Battle에 세운다.
         *
         * 판이 아니라 Battle에 사는 이유는 **판이 꺼져 있어도 돌아야** 하기
         * 때문이다. 랭킹 화면을 한 번도 안 연 사람의 도달층도 올라가야 한다 -
         * 그러지 않으면 랭킹표에 랭킹을 본 사람만 있게 된다.
         */
        private static void EnsureSubmitter()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogWarning("[Onikiri] Battle root missing - leaderboard submitter not wired.");
                return;
            }

            var submitter = battle.GetComponent<LeaderboardSubmitter>();
            if (submitter == null) submitter = battle.AddComponent<LeaderboardSubmitter>();

            var so = new SerializedObject(submitter);
            so.FindProperty("progress").objectReferenceValue = battle.GetComponent<StageProgress>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 상단 바의 칩에 이 화면을 물린다 (HudScreensBuilder.WireScreenButton과 같은 규칙) */
        private static void WireButton(Transform safeArea)
        {
            var topBar = MainSceneBuilder.FindBand("TopBar");
            if (topBar == null) return;

            var buttonObject = topBar.Find(ButtonName);
            var screen = safeArea.Find(PanelName);
            if (buttonObject == null)
            {
                Debug.LogWarning("[Onikiri] " + ButtonName + " missing - run Build Combat Content first.");
                return;
            }
            if (screen == null) return;

            var control = buttonObject.GetComponent<Onikiri.UI.HudScreenButton>();
            if (control == null)
                control = buttonObject.gameObject.AddComponent<Onikiri.UI.HudScreenButton>();

            var so = new SerializedObject(control);
            so.FindProperty("button").objectReferenceValue = buttonObject.GetComponent<Button>();
            so.FindProperty("screen").objectReferenceValue = screen.gameObject;
            so.FindProperty("needsReselect").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 조각

        private static RectTransform EnsureRow(RectTransform panel, string name, float top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, height);
            rect.anchoredPosition = new Vector2(0f, -top);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            return rect;
        }

        private static Button CreateButton(Transform parent, TMP_FontAsset font, string name,
                                           string text, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

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

        /** 가로 비율로 자리를 잡는다. 세로는 Ellipsis 잘림 방지로 넓힌다 */
        private static void PlaceStretched(RectTransform rect, float padding,
                                           float anchorMinX, float anchorMaxX)
        {
            rect.anchorMin = new Vector2(anchorMinX, 0f);
            rect.anchorMax = new Vector2(anchorMaxX, 1f);
            rect.offsetMin = new Vector2(padding, -14f);
            rect.offsetMax = new Vector2(-padding, 14f);
        }

        private static void StretchInside(TMP_Text label, float sidePadding, float anchorMaxX)
        {
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(anchorMaxX, 1f);
            rect.offsetMin = new Vector2(sidePadding, -14f);
            rect.offsetMax = new Vector2(-sidePadding, 14f);
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        /**
         * @brief 이름 라벨을 동적 폰트로 바꾼다. **남이 지은 글자가 오는 자리 전용.**
         *
         * 정적 아틀라스에는 UIStrings.txt에 적힌 글자만 있다(PixelFontAssetBuilder).
         * 그 계약이 성립하지 않는 유일한 자리가 남의 이름이라, 이름 라벨만
         * 런타임 래스터가 되는 한 벌을 쓴다. 나머지 라벨은 손대지 않는다.
         */
        private static void UseNameFont(TMP_Text label)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                PixelFontAssetBuilder.NameFontPath);
            if (label == null || font == null) return;

            label.font = font;
            label.fontSharedMaterial = font.material;
            // 캡션 크기로 구운 아틀라스다 - 크기도 함께 맞춰야 1:1이 유지된다
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriCaption;
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
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
    }
}
