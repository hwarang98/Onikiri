using Onikiri.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 새 기기의 **인수 확인** 팝업 (63단계). 두 개 중 첫 번째다.
     *
     * ```
     * 다른 기기에서 플레이 중입니다.
     *
     * 이 기기에서 이어서 플레이하면
     * 기존 기기의 플레이가 종료됩니다.
     *
     * [취소]  [이 기기로 이어하기]
     * ```
     *
     * ## 이 화면이 뜨는 동안 게임은 시작되지 않았다
     *
     * `GameSession.BootRoutine`이 세션 게이트에서 멈춰 있다. 뒤에 보이는 것은
     * 아직 아무것도 얹히지 않은 씬이고, 그래서 이 팝업 뒤로 눌릴 버튼이 없다 -
     * 딤을 죽여 두는 것(dimIsInert)은 **딤 탭이 취소와 구분되지 않기** 때문이지
     * 뒤를 막기 위해서가 아니다.
     *
     * ## 중복 클릭
     *
     * 요청이 나간 뒤에는 두 버튼이 다 꺼진다. 한 번 더 누르면
     * `CloudSaveTakeover.Confirm`이 국면을 보고 조용히 무시하지만, **꺼진 버튼이
     * 그 사실을 화면에도 적는다** - 눌리는데 아무 일도 안 일어나는 버튼은
     * 사람에게 앱이 멈춘 것으로 읽힌다.
     */
    public sealed class CloudTakeoverPanel : MonoBehaviour
    {
        [SerializeField] private GameObject body;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button retryButton;

        private const string AskMessage =
            "다른 기기에서 플레이 중입니다.\n\n"
            + "이 기기에서 이어서 플레이하면\n기존 기기의 플레이가 종료됩니다.";

        private const string WaitMessage =
            "이전 기기의 응답을 기다립니다.\n\n"
            + "응답이 없으면 잠시 뒤\n이 기기가 이어받습니다.";

        private const string CancelledMessage =
            "이 기기에서는 시작하지 않았습니다.\n\n"
            + "다른 기기의 플레이가 끝났는지\n다시 확인할 수 있습니다.";

        private const string TimedOutMessage =
            "이전 기기가 응답하지 않습니다.\n\n"
            + "잠시 뒤 다시 시도해 주세요.";

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);

            CloudSaveTakeover.PhaseChanged -= OnPhaseChanged;
            CloudSaveTakeover.PhaseChanged += OnPhaseChanged;

            SetVisible(false);
        }

        private void OnDestroy()
        {
            CloudSaveTakeover.PhaseChanged -= OnPhaseChanged;
        }

        private void Update()
        {
            // 안드로이드 뒤로 가기 = 취소. 승인을 기다리는 동안에만 받는다 -
            // 요청이 나간 뒤의 뒤로 가기는 아무것도 되돌리지 못한다
            if (!IsShowing) return;
            if (!BackPressed()) return;
            if (CloudSaveTakeover.Phase != CloudSaveTakeoverPhase.AwaitingConfirm) return;

            OnCancel();
        }

        /**
         * @brief 국면 하나에 화면 하나. **표시 여부까지 여기서 정한다.**
         *
         * 팝업을 여는 자리를 밖에 두지 않는 이유는 국면이 여섯이기 때문이다 -
         * 열고 닫는 결정이 흩어지면 "취소했는데 팝업이 남는" 조합이 반드시 하나
         * 생긴다. 국면 -> 화면은 전사(全射)여야 한다.
         */
        private void OnPhaseChanged(CloudSaveTakeoverPhase phase)
        {
            switch (phase)
            {
                case CloudSaveTakeoverPhase.AwaitingConfirm:
                    Draw(AskMessage, confirm: true, cancel: true, retry: false);
                    break;

                case CloudSaveTakeoverPhase.Requesting:
                case CloudSaveTakeoverPhase.Waiting:
                    Draw(WaitMessage, confirm: false, cancel: false, retry: false);
                    break;

                case CloudSaveTakeoverPhase.Cancelled:
                    Draw(CancelledMessage, confirm: false, cancel: false, retry: true);
                    break;

                case CloudSaveTakeoverPhase.TimedOut:
                    Draw(TimedOutMessage, confirm: false, cancel: false, retry: true);
                    break;

                default:
                    // Acquired · Offline · Checking · Idle - 화면에 아무 일도 없다
                    SetVisible(false);
                    break;
            }
        }

        private void Draw(string message, bool confirm, bool cancel, bool retry)
        {
            SetVisible(true);

            if (messageLabel != null) messageLabel.text = message;

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(confirm);
                confirmButton.interactable = confirm;
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(cancel);
                cancelButton.interactable = cancel;
            }

            if (retryButton != null) retryButton.gameObject.SetActive(retry);
        }

        public void OnConfirm()
        {
            // 버튼을 먼저 끄고 요청을 낸다. 순서가 반대면 요청이 나가는 사이의
            // 프레임에 두 번째 클릭이 들어온다 - 그 요청은 takeoverAt을 갱신해
            // 강제 인수 시계를 처음부터 다시 돌린다
            if (confirmButton != null) confirmButton.interactable = false;
            if (cancelButton != null) cancelButton.interactable = false;

            CloudSaveTakeover.Confirm();
        }

        public void OnCancel()
        {
            if (cancelButton != null) cancelButton.interactable = false;
            CloudSaveTakeover.Cancel();
        }

        /** 다시 확인 = 씬 재로드. 새 실행이 게이트를 처음부터 지난다 */
        public void OnRetry()
        {
            if (retryButton != null) retryButton.interactable = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SuppressReloadForTests) return;
