using System;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 보석. 퀘스트가 주는 **리텐션 재화**다.
     *
     * ## 왜 골드와 다른 타입인가
     *
     * 골드는 첫 코인부터 BigDouble이다(PlayerWallet 주석) - 방치형에서 몇 시간이면
     * long을 넘기기 때문이다. 보석은 그 반대다. **파밍으로 늘지 않고** 퀘스트를
     * 완료해야만 들어오므로, 하루에 수십 개 단위로 자란다. 평생 플레이해도 long의
     * 근처에도 가지 않는다.
     *
     * 타입을 나누는 것이 그 사실을 코드에 적어두는 방법이다. BigDouble로 두면
     * "언젠가 파밍으로 폭증할 수 있는 값"처럼 보이고, 그러면 상점 가격을 지수로
     * 설계하고 싶어진다 - 그 순간 보석은 두 번째 골드가 된다.
     *
     * ## 파워가 아니다
     *
     * 보석은 DPS로 환산되지 않는다. 그래서 보스 여유 밴드와 무관하고
     * StageSimulation에 들어가지 않는다. **이 사실이 깨지는 날**(보석으로 공격력을
     * 사는 상점이 생기는 날) 시뮬레이션에 편입해야 한다.
     *
     * ## 지금은 쓸 곳이 없다
     *
     * 31단계에서는 쌓이기만 한다. 소비처(상점/전직/펫)는 다음 pillar다.
     * 20단계의 골드 축이 "사도 이득이 없는 축"이었던 것과 같은 함정이 될 수 있어서,
     * 화면에 **"곧 상점에서 사용"**을 명시한다 - 쌓이는 것이 보이고 쓸 곳이 온다는
     * 것을 알면 죽은 재화가 아니라 예고가 된다.
     */
    public sealed class GemWallet : MonoBehaviour
    {
        public static GemWallet Instance { get; private set; }

        [SerializeField] private long gems;

        /** 잔액이 바뀔 때마다 새 합계와 함께 발생 */
        public event Action<long> GemsChanged;

        public long Gems { get { return gems; } }

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

        public void Add(long amount)
        {
            if (amount <= 0L) return;

            // long이 넘칠 일은 없지만, 손상된 세이브가 long.MaxValue 근처를 들고
            // 오면 덧셈이 음수로 돌아 잔액이 사라진다. 저장된 값을 그대로 믿지
            // 않는 것은 SaveData.LastQuitUtc가 시각 범위를 확인하는 것과 같은 규칙이다
            if (gems > long.MaxValue - amount) gems = long.MaxValue;
            else gems += amount;

            Raise();
        }

        public bool CanAfford(long cost)
        {
            return cost >= 0L && gems >= cost;
        }

        /** 감당 가능할 때만 소비한다. 소비처는 다음 pillar에서 붙는다 */
        public bool TrySpend(long cost)
        {
            if (!CanAfford(cost)) return false;

            gems -= cost;
            Raise();
            return true;
        }

        /** 세이브/로드용. 획득이 아니라 잔액을 복원하는 경로 */
        public void SetBalance(long value)
        {
            gems = value < 0L ? 0L : value;
            Raise();
        }

        private void Raise()
        {
            var handler = GemsChanged;
            if (handler != null) handler(gems);
        }
    }
}
