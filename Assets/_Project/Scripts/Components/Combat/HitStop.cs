using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 타격 순간 게임 시간을 수십 밀리초 동안 정지시킨다.
     *
     * 타격이 '맞았다'고 느끼게 만드는 가장 싸고 효과적인 수단이다. 스윙과 적과
     * 참격 이펙트가 한순간 동시에 멈추면 그것이 무게로 읽힌다. 게임플레이 쪽은
     * 전부 스케일 타임으로 돌아가므로 timeScale 하나만 건드리면 전부 걸린다.
     *
     * 복구는 unscaled 시간으로 돈다. timeScale이 0인 상태에서 스케일 타임으로
     * 재면 타이머가 영원히 줄지 않아 게임이 멈춘 채로 남는다.
     */
    [DefaultExecutionOrder(-100)]
    public sealed class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        /**
         * @brief 정지가 끝난 뒤 복구할 타임스케일.
         *
         * 예전에는 이 값이 곧 복구값이었고 항상 1이었다. 그래서 정지가 끝날 때마다
         * 외부에서 걸어둔 배속이 1로 지워졌다 - 테스트 패널에서 4배속을 걸고 전투를
         * 보면 **첫 타격에 1배속으로 돌아갔다.** 배속이 안 걸리는 것처럼 보이는 것도
         * 아니고, 잠깐 걸렸다가 조용히 풀리므로 원인을 찾기 어려웠다.
         *
         * 이제 이것은 기본값일 뿐이고, 실제 복구값은 정지 직전의 타임스케일을 기억한다.
         */
        [Tooltip("복구할 타임스케일의 기본값. 실제로는 정지 직전 값을 기억해 되돌린다")]
        [SerializeField] private float normalTimeScale = 1f;

        private float remainingUnscaled;

        /**
         * @brief 정지가 끝나면 되돌아갈 타임스케일.
         *
         * 정지에 **들어가는 순간에만** 갱신한다. 정지 중에 다시 Freeze가 불리면
         * (연타 구간에서는 늘 그렇다) 그때의 타임스케일은 0이므로, 그것을 기억하면
         * 정지가 끝나도 0에 머물러 게임이 멈춘 채로 남는다.
         */
        private float restoreTimeScale = 1f;

        public bool IsFrozen { get { return remainingUnscaled > 0f; } }

        /** 정지가 끝난 뒤 돌아갈 배속. 테스트 패널이 표시에 쓴다 */
        public float BaseTimeScale { get { return remainingUnscaled > 0f ? restoreTimeScale : Time.timeScale; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            restoreTimeScale = normalTimeScale;

            // **플레이를 시작할 때 배속을 정상으로 되돌린다.**
            //
            // Time.timeScale은 에디터에서 플레이 모드를 나갔다 들어와도 리셋되지
            // 않는다. 테스트 패널로 0.25x를 걸어두고 잊으면 그 뒤의 모든 플레이가
            // 조용히 느려지고, 화면에는 "게임이 느리다"로만 보인다.
            //
            // 17단계에서 실제로 걸렸다. 배속이 0.3에 걸린 줄 모르고 전진 속도를
            // 3.2 -> 8 -> 16으로 세 번 올렸는데, 매번 0.3이 곱해져 체감이 거의
            // 안 바뀌었다. 다리 애니메이션도 32fps가 실효 9.6fps로 돌아 "다리가
            // 안 움직인다"가 됐다. 세 번의 수정이 전부 헛돌았고, 원인은 게임이
            // 아니라 에디터에 남은 값이었다.
            //
            // 빌드에서는 항상 1로 시작하므로 이것은 에디터 전용 함정이다.
            // 그래서 조용히 고치지 않고 경고를 남긴다 - 배속을 일부러 걸어둔
            // 사람에게는 "꺼졌다"가 보여야 하고, 잊은 사람에게는 원인이 보여야 한다
            if (!Mathf.Approximately(Time.timeScale, normalTimeScale))
            {
                Debug.LogWarning("[Onikiri] Time.timeScale was " + Time.timeScale
                                 + " at startup - reset to " + normalTimeScale
                                 + ". (Editor keeps timeScale across play sessions;"
                                 + " use the test panel to set it again if intended.)");
                Time.timeScale = normalTimeScale;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // 정지 도중 이 오브젝트가 사라져서 게임이 멈춘 채로 남는 일은 없어야 한다.
                // 기억해둔 값이 0이면 그것도 믿을 수 없으므로 기본값으로 되돌린다
                if (Time.timeScale == 0f)
                    Time.timeScale = restoreTimeScale > 0f ? restoreTimeScale : normalTimeScale;
            }
        }

        private void OnDisable()
        {
            if (remainingUnscaled > 0f)
            {
                remainingUnscaled = 0f;
                Time.timeScale = restoreTimeScale > 0f ? restoreTimeScale : normalTimeScale;
            }
        }

        /**
         * @brief 배속을 건다. 히트스톱이 끝나도 이 값이 유지된다.
         *
         * Time.timeScale을 직접 쓰지 말고 이쪽을 쓴다. 직접 쓰면 다음 타격의
         * 히트스톱이 끝날 때 그 값이 지워진다 - 정지 직전 값을 기억하는 구조라
         * 정지 중에 바꾼 값은 기억에 반영되지 않기 때문이다.
         */
        public void SetBaseTimeScale(float value)
        {
            restoreTimeScale = Mathf.Max(0f, value);

            // 정지 중이 아니면 즉시 반영한다. 정지 중이면 해제될 때 이 값으로 돌아간다
            if (remainingUnscaled <= 0f) Time.timeScale = restoreTimeScale;
        }

        public static void RequestBaseTimeScale(float value)
        {
            if (Instance != null) Instance.SetBaseTimeScale(value);
            else Time.timeScale = value;
        }

        /**
         * @brief 실제 시간 기준 seconds 만큼 정지시킨다.
         *
         * 겹쳐 호출되면 누적되지 않고 연장된다. 연타가 긴 정지로 불어나는 것을 막는다.
         */
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;

            // 정지에 처음 들어가는 순간에만 복구값을 갱신한다. 이미 정지 중이면
            // 지금의 타임스케일은 0이고, 그것을 기억하면 영원히 풀리지 않는다
            if (remainingUnscaled <= 0f && Time.timeScale > 0f) restoreTimeScale = Time.timeScale;

            remainingUnscaled = Mathf.Max(remainingUnscaled, seconds);
            Time.timeScale = 0f;
        }

        public static void Request(float seconds)
        {
            if (Instance != null) Instance.Freeze(seconds);
        }

        private void Update()
        {
            if (remainingUnscaled <= 0f) return;

            remainingUnscaled -= Time.unscaledDeltaTime;
            if (remainingUnscaled <= 0f)
            {
                remainingUnscaled = 0f;
                // 1이 아니라 정지 직전의 값으로 돌아간다. 이 한 줄이 배속을
                // 전투 중에도 유지되게 한다
                Time.timeScale = restoreTimeScale;
            }
        }
    }
}
