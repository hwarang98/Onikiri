using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 지역이 바뀌는 순간을 참격으로 가른다.
     *
     * ## 왜 커버가 필요한가
     *
     * 배경 스왑은 한 프레임에 일어난다 - 레이어를 전부 지우고 새로 만든다
     * (BackgroundStage). 그것을 그대로 보여주면 화면이 **툭 바뀐다.** 지역이
     * 넘어간 것이 아니라 렌더링이 고장난 것처럼 읽힌다.
     *
     * 커버는 그 한 프레임을 가리는 것이 전부다. 연출이 예뻐서가 아니라
     * 교체가 보이면 안 되기 때문에 있다.
     *
     * ## 왜 참격인가
     *
     * 페이드도 같은 일을 하지만, 이 게임은 발도가 주제다. 화면을 가르는 동작이
     * 이미 플레이어가 3분 동안 계속 보고 있던 것이라 새 문법을 배울 필요가 없다.
     *
     * ## 지역 경계에서만
     *
     * 스테이지는 10초마다 오른다. 거기에 매번 암전을 걸면 게임의 절반이 암전이
     * 된다. 지역은 10스테이지마다이므로 몇 분에 한 번이고, 그 정도면 "사건"으로
     * 읽힌다 - 17단계가 피날레에만 전체 연출을 준 것과 같은 판단이다.
     */
    public sealed class RegionTransition : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        [Tooltip("화면을 덮는 판. 이것이 배경 교체를 가린다")]
        [SerializeField] private Image cover;

        [Tooltip("화면을 가로지르는 참격 띠")]
        [SerializeField] private RectTransform slash;

        [SerializeField] private Image slashImage;

        [Tooltip("참격이 화면을 가로지르는 시간")]
        [SerializeField] private float slashSeconds = 0.16f;

        [Tooltip("암전이 걷히는 시간. 새 지역이 드러나는 속도다")]
        [SerializeField] private float revealSeconds = 0.3f;

        /**
         * @brief 참격이 지나간 자리가 어두워지는 시간.
         *
         * 짧아야 한다. 이 구간은 아무것도 알려주지 않는 대기 시간이고, 길면
         * "멈췄나?"가 된다. 가리는 데 필요한 최소치만 쓴다.
         */
        [SerializeField] private float coverSeconds = 0.08f;

        private Coroutine running;

        /** 지금 전환 중인가. 겹쳐 도는 것을 막는다 */
        public bool IsPlaying { get { return running != null; } }

        /**
         * @brief 꺼두지 않고 **투명하게** 둔다.
         *
         * 처음에 `root.SetActive(false)`로 숨겼는데, root가 이 컴포넌트가 붙은
         * 오브젝트라 **비활성 상태에서 StartCoroutine이 아예 시작되지 않았다.**
         * 전환을 요청해도 아무 일도 일어나지 않고, 배경만 바뀌지 않은 채 남았다.
         *
         * 알파 0으로 두면 코루틴이 살아 있고 그리는 비용은 Image 두 장뿐이다.
         * 대신 알파가 0이어도 레이캐스트는 받으므로, 입력을 막는 것은 그쪽을
         * 따로 끈다 - 안 그러면 투명한 판이 화면 전체의 터치를 삼킨다.
         */
        private void Awake()
        {
            if (root == null) root = gameObject;

            SetCoverAlpha(0f);
            SetSlashAlpha(0f);
            SetBlocking(false);
        }

        /**
         * @brief 전환을 재생하고, 가장 어두운 순간에 swap을 부른다.
         *
         * swap을 콜백으로 받는 이유는 **타이밍이 이 컴포넌트의 책임**이기
         * 때문이다. 부르는 쪽이 "언제 바꿀지"까지 정하면 연출을 손볼 때마다
         * 두 곳을 맞춰야 한다.
         *
         * 이미 돌고 있으면 새 요청은 **무시하지 않고 즉시 처리한다** - 전환이
         * 겹칠 상황(연속 지역 돌파)에서 swap을 버리면 배경이 한 지역 뒤에
         * 남는다. 화면보다 상태가 맞는 것이 중요하다.
         */
        public void Play(Action swap)
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
                if (swap != null) swap();
                Finish();
                return;
            }

            running = StartCoroutine(Run(swap));
        }

        private IEnumerator Run(Action swap)
        {
            SetBlocking(true);
            SetCoverAlpha(0f);
            SetSlashAlpha(1f);

            // 1. 참격이 화면을 가로지른다
            float width = ScreenWidth();
            yield return Sweep(-width, width, slashSeconds);

            SetSlashAlpha(0f);

            // 2. 벤 자리가 어두워진다
            yield return Fade(0f, 1f, coverSeconds);

            // 3. 가려진 동안 배경을 갈아끼운다. 이 한 프레임이 이 연출의 존재 이유다
            if (swap != null) swap();

            // 스왑 직후 한 프레임 쉰다. 레이어 생성이 같은 프레임에 끝나지 않으면
            // 걷히는 첫 프레임에 옛 배경이 비친다
            yield return null;

            // 4. 새 지역이 드러난다
            yield return Fade(1f, 0f, revealSeconds);

            Finish();
        }

        private IEnumerator Sweep(float fromX, float toX, float seconds)
        {
            if (slash == null || seconds <= 0f) yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                // 스케일 타임을 쓰지 않는다. 히트스톱이 걸린 채로 지역을 넘으면
                // 전환이 그만큼 늘어지고, 그 정지는 타격이 아니라 고장으로 읽힌다
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / seconds);
                slash.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), 0f);
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            if (cover == null) yield break;

            if (seconds <= 0f) { SetCoverAlpha(to); yield break; }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetCoverAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }
            SetCoverAlpha(to);
        }

        private void Finish()
        {
            SetCoverAlpha(0f);
            SetSlashAlpha(0f);
            SetBlocking(false);
            running = null;
        }

        /** 전환 중에만 입력을 막는다. 가려진 화면을 누르면 안 보이는 것이 눌린다 */
        private void SetBlocking(bool blocking)
        {
            if (cover != null) cover.raycastTarget = blocking;
        }

        private float ScreenWidth()
        {
            var rect = root.transform as RectTransform;
            if (rect != null && rect.rect.width > 1f) return rect.rect.width;
            return Onikiri.Core.DisplayConfig.DesignWidth;
        }

        private void SetCoverAlpha(float alpha)
        {
            if (cover == null) return;
            var c = cover.color;
            c.a = alpha;
            cover.color = c;
        }

        private void SetSlashAlpha(float alpha)
        {
            if (slashImage == null) return;
            var c = slashImage.color;
            c.a = alpha;
            slashImage.color = c;
        }
    }
}
