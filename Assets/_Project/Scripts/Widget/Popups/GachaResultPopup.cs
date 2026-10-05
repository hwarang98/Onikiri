using System;
using System.Collections.Generic;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 뽑기 결과를 **타일 그리드**로 펼친다 (69단계).
     *
     * ## 46~68단계의 줄 목록에서 무엇이 바뀌었나
     *
     * 결과가 글자 줄이었다 - "파편 6", "혼 정수 → 처형인의 혼". 열 줄이 같은
     * 모양이라 흘깃 볼 때 **어느 칸이 좋은 결과인가**를 글자를 읽어야 알았다.
     * 타일은 그것을 모양으로 말한다: 테두리 색이 등급이고, ★4·★5는 타일 뒤에
     * 빛이 돈다. 글자는 모서리 두 칸(등급명 · 보조값)으로 줄었다.
     *
     * 한 판에 다 보여준다는 46단계 규칙과 0.7초 열림 연출(#12)은 그대로다 -
     * 열리는 것이 줄에서 타일로 바뀌었을 뿐이다.
     *
     * ## 화면 전체를 덮는다 - 상단 바만 남긴다
     *
     * 결과 판이 상점 띠 안의 카드였을 때는 그 아래 전투 화면이 보였다. 이제
     * 판은 SafeArea의 0 ~ BattleAreaTop을 덮고 상단 바(보석 잔액)만 남긴다 -
     * 재뽑기 버튼이 판 위에 있으므로 "한 번 더 돌릴 수 있는가"의 답(잔액)이
     * 같은 화면에 보여야 한다.
     *
     * ## 판이 스스로 닫히지 않는다
     *
     * 자동으로 사라지면 10연의 결과가 **뭐였는지 모르는 채로** 지나간다.
     * 확인 버튼 하나를 두는 이유이고, 오프라인 보상 팝업(OfflineRewardPopup)이
     * 같은 규칙을 쓴다. 닫히는 순간 상점 배너의 레벨업 연출이 이어진다
     * (Closed - ShopPanel이 듣는다).
     */
    public sealed class GachaResultPopup : MonoBehaviour
    {
        /** 타일 하나의 조각들. 빌더가 짓고 배선한다 */
        [Serializable]
        public sealed class Tile
        {
            public RectTransform root;

            /** 테두리. 등급 색이다 */
            public Image frame;

            /** 안쪽 판. 등급 색을 눌러 어둡게 - 아이콘이 테두리보다 앞에 읽힌다 */
            public Image inner;

            public Image icon;

            /** 좌상단 등급명 */
            public TMP_Text grade;

            /** 우하단 보조값 (파편 수 · 혼격 ★ · 오의 이름 · +XP) */
            public TMP_Text value;

            /** 타일 뒤의 빛. ★4 보라 · ★5 금. 그 아래 등급은 꺼진다 */
            public Image glow;
        }

        [Tooltip("켜고 끄는 판. 이 컴포넌트는 항상 켜진 뿌리에 산다")]
        [SerializeField] private GameObject visual;

        /** 그리드 위 한 줄. 합계 + ★3 이상 개수, 공개가 끝나면 레벨업 꼬리가 붙는다 */
        [SerializeField] private TMP_Text titleLabel;

        /**
         * 제목이 한 줄 폭을 넘을 때 내려 쓰는 캡션 글꼴(33) - 70단계. 최장형
         * "파편 278 · 희귀 1 · 영웅 2 · 전설 1 · 소환 Lv.8 달성"이 44pt로 1037px라
         * 레벨 · 개수가 두 자리가 되면 넘친다(실기). 비면 내리지 않는다
         */
        [SerializeField] private TMP_FontAsset titleCaptionFont;

        /** 판 자체(딤)의 버튼. 열리는 중에 누르면 남은 타일을 전부 연다 - 70단계 */
        [SerializeField] private Button overlayButton;

        [SerializeField] private RectTransform grid;
        [SerializeField] private Tile[] tiles = new Tile[0];

        /** 그리드 아래 줄. 미끄러진 결과의 경로("등급 하락 · …")를 적는다 */
        [SerializeField] private TMP_Text[] notes = new TMP_Text[0];

        [SerializeField] private Button confirmButton;

        [Header("재뽑기 (10회)")]
        [SerializeField] private Button repullButton;
        [SerializeField] private Image repullBackground;
        [SerializeField] private TMP_Text repullTitle;
        [SerializeField] private TMP_Text repullCost;
        [SerializeField] private Image repullGem;
        [SerializeField] private Color repullTint = new Color(0.42f, 0.56f, 1.00f, 1f);

        [Header("색")]
        [SerializeField] private Color textColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color goldColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        /**
         * @brief 등급 다섯 칸의 색 (47단계). 빌더가 UiSkin.Grades에서 옮겨 적는다.
         *
         * 스크립트 기본값이 아니라 빌더가 쓰는 이유는 이 프로젝트의 규칙이다 -
         * 컴포넌트가 이미 씬에 있으면 스크립트 기본값을 고쳐도 반영되지 않는다.
         * 배열이 비어 있으면 46단계의 색으로 떨어진다(ColorOf).
         */
        [SerializeField] private Color[] gradeColors = new Color[0];

        /** 안쪽 판 = 등급 색 x 이 값 (RGB만). 테두리와 같은 계열로 어둡게 */
        [SerializeField] private float innerShade = 0.22f;

        [SerializeField] private Color glowEpic = new Color(0.78f, 0.30f, 1.00f, 0.85f);
        [SerializeField] private Color glowLegendary = new Color(1.00f, 0.80f, 0.25f, 0.95f);

        [Tooltip("빛이 도는 속도(도/초). 실시간이다")]
        [SerializeField] private float glowSpinDegreesPerSecond = 24f;

        [Header("그리드 치수 - 빌더가 옮겨 적는다")]
        [SerializeField] private float tileSize = 160f;
        [SerializeField] private float tileGap = 32f;
        [SerializeField] private int columns = 5;

        [Header("아이콘 - 빌더가 옮겨 적는다")]
        [SerializeField] private Sprite shardSprite;
        [SerializeField] private Sprite soulSprite;

        /** YodoCatalog 순서. GachaSystem.PullResult.BladeIndex가 이 인덱스다 */
        [SerializeField] private Sprite[] bladeSprites = new Sprite[0];

        /** LegendaryYodoCatalog 순서 */
        [SerializeField] private Sprite[] legendarySprites = new Sprite[0];

        /** SkillCatalog 순서. 해금·개안 결과의 인덱스가 이것이다 */
        [SerializeField] private Sprite[] skillSprites = new Sprite[0];

        [SerializeField] private Sprite skillXpSprite;

        /** 이 판이 닫혔다. 상점이 레벨업 연출을 이어서 재생한다 */
        public event Action Closed;

        /** 재뽑기 버튼(10회)을 눌렀다. 어느 배너인지는 판을 연 상점이 안다 */
        public event Action RepullRequested;

        // ---------------------------------------------------------------- 판의 상태

        private int count;
        private string headline = string.Empty;

        /** 열리는 동안의 제목. 합계를 먼저 쓰면 타일이 열리기 전에 결과를 말해 버린다(70단계 실기) */
        public const string RevealingText = "소환 중...";

        private List<string> pendingNotes;
        private TMP_FontAsset titleFont;
        private float titleFontSize;
        private int summonLevelUp;
        private int[] tileGrades;
        private Color[] tileColors;
        private string[] tileValues;

        public bool IsOpen { get { return visual != null && visual.activeSelf; } }

        /** 이번 판에 실제로 선 타일 수 */
        public int VisibleTileCount { get { return count; } }

        /** 열림 연출 중인가 */
        public bool IsRevealing { get { return Revealing; } }

        /** 타일 i의 등급 (GachaCurve.Grade). 검사가 읽는다 */
        public int TileGrade(int index)
        {
            return tileGrades != null && index >= 0 && index < count ? tileGrades[index] : -1;
        }

        /** 타일 i의 테두리 색 */
        public Color TileFrameColor(int index)
        {
            return tileColors != null && index >= 0 && index < count ? tileColors[index] : Color.clear;
        }

        /** 타일 i의 우하단 보조값 */
        public string TileValue(int index)
        {
            return tileValues != null && index >= 0 && index < count ? tileValues[index] : string.Empty;
        }

        // ---------------------------------------------------------------- 열림 연출 (#12)

        /**
         * @brief 타일이 **하나씩 열린다.** 대신 아주 빠르게.
         *
         * 46단계 머리 주석의 "하나씩 여는 연출은 이 게임에 맞지 않는다 - 10연이
         * 30초짜리 사건이 되면 다음에 또 하고 싶지 않아진다"는 판단은 지금도
         * 옳다. 여기서 하는 것은 **0.7초짜리**다 - 열 타일이 70ms 간격으로 켜지고
         * 끝난다. 흘깃 보는 시간 안에 끝나면서, 눈이 타일을 따라가고 빛나는
         * 타일이 **언제** 나오는지가 사건이 된다.
         *
         * 연출 중에 확인을 누르면 닫히는 대신 **전부 열린다**(RevealAll).
         */
        [Header("열림 연출 (#12)")]
        [Tooltip("타일과 타일 사이(초). 실시간이다 - 히트스톱이 걸려도 같은 속도로 열린다")]
        [SerializeField] private float revealInterval = 0.07f;

        [Tooltip("한 타일이 제 크기로 내려앉는 시간(초)")]
        [SerializeField] private float revealPunchSeconds = 0.16f;

        /**
         * @brief 등급별 튀어나오는 배율. 높을수록 크게 튄다 (#12).
         *
         * ★1~★2는 거의 안 튄다(1.10). 열 타일 중 여덟이 그것이라 크게 튀면
         * 판 전체가 덜컹거리고, 그러면 정작 빛나는 타일이 튀는 것이 안 보인다 -
         * 화려함은 **대비**이지 총량이 아니다.
         */
        [SerializeField] private float[] revealPunchByGrade = { 1.10f, 1.12f, 1.22f, 1.45f, 1.75f };

        [Tooltip("★3 이상이 열릴 때 안쪽 판이 흰색에서 제 색으로 물든다. 0이면 안 쓴다")]
        [SerializeField] private float revealFlashSeconds = 0.12f;

        private int revealed;
        private float nextRevealAt;
        private List<int> shrinking;
        private float[] shrinkStart;

        private bool Revealing { get { return nextRevealAt > 0f; } }

        // ---------------------------------------------------------------- 배치

        /**
         * @brief count개 타일의 중심 좌표(그리드 중심 기준). **줄마다 가운데 정렬.**
         *
         * 1회면 한 타일이 한가운데, 10회면 다섯씩 두 줄. 줄 수가 결과 수에서
         * 나오므로 상수가 아니다 - 47단계가 배너 높이를 식으로 바꾼 규칙과 같다.
         * 검사(GachaUiLayoutTests)가 같은 함수를 부른다.
         */
        public static Vector2[] TileLayout(int count, int columns, float tile, float gap)
        {
            if (count <= 0 || columns <= 0) return new Vector2[0];

            var centers = new Vector2[count];
            int rows = (count + columns - 1) / columns;
            float step = tile + gap;
            float top = (rows - 1) * step * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int inRow = Mathf.Min(columns, count - row * columns);
                int column = i % columns;
                float left = -(inRow - 1) * step * 0.5f;
                centers[i] = new Vector2(left + column * step, top - row * step);
            }
            return centers;
        }

        /** 그리드 전체의 높이 */
        public static float GridHeight(int count, int columns, float tile, float gap)
        {
            if (count <= 0 || columns <= 0) return 0f;
            int rows = (count + columns - 1) / columns;
            return rows * tile + (rows - 1) * gap;
        }

        public int Columns { get { return columns; } }

        // ---------------------------------------------------------------- 그리기

        private void EnsureScratch()
        {
            int n = tiles != null ? tiles.Length : 0;
            if (tileGrades != null && tileGrades.Length == n) return;

            tileGrades = new int[n];
            tileColors = new Color[n];
            tileValues = new string[n];
            shrinkStart = new float[n];
        }

        private void SetTile(int index, int grade, Sprite icon, string value)
        {
            var tile = tiles[index];
            var color = ColorOf((GachaCurve.Grade)grade);

            tileGrades[index] = grade;
            tileColors[index] = color;
            tileValues[index] = value;

            if (tile.frame != null) tile.frame.color = color;
            if (tile.inner != null) tile.inner.color = Shade(color);

            if (tile.icon != null)
            {
                tile.icon.sprite = icon;
                tile.icon.enabled = icon != null;
                tile.icon.preserveAspect = true;
            }

            if (tile.grade != null)
            {
                tile.grade.text = GachaCurve.GradeNames[Mathf.Clamp(grade, 0, GachaCurve.GradeCount - 1)];
                tile.grade.color = color;
            }

            if (tile.value != null)
            {
                tile.value.text = value;
                tile.value.color = grade >= (int)GachaCurve.Grade.Epic ? goldColor : textColor;
            }

            // 빛은 열릴 때 켠다(RevealNext). 지금은 꺼 둔다
            if (tile.glow != null) tile.glow.enabled = false;
        }

        private Color Shade(Color color)
        {
            var shaded = color;
            shaded.r *= innerShade; shaded.g *= innerShade; shaded.b *= innerShade;
            shaded.a = 1f;
            return shaded;
        }

        private void Render(int shown, string head, List<string> noteLines, int levelUp)
        {
            count = shown;
            headline = head;
            summonLevelUp = levelUp;

            var centers = TileLayout(count, columns, tileSize, tileGap);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null || tile.root == null) continue;

                bool on = i < count;
                tile.root.gameObject.SetActive(on);
                if (!on) continue;

                tile.root.anchoredPosition = centers[i];
                tile.root.sizeDelta = new Vector2(tileSize, tileSize);
            }

            // 등급 하락 줄도 결과다 - 마지막 타일이 열린 뒤에 쓴다(70단계)
            pendingNotes = noteLines;
            WriteNotes(null);

            // 합계와 꼬리(소환 Lv.n 달성)는 **마지막 타일이 열린 뒤에** 쓴다
            // (69단계 결정 4 + 70단계 - 그 전에 쓰면 타일이 열리기 전에 결과가 보인다)
            if (titleLabel != null)
            {
                SetTitle(RevealingText);
                titleLabel.color = textColor;
            }

            visual.SetActive(true);
            visual.transform.SetAsLastSibling();

            BeginReveal();
        }

        private void BeginReveal()
        {
            revealed = 0;
            if (shrinking != null) shrinking.Clear();

            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] != null && tiles[i].root != null) tiles[i].root.localScale = Vector3.zero;

            // 첫 타일은 다음 프레임에 바로 연다. 첫 타일까지 기다리게 하면
            // 판이 빈 채로 떠 있는 순간이 생기고, 그것은 고장으로 읽힌다
            nextRevealAt = count > 0 ? Time.unscaledTime : 0f;
            if (count == 0) FinishReveal();
        }

        private void Update()
        {
            if (!Revealing) return;

            // unscaled다. 히트스톱이 timeScale을 0으로 붙드는 게임이라
            // 스케일 시간으로 재면 뽑기 연출이 전투 타이밍에 따라 늘어난다
            while (Revealing && Time.unscaledTime >= nextRevealAt)
            {
                RevealNext();
                if (revealed >= count) { FinishReveal(); break; }
                nextRevealAt += revealInterval;
            }
        }

        private void FinishReveal()
        {
            nextRevealAt = 0f;
            if (titleLabel != null) SetTitle(WithLevelUp(headline, summonLevelUp));
            WriteNotes(pendingNotes);
        }

        private void WriteNotes(List<string> lines)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                if (notes[i] == null) continue;
                notes[i].text = lines != null && i < lines.Count ? lines[i] : string.Empty;
                notes[i].color = dimColor;
            }
        }

        /**
         * 제목 한 줄. 44pt로 폭을 넘으면 캡션(33)으로 내린다 - 래스터 글꼴이라
         * 크기를 줄이지 않고 **구운 다른 배수의 아틀라스로 갈아 끼운다**(PixelFontSizes)
         */
        private void SetTitle(string text)
        {
            if (titleFont == null) { titleFont = titleLabel.font; titleFontSize = titleLabel.fontSize; }

            titleLabel.font = titleFont;
            titleLabel.fontSize = titleFontSize;
            titleLabel.text = text;

            if (titleCaptionFont == null) return;
            float room = titleLabel.rectTransform.rect.width;
            if (room > 0f && titleLabel.GetPreferredValues(text).x > room)
            {
                titleLabel.font = titleCaptionFont;
                titleLabel.fontSize = Onikiri.UI.PixelFontSizes.GalmuriCaption;
            }
        }

        /** 제목이 지금 캡션으로 내려가 있는가 (검사용) */
        public bool TitleDemoted { get { return titleLabel != null && titleCaptionFont != null && titleLabel.font == titleCaptionFont; } }

        /** 지금 제목 글자 (검사용) */
        public string TitleText { get { return titleLabel != null ? titleLabel.text : null; } }

        /** 딤 탭. 열리는 중이면 전부 연다 - 다 열린 뒤에는 아무 일도 안 한다(닫기는 확인 버튼) */
        private void OnOverlay()
        {
            if (Revealing) RevealAll();
        }

        /**
         * @brief 다음 타일 하나를 연다. 등급이 셀수록 크게 튄다.
         *
         * 코루틴을 쓰지 않는다 - 판이 꺼졌다 켜지는 사이에 코루틴이 살아
         * 있으면 다음 판의 타일을 지난 판의 연출이 건드린다.
         */
        private void RevealNext()
        {
            int index = revealed++;
            if (index < 0 || index >= tiles.Length || tiles[index] == null) return;

            var tile = tiles[index];
            if (tile.root != null) tile.root.localScale = Vector3.one * PunchOf(index);

            if (tile.glow != null)
            {
                int grade = tileGrades[index];
                tile.glow.enabled = grade >= (int)GachaCurve.Grade.Epic;
                tile.glow.color = grade >= (int)GachaCurve.Grade.Legendary ? glowLegendary : glowEpic;
            }

            // ★3 이상은 안쪽 판이 흰빛에서 제 색으로 물든다. 아래 등급까지 물들이면
            // 열 타일이 다 반짝여서 무엇이 좋은 결과인지가 안 보인다
            if (revealFlashSeconds > 0f && tile.inner != null && tileGrades[index] >= (int)GachaCurve.Grade.Rare)
                tile.inner.color = Color.white;

            if (shrinking == null) shrinking = new List<int>();
            shrinkStart[index] = Time.unscaledTime;
            if (!shrinking.Contains(index)) shrinking.Add(index);
        }

        private float PunchOf(int index)
        {
            int grade = tileGrades != null && index < tileGrades.Length ? tileGrades[index] : 0;
            if (revealPunchByGrade == null || grade < 0 || grade >= revealPunchByGrade.Length) return 1.15f;
            return revealPunchByGrade[grade];
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;

            // 빛은 천천히 돈다 - 켜진 것만
            float spin = glowSpinDegreesPerSecond * Time.unscaledDeltaTime;
            for (int i = 0; i < count && i < tiles.Length; i++)
                if (tiles[i] != null && tiles[i].glow != null && tiles[i].glow.enabled)
                    tiles[i].glow.rectTransform.Rotate(0f, 0f, -spin);

            if (shrinking == null || shrinking.Count == 0) return;

            for (int n = shrinking.Count - 1; n >= 0; n--)
            {
                int index = shrinking[n];
                var tile = index < tiles.Length ? tiles[index] : null;
                if (tile == null || tile.root == null) { shrinking.RemoveAt(n); continue; }

                float t = revealPunchSeconds <= 0f ? 1f : (Time.unscaledTime - shrinkStart[index]) / revealPunchSeconds;
                if (t >= 1f)
                {
                    tile.root.localScale = Vector3.one;
                    if (tile.inner != null) tile.inner.color = Shade(tileColors[index]);
                    shrinking.RemoveAt(n);
                    continue;
                }

                tile.root.localScale = Vector3.one * Mathf.Lerp(PunchOf(index), 1f, t);

                // 흰빛에서 제 색으로. 크기보다 빨리 끝난다 - 색이 오래
                // 남아 있으면 등급 색이 헷갈린다
                if (revealFlashSeconds > 0f && tile.inner != null)
                {
                    float ct = (Time.unscaledTime - shrinkStart[index]) / revealFlashSeconds;
                    if (ct < 1f && tileGrades[index] >= (int)GachaCurve.Grade.Rare)
                        tile.inner.color = Color.Lerp(Color.white, Shade(tileColors[index]), ct);
                }
            }
        }

        /** 남은 타일을 그 자리에서 전부 연다. 연출을 못 기다리는 순간을 위한 문 */
        public void RevealAll()
        {
            while (revealed < count) RevealNext();
            FinishReveal();
        }

        private void Start()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (repullButton != null) repullButton.onClick.AddListener(OnRepull);
            if (overlayButton != null) overlayButton.onClick.AddListener(OnOverlay);
            if (visual != null && visual.activeSelf) Close();
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
            if (repullButton != null) repullButton.onClick.RemoveListener(OnRepull);
            if (overlayButton != null) overlayButton.onClick.RemoveListener(OnOverlay);
        }

        /**
         * @brief 확인 버튼. **연출 중이면 닫지 않고 전부 연다** (#12).
         *
         * 첫 탭은 건너뛰기, 두 번째 탭이 닫기다 - 뽑기 화면의 표준이고,
         * 0.7초라 두 번 누를 일도 드물다.
         */
        private void OnConfirm()
        {
            if (Revealing) { RevealAll(); return; }
            Close();
        }

        /** 재뽑기. 연출 중이면 먼저 다 연다 - 확인과 같은 규칙 */
        private void OnRepull()
        {
            if (Revealing) { RevealAll(); return; }
            var handler = RepullRequested;
            if (handler != null) handler();
        }

        public void Close()
        {
            nextRevealAt = 0f;
            if (shrinking != null) shrinking.Clear();

            if (tiles != null)
                foreach (var tile in tiles)
                    if (tile != null && tile.root != null) tile.root.localScale = Vector3.one;

            bool wasOpen = IsOpen;
            if (visual != null) visual.SetActive(false);

            if (wasOpen)
            {
                var handler = Closed;
                if (handler != null) handler();
            }
        }

        /**
         * @brief 재뽑기 버튼의 상태. 상점이 Refresh마다 부른다 - 같은 지갑 규칙(CanPull).
         *
         * 못 사면 판을 죽인다. RGB만 곱한다 - 스칼라 곱은 알파까지 눌러 판을
         * 반투명으로 만든다(41단계에서 물린 자리).
         */
        public void SetRepull(bool affordable, int cost)
        {
            if (repullButton != null) repullButton.interactable = affordable;
            if (repullCost != null)
            {
                repullCost.text = cost.ToString();
                repullCost.color = affordable ? textColor : dimColor;
            }
            if (repullTitle != null) repullTitle.color = affordable ? textColor : dimColor;
            if (repullBackground != null)
            {
                var tint = repullTint;
                if (!affordable) { tint.r *= 0.55f; tint.g *= 0.55f; tint.b *= 0.55f; tint.a = 1f; }
                repullBackground.color = tint;
            }
        }

        // ---------------------------------------------------------------- 요도 뽑기

        public void Show(List<GachaSystem.PullResult> results, YodoSystem yodo)
        {
            Show(results, yodo, 0);
        }

        /**
         * @param summonLevelUp 이 뽑기 묶음이 닿은 소환 레벨. 0이면 안 올랐다
         *                      (GachaSystem.LastBatchLevelUp). 오르면 마지막 타일이
         *                      열린 뒤 제목 줄 끝에 "소환 Lv.n 달성"이 붙는다
         */
        public void Show(List<GachaSystem.PullResult> results, YodoSystem yodo, int summonLevelUp)
        {
            if (visual == null || tiles == null || results == null) return;
            EnsureScratch();

            int shards = 0;
            var counts = new int[GachaCurve.GradeCount];
            var noteLines = new List<string>();
            int shown = Mathf.Min(results.Count, tiles.Length);

            for (int i = 0; i < shown; i++)
            {
                var result = results[i];
                shards += result.Shards;

                int grade = (int)GachaCurve.GradeFor(result.Outcome);
                counts[grade]++;

                SetTile(i, grade, YodoIcon(result), YodoValue(result, yodo));

                // 미끄러진 결과는 그리드 아래에 경로를 적는다 - 타일은 도착한
                // 것을 보여주고, 무엇이 무엇이 됐는지는 이 줄이 말한다
                // (44단계의 "버려지는 드랍 0"을 문구로 지키는 자리)
                if (result.Downgraded) noteLines.Add(DowngradePrefix + TextFor(result, yodo));
            }

            Render(shown, Headline(shards, counts), TrimNotes(noteLines), summonLevelUp);
        }

        /** 미끄러짐 줄의 머리. 47단계의 "천장!" 자리 - 68단계에 천장이 사라졌다 */
        public const string DowngradePrefix = "등급 하락 · ";

        private Sprite YodoIcon(GachaSystem.PullResult result)
        {
            switch (result.Outcome)
            {
                case GachaCurve.Outcome.SoulEssence:
                    return result.BladeIndex >= 0 ? soulSprite : shardSprite;

                case GachaCurve.Outcome.SoulRarity:
                    if (result.BladeIndex >= 0 && result.BladeIndex < bladeSprites.Length)
                        return result.Downgraded ? soulSprite : bladeSprites[result.BladeIndex];
                    return shardSprite;

                case GachaCurve.Outcome.LegendaryBlade:
                    return result.LegendaryIndex >= 0 && result.LegendaryIndex < legendarySprites.Length
                        ? legendarySprites[result.LegendaryIndex] : shardSprite;

                default:
                    return shardSprite;
            }
        }

        /**
         * @brief 우하단 보조값. **짧다** - 타일 160px의 모서리 한 칸이다.
         *
         * 파편이면 "+수", 혼 정수면 "혼 +1", 혼격이면 올라간 뒤의 "★n", 전설이면
         * "획득" / "돌파 n". 긴 문장은 그리드 아래 노트 줄이 맡는다.
         */
        private static string YodoValue(GachaSystem.PullResult result, YodoSystem yodo)
        {
            switch (result.Outcome)
            {
                case GachaCurve.Outcome.SoulEssence:
                    return result.BladeIndex >= 0 ? "혼 +1" : "+" + result.Shards;

                case GachaCurve.Outcome.SoulRarity:
                {
                    if (result.Downgraded)
                        return result.BladeIndex >= 0 ? "혼 +1" : "+" + result.Shards;
                    var blade = yodo != null ? yodo.GetBlade(result.BladeIndex) : null;
                    return blade != null ? "★" + blade.rarity : "★";
                }

                case GachaCurve.Outcome.LegendaryBlade:
                {
                    if (result.LegendaryIndex < 0) return "+" + result.Shards;
                    var blade = yodo != null ? yodo.GetLegendary(result.LegendaryIndex) : null;
                    if (blade == null) return "획득";
                    return blade.copies <= 1 ? "획득" : "돌파 " + blade.Breakthrough;
                }

                default:
                    return "+" + result.Shards;
            }
        }

        /** 노트 줄은 판에 있는 칸만큼. 넘치면 마지막 칸이 "외 n"이 된다 */
        private List<string> TrimNotes(List<string> lines)
        {
            int capacity = notes != null ? notes.Length : 0;
            if (lines.Count <= capacity || capacity == 0) return lines;

            var trimmed = lines.GetRange(0, capacity - 1);
            trimmed.Add("외 " + (lines.Count - capacity + 1));
            return trimmed;
        }

        /**
         * @brief 제목 줄 끝에 레벨업 한 줄을 붙인다. 안 올랐으면 그대로다.
         *
         * 문구의 출처는 곡선이다(SummonLevelCurve.LevelUpText). 69단계부터는
         * **마지막 타일이 열린 뒤에** 붙는다(FinishReveal).
         */
        public static string WithLevelUp(string headline, int summonLevelUp)
        {
            if (summonLevelUp <= 0) return headline;
            return headline + "  ·  " + SummonLevelCurve.LevelUpText(summonLevelUp);
        }

        /**
         * @brief 머리글. **합계이고, 위쪽 등급만 센다.**
         *
         * 파편 합계 + ★3 이상의 개수. ★1·★2는 전부 파편이라 이미 합계에
         * 들어 있고, 위쪽 셋은 개수가 곧 사건이다.
         */
        private static string Headline(int shards, int[] counts)
        {
            string text = "파편 " + shards;

            for (int g = (int)GachaCurve.Grade.Rare; g < counts.Length; g++)
            {
                if (counts[g] <= 0) continue;
                text += " · " + GachaCurve.GradeNames[g] + " " + counts[g];
            }
            return text;
        }

        /**
         * @brief 미끄러짐 노트의 본문. 46~68단계 결과 줄의 문장 그대로다.
         */
        private static string TextFor(GachaSystem.PullResult result, YodoSystem yodo)
        {
            switch (result.Outcome)
            {
                case GachaCurve.Outcome.SoulEssence:    return EssenceText(result, yodo, string.Empty);
                case GachaCurve.Outcome.SoulRarity:     return RarityText(result, yodo);
                case GachaCurve.Outcome.LegendaryBlade: return LegendaryText(result, yodo);
                default:                                return "파편 " + result.Shards;
            }
        }

        private static string EssenceText(GachaSystem.PullResult result, YodoSystem yodo, string prefix)
        {
            if (result.BladeIndex < 0) return prefix + "혼 정수 → 파편 " + result.Shards;

            var blade = yodo != null ? yodo.GetBlade(result.BladeIndex) : null;
            string name = blade != null ? blade.soulName : "혼";
            return prefix + "혼 정수 → " + name;
        }

        private static string RarityText(GachaSystem.PullResult result, YodoSystem yodo)
        {
            if (result.Downgraded) return EssenceText(result, yodo, "상위 혼 → ");

            var blade = yodo != null ? yodo.GetBlade(result.BladeIndex) : null;
            if (blade == null) return "상위 혼";
            return blade.bladeName + " " + YodoRarityCurve.Stars(blade.rarity);
        }

        private static string LegendaryText(GachaSystem.PullResult result, YodoSystem yodo)
        {
            if (result.LegendaryIndex < 0) return "전설 → 파편 " + result.Shards;

            var blade = yodo != null ? yodo.GetLegendary(result.LegendaryIndex) : null;
            if (blade == null) return "전설 요도";
            return blade.copies <= 1 ? blade.bladeName + " 획득!" : blade.bladeName + " 돌파 " + blade.Breakthrough;
        }

        // ---------------------------------------------------------------- 오의 뽑기 (50단계)

        public void Show(List<SkillGachaSystem.PullResult> results, SkillSystem skills)
        {
            Show(results, skills, 0);
        }

        /**
         * @brief 오의 뽑기 결과를 **같은 판에** 그린다.
         *
         * 판을 따로 만들지 않은 이유는 47단계가 등급 색과 별로 세운 것이
         * **눈금**이기 때문이다(UiSkin.Grades 주석). 타일의 색·빛은 **굴린
         * 등급**이다 - 미끄러져 XP로 떨어진 ★5도 ★5로 빛난다: 그 타일이
         * 기록하는 사건이 도착지가 아니라 출발지이기 때문이다(50b 판단 그대로).
         * 도착한 것은 아이콘과 보조값이, 경로는 노트 줄이 말한다.
         */
        public void Show(List<SkillGachaSystem.PullResult> results, SkillSystem skills, int summonLevelUp)
        {
            if (visual == null || tiles == null || results == null) return;
            EnsureScratch();

            int xp = 0;
            var counts = new int[GachaCurve.GradeCount];
            var noteLines = new List<string>();
            int shown = Mathf.Min(results.Count, tiles.Length);

            for (int i = 0; i < shown; i++)
            {
                var result = results[i];
                xp += result.Xp;

                int rolled = (int)GachaCurve.GradeOf[(int)result.Rolled];
                counts[(int)result.Grade]++;

                SetTile(i, rolled, SkillIcon(result), SkillValue(result, skills));

                if (result.Downgraded) noteLines.Add(DowngradePrefix + SkillTextFor(result, skills));
            }

            Render(shown, SkillHeadline(xp, counts), TrimNotes(noteLines), summonLevelUp);
        }

        private Sprite SkillIcon(SkillGachaSystem.PullResult result)
        {
            int index = result.Outcome == SkillGachaCurve.Outcome.Awakening ? result.AwakenedIndex
                      : result.Outcome == SkillGachaCurve.Outcome.SkillUnlock ? result.UnlockedIndex
                      : -1;
            if (index >= 0 && index < skillSprites.Length && skillSprites[index] != null) return skillSprites[index];
            return skillXpSprite;
        }

        private static string SkillValue(SkillGachaSystem.PullResult result, SkillSystem skills)
        {
            switch (result.Outcome)
            {
                case SkillGachaCurve.Outcome.Awakening:   return "개안";
                case SkillGachaCurve.Outcome.SkillUnlock: return NameOf(skills, result.UnlockedIndex);
                default:                                  return "+" + result.Xp;
            }
        }

        private static string SkillHeadline(int xp, int[] counts)
        {
            string text = "스킬 XP " + xp;

            for (int g = (int)GachaCurve.Grade.Rare; g < counts.Length; g++)
            {
                if (counts[g] <= 0) continue;
                text += " · " + GachaCurve.GradeNames[g] + " " + counts[g];
            }
            return text;
        }

        /**
         * @brief 미끄러짐 노트의 본문. 50b 결과 줄의 문장 그대로다.
         *
         * 미끄러진 칸을 **출발점부터 하나씩** 적는다 - "오의 개안 → 오의 해금 →
         * XP +240". 아래로 걸어 닿는지는 사다리가 판정한다(SlidesTo).
         */
        private static string SkillTextFor(SkillGachaSystem.PullResult result, SkillSystem skills)
        {
            string prefix = string.Empty;

            if (SkillGachaCurve.SlidesTo(result.Rolled, result.Outcome))
                for (var at = result.Rolled; at != result.Outcome; at = SkillGachaCurve.SlideFor(at))
                    prefix += NameOfOutcome(at) + " → ";

            switch (result.Outcome)
            {
                case SkillGachaCurve.Outcome.Awakening:
                    return prefix + "오의 개안 · " + NameOf(skills, result.AwakenedIndex)
                         + " Lv." + SkillCurve.MaxLevel;

                case SkillGachaCurve.Outcome.SkillUnlock:
                    return prefix + "오의 해금 · " + NameOf(skills, result.UnlockedIndex);

                default:
                {
                    string text = prefix + "XP +" + result.Xp;
                    if (result.LevelsGained > 0) text += " · Lv +" + result.LevelsGained;
                    return text;
                }
            }
        }

        /**
         * @brief 결과의 이름. **확률표와 같은 말을 쓴다.**
         *
         * 표에서 "영웅 오의 해금"으로 읽은 것이 결과 판에서 다른 이름으로
         * 뜨면 플레이어는 그 둘이 같은 것인지 알 수 없다.
         */
        public static string NameOfOutcome(SkillGachaCurve.Outcome outcome)
        {
            switch (outcome)
            {
                case SkillGachaCurve.Outcome.Awakening:   return "오의 개안";
                case SkillGachaCurve.Outcome.SkillUnlock: return "오의 해금";
                default: return "스킬 XP " + SkillGachaCurve.XpFor(outcome);
            }
        }

        private static string NameOf(SkillSystem skills, int index)
        {
            var slot = skills != null ? skills.GetSlot(index) : null;
            return slot != null ? slot.displayName : "오의";
        }

        /**
         * @brief 등급의 색. 배열이 안 배선된 씬에서는 46단계의 색으로 떨어진다.
         */
        private Color ColorOf(GachaCurve.Grade grade)
        {
            int index = (int)grade;
            if (gradeColors != null && index >= 0 && index < gradeColors.Length) return gradeColors[index];

            return grade >= GachaCurve.Grade.Rare ? goldColor
                 : grade == GachaCurve.Grade.Uncommon ? textColor
                 : dimColor;
        }
    }
}
