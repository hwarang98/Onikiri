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
