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
        [SerializeField] private Transform vfxParent;
        [SerializeField] private Onikiri.UI.DamageNumberSpawner damageNumbers;

        [Header("애니메이션")]
        [SerializeField] private Sprite[] idleFrames;
        [Tooltip("발도 클립. **참격 궤적이 이 시트에 이미 그려져 있다** - 5번째 " +
                 "프레임의 흰 아크가 그것이고, 별도 참격 이펙트를 두지 않는 이유다")]
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField] private float idleFrameRate = 10f;

        [Tooltip("사거리가 비었을 때 도는 달리기 클립. 사무라이의 X는 고정이고 " +
                 "배경이 흘러 전진을 만든다")]
        [SerializeField] private Sprite[] runFrames;

        [Tooltip("달리기 클립 재생 속도. 16프레임 = 두 걸음이므로 걸음당 = 8/이 값. " +
                 "24fps면 0.333초로 사람 달리기 주기다. 12fps는 정확히 절반이라 " +
                 "슬로모션으로 읽혔다 - 배경 속도가 아니라 이것이 '느리다'의 주범이었다. " +
                 "플레이 중에 끌면 바로 반영된다. 확정된 값은 빌더가 쓴다")]
        [Range(6f, 36f)]
        [SerializeField] private float runFrameRate = 32f;
        [SerializeField] private float attackFrameRate = 14f;

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

        // ---------------------------------------------------------------- 타격 불꽃

        /**
         * @brief 칼이 닿은 자리에서 터지는 작은 불꽃.
         *
         * 22단계까지 여기에는 별도 참격 아크가 있었고, 그것이 "이펙트가 칼 궤적과 어긋난다"의
         * 원인이었다. **참격이 두 번 그려지고 있었다** - 사무라이 시트의 5번째 프레임에는
         * 원화가가 칼에 맞춰 그린 흰 아크가 이미 들어 있는데, 그 위에 팩 아크를 한 장 더
         * 얹고 있었다. 두 호는 모양도 방향도 달라서(팩은 아래로 긋는 세로 베기, 발도는
         * 오른쪽 위) 배치를 어떻게 바꿔도 맞출 수 없었다.
         *
         * 아크는 스프라이트 하나로 줄였다. 남은 것은 스프라이트가 말해주지 못하는 것 -
         * 참격은 사무라이 쪽에 그려져 있으므로 **요괴 쪽에는 아무 일도 일어나지 않는다.**
         * 불꽃이 그 자리를 찍는다. ImpactSpark 참고.
         */
        [SerializeField] private ImpactSpark sparkPrefab;

        [Tooltip("불꽃 프레임. 12px 네 장이다(ImpactSparkBuilder가 굽는다)")]
        [SerializeField] private Sprite[] sparkFrames;

        [SerializeField] private float sparkFrameRate = 30f;

        [Tooltip("플레이 1초당 허용되는 불꽃 표시 시간. 공격속도가 오르면 짧아진다. " +
                 "걷어낸 아크만큼 절박하지는 않다 - 12px라 겹쳐도 화면을 덮지 않는다. " +
                 "그래도 같은 규칙을 따르게 둔다. CombatFeel 참고")]
        [SerializeField] private float sparkBudgetPerSecond = 0.45f;

        [Tooltip("요괴 중심에서 불꽃까지의 보정. x는 요괴가 바라보는 쪽으로 적용된다 " +
                 "(Enemy.FacingDirection) - 칼은 앞면으로 들어온다")]
        [SerializeField] private Vector2 sparkOffset = new Vector2(0.22f, 0f);

        [Tooltip("타격마다 터지는 벚꽃잎. 비어 있어도 전투는 그대로 돈다")]
        [SerializeField] private SakuraBurst sakura;

        [Tooltip("베는 기준 방향. 사무라이는 오른쪽 위로 벤다. 꽃잎이 흩어지는 " +
                 "부채꼴의 중심이다")]
        [SerializeField] private Vector2 slashDirection = new Vector2(1f, 0.45f);

        [Header("풀")]
        [SerializeField] private int sparkPrewarm = 6;

        private ObjectPool<ImpactSpark> sparkPool;

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

        public int SparkPoolGrowthCount { get { return sparkPool != null ? sparkPool.GrowthCount : 0; } }

        /**
         * @brief 요괴 목록. 관통·광역이 대상을 훑는다.
         *
         * 스포너를 직접 내보내지 않고 목록만 내보낸다 - 오의가 스폰을 건드릴
         * 이유가 없고, 참조를 넘기면 언젠가 건드린다.
         */
        public System.Collections.Generic.IReadOnlyList<Enemy> ActiveEnemies
        {
            get
            {
                return spawner != null
                    ? spawner.Active
                    : (System.Collections.Generic.IReadOnlyList<Enemy>)new Enemy[0];
            }
        }

        /**
         * @brief 사거리 안의 가장 가까운 요괴. **평타와 같은 사거리를 쓴다.**
         *
         * 오의가 스스로 찾지 않고 여기를 지나는 이유는 사거리가 두 곳에 적히면
         * 안 되기 때문이다 - 한쪽만 고쳐지는 날 "평타는 때리는데 오의는 안
         * 나가는" 상태가 되고, 그 원인은 화면에서 읽히지 않는다.
         */
        public Enemy FindTarget()
        {
            return spawner != null ? spawner.FindNearestAlive(transform.position.x, attackRange) : null;
        }

        /** 오의가 겨냥할 때 쓰는 사거리. 진단용으로 내보낸다 */
        public float AttackRange { get { return attackRange; } }

        /**
         * @brief 전방 일렬을 벤다. 사거리 안의 살아 있는 요괴 **전부**에 같은 배율.
         *
         * ## 왜 여기로 올라왔는가
         *
         * 45c까지 이 산수는 SkillPerformer 안에 있었다. 손님이 오의 하나뿐이라
         * 그래도 됐는데, 영체(SpiritSummon)가 같은 모양의 타격을 하게 되면서
         * 둘이 됐다 - 그때 복사하면 "무엇이 맞는가"의 정의가 두 곳에 살고,
         * 한쪽만 고쳐지는 날 오의는 도깨비불을 베는데 영체는 지나친다.
         *
         * PlayerCombat이 맞는 자리인 이유는 이미 여기가 **한 대가 무엇을
         * 하는가**의 주인이기 때문이다(DeliverSkillHit 머리 주석). 누가 맞는가도
         * 같은 질문의 일부다. 안무(언제·몇 번)는 여전히 부르는 쪽이 정한다.
         *
         * ## 세로 폭을 함께 보는 이유
         *
         * 떠 있는 도깨비불이다. 그려진 중심이 지면보다 1u 위에 있어서, 세로를
         * 안 보면 관통이 지면의 요괴만 베고 도깨비불은 지나친다 - 화면에서는
         * "가끔 안 맞는다"로만 보인다.
         *
         * @param originX 앞을 재는 기준 x. 뒤로 0.3u까지는 봐준다(발치의 요괴)
         * @param originY 세로 창의 중심. 사무라이의 **그려진** 중심이지 발이 아니다
         * @return 실제로 벤 수
         */
        public int DeliverSkillLane(float originX, float originY, float range, float height,
                                    BigDouble multiplier, Color tint, int numberSizeMultiple)
        {
            int hits = 0;
            var enemies = ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsTargetable) continue;

                var point = enemy.HitPoint;
                float dx = point.x - originX;
                if (dx < -0.3f || dx > range) continue;
                if (Mathf.Abs(point.y - originY) > height * 0.5f) continue;

                if (DeliverSkillHit(enemy, multiplier, tint, numberSizeMultiple)) hits++;
            }
            return hits;
        }

        /** 화면 광역. 살아 있는 요괴 전부. 관통과 같은 이유로 여기 있다 */
        public int DeliverSkillAll(BigDouble multiplier, Color tint, int numberSizeMultiple)
        {
            int hits = 0;
            var enemies = ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsTargetable) continue;

                if (DeliverSkillHit(enemy, multiplier, tint, numberSizeMultiple)) hits++;
            }
            return hits;
        }

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
         * @brief 치명타 확률. 10단계부터 강화 대상이다.
         *
         * 0~1로 자른다. 곡선의 상한은 UpgradeTrack의 valueCeiling이 걸지만,
         * 스탯을 밀어넣는 경로가 강화 하나뿐이라는 보장은 없다(테스트 패널,
         * 세이브 복원). 값이 1을 넘으면 모든 타격이 치명타가 되어 강조가 무의미해진다.
         */
        public float CritChance
        {
            get { return critChance; }
            set { critChance = Mathf.Clamp01(value); }
        }

        /** 치명타 배수. 상한이 없는 축이라 아래로만 막는다 */
        public float CritMultiplier
        {
            get { return critMultiplier; }
            set { critMultiplier = Mathf.Max(1f, value); }
        }

        /**
         * @brief 초월 치명타 배수 (43단계 심화 축). 피해 전체에 곱해진다.
         *
         * 기본 1이 중요하다 - 심화 축이 잠긴(또는 없던 시절의) 세이브가
         * 그대로 예전 피해를 낸다. 상한이 없는 축이라 아래로만 막는다.
         */
        public float TranscendMultiplier
        {
            get { return transcendMultiplier; }
            set { transcendMultiplier = Mathf.Max(1f, value); }
        }

        /**
         * @brief 연격 확률 (43단계 심화 축). 타격마다 이 확률로 한 번 더 벤다.
         *
         * 추가타는 온전한 한 타다 - 치명타·초월을 그대로 상속하고, 자기
         * 숫자가 따로 뜬다. 0~1로 자르는 것은 치명타 확률과 같은 이중
         * 안전장치다.
         */
        public float ComboChance
        {
            get { return comboChance; }
            set { comboChance = Mathf.Clamp01(value); }
        }

        private float transcendMultiplier = 1f;
        private float comboChance;

        /**
         * @brief 자동 공격 + 오의를 합친 **초당 환산 공격 횟수**.
         *
         * 오의 한 번은 공격력 x 배율이고 평타 한 대는 공격력 x1이므로, 배율을
         * 쿨다운으로 나누면 같은 단위가 되어 더할 수 있다. 시뮬레이션의
         * CombatStats가 쓰는 것과 **같은 정의**여야 밸런스 판정이 화면과 갈리지
         * 않는다.
         *
         * SkillSystem이 없으면 0이 더해진다. 전투 전용 테스트 씬은 스킬 없이
         * 스포너만 세우는데, 거기서 값이 사라지면 26단계 이전 검사가 이유 없이
         * 깨진다 - UpgradeSystem.CurrentGoldGain이 1로 떨어지는 것과 같은 규칙이다.
         */
        public float EffectiveAttacksPerSecond
        {
            get
            {
                // 45단계의 영체도 같은 괄호 안이다. 소환 한 번이 공격력 x 배율
                // 뭉치이므로 쿨다운으로 나누면 오의와 같은 단위가 되고, 그래서
                // 더할 수 있다(CombatStats.SpiritRate).
                //
                // 빠뜨리면 **방치 보상이 시뮬레이션보다 가난해진다** - 화면의
                // DPS도 실제보다 낮게 뜬다. YodoSystem이 없으면 0이라 전투 전용
                // 테스트 씬은 예전 값 그대로다
                return attacksPerSecond
                     + (float)Onikiri.Progression.SkillSystem.CurrentCastRate
                     + (float)Onikiri.Progression.YodoSystem.CurrentSpiritRate;
            }
        }

        /** 치명타 기대값을 포함한 초당 피해. 테스트 패널과 방치 보상이 쓴다 */
        public BigDouble ExpectedDps
        {
            get
            {
                // 심화 축(초월·연격)까지 곱한다. CombatStats.ExpectedDps와 같은
                // 식이어야 한다 - 두 구현이 갈리면 방치 보상이 시뮬레이션과
                // 다른 플레이어를 잰다
                float factor = 1f + critChance * (critMultiplier - 1f);
                float mastery = transcendMultiplier * (1f + comboChance);
                return damage * BigDouble.FromDouble(EffectiveAttacksPerSecond * factor * mastery);
            }
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
            if (sparkPrefab != null) sparkPool = new ObjectPool<ImpactSpark>(sparkPrefab, vfxParent, sparkPrewarm);

            // 런지의 기준점. 사무라이의 X는 고정이 규칙이고(17단계 - 배경이 흘러
            // 전진을 만든다), 일섬의 돌진은 그 규칙을 깨는 것이 아니라 **잠깐
            // 튀어나갔다 돌아오는 것**이다. 기준을 여기서 한 번 기억해두면
            // 시전이 중간에 끊겨도 오프셋을 0으로 되돌리는 것만으로 복구된다
            baseLocalX = transform.localPosition.x;

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

            HasTargetInRange = target != null;

            if (target == null)
            {
                // 벨 것이 사라지면 스윙 상태도 함께 끝난다.
                //
                // 이 줄이 없으면 플래그가 참인 채로 남는다 - 아래 리셋은 타격이
                // 들어가는 순간에만 도는데, 대상이 없으면 거기까지 가지 못하기
                // 때문이다. 보스전에 들어가면서 큐가 비는 순간이 정확히 그렇다.
                //
                // 낡은 플래그는 두 가지를 망가뜨린다. 새 요괴가 들어와도 예비
                // 동작 없이 타격만 나가고(아래 !swingStarted 조건), 달리기 전환이
                // 거절당한다(RefreshRestingClip). 후자가 "보스에게 idle로
                // 달려가는" 버그였다
                swingStarted = false;

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

            // 오의 클립이 도는 동안에는 평타 스윙을 걸지 않는다. 걸면 오의 동작이
            // 첫 프레임으로 잘리고, 그 사고는 오의와 평타의 주기가 맞물릴 때만
            // 나타나서 간헐적으로 보인다(26단계 메모). 데미지는 그대로 들어간다 -
            // 12단계 규칙대로 타이머가 결정하고 애니메이션은 장식이다
            if (!swingStarted && target != null && attackTimer >= interval - lead && !IsPlayingOneShot)
            {
                PlaySwing(swingDuration);
                swingStarted = true;
            }
        }

        /**
         * @brief 런지 오프셋을 적용한다. **LateUpdate다.**
         *
         * BattleStageLayout이 LateUpdate에서 지면 앵커의 Y를 다시 놓는데, 그것은
         * 부모라서 이쪽의 로컬 X와 다투지 않는다. 그래도 같은 단계에 두는 이유는
         * 순서를 한 곳에서 읽을 수 있게 하기 위함이다.
         */
        private void LateUpdate()
        {
            var local = transform.localPosition;
            float wanted = baseLocalX + lungeOffsetX;
            if (Mathf.Approximately(local.x, wanted)) return;

            local.x = wanted;
            transform.localPosition = local;
        }

        /**
         * @brief 데미지가 들어가는 순간. 불꽃·꽃잎·숫자·소리·정지가 전부 여기서 함께 난다.
         *
         * **"같은 프레임에 터진다"가 이 함수의 존재 이유다.** 애님 이벤트를 쓰지 않는
         * 것도 같은 이유다 - 이벤트로 흩으면 다섯 연출이 각자의 조건으로 나게 되고,
         * 하나가 한 프레임 밀리면 타격이 두 번 일어난 것처럼 보인다. 여기서는 밀릴
         * 방법이 없다. 애니메이션 쪽은 반대로 맞춘다: 스윙을 lead 시간만큼 먼저
         * 시작시켜서, 참격이 그려진 5번째 프레임이 이 함수가 도는 순간에 화면에 있다.
         */
        private void DeliverHit(Enemy target)
        {
            if (target == null || !target.IsTargetable) return;

            AttackCount++;

            bool crit = critChance > 0f && Random.value < critChance;
            BigDouble dealt = crit
                ? damage * BigDouble.FromDouble(critMultiplier)
                : damage;

            // 초월 치명타(43단계). 치명타 여부와 무관하게 피해 전체에 곱해진다
            if (transcendMultiplier > 1f) dealt = dealt * BigDouble.FromDouble(transcendMultiplier);

            // transform이 아니라 그려진 스프라이트의 중심을 겨냥한다. Enemy.HitPoint 참고
            var hitPoint = target.HitPoint;

            // 참격 아크는 여기서 내지 않는다. **사무라이 스프라이트에 이미 그려져 있다.**
            // 이 자리에서 나는 것은 맞은 지점의 작은 불꽃뿐이다
            SpawnSpark(target, hitPoint);

            // 꽃잎은 베인 대상에서 떨어져 나가는 것이므로 앞면 보정 없이 요괴가 그려진
            // 자리 한가운데에서 나온다
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

            // 연격(43단계). 같은 프레임에 한 번 더 벤다 - 온전한 한 타라 같은
            // 피해에 자기 숫자·불꽃이 따로 난다. 정지·흔들림은 다시 내지
            // 않는다(한 프레임에 두 번 내면 예산 계산이 두 배로 샌다).
            // 첫 타로 죽었으면 벨 것이 없다
            if (!killed && comboChance > 0f && Random.value < comboChance)
            {
                SpawnSpark(target, hitPoint);
                target.TakeDamage(dealt);

                if (damageNumbers != null)
                    damageNumbers.Show(dealt, hitPoint,
                        !target.IsAlive ? Onikiri.UI.DamageStyle.Kill
                            : crit ? Onikiri.UI.DamageStyle.Critical
                                   : Onikiri.UI.DamageStyle.Normal,
                        target);

                if (hitAudio != null)
                {
                    if (!target.IsAlive) hitAudio.PlayKill();
                    else hitAudio.PlayHit();
                }
            }
        }

        // ---------------------------------------------------------------- 오의

        /**
         * @brief 오의 **한 대**. 시전 한 번이 아니라 타격 한 번이다.
         *
         * 27단계에 갈라졌다. 26단계에는 `CastSkill` 하나가 "겨냥 + 한 방 + 정지 +
         * 흔들림"을 전부 했는데, 오의마다 타격을 시간(다타)과 공간(관통·광역)에
         * 펴게 되면서 그 셋의 주기가 서로 달라졌다 - 연참은 타격이 셋인데 정지는
         * 마지막에 한 번이고, 귀참은 타격이 여럿인데 화면 정지는 한 번이다.
         *
         * 그래서 경계를 다시 그었다:
         *
         *   PlayerCombat    **한 대가 무엇을 하는가** (피해·불꽃·꽃잎·숫자·소리)
         *   SkillPerformer  **한 시전이 그 대를 어떻게 뿌리는가** (안무·정지·흔들림)
         *
         * 12단계의 "한 함수 안에서 전부 난다"는 규칙은 그대로다 - 한 대의 다섯
         * 연출은 여기서 같은 프레임에 나고, 애님 이벤트로 흩지 않는다.
         *
         * @param multiplier        이 **한 대**의 배율. 다타면 이미 나눠진 값이다
         * @param numberSizeMultiple 데미지 숫자의 크기 배수. 정수만 (래스터 폰트)
         * @return 실제로 벤 것이 있으면 true
         */
        public bool DeliverSkillHit(Enemy target, BigDouble multiplier, Color tint,
                                    int numberSizeMultiple)
        {
            if (target == null || !target.IsTargetable) return false;

            bool crit = critChance > 0f && Random.value < critChance;

            // 치명타와 스탯 포인트 증폭이 둘 다 상속된다. 증폭은 damage에 이미
            // 들어 있고(UpgradeSystem.Apply), 치명타는 여기서 같은 규칙으로
            // 굴린다 - 오의만 다른 확률을 쓰면 시뮬레이션의 기대값과 갈린다
            BigDouble dealt = damage * multiplier;
            if (crit) dealt = dealt * BigDouble.FromDouble(critMultiplier);

            // 초월 치명타(43단계). 평타와 같은 자리, 같은 이유 - 오의만 빠지면
            // 시뮬레이션의 기대값(DPS 전체에 곱함)과 갈린다
            if (transcendMultiplier > 1f) dealt = dealt * BigDouble.FromDouble(transcendMultiplier);

            var hitPoint = target.HitPoint;

            // 불꽃은 평타와 같은 것을 쓴다. 오의 전용 불꽃을 만들지 않는 이유는
            // 불꽃이 말하는 것이 "여기 맞았다" 하나이고, 그것은 평타든 오의든
            // 같은 사실이기 때문이다. 오의라는 사실은 아크·이름·정지가 말한다
            SpawnSpark(target, hitPoint);
            if (sakura != null) sakura.Play(hitPoint, slashDirection, attacksPerSecond);

            target.TakeDamage(dealt);
            bool killed = !target.IsAlive;

            // 오의 숫자는 **오의 색**으로 뜬다. 치명타(금색)와 같은 단계를 쓰면
            // 치명타율 60% 구간에서 화면의 큰 숫자 대부분이 이미 금색이라
            // "무엇이 나갔는지"가 색으로 읽히지 않는다 - 26단계 소감의 절반이
            // 이것이었다
            if (damageNumbers != null)
                damageNumbers.ShowSkill(dealt, hitPoint, tint, numberSizeMultiple);

            if (hitAudio != null)
            {
                if (killed) hitAudio.PlayKill();
                else hitAudio.PlayHit();
            }

            // 연격(43단계). 오의의 한 대에도 같은 확률로 한 번 더 - 평타만
            // 빼거나 오의만 빼면 기대값 식(모든 타격 x (1+p))과 갈린다
            if (!killed && comboChance > 0f && Random.value < comboChance)
            {
                SpawnSpark(target, hitPoint);
                target.TakeDamage(dealt);

                if (damageNumbers != null)
                    damageNumbers.ShowSkill(dealt, hitPoint, tint, numberSizeMultiple);

                if (hitAudio != null)
                {
                    if (!target.IsAlive) hitAudio.PlayKill();
                    else hitAudio.PlayHit();
                }
            }

            return true;
        }

        /**
         * @brief 오의의 무게. 정지와 흔들림을 **시전당 한 번** 낸다.
         *
         * 평타의 예산 계산 결과에 배수를 곱한다. 예산 규칙(CombatFeel)을 깨지
         * 않는 이유는 시전 빈도다 - 7~22초에 한 번 0.15초를 멈춰도 초당 0.02초로,
         * 평타 예산 0.3초/초의 7%다. 같은 잣대로 재면 오의는 애초에 예산을 다
         * 쓰지 않는다.
         *
         * 고정 길이로 두지 않는 이유는 비율이다. 공격속도가 오를수록 평타 정지는
         * 짧아지는데 오의만 그대로면, 후반에 오의 하나가 화면을 통째로 세우는
         * 것처럼 보인다.
         */
        public void SkillFeedback(float hitStopMultiplier, float shakeMultiplier)
        {
            if (hitStopMultiplier > 0f)
                HitStop.Request(CombatFeel.ScaledDuration(
                    hitStopSeconds, hitStopBudgetPerSecond, attacksPerSecond) * hitStopMultiplier);

            if (shakeMultiplier > 0f)
                ScreenShake.Request(cameraShake,
                    CombatFeel.ScaledDuration(shakeSeconds, shakeBudgetPerSecond, attacksPerSecond)
                        * shakeMultiplier,
                    shakePixels * shakeMultiplier);
        }

        // ---------------------------------------------------------------- 오의 클립 / 런지

        /**
         * @brief 오의 전용 클립을 재생한다. 끝나면 스스로 쉬는 동작으로 돌아간다.
         *
         * 애니메이터를 밖으로 내보내지 않고 이 함수를 지나게 하는 이유는 26단계
         * 메모다 - **진행 중인 클립을 다시 재생하면 첫 프레임으로 튄다.** 재생
         * 주체가 둘이면 그 사고가 간헐적으로만 나타나고, 그때는 원인을 찾을 수
         * 없다. 여기 하나만 애니메이터를 만진다.
         *
         * 스윙 타이머는 건드리지 않는다. 평타는 계속 자기 주기로 데미지를 넣고
         * 있고(12단계: 데미지는 타이머가 결정하고 애니메이션은 장식이다), 오의
         * 클립은 그 장식을 잠깐 다른 것으로 바꿀 뿐이다.
         */
        public void PlaySkillClip(Sprite[] frames, float framesPerSecond)
        {
            if (animator == null || frames == null || frames.Length == 0) return;
            animator.Play(frames, framesPerSecond, false, PlayIdle);
        }

        /** 오의 클립이 도는 동안 평타 스윙이 끼어들지 않게 막는다 */
        public bool IsPlayingOneShot { get { return animator != null && animator.IsOneShot; } }

        /**
         * @brief 전직이 사무라이의 클립 세트를 통째로 바꾼다 (33단계).
         *
         * EvolutionAppearance만 부른다. 애니메이터를 직접 내보내지 않는 이유는
         * PlaySkillClip과 같다 - 재생 주체가 둘이면 "진행 중인 클립을 다시
         * 재생하면 첫 프레임으로 튄다"는 사고가 간헐적으로만 나타난다.
         *
         * **measuredAttackDuration은 되재지 않는다.** 공격속도 상한
         * (MaxAttacksPerSecond -> AttackSpeedCurve.MaxLevel)이 그 값에서
         * 유도되는데, 팩마다 발도 프레임 수가 달라서 진화할 때마다 상한이
         * 움직이면 강화 트랙의 maxLevel과 어긋난다. 상한은 기본 팩(로닌)의
         * 클립 길이에 앵커하고, 새 클립은 그 길이 안에서 재생 속도로 맞춘다 -
         * PlaySwing이 원래 그렇게 돈다(프레임 수 / 길이 = 재생 속도).
         *
         * 도는 중인 루프 클립(idle/달리기)은 즉시 갈아탄다. 스윙·오의 같은
         * 원샷은 끝나면 PlayIdle이 새 프레임으로 돌아오므로 건드리지 않는다.
         */
        public void SetCharacterFrames(Sprite[] idle, Sprite[] attack, Sprite[] run)
        {
            if (idle != null && idle.Length > 0) idleFrames = idle;
            if (attack != null && attack.Length > 0) attackFrames = attack;
            if (run != null && run.Length > 0) runFrames = run;

            if (animator != null && !animator.IsOneShot) PlayIdle();
        }

        /** 사무라이의 기준 X. Awake에서 한 번 기억한다 */
        private float baseLocalX;

        private float lungeOffsetX;

        /**
         * @brief 기준 X에서 앞으로 밀려난 거리. 일섬의 돌진이 쓴다.
         *
         * **매 프레임 기준점에서 다시 놓는다.** 누적으로 움직이면 시전이 중간에
         * 끊길 때(요괴가 사라짐, 씬 리로드, 사망) 사무라이가 밀려난 자리에 남고,
         * 그 상태는 "사무라이 위치가 이상하다"로만 나타나서 원인이 안 읽힌다.
         * 0으로 되돌리는 것만으로 복구되는 편이 낫다.
         *
         * 17단계의 "사무라이 X는 고정"이라는 규칙을 깨는 것이 아니다. 고정은
         * **전진을 배경이 만든다**는 뜻이고, 돌진은 잠깐 튀어나갔다 돌아오는
         * 것이라 평상 상태의 X가 그대로 남는다.
         */
        public float LungeOffsetX
        {
            get { return lungeOffsetX; }
            set { lungeOffsetX = value; }
        }

        /** 장식용 스윙. 끝나면 스스로 idle로 돌아간다 */
        private void PlaySwing(float duration)
        {
            if (attackFrames == null || attackFrames.Length == 0) return;

            float rate = attackFrames.Length / Mathf.Max(0.0001f, duration);
            animator.Play(attackFrames, rate, false, PlayIdle);
        }

        /**
         * @brief 참격이 지나가는 방향 (도).
         *
         * slashDirection 하나에서 나온다. 꽃잎이 흩어지는 방향과 이펙트가 눕는 방향이
         * 같은 값에서 나와야 한 번의 베기로 읽힌다 - 예전에는 꽃잎만 이 방향을 쓰고
         * 이펙트는 0도 부근의 무작위였다.
         *
         * 사무라이 아트에서 잰 값과도 맞는다. ATTACK 1의 5번째 프레임(임팩트)에서
         * 흰 궤적이 피벗 기준 오른쪽 1.375u, 위로 0.969u까지 뻗어 있어 약 24도다.
         */
        /**
         * @brief 요괴가 맞은 자리에 불꽃을 터뜨린다.
         *
         * 위치는 요괴의 그려진 중심에서 **바라보는 쪽으로** 조금 나간 지점이다. 칼은
         * 앞면으로 들어오므로 불꽃도 앞면에 찍혀야 한다. 방향은 하드코딩하지 않고
         * {@link Enemy.FacingDirection}이 `artFacesLeft`에서 끌어온다.
         *
         * 요괴 스프라이트의 가장자리(bounds.min.x)를 안 쓴다. 그 값은 그려진 몸이 아니라
         * **투명 여백까지 포함한 프레임 경계**이고, Aseprite 임포터가 프레임마다 다르게
         * 잘라내서 같은 팩 안에서도 반너비가 0.27u에서 1.22u까지 흔들린다. 넓게 잘린
         * 프레임에서는 '가장자리'가 사무라이 발밑까지 왔다 - 실제로 그렇게 나왔고,
         * 이펙트가 요괴가 아니라 사무라이를 덮었다. 중심 + 고정 보정이 흔들리지 않는다.
         */
        private void SpawnSpark(Enemy target, Vector3 hitPoint)
        {
            if (sparkPool == null || sparkFrames == null || sparkFrames.Length == 0) return;

            int facing = target.FacingDirection;

            var at = new Vector3(hitPoint.x + sparkOffset.x * facing,
                                 hitPoint.y + sparkOffset.y,
                                 0f);

            // 불꽃도 히트스톱·흔들림과 같은 예산 규칙을 따른다. 스윙 압축으로 공격속도가
            // 두 자릿수까지 올라가므로, 고정 재생 속도를 유지하면 초당 열 번 구간에서
            // 불꽃이 끊이지 않고 켜져 있어 '터진다'가 아니라 '켜져 있다'로 읽힌다.
            //
            // 프레임 수를 줄이지 않고 재생 속도만 올린다. 형태는 그대로 두고 화면에
            // 머무는 시간만 줄이는 쪽이 픽셀 아트에서 훨씬 덜 티가 난다.
            SpawnSparkAt(at, facing < 0);
        }

        /**
         * @brief 불꽃 하나를 **자리를 지정해** 터뜨린다. 풀은 평타 것을 그대로 쓴다.
         *
         * 요괴가 아니라 좌표를 받는 것이 SpawnSpark와의 차이다. 영체의 강림
         * (SpiritSummon)이 찍고 싶은 자리는 피격점이 아니라 **소환 지점**이라,
         * 대상에서 자리를 끌어올 수가 없다.
         *
         * 재생 속도의 예산 규칙은 그대로 탄다. 공격속도가 두 자릿수인 구간에서
         * 이것만 고정 속도로 두면 영체 불꽃 하나가 화면에 계속 켜져 있다.
         */
        public void SpawnSparkAt(Vector3 position, bool mirror)
        {
            if (sparkPool == null || sparkFrames == null || sparkFrames.Length == 0) return;

            // 불꽃도 히트스톱·흔들림과 같은 예산 규칙을 따른다. 스윙 압축으로 공격속도가
            // 두 자릿수까지 올라가므로, 고정 재생 속도를 유지하면 초당 열 번 구간에서
            // 불꽃이 끊이지 않고 켜져 있어 '터진다'가 아니라 '켜져 있다'로 읽힌다.
            //
            // 프레임 수를 줄이지 않고 재생 속도만 올린다. 형태는 그대로 두고 화면에
            // 머무는 시간만 줄이는 쪽이 픽셀 아트에서 훨씬 덜 티가 난다.
            float baseDuration = sparkFrames.Length / Mathf.Max(0.0001f, sparkFrameRate);
            float duration = CombatFeel.ScaledDuration(baseDuration, sparkBudgetPerSecond, attacksPerSecond);
            float rate = sparkFrames.Length / Mathf.Max(0.0001f, duration);

            var spark = sparkPool.Get();
            spark.Play(sparkFrames, rate, position, mirror, ReleaseSpark);
        }

        private void ReleaseSpark(ImpactSpark spark)
        {
            sparkPool.Release(spark);
        }

        /**
         * @brief 벨 것이 없을 때의 동작. 달리는 중이면 달리기, 아니면 idle.
         *
         * 17단계에서 갈렸다. 그전에는 언제나 idle이었고, 화면은 사무라이가
         * 제자리에 선 채 요괴가 걸어오기를 기다리는 그림이었다 - "나아가는
         * 느낌이 없다"는 소감의 원인이 정확히 이것이다.
         *
         * 이제 사거리가 비면 달린다. 사무라이의 X는 그대로고 배경이 흐르므로,
         * 달리기 클립과 배경 스크롤이 같은 사실의 앞뒤다.
         *
         * 스크롤 여부를 {@link ParallaxScroller}에서 읽는 이유는 그것이 이미
         * 모든 레이어가 공유하는 단일 상태이기 때문이다. 여기서 따로 판단하면
         * "배경은 흐르는데 사무라이는 서 있는" 조합이 생긴다.
         */
        private void PlayIdle()
        {
            bool advancing = ParallaxScroller.IsScrolling;

            if (advancing && runFrames != null && runFrames.Length > 0)
            {
                animator.Play(runFrames, runFrameRate, true);
                return;
            }

            if (idleFrames != null && idleFrames.Length > 0)
                animator.Play(idleFrames, idleFrameRate, true);
        }

        /**
         * @brief 사거리에 벨 것이 있는가.
         *
         * 전진 판단이 이 값 하나에 달려 있다. 여기서 내보내는 이유는 대상을
         * 찾는 코드가 이미 여기 있기 때문이다 - 밖에서 다시 찾으면 사거리가
         * 두 곳에 적히고, 한쪽만 고쳐지는 날 "때리는데 안 멈추는" 상태가 된다.
         */
        public bool HasTargetInRange { get; private set; }

        /**
         * @brief 쉬는 동작을 다시 고른다.
         *
         * 달리기와 idle이 바뀌는 순간에 불린다. 스윙 도중이면 건드리지 않는다 -
         * 동작 중에 클립을 갈면 벤 자세가 끊긴다.
         *
         * ## 왜 성공 여부를 돌려주는가
         *
         * 거절할 수 있는 호출이기 때문이다. 부르는 쪽(StageAdvance)은 스크롤
         * 상태가 **바뀌는 순간에만** 부르는데, 그 순간에 거절당하면 다시 부를
         * 기회가 영영 오지 않는다 - 상태는 이미 바뀌었으므로 다음 프레임에는
         * 전환이 감지되지 않는다.
         *
         * "보스 도전을 눌렀는데 사무라이가 idle 자세로 달려가는" 버그가 이것이었다.
         * 도전 순간에 마침 스윙 중이면 달리기 전환이 통째로 유실됐고, 스윙이
         * 걸리는 타이밍이라 **간헐적으로만** 나타났다.
         *
         * @return 클립을 실제로 다시 골랐으면 true. false면 부른 쪽이 다시 시도해야 한다
         */
        public bool RefreshRestingClip()
        {
            // 한 번 재생 클립(스윙)이 도는 중에만 거절한다. 예전에는
            // `IsPlaying && swingStarted`로 판정했는데, idle도 IsPlaying이 true라
            // swingStarted가 낡은 값으로 남으면 영원히 거절하는 상태가 됐다
            if (animator != null && animator.IsOneShot) return false;

            PlayIdle();
            return true;
        }

        /**
         * @brief 인스펙터에서 값을 끄는 동안 달리기 속도를 즉시 반영한다.
         *
         * 속도감은 숫자로 판정할 수 있는 것이 아니라 눈으로 봐야 하는 것이고,
         * 눈으로 보려면 끄는 즉시 화면이 따라와야 한다. 클립을 다시 걸지 않고
         * 속도만 갈아끼우므로 동작이 첫 프레임으로 튀지 않는다.
         *
         * 여기서 정한 값은 **플레이를 나가면 사라진다.** 확정되면 빌더에
         * 적어야 씬에 남는다(BattleContentBuilder) - 직렬화 값이 코드 기본값을
         * 이기는 자리라 코드만 고치면 조용히 무시된다.
         */
        private void OnValidate()
        {
            if (!Application.isPlaying || animator == null) return;
            if (ParallaxScroller.IsScrolling) animator.SetFrameRate(runFrameRate);
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
