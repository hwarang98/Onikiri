using System;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 보스전 한 판의 상태 기계.
     *
     * 스테이지 게이트의 실행부다. StageProgress는 "보스가 열렸는가"만 알고, 실제로
     * 필드를 비우고 보스를 세우고 시계를 재는 것은 여기다.
     *
     * 흐름은 네 상태다:
     *
     *   Farming  잡몹 무한 스폰. 할당량을 채우면 도전 버튼이 열린다
     *   Intro    화면이 어두워지고 이름이 뜬다. 필드는 이미 비어 있다
     *   Fighting 보스 하나. 30초 시계가 돈다
     *   Failed   실패 문구를 잠깐 보여주고 Farming으로 돌아간다
     *
     * **실패에 패널티가 없다.** 스테이지도 골드도 그대로다. 잃는 것은 시간뿐이고,
     * 다시 도전하려면 버튼을 눌러야 한다. 자동 재도전을 넣지 않은 이유는 그러면
     * 실패가 배경 소음이 되기 때문이다 - 30초마다 조용히 실패하는 화면에서는
     * 강화가 필요하다는 신호가 전달되지 않는다.
     *
     * 할당량은 실패해도 유지된다(kill count 재적립 없음). 잡몹 10마리를 다시 잡는
     * 것은 강해지는 일이 아니라 기다리는 일이고, 실패의 대가로 기다림을 물리면
     * 플레이어가 배우는 것은 "보스에 도전하지 말자"다.
     *
     * 시계는 Time.deltaTime으로 돈다(스케일 타임). 히트스톱이 걸린 만큼은 제한
     * 시간에서 빠지지 않는다. 타격을 잘 넣을수록 정지가 길어지는데 그만큼 시간을
     * 뺏기면, 잘 싸울수록 손해라는 규칙이 된다.
     */
    public sealed class BossFight : MonoBehaviour
    {
        public enum Phase { Farming, Intro, Fighting, Failed }

        [Header("참조")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private StageProgress progress;
        [SerializeField] private EnemyDefinition bossDefinition;

        [Tooltip("실패 문구가 어느 축을 올리라고 안내할지 판단하는 데 쓴다")]
        [SerializeField] private UpgradeSystem upgrades;

        [Header("연출")]
        [Tooltip("이름이 뜨고 화면이 어두워져 있는 시간. 보스가 들어오기 전이다")]
        [SerializeField] private float introSeconds = 1f;

        [Tooltip("실패 문구를 붙들고 있는 시간. 그 뒤 잡몹 파밍으로 돌아간다")]
        [SerializeField] private float failSeconds = 3f;

        [Tooltip("보스 이름. 화면에 그대로 뜬다")]
        [SerializeField] private string bossName = "다크 사무라이";

        /** 상태가 바뀔 때마다 발생. HUD가 이것만 듣는다 */
        public event Action Changed;

        private Phase phase = Phase.Farming;
        private Enemy boss;
        private float timer;

        /** 실패 화면이 보여줄 문구. 실패 시점에 한 번 계산해서 붙들어둔다 */
        private string failureMessage = string.Empty;

        public Phase Current { get { return phase; } }
        public string BossName { get { return bossName; } }
        public Enemy Boss { get { return boss; } }
        public string FailureMessage { get { return failureMessage; } }

        /** 남은 제한 시간. Fighting이 아니면 0 */
        public float SecondsLeft
        {
            get { return phase == Phase.Fighting ? Mathf.Max(0f, timer) : 0f; }
        }

        public float BossHealthFraction
        {
            get { return boss != null && boss.IsAlive ? boss.HealthFraction : 0f; }
        }

        /**
         * @brief 지금 도전 버튼을 누를 수 있는가.
         *
         * 파밍 중이고 할당량을 채웠을 때만이다. 보스전 도중이나 실패 문구가 떠 있는
         * 동안 버튼이 살아 있으면 연출 중에 다시 진입해 상태가 겹친다.
         */
        public bool CanChallenge
        {
            get { return phase == Phase.Farming && progress != null && progress.IsBossReady; }
        }

        /** 이번 스테이지 보스의 체력. HUD와 실패 계산이 함께 쓴다 */
        public BigDouble BossMaxHealth
        {
            get
            {
                if (spawner == null || progress == null) return BigDouble.Zero;
                return StageCurve.BossHealthForStage(spawner.AverageBaseHealth, progress.Stage);
            }
        }

        public BigDouble BossGoldReward
        {
            get
            {
                if (spawner == null || progress == null) return BigDouble.Zero;
                return StageCurve.BossGoldForStage(spawner.AverageBaseGold, progress.Stage);
            }
        }

        private void OnEnable()
        {
            if (spawner != null) spawner.EnemyKilled += OnEnemyKilled;

            // 할당량이 차는 순간 도전 버튼이 떠야 한다. 그 사건은 StageProgress에서
            // 나오므로 여기서 받아 그대로 넘긴다. HUD가 두 곳을 구독하게 두면
            // 어느 쪽이 화면을 마지막으로 갱신했는지에 따라 버튼 상태가 갈린다
            if (progress != null) progress.Changed += Raise;
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.EnemyKilled -= OnEnemyKilled;
            if (progress != null) progress.Changed -= Raise;
        }

        // ---------------------------------------------------------------- 진입

        /**
         * @brief 보스전을 시작한다. 도전 버튼과 테스트 패널이 부르는 유일한 진입점.
         *
         * 조건을 여기서 다시 확인한다. 버튼이 꺼져 있다는 것만 믿으면 UI가 한 프레임
         * 늦게 갱신되는 경로에서 중복 진입이 생긴다.
         */
        public void Challenge()
        {
            if (!CanChallenge) return;

            // 필드를 먼저 비우고 어둡게 한다. 순서가 반대면 어두워진 화면 위에서
            // 잡몹이 사라지는 것이 보인다
            if (spawner != null)
            {
                spawner.SuspendSpawning();
                spawner.ClearField();
            }

            boss = null;
            // 지난 판의 실패 문구를 들고 가지 않는다. 읽는 쪽이 Failed 상태에서만
            // 보긴 하지만, 남아 있으면 디버그 표시가 "지금 실패한 것처럼" 보인다
            failureMessage = string.Empty;
            timer = introSeconds;
            SetPhase(Phase.Intro);
        }

        // ---------------------------------------------------------------- 진행

        private void Update()
        {
            if (phase == Phase.Farming) return;

            timer -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Intro:
                    if (timer <= 0f) BeginFight();
                    break;

                case Phase.Fighting:
                    // 보스가 스폰에 실패했거나(정의 누락) 어떤 이유로 사라졌으면
                    // 시계만 도는 상태로 30초를 버리지 않고 즉시 정리한다
                    if (boss == null || !boss.IsAlive) { Fail(); break; }
                    if (timer <= 0f) Fail();
                    break;

                case Phase.Failed:
                    if (timer <= 0f) ReturnToFarming();
                    break;
            }

            // 시계는 여기서 알리지 않는다. Changed는 상태가 바뀔 때만 발생한다.
            // 매 프레임 발생시키면 HUD가 그때마다 TMP 메시를 다시 만들고, 남은 시간
            // 표시는 1초 단위라 그중 59/60은 같은 글자를 다시 그리는 일이 된다.
            // 시계와 체력 바는 BossHud가 직접 읽어 간다
        }

        private void BeginFight()
        {
            if (spawner == null || bossDefinition == null)
            {
                Debug.LogError("[Onikiri] BossFight is missing its spawner or boss definition.");
                ReturnToFarming();
                return;
            }

            boss = spawner.SpawnBoss(bossDefinition, BossMaxHealth, BossGoldReward);
            timer = StageCurve.BossTimeLimitSeconds;
            SetPhase(Phase.Fighting);
        }

        /**
         * @brief 보스가 죽었다. 스테이지가 오르는 유일한 지점.
         *
         * 골드는 여기서 주지 않는다. 스포너가 다른 요괴와 똑같이 처치 시점에 이미
         * 지급했다. 보상 경로를 둘로 나누면 언젠가 한쪽만 도는 상태가 생긴다.
         */
        private void OnEnemyKilled(Enemy enemy)
        {
            if (phase != Phase.Fighting || enemy == null || !enemy.IsBoss || enemy != boss) return;

            if (progress != null) progress.AdvanceStage();

            boss = null;
            ReturnToFarming();
        }

        private void Fail()
        {
            failureMessage = BuildFailureMessage();

            // 보스를 보상 없이 치운다. 남겨두면 파밍으로 돌아간 뒤에도 필드에 서
            // 있고, 잡몹 큐의 앞을 막아 사무라이가 보스만 때리게 된다
            if (spawner != null) spawner.ClearField();
            boss = null;

            timer = failSeconds;
            SetPhase(Phase.Failed);
        }

        /**
         * @brief 실패 문구. 제한 시간 동안 실제로 넣은 피해로 계산한다.
         *
         * 보스는 이미 정리 직전이므로 여기서 먼저 읽어야 한다. Fail()이 이것을
         * ClearField보다 앞에서 부르는 이유다.
         */
        private string BuildFailureMessage()
        {
            var dealt = boss != null ? boss.DamageTaken : BigDouble.Zero;
            var maxHealth = boss != null ? boss.MaxHealth : BossMaxHealth;

            var tracks = new UpgradeTrack[0];
            if (upgrades != null)
            {
                tracks = new UpgradeTrack[upgrades.TrackCount];
                for (int i = 0; i < tracks.Length; i++) tracks[i] = upgrades.GetTrack(i);
            }

            return BossFailureAdvice.Message(maxHealth, dealt, tracks);
        }

        /**
         * @brief 잡몹 파밍으로 되돌린다.
         *
         * 성공·실패 양쪽이 여기로 온다. 소프트락을 막는 지점이라 경로를 하나로
         * 묶어뒀다 - 어느 한쪽만 스폰을 다시 켜는 것을 잊으면 화면이 영원히 빈다.
         */
        private void ReturnToFarming()
        {
            if (spawner != null) spawner.ResumeSpawning();
            SetPhase(Phase.Farming);
        }

        /**
         * @brief 남은 시간을 0으로 만든다. 테스트 패널 전용.
         *
         * 상태를 Failed로 직접 바꾸지 않는 이유는, 그러면 실패 문구 계산과 필드
         * 정리를 건너뛰게 되어 정작 확인하려던 경로가 돌지 않기 때문이다. 시계만
         * 밀고 나머지는 실제 코드에 맡긴다.
         */
        public void DebugExpireTimer()
        {
            if (phase == Phase.Fighting) timer = 0f;
        }

        private void SetPhase(Phase value)
        {
            phase = value;
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