#endif
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }


        /**
         * @brief 안드로이드 뒤로 가기. **Input System으로 읽는다.**
         *
         * 이 프로젝트는 `activeInputHandler: 1`이라 옛 `UnityEngine.Input`은
         * 읽는 순간 예외를 던진다(PlayMode 여덟이 그것을 잡았다). Input System
         * 에서 안드로이드 back은 `Keyboard.escapeKey`로 온다.
         *
         * 키보드가 없는 기기·에디터에서는 `Keyboard.current`가 null이다 -
         * 그것을 안 보면 팝업이 뜨는 순간 매 프레임 예외가 난다.
         */
        private static bool BackPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        /**
         * @brief 뿌리는 **늘 켜져 있고**, 보이는 것은 창과 딤이다.
         *
         * ## 왜 뿌리를 끄지 않는가 - 실제로 물렸다
         *
         * 이 팝업들은 남이 열어 주지 않는다. 자기 이벤트(국면 변화·회수)를
         * 구독해 **스스로** 뜬다. 그런데 꺼진 GameObject에서는 `Awake`가 돌지
         * 않는다 - 구독이 영영 안 걸리고, 팝업은 영영 안 뜬다. PlayMode 검사
         * 넷이 그것을 잡았다("묻지 않고 지나갔다").
         *
         * `CloudConflictPanel`은 설정 화면이 켜 주므로 그 자리에서 `Awake`가
         * 돌았다. 63단계의 둘은 켜 줄 사람이 없다는 것이 다른 점이다.
         *
         * 대신 딤을 함께 죽인다. 뿌리만 켜 두고 딤을 살려 두면 **보이지 않는
         * 전체 화면 판**이 게임 위에 깔려 아무것도 눌리지 않는다.
         */
        private void SetVisible(bool visible)
        {
            // ★ **인트로보다 위여야 한다.** `IntroFlow.Awake`가 스스로
            // `SetAsLastSibling()`을 해서 부팅 화면을 맨 위에 놓는다("다른 빌더가
            // SafeArea 뒤에 무엇을 더 세우든"). 이 팝업은 그 인트로가 아직 떠
            // 있는 동안 뜨므로, 형제 순서를 잡지 않으면 **로그에는 떴는데
            // 화면에는 없는** 상태가 된다 - 실기에서 그대로 물렸다.
            if (visible) transform.SetAsLastSibling();

            if (body != null) body.SetActive(visible);

            var dim = GetComponent<UnityEngine.UI.Image>();
            if (dim != null)
            {
                dim.enabled = visible;
                dim.raycastTarget = visible;
            }
        }

        /** 지금 화면에 떠 있는가. 검사와 진단이 읽는다 */
        public bool IsShowing
        {
            get { return body != null && body.activeSelf; }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool SuppressReloadForTests;
#endif
    }
}
