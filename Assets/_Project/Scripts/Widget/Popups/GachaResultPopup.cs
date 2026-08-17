using System.Collections.Generic;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 뽑기 결과를 한 판에 펼친다.
     *
     * ## 왜 한 판에 다 보여주는가 - 연출을 한 줄씩 넘기지 않는다
     *
     * 뽑기 연출의 표준은 하나씩 열어 보이는 것인데, 이 게임에서는 그것이
     * 맞지 않는다. 방치형이고 10연이 30초짜리 사건이 되면 **다음에 또 하고
     * 싶지 않아진다** - 26단계가 오의 쿨다운을 20초 안팎에 묶은 것과 같은
     * 기준(흘깃 볼 때 보여야 한다)이 여기에도 있다.
     *
     * 대신 크기로 말한다. 파편 등급 셋과 혼 정수가 색과 글자로 갈리고,
     * 잭팟과 정수만 금색이다 - 열 줄 중 금색이 몇 개인가가 이 판이 전하는
     * 유일한 정보이고, 그것은 한눈에 읽힌다.
     *
     * ## 판이 스스로 닫히지 않는다
     *
     * 자동으로 사라지면 10연의 결과가 **뭐였는지 모르는 채로** 지나간다.
     * 확인 버튼 하나를 두는 이유이고, 오프라인 보상 팝업(OfflineRewardPopup)이
     * 같은 규칙을 쓴다.
     */
    public sealed class GachaResultPopup : MonoBehaviour
    {
        [Tooltip("켜고 끄는 판. 이 컴포넌트는 항상 켜진 뿌리에 산다")]
        [SerializeField] private GameObject visual;

        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text[] lines;
        [SerializeField] private Button confirmButton;

        [SerializeField] private Color textColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color goldColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        /**
         * @brief 등급 다섯 칸의 색 (47단계). 빌더가 UiSkin.Grades에서 옮겨 적는다.
         *
         * 스크립트 기본값이 아니라 빌더가 쓰는 이유는 이 프로젝트의 규칙이다 -
         * 컴포넌트가 이미 씬에 있으면 스크립트 기본값을 고쳐도 반영되지 않는다.
         * 배열이 비어 있으면 46단계의 색으로 떨어진다(ColorFor).
         */
        [SerializeField] private Color[] gradeColors = new Color[0];

        /**
         * @brief 전설이 나왔을 때 판 전체가 물드는 색.
         *
         * ## 왜 연출이 이것 하나뿐인가
         *
         * 46단계가 정한 규칙("한 판에 다 보여준다 - 10연이 30초짜리 사건이
         * 되면 다음에 또 하고 싶지 않아진다")이 여기서도 그대로다. 전설
         * 하나 때문에 판을 한 줄씩 여는 연출로 바꾸면, 200회 중 199회는
         * 그 연출을 **전설 없이** 지나야 한다.
         *
         * 그래서 사건성은 **판의 색**에 싣는다. 여섯 줄 중 하나가 금색인
         * 것과 판 전체가 금테를 두른 것은 흘깃 볼 때 다른 화면이고, 그
         * 차이가 200회에 한 번만 나타난다.
         */
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color cardNormalTint = new Color(0.34f, 0.36f, 0.68f, 1f);
        [SerializeField] private Color cardLegendaryTint = new Color(0.62f, 0.50f, 0.22f, 1f);

        /**
         * @brief 판의 치수. **빌더가 옮겨 적는다** - 스크립트 기본값이 아니라.
         *
         * 50b에 판이 자리를 스스로 놓게 되면서(Render) 빌더의 상수 여섯이
         * 여기로 들어왔다. 빌더가 초기 배치에 쓰는 값과 판이 다시 놓는 값이
         * 두 곳에 살면 반드시 갈리고, 그 증상은 "첫 프레임과 갱신 후의 판이
         * 다르다"다.
         */
        [SerializeField] private float lineHeight = 52f;
        [SerializeField] private float topPad = 16f;
        [SerializeField] private float buttonGap = 28f;
        [SerializeField] private float confirmHeight = 84f;
        [SerializeField] private float cardSidePad = 24f;
        [SerializeField] private float outerSidePad = 48f;

        /**
         * @brief 좁은 칸의 들여쓰기. 왼쪽 정렬이 판 가장자리에 붙지 않게.
         *
         * public인 이유는 빌더의 검산 때문이다 - 좁은 칸의 실사용 폭이
         * 이만큼 줄었으므로 VerifyTextFits가 같은 값을 빼고 재야 한다.
         * 두 곳에 적으면 이 값이 움직이는 날 검산이 옛 폭을 잰다.
         */
        public const float NarrowInset = 60f;

        /** 줄 하나의 재료. Show가 채우고 Render가 놓는다 */
        private string[] rowTexts;
        private Color[] rowColors;
        private bool[] rowWide;

        /** 줄의 등급. 열리는 연출의 세기가 여기서 나온다 (#12) */
        private int[] rowGrades;

        // ---------------------------------------------------------------- 열림 연출 (#12)

        /**
         * @brief 줄이 **하나씩 열린다.** 대신 아주 빠르게.
         *
         * ## 46단계의 규칙을 어기지 않는다
         *
         * 이 판의 머리 주석은 "하나씩 열어 보이는 연출은 이 게임에 맞지
         * 않는다 - 10연이 30초짜리 사건이 되면 다음에 또 하고 싶지 않아진다"
         * 고 적어뒀고, 그 판단은 지금도 옳다. 어긴 적이 없다.
         *
         * 여기서 하는 것은 **0.7초짜리**다. 열 줄이 70ms 간격으로 켜지고
         * 끝난다 - 흘깃 보는 시간 안에 끝나므로 "기다림"이 되지 않으면서,
         * 열 줄이 한 프레임에 통째로 나타날 때는 없던 것이 생긴다: 눈이
         * 줄을 따라 내려가고, 금색 줄이 **언제** 나오는지가 사건이 된다.
         *
         * 30초 연출과 이것을 가르는 것은 방향이 아니라 시간이다.
         *
         * ## 기다릴 수 없으면 건너뛴다
         *
         * 연출 중에 확인 버튼을 누르면 닫히는 대신 **전부 열린다.** 0.7초도
         * 참지 못하는 순간이 있고(백 번째 10연), 그때 연출이 손을 막으면
         * 그것이 정확히 46단계가 피하려던 상태다.
         */
        [Header("열림 연출 (#12)")]
        [Tooltip("줄과 줄 사이(초). 실시간이다 - 히트스톱이 걸려도 같은 속도로 열린다")]
        [SerializeField] private float revealInterval = 0.07f;

        [Tooltip("한 줄이 제 크기로 내려앉는 시간(초)")]
        [SerializeField] private float revealPunchSeconds = 0.16f;

        /**
         * @brief 등급별 튀어나오는 배율. 높을수록 크게 튄다 (#12).
         *
         * 인덱스가 곧 등급이다(GachaCurve.Grade). 배열이 비어 있거나 짧으면
         * 1.15배로 떨어진다 - 배선이 빠져도 연출이 아예 없어지지는 않는다.
         *
         * ★1~★2는 거의 안 튄다(1.10). 열 줄 중 여덟이 그것이라 크게 튀면
         * 판 전체가 덜컹거리고, 그러면 정작 금색 줄이 튀는 것이 안 보인다 -
         * 화려함은 **대비**이지 총량이 아니다.
         */
        [SerializeField] private float[] revealPunchByGrade = { 1.10f, 1.12f, 1.22f, 1.45f, 1.75f };

        [Tooltip("★3 이상이 열릴 때 흰색에서 제 색으로 물든다. 0이면 안 쓴다")]
        [SerializeField] private float revealFlashSeconds = 0.12f;

        /** 지금 열고 있는 줄. count 이상이면 연출이 끝났다 */
        private int revealed;

        /** 다음 줄이 열리는 시각(unscaled). 0이면 연출 중이 아니다 */
        private float nextRevealAt;

        /** 이번 판에 실제로 그려진 줄 수 */
        private int revealCount;

        private bool Revealing { get { return nextRevealAt > 0f; } }

        private void EnsureRowScratch()
        {
            if (lines == null) return;
            if (rowTexts != null && rowTexts.Length == lines.Length) return;

            rowTexts = new string[lines.Length];
            rowColors = new Color[lines.Length];
            rowWide = new bool[lines.Length];
            rowGrades = new int[lines.Length];
        }

        /**
         * @brief 줄들을 실제로 놓는다. **자리가 상수가 아니라 결과에서 나온다.**
         *
         * ## 50b - 두 열 격자가 문자열에 밀렸다
         *
         * 처음에는 열 줄이 두 칸 x 다섯 줄 고정이었고, 그 격자에 "오의 해금 →
         * 스킬 XP 240 → Lv +9" 같은 긴 줄이 들어오자 열 경계를 넘어 판 밖으로
         * 삐져나왔다(실기 캡처). 44단계의 규칙 그대로다 - **상자를 글자에
         * 맞추지 그 반대가 아니다**(VerifyRowsFit).
         *
         * 그래서 줄이 두 종류가 됐다:
         *
         *   좁은 줄   반 폭, 두 칸씩.  짧은 고정 포맷("XP +6")만 들어온다
         *   넓은 줄   전 폭, 한 줄 통째.  **드문 사건**(해금·개안·전설)이 들어온다
         *
         * 넓은 줄은 오버플로 수리이면서 동시에 연출이다 - 200회에 한 번의
         * 결과가 열 줄 사이에 끼어 있으면 흘깃 볼 때 안 읽히는데, 혼자 한
         * 줄을 다 쓰면 판의 리듬이 거기서 끊긴다. 47단계가 전설에서만 판을
         * 금테로 물들인 것과 같은 층의 신호이고, 같은 이유로 드물어야 한다.
         *
         * 판의 높이도 여기서 나온다 - 줄 수가 결과마다 다르므로(넓은 줄 하나가
         * 좁은 칸 둘 몫을 쓴다) 상수로 두면 넓은 줄이 많은 판에서 반드시
         * 넘친다. 상점 배너 높이를 상수에서 식으로 바꾼 47단계 규칙의 연장이다.
         */
        private void Render(int count, string headline, Color headColor, bool legendaryCard)
        {
            // 줄 수 먼저. 좁은 칸은 둘씩 접히고 넓은 줄은 혼자 한 줄이다.
            // 넓은 줄이 중간에 오면 차 있던 왼쪽 칸은 그대로 두고 다음 줄로
            // 내려간다 - 순서를 지키는 것이 정렬보다 먼저다(뽑기 순서가 곧
            // 사건의 순서다)
            int rows = 0, column = 0;
            for (int i = 0; i < count; i++)
            {
                if (rowWide[i]) { if (column > 0) { rows++; column = 0; } rows++; }
                else { column++; if (column == 2) { rows++; column = 0; } }
            }
            if (column > 0) rows++;

            // 판의 폭은 뿌리(패널 전체)에서 유도한다. visual은 이 순간 아직
            // 꺼져 있을 수 있어 자기 rect가 낡았을 수 있다
            float cardWidth = ((RectTransform)transform).rect.width - outerSidePad * 2f;
            float innerWidth = cardWidth - cardSidePad * 2f;

            float cardHeight = topPad + lineHeight * 1.4f + rows * lineHeight
                             + buttonGap + confirmHeight + topPad;

            var visualRect = (RectTransform)visual.transform;
            visualRect.offsetMin = new Vector2(outerSidePad, -cardHeight * 0.5f);
            visualRect.offsetMax = new Vector2(-outerSidePad, cardHeight * 0.5f);

            float half = innerWidth * 0.5f;
            int row = 0;
            column = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;

                if (i >= count)
                {
                    lines[i].text = string.Empty;
                    continue;
                }

                var rect = (RectTransform)lines[i].transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);

                if (rowWide[i])
                {
                    if (column > 0) { row++; column = 0; }
                    rect.sizeDelta = new Vector2(innerWidth, lineHeight);
                    rect.anchoredPosition = new Vector2(cardSidePad,
                        -(topPad + lineHeight * (1.4f + row)));

                    // 넓은 줄은 가운데다 - 혼자 한 줄을 쓰는 사건이라 제목과
                    // 같은 축에 선다
                    lines[i].alignment = TMPro.TextAlignmentOptions.Center;
                    row++;
                }
                else
                {
                    // 좁은 칸은 **왼쪽 정렬**이다 (50b). 가운데로 두면 길이가
                    // 다른 줄들("XP +6" / "XP +20 · Lv +1")의 시작점이 칸마다
                    // 흔들려 열이 열로 안 읽힌다 - 확률표가 열을 세운 것과
                    // 같은 이유이고, 들여쓰기 한 칸이 두 열의 경계를 만든다
                    rect.sizeDelta = new Vector2(half - NarrowInset, lineHeight);
                    rect.anchoredPosition = new Vector2(
                        cardSidePad + column * half + NarrowInset,
                        -(topPad + lineHeight * (1.4f + row)));

                    lines[i].alignment = TMPro.TextAlignmentOptions.Left;
                    column++;
                    if (column == 2) { row++; column = 0; }
                }

                lines[i].text = rowTexts[i];
                lines[i].color = rowColors[i];
            }

            if (titleLabel != null)
            {
                titleLabel.text = headline;
                titleLabel.color = headColor;
            }

            if (cardBackground != null)
                cardBackground.color = legendaryCard ? cardLegendaryTint : cardNormalTint;

            visual.SetActive(true);
            visual.transform.SetAsLastSibling();

            BeginReveal(count);
        }

        /**
         * @brief 줄을 전부 숨기고 하나씩 여는 연출을 시작한다 (#12).
         *
         * 숨기는 방법은 **크기 0**이다. 알파를 쓰지 않는 이유는 TMP의 색을
         * 건드리면 등급 색을 다시 계산해야 하고(줄마다 다르다), 열릴 때
         * 튀어나오는 연출도 어차피 크기로 하기 때문이다 - 한 가지 값으로
         * 숨김과 등장을 다 처리하면 중간에 끊겨도 어긋나지 않는다.
         */
        private void BeginReveal(int count)
        {
            revealCount = count;
            revealed = 0;

            if (lines == null) return;

            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null) lines[i].transform.localScale = Vector3.zero;

            // 첫 줄은 다음 프레임에 바로 연다. 첫 줄까지 기다리게 하면
            // 판이 빈 채로 떠 있는 순간이 생기고, 그것은 고장으로 읽힌다
            nextRevealAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (!Revealing) return;

            // unscaled다. 히트스톱이 timeScale을 0으로 붙드는 게임이라
            // 스케일 시간으로 재면 뽑기 연출이 전투 타이밍에 따라 늘어난다
            while (Revealing && Time.unscaledTime >= nextRevealAt)
            {
                RevealNext();
                if (revealed >= revealCount) { nextRevealAt = 0f; break; }
                nextRevealAt += revealInterval;
            }
        }

        /**
         * @brief 다음 줄 하나를 연다. 등급이 셀수록 크게 튄다.
         *
         * 코루틴을 쓰지 않는다 - 판이 꺼졌다 켜지는 사이에 코루틴이 살아
         * 있으면 다음 판의 줄을 지난 판의 연출이 건드린다. 상태를 필드에
         * 두면 판이 꺼질 때(Close) 그냥 멈춘다.
         */
        private void RevealNext()
        {
            int index = revealed++;
            if (lines == null || index < 0 || index >= lines.Length) return;

            var line = lines[index];
            if (line == null) return;

            line.transform.localScale = Vector3.one * PunchOf(index);
            StartShrink(index);

            // ★3 이상은 흰빛에서 제 색으로 물든다. 아래 등급까지 물들이면
            // 열 줄이 다 반짝여서 무엇이 좋은 결과인지가 안 보인다
            if (revealFlashSeconds > 0f && rowGrades != null && index < rowGrades.Length
                && rowGrades[index] >= (int)GachaCurve.Grade.Rare)
                line.color = Color.white;
        }

        private float PunchOf(int index)
        {
            if (rowGrades == null || index >= rowGrades.Length) return 1.15f;

            int grade = rowGrades[index];
            if (revealPunchByGrade == null || grade < 0 || grade >= revealPunchByGrade.Length)
                return 1.15f;

            return revealPunchByGrade[grade];
        }

        /**
         * @brief 튀어나온 줄을 제 크기로 되돌린다.
         *
         * 줄마다 시작 시각이 다르므로 리스트로 들고 매 프레임 민다. 코루틴을
         * 안 쓰는 이유는 RevealNext와 같다.
         */
        private void StartShrink(int index)
        {
            if (shrinking == null) shrinking = new List<int>();
            if (shrinkStart == null) shrinkStart = new float[lines.Length];

            shrinkStart[index] = Time.unscaledTime;
            if (!shrinking.Contains(index)) shrinking.Add(index);
        }

        private List<int> shrinking;
        private float[] shrinkStart;

        private void LateUpdate()
        {
            if (shrinking == null || shrinking.Count == 0) return;

            for (int n = shrinking.Count - 1; n >= 0; n--)
            {
                int index = shrinking[n];
                if (lines == null || index >= lines.Length || lines[index] == null)
                {
                    shrinking.RemoveAt(n);
                    continue;
                }

                float t = revealPunchSeconds <= 0f
                    ? 1f
                    : (Time.unscaledTime - shrinkStart[index]) / revealPunchSeconds;

                if (t >= 1f)
                {
                    lines[index].transform.localScale = Vector3.one;
                    if (rowColors != null && index < rowColors.Length)
                        lines[index].color = rowColors[index];
                    shrinking.RemoveAt(n);
                    continue;
                }

                float punch = PunchOf(index);
                lines[index].transform.localScale = Vector3.one * Mathf.Lerp(punch, 1f, t);

                // 흰빛에서 제 색으로. 크기보다 빨리 끝난다 - 색이 오래
                // 남아 있으면 등급 색이 헷갈린다
                if (revealFlashSeconds > 0f && rowColors != null && index < rowColors.Length)
                {
                    float ct = (Time.unscaledTime - shrinkStart[index]) / revealFlashSeconds;
                    if (ct < 1f) lines[index].color = Color.Lerp(Color.white, rowColors[index], ct);
                }
            }
        }

        /** 남은 줄을 그 자리에서 전부 연다. 연출을 못 기다리는 순간을 위한 문 */
        private void RevealAll()
        {
            while (revealed < revealCount) RevealNext();
            nextRevealAt = 0f;
        }

        private void Start()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            Close();
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
        }

        /**
         * @brief 확인 버튼. **연출 중이면 닫지 않고 전부 연다** (#12).
         *
         * 연출이 도는 동안 이 버튼이 판을 닫으면, 급해서 누른 사람은 자기가
         * 무엇을 뽑았는지 못 본 채로 판을 잃는다. 첫 탭은 건너뛰기, 두 번째
         * 탭이 닫기다 - 뽑기 화면의 표준이고, 0.7초라 두 번 누를 일도 드물다.
         */
        private void OnConfirm()
        {
            if (Revealing) { RevealAll(); return; }
            Close();
        }

        public void Close()
        {
            // 열리다 만 상태로 꺼지면 다음 판이 그 상태를 이어받는다.
            // 크기 0인 줄이 남아 있는 판이 바로 그것이다
            nextRevealAt = 0f;
            if (shrinking != null) shrinking.Clear();

            if (lines != null)
                foreach (var line in lines)
                    if (line != null) line.transform.localScale = Vector3.one;

            if (visual != null) visual.SetActive(false);
        }

        /**
         * @brief 결과 목록을 그린다. 목록은 **보관하지 않는다.**
         *
         * GachaSystem이 돌려 쓰는 목록이라 다음 뽑기가 덮어쓴다. 그 자리에서
         * 문자열로 옮기고 끝낸다.
         */
        public void Show(List<GachaSystem.PullResult> results, YodoSystem yodo)
        {
            if (visual == null || lines == null || results == null) return;
            EnsureRowScratch();

            int shards = 0;
            var best = GachaCurve.Grade.Common;
            var counts = new int[GachaCurve.GradeCount];
            int count = System.Math.Min(results.Count, lines.Length);

            for (int i = 0; i < count; i++)
            {
                var result = results[i];
                shards += result.Shards;

                var grade = GachaCurve.GradeFor(result.Outcome);
                counts[(int)grade]++;
                if (grade > best) best = grade;

                rowTexts[i] = TextFor(result, yodo);
                rowColors[i] = ColorOf(grade);

                // 열리는 세기가 등급에서 나온다 (#12)
                rowGrades[i] = (int)grade;

                // ★4 이상이 전 폭 줄이다 - 오의 배너와 **같은 자**다 (50b).
                // 처음에 전설만 넓혔다가 실기에서 물렸다: ★4의 미끄러짐 줄
                // ("천장! 상위 혼 → 혼 정수 → 파편 80")이 들여쓰기로 좁아진
                // 칸을 넘어 오른쪽 칸을 덮었다. 실효 ★4가 4.1%라 10연에
                // 반 줄꼴 - 천장이 지키는 것이 ★4+인 것과 같은 경계이고,
                // 넓은 줄 = "천장이 보장하는 것"으로 두 배너가 같은 말을 한다
                rowWide[i] = grade >= GachaCurve.Grade.Epic;
            }

            // 판이 물드는 것은 전설에서만이다. ★4까지는 줄 하나의 색으로
            // 충분하고, 판까지 바뀌면 200회에 한 번의 사건이 20회에 한 번이
            // 된다 - 사건은 드물어야 사건이다
            Render(count, Headline(shards, counts), ColorOf(best),
                   best == GachaCurve.Grade.Legendary);
        }

        /**
         * @brief 머리글. **합계이고, 위쪽 등급만 센다.**
         *
         * 46단계는 "파편 54 · 혼 정수 1"이었다. 사다리가 다섯이 되면서
         * 등급을 다 세면 머리글이 다섯 조각이 되는데, 그러면 열 줄을 안
         * 세게 하려고 만든 줄이 그 자체로 세야 하는 줄이 된다.
         *
         * 그래서 **파편 합계 + ★3 이상만** 적는다. ★1·★2는 전부 파편이라
         * 이미 합계에 들어 있고, 위쪽 셋은 개수가 곧 사건이다.
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
         * @brief 한 줄의 문구.
         *
         * 혼 정수가 갈 곳이 있었는지를 반드시 적는다. 상한(리드)에 닿아
         * 파편이 된 경우에 "혼 정수"라고만 적으면 플레이어는 티어가 오를
         * 것을 기대하고 대장간에 갔다가 아무것도 못 찾는다 - 44단계가
         * "버려지는 드랍 0"을 값으로 지켰다면 여기서는 **문구로** 지킨다.
         */
        private static string TextFor(GachaSystem.PullResult result, YodoSystem yodo)
        {
            switch (result.Outcome)
            {
                case GachaCurve.Outcome.SoulEssence:
                    return EssenceText(result, yodo, string.Empty);

                case GachaCurve.Outcome.SoulRarity:
                    return RarityText(result, yodo);

                case GachaCurve.Outcome.LegendaryBlade:
                    return LegendaryText(result, yodo);

                default:
                    return "파편 " + result.Shards;
            }
        }

        private static string EssenceText(GachaSystem.PullResult result, YodoSystem yodo,
                                          string prefix)
        {
            if (result.BladeIndex < 0)
                return prefix + "혼 정수 → 파편 " + result.Shards;

            var blade = yodo != null ? yodo.GetBlade(result.BladeIndex) : null;
            string name = blade != null ? blade.soulName : "혼";
            return prefix + "혼 정수 → " + name;
        }

        /**
         * @brief ★4 한 줄. **미끄러진 것을 반드시 적는다.**
         *
         * 혼격이 꽉 차면 ★4는 ★3으로 내려간다(GachaSystem.GrantRarity).
         * 그때 "상위 혼"이라고만 적으면 플레이어는 대장간에 가서 혼격이 안
         * 오른 것을 보고, "혼 정수"라고만 적으면 등급이 내려간 것을 모른다.
         * 두 단어를 다 적어 **무엇이 무엇이 됐는지**를 그 자리에서 말한다 -
         * 46단계의 "혼 정수 → 파편 40"과 같은 문법이다.
         */
        private static string RarityText(GachaSystem.PullResult result, YodoSystem yodo)
        {
            string prefix = result.FromPity ? "천장! " : string.Empty;

            if (result.Downgraded)
                return EssenceText(result, yodo, prefix + "상위 혼 → ");

            var blade = yodo != null ? yodo.GetBlade(result.BladeIndex) : null;
            if (blade == null) return prefix + "상위 혼";

            // 혼격은 올라간 **뒤**의 값이다. 화면이 도감과 같은 숫자를 적어야
            // "뽑았는데 안 올랐다"가 안 나온다
            return prefix + blade.bladeName + " " + YodoRarityCurve.Stars(blade.rarity);
        }

        /**
         * @brief ★5 한 줄. 새 칼인가 돌파인가를 가른다.
         *
         * 두 사건의 크기가 다르다 - 새 칼은 도감에 줄이 하나 켜지는 일이고
         * 돌파는 있던 줄이 깊어지는 일이다. 같은 문구로 적으면 200회에 한
         * 번의 결과가 어느 쪽이었는지 판에서 읽히지 않는다.
         */
        private static string LegendaryText(GachaSystem.PullResult result, YodoSystem yodo)
        {
            if (result.LegendaryIndex < 0)
                return "전설 → 파편 " + result.Shards;

            var blade = yodo != null ? yodo.GetLegendary(result.LegendaryIndex) : null;
            if (blade == null) return "전설 요도";

            return blade.copies <= 1
                ? blade.bladeName + " 획득!"
                : blade.bladeName + " 돌파 " + blade.Breakthrough;
        }

        // ---------------------------------------------------------------- 오의 뽑기 (50단계)

        /**
         * @brief 오의 뽑기 결과를 **같은 판에** 그린다.
         *
         * 판을 따로 만들지 않은 이유는 47단계가 등급 색과 별로 세운 것이
         * **눈금**이기 때문이다(UiSkin.Grades 주석) - 눈금은 뜻이 하나여야
         * 하고, 같은 ★4가 두 판에서 다른 색·다른 배치로 뜨면 그 하나가 깨진다.
         * 갈리는 것은 줄의 문구뿐이고, 그것이 두 배너의 차이 전부다.
         *
         * 머리글도 규칙이 같다 - 합계 하나 + ★3 이상만 센다. 저쪽의 합계가
         * 파편이고 이쪽은 XP다.
         */
        public void Show(List<SkillGachaSystem.PullResult> results, SkillSystem skills)
        {
            if (visual == null || lines == null || results == null) return;
            EnsureRowScratch();

            int xp = 0;
            var best = GachaCurve.Grade.Common;
            var counts = new int[GachaCurve.GradeCount];
            int count = System.Math.Min(results.Count, lines.Length);

            for (int i = 0; i < count; i++)
            {
                var result = results[i];
                xp += result.Xp;

                counts[(int)result.Grade]++;
                if (result.Grade > best) best = result.Grade;

                // **굴린 등급이 넓은 줄을 정한다** - 도착한 등급이 아니라.
                // ★4·★5는 미끄러져 XP로 떨어져도 그 줄이 사건의 기록이고
                // ("오의 해금 → XP +240"), 실기에서 삐져나온 것이 정확히
                // 그 미끄러짐 줄이었다. 요도 쪽과 기준이 다른 이유는 재고다 -
                // 이 배너의 ★4(해금)는 평생 두 번뿐이라 넓혀도 사건으로
                // 남지만, 요도의 ★4는 실효 4.1%라 넓히면 판이 넓은 줄투성이가
                // 된다
                rowWide[i] = GachaCurve.GradeOf[(int)result.Rolled] >= GachaCurve.Grade.Epic;

                rowTexts[i] = SkillTextFor(result, skills, rowWide[i]);

                // 미끄러진 줄의 색도 굴린 등급이다. 도착한 등급(XP = ★1~★3)
                // 으로 칠하면 전 폭 줄이 바닥 색을 입어 "넓은데 수수한" 줄이
                // 되고, 그것은 신호 둘이 서로를 지우는 것이다
                rowColors[i] = ColorOf(GachaCurve.GradeOf[(int)result.Rolled]);

                // 색과 같은 자에서 나온다 - 굴린 등급이다 (#12). 미끄러져
                // XP로 떨어진 ★5도 ★5만큼 크게 튄다: 그 줄이 기록하는 사건이
                // 도착지가 아니라 출발지이기 때문이다(위 rowWide와 같은 판단)
                rowGrades[i] = (int)GachaCurve.GradeOf[(int)result.Rolled];
            }

            Render(count, SkillHeadline(xp, counts), ColorOf(best),
                   best == GachaCurve.Grade.Legendary);
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
         * @brief 오의 뽑기 한 줄. **넓은 줄과 좁은 줄이 다른 문법을 쓴다** (50b).
         *
         * 처음에는 한 문법이었다("스킬 XP 240 → Lv +8"). 그 줄이 반 폭 칸을
         * 넘어 판 밖으로 삐져나왔고, 화살표가 두 가지 뜻(미끄러짐 / 레벨업)
         * 으로 겹쳐 있기도 했다. 갈랐다:
         *
         *   좁은 줄   "XP +6" · "XP +70 · Lv +5"    반 폭에 반드시 든다
         *   넓은 줄   "천장! 오의 해금 · 혈폭"        화살표는 미끄러짐 전용
         *
         * 좁은 줄에서 "스킬"을 뗀 것은 상자 때문이 아니라(그래도 들어간다)
         * 열 줄이 다 같은 말로 시작하면 눈이 훑을 것이 없어지기 때문이다 -
         * 헤드라인이 이미 "스킬 XP 360"으로 합계를 말하고 있다.
         *
         * **미끄러진 것은 반드시 적는다.** 47단계가 "상위 혼 → 혼 정수"로
         * 세운 문법 그대로다 - 도착한 곳만 적으면 등급이 내려간 것을 모르고,
         * 출발한 곳만 적으면 화면과 실제가 갈린다. 미끄러진 줄은 굴린 등급이
         * ★4+라 언제나 넓은 줄이고, 그래서 긴 사슬이 좁은 칸에 끼일 일이
         * 없다.
         *
         * XP 줄에 **오른 레벨을 함께 적는** 규칙은 그대로다. XP는 게이지
         * 안으로 사라지는 값이라 수량만 적으면 그 줄이 무슨 일을 했는지
         * 화면에서 읽히지 않는다.
         */
        private static string SkillTextFor(SkillGachaSystem.PullResult result, SkillSystem skills,
                                           bool wide)
        {
            string prefix = result.FromPity ? "천장! " : string.Empty;

            // 미끄러진 칸을 **출발점부터 하나씩** 적는다. ★5가 두 칸 내려간
            // 줄은 "오의 개안 → 오의 해금 → XP +240"이 되고, 그 줄 하나로
            // 200회에 한 번의 결과가 무엇이었고 왜 그것이 안 됐는지가 읽힌다
            //
            // **먼저 미끄러졌는지 묻는다.** 굴린 칸과 받은 칸이 다른 이유가
            // 둘이기 때문이다 - 미끄러짐(위 -> 아래)과 **천장**(아래 -> 위)이다.
            // 천장이 덮은 회차에서 아래로 걸으면 도착점에 영영 못 닿는다.
            // 그 경우 적을 경로가 없다 - 사다리를 안 탔기 때문이고, 그 회차가
            // 무엇이었는지는 "천장!" 표시가 이미 말한다
            if (SkillGachaCurve.SlidesTo(result.Rolled, result.Outcome))
                for (var at = result.Rolled; at != result.Outcome;
                     at = SkillGachaCurve.SlideFor(at))
                    prefix += NameOfOutcome(at) + " → ";

            switch (result.Outcome)
            {
                case SkillGachaCurve.Outcome.Awakening:
                    // 도달한 레벨을 함께 적는다. 개안의 사건은 "어디까지 갔는가"
                    // 이고, 상한이 곧 그 답이다 - 적지 않으면 해금 줄과 같은
                    // 무게로 읽힌다
                    return prefix + "오의 개안 · " + NameOf(skills, result.AwakenedIndex)
                         + " Lv." + Onikiri.Progression.SkillCurve.MaxLevel;

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
         * 뜨면 플레이어는 그 둘이 같은 것인지 알 수 없다 - 47단계가 확률표와
         * 결과 판을 같은 배열에서 뽑은 것과 같은 규칙이다.
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

        private Color ColorFor(GachaSystem.PullResult result)
        {
            return ColorOf(GachaCurve.GradeFor(result.Outcome));
        }

        /**
         * @brief 등급의 색. 배열이 안 배선된 씬에서는 46단계의 색으로 떨어진다.
         *
         * 폴백을 남기는 이유는 41단계 이후의 규칙이다 - 배선이 빠진 씬에서
         * 화면이 아예 안 뜨는 것보다 조용히 수수한 편이 낫다.
         */
        private Color ColorOf(GachaCurve.Grade grade)
        {
            int index = (int)grade;
            if (gradeColors != null && index >= 0 && index < gradeColors.Length)
                return gradeColors[index];

            return grade >= GachaCurve.Grade.Rare ? goldColor
                 : grade == GachaCurve.Grade.Uncommon ? textColor
                 : dimColor;
        }
    }
}
