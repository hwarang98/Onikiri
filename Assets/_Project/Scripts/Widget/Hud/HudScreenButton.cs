using Onikiri.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 상단 바에서 화면 하나를 여닫는 버튼 (37단계).
     *
     * 하단 탭(LockedTab)과 같은 토글 규칙이다 - 같은 버튼이 열고 닫는다.
     * LockedTab을 그대로 쓰지 않는 이유는 그쪽이 탭 캡션·배지·잠금 문구까지
     * 라벨에 직접 쓰기 때문이다. 상단 바 버튼의 라벨은 HUD(HUDStage, LevelHud)가
     * 이미 쓰고 있어서, 두 컴포넌트가 같은 라벨을 다투면 마지막에 쓴 쪽이 이긴다.
     *
     * 재선택 잠금(needsReselect)은 문구가 아니라 밝기로 말한다 - 상단 바에는
     * "11스테이지부터" 같은 안내를 쓸 자리가 없고, 스테이지 표시 자체는 잠겨
     * 있어도 읽혀야 한다.
     */
    public sealed class HudScreenButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("여닫을 화면. 꺼진 채 저장돼 있고 이 버튼이 켠다")]
        [SerializeField] private GameObject screen;

        [Tooltip("재선택이 열려야(최전선 st11) 눌리는 버튼인가")]
        [SerializeField] private bool needsReselect;

        /**
         * @brief 홈 버튼 모드 (개선안 v2 - 초상화가 캐릭터 패널을 연다).
         *
         * 하단 캐릭터 탭(LockedTab.homeTab)과 같은 규칙이다: 자기 화면을
         * 토글하지 않고 **덮고 있는 다른 화면들만 닫는다.** 바탕(GrowthPanel)은
         * 밴드의 바닥층이라 끌 수 없고, 모두 닫힌 상태가 곧 캐릭터 화면이다.
         * screen 참조는 비워 둔다.
         */
        [Tooltip("홈 버튼. 화면을 토글하지 않고 다른 화면들만 닫는다")]
        [SerializeField] private bool homeButton;

        /** 열 때 닫을 다른 화면들. 하단 탭과 같은 상호 배타 규칙 (38단계) */
        [SerializeField] private GameObject[] otherScreens;

        private StageProgress progress;

        /**
         * @brief 눌림을 잠시 막는다. **같은 버튼이 다른 것을 여는 동안** 쓴다 (4단계 §3).
         *
         * 가이드 카드는 평소 퀘스트 화면을 여는 문이지만, 귀문 대기 중에는
         * 그 자리가 「일문 도전」이 된다. 두 리스너가 같은 `Button`에 붙어 있고
         * 서로를 모르므로, 한 번의 탭에 퀘스트 화면과 귀문이 **함께** 열린다.
         *
         * 컴포넌트를 껐다 켜는 대신 이 스위치를 쓰는 이유는 `Start`가 리스너를
         * 붙이기 때문이다 - 껐다 켜면 구독이 두 벌이 되거나 영영 끊긴다.
         */
        public bool Suppressed { get; set; }

        /** {@link SuppressThisFrame}가 찍어 둔 프레임 */
        private int suppressedFrame = -1;

        /**
         * @brief 이 프레임에는 안 눌린다. **리스너 순서에 안 기대는 쪽 절반.**
         *
         * `Suppressed`만으로는 모자랐다. 가이드 카드가 먼저 실행되면 그 자리에서
         * 귀문이 열리고, `BossFight.Changed`가 **같은 콜스택 안에서** 카드를 다시
         * 그리면서 억제를 풀어 버린다 - 그 다음 순서인 이 버튼이 풀린 억제를 보고
         * 퀘스트 화면을 연다. 실기에서 정확히 그 화면이 나왔다(귀문 + 퀘스트 동시).
         *
         * 두 겹이 순서를 모두 덮는다:
         *
         * ```
         * 이 버튼이 먼저   Suppressed == true 가 막는다
         * 카드가 먼저      카드가 찍은 이 프레임 표식이 막는다
         * ```
         */
        public void SuppressThisFrame()
        {
            suppressedFrame = Time.frameCount;
        }

        private void Start()
        {
            if (button != null) button.onClick.AddListener(Toggle);

            if (needsReselect)
            {
                progress = StageProgress.Instance;
                if (progress != null) progress.Changed += Refresh;
                Refresh();
            }
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Toggle);
            if (progress != null) progress.Changed -= Refresh;
        }

        private void Toggle()
        {
            if (Suppressed || suppressedFrame == Time.frameCount) return;
            if (needsReselect && (progress == null || !progress.IsReselectUnlocked)) return;

            if (homeButton)
            {
                if (otherScreens != null)
                    foreach (var other in otherScreens)
                        if (other != null && other.activeSelf) other.SetActive(false);
                return;
            }

            if (screen == null) return;

            bool opening = !screen.activeSelf;

            if (otherScreens != null)
                foreach (var other in otherScreens)
                    if (other != null && other.activeSelf) other.SetActive(false);

            screen.SetActive(opening);
        }

        private void Refresh()
        {
            if (button == null || progress == null) return;
            button.interactable = progress.IsReselectUnlocked;
        }
    }
}
