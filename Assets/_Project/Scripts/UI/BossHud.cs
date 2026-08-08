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

        [Header("등장 연출")]
        [Tooltip("화면 전체를 덮는 어두운 판. 보스가 들어오기 전 1초 동안 떠 있다")]
        [SerializeField] private GameObject introRoot;
        [SerializeField] private TMP_Text introLabel;

        [Header("전투")]
        [SerializeField] private GameObject fightRoot;
        [SerializeField] private TMP_Text timerLabel;
        [Tooltip("Image.type = Filled 여야 한다. fillAmount만 건드리므로 메시 재생성이 없다")]
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
        private int shownPlayerHealth = -1;

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

        private void OnChallengeClicked()
        {
            if (fight != null) fight.Challenge();
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
            Show(challengeRoot, phase == BossFight.Phase.Farming && fight.CanChallenge);
            Show(introRoot, phase == BossFight.Phase.Intro);
            Show(fightRoot, phase == BossFight.Phase.Approaching || phase == BossFight.Phase.Fighting);
            Show(resultRoot, phase == BossFight.Phase.Failed);

            if (phase == BossFight.Phase.Intro && introLabel != null)
                introLabel.text = fight.BossName;

            if (phase == BossFight.Phase.Failed && resultLabel != null)
                resultLabel.text = fight.FailureMessage;

            if (phase == BossFight.Phase.Farming && challengeLabel != null)
                challengeLabel.text = "보스 도전";

            // 새 전투가 시작될 때마다 시계 표시를 무효화한다. 그러지 않으면 이전
            // 전투가 끝난 초와 같은 값으로 시작하는 경우 첫 갱신이 통째로 생략된다
            if (phase != BossFight.Phase.Fighting)
            {
                shownSeconds = -1;
                shownPlayerHealth = -1;
            }

            // 달려가는 동안은 제한 시간이 가득 찬 채로 서 있는다. 0으로 두면
            // 도착하는 순간 0에서 30으로 튀어 시계가 고장 난 것처럼 보인다
            if (phase == BossFight.Phase.Approaching && timerLabel != null)
                timerLabel.text = Mathf.CeilToInt(StageCurve.BossTimeLimitSeconds) + "초";
        }

        private void Update()
        {
            if (fight == null || fight.Current != BossFight.Phase.Fighting) return;

            // fillAmount는 셰이더 파라미터라 메시를 다시 만들지 않는다. 매 프레임
            // 써도 되는 몇 안 되는 UI 값이고, 체력 바는 끊기면 곧바로 티가 난다
            if (healthFill != null) healthFill.fillAmount = fight.BossHealthFraction;

            if (playerHealth != null)
            {
                if (playerHealthFill != null) playerHealthFill.fillAmount = playerHealth.Fraction;

                // 숫자는 정수로만 바꾼다. 회복이 매 프레임 소수점을 올리는데
                // 그때마다 TMP 메시를 다시 만들면 보스전 내내 재생성이 돈다.
                //
                // 현재값은 올림, 최대값은 반올림을 쓰다가 "2811 / 2810"이 나왔다.
                // 체력이 가득 찬 상태에서 소수점이 남으면 올림과 반올림이 서로 다른
                // 정수로 가기 때문이다. 둘 다 올림으로 맞추고, 표시값을 최대값에서
                // 한 번 더 자른다 - 회복이 상한을 넘지 않는데 화면만 넘는 것은
                // 계산이 틀린 것처럼 보인다
                int max = Mathf.CeilToInt((float)playerHealth.MaxHealth);
                int shown = Mathf.Min(max, Mathf.CeilToInt((float)playerHealth.Current));

                if (shown != shownPlayerHealth && playerHealthLabel != null)
                {
                    shownPlayerHealth = shown;
                    playerHealthLabel.text = shown + " / " + max;
                }
            }

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
