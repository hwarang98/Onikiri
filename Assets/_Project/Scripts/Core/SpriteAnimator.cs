using System;
using UnityEngine;

namespace Onikiri.Core
{
    /**
     * @brief SpriteRenderer에 스프라이트 배열을 재생한다.
     *
     * Unity Animator 대신 직접 만든 이유는, 전투가 상태 머신으로는 다루기 번거로운
     * 프레임 단위 제어를 요구하기 때문이다. 공격은 임팩트 시점을 알려야 하고,
     * 사망 애니메이션은 마지막 프레임에 적을 풀로 반환해야 하며, 풀링된 오브젝트는
     * 재사용될 때 깨끗하게 처음부터 시작해야 한다.
     *
     * 의도적으로 스케일 타임으로 돌린다. 그래야 히트스톱이 스윙 도중의 애니메이션까지
     * 함께 얼린다. 타격이 '맞았다'고 느껴지게 하는 것의 대부분이 이 정지다.
     */
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;

        private Sprite[] frames;
        private float secondsPerFrame;
        private bool loop;
        private Action onComplete;

        private float timer;
        private int frameIndex;

        public bool IsPlaying { get; private set; }

        /**
         * @brief 지금 재생 중인 것이 **한 번 재생 클립**인가.
         *
         * "동작 도중이라 끊으면 안 되는 상태"의 정확한 정의다. 루프 클립(idle,
         * 달리기)은 언제 갈아끼워도 되지만 한 번 재생 클립(공격, 피격)은 끝까지
         * 가야 한다.
         *
         * `IsPlaying`만으로는 그 구분이 안 된다 - 루프 클립도 계속 true이기
         * 때문이다. 그것을 "동작 중"으로 오해한 자리가 실제로 있었다:
         * PlayerCombat이 `IsPlaying && swingStarted`로 스윙 여부를 판정했는데,
         * idle이 재생 중이고 swingStarted가 낡은 값으로 남아 있으면 둘 다 참이라
         * **영원히 달리기로 전환되지 않았다.**
         */
        public bool IsOneShot { get { return IsPlaying && !loop; } }

        /**
         * @brief 지금 돌고 있는 클립. 재생 중이 아니면 null.
         *
         * "무엇이 도는가"를 부르는 쪽이 물어볼 수 있게 한다. 예전에는 그것을
         * 각자 bool로 기억했는데, 그 값은 반드시 어딘가에서 낡는다 - 사무라이가
         * idle로 달려간 버그가 정확히 그것이었다. 배열 참조 비교라 재생을 시작한
         * 쪽이 자기 클립인지 확인하는 데 추가 상태가 필요 없다.
         */
        public Sprite[] CurrentClip { get { return IsPlaying ? frames : null; } }

        /** 현재 화면에 떠 있는 프레임의 0-기반 인덱스 */
        public int FrameIndex { get { return frameIndex; } }

        public int FrameCount { get { return frames != null ? frames.Length : 0; } }

        private void Reset()
        {
            target = GetComponent<SpriteRenderer>();
        }

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
        }

        /**
         * @brief 클립 재생을 시작한다.
         *
         * completed 콜백은 마지막 프레임이 끝날 때 한 번만 호출되며, 루프가 아닌
         * 클립에서만 발생한다.
         */
        public void Play(Sprite[] clip, float framesPerSecond, bool looping, Action completed = null)
        {
            if (clip == null || clip.Length == 0)
            {
                Stop();
                return;
            }

            frames = clip;
            secondsPerFrame = framesPerSecond > 0f ? 1f / framesPerSecond : 0.1f;
            loop = looping;
            onComplete = completed;

            timer = 0f;
            frameIndex = 0;
            IsPlaying = true;

            if (target != null) target.sprite = frames[0];
        }

        public void Stop()
        {
            IsPlaying = false;
            onComplete = null;
        }

        /**
         * @brief 재생 중인 클립의 속도만 바꾼다. 프레임 위치는 그대로 둔다.
         *
         * Play를 다시 부르면 frameIndex가 0으로 돌아가는데, 인스펙터에서 값을
         * 끌면 매 키 입력마다 동작이 첫 프레임으로 튄다 - 그 상태로는 빠른지
         * 느린지를 볼 수가 없다. 조율은 눈으로 하는 일이라 튀지 않는 것이 조건이다.
         */
        public void SetFrameRate(float framesPerSecond)
        {
            if (!IsPlaying) return;
            secondsPerFrame = framesPerSecond > 0f ? 1f / framesPerSecond : 0.1f;
        }

        private void Update()
        {
            if (!IsPlaying || frames == null) return;

            timer += Time.deltaTime;
            while (timer >= secondsPerFrame)
            {
                timer -= secondsPerFrame;
                frameIndex++;

                if (frameIndex >= frames.Length)
                {
                    if (loop)
                    {
                        frameIndex = 0;
                    }
                    else
                    {
                        frameIndex = frames.Length - 1;
                        IsPlaying = false;

                        // 호출 전에 비워둔다. 콜백이 다른 클립을 바로 시작해도
                        // 이 클립이 그것을 덮어쓰지 않게 하기 위함
                        var callback = onComplete;
                        onComplete = null;
                        if (callback != null) callback();
                        return;
                    }
                }

                if (target != null) target.sprite = frames[frameIndex];
            }
        }
    }
}
