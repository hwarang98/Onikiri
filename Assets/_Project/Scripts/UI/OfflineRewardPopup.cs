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
        }

        private void OnDestroy()
        {
            if (claimButton != null) claimButton.onClick.RemoveListener(Hide);
        }

        /**
         * @brief 팝업을 띄운다.
         *
         * capped가 true면 상한(8시간)에 걸렸다는 것을 함께 알린다. 그것을 숨기면
         * 플레이어는 12시간 자리를 비우고도 8시간치만 받은 이유를 알 수 없다.
         */
        public void Show(BigDouble amount, TimeSpan awayFor, bool capped)
        {
            Wire();

            // SetActive(true)보다 먼저 세워야 한다. 그 호출 안에서 Awake가 실행되고,
            // Awake는 이 값을 보고 숨길지 말지를 정한다
            shown = true;

            if (titleLabel != null) titleLabel.text = "오프라인 보상";

            if (durationLabel != null)
                durationLabel.text = "자리를 비운 동안  " + NumberFormatter.FormatDuration(awayFor)
                                     + (capped ? "  (상한 도달)" : string.Empty);

            if (amountLabel != null)
                amountLabel.text = NumberFormatter.Format(amount) + " 획득했습니다";

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
