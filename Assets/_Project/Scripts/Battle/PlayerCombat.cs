using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 사무라이의 자동 발도 공격.
     *
     * 방치형은 플레이하는 것이 아니라 보는 것이므로 루프 전체는 단순하다.
     * 요괴가 사거리에 들어오길 기다렸다가 일정 간격으로 휘두르고, 접촉을 납득시킨다.
     *
     * **데미지는 타이머가 결정하고 애니메이션은 장식이다.** 예전에는 반대였다. 스윙
     * 상태 기계가 끝나야 다음 공격이 시작될 수 있어서, 공격 하나가 항상 정수 개의
     * 프레임을 차지했고 실제 공격 횟수가 설정값의 70~85%에 머물렀다. 방치형에서
     * 스탯 표기와 실제가 다른 것은 연출 문제가 아니라 신뢰 문제다. 이제 타이머는
     * 프레임 경계와 무관하게 누적되고, 남은 시간은 다음 프레임으로 이월된다.
     *
     * 애니메이션은 타격보다 lead 시간만큼 먼저 시작한다. 사무라이 아트에는 흰 검격
     * 궤적이 스윙의 4/7 지점에 그려져 있어서, 그 프레임이 데미지가 들어가는 순간과
     * 겹쳐야 참격·궤적·정지가 하나로 읽힌다.
     */
    public sealed class PlayerCombat : MonoBehaviour
    {
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

        [Tooltip("칼이 닿기까지 공격 애니메이션에서 지나가는 비율. 데미지 시점을 " +
                 "정하지 않는다. 애니메이션을 얼마나 먼저 시작할지를 정한다")]
        [Range(0f, 1f)]
        [SerializeField] private float impactPoint = 0.45f;

        [Header("치명타")]
        [Tooltip("치명타 확률. 아직 강화 대상이 아니라 고정값이다")]
        [Range(0f, 1f)]
        [SerializeField] private float critChance = 0.12f;

        [Tooltip("치명타 배수")]
        [SerializeField] private float critMultiplier = 2f;

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

        [Tooltip("타격마다 터지는 벚꽃잎. 비어 있어도 전투는 그대로 돈다")]
        [SerializeField] private SakuraBurst sakura;

        [Tooltip("참격이 지나가는 기준 방향. 사무라이는 오른쪽 위로 벤다. " +
                 "꽃잎은 이 방향을 중심으로 부채꼴로 흩어진다")]
        [SerializeField] private Vector2 slashDirection = new Vector2(1f, 0.45f);

        [Header("풀")]
        [SerializeField] private int slashPrewarm = 6;

        private ObjectPool<SlashVfx> slashPool;

        /**
         * @brief 다음 타격까지 쌓인 시간.
         *
         * 프레임 경계에서 0으로 되돌리지 않고 간격만큼만 빼낸다. 그래야 남은 시간이
         * 이월되어 실제 공격 횟수가 설정값과 맞는다.
         */
        private float attackTimer;

        /** 공격속도로 압축되기 전, 설계된 스윙 길이. 0이면 아직 재지 않았다 */
        private float measuredAttackDuration;

        /** 이번 주기의 스윙 애니메이션이 이미 시작됐는지 */
        private bool swingStarted;

        /**
         * @brief 한 프레임에 밀어넣을 수 있는 타격 수의 상한.
         *
         * 프레임이 길어지면 (에디터 멈칫, 앱 복귀 직후) 밀린 시간만큼 타격이 한꺼번에
         * 쏟아진다. 소리와 이펙트가 같은 프레임에 몰리는 것은 보상이 아니라 사고로
         * 보이므로, 넘치는 분은 지급하지 않고 버린다.
         */
        private const int MaxHitsPerFrame = 3;

        public int SlashPoolGrowthCount { get { return slashPool != null ? slashPool.GrowthCount : 0; } }

        public int SakuraPoolGrowthCount { get { return sakura != null ? sakura.PoolGrowthCount : 0; } }

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

        /**
         * @brief 설계된 스윙 길이. 프레임 수를 프레임레이트로 나눈 값이다.
         *
         * Awake가 아니라 여기서 늦게 재는 이유는, 강화가 Start에서 스탯을 밀어넣을 때
         * 이미 상한이 필요하기 때문이다. Awake에서만 재면 실행 순서가 조금만 달라져도
         * 상한이 0인 상태로 클램프가 돌아 공격속도가 0에 붙는다.
         */
        public float BaseAttackDuration
        {
            get
            {
                if (measuredAttackDuration <= 0f)
                {
                    measuredAttackDuration = attackFrames != null && attackFrames.Length > 0 && attackFrameRate > 0f
                        ? attackFrames.Length / attackFrameRate
                        : 0.4f;
                }
                return measuredAttackDuration;
            }
        }

        /**
         * @brief 아트가 허용하는 최대 공격속도.
         *
         * 튜닝 값이 아니라 계산 결과다. 스윙은 CombatFeel.MaxAnimationSpeed 배까지만
         * 당길 수 있고 공격 간격 안에 들어가야 하므로, 상한은 클립 길이 하나로 정해진다.
         * 클립을 다른 것으로 바꾸면 상한도 따라 움직인다.
         */
        public float MaxAttacksPerSecond
        {
            get { return CombatFeel.MaxAttacksPerSecond(BaseAttackDuration); }
        }

        /**
         * @brief 공격속도. 아트가 정한 상한에서 잘린다.
         *
         * 클램프를 여기 둔 이유는 진입점이 하나여야 하기 때문이다. 강화·테스트 패널·
         * 세이브 복원이 각자 값을 넣는데, 그중 하나라도 상한을 잊으면 스윙이 프레임
         * 단위로 깜빡이는 상태가 되고 그건 화면을 봐야만 알 수 있다.
         */
        public float AttacksPerSecond
        {
            get { return attacksPerSecond; }
            set { attacksPerSecond = Mathf.Clamp(value, 0.01f, MaxAttacksPerSecond); }
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

            // 인스펙터에 상한을 넘는 값이 남아 있을 수 있다. 프로퍼티를 거쳐 한 번
            // 통과시켜 씬의 값과 코드가 정한 상한을 처음부터 맞춰둔다
            AttacksPerSecond = attacksPerSecond;
        }

        private void Start()
        {
            PlayIdle();
        }

        private void Update()
        {
            float interval = 1f / Mathf.Max(0.01f, attacksPerSecond);

            // 스윙은 공격 간격 안에 들어가야 한다. 아니면 다음 스윙이 시작될 때
            // 이전 스윙이 아직 재생 중이라 동작이 뭉개진다. 스탯이 오르면 스윙도
            // 눈에 띄게 빨라지므로 연출상으로도 맞다.
            //
            // 다만 무한히 당기지는 않는다. 2배속 아래로 내려가면 프레임 하나가 화면
            // 프레임 하나보다 짧아져 발도 동작이 아니라 깜빡임으로 읽힌다. 공격속도
            // 자체가 MaxAttacksPerSecond에서 잘리므로 정상 경로에서는 이 하한에 정확히
            // 닿을 뿐 넘지 않는다. 여기 클램프는 테스트 패널이 상한 밖의 값을 직접
            // 밀어넣을 때를 위한 것이다
            float swingDuration = Mathf.Max(
                CombatFeel.MinSwingDuration(BaseAttackDuration),
                Mathf.Min(BaseAttackDuration, interval));
            float lead = swingDuration * impactPoint;

            var target = spawner != null
                ? spawner.FindNearestAlive(transform.position.x, attackRange)
                : null;

            if (target == null)
            {
                // 대상이 없으면 타격 직전에서 타이머를 멈춰 세운다. 요괴가 들어오면
                // lead 시간만큼 예비 동작을 하고 곧바로 벤다. 자유롭게 쌓이게 두면
                // 비어 있던 시간만큼 첫 등장에 타격이 몰아친다
                attackTimer = Mathf.Min(attackTimer + Time.deltaTime, interval - lead);
                return;
            }

            attackTimer += Time.deltaTime;

            int delivered = 0;
            while (attackTimer >= interval && delivered < MaxHitsPerFrame)
            {
                attackTimer -= interval;
                DeliverHit(target);
                swingStarted = false;
                delivered++;

                target = spawner.FindNearestAlive(transform.position.x, attackRange);
                if (target == null) break;
            }

            // 상한에 걸려 남은 몫은 이월하지 않고 버린다
            if (attackTimer > interval) attackTimer = interval;

            if (!swingStarted && target != null && attackTimer >= interval - lead)
            {
                PlaySwing(swingDuration);
                swingStarted = true;
            }
        }

        /** 데미지가 들어가는 순간. 참격·숫자·소리·정지가 전부 여기서 함께 난다 */
        private void DeliverHit(Enemy target)
        {
            if (target == null || !target.IsTargetable) return;

            AttackCount++;

            bool crit = critChance > 0f && Random.value < critChance;
            BigDouble dealt = crit
                ? damage * BigDouble.FromDouble(critMultiplier)
                : damage;

            // transform이 아니라 그려진 스프라이트의 중심을 겨냥한다. Enemy.HitPoint 참고
            var hitPoint = target.HitPoint;
            SpawnSlash(hitPoint + new Vector3(slashOffset.x, slashOffset.y, 0f));

            // 꽃잎은 참격 오프셋을 쓰지 않는다. 참격은 이펙트 아트의 중심을 맞추려고
            // 칼 쪽으로 당겨져 있지만, 꽃잎은 베인 대상에서 떨어져 나가는 것이므로
            // 요괴가 그려진 자리에서 나와야 한다
            if (sakura != null) sakura.Play(hitPoint, slashDirection, attacksPerSecond);

            target.TakeDamage(dealt);
            bool killed = !target.IsAlive;

            if (damageNumbers != null)
            {
                // 처치가 치명타보다 우선한다. 한 타격에 둘 다 해당하면 플레이어에게
                // 더 중요한 정보는 "죽었다" 쪽이다
                var style = killed ? Onikiri.UI.DamageStyle.Kill
                          : crit   ? Onikiri.UI.DamageStyle.Critical
                                   : Onikiri.UI.DamageStyle.Normal;

                // 대상 키를 함께 넘겨서 같은 요괴를 연속으로 때릴 때 숫자가 합산되게
                // 한다. 초당 여덟 번 구간에서는 팝업이 서로 겹쳐 어떤 숫자도 읽을 수 없다
                damageNumbers.Show(dealt, hitPoint, style, target);
            }

            if (hitAudio != null)
            {
                if (killed) hitAudio.PlayKill();
                else hitAudio.PlayHit();
            }

            // 정지와 흔들림은 공격속도가 오를수록 짧아져서 후반의 연속 스윙이 계속
            // 끊기는 화면이 되지 않게 한다. CombatFeel 참고
            HitStop.Request(CombatFeel.ScaledDuration(hitStopSeconds, hitStopBudgetPerSecond, attacksPerSecond));
            ScreenShake.Request(cameraShake,
                CombatFeel.ScaledDuration(shakeSeconds, shakeBudgetPerSecond, attacksPerSecond),
                shakePixels);
        }

        /** 장식용 스윙. 끝나면 스스로 idle로 돌아간다 */
        private void PlaySwing(float duration)
        {
            if (attackFrames == null || attackFrames.Length == 0) return;

            float rate = attackFrames.Length / Mathf.Max(0.0001f, duration);
            animator.Play(attackFrames, rate, false, PlayIdle);
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
