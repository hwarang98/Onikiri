using Onikiri.Battle;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 보스전 화면. 도전 버튼, 등장 연출, 시계와 체력 바, 실패 문구.
     *
     * 네 덩어리가 한 컴포넌트에 있는 이유는 이들이 서로 배타적이기 때문이다. 어느
     * 하나가 떠 있으면 나머지는 반드시 꺼져 있어야 하고, 그 규칙을 여러 컴포넌트에
     * 나눠 두면 "실패 문구 위에 도전 버튼이 겹쳐 있는" 상태가 조합으로 생긴다.
     * 상태 하나에 화면 하나를 대응시키면 그런 조합이 존재할 수 없다.
     *
     * 갱신은 두 갈래다. 상태 전환은 BossFight.Changed 이벤트로, 시계와 체력 바는
     * 여기서 직접 읽는다. 시계를 이벤트로 받으면 매 프레임 TMP 메시를 다시 만들게
     * 되는데, 표시는 1초 단위라 그중 대부분이 같은 글자를 다시 그리는 일이다.
     */
    public sealed class BossHud : MonoBehaviour
    {
        [SerializeField] private BossFight fight;

        [Header("도전")]
        [SerializeField] private GameObject challengeRoot;
        [SerializeField] private Button challengeButton;
        [SerializeField] private TMP_Text challengeLabel;

        /**
         * @brief 처치 할당량 표시. 도전 버튼과 같은 자리에 뜬다 (37단계).
         *
         * 원래 상단 바의 스테이지 문구에 붙어 있었는데, 상단 바 재배치에서
         * 이리로 옮겼다. 카운터가 차면 같은 자리가 도전 버튼으로 바뀌므로
         * "채우면 무슨 일이 생기는가"가 한 자리에서 이어진다.
         *
         * 최전선 아래(재선택)에서는 숨긴다 - 보스가 잠겨 있어 할당량이
         * 거짓말이 된다. 그 상태는 상단 바가 "클리어"로 말한다.
         */
        [Header("할당량")]
        [SerializeField] private GameObject quotaRoot;
        [SerializeField] private TMP_Text quotaLabel;
        [SerializeField] private StageProgress progress;

        [Header("등장 연출")]
        [Tooltip("화면 전체를 덮는 어두운 판. 보스가 들어오기 전 1초 동안 떠 있다")]
        [SerializeField] private GameObject introRoot;
        [SerializeField] private TMP_Text introLabel;

        [Header("전투")]
        [SerializeField] private GameObject fightRoot;
        [SerializeField] private TMP_Text timerLabel;
        [Tooltip("sprite 없는 민짜 판. 폭은 anchorMax.x로 구동한다 - Filled+UISprite는 " +
                 "둥근 소프트 가장자리가 얇은 바에서 그라데이션으로 보인다(38b 규칙, " +
                 "EXP 스트립 = LevelHud와 같은 방식)")]
        [SerializeField] private Image healthFill;

        /**
         * @brief 플레이어 체력 바. **보스전 중에만 뜬다.**
         *
         * 파밍 중에는 체력이 깎일 일이 없다 - 잡몹은 공격하지 않는다. 아무 일도
         * 일어나지 않는 화면에 항상 가득 찬 바를 하나 더 얹으면, 그 바는 정보가
         * 아니라 배경이 되고 정작 보스전에서 줄어들 때도 눈에 덜 걸린다.
         *
         * 상시 표시가 필요해지는 것은 파밍 중에도 체력이 변할 때다. 그때 다시
         * 판단하면 된다.
         */
        [SerializeField] private Onikiri.Battle.PlayerHealth playerHealth;
        [SerializeField] private Image playerHealthFill;
        [SerializeField] private TMP_Text playerHealthLabel;

        [Header("결과")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private TMP_Text resultLabel;

        /**
         * @brief 마지막으로 화면에 찍은 남은 초.
         *
         * -1로 시작해 첫 프레임에서 반드시 한 번 갱신되게 한다.
         */
        private int shownSeconds = -1;

        /** 마지막으로 찍은 플레이어 체력. 정수가 바뀔 때만 다시 그린다 */
        private double shownPlayerHealth = -1d;

        private void Start()
        {
            if (fight != null) fight.Changed += Refresh;
            if (challengeButton != null) challengeButton.onClick.AddListener(OnChallengeClicked);

            Refresh();
        }

        private void OnDestroy()
        {
            if (fight != null) fight.Changed -= Refresh;
            if (challengeButton != null) challengeButton.onClick.RemoveListener(OnChallengeClicked);
        }

        /**
         * @brief 일반 보스만 연다 (4단계 §3에서 귀문 분기를 걷어냈다).
         *
         * 귀문 검사를 **남겨 둔다.** 이 버튼은 귀문 대기 중에 뜨지 않으므로
         * 그 경로로 들어올 일이 없지만, 배선 사고로 뜨는 날 일반 보스를 다시
         * 잡아 보상이 중복되고 D-4의 복구 조건이 깨지는 것보다는 안전한 쪽으로
         * 떨어지는 편이 낫다.
         */
        private void OnChallengeClicked()
        {
            if (fight == null) return;

            if (fight.CanChallengeTrial) { fight.ChallengeTrial(); return; }
            if (fight.CanChallenge) fight.Challenge();
        }

        /**
         * @brief 상태 하나에 화면 하나.
         *
         * 매번 넷을 전부 껐다 필요한 것만 켠다. "이전 상태에서 무엇이 켜져 있었는지"를
         * 따지지 않으므로 전환 표를 유지할 필요가 없다.
         */
        private void Refresh()
        {
            if (fight == null) return;

            var phase = fight.Current;

            // 접근 중에도 전투 화면을 띄운다. 보스 체력 바가 이때 이미 보여야
            // "저놈을 상대한다"가 전달되고, 시계는 아직 안 돌므로 제한 시간이
            // 가득 찬 채로 멈춰 있는다 - 달려가는 동안 시간이 안 깎인다는 것이
            // 화면에서도 읽힌다
            /**
             * @brief 도전 버튼은 **일반 보스 전용이다** (4단계 §3).
             *
             * 3단계까지는 이 버튼 하나가 상태에 따라 「보스 도전」과 「귀문
             * 도전」으로 갈렸다. 4단계에서 귀문 입구가 우측 가이드 카드로
             * 옮겨가면서 그 분기를 걷어낸다 - 같은 것을 여는 버튼이 화면에
             * 둘 있으면 그중 하나는 반드시 안 눌리는 장식이 된다.
             *
             * `CanChallenge`가 이미 `PendingTrialGate == 0`을 요구하므로,
             * 귀문 대기 중에는 이 버튼이 저절로 잠긴 채로 있는다.
             */
            bool trialPending = fight.CanChallengeTrial;
            Show(challengeRoot, phase == BossFight.Phase.Farming && fight.CanChallenge);
            Show(introRoot, phase == BossFight.Phase.Intro);
            Show(fightRoot, phase == BossFight.Phase.Approaching || phase == BossFight.Phase.Fighting);
            Show(resultRoot, phase == BossFight.Phase.Failed);

            // 할당량은 도전 버튼의 앞 단계다 - 같은 자리, 배타로 뜬다.
            // Changed는 처치마다 발생하므로(StageProgress) 숫자가 여기서 갱신된다
            // 할당량은 **귀문 버튼과 겹치지 않는다.** 귀문 대기 중에는 이미
            // 10/10이라 그 숫자가 더 알려줄 것이 없고, 두 줄이 같은 자리에서
            // 다투면 정작 눌러야 할 버튼이 안 읽힌다
            // 귀문 대기도 여기서 함께 뺀다. 그때는 이미 10/10이라 숫자가 더
            // 알려줄 것이 없고, 화면이 말해야 하는 것은 우측 카드의 「일문 도전」
            // 하나다 (4단계 §3)
            bool farmingTowardBoss = phase == BossFight.Phase.Farming
                                     && !fight.CanChallenge && !trialPending
                                     && progress != null && progress.IsAtFrontier;
            Show(quotaRoot, farmingTowardBoss);
            if (farmingTowardBoss && quotaLabel != null)
                quotaLabel.text = "처치 " + progress.KillsThisStage + "/" + progress.KillsRequired;

            if (phase == BossFight.Phase.Intro && introLabel != null)
                introLabel.text = fight.BossName;

            if (phase == BossFight.Phase.Failed && resultLabel != null)
                resultLabel.text = fight.FailureMessage;

            // 문구는 하나다. 귀문 문구는 가이드 카드가 적는다 (4단계 §3)
            if (phase == BossFight.Phase.Farming && challengeLabel != null)
                challengeLabel.text = "보스 도전";

            // 새 전투가 시작될 때마다 시계 표시를 무효화한다. 그러지 않으면 이전
            // 전투가 끝난 초와 같은 값으로 시작하는 경우 첫 갱신이 통째로 생략된다
            if (phase != BossFight.Phase.Fighting)
            {
                shownSeconds = -1;
                shownPlayerHealth = -1;
            }

            // 전투 화면이 뜨는 순간(접근 포함) 바를 즉시 맞춘다. Update의 바
            // 구동은 접근+전투에서만 돌아서, 여기서도 한 번 밀어주지 않으면
            // 상태 전환 프레임에 직전 보스전의 마지막 값이 잠깐 비친다 -
            // "새 보스인데 체력 바가 초기화가 안 돼 있다"로 신고된 버그다
            if (phase == BossFight.Phase.Approaching || phase == BossFight.Phase.Fighting)
                SyncBars(phase);

            // 달려가는 동안은 제한 시간이 가득 찬 채로 서 있는다. 0으로 두면
            // 도착하는 순간 0에서 30으로 튀어 시계가 고장 난 것처럼 보인다
            if (phase == BossFight.Phase.Approaching && timerLabel != null)
                timerLabel.text = Mathf.CeilToInt(StageCurve.BossTimeLimitSeconds) + "초";
        }

        /**
         * @brief 두 체력 바와 플레이어 수치를 지금 값으로 그린다.
         *
         * 접근 중 플레이어 바는 **가득**으로 그린다. 실제 current에는 직전
         * 보스전의 잔량이 남아 있지만, 개전 순간 BeginFight가 가득 채우는 것이
         * 계약이다("가득 찬 상태로 연다") - 잔량을 보여주면 새 보스 앞에서
         * 깎인 바로 시작하는 것처럼 읽힌다.
         */
        private void SyncBars(BossFight.Phase phase)
        {
            bool approaching = phase == BossFight.Phase.Approaching;

            SetBar(healthFill, fight.BossHealthFraction);

            if (playerHealth == null) return;

            SetBar(playerHealthFill, approaching ? 1f : playerHealth.Fraction);

            // 숫자는 정수로만 바꾼다. 회복이 매 프레임 소수점을 올리는데
            // 그때마다 TMP 메시를 다시 만들면 보스전 내내 재생성이 돈다.
            //
            // 현재값은 올림, 최대값은 반올림을 쓰다가 "2811 / 2810"이 나왔다.
            // 체력이 가득 찬 상태에서 소수점이 남으면 올림과 반올림이 서로 다른
            // 정수로 가기 때문이다. 둘 다 올림으로 맞추고, 표시값을 최대값에서
            // 한 번 더 자른다 - 회복이 상한을 넘지 않는데 화면만 넘는 것은
            // 계산이 틀린 것처럼 보인다.
            //
            // **int로 내리지 않는다.** Mathf.CeilToInt는 21억을 넘는 순간
            // -2147483648로 뒤집힌다 - 체력 축이 지수 곡선이라 후반에는 실제로
            // 닿는 값이고, 화면에서 확인했다. double로 자르고 표기는 골드와
            // 같은 NumberFormatter를 쓴다(2811 -> "2811", 3.2조 -> "3.2T")
            double max = System.Math.Ceiling(playerHealth.MaxHealth);
            double shown = approaching
                ? max
                : System.Math.Min(max, System.Math.Ceiling(playerHealth.Current));

            if (shown != shownPlayerHealth && playerHealthLabel != null)
            {
                shownPlayerHealth = shown;
                playerHealthLabel.text = Onikiri.Core.NumberFormatter.Format(shown)
                                         + " / " + Onikiri.Core.NumberFormatter.Format(max);
            }
        }

        /**
         * @brief 채움 폭 = anchorMax.x. 스프라이트 없는 민짜 판이라 끝까지 균일하다.
         *
         * fillAmount를 버린 이유는 38b의 함정 그대로다 - Filled는 스프라이트가
         * 필요하고, 내장 UISprite의 둥근 소프트 가장자리는 얇은 바에서
         * 그라데이션으로 보인다(게이지에 그라데이션 금지). 앵커 쓰기도 메시
         * 재생성 없이 사각형만 늘리므로 매 프레임 써도 된다(LevelHud 실증).
         */
        private static void SetBar(Image fill, float fraction)
        {
            if (fill == null) return;

            var rect = (RectTransform)fill.transform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), rect.anchorMax.y);
        }

        private void Update()
        {
            if (fight == null) return;

            var phase = fight.Current;

            // 접근부터 바가 산다. 예전에는 Fighting에서만 돌아서, 달려가는 5.3초
            // 동안 직전 보스전의 마지막 값이 그대로 떠 있었다
            if (phase != BossFight.Phase.Approaching && phase != BossFight.Phase.Fighting) return;

            SyncBars(phase);

            if (phase != BossFight.Phase.Fighting) return;

            // 남은 시간은 올림한다. 29.4초를 "29"로 찍으면 시작하자마자 1초가
            // 사라진 것처럼 보이고, 0은 시간이 실제로 다 됐을 때만 나와야 한다
            int seconds = Mathf.CeilToInt(fight.SecondsLeft);
            if (seconds == shownSeconds) return;

            shownSeconds = seconds;
            if (timerLabel != null) timerLabel.text = seconds + "초";
        }

        private static void Show(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
