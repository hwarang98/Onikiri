using System.Text;
using NUnit.Framework;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 장부의 **도달일 표를 출력한다.** 계약이 아니라 보고서다.
     *
     * ## 왜 검사가 아니라 출력인가
     *
     * 도달일은 정책(하루 20분 / 5분), 재도전 횟수, 파밍 시간의 함수이고, 그
     * 조합이 아홉 줄이다. 아홉 줄에 각각 상한을 걸면 그 상한들이 곧 아홉 개의
     * 임의 상수가 된다 - 근거 없는 계약을 아홉 개 만드는 것보다, 표를 찍고
     * **사람이 읽는 것**이 낫다.
     *
     * 실제로 지켜야 하는 것은 따로 있고 그쪽은 계약이다:
     *
     *   `MoreAttemptsAndFarming_OnlySlowProgress`   단조성
     *   `RetryCorrection_PushesTheReachDatesBack`   교정이 실제로 걸린다
     *   `TrialTimeShare_IsReported`                 귀문 몫이 밴드 안
     *
     * 이 파일은 그 계약들이 통과한 뒤 **무슨 숫자가 나오는지**를 보고서에 옮길
     * 수 있게 찍는다. 스텝 보고서의 도달일 표가 손계산이던 시절로 돌아가지
     * 않기 위해서다.
     */
    public class PromotionLedgerReportTests
    {
        const int Horizon = 400;

        /**
         * @brief 재도전 교정 **전/후**의 도달일. 2단계 보고서의 표다.
         *
         * 옛 모형은 실패 시간을 안 쓰고 성공 시간에 횟수를 곱했다. 두 세계를
         * 나란히 찍어 그 차이가 며칠인지 보인다.
         */
        [Test]
        public void ReportReachDates_BeforeAndAfterTheRetryCorrection()
        {
            var report = new StringBuilder();
            report.AppendLine("[승급 2단계] 도달일 - 재도전 교정 전/후");
            report.AppendLine("정책 | 시도 | 파밍 | 문1~6 도달일 | st150 | st200 | 귀문 총시간 | 귀문 몫");

            foreach (var play in new[] { 1200d, 300d })
            {
                string label = play >= 1200d ? "20분" : " 5분";

                foreach (var attempts in new[] { 1d, 1.5d })
                {
                    // 교정 후 (실패 시간 사용)
                    Row(report, label + " 교정후", attempts,
                        PromotionEconomyFixture.Build(
                            PromotionEconomyFixture.FreePromotion, 0.40d, Horizon,
                            PromotionEconomyFixture.DailyGems, play,
                            PromotionEconomyFixture.TrialSecondsFloor, attempts,
                            PromotionEconomyFixture.NoFarm),
                        "0");

                    if (attempts <= 1d) continue;

                    // 교정 전 (실패 = 성공으로 두면 옛 곱하기 모형과 같은 답)
                    var old = PromotionEconomyFixture.Build(
                        PromotionEconomyFixture.FreePromotion, 0.40d, Horizon,
                        PromotionEconomyFixture.DailyGems, play,
                        PromotionEconomyFixture.TrialSecondsFloor, attempts,
                        PromotionEconomyFixture.NoFarm);
                    old.TrialFailureSecondsByGate = PromotionEconomyFixture.TrialSecondsFloor;

                    Row(report, label + " 교정전", attempts, old, "0");
                }
            }

            // 파밍 민감도 - **문1에만** 넣는다 (0.50배 화력이 실패하는 유일한 문)
            foreach (var minutes in new[] { 5d, 10d })
                foreach (var play in new[] { 1200d, 300d })
                {
                    string label = play >= 1200d ? "20분" : " 5분";

                    Row(report, label + " 문1파밍", 1d,
                        PromotionEconomyFixture.Build(
                            PromotionEconomyFixture.FreePromotion, 0.40d, Horizon,
                            PromotionEconomyFixture.DailyGems, play,
                            PromotionEconomyFixture.TrialSecondsFloor, 1d,
                            PromotionEconomyFixture.FarmAtGateOne(minutes * 60d)),
                        minutes + "분(문1)");
                }

            report.AppendLine();
            report.Append("실패 시간(실측, 하한 x0.35): ");
            foreach (var s in PromotionEconomyFixture.TrialFailureSecondsFloor)
                report.Append(s.ToString("F1") + "s ");

            Debug.Log(report.ToString());

            // 이 파일이 계약을 대신하지 않는다는 것만 최소로 못 박는다
            Assert.Greater(PromotionEconomyFixture.TrialFailureSecondsFloor.Length, 0);
        }

        static void Row(StringBuilder report, string label, double attempts,
                        PromotionEconomyLedger.Inputs input, string farm)
        {
            var r = PromotionEconomyLedger.Run(input);

            var days = new StringBuilder();
            for (int t = 0; t < r.TierDay.Length; t++)
            {
                days.Append(r.TierDay[t]);
                if (t < r.TierDay.Length - 1) days.Append(' ');
            }

            report.AppendLine(string.Format("{0} | {1:F1}회 | {2} | {3} | {4}일 | {5}일 | {6:F0}s | {7:P1}",
                label, attempts, farm, days,
                DayReaching(r, 150), DayReaching(r, 200),
                r.TrialSecondsTotal, r.TrialTimeShareToGate6));
        }

        static int DayReaching(PromotionEconomyLedger.Result result, int stage)
        {
            for (int day = 1; day < result.StageOnDay.Length; day++)
                if (result.StageOnDay[day] >= stage) return day;
            return -1;
        }
    }
}
