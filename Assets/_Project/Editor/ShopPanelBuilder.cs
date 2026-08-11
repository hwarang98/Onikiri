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
         * @brief 배너 본문 줄 수. 이름 / 설명 / 확률표 / 천장.
         *
         * **확률표를 접지 않기 때문에** 줄 수가 결과 수에서 나온다 - 숨기면
         * 공개한 것이 아니고, 그 원칙이 46단계부터 이 배너의 크기를 정해
         * 왔다. 47단계에 결과가 넷에서 여섯이 되면서 확률표가 두 줄에서
         * 세 줄이 됐다(두 칸씩).
         */
        private static int BannerLines
        {
            get { return 2 + (GachaCurve.OutcomeCount + 1) / 2 + 1; }
        }

        /**
         * @brief 뽑기 배너의 높이. 본문 + 버튼 한 줄.
         *
         * 46b에 392 -> 420. 버튼이 두 줄을 담을 만큼 커지면서(아래
         * BannerButtonHeight) 그 아래 여백이 사라졌다 - 상수를 손으로 맞추지
         * 않게 VerifyRowsFit이 둘의 관계를 검산한다.
         *
         * 47단계에 그 검산을 **상수에서 식으로** 바꿨다. 확률표가 결과 수를
         * 따라 자라므로 손으로 맞추면 사다리에 칸을 더하는 날 반드시 넘친다 -
         * 44단계 요도 행이 실기에서 넘친 뒤 세운 규칙(상자를 글자에 맞추지
         * 그 반대가 아니다)을 여기서는 아예 계산으로 둔다.
         */
        private static float BannerHeight
        {
            get { return 16f + LineHeight * BannerLines + 8f + BannerButtonHeight + 16f; }
        }

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
         * 그것이 천장의 절반이라는 사실은 이 스텝에서 이미 정해져 있어야
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

        private static float ContentHeight
        {
            get
            {
                // 배너 + 무료 + 광고 + 보석 팩들
                return BannerHeight + RowGap
                     + (2 + Packs.Length) * (RowHeight + RowGap);
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
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            VerifyRowsFit();
            VerifyTextFits();

            var panel = EnsurePanel(safeArea);
            var header = BuildHeader(panel, font);
            var content = EnsureScroll(panel);

            var shop = panel.gameObject.AddComponent<Onikiri.UI.ShopPanel>();
            var so = new SerializedObject(shop);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("yodo").objectReferenceValue = battle.GetComponent<YodoSystem>();
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("goldColor").colorValue = UiSkin.Gold;
            so.FindProperty("buttonTint").colorValue = GemButtonTint;
            so.FindProperty("freeTint").colorValue = FreeButtonTint;

            BuildBanner(content, font, so);
            BuildFreeRow(content, font, so, 0);
            BuildAdRow(content, font, 1);
            for (int i = 0; i < Packs.Length; i++) BuildPackRow(content, font, 2 + i, Packs[i]);

            so.FindProperty("popup").objectReferenceValue = BuildPopup(panel, font);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 잠긴 동안의 안내. 탭은 눌리고 화면은 열리되 버튼만 죽는다
            // (41단계 규칙). 문구 형식은 LockedTab.Requirement와 같은 말이어야
            // 한다 - 탭과 배너가 다른 문장으로 같은 조건을 말하면 두 규칙이 된다
            LockBannerBuilder.Build(header, font,
                                    GachaCurve.UnlockStage + "스테이지 도달 시 해금",
                                    1, GachaCurve.UnlockStage);

            panel.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Shop panel built: 배너 + 무료 + 광고 + 보석 팩 {0} = {1:F0}px "
                + "(뷰포트 {2:F0}px). 단연 {3} · 10연 {4} · 천장 {5}회",
                Packs.Length, ContentHeight, ViewportHeight,
                GachaCurve.PullCostGems, GachaCurve.TenPullCostGems, GachaCurve.PityPulls));

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

            // 진행(천장·누적·무료 쿨)은 덮어쓰지 않는다. 빌더를 한 번 돌릴
            // 때마다 천장 카운터가 0으로 돌아가면 플레이어가 지불한 29회가
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
            title.text = "상점";
            title.color = DimColor;

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

        private static void BuildBanner(RectTransform content, TMP_FontAsset font,
                                        SerializedObject shop)
        {
            var go = new GameObject("GachaBanner", typeof(RectTransform));
            go.transform.SetParent(content, false);
            Place(go, 0f, BannerHeight);

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Chrome);

            CreateIcon(go.transform, UiIcons.LoadItem(YodoSprites.SoulSprite), Color.white, 18f);

            var title = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Left);
            Place((RectTransform)title.transform, TextLeft, 24f, 16f, LineHeight);
            title.text = "요괴 봉인 뽑기";

            var desc = CreateLabel(go.transform, font, "Desc", TextAlignmentOptions.Left);
            UiFonts.Demote(desc);
            Place((RectTransform)desc.transform, TextLeft, 24f, 16f + LineHeight, LineHeight);
            desc.color = DimColor;
            desc.text = BannerDesc;

            // 확률표. 접지 않는다 - 숨기면 공개한 것이 아니다.
            //
            // 47b: 실기 캡처에서 **"파편 6 74%"가 "파편 674%"로 읽혔다.**
            // 원인은 한 라벨 안에 네 조각을 공백으로 이어 붙인 것이고
            // (46단계의 RateLine), 공백은 비례폭 폰트에서 자릿수 사이의
            // 공백과 구분되지 않는다. 고친 것은 둘이다:
            //
            //   1. 이름과 확률 **사이에 가운뎃점**을 넣는다 (" · ")
            //   2. 한 라벨을 둘로 쪼개 **확률을 오른쪽 정렬**한다 -
            //      열이 서면 숫자를 세로로 훑을 수 있고, 그것이 확률표가
            //      해야 하는 유일한 일이다
            //
            // 줄이 둘에서 셋으로 는 것은 결과가 여섯이 됐기 때문이다
            // (46단계 넷 + ★4·★5). 두 칸 x 세 줄이고, 등급 색이 세로로
            // 사다리를 만든다
            int rows = (GachaCurve.OutcomeCount + 1) / 2;
            for (int i = 0; i < GachaCurve.OutcomeCount; i++)
                BuildRateCell(go.transform, font, i, i % 2, i / 2);

            var pity = CreateLabel(go.transform, font, "Pity", TextAlignmentOptions.Left);
            UiFonts.Demote(pity);
            Place((RectTransform)pity.transform, TextLeft, 24f,
                  16f + LineHeight * (2f + rows), LineHeight);
            pity.color = DimColor;
            pity.text = PityText(GachaCurve.PityPulls, 0);

            TMP_Text singleCost, tenCost;
            Image singleImage, tenImage;
            var single = BuildBannerButton(go.transform, font, "Single", GemButtonTint,
                                           "단연", "보석 " + GachaCurve.PullCostGems,
                                           0, out singleCost, out singleImage);
            var ten = BuildBannerButton(go.transform, font, "Ten", GemButtonTint,
                                        GachaCurve.TenPullCount + "연",
                                        "보석 " + GachaCurve.TenPullCostGems,
                                        1, out tenCost, out tenImage);

            shop.FindProperty("singleButton").objectReferenceValue = single;
            shop.FindProperty("singleCost").objectReferenceValue = singleCost;
            shop.FindProperty("singleBackground").objectReferenceValue = singleImage;
            shop.FindProperty("tenButton").objectReferenceValue = ten;
            shop.FindProperty("tenCost").objectReferenceValue = tenCost;
            shop.FindProperty("tenBackground").objectReferenceValue = tenImage;
            shop.FindProperty("pityLabel").objectReferenceValue = pity;
        }

        /**
         * @brief 확률표 한 칸. **라벨 둘이다 - 이름 왼쪽, 확률 오른쪽.**
         *
         * 쪼갠 이유는 위 주석에 있다(가독 - "파편 674%"). 한 칸의 폭이
         * 고정이므로 오른쪽 정렬만으로 열이 서고, TMP의 한 줄 안에서
         * 두 정렬을 섞는 방법(리치 텍스트 pos 태그)보다 이쪽이 검산
         * (VerifyTextFits)에도 정직하다 - 두 조각의 폭을 따로 잴 수 있다.
         */
        private static void BuildRateCell(Transform parent, TMP_FontAsset font,
                                          int outcome, int column, int row)
        {
            float cellWidth = (RowWidth - TextLeft - 24f) * 0.5f;
            float left = TextLeft + column * cellWidth;
            float top = 16f + LineHeight * (2f + row);
            var tint = UiSkin.Grades[(int)GachaCurve.GradeOf[outcome]];

            var name = CreateLabel(parent, font, "Rate" + outcome, TextAlignmentOptions.Left);
            UiFonts.Demote(name);
            Place((RectTransform)name.transform, left,
                  RowWidth - (left + cellWidth) + RateNumberWidth, top, LineHeight);
            name.color = tint;
            name.text = RewardName(outcome);

            var chance = CreateLabel(parent, font, "Rate" + outcome + "Pct",
                                     TextAlignmentOptions.Right);
            UiFonts.Demote(chance);
            Place((RectTransform)chance.transform,
                  left + cellWidth - RateNumberWidth - RateGap,
                  RowWidth - (left + cellWidth) + RateGap, top, LineHeight);
            chance.color = tint;
            chance.text = PercentText(outcome);
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
            return (GachaCurve.Chances[outcome] * 100d).ToString("0.0") + "%";
        }

        /** 배너 아래 진행 줄. 문구의 출처는 런타임이다 - GachaCurve.PityText 주석 */
        private static string PityText(int left, int total)
        {
            return GachaCurve.PityText(left, total);
        }

        /**
         * @brief 배너 아래쪽 버튼 둘. 좌우로 반씩 나눈다.
         *
         * 요도 행의 버튼(BuildSideButton)과 자리가 다른 이유는 배너가 행이
         * 아니라 카드이기 때문이다 - 오른쪽에 세우면 본문 다섯 줄이 320px을
         * 잃고 확률표가 접힌다.
         */
        private static Button BuildBannerButton(Transform parent, TMP_FontAsset font, string name,
                                                Color tint, string title, string cost, int slot,
                                                out TMP_Text costLabel, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            float half = (RowWidth - 24f * 2f - BannerButtonGap) * 0.5f;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(half, BannerButtonHeight);
            rect.anchoredPosition = new Vector2(24f + slot * (half + BannerButtonGap), 16f);

            image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, tint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Center);
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

        // ---------------------------------------------------------------- 상품 줄

        private static RectTransform BuildRow(RectTransform content, string name, int slot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);

            float top = BannerHeight + RowGap + slot * (RowHeight + RowGap);
            Place(go, top, RowHeight);

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            return (RectTransform)go.transform;
        }

        private static void BuildFreeRow(RectTransform content, TMP_FontAsset font,
                                         SerializedObject shop, int slot)
        {
            var row = BuildRow(content, "FreePull", slot);

            CreateIcon(row, UiIcons.LoadItem(UiIcons.QuestSprite), UiIcons.Tint, 20f);

            // **행 이름은 캡션이다** (2a 위계 - 행 이름/비용/전후값은 33,
            // 44로 남는 것은 재화·헤더·동사뿐). 처음에 44로 뒀다가 실기에서
            // 물렸다("폰트가 너무 크다") - 이 판에서 44는 배너 제목 하나와
            // 버튼의 동사뿐이어야 목록이 훑어진다
            var title = CreateLabel(row, font, "Title", TextAlignmentOptions.Left);
            UiFonts.Demote(title);
            Place((RectTransform)title.transform, TextLeft, TextRight, 20f, LineHeight);
            title.text = "오늘의 무료 뽑기";

            var state = CreateLabel(row, font, "State", TextAlignmentOptions.Left);
            UiFonts.Demote(state);
            Place((RectTransform)state.transform, TextLeft, TextRight, 20f + LineHeight, LineHeight);
            state.color = DimColor;
            state.text = "오늘의 무료 뽑기가 남아 있다";

            TMP_Text buyTitle, buyCost;
            Image buyImage;
            var button = BuildSideButton(row, font, "Claim", FreeButtonTint,
                                         "뽑기", "무료", out buyTitle, out buyCost, out buyImage);

            shop.FindProperty("freeButton").objectReferenceValue = button;
            shop.FindProperty("freeTitle").objectReferenceValue = buyTitle;
            shop.FindProperty("freeCost").objectReferenceValue = buyCost;
            shop.FindProperty("freeBackground").objectReferenceValue = buyImage;
            shop.FindProperty("freeStateLabel").objectReferenceValue = state;
        }

        /**
         * @brief 광고 뽑기 자리. **비활성이고 그렇게 보인다.**
         *
         * 광고 SDK는 다음 스텝이다. 그때 이 줄의 버튼이 살아나고 나머지는
         * 그대로다 - 자리와 문구를 지금 정해 두면 그 스텝이 배선만 하면 된다.
         */
        private static void BuildAdRow(RectTransform content, TMP_FontAsset font, int slot)
        {
            var row = BuildRow(content, "AdPull", slot);
            // 버튼 제목이 "광고"인 이유: 보석 팩 줄은 그 자리에 **가격**을 적고
            // (그것이 그 줄의 값이다) 광고 줄에는 가격이 없다. 둘 다 "준비 중"을
            // 적으면 한 판에 같은 말이 두 번 뜬다 - 실기 캡처에서 실제로 그랬다
            BuildPlaceholder(row, font, UiGlyphBuilder.Load(UiGlyphBuilder.Gear),
                             "광고 보고 한 번", "광고를 보면 뽑기 한 번", "광고");
        }

        private static void BuildPackRow(RectTransform content, TMP_FontAsset font,
                                         int slot, PackSpec spec)
        {
            var row = BuildRow(content, "Pack" + spec.Gems, slot);
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
         * @brief 뽑기 결과 판. **패널 위에 겹쳐 서고 스크롤 밖이다.**
         *
         * 스크롤 콘텐츠 안에 두면 10연을 돌린 순간 결과가 화면 밖에 있을 수
         * 있다 - 41단계의 레벨업 버튼이 같은 이유로 스크롤 밖에 섰다.
         */
        private static Onikiri.UI.GachaResultPopup BuildPopup(RectTransform panel,
                                                              TMP_FontAsset font)
        {
            var root = new GameObject("ResultPopup", typeof(RectTransform));
            root.transform.SetParent(panel, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(root.transform, false);

            // **띠 전체를 덮지 않는다.** 내용(머리글 + 다섯 줄 + 버튼)에 맞춘
            // 카드로 세우고 가운데에 띄운다 - 처음에 띠를 통째로 덮었더니
            // 결과 열 줄 아래로 250px이 비어 "무언가 더 있어야 하는데 없는
            // 판"으로 읽혔다(실기 캡처)
            float lineRows = GachaCurve.TenPullCount / 2;
            float cardHeight = PopupTop + LineHeight * (1.4f + lineRows)
                             + PopupGap + BannerButtonHeight + PopupTop;

            var visualRect = (RectTransform)visual.transform;
            visualRect.anchorMin = new Vector2(0f, 0.5f);
            visualRect.anchorMax = new Vector2(1f, 0.5f);
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.offsetMin = new Vector2(SidePadding, -cardHeight * 0.5f);
            visualRect.offsetMax = new Vector2(-SidePadding, cardHeight * 0.5f);

            var image = visual.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Chrome);
            image.raycastTarget = true;

            var title = CreateLabel(visual.transform, font, "Title", TextAlignmentOptions.Center);
            Place((RectTransform)title.transform, 24f, 24f, PopupTop, LineHeight);
            title.text = "파편 0";

            // 두 칸 x 다섯 줄. 10연이 한눈에 들어오는 유일한 배치다
            var lines = new TMP_Text[GachaCurve.TenPullCount];
            for (int i = 0; i < lines.Length; i++)
            {
                var label = CreateLabel(visual.transform, font, "Line" + i,
                                        TextAlignmentOptions.Center);
                UiFonts.Demote(label);

                int column = i % 2;
                int line = i / 2;
                float half = (RowWidth - 48f) * 0.5f;

                var rect = (RectTransform)label.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(half, LineHeight);
                rect.anchoredPosition = new Vector2(24f + column * half,
                                                    -(PopupTop + LineHeight * (1.4f + line)));
                lines[i] = label;
            }

            TMP_Text confirmTitle, confirmCost;
            Image confirmImage;
            var confirm = BuildSideButton(visualRect, font, "Confirm", UiSkin.Chrome,
                                          "확인", string.Empty,
                                          out confirmTitle, out confirmCost, out confirmImage);
            var confirmRect = (RectTransform)confirm.transform;
            confirmRect.anchorMin = new Vector2(0.5f, 0f);
            confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.sizeDelta = new Vector2(ButtonWidth, BannerButtonHeight);
            confirmRect.anchoredPosition = new Vector2(0f, PopupTop);

            var popup = root.AddComponent<Onikiri.UI.GachaResultPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("textColor").colorValue = TextColor;
            so.FindProperty("dimColor").colorValue = DimColor;
            so.FindProperty("goldColor").colorValue = UiSkin.Gold;
            so.FindProperty("cardBackground").objectReferenceValue = image;
            so.FindProperty("cardNormalTint").colorValue = UiSkin.Chrome;
            so.FindProperty("cardLegendaryTint").colorValue = LegendaryCardTint;

            var grades = so.FindProperty("gradeColors");
            grades.arraySize = UiSkin.Grades.Length;
            for (int i = 0; i < UiSkin.Grades.Length; i++)
                grades.GetArrayElementAtIndex(i).colorValue = UiSkin.Grades[i];

            var array = so.FindProperty("lines");
            array.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            visual.SetActive(false);
            return popup;
        }

        // ---------------------------------------------------------------- 검산

        /**
         * @brief 최악 문자열을 **실제 폰트로 재서** 상자 폭과 대조한다.
         *
         * 44단계가 요도 행에서 실기 캡처로 물린 뒤 세운 규칙이다
         * (YodoPanelBuilder.VerifyTextFits) - 화면 크기 주장은 빌드가 잰다.
         * 여기서 특히 위험한 것은 확률표 줄이다: 네 조각이 두 줄에 나뉘어
         * 들어가는데, 그 폭은 표의 숫자가 바뀌면 함께 움직인다.
         */
        /**
         * @brief 상자가 줄 수를 담는지 빌드가 검산한다.
         *
         * 44단계 요도 행이 실기 캡처에서 넘친 뒤 세운 규칙(VerifyRowsFit)을
         * 이 화면에도 둔다. 폭은 VerifyTextFits가 재고, 여기는 **세로**다 -
         * 46b에 버튼이 정확히 그 자리에서 물렸다.
         */
        private static void VerifyRowsFit()
        {
            float buttonContent = LineHeight * 2f;
            if (buttonContent > BannerButtonHeight)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shop banner button needs {0:F0}px for two lines but the button "
                    + "is {1:F0}px - the title spills above the panel.",
                    buttonContent, BannerButtonHeight));

            float sideInset = Mathf.Min(18f, Mathf.Max(0f, (RowHeight - buttonContent) * 0.5f));
            if (buttonContent > RowHeight - sideInset * 2f)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shop row button needs {0:F0}px but the row leaves {1:F0}px.",
                    buttonContent, RowHeight - sideInset * 2f));

            float bannerContent = 16f + LineHeight * BannerLines + 8f + BannerButtonHeight + 16f;
            if (bannerContent > BannerHeight)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shop banner needs {0:F0}px but it is {1:F0}px - "
                    + "the buttons overlap the rate table.", bannerContent, BannerHeight));
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
                foreach (var title in new[] { "단연", GachaCurve.TenPullCount + "연", "뽑기", "확인" })
                    CheckLine(text, title, ButtonTextWidth, "button title");
                CheckLine(text, "광고", ButtonTextWidth, "ad button title");

                SetFont(text, caption, Onikiri.UI.PixelFontSizes.GalmuriCaption);

                // 배너 본문은 버튼이 아래에 있어 좌우를 다 쓴다
                float bannerWidth = RowWidth - TextLeft - 24f;
                CheckLine(text, BannerDesc, bannerWidth, "banner desc");

                // 확률표는 칸마다 라벨 둘이다. **두 조각을 따로 잰다** -
                // 이름이 확률 열을 침범하면 두 열이 붙어 46단계의 "파편
                // 674%"가 그대로 돌아온다
                float cellWidth = bannerWidth * 0.5f;
                for (int i = 0; i < GachaCurve.OutcomeCount; i++)
                {
                    CheckLine(text, RewardName(i), cellWidth - RateNumberWidth - RateGap,
                              "rate name");
                    CheckLine(text, PercentText(i), RateNumberWidth, "rate percent");
                }

                CheckLine(text, PityText(GachaCurve.PityPulls, 8888), bannerWidth, "pity line");

                CheckLine(text, "보석 " + GachaCurve.TenPullCostGems, ButtonTextWidth, "cost");
                foreach (var pack in Packs)
                {
                    CheckLine(text, pack.Name, TextWidth, "pack name");
                    CheckLine(text, pack.Price, ButtonTextWidth, "pack price");
                }
                CheckLine(text, "오늘의 무료 뽑기", TextWidth, "row name");
                CheckLine(text, "광고 보고 한 번", TextWidth, "row name");
                CheckLine(text, "오늘의 무료 뽑기가 남아 있다", TextWidth, "free state");
                CheckLine(text, "새벽 4시에 다시 열린다", TextWidth, "free state");
                CheckLine(text, "광고를 보면 뽑기 한 번", TextWidth, "ad note");
                CheckLine(text, "준비 중", ButtonTextWidth, "placeholder");
                CheckLine(text, "00:00:00", ButtonTextWidth, "free timer");
                foreach (var pack in Packs)
                    CheckLine(text, "뽑기 " + (pack.Gems / GachaCurve.PullCostGems) + "회 분량",
                              TextWidth, "pack note");

                // 결과 줄. 두 칸이라 폭이 절반이다
                float halfLine = (RowWidth - 48f) * 0.5f;
                CheckLine(text, "혼 정수 → 처형인의 혼", halfLine, "result line");
                CheckLine(text, "혼 정수 → 파편 " + YodoCurve.ShardsPerOverflowSoul,
                          halfLine, "result line");
                CheckLine(text, "파편 " + GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardJackpot],
                          halfLine, "result line");

                // 47단계의 최악 줄들. ★4가 미끄러진 줄이 가장 길다 -
                // 두 단어를 다 적기 때문이고, 그것이 문구로 지키는 규칙이다
                CheckLine(text, "천장! 상위 혼 → 혼 정수 → 처형인의 혼", halfLine, "result line");
                CheckLine(text, "천장! 상위 혼 → 파편 " + GachaCurve.ShardsPerOverflowRarity,
                          halfLine, "result line");
                string fullStars = YodoRarityCurve.Stars(YodoRarityCurve.MaxRarity);
                foreach (var blade in YodoCatalog.Blades)
                    CheckLine(text, "천장! " + blade.BladeName + " " + fullStars,
                              halfLine, "result line");

                foreach (var blade in LegendaryYodoCatalog.Blades)
                {
                    CheckLine(text, blade.BladeName + " 획득!", halfLine, "result line");
                    CheckLine(text, blade.BladeName + " 돌파 "
                                  + (LegendaryYodoCurve.MaxCopies - 1), halfLine, "result line");
                }
                CheckLine(text, "전설 → 파편 " + LegendaryYodoCurve.ShardsPerOverflow,
                          halfLine, "result line");

                CheckLine(text, "파편 8888 · 희귀 8 · 영웅 8 · 전설 8",
                          RowWidth - 48f, "result title");
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
