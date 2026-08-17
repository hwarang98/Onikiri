using System;
using Onikiri.Battle;

namespace Onikiri.Progression
{
    /**
     * @brief 치명타 확률 강화 곡선 - **만렙 Lv.1000 = 정확히 100%** (43단계).
     *
     * **가산이다.** 확률은 곱연산으로 키울 수 없다 - 1을 넘는 순간 의미를 잃고,
     * 상한에 부딪히기 전까지의 구간이 곡선의 대부분을 차지하게 된다.
     *
     * 43단계에 깊이가 사용자 스펙으로 확정됐다: 레벨당 아주 조금씩(+0.088%p)
     * 균등하게 올라 Lv.1000에서 딱 100%에 닿는다 - 슬레이어의 치명타 확률
     * Lv.1000 MAX와 같은 구조다. 도달이 곧 발도 개방의 문이고
     * (TranscendCurve.IsUnlockedAt), 화면은 MASTER 금색으로 축하한다.
     *
     * 10단계가 100%를 막았던 근거("모든 타격이 치명타면 그것은 그냥 공격력")는
     * 뒤집혀서 이 문의 존재 이유가 됐다 - 전타 치명타에 도달하면 치명타
     * 피해가 순수 배수가 되고, 그 위로 초월 치명타·연격이 쌓인다.
     *
     * ## 값은 한 결, 비용은 두 결
     *
     * 60% 문턱(DeepPhaseLevel) 앞까지는 42단계까지의 곡선(+0.5%p, x1.15)을
     * 값 등가로 미세화한 것이다 - 레벨 하나가 5.68칸이 됐을 뿐 골드와 확률의
     * 관계는 같다. 그래야 조율이 끝난 코리더(1~30)가 살아남는다(시뮬 실측으로
     * 곡선 추종 플레이어는 st22에 이미 60%에 서 있다).
     *
     * 문턱부터는 도약(WallJump) 뒤 가파른 결(DeepGrowth)이다. 60%의 벽을
     * 뚫는 수련은 앞의 545칸과 차원이 다르다 - 이 도약이 코리더·가속
     * 구간(~st58)을 지키고, 100% 완주를 무한 구간(st80대)의 콘텐츠로
     * 만든다. 값은 전부 하네스 실측으로 잡았다.
     */
    public static class CritRateCurve
    {
        /** 레벨 1의 확률. CombatBaseline의 기본값과 같아야 한다 */
        public const double BaseValue = CombatBaseline.CritChance;

        /** 만렙. 여기서 정확히 100%다 - 스텝이 이 값에서 유도된다 */
        public const int DeepMaxLevel = 1000;

        /** 레벨당 가산 (확률 포인트). (100% - 12%) / 999칸 = +0.0881%p */
        public const double Step = (Ceiling - BaseValue) / (DeepMaxLevel - 1);

        /** 확률 상한 = 100%. Lv.1000의 값이고, 발도 개방의 문이다 */
        public const double Ceiling = 1.00d;

        /**
         * @brief 비용 - 첫 결. 42단계까지의 곡선(1골드, x1.15)의 값 등가 미세화.
         *
         * 옛 한 레벨(+0.5%p)이 새 5.676칸이므로 증가율은 1.15^(1/5.676)이고,
         * 기준 비용은 연속체 등가값((g-1)/0.15 = 0.166)에 **이산 보정 x1.2**를
         * 얹은 값이다 - 미세 칸은 초반 골드를 치명타로 더 흘려보내
         * (후불 이득) st10 여유를 12% 깎았고, 이 보정이 되돌린다.
         *
         * E-3 수정: 1골드 아래 비용은 이제 존재하지 않는다 - 전 축 일괄 상향과
         * 정수화(최소 1골드)는 UpgradeCost가 단일 출처다.
         */
        public const double BaseCost = 0.19943d * UpgradeCost.RaiseScale;
        public const double CostGrowth = 1.0249283d;

        /**
         * @brief 60% 문턱 - 값이 60%를 처음 넘는 레벨.
         *
         * Lv.545가 59.92%, Lv.546이 60.01%다. 이 앞까지가 코리더 시절의
         * 치명타 축이고, NeutralizeMastery(42단계 재현 비교군)가 여기서
         * 멈춘다.
         */
        public const int DeepPhaseLevel = 546;

        /**
         * @brief 문턱의 도약과 두 번째 결. 하네스 실측으로 잡았다.
         *
         * 도약이 정하는 것은 **언제 벽을 밀기 시작하는가**(st50대 후반 -
         * 42단계 완충 스파이크 뒤), 두 번째 결이 정하는 것은 **얼마나 걸려
         * 100%에 닿는가**(st80대)다. 42단계까지의 구간(~st58)에서는 다음
         * 칸이 수입 밖이라 치명타가 60%에 서 있다 - 그것이 코리더·가속
         * 구간 보존의 기제다.
         */
        public const double WallJump = 3.5e8d;
        public const double DeepGrowth = 1.0315d;

        public static double ValueAtLevel(int level)
        {
            return BaseValue + Step * Math.Max(0, level - 1);
        }

        public static double CappedValueAtLevel(int level)
        {
            return Math.Min(Ceiling, ValueAtLevel(level));
        }

        /**
         * @brief 비용. CostAtLevel(L)은 L+1로 가는 가격이다(전 축 공통 관례).
         *
         * 문턱 조건이 DeepPhaseLevel - 1인 이유가 그 관례다 - Lv.545 -> 546
         * 걸음(60%를 넘는 첫 걸음)이 첫 도약 가격이어야 한다. 43단계 첫
         * 구현에서 이 한 칸을 틀려 코리더 앵커가 통째로 흔들렸다.
         */
        public static double CostAtLevel(int level)
        {
            int steps = Math.Max(0, level - 1);

            if (level < DeepPhaseLevel - 1)
                return UpgradeCost.Quantize(BaseCost * Math.Pow(CostGrowth, steps));

            return UpgradeCost.Quantize(
                BaseCost * Math.Pow(CostGrowth, DeepPhaseLevel - 2)
                 * WallJump
                 * Math.Pow(DeepGrowth, level - (DeepPhaseLevel - 1)));
        }

        /** 상한(100%)에 닿는 레벨 = 1000. UI의 MASTER와 개방 판정이 읽는다 */
        public static int MaxLevel
        {
            get { return DeepMaxLevel; }
        }
    }
}
