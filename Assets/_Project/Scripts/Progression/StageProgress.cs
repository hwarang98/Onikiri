using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 현재 스테이지와 그 안에서의 처치 수.
     *
     * 스포너가 요괴를 만들 때 여기서 체력·골드 배수를 가져간다. 스테이지가 오르는
     * 유일한 경로도 여기다.
     */
    public sealed class StageProgress : MonoBehaviour
    {
        public static StageProgress Instance { get; private set; }

        [SerializeField] private int stage = 1;
        [SerializeField] private int killsThisStage;

        /** 스테이지나 처치 수가 바뀔 때마다 발생 */
        public event Action Changed;

        public int Stage { get { return stage; } }
        public int KillsThisStage { get { return killsThisStage; } }
        public int KillsRequired { get { return StageCurve.KillsPerStage; } }

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
         * @brief 처치 하나를 기록한다. 필요 수를 채우면 스테이지가 오른다.
         *
         * 이미 필드에 나와 있는 요괴의 체력은 올리지 않는다. 스폰 시점에 배수가
         * 적용되므로, 스테이지가 오르는 순간 화면의 요괴가 갑자기 단단해지지 않고
         * 다음에 들어오는 것부터 강해진다.
         */
        public void RegisterKill()
        {
            killsThisStage++;
            if (killsThisStage >= StageCurve.KillsPerStage)
            {
                killsThisStage = 0;
                stage++;
            }
            Raise();
        }

        /** 세이브 복원용 */
        public void SetProgress(int savedStage, int savedKills)
        {
            stage = Mathf.Max(1, savedStage);
            killsThisStage = Mathf.Clamp(savedKills, 0, StageCurve.KillsPerStage - 1);
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
