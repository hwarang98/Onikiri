using System;

namespace Onikiri.Progression
{
    /**
     * @brief 골드 획득 축을 재는 자. 단위는 **초**다.
     *
     * ## 왜 새 자가 필요한가
     *
     * UpgradeEfficiency는 골드당 %DPS를 잰다. 그 자로 골드 획득 축을 재면 답이
     * 항상 0이다 - 이 축은 DPS에 1도 기여하지 않는다. 10단계 주석이 "치명타 축은
     * DPS 기여 구조가 다르다"고 적었고, 11단계가 생존 축을 위해 SurvivalEfficiency를
     * 따로 만들었다. 이번이 세 번째이고, 이유는 매번 같다 - **축이 하는 일이
     * 다르면 자도 달라야 한다.**
     *
     * ## 회수 시간
     *
     * 이 축은 골드를 써서 골드를 더 빨리 버는 메타 축이다. 그런 투자를 재는 자는
     * 하나뿐이다: **얼마 만에 본전을 뽑는가.**
     *
     *     회수 시간 = 이번 레벨 비용 / 늘어난 초당 골드
     *               = 비용(L) / (지금 초당 골드 x (다음 배수/지금 배수 - 1))
     *
     * 곱연산이라 배수 비율은 항상 Step이고, 따라서 분모는 `초당 골드 x (Step-1)`로
     * 정리된다. 상한에 닿으면 분모가 0이 되어 회수가 영원히 안 된다 - 무한대를
     * 그대로 돌려준다. 그것이 "이 버튼은 이제 사면 안 된다"의 정직한 표현이다.
     *
     * ## 밴드
     *
     * 회수 시간이 너무 짧으면 이 축이 다른 모든 축을 제친다. 어떤 축이든 골드가
     * 있어야 사는데 이 축은 그 골드 자체를 늘리므로, 즉시 회수되는 순간
     * "무조건 이것부터"가 정답이 되고 나머지 여섯은 나중 문제가 된다.
     *
     * 너무 길면 반대로 아무도 안 산다 - 한 세션(수 분~십수 분)보다 긴 회수는
     * 플레이어가 체감할 수 없고, 8단계 공격속도와 같은 죽은 버튼이 된다.
     */
    public static class GoldGainEfficiency
    {
        /**
         * @brief 건강 밴드의 아래쪽 (초).
         *
         * 30초보다 빨리 회수되면 스노볼이다. 잡몹 한 마리 처치가 2초 안팎이므로
         * 30초는 대략 15마리 - 그 정도는 "투자했다"는 감각이 남는 최소 길이다.
         */
        public const double HealthyMinSeconds = 30d;

        /**
         * @brief 건강 밴드의 위쪽 (초).
         *
         * 120초 = 2분. 한 스테이지가 초반 30초, 후반 1~2분이므로 대략 "이번
         * 스테이지 안에 회수된다"에 해당한다. 그보다 길면 사고 나서 아무 일도
         * 안 일어난 것처럼 보인다.
         *
         * 처음에 300초(5분)로 잡았다가 내렸다. 300이면 **1스테이지에서만 10레벨을
         * 사고 5스테이지에 상한에 닿았다** - 회수를 기다리는 동안 화력이 밀려
         * 그 구간 보스 여유가 1.38까지 떨어지고, 회수가 끝난 뒤에는 4.69까지
         * 뛰었다. 밴드 폭(1.5~3.0)보다 큰 진폭을 이 축 하나가 만든 셈이다.
         *
         * 임계값을 좁히면 같은 총량을 여러 스테이지에 나눠 사게 되어 그 진폭이
         * 흩어진다.
         */
        public const double HealthyMaxSeconds = 120d;

        /**
         * @brief 시뮬레이션의 구매 정책이 쓰는 임계값 (초).
         *
         * 회수 시간이 이보다 짧으면 산다. 밴드 위쪽과 같은 값인 것이 중요하다 -
         * 정책이 밴드 밖까지 사들이면 시뮬레이션은 밴드를 지키지 않는 플레이어를
         * 재게 되고, 그 결과로 맞춘 밸런스는 실제 플레이와 다른 사람을 위한 것이 된다.
         */
        public const double BuyThresholdSeconds = HealthyMaxSeconds;

        /**
         * @brief 이 레벨에서 한 칸 올릴 때의 회수 시간 (초).
         *
         * @param level          지금 레벨
         * @param goldPerSecond  지금 벌고 있는 초당 골드. **이 축의 배수가 이미
         *                       곱해진 값이어야 한다** - 늘어나는 양은 현재 수입에
         *                       비례하므로, 원시 수입을 넘기면 회수 시간이 실제보다
         *                       길게 나와 이 축을 과소평가한다
         * @return 회수까지 걸리는 초. 상한에 닿았거나 수입이 없으면 무한대
         */
        public static double PaybackSeconds(int level, double goldPerSecond)
        {
            if (goldPerSecond <= 0d) return double.PositiveInfinity;
            if (level >= GoldGainCurve.MaxLevel) return double.PositiveInfinity;

            double now = GoldGainCurve.CappedValueAtLevel(level);
            double next = GoldGainCurve.CappedValueAtLevel(level + 1);
            if (now <= 0d) return double.PositiveInfinity;

            // 상한 근처에서는 마지막 한 칸이 Step보다 작게 오른다. 비율을 상수
            // (Step-1)로 굳히면 그 칸의 회수 시간을 실제보다 짧게 보고하게 된다
            double gainRatio = next / now - 1d;
            if (gainRatio <= 0d) return double.PositiveInfinity;

            double extraPerSecond = goldPerSecond * gainRatio;
            if (extraPerSecond <= 0d) return double.PositiveInfinity;

            return GoldGainCurve.CostAtLevel(level) / extraPerSecond;
        }

        /** 지금 사는 것이 이득인가. 시뮬레이션 구매 정책과 같은 판단 */
        public static bool WorthBuying(int level, double goldPerSecond)
        {
            return PaybackSeconds(level, goldPerSecond) <= BuyThresholdSeconds;
        }

        public static bool IsHealthy(double paybackSeconds)
        {
            return paybackSeconds >= HealthyMinSeconds && paybackSeconds <= HealthyMaxSeconds;
        }
    }
}
