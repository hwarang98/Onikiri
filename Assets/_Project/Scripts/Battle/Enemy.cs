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

        /** 확대 배율과 틴트. 보스 유형에 따라 스폰 시점에 정해진다 */
        private float bodyScale = 1f;
        private Color baseTint = Color.white;

        /** 공격 주기. 0이면 공격하지 않는다 (잡몹) */
        private float attackTimer;
        private bool swingStarted;

        /**
         * @brief 한 번 때릴 때의 피해량. 0이면 공격하지 않는다.
         *
         * 스폰 시점에 확정한다. 스테이지 배율이 이미 곱해진 값이다.
         */
        public double AttackDamage { get; private set; }

        /** 이 요괴가 플레이어를 때렸다. 사거리 안이고 주기가 찼을 때 발생 */
        public event Action<Enemy> Attacked;

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

        /**
         * @brief 보스인가.
         *
         * 스포너가 이것으로 두 가지를 가른다. 처치가 스테이지 할당량에 들어가는지,
         * 그리고 죽었을 때 보스전을 끝내야 하는지다. 별도 클래스를 만들지 않은 이유는
         * 보스와 잡몹이 하는 일이 완전히 같기 때문이다 - 걸어와서 멈추고 맞고 죽는다.
         * 다른 것은 체력·보상·연출뿐이고 그것은 전부 스폰 시점의 값이다.
         */
        public bool IsBoss { get; private set; }

        /** 체력 비율 0~1. 보스 체력 바가 쓴다 */
        public float HealthFraction
        {
            get
            {
                if (maxHealth <= BigDouble.Zero) return 0f;
                if (health <= BigDouble.Zero) return 0f;
                return Mathf.Clamp01((float)(health / maxHealth).ToDouble());
            }
        }

        /**
         * @brief 이 개체가 지금까지 받은 총 피해.
         *
         * 실패 화면이 "몇 배 모자랐는가"를 계산할 때 쓴다. 남은 체력이 아니라 받은
         * 피해라야 한다 - 실패 시점에 이미 죽어 사라진 개체를 붙들고 있을 필요가 없고,
         * 제한 시간 동안의 실제 DPS가 그대로 드러난다.
         */
        public BigDouble DamageTaken
        {
            get
            {
                var dealt = maxHealth - health;
                return dealt > BigDouble.Zero ? dealt : BigDouble.Zero;
            }
        }

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
        }

        /**
         * @brief 풀에서 꺼낸 인스턴스를 spawnX 위치에서 살려낸다.
         *
         * 체력과 골드는 배수가 아니라 **확정된 절대값**으로 받는다. 예전에는 배수를
         * 넘겨 여기서 곱했는데, 보스는 스테이지 배수 위에 다시 보스 배수가 얹히고
         * 기준이 되는 잡몹도 정의 에셋 하나가 아니라 가중 평균이라, 곱셈이 이 안에
         * 남아 있으면 스포너와 여기 양쪽에 밸런스 계산이 흩어진다.
         */
        public void Spawn(EnemyDefinition def, float spawnX, float ground, int sortingOrder,
                          BigDouble totalHealth, BigDouble goldOnKill, bool isBoss = false,
                          float scale = 1f, Color tint = default(Color), double attackDamage = 0d)
        {
            definition = def;
            maxHealth = totalHealth;
            goldReward = goldOnKill;
            IsBoss = isBoss;
            health = maxHealth;

            AttackDamage = attackDamage;
            attackTimer = 0f;
            swingStarted = false;

            // 잡몹 확대판 보스용. 정수 배율만 쓴다 - 픽셀 격자가 어긋나는 이유는
            // BossContentBuilder에 적어뒀다
            transform.localScale = new Vector3(scale, scale, 1f);
            bodyScale = scale;
            groundY = ground;
            targetX = spawnX;
            hurtFlashRemaining = 0f;
            CurrentState = State.Approaching;

            spriteRenderer.sortingOrder = sortingOrder;
            // 틴트는 "같은 놈이 커진 것"이 아니라 "우두머리"로 읽히게 하는 장치다.
            // 기본값(투명 검정)이면 손대지 않는다
            baseTint = tint.a > 0f ? tint : Color.white;
            spriteRenderer.color = baseTint;
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
                // 피격 시 붉은 흰색으로 번쩍였다가 원래 색으로 돌아온다.
                // 돌아갈 색은 흰색이 아니라 이 개체의 틴트다 - 확대판 보스는
                // 물들여 두었으므로 흰색으로 돌리면 맞을 때마다 색이 벗겨진다
                float t = Mathf.Clamp01(hurtFlashRemaining / 0.08f);
                spriteRenderer.color = Color.Lerp(baseTint, new Color(1f, 0.45f, 0.45f), t);
            }

            if (CurrentState != State.Approaching && CurrentState != State.Engaged) return;

            UpdateAttack();

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
         * @brief 보스의 공격 주기.
         *
         * 큐 앞줄에 도착한 뒤에만 돈다(Engaged). 걸어 들어오는 동안 때리면
         * 화면 밖에서 피해가 들어오고, 플레이어는 무엇에 맞았는지 알 수 없다.
         *
         * 잡몹은 attackInterval이 0이라 이 함수가 즉시 빠져나간다. 잡몹이
         * 공격하지 않는 것은 성능 최적화가 아니라 설계다 - PlayerHealth 참고.
         */
        private void UpdateAttack()
        {
            if (AttackDamage <= 0d || definition == null || definition.attackInterval <= 0f) return;
            if (CurrentState != State.Engaged) return;

            float interval = definition.attackInterval;
            attackTimer += Time.deltaTime;

            // 애니메이션은 타격보다 lead 시간만큼 먼저 시작한다. 플레이어의
            // PlayerCombat과 같은 규칙이다 - 칼이 닿는 프레임과 피해가 들어가는
            // 순간이 겹쳐야 한 사건으로 읽힌다
            float swingDuration = definition.attackFrames != null && definition.attackFrames.Length > 0
                ? Mathf.Min(definition.attackFrames.Length / definition.frameRate, interval)
                : 0f;
            float lead = swingDuration * definition.attackImpactPoint;

            if (!swingStarted && attackTimer >= interval - lead)
            {
                if (swingDuration > 0f)
                {
                    float rate = definition.attackFrames.Length / Mathf.Max(0.0001f, swingDuration);
                    animator.Play(definition.attackFrames, rate, false, PlayIdle);
                }
                swingStarted = true;
            }

            if (attackTimer < interval) return;

            attackTimer -= interval;
            swingStarted = false;

            var handler = Attacked;
            if (handler != null) handler(this);
        }

        /**
         * @brief 가장 아래 그려진 픽셀이 지면에서 정확히 hoverHeight 만큼 뜨는 월드 Y.
         *
         * 스프라이트 피벗은 아트가 아니라 캔버스 가장자리이고, 요괴마다 캔버스 안에서
         * 그려지는 높이가 다르다. 그래서 측정해 둔 오프셋을 여기서 다시 빼준다.
         */
        private float RestingY()
        {
            // 보정값에 배율을 곱한다. artBottomOffset은 배율 1의 스프라이트에서
            // 측정한 값이라, 확대판 보스에 그대로 쓰면 발이 지면에 파묻힌다
            return groundY + definition.hoverHeight - definition.artBottomOffset * bodyScale;
        }

        /** 스포너가 필드를 비울 때 쓰는 강제 초기화 */
        public void Deactivate()
        {
            CurrentState = State.Inactive;
            animator.Stop();
            Died = null;
            Killed = null;
            Attacked = null;

            // 풀로 돌아가는 인스턴스는 확대판 보스였을 수 있다. 배율과 틴트를
            // 되돌리지 않으면 다음에 잡몹으로 재사용될 때 두 배 크기의 물든
            // 요괴가 나온다
            bodyScale = 1f;
            baseTint = Color.white;
            AttackDamage = 0d;
            transform.localScale = Vector3.one;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }
    }
}
