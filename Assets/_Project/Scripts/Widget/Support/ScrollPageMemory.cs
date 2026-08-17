using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 탭 한 장의 스크롤 상태 (#6 · #7). 높이와 위치를 자기가 들고 있다.
     *
     * ## 무엇이 고장나 있었는가
     *
     * 탭 여럿이 **하나의 ScrollRect를 공유**한다. 페이지는 켜고 끄는 것으로
     * 바뀌는데, 스크롤은 그 사실을 모른다. 그래서 두 가지가 어긋났다.
     *
     * **콘텐츠 높이가 가장 긴 페이지로 고정돼 있었다** (#6). 강화 목록이
     * 1866px이므로 성장 탭(486px)에서도 1866px만큼 스크롤됐고, 목록이 끝난
     * 뒤로 1300px이 빈 채로 흘렀다 - "안 보일 때까지 스크롤되는" 것의 정체가
     * 이것이다. 퀘스트 탭들도 같은 구조다.
     *
     * **스크롤 위치가 탭 사이에 이월됐다** (#7). 강화를 끝까지 내리고 성장으로
     * 가면 그 위치가 그대로 남는데, 성장 페이지는 그 높이에 아무것도 없다.
     * 화면은 빈 판이고 플레이어는 위로 올려야 자기 탭을 본다.
     *
     * ## 규칙
     *
     * 페이지가 켜질 때 **콘텐츠를 자기 높이로 줄이고**, 자기가 기억해 둔
     * 위치로 되돌린다. 꺼질 때 그 위치를 적어둔다. 그러면 탭마다 독립된
     * 스크롤이 되고, 클램프는 저절로 따라온다 - 콘텐츠가 딱 목록 높이라
     * 목록 밖으로 나갈 자리가 아예 없다.
     *
     * 위치는 세이브가 아니라 **이번 실행 동안의 것**이다. 앱을 껐다 켜면
     * 맨 위에서 시작하는데, 목록의 맨 위는 어느 탭에서나 지금 살 만한
     * 것이고(강화는 공격, 퀘스트는 가장 가까운 것) 그것이 옳은 기본값이다.
     */
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScrollPageMemory : MonoBehaviour
    {
        [Tooltip("이 페이지가 들어 있는 스크롤")]
        [SerializeField] private ScrollRect scroll;

        [Tooltip("스크롤 콘텐츠. 켜질 때 이 rect를 자기 높이로 맞춘다")]
        [SerializeField] private RectTransform content;

        [Tooltip("이 페이지의 실제 높이. 0이면 자기 rect에서 읽는다")]
        [SerializeField] private float pageHeight;

        /**
         * @brief 기억해 둔 위치. **정규화 값이 아니라 픽셀이다.**
         *
         * 정규화(0~1)로 기억하면 페이지 높이가 바뀔 때 같은 0.5가 다른 줄을
         * 가리킨다. 퀘스트 목록은 항목이 늘고 줄므로 실제로 바뀐다.
         */
        private float savedY;

        /** 한 번도 안 열어본 탭은 맨 위에서 시작한다 */
        private bool visited;

        private void OnEnable()
        {
            if (scroll == null || content == null) return;

            float height = pageHeight > 0f ? pageHeight : ((RectTransform)transform).rect.height;

            // 콘텐츠를 이 페이지 높이로. 이 한 줄이 #6이다 - 목록이 끝나는
            // 곳에서 스크롤도 끝난다
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);

            // 뷰포트보다 짧으면 스크롤할 것이 없다. 0으로 두지 않으면
            // 이전 탭에서 내려간 만큼 목록이 위로 밀린 채 굳는다
            float viewport = scroll.viewport != null ? scroll.viewport.rect.height : height;
            float maxY = Mathf.Max(0f, height - viewport);

            float target = visited ? Mathf.Clamp(savedY, 0f, maxY) : 0f;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, target);

            // 관성이 남아 있으면 되돌린 위치에서 그대로 미끄러진다
            scroll.velocity = Vector2.zero;
        }

        private void OnDisable()
        {
            if (content == null) return;

            savedY = content.anchoredPosition.y;
            visited = true;
        }

        /** 빌더가 높이를 알고 있을 때 적어준다. 0이면 런타임에 rect에서 읽는다 */
        public void SetPageHeight(float value)
        {
            pageHeight = value;
        }
    }
}
