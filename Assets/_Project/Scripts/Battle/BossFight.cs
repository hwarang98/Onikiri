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
        /**
         * @brief 보스전의 다섯 상태.
         *
         * 17단계에서 `Approaching`이 생겼다. 그전에는 보스가 스폰되자마자
         * 30초 시계가 돌기 시작했는데, 보스는 화면 밖에서 걸어 들어오고 있어서
         * **제한 시간의 18%가 사거리에 아무도 없는 상태로 흘렀다.**
         *
         * 이제 그 구간이 별도 상태다. 플레이어가 보스에게 달려가고(배경 스크롤),
         * 도달한 순간부터 시계가 시작한다. 달려가는 시간은 타이머 밖이다.
         */
        public enum Phase { Farming, Intro, Approaching, Fighting, Cleared, Failed }

        /** 실패한 이유. 문구가 갈린다 */
        public enum FailureReason { TimeOut, Death }

        [Header("참조")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private StageProgress progress;
        [SerializeField] private PlayerHealth playerHealth;

        /**
         * @brief 스테이지 -> 보스 매핑. 13단계에서 코드 분기를 대체했다.
         *
         * 비어 있으면 아래의 옛 경로(bossDefinition + 5의 배수)로 떨어진다.
         * 폴백을 남겨두는 이유는 로스터가 없는 테스트 씬에서 보스전이 아예
         * 돌지 않으면 원인이 "로스터가 없다"인지 "보스전이 깨졌다"인지
         * 구분되지 않기 때문이다.
         */
        [SerializeField] private BossRoster roster;

        [Tooltip("로스터가 없을 때만 쓰는 폴백 정의")]
        [SerializeField] private EnemyDefinition bossDefinition;

        [Tooltip("일반 스테이지 보스로 쓸 잡몹 정의들. 스테이지로 하나를 고른다")]
        [SerializeField] private EnemyDefinition[] stageBossDefinitions;

        [Tooltip("잡몹을 보스로 쓸 때의 확대 배율. 정수만 쓴다 - 픽셀 격자가 " +
                 "어긋나지 않는 유일한 값이다")]
        [SerializeField] private float stageBossScale = 2f;

        [Tooltip("확대판 보스의 틴트. 같은 놈이 커진 것이 아니라 우두머리로 읽히게 한다")]
        [SerializeField] private Color stageBossTint = new Color32(0xFF, 0xC0, 0xC8, 0xFF);

        [Tooltip("실패 문구가 어느 축을 올리라고 안내할지 판단하는 데 쓴다")]
        [SerializeField] private UpgradeSystem upgrades;

        [Tooltip("보스에게 달려가는 동안 한 번 지나가는 토리이 관문. " +
                 "파밍 중에는 화면에 없다")]
        [SerializeField] private BossGate gate;

        [Tooltip("전진 속도를 읽어 달려가기 거리를 유도한다")]
        [SerializeField] private StageAdvance advance;

        [Header("연출")]
        /**
         * @brief 보스가 휘두를 때 앞에 뜨는 참격. 요괴 팩에서 뜯어낸 조각이다.
         *
         * 없어도 보스전은 그대로 돈다 - 이펙트는 전부 이 참조 뒤에 있고, 비어
         * 있으면 아무것도 뜨지 않는다. 로스터 없는 테스트 씬에서 보스전을 돌릴 때
         * 이것까지 배선하라고 요구하지 않기 위해서다.
         */
        [Tooltip("보스가 휘두를 때 앞에 참격을 띄운다. 비워도 보스전은 그대로 돈다")]
        [SerializeField] private VfxBurst attackVfx;

        /**
         * @brief BossConfig가 이펙트를 지정하지 않았을 때 쓰는 이름. **기본은 비어 있다.**
         *
         * 한 번 `crescent`를 기본값으로 두었다가 곧바로 되돌렸다. 그 순간
         * **다섯 보스가 전부 같은 참격을 뿜었다** - 등롱도 처형인도 붉은눈도
         * 요괴(Inimig 9)의 붉은 초승달을 뿌렸다. 어느 BossConfig도 이 칸을
         * 채우지 않았으니 전부 기본값을 상속한 것이고, 그것이 기본값을 두는
         * 일의 실제 결과였다.
         *
         * 참격은 **한 요괴의 서명**이지 보스라는 역할에 딸린 장식이 아니다.
         * 넷이 같은 것을 뿜으면 넷을 구분하던 유일한 신호가 사라진다.
         *
         * 그래서 비워 둔다. 이펙트를 쓰는 보스는 자기 BossConfig에 이름을 적고,
         * 안 적은 보스는 지금까지처럼 자기 시트의 공격 모션만 쓴다. 언젠가
         * 공용 중립 참격이 필요해지면 그때 이 칸에 그 이름을 적으면 되고,
         * **그 이름이 요괴의 것이어서는 안 된다.**
         */
        [Tooltip("BossConfig가 비어 있을 때 쓸 참격 이름. 비우면 아무것도 안 뜬다. " +
                 "특정 요괴의 시그니처 참격을 여기 적지 말 것 - 모든 보스가 그것을 뿜는다")]
        [SerializeField] private string defaultAttackVfxId = string.Empty;

        [Tooltip("챕터 보스의 등장 연출 시간. 화면이 어두워지고 이름이 뜬다")]
        [SerializeField] private float introSeconds = 1f;

        [Tooltip("일반 스테이지 보스의 등장 시간. 이름만 짧게 스친다")]
        [SerializeField] private float stageBossIntroSeconds = 0.4f;

        [Tooltip("실패 문구를 붙들고 있는 시간. 그 뒤 잡몹 파밍으로 돌아간다")]
        [SerializeField] private float failSeconds = 3f;

        [Tooltip("보스에게 달려가는 데 허용하는 최대 시간. 제한 시간이 아니라 " +
                 "안전장치다 - 보스가 영영 도달하지 못하면 접근 상태에 갇히므로 " +
                 "그때는 그냥 전투를 시작해 정상 경로로 흘려보낸다")]
        [SerializeField] private float approachTimeoutSeconds = 15f;

        [Tooltip("클리어 배너를 붙들고 있는 시간. 곧바로 잡몹이 나오면 방금 " +
                 "무엇을 해냈는지가 화면에서 지워진다")]
        [SerializeField] private float clearBannerSeconds = 2f;

        [Tooltip("지역 피날레는 더 길게 붙든다. 지역을 넘는 것은 스테이지를 " +
                 "넘는 것과 다른 사건이다")]
        [SerializeField] private float regionClearBannerSeconds = 3.5f;

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

        /** 지금(또는 다음) 스테이지 번호. progress가 없으면 1 */
        private int CurrentStage { get { return progress != null ? progress.Stage : 1; } }

        /** 이 스테이지에 배치된 보스. null이면 잡몹 확대판 */
        public BossConfig CurrentBossConfig
        {
            get { return roster != null ? roster.BossForStage(CurrentStage) : null; }
        }

        /** 지금(또는 다음) 보스가 확대판이 아닌 보스인가. 배수가 갈린다 */
        public bool IsChapterBoss
        {
            get { return progress != null && BossCurve.IsChapterBoss(progress.Stage); }
        }

        /**
         * @brief 전체 등장 연출을 쓰는가.
         *
         * 배치 애셋이 있으면 그쪽 말을 듣는다. 연출의 크기는 밸런스가 아니라
         * 그 보스가 어떤 자리인가의 문제이고, 그 자리를 정하는 것은 로스터다.
         */
        public bool UsesFullIntro
        {
            get
            {
                var config = CurrentBossConfig;
                if (config != null) return config.fullIntro;
                return BossCurve.UsesFullIntro(CurrentStage);
            }
        }

        /**
         * @brief 화면에 세울 보스 이름.
         *
         * 챕터 보스만 고유 이름을 갖는다. 일반 스테이지 보스는 잡몹의 확대판이라
         * 그 잡몹의 이름 앞에 우두머리 표시를 붙인다.
         */
        public string BossName
        {
            get
            {
                var config = CurrentBossConfig;
                if (config != null)
                {
                    // 확대형은 이름 앞에 우두머리 표시를 붙인다. 원본 잡몹을
                    // 방금까지 베고 있었으므로 "저놈의 우두머리"로 읽혀야 한다.
                    // 시트형은 전용 아트라 그럴 필요가 없다
                    if (config.kind == BossConfig.ArtKind.Sheets) return config.displayName;
                    return string.IsNullOrEmpty(config.displayName)
                        ? "거대 " + SafeMobName()
                        : config.displayName;
                }

                if (IsChapterBoss) return bossName;
                var definition = StageBossDefinition();
                return definition != null ? "거대 " + definition.displayName : bossName;
            }
        }

        private string SafeMobName()
        {
            var definition = StageBossDefinition();
            return definition != null ? definition.displayName : bossName;
        }
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
            get
            {
                // 최전선에서만 열린다(37단계). 클리어한 스테이지에서 보스를 다시
                // 잡을 수 있으면 클리어 보너스와 보스 경험치가 반복 수급된다 -
                // 되돌아간 스테이지는 순수 파밍이고, 복귀는 재선택 화면이 맡는다
                return phase == Phase.Farming && progress != null
                       && progress.IsBossReady && progress.IsAtFrontier;
            }
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

        /** 챕터 배수는 ExpCurve.BossExp 안에 들어 있다. 여기서 또 곱하면 두 번 적용된다 */
        public BigDouble BossExpReward
        {
            get { return progress != null ? ExpCurve.BossExp(progress.Stage) : BigDouble.Zero; }
        }

        /**
         * @brief 일반 스테이지 보스로 쓸 잡몹 정의.
         *
         * 스테이지로 고른다. 무작위로 뽑으면 재도전할 때마다 다른 놈이 나와서
         * "이 스테이지의 우두머리"라는 인상이 생기지 않는다.
         */
        /**
         * @brief 일반 보스·정예로 쓸 잡몹 풀을 바꾼다. 지역이 넘어갈 때
         * RegionMobSwitcher가 스포너와 함께 부른다.
         *
         * 스포너만 바꾸고 이쪽을 두면 잡몹은 새 지역인데 확대판 보스만 옛 지역
         * 몹이 된다 - "방금까지 베던 놈의 우두머리"가 깨진다.
         */
        public void SetStageBossDefinitions(EnemyDefinition[] next)
        {
            if (next == null || next.Length == 0) return;
            stageBossDefinitions = next;
        }

        private EnemyDefinition StageBossDefinition()
        {
            if (stageBossDefinitions == null || stageBossDefinitions.Length == 0) return bossDefinition;

            int stage = progress != null ? progress.Stage : 1;
            int index = Mathf.Abs(stage - 1) % stageBossDefinitions.Length;
            return stageBossDefinitions[index] != null ? stageBossDefinitions[index] : bossDefinition;
        }

        /** 이번 보스가 한 번 때릴 때의 피해 */
        public double BossAttackDamage
        {
            get { return BossCurve.AttackDamageForStage(progress != null ? progress.Stage : 1); }
        }

        /** 보스가 플레이어를 때렸다 */
        private void OnBossAttacked(Enemy attacker)
        {
            if (phase != Phase.Fighting || playerHealth == null) return;
            playerHealth.TakeDamage(attacker.AttackDamage);
        }

        /**
         * @brief 보스가 **동작을 시작했다.** 타격보다 예비 동작만큼 이르다.
         *
         * 여기서 하는 일은 그림을 하나 띄우는 것뿐이다. 피해는 위의
         * `OnBossAttacked`가 넣고, 그쪽은 이 함수가 무엇을 하든 같은 주기로
         * 같은 양을 넣는다 - 연출을 얹는 자리와 밸런스가 사는 자리를 갈라둔
         * 것이 `SwingStarted`와 `Attacked`가 다른 사건인 이유다(Enemy 참고).
         */
        private void OnBossSwing(Enemy attacker)
        {
            if (phase != Phase.Fighting || attackVfx == null || attacker == null) return;

            var config = CurrentBossConfig;
            string id = config != null && !string.IsNullOrEmpty(config.attackVfxId)
                ? config.attackVfxId
                : defaultAttackVfxId;

            // 참격은 보스가 **바라보는 쪽**에 뜬다. 보스는 오른쪽에 서서 왼쪽의
            // 사무라이를 보므로 대개 왼쪽이고, 그 판단은 요괴가 이미 하고 있다
            attackVfx.Play(id, attacker.transform.position, attacker.FacingDirection < 0);
        }

        /** 플레이어가 쓰러졌다. 시간 초과와 같은 '스테이지 실패'로 합류한다 */
        private void OnPlayerDied()
        {
            if (phase != Phase.Fighting) return;
            Fail(FailureReason.Death);
        }

        private void OnEnable()
        {
            if (spawner != null) spawner.EnemyKilled += OnEnemyKilled;
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;

            // 할당량이 차는 순간 도전 버튼이 떠야 한다. 그 사건은 StageProgress에서
            // 나오므로 여기서 받아 그대로 넘긴다. HUD가 두 곳을 구독하게 두면
            // 어느 쪽이 화면을 마지막으로 갱신했는지에 따라 버튼 상태가 갈린다
            if (progress != null) progress.Changed += Raise;
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.EnemyKilled -= OnEnemyKilled;
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
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

            // 전체 연출(화면 어둡게 + 이름 + 워크인)은 챕터 보스 전용이다.
            // 일반 스테이지 보스는 짧은 이름만 띄우고 곧바로 싸운다 - 매 스테이지
            // 6초씩 반복되면 연출은 무게가 아니라 대기 시간이 된다. BossCurve 참고
            // 암전 시간도 전체 연출을 쓰는 보스만 길다. 챕터 관문(엘리트 확대판)은
            // 짧게 스치는 쪽이고, 그것이 피날레와 챕터를 화면에서 가르는 신호다
            timer = UsesFullIntro ? introSeconds : stageBossIntroSeconds;
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
                    if (timer <= 0f) BeginApproach();
                    break;

                case Phase.Approaching:
                    // 보스가 사거리에 들어오면 시계가 시작한다. 여기 timer는
                    // 제한 시간이 아니라 **안전장치**다 - 어떤 이유로 보스가
                    // 영영 도달하지 못하면(스폰 실패, 이동 속도 0) 접근 상태에
                    // 갇히므로, 그때는 그냥 전투를 시작해 정상 경로로 흘려보낸다
                    if (boss == null || !boss.IsAlive) { ReturnToFarming(); break; }
                    if (boss.CurrentState == Enemy.State.Engaged || timer <= 0f) BeginFight();
                    break;

                case Phase.Fighting:
                    // 보스가 스폰에 실패했거나(정의 누락) 어떤 이유로 사라졌으면
                    // 시계만 도는 상태로 30초를 버리지 않고 즉시 정리한다
                    if (boss == null || !boss.IsAlive) { Fail(FailureReason.TimeOut); break; }
                    if (timer <= 0f) Fail(FailureReason.TimeOut);
                    break;

                case Phase.Cleared:
                    if (timer <= 0f) ReturnToFarming();
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

        /**
         * @brief 보스를 세우고 **달려가기 시작한다.** 시계는 아직 안 돈다.
         *
         * 17단계 이전에는 이 함수가 스폰과 시계 시작을 함께 했다. 그래서 보스가
         * 걸어 들어오는 5.3초가 제한 시간 안에 있었고, 그동안 사거리는 비어
         * 있었다 - 제한 시간의 18%가 아무 일도 일어나지 않는 구간이었다.
         *
         * 이제 둘이 나뉜다. 여기서는 보스를 화면 밖에 세우고 접근 상태로 들어가며,
         * 시계는 보스가 사거리에 닿을 때 {@link BeginFight}가 시작한다.
         *
         * **모든 보스가 달려가기를 거친다.** 13단계에서는 피날레만 워크인을
         * 썼는데, 그때는 워크인이 제한 시간을 갉아먹었기 때문에 아껴 쓴 것이다.
         * 이제 타이머 밖이므로 아낄 이유가 없고, 오히려 매 스테이지 "달려가서
         * 벤다"는 리듬이 생긴다.
         */
        private void BeginApproach()
        {
            if (spawner == null)
            {
                Debug.LogError("[Onikiri] BossFight has no spawner.");
                ReturnToFarming();
                return;
            }

            var config = CurrentBossConfig;

            // 배치 애셋이 있으면 그쪽이 정의·확대·틴트를 전부 정한다.
            // 없으면 12단계까지의 경로로 떨어진다
            EnemyDefinition definition;
            float scale;
            Color tint;
            BigDouble health = BossMaxHealth;
            BigDouble gold = BossGoldReward;
            double attack = BossAttackDamage;

            if (config != null)
            {
                // 확대형인데 baseMob이 비어 있으면 그 스테이지의 잡몹을 쓴다.
                // "이 스테이지의 우두머리"라는 인상은 방금까지 베던 놈이라야 생긴다
                definition = config.Definition != null ? config.Definition : StageBossDefinition();
                scale = config.SpawnScale;
                tint = config.SpawnTint;

                // 애셋의 추가 배수. 기본 1이라 대개 아무 일도 하지 않는다
                health = config.ApplyHealth(health);
                gold = config.ApplyGold(gold);
                attack = config.ApplyAttack(attack);
            }
            else
            {
                bool chapter = IsChapterBoss;
                definition = chapter ? bossDefinition : StageBossDefinition();
                scale = chapter ? 1f : stageBossScale;
                tint = chapter ? Color.white : stageBossTint;
            }

            if (definition == null)
            {
                Debug.LogError("[Onikiri] BossFight found no definition for stage " + CurrentStage + ".");
                ReturnToFarming();
                return;
            }

            // 모든 보스가 화면 밖에서 시작한다. 그 구간이 타이머 밖이 되면서
            // 아낄 이유가 없어졌다
            // 달려가기 거리를 **시간에서 유도한다.** 시뮬레이션이 5.3초를
            // 가정하므로(StageSimulation.BossRunUpSeconds) 거리가 아니라 그
            // 시간이 기준이고, 전진 속도를 바꿔도 달려가기 길이는 안 변한다
            float closing = definition.moveSpeed + (advance != null ? advance.ScrollSpeed : 0f);
            float approachDistance = closing * (float)StageSimulation.BossRunUpSeconds;

            boss = spawner.SpawnBoss(
                definition, health, gold, BossExpReward,
                scale, tint, attack,
                true, approachDistance);

            if (boss != null)
            {
                boss.Attacked += OnBossAttacked;
                boss.SwingStarted += OnBossSwing;
            }

            // 관문을 세운다. 달려가기 구간의 중간쯤에 서므로 플레이어가 먼저
            // 지나고, 그 너머가 보스의 영역이 된다
            if (gate != null)
                gate.Show(UsesFullIntro, (float)StageSimulation.BossRunUpSeconds,
                          advance != null ? advance.ScrollSpeed : 0f);

            // 안전장치용 시계. 제한 시간이 아니라 "이만큼 지나도 도달 못 하면
            // 그냥 시작한다"는 상한이다. 접근에 걸리는 시간의 세 배쯤 잡는다
            timer = approachTimeoutSeconds;
            SetPhase(Phase.Approaching);
        }

        /** 보스가 사거리에 닿았다. **여기서부터 30초가 돈다** */
        private void BeginFight()
        {
            if (playerHealth != null) playerHealth.BeginFight();

            timer = StageCurve.BossTimeLimitSeconds;
            SetPhase(Phase.Fighting);
        }

        /**
         * @brief 보스가 죽었다. 스테이지가 오르는 유일한 지점.
         *
         * **처치 골드**는 여기서 주지 않는다. 스포너가 다른 요괴와 똑같이 처치
         * 시점에 이미 지급했다. 보상 경로를 둘로 나누면 언젠가 한쪽만 도는
         * 상태가 생긴다.
         *
         * **클리어 보너스**는 여기서 준다(17단계). 그것은 요괴를 벤 대가가 아니라
         * 스테이지를 넘은 대가라 스포너가 알 수 없는 사건이다.
         */
        private void OnEnemyKilled(Enemy enemy)
        {
            if (phase != Phase.Fighting || enemy == null || !enemy.IsBoss || enemy != boss) return;

            int clearedStage = CurrentStage;
            ClearedStage = clearedStage;
            ClearBonus = GrantClearBonus(clearedStage);
            ClearedRegion = roster != null && roster.IsFinale(clearedStage);

            // 혼과 파편(44단계). **AdvanceStage보다 앞이다** - 드랍 판정이
            // "방금 벤 보스가 서 있던 스테이지"를 봐야 하고, 스테이지가 먼저
            // 오르면 한 칸 뒤의 등급을 읽는다(정예/피날레가 갈리는 자리라
            // 그 한 칸이 곧 파편이냐 혼이냐다).
            //
            // 클리어 보너스가 여기 있는 것과 같은 이유로 여기 있다 - 요괴를
            // 벤 대가가 아니라 **그 요괴가 무엇이었는가**의 대가이고, 그것은
            // 스포너가 알 수 없는 사실이다(로스터가 안다)
            var yodo = Onikiri.Progression.YodoSystem.Instance;
            if (yodo != null) yodo.ReportBossDefeated(CurrentBossConfig, clearedStage);

            if (progress != null) progress.AdvanceStage();

            boss = null;

            // 클리어 배너를 띄우고 그동안은 파밍으로 돌아가지 않는다. 곧바로
            // 잡몹이 나오면 방금 무엇을 해냈는지가 화면에서 지워진다
            timer = ClearedRegion ? regionClearBannerSeconds : clearBannerSeconds;
            SetPhase(Phase.Cleared);
        }

        /** 마지막으로 클리어한 스테이지. 배너가 "지역 1 · 10/10"을 만들 때 쓴다 */
        public int ClearedStage { get; private set; }

        /** 그 클리어로 받은 보너스 골드. 배너가 숫자로 보여준다 */
        public BigDouble ClearBonus { get; private set; }

        /** 지역 피날레를 넘었는가. 배너 문구와 크기가 갈린다 */
        public bool ClearedRegion { get; private set; }

        /**
         * @brief 클리어 보너스 골드를 지급한다.
         *
         * 크기는 StageCurve가 정한다. 16단계에서 피날레 골드가 다음 스테이지
         * 보스를 무의미하게 만든 적이 있어서, 이 값은 시뮬레이션에 반영된
         * 상태로만 움직여야 한다.
         */
        private BigDouble GrantClearBonus(int stage)
        {
            if (spawner == null) return BigDouble.Zero;

            // 획득 축(20단계)이 여기에도 곱해진다. 처치 골드에만 붙이면 후반에
            // 클리어 보너스의 비중이 상대적으로 줄어들어, 같은 "번 골드"인데
            // 한쪽만 성장에서 빠지는 상태가 된다
            var bonus = StageCurve.ClearGoldForStage(spawner.AverageBaseGold, stage)
                        * BigDouble.FromDouble(UpgradeSystem.CurrentGoldGain);
            if (bonus <= BigDouble.Zero) return BigDouble.Zero;

            var wallet = PlayerWallet.Instance;
            if (wallet != null) wallet.Add(bonus);
            return bonus;
        }

        private void Fail(FailureReason reason)
        {
            LastFailure = reason;
            failureMessage = BuildFailureMessage(reason);

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
        private string BuildFailureMessage(FailureReason reason)
        {
            var dealt = boss != null ? boss.DamageTaken : BigDouble.Zero;
            var maxHealth = boss != null ? boss.MaxHealth : BossMaxHealth;

            var tracks = new UpgradeTrack[0];
            if (upgrades != null)
            {
                tracks = new UpgradeTrack[upgrades.TrackCount];
                for (int i = 0; i < tracks.Length; i++) tracks[i] = upgrades.GetTrack(i);
            }

            if (reason == FailureReason.Death && playerHealth != null)
            {
                double incoming = BossCurve.TotalDamageOverFight(
                    progress != null ? progress.Stage : 1, StageCurve.BossTimeLimitSeconds);

                double effectiveHealth = SurvivalEfficiency.EffectiveHealth(
                    playerHealth.MaxHealth, playerHealth.RegenPerSecond);

                return BossFailureAdvice.DeathMessage(incoming, effectiveHealth, tracks);
            }

            return BossFailureAdvice.Message(maxHealth, dealt, tracks);
        }

        /** 마지막 실패의 이유. 테스트 패널과 보고가 읽는다 */
        public FailureReason LastFailure { get; private set; }

        /**
         * @brief 잡몹 파밍으로 되돌린다.
         *
         * 성공·실패 양쪽이 여기로 온다. 소프트락을 막는 지점이라 경로를 하나로
         * 묶어뒀다 - 어느 한쪽만 스폰을 다시 켜는 것을 잊으면 화면이 영원히 빈다.
         */
        private void ReturnToFarming()
        {
            // 관문은 보스전에만 존재한다. 파밍으로 돌아가면 화면에서 사라져야
            // 하고, 그래야 다음 보스에서 다시 "한 번 나타나는 것"이 된다
            if (gate != null) gate.Hide();

            if (spawner != null) spawner.ResumeSpawning();
            // 체력은 파밍으로 돌아가며 가득 찬다. 회복에 시간을 들이지 않는
            // 이유는 PlayerHealth.EndFight 참고
            if (playerHealth != null) playerHealth.EndFight();
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
