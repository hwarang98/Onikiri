using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 플레이어의 재화.
     *
     * 골드는 첫 코인부터 BigDouble이다. 나중에 long에서 승격시키는 방식이 아니다.
     * 방치형은 플레이 몇 시간 만에 합계가 long 범위를 넘어가고, 그때 가서 숫자 타입을
     * 바꾸면 잔액을 읽는 모든 시스템을 손대야 한다.
     */
    public sealed class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }

        [SerializeField] private BigDouble gold;

        /** 잔액이 바뀔 때마다 새 합계와 함께 발생 */
        public event Action<BigDouble> GoldChanged;

        /** 지금까지 획득한 누적 총액. 나중에 환생 계산에 쓴다 */
        public BigDouble LifetimeGold { get; private set; }

        public BigDouble Gold { get { return gold; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Raise();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Add(BigDouble amount)
        {
            if (amount.IsZero || amount.IsNegative) return;

            gold += amount;
            LifetimeGold += amount;
            Raise();

            // 퀘스트의 "골드 획득"은 **여기 하나에서** 센다.
            //
            // 골드가 들어오는 길은 여럿이다 - 처치(EnemySpawner), 보스 보상과
            // 클리어 보너스(BossFight), 방치 보상(GameSession), 업적 수령
            // (QuestSystem). 그 넷에 각각 훅을 걸면 하나를 빠뜨리거나 두 번 세는
            // 날이 오고, 증상은 "퀘스트 진행이 조금 안 맞는다"라 원인을 찾기 어렵다.
            //
            // Add는 그 전부가 지나는 유일한 문이다. 잔액 복원(SetBalance)은 여기를
            // 지나지 않으므로 세이브를 불러올 때 카운터가 부풀지도 않는다
            var quests = QuestSystem.Instance;
            if (quests != null) quests.ReportGoldEarned(amount);
        }

        public bool CanAfford(BigDouble cost)
        {
            return gold >= cost;
        }

        /** 감당 가능할 때만 소비한다. 구매가 성사됐는지를 반환 */
        public bool TrySpend(BigDouble cost)
        {
            if (cost.IsNegative || !CanAfford(cost)) return false;

            gold -= cost;
            Raise();
            return true;
        }

        /** 세이브/로드용. 획득이 아니라 잔액을 복원하는 경로 */
        public void SetBalance(BigDouble value, BigDouble lifetime)
        {
            gold = value;
            LifetimeGold = lifetime;
            Raise();
        }

        private void Raise()
        {
            var handler = GoldChanged;
            if (handler != null) handler(gold);
        }
    }
}
