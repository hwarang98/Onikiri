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
     *
     * ## 44단계에 서브탭 셋이 됐다 - 장비 / 요도 / 도감
     *
     * 요도(妖刀)는 대장간에서 벼린다. 하단 탭을 하나 더 만들지 않은 이유가
     * 둘이다. 하나는 자리다 - 하단 탭은 다섯이고(38단계) 여섯째를 넣으면
     * 아이콘 폭이 무너진다. 다른 하나가 더 중요하다: **같은 건물에서 하는
     * 일이다.** 요도 탭을 따로 세우면 화면 두 곳이 "칼을 만드는 곳"이 되고,
     * 그중 어느 쪽이 진짜 대장간인지 알 수 없어진다.
     *
     * 서브탭이 생기면서 목록이 스크롤 안으로 들어갔다(퀘스트 판과 같은 구조).
     * 32단계에 "둘뿐이라 스크롤이 필요 없다"고 적었는데, 페이지가 셋이 되면
     * 가장 긴 페이지가 띠를 넘는다 - 그때 걸리라고 둔 검산
     * (VerifyPanelFits)이 실제로 걸렸다.
     */
    public static class EquipmentPanelBuilder
    {
        public const string PanelName = "EquipmentPanel";

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        private const float SidePadding = 48f;
        private const float TopPadding = 12f;

        private const float HeaderHeight = 60f;
        private const float HeaderGap = 10f;

        /** 서브탭 줄. 퀘스트 판과 같은 값이어야 두 화면이 같은 결로 읽힌다 */
        private const float TabHeight = 68f;
        private const float TabGap = 12f;

        private static readonly string[] TabNames = { "장비", "요도", "도감" };

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

        // 39단계 톤 통일: 자기 색을 갖지 않는다. 팔레트의 단일 출처는 UiSkin이다
        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        /**
         * @brief 등급업 버튼의 틴트. **보석 쪽 색이다.**
         *
         * 골드 버튼(단련)은 화면의 나머지와 같은 자주색 판을 쓰고 이쪽만 청으로
         * 민다. 두 재화가 섞이지 않게 하는 세 장치 중 하나이고(EquipmentRow
         * 주석), 색을 고른 근거는 상단 바의 보석 아이콘이 파란 다이아라는 것이다 -
         * 화면에 이미 있는 연결을 쓰는 것이 새 규칙을 하나 만드는 것보다 싸다.
         * 값은 UiSkin이 갖는다(39단계) - 동료 해금·전직 버튼과 같은 청이어야 한다.
         */
        private static readonly Color GemButtonTint = UiSkin.GemAction;

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

            var yodoSystem = YodoPanelBuilder.EnsureSystem(battle);

            // 영체 소환체(45단계). 화면이 아니라 전장에 서는 물건인데 여기서
            // 세우는 이유는 **요도의 소유자가 하나여야** 하기 때문이다 -
            // 상성·영체·도감이 전부 같은 티어를 읽으므로, 그 배선이 두 빌더로
            // 갈리면 한쪽만 돌린 씬이 반쯤 동작한다
            YodoPanelBuilder.EnsureSpiritSummon(battle);

            var panel = EnsurePanel(safeArea);
            BuildHeader(panel, font);

            var content = EnsureScroll(panel);

            var pages = new GameObject[TabNames.Length];
            pages[0] = BuildEquipmentPage(content, system, font);
            pages[1] = YodoPanelBuilder.BuildForgePage(content, yodoSystem, font);
            pages[2] = YodoPanelBuilder.BuildCodexPage(content, yodoSystem, font);

            BuildTabs(panel, font, pages);

            VerifyPanelFits();

            // 판은 꺼진 채로 저장된다. 하단 탭이 켠다 - 스킬·퀘스트와 같은 규칙
            panel.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Forge panel built: 장비 {0}슬롯 / 요도 {1}자루 / 도감 {2}줄. "
                + "가장 긴 페이지 {3:F0}px (뷰포트 {4:F0}px). "
                + "무기 상한 x{5:F2} / 방어구 상한 x{6:F2} (해금 st{7}), "
                + "요도 상한 x{8:F2} (해금 st{9}).",
                // 도감 줄 수는 요도 넷 + 오니키리 + 전설 둘이다(47단계).
                // 손으로 적으면 풀이 늘어나는 날 로그만 옛 수를 말한다
                EquipmentCatalog.Count, YodoCatalog.Count,
                YodoCatalog.Count + 1 + LegendaryYodoCatalog.Count,
                TallestPageHeight, ViewportHeight,
                EquipmentCatalog.Slots[0].Ceiling, EquipmentCatalog.Slots[1].Ceiling,
                EquipmentCurve.UnlockStage, YodoCurve.Ceiling, YodoCurve.UnlockStage));

            // 판을 새로 만들었으니 이 판을 가리키던 하단 탭을 다시 물린다.
            // 안 하면 장비 탭이 잠긴 채 남는다 - RelinkScreenTabs 주석 참고
            BattleContentBuilder.RelinkScreenTabs();

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

        /** 스크롤 안쪽의 높이. 셋 중 가장 긴 페이지가 여기에 담긴다 */
        private static float ViewportHeight
        {
            get { return BandHeight - (TopPadding + HeaderHeight + HeaderGap + TabHeight + TabGap); }
        }

        private static float EquipmentPageHeight
        {
            get { return EquipmentCatalog.Count * (CardHeight + CardGap); }
        }

        private static float TallestPageHeight
        {
            get
            {
                float tallest = EquipmentPageHeight;
                tallest = Mathf.Max(tallest, YodoPanelBuilder.ForgePageHeight);
                tallest = Mathf.Max(tallest, YodoPanelBuilder.CodexPageHeight);
                return tallest;
            }
        }

        /**
         * @brief 헤더·탭이 띠 안에 들어가는지 빌드가 검산한다.
         *
         * ## 32단계에는 "스크롤을 두지 않았다"였고, 그 판단이 44단계에 걸렸다
         *
         * 그때 근거는 "슬롯이 둘뿐이라 필요가 없고, 스크롤이 있으면 없는 것을
         * 찾게 만든다"였다. 옳았지만 **셋째 페이지가 생기는 순간 무너지는
         * 근거**였고, 그때 걸리라고 이 검산을 남겼다. 실제로 걸렸다 -
         * 요도 페이지가 뷰포트의 1.4배다.
         *
         * 이제 검산하는 것은 목록이 아니라 **머리다.** 목록은 스크롤이
         * 받으므로 넘칠 수 없고, 헤더+탭이 띠를 먹어 뷰포트가 한 행보다
         * 작아지는 것이 새 실패 모양이다.
         */
        private static void VerifyPanelFits()
        {
            if (ViewportHeight >= CardHeight) return;

            Debug.LogWarning(string.Format(
                "[Onikiri] Forge viewport is only {0:F0}px but one card is {1:F0}px - "
                + "the header and tabs ate the band ({2:F0}px). Shrink the header or the tabs.",
                ViewportHeight, CardHeight, BandHeight));
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

            // 화지 위의 벚가지 (39단계). 스킬·퀘스트 패널과 같은 헬퍼
            BackdropTextureBuilder.AddSakuraBranch(rect);

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

            // 잠긴 미리보기의 해금 조건 배너(41단계). 행마다 "대장간 미개방"이
            // 이미 서 있지만, 화면 전체가 왜 죽어 있는지는 헤더가 한 문장으로
            // 말한다. 조건은 탭과 같은 출처(EquipmentCurve)다
            LockBannerBuilder.Build(go.transform, font,
                                    EquipmentCurve.UnlockStage + "스테이지 도달 시 해금",
                                    1, EquipmentCurve.UnlockStage);
        }

        // ---------------------------------------------------------------- 스크롤·탭

        /**
         * @brief 스크롤. 세 페이지가 이 안에 겹쳐 선다.
         *
         * 퀘스트 판과 같은 구조다 - Viewport(RectMask2D) 안에 Content가 있고
         * 페이지들이 그 아래 겹쳐 있다. Content 높이는 **가장 긴 페이지**에
         * 맞춘다. 합으로 잡으면 어느 탭을 보든 그 아래로 빈 공간이 스크롤된다.
         *
         * 좌우 여백(SidePadding)을 뷰포트가 먹는다. 32단계에는 카드가 직접
         * 물고 있었는데, 스크롤이 생기면 마스크 경계가 카드 테두리를 자르므로
         * 여백이 마스크 밖에 있어야 한다.
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

        private static GameObject BuildEquipmentPage(RectTransform content, EquipmentSystem system,
                                                     TMP_FontAsset font)
        {
            var page = YodoPanelBuilder.CreatePage(content, "EquipmentPage", EquipmentPageHeight);

            for (int i = 0; i < EquipmentCatalog.Count; i++)
                BuildCard(page, system, font, i);

            return page.gameObject;
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

            var sources = new[]
            {
                Onikiri.UI.ForgePanelTabs.Source.Equipment,
                Onikiri.UI.ForgePanelTabs.Source.Yodo,

                // 도감은 배지가 없다. 누를 것이 없는 화면에 배지를 달면
                // 그 배지는 "가서 할 일이 있다"를 뜻하지 못한다
                Onikiri.UI.ForgePanelTabs.Source.None
            };

            var tabs = panel.gameObject.AddComponent<Onikiri.UI.ForgePanelTabs>();
            var so = new SerializedObject(tabs);
            var list = so.FindProperty("pages");
            list.arraySize = TabNames.Length;

            for (int i = 0; i < TabNames.Length; i++)
            {
                var go = new GameObject("Tab" + i, typeof(RectTransform));
                go.transform.SetParent(bar.transform, false);

                var rect = (RectTransform)go.transform;
                float slice = 1f / TabNames.Length;
                rect.anchorMin = new Vector2(i * slice, 0f);
                rect.anchorMax = new Vector2((i + 1) * slice, 1f);
                rect.offsetMin = new Vector2(6f, 0f);
                rect.offsetMax = new Vector2(-6f, 0f);

                var image = go.AddComponent<Image>();
                UiSkin.ApplyPanel(image, UiSkin.Chrome);

                var button = go.AddComponent<Button>();
                UiSkin.ApplyButton(button, image);

                // 서브탭 글자는 캡션 크기 - 퀘스트 서브탭과 같은 티어다
                var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
                UiFonts.Demote(label);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(0f, -6f);
                labelRect.offsetMax = new Vector2(0f, 6f);
                label.text = TabNames[i];

                var badge = QuestPanelBuilder.BuildBadge(go.transform, font);

                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("badgeSource").enumValueIndex = (int)sources[i];
                element.FindPropertyRelative("tab").objectReferenceValue = button;
                element.FindPropertyRelative("tabLabel").objectReferenceValue = label;
                element.FindPropertyRelative("tabBackground").objectReferenceValue = image;
                element.FindPropertyRelative("root").objectReferenceValue = pages[i];
                element.FindPropertyRelative("badge").objectReferenceValue = badge.gameObject;
                element.FindPropertyRelative("badgeLabel").objectReferenceValue =
                    badge.GetComponentInChildren<TMP_Text>(true);
            }

            // 탭마다 스크롤 길이를 맞춘다(ForgePanelTabs.FitScroll 주석). 페이지
            // 높이는 빌더가 이미 알고 있으므로 여기서 적어 준다 - 런타임이 자식
            // RectTransform을 재는 것보다 싸고, 레이아웃이 아직 안 돌았을 때도
            // 맞는 값이다
            so.FindProperty("scroll").objectReferenceValue = panel.GetComponent<ScrollRect>();
            var heights = so.FindProperty("pageHeights");
            heights.arraySize = TabNames.Length;
            heights.GetArrayElementAtIndex(0).floatValue = EquipmentPageHeight;
            heights.GetArrayElementAtIndex(1).floatValue = YodoPanelBuilder.ForgePageHeight;
            heights.GetArrayElementAtIndex(2).floatValue = YodoPanelBuilder.CodexPageHeight;

            so.FindProperty("selectedText").colorValue = TextColor;
            so.FindProperty("unselectedText").colorValue = DimColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 카드

        private static void BuildCard(RectTransform page, EquipmentSystem system,
                                      TMP_FontAsset font, int index)
        {
            var spec = EquipmentCatalog.Slots[index];

            var go = new GameObject("Slot" + index, typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, CardHeight);
            rect.anchoredPosition = new Vector2(0f, -index * (CardHeight + CardGap));

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            var icon = CreateIcon(go.transform, UiIcons.LoadItem(spec.IconSprite));

            // 카드 글자는 전부 캡션 크기(39단계 - 캐릭터 화면과 같은 위계).
            // 44pt로 남는 것은 두 동작 버튼의 제목과 머리글(제목·보석 잔액)뿐이다
            var gradeLabel = CreateLabel(go.transform, font, "Grade", TextAlignmentOptions.Left);
            UiFonts.Demote(gradeLabel);
            PlaceStretched((RectTransform)gradeLabel.transform, TextLeft, 340f, 12f, LineHeight);
            gradeLabel.text = spec.GradeName(1);

            var statLabel = CreateLabel(go.transform, font, "Stat", TextAlignmentOptions.Right);
            UiFonts.Demote(statLabel);
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
            UiFonts.Demote(slotLabel);
            PlaceStretched((RectTransform)slotLabel.transform, TextLeft, 300f,
                           12f + LineHeight, LineHeight);
            slotLabel.color = DimColor;
            slotLabel.text = spec.SlotName + " · 장착 중";

            var levelLabel = CreateLabel(go.transform, font, "Level", TextAlignmentOptions.Right);
            UiFonts.Demote(levelLabel);
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
            // 버튼 판의 평상시 틴트. 잠긴 미리보기(41b)가 판을 눌렀다가
            // 해금 때 이 값으로 되살린다 - 빌더가 칠한 값과 같아야 한다
            so.FindProperty("temperButtonTint").colorValue = UiSkin.Chrome;
            so.FindProperty("gradeButtonTint").colorValue = GemButtonTint;
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

            // 동작(단련·등급업)은 44pt로 말하고, 비용은 캡션으로 받친다(39단계).
            // 같은 크기로 두면 버튼 안에서 무엇이 동사인지 읽히지 않는다
            titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Center);
            PlaceStretched((RectTransform)titleLabel.transform, 8f, 8f, 8f, 52f);
            titleLabel.text = title;

            costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Center);
            UiFonts.Demote(costLabel);
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
