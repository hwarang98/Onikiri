using System;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 강화 비용의 공통 규칙 - **정수 골드, 최소 1골드, 전반 상향** (E-3 수정).
     *
     * ## 왜 생겼는가
     *
     * 43단계 미세화가 한 레벨을 여덟 칸으로 나누면서 기준 비용도 8분의 1이 됐고,
     * 그 결과 강화 첫 칸이 0.2~1.5골드가 됐다. 두 가지가 무너졌다.
     *
     *   소수 골드    0.19골드짜리 물건은 게임 화폐의 문법에 없다. 표시는
     *               UpgradeButton이 소수로 적어 때웠지만(43단계), 그것은 증상을
     *               가린 것이지 고친 것이 아니다
     *   병목의 실종  st1 수입이 마리당 5.4골드인데 한 칸이 1골드면, 골드는
     *               모으는 재화가 아니라 흘리는 재화다. 강화가 "골드 쓸 데"로
     *               읽히지 않는다
     *
     * ## 두 규칙
     *
     * **하나. 모든 강화 비용은 정수이고 1골드 아래로 내려가지 않는다.**
     * 반올림은 사사오입(AwayFromZero)이고, 원곡선이 단조 증가이므로 반올림 뒤에도
     * 값이 뒤 레벨보다 커지는 일이 없다(단조 비감소). 10^15 위는 double의 정수
     * 해상도 밖이라 반올림이 항등이고, 그 크기에서 끝전은 어차피 표시 밖이다.
     *
     * **둘. 기준 비용을 전 축 일괄 RaiseScale배로 올린다.**
     * 일괄인 이유는 축 사이 비율이 전부 조율된 값이기 때문이다 - 치명타 피해는
     * 확률의 2배(10단계), 체력/회복은 정수 조합 탐색(11단계), 이산 보정
     * x1.3/x1.2(43단계). 배율 하나로 올리면 그 관계가 전부 보존되고, 바뀌는 것은
     * 골드 수입 대비 강화의 무게뿐이다.
     *
     * ## 여파는 곡선이 아니라 램프가 받는다
     *
     * 비용이 오르면 곡선 추종 플레이어의 화력이 내려가고 코리더 여유가 가라앉는다.
     * 그 몫은 보스 램프(StageCurve.BossHealthRamp*)가 다시 받는다 - 밴드
     * (코리더 1~30 / 가속 / 심층)의 재적합 기록은 E-3 수정 보고서에 있다.
     */
    public static class UpgradeCost
    {
        /**
         * @brief 전 축 일괄 상향 배율.
         *
         * 8은 미세화(43단계)가 나눈 칸 수와 같은 크기다 - 첫 칸이 대략
         * "미세화 전 한 레벨 값"으로 돌아오고, st1 기준 공격력 한 칸(12골드)이
         * 잡몹 두 마리 몫이 된다. 온보딩·코리더 실측은 E-3 수정 보고서 참고.
         */
        public const double RaiseScale = 3.5d;

        /** double 정수 해상도의 경계. 이 위는 반올림이 항등이라 건드리지 않는다 */
        private const double IntegerResolutionLimit = 1e15d;

        /** 곡선 statics가 쓰는 정수화. 시뮬레이션·효율 지표가 같은 값을 읽는다 */
        public static double Quantize(double raw)
        {
            if (raw < 1d) return 1d;
            if (raw >= IntegerResolutionLimit) return raw;
            return Math.Round(raw, MidpointRounding.AwayFromZero);
        }

        /**
         * @brief UpgradeTrack(씬의 실제 트랙)이 쓰는 정수화.
         *
         * 경계(1e15)가 double 판과 같아야 한다 - 두 판이 다른 자리에서 갈리면
         * "시뮬레이션이 화면과 다른 가격을 잰다"(UpgradeTrack.CostAtLevel 주석)가
         * 정확히 재현된다. SimulationCurves_MatchTheLiveUpgradeTracks가 못 박는다.
         */
        public static BigDouble Quantize(BigDouble raw)
        {
            if (raw < BigDouble.One) return BigDouble.One;
            if (raw.Exponent >= 15) return raw;
            return BigDouble.FromDouble(Math.Round(raw.ToDouble(), MidpointRounding.AwayFromZero));
        }
    }
}
