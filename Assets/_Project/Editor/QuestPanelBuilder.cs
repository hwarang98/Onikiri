using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 퀘스트 화면과 그것을 구동하는 QuestSystem을 세운다.
     *
     * ## 자리는 스킬 패널과 같다
     *
     * 화면 10~45%를 덮는다. 성장·스킬과 같은 띠를 쓰는 이유도 같다 - **셋이
     * 동시에 보일 일이 없기** 때문이다. 하단 탭이 셋 사이를 오가고, 같은 자리를
     * 쓰면 "아래쪽 절반은 목록"이라는 화면의 문법이 유지된다.
     *
     * ## 진입점은 하단 탭이다
     *
     * 사양이 "상단바 아이콘 또는 하단 탭 - 판단해서 보고"라고 열어 뒀는데,
     * 하단 탭을 골랐다. 이유는 셋이다:
     *
     *   1. **상단 바에는 자리가 없다.** 세로 192px 안에 골드·스테이지·경험치 바가
     *      이미 들어 있고, 17단계에 스테이지 표시가 길어지면서 골드 라벨의 "골드"
     *      두 글자를 지워야 했다(BattleContentBuilder 주석). 거기에 보석 표시까지
     *      더해야 하는 단계에서 진입 아이콘을 또 넣을 수 없다.
     *   2. **퀘스트는 화면이지 상태가 아니다.** 상단 바는 지금 얼마인지를 말하고
     *      하단 탭은 어디로 갈지를 말한다. 스킬이 하단 탭에 있는 것과 같은 층위다.
     *   3. 하단 탭이 하나뿐이라 **탭바가 탭바로 보이지 않았다** - MinTabSlots로
     *      빈 칸을 만들어 두고 있었다. 둘이 되면 그 임시방편이 없어진다.
     *
     * 상단 바에는 보석 **잔액**만 올린다. 그것은 상태이므로 그 줄이 맞다.
     */
    public static class QuestPanelBuilder
    {
        public const string PanelName = "QuestPanel";

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        // 1080 폭 기준. 스킬 패널과 같은 여백을 쓴다
        private const float SidePadding = 48f;
        private const float TopPadding = 10f;

        /** 제목 + 리셋 타이머 한 줄 */
        private const float HeaderHeight = 56f;
        private const float HeaderGap = 8f;

        /** 서브탭 줄 */
        private const float TabHeight = 68f;
        private const float TabGap = 10f;

        /**
         * @brief 퀘스트 한 줄의 높이.
         *
         * **두 줄이다.** 제목+보상이 윗줄, 진행바+진행숫자가 아랫줄이다.
         *
         * 처음에 세 줄(제목 / 보상 / 진행바)로 108px에 넣었다가 물렸다 - 글자가
         * 44pt인데 줄 상자를 34px로 줬더니 **보상 문구가 아래로 넘쳐 진행바를
         * 가로질렀다.** 44는 아틀라스를 구운 크기라 더 줄일 수 없다
         * (PixelFontSizes.GalmuriSmall) - 상자를 글자에 맞춰야지 그 반대가 아니다.
         *
         * 보상을 제목과 같은 줄 오른쪽으로 올려 한 줄을 없앴다. 둘 다 "이 퀘스트가
         * 무엇인가"라 같은 줄에 있는 것이 오히려 맞다.
         */
        private const float RowHeight = 116f;
        private const float RowGap = 10f;

        /**
         * @brief 받기 버튼이 차지하는 오른쪽 폭.
         *
         * 176으로 잡았다가 늘렸다. 반복 퀘스트가 티어를 쌓으면 문구가
         * "받기 x20"이 되는데 44pt에서 그것이 200px을 넘어 **버튼 밖으로 삐져나가고
         * 진행 숫자와 겹쳤다.** 배수는 작게 적어(QuestRow) 폭도 함께 늘린다.
         */
        private const float ClaimWidth = 208f;

        // 39단계 톤 통일: 자기 색을 갖지 않는다. 팔레트의 단일 출처는 UiSkin이다
        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        private static readonly QuestKind[] Kinds =
        {
            QuestKind.Daily, QuestKind.Repeat, QuestKind.Achievement
        };

        private static readonly string[] KindNames = { "일일", "반복", "업적" };

        [MenuItem("Onikiri/Build Quest Panel")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static QuestSystem Build()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogError("[Onikiri] Battle root missing - run Build Combat Content first.");
                return null;
            }

            var system = EnsureSystem(battle);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - run Build Main Scene first.");
                return null;
            }

            RectTransform root;
            var panel = EnsurePanel(safeArea, font, out root);
            BuildHeader(panel, font);

            var pages = new GameObject[Kinds.Length];
            var content = EnsureScroll(panel);

            for (int i = 0; i < Kinds.Length; i++)
                pages[i] = BuildPage(content, system, font, Kinds[i]);

            BuildTabs(panel, font, pages);

            // 판은 꺼진 채로 저장된다. **플레이 화면의 퀘스트 아이콘이 켠다**
            // (#16) - 하단 탭이 아니다
            root.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Quest panel built: 일일 {0} / 반복 {1} / 업적 {2} = {3}줄, "
                + "가장 긴 페이지 {4:F0}px (뷰포트 {5:F0}px).",
                QuestCatalog.DailyCount, QuestCatalog.RepeatCount, QuestCatalog.AchievementCount,
                QuestCatalog.TotalCount, TallestPageHeight, ViewportHeight));

            // 판을 새로 만들었으니 여는 쪽을 다시 물린다. #16 뒤로 그것은
            // 하단 탭이 아니라 플레이 화면의 퀘스트 아이콘이다 - 그 배선은
            // BattleContentBuilder가 들고 있다(BuildQuestButton)
            BattleContentBuilder.RelinkScreenTabs();

            return system;
        }

        // ---------------------------------------------------------------- 시스템

        private static QuestSystem EnsureSystem(GameObject battle)
        {
            if (battle.GetComponent<GemWallet>() == null) battle.AddComponent<GemWallet>();

            var system = battle.GetComponent<QuestSystem>();
            if (system == null) system = battle.AddComponent<QuestSystem>();

            var samurai = GameObject.Find("Samurai");

            var so = new SerializedObject(system);
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("wallet").objectReferenceValue = battle.GetComponent<PlayerWallet>();
            so.FindProperty("character").objectReferenceValue = battle.GetComponent<CharacterLevel>();
            so.FindProperty("stage").objectReferenceValue = battle.GetComponent<StageProgress>();
            so.FindProperty("upgrades").objectReferenceValue =
                Object.FindFirstObjectByType<UpgradeSystem>(FindObjectsInactive.Include);
            so.FindProperty("skills").objectReferenceValue =
                Object.FindFirstObjectByType<SkillSystem>(FindObjectsInactive.Include);
            so.FindProperty("spawner").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.Battle.EnemySpawner>(FindObjectsInactive.Include);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 세션이 세이브를 넘겨줘야 한다. 배선이 빠지면 퀘스트가 매번 0에서
            // 시작하고, 그 증상은 "받았는데 껐다 켜면 다시 받을 수 있다"로 나온다
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null)
            {
                var sessionSo = new SerializedObject(session);
                sessionSo.FindProperty("quests").objectReferenceValue = system;
                sessionSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (samurai == null)
                Debug.LogWarning("[Onikiri] Samurai missing while wiring quests.");

            return system;
        }

        // ---------------------------------------------------------------- 판

        /**
         * @brief 창 높이 / 디자인 높이 (#16 팝업화).
         *
         * ## 어느 탭에 맞추는가
         *
         * 탭 셋의 길이가 제각각이다 - 일일 5줄(600px), 반복 3줄, 업적 14줄
         * (1764px). 고정 높이라 어딘가는 반드시 어긋나는데, **가장 자주 보는
         * 탭**에 맞춘다.
         *
         * 처음에 랭킹과 같은 0.62(1190px)로 잡았다가 실기에서 되돌렸다 -
         * 일일 탭에서 목록 아래가 **700px 비었다.** 업적을 다 담으려던 값인데,
         * 업적은 어차피 어떤 높이에서도 스크롤되므로 그 여유가 사는 것은
         * 업적 하나뿐이고 나머지 둘이 값을 치른다.
         *
         * 0.42(806px) = 머리(152) + 일일 다섯 줄(600) + 여백. 44단계의
         * 규칙 그대로다 - **상자를 내용에 맞춘다.**
         */
        private const float WindowFraction = 0.42f;

        private static float BandHeight
        {
            get { return PopupBuilder.WindowHeight(WindowFraction); }
        }

        private static float ViewportHeight
        {
            get { return BandHeight - (TopPadding + HeaderHeight + HeaderGap + TabHeight + TabGap); }
        }

        private static float PageHeight(QuestKind kind)
        {
            return QuestCatalog.Of(kind).Length * (RowHeight + RowGap);
        }

        private static float TallestPageHeight
        {
            get
            {
                float tallest = 0f;
                foreach (var kind in Kinds) tallest = Mathf.Max(tallest, PageHeight(kind));
                return tallest;
            }
        }

        /**
         * @brief 퀘스트는 **화면이 아니라 팝업이다** (#16).
         *
         * ## 왜 하단 탭에서 내려왔는가
         *
         * 하단 탭은 **키우는 것들**의 줄이다 - 캐릭터·스킬·장비·동료·상점은
         * 전부 "내 것을 키우러 들어가는" 화면이고, 거기서 무언가를 사고 끼운다.
         * 퀘스트는 그 층위가 아니다. 받고 나오는 화면이고, 열어야 할 이유는
         * **받을 것이 생겼을 때** 뿐이다.
         *
         * 그래서 진입점이 플레이 화면의 아이콘으로 간다(BuildQuestButton).
         * 알림 점이 거기 붙으므로 "받을 것이 있다 → 그 자리를 누른다"가 한
         * 동작이 되고, 진행도 줄(#13)이 바로 옆에 있어 "지금 뭘 하는 중인가 →
         * 받으러 간다"가 같은 자리에서 이어진다.
         *
         * 컨테이너만 바뀐다. 목록·수령·탭·배지 로직은 한 줄도 안 옮겼다.
         */
        private static RectTransform EnsurePanel(Transform safeArea, TMP_FontAsset font,
                                                 out RectTransform root)
        {
            return PopupBuilder.Ensure(safeArea, PanelName, font, WindowFraction, out root);
        }

        /**
         * @brief 제목 + 일일 리셋 타이머.
         *
         * 타이머를 **머리글에** 둔다. 일일 탭 안에 넣으면 다른 탭에서 사라지는데,
         * 남은 시간은 어느 탭을 보고 있든 같은 사실이고 "일일을 지금 해야 하나"의
         * 답이 거기 있다.
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
            titleRect.anchorMax = new Vector2(0.45f, 1f);
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = Vector2.zero;
            title.text = "퀘스트";
            title.color = DimColor;

            // 남은 시간은 보조정보다(39단계 위계). 머리글에서 44pt로 남는 것은
            // 제목뿐이고, 시계는 캡션으로 받친다
            var timer = CreateLabel(go.transform, font, "ResetTimer", TextAlignmentOptions.Right);
            UiFonts.Demote(timer);
            var timerRect = (RectTransform)timer.transform;
            timerRect.anchorMin = new Vector2(0.35f, 0f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.offsetMin = Vector2.zero;
            // 팝업의 닫기 X가 창 오른쪽 위 모서리에 앉으므로 그만큼 물러난다
            // (#16). 안 물러나면 "초기화까지 00:00:00"의 끝자리가 X 밑으로
            // 들어간다 - 팝업 뼈대가 공용이라 이 여백도 공용 상수에서 온다
            timerRect.offsetMax = new Vector2(-(PopupBuilder.CloseSize + 16f), 0f);
            timer.color = DimColor;
            timer.text = "초기화까지 00:00:00";

            var component = go.AddComponent<Onikiri.UI.DailyResetTimer>();
            var so = new SerializedObject(component);
            so.FindProperty("label").objectReferenceValue = timer;
            so.FindProperty("prefix").stringValue = "초기화까지 ";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 스크롤. 업적 열 줄이 뷰포트에 안 들어간다.
         *
         * 성장 패널과 같은 구조다 - Viewport(RectMask2D) 안에 Content가 있고
         * 페이지들이 그 아래 겹쳐 있다. Content 높이는 **가장 긴 페이지**에
         * 맞춘다. 합으로 잡으면 어느 탭을 보든 그 아래로 빈 공간이 스크롤된다.
         */
        private static RectTransform EnsureScroll(RectTransform panel)
        {
            var go = new GameObject("Viewport", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var viewport = (RectTransform)go.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(SidePadding, 0f);
            viewport.offsetMax = new Vector2(-SidePadding,
                -(TopPadding + HeaderHeight + HeaderGap + TabHeight + TabGap));

            go.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);

            var content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, TallestPageHeight);
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

        private static GameObject BuildPage(RectTransform content, QuestSystem system,
                                            TMP_FontAsset font, QuestKind kind)
        {
            var go = new GameObject(kind + "Page", typeof(RectTransform));
            go.transform.SetParent(content, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, PageHeight(kind));
            rect.anchoredPosition = Vector2.zero;

            // 탭별 독립 스크롤 (#6). 퀘스트 안쪽 탭 셋이 한 스크롤을 공유해서
            // 한 탭에서 내린 위치가 다른 탭에 그대로 남았고, Content 높이도
            // 가장 긴 탭에 고정이라 짧은 탭에서는 목록이 끝난 뒤로도 계속
            // 스크롤됐다 - "목록이 안 보일 때까지 스크롤되는" 것이 그것이다
            var memory = go.AddComponent<Onikiri.UI.ScrollPageMemory>();
            var memorySo = new UnityEditor.SerializedObject(memory);
            memorySo.FindProperty("scroll").objectReferenceValue =
                content.GetComponentInParent<ScrollRect>();
            memorySo.FindProperty("content").objectReferenceValue = content;
            memorySo.FindProperty("pageHeight").floatValue = PageHeight(kind);
            memorySo.ApplyModifiedPropertiesWithoutUndo();

            var specs = QuestCatalog.Of(kind);
            for (int i = 0; i < specs.Length; i++)
                BuildRow(rect, system, font, kind, i);

            return go;
        }

        private static void BuildRow(RectTransform page, QuestSystem system, TMP_FontAsset font,
                                     QuestKind kind, int index)
        {
            var spec = QuestCatalog.Of(kind)[index];

            var go = new GameObject("Quest" + index, typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowGap));

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            // ---- 왼쪽: 제목 / 보상 · 진행 / 진행바 ----

            // 윗줄: 제목(왼쪽) + 진행숫자(오른쪽)
            //
            // **짧은 것이 윗줄이다.** 처음에는 보상을 제목 옆에 뒀는데, 업적 보상이
            // "보석 20 · 골드 · EXP"까지 길어져(44pt에서 약 500px) "30스테이지 도달"
            // 같은 긴 제목과 정면으로 겹쳤다. 진행 숫자는 "1 / 30"이라 짧다.
            // 행 글자는 전부 캡션 크기(39단계 - 캐릭터 화면과 같은 위계). 이
            // 행에서 44pt로 남는 것은 받기 버튼뿐이다 - 목록은 훑는 화면이다
            var title = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Left);
            UiFonts.Demote(title);
            PlaceStretched((RectTransform)title.transform, 24f, ClaimWidth + 200f, 6f, 50f);
            title.text = spec.Title;

            var progress = CreateLabel(go.transform, font, "Progress", TextAlignmentOptions.Right);
            UiFonts.Demote(progress);
            PlaceStretched((RectTransform)progress.transform, 24f, ClaimWidth + 24f, 6f, 50f);
            progress.color = DimColor;
            progress.text = "0 / " + spec.Target;

            // 아랫줄: 진행바(왼쪽) + 보상(오른쪽)
            var reward = BuildRewardGroup(go.transform, font, spec);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(go.transform, false);
            // 보상 문구가 차지할 만큼 비운다. 업적이 가장 길고, QuestRow가 그것을
            // 75% 크기로 적으므로 약 380px이다
            PlaceStretched((RectTransform)track.transform, 24f, ClaimWidth + 400f, 78f, 12f);
            var trackImage = track.AddComponent<Image>();
            // 바의 빈 부분은 화면 어디서든 같은 어둠이다(UiSkin.BarTrack 규칙).
            // 이 판만 #2A253C를 따로 들고 있었다 - 39단계 톤 통일
            trackImage.color = UiSkin.BarTrack;
            trackImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color32(0x62, 0x6A, 0xC8, 0xFF);
            fillImage.raycastTarget = false;

            // ---- 오른쪽: 받기 버튼 / 완료 배지 ----

            var claimObject = new GameObject("Claim", typeof(RectTransform));
            claimObject.transform.SetParent(go.transform, false);

            var claimRect = (RectTransform)claimObject.transform;
            claimRect.anchorMin = new Vector2(1f, 0.5f);
            claimRect.anchorMax = new Vector2(1f, 0.5f);
            claimRect.pivot = new Vector2(1f, 0.5f);
            claimRect.sizeDelta = new Vector2(ClaimWidth, 72f);
            claimRect.anchoredPosition = new Vector2(-20f, 0f);

            var claimImage = claimObject.AddComponent<Image>();
            UiSkin.ApplyPanel(claimImage, UiSkin.Chrome);

            var claimButton = claimObject.AddComponent<Button>();
            UiSkin.ApplyButton(claimButton, claimImage);

            var claimLabel = CreateLabel(claimObject.transform, font, "Label", TextAlignmentOptions.Center);
            var claimLabelRect = (RectTransform)claimLabel.transform;
            claimLabelRect.anchorMin = Vector2.zero;
            claimLabelRect.anchorMax = Vector2.one;
            claimLabelRect.offsetMin = new Vector2(0f, -8f);
            claimLabelRect.offsetMax = new Vector2(0f, 8f);
            claimLabel.text = "받기";

            var doneBadge = new GameObject("Done", typeof(RectTransform));
            doneBadge.transform.SetParent(go.transform, false);
            var doneRect = (RectTransform)doneBadge.transform;
            doneRect.anchorMin = new Vector2(1f, 0.5f);
            doneRect.anchorMax = new Vector2(1f, 0.5f);
            doneRect.pivot = new Vector2(1f, 0.5f);
            doneRect.sizeDelta = new Vector2(ClaimWidth, 72f);
            doneRect.anchoredPosition = new Vector2(-20f, 0f);

            // "완료"는 상태 표시지 동작이 아니다. 받기(44)와 크기가 갈려야
            // 눌리는 것과 끝난 것이 형태로 나뉜다
            var doneLabel = CreateLabel(doneBadge.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(doneLabel);
            var doneLabelRect = (RectTransform)doneLabel.transform;
            doneLabelRect.anchorMin = Vector2.zero;
            doneLabelRect.anchorMax = Vector2.one;
            doneLabelRect.offsetMin = Vector2.zero;
            doneLabelRect.offsetMax = Vector2.zero;
            doneLabel.text = "완료";
            doneLabel.color = DimColor;
            doneBadge.SetActive(false);

            var row = go.AddComponent<Onikiri.UI.QuestRow>();
            var so = new SerializedObject(row);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("index").intValue = index;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("progressLabel").objectReferenceValue = progress;
            so.FindProperty("rewardLabel").objectReferenceValue = reward;
            so.FindProperty("progressFill").objectReferenceValue = fillRect;
            so.FindProperty("claimButton").objectReferenceValue = claimButton;
            so.FindProperty("claimLabel").objectReferenceValue = claimLabel;
            so.FindProperty("doneBadge").objectReferenceValue = doneBadge;
            so.FindProperty("readyText").colorValue = TextColor;
            so.FindProperty("dimText").colorValue = DimColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 서브탭 줄. 스크롤 밖에 고정된다 - 목록이 움직여도 탭은 제자리 */
        private static void BuildTabs(RectTransform panel, TMP_FontAsset font, GameObject[] pages)
        {
            var bar = new GameObject("Tabs", typeof(RectTransform));
            bar.transform.SetParent(panel, false);

            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(-SidePadding * 2f, TabHeight);
            barRect.anchoredPosition = new Vector2(0f, -(TopPadding + HeaderHeight + HeaderGap));

            var tabs = panel.gameObject.AddComponent<Onikiri.UI.QuestPanelTabs>();
            var so = new SerializedObject(tabs);
            var list = so.FindProperty("pages");
            list.arraySize = Kinds.Length;

            for (int i = 0; i < Kinds.Length; i++)
            {
                var go = new GameObject("Tab" + i, typeof(RectTransform));
                go.transform.SetParent(bar.transform, false);

                // 탭 셋이 줄의 왼쪽 ClaimAllLeft만큼을 나눠 쓴다. 오른쪽
                // 나머지는 [일괄 수령] 자리다 (#5)
                var rect = (RectTransform)go.transform;
                float slice = ClaimAllLeft / Kinds.Length;
                rect.anchorMin = new Vector2(i * slice, 0f);
                rect.anchorMax = new Vector2((i + 1) * slice, 1f);
                rect.offsetMin = new Vector2(6f, 0f);
                rect.offsetMax = new Vector2(-6f, 0f);

                var image = go.AddComponent<Image>();
                UiSkin.ApplyPanel(image, UiSkin.Chrome);

                var button = go.AddComponent<Button>();
                UiSkin.ApplyButton(button, image);

                // 서브탭 글자는 캡션 크기 - 하단 탭(38단계)과 같은 티어다
                var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
                UiFonts.Demote(label);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(0f, -6f);
                labelRect.offsetMax = new Vector2(0f, 6f);
                label.text = KindNames[i];

                var badge = BuildBadge(go.transform, font);

                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("kind").enumValueIndex = (int)Kinds[i];
                element.FindPropertyRelative("tab").objectReferenceValue = button;
                element.FindPropertyRelative("tabLabel").objectReferenceValue = label;
                element.FindPropertyRelative("tabBackground").objectReferenceValue = image;
                element.FindPropertyRelative("root").objectReferenceValue = pages[i];
                element.FindPropertyRelative("badge").objectReferenceValue = badge.gameObject;
                element.FindPropertyRelative("badgeLabel").objectReferenceValue =
                    badge.GetComponentInChildren<TMP_Text>(true);
            }

            BuildClaimAll(bar.transform, font);

            so.FindProperty("selectedText").colorValue = TextColor;
            so.FindProperty("unselectedText").colorValue = DimColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 탭 줄에서 탭 셋이 쓰는 몫 (#5). 나머지가 [일괄 수령] 자리다.
         *
         * 0.72 = 984px의 708px. 탭 하나가 236px이라 "일일"·"반복"·"업적"
         * 두 글자(74px)가 넉넉히 들어간다. 남는 276px에 "일괄 수령"
         * (다섯 글자, 캡션 37px 기준 185px)이 좌우 여백과 함께 선다.
         */
        private const float ClaimAllLeft = 0.72f;

        /**
         * @brief [일괄 수령] (#5). 탭 줄의 오른쪽 끝.
         *
         * ## 왜 탭 줄인가
         *
         * 스크롤 밖이라 목록이 어디에 있든 제자리이고, 세 탭 어디에서나
         * 같은 자리에 있다 - 받을 것은 탭마다 따로 쌓이는데 버튼이 탭 안에
         * 있으면 세 번 눌러야 한다. 이 버튼 하나가 셋을 전부 받는다
         * (QuestSystem.ClaimAll).
         *
         * 목록 위에 띄우는 방법도 있었는데, 그러면 마지막 줄의 받기 버튼을
         * 덮는다 - 백 번 누르지 않게 하려고 만든 것이 한 번 누르는 것을
         * 막는 모양이 된다.
         *
         * 받을 것이 없으면 흐려진다. 숨기지 않는 이유는 자리가 사라지면
         * 탭 셋의 폭이 그때마다 바뀌기 때문이다.
         */
        private static void BuildClaimAll(Transform bar, TMP_FontAsset font)
        {
            var go = new GameObject("ClaimAll", typeof(RectTransform));
            go.transform.SetParent(bar, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(ClaimAllLeft, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(6f, 0f);
            rect.offsetMax = new Vector2(-6f, 0f);

            var image = go.AddComponent<Image>();
            // 동작 버튼이라 행(Row)이 아니라 강조판이다. 받는 것은 이 화면에서
            // 가장 하고 싶은 일이고, 초록은 이 게임에서 "좋은 것"이다
            UiSkin.ApplyPanel(image, UiSkin.Panel, UiSkin.Good);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(label);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -6f);
            labelRect.offsetMax = new Vector2(0f, 6f);
            label.text = "일괄 수령";

            var component = go.AddComponent<Onikiri.UI.QuestClaimAllButton>();
            var so = new SerializedObject(component);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("idleLabel").stringValue = "일괄 수령";
            so.ApplyModifiedPropertiesWithoutUndo();

            // 글자가 칸을 넘지 않는지 빌드가 검산한다 (이 프로젝트의 규칙)
            float width = (Onikiri.Core.DisplayConfig.DesignWidth - SidePadding * 2f)
                          * (1f - ClaimAllLeft) - 12f;
            float text = label.GetPreferredValues("일괄 수령", 0f, 0f).x;
            if (text > width)
                Debug.LogWarning(string.Format(
                    "[Onikiri] '일괄 수령' is {0:F0}px but its slot is {1:F0}px"
                    + " - widen the slot (ClaimAllLeft) or shorten the label.", text, width));
        }

        /**
         * @brief 보상 표기 = **아이콘 + 수량** (#11).
         *
         * 그전에는 글자였다: "보석 20 <size=75%>·골드 ·EXP</size>". 세 가지가
         * 걸렸다 - 재화 이름을 매번 읽어야 하고, 곁가지를 75%로 줄여 넣는
         * 편법이 필요했고, 그 문구가 380px을 먹어 진행바를 밀어냈다.
         *
         * 아이콘은 이 게임의 다른 곳에서 이미 그 재화를 뜻한다(상단 바
         * 트레이의 보석·골드가 같은 스프라이트다). 같은 그림을 여기서도
         * 쓰면 읽는 것이 아니라 알아보는 것이 된다.
         *
         * ## 배치
         *
         * 오른쪽 끝에서 왼쪽으로 쌓는다: `[EXP][골드][보석][수량]`. 수량이
         * 오른쪽 끝인 이유는 자릿수가 자라는 쪽이 고정 모서리에 붙어야
         * 나머지가 안 흔들리기 때문이다(비용 칸과 같은 규칙).
         *
         * 골드·EXP에는 수량이 없다. 업적의 그 둘은 **받는 순간의 스테이지**로
         * 환산되므로(QuestSystem.GrantAchievementSpoils) 빌드 시점에 적을 수
         * 있는 수가 아니다 - 아이콘만으로 "이것도 준다"를 말한다.
         */
        private static TMP_Text BuildRewardGroup(Transform row, TMP_FontAsset font, QuestSpec spec)
        {
            var groupObject = new GameObject("Reward", typeof(RectTransform));
            groupObject.transform.SetParent(row, false);
            PlaceStretched((RectTransform)groupObject.transform, 24f, ClaimWidth + 24f, 58f, 50f);

            var count = CreateLabel(groupObject.transform, font, "Count", TextAlignmentOptions.Right);
            UiFonts.Demote(count);
            var countRect = (RectTransform)count.transform;
            countRect.anchorMin = countRect.anchorMax = new Vector2(1f, 0.5f);
            countRect.pivot = new Vector2(1f, 0.5f);
            countRect.sizeDelta = new Vector2(RewardCountWidth, 50f);
            countRect.anchoredPosition = Vector2.zero;
            count.color = DimColor;
            count.text = spec.Gems.ToString();

            float x = -(RewardCountWidth + 6f);
            AddRewardIcon(groupObject.transform, UiIcons.LoadItem(UiIcons.GemSprite), x);

            // 곁가지는 보석 왼쪽으로. 순서는 골드 -> EXP인데, 왼쪽으로 쌓으므로
            // 화면에서는 [EXP][골드][보석]이 된다 - 주 보상이 수량에 가장 가깝다
            if (spec.GoldMobs > 0d)
            {
                x -= RewardIconStride;
                AddRewardIcon(groupObject.transform, UiIcons.Load(UiIcons.GoldIcon), x);
            }
            if (spec.ExpBosses > 0d)
            {
                x -= RewardIconStride;
                AddRewardIcon(groupObject.transform, UiIcons.Load(UiIcons.ExpIcon), x);
            }

            return count;
        }

        private static void AddRewardIcon(Transform parent, Sprite sprite, float x)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(RewardIconSize, RewardIconSize);
            rect.anchoredPosition = new Vector2(x, 0f);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = UiIcons.Tint;
            image.raycastTarget = false;
            image.preserveAspect = true;
            // 스프라이트가 없으면 흰 사각형이 뜬다 - 없는 것보다 나쁘다
            image.enabled = sprite != null;
        }

        /** 보상 수량 칸. 두 자리("20")가 넉넉히 들어가는 폭 */
        private const float RewardCountWidth = 74f;
        private const float RewardIconSize = 36f;

        /** 아이콘 하나가 왼쪽으로 먹는 몫 */
        private const float RewardIconStride = RewardIconSize + 6f;

        /**
         * @brief 빨간 **점**. 숫자를 적지 않는다 (#4).
         *
         * 탭 버튼의 **자식**이라 페이지가 꺼져 있어도 보인다 - 배지의 존재
         * 이유가 그것이다(GrowthPanelTabs 주석).
         *
         * ## 왜 숫자를 버렸는가
         *
         * 배지가 답하는 질문은 **"열어볼 이유가 있는가"** 하나다. 그 답은
         * 예/아니오이고, 수량은 그 답을 더 정확하게 만들지 않는다 - 3개든
         * 100개든 해야 할 일은 "열어서 받는다"로 같다.
         *
         * 그런데 수량을 적으면 값을 치른다. 52px 판에 세 자리가 들어가면
         * 글자가 판을 넘고(실제로 100개에서 그랬다), 두 자리와 세 자리의
         * 폭이 달라 탭 줄이 미세하게 흔들린다. 답하지 않아도 될 질문에
         * 레이아웃을 내주는 셈이다.
         *
         * 점은 28px 정사각이라 자릿수와 무관하게 같은 자리를 쓴다. 정확한
         * 수량은 안에 들어가면 목록이 그대로 보여준다.
         */
        public static Image BuildBadge(Transform parent, TMP_FontAsset font)
        {
            /**
             * @brief 같은 이름을 **전부** 지운다. 하나가 아니라.
             *
             * `Find`는 첫 하나만 찾는다. 그래서 이 함수는 빌드마다 한 장을
             * 지우고 한 장을 새로 만들었는데, 어느 시점에 두 장 이상이
             * 생기면 그 뒤로는 영영 줄지 않는다 - 실기 세이브의 씬에서
             * 하단 탭 하나에 배지가 **여든 장 넘게** 쌓여 있었다.
             *
             * 겹쳐 있어서 화면에서는 한 장으로 보인다. 그래서 이 배치에서
             * 배지를 원으로 바꿨을 때 "원 11 / 사각 81"로 드러났다 - 새로
             * 만든 것만 원이고 쌓인 옛것들이 그 아래 그대로 있었다.
             */
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name == "Badge") Object.DestroyImmediate(child.gameObject);
            }

            var go = new GameObject("Badge", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(BadgeDotSize, BadgeDotSize);
            // 숫자 판이었을 때보다 작아졌으므로 모서리에 더 가까이 붙는다.
            // 예전 자리(6, 12)에 그대로 두면 점이 탭 안쪽으로 들어와 보인다
            rect.anchoredPosition = new Vector2(2f, 4f);

            var image = go.AddComponent<Image>();

            // **9-슬라이스 판이 아니라 원 스프라이트다** (#4). 판을 정사각으로
            // 눌러 쓰면 모서리가 둥근 사각형이 나오고, 28px에서도 그것은
            // 사각형으로 읽힌다 - 알림 점은 점이어야 한다
            image.sprite = UiGlyphBuilder.Load(UiGlyphBuilder.Dot);
            image.type = Image.Type.Simple;
            image.color = new Color32(0xC8, 0x32, 0x32, 0xFF);
            image.raycastTarget = false;

            // 글자 칸이 없다 (#4). 배지 컴포넌트들은 라벨 참조가 비어 있으면
            // 아무것도 안 적는다 - 넷 다 `label != null`을 먼저 본다
            go.SetActive(false);
            return image;
        }

        /**
         * @brief 알림 점의 지름 (#4).
         *
         * 28px = 탭 아이콘(64)의 절반보다 작다. 눈에 걸리되 읽는 것이
         * 아니어야 하는 크기다 - 더 키우면 아이콘과 다투고, 더 줄이면
         * 픽셀 격자에서 원이 아니라 얼룩으로 보인다.
         */
        public const float BadgeDotSize = 28f;

        // ---------------------------------------------------------------- 조각

        private static void PlaceStretched(RectTransform rect, float left, float right,
                                           float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static TMP_Text CreateLabel(Transform parent, TMP_FontAsset font, string name,
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
