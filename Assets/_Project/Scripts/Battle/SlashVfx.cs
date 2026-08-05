using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 풀링된 참격 이펙트 하나.
     *
     * 타격 지점에서 프레임을 한 번 재생하고, 소유자가 회수할 수 있도록 끝났음을 알린다.
     */
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
            // 회전을 조금 흔들어야 반복되는 스윙이 도장 찍은 것처럼 보이지 않는다
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
