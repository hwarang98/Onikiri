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
         */
        public int MaxStageReached { get { return Mathf.Max(maxStageReached, stage); } }

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
            stage = Mathf.Max(1, savedStage);
            // 상한을 포함해서 클램프한다. 10/10은 유효한 상태이고 "보스가 열려 있다"는
            // 뜻이다. 8단계까지는 이 값이 상한 미만이어야 했는데, 그때는 10에 닿는
            // 순간 스테이지가 올라가 그 상태가 존재하지 않았기 때문이다
            killsThisStage = Mathf.Clamp(savedKills, 0, StageCurve.KillsPerStage);
            bossKillCount = Mathf.Max(0, savedBossKills);
            maxStageReached = Mathf.Max(stage, savedMaxStage);
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
