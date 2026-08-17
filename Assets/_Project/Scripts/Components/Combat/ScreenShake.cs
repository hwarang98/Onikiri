using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 타격 순간 카메라를 몇 픽셀 흔든다.
     *
     * unscaled 시간으로 돌기 때문에 히트스톱이 timeScale을 0으로 붙잡고 있는 동안에도
     * 계속 움직인다. 정지와 흔들림이 동시에 오면 각각보다 훨씬 무거운 타격으로 읽힌다.
     *
     * 오프셋은 원본 픽셀 단위로 반올림한다. 서브픽셀 오프셋은 Pixel Perfect Camera와
     * 충돌해서 화면이 흔들리는 대신 전체가 일렁이게 만든다.
     *
     * BattleStageLayout(실행 순서 1000) 뒤에 돈다. 레이아웃이 항상 흔들리지 않은
     * 카메라를 읽게 하기 위함이며, 자세한 것은 BasePosition 참고.
     */
    [DefaultExecutionOrder(2000)]
    public sealed class ScreenShake : MonoBehaviour
    {
        [Tooltip("기본 흔들림 세기 (원본 픽셀)")]
        [SerializeField] private float defaultPixels = 3f;

        [Tooltip("공격속도 보정 전 기본 흔들림 길이 (초)")]
        [SerializeField] private float defaultSeconds = 0.1f;

        private Vector3 basePosition;
        private bool shaking;
        private float remaining;
        private float duration;
        private float pixels;

        /**
         * @brief 흔들림을 제거한 카메라 위치.
         *
         * 카메라를 기준으로 월드 콘텐츠를 배치하는 쪽은 반드시 이 값을 써야 한다.
         * BattleStageLayout은 카메라에서 전투 밴드를 유도하는데, 흔들린 위치를 따라가면
         * 배경이 카메라와 함께 움직여 흔들림이 완전히 상쇄된다.
         */
        public Vector3 BasePosition { get { return shaking ? basePosition : transform.position; } }

        public bool IsShaking { get { return shaking; } }

        private void OnEnable()
        {
            basePosition = transform.position;
            shaking = false;
            remaining = 0f;
        }

        private void OnDisable()
        {
            if (shaking)
            {
                transform.position = basePosition;
                shaking = false;
            }
        }

        public void Shake()
        {
            Shake(defaultSeconds, defaultPixels);
        }

        /**
         * @brief 흔들림을 시작한다.
         *
         * 겹쳐 호출되면 누적하지 않고 더 강한 쪽을 취한다. 연타가 영구적인 진동으로
         * 번지는 것을 막는다.
         */
        public void Shake(float seconds, float strengthPixels)
        {
            if (seconds <= 0f || strengthPixels <= 0f) return;

            if (!shaking)
            {
                basePosition = transform.position;
                shaking = true;
            }

            duration = Mathf.Max(duration * (remaining > 0f ? 1f : 0f), seconds);
            remaining = Mathf.Max(remaining, seconds);
            pixels = Mathf.Max(pixels * (remaining > 0f ? 1f : 0f), strengthPixels);
        }

        public static void Request(ScreenShake instance, float seconds, float strengthPixels)
        {
            if (instance != null) instance.Shake(seconds, strengthPixels);
        }

        private void LateUpdate()
        {
            if (remaining <= 0f)
            {
                if (shaking)
                {
                    transform.position = basePosition;
                    shaking = false;
                    duration = 0f;
                    pixels = 0f;
                }
                else
                {
                    // 흔들리지 않는 동안의 의도적인 카메라 이동을 따라간다.
                    // 그래야 흔들림이 끝날 때 낡은 위치로 튕겨 돌아가지 않는다
                    basePosition = transform.position;
                }
                return;
            }

            remaining -= Time.unscaledDeltaTime;

            float falloff = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            float amplitude = pixels * falloff;

            float offsetX = Mathf.Round(Random.Range(-amplitude, amplitude)) / DisplayConfig.PixelsPerUnit;

            // **세로는 위로만 흔든다.** 아래로 내려가면 화면 밑에 하늘이 뜬다.
            //
            // 배경은 전투 밴드의 밑단에 **딱 붙어** 서 있다 - 레이어마다 자기
            // 스프라이트의 밑변이 밴드 바닥에 오도록 놓인다(BackgroundStage.
            // BuildScrollingLayer). 아래쪽 여유가 0이라는 뜻이고, 카메라가
            // 한 픽셀이라도 내려가면 그만큼 밴드 바닥에 **아무것도 안 그려진
            // 자리**가 생긴다. 거기서 비치는 것이 SkyFill이라, 성장 패널
            // 윗선과 지면 사이에 하늘색 띠가 번쩍인다(실기 제보 - 경험치 바
            // 위에 이상한 띠가 하나 더 생겼다는 것이 이것이다).
            //
            // 위로는 안전하다. 배경 꼭대기가 전투 창 위로 216px 남고(스카이
            // 레이어 216px 대 밴드), 최대 흔들림은 귀참의 3px x2.8 = 8.4px,
            // 화면으로 42px다. 아래로만 막으면 그 여유를 그대로 쓴다.
            //
            // 아트로 푸는 길도 있었다 - 지면 스트립을 아래로 더 굽는 것인데,
            // 그러면 SurfaceFromBottom이 함께 움직여 캐릭터가 서는 선이 바뀐다
            // (SpringForestBuilder.SurfaceFromBottom). 연출 버그를 고치자고
            // 레이아웃을 옮기는 것은 값이 맞지 않는다.
            float offsetY = Mathf.Abs(Mathf.Round(Random.Range(-amplitude, amplitude)))
                            / DisplayConfig.PixelsPerUnit;

            transform.position = basePosition + new Vector3(offsetX, offsetY, 0f);
        }
    }
}
