using System.Collections.Generic;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Keeps a steady stream of yokai walking in from off-screen right.
    ///
    /// The portrait battle area is only 6.75 world units wide, so the spec caps what is
    /// readable at 3-5 enemies at once. Rather than spawning waves on a timer and hoping,
    /// this tops the field back up to a target count, which keeps the screen busy without
    /// ever crowding.
    ///
    /// Queue positions are recomputed every frame from the live enemy order, so enemies
    /// line up behind each other instead of stacking on the same spot, and the queue closes
    /// up automatically when a front enemy dies.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleStageLayout stage;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnemyDefinition[] definitions;
        [SerializeField] private Transform enemyParent;

        [Header("Field")]
        [Tooltip("How many yokai should be alive at once. The spec calls for 3-5 on screen.")]
        [SerializeField] private int targetAlive = 4;

        [Tooltip("Seconds between top-ups.")]
        [SerializeField] private float spawnInterval = 1.1f;

        [Tooltip("How far beyond the right screen edge they appear, in world units.")]
        [SerializeField] private float offscreenMargin = 1.2f;

        [Header("Queue")]
        [Tooltip("World X the leading enemy stops at - just inside the player's reach.")]
        [SerializeField] private float frontLineX = -0.9f;

        [Header("Pool")]
        [SerializeField] private int prewarm = 8;

        private ObjectPool<Enemy> pool;
        private readonly List<Enemy> active = new List<Enemy>();
        private float spawnTimer;

        /// <summary>Live enemies, nearest to the player first.</summary>
        public IReadOnlyList<Enemy> Active { get { return active; } }

        public int PoolGrowthCount { get { return pool != null ? pool.GrowthCount : 0; } }

        private void Awake()
        {
            if (enemyParent == null) enemyParent = transform;
            pool = new ObjectPool<Enemy>(enemyPrefab, enemyParent, prewarm);
        }

        private void Update()
        {
            UpdateQueuePositions();

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && CountAlive() < targetAlive)
            {
                Spawn();
                spawnTimer = spawnInterval;
            }
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

            // Nearer enemies draw in front of the ones behind them.
            int sorting = SortingOrders.EnemyBase + (active.Count % SortingOrders.EnemySlots);

            enemy.Killed += OnEnemyKilled;
            enemy.Died += OnEnemyDied;
            enemy.Spawn(definition, RightEdgeX() + offscreenMargin, stage.GroundY, sorting);
            active.Add(enemy);
        }

        /// <summary>
        /// Weighted pick, so the size mix on screen is authored rather than uniform: small
        /// filler yokai carry a high weight and elites a low one.
        /// </summary>
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

        private void OnEnemyKilled(Enemy enemy)
        {
            enemy.Killed -= OnEnemyKilled;

            var wallet = Onikiri.Progression.PlayerWallet.Instance;
            if (wallet != null && enemy.Definition != null) wallet.Add(enemy.Definition.goldReward);
        }

        private void OnEnemyDied(Enemy enemy)
        {
            enemy.Died -= OnEnemyDied;
            active.Remove(enemy);
            pool.Release(enemy);
        }

        /// <summary>
        /// Walks the live enemies front to back and parks each one a fixed gap behind the
        /// one ahead. Dying enemies are skipped so the queue closes up immediately.
        /// </summary>
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

        /// <summary>World X of the right screen edge. Width is constant across our phones.</summary>
        private float RightEdgeX()
        {
            var camera = Camera.main;
            if (camera == null) return 3.375f;
            return camera.transform.position.x + camera.orthographicSize * camera.aspect;
        }

        /// <summary>Nearest living enemy within <paramref name="range"/> of <paramref name="fromX"/>.</summary>
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
