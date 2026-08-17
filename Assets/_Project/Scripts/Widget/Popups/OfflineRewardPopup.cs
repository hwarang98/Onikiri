using System;
using Onikiri.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 복귀 시 한 번 뜨는 방치 보상 팝업.
     *
     * 보상 지급은 여기서 하지 않는다. 팝업은 이미 지갑에 들어간 금액을 보여주기만
     * 한다. 지급을 버튼에 걸면 팝업을 띄우지 못한 경우(다른 씬, UI 오류)에 보상이
     * 통째로 사라진다.
     */
    public sealed class OfflineRewardPopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text durationLabel;
        [SerializeField] private TMP_Text amountLabel;
        [SerializeField] private Button claimButton;

        /**
         * @brief 광고 리워드 자리(20-3). **자리만이고 눌리지 않는다.**
         *
         * 여기 두는 이유는 방치 보상이 광고 리워드의 표준 자리이기 때문이다 -
         * 이미 "공짜로 받은 것"이 화면에 떠 있는 순간이 배로 늘릴 제안을 받아
         * 들이기 가장 쉬운 지점이고, 그래서 이 장르가 거의 예외 없이 여기에 둔다.
         *
         * 실제 광고 SDK 연동은 수익화 단계다(이번 범위 밖). 그때까지 **거짓말은
         * 하지 않는다** - 눌리는데 아무 일도 없는 버튼이 아니라 "준비 중"이라고
         * 적힌 잠긴 버튼으로 선다. LockedTab이 스킬·전직에서 지키는 규칙과 같다.
         */
        [SerializeField] private Button doubleButton;
        [SerializeField] private TMP_Text doubleLabel;

        [Tooltip("광고 연동 전까지 버튼에 적을 문구. 비우면 잠금 표시를 하지 않는다")]
        [SerializeField] private string doublePendingLabel = "골드 2배 준비 중";

        /** Show가 Awake보다 먼저 올 수 있어서 배선을 두 진입점에서 공유한다 */
        private bool wired;

        /** Show가 이미 다녀갔는지. Awake가 그것을 되돌리지 않게 한다 */
        private bool shown;

        /**
         * @brief 시작할 때 숨긴다. 단, 이미 띄워진 뒤라면 건드리지 않는다.
         *
         * 이 조건이 없으면 팝업이 영원히 뜨지 않는다. 씬에는 팝업이 꺼진 채로
         * 저장돼 있고, **꺼져 있는 오브젝트의 Awake는 켜지는 순간에야 실행된다.**
         * 그래서 순서가 이렇게 된다:
         *
         *   GameSession.Start -> Show() -> root.SetActive(true)
         *                                   -> 그 안에서 Awake가 지금 실행됨
         *                                   -> Awake가 SetActive(false)
         *
         * 보상은 지갑에 정상적으로 들어가고 로그도 남기 때문에, 겉으로는 "팝업만
         * 안 뜨는" 것처럼 보여서 원인을 찾기 어려웠다.
         */
        private void Awake()
        {
            Wire();
            if (!shown) root.SetActive(false);
        }

        private void Wire()
        {
            if (wired) return;
            wired = true;

            if (root == null) root = gameObject;
            if (claimButton != null) claimButton.onClick.AddListener(Hide);

            // 리스너를 달지 않는다. 다는 순간 "언젠가 동작하는 버튼"이 되고,
            // 광고를 붙이는 사람이 여기 이미 뭔가 연결돼 있다고 믿게 된다
            if (doubleButton != null) doubleButton.interactable = false;
            if (doubleLabel != null && !string.IsNullOrEmpty(doublePendingLabel))
                doubleLabel.text = doublePendingLabel;
        }

        private void OnDestroy()
        {
            if (claimButton != null) claimButton.onClick.RemoveListener(Hide);

            // static 이벤트라 구독을 남기면 씬을 다시 열어도 죽은 객체가 붙어
            // 있는다
            if (waiting) IntroFlow.Entered -= OnIntroEntered;
        }

        /**
         * @brief 팝업을 띄운다.
         *
         * capped가 true면 상한(8시간)에 걸렸다는 것을 함께 알린다. 그것을 숨기면
         * 플레이어는 12시간 자리를 비우고도 8시간치만 받은 이유를 알 수 없다.
         */
        public void Show(BigDouble amount, TimeSpan awayFor, bool capped)
        {
            /**
             * @brief **게임에 들어오기 전에는 뜨지 않는다.**
             *
             * 이 팝업은 세이브를 읽는 순간 뜬다(GameSession.GrantOfflineReward).
             * 그런데 그 순간은 아직 부팅 오버레이가 화면을 덮고 있는 때라,
             * 로고와 "터치하여 시작" 위에 "2시간 방치 +147K 골드"가 떴다 -
             * 시작하지도 않은 게임이 보상부터 내미는 화면이다.
             *
             * 지급은 그대로 둔다. 미루는 것은 **보여주는 일**뿐이고, 그것이
             * 이 클래스의 머리 주석("지급은 여기서 하지 않는다")과 같은 결이다 -
             * 팝업을 못 띄운 경우에도 골드는 이미 지갑에 있다.
             *
             * 인트로가 없는 씬에서는 IntroFlow.HasEntered가 처음부터 참이라
             * 예전 그대로 즉시 뜬다.
             */
            if (!IntroFlow.HasEntered)
            {
                pending = true;
                pendingAmount = amount;
                pendingAway = awayFor;
                pendingCapped = capped;

                // 두 번 달지 않는다. 한 판에 Show가 두 번 오는 경로는 없지만,
                // 이벤트 구독은 새는 쪽이 조용하다
                if (!waiting)
                {
                    waiting = true;
                    IntroFlow.Entered += OnIntroEntered;
                }
                return;
            }

            ShowNow(amount, awayFor, capped);
        }

        private bool pending;
        private bool waiting;
        private BigDouble pendingAmount;
        private TimeSpan pendingAway;
        private bool pendingCapped;

        private void OnIntroEntered()
        {
            IntroFlow.Entered -= OnIntroEntered;
            waiting = false;

            if (!pending) return;
            pending = false;

            ShowNow(pendingAmount, pendingAway, pendingCapped);
        }

        private void ShowNow(BigDouble amount, TimeSpan awayFor, bool capped)
        {
            Wire();

            // SetActive(true)보다 먼저 세워야 한다. 그 호출 안에서 Awake가 실행되고,
            // Awake는 이 값을 보고 숨길지 말지를 정한다
            shown = true;

            if (titleLabel != null) titleLabel.text = "오프라인 보상";

            // 시간을 앞에 세운다. 이 줄이 하는 일은 금액의 **근거**를 대는 것이라
            // ("8시간 방치했으니 이만큼"), 시간이 먼저 읽혀야 순서가 맞는다
            if (durationLabel != null)
                durationLabel.text = NumberFormatter.FormatDurationKo(awayFor) + " 방치"
                                     + (capped ? "  (상한 도달)" : string.Empty);

            // 부호를 붙인다. 이 팝업의 숫자는 잔액이 아니라 **증가분**인데,
            // 상단 바에 같은 서체로 총 골드가 떠 있어서 부호가 없으면 둘이
            // 같은 종류의 숫자로 읽힌다
            if (amountLabel != null)
                amountLabel.text = "+" + NumberFormatter.Format(amount) + " 골드";

            root.SetActive(true);
        }

        public void Hide()
        {
            shown = false;
            if (root != null) root.SetActive(false);
        }

        public bool IsVisible { get { return root != null && root.activeSelf; } }
    }
}
