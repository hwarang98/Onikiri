using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 보스전 실패를 "더 강해져야 한다"는 신호로 번역한다.
     *
     * 실패 화면이 그냥 "실패"라고만 말하면 플레이어가 얻는 정보는 0이다. 방치형에서
     * 그 상태의 정답은 항상 "강화하고 다시 온다"인데, **무엇을** 얼마나 올려야 하는지는
     * 화면 어디에도 없다. 강화 버튼 두 개의 비용과 보스 체력을 머릿속에서 곱해보라는
     * 요구가 되고, 대부분은 그냥 게임을 닫는다.
     *
     * 그래서 두 가지를 계산해서 문장으로 준다.
     *
     *  - **얼마나**: 제한 시간 동안 실제로 넣은 피해와 보스 체력의 비율. "1.4배"는
     *    추정이 아니라 방금 일어난 일의 나눗셈이다.
     *  - **무엇을**: 지금 골드당 DPS 증가율이 더 좋은 축. UpgradeEfficiency가 이미
     *    재고 있는 값이라 새 판단 기준을 만들지 않는다.
     *
     * MonoBehaviour가 아닌 이유는 이 계산에 씬이 필요 없기 때문이다. 실패 문구가
     * 틀리는 것은 화면을 봐야만 알 수 있는 종류의 버그라 테스트로 못 박는 편이 낫다.
     */
    public static class BossFailureAdvice
    {
        /**
         * @brief 제한 시간 안에 보스를 잡으려면 지금의 몇 배가 필요했는가.
         *
         * 넣은 피해가 0이면(사거리 밖에서 시간만 흘렀다면) 배율을 낼 수 없다.
         * 그때는 0을 반환하고 호출부가 일반 문구로 넘어간다.
         */
        public static double ShortfallFactor(BigDouble bossMaxHealth, BigDouble damageDealt)
        {
            if (damageDealt <= BigDouble.Zero) return 0d;

            double factor = (bossMaxHealth / damageDealt).ToDouble();
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor < 1d) return 0d;
            return factor;
        }

        /**
         * @brief 지금 올리면 골드당 이득이 가장 큰 축.
         *
         * 상한에 닿은 축은 후보에서 뺀다. 공격속도는 아트가 정한 상한이 있어서
         * (AttackSpeedCurve) 실제로 여기 걸리고, 그 상태에서 "공격속도를 올려라"는
         * 안내는 실행할 수 없는 지시다.
         */
        public static UpgradeTrack BestAxis(UpgradeTrack[] tracks)
        {
            if (tracks == null) return null;

            UpgradeTrack best = null;
            double bestGain = 0d;

            foreach (var track in tracks)
            {
                if (track == null || track.IsMaxed) continue;

                double gain = UpgradeEfficiency.GainPerGold(track, track.Level);
                if (gain <= bestGain) continue;

                bestGain = gain;
                best = track;
            }

            return best;
        }

        /**
         * @brief 실패 화면에 세울 두 줄짜리 문장.
         *
         * 첫 줄은 무슨 일이 있었는지, 둘째 줄은 무엇을 하면 되는지다. 순서를 바꾸지
         * 않는다. 지시가 먼저 오면 왜 그래야 하는지 모르는 채로 읽게 된다.
         */
        /**
         * @brief 생존 축 중 지금 올리면 유효체력 이득이 가장 큰 것.
         *
         * BestAxis와 같은 일을 하지만 자가 다르다. 체력·회복은 DPS에 기여하지
         * 않으므로 UpgradeEfficiency로 재면 0이 나오고, 그러면 사망 문구가
         * 엉뚱하게 공격력을 가리킨다.
         */
        public static UpgradeTrack BestSurvivalAxis(UpgradeTrack[] tracks)
        {
            if (tracks == null) return null;

            UpgradeTrack best = null;
            double bestGain = 0d;

            foreach (var track in tracks)
            {
                if (track == null || track.IsMaxed) continue;
                if (!SurvivalEfficiency.FeedsSurvival(track.Id)) continue;

                double gain = SurvivalEfficiency.GainPerGold(track, track.Level);
                if (gain <= bestGain) continue;

                bestGain = gain;
                best = track;
            }

            return best;
        }

        /**
         * @brief 쓰러져서 실패했을 때의 문구.
         *
         * 부족 배율의 정의는 **받을 피해 / 유효체력**이다.
         *
         *   받을 피해  = 보스 공격력 x (제한 시간 / 공격 간격)
         *   유효체력   = 최대체력 + 초당회복 x 제한 시간
         *
         * 실제로 받은 피해가 아니라 **받았을 피해**를 쓴다. 죽는 순간 전투가
         * 끝나므로 실제로 받은 피해는 언제나 유효체력과 같고, 그 값으로는
         * "얼마나 모자랐는가"가 나오지 않는다(항상 1.0배가 된다). 끝까지
         * 버텼다면 얼마를 맞았을지와 비교해야 배율이 의미를 갖는다.
         */
        public static string DeathMessage(double incomingDamage, double effectiveHealth,
                                          UpgradeTrack[] tracks)
        {
            double factor = effectiveHealth > 0d ? incomingDamage / effectiveHealth : 0d;
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor < 1d) factor = 0d;

            string shortfall = factor <= 0d
                ? "버티지 못했다"
                : factor < 1.1d
                    ? "한 대를 더 견디지 못했다"
                    : "버티지 못했다. 체력이 " + factor.ToString("F1") + "배 모자란다";

            var axis = BestSurvivalAxis(tracks);
            string action = axis != null
                ? axis.DisplayName + "를 올리고 다시 도전하라"
                : "체력 강화를 올리고 다시 도전하라";

            return shortfall + ".\n" + action;
        }

        public static string Message(BigDouble bossMaxHealth, BigDouble damageDealt, UpgradeTrack[] tracks)
        {
            double factor = ShortfallFactor(bossMaxHealth, damageDealt);
            var axis = BestAxis(tracks);

            string shortfall = factor <= 0d
                ? "칼이 닿지 못했다"
                : factor < 1.1d
                    ? "한 끗이 모자랐다"
                    : "화력이 " + factor.ToString("F1") + "배 모자란다";

            // 모든 축이 상한이면 올릴 것이 없다. 지금 곡선에서는 공격력에 상한이
            // 없으므로 도달할 수 없는 경로지만, 축이 늘어나면 생길 수 있다
            string action = axis != null
                ? axis.DisplayName + "를 올리고 다시 도전하라"
                : "잡몹을 더 베어 골드를 모아라";

            return shortfall + ".\n" + action;
        }
    }
}
