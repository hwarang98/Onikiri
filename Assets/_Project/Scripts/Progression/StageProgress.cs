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

        /** 스테이지·처치 수·보스 개방 여부가 바뀔 때마다 발생 */
        public event Action Changed;

        public int Stage { get { return stage; } }
        public int KillsThisStage { get { return killsThisStage; } }
        public int KillsRequired { get { return StageCurve.KillsPerStage; } }
        public int BossKillCount { get { return bossKillCount; } }

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
            killsThisStage = 0;
            bossKillCount++;
            Raise();
        }

        /** 세이브 복원용 */
        public void SetProgress(int savedStage, int savedKills, int savedBossKills)
        {
            stage = Mathf.Max(1, savedStage);
            // 상한을 포함해서 클램프한다. 10/10은 유효한 상태이고 "보스가 열려 있다"는
            // 뜻이다. 8단계까지는 이 값이 상한 미만이어야 했는데, 그때는 10에 닿는
            // 순간 스테이지가 올라가 그 상태가 존재하지 않았기 때문이다
            killsThisStage = Mathf.Clamp(savedKills, 0, StageCurve.KillsPerStage);
            bossKillCount = Mathf.Max(0, savedBossKills);
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
