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

        [Tooltip("정지가 끝난 뒤 복구할 타임스케일")]
        [SerializeField] private float normalTimeScale = 1f;

        private float remainingUnscaled;

        public bool IsFrozen { get { return remainingUnscaled > 0f; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // 정지 도중 이 오브젝트가 사라져서 게임이 멈춘 채로 남는 일은 없어야 한다
                if (Time.timeScale == 0f) Time.timeScale = normalTimeScale;
            }
        }

        private void OnDisable()
        {
            if (remainingUnscaled > 0f)
            {
                remainingUnscaled = 0f;
                Time.timeScale = normalTimeScale;
            }
        }

        /**
         * @brief 실제 시간 기준 seconds 만큼 정지시킨다.
         *
         * 겹쳐 호출되면 누적되지 않고 연장된다. 연타가 긴 정지로 불어나는 것을 막는다.
         */
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;

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
                Time.timeScale = normalTimeScale;
            }
        }
    }
}
