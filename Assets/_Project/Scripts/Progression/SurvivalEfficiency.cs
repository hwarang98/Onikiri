using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 생존 축들이 서로 비슷한 값어치를 갖는지 재는 지표.
     *
     * UpgradeEfficiency와 같은 일을 하지만 자가 다르다. 체력과 회복은 DPS에
     * 전혀 기여하지 않으므로 "골드당 %DPS"로는 0이 나온다. 그 자로 재면 두 축은
     * 즉시 죽은 버튼으로 판정되고, 그것은 지표가 틀린 것이지 축이 틀린 것이 아니다.
     *
     * ## 유효체력(EHP)의 정의
     *
     *     EHP = 최대체력 + 초당회복 x 전투지속시간
     *
     * "이 보스전에서 총 얼마의 피해를 견딜 수 있는가"이다. 회복을 체력으로
     * 환산하는 항이 핵심이고, 환산율이 곧 **전투 지속시간**이다. 회복 1/초는
     * 25초짜리 전투에서 체력 25와 같다.
     *
     * 지속시간을 상수로 고정한 이유는 비교의 기준을 하나로 두기 위해서다.
     * 실제 전투는 보스를 언제 잡느냐에 따라 짧아지지만, 그 시간을 계산에 넣으면
     * 회복 축의 값어치가 플레이어의 화력에 의존하게 되어 축 사이 비교가
     * 화력 레벨마다 달라진다. 최악의 경우(제한 시간을 다 쓰는 전투)를 기준으로
     * 잡으면 회복을 과대평가하지 않으면서 기준이 하나로 유지된다.
     *
     * ## 왜 DPS 축과 직접 비교하지 않는가
     *
     * %DPS와 %EHP는 단위가 달라서 나눌 수 없다. 둘의 균형은 자가 아니라
     * **시뮬레이션 게이트**로 잡는다 - 곡선을 따라온 플레이어가 보스전에서
     * 죽지 않는가, 무강화 플레이어는 몇 스테이지에서 죽는가. StageSimulation 참고.
     */
    public static class SurvivalEfficiency
    {
        /**
         * @brief 회복을 체력으로 환산할 때 쓰는 전투 지속시간 (초).
         *
         * 제한 시간을 그대로 쓴다. 실제 전투는 이보다 짧게 끝나는 경우가 많지만,
         * 생존이 문제가 되는 것은 언제나 끝까지 가는 전투다.
         */
        public static double ReferenceFightSeconds
        {
            get { return StageCurve.BossTimeLimitSeconds; }
        }

        /**
         * @brief 유효체력.
         *
         * regenFraction은 **초당 최대 체력의 몇 %**다(절대량이 아니다).
         * 그래서 식이 곱으로 정리된다:
         *
         *     EHP = 최대체력 x (1 + 비율 x 전투시간)
         *
         * 이 모양 덕분에 체력 축의 기여율이 레벨과 무관하게 일정해진다.
         * 절대 회복량을 쓰면 두 축이 자릿수 경주를 하게 되고, 지는 쪽은
         * 곡선이 건강해도 화면에서 눌리지 않는 버튼이 된다. HealthRegenCurve 참고.
         */
        public static double EffectiveHealth(double maxHealth, double regenFraction)
        {
            return maxHealth * (1d + regenFraction * ReferenceFightSeconds);
        }

        /** 모든 생존 축이 이 레벨일 때의 유효체력 */
        public static double EffectiveHealthAtLevel(int level)
        {
            return EffectiveHealth(HealthCurve.ValueAtLevel(level), HealthRegenCurve.ValueAtLevel(level));
        }

        /**
         * @brief 이 축을 한 레벨 올릴 때 유효체력이 오르는 비율.
         *
         * UpgradeEfficiency.RelativeGain과 같은 모양이고, DPS 자리에 EHP가 들어간다.
         * 상한을 걷어낸 곡선으로 재는 것도 같다 - 보는 것이 형태이기 때문이다.
         */
        public static double RelativeGain(UpgradeTrack track, int level)
        {
            if (track == null || !FeedsSurvival(track.Id)) return 0d;

            double health = HealthCurve.ValueAtLevel(level);
            double regenFraction = HealthRegenCurve.ValueAtLevel(level);

            double current = EffectiveHealth(health, regenFraction);
            if (current <= 0d) return 0d;

            double next = track.Id == UpgradeSystem.HealthId
                ? EffectiveHealth(track.UncappedValueAtLevel(level + 1).ToDouble(), regenFraction)
                : EffectiveHealth(health, track.UncappedValueAtLevel(level + 1).ToDouble());

            if (double.IsNaN(next) || double.IsInfinity(next)) return 0d;

            return next / current - 1d;
        }

        public static double GainPerGold(UpgradeTrack track, int level)
        {
            if (track == null) return 0d;

            double cost = track.CostAtLevel(level).ToDouble();
            if (cost <= 0d || double.IsNaN(cost) || double.IsInfinity(cost)) return 0d;

            return RelativeGain(track, level) / cost;
        }

        public static double Ratio(UpgradeTrack a, UpgradeTrack b, int level)
        {
            double ea = GainPerGold(a, level);
            double eb = GainPerGold(b, level);

            if (ea <= 0d || eb <= 0d) return double.PositiveInfinity;

            return ea > eb ? ea / eb : eb / ea;
        }

        /** 유효체력 기여가 레벨과 함께 줄어드는가 */
        public static bool DecaysStructurally(UpgradeTrack track, int fromLevel, int toLevel)
        {
            if (track == null || toLevel <= fromLevel) return false;

            double first = RelativeGain(track, fromLevel);
            double last = RelativeGain(track, toLevel);
            if (first <= 0d) return false;

            return last < first * 0.9d;
        }

        public static bool FeedsSurvival(string trackId)
        {
            return trackId == UpgradeSystem.HealthId || trackId == UpgradeSystem.HealthRegenId;
        }

        public static string Describe(UpgradeTrack track, int level)
        {
            if (track == null) return "(없음)";

            return string.Format("{0} Lv.{1}  +{2:P2} EHP / {3}골드",
                track.DisplayName, level,
                RelativeGain(track, level),
                NumberFormatter.Format(track.CostAtLevel(level)));
        }
    }
}
