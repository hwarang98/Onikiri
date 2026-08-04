using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 강화 한 줄. 레벨, 다음 비용, 현재 효과값을 계산한다.
     *
     * MonoBehaviour가 아니라 직렬화 가능한 순수 클래스다. 곡선 계산에 씬이나 프레임이
     * 필요 없고, 밸런싱이 어긋나면 폰에서 몇 시간 방치한 뒤가 아니라 테스트에서 먼저
     * 드러나야 하기 때문이다.
     *
     * 비용은 항상 지수 곡선이다. 방치형의 진행은 "다음 것이 늘 조금 더 멀다"로 만들어지며,
     * 선형 비용이면 골드 획득량이 조금만 올라도 남은 레벨을 한 번에 전부 사버린다.
     */
    [Serializable]
    public sealed class UpgradeTrack
    {
        /**
         * @brief 레벨이 오를 때 효과값이 자라는 방식.
         *
         * 둘 다 필요하다. 공격력은 지수로 커져야 후반의 요괴 체력을 따라가고,
         * 공격속도는 선형이어야 한다. 공격속도를 지수로 두면 몇 십 레벨 만에
         * 프레임당 여러 번 공격하는 값이 되어 의미를 잃는다.
         */
        public enum Curve
        {
            Additive,
            Multiplicative
        }

        [SerializeField] private string id;
        [SerializeField] private string displayName;

        [Tooltip("현재 레벨. 1부터 시작한다")]
        [SerializeField] private int level = 1;

        [Tooltip("최대 레벨. 0이면 상한 없음")]
        [SerializeField] private int maxLevel;

        [Header("비용")]
        [SerializeField] private BigDouble baseCost = BigDouble.FromDouble(10d);

        [Tooltip("레벨당 비용 배수. 1.15면 레벨 20마다 약 16배")]
        [SerializeField] private double costGrowth = 1.15d;

        [Header("효과")]
        [SerializeField] private Curve curve = Curve.Multiplicative;
        [SerializeField] private BigDouble baseValue = BigDouble.One;

        [Tooltip("Additive면 레벨당 더할 값, Multiplicative면 레벨당 곱할 배수")]
        [SerializeField] private double step = 1.12d;

        public string Id { get { return id; } }
        public string DisplayName { get { return displayName; } }
        public int Level { get { return level; } }
        public int MaxLevel { get { return maxLevel; } }

        public bool IsMaxed { get { return maxLevel > 0 && level >= maxLevel; } }

        /**
         * @brief 다음 레벨의 비용.
         *
         * 레벨 1에서 baseCost이고, 이후 레벨마다 costGrowth를 곱한다.
         */
        public BigDouble Cost
        {
            get { return baseCost * BigDouble.Pow(BigDouble.FromDouble(costGrowth), level - 1); }
        }

        /** 현재 레벨에서의 효과값 */
        public BigDouble Value
        {
            get { return ValueAtLevel(level); }
        }

        public BigDouble ValueAtLevel(int atLevel)
        {
            int steps = Mathf.Max(0, atLevel - 1);
            return curve == Curve.Additive
                ? baseValue + BigDouble.FromDouble(step) * steps
                : baseValue * BigDouble.Pow(BigDouble.FromDouble(step), steps);
        }

        /**
         * @brief 지갑이 감당할 수 있으면 한 레벨 올린다.
         *
         * 구매가 성사됐는지를 반환한다. 지불과 레벨 상승을 한 곳에 둔 이유는, 둘을
         * 호출부에 맡기면 언젠가 한쪽만 실행되는 경로가 생기기 때문이다.
         */
        public bool TryPurchase(PlayerWallet wallet)
        {
            if (wallet == null || IsMaxed) return false;

            if (!wallet.TrySpend(Cost)) return false;

            level++;
            return true;
        }

        /** 세이브/로드용 */
        public void SetLevel(int value)
        {
            level = Mathf.Max(1, maxLevel > 0 ? Mathf.Min(value, maxLevel) : value);
        }

        /**
         * @brief 테스트와 에디터 빌더가 쓰는 생성자.
         *
         * 인스펙터 직렬화에는 기본 생성자가 필요하므로 이쪽은 추가 생성자다.
         */
        public UpgradeTrack() { }

        public UpgradeTrack(string id, string displayName,
                            BigDouble baseCost, double costGrowth,
                            Curve curve, BigDouble baseValue, double step,
                            int maxLevel = 0)
        {
            this.id = id;
            this.displayName = displayName;
            this.baseCost = baseCost;
            this.costGrowth = costGrowth;
            this.curve = curve;
            this.baseValue = baseValue;
            this.step = step;
            this.maxLevel = maxLevel;
            level = 1;
        }
    }
}
