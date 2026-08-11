using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 레벨 칩(상단 바), 경험치 스트립(성장 패널 상단 경계), 레벨업 버튼.
     *
     * 경험치 바는 상단 바의 두툼한 줄이 아니라 **전투 화면과 성장 패널을 가르는
     * 얇은 스트립**이다(2a 후속). 먹빛 상단 바에서 큰 초록 덩어리가 혼자
     * 소리를 지르던 것을 걷어냈고, 진행 표시는 어차피 연속량이라 몇 px 높이로도
     * 충분하다. 숫자는 바에서 뺐다 - 정확한 값이 필요한 사람은 레벨 칩을 눌러
     * 스탯 창에서 본다(StatsPanel).
     *
     * 레벨업 버튼은 **올릴 수 있을 때만 보인다.** 항상 띄워두고 비활성으로 두는
     * 방법도 있지만, 방치형에서 화면에 상시 존재하는 비활성 버튼은 곧 배경이 된다.
     * 없다가 나타나는 것이 눈에 걸리는 유일한 방법이다.
     *
     * 여러 레벨분이 쌓이면 버튼에 개수를 함께 적는다. 한 번에 하나씩 오르므로
     * (CharacterLevel.TryLevelUp) 몇 번 더 누를 수 있는지가 곧 남은 보상의 크기다.
     *
     * 버튼이 얇은 바에서 멀어진 만큼, 올릴 수 있는 동안은 **스트립도 은은히
     * 맥동한다** - 바를 보던 눈에도 "지금 누를 것이 있다"가 걸리도록.
     */
    public sealed class LevelHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelLabel;

        [Tooltip("스트립의 채움 사각형. Filled가 아니라 **앵커 폭**으로 늘린다 - " +
                 "Filled는 스프라이트가 필요한데, 소프트 가장자리 스프라이트는 " +
                 "10px 줄에서 그라데이션으로 읽힌다. 민짜 사각형이 가장 깨끗하다")]
        [SerializeField] private Image expFill;

        [SerializeField] private GameObject levelUpRoot;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpLabel;

        [Tooltip("남은 스탯 포인트. 상단이 아니라 증폭 행 근처에 두는 라벨이지만, " +
                 "값의 출처가 CharacterLevel 하나뿐이라 여기서 함께 갱신한다")]
        [SerializeField] private TMP_Text pointsLabel;

        [SerializeField] private string levelPrefix = "레벨 ";

        /** 맥동 한 주기. 호흡 정도의 속도 - 깜빡임이 아니라 살아 있음이어야 한다 */
        private const float PulsePeriod = 1.6f;

        /** 맥동 정점에서 흰색 쪽으로 끌어올리는 몫. 얇은 바라 이 정도도 충분히 보인다 */
        private const float PulseLift = 0.45f;

        /**
         * @brief 경험치가 조금이라도 있으면 채움이 최소한 이만큼은 보인다.
         *
         * 채움을 진행률 그대로 그리면 **바가 통째로 사라지는 구간**이 생긴다.
         * 스트립은 1080px 폭이라 진행률 0.1% 아래에서는 채움이 1px이 안 되고,
         * 그러면 아무것도 그려지지 않는다 - 화면에는 "빈 바"가 아니라 "바가
         * 없음"으로 읽힌다(실기 제보. 레벨 98에서 진행률 0.02%였다).
         *
         * 레벨이 오를수록 필요량이 지수로 자라므로(ExpCurve.RequirementStep)
         * 이 구간은 후반에 반드시 온다. 방치형에서 후반 몇 시간이 "경험치 바가
         * 고장 났다"로 보이는 것은 곡선 문제이기 전에 표시 문제다.
         *
         * 0은 그대로 0이다. 레벨업 직후의 빈 바까지 채워 두면 이번엔 반대
         * 거짓말이 된다.
         */
        private const float MinVisibleFillPixels = 6f;

        private CharacterLevel character;
        private Color fillBase;
        private bool pulsing;

        private void Start()
        {
            character = CharacterLevel.Instance;
            if (character == null)
            {
                Debug.LogWarning("[Onikiri] LevelHud found no CharacterLevel.");
                return;
            }

            if (expFill != null) fillBase = expFill.color;

            character.Changed += Refresh;
            if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);

            Refresh();
        }

        private void OnDestroy()
        {
            if (character != null) character.Changed -= Refresh;
            if (levelUpButton != null) levelUpButton.onClick.RemoveListener(OnLevelUpClicked);
        }

        private void Update()
        {
            if (!pulsing || expFill == null) return;

            // 히트스톱이 timeScale을 누르는 게임이라 unscaled로 돈다
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / PulsePeriod));
            expFill.color = Color.Lerp(fillBase, Color.white, PulseLift * wave);
        }

        private void OnLevelUpClicked()
        {
            if (character != null) character.TryLevelUp();
        }

        private void Refresh()
        {
            if (character == null) return;

            if (levelLabel != null) levelLabel.text = levelPrefix + character.Level;

            if (expFill != null)
            {
                var fillRect = (RectTransform)expFill.transform;
                fillRect.anchorMax = new Vector2(
                    VisibleFraction(fillRect, character.ExpFraction), fillRect.anchorMax.y);
            }

            if (pointsLabel != null)
                pointsLabel.text = "남은 포인트 " + character.UnspentPoints;

            int pending = character.PendingLevelUps;
            if (levelUpRoot != null && levelUpRoot.activeSelf != pending > 0)
                levelUpRoot.SetActive(pending > 0);

            if (levelUpLabel != null && pending > 0)
                levelUpLabel.text = pending > 1 ? "레벨업 " + pending : "레벨업";

            bool shouldPulse = pending > 0;
            if (pulsing && !shouldPulse && expFill != null) expFill.color = fillBase;
            pulsing = shouldPulse;
        }

        /**
         * @brief 진행률을 앵커 폭으로. 0이 아니면 최소 몇 px은 남긴다.
         *
         * 최소치를 px로 정하고 여기서 비율로 바꾸는 이유는, 바닥값이 "보이느냐"의
         * 문제라 화면 픽셀이 단위이기 때문이다. 트랙 폭을 매번 묻는 것도 그래서다 -
         * 스트립은 풀폭이라 기기마다 폭이 다르다.
         */
        private static float VisibleFraction(RectTransform fillRect, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0f) return 0f;

            var track = fillRect.parent as RectTransform;
            float width = track != null ? track.rect.width : 0f;
            if (width <= 0f) return fraction;

            return Mathf.Min(1f, Mathf.Max(fraction, MinVisibleFillPixels / width));
        }
    }
}
