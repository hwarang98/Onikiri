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
        /**
         * `Trial`이 3단계에 붙었다 - 귀문(승급전)이 도는 동안의 상태다. 기존
         * 다섯과 나란히 두는 이유는 이 구간이 **보스전이 아니기** 때문이다:
         * 제한 시간도, 클리어 보상도, 스테이지 상승도 다른 규칙을 쓴다.
         *
         * 값을 더한 것뿐이라 기존 판정은 안 움직인다. `CountsAsClear`가 이
         * 값을 포함하지 않는 것이 그 계약이고, `TrialMode`가 꺼져 있으면
         * `phase`가 이 값을 지나지도 않는다.
         */
        public enum Phase { Farming, Intro, Approaching, Fighting, Cleared, Failed, Trial }

        /** 실패한 이유. 문구가 갈린다 */
        public enum FailureReason { TimeOut, Death }

        /**
         * @brief **보스가 이 상태에서 죽으면 클리어로 세는가.**
         *
         * 보스가 실제로 필드에 서 있는 두 상태다. 달려오는 중(Approaching)과
         * 싸우는 중(Fighting) - 사무라이의 사거리가 전선보다 앞까지 닿기 때문에
         * 접근 구간에서도 보스는 맞고 죽는다(OnEnemyKilled 머리 주석).
         *
         * 판정을 순수 함수로 빼둔 이유는 이 집합이 틀렸을 때의 증상이
         * **진행 정지**이기 때문이다. 씬도 풀도 없이 검사할 수 있어야 회귀가
         * 테스트에서 걸린다(BossClearTests).
         */
        public static bool CountsAsClear(Phase phase)
        {
            return phase == Phase.Fighting || phase == Phase.Approaching;
        }

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
                if (phase != Phase.Farming || progress == null) return false;
                if (!progress.IsBossReady || !progress.IsAtFrontier) return false;

                /**
                 * @brief **귀문이 열려 있으면 일반 보스는 못 잡는다.**
                 *
                 * 이 한 줄이 없으면 재도전 경로가 보상 반복이 된다. 귀문에
                 * 실패하고 파밍으로 돌아오면 `killsThisStage`가 10 그대로이고
                 * 최전선이므로 옛 조건이 전부 참이 된다 - 그 버튼으로 일반
                 * 보스를 다시 잡으면
                 *
                 *   골드·경험치·퀘스트·클리어 보너스·요도를 **중복 획득**하고
                 *   `RegisterGateBossKill`이 `bossKillCount`를 stage+1로 만들어
                 *   D-4의 복구 조건(`bossKillCount == stage`)이 깨진다
                 *
                 * 즉 재도전할수록 부유해지고, 앱을 끄면 대기 상태를 잃는다.
                 */
                return progress.PendingTrialGate == 0;
            }
        }

        /**
         * @brief 귀문에 도전할 수 있는가. **일반 보스 도전과 배타다.**
         *
         * 같은 버튼이 상태에 따라 둘 중 하나로 라우팅된다 - 두 버튼을 나란히
         * 두면 "지금 눌러야 할 것"이 둘이 되고, 그중 하나는 반드시 틀린 선택이다.
         */
        public bool CanChallengeTrial
        {
            get { return phase == Phase.Farming && PendingTrialGate > 0; }
        }

        /**
         * @brief 이 귀문을 한 번이라도 시도했는가. 버튼 문구가 갈린다.
         *
         * 재도전은 무료·무제한이지만 **처음 여는 것과 다시 여는 것은 다른
         * 사건**이라 화면에서도 갈라야 한다. 저장하지 않는 값이다 - 앱을 껐다
         * 켜면 "귀문 도전"으로 돌아오고, 그것이 맞다(그 시점의 플레이어에게는
         * 실제로 처음 보는 화면이다).
         */
        public bool TrialAttempted { get; private set; }

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

        /**
         * @brief 보스가 플레이어를 때렸다.
         *
         * 귀문의 3체도 같은 경로를 쓴다 - 적이 주는 피해는 일반 보스와 같은
         * 규칙이어야 하고(소프트캡은 **플레이어 쪽에만** 걸린다), 격노는
         * 공격력 자체를 갈아 끼워 표현하므로 여기 분기가 필요 없다.
         */
        private void OnBossAttacked(Enemy attacker)
        {
            if (playerHealth == null) return;
            if (phase != Phase.Fighting && phase != Phase.Trial) return;

            // 전환 중에는 적이 없다. 죽은 적의 마지막 스윙이 전환으로 넘어와
            // 때리는 경로를 막는다 - "전환 중 적 공격은 없다"가 계약이다
            if (phase == Phase.Trial && (trial == TrialState.Transition1
                                      || trial == TrialState.Transition2)) return;

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
            // 귀문의 사망은 다른 경로다 - 재화도 스테이지도 잃지 않고,
            // 그 자리에서 무료로 다시 도전한다
            if (phase == Phase.Trial) { EndTrial(TrialState.Failure); return; }

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
                    //
                    // 이 줄은 **스폰이 실패한 경우**만 잡는다. 달려오다 맞아
                    // 죽은 보스는 OnEnemyKilled가 먼저 받아 클리어로 보내므로
                    // (같은 프레임에 phase가 Cleared로 바뀐다) 여기 오지 않는다.
                    // 한때는 그 처치가 버려져 이 줄이 스테이지를 안 올린 채
                    // 파밍으로 되돌렸고, 그것이 진행이 막히는 증상이었다
                    if (boss == null || !boss.IsAlive) { ReturnToFarming(); break; }
                    if (boss.CurrentState == Enemy.State.Engaged || timer <= 0f) BeginFight();
                    break;

                case Phase.Fighting:
                    // 보스가 스폰에 실패했거나(정의 누락) 어떤 이유로 사라졌으면
                    // 시계만 도는 상태로 30초를 버리지 않고 즉시 정리한다
                    if (boss == null || !boss.IsAlive) { Fail(FailureReason.TimeOut); break; }
                    if (timer <= 0f) Fail(FailureReason.TimeOut);
                    break;

                case Phase.Trial:
                    UpdateTrial();
                    break;

                case Phase.Cleared:
                    // 배너가 끝나면 귀문이 기다리고 있는지 먼저 본다
                    if (timer <= 0f && pendingTrialGate > 0) { BeginTrial(pendingTrialGate); break; }
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
        /**
         * @brief 보스 외형 한 벌. 정의·확대·틴트가 **함께** 정해져야 한다.
         *
         * 셋이 따로 놀면 "챕터 보스의 큰 스프라이트"에 "잡몹 확대판의 x2"가
         * 곱해지는 조합이 만들어진다. 실제로 그렇게 됐다 - `SpawnTrialFoe`가
         * 정의만 규칙대로 고르고 배율·틴트는 `stageBossScale`·`stageBossTint`를
         * 고정으로 썼고, st40(챕터 보스) 귀문에서 적이 전투 화면을 덮었다.
         */
        private struct BossVisual
        {
            public EnemyDefinition Definition;
            public float Scale;
            public Color Tint;
        }

        /**
         * @brief 지금 스테이지 보스의 외형을 고른다. **일반 보스와 귀문의 공통 출처.**
         *
         * ```
         * 배치 애셋 있음   애셋이 정의·배율·틴트를 전부 정한다
         * 챕터 보스        전용 정의 · 배율 1 · 틴트 없음 (스프라이트가 이미 크다)
         * 그 외            그 스테이지 잡몹 · stageBossScale · stageBossTint
         * ```
         *
         * 확대형인데 `baseMob`이 비어 있으면 그 스테이지의 잡몹을 쓴다 -
         * "이 스테이지의 우두머리"라는 인상은 방금까지 베던 놈이라야 생긴다.
         */
        private BossVisual ResolveBossVisual()
        {
            var visual = new BossVisual();
            var config = CurrentBossConfig;

            if (config != null)
            {
                visual.Definition = config.Definition != null
                    ? config.Definition : StageBossDefinition();
                visual.Scale = config.SpawnScale;
                visual.Tint = config.SpawnTint;
                return visual;
            }

            bool chapter = IsChapterBoss;
            visual.Definition = chapter ? bossDefinition : StageBossDefinition();
            visual.Scale = chapter ? 1f : stageBossScale;
            visual.Tint = chapter ? Color.white : stageBossTint;
            return visual;
        }

        private void BeginApproach()
        {
            if (spawner == null)
            {
                Debug.LogError("[Onikiri] BossFight has no spawner.");
                ReturnToFarming();
                return;
            }

            var config = CurrentBossConfig;

            // 정의·확대·틴트는 **한 곳에서만** 고른다(ResolveBossVisual).
            // 귀문 적도 같은 함수를 지난다 - 두 벌로 두었을 때 챕터 보스가
            // 귀문에서만 두 배로 부풀었다(4단계 §1)
            var visual = ResolveBossVisual();
            EnemyDefinition definition = visual.Definition;
            float scale = visual.Scale;
            Color tint = visual.Tint;

            BigDouble health = BossMaxHealth;
            BigDouble gold = BossGoldReward;
            double attack = BossAttackDamage;

            // 애셋의 추가 배수. 기본 1이라 대개 아무 일도 하지 않는다.
            // **외형이 아니라 값**이라 위 함수에 넣지 않는다 - 귀문은 체력을
            // 카탈로그가 따로 내므로 이 셋을 타면 안 된다
            if (config != null)
            {
                health = config.ApplyHealth(health);
                gold = config.ApplyGold(gold);
                attack = config.ApplyAttack(attack);
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
            // 귀문의 적은 자기 상태 머신이 받는다. 보상 경로는 스포너가 이미
            // 걷어냈고(Enemy.IsTrialFoe), 여기서는 다음 적을 세우는 일만 한다
            if (enemy != null && enemy.IsTrialFoe) { OnTrialFoeKilled(enemy); return; }

            if (enemy == null || !enemy.IsBoss || enemy != boss) return;

            /**
             * @brief **달려오는 중에 죽은 보스도 클리어다.**
             *
             * 여기는 한때 `phase != Phase.Fighting`이면 그냥 돌아갔다. 그 조건은
             * "보스가 죽는 것은 전투 중뿐"이라는 가정 위에 서 있었는데, 그 가정이
             * 참인 적이 없었다.
             *
             * 접근(Approaching)이 끝나는 지점은 보스가 **전선에 닿는** 순간이다
             * (frontLineX = 0.05, Enemy.State.Engaged). 그런데 사무라이의 사거리는
             * 자기 자리(x = -1.2)에서 2.0이라 **x = 0.8까지 닿는다.** 그 사이
             * 0.75칸 - 동료(사거리 2.2~3.5)와 스킬까지 더하면 더 넓다 - 는 보스가
             * 아직 접근 상태인데 이미 맞고 있는 구간이다.
             *
             * 그 구간에서 보스가 죽으면 옛 조건이 처치를 버렸다. 그러면
             * `Update`의 접근 분기가 시체를 보고 파밍으로 되돌리고, 화면에서는
             * **보스를 벴는데 아무 일도 일어나지 않는다** - 스테이지도 안 오르고
             * 클리어 보너스도 혼도 없다. 강할수록 첫 타격이 그 구간에 떨어지므로,
             * 앞서 나간 플레이어일수록 진행이 확실하게 막혔다.
             *
             * 밸런스는 그대로다. 접근 구간의 피해는 원래부터 들어가고 있었고
             * (그래서 거기서 죽을 수 있었다), 보스는 같은 피해를 받고 같은 순간에
             * 죽는다. 달라지는 것은 그 죽음을 세는가뿐이다.
             *
             * 나머지 상태에서는 여전히 돌아간다. Intro에는 보스가 아직 없고,
             * Cleared·Failed·Farming에서는 boss가 null이라 위의 동일성 검사에서
             * 이미 걸리지만, 그 사실에 기대지 않고 여기서 명시한다.
             */
            if (!CountsAsClear(phase)) return;

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

            boss = null;

            /**
             * @brief **게이트 스테이지는 여기서 진행이 멈춘다.** 귀문이 남았다.
             *
             * 보상은 위에서 평소대로 정확히 한 번 줬다 - 골드·경험치는 스포너가,
             * 클리어 보너스와 요도는 이 함수가. 멈추는 것은 **진행뿐**이다.
             *
             * `RegisterGateBossKill`이 `bossKillCount`만 올린다. 그 한 칸의
             * 어긋남이 신규 세이브 필드 없이 "보스는 벴는데 문을 못 넘었다"를
             * 기록하는 유일한 수단이고(D-4), 앱이 여기서 죽어도 그대로 읽힌다.
             */
            int gate = progress != null
                ? Onikiri.Progression.PromotionTrialCatalog.RequiredGateAfterClearing(
                      clearedStage, CurrentTier)
                : 0;

            if (gate > 0)
            {
                /**
                 * @brief **게이트에서는 어떤 경우에도 `AdvanceStage`로 안 떨어진다.**
                 *
                 * 처음에 등록 실패 시 이 분기를 빠져나가게 뒀다가 정반대의
                 * 결과를 냈다 - 아래 `AdvanceStage`가 돌아 **귀문을 통째로
                 * 건너뛰고** 스테이지가 올랐다. "손상 상태에서 자동 돌파 없음"을
                 * 지키려던 코드가 자동 돌파 그 자체였다.
                 *
                 * 이제 갈래가 둘뿐이다:
                 *
                 *   등록 성공(정규화 포함)  ->  귀문 대기
                 *   그 외                   ->  아무것도 안 올리고 파밍 복귀
                 *
                 * 둘 다 `AdvanceStage`를 안 지난다. 진행은 귀문을 이겨야만
                 * 열린다는 규칙이 이 함수에서 예외를 갖지 않는다.
                 */
                bool registered = progress != null && progress.RegisterGateBossKill();

                if (registered)
                {
                    // 클리어 배너를 먼저 보여주고 그 뒤에 귀문이 열린다. 보스를 벤
                    // 사건과 문이 열리는 사건은 다른 것이라 같은 프레임에 겹치면
                    // 둘 다 안 읽힌다
                    pendingTrialGate = gate;
                    timer = clearBannerSeconds;
                    SetPhase(Phase.Cleared);
                    return;
                }

                // 정규화로도 못 살린 상태. 스테이지도 경지도 안 올리고 조용히
                // 돌아간다 - 도전 버튼은 `CanChallenge`가 잠그므로 같은 보스를
                // 반복해 보상을 파밍할 수도 없다
                Debug.LogError(string.Format(
                    "[Onikiri] 게이트 st{0}의 보스를 벴는데 귀문 등록이 실패했다 "
                    + "(최전선 {1} · 처치 {2} · bossKillCount {3}). 진행을 올리지 않고 "
                    + "파밍으로 돌아간다 - 세이브가 손상됐을 수 있다.",
                    clearedStage, progress != null ? progress.MaxStageReached : -1,
                    progress != null ? progress.KillsThisStage : -1,
                    progress != null ? progress.BossKillCount : -1));

                timer = failSeconds;
                SetPhase(Phase.Failed);
                return;
            }

            if (progress != null) progress.AdvanceStage();

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
            // 귀문의 흔적을 남기지 않는다. 어떤 경로로 여기 왔든 배율은 꺼져야
            // 하고(EndTrial이 이미 껐지만 여기가 마지막 그물이다), 상태도
            // Idle로 돌아가야 다음 도전이 깨끗하게 시작한다
            if (Onikiri.Progression.TrialDamageScale.IsActive)
                Onikiri.Progression.TrialDamageScale.Exit();

            trial = TrialState.Idle;
            TrialGate = 0;
            trialFoe = null;
            trialClock = 0f;

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

        // ================================================================ 귀문 (승급전)

        /**
         * @brief 귀문 3연전의 상태. **명시적 열 상태다.**
         *
         * 전환을 "적이 없는 전투 상태"로 뭉뜽그리지 않는 이유는 그 구간에만
         * 걸리는 규칙이 셋이기 때문이다 - 시계는 흐르고, 재생은 계속되고,
         * **적 공격은 없다.** 이름이 없으면 그 셋이 조건문 안에 흩어진다.
         */
        public enum TrialState
        {
            Idle,
            Entering,
            FightingFoe1, Transition1,
            FightingFoe2, Transition2,
            FightingFoe3,
            Victory, Failure, Closed
        }

        private TrialState trial = TrialState.Idle;
        private int pendingTrialGate;
        private Enemy trialFoe;
        private int trialFoeIndex;
        private float trialClock;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /**
         * @brief 같은 구간을 **실시간으로** 잰 값. 계측 전용이다 (승급 5.0단계 §4).
         *
         * 판정에는 한 글자도 안 쓰인다. `trialClock`과 나란히 쌓아 두면 그 비가
         * 곧 `HitStop`이 멈춘 몫이고, 그것이 "화면의 폐쇄 시간이 실제로 몇 초인가"의
         * 답이다 - 3단계는 그 값을 스톱워치로 쟀는데(게임 142초 = 실시간 202초),
         * 기기·프레임마다 다르므로 앱이 직접 적는 것이 맞다.
         *
         * **출시 빌드에는 없다.** 계측이 판정을 지나지 않으므로 컴파일에서 빼도
         * 게임이 한 글자도 달라지지 않고, 남겨 두면 매 프레임 한 번의 덧셈과
         * 로그 문자열 조립이 출시 빌드에 실린다.
         */
        private float trialUnscaledClock;
#endif

        private float trialStateTimer;
        private double trialBaseAttack;
        private int trialEnrageStepsApplied;

        /** 지금 도는 귀문의 상태. HUD가 읽는다 */
        public TrialState Trial { get { return trial; } }

        /** 지금 도는 문의 번호(1~6). 0이면 귀문이 아니다 */
        public int TrialGate { get; private set; }

        /** 귀문 진입 이후 흐른 시간(초). 전환 구간도 포함한다 */
        public float TrialClock { get { return trialClock; } }

        /** 지금까지 오른 격노 단계 */
        public int TrialEnrageSteps
        {
            get { return Onikiri.Progression.PromotionTrialCatalog.EnrageStepsAt(trialClock); }
        }

        /** 폐쇄까지 남은 시간(초) */
        public float TrialSecondsLeft
        {
            get
            {
                float left = (float)Onikiri.Progression.PromotionTrialCatalog.CloseSeconds - trialClock;
                return left > 0f ? left : 0f;
            }
        }

        /** 몇 번째 적인가 (1~3). 화면의 "1/3"이 쓴다 */
        public int TrialFoeNumber { get { return Mathf.Clamp(trialFoeIndex + 1, 1, 3); } }

        /**
         * @brief 지금 상대의 남은 체력 비율(0~1). 상대가 없으면 0.
         *
         * `BossHealthFraction`과 같은 규칙이다 - 죽었거나 아직 안 섰으면 0.
         * 전환 구간에서 `trialFoe`가 null이므로 자연히 0이 되고, HUD는 그때
         * 바를 감춘다(빈 바를 세우면 "0이 됐다"로 읽혀 승리처럼 보인다).
         *
         * **최대 150초짜리 3연전에 이것이 없으면** 플레이어는 자기가 깎고 있는지
         * 알 수 없다 - 실기에서 1/3이 87초 동안 그대로였는데 화면만으로는
         * 진행 중인지 멈춘 건지 구분되지 않았다(3단계 실기 §9.7 결함 B).
         */
        public float TrialFoeHealthFraction
        {
            get { return trialFoe != null && trialFoe.IsAlive ? trialFoe.HealthFraction : 0f; }
        }

        /** 귀문이 지금 열려 있는가 - 진행이 막혀 있다는 뜻이다 */
        public int PendingTrialGate
        {
            get { return progress != null ? progress.PendingTrialGate : 0; }
        }

        private int CurrentTier
        {
            get
            {
                var evolution = Onikiri.Progression.EvolutionSystem.Instance;
                return evolution != null ? evolution.Tier : 0;
            }
        }

        /**
         * @brief 대기 중인 귀문에 들어간다. **재도전과 앱 복귀가 쓰는 진입점.**
         *
         * 조건은 `StageProgress.PendingTrialGate`가 판정한다(D-4) - 저장된
         * 상태에서 유도되므로 앱이 죽었다 살아나도 같은 답을 낸다. 조건이
         * 어긋나면 아무 일도 안 한다: 손상된 세이브에서 문이 저절로 열리지
         * 않게 하는 것이 그 엄격함의 이유이고, 그때는 일반 보스를 다시 잡는
         * 안전한 경로로 떨어진다.
         */
        public void ChallengeTrial()
        {
            if (phase != Phase.Farming) return;

            int gate = PendingTrialGate;
            if (gate <= 0) return;

            BeginTrial(gate);
        }

        /**
         * @brief 귀문을 연다. 적 셋을 세우고 소프트캡을 **한 번** 건다.
         *
         * 점수는 **지금 티어**로 잰다 - 얻으려는 티어가 아니다. 아직 못 받은
         * 배수로 캡을 계산하면 플레이어가 갖지 않은 화력을 기준으로 깎인다.
         */
        private void BeginTrial(int gate)
        {
            pendingTrialGate = 0;
            TrialGate = gate;

            if (spawner == null) { ReturnToFarming(); return; }

            spawner.SuspendSpawning();
            spawner.ClearField();

            // 관문(BossGate)은 세우지 않는다. 그것은 "보스에게 달려간다"의
            // 연출이고 귀문은 달려가는 구간이 없다 - 적이 그 자리에 선다.
            // 인자 이름이 `gate`(문 번호)라 필드를 this로 가리킨다
            if (this.gate != null) this.gate.Hide();

            boss = null;
            trialFoe = null;
            trialFoeIndex = 0;
            trialClock = 0f;
            trialEnrageStepsApplied = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            trialUnscaledClock = 0f;
#endif

            // ---- 소프트캡. 입장 시 한 번, 공통 배율 하나
            var combat = FindObjectOfType<PlayerCombat>();
            double power = combat != null ? combat.ReadTrialPower().Total : 0d;
            double reference = Onikiri.Progression.PromotionTrialCatalog.ReferencePowerForGate(
                spawner.AverageBaseHealth, gate);

            Onikiri.Progression.TrialDamageScale.Enter(
                power, reference, Onikiri.Progression.PromotionTrialCatalog.SoftCapExponent);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 계측 (승급 5.0단계 §5). 실기 보고가 요구하는 값은 로그로만 증명된다 -
            // 화면에는 P도 damageScale도 뜨지 않는다(그것이 SoftCapNotice의 판단이다).
            // **개발 빌드 전용이다** - 출시 빌드에는 이 블록이 컴파일되지 않는다
            Debug.Log(string.Format(
                "[Trial5] enter gate={0} st={1} tier={2} k={3:F2} power={4:E6} ref={5:E6} "
                + "P={6:E6} scale={7:E6} effective={8:E6} maxHp={9:E4} regen={10:E4}",
                gate, CurrentStage, CurrentTier,
                Onikiri.Progression.PromotionTrialCatalog.SoftCapExponent,
                power, reference, reference > 0d ? power / reference : 0d,
                Onikiri.Progression.TrialDamageScale.Current,
                power * Onikiri.Progression.TrialDamageScale.Current,
                playerHealth != null ? playerHealth.MaxHealth : 0d,
                playerHealth != null ? playerHealth.RegenPerSecond : 0d));
#endif

            if (playerHealth != null) playerHealth.BeginFight();

            // 이 문을 한 번이라도 열었다는 사실. 버튼 문구가 "도전"에서
            // "재도전"으로 갈리는 유일한 근거다
            TrialAttempted = true;

            trialStateTimer = trialEntrySeconds;
            trial = TrialState.Entering;
            SetPhase(Phase.Trial);
        }

        [Tooltip("귀문 진입 연출 시간(초). 규칙 안내가 뜨는 구간이다")]
        [SerializeField] private float trialEntrySeconds = 1.2f;

        [Tooltip("귀문 결과 화면을 붙들고 있는 시간(초)")]
        [SerializeField] private float trialResultSeconds = 2.5f;

        private void UpdateTrial()
        {
            float dt = Time.deltaTime;

            switch (trial)
            {
                case TrialState.Entering:
                    trialStateTimer -= dt;
                    if (trialStateTimer <= 0f) SpawnTrialFoe(0);
                    break;

                case TrialState.FightingFoe1:
                case TrialState.FightingFoe2:
                case TrialState.FightingFoe3:
                    trialClock += dt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    trialUnscaledClock += Time.unscaledDeltaTime;
#endif
                    ApplyEnrage();

                    /**
                     * @brief **폐쇄를 적 참조보다 먼저 본다.**
                     *
                     * 순서가 반대였을 때 무한 정지가 있었다. 적 참조가 사라지면
                     * `break`로 빠져나가 폐쇄 검사에 영영 도달하지 못했고,
                     * 그러면 `Phase.Trial`에 갇혀 게임이 멈춘다 - 소프트캡도
                     * 켜진 채로 남는다.
                     *
                     * 시계는 어떤 경우에도 흐르므로 이 줄이 종료를 보장한다.
                     */
                    if (trialClock >= (float)Onikiri.Progression.PromotionTrialCatalog.CloseSeconds)
                    { EndTrial(TrialState.Closed); break; }

                    /**
                     * @brief 적이 없으면 **즉시 안전 실패.**
                     *
                     * 정상 처치는 `OnTrialFoeKilled`가 먼저 상태를 Transition이나
                     * Victory로 바꾸므로 여기 오지 않는다. 그래도 여기 오면
                     * 스폰이 실패했거나 콜백이 누락된 것이고, 둘 다 플레이어가
                     * 이길 수 없는 상태다 - 기다리게 두지 않고 끝낸다.
                     *
                     * 실패는 아무것도 뺏지 않으므로(재화·스테이지·티어 유지)
                     * 이 안전 실패의 대가는 재도전 한 번뿐이다.
                     */
                    if (trialFoe == null || !trialFoe.IsAlive)
                        EndTrial(TrialState.Failure);
                    break;

                case TrialState.Transition1:
                case TrialState.Transition2:
                    // **시계와 재생은 전환에도 흐른다.** 전환을 늘려 폐쇄 시간을
                    // 버는 회피를 막고, 재생 축이 이 구간에서만 죽지 않게 한다
                    trialClock += dt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    trialUnscaledClock += Time.unscaledDeltaTime;
#endif
                    trialStateTimer -= dt;

                    if (trialClock >= (float)Onikiri.Progression.PromotionTrialCatalog.CloseSeconds)
                    { EndTrial(TrialState.Closed); break; }

                    if (trialStateTimer <= 0f) SpawnTrialFoe(trialFoeIndex + 1);
                    break;

                case TrialState.Victory:
                case TrialState.Failure:
                case TrialState.Closed:
                    trialStateTimer -= dt;
                    if (trialStateTimer <= 0f) ReturnToFarming();
                    break;
            }
        }

        /**
         * @brief 격노. 90초부터 10초마다 적 공격력에 x1.3이 **누적**된다.
         *
         * 단계가 바뀐 순간에만 갈아 끼운다 - 매 프레임 곱하면 지수가 프레임
         * 수만큼 쌓인다. 기준은 항상 스폰 시점의 값(`trialBaseAttack`)이라
         * 누적이 어디서 시작했는지가 한 값에 남는다.
         */
        private void ApplyEnrage()
        {
            int steps = Onikiri.Progression.PromotionTrialCatalog.EnrageStepsAt(trialClock);
            if (steps == trialEnrageStepsApplied || trialFoe == null) return;

            trialEnrageStepsApplied = steps;
            trialFoe.SetAttackDamage(
                trialBaseAttack * Onikiri.Progression.PromotionTrialCatalog.EnrageMultiplierAt(trialClock));

            Raise();
        }

        /** 적 `index`(0~2)를 세운다. 체력은 카탈로그가 낸다 */
        private void SpawnTrialFoe(int index)
        {
            trialFoeIndex = index;

            // **일반 보스와 같은 규칙을 지난다.** 정의만 규칙대로 고르고
            // 배율·틴트를 고정으로 쓰던 것이 4단계 §1에서 고쳐진 결함이다
            var visual = ResolveBossVisual();

            if (visual.Definition == null || spawner == null) { EndTrial(TrialState.Failure); return; }

            var health = Onikiri.Progression.PromotionTrialCatalog.FoeHealth(
                spawner.AverageBaseHealth, TrialGate, index);

            trialBaseAttack = BossAttackDamage;
            trialEnrageStepsApplied = 0;

            // 보상은 0으로 스폰하고, 퀘스트·할당량은 IsTrialFoe가 막는다.
            // 두 겹인 이유는 값과 카운터가 서로 다른 경로이기 때문이다
            trialFoe = spawner.SpawnBoss(visual.Definition, health, BigDouble.Zero, BigDouble.Zero,
                                         visual.Scale, visual.Tint, trialBaseAttack,
                                         false);

            if (trialFoe != null)
            {
                trialFoe.ConfigureAsTrialFoe(
                    (float)Onikiri.Progression.PromotionTrialCatalog.FoeAttackIntervalSeconds,
                    (float)Onikiri.Progression.PromotionTrialCatalog.FirstAttackDelaySeconds);

                trialFoe.Attacked += OnBossAttacked;
                trialFoe.SwingStarted += OnBossSwing;
            }

            // 격노가 이미 올라 있는 시각에 세워지면 그 배수부터 시작한다
            ApplyEnrage();

            trial = index == 0 ? TrialState.FightingFoe1
                  : index == 1 ? TrialState.FightingFoe2
                               : TrialState.FightingFoe3;
            Raise();
        }

        private void OnTrialFoeKilled(Enemy enemy)
        {
            if (phase != Phase.Trial || enemy != trialFoe) return;

            trialFoe = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 계측 (5.0단계 §3의 "1·2·3체별 시간"). 개발 빌드 전용
            Debug.Log(string.Format("[Trial5] foe {0}/3 down  game={1:F2}s real={2:F2}s enrage={3}",
                trialFoeIndex + 1, trialClock, trialUnscaledClock, TrialEnrageSteps));
#endif

            if (trialFoeIndex >= Onikiri.Progression.PromotionTrialCatalog.FoeCount - 1)
            {
                EndTrial(TrialState.Victory);
                return;
            }

            trialStateTimer = (float)Onikiri.Progression.PromotionTrialCatalog.SwapSeconds;
            trial = trialFoeIndex == 0 ? TrialState.Transition1 : TrialState.Transition2;
            Raise();
        }

        /**
         * @brief 귀문이 끝났다. **승리·사망·폐쇄가 전부 이 한 경로로 온다.**
         *
         * 소프트캡을 끄는 자리가 하나여야 하기 때문이다 - 어느 한쪽만 끄기를
         * 잊으면 배율이 일반 스테이지까지 따라 나가고, 플레이어의 화력이
         * 영구히 깎인 채로 남는다.
         *
         * 승리에서만 티어와 스테이지가 오른다. 그 둘이 **각각 정확히 한 번**
         * 오르는 것이 이 함수의 계약이고, `GrantTrialVictory`가 멱등이라
         * 중복 콜백에서도 두 번 오르지 않는다.
         */
        private void EndTrial(TrialState result)
        {
            if (trial == TrialState.Victory || trial == TrialState.Failure
                || trial == TrialState.Closed) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 계측 (5.0단계 §4·§5). **`Exit()` 앞이다** - 배율과 감사 장부를
            // 아직 읽을 수 있는 유일한 지점이다. 개발 빌드 전용
            Debug.Log(string.Format(
                "[Trial5] end gate={0} result={1} game={2:F2}s real={3:F2}s ratio={4:F4} "
                + "enrage={5} hpLeft={6:E4} scale={7:E6} raw={8} scaled={9} hits={10}",
                TrialGate, result, trialClock, trialUnscaledClock,
                trialUnscaledClock > 0f ? trialClock / trialUnscaledClock : 0f,
                TrialEnrageSteps, playerHealth != null ? playerHealth.Current : 0d,
                Onikiri.Progression.TrialDamageScale.Current,
                Onikiri.Progression.TrialDamageScale.RawTotal,
                Onikiri.Progression.TrialDamageScale.ScaledTotal,
                Onikiri.Progression.TrialDamageScale.HitCount));
#endif

            Onikiri.Progression.TrialDamageScale.Exit();

            if (spawner != null) spawner.ClearField();
            trialFoe = null;

            if (result == TrialState.Victory)
            {
                var evolution = Onikiri.Progression.EvolutionSystem.Instance;
                bool raised = evolution != null && evolution.GrantTrialVictory(TrialGate);

                // 티어가 실제로 오른 경우에만 진행을 연다. 이미 갖고 있던
                // 문을 다시 이긴 것이라면(재도전) 스테이지는 그대로다 -
                // 같은 문으로 두 칸을 오를 수 없다는 것이 이 조건이다
                if (raised && progress != null) progress.AdvanceAfterTrial();

                // 즉시 저장한다. 티어와 스테이지가 함께 오른 순간이 이 재설계에서
                // 되돌릴 수 없는 유일한 지점이라, 여기서 앱이 죽으면 무엇을
                // 잃었는지가 애매해진다
                var session = FindObjectOfType<Onikiri.Progression.GameSession>();
                if (session != null) session.Save();
            }

            trialStateTimer = trialResultSeconds;
            trial = result;
            Raise();
        }

        /**
         * @brief **정적 배율이 이 컴포넌트보다 오래 살지 못하게 한다.**
         *
         * `TrialDamageScale`은 정적이라 씬이나 컴포넌트의 수명과 무관하다.
         * 귀문 도중에 이 컴포넌트가 꺼지면(씬 전환, `SetActive(false)`,
         * 오브젝트 파괴, 앱 종료) 배율이 켜진 채로 남고, 그러면 다음 일반
         * 전투의 화력이 조용히 깎인다.
         *
         * `OnDisable`이 **가장 중요한 그물**이다 - 씬 전환과 비활성화가 전부
         * 그것을 지나고, `OnDestroy`는 그중 일부에서만 온다. 셋을 다 거는
         * 이유는 어느 하나가 안 오는 플랫폼·경로가 있기 때문이고, `Exit`이
         * 멱등이라 여러 번 불려도 안전하다.
         */
        private void OnDisable()
        {
            AbandonTrialIfRunning();

            if (spawner != null) spawner.EnemyKilled -= OnEnemyKilled;
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (progress != null) progress.Changed -= Raise;
        }

        /**
         * @brief 귀문 도중에 꺼졌다면 **시도를 통째로 중단한다.**
         *
         * ## 배율만 끄는 것으로는 모자랐다
         *
         * 처음에는 `TrialDamageScale.Exit()` 한 줄이었다. 그러면 다시 켰을 때
         * `phase`는 여전히 `Trial`이고 `trial`도 전투 상태인데 **소프트캡만
         * 꺼져 있다** - 캡 없이 귀문을 계속 도는 상태이고, 적은 이미 사라졌으니
         * 폐쇄까지 아무 일도 안 일어난다.
         *
         * 그래서 시도 자체를 접는다. 실패와 같은 처리이므로 잃는 것은 없다.
         *
         * ## D-4 대기는 **유지한다**
         *
         * `bossKillCount`를 안 건드리므로 `PendingTrialGate`가 그대로 열려
         * 있다. 다시 켜면 `CanChallengeTrial`이 참이고 `CanChallenge`는
         * 거짓이라, 플레이어는 일반 보스를 다시 잡지 않고 귀문만 무료로
         * 재도전한다 - 앱이 죽었다 살아난 것과 같은 상태다.
         */
        private void AbandonTrialIfRunning()
        {
            ReleaseTrialScale();

            if (phase != Phase.Trial) return;

            // 적을 치운다. 남겨두면 다시 켰을 때 필드에 귀문의 적이 서 있고,
            // 그것은 보상도 안 주면서 잡몹 큐 앞을 막는다
            if (spawner != null)
            {
                spawner.ClearField();
                spawner.ResumeSpawning();
            }

            if (playerHealth != null) playerHealth.EndFight();
            if (this.gate != null) this.gate.Hide();

            trialFoe = null;
            trial = TrialState.Idle;
            TrialGate = 0;
            trialClock = 0f;
            pendingTrialGate = 0;

            phase = Phase.Farming;
        }

        private void OnDestroy()
        {
            ReleaseTrialScale();
        }

        private void OnApplicationQuit()
        {
            ReleaseTrialScale();
        }

        /** 배율을 끄는 한 줄. 여러 번 불려도 안전하다 */
        private void ReleaseTrialScale()
        {
            if (Onikiri.Progression.TrialDamageScale.IsActive)
                Onikiri.Progression.TrialDamageScale.Exit();
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
