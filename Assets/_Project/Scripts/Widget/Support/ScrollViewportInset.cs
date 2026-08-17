using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 자기가 켜져 있는 동안 스크롤 뷰포트의 위를 그만큼 눌러둔다.
     *
     * 탭에 딸린 **고정 줄**을 위한 것이다(강화 배수 줄, #9). 그 줄은 목록과 함께
     * 스크롤되면 안 된다 - 배수를 바꾸려고 목록 맨 위까지 올라가야 하면 배수
     * 버튼이 아끼려던 탭 횟수를 스크롤로 도로 물어낸다.
     *
     * 그런데 고정 줄은 **한 탭에만 있다.** 뷰포트를 빌드 시점에 줄여버리면 성장·
     * 전직 탭도 같이 짧아지고, 그 둘은 지금 스크롤 없이 들어가는 높이라 이유
     * 없이 스크롤이 생긴다(UpgradePanelBuilder.VerifyPageHeights가 재는 값이
     * 바로 그것이다).
     *
     * 그래서 줄 자신이 켜질 때 밀고 꺼질 때 되돌린다. 탭 전환은 GrowthPanelTabs가
     * roots를 켜고 끄는 것뿐이고(그 클래스 주석의 "탭에 딸린 고정 줄"이 여기다),
     * 레이아웃 지식은 이 줄 하나에 남는다.
     */
    public sealed class ScrollViewportInset : MonoBehaviour
    {
        [Tooltip("눌러둘 스크롤 뷰포트")]
        [SerializeField] private RectTransform viewport;

        [Tooltip("위에서 밀어낼 픽셀. 이 줄이 세로로 먹는 몫과 같아야 한다")]
        [SerializeField] private float inset = 80f;

        /**
         * @brief 되돌릴 값. **원본을 한 번만 기억한다.**
         *
         * 매번 현재 값에서 빼고 더하면 켜고 끄기를 반복하는 사이에 오차가 쌓이고,
         * 도메인 리로드 뒤 처음 켜질 때의 값이 이미 밀린 값일 수도 있다.
         */
        private float? restoreTop;

        private void OnEnable()
        {
            if (viewport == null) return;

            if (restoreTop == null) restoreTop = viewport.offsetMax.y;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, restoreTop.Value - inset);
        }

        private void OnDisable()
        {
            if (viewport == null || restoreTop == null) return;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, restoreTop.Value);
        }
    }
}
