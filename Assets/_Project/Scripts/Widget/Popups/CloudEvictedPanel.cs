using Onikiri.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 이전 기기의 **종료 알림** 팝업 (63단계). 두 개 중 두 번째다.
     *
     * ```
     * 다른 기기에서 접속했습니다.
     * 현재 기기의 플레이를 종료합니다.
     *
     * [확인]
     * ```
     *
     * ## 이 팝업보다 정지가 먼저다
     *
     * `CloudSavePlayLock.Engage`가 깃발을 세운 **뒤에** 이벤트를 쏘고, 이 화면은
     * 그 이벤트를 받는다. 감지와 그리기 사이의 한두 프레임에 10연 버튼이 눌려도
     * 그 뽑기는 이미 막혀 있다 - 지갑·처치·저장·동기화가 전부 깃발을 본다.
     *
     * ## Firebase 로그인은 건드리지 않는다
     *
     * 회수하는 것은 **권한**이지 계정이 아니다. 확인을 누르면 씬을 다시 열고,
     * 그 새 실행은 같은 uid로 세션 게이트를 처음부터 지난다 - 다른 기기가
     * 여전히 활성이면 인수 확인 팝업이 다시 뜨고(실기 검증 8번), 끝나 있으면
     * 그냥 들어간다.
     *
     * ## 로컬 세이브는 지우지 않는다
     *
     * 회수 시점의 디스크는 그대로 남는다. 그것이 복구 자료다 - 이 기기가
     * 마지막으로 굳힌 진행이 서버 정본과 다르면 다음 부팅의 판정이 그것을
     * 사람에게 보여 준다(60단계 충돌 화면). 조용히 덮지도, 지우지도 않는다.
     */
    public sealed class CloudEvictedPanel : MonoBehaviour
    {
        [SerializeField] private GameObject body;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button confirmButton;

        private const string Message =
            "다른 기기에서 접속했습니다.\n현재 기기의 플레이를 종료합니다.";

        private bool leaving;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

            CloudSavePlayLock.Engaged -= OnEngaged;
            CloudSavePlayLock.Engaged += OnEngaged;

            SetVisible(false);
        }

        private void OnDestroy()
        {
            CloudSavePlayLock.Engaged -= OnEngaged;
        }

        private void Update()
        {
            // 뒤로 가기로는 못 닫는다. 닫아 봐야 플레이가 돌아오지 않고,
            // 사람에게는 "멈춘 게임"만 남는다 - 확인이 유일한 출구다
            if (!IsShowing) return;
            if (!BackPressed()) return;

            OnConfirm();
        }

        /** `Engaged`는 **한 번만** 울린다(PlayLock이 지킨다) - 팝업도 한 번만 뜬다 */
        private void OnEngaged()
        {
            SetVisible(true);

            if (messageLabel != null) messageLabel.text = Message;
            if (confirmButton != null) confirmButton.interactable = true;
        }

        public void OnConfirm()
        {
            if (leaving) return;
            leaving = true;

            if (confirmButton != null) confirmButton.interactable = false;

            // ★ **다음 실행의 몫을 비운다.** 씬을 다시 열어도 정적 상태는
            // 살아남는다 - 세션 id와 세대를 그대로 두면 새 실행이 첫 폴링에서
            // 또 상실을 보고 스스로를 잠근다(실기에서 물렸다). 그 뒤에는
            // 인수에 성공해도 저장이 막힌 채로 돈다.
            CloudSaveSession.BeginNewRun();

            // 깃발을 내리고 나간다. 내리지 않으면 재로드된 씬이 잠긴 채로
            // 열리고, 그 화면에서는 아무것도 눌리지 않는다(timeScale도 0이다)
            CloudSavePlayLock.Release();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LeftToTitle = true;

            // 검사는 "타이틀로 갔다"까지만 재고 재로드는 스스로 한다 -
            // CloudConflictPanel과 같은 규칙이다
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

        /** 확인을 눌러 타이틀로 나갔는가. 재로드를 멈춘 검사가 읽는다 */
        public static bool LeftToTitle;
#endif
    }
}
