using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 타격음과 처치음.
     *
     * 어려운 쪽은 소리를 재생하는 것이 아니라, 수천 번을 재생하면서도 소음이 되지
     * 않게 하는 것이다. 방치형은 결국 초당 10회 이상 공격에 도달하고, 순진한 구현
     * (AudioSource 하나, 클립 하나, 매 타격마다 재생)은 1분 안에 웅웅거리는 잡음이
     * 된다. 네 가지 가드가 전부 필요하다:
     *
     *   클립 변형    여러 테이크 중 무작위 선택
     *   피치 변형    재생마다 몇 퍼센트씩 흔들어 반복이 위상 고정되지 않게 함
     *   보이스 상한  고정 크기 링. 다섯 번째 겹치는 타격은 소리 벽을 쌓는 대신
     *                가장 오래된 보이스를 뺏는다
     *   최소 간격    이보다 촘촘한 타격은 버린다. 초당 25회를 넘어가면 어차피
     *                개별적으로 들리지 않는다
     *
     * 처치음은 전용 뱅크를 쓰고 더 낮고 크게 재생한다. 그래야 죽음이 평범한 타격의
     * 흐름과 항상 다르게 읽힌다.
     *
     * AudioSource 재생은 Time.timeScale을 무시하므로 히트스톱 중에도 소리가 이어진다.
     * 정지가 프레임 드랍이 아니라 타격으로 읽히는 이유가 이것이다.
     *
     * 클립이 하나도 없으면 모든 메서드가 조용한 no-op이 된다. 사운드 파일을 구하는
     * 동안에도 전투 코드가 조건 없이 호출할 수 있다.
     */
    public sealed class HitAudio : MonoBehaviour
    {
        [Header("클립")]
        [Tooltip("타격음. 매 타격마다 하나를 무작위로 고른다")]
        [SerializeField] private AudioClip[] hitClips;

        [Tooltip("처치음. 타격음보다 낮고 무겁게")]
        [SerializeField] private AudioClip[] killClips;

        [Header("보이스")]
        [Tooltip("동시 재생 보이스 수. 이를 넘으면 가장 오래된 것을 재사용한다")]
        [SerializeField] private int voices = 4;

        [Header("피로 대책")]
        [Tooltip("타격음 사이 최소 간격. 이보다 촘촘한 타격은 버린다")]
        [SerializeField] private float minHitInterval = 0.04f;

        [Tooltip("처치음 사이 최소 간격")]
        [SerializeField] private float minKillInterval = 0.02f;

        [Tooltip("타격음 피치 랜덤 범위")]
        [SerializeField] private Vector2 hitPitchRange = new Vector2(0.94f, 1.06f);

        [Tooltip("처치 피치는 1보다 낮게. 죽음이 더 무겁게 떨어지도록")]
        [SerializeField] private Vector2 killPitchRange = new Vector2(0.78f, 0.88f);

        [Range(0f, 1f)] [SerializeField] private float hitVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float killVolume = 0.85f;

        private AudioSource[] sources;
        private int nextVoice;
        private float lastHitTime;
        private float lastKillTime;

        public bool HasHitClips { get { return hitClips != null && hitClips.Length > 0; } }
        public bool HasKillClips { get { return killClips != null && killClips.Length > 0; } }
        public int HitClipCount { get { return hitClips != null ? hitClips.Length : 0; } }
        public int KillClipCount { get { return killClips != null ? killClips.Length : 0; } }

        /** 간격 가드에 걸려 버려진 재생 횟수. 스로틀을 조율할 때 쓴다 */
        public int ThrottledCount { get; private set; }

        private void Awake()
        {
            voices = Mathf.Max(1, voices);
            sources = new AudioSource[voices];

            // 한 번만 만든다. 타격마다 AddComponent를 하면 게임이 가장 바쁜 지점에서
            // 정확히 할당이 일어난다
            for (int i = 0; i < voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;   // 2D. 사이드뷰 방치형에는 리스너 거리 개념이 없다
                sources[i] = source;
            }

            // 음수로 둬서 세션 첫 타격이 스로틀에 걸리지 않게 한다
            lastHitTime = -999f;
            lastKillTime = -999f;
        }

        public void PlayHit()
        {
            if (!HasHitClips) return;

            // unscaled 시간을 쓴다. 히트스톱이 timeScale을 0으로 붙잡는 동안에도
            // 스로틀이 동작해야 한다. 아니면 정지 중 들어온 타격이 한꺼번에 통과한다
            if (Time.unscaledTime - lastHitTime < minHitInterval)
            {
                ThrottledCount++;
                return;
            }
            lastHitTime = Time.unscaledTime;

            Play(hitClips, hitPitchRange, hitVolume);
        }

        public void PlayKill()
        {
            if (Time.unscaledTime - lastKillTime < minKillInterval)
            {
                ThrottledCount++;
                return;
            }

            // 전용 처치 뱅크가 생기기 전까지는 타격음을 크게 낮춰 재사용한다.
            // 그래야 죽음이 평범한 일격보다 무겁게 읽힌다
            var bank = HasKillClips ? killClips : hitClips;
            if (bank == null || bank.Length == 0) return;

            lastKillTime = Time.unscaledTime;
            Play(bank, killPitchRange, killVolume);
        }

        private void Play(AudioClip[] bank, Vector2 pitchRange, float volume)
        {
            if (sources == null) return;

            var source = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Length;

            source.clip = bank[Random.Range(0, bank.Length)];
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.volume = volume;
            source.Play();
        }
    }
}
