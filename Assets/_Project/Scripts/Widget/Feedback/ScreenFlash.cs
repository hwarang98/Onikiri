using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 귀참 전용 화면 연출. **가장자리가 확 닫혔다 열린다.**
     *
     * ## 27단계의 흰 풀스크린을 걷어냈다
     *
     * 처음에는 화면 전체를 흰색으로 0.55까지 덮었다. 무게는 났지만 두 가지가
     * 걸렸다 - 그 0.09초 동안 **무엇이 죽었는지 볼 수 없고**, 먹빛·자주색인 이
     * 게임의 톤에서 흰 화면만 다른 게임처럼 보인다.
     *
     * 이번에는 반대로 간다. 가운데는 건드리지 않고 **가장자리만** 움직인다:
     *
     *   엣지 번쩍   비네트 모양의 붉은/흰 빛이 테두리에서 짧게 터진다 (0.10초)
     *   비네트     검은 테두리가 확 닫혔다가 천천히 열린다 (0.34초)
     *
     * 가운데가 비어 있으므로 벤 자리와 데미지 숫자가 계속 보인다. 무게는 화면을
     * 가리는 것이 아니라 **히트스톱과 셰이크**가 담당한다 - 귀참의 뽕맛은
     * 화면이 멈추는 것이지 거대한 그림이 아니다.
     *
     * ## 두 겹의 방향이 반대다
     *
     * 엣지 번쩍은 밝게 더하고 비네트는 어둡게 누른다. 하나만 쓰면 각각
     * "화면이 하얘졌다" 또는 "화면이 어두워졌다"인데, 겹치면 **테두리에서 빛이
     * 터지고 그 뒤를 어둠이 조인다** - 그것이 무거운 한 방의 모양이다.
     *
     * ## 히트스톱 규칙
     *
     * **unscaled 시간으로 돈다.** 23단계에 정한 규칙에서 화면 흔들림 하나만
     * 예외였고(정지 중에도 흔들려야 무게가 산다), 이 연출도 같은 이유로 예외다 -
     * 귀참은 강한 정지를 함께 내므로, 스케일 타임으로 두면 번쩍이 정지 동안
     * 그대로 멈춰 있다가 정지가 풀린 뒤에야 사라진다. 그러면 "멈추고 번쩍"이
     * 아니라 "화면 테두리가 한참 붉게 떠 있다"가 된다.
     */
    public sealed class ScreenFlash : MonoBehaviour
    {
        [Tooltip("가장자리에서 터지는 빛. 비네트와 같은 모양이라 가운데는 비어 있다")]
        [SerializeField] private Image edgeFlash;

        [Tooltip("가장자리를 조이는 어둠")]
        [SerializeField] private Image vignette;

        [Range(0f, 1f)]
        [SerializeField] private float edgePeak = 0.85f;

        [SerializeField] private float edgeSeconds = 0.10f;

        [Range(0f, 1f)]
        [SerializeField] private float vignettePeak = 0.88f;

        [SerializeField] private float vignetteSeconds = 0.34f;

        /**
         * @brief 비네트가 최대까지 닫히는 데 걸리는 시간.
         *
         * 열리는 시간(vignetteSeconds - 이 값)보다 훨씬 짧아야 한다. **닫힘은
         * 사건이고 열림은 여운이다** - 같은 속도로 여닫으면 숨을 쉬는 것처럼
         * 보이고, 한 방의 무게가 사라진다.
         */
        [SerializeField] private float vignetteCloseSeconds = 0.05f;

        private float elapsed;
        private bool playing;

        /**
         * @brief 빌더가 적어준 엣지 색. 틴트를 준 시전 뒤에 되돌릴 값이다.
         *
         * 채집을 Awake에만 맡길 수 없다. 이 오브젝트는 꺼진 채로 저장되므로
         * Awake가 **처음 Play하는 순간**에 도는데, 그 Play가 틴트를 주는
         * 쪽이면 Awake는 이미 덧칠된 색을 원본으로 적어버린다. 그래서 Play가
         * 활성화보다 먼저 한 번 채집한다.
         */
        private Color edgeBase = Color.white;
        private bool edgeBaseRead;

        /**
         * @brief 알파만 비운다. **자기 자신을 끄지 않는다.**
         *
         * 이 오브젝트는 꺼진 채로 씬에 저장되므로 Awake가 씬 로드가 아니라
         * **처음 켜지는 순간**에 도는데, 켜는 쪽이 Play다. 여기서 SetActive(false)를
         * 하면 `Play -> SetActive(true) -> Awake -> SetActive(false)`가 되어
         * **첫 시전이 통째로 삼켜진다.** 27단계에 실제로 그렇게 만들었고, 화면에서는
         * "귀참인데 번쩍이 없다"로만 나타났다 - 두 번째 시전부터 정상이라
         * 간헐적으로 보인다.
         *
         * 끄는 것은 Update가 끝날 때와 빌더뿐이다.
         */
        private void Awake()
        {
            CaptureEdgeBase();
            SetAlpha(edgeFlash, 0f);
            SetAlpha(vignette, 0f);
        }

        private void CaptureEdgeBase()
        {
            if (edgeBaseRead || edgeFlash == null) return;
            edgeBase = edgeFlash.color;
            edgeBaseRead = true;
        }

        /**
         * @brief 연출을 시작한다. **활성화가 먼저, 상태가 나중.**
         *
         * 순서가 규칙이다. `SetActive(true)`가 Awake를 그 자리에서 돌리므로,
         * 상태를 먼저 쓰면 Awake가 그것을 덮어쓴다.
         */
        public void Play()
        {
            Begin(Color.white, false, 1f);
        }

        /**
         * @brief 엣지 색을 갈아끼우고 한 번 터뜨린다.
         *
         * 귀참은 이 연출의 주인이라 빌더가 적어준 색을 쓰고, **빌려 쓰는 쪽만**
         * 색을 가져온다 - 45b의 흑야 영체가 첫 손님이다(먹빛에 붉은 기).
         * 같은 번쩍이 두 사건에 쓰이면 화면이 "또 귀참인가"로 읽히는데,
         * 색 하나로 갈리면 새 연출을 만들지 않고도 둘이 구분된다.
         *
         * 알파는 여기서 건드리지 않는다. 세기는 edgePeak가 정하고 이 함수는
         * 색상만 바꾼다 - 빌려 쓰는 쪽이 세기까지 바꾸면 예산이 갈라진다.
         */
        public void Play(Color edgeTint)
        {
            Begin(edgeTint, true, 1f);
        }

        /**
         * @brief 색과 **지속 배율**을 함께 받는다 (15종 재설계).
         *
         * 세기가 아니라 지속만 배수를 받는 이유는 클램프다. edgePeak 0.85에
         * 1.4를 곱하면 1.19가 되어 잘리고, 잘리면 "더 세게"가 화면에서 안
         * 읽힌다 - 두 배를 줘도 같은 흰 화면이다. 세기의 차이는 **색**이
         * 말하고 이 값은 시간만 늘린다.
         *
         * 귀참·혈폭은 흰색 x1.0으로 이 경로를 지나므로 기존 연출과 같다.
         */
        public void Play(Color edgeTint, float durationScale)
        {
            Begin(edgeTint, true, durationScale);
        }

        private void Begin(Color edgeTint, bool tinted, float durationScale)
        {
            // 0이나 음수가 오면 연출이 시작하자마자 끝난다. 배선 실수를
            // 화면에서 알아채려면 최소 길이가 있어야 한다
            this.durationScale = Mathf.Max(0.05f, durationScale);

            // 채집이 활성화보다 먼저다. 위 edgeBase 주석 참고
            CaptureEdgeBase();

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            SetRgb(edgeFlash, tinted ? edgeTint : edgeBase);

            elapsed = 0f;
            playing = true;
        }

        /** 이번 재생의 지속 배율. Play가 매번 다시 쓴다 */
        private float durationScale = 1f;

        /** 배율이 얹힌 실제 지속. Update와 비네트가 이 값만 본다 */
        private float EdgeSeconds { get { return edgeSeconds * durationScale; } }
        private float VignetteSeconds { get { return vignetteSeconds * durationScale; } }

        private void Update()
        {
            if (!playing) return;

            // unscaled다. 위 주석 참고 - 정지 중에도 이 연출은 흘러야 한다
            elapsed += Time.unscaledDeltaTime;

            float longest = Mathf.Max(EdgeSeconds, VignetteSeconds);
            if (elapsed >= longest)
            {
                playing = false;
                SetAlpha(edgeFlash, 0f);
                SetAlpha(vignette, 0f);
                gameObject.SetActive(false);
                return;
            }

            // 엣지 번쩍: 곧바로 최대에서 시작해 사라진다. 올라가는 구간을 두면
            // 그 프레임들이 "밝아지는 중"으로 보이는데, 번쩍은 이미 일어난 일이라
            // 시작이 곧 최대여야 한다
            SetAlpha(edgeFlash, EdgeSeconds > 0f
                ? edgePeak * (1f - Mathf.Clamp01(elapsed / EdgeSeconds))
                : 0f);

            SetAlpha(vignette, VignetteAlphaAt(elapsed));
        }

        /** 빠르게 닫히고 천천히 열린다 */
        private float VignetteAlphaAt(float t)
        {
            float total = VignetteSeconds;
            if (total <= 0f) return 0f;

            // 닫힘도 함께 늘어난다. 닫힘만 고정하면 배율이 큰 연출에서
            // "번쩍 닫히고 한참 열린다"가 되어 리듬이 갈린다
            float close = Mathf.Clamp(vignetteCloseSeconds * durationScale, 0.0001f, total);
            if (t <= close) return vignettePeak * (t / close);

            float open = Mathf.Clamp01((t - close) / (total - close));
            // 처음이 느리고 끝에서 빠르게 사라진다. 여운이 끌리는 모양이다
            return vignettePeak * (1f - open * open);
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null) return;
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        /** 색상만 바꾼다. 알파는 연출이 매 프레임 다시 쓰므로 건드리지 않는다 */
        private static void SetRgb(Image image, Color rgb)
        {
            if (image == null) return;
            var color = image.color;
            image.color = new Color(rgb.r, rgb.g, rgb.b, color.a);
        }
    }
}
