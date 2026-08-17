using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 경지 상승 전용 전면 백광 (39단계).
     *
     * ## 왜 ScreenFlash를 안 쓰는가
     *
     * 33단계의 경지 연출은 귀참의 ScreenFlash(엣지 번쩍 + 비네트)를 빌려 썼다.
     * 그 연출은 **가운데를 일부러 비워 둔다** - 귀참은 무엇이 죽었는지 보여야
     * 하기 때문이다. 그래서 경지가 올라도 화면 가장자리만 살짝 붉어졌고,
     * 캡처에서는 거의 보이지 않았다. 도약(로닌→데몬)의 무게가 연출에 없었다.
     *
     * 경지 상승은 반대다. **그 순간 화면에서 유일한 사건**이므로 가운데를
     * 덮어도 되고, 덮어야 한다 - 백광이 차오르는 동안 변신이 완성되고, 빛이
     * 걷히면 새 모습이 서 있다. 27단계에 귀참에서 걷어낸 흰 풀스크린이
     * 여기서는 맞는 도구다. 시전마다 나오는 연출이 아니라 게임 전체에 여섯 번
     * 나오는 축하라서, "화면을 가린다"는 비용을 낼 자리다.
     *
     * ## 시간 규칙
     *
     * unscaled로 돈다. 경지 상승은 짧은 정지(HitStop)와 함께 터지므로, 스케일
     * 타임이면 백광이 정지 동안 멈춰 있다가 뒤늦게 사라진다 - ScreenFlash가
     * 히트스톱에서 겪은 것과 같은 함정이다.
     */
    public sealed class EvolveFlash : MonoBehaviour
    {
        [Tooltip("화면 전체를 덮는 흰 판")]
        [SerializeField] private Image cover;

        [Range(0f, 1f)]
        [Tooltip("정점 알파. 1이면 완전한 백광 - 변신을 완전히 가린다")]
        [SerializeField] private float peak = 1f;

        [Tooltip("정점에 머무는 시간. 이 동안 화면이 온전히 희다")]
        [SerializeField] private float holdSeconds = 0.08f;

        [Tooltip("걷히는 시간. 빛이 걷히며 새 모습이 드러난다")]
        [SerializeField] private float fadeSeconds = 0.45f;

        private float elapsed;
        private bool playing;

        /** ScreenFlash와 같은 규칙 - Awake는 알파만 비우고 자신을 끄지 않는다 */
        private void Awake()
        {
            SetAlpha(0f);
        }

        /** 활성화가 먼저, 상태가 나중 (ScreenFlash.Play와 같은 순서 규칙) */
        public void Play()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            elapsed = 0f;
            playing = true;

            // 번쩍은 이미 일어난 일이다 - 시작이 곧 정점이어야 한다 (ScreenFlash 규칙)
            SetAlpha(peak);
        }

        private void Update()
        {
            if (!playing) return;

            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= holdSeconds + fadeSeconds)
            {
                playing = false;
                SetAlpha(0f);
                gameObject.SetActive(false);
                return;
            }

            if (elapsed <= holdSeconds)
            {
                SetAlpha(peak);
                return;
            }

            // 처음이 느리고 끝에서 빠르다. 여운이 끌리다 걷히는 모양 -
            // ScreenFlash의 비네트 열림과 같은 곡선이다
            float open = Mathf.Clamp01((elapsed - holdSeconds) / fadeSeconds);
            SetAlpha(peak * (1f - open * open));
        }

        private void SetAlpha(float alpha)
        {
            if (cover == null) return;
            var color = cover.color;
            color.a = alpha;
            cover.color = color;
        }
    }
}
