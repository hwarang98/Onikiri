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

        /** 열 때 닫을 다른 화면들. 하단 탭과 같은 상호 배타 규칙 (38단계) */
        [SerializeField] private GameObject[] otherScreens;

        private StageProgress progress;

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
            if (screen == null) return;
            if (needsReselect && (progress == null || !progress.IsReselectUnlocked)) return;

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
