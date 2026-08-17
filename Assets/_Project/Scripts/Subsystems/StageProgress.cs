using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 현재 스테이지와 그 안에서의 처치 수. 그리고 스테이지가 오르는 조건.
     *
     * 스포너가 요괴를 만들 때 여기서 체력·골드 배수를 가져간다.
     *
     * 8단계까지는 10마리를 잡으면 스테이지가 그냥 올라갔다. 9단계에서 그 자리에
     * 보스를 세운다. 잡몹 처치는 이제 스테이지를 올리지 않고 **보스를 여는 데까지만**
     * 쓰이고, 스테이지는 보스를 잡아야 오른다.
     *
     * 이렇게 나눈 이유는 진행에 확인 지점이 필요하기 때문이다. 자동 진행에서는
     * 강화가 뒤처져도 스테이지가 계속 올라가고, 어느 순간 요괴 한 마리에 수십 초가
     * 걸리는데 플레이어는 무엇이 잘못됐는지 알 수 없다. 보스는 그 어긋남을 30초
     * 안에 명시적으로 드러낸다.
     *
     * **할당량을 채운 뒤에도 잡몹은 계속 나온다.** killsThisStage가 상한에서 멈출 뿐
     * 처치와 골드는 그대로다. 보스에서 막힌 플레이어가 강화할 골드를 못 버는 상태로
     * 갇히면 그건 난이도가 아니라 소프트락이다.
     */
    public sealed class StageProgress : MonoBehaviour
    {
        public static StageProgress Instance { get; private set; }

        [SerializeField] private int stage = 1;
        [SerializeField] private int killsThisStage;

        [Tooltip("지금까지 잡은 보스 수. 진행 자체는 stage가 들고 있고 이것은 통계다")]
        [SerializeField] private int bossKillCount;

        /**
         * @brief 최고 도달 스테이지 - **최전선**. 37단계(스테이지 재선택)에 생겼다.
         *
         * 재선택으로 stage가 내려갈 수 있게 되면서 "지금 서 있는 곳"과 "여기까지
         * 왔다"가 갈라졌다. 해금(장비 st11·동료 st31·성장 축)과 업적 도달 지표는
         * 이쪽을 읽어야 한다 - 현재 스테이지를 읽으면 클리어한 지역으로 파밍하러
         * 돌아간 순간 대장간이 다시 잠기고 동료가 화면에서 사라진다.
         */
        [SerializeField] private int maxStageReached = 1;

        /** 스테이지·처치 수·보스 개방 여부가 바뀔 때마다 발생 */
        public event Action Changed;

        public int Stage { get { return stage; } }
        public int KillsThisStage { get { return killsThisStage; } }
        public int KillsRequired { get { return StageCurve.KillsPerStage; } }
        public int BossKillCount { get { return bossKillCount; } }

        /**
         * @brief 최전선. stage보다 작게 보고하지 않는다.
         *
         * 필드가 어긋나는 경로(직렬화 기본값, 옛 테스트 픽스처)에서도 "현재 위치가
         * 곧 최소한의 도달 기록"이라는 성질이 지켜져야 해금 판정이 뒤로 가지 않는다.
         *
         * ## 52단계 - 이 값이 곧 리더보드 점수다 (무한층 도달)
         *
         * 밴드 계약이 도달층으로 옮겨 가며(StageSimulation.ReachContractFrom
         * 주석) 랭킹과 밸런스가 같은 자를 쓰게 됐고, 그 자의 런타임 쪽 값이
         * 이것이다. 성질 셋이 이미 서 있다:
         *
         *   결정론   같은 세이브 상태 -> 같은 값. 난수도 시계도 안 낀다 -
         *            서버(Firebase, 다음 스텝)가 재검증할 수 있는 조건이다
         *   단조     오르기만 한다. 재선택(SelectStage)은 상한이 이 값이고,
         *            복원(SetProgress)은 낮추지 않는다
         *   유일 경로 올리는 곳은 AdvanceStage(보스 처치) 하나뿐이다
         *
         * 변조 세이브의 진짜 거부는 서버 몫이다(같은 세이브를 서버가 다시
         * 시뮬레이션한다). 로컬은 SanityCap이 터무니없는 값만 걸러 UI·퀘스트
         * 산수가 오염되는 것을 막는다.
         */
        public int MaxStageReached { get { return Mathf.Max(maxStageReached, stage); } }

        /**
         * @brief 도달층의 로컬 새니티 상한. **계약이 아니라 오염 방지다.**
         *
         * 시뮬레이션 실측으로 과금 최대 빌드의 벽이 st680 언저리다(52단계 -
         * 성장 축이 전부 하드캡이라 그 뒤는 여유가 지수로 무너진다). 100,000은
         * 그 백 배가 넘는 값이라 정상 플레이가 닿을 수 없고, 세이브 변조로만
         * 온다. 걸리면 잘라서 넣는다 - 세이브 전체를 거부하지 않는 이유는
         * 로컬에서는 어차피 증명이 안 되고(그건 서버 재검증의 일), 남의
         * 진행을 파일 오염 하나로 날리는 쪽이 더 나쁘기 때문이다.
         */
        public const int ReachSanityCap = 100000;

        /**
         * @brief 지금 최전선에 서 있는가.
         *
         * 보스 도전은 최전선에서만 열린다(BossFight.CanChallenge). 클리어한
         * 스테이지에서 보스를 다시 잡을 수 있으면 클리어 보너스·보스 경험치가
         * 반복 수급되고, bossKillCount 같은 통계도 이중으로 오른다. 되돌아간
         * 스테이지는 순수 파밍이고, 복귀는 재선택 화면의 "최전선으로"가 맡는다.
         */
        public bool IsAtFrontier { get { return stage >= MaxStageReached; } }

        /** 할당량을 채워 보스에 도전할 수 있는 상태인가 */
        public bool IsBossReady { get { return killsThisStage >= StageCurve.KillsPerStage; } }

        /**
         * @brief 지금 **귀문이 열려 있는가.** 열려 있으면 그 문의 번호, 아니면 0.
         *
         * 판정은 `PromotionTrialCatalog.PendingGate` 한 곳에 있다 - 순수 함수라
         * 씬 없이 검사할 수 있고, 여기서 `>=` 비교를 다시 쓰면 두 벌이 된다.
         *
         * 경지는 `EvolutionSystem`이 들고 있다. 없으면 0티어로 읽는다 -
         * 전투 전용 테스트 씬에서 이 판정이 예외로 죽지 않게 하기 위해서다.
         */
        public int PendingTrialGate
        {
            get
            {
                var evolution = EvolutionSystem.Instance;
                int tier = evolution != null ? evolution.Tier : 0;

                return PromotionTrialCatalog.PendingGate(
                    stage, MaxStageReached, killsThisStage, bossKillCount, tier);
            }
        }

        public BigDouble HealthMultiplier { get { return StageCurve.HealthMultiplier(stage); } }
        public BigDouble GoldMultiplier { get { return StageCurve.GoldMultiplier(stage); } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Raise();
        }

        /**
         * @brief 잡몹 처치 하나를 기록한다. 할당량을 채우면 보스가 열린다.
         *
         * 상한에서 멈추고 넘치지 않는다. 계속 세면 보스를 한 번 실패한 뒤 파밍하는
         * 동안 숫자가 47/10 같은 모양이 되고, 그 표시는 무엇을 해야 하는지 알려주지
         * 않는다. 멈춘 10/10이 "보스가 기다린다"를 뜻한다.
         *
         * 보스 처치는 여기로 오지 않는다. AdvanceStage가 따로 있다.
         */
        public void RegisterKill()
        {
            if (killsThisStage >= StageCurve.KillsPerStage) return;

            killsThisStage++;
            Raise();
        }

        /**
         * @brief 보스를 잡았다. 다음 스테이지로 넘어간다.
         *
         * 스테이지가 오르는 유일한 경로다. 처치 수를 0으로 되돌리므로 다음 보스는
         * 다시 10마리를 잡아야 열린다.
         */
        public void AdvanceStage()
        {
            stage++;
            if (stage > maxStageReached) maxStageReached = stage;
            killsThisStage = 0;
            bossKillCount++;
            Raise();
        }

        /**
         * @brief 게이트의 **일반 보스**를 잡았다. 스테이지는 아직 안 오른다 (D-4).
         *
         * 귀문이 남아 있으므로 진행을 여기서 멈춘다. 그런데 `bossKillCount`만은
         * 올린다 - 그 한 칸의 어긋남이 **신규 세이브 필드 없이** "보스는 벴는데
         * 문을 못 넘었다"를 기록하는 유일한 수단이다
         * (`PromotionTrialCatalog.PendingGate`).
         *
         * `killsThisStage`를 0으로 안 되돌리는 것도 그 판정의 일부다. 10/10이
         * 유지돼야 앱이 죽었다 살아난 뒤에도 대기 상태로 읽히고, 판정이 어긋나
         * 폴백으로 떨어졌을 때 **보스에 즉시 다시 도전**할 수 있다.
         *
         * 보스의 보상(골드·경험치·클리어 보너스·요도)은 `BossFight`가 평소대로
         * 정확히 한 번 준다 - 진행만 멈추는 것이지 벤 것을 무르는 것이 아니다.
         */
        /**
         * @return 등록됐거나 **이미 등록돼 있으면** true. 손상 상태면 false
         *
         * ## 왜 멱등이어야 하는가
         *
         * 무조건 `bossKillCount++` 하면 콜백이 두 번 오는 날 값이 stage+1이 되고,
         * 그러면 D-4의 복구 조건(`bossKillCount == stage`)이 **영구히** 깨진다.
         * 앱을 껐다 켜면 대기 상태를 잃고, 다음 문에서도 판정이 어긋난다.
         *
         * 그 한 칸이 세이브 필드를 대신하는 값이라, 여기서 한 번 어긋나면
         * 되돌릴 방법이 없다 - 그래서 조건을 만족할 때만 올린다.
         *
         *   정상 등록   최전선 · 게이트 · 할당량 충족 · `bossKillCount == stage - 1`
         *   이미 등록   `bossKillCount == stage`  -> 성공으로 치되 **안 올린다**
         *   그 외       값을 안 바꾸고 false. **자동 돌파하지 않는다**
         */
        public bool RegisterGateBossKill()
        {
            if (stage != MaxStageReached) return false;
            if (killsThisStage != StageCurve.KillsPerStage) return false;
            if (!PromotionTrialCatalog.IsGateStage(stage)) return false;

            // 이미 등록된 상태. 중복 콜백이 여기로 온다 - 성공으로 치되
            // 값을 다시 올리지 않는 것이 이 함수의 전부다
            if (bossKillCount == stage) return true;

            /**
             * @brief 손상된 `bossKillCount`를 **실제 처치를 근거로 정규화한다.**
             *
             * 처음에는 `stage - 1`이 아니면 실패를 냈다. 그런데 그 실패가
             * `BossFight`에서 **귀문 우회**로 떨어졌다 - 게이트 분기를 빠져나와
             * `AdvanceStage`가 돌아 문을 통째로 건너뛰었다. 안전하려던 엄격함이
             * 정반대의 결과를 냈다.
             *
             * 부르는 쪽이 이미 세 가지를 확인했다: 최전선이고, 게이트
             * 스테이지이고, 할당량이 찼다. 그리고 이 함수는 **실제 일반 보스
             * 처치 콜백 안**에서만 불린다 - 그 넷이 맞으면 "방금 이 게이트의
             * 보스를 벴다"는 사실 자체는 참이다.
             *
             * 그러면 `bossKillCount`가 몇이든 그 사실에 맞춰 `stage`로 맞춘다.
             * **공짜 돌파가 아니다** - 플레이어는 여전히 귀문을 이겨야 티어와
             * 진행을 받는다. 정규화가 여는 것은 문이지 통과가 아니다.
             *
             * 값이 올라가는 쪽이든(손상으로 낮았다) 내려가는 쪽이든(stage+1로
             * 부풀었다) 같은 자리로 온다. 어느 쪽도 진행을 앞당기지 않는다.
             */
            bossKillCount = stage;
            Raise();
            return true;
        }

        /**
         * @brief 귀문을 이겼다. 스테이지를 올리되 **`bossKillCount`는 안 올린다** (D-4).
         *
         * 그 칸은 게이트 보스를 벨 때 이미 올랐다. 여기서 또 올리면 정상 불변식
         * (`bossKillCount == maxStageReached - 1`)이 깨진 채로 남아, 다음 문에서
         * 대기 상태 판정이 거짓으로 켜진다.
         *
         * 귀문의 3체는 보스로 세지 않는다 - 그것이 이 함수가 `AdvanceStage`와
         * 갈라져 있는 이유다.
         */
        public void AdvanceAfterTrial()
        {
            stage++;
            if (stage > maxStageReached) maxStageReached = stage;
            killsThisStage = 0;
            Raise();
        }

        /**
         * @brief 재선택이 열리는 최전선. 대장간(EquipmentCurve.UnlockStage)과 같은
         * 지점 - 지역 1을 완주해야 "되돌아갈 여정"이 생긴다.
         *
         * 온보딩 노이즈를 줄이는 것만이 이유가 아니다. **최전선 2에서는 1스테이지
         * 파밍이 경험치/초에서 앞선다** (StageReselectTests가 실측한 유일한 역전 -
         * 초반 경험치 곡선이 처치 속도 하락을 아직 못 따라잡는 구간이다). 게이트를
         * st11에 두면 그 구간에서는 재선택 자체가 없으므로, "최전선이 최적"이
         * 재선택이 존재하는 모든 구간에서 참이 된다.
         */
        public const int ReselectUnlockStage = 11;

        /** 재선택이 열렸는가. 재선택 화면과 SelectStage가 같은 판정을 쓴다 */
        public bool IsReselectUnlocked { get { return MaxStageReached >= ReselectUnlockStage; } }

        /**
         * @brief 스테이지 재선택. 최전선 이하로만 이동한다 (37단계).
         *
         * 미클리어 구간 앞지르기는 클램프로 막는다 - 보스 게이트가 진행의
         * 유일한 상승 경로라는 규칙(AdvanceStage)은 그대로다.
         *
         * 처치 수는 0으로 되돌린다. 되돌아간 스테이지는 보스가 잠겨 있어
         * 할당량이 의미가 없고, 최전선으로 복귀할 때 남아 있던 할당량을
         * 이어받으면 "어느 스테이지에서 채운 10마리인가"가 애매해진다.
         */
        public void SelectStage(int target)
        {
            if (!IsReselectUnlocked) return;

            int clamped = Mathf.Clamp(target, 1, MaxStageReached);
            if (clamped == stage) return;

            stage = clamped;
            killsThisStage = 0;
            Raise();
        }

        /** 재선택 화면의 "최전선으로" */
        public void ReturnToFrontier()
        {
            SelectStage(MaxStageReached);
        }

        /**
         * @brief 세이브 복원용 (v11 이하 - 최전선 기록이 없던 시절).
         *
         * 최전선은 낮추지 않고 현재 스테이지까지만 끌어올린다. 테스트 패널의
         * 지역 점프가 이 경로를 쓰므로, 여기서 최전선을 stage로 덮으면
         * 점프 한 번에 도달 기록이 사라진다.
         */
        public void SetProgress(int savedStage, int savedKills, int savedBossKills)
        {
            SetProgress(savedStage, savedKills, savedBossKills,
                Mathf.Max(maxStageReached, savedStage));
        }

        /** 세이브 복원용 (v12 - 최전선 포함) */
        public void SetProgress(int savedStage, int savedKills, int savedBossKills, int savedMaxStage)
        {
            // 도달층 새니티 (52단계). 현재 스테이지도 함께 자른다 - 최전선만
            // 자르면 stage가 그보다 커서 MaxStageReached가 도로 오염된다
            stage = Mathf.Clamp(savedStage, 1, ReachSanityCap);
            // 상한을 포함해서 클램프한다. 10/10은 유효한 상태이고 "보스가 열려 있다"는
            // 뜻이다. 8단계까지는 이 값이 상한 미만이어야 했는데, 그때는 10에 닿는
            // 순간 스테이지가 올라가 그 상태가 존재하지 않았기 때문이다
            killsThisStage = Mathf.Clamp(savedKills, 0, StageCurve.KillsPerStage);
            bossKillCount = Mathf.Max(0, savedBossKills);
            maxStageReached = Mathf.Min(Mathf.Max(stage, savedMaxStage), ReachSanityCap);
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
