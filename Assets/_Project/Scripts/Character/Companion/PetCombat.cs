using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 로닌 옆에서 함께 싸우는 동료 하나. 동료마다 이 컴포넌트가 하나씩 선다.
     *
     * ## 다중 출전 - 컴포넌트 하나 = 동료 하나
     *
     * 보유 동료 전원이 함께 출전하므로, 빌더가 동료 수만큼 전투체를 세우고
     * 각자 자기 id의 해금 상태만 본다. 처음(단일 출전)에는 한 컴포넌트가
     * 액티브 펫으로 갈아입는 구조였는데, 다중이 되면서 "갈아입기"가 통째로
     * 사라졌다 - 각자 자기 옷만 입는다.
     *
     * ## 데미지는 타이머가 결정하고 애니메이션은 장식이다
     *
     * PlayerCombat과 같은 원칙이다. 공격 간격은 PetSystem의 자기 슬롯에서
     * 읽고, 타이머가 간격을 넘을 때 타격이 나간다. 애니메이션 이벤트를 쓰지
     * 않는다.
     *
     * ## 한 타의 크기 = 자기 보너스 x 플레이어 기대 DPS x 자기 공격 간격
     *
     * 그래서 동료들의 초당 기여 합이 정확히 "합산 보너스 x 플레이어 DPS"가
     * 되고, 시뮬레이션의 (1 + 합산 보너스) 축과 화면이 같은 값을 낸다
     * (PetCurve 주석). 치명타를 따로 굴리지 않는 것도 이 식의 결과다 -
     * 기대 DPS에 치명타 기대값이 이미 들어 있다.
     *
     * ## 방향은 artFacesLeft에서 유도한다 - 하드코딩 flip 금지
     *
     * 동료는 적(오른쪽)을 본다. 그려진 방향은 팩마다 다르므로(늑대는 왼쪽,
     * 궁수·팬더는 오른쪽) Enemy.FacingDirection과 같은 규칙으로 유도한다:
     * flipX = artFacesLeft (왼쪽으로 그려진 팩만 뒤집으면 모두 오른쪽을
     * 본다). 화살 머즐·비행 방향도 이 facing에서 나온다.
     *
     * ## 히트스톱 예산을 쓰지 않는다
     *
     * 정지 예산(초당 0.3초)은 플레이어 공격속도 기준으로 짜여 있다
     * (CombatFeel). 동료가 셋씩 얹히면 후반 화면이 계속 끊기므로, 동료
     * 타격은 데미지 숫자(전용 색)와 타격음만 낸다. 모든 시간은 스케일
     * 타임이다 - 히트스톱이 걸리면 동료도 화살도 함께 언다.
     */
    public sealed class PetCombat : MonoBehaviour
    {
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private BossFight bossFight;
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private HitAudio hitAudio;
        [SerializeField] private Onikiri.UI.DamageNumberSpawner damageNumbers;

        [Tooltip("이 전투체가 맡는 동료의 id (PetCatalog)")]
        [SerializeField] private string petId;

        [Tooltip("원화가 왼쪽을 보고 그렸는가. 늑대만 true - flipX가 여기서 유도된다")]
        [SerializeField] private bool artFacesLeft;

        [Tooltip("이 동료의 클립. 빌더가 굽는다")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] runFrames;
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField] private float idleFrameRate = 8f;
        [SerializeField] private float runFrameRate = 12f;
        [SerializeField] private float attackFrameRate = 14f;

        [Tooltip("타깃을 찾는 사거리. 제자리에서 프론트라인까지 닿아야 한다")]
        [SerializeField] private float attackRange = 2.4f;

        [Tooltip("동료 데미지 숫자의 색. 플레이어의 흰·금·적과 갈라야 한다")]
        [SerializeField] private Color numberTint = new Color32(0x9B, 0xE8, 0xD8, 0xFF);

        [Tooltip("스윙 시작을 타격보다 얼마나 앞당길지 (0~1, 클립 진행 비율)")]
        [SerializeField] private float impactPoint = 0.5f;

        [Tooltip("등장 연출에서 달려오는 속도 (유닛/초)")]
        [SerializeField] private float entranceSpeed = 3.2f;

        // ---------------------------------------------------------------- 화살 (궁수만)

        [Tooltip("궁수의 화살 한 장. 비어 있으면 근접으로 때린다")]
        [SerializeField] private Sprite arrowSprite;

        [Tooltip("화살이 꽂힐 때의 프레임들 (ARROW HIT)")]
        [SerializeField] private Sprite[] arrowHitFrames;

        [SerializeField] private float arrowHitFrameRate = 20f;
        [SerializeField] private float arrowSpeed = 14f;

        [Tooltip("화살이 나가는 지점 (동료 기준 로컬, x는 바라보는 방향)")]
        [SerializeField] private Vector2 arrowMuzzle = new Vector2(0.4f, 0.7f);

        private float attackTimer;
        private bool swingStarted;
        private float baseLocalX;
        private float entranceFromX;
        private float entranceProgress = 1f;

        private int petIndex = -1;
        private bool fielded;
        private bool ranged;
        private double attackInterval = 1d;

        /** 이번 프레임에 처리할 수 있는 최대 타격 수. PlayerCombat과 같은 상한 */
        private const int MaxHitsPerFrame = 3;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            baseLocalX = transform.localPosition.x;

            // 동료는 아군이다 - 적(오른쪽)을 본다. 규칙은 Facing이 들고 있고
            // (거기 머리 주석에 이 느낌표를 세 번 틀린 이야기가 있다), 여기
            // 말고 다른 곳에서 flipX를 만지지 않는다
            if (spriteRenderer != null) spriteRenderer.flipX = Facing.Ally(artFacesLeft);
        }

        private void Start()
        {
            var system = PetSystem.Instance;
            if (system != null)
            {
                system.Changed += Refresh;
                system.Unlocked += OnUnlocked;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            var system = PetSystem.Instance;
            if (system != null)
            {
                system.Changed -= Refresh;
                system.Unlocked -= OnUnlocked;
            }
        }

        // ---------------------------------------------------------------- 출전 상태

        /**
         * @brief 자기 동료의 해금 상태에 맞춰 선다/숨는다.
         *
         * 세이브 복원·해금·초기화가 전부 이 문을 지난다(PetSystem.Changed).
         * 재생 주체는 자기 animator 하나뿐이다 - EvolutionAppearance가 못 박은
         * "애니메이터를 직접 만지는 곳은 한 곳" 규칙 그대로다.
         */
        private void Refresh()
        {
            var system = PetSystem.Instance;
            petIndex = system != null ? system.IndexOf(petId) : -1;

            var slot = system != null ? system.GetPet(petIndex) : null;
            bool wantFielded = slot != null && slot.unlocked
                               && idleFrames != null && idleFrames.Length > 0;

            if (!wantFielded)
            {
                fielded = false;
                if (animator != null) animator.Stop();
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                return;
            }

            ranged = slot.ranged;
            attackInterval = slot.attackIntervalSeconds > 0.05d ? slot.attackIntervalSeconds : 1d;

            if (spriteRenderer != null) spriteRenderer.enabled = true;

            // 새로 서는 순간에만 클립을 튼다. 레벨업 같은 무관한 Changed에서
            // 진행 중 클립을 다시 재생하면 첫 프레임으로 튄다
            if (!fielded)
            {
                fielded = true;
                swingStarted = false;
                attackTimer = 0f;
                PlayResting();
            }
        }

        /** 해금 순간의 등장 - 화면 왼쪽 밖에서 자기 자리까지 달려 들어온다 */
        private void OnUnlocked(int index)
        {
            var system = PetSystem.Instance;
            if (system == null || system.IndexOf(petId) != index) return;

            entranceFromX = baseLocalX - 3.4f;
            entranceProgress = 0f;

            var local = transform.localPosition;
            local.x = entranceFromX;
            transform.localPosition = local;
        }

        // ---------------------------------------------------------------- 루프

        private void Update()
        {
            if (!fielded) return;

            // 등장 연출. 달리는 클립으로 자기 자리까지 - 스케일 타임이라
            // 히트스톱이 걸리면 등장도 함께 멈춘다
            if (entranceProgress < 1f)
            {
                RunEntrance();
                return;
            }

            // 전투 정지 상태 존중. 등장 연출·클리어·실패 중에는 공격하지 않고,
            // 타이머도 쉰다 - 보스전이 시작되는 순간 밀린 타격이 쏟아지면 안 된다
            var phase = bossFight != null ? bossFight.Current : BossFight.Phase.Farming;
            if (phase == BossFight.Phase.Intro || phase == BossFight.Phase.Cleared
                || phase == BossFight.Phase.Failed)
            {
                swingStarted = false;
                PlayRestingIfIdle();
                return;
            }

            float interval = (float)attackInterval;
            var target = spawner != null
                ? spawner.FindNearestAlive(transform.position.x, attackRange)
                : null;

            if (target == null)
            {
                // 사거리에 아무도 없다. 타이머는 타격 직전까지만 차오른다 -
                // PlayerCombat과 같은 처리다. 다음 상대가 오자마자 즉시 한 대가
                // 나가되, 기다린 시간이 두 대 세 대로 불어나지는 않는다
                swingStarted = false;
                attackTimer = Mathf.Min(attackTimer + Time.deltaTime, interval - SwingLead(interval));
                PlayRestingIfIdle();
                return;
            }

            attackTimer += Time.deltaTime;

            int delivered = 0;
            while (attackTimer >= interval && delivered < MaxHitsPerFrame)
            {
                attackTimer -= interval;
                DeliverHit(target);
                delivered++;
                swingStarted = false;

                if (target == null || !target.IsTargetable)
                {
                    target = spawner.FindNearestAlive(transform.position.x, attackRange);
                    if (target == null) break;
                }
            }

            // 스윙은 타격보다 lead만큼 먼저 시작한다. 타격이 그려진 프레임이
            // DeliverHit 순간에 화면에 있게 하는 PlayerCombat의 규칙이다
            if (!swingStarted && attackTimer >= interval - SwingLead(interval)
                && animator != null && !animator.IsOneShot)
            {
                swingStarted = true;
                PlaySwing(interval);
            }
        }

        private void RunEntrance()
        {
            float distance = baseLocalX - entranceFromX;
            if (distance <= 0.001f) { FinishEntrance(); return; }

            entranceProgress += Time.deltaTime * entranceSpeed / distance;

            var local = transform.localPosition;
            local.x = Mathf.Lerp(entranceFromX, baseLocalX, entranceProgress);
            transform.localPosition = local;

            if (animator != null && animator.CurrentClip != runFrames
                && runFrames != null && runFrames.Length > 0)
                animator.Play(runFrames, runFrameRate, true);

            if (entranceProgress >= 1f) FinishEntrance();
        }

        private void FinishEntrance()
        {
            entranceProgress = 1f;
            var local = transform.localPosition;
            local.x = baseLocalX;
            transform.localPosition = local;
            PlayResting();
        }

        /** 스윙 시작을 타격보다 앞당기는 시간. 클립 길이와 간격 중 짧은 쪽 기준 */
        private float SwingLead(float interval)
        {
            if (attackFrames == null || attackFrames.Length == 0 || attackFrameRate <= 0f)
                return 0f;

            float clip = attackFrames.Length / attackFrameRate;
            return Mathf.Min(clip, interval) * Mathf.Clamp01(impactPoint);
        }

        // ---------------------------------------------------------------- 타격

        /**
         * @brief 이 동료의 한 대. 근접은 즉시, 궁수는 화살이 날아가 꽂힐 때 피해가 든다.
         *
         * 연출(피해·숫자·소리)을 한 함수 안에서 같은 프레임에 낸다 -
         * PlayerCombat.DeliverHit의 축소판이다. 불꽃과 정지·흔들림은 일부러
         * 없다(머리 주석).
         */
        private void DeliverHit(Enemy target)
        {
            if (target == null || !target.IsTargetable) return;

            var dealt = HitDamage();
            if (dealt <= BigDouble.Zero) return;

            if (ranged)
            {
                LaunchArrow(target, dealt);
                return;
            }

            ApplyHit(target, dealt);
        }

        /** 한 타의 크기. 머리 주석의 식 그대로다 */
        private BigDouble HitDamage()
        {
            var system = PetSystem.Instance;
            double bonus = system != null ? system.FieldedBonusOf(petIndex) : 0d;
            if (bonus <= 0d || playerCombat == null) return BigDouble.Zero;

            return playerCombat.ExpectedDps * BigDouble.FromDouble(bonus * attackInterval);
        }

        private void ApplyHit(Enemy target, BigDouble dealt)
        {
            if (target == null || !target.IsTargetable) return;

            var hitPoint = target.HitPoint;

            // 화면 숫자는 실제로 깎인 양이다 (PlayerCombat.DeliverHit과 같은 이유)
            dealt = target.TakeDamage(dealt, Onikiri.Progression.TrialDamageScale.Source.Companion);
            bool killed = !target.IsAlive;

            // 동료 숫자는 ShowSkill 통로를 쓴다. 새 DamageStyle을 만들지 않는
            // 규칙(셋뿐인 것이 설계)이고, 합산되지 않아 플레이어 숫자에 동료
            // 타격이 묻히지 않는다. 셋이 겹칠 때의 재사용은 스포너의 풀이
            // 맡는다. 크기 1배 - 구분은 색이 한다
            if (damageNumbers != null) damageNumbers.ShowSkill(dealt, hitPoint, numberTint, 1);

            if (hitAudio != null)
            {
                if (killed) hitAudio.PlayKill();
                else hitAudio.PlayHit();
            }
        }

        private void PlaySwing(float interval)
        {
            if (animator == null || attackFrames == null || attackFrames.Length == 0) return;

            // 간격이 클립보다 짧으면 클립을 간격에 맞춰 빠르게 돌린다 -
            // PlayerCombat이 swingDuration을 간격으로 자르는 것과 같은 이유다
            float fps = attackFrameRate;
            float clip = attackFrames.Length / fps;
            if (clip > interval) fps = attackFrames.Length / interval;

            animator.Play(attackFrames, fps, false, PlayResting);
        }

        private void PlayResting()
        {
            if (animator == null) return;

            if (ParallaxScroller.IsScrolling && runFrames != null && runFrames.Length > 0)
                animator.Play(runFrames, runFrameRate, true);
            else if (idleFrames != null && idleFrames.Length > 0)
                animator.Play(idleFrames, idleFrameRate, true);
        }

        /** 원샷(공격) 중이 아니면 전진/대기 클립으로 맞춘다 */
        private void PlayRestingIfIdle()
        {
            if (animator == null || animator.IsOneShot) return;

            bool scrolling = ParallaxScroller.IsScrolling;
            var want = scrolling && runFrames != null && runFrames.Length > 0
                ? runFrames : idleFrames;

            if (animator.CurrentClip != want) PlayResting();
        }

        // ---------------------------------------------------------------- 화살

        /** 날아가는 화살 하나. 풀에서 꺼내 쓴다 */
        private sealed class Arrow
        {
            public SpriteRenderer Renderer;
            public Enemy Target;
            public BigDouble Damage;
            public bool Flying;
        }

        private readonly System.Collections.Generic.List<Arrow> arrows =
            new System.Collections.Generic.List<Arrow>();

        private void LaunchArrow(Enemy target, BigDouble dealt)
        {
            if (arrowSprite == null)
            {
                // 화살 스프라이트가 안 구워졌으면 근접처럼 즉시 때린다 -
                // 데미지가 연출 누락에 볼모로 잡히면 안 된다
                ApplyHit(target, dealt);
                return;
            }

            var arrow = GetArrow();
            arrow.Target = target;
            arrow.Damage = dealt;
            arrow.Flying = true;
            arrow.Renderer.enabled = true;
            arrow.Renderer.sprite = arrowSprite;

            // 머즐 x는 바라보는 방향(오른쪽)의 오프셋이다. facing이 flipX에서
            // 유도되듯 여기도 같은 값에서 나온다 - 동료는 항상 적을 본다
            arrow.Renderer.transform.position =
                transform.position + new Vector3(arrowMuzzle.x, arrowMuzzle.y, 0f);
        }

        private Arrow GetArrow()
        {
            foreach (var arrow in arrows)
                if (!arrow.Flying) return arrow;

            var go = new GameObject("Arrow");
            go.transform.SetParent(transform.parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingOrders.PetProjectile;
            renderer.enabled = false;

            var created = new Arrow { Renderer = renderer };
            arrows.Add(created);
            return created;
        }

        private void LateUpdate()
        {
            for (int i = 0; i < arrows.Count; i++)
            {
                var arrow = arrows[i];
                if (!arrow.Flying) continue;

                // 목표가 비행 중에 죽었으면 가까운 적으로 갈아탄다. 없으면
                // 사라진다 - 비행이 0.2초 안팎이라 다음 타이머 타격이 곧 메운다
                if (arrow.Target == null || !arrow.Target.IsTargetable)
                    arrow.Target = spawner != null
                        ? spawner.FindNearestAlive(arrow.Renderer.transform.position.x, attackRange)
                        : null;

                if (arrow.Target == null)
                {
                    FinishArrow(arrow, null);
                    continue;
                }

                var position = arrow.Renderer.transform.position;
                var goal = arrow.Target.HitPoint;
                var delta = goal - position;

                float step = arrowSpeed * Time.deltaTime;
                if (delta.magnitude <= step)
                {
                    FinishArrow(arrow, arrow.Target);
                    continue;
                }

                position += delta.normalized * step;
                arrow.Renderer.transform.position = position;

                // 화살은 오른쪽을 보고 그려져 있다. 진행 방향으로 눕힌다
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                arrow.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void FinishArrow(Arrow arrow, Enemy target)
        {
            arrow.Flying = false;
            arrow.Renderer.enabled = false;
            arrow.Renderer.transform.rotation = Quaternion.identity;

            if (target != null)
            {
                ApplyHit(target, arrow.Damage);
                PlayArrowHit(target.HitPoint);
            }
        }

        /** 꽂히는 순간의 ARROW HIT. 화살 렌더러를 그 자리에서 원샷으로 돌려 쓴다 */
        private void PlayArrowHit(Vector3 at)
        {
            if (arrowHitFrames == null || arrowHitFrames.Length == 0) return;

            var arrow = GetArrow();
            arrow.Flying = true;   // 재생 동안 풀에서 빌려둔다
            arrow.Renderer.enabled = true;
            arrow.Renderer.transform.position = at;
            arrow.Renderer.transform.rotation = Quaternion.identity;

            StartCoroutine(PlayArrowHitFrames(arrow));
        }

        private System.Collections.IEnumerator PlayArrowHitFrames(Arrow arrow)
        {
            float perFrame = arrowHitFrameRate > 0f ? 1f / arrowHitFrameRate : 0.05f;

            for (int i = 0; i < arrowHitFrames.Length; i++)
            {
                arrow.Renderer.sprite = arrowHitFrames[i];

                // 스케일 타임으로 기다린다. 히트스톱이 걸리면 꽂힘 연출도
                // 함께 멈춰야 한 순간으로 읽힌다
                float waited = 0f;
                while (waited < perFrame)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            arrow.Flying = false;
            arrow.Renderer.enabled = false;
        }
    }
}
