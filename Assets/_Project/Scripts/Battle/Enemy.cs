using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 요괴 한 마리. 오른쪽에서 걸어 들어와 스포너가 지정한 위치에 멈추고,
     *        맞고 죽는다.
     *
     * 풀링되므로 생애마다 초기화해야 할 상태는 Awake가 아니라 Spawn에서 전부 리셋한다.
     * 스스로를 파괴하지 않으며, 사망 시 Died를 통해 인스턴스를 돌려준다.
     */
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Enemy : MonoBehaviour
    {
        public enum State { Inactive, Approaching, Engaged, Dying }

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimator animator;

        /**
         * @brief 체력이 0이 되는 즉시, 사망 애니메이션 재생 전에 발생.
         *
         * 보상을 Died가 아니라 여기에 붙이는 이유는, 골드가 죽인 타격과 같은 순간에
         * 들어와야 하기 때문이다. Died를 쓰면 시체가 다 사라진 0.5초 뒤에 들어온다.
         */
        public event Action<Enemy> Killed;

        /** 사망 애니메이션이 끝나 인스턴스를 회수할 수 있을 때 발생 */
        public event Action<Enemy> Died;

        private EnemyDefinition definition;
        private BigDouble health;
        private BigDouble maxHealth;
        private BigDouble goldReward;
        private float groundY;
        private float targetX;
        private float hurtFlashRemaining;

        public State CurrentState { get; private set; }
        public bool IsAlive { get { return CurrentState == State.Approaching || CurrentState == State.Engaged; } }
        public bool IsTargetable { get { return IsAlive; } }
        public EnemyDefinition Definition { get { return definition; } }
        public float QueueSpacing { get { return definition != null ? definition.queueSpacing : 0.75f; } }

        /**
         * @brief 스폰 시점의 스테이지 배수가 적용된 값.
         *
         * 스폰할 때 확정하고 그 뒤로는 바꾸지 않는다. 스테이지가 오르는 순간 이미
         * 화면에 있던 요괴가 갑자기 단단해지거나 보상이 달라지면, 플레이어 입장에서는
         * 방금 때리던 대상이 이유 없이 변한 것으로 보인다.
         */
        public BigDouble MaxHealth { get { return maxHealth; } }
        public BigDouble GoldReward { get { return goldReward; } }

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
        }

        /**
         * @brief 풀에서 꺼낸 인스턴스를 spawnX 위치에서 살려낸다.
         *
         * 체력·골드 배수는 스테이지에서 온다. 정의 에셋의 값은 1스테이지 기준이다.
         */
        public void Spawn(EnemyDefinition def, float spawnX, float ground, int sortingOrder,
                          BigDouble healthMultiplier, BigDouble goldMultiplier)
        {
            definition = def;
            maxHealth = def.maxHealth * healthMultiplier;
            goldReward = def.goldReward * goldMultiplier;
            health = maxHealth;
            groundY = ground;
            targetX = spawnX;
            hurtFlashRemaining = 0f;
            CurrentState = State.Approaching;

            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = Color.white;
            // 적은 오른쪽에서 와서 플레이어를 바라본다. 원본 아트가 그려진 방향의 반대다
            spriteRenderer.flipX = true;

            transform.position = new Vector3(spawnX, RestingY(), 0f);
            PlayIdle();
        }

        /** 이 적이 걸어가야 할 목표 지점. 스포너가 매 프레임 지정한다 */
        public void SetTargetX(float x)
        {
            targetX = x;
        }

        public float CurrentX { get { return transform.position.x; } }

        /**
         * @brief 그려진 스프라이트의 중심. 타격이 떨어져야 할 지점이다.
         *
         * transform이 아니다. 피벗은 캔버스 하단이고 요괴마다 캔버스 안에서 그려지는
         * 높이가 달라서, transform을 겨냥하면 떠 있는 도깨비불이 아니라 그 아래 지면에
         * 이펙트가 떨어진다.
         */
        public Vector3 HitPoint
        {
            get { return spriteRenderer != null ? spriteRenderer.bounds.center : transform.position; }
        }

        public void TakeDamage(BigDouble amount)
        {
            if (!IsAlive) return;

            health -= amount;

            if (health <= BigDouble.Zero)
            {
                Die();
                return;
            }

            // 짧은 흰색 플래시. 이 스프라이트 크기에서는 별도 피격 애니메이션보다
            // 싸고 잘 읽히며, 다음 타격에 끊겨도 문제가 없다
            hurtFlashRemaining = 0.08f;
            spriteRenderer.color = Color.white;

            if (definition.hurtFrames != null && definition.hurtFrames.Length > 0)
                animator.Play(definition.hurtFrames, definition.frameRate, false, PlayIdle);
        }

        private void Die()
        {
            CurrentState = State.Dying;
            spriteRenderer.color = Color.white;

            var killed = Killed;
            if (killed != null) killed(this);

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
                // 피격 시 붉은 흰색으로 번쩍였다가 원래 색으로 돌아온다
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

            position.y = RestingY();
            transform.position = position;
        }

        /**
         * @brief 가장 아래 그려진 픽셀이 지면에서 정확히 hoverHeight 만큼 뜨는 월드 Y.
         *
         * 스프라이트 피벗은 아트가 아니라 캔버스 가장자리이고, 요괴마다 캔버스 안에서
         * 그려지는 높이가 다르다. 그래서 측정해 둔 오프셋을 여기서 다시 빼준다.
         */
        private float RestingY()
        {
            return groundY + definition.hoverHeight - definition.artBottomOffset;
        }

        /** 스포너가 필드를 비울 때 쓰는 강제 초기화 */
        public void Deactivate()
        {
            CurrentState = State.Inactive;
            animator.Stop();
            Died = null;
            Killed = null;
        }
    }
}
