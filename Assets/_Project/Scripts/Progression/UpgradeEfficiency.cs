using System;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 강화 축들이 서로 비슷한 값어치를 갖는지 재는 지표.
     *
     * 이것이 존재하는 이유는 공격속도 강화가 죽었기 때문이다. 값은 선형(+0.12/레벨)인데
     * 비용은 지수(x1.35/레벨)라, 레벨이 오를수록 "골드당 얻는 것"이 0으로 수렴했다.
     * 8단계 시점에 공격력의 1/88까지 벌어져 있었고, 그 상태에서는 아무도 그 버튼을
     * 누르지 않는다. 화면에는 멀쩡한 버튼으로 보이므로 눈으로는 알 수 없다.
     *
     * 핵심은 **구조**다. 값이 선형이고 비용이 지수면 그 축은 반드시 죽는다. 언제
     * 죽느냐만 계수가 정한다. 두 축이 끝까지 나란히 가려면 둘 다 곱연산이고 비용
     * 증가율이 같아야 한다. 그러면 아래 비율이 레벨과 무관하게 일정해진다.
     *
     * DPS = 공격력 x 공격속도 이므로, 어느 축이든 "값을 몇 % 올리는가"가 곧
     * "DPS를 몇 % 올리는가"다. 그래서 축 종류에 상관없이 같은 자로 잴 수 있다.
     */
    public static class UpgradeEfficiency
    {
        /**
         * @brief 한 레벨 구매가 값을 올리는 비율.
         *
         * 곱연산이면 레벨과 무관하게 일정하고(step - 1), 합연산이면 레벨이 오를수록
         * 0으로 수렴한다. 축이 죽는 과정이 정확히 이 수치의 감소다.
         */
        public static double RelativeGain(UpgradeTrack track, int level)
        {
            if (track == null) return 0d;

            double current = track.ValueAtLevel(level).ToDouble();
            if (current <= 0d || double.IsNaN(current) || double.IsInfinity(current)) return 0d;

            double next = track.ValueAtLevel(level + 1).ToDouble();
            if (double.IsNaN(next) || double.IsInfinity(next)) return 0d;

            return next / current - 1d;
        }

        /**
         * @brief 골드 1당 얻는 DPS 증가율.
         *
         * 축 사이를 비교하는 단위다. 절대값 자체는 레벨이 오르면 계속 작아지므로
         * 의미가 없고, **축 사이의 비율**만 본다.
         */
        public static double GainPerGold(UpgradeTrack track, int level)
        {
            if (track == null) return 0d;

            double cost = track.CostAtLevel(level).ToDouble();
            if (cost <= 0d || double.IsNaN(cost) || double.IsInfinity(cost)) return 0d;

            return RelativeGain(track, level) / cost;
        }

        /**
         * @brief 두 축의 효율 비율. 항상 1 이상 (큰 쪽 / 작은 쪽).
         *
         * 1에 가까울수록 둘 다 살아 있다. 커질수록 한쪽이 지배적이 되고, 어느
         * 지점부터는 낮은 쪽이 눌리지 않는 버튼이 된다.
         */
        public static double Ratio(UpgradeTrack a, UpgradeTrack b, int level)
        {
            double ea = GainPerGold(a, level);
            double eb = GainPerGold(b, level);

            if (ea <= 0d || eb <= 0d) return double.PositiveInfinity;

            return ea > eb ? ea / eb : eb / ea;
        }

        /**
         * @brief 이 축이 구조적으로 죽는가.
         *
         * 값이 합연산인데 비용이 지수면 참이다. 계수와 무관하게 성립하므로,
         * 새 강화를 추가할 때 이 검사 하나로 같은 실수를 막을 수 있다.
         */
        public static bool DecaysStructurally(UpgradeTrack track, int fromLevel, int toLevel)
        {
            if (track == null || toLevel <= fromLevel) return false;

            double first = GainPerGold(track, fromLevel);
            double last = GainPerGold(track, toLevel);
            if (first <= 0d) return false;

            // 비용이 지수로 오르므로 절대값은 어느 축이든 줄어든다. 문제는 그 감소가
            // 다른 축보다 훨씬 가파른 경우이고, 그것은 Ratio로 잡는다. 여기서는
            // 값 자체의 증가율이 줄어드는지만 본다 - 곱연산이면 일정해야 한다
            double gainFirst = RelativeGain(track, fromLevel);
            double gainLast = RelativeGain(track, toLevel);

            return gainLast < gainFirst * 0.9d;
        }

        /** 사람이 읽는 한 줄. 테스트 패널과 실패 메시지에 쓴다 */
        public static string Describe(UpgradeTrack track, int level)
        {
            if (track == null) return "(없음)";

            return string.Format("{0} Lv.{1}  +{2:P1} / {3}골드",
                track.DisplayName, level,
                RelativeGain(track, level),
                NumberFormatter.Format(track.CostAtLevel(level)));
        }
    }
}
