using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 펫(동료) 화면을 세운다.
     *
     * ## 자리는 성장·스킬·퀘스트·장비와 같은 띠, 진입은 하단 탭이다
     *
     * 화면 10~45%. 다섯이 동시에 보일 일이 없고, 같은 자리를 쓰면 "아래쪽
     * 절반은 목록"이라는 화면의 문법이 유지된다. 진입을 하단 탭으로 둔 이유는
     * 장비·퀘스트와 같다 - 하단 탭이 이 게임의 "어디로 갈지"다. 상단바·탭바
     * 전면 재정리는 폴리싱 몫이므로 여기서는 탭 하나만 늘린다.
     *
     * ## 스크롤이 있다 - 장비(스크롤 없음)와 갈린 이유
     *
     * 장비는 카드 둘이 띠(672px)에 들어가 스크롤을 두지 않았고, "셋째 슬롯이
     * 생기는 순간 검산에서 걸린다"고 적어 뒀다. 펫은 처음부터 셋이고 카드를
     * 아무리 깎아도 세 장 + 머리글이 672px에 들어가지 않는다(최소 706px 실측).
     * 게다가 이 목록은 자랄 자리다 - 가챠가 오면 카드가 늘어난다. 퀘스트
     * 패널과 같은 Viewport(RectMask2D) + Content 구조를 쓴다.
     */
    public static class PetPanelBuilder
    {
        public const string PanelName = "PetPanel";

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        private const float SidePadding = 48f;
        private const float TopPadding = 12f;

        private const float HeaderHeight = 60f;
        private const float HeaderGap = 10f;

        /**
         * @brief 카드 높이 (41b에서 224 -> 160).
         *
         * 224는 풀폭 레벨업 버튼(96px)이 카드 바닥을 통째로 먹던 시절의
         * 값이다. 그 버튼이 카드를 지배해서 "버튼에 카드가 붙은" 모양이
         * 됐고, 강화 행(144px, 비용이 오른쪽 칸)과 문법도 갈렸다. 버튼을
         * 오른쪽의 컴팩트한 비용 칸으로 옮기면 본문 세 줄(이름/역할/DPS)
         * + 초상이 160에 넉넉히 든다.
         */
        private const float CardHeight = 160f;
        private const float CardGap = 12f;

        /** 본문 한 줄. 카드 글자가 전부 캡션(33pt)이라 44면 숨이 쉰다 */
        private const float LineHeight = 44f;

        /** 오른쪽 동작 버튼 칸. 강화 행의 비용 폭(300)과 같은 값이다 */
        private const float ActionWidth = 300f;
        private const float ActionInset = 20f;

        /** 본문 글줄이 버튼을 침범하지 않는 오른쪽 여백 */
        private const float TextRight = 24f + ActionWidth + 16f;

        private const float IconLeft = 24f;

        /**
         * @brief 펫 초상 칸 (41b에서 96 -> 136 + 마스크/실측 정렬).
         *
         * 96 상자에 preserveAspect로 넣었더니 동료가 콩알만 했다. 원인은
         * 아트가 아니라 **캔버스**다 - 프레임이 옆으로 넓고(청랑 192x64)
         * 그 안의 그려진 픽셀은 66x44뿐이라, 상자에 캔버스를 맞추면 몸은
         * 1/3 크기가 된다. 전직 초상(preserveAspect 피벗 함정)과 같은
         * 계열의 사고다: 기준은 캔버스가 아니라 그려진 픽셀이어야 한다.
         *
         * 그래서 캐릭터 탭처럼 마스크 칸을 파고, 그려진 픽셀을 실측해
         * (MeasureDrawnRect) 그 중심을 칸 중앙에 놓는다. 배율은 세 마리
         * 공통 2배(정수 - 픽셀이 울지 않는 최소 규칙)다. 실측 폭이 가장
         * 큰 청랑(66px)의 2배(132)가 들어가는 136으로 칸을 정했다 -
         * 마리마다 배율을 다르게 맞추면 청랑 1배/명궁 3배가 되어 화면의
         * 상대 크기가 거짓말이 된다.
         */
        private const float PortraitBox = 136f;
        private const int PortraitScale = 2;
        private const float TextLeft = IconLeft + PortraitBox + 20f;

        // 39단계 톤 통일: 자기 색을 갖지 않는다. 팔레트의 단일 출처는 UiSkin이다
        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        /** 보석 버튼(해금)의 틴트. 장비 등급업과 같은 청 - 재화가 색이다 */
        private static readonly Color GemButtonTint = UiSkin.GemAction;

        /** 출전 중인 카드의 판 틴트. 금색 계열 - MASTER와 같은 "완성" 축이 아니라
            "지금 이 아이"라는 표시라, 채도를 낮춰 배경으로만 남긴다 */
        private static readonly Color ActiveRowTint = new Color(0.52f, 0.47f, 0.34f, 1f);

        [MenuItem("Onikiri/Build Pet Panel")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Build()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogError("[Onikiri] Battle root missing - run Build Combat Content first.");
                return;
            }

            var system = battle.GetComponent<PetSystem>();
            if (system == null)
            {
                Debug.LogError("[Onikiri] PetSystem missing - run Build Pet Content first.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - run Build Main Scene first.");
                return;
            }

            var panel = EnsurePanel(safeArea);
            BuildHeader(panel, font);
            var content = EnsureScroll(panel);

            for (int i = 0; i < PetCatalog.Count; i++)
                BuildCard(content, system, font, i);

            // 판은 꺼진 채로 저장된다. 하단 탭이 켠다 - 스킬·퀘스트·장비와 같은 규칙
            panel.gameObject.SetActive(false);

            Debug.Log(string.Format(
                "[Onikiri] Companion panel built: {0} companions, {1:F0}px (band {2:F0}px), unlock st{3}.",
                PetCatalog.Count, PanelContentHeight, BandHeight, PetCurve.UnlockStage));

            // 판을 새로 만들었으니 하단 탭을 다시 물린다 (RelinkScreenTabs 주석)
            BattleContentBuilder.RelinkScreenTabs();
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
                       + PetCatalog.Count * (CardHeight + CardGap);
            }
        }

        /**
         * @brief 스크롤. 세 카드 + 머리글이 뷰포트에 안 들어간다.
         *
         * 퀘스트 패널과 같은 구조다 - Viewport(RectMask2D) 안에 Content가 있고
         * 카드들이 그 아래 쌓인다.
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
                -(TopPadding + HeaderHeight + HeaderGap));

            go.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);

            var content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, PetCatalog.Count * (CardHeight + CardGap));
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

            // 화지 위의 벚가지 (39단계). 다른 하단 패널과 같은 헬퍼
            BackdropTextureBuilder.AddSakuraBranch(rect);

            return rect;
        }

        /** 제목 + 보석 잔액. 해금 버튼이 보석을 요구하는 화면이라 잔액이 같은 화면에 선다 */
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
            title.text = "동료";
            title.color = DimColor;

            var balance = CreateLabel(go.transform, font, "GemBalance", TextAlignmentOptions.Right);
            var balanceRect = (RectTransform)balance.transform;
            balanceRect.anchorMin = new Vector2(0.4f, 0f);
            balanceRect.anchorMax = new Vector2(1f, 1f);
            balanceRect.offsetMin = Vector2.zero;
            balanceRect.offsetMax = new Vector2(-24f - 48f - 10f, 0f);
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

            var hud = go.AddComponent<Onikiri.UI.HUDGems>();
            var so = new SerializedObject(hud);
            so.FindProperty("label").objectReferenceValue = balance;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 잠긴 미리보기의 해금 조건 배너(41단계). 세 패널 중 여기가 가장
            // 필요하다 - PetRow는 잠긴 이유를 행에 적지 않는다("잠김"뿐).
            // 조건은 탭과 같은 출처(PetCurve)다
            LockBannerBuilder.Build(go.transform, font,
                                    PetCurve.UnlockStage + "스테이지 도달 시 해금",
                                    1, PetCurve.UnlockStage);
        }

        // ---------------------------------------------------------------- 카드

        private static void BuildCard(RectTransform content, PetSystem system,
                                      TMP_FontAsset font, int index)
        {
            var spec = PetCatalog.Pets[index];

            var go = new GameObject("Pet" + index, typeof(RectTransform));
            go.transform.SetParent(content, false);

            // 좌우 여백은 뷰포트가 이미 가진다. 카드는 Content 폭을 다 쓴다
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, CardHeight);
            rect.anchoredPosition = new Vector2(0f, -(index * (CardHeight + CardGap)));

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            // 초상. 캐릭터 스프라이트에는 틴트를 곱하지 않는다 - 멀티플라이
            // 틴트가 픽셀아트 팔레트를 통째로 물들인다. 잠금 표현(실루엣)은
            // PetRow가 색으로 굴린다
            var icon = CreatePortrait(go.transform, PetContentBuilder.PortraitOf(spec.Id));

            // 카드 글자는 전부 캡션 크기(39단계 - 캐릭터 화면과 같은 위계).
            // 44pt로 남는 것은 동작 버튼의 제목뿐이다. 본문 세 줄은 전부
            // 버튼 왼쪽(TextRight)에서 끝난다 - 오른쪽 칸은 버튼의 것이다
            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            UiFonts.Demote(nameLabel);
            PlaceStretched((RectTransform)nameLabel.transform, TextLeft, TextRight, 14f, LineHeight);
            nameLabel.text = spec.Name;

            var roleLabel = CreateLabel(go.transform, font, "Role", TextAlignmentOptions.Left);
            UiFonts.Demote(roleLabel);
            PlaceStretched((RectTransform)roleLabel.transform, TextLeft, TextRight,
                           14f + LineHeight, LineHeight);
            roleLabel.color = DimColor;
            roleLabel.text = spec.Role;

            // 셋째 줄이 이 카드의 주인공이다 - 얻는 것(DPS)이 왼쪽에 밝게,
            // 진행(레벨)이 오른쪽에 흐리게. 잠긴 미리보기에서도 이 줄은
            // 또렷해야 한다 - "빨리 저기까지 가고 싶다"를 만드는 것이 숫자다
            var bonusLabel = CreateLabel(go.transform, font, "Bonus", TextAlignmentOptions.Left);
            UiFonts.Demote(bonusLabel);
            PlaceStretched((RectTransform)bonusLabel.transform, TextLeft, TextRight,
                           14f + LineHeight * 2f, LineHeight);
            bonusLabel.text = "DPS +?";

            // 레벨(진행)은 이름 줄 오른쪽이다. 셋째 줄에 DPS와 같이 두면
            // 전후값("+7.0% → +7.6%")이 길어서 서로 붙는다 - 실측으로 물렸다
            var levelLabel = CreateLabel(go.transform, font, "Level", TextAlignmentOptions.Right);
            UiFonts.Demote(levelLabel);
            PlaceStretched((RectTransform)levelLabel.transform, TextLeft, TextRight,
                           14f, LineHeight);
            levelLabel.color = DimColor;
            levelLabel.text = "잠김";

            // 버튼 하나 - 해금(보석) 또는 레벨업(골드). 출전 버튼은 없다:
            // 보유 동료 전원이 출전하므로 선택할 것이 없다. 자리는 강화 행의
            // 비용 칸처럼 **오른쪽의 컴팩트한 칸**이다(41b) - 풀폭 버튼은
            // 카드를 지배해서 본문이 버튼의 장식으로 읽혔다
            TMP_Text actionTitle, actionCost;
            Image actionImage;
            var actionButton = BuildActionButton(go.transform, font, "Action",
                                                 UiSkin.Chrome, "해금", "보석 " + spec.UnlockGems,
                                                 out actionTitle, out actionCost, out actionImage);

            var row = go.AddComponent<Onikiri.UI.PetRow>();
            var so = new SerializedObject(row);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("petIndex").intValue = index;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("rowBackground").objectReferenceValue = background;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("roleLabel").objectReferenceValue = roleLabel;
            so.FindProperty("bonusLabel").objectReferenceValue = bonusLabel;
            so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            so.FindProperty("actionButton").objectReferenceValue = actionButton;
            so.FindProperty("actionBackground").objectReferenceValue = actionImage;
            so.FindProperty("actionTitle").objectReferenceValue = actionTitle;
            so.FindProperty("actionCost").objectReferenceValue = actionCost;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("normalRowTint").colorValue = UiSkin.Row;
            so.FindProperty("activeRowTint").colorValue = ActiveRowTint;
            so.FindProperty("goldButtonTint").colorValue = UiSkin.Chrome;
            so.FindProperty("gemButtonTint").colorValue = GemButtonTint;
            so.FindProperty("lockedIconTint").colorValue = new Color(0.12f, 0.10f, 0.16f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 카드 오른쪽의 컴팩트 동작 버튼. 상태(해금/레벨업)는 PetRow가 다시 그린다.
         *
         * 장비 카드의 두 줄 버튼(제목 44 + 비용 33)과 같은 문법이고, 폭만
         * 강화 행의 비용 칸(300)이다. 세 하단 화면(강화/장비/동료)의 "누르는
         * 곳"이 전부 같은 모양이어야 손이 화면마다 새로 배우지 않는다.
         */
        private static Button BuildActionButton(Transform parent, TMP_FontAsset font, string name,
                                                Color tint, string title, string cost,
                                                out TMP_Text titleLabel, out TMP_Text costLabel,
                                                out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-(24f + ActionWidth), ActionInset);
            rect.offsetMax = new Vector2(-24f, -ActionInset);

            image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, tint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            // 동작(해금·레벨업)은 44pt, 비용은 캡션(39단계 - 장비 버튼과 같은 위계)
            titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Center);
            PlaceStretched((RectTransform)titleLabel.transform, 8f, 8f, 12f, 48f);
            titleLabel.text = title;

            costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Center);
            UiFonts.Demote(costLabel);
            PlaceStretched((RectTransform)costLabel.transform, 8f, 8f, 12f + 48f, LineHeight);
            costLabel.color = DimColor;
            costLabel.text = cost;

            return button;
        }

        // ---------------------------------------------------------------- 조각

        private static Image CreatePortrait(Transform parent, Sprite sprite)
        {
            // 마스크 칸. 캔버스의 빈 여백이 아무리 넓어도 여기서 잘린다
            var maskGo = new GameObject("IconMask", typeof(RectTransform));
            maskGo.transform.SetParent(parent, false);

            var maskRect = (RectTransform)maskGo.transform;
            maskRect.anchorMin = new Vector2(0f, 0.5f);
            maskRect.anchorMax = new Vector2(0f, 0.5f);
            maskRect.pivot = new Vector2(0f, 0.5f);
            maskRect.sizeDelta = new Vector2(PortraitBox, PortraitBox);
            maskRect.anchoredPosition = new Vector2(IconLeft, 0f);
            maskGo.AddComponent<UnityEngine.UI.RectMask2D>();

            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(maskGo.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;

            // 스프라이트가 없으면 자홍색 사각형이 남는다 - 빠진 초상이 눈에도
            // 드러나야 한다. 슬라이싱 전에 패널을 돌린 경우가 여기 걸린다
            if (sprite == null)
            {
                image.color = new Color(1f, 0f, 1f, 0.35f);
                rect.sizeDelta = new Vector2(PortraitBox, PortraitBox);
                return image;
            }

            var drawn = MeasureDrawnRect(sprite);
            if (drawn.z <= 0f)
            {
                // 실측 실패 - 예전 방식으로라도 그린다. 콩알이 빈 칸보다 낫다
                image.preserveAspect = true;
                rect.sizeDelta = new Vector2(PortraitBox, PortraitBox);
                return image;
            }

            // 캔버스 전체를 2배로 세우고, 그려진 픽셀의 중심이 칸 중앙에
            // 오도록 민다. 이미지가 칸보다 훨씬 커도 마스크가 지운다
            var cell = sprite.rect;
            var imageSize = new Vector2(cell.width, cell.height) * PortraitScale;
            rect.sizeDelta = imageSize;
            rect.anchoredPosition = new Vector2((0.5f - drawn.x) * imageSize.x,
                                                (0.5f - drawn.y) * imageSize.y);

            return image;
        }

        /**
         * @brief 셀 안에서 그려진 픽셀의 (중심X01, 중심Y01, 폭px). 폭 0 이하 = 실패.
         *
         * 임포트된 텍스처는 CPU에서 못 읽으므로 원본 PNG를 직접 읽는다 -
         * UpgradePanelBuilder.MeasurePortraitBounds와 같은 방법인데, 그쪽은
         * 발선·머리선(세로)만 재고 여기는 **가로 중심까지** 필요해서 따로 잰다
         * (동물 팩은 옆으로 길어서 가로 여백이 세로보다 크다).
         */
        private static Vector3 MeasureDrawnRect(Sprite sprite)
        {
            var missing = new Vector3(0f, 0f, -1f);
            if (sprite == null || sprite.texture == null) return missing;

            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(path)) return missing;

            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!readable.LoadImage(bytes)) return missing;

                var cell = sprite.rect;
                int x0 = Mathf.Clamp((int)cell.x, 0, readable.width);
                int y0 = Mathf.Clamp((int)cell.y, 0, readable.height);
                int w = Mathf.Clamp((int)cell.width, 0, readable.width - x0);
                int h = Mathf.Clamp((int)cell.height, 0, readable.height - y0);
                if (w <= 0 || h <= 0) return missing;

                var pixels = readable.GetPixels(x0, y0, w, h);
                Object.DestroyImmediate(readable);

                int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (pixels[y * w + x].a <= 0.03f) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }

                if (maxX < 0) return missing;

                return new Vector3((minX + maxX + 1) * 0.5f / w,
                                   (minY + maxY + 1) * 0.5f / h,
                                   maxX - minX + 1);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Onikiri] Could not measure pet portrait in " + path + ": " + e.Message);
                return missing;
            }
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
