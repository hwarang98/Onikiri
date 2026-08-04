using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief UI를 기기의 안전 영역 안으로 밀어 넣는다.
     *
     * 노치, 펀치홀, 제스처 바가 있는 기기에서는 화면의 물리적 네 귀퉁이가 항상 보이는
     * 것이 아니다. Screen.safeArea가 실제로 가려지지 않는 사각형을 알려준다.
     *
     * 월드가 아니라 UI에만 적용한다. 배경과 전투는 화면 끝까지 그려야 노치 옆에 검은
     * 띠가 생기지 않고, 잘려도 잃는 것은 하늘뿐이다. 반면 골드 표시나 강화 버튼이
     * 노치에 가리면 그대로 못 쓰는 UI가 된다.
     *
     * 해상도나 방향이 바뀌면 다시 계산한다. 안드로이드에서는 앱이 살아 있는 동안에도
     * safeArea가 바뀔 수 있다(멀티윈도우, 제스처 바 표시 전환).
     */
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;
        private ScreenOrientation appliedOrientation;

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            // 캐시를 무효화해서 활성화 직후 한 번은 반드시 적용되게 한다
            appliedScreenSize = Vector2Int.zero;
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            if (rect == null) rect = (RectTransform)transform;

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);

            if (safeArea == appliedSafeArea &&
                screenSize == appliedScreenSize &&
                Screen.orientation == appliedOrientation)
                return;

            if (screenSize.x <= 0 || screenSize.y <= 0) return;

            appliedSafeArea = safeArea;
            appliedScreenSize = screenSize;
            appliedOrientation = Screen.orientation;

            // 앵커는 0~1 비율이므로 화면 픽셀을 그대로 나눠 쓸 수 있다. offsetMin/Max를
            // 쓰면 캔버스 스케일이 곱해져 기기마다 다른 값이 되어버린다
            var min = safeArea.position;
            var max = safeArea.position + safeArea.size;

            min.x /= screenSize.x;
            min.y /= screenSize.y;
            max.x /= screenSize.x;
            max.y /= screenSize.y;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
