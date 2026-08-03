using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// One pooled slash effect. Plays its frames once at the impact point and reports back
    /// so the owner can recycle it.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SlashVfx : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimator animator;

        private Action<SlashVfx> finished;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
            spriteRenderer.sortingOrder = SortingOrders.Vfx;
        }

        public void Play(Sprite[] frames, float framesPerSecond, Vector3 position, bool flip, Action<SlashVfx> onFinished)
        {
            finished = onFinished;

            transform.position = position;
            // A little rotation variety stops repeated swings looking like a stamp.
            transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-12f, 12f));

            spriteRenderer.flipX = flip;
            spriteRenderer.sortingOrder = SortingOrders.Vfx;
            spriteRenderer.enabled = true;

            animator.Play(frames, framesPerSecond, false, Complete);
        }

        private void Complete()
        {
            spriteRenderer.enabled = false;
            var callback = finished;
            finished = null;
            if (callback != null) callback(this);
        }
    }
}
