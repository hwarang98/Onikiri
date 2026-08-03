using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// One yokai: walks in from the right, stops where the spawner tells it to, takes hits
    /// and dies.
    ///
    /// Pooled, so every piece of per-life state is reset in <see cref="Spawn"/> rather than
    /// in Awake. Nothing here destroys itself; death hands the instance back through
    /// <see cref="Died"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Enemy : MonoBehaviour
    {
        public enum State { Inactive, Approaching, Engaged, Dying }

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimator animator;

        /// <summary>Raised when the death animation finishes. Argument is this enemy.</summary>
        public event Action<Enemy> Died;

        private EnemyDefinition definition;
        private float health;
        private float groundY;
        private float targetX;
        private float hurtFlashRemaining;

        public State CurrentState { get; private set; }
        public bool IsAlive { get { return CurrentState == State.Approaching || CurrentState == State.Engaged; } }
        public bool IsTargetable { get { return IsAlive; } }
        public EnemyDefinition Definition { get { return definition; } }
        public float QueueSpacing { get { return definition != null ? definition.queueSpacing : 0.75f; } }

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
        }

        /// <summary>Brings a pooled instance to life at <paramref name="spawnX"/>.</summary>
        public void Spawn(EnemyDefinition def, float spawnX, float ground, int sortingOrder)
        {
            definition = def;
            health = def.maxHealth;
            groundY = ground;
            targetX = spawnX;
            hurtFlashRemaining = 0f;
            CurrentState = State.Approaching;

            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = Color.white;
            // Enemies come from the right and face the player, which is the mirror of how
            // the source art is drawn.
            spriteRenderer.flipX = true;

            transform.position = new Vector3(spawnX, ground + def.hoverHeight, 0f);
            PlayIdle();
        }

        /// <summary>Where this enemy should walk to. Assigned by the spawner each frame.</summary>
        public void SetTargetX(float x)
        {
            targetX = x;
        }

        public float CurrentX { get { return transform.position.x; } }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;

            health -= amount;

            if (health <= 0f)
            {
                Die();
                return;
            }

            // Brief white flash. Cheaper and more readable at this sprite size than a
            // separate hurt animation, and it survives being interrupted by the next hit.
            hurtFlashRemaining = 0.08f;
            spriteRenderer.color = Color.white;

            if (definition.hurtFrames != null && definition.hurtFrames.Length > 0)
                animator.Play(definition.hurtFrames, definition.frameRate, false, PlayIdle);
        }

        private void Die()
        {
            CurrentState = State.Dying;
            spriteRenderer.color = Color.white;

            if (definition.deathFrames != null && definition.deathFrames.Length > 0)
            {
                animator.Play(definition.deathFrames, definition.frameRate, false, FinishDeath);
            }
            else
            {
                FinishDeath();
            }
        }

        private void FinishDeath()
        {
            CurrentState = State.Inactive;
            var handler = Died;
            if (handler != null) handler(this);
        }

        private void PlayIdle()
        {
            if (definition == null) return;
            animator.Play(definition.idleFrames, definition.frameRate, true);
        }

        private void Update()
        {
            if (hurtFlashRemaining > 0f)
            {
                hurtFlashRemaining -= Time.deltaTime;
                // Flash red-white on hit, then settle back to normal.
                float t = Mathf.Clamp01(hurtFlashRemaining / 0.08f);
                spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.45f), t);
            }

            if (CurrentState != State.Approaching && CurrentState != State.Engaged) return;

            var position = transform.position;
            float step = definition.moveSpeed * Time.deltaTime;

            if (position.x > targetX + 0.001f)
            {
                position.x = Mathf.Max(targetX, position.x - step);
                CurrentState = State.Approaching;
            }
            else
            {
                CurrentState = State.Engaged;
            }

            position.y = groundY + definition.hoverHeight;
            transform.position = position;
        }

        /// <summary>Hard reset used when the spawner clears the field.</summary>
        public void Deactivate()
        {
            CurrentState = State.Inactive;
            animator.Stop();
            Died = null;
        }
    }
}
