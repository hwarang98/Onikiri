using System.Collections.Generic;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 화면 밖 오른쪽에서 요괴가 끊기지 않고 걸어 들어오게 유지한다.
     *
     * 세로 화면의 전투 영역은 가로 6.75 world units뿐이라, 사양서는 읽히는 한계를
     * 동시 3~5마리로 잡았다. 타이머로 웨이브를 쏟아붓고 기대하는 대신 목표 생존 수를
     * 유지하는 방식이라, 화면이 비지도 붐비지도 않는다.
     *
     * 큐 위치는 매 프레임 살아 있는 적의 순서로 다시 계산한다. 그래서 적들이 같은
     * 자리에 겹치지 않고 뒤로 줄을 서며, 앞의 적이 죽으면 큐가 자동으로 당겨진다.
     */
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private BattleStageLayout stage;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnemyDefinition[] definitions;
        [SerializeField] private Transform enemyParent;

        [Tooltip("경험치 흡수 연출. 처치 지점이 여기에서만 알 수 있으므로 " +
                 "타격 팝업(PlayerCombat)과 달리 스포너가 들고 있다")]
        [SerializeField] private Onikiri.UI.DamageNumberSpawner damageNumbers;

        [Header("필드")]
        [Tooltip("동시에 살아 있어야 할 요괴 수. 사양서는 화면에 3~5마리를 요구한다")]
        [SerializeField] private int targetAlive = 4;

        [Tooltip("보충 사이의 간격 (초). 성장하지 않은 상태의 시작값이며, " +
                 "필드가 굶으면 SpawnPacing 규칙에 따라 줄어든다")]
        [SerializeField] private float spawnInterval = 1.1f;

        /**
         * @brief 지금 실제로 쓰고 있는 보충 간격.
         *
         * spawnInterval은 시작값이고 이쪽이 살아 움직인다. 둘을 나눈 이유는
         * 인스펙터의 값이 런타임에 조용히 바뀌면 씬을 저장할 때 그 값이 굳어버리기
         * 때문이다 - 다음 실행이 0.4초에서 시작하게 된다.
         */
        private float currentInterval;

        [Tooltip("화면 오른쪽 끝에서 얼마나 바깥에 나타날지 (world units)")]
        [SerializeField] private float offscreenMargin = 1.2f;

        [Header("큐")]
        [Tooltip("선두 적이 멈추는 월드 X. 플레이어 사거리 안쪽")]
        [SerializeField] private float frontLineX = -0.9f;

        [Header("풀")]
        [SerializeField] private int prewarm = 8;

        private ObjectPool<Enemy> pool;
        private readonly List<Enemy> active = new List<Enemy>();
        private float spawnTimer;

        /**
         * @brief 보스전 동안 잡몹 보충을 멈춘다.
         *
         * 보스만 남기지 않으면 세로 화면의 좁은 큐에서 보스가 잡몹 뒤에 서게 되고,
         * 사무라이의 사거리에는 앞줄만 들어오므로 제한 시간이 흐르는 동안 정작 보스는
         * 맞지 않는다.
         */
        private bool spawningSuspended;

        /** 살아 있는 적. 플레이어에 가까운 순 */
        public IReadOnlyList<Enemy> Active { get { return active; } }

        public bool SpawningSuspended { get { return spawningSuspended; } }

        public int PoolGrowthCount { get { return pool != null ? pool.GrowthCount : 0; } }

        /**
         * @brief 방치 보상이 요괴 공급 상한을 계산할 때 쓴다.
         *
         * 인스펙터의 시작값이 아니라 **지금 쓰고 있는 간격**을 준다. 10단계부터
         * 간격이 처치 속도에 따라 좁아지므로, 시작값을 쓰면 성장한 플레이어의
         * 방치 수입이 실제보다 최대 세 배 적게 계산된다.
         */
        public float SpawnInterval
        {
            get { return currentInterval > 0f ? currentInterval : spawnInterval; }
        }

        /**
         * @brief 스폰 가중치로 평균 낸 1스테이지 기준 체력과 골드.
         *
         * 방치 보상은 "평균적인 요괴 한 마리"를 기준으로 계산한다. 가중치를 무시하고
         * 단순 평균을 내면 가중치 1짜리 정예가 가중치 5짜리 잡몹과 같은 비중을 갖게
         * 되어, 실제보다 후한 보상이 나온다.
         */
        public BigDouble AverageBaseHealth { get { return WeightedAverage(true); } }
        public BigDouble AverageBaseGold { get { return WeightedAverage(false); } }

        private BigDouble WeightedAverage(bool health)
        {
            if (definitions == null || definitions.Length == 0) return BigDouble.Zero;

            BigDouble sum = BigDouble.Zero;
            float totalWeight = 0f;

            foreach (var definition in definitions)
            {
                if (definition == null) continue;
                float weight = Mathf.Max(0f, definition.spawnWeight);
                if (weight <= 0f) continue;

                sum += (health ? definition.maxHealth : definition.goldReward) * BigDouble.FromDouble(weight);
                totalWeight += weight;
            }

            return totalWeight > 0f ? sum / BigDouble.FromDouble(totalWeight) : BigDouble.Zero;
        }

        private void Awake()
        {
            if (enemyParent == null) enemyParent = transform;
            pool = new ObjectPool<Enemy>(enemyPrefab, enemyParent, prewarm);
            currentInterval = spawnInterval;
        }

        private void Update()
        {
            UpdateQueuePositions();

            if (spawningSuspended) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;

            // 보충 시점에 필드가 목표에 못 미치면 굶고 있다는 뜻이다 - 요괴가
            // 들어오는 것보다 빨리 죽는다. 그때 간격을 좁힌다. 반대로 목표를
            // 채우고 있으면 쌓이고 있으므로 되돌린다. SpawnPacing 참고.
            //
            // 이 되먹임이 9단계의 병목을 푼다. 예전에는 간격이 1.1초 고정이라
            // 공격력을 아무리 올려도 잡몹 파밍이 10 x 1.1초에 묶여 있었다
            bool starved = CountAlive() < targetAlive;
            currentInterval = SpawnPacing.Next(currentInterval, starved);

            if (starved) Spawn();

            spawnTimer = currentInterval;
        }

        /** 지금 간격. 테스트 패널이 병목이 풀렸는지 볼 때 쓴다 */
        public float CurrentSpawnInterval { get { return currentInterval; } }

        // ---------------------------------------------------------------- 보스전 제어

        public void SuspendSpawning()
        {
            spawningSuspended = true;
        }

        /**
         * @brief 잡몹 보충을 다시 켠다.
         *
         * 타이머를 0으로 두어 곧바로 한 마리가 들어오게 한다. spawnInterval을 기다리면
         * 보스전이 끝난 직후 화면이 1초 넘게 비고, 그 정적이 실패 화면 뒤에 붙으면
         * 게임이 멈춘 것처럼 읽힌다.
         */
        public void ResumeSpawning()
        {
            spawningSuspended = false;
            spawnTimer = 0f;

            // 간격도 시작값으로 되돌린다. 보스전 동안 필드가 비어 있었으므로
            // 되먹임 입장에서는 계속 굶은 상태였고, 그대로 두면 파밍 복귀 직후
            // 하한(0.4초)에서 시작해 요괴 넷이 한꺼번에 쏟아진다
            currentInterval = spawnInterval;
        }

        /**
         * @brief 필드의 잡몹을 보상 없이 치운다.
         *
         * 보스가 들어올 자리를 비우는 용도다. TakeDamage로 죽이지 않는 이유는 그러면
         * 골드가 지급되고 처치 수가 오르기 때문이다. 보스에 도전할 때마다 화면의
         * 네 마리가 공짜 골드로 바뀌면, 도전 버튼을 반복해서 누르는 것이 최적 전략이 된다.
         */
        public void ClearField()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var enemy = active[i];
                if (enemy == null) continue;

                enemy.Killed -= OnEnemyKilled;
                enemy.Died -= OnEnemyDied;
                enemy.Deactivate();
                pool.Release(enemy);
            }
            active.Clear();
        }

        /**
         * @brief 보스를 필드에 세운다. 체력과 보상은 호출부가 확정해서 넘긴다.
         *
         * 잡몹과 같은 Enemy이고 같은 풀에서 나온다. 다른 것은 정의 에셋, 스탯,
         * 그리고 정렬 순서뿐이다.
         */
        public Enemy SpawnBoss(EnemyDefinition definition, BigDouble health, BigDouble gold,
                               BigDouble exp, float scale, Color tint, double attackDamage, bool walkIn,
                               float approachDistance = 0f)
        {
            if (definition == null) return null;

            var boss = pool.Get();

            // 챕터 보스만 화면 밖에서 걸어 들어온다. 일반 스테이지 보스는 큐 앞줄에
            // 바로 선다 - 워크인 5.3초가 매 스테이지 반복되면 제한 시간의 18%가
            // 기다림으로 사라진다. BossCurve 참고
            // 달려가기 거리를 호출부가 정한다. 17단계에서 플레이어가 보스에게
            // 달려가게 되면서 접근 속도가 (보스 걸음 + 스크롤)로 올라갔고,
            // 화면 밖 여백만큼만 띄우면 1초 만에 도착해 관문을 지날 틈이 없다
            float spawnX = walkIn
                ? (approachDistance > 0f
                    ? frontLineX + approachDistance
                    : RightEdgeX() + offscreenMargin)
                : frontLineX;

            boss.Killed += OnEnemyKilled;
            boss.Died += OnEnemyDied;
            boss.Spawn(definition, spawnX, stage.GroundY,
                       Onikiri.Core.SortingOrders.Boss, health, gold, exp, true,
                       scale, tint, attackDamage);
            active.Add(boss);

            return boss;
        }

        private int CountAlive()
        {
            int alive = 0;
            for (int i = 0; i < active.Count; i++)
                if (active[i].IsAlive) alive++;
            return alive;
        }

        private void Spawn()
        {
            if (definitions == null || definitions.Length == 0) return;

            var definition = PickDefinition();
            if (definition == null) return;

            var enemy = pool.Get();

            // 가까운 적이 뒤쪽 적보다 앞에 그려진다
            int sorting = SortingOrders.EnemyBase + (active.Count % SortingOrders.EnemySlots);

            // 배수는 스폰 시점에 확정한다. 스테이지 오브젝트가 아직 없으면(테스트 씬 등)
            // 1배로 떨어져 1스테이지 밸런스가 된다
            var progress = Onikiri.Progression.StageProgress.Instance;
            var healthMultiplier = progress != null ? progress.HealthMultiplier : BigDouble.One;
            var goldMultiplier = progress != null ? progress.GoldMultiplier : BigDouble.One;

            // 경험치는 정의 에셋이 아니라 스테이지에서만 나온다. 요괴 종류마다
            // 다른 경험치를 주면 가중치 추첨이 진행 속도에 섞여 들어가고,
            // ExpCurve가 재는 "스테이지당 경험치"가 추첨 결과에 따라 흔들린다
            int stageNumber = progress != null ? progress.Stage : 1;

            enemy.Killed += OnEnemyKilled;
            enemy.Died += OnEnemyDied;
            enemy.Spawn(definition, RightEdgeX() + offscreenMargin, stage.GroundY, sorting,
                        definition.maxHealth * healthMultiplier,
                        definition.goldReward * goldMultiplier,
                        Onikiri.Progression.ExpCurve.MobExp(stageNumber));
            active.Add(enemy);
        }

        /**
         * @brief 가중치 추첨. 화면의 크기 구성을 균등이 아니라 의도대로 만든다.
         *
         * 작은 필러 요괴는 높은 가중치를, 정예는 낮은 가중치를 갖는다.
         */
        private EnemyDefinition PickDefinition()
        {
            float total = 0f;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null) total += Mathf.Max(0f, definitions[i].spawnWeight);
            }
            if (total <= 0f) return definitions[0];

            float roll = Random.Range(0f, total);
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] == null) continue;
                roll -= Mathf.Max(0f, definitions[i].spawnWeight);
                if (roll <= 0f) return definitions[i];
            }
            return definitions[definitions.Length - 1];
        }

        /** 요괴 하나가 죽는 순간 발생. 보스전이 자기 보스의 죽음을 듣는다 */
        public event System.Action<Enemy> EnemyKilled;

        private void OnEnemyKilled(Enemy enemy)
        {
            enemy.Killed -= OnEnemyKilled;

            // 정의 에셋의 값이 아니라 이 개체가 스폰될 때 확정된 보상을 준다.
            //
            // 획득 축(20단계)은 **여기서** 곱한다. 지갑 쪽에서 곱하면 방치 보상이
            // 두 번 곱해진다 - 그쪽은 이미 배수가 반영된 초당 골드에서 나오고
            // 지급 경로는 같은 wallet.Add다. UpgradeSystem.GoldGainMultiplier 참고
            var wallet = Onikiri.Progression.PlayerWallet.Instance;
            if (wallet != null)
                wallet.Add(enemy.GoldReward
                    * Onikiri.Core.BigDouble.FromDouble(
                        Onikiri.Progression.UpgradeSystem.CurrentGoldGain));

            // 경험치도 같은 자리에서. 골드와 경험치가 서로 다른 경로로 지급되면
            // 언젠가 한쪽만 도는 상태가 생긴다 - BossFight가 골드를 여기 맡긴
            // 이유와 같다
            var character = Onikiri.Progression.CharacterLevel.Instance;
            if (character != null)
            {
                character.AddExp(enemy.ExpReward);
                if (damageNumbers != null)
                    damageNumbers.ShowExp(enemy.ExpReward, enemy.transform.position);
            }

            // 보스는 스테이지 할당량에 들어가지 않는다. 보스가 하는 일은 할당량을
            // 채우는 것이 아니라 스테이지를 올리는 것이고, 그 판단은 BossFight가 한다
            if (!enemy.IsBoss)
            {
                var progress = Onikiri.Progression.StageProgress.Instance;
                if (progress != null) progress.RegisterKill();
            }

            var handler = EnemyKilled;
            if (handler != null) handler(enemy);
        }

        private void OnEnemyDied(Enemy enemy)
        {
            enemy.Died -= OnEnemyDied;
            active.Remove(enemy);
            pool.Release(enemy);
        }

        /**
         * @brief 살아 있는 적을 앞에서 뒤로 훑으며 앞 적으로부터 일정 간격 뒤에 세운다.
         *
         * 죽어가는 적은 건너뛴다. 그래야 큐가 즉시 당겨진다.
         */
        private void UpdateQueuePositions()
        {
            active.Sort(CompareByX);

            float nextStop = frontLineX;
            for (int i = 0; i < active.Count; i++)
            {
                var enemy = active[i];
                if (!enemy.IsAlive) continue;

                enemy.SetTargetX(nextStop);
                nextStop += enemy.QueueSpacing;
            }
        }

        private static int CompareByX(Enemy a, Enemy b)
        {
            return a.CurrentX.CompareTo(b.CurrentX);
        }

        /** 화면 오른쪽 끝의 월드 X. 대상 기기 전체에서 가로 폭은 일정하다 */
        private float RightEdgeX()
        {
            var camera = Camera.main;
            if (camera == null) return 3.375f;
            return camera.transform.position.x + camera.orthographicSize * camera.aspect;
        }

        /** fromX 로부터 range 안에 있는 가장 가까운 생존 적 */
        public Enemy FindNearestAlive(float fromX, float range)
        {
            Enemy best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < active.Count; i++)
            {
                var enemy = active[i];
                if (!enemy.IsTargetable) continue;

                float distance = Mathf.Abs(enemy.CurrentX - fromX);
                if (distance > range || distance >= bestDistance) continue;

                bestDistance = distance;
                best = enemy;
            }

            return best;
        }
    }
}
