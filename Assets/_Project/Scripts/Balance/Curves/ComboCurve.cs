using System;

namespace Onikiri.Progression
{
    /**
     * @brief 연격 곡선 (43단계 심화 축).
     *
     * 타격마다 일정 확률로 **한 번 더 벤다.** 기대 DPS로는 x(1 + 확률)이고,
     * 추가타는 온전한 한 타라 치명타·스킬 계수를 전부 상속한다(전타
     * 치명타에 도달한 뒤에 열리므로 추가타도 언제나 치명타다).
     *
     * ## 왜 가산 확률인가 - 그리고 왜 치명타 확률의 죽음을 반복하지 않는가
     *
     * DPS 기여가 1/(1+p)씩이라 레벨당 기여율은 100%에 가까워질수록 절반으로
     * 줄지만, 0으로 수렴하지는 않는다(상한 도달 시 +0.2%). 그보다 먼저
     * 상한(100% = 확정 2연격)이 온다 - 치명타 확률과 같은 생애를 살고,
     * 같은 방식(MASTER)으로 끝난다. 초월 치명타(무상한 복리)와 유한한
     * 확률 축을 짝으로 두는 것이 이 절의 구성이다: 하나는 영원한 싱크,
     * 하나는 완성의 맛.
     *
     * 확률이 1을 넘는 설계(초과분 = 3연격 확률)는 수익화 스텝의 요도 훅
     * 자리로 남긴다.
     */
    public static class ComboCurve
    {
        /**
         * 레벨당 가산 (확률 포인트). Lv.1은 0% - 없는 것과 같다.
         *
         * 43단계 확정 스펙: 치명타 확률과 같은 문법으로 **Lv.1000에서 딱
         * 100%**(확정 2연격)다. 비용은 값 등가 재스케일(옛 0.4%p 곡선의
         * 4분할)이라 골드와 확률의 관계는 그대로다.
         */
        public const double Step = Ceiling / (DeepMaxLevel - 1);

        /** 확정 2연격. 여기서 MASTER다 */
        public const double Ceiling = 1.00d;

        /** 만렙. 치명타 확률과 같은 1000이고, 스텝이 여기서 유도된다 */
        public const int DeepMaxLevel = 1000;

        /** E-3 수정: 전 축 일괄 상향 + 정수화. 규칙과 배율은 UpgradeCost가 단일 출처다 */
        public const double BaseCost = 7.1189e12d * UpgradeCost.RaiseScale;
        public const double CostGrowth = 1.0355945d;

        public static double ChanceAtLevel(int level)
        {
            return Math.Min(Ceiling, UncappedChanceAtLevel(level));
        }

        /** 상한 무시. 효율 지표의 형태 검사(CombatStats.AtLevel)가 쓴다 */
        public static double UncappedChanceAtLevel(int level)
        {
            return Step * Math.Max(0, level - 1);
        }

        public static double CostAtLevel(int level)
        {
            return UpgradeCost.Quantize(BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1)));
        }

        /**
         * @brief 상한에 닿는 레벨 = 1000.
         *
         * 나눗셈으로 유도하지 않는다 - Step이 Ceiling/999라 floor(Ceiling/Step)이
         * 부동소수점에서 998로 떨어질 수 있고, 그러면 만렙이 조용히 999가 된다.
         * 상수가 근원이고 스텝이 유도값이다.
         */
        public static int MaxLevel
        {
            get { return DeepMaxLevel; }
        }

        /** 해금 조건은 초월 치명타와 같은 문이다 */
        public static bool IsUnlockedAt(int critRateLevel)
        {
            return TranscendCurve.IsUnlockedAt(critRateLevel);
        }
    }
}
