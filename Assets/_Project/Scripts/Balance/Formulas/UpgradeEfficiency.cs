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
     * 핵심은 **구조**다. DPS 기여가 선형이고 비용이 지수면 그 축은 반드시 죽는다.
     * 언제 죽느냐만 계수가 정한다.
     *
     * **10단계에서 측정 기준이 바뀌었다.** 축이 둘일 때는 DPS = 공격력 x 공격속도라
     * "값이 몇 % 오르는가"가 곧 "DPS가 몇 % 오르는가"였다. 치명타가 들어오면서
     * 그것이 깨진다 - 치명타율 12% -> 12.5%는 값으로는 4.2% 증가지만 DPS로는
     * 0.45%뿐이고, 그 차이가 치명타 배수(다른 축)에 달려 있다.
     *
     * 그래서 이제 값이 아니라 **DPS를 직접 잰다**. CombatStats가 네 축을 합쳐
     * 기대 DPS를 내고, 여기서는 "이 축만 한 레벨 올렸을 때 그 DPS가 몇 % 오르는가"를
     * 본다. 축이 늘어나도 이 정의는 그대로다.
     */
    public static class UpgradeEfficiency
    {
        /**
         * @brief 이 축을 한 레벨 올릴 때 기대 DPS가 오르는 비율.
         *
         * 상한을 걷어낸 곡선으로 잰다. 이 지표가 보는 것은 **곡선의 형태**이지
         * 지금 실제로 낼 수 있는 값이 아니다. 상한이 걸린 값을 쓰면 상한 위에서
         * 증가율이 0이 되고, 두 축의 비율이 무한대로 발산해 "그 축이 죽었다"는
         * 결론이 나온다. 그건 틀렸다 - 상한 위의 레벨은 팔리지 않으므로 죽은
         * 버튼이 아니라 아예 없는 버튼이다.
         *
         * 새 축을 추가할 때 잡아야 하는 것도 형태다. 상한을 붙이면 어떤 나쁜
         * 곡선이든 그 지점부터는 검사를 통과해버린다.
         */
        public static double RelativeGain(UpgradeTrack track, int level)
        {
            if (track == null || !CombatStats.FeedsDps(track.Id)) return 0d;

            var baseline = CombatStats.AtLevel(level);

            double current = baseline
                .With(track.Id, track.UncappedValueAtLevel(level).ToDouble())
                .ExpectedDps;

            double next = baseline
                .With(track.Id, track.UncappedValueAtLevel(level + 1).ToDouble())
                .ExpectedDps;

            if (current <= 0d || double.IsNaN(current) || double.IsInfinity(current)) return 0d;
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
         * DPS 기여 증가율이 레벨과 함께 줄어들면 참이다.
         *
         * 값이 가산이라는 것만으로는 판정하지 않는다. 치명타율은 가산이지만
         * 죽지 않는다 - DPS 기여가 rate x (mult - 1) 이라 치명타 피해 축이 함께
         * 자라면 확률 한 칸의 값어치도 함께 자라기 때문이다. 판정 기준은 언제나
         * 값이 아니라 DPS다.
         */
        public static bool DecaysStructurally(UpgradeTrack track, int fromLevel, int toLevel)
        {
            if (track == null || toLevel <= fromLevel) return false;

            double gainFirst = RelativeGain(track, fromLevel);
            double gainLast = RelativeGain(track, toLevel);
            if (gainFirst <= 0d) return false;

            return gainLast < gainFirst * 0.9d;
        }

        /** 사람이 읽는 한 줄. 테스트 패널과 실패 메시지에 쓴다 */
        public static string Describe(UpgradeTrack track, int level)
        {
            if (track == null) return "(없음)";

            return string.Format("{0} Lv.{1}  +{2:P2} DPS / {3}골드",
                track.DisplayName, level,
                RelativeGain(track, level),
                NumberFormatter.Format(track.CostAtLevel(level)));
        }
    }
}
