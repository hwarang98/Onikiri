using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// The samurai's automatic iai attack.
    ///
    /// Idle games are watched, not played, so the whole loop is: wait for a yokai to come
    /// into reach, swing on cooldown, and sell the contact. The selling is three things
    /// landing on the same frame - the slash effect, the damage, and a hitstop - which is
    /// why impact is scheduled off the attack animation rather than fired when the swing
    /// starts.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        private enum State { Idle, Winding, Recovering }

        [Header("References")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private SlashVfx slashPrefab;
        [SerializeField] private Transform vfxParent;

        [Header("Animation")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField] private Sprite[] slashFrames;
        [SerializeField] private float idleFrameRate = 10f;
        [SerializeField] private float attackFrameRate = 14f;
        [SerializeField] private float slashFrameRate = 24f;

        [Header("Combat")]
        [Tooltip("How far the samurai can reach, in world units.")]
        [SerializeField] private float attackRange = 1.9f;
        [SerializeField] private float attacksPerSecond = 1.15f;
        [SerializeField] private float damage = 5f;

        [Tooltip("Fraction of the attack animation before the blade connects.")]
        [Range(0f, 1f)]
        [SerializeField] private float impactPoint = 0.45f;

        [Header("Feel")]
        [SerializeField] private float hitStopSeconds = 0.07f;
        [Tooltip("Where the slash appears, measured from the target towards the samurai.")]
        [SerializeField] private Vector2 slashOffset = new Vector2(-0.15f, 0.55f);

        [Header("Pool")]
        [SerializeField] private int slashPrewarm = 6;

        private ObjectPool<SlashVfx> slashPool;
        private State state = State.Idle;
        private float cooldownRemaining;
        private float stateTimer;
        private float attackDuration;
        private bool impactDelivered;
        private Enemy currentTarget;

        public int SlashPoolGrowthCount { get { return slashPool != null ? slashPool.GrowthCount : 0; } }

        private void Awake()
        {
            if (vfxParent == null) vfxParent = transform;
            if (slashPrefab != null) slashPool = new ObjectPool<SlashVfx>(slashPrefab, vfxParent, slashPrewarm);

            attackDuration = attackFrames != null && attackFrames.Length > 0
                ? attackFrames.Length / attackFrameRate
                : 0.4f;
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
            cooldownRemaining = 1f / Mathf.Max(0.01f, attacksPerSecond);

            if (attackFrames != null && attackFrames.Length > 0)
                animator.Play(attackFrames, attackFrameRate, false);
        }

        /// <summary>The frame the blade lands: effect, damage and freeze together.</summary>
        private void DeliverImpact()
        {
            impactDelivered = true;

            // The target can die or walk out of reach during the wind-up; re-acquire so the
            // swing still connects with whatever is actually in front of the samurai.
            if (currentTarget == null || !currentTarget.IsTargetable)
                currentTarget = spawner.FindNearestAlive(transform.position.x, attackRange);

            if (currentTarget == null) return;

            // Aim at the drawn sprite's centre, not the transform - see Enemy.HitPoint.
            Vector3 impactPosition = currentTarget.HitPoint
                                     + new Vector3(slashOffset.x, slashOffset.y, 0f);

            SpawnSlash(impactPosition);
            currentTarget.TakeDamage(damage);
            HitStop.Request(hitStopSeconds);
        }

        private void SpawnSlash(Vector3 position)
        {
            if (slashPool == null || slashFrames == null || slashFrames.Length == 0) return;

            var slash = slashPool.Get();
            slash.Play(slashFrames, slashFrameRate, position, false, ReleaseSlash);
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
