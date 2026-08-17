using System;
using Onikiri.Cloud;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief "구글 로그인/연동" 버튼 한 벌. 타이틀과 설정이 **같은 것**을 쓴다.
     *
     * 연동 흐름의 입구가 세 곳이 됐다(랭킹 54단계 · 타이틀 · 설정). 입구마다
     * 버튼 코드를 따로 적으면 "숨김 규칙"과 "연타 방지"가 세 벌이 되고,
     * 한 벌만 고쳐지는 날 어느 입구는 함정 버튼이 된다. 이 컴포넌트가
     * 그 규칙의 단일 출처다:
     *
     *   숨김    이 기기에서 못 쓰거나(에디터) 이미 연동됐으면 **버튼째 감춘다**.
     *           눌러도 아무 일 없는 버튼은 거짓말이다(20단계 함정 버튼 규칙)
     *   잠금    연동이 도는 동안 interactable을 내린다 - 연타가 계정 선택창을
     *           두 장 띄우는 것을 막는다
     *   호출    AccountLink.LinkAsync(구글) 하나뿐이다. 연동이냐 복구냐는
     *           저쪽이 정한다 (55단계)
     *
     * 결과는 Finished(changed)로 알린다. 타이틀은 changed=true에서 게임으로
     * 들어가고, 설정은 상태줄만 다시 그린다 - 무엇을 할지는 입구가 정하고,
     * 어떻게 연동하는지는 여기가 정한다.
     */
    public sealed class GoogleLinkButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        /** 감출 때 버튼만이 아니라 이 뿌리를 통째로 끈다. 없으면 버튼만 */
        [SerializeField] private GameObject hideRoot;

        /** 연동 시도가 끝났다. 인자는 "정체성이 실제로 바뀌었는가" */
        public event Action<bool> Finished;

        /** 지금 이 기기에서 이 버튼이 뜻이 있는가 (숨김 규칙의 반대) */
        public static bool IsUseful
        {
            get
            {
                return AuthProviders.Google.IsAvailable
                       && AccountLink.State != AccountState.Linked;
            }
        }

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            AccountLink.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            AccountLink.Changed -= Refresh;
        }

        /** 숨김·잠금 규칙을 다시 적용한다. 연동 상태가 바뀔 때마다 불린다 */
        public void Refresh()
        {
            if (this == null || button == null) return;

            bool useful = IsUseful;

            var root = hideRoot != null ? hideRoot : button.gameObject;
            if (root.activeSelf != useful) root.SetActive(useful);

            button.interactable = useful && !AccountLink.IsBusy;
        }

        private void OnClick()
        {
            var _ = RunAsync();
        }

        private async System.Threading.Tasks.Task RunAsync()
        {
            if (AccountLink.IsBusy) return;

            if (button != null) button.interactable = false;

            bool changed = await AccountLink.LinkAsync(AuthProviders.Google);

            // 파괴된 뒤에 응답이 오는 경로가 실재한다 (LeaderboardPanel과 같은 사정)
            if (this == null) return;

            Refresh();

            var handler = Finished;
            if (handler != null) handler(changed);
        }
    }
}
