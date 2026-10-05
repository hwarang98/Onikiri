using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 상점 화면과 GachaSystem을 세운다 (46단계).
     *
     * ## 진입점은 하단 탭이다 - 여섯째 칸
     *
     * 사양이 "하단 탭 or HUD 버튼 - 판단해서"라고 열어 뒀고 하단 탭을 골랐다.
     * 31단계의 퀘스트가 같은 자리에서 같은 판단을 했고 이유도 대부분 같다
     * (QuestPanelBuilder 머리 주석):
     *
     *   1. **상단 바에는 자리가 없다.** 40단계에 초상 액자·Lv 배지·재화 트레이·
     *      스테이지 칩·설정 톱니가 들어가면서 폭 실측 검사까지 생겼다.
     *   2. **상점은 화면이지 상태가 아니다.** 상단 바는 지금 얼마인지를 말하고
     *      하단 탭은 어디로 갈지를 말한다.
     *   3. 그리고 이번에만 있는 이유: 사용자가 "상점이 어디에도 안 보인다"고
     *      지적했다. **보이는 것 자체가 이 스텝의 요구사항**이고, 하단 탭은
     *      st1부터 화면에 서 있는 유일한 자리다.
     *
     * ## 탭은 잠기지만 화면은 열린다
     *
     * 조건은 요도와 같은 st41이다(GachaCurve.UnlockStage - 팔 것이 전부 요도의
     * 재료다). 그런데 41단계 규칙대로 **진입은 허용하고 액션만 잠근다** -
     * 잠긴 채로 들어가면 확률표와 가격이 그대로 보이고 버튼만 죽어 있다.
     * "저기까지 가면 저것을 살 수 있다"가 이 화면이 st1~40에 하는 일이고,
     * 그것은 잠긴 도감이 44단계에 하던 일과 같다.
     *
     * ## 준비 중 두 줄
     *
     * 광고와 보석 팩은 자리와 가격만 있다. 다음 스텝(IAP·광고 SDK)이 그
     * 자리를 실기능으로 채운다 - 41단계의 "눌리는데 빈 화면이 나오는 것보다
     * 안 눌리는 편이 정직하다"를 상품에 적용한 것이다.
     */
    public static class ShopPanelBuilder
    {
        public const string PanelName = "ShopPanel";

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        private const float SidePadding = 48f;
        private const float TopPadding = 10f;
        private const float HeaderHeight = 56f;
        private const float HeaderGap = 8f;

        private const float RowGap = 12f;
        private const float LineHeight = 52f;

        /**
         * @brief 배너 행의 높이 (69단계). **강화 행 리듬(158)의 두 배다.**
         *
         * 46~68단계의 배너는 본문 다섯 줄(이름 · 설명 · 확률표 셋)에 버튼 줄이
         * 붙은 카드였고(468px), 그 아래에 무료 줄(140px)이 따로 붙었다. 69단계에
         * 그것을 레퍼런스의 행 하나로 접었다 - 큰 아이콘 · 제목 줄 · 버튼 둘 ·
         * 소환 레벨 바. 확률표는 "확률" 버튼 뒤(GachaRatePopup)로, 무료는 1회 /
         * 10회 버튼의 문구로 갔다.
         *
         * 316 x 2 + 간격 12 = 644px이 뷰포트 646px 안이다 - 두 배너가 **스크롤
         * 없이** 한 화면에 선다(VerifyRowsFit이 잰다). 행 안의 세로 배치:
         *
         *   16 여백 · 52 제목 · 12 · 116 버튼 · 18 · 48 바 · 46 여백 = 316
         */
        private const float BannerRowHeight = 316f;

        /** 배너 행의 큰 아이콘. 16px 아이콘의 10배 = 행 높이의 51% (224는 버튼을 밀어 너무 컸다 - 실기 피드백) */
        private const float BannerIconSize = 160f;

        private const float BannerTitleTop = 16f;
        private const float BannerButtonTop = 80f;
        private const float BannerBarTop = 214f;
        private const float BannerBarHeight = 48f;

        /** 아이콘 오른쪽 내용의 시작과 폭 */
        private const float BannerContentLeft = 24f + BannerIconSize + 20f;
        private const float BannerContentWidth = RowWidth - BannerContentLeft - 24f;

        /** 제목 줄 오른쪽의 작은 버튼 둘(확률 · 광고) */
        private const float ChipWidth = 104f;
        private const float ChipGap = 8f;


        /** 상품 한 줄. 요도의 파편 줄(130)과 같은 결 - 두 줄 + 오른쪽 버튼 */
        private const float RowHeight = 140f;

        private const float IconLeft = 24f;
        private static readonly float TextLeft = IconLeft + UiIcons.Size + 20f;

        private const float ButtonWidth = 320f;
        private const float ButtonRight = 24f;
        private const float ButtonTextPad = 8f;
        private const float ButtonTextWidth = ButtonWidth - ButtonTextPad * 2f;

        private static readonly float TextRight = ButtonRight + ButtonWidth + 16f;

        private const float RowWidth = DisplayConfig.DesignWidth - SidePadding * 2f;
        private static readonly float TextWidth = RowWidth - TextLeft - TextRight;

        /** 결과 판의 위아래 여백과 마지막 줄 ~ 확인 버튼 사이 */
        private const float PopupTop = 16f;
        private const float PopupGap = 28f;

        /**
         * @brief 확인 버튼의 높이. **한 줄짜리 동사 버튼이다** (50b).
         *
         * 처음에는 BuildSideButton(두 줄 - 동사 + 비용)을 빈 비용으로 세웠고,
         * 그 결과 "확인"이 위 칸에 몰리고 아래 칸이 비어 **판 안에서 붕 뜬
         * 버튼**이 됐다(실기 캡처). 비용이 없는 버튼은 애초에 두 줄 틀에
         * 들어갈 이유가 없다 - 동사 하나(44pt) + 위아래 여백이 이 버튼의
         * 전부이고, 그것이 2a 위계가 요구하는 크기다.
         */
        private const float ConfirmHeight = LineHeight + 32f;

        /**
         * @brief 배너 아래쪽 두 버튼의 높이. 좌우로 반씩 나눈다.
         *
         * **두 줄이 들어가야 한다**(동사 44pt + 비용 33pt, 각자 52px 상자).
         * 88로 뒀다가 실기에서 물렸다 - 내용 104px이 판 88px을 16px 넘쳐
         * 제목이 버튼 **위로 삐져나갔다**. 상자를 글자에 맞추지 그 반대가
         * 아니라는 것은 31단계 퀘스트 행이 같은 자리에서 배운 규칙이고
         * (44pt는 아틀라스를 구운 크기라 줄일 수 없다), 그때는 줄을 없애
         * 해결했지만 여기서는 없앨 줄이 없다 - 동사와 값 둘 다 필요하다.
         *
         * 요도 행의 버튼(BuildSideButton)이 같은 두 줄을 담는데 안 물린 이유는
         * 그쪽이 행 높이에서 여백을 빼 만들어져(140 - 18x2 = 104) 우연히
         * 정확히 맞았기 때문이다. 우연에 기대지 않게 VerifyRowsFit이 둘 다 잰다.
         */
        private const float BannerButtonHeight = LineHeight * 2f + 12f;
        private const float BannerButtonGap = 16f;

        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        /** 보석이 드는 버튼은 청이다 - 32단계부터의 규칙 (UiSkin.GemAction) */
        private static readonly Color GemButtonTint = UiSkin.GemAction;

        /** 무료는 초록이다. 화면에서 "지금 공짜로 받을 것"의 색 */
        private static readonly Color FreeButtonTint = UiSkin.Good;

        /**
         * @brief 전설이 나온 판의 틴트. **먹빛/금이고, 나무 판에 곱한다.**
         *
         * 값이 UiSkin.Gold가 아닌 이유는 이것이 글자색이 아니라 **판에 곱할
         * 틴트**이기 때문이다(UiSkin.Danger 주석의 규칙 - 한 채널을 1.0
         * 근처에 두지 않으면 어두워지기만 한다). 금색을 그대로 곱하면 판이
         * 노란 종이가 되어 그 위의 금색 글자가 안 읽힌다 - 반쯤 눌러 물들인
         * 나무로 두면 금색 글자가 그 위에 뜬다.
         */
        private static readonly Color LegendaryCardTint = new Color(0.62f, 0.50f, 0.22f, 1f);

        /**
         * @brief 준비 중 상품의 가격표. **다음 스텝이 이 표를 실상품으로 바꾼다.**
         *
         * 값을 지금 적는 이유는 화면이 비어 보이지 않게 하려는 것이 아니라,
         * **가격이 밸런스의 일부**이기 때문이다 - 보석 300이 뽑기 12회이고
         * 그것이 (47단계) 천장의 절반이라는 사실은 이 스텝에서 이미 정해져 있어야
         * 다음 스텝이 그 위에 결제만 얹을 수 있다.
         *
         * 원화 표기에 ₩를 쓰지 않는다. 폰트 아틀라스에 그 글리프가 없고
         * (FontCharset.txt), 통화 기호 하나를 위해 세 아틀라스를 다시 굽는
         * 것보다 "원"이 낫다 - 33단계부터의 규칙이다.
         */
        private struct PackSpec
        {
            public string Name;
            public string Price;
            public int Gems;
        }

        private static readonly PackSpec[] Packs =
        {
            new PackSpec { Name = "보석 300",   Price = "3,300원",  Gems = 300 },
            new PackSpec { Name = "보석 1,000", Price = "11,000원", Gems = 1000 },
            new PackSpec { Name = "보석 3,500", Price = "33,000원", Gems = 3500 }
        };

        /**
         * @brief 화면에 서는 순서와 그 높이. 배너 행 둘 + 광고 + 보석 팩들.
         *
         * 50단계에 자리 계산을 상수에서 커서로 바꿨고(높이가 다른 블록이 번갈아
         * 선다), 69단계에 무료 줄 둘이 배너 버튼으로 들어가 블록이 하나씩 줄었다.
         */
        private static float ContentHeight
        {
            get
            {
                // 배너 행 둘 + 광고 + 보석 팩들. 69단계에 무료 줄 둘이 버튼으로 들어갔다
                return 2f * (BannerRowHeight + RowGap)
                     + (1 + Packs.Length) * (RowHeight + RowGap);
            }
        }

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
            get { return BandHeight - (TopPadding + HeaderHeight + HeaderGap); }
        }

        [MenuItem("Onikiri/Build Shop Panel")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static GachaSystem Build()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogError("[Onikiri] Battle root missing - run Build Combat Content first.");
                return null;
            }

            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - run Build Main Scene first.");
                return null;
            }

            var system = EnsureSystem(battle);
            EnsureSkillSystem(battle);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            VerifyRowsFit();
            VerifyTextFits();

            var panel = EnsurePanel(safeArea);
            var header = BuildHeader(panel, font);
            var content = EnsureScroll(panel);

            var shop = panel.gameObject.AddComponent<Onikiri.UI.ShopPanel>();
            var so = new SerializedObject(shop);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("skillSystem").objectReferenceValue =
                battle.GetComponent<SkillGachaSystem>();
            so.FindProperty("skills").objectReferenceValue = FindSkills();
            so.FindProperty("yodo").objectReferenceValue = battle.GetComponent<YodoSystem>();
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("goldColor").colorValue = UiSkin.Gold;
            so.FindProperty("buttonTint").colorValue = GemButtonTint;
            so.FindProperty("freeTint").colorValue = FreeButtonTint;

            float top = 0f;
            BuildBannerRow(content, font, so.FindProperty("yodoBanner"), "GachaBanner",
                           UiIcons.LoadItem(LegendaryYodoSprites.WhiteMaskSprite),
                           Onikiri.UI.ShopPanel.YodoTitle, Onikiri.UI.ShopPanel.YodoSubtitle,
                           GachaCurve.PullCostGems, GachaCurve.TenPullCostGems, ref top);
            BuildBannerRow(content, font, so.FindProperty("skillBanner"), "SkillGachaBanner",
                           UiIcons.Load(BurstIconFile),
                           Onikiri.UI.ShopPanel.SkillTitle, Onikiri.UI.ShopPanel.SkillSubtitle,
                           SkillGachaCurve.PullCostGems, SkillGachaCurve.TenPullCostGems, ref top);
            BuildAdRow(content, font, ref top);
            foreach (var pack in Packs) BuildPackRow(content, font, ref top, pack);

            so.FindProperty("popup").objectReferenceValue = BuildPopup(safeArea, font);
            so.FindProperty("ratePopup").objectReferenceValue = BuildRatePopup(safeArea, font);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 잠긴 동안의 안내. 탭은 눌리고 화면은 열리되 버튼만 죽는다
            // (41단계 규칙). 문구 형식은 LockedTab.Requirement와 같은 말이어야
            // 한다 - 탭과 배너가 다른 문장으로 같은 조건을 말하면 두 규칙이 된다
            LockBannerBuilder.Build(header, font,
                                    GachaCurve.UnlockStage + "스테이지 도달 시 해금",
                                    1, GachaCurve.UnlockStage);

            panel.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Shop panel built: 배너 행 2 + 광고 + 보석 팩 {0} = {1:F0}px "
                + "(뷰포트 {2:F0}px). 단연 {3} · 10연 {4} · 소환 Lv.1->2 {5}회 · "
                + "오의 상한까지 XP {6} (Lv.1 기대 {7:F1}회)",
                Packs.Length, ContentHeight, ViewportHeight,
                GachaCurve.PullCostGems, GachaCurve.TenPullCostGems, SummonLevelCurve.XpToNext(1),
                SkillGachaCurve.TotalXpToCap,
                SkillGachaCurve.TotalXpToCap / SkillGachaCurve.ExpectedXpPerPull(1)));

            BattleContentBuilder.RelinkScreenTabs();
            return system;
        }

        // ---------------------------------------------------------------- 시스템

        private static GachaSystem EnsureSystem(GameObject battle)
        {
            var system = battle.GetComponent<GachaSystem>();
            if (system == null) system = battle.AddComponent<GachaSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("yodo").objectReferenceValue = battle.GetComponent<YodoSystem>();
            so.FindProperty("stage").objectReferenceValue = battle.GetComponent<StageProgress>();

            // 진행(소환 경험치·누적·무료 쿨)은 덮어쓰지 않는다. 빌더를 한 번
            // 돌릴 때마다 경험치가 0으로 돌아가면 플레이어가 지불한 뽑기가
            // 사라진다 - YodoPanelBuilder.EnsureSystem과 같은 규칙이다
            so.ApplyModifiedPropertiesWithoutUndo();

            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null)
            {
                var sessionSo = new SerializedObject(session);
                sessionSo.FindProperty("gacha").objectReferenceValue = system;
                sessionSo.ApplyModifiedPropertiesWithoutUndo();
            }

            return system;
        }

        /**
         * @brief 오의 뽑기 시스템 (50단계). **진행은 덮어쓰지 않는다.**
         *
         * EnsureSystem과 같은 규칙이다 - 빌더를 한 번 돌릴 때마다 소환
         * 경험치가 0으로 돌아가면 플레이어가 지불한 뽑기가 사라진다.
         *
         * `skills`를 배선하는 것이 이 함수가 하는 일의 전부에 가깝다. 결과를
         * 적용하는 곳이 그쪽이고(SkillGachaSystem 머리 주석), 참조가 비면
         * 뽑기가 돌기는 하는데 아무것도 안 들어오는 상태가 된다 - 49단계가
         * StageProgress 미배선으로 실기에서 물린 것과 같은 종류의 사고다.
         */
        private static SkillGachaSystem EnsureSkillSystem(GameObject battle)
        {
            var system = battle.GetComponent<SkillGachaSystem>();
            if (system == null) system = battle.AddComponent<SkillGachaSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("skills").objectReferenceValue = FindSkills();
            so.FindProperty("stage").objectReferenceValue = battle.GetComponent<StageProgress>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null)
            {
                var sessionSo = new SerializedObject(session);
                sessionSo.FindProperty("skillGacha").objectReferenceValue = system;
                sessionSo.ApplyModifiedPropertiesWithoutUndo();
            }

            return system;
        }

        /**
         * @brief SkillSystem을 찾는다. **Battle 루트에 없다.**
         *
         * 오의는 사무라이에 붙어 있다(Battle/GroundAnchor/Player/Samurai) -
         * 시전이 캐릭터의 일이기 때문이다. 나머지 시스템처럼
         * `battle.GetComponent`로 찾으면 조용히 null이 들어오고, 그러면
         * 런타임 폴백(SkillSystem.Instance)이 가려서 **실기에서만 늦게**
         * 드러난다. 49단계가 StageProgress 미배선으로 물린 자리와 같은
         * 종류의 사고라, 여기서는 찾는 방법을 함수 하나로 못박는다.
         */
        private static SkillSystem FindSkills()
        {
            return Object.FindFirstObjectByType<SkillSystem>(FindObjectsInactive.Include);
        }

        // ---------------------------------------------------------------- 판

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
            backdrop.raycastTarget = true;

            BackdropTextureBuilder.AddSakuraBranch(rect);
            return rect;
        }

        private static Transform BuildHeader(RectTransform panel, TMP_FontAsset font)
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
            // 69단계: 탭 줄은 **소환** 하나다(서브탭 없음). 레퍼런스의 탭 자리에 같은
            // 말을 적고, 광고·보석 팩은 그 아래 스크롤에 남는다
            title.text = "소환";
            title.color = UiSkin.Gold;

            // 보석 잔액을 머리글 오른쪽에 적는다. 상단 바에 이미 있지만
            // **여기서는 값이 아니라 예산**이다 - 뽑기 값과 같은 화면에
            // 있어야 "한 번 더 돌릴 수 있는가"가 눈으로 답해진다
            var balance = CreateLabel(go.transform, font, "Balance", TextAlignmentOptions.Right);
            UiFonts.Demote(balance);
            var balanceRect = (RectTransform)balance.transform;
            balanceRect.anchorMin = new Vector2(0.4f, 0f);
            balanceRect.anchorMax = new Vector2(1f, 1f);
            balanceRect.offsetMin = Vector2.zero;
            balanceRect.offsetMax = new Vector2(-24f, 0f);
            balance.color = DimColor;
            balance.text = "보석 0";

            var hud = go.AddComponent<Onikiri.UI.HUDGems>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("label").objectReferenceValue = balance;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            return go.transform;
        }

        private static RectTransform EnsureScroll(RectTransform panel)
        {
            var go = new GameObject("Viewport", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var viewport = (RectTransform)go.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(SidePadding, 0f);
            viewport.offsetMax = new Vector2(-SidePadding,
                                             -(TopPadding + HeaderHeight + HeaderGap));

            go.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);

            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, ContentHeight);
            content.anchoredPosition = Vector2.zero;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            return content;
        }

        // ---------------------------------------------------------------- 배너

        /**
         * @brief 배너 행 하나 (69단계). 요도·오의가 **같은 함수**를 지난다.
         *
         *   [큰 아이콘]  제목 (부제)                 [확률] [광고]
         *               [ 1회 소환 · 보석 25 ] [ 10회 소환 · 보석 225 ]
         *               소환 Lv.n  [========== xp / next ]
         *
         * 46~68단계에 두 배너가 같은 조각(BuildRateCell · BuildBannerButton)을 쓴
         * 이유가 여기서도 그대로다 - 눈금이 하나여야 사다리가 하나로 읽힌다.
         * 좌상단 배지 자리는 비운다(지시서).
         */
        private static void BuildBannerRow(RectTransform content, TMP_FontAsset font, SerializedProperty view,
                                           string name, Sprite icon, string title, string subtitle,
                                           int singleCost, int tenCost, ref float cursor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);
            Place(go, cursor, BannerRowHeight);
            cursor += BannerRowHeight + RowGap;

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Chrome);

            // ---- 큰 아이콘. 행 높이의 71%, 세로 가운데
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            if (icon == null) iconImage.color = new Color(1f, 0f, 1f, 0.35f);
            PlaceTopLeft((RectTransform)iconGo.transform, 24f, (BannerRowHeight - BannerIconSize) * 0.5f,
                         BannerIconSize, BannerIconSize);

            // ---- 제목 줄. 이름은 44(2a 위계의 머리), 부제는 33
            var titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Left);
            PlaceTopLeft((RectTransform)titleLabel.transform, BannerContentLeft, BannerTitleTop, 200f, LineHeight);
            titleLabel.text = title;

            var subLabel = CreateLabel(go.transform, font, "Subtitle", TextAlignmentOptions.Left);
            UiFonts.Demote(subLabel);
            PlaceTopLeft((RectTransform)subLabel.transform, BannerContentLeft + 200f, BannerTitleTop,
                         BannerContentWidth - 200f - 2f * (ChipWidth + ChipGap), LineHeight);
            subLabel.text = subtitle;
            subLabel.color = DimColor;

            // ---- 확률 · 광고. 광고는 SDK가 없으므로 회색 placeholder - 호출 코드 없음
            float chipLeft = RowWidth - 24f - ChipWidth;
            var adImage = BuildChip(go.transform, font, "Ad", Onikiri.UI.ShopPanel.AdButtonText,
                                    chipLeft, UiSkin.RowDisabled);
            var ad = adImage.GetComponent<Button>();
            ad.interactable = false;
            var adTint = UiSkin.RowDisabled;
            adTint.r *= 0.55f; adTint.g *= 0.55f; adTint.b *= 0.55f; adTint.a = 1f;
            adImage.color = adTint;
            adImage.GetComponentInChildren<TMP_Text>().color = DimColor;

            var rateImage = BuildChip(go.transform, font, "Rates", Onikiri.UI.ShopPanel.RateButtonText,
                                      chipLeft - ChipGap - ChipWidth, UiSkin.InkChip);

            // ---- 버튼 둘. 같은 폭, 가로 나란히
            float buttonWidth = (BannerContentWidth - BannerButtonGap) * 0.5f;
            BuildPullButton(go.transform, font, view, "single", "Single", BannerContentLeft,
                            buttonWidth, Onikiri.UI.ShopPanel.SingleTitle, singleCost);
            BuildPullButton(go.transform, font, view, "ten", "Ten",
                            BannerContentLeft + buttonWidth + BannerButtonGap,
                            buttonWidth, Onikiri.UI.ShopPanel.TenTitle, tenCost);

            // ---- 소환 레벨 바
            var barRoot = new GameObject("SummonBar", typeof(RectTransform));
            barRoot.transform.SetParent(go.transform, false);
            PlaceTopLeft((RectTransform)barRoot.transform, BannerContentLeft, BannerBarTop,
                         BannerContentWidth, BannerBarHeight);

            var level = CreateLabel(barRoot.transform, font, SummonLevelName, TextAlignmentOptions.Left);
            UiFonts.Demote(level);
            PlaceTopLeft((RectTransform)level.transform, 0f, 0f, SummonLabelWidth, BannerBarHeight);
            level.text = Onikiri.UI.ShopPanel.LevelLabel(1);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(barRoot.transform, false);
            var trackImage = track.AddComponent<Image>();
            trackImage.sprite = null;                    // 민짜 - 그라데이션 금지(38b)
            trackImage.color = UiSkin.BarTrack;
            trackImage.raycastTarget = false;
            PlaceTopLeft((RectTransform)track.transform, SummonLabelWidth,
                         (BannerBarHeight - BarTrackHeight) * 0.5f,
                         BannerContentWidth - SummonLabelWidth, BarTrackHeight);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.sprite = null;
            fillImage.color = SummonBarTint;
            fillImage.raycastTarget = false;
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);    // 폭은 anchorMax.x로 구동한다
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var barText = CreateLabel(track.transform, font, "Text", TextAlignmentOptions.Center);
            UiFonts.Demote(barText);
            var barTextRect = (RectTransform)barText.transform;
            barTextRect.anchorMin = Vector2.zero;
            barTextRect.anchorMax = Vector2.one;
            barTextRect.offsetMin = new Vector2(0f, -10f);
            barTextRect.offsetMax = new Vector2(0f, 10f);
            barText.text = Onikiri.UI.ShopPanel.BarText(0, SummonLevelCurve.XpToNext(1));

            var state = CreateLabel(go.transform, font, "State", TextAlignmentOptions.Left);
            UiFonts.Demote(state);
            PlaceTopLeft((RectTransform)state.transform, BannerContentLeft, BannerBarTop,
                         BannerContentWidth, BannerBarHeight);
            state.color = DimColor;
            state.text = string.Empty;
            state.gameObject.SetActive(false);

            view.FindPropertyRelative("rateButton").objectReferenceValue = rateImage.GetComponent<Button>();
            view.FindPropertyRelative("barRoot").objectReferenceValue = barRoot;
            view.FindPropertyRelative("levelLabel").objectReferenceValue = level;
            view.FindPropertyRelative("barFill").objectReferenceValue = fillRect;
            view.FindPropertyRelative("barFillImage").objectReferenceValue = fillImage;
            view.FindPropertyRelative("barText").objectReferenceValue = barText;
            view.FindPropertyRelative("stateLabel").objectReferenceValue = state;
        }

        /** 바 왼쪽 "소환 Lv.n"의 칸. "소환 Lv.12"가 33pt에서 166px + 여유 */
        private const float SummonLabelWidth = 200f;

        private const float BarTrackHeight = 30f;

        /** 소환 레벨 바의 채움 색. 금빛 주황 - 경험치 줄(옥색)과 다른 축이라는 표시 */
        private static readonly Color SummonBarTint = new Color32(0xFF, 0xB0, 0x3B, 0xFF);

        private static void PlaceTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        /** 제목 줄 오른쪽의 작은 버튼(확률 · 광고). 글자 하나짜리 칩 */
        private static Image BuildChip(Transform parent, TMP_FontAsset font, string name, string text,
                                       float left, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            PlaceTopLeft((RectTransform)go.transform, left, BannerTitleTop, ChipWidth, LineHeight);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, tint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Center);
            UiFonts.Demote(label);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, -6f);
            rect.offsetMax = new Vector2(0f, 6f);
            label.text = text;
            return image;
        }

        /**
         * @brief 뽑기 버튼 하나. 위 줄 동사(33) · 아래 줄 [보석 아이콘 + 숫자] 또는 무료 문구.
         *
         * 동사는 캡션(33), 숫자는 제목과 같은 Galmuri 44다(실기 피드백 - 지시서 D의
         * ThaleahFat은 32는 작고 64는 두꺼웠다). 무료일 때는 아이콘·숫자를 끄고 같은 줄에 캡션 한 줄
         * ("오늘 한 번" / "무료")을 켠다(ShopPanel.DrawFree).
         */
        private static void BuildPullButton(Transform parent, TMP_FontAsset font, SerializedProperty view,
                                            string prefix, string name, float left, float width,
                                            string title, int cost)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            PlaceTopLeft((RectTransform)go.transform, left, BannerButtonTop, width, BannerButtonHeight);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, GemButtonTint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Center);
            UiFonts.Demote(titleLabel);                 // 동사는 캡션 - 값(보석 숫자)이 버튼의 주인이다
            PlaceCentered((RectTransform)titleLabel.transform, ButtonTextPad, LineHeight * 0.5f, LineHeight);
            titleLabel.text = title;

            var gem = new GameObject("Gem", typeof(RectTransform));
            gem.transform.SetParent(go.transform, false);
            var gemImage = gem.AddComponent<Image>();
            gemImage.sprite = UiIcons.LoadItem(UiIcons.GemSprite);
            gemImage.preserveAspect = true;
            gemImage.raycastTarget = false;
            var gemRect = (RectTransform)gem.transform;
            gemRect.anchorMin = gemRect.anchorMax = new Vector2(0.5f, 0.5f);
            gemRect.pivot = new Vector2(1f, 0.5f);
            gemRect.sizeDelta = new Vector2(GemIconSize, GemIconSize);
            gemRect.anchoredPosition = new Vector2(-8f, -LineHeight * 0.5f);

            var costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Left);
            costLabel.fontSize = PullCostFontSize;
            var costRect = (RectTransform)costLabel.transform;
            costRect.anchorMin = costRect.anchorMax = new Vector2(0.5f, 0.5f);
            costRect.pivot = new Vector2(0f, 0.5f);
            costRect.sizeDelta = new Vector2(width * 0.5f - ButtonTextPad, PullCostFontSize);
            costRect.anchoredPosition = new Vector2(4f, -LineHeight * 0.5f);
            costLabel.text = cost.ToString();

            var note = CreateLabel(go.transform, font, "Note", TextAlignmentOptions.Center);
            UiFonts.Demote(note);
            PlaceCentered((RectTransform)note.transform, ButtonTextPad, -LineHeight * 0.5f, LineHeight);
            note.color = UiSkin.Gold;
            note.text = string.Empty;
            note.gameObject.SetActive(false);

            view.FindPropertyRelative(prefix + "Button").objectReferenceValue = button;
            view.FindPropertyRelative(prefix + "Background").objectReferenceValue = image;
            view.FindPropertyRelative(prefix + "Title").objectReferenceValue = titleLabel;
            view.FindPropertyRelative(prefix + "Cost").objectReferenceValue = costLabel;
            view.FindPropertyRelative(prefix + "Note").objectReferenceValue = note;
            view.FindPropertyRelative(prefix + "Gem").objectReferenceValue = gemImage;
        }

        private const float GemIconSize = 52f;

        /**
         * 보석 숫자. 제목("요도 뽑기")과 같은 Galmuri 44 - 같은 굵기다(실기 피드백).
         * Thaleah 32는 점처럼 작았고, 64는 제목보다 두꺼워 버튼에서 혼자 튀었다.
         */
        private const float PullCostFontSize = Onikiri.UI.PixelFontSizes.GalmuriSmall;

        /**
         * @brief 확률표 한 칸. **라벨 둘이다 - 이름 왼쪽, 확률 오른쪽.**
         *
         * 47b 실기 캡처에서 "파편 6 74%"가 "파편 674%"로 읽힌 뒤 쪼갰다. 69단계에
         * 표가 배너에서 확률 팝업으로 옮겨 가며 칸이 넓어졌다(cellWidth를 부르는
         * 쪽이 준다) - 68단계까지 남아 있던 폭 경고("희귀 스킬 XP 240" 270px / 칸
         * 254px)가 그 넓이로 풀린다.
         */
        private static TMP_Text BuildRateCell(Transform parent, TMP_FontAsset font, int outcome,
                                              float left, float top, float cellWidth, string label)
        {
            var tint = UiSkin.Grades[(int)GachaCurve.GradeOf[outcome]];

            var name = CreateLabel(parent, font, "Rate" + outcome, TextAlignmentOptions.Left);
            UiFonts.Demote(name);
            PlaceTopLeft((RectTransform)name.transform, left, top, cellWidth - RateNumberWidth - RateGap, LineHeight);
            name.color = tint;
            name.text = label;

            var chance = CreateLabel(parent, font, "Rate" + outcome + "Pct", TextAlignmentOptions.Right);
            UiFonts.Demote(chance);
            PlaceTopLeft((RectTransform)chance.transform, left + cellWidth - RateNumberWidth, top,
                         RateNumberWidth, LineHeight);
            chance.color = tint;
            chance.text = Onikiri.UI.GachaRatePopup.PercentText(GachaCurve.Chances[outcome]);   // 열 때 지금 레벨 값으로 바뀐다
            return chance;
        }

        /**
         * @brief 배너 한 줄 설명. **46단계의 문장을 사다리로 바꾼다.**
         *
         * 46단계는 "파편이 나온다 · 낮은 확률로 혼 정수"였다. 그 문장은
         * 상품이 둘일 때 맞는 말이었고, 지금은 다섯이라 아래 확률표와
         * 어긋난다 - 설명이 표보다 좁으면 표가 설명을 반박한다.
         */
        private const string BannerDesc = "파편부터 전설 요도까지 · 다섯 등급";

        /** 확률 열의 폭. "71.6%"가 33pt에서 차지하는 자리 + 여유 */
        private const float RateNumberWidth = 132f;

        /** 칸과 칸 사이. 두 열이 붙어 한 줄로 읽히지 않을 만큼만 */
        private const float RateGap = 24f;

        /**
         * @brief 결과의 이름. **등급 이름을 앞에 붙인다.**
         *
         * 색만으로 등급을 말하지 않는 이유는 UiSkin.Grades 주석에 있다.
         * 별(GachaCurve.StarsFor)은 결과 판이 쓰고 여기서는 이름을 쓴다 -
         * 확률표는 여섯 줄이라 별 다섯 개가 여섯 번 서면 표가 별밭이 되고,
         * 정작 읽어야 하는 수량과 확률이 묻힌다.
         */
        private static string RewardName(int outcome)
        {
            string grade = GachaCurve.GradeNames[(int)GachaCurve.GradeOf[outcome]];

            switch ((GachaCurve.Outcome)outcome)
            {
                case GachaCurve.Outcome.SoulEssence:    return grade + " 혼 정수";
                case GachaCurve.Outcome.SoulRarity:     return grade + " 상위 혼";
                case GachaCurve.Outcome.LegendaryBlade: return grade + " 요도";
                default: return grade + " 파편 " + GachaCurve.ShardsOf[outcome];
            }
        }

        /**
         * @brief 확률 한 조각. **소수 한 자리로 고정한다.**
         *
         * 46단계는 "0.#"이었다 - 3.0%가 "3%"로, 5.0%가 "5%"로 줄어든다.
         * 한 줄에 이어 붙일 때는 짧은 편이 나았는데, 열로 세우니 소수점의
         * 자리가 줄마다 달라져 **숫자가 세로로 안 맞는다.** 열 정렬이
         * 하려던 일이 그 한 글자에서 무너진다.
         */
        private static string PercentText(int outcome)
        {
            return Onikiri.UI.GachaRatePopup.PercentText(GachaCurve.Chances[outcome]);
        }

        /**
         * @brief 배너 아래 진행 줄의 오브젝트 이름 (68단계).
         *
         * 47단계의 천장 줄이 서던 자리다. 문구의 출처는 런타임이다
         * (SummonLevelCurve.LevelText 주석) - 여기서는 이름만 정한다.
         */
        public const string SummonLevelName = "SummonLevel";


        // ------------------------------------------------------- 오의 배너 (50단계)



        private const string SkillBannerTitle = "오의 뽑기";

        /**
         * @brief 오의 배너 한 줄 설명.
         *
         * 요도 배너의 문장("파편부터 전설 요도까지 · 다섯 등급")과 **같은
         * 문법**이다. 두 배너가 같은 사다리를 쓰므로 설명도 같은 모양이어야
         * 사다리가 하나로 읽힌다 - 갈리는 것은 양 끝의 이름뿐이다.
         */
        private const string SkillBannerDesc = "스킬 XP부터 오의 개안까지 · 다섯 등급";


        /** 배너 아이콘. 이 뽑기가 파는 첫 오의(혈폭)의 아이콘이다 */
        private static string BurstIconFile
        {
            get
            {
                int index = SkillCatalog.IndexOf(SkillCatalog.BloodBurstId);
                return index >= 0 ? SkillCatalog.Skills[index].IconFile : UiIcons.GoldIcon;
            }
        }

        /**
         * @brief 오의 뽑기 결과의 이름. **등급 이름을 앞에 붙인다** - 요도 표와 같은 규칙.
         *
         * 결과 판이 쓰는 이름과 같은 말이어야 한다(GachaResultPopup.
         * NameOfOutcome) - 표에서 "영웅 오의 해금"으로 읽은 것이 판에서 다른
         * 이름으로 뜨면 그 둘이 같은 것인지 알 수 없다.
         */
        private static string SkillRewardName(int outcome)
        {
            string grade = GachaCurve.GradeNames[(int)GachaCurve.GradeOf[outcome]];
            return grade + " " + Onikiri.UI.GachaResultPopup.NameOfOutcome(
                (SkillGachaCurve.Outcome)outcome);
        }

        // ---------------------------------------------------------------- 상품 줄

        private static RectTransform BuildRow(RectTransform content, string name, ref float cursor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);

            Place(go, cursor, RowHeight);
            cursor += RowHeight + RowGap;

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            return (RectTransform)go.transform;
        }


        /**
         * @brief 광고 뽑기 자리. **비활성이고 그렇게 보인다.**
         *
         * 광고 SDK는 다음 스텝이다. 그때 이 줄의 버튼이 살아나고 나머지는
         * 그대로다 - 자리와 문구를 지금 정해 두면 그 스텝이 배선만 하면 된다.
         */
        private static void BuildAdRow(RectTransform content, TMP_FontAsset font, ref float cursor)
        {
            var row = BuildRow(content, "AdPull", ref cursor);
            // 버튼 제목이 "광고"인 이유: 보석 팩 줄은 그 자리에 **가격**을 적고
            // (그것이 그 줄의 값이다) 광고 줄에는 가격이 없다. 둘 다 "준비 중"을
            // 적으면 한 판에 같은 말이 두 번 뜬다 - 실기 캡처에서 실제로 그랬다
            BuildPlaceholder(row, font, UiGlyphBuilder.Load(UiGlyphBuilder.Gear),
                             "광고 보고 한 번", "광고를 보면 뽑기 한 번", "광고");
        }

        private static void BuildPackRow(RectTransform content, TMP_FontAsset font,
                                         ref float cursor, PackSpec spec)
        {
            var row = BuildRow(content, "Pack" + spec.Gems, ref cursor);
            BuildPlaceholder(row, font, UiIcons.LoadItem(UiIcons.GemSprite),
                             spec.Name, "뽑기 " + (spec.Gems / GachaCurve.PullCostGems) + "회 분량",
                             spec.Price);
        }

        /**
         * @brief 준비 중 줄 하나. 판과 버튼을 눌러 죽인 채로 세운다.
         *
         * 색만 죽이지 않고 **버튼도 못 누르게** 한다. 41b가 동료 카드에서
         * 확정한 규칙이다 - SpriteSwap 버튼은 interactable=false로 두어도
         * 판이 안 죽으므로 판을 직접 눌러야 한다.
         */
        private static void BuildPlaceholder(RectTransform row, TMP_FontAsset font, Sprite icon,
                                             string name, string note, string price)
        {
            CreateIcon(row, icon, UiIcons.AmplifierTint, 20f);

            var title = CreateLabel(row, font, "Title", TextAlignmentOptions.Left);
            UiFonts.Demote(title);
            Place((RectTransform)title.transform, TextLeft, TextRight, 20f, LineHeight);
            title.text = name;
            title.color = DimColor;

            var state = CreateLabel(row, font, "State", TextAlignmentOptions.Left);
            UiFonts.Demote(state);
            Place((RectTransform)state.transform, TextLeft, TextRight, 20f + LineHeight, LineHeight);
            state.color = DimColor;
            state.text = note;

            TMP_Text buyTitle, buyCost;
            Image buyImage;
            var button = BuildSideButton(row, font, "Buy", UiSkin.RowDisabled,
                                         price, "준비 중", out buyTitle, out buyCost, out buyImage);

            button.interactable = false;

            // 가격은 **비용**이라 캡션이다(2a 위계). 준비 중 줄의 버튼에는
            // 동사가 없으므로 이 판에서 44로 남을 것이 하나도 없다
            UiFonts.Demote(buyTitle);
            buyTitle.color = DimColor;

            var tint = UiSkin.RowDisabled;
            tint.r *= 0.55f; tint.g *= 0.55f; tint.b *= 0.55f; tint.a = 1f;
            buyImage.color = tint;
        }

        // ---------------------------------------------------------------- 결과 팝업

        /**
         * @brief 뽑기 결과 판 (69단계). **화면 전체 - 상단 바만 남긴다.**
         *
         * SafeArea의 0 ~ BattleAreaTop을 덮는다. 상단 바(보석 잔액)가 보여야
         * 판 위의 재뽑기 버튼이 "한 번 더 돌릴 수 있는가"를 같은 화면에서 답한다.
         * 층은 팝업(20) - 경험치 줄(10)이 그 위에 그려지면 딤이 뚫린 것으로 읽힌다.
         *
         *   제목 줄 (합계 · ★3 이상 · 공개 뒤 "소환 Lv.n 달성")
         *   타일 5열 x 2줄 (160px, 간격 32)
         *   노트 줄 셋 ("등급 하락 · …")
         *   [확인]                       [10회 소환 · 보석 225]
         */
        private static Onikiri.UI.GachaResultPopup BuildPopup(Transform safeArea, TMP_FontAsset font)
        {
            var existing = safeArea.Find(ResultPopupName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(ResultPopupName, typeof(RectTransform));
            root.transform.SetParent(safeArea, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = new Vector2(1f, DisplayConfig.BattleAreaTop);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(root.transform, false);
            var visualRect = (RectTransform)visual.transform;
            visualRect.anchorMin = Vector2.zero;
            visualRect.anchorMax = Vector2.one;
            visualRect.offsetMin = Vector2.zero;
            visualRect.offsetMax = Vector2.zero;

            var overlay = visual.AddComponent<Image>();
            overlay.sprite = null;
            overlay.color = ResultOverlayColor;
            overlay.raycastTarget = true;               // 판 뒤의 상점이 눌리지 않게

            // ---- 그리드. 가운데보다 조금 위 - 아래에 노트와 버튼이 선다
            var gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(visual.transform, false);
            var grid = (RectTransform)gridGo.transform;
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.pivot = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(ResultColumns * ResultTileSize + (ResultColumns - 1) * ResultTileGap,
                                         Onikiri.UI.GachaResultPopup.GridHeight(GachaCurve.TenPullCount,
                                             ResultColumns, ResultTileSize, ResultTileGap));
            grid.anchoredPosition = new Vector2(0f, ResultGridCenterY);

            float gridHalf = grid.sizeDelta.y * 0.5f;

            var title = CreateLabel(visual.transform, font, "Title", TextAlignmentOptions.Center);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0f);
            titleRect.sizeDelta = new Vector2(DisplayConfig.DesignWidth - ResultSidePad * 2f, LineHeight);
            titleRect.anchoredPosition = new Vector2(0f, ResultGridCenterY + gridHalf + 36f);
            title.text = "파편 0";

            var glow = GachaGlowBaker.Ensure();
            var tiles = new GameObject[GachaCurve.TenPullCount];
            var parts = new System.Collections.Generic.List<object[]>();
            for (int i = 0; i < tiles.Length; i++)
                parts.Add(BuildTile(grid, font, i, glow));

            // ---- 노트 줄. 그리드 바로 아래
            var notes = new TMP_Text[ResultNoteLines];
            for (int i = 0; i < notes.Length; i++)
            {
                var note = CreateLabel(visual.transform, font, "Note" + i, TextAlignmentOptions.Center);
                UiFonts.Demote(note);
                var rect = (RectTransform)note.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(DisplayConfig.DesignWidth - ResultSidePad * 2f, ResultNoteHeight);
                rect.anchoredPosition = new Vector2(0f, ResultGridCenterY - gridHalf - 28f - i * ResultNoteHeight);
                note.color = DimColor;
                note.text = string.Empty;
                notes[i] = note;
            }

            // ---- 버튼 둘. 확인은 가운데, 재뽑기(10회)는 오른쪽
            var confirmGo = new GameObject("Confirm", typeof(RectTransform));
            confirmGo.transform.SetParent(visual.transform, false);
            var confirmRect = (RectTransform)confirmGo.transform;
            confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.sizeDelta = new Vector2(ResultConfirmWidth, BannerButtonHeight);
            confirmRect.anchoredPosition = new Vector2(0f, ResultBottomPad);

            var confirmImage = confirmGo.AddComponent<Image>();
            UiSkin.ApplyPanel(confirmImage, UiSkin.InkChip);
            var confirm = confirmGo.AddComponent<Button>();
            UiSkin.ApplyButton(confirm, confirmImage);

            var confirmTitle = CreateLabel(confirmGo.transform, font, "Title", TextAlignmentOptions.Center);
            var confirmTitleRect = (RectTransform)confirmTitle.transform;
            confirmTitleRect.anchorMin = Vector2.zero;
            confirmTitleRect.anchorMax = Vector2.one;
            confirmTitleRect.offsetMin = new Vector2(ButtonTextPad, 0f);
            confirmTitleRect.offsetMax = new Vector2(-ButtonTextPad, 0f);
            confirmTitle.text = "확인";
            confirmTitle.color = UiSkin.Gold;

            var repullGo = new GameObject("Repull", typeof(RectTransform));
            repullGo.transform.SetParent(visual.transform, false);
            var repullRect = (RectTransform)repullGo.transform;
            repullRect.anchorMin = repullRect.anchorMax = new Vector2(1f, 0f);
            repullRect.pivot = new Vector2(1f, 0f);
            repullRect.sizeDelta = new Vector2(ResultRepullWidth, BannerButtonHeight);
            repullRect.anchoredPosition = new Vector2(-ResultSidePad + 24f, ResultBottomPad);

            var repullImage = repullGo.AddComponent<Image>();
            UiSkin.ApplyPanel(repullImage, GemButtonTint);
            var repull = repullGo.AddComponent<Button>();
            UiSkin.ApplyButton(repull, repullImage);

            var repullTitle = CreateLabel(repullGo.transform, font, "Title", TextAlignmentOptions.Center);
            UiFonts.Demote(repullTitle);
            PlaceCentered((RectTransform)repullTitle.transform, ButtonTextPad, LineHeight * 0.5f, LineHeight);
            repullTitle.text = Onikiri.UI.ShopPanel.TenTitle;

            var gem = new GameObject("Gem", typeof(RectTransform));
            gem.transform.SetParent(repullGo.transform, false);
            var gemImage = gem.AddComponent<Image>();
            gemImage.sprite = UiIcons.LoadItem(UiIcons.GemSprite);
            gemImage.preserveAspect = true;
            gemImage.raycastTarget = false;
            var gemRect = (RectTransform)gem.transform;
            gemRect.anchorMin = gemRect.anchorMax = new Vector2(0.5f, 0.5f);
            gemRect.pivot = new Vector2(1f, 0.5f);
            gemRect.sizeDelta = new Vector2(GemIconSize, GemIconSize);
            gemRect.anchoredPosition = new Vector2(-8f, -LineHeight * 0.5f);

            var repullCost = CreateLabel(repullGo.transform, font, "Cost", TextAlignmentOptions.Left);
            repullCost.fontSize = PullCostFontSize;
            var repullCostRect = (RectTransform)repullCost.transform;
            repullCostRect.anchorMin = repullCostRect.anchorMax = new Vector2(0.5f, 0.5f);
            repullCostRect.pivot = new Vector2(0f, 0.5f);
            repullCostRect.sizeDelta = new Vector2(ResultRepullWidth * 0.5f - ButtonTextPad, PullCostFontSize);
            repullCostRect.anchoredPosition = new Vector2(4f, -LineHeight * 0.5f);
            repullCost.text = GachaCurve.TenPullCostGems.ToString();

            // ---- 컴포넌트
            var popup = root.AddComponent<Onikiri.UI.GachaResultPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("grid").objectReferenceValue = grid;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("repullButton").objectReferenceValue = repull;
            so.FindProperty("repullBackground").objectReferenceValue = repullImage;
            so.FindProperty("repullTitle").objectReferenceValue = repullTitle;
            so.FindProperty("repullCost").objectReferenceValue = repullCost;
            so.FindProperty("repullGem").objectReferenceValue = gemImage;
            so.FindProperty("repullTint").colorValue = GemButtonTint;
            so.FindProperty("textColor").colorValue = TextColor;
            so.FindProperty("dimColor").colorValue = DimColor;
            so.FindProperty("goldColor").colorValue = UiSkin.Gold;
            so.FindProperty("tileSize").floatValue = ResultTileSize;
            so.FindProperty("tileGap").floatValue = ResultTileGap;
            so.FindProperty("columns").intValue = ResultColumns;

            var grades = so.FindProperty("gradeColors");
            grades.arraySize = UiSkin.Grades.Length;
            for (int i = 0; i < UiSkin.Grades.Length; i++)
                grades.GetArrayElementAtIndex(i).colorValue = UiSkin.Grades[i];

            var tileArray = so.FindProperty("tiles");
            tileArray.arraySize = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                var element = tileArray.GetArrayElementAtIndex(i);
                var p = parts[i];
                element.FindPropertyRelative("root").objectReferenceValue = (Object)p[0];
                element.FindPropertyRelative("frame").objectReferenceValue = (Object)p[1];
                element.FindPropertyRelative("inner").objectReferenceValue = (Object)p[2];
                element.FindPropertyRelative("icon").objectReferenceValue = (Object)p[3];
                element.FindPropertyRelative("grade").objectReferenceValue = (Object)p[4];
                element.FindPropertyRelative("value").objectReferenceValue = (Object)p[5];
                element.FindPropertyRelative("glow").objectReferenceValue = (Object)p[6];
            }

            var noteArray = so.FindProperty("notes");
            noteArray.arraySize = notes.Length;
            for (int i = 0; i < notes.Length; i++)
                noteArray.GetArrayElementAtIndex(i).objectReferenceValue = notes[i];

            // ---- 아이콘. 카탈로그 순서 그대로 - 결과의 인덱스가 곧 이 배열의 인덱스다
            so.FindProperty("shardSprite").objectReferenceValue = UiIcons.LoadItem(YodoSprites.ShardSprite);
            so.FindProperty("soulSprite").objectReferenceValue = UiIcons.LoadItem(YodoSprites.SoulSprite);
            so.FindProperty("skillXpSprite").objectReferenceValue = UiIcons.LoadItem(UiIcons.QuestSprite);

            var blades = so.FindProperty("bladeSprites");
            blades.arraySize = YodoCatalog.Count;
            for (int i = 0; i < YodoCatalog.Count; i++)
                blades.GetArrayElementAtIndex(i).objectReferenceValue =
                    UiIcons.LoadItem(YodoCatalog.Blades[i].IconSprite);

            var legends = so.FindProperty("legendarySprites");
            legends.arraySize = LegendaryYodoCatalog.Count;
            for (int i = 0; i < LegendaryYodoCatalog.Count; i++)
                legends.GetArrayElementAtIndex(i).objectReferenceValue =
                    UiIcons.LoadItem(LegendaryYodoCatalog.Blades[i].IconSprite);

            var skillIcons = so.FindProperty("skillSprites");
            skillIcons.arraySize = SkillCatalog.Count;
            for (int i = 0; i < SkillCatalog.Count; i++)
                skillIcons.GetArrayElementAtIndex(i).objectReferenceValue =
                    UiIcons.Load(SkillCatalog.Skills[i].IconFile);
            so.ApplyModifiedPropertiesWithoutUndo();

            BattleContentBuilder.RaiseToLayer(root.transform, DisplayConfig.SortingPopup, true);

            visual.SetActive(false);
            return popup;
        }

        public const string ResultPopupName = "GachaResultPopup";

        /** 판 뒤 딤. 선형 색 공간이라 알파 0.88은 체감 38% 밝기로만 어두워진다 - 0.96(체감 약 23%)으로 결과만 보이게, 그래도 게임이 돈다는 기색은 남긴다 */
        private static readonly Color ResultOverlayColor = new Color(0f, 0f, 0f, 0.96f);

        /** 타일 한 변. (1080 - 여백 72 x 2 - 간격 32 x 4) / 5 = 161.6 -> 160 (정수) */
        private const float ResultTileSize = 160f;
        private const float ResultTileGap = 32f;
        private const int ResultColumns = 5;
        private const float ResultSidePad = 72f;

        /** 아이콘 = 16px x 7. 픽셀 아트라 정수 배율이고, 둘레 24px에 모서리 글자가 선다 */
        private const float ResultIconSize = 112f;

        private const float ResultGridCenterY = 140f;
        private const int ResultNoteLines = 3;
        private const float ResultNoteHeight = 48f;
        private const float ResultBottomPad = 48f;
        private const float ResultConfirmWidth = 300f;
        private const float ResultRepullWidth = 320f;

        /** 빛의 크기 = 타일의 2.4배. 이웃 타일 사이로 새어 나오는 정도 */
        private const float ResultGlowScale = 2.4f;

        /**
         * @brief 타일 하나. **빛 · 테두리 · 안쪽 판 · 아이콘 · 모서리 글자 둘.**
         *
         * 빛이 맨 뒤다(형제 순서). 테두리는 등급 색 민짜 판이고 안쪽 판이 6px
         * 안으로 들어와 그 색을 눌러 담는다 - 9-slice 테두리를 쓰지 않는 이유는
         * 등급 색을 판 전체에 곱하면 Kenney 판의 명암이 색마다 다르게 읽히기
         * 때문이다(38b 그라데이션 금지와 같은 쪽의 판단).
         */
        private static object[] BuildTile(RectTransform grid, TMP_FontAsset font, int index, Sprite glowSprite)
        {
            var go = new GameObject("Tile" + index, typeof(RectTransform));
            go.transform.SetParent(grid, false);
            var root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(ResultTileSize, ResultTileSize);

            var glowGo = new GameObject("Glow", typeof(RectTransform));
            glowGo.transform.SetParent(go.transform, false);
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = glowSprite;
            glow.raycastTarget = false;
            glow.enabled = false;
            var glowRect = (RectTransform)glowGo.transform;
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = Vector2.one * ResultTileSize * ResultGlowScale;

            var frameGo = new GameObject("Frame", typeof(RectTransform));
            frameGo.transform.SetParent(go.transform, false);
            var frame = frameGo.AddComponent<Image>();
            frame.sprite = null;
            frame.raycastTarget = false;
            Stretch((RectTransform)frameGo.transform, 0f);

            var innerGo = new GameObject("Inner", typeof(RectTransform));
            innerGo.transform.SetParent(go.transform, false);
            var inner = innerGo.AddComponent<Image>();
            inner.sprite = null;
            inner.raycastTarget = false;
            Stretch((RectTransform)innerGo.transform, 6f);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(ResultIconSize, ResultIconSize);

            var grade = CreateLabel(go.transform, font, "Grade", TextAlignmentOptions.TopLeft);
            UiFonts.Demote(grade);
            var gradeRect = (RectTransform)grade.transform;
            gradeRect.anchorMin = gradeRect.anchorMax = new Vector2(0f, 1f);
            gradeRect.pivot = new Vector2(0f, 1f);
            gradeRect.sizeDelta = new Vector2(ResultTileSize - 12f, 48f);
            gradeRect.anchoredPosition = new Vector2(10f, -4f);
            grade.text = GachaCurve.GradeNames[0];

            var value = CreateLabel(go.transform, font, "Value", TextAlignmentOptions.BottomRight);
            UiFonts.Demote(value);
            var valueRect = (RectTransform)value.transform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(1f, 0f);
            valueRect.pivot = new Vector2(1f, 0f);
            valueRect.sizeDelta = new Vector2(ResultTileSize - 12f, 48f);
            valueRect.anchoredPosition = new Vector2(-10f, 4f);
            value.text = "+6";

            return new object[] { root, frame, inner, icon, grade, value, glow };
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        // ---------------------------------------------------------------- 확률 팝업

        /**
         * @brief 확률표 팝업 (69단계). 배너 제목 줄의 "확률" 버튼이 연다.
         *
         * 뼈대는 PopupBuilder(딤 · 창 · X · 층 20). 안에 두 배너의 표를 하나씩 세우고
         * GachaRatePopup이 하나만 켠다. 표는 46단계부터의 그 표다 - 두 칸 x 세 줄,
         * 등급 색, 소수 한 자리. 칸이 배너 안일 때보다 넓다(456px).
         */
        private static Onikiri.UI.GachaRatePopup BuildRatePopup(Transform safeArea, TMP_FontAsset font)
        {
            RectTransform root;
            var window = PopupBuilder.Ensure(safeArea, RatePopupName, font, RatePopupHeightFraction, out root);

            var title = CreateLabel(window, font, "Title", TextAlignmentOptions.Left);
            PlaceTopLeft((RectTransform)title.transform, RatePad, 28f,
                         RateWindowWidth - RatePad * 2f - PopupBuilder.CloseSize, LineHeight);
            title.text = Onikiri.UI.GachaRatePopup.YodoTitle;
            title.color = UiSkin.Gold;

            TMP_Text[] yodoChances, skillChances;
            var yodoTable = BuildRateTable(window, font, "YodoRates", i => RewardName(i), out yodoChances);
            var skillTable = BuildRateTable(window, font, "SkillRates", i => SkillRewardName(i), out skillChances);
            skillTable.SetActive(false);

            var footnote = CreateLabel(window, font, "Footnote", TextAlignmentOptions.Left);
            UiFonts.Demote(footnote);
            PlaceTopLeft((RectTransform)footnote.transform, RatePad,
                         RateTableTop + LineHeight * ((GachaCurve.OutcomeCount + 1) / 2) + 16f,
                         RateWindowWidth - RatePad * 2f, LineHeight);
            footnote.text = Onikiri.UI.GachaRatePopup.FootnoteFor(1);
            footnote.color = DimColor;

            var popup = root.gameObject.AddComponent<Onikiri.UI.GachaRatePopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("yodoTable").objectReferenceValue = yodoTable;
            so.FindProperty("skillTable").objectReferenceValue = skillTable;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("footnoteLabel").objectReferenceValue = footnote;
            WireTexts(so.FindProperty("yodoChances"), yodoChances);
            WireTexts(so.FindProperty("skillChances"), skillChances);
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return popup;
        }

        private static void WireTexts(SerializedProperty array, TMP_Text[] texts)
        {
            array.arraySize = texts.Length;
            for (int i = 0; i < texts.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = texts[i];
        }

        private static GameObject BuildRateTable(RectTransform window, TMP_FontAsset font, string name,
                                                 System.Func<int, string> label, out TMP_Text[] chances)
        {
            chances = new TMP_Text[GachaCurve.OutcomeCount];
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(window, false);
            Stretch((RectTransform)go.transform, 0f);

            float cellWidth = (RateWindowWidth - RatePad * 2f - RateGap) * 0.5f;
            for (int i = 0; i < GachaCurve.OutcomeCount; i++)
            {
                int column = i % 2, row = i / 2;
                chances[i] = BuildRateCell(go.transform, font, i, RatePad + column * (cellWidth + RateGap),
                              RateTableTop + row * LineHeight, cellWidth, label(i));
            }
            return go;
        }

        public const string RatePopupName = "GachaRatePopup";

        /** 표 아래 한 줄의 가장 긴 꼴(세 자리 레벨). 문구의 출처는 런타임이다(GachaRatePopup.FootnoteFor) */
        private static string RateFootnote { get { return Onikiri.UI.GachaRatePopup.FootnoteFor(888); } }

        private const float RatePopupHeightFraction = 0.24f;
        private const float RatePad = 48f;
        private const float RateTableTop = 104f;
        private static float RateWindowWidth { get { return DisplayConfig.DesignWidth - 72f; } }

        // ---------------------------------------------------------------- 검산

        /**
         * @brief 상자가 줄 수를 담는지 빌드가 검산한다 (세로).
         *
         * 69단계: 배너 행 안의 세 덩어리(제목 · 버튼 · 바)가 행 높이 안에 들고,
         * 배너 행 둘이 **뷰포트 안에 스크롤 없이** 서는지를 잰다.
         */
        private static void VerifyRowsFit()
        {
            float buttonContent = LineHeight * 2f;
            if (buttonContent > BannerButtonHeight)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shop banner button needs {0:F0}px for two lines but the button "
                    + "is {1:F0}px - the title spills above the panel.", buttonContent, BannerButtonHeight));

            float sideInset = Mathf.Min(18f, Mathf.Max(0f, (RowHeight - buttonContent) * 0.5f));
            if (buttonContent > RowHeight - sideInset * 2f)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shop row button needs {0:F0}px but the row leaves {1:F0}px.",
                    buttonContent, RowHeight - sideInset * 2f));

            if (BannerTitleTop + LineHeight > BannerButtonTop
                || BannerButtonTop + BannerButtonHeight > BannerBarTop
                || BannerBarTop + BannerBarHeight > BannerRowHeight - 16f)
                Debug.LogWarning("[Onikiri] Shop banner row: title / buttons / bar overlap or overflow the row.");

            if (BannerIconSize > BannerRowHeight - 32f)
                Debug.LogWarning("[Onikiri] Shop banner icon is taller than the row.");

            float twoRows = 2f * BannerRowHeight + RowGap;
            if (twoRows > ViewportHeight)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shop banner rows need {0:F0}px but the viewport is {1:F0}px - "
                    + "the second banner needs a scroll.", twoRows, ViewportHeight));
        }

        /** 배너 둘이 스크롤 없이 서는가 - 검사(GachaUiLayoutTests)가 읽는다 */
        public static bool BannerRowsFitTheViewport
        {
            get { return 2f * BannerRowHeight + RowGap <= ViewportHeight; }
        }

        private static void VerifyTextFits()
        {
            var caption = UiFonts.Caption;
            var primary = UiFonts.Primary;
            if (caption == null || primary == null) return;

            var probe = new GameObject("__ShopTextProbe", typeof(RectTransform));
            probe.hideFlags = HideFlags.HideAndDontSave;
            var text = probe.AddComponent<TextMeshProUGUI>();

            try
            {
                SetFont(text, primary, Onikiri.UI.PixelFontSizes.GalmuriSmall);
                CheckLine(text, "요괴 봉인 뽑기", TextWidth + TextRight - 24f, "banner title");
                CheckLine(text, SkillBannerTitle, TextWidth + TextRight - 24f, "banner title");
                foreach (var title in new[] { "단연", GachaCurve.TenPullCount + "연", "뽑기", "확인" })
                    CheckLine(text, title, ButtonTextWidth, "button title");
                CheckLine(text, "광고", ButtonTextWidth, "ad button title");

                SetFont(text, caption, Onikiri.UI.PixelFontSizes.GalmuriCaption);

                // 배너 본문은 버튼이 아래에 있어 좌우를 다 쓴다
                float bannerWidth = RowWidth - TextLeft - 24f;
                // ---- 69단계 배너 행
                float subtitleWidth = BannerContentWidth - 200f - 2f * (ChipWidth + ChipGap);
                CheckLine(text, Onikiri.UI.ShopPanel.YodoSubtitle, subtitleWidth, "banner subtitle");
                CheckLine(text, Onikiri.UI.ShopPanel.SkillSubtitle, subtitleWidth, "banner subtitle");
                CheckLine(text, Onikiri.UI.ShopPanel.RateButtonText, ChipWidth - 8f, "chip");
                CheckLine(text, Onikiri.UI.ShopPanel.AdButtonText, ChipWidth - 8f, "chip");
                float buttonWidth = (BannerContentWidth - BannerButtonGap) * 0.5f;
                CheckLine(text, Onikiri.UI.ShopPanel.FreeSingleNote, buttonWidth - ButtonTextPad * 2f, "button note");
                CheckLine(text, Onikiri.UI.ShopPanel.IntroTenNote, buttonWidth - ButtonTextPad * 2f, "button note");
                CheckLine(text, Onikiri.UI.ShopPanel.LevelLabel(888), SummonLabelWidth, "summon level");
                CheckLine(text, Onikiri.UI.ShopPanel.BarText(888888, 888888),
                          BannerContentWidth - SummonLabelWidth, "summon bar");
                CheckLine(text, Onikiri.UI.ShopPanel.SkillBuyLaterText, BannerContentWidth, "banner state");
                CheckLine(text, Onikiri.UI.ShopPanel.SoldOutText, BannerContentWidth, "banner state");

                // ---- 확률 팝업 (68단계까지의 "희귀 스킬 XP 240" 경고가 여기서 풀린다)
                float cellWidth = (RateWindowWidth - RatePad * 2f - RateGap) * 0.5f;
                for (int i = 0; i < GachaCurve.OutcomeCount; i++)
                {
                    CheckLine(text, RewardName(i), cellWidth - RateNumberWidth - RateGap, "rate name");
                    CheckLine(text, SkillRewardName(i), cellWidth - RateNumberWidth - RateGap, "skill rate name");
                    CheckLine(text, PercentText(i), RateNumberWidth, "rate percent");
                }
                // 열 때 지금 레벨 값이 선다 - 가장 넓은 꼴(두 자리 + 소수)도 칸 안이어야 한다
                CheckLine(text, Onikiri.UI.GachaRatePopup.PercentText(0.888), RateNumberWidth, "rate percent (level)");
                CheckLine(text, RateFootnote, RateWindowWidth - RatePad * 2f, "rate footnote");

                // ---- 상품 줄 (광고 · 보석 팩)
                CheckLine(text, "보석 " + GachaCurve.TenPullCostGems, ButtonTextWidth, "cost");
                foreach (var pack in Packs)
                {
                    CheckLine(text, pack.Name, TextWidth, "pack name");
                    CheckLine(text, pack.Price, ButtonTextWidth, "pack price");
                    CheckLine(text, "뽑기 " + (pack.Gems / GachaCurve.PullCostGems) + "회 분량", TextWidth, "pack note");
                }
                CheckLine(text, "광고 보고 한 번", TextWidth, "row name");
                CheckLine(text, "광고를 보면 뽑기 한 번", TextWidth, "ad note");
                CheckLine(text, "준비 중", ButtonTextWidth, "placeholder");

                // ---- 결과 판 (69단계 타일). 모서리 칸 = 타일 - 12
                float corner = ResultTileSize - 12f;
                foreach (var gradeName in GachaCurve.GradeNames) CheckLine(text, gradeName, corner, "tile grade");
                foreach (var v in new[] { "+70", "+" + LegendaryYodoCurve.ShardsPerOverflow, "혼 +1", "★4", "획득",
                                          "돌파 " + (LegendaryYodoCurve.MaxCopies - 1), "개안",
                                          "+" + SkillGachaCurve.XpFor(SkillGachaCurve.Outcome.XpSurge) })
                    CheckLine(text, v, corner, "tile value");
                foreach (var skill in SkillCatalog.Skills) CheckLine(text, skill.DisplayName, corner, "tile skill name");

                float noteWidth = DisplayConfig.DesignWidth - ResultSidePad * 2f;
                CheckLine(text, Onikiri.UI.GachaResultPopup.DowngradePrefix + "상위 혼 → 혼 정수 → 처형인의 혼",
                          noteWidth, "result note");
                CheckLine(text, Onikiri.UI.GachaResultPopup.DowngradePrefix + "상위 혼 → 혼 정수 → 파편 "
                              + YodoCurve.ShardsPerOverflowSoul, noteWidth, "result note");
                CheckLine(text, Onikiri.UI.GachaResultPopup.DowngradePrefix + "오의 개안 → 오의 해금 → XP +"
                              + SkillGachaCurve.XpFor(SkillGachaCurve.Outcome.XpSurge) + " · Lv +"
                              + (SkillCurve.MaxLevel - 1), noteWidth, "result note");
                CheckLine(text, "확인", ResultConfirmWidth - ButtonTextPad * 2f, "confirm");

                CheckLine(text, Onikiri.UI.GachaResultPopup.WithLevelUp(
                              "파편 8888 · 희귀 8 · 영웅 8 · 전설 8", 88), noteWidth, "result title + level up");
                CheckLine(text, Onikiri.UI.GachaResultPopup.WithLevelUp(
                              "스킬 XP 8888 · 희귀 8 · 영웅 8 · 전설 8", 88), noteWidth, "result title + level up");

                // 44pt - 배너 제목 · 버튼 동사
                SetFont(text, primary, Onikiri.UI.PixelFontSizes.GalmuriSmall);
                CheckLine(text, Onikiri.UI.ShopPanel.YodoTitle, 200f, "banner title");
                CheckLine(text, Onikiri.UI.ShopPanel.SkillTitle, 200f, "banner title");
                foreach (var t in new[] { Onikiri.UI.ShopPanel.SingleTitle, Onikiri.UI.ShopPanel.TenTitle,
                                          Onikiri.UI.ShopPanel.FreeSingleTitle, Onikiri.UI.ShopPanel.IntroTenTitle })
                    CheckLine(text, t, buttonWidth - ButtonTextPad * 2f, "pull button title");
                CheckLine(text, Onikiri.UI.ShopPanel.TenTitle, ResultRepullWidth - ButtonTextPad * 2f, "repull title");
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static void SetFont(TMP_Text text, TMP_FontAsset font, float size)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontSize = size;
        }

        /**
         * @brief 뽑기가 열 수 있는 오의 전부 (표준 다섯 + 귀오의 넷).
         *
         * 두 배열을 여기서 합치는 이유는 **폭 검산이 묻는 질문이 하나**이기
         * 때문이다 - "천장 줄에 뜰 수 있는 가장 긴 이름이 칸에 드는가". 어느
         * 풀에서 왔는지는 그 질문과 무관하다.
         */
        private static System.Collections.Generic.IEnumerable<string> AllGachaSkillIds()
        {
            foreach (var id in SkillGachaCurve.StandardUnlockOrder) yield return id;
            foreach (var id in SkillGachaCurve.OniSecretUnlockOrder) yield return id;
        }

        private static void CheckLine(TMP_Text probe, string worst, float boxWidth, string where)
        {
            probe.textWrappingMode = TextWrappingModes.NoWrap;
            float needed = probe.GetPreferredValues(worst).x;
            if (needed > boxWidth)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shop {0} overflows: \"{1}\" needs {2:F0}px but its box is {3:F0}px.",
                    where, worst, needed, boxWidth));
        }

        // ---------------------------------------------------------------- 조각

        private static Button BuildSideButton(Transform parent, TMP_FontAsset font, string name,
                                              Color tint, string title, string cost,
                                              out TMP_Text titleLabel, out TMP_Text costLabel,
                                              out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            float inset = Mathf.Min(18f, Mathf.Max(0f, (RowHeight - LineHeight * 2f) * 0.5f));

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-ButtonWidth - ButtonRight, inset);
            rect.offsetMax = new Vector2(-ButtonRight, -inset);

            image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, tint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Center);
            PlaceCentered((RectTransform)titleLabel.transform, ButtonTextPad,
                          LineHeight * 0.5f, LineHeight);
            titleLabel.text = title;

            costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Center);
            UiFonts.Demote(costLabel);
            PlaceCentered((RectTransform)costLabel.transform, ButtonTextPad,
                          -LineHeight * 0.5f, LineHeight);
            costLabel.color = DimColor;
            costLabel.text = cost;

            return button;
        }

        private static void Place(GameObject go, float top, float height)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        private static void Place(RectTransform rect, float left, float right,
                                  float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void PlaceCentered(RectTransform rect, float pad,
                                          float centerY, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(pad, centerY - height * 0.5f);
            rect.offsetMax = new Vector2(-pad, centerY + height * 0.5f);
        }

        private static Image CreateIcon(Transform parent, Sprite sprite, Color tint, float top)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(UiIcons.Size, UiIcons.Size);
            rect.anchoredPosition = new Vector2(IconLeft, -top);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            if (sprite == null) image.color = new Color(1f, 0f, 1f, 0.35f);
            return image;
        }

        private static TMP_Text CreateLabel(Transform parent, TMP_FontAsset font, string name,
                                            TextAlignmentOptions alignment)
        {
            var label = QuestPanelBuilder.CreateLabel(parent, font, name, alignment);
            label.color = TextColor;
            return label;
        }
    }
}
