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
        private BigDouble expReward;
        private float groundY;
        private float targetX;
        private float hurtFlashRemaining;

        /** 확대 배율과 틴트. 보스 유형에 따라 스폰 시점에 정해진다 */
        private float bodyScale = 1f;
        private Color baseTint = Color.white;

        /** 공격 주기. 0이면 공격하지 않는다 (잡몹) */
        private float attackTimer;
        private bool swingStarted;

        /** 이번 주기에 고른 공격 그림. 주기가 끝나면 비워 다시 고른다 */
        private Sprite[] currentAttack;

        /** 직전 주기에 쓴 것. 연속으로 같은 것이 나오지 않게 하는 데만 쓴다 */
        private Sprite[] lastAttack;

        // `IsSwinging`("지금 공격 클립이 도는가")이 여기 있었다. 피격이 공격
        // 동작을 덮지 않게 막는 데만 쓰였는데, 그 자리가 보스 전체를 덮는
        // 슈퍼아머로 넓어지면서(TakeDamage) 물어볼 일이 없어졌다.

        /**
         * @brief 한 번 때릴 때의 피해량. 0이면 공격하지 않는다.
         *
         * 스폰 시점에 확정한다. 스테이지 배율이 이미 곱해진 값이다.
         */
        public double AttackDamage { get; private set; }

        /** 이 요괴가 플레이어를 때렸다. 사거리 안이고 주기가 찼을 때 발생 */
        public event Action<Enemy> Attacked;

        /**
         * @brief 공격 **동작이 시작되는** 순간. 타격보다 예비 동작만큼 이르다.
         *
         * `Attacked`와 갈라놓는 이유는 둘이 다른 사건이기 때문이다. `Attacked`는
         * 피해가 들어가는 순간이고 밸런스가 그 주기 위에 서 있다. 이쪽은 화면에
         * 그림이 뜨기 시작하는 순간이고, 앞당기든 미루든 피해량과 주기는 한 치도
         * 안 움직인다 - 연출을 얹는 자리가 밸런스를 건드리지 않게 갈라둔 것이다.
         */
        public event Action<Enemy> SwingStarted;

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
         * @brief 처치 시 주는 경험치.
         *
         * 골드와 나란히 스폰 시점에 확정한다. 값 자체는 스테이지만의 함수라
         * (ExpCurve.MobExp) 죽는 순간 계산해도 대개 같은 답이 나오지만, 보스를
         * 잡아 스테이지가 오르는 그 프레임에 아직 걸어오던 잡몹이 죽으면 답이
         * 갈린다. 골드가 절대값인 이유와 같은 이유로 여기서도 절대값이다.
         */
        public BigDouble ExpReward { get { return expReward; } }

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
                          BigDouble totalHealth, BigDouble goldOnKill, BigDouble expOnKill,
                          bool isBoss = false,
                          float scale = 1f, Color tint = default(Color), double attackDamage = 0d)
        {
            definition = def;
            maxHealth = totalHealth;
            goldReward = goldOnKill;
            expReward = expOnKill;
            IsBoss = isBoss;
            health = maxHealth;

            AttackDamage = attackDamage;
            attackTimer = 0f;
            swingStarted = false;

            // 고른 공격 그림은 정의에 딸린 것이라 반드시 함께 비운다. 풀에서
            // 돌아온 인스턴스가 옛 정의의 배열을 들고 있으면 다른 요괴의
            // 공격이 재생된다
            currentAttack = null;
            lastAttack = null;

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
            // 적은 오른쪽에서 와서 **왼쪽의 플레이어를 바라본다.** 대부분의 팩이
            // 오른쪽을 보고 그려져 있어 뒤집어야 하지만, 그것은 팩의 성질이지
            // 규칙이 아니다 - 처형인 팩은 왼쪽을 보고 그려져 있어서 뒤집으면
            // 플레이어에게 등을 돌린다.
            //
            // 규칙은 Facing이 들고 있다. 아군 쪽 식과 느낌표 하나 차이라
            // 값으로 적으면 반드시 한 번은 틀린다(Facing 머리 주석)
            spriteRenderer.flipX = def == null || Facing.Enemy(def.artFacesLeft);

            transform.position = new Vector3(spawnX, RestingY(), 0f);
            PlayResting();
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

        /**
         * @brief 이 요괴가 바라보는 방향. -1이면 왼쪽, +1이면 오른쪽.
         *
         * 그려진 방향(`artFacesLeft`)과 Spawn이 건 뒤집기를 함께 읽는다. 둘 중 하나만
         * 보면 틀린다 - 팩마다 그려진 방향이 다르고(처형인은 왼쪽, 나머지는 오른쪽),
         * 그 차이를 없애려고 뒤집는 것이 바로 `artFacesLeft`의 일이기 때문이다.
         *
         * 타격 불꽃이 이 값으로 선다. 요괴가 바라보는 쪽이 칼이 들어오는 앞면이므로,
         * 불꽃은 그쪽에 찍혀야 하고 가시도 그쪽으로 길어야 한다.
         *
         * 지금은 모든 요괴가 오른쪽에서 와서 왼쪽의 플레이어를 보므로 결과가 늘 -1이다.
         * 그래도 상수 -1을 쓰지 않는 이유는, 그 -1이 **여기서 한 번 계산되는 결론**이지
         * 부르는 쪽이 알아야 할 사실이 아니기 때문이다. 왼쪽에서 오는 적이 생기면
         * Spawn의 뒤집기 규칙만 바뀌고 불꽃은 따라온다.
         */
        public int FacingDirection
        {
            get
            {
                bool drawnFacingLeft = definition != null && definition.artFacesLeft;
                bool flipped = spriteRenderer != null && spriteRenderer.flipX;
                return drawnFacingLeft != flipped ? -1 : 1;
            }
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

            /**
             * @brief 보스는 **슈퍼아머**다 - 피격 애니를 아예 타지 않는다.
             *
             * ## 왜 스윙 보호만으로는 모자랐는가
             *
             * 오래 전부터 "스윙 중에는 hurt로 덮지 않는다"는 보호가 있었다.
             * 0.92초짜리 공격 클립이 초당 네 번의 타격에 매번 지워져서 처형인이
             * 도끼를 끝까지 들지 못했던 것이 그 보호가 생긴 이유다.
             *
             * 그런데 그 보호는 **공격 클립이 이미 돌고 있을 때만** 걸린다.
             * 대기와 예비 동작은 무방비라 0.26초마다 hurt가 덮고, 그동안 보스는
             * 계속 움찔하기만 한다. 공격 그림이 아예 없는 확대판 보스
             * (외눈 등롱·정예)는 걸릴 스윙조차 없어서 **평생 hurt만 탄다.**
             *
             * ## 왜 이제 와서 드러났는가
             *
             * 48단계 전까지 잡몹 정의에는 hurt 프레임이 없었다(빈 배열). 확대판
             * 보스는 잡몹 정의를 그대로 쓰므로 덮을 것이 없었고, 피격 신호는
             * 흰 플래시뿐이었다. 그 단계에서 팩에 남아 있던 hurt 태그를 채우면서
             * 이 경로가 처음으로 열렸다 - 없던 그림이 생긴 것이 아니라, 없어서
             * 안 보이던 결함이 보이게 된 것이다.
             *
             * ## 무엇을 끄는가
             *
             * **애니메이션만 끈다.** 위에서 흰 플래시는 이미 켰고 피해는 이미
             * 깎였다. 보스는 같은 피해를 받고 같은 시각에 죽는다 - 슈퍼아머라는
             * 이름이 흔히 뜻하는 피해 감소나 경직 면역이 아니라, 말 그대로
             * 피격 클립을 안 거는 것뿐이다.
             *
             * 잡몹은 그대로 hurt를 탄다. 두세 대에 죽어서 움찔이 반복될 일이
             * 없고, 그 짧은 반응이 타격감의 일부다.
             */
            if (IsBoss) return;

            if (definition.hurtFrames != null && definition.hurtFrames.Length > 0)
                animator.Play(definition.hurtFrames, definition.frameRate, false, PlayResting);
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

        /**
         * @brief 지금 상태에 맞는 **반복 클립**을 건다.
         *
         * 걸어오는 중이면 걷기, 서 있으면 idle이다. 걷기 프레임이 없는 요괴는
         * 지금까지와 똑같이 idle로 걷는다 - 도깨비불처럼 떠다니는 팩에는 걷기가
         * 아예 없고, 있어야 할 이유도 없다.
         *
         * 한 번 재생 클립(공격·피격·사망)이 도는 중이면 손대지 않는다. 그쪽은
         * 끝날 때 이 함수를 콜백으로 부르므로, 끝나는 시점의 상태에 맞는 클립이
         * 자동으로 걸린다.
         */
        private void PlayResting()
        {
            if (definition == null) return;
            if (animator.IsOneShot) return;

            var clip = CurrentState == State.Approaching
                       && definition.walkFrames != null && definition.walkFrames.Length > 0
                ? definition.walkFrames
                : definition.idleFrames;

            animator.Play(clip, definition.frameRate, true);
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

            /*
             * 자기 걸음 + **세계가 실어 나르는 몫**.
             *
             * 17단계에서 배경이 흐르기 시작하면서 필요해졌다. 플레이어가 앞으로
             * 달리면 지면이 왼쪽으로 흐르는데, 요괴가 자기 걸음(1.0)으로만
             * 움직이면 지면(3.2)에 대해 오른쪽으로 2.2씩 미끄러진다 - 발이 땅에
             * 안 붙고 뒤로 밀리는 것처럼 보인다.
             *
             * 요괴도 세계의 일부다. 지면과 같은 속도로 실려 오고, 그 위에서
             * 자기 걸음을 더 걷는다. 그래야 발과 땅이 맞물린다.
             *
             * 접근이 빨라지는 것은 부작용이 아니라 의도다 - 플레이어가 달려가
             * 만나는 것이므로 거리가 빨리 좁혀지는 것이 맞다. 공급 속도는
             * 스폰 간격(SpawnPacing)이 정하지 이동 시간이 정하지 않으므로
             * 밸런스 모델은 그대로다.
             */
            float step = (definition.moveSpeed + ParallaxScroller.BaseSpeed) * Time.deltaTime;

            var wasState = CurrentState;

            if (position.x > targetX + 0.001f)
            {
                position.x = Mathf.Max(targetX, position.x - step);
                CurrentState = State.Approaching;
            }
            else
            {
                CurrentState = State.Engaged;
            }

            // 걷기와 서기의 전환은 **여기 한 곳**에서만 일어난다. 이동 판정이
            // 곧 걷는지 여부이므로, 그 판정을 내린 자리에서 클립을 바꾸는 것이
            // 두 값이 어긋날 수 없는 유일한 배치다
            if (CurrentState != wasState) PlayResting();

            position.y = RestingY();
            transform.position = position;
        }

        /**
         * @brief 이번 주기에 쓸 공격 그림을 고른다.
         *
         * 후보는 `attackFrames` 하나와 `attackVariants`의 나머지다. 변형이
         * 없으면 후보가 하나뿐이라 지금까지와 똑같이 같은 그림이 돈다 -
         * 잡몹과 대부분의 보스가 그 경우다.
         *
         * **직전 것은 피한다.** 균등 추첨만 하면 넷 중에서도 같은 것이 두 번
         * 세 번 이어지는 구간이 반드시 생기고, 2초 주기에서 그것은 "변형이
         * 있다"가 아니라 "가끔 바뀐다"로 읽힌다. 사람이 무작위에서 기대하는
         * 것은 균등 분포가 아니라 **겹치지 않음**이다.
         */
        private Sprite[] PickAttackClip()
        {
            var primary = definition.attackFrames;
            var variants = definition.attackVariants;

            int extra = 0;
            if (variants != null)
            {
                foreach (var variant in variants)
                    if (variant != null && variant.frames != null && variant.frames.Length > 0) extra++;
            }

            bool hasPrimary = primary != null && primary.Length > 0;
            int total = extra + (hasPrimary ? 1 : 0);
            if (total == 0) return primary;
            if (total == 1) return hasPrimary ? primary : FirstVariant(variants);

            // 직전 것을 뺀 나머지에서 고른다
            int pick = UnityEngine.Random.Range(0, total - 1);
            int seen = 0;

            if (hasPrimary)
            {
                if (primary != lastAttack)
                {
                    if (seen == pick) return Remember(primary);
                    seen++;
                }
            }

            foreach (var variant in variants)
            {
                if (variant == null || variant.frames == null || variant.frames.Length == 0) continue;
                if (variant.frames == lastAttack) continue;
                if (seen == pick) return Remember(variant.frames);
                seen++;
            }

            // 직전 것이 후보에 없었다면(첫 주기) 하나가 남는다
            return Remember(hasPrimary ? primary : FirstVariant(variants));
        }

        private Sprite[] Remember(Sprite[] clip)
        {
            lastAttack = clip;
            return clip;
        }

        private static Sprite[] FirstVariant(EnemyDefinition.AttackVariant[] variants)
        {
            if (variants == null) return null;
            foreach (var variant in variants)
                if (variant != null && variant.frames != null && variant.frames.Length > 0) return variant.frames;
            return null;
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

            // 이번 주기에 쓸 공격 그림. **주기가 바뀔 때 한 번만** 고른다.
            //
            // 매 프레임 고르면 예비 동작 길이가 프레임마다 달라져 스윙이
            // 시작되는 시각이 흔들린다 - 클립마다 길이가 다르기 때문이다.
            if (currentAttack == null) currentAttack = PickAttackClip();

            // 애니메이션은 타격보다 lead 시간만큼 먼저 시작한다. 플레이어의
            // PlayerCombat과 같은 규칙이다 - 칼이 닿는 프레임과 피해가 들어가는
            // 순간이 겹쳐야 한 사건으로 읽힌다
            float swingDuration = currentAttack != null && currentAttack.Length > 0
                ? Mathf.Min(currentAttack.Length / definition.frameRate, interval)
                : 0f;

            // 공격 그림이 없는 요괴도 예비 동작 시간을 갖는다.
            //
            // 잡몹 팩에는 공격 태그가 없는 것이 대부분인데, 그 잡몹이 확대판
            // 보스로 서면(외눈 등롱, 정예) 선 자세 그대로 플레이어의 체력을
            // 깎는다. 그때 알릴 수 있는 것은 이펙트뿐이고, 이펙트도 타격보다
            // 먼저 떠야 "지금 친다"가 된다 - 동시에 뜨면 이미 맞은 뒤다.
            //
            // 이 값은 **그림이 뜨는 시각만** 옮긴다. 피해는 아래에서 주기가
            // 찰 때 들어가고, 그 판정은 이 값을 보지 않는다
            float lead = swingDuration > 0f
                ? swingDuration * definition.attackImpactPoint
                : Mathf.Min(definition.attackTelegraphSeconds, interval);

            if (!swingStarted && attackTimer >= interval - lead)
            {
                if (swingDuration > 0f)
                {
                    float rate = currentAttack.Length / Mathf.Max(0.0001f, swingDuration);
                    animator.Play(currentAttack, rate, false, PlayResting);
                }
                swingStarted = true;

                var swing = SwingStarted;
                if (swing != null) swing(this);
            }

            if (attackTimer < interval) return;

            attackTimer -= interval;
            swingStarted = false;

            // 다음 주기는 다시 고른다
            currentAttack = null;

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
            SwingStarted = null;

            // 풀로 돌아가는 인스턴스는 확대판 보스였을 수 있다. 배율과 틴트를
            // 되돌리지 않으면 다음에 잡몹으로 재사용될 때 두 배 크기의 물든
            // 요괴가 나온다
            bodyScale = 1f;
            baseTint = Color.white;
            AttackDamage = 0d;
            swingStarted = false;
            currentAttack = null;
            lastAttack = null;
            transform.localScale = Vector3.one;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }
    }
}
