using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 사무라이의 자동 발도 공격.
     *
     * 방치형은 플레이하는 것이 아니라 보는 것이므로 루프 전체는 단순하다.
     * 요괴가 사거리에 들어오길 기다렸다가 쿨다운마다 휘두르고, 접촉을 납득시킨다.
     * 납득시키는 것은 참격 이펙트·데미지·히트스톱 세 가지가 같은 프레임에 떨어지는
     * 일이며, 임팩트를 스윙 시작 시점이 아니라 공격 애니메이션에서 유도하는 이유가
     * 바로 이것이다.
     */
    public sealed class PlayerCombat : MonoBehaviour
    {
        private enum State { Idle, Winding, Recovering }

        [Header("참조")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private SlashVfx slashPrefab;
        [SerializeField] private Transform vfxParent;
        [SerializeField] private Onikiri.UI.DamageNumberSpawner damageNumbers;

        [Header("애니메이션")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField] private Sprite[] slashFrames;
        [SerializeField] private float idleFrameRate = 10f;
        [SerializeField] private float attackFrameRate = 14f;
        [SerializeField] private float slashFrameRate = 24f;

        [Header("전투")]
        [Tooltip("사무라이의 사거리 (world units)")]
        [SerializeField] private float attackRange = 1.9f;
        [SerializeField] private float attacksPerSecond = 1.15f;
        [Tooltip("처음부터 BigDouble. 공격력은 주 성장 축이라 방치 몇 시간이면 " +
                 "long 범위를 벗어난다")]
        [SerializeField] private BigDouble damage = BigDouble.FromDouble(5d);

        [Tooltip("칼이 닿기까지 공격 애니메이션에서 지나가는 비율")]
        [Range(0f, 1f)]
        [SerializeField] private float impactPoint = 0.45f;

        [Header("타격감")]
        [SerializeField] private ScreenShake cameraShake;
        [SerializeField] private HitAudio hitAudio;

        [Tooltip("보정 전, 낮은 공격속도에서의 히트스톱 길이")]
        [SerializeField] private float hitStopSeconds = 0.07f;

        [Tooltip("플레이 1초당 허용되는 히트스톱 시간. 공격속도가 오르면 정지를 " +
                 "제한한다. CombatFeel 참고")]
        [SerializeField] private float hitStopBudgetPerSecond = 0.3f;

        [Tooltip("보정 전, 낮은 공격속도에서의 흔들림 길이")]
        [SerializeField] private float shakeSeconds = 0.1f;

        [Tooltip("플레이 1초당 허용되는 흔들림 시간")]
        [SerializeField] private float shakeBudgetPerSecond = 0.4f;

        [Tooltip("흔들림 세기 (원본 픽셀)")]
        [SerializeField] private float shakePixels = 3f;

        [Tooltip("플레이 1초당 허용되는 참격 이펙트 표시 시간. 이것이 없으면 공격속도가 " +
                 "오를 때 참격이 서로 겹쳐 화면이 흰 덩어리가 된다")]
        [SerializeField] private float slashBudgetPerSecond = 0.45f;

        [Tooltip("참격이 나타나는 위치. 대상에서 사무라이 쪽으로의 오프셋")]
        [SerializeField] private Vector2 slashOffset = new Vector2(-0.15f, 0.55f);

        [Header("풀")]
        [SerializeField] private int slashPrewarm = 6;

        private ObjectPool<SlashVfx> slashPool;
        private State state = State.Idle;
        private float cooldownRemaining;
        private float stateTimer;

        /** 공격속도로 압축되기 전, 설계된 스윙 길이 */
        private float baseAttackDuration;

        /** 현재 재생 중인 스윙의 길이 */
        private float attackDuration;
        private bool impactDelivered;
        private Enemy currentTarget;

        public int SlashPoolGrowthCount { get { return slashPool != null ? slashPool.GrowthCount : 0; } }

        /**
         * @brief 업그레이드가 스탯을 갱신하는 진입점.
         *
         * 값을 바꾸는 것만으로 즉시 반영된다. 스윙 길이와 각 효과의 예산은 공격마다
         * attacksPerSecond에서 다시 계산되므로, 여기서 따로 해줄 일이 없다.
         */
        public BigDouble Damage
        {
            get { return damage; }
            set { damage = value; }
        }

        public float AttacksPerSecond
        {
            get { return attacksPerSecond; }
            set { attacksPerSecond = Mathf.Max(0.01f, value); }
        }

        /**
         * @brief 시작 이후 실제로 시작된 스윙 수.
         *
         * 공격속도 스탯이 실제 공격 횟수로 이어지는지 확인하는 용도다. 애니메이션
         * 길이가 조용히 상한이 되어 스탯을 올려도 초당 2회에서 멈춘 적이 있었고,
         * 그때는 화면만 봐서는 알아챌 수 없었다.
         */
        public int AttackCount { get; private set; }

        private void Awake()
        {
            if (vfxParent == null) vfxParent = transform;
            if (slashPrefab != null) slashPool = new ObjectPool<SlashVfx>(slashPrefab, vfxParent, slashPrewarm);

            baseAttackDuration = attackFrames != null && attackFrames.Length > 0
                ? attackFrames.Length / attackFrameRate
                : 0.4f;
            attackDuration = baseAttackDuration;
        }

        private void Start()
        {
            PlayIdle();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f) cooldownRemaining -= Time.deltaTime;

            switch (state)
            {
                case State.Idle:
                    TryStartAttack();
                    break;

                case State.Winding:
                    stateTimer += Time.deltaTime;
                    if (!impactDelivered && stateTimer >= attackDuration * impactPoint) DeliverImpact();
                    if (stateTimer >= attackDuration)
                    {
                        state = State.Idle;
                        PlayIdle();
                        // 스윙이 끝난 프레임에서 바로 다음 스윙을 시도한다. 다음 Update로
                        // 넘기면 공격마다 한 프레임이 통째로 버려지는데, 공격속도가 낮을
                        // 때는 오차가 1%도 안 되지만 초당 20회에서는 프레임 하나가 공격
                        // 주기의 절반이라 실제 공격 횟수가 설정값의 절반으로 떨어진다
                        TryStartAttack();
                    }
                    break;
            }
        }

        private void TryStartAttack()
        {
            if (cooldownRemaining > 0f) return;
            if (spawner == null) return;

            var target = spawner.FindNearestAlive(transform.position.x, attackRange);
            if (target == null) return;

            currentTarget = target;
            state = State.Winding;
            stateTimer = 0f;
            impactDelivered = false;
            AttackCount++;

            float interval = 1f / Mathf.Max(0.01f, attacksPerSecond);
            cooldownRemaining = interval;

            // 스윙이 공격 간격 안에 들어가야 한다. 아니면 애니메이션이 실제 공격
            // 속도의 상한이 된다. 7프레임 14fps 스윙은 0.5초라, 스탯을 아무리 올려도
            // 초당 2회에서 멈춘다. 공격속도는 두 자릿수까지 계속 의미가 있어야 하는
            // 핵심 성장 축이다. 클립을 압축하면 연출상으로도 맞다. 스탯이 오르면
            // 스윙도 눈에 띄게 빨라진다.
            attackDuration = Mathf.Min(baseAttackDuration, interval);

            if (attackFrames != null && attackFrames.Length > 0)
            {
                float rate = attackFrames.Length / Mathf.Max(0.0001f, attackDuration);
                animator.Play(attackFrames, rate, false);
            }
        }

        /** 칼이 닿는 프레임. 이펙트·데미지·정지가 동시에 일어난다 */
        private void DeliverImpact()
        {
            impactDelivered = true;

            // 예비 동작 도중 대상이 죽거나 사거리 밖으로 나갈 수 있다. 다시 탐색해서
            // 스윙이 실제로 사무라이 앞에 있는 대상에게 닿게 한다
            if (currentTarget == null || !currentTarget.IsTargetable)
                currentTarget = spawner.FindNearestAlive(transform.position.x, attackRange);

            if (currentTarget == null) return;

            // transform이 아니라 그려진 스프라이트의 중심을 겨냥한다. Enemy.HitPoint 참고
            Vector3 impactPosition = currentTarget.HitPoint
                                     + new Vector3(slashOffset.x, slashOffset.y, 0f);

            SpawnSlash(impactPosition);

            var hitPoint = currentTarget.HitPoint;
            currentTarget.TakeDamage(damage);
            bool killed = !currentTarget.IsAlive;

            if (damageNumbers != null) damageNumbers.Show(damage, hitPoint, killed);
            if (hitAudio != null)
            {
                if (killed) hitAudio.PlayKill();
                else hitAudio.PlayHit();
            }

            // 셋이 같은 프레임에 떨어진다. 정지와 흔들림은 공격속도가 오를수록 짧아져서
            // 후반의 연속 스윙이 계속 끊기는 화면이 되지 않게 한다. CombatFeel 참고
            HitStop.Request(CombatFeel.ScaledDuration(hitStopSeconds, hitStopBudgetPerSecond, attacksPerSecond));
            ScreenShake.Request(cameraShake,
                CombatFeel.ScaledDuration(shakeSeconds, shakeBudgetPerSecond, attacksPerSecond),
                shakePixels);
        }

        private void SpawnSlash(Vector3 position)
        {
            if (slashPool == null || slashFrames == null || slashFrames.Length == 0) return;

            // 참격도 히트스톱·흔들림과 같은 예산 규칙을 따른다. 여기가 상한이 없던
            // 마지막 효과였다. 스윙 압축으로 공격속도가 실제로 두 자릿수까지 올라가므로,
            // 고정 22fps(4프레임 = 0.18초)를 유지하면 초당 열 번 공격할 때 참격 두세
            // 개가 항상 겹쳐 있게 된다. 흰 호가 서로 포개지면 개별 타격이 보이지 않고
            // 화면 가운데가 흰 얼룩으로 뭉개진다.
            //
            // 프레임 수를 줄이지 않고 재생 속도만 올린다. 이펙트의 형태는 그대로 두고
            // 화면에 머무는 시간만 줄이는 쪽이 픽셀 아트에서는 훨씬 덜 티가 난다.
            float baseDuration = slashFrames.Length / Mathf.Max(0.0001f, slashFrameRate);
            float duration = CombatFeel.ScaledDuration(baseDuration, slashBudgetPerSecond, attacksPerSecond);
            float rate = slashFrames.Length / Mathf.Max(0.0001f, duration);

            var slash = slashPool.Get();
            slash.Play(slashFrames, rate, position, false, ReleaseSlash);
        }

        private void ReleaseSlash(SlashVfx slash)
        {
            slashPool.Release(slash);
        }

        private void PlayIdle()
        {
            if (idleFrames != null && idleFrames.Length > 0)
                animator.Play(idleFrames, idleFrameRate, true);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, attackRange);
        }
#endif
    }
}
