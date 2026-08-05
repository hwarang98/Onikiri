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

        [Header("필드")]
        [Tooltip("동시에 살아 있어야 할 요괴 수. 사양서는 화면에 3~5마리를 요구한다")]
        [SerializeField] private int targetAlive = 4;

        [Tooltip("보충 사이의 간격 (초)")]
        [SerializeField] private float spawnInterval = 1.1f;

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

        /** 방치 보상이 요괴 공급 상한을 계산할 때 쓴다 */
        public float SpawnInterval { get { return spawnInterval; } }

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
        }

        private void Update()
        {
            UpdateQueuePositions();

            if (spawningSuspended) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && CountAlive() < targetAlive)
            {
                Spawn();
                spawnTimer = spawnInterval;
            }
        }

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
        public Enemy SpawnBoss(EnemyDefinition definition, BigDouble health, BigDouble gold)
        {
            if (definition == null) return null;

            var boss = pool.Get();

            boss.Killed += OnEnemyKilled;
            boss.Died += OnEnemyDied;
            boss.Spawn(definition, RightEdgeX() + offscreenMargin, stage.GroundY,
                       Onikiri.Core.SortingOrders.Boss, health, gold, true);
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

            enemy.Killed += OnEnemyKilled;
            enemy.Died += OnEnemyDied;
            enemy.Spawn(definition, RightEdgeX() + offscreenMargin, stage.GroundY, sorting,
                        definition.maxHealth * healthMultiplier,
                        definition.goldReward * goldMultiplier);
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

            // 정의 에셋의 값이 아니라 이 개체가 스폰될 때 확정된 보상을 준다
            var wallet = Onikiri.Progression.PlayerWallet.Instance;
            if (wallet != null) wallet.Add(enemy.GoldReward);

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
