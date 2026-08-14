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

        /**
         * @brief 효과값을 화면에 어떻게 읽히게 할 것인가.
         *
         * 축마다 단위가 다르다. 공격력 3.21M, 공격속도 3.88, 치명타 확률 12.0%,
         * 치명타 피해 x2.00. 숫자만 찍으면 확률 0.12가 "0.12"로 나와서 무엇의
         * 0.12인지 알 수 없다.
         */
        public enum Display
        {
            Plain,
            Percent,
            Multiplier,
            PerSecond
        }

        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Display display = Display.Plain;

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

        /**
         * @brief 효과값의 상한. 0이면 없음.
         *
         * maxLevel과 다른 것이다. maxLevel은 **더 팔지 않는다**는 뜻이고, 이것은
         * **더 세지지 않는다**는 뜻이다. 보통은 둘이 같은 곳에서 나오지만
         * (공격속도는 아트가 정한 상한 하나에서 둘 다 유도된다), 세이브를 복원할 때
         * 갈라진다.
         *
         * 예전 세이브에 상한보다 높은 레벨이 들어 있을 수 있다. 그 레벨을 잘라버리면
         * 플레이어가 산 것이 사라지고, 나중에 상한이 올라가도 돌아오지 않는다.
         * 레벨은 그대로 두고 효과만 여기서 막으면, 상한이 오르는 순간 그 레벨이
         * 곧바로 제 값을 낸다.
         */
        [Tooltip("효과값의 상한. 0이면 없음. maxLevel과 달리 구매가 아니라 값을 막는다")]
        [SerializeField] private double valueCeiling;

        /**
         * @brief 관문 - 두 번째 비용 구간 (43단계, 치명타 확률 전용).
         *
         * 균등한 값 곡선(레벨당 +0.088%p) 위에 비용만 두 결이다: 60% 문턱
         * 앞까지는 코리더 시절의 곡선을 미세화한 완만한 결, 문턱부터는
         * 도약(wallJump) 뒤 가파른 결(wallGrowth). 값 곡선은 하나인데 비용이
         * 갈리는 이유는 CritRateCurve.WallJump 주석에 있다 - 60%의 벽을
         * 뚫는 수련은 앞의 545칸과 차원이 다르다.
         *
         * wallFromLevel은 **비용 인덱스**다(CostAtLevel(L) = L+1로 가는 가격,
         * 이 클래스의 관례). 0이면 관문 없음 - 나머지 여덟 축이 그쪽이다.
         */
        [Header("관문")]
        [Tooltip("이 비용 인덱스부터 두 번째 구간. 0이면 관문 없음")]
        [SerializeField] private int wallFromLevel;
        [SerializeField] private double wallJump = 1d;
        [SerializeField] private double wallGrowth = 1d;

        public string Id { get { return id; } }
        public string DisplayName { get { return displayName; } }
        public int Level { get { return level; } }
        public int MaxLevel { get { return maxLevel; } }

        public bool IsMaxed { get { return maxLevel > 0 && level >= maxLevel; } }

        /**
         * @brief 효과값 하나를 사람이 읽는 문자열로.
         *
         * 소수 둘째 자리까지 두는 이유는 버튼이 보여줘야 할 변화가 그 자리에서
         * 일어나기 때문이다. 첫째 자리로는 공격속도 1.15 -> 1.27이 둘 다 1.2로
         * 뭉개지고, 치명타 확률 12.0% -> 12.5%는 아예 사라진다.
         */
        public string Format(BigDouble value)
        {
            switch (display)
            {
                case Display.Percent:
                    return (value.ToDouble() * 100d).ToString("F1") + "%";
                case Display.Multiplier:
                    return "x" + NumberFormatter.FormatStat(value, 2);
                case Display.PerSecond:
                    // 최대 체력 대비 비율이다. 절대량으로 읽히면 "1/s"가
                    // 체력 100에서도 100000에서도 같아 보인다
                    return (value.ToDouble() * 100d).ToString("F1") + "%/s";
                default:
                    return NumberFormatter.FormatStat(value, 2);
            }
        }

        /**
         * @brief 다음 레벨의 비용.
         *
         * 레벨 1에서 baseCost이고, 이후 레벨마다 costGrowth를 곱한다.
         */
        public BigDouble Cost
        {
            get { return CostAtLevel(level); }
        }

        /** 임의 레벨의 비용. 밸런스 비교가 현재 레벨에 묶이지 않게 한다 */
        public BigDouble CostAtLevel(int atLevel)
        {
            int steps = Mathf.Max(0, atLevel - 1);

            // 관문 앞(또는 관문 없음): 단일 지수 그대로.
            // 정수화(최소 1골드, E-3 수정)는 곡선 statics와 같은 규칙이어야
            // 한다 - UpgradeCost.Quantize가 단일 출처다
            if (wallFromLevel <= 0 || atLevel < wallFromLevel)
                return UpgradeCost.Quantize(
                    baseCost * BigDouble.Pow(BigDouble.FromDouble(costGrowth), steps));

            // 관문 뒤: 앞 구간의 끝값 x 도약 x 두 번째 결.
            // CritRateCurve.CostAtLevel과 같은 식이어야 한다 - 두 곳이 갈리면
            // 시뮬레이션이 화면과 다른 가격을 잰다
            return UpgradeCost.Quantize(baseCost
                * BigDouble.Pow(BigDouble.FromDouble(costGrowth), wallFromLevel - 1)
                * BigDouble.FromDouble(wallJump)
                * BigDouble.Pow(BigDouble.FromDouble(wallGrowth), atLevel - wallFromLevel));
        }

        /** 관문 설정. 빌더가 곡선 상수에서 옮겨 적는다 */
        public void SetWall(int fromLevel, double jump, double growth)
        {
            wallFromLevel = Mathf.Max(0, fromLevel);
            wallJump = jump;
            wallGrowth = growth;
        }

        /** 현재 레벨에서의 효과값 */
        public BigDouble Value
        {
            get { return ValueAtLevel(level); }
        }

        public BigDouble ValueAtLevel(int atLevel)
        {
            int steps = Mathf.Max(0, atLevel - 1);
            var raw = curve == Curve.Additive
                ? baseValue + BigDouble.FromDouble(step) * steps
                : baseValue * BigDouble.Pow(BigDouble.FromDouble(step), steps);

            if (valueCeiling <= 0d) return raw;

            var ceiling = BigDouble.FromDouble(valueCeiling);
            return raw > ceiling ? ceiling : raw;
        }

        /** 상한을 무시한 곡선 그대로의 값. 상한이 올라갔을 때 무엇이 돌아오는지를 본다 */
        public BigDouble UncappedValueAtLevel(int atLevel)
        {
            int steps = Mathf.Max(0, atLevel - 1);
            return curve == Curve.Additive
                ? baseValue + BigDouble.FromDouble(step) * steps
                : baseValue * BigDouble.Pow(BigDouble.FromDouble(step), steps);
        }

        /** 지금 레벨의 효과가 상한에 막혀 있는가. UI가 "MAX"를 판단할 때 쓴다 */
        public bool IsValueCapped
        {
            get { return valueCeiling > 0d && UncappedValueAtLevel(level) > BigDouble.FromDouble(valueCeiling); }
        }

        /**
         * @brief 상한을 올린다. 아트나 규칙이 바뀌었을 때 곡선을 다시 열어주는 경로.
         *
         * 레벨을 건드리지 않으므로, 상한 위에 잠들어 있던 레벨이 그대로 깨어난다.
         */
        public void SetValueCeiling(double value, int newMaxLevel)
        {
            valueCeiling = Mathf.Max(0f, (float)value);
            maxLevel = Mathf.Max(0, newMaxLevel);
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

        /**
         * @brief 배수 구매의 상한. **최대**가 무한 루프가 되지 않게 하는 안전장치다.
         *
         * 비용은 어느 축이든 지수(최소 1.12배/레벨)라, 잔액이 아무리 커도 살 수
         * 있는 칸 수는 로그로 묶인다 - 1골드에서 double이 표현할 수 있는 끝
         * (약 1e308)까지가 1.12배 기준 6천 칸 남짓이다. 그 열 배를 상한으로 둔다.
         *
         * 이 값에 실제로 걸리는 상황은 곡선이 망가졌을 때뿐이고(costGrowth가 1에
         * 가까워지는 등), 그때 프레임이 멈추는 대신 여기서 끊긴다.
         */
        public const int MaxBatchLevels = 65536;

        /**
         * @brief 지금 잔액으로 **몇 칸까지 살 수 있는가.** 사지는 않는다.
         *
         * "최대" 버튼의 수량과, 배수 버튼이 실제로 몇 칸을 살지 미리 보여주는 데
         * 함께 쓴다. 값을 여기서 한 번 계산하고 구매도 이 수를 따라가므로,
         * 화면에 뜬 수와 실제로 사는 수가 갈리지 않는다.
         *
         * **할인은 없다.** 칸마다 CostAtLevel을 그대로 더한다 - 한 번에 사든
         * 백 번 눌러서 사든 총액이 같아야 하고, 그것이 배수 구매가 편의이지
         * 밸런스가 아니라는 말의 뜻이다(9단계 이후로 곡선은 신성하다).
         *
         * @param wallet 잔액. null이면 0칸
         * @param limit  최대 몇 칸까지 볼 것인가. 0 이하면 살 수 있는 데까지
         */
        public int AffordableLevels(PlayerWallet wallet, int limit)
        {
            if (wallet == null) return 0;

            int ceiling = limit > 0 ? Mathf.Min(limit, MaxBatchLevels) : MaxBatchLevels;

            var budget = wallet.Gold;
            int count = 0;

            while (count < ceiling)
            {
                // 최대 레벨에서 멈춘다. IsMaxed와 같은 판정을 미래 레벨에 적용한다
                if (maxLevel > 0 && level + count >= maxLevel) break;

                var cost = CostAtLevel(level + count);
                if (cost > budget) break;

                budget -= cost;
                count++;
            }

            return count;
        }

        /**
         * @brief 이 칸 수를 사는 데 드는 총액. 버튼이 가격으로 보여준다.
         *
         * AffordableLevels가 센 것과 같은 합이다 - 두 식이 갈리면 "살 수 있다"고
         * 표시된 수량이 결제에서 한 칸 모자라는 상태가 생긴다.
         */
        public BigDouble CostOfNextLevels(int count)
        {
            var total = BigDouble.Zero;
            for (int i = 0; i < count; i++)
            {
                if (maxLevel > 0 && level + i >= maxLevel) break;
                total += CostAtLevel(level + i);
            }
            return total;
        }

        /**
         * @brief 여러 칸을 한 번에 산다. **살 수 있는 데까지만.**
         *
         * 실제로 오른 칸 수를 돌려준다. 골드가 모자라면 거기서 멈추고, 그때까지
         * 산 것은 그대로 남는다 - 전부 아니면 전무로 만들면 "×100을 눌렀는데
         * 아무 일도 안 일어난다"가 되고, 그게 이 버튼을 넣는 이유(탭 횟수 절약)를
         * 정면으로 배신한다.
         *
         * 한 칸씩 TryPurchase를 도는 것과 결과가 같아야 하므로 지불도 칸마다
         * 한다. 합계를 미리 계산해 한 번에 빼면 반올림(UpgradeCost.Quantize)이
         * 한 번만 걸려 총액이 갈릴 수 있다.
         */
        public int TryPurchaseMany(PlayerWallet wallet, int count)
        {
            if (wallet == null || count <= 0) return 0;

            int target = Mathf.Min(count, MaxBatchLevels);
            int bought = 0;

            while (bought < target && TryPurchase(wallet)) bought++;

            return bought;
        }

        /**
         * @brief 세이브/로드용. 저장된 레벨을 **자르지 않는다**.
         *
         * 예전에는 maxLevel로 잘랐다. 그러면 상한이 내려간 업데이트에서 플레이어가
         * 산 레벨이 영구히 사라진다 - 9단계에서 공격속도 상한을 51에서 32로 낮췄을 때
         * Lv.44 세이브가 정확히 그렇게 됐다. 골드는 이미 썼는데 되돌릴 방법이 없다.
         *
         * 이제 레벨은 그대로 남고 효과만 valueCeiling에서 막힌다. 상한이 다시 오르면
         * (더 긴 공격 클립, 새 규칙) 잠들어 있던 레벨이 그대로 제 값을 낸다.
         *
         * 구매는 여전히 maxLevel에서 막힌다. IsMaxed 참고 - 상한 위의 레벨은
         * "더 살 수 없는 상태"로 읽히므로 UI 동작은 달라지지 않는다.
         */
        public void SetLevel(int value)
        {
            level = Mathf.Max(1, value);
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
                            int maxLevel = 0, double valueCeiling = 0d,
                            Display display = Display.Plain)
        {
            this.valueCeiling = valueCeiling;
            this.display = display;
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
