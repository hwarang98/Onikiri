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

        private static readonly Color TextColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        private static readonly Color DimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

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

            var panel = EnsurePanel(safeArea);
            BuildHeader(panel, font);

            var pages = new GameObject[Kinds.Length];
            var content = EnsureScroll(panel);

            for (int i = 0; i < Kinds.Length; i++)
                pages[i] = BuildPage(content, system, font, Kinds[i]);

            BuildTabs(panel, font, pages);

            // 판은 꺼진 채로 저장된다. 하단 탭이 켠다 - 스킬 패널과 같은 규칙
            panel.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Quest panel built: 일일 {0} / 반복 {1} / 업적 {2} = {3}줄, "
                + "가장 긴 페이지 {4:F0}px (뷰포트 {5:F0}px).",
                QuestCatalog.DailyCount, QuestCatalog.RepeatCount, QuestCatalog.AchievementCount,
                QuestCatalog.TotalCount, TallestPageHeight, ViewportHeight));

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

        private static float BandHeight
        {
            get
            {
                return DisplayConfig.DesignHeight
                       * (DisplayConfig.GrowthPanelTop - DisplayConfig.BottomTabBarTop);
            }
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

            // 자기 바탕. 뒤의 성장 패널이 비쳐 보이면 두 목록이 겹친 것으로 읽힌다
            var backdrop = go.AddComponent<Image>();
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTextureBuilder.WashiPath);
            backdrop.type = Image.Type.Tiled;
            backdrop.color = UiSkin.PanelInk;
            backdrop.raycastTarget = true;

            return rect;
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

            var timer = CreateLabel(go.transform, font, "ResetTimer", TextAlignmentOptions.Right);
            var timerRect = (RectTransform)timer.transform;
            timerRect.anchorMin = new Vector2(0.35f, 0f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = new Vector2(-24f, 0f);
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
            var title = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)title.transform, 24f, ClaimWidth + 200f, 6f, 50f);
            title.text = spec.Title;

            var progress = CreateLabel(go.transform, font, "Progress", TextAlignmentOptions.Right);
            PlaceStretched((RectTransform)progress.transform, 24f, ClaimWidth + 24f, 6f, 50f);
            progress.color = DimColor;
            progress.text = "0 / " + spec.Target;

            // 아랫줄: 진행바(왼쪽) + 보상(오른쪽)
            var reward = CreateLabel(go.transform, font, "Reward", TextAlignmentOptions.Right);
            PlaceStretched((RectTransform)reward.transform, 24f, ClaimWidth + 24f, 58f, 50f);
            reward.color = DimColor;
            reward.text = "보석 " + spec.Gems;

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(go.transform, false);
            // 보상 문구가 차지할 만큼 비운다. 업적이 가장 길고, QuestRow가 그것을
            // 75% 크기로 적으므로 약 380px이다
            PlaceStretched((RectTransform)track.transform, 24f, ClaimWidth + 400f, 78f, 12f);
            var trackImage = track.AddComponent<Image>();
            trackImage.color = new Color32(0x2A, 0x25, 0x3C, 0xFF);
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

            var doneLabel = CreateLabel(doneBadge.transform, font, "Label", TextAlignmentOptions.Center);
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

                var rect = (RectTransform)go.transform;
                float slice = 1f / Kinds.Length;
                rect.anchorMin = new Vector2(i * slice, 0f);
                rect.anchorMax = new Vector2((i + 1) * slice, 1f);
                rect.offsetMin = new Vector2(6f, 0f);
                rect.offsetMax = new Vector2(-6f, 0f);

                var image = go.AddComponent<Image>();
                UiSkin.ApplyPanel(image, UiSkin.Chrome);

                var button = go.AddComponent<Button>();
                UiSkin.ApplyButton(button, image);

                var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
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

            so.FindProperty("selectedText").colorValue = TextColor;
            so.FindProperty("unselectedText").colorValue = DimColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 빨간 원형 배지. 성장 탭의 것과 같은 모양이다.
         *
         * 탭 버튼의 **자식**이라 페이지가 꺼져 있어도 보인다 - 배지의 존재
         * 이유가 그것이다(GrowthPanelTabs 주석).
         */
        public static Image BuildBadge(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Badge", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(52f, 52f);
            rect.anchoredPosition = new Vector2(6f, 12f);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, new Color32(0xC8, 0x32, 0x32, 0xFF));
            image.raycastTarget = false;

            var label = CreateLabel(go.transform, font, "Count", TextAlignmentOptions.Center);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -4f);
            labelRect.offsetMax = new Vector2(0f, 4f);
            label.text = "0";
            label.color = Color.white;

            go.SetActive(false);
            return image;
        }

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
