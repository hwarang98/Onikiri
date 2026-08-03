using System;
using UnityEngine;

namespace Onikiri.Core
{
    /// <summary>
    /// Plays an array of sprites on a SpriteRenderer.
    ///
    /// Used instead of Unity's Animator because combat here needs frame-exact control that
    /// state machines make awkward: the attack has to report the moment of impact, the
    /// death animation has to hand the enemy back to its pool on the last frame, and pooled
    /// objects have to restart cleanly on reuse.
    ///
    /// Runs on scaled time on purpose, so a hitstop freezes the animation mid-swing along
    /// with everything else. That freeze is most of what makes a hit feel like it landed.
    /// </summary>
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

        /// <summary>0-based index of the frame currently on screen.</summary>
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

        /// <summary>
        /// Starts a clip. <paramref name="onComplete"/> fires once, at the end of the last
        /// frame, and only for non-looping clips.
        /// </summary>
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

                        // Cleared before invoking so the callback is free to start another
                        // clip without this one stomping it.
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
