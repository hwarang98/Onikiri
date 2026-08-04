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
