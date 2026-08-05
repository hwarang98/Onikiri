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
            float offsetY = Mathf.Round(Random.Range(-amplitude, amplitude)) / DisplayConfig.PixelsPerUnit;

            transform.position = basePosition + new Vector3(offsetX, offsetY, 0f);
        }
    }
}
