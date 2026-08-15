using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 승급 사다리의 **달력 경제** 검사 (1.6단계 개정).
     *
     * ## v1.5에서 재는 세계가 바뀌었다
     *
     * 승급·전직의 보석 비용이 **0**이 됐다. 배수도 이름도 기본 외형도 오라도
     * 귀문 돌파로 무료로 온다. 그래서 이 파일이 묻는 질문이 통째로 바뀐다:
     *
     *   v1.2~v1.4   "무과금이 며칠에 사다리를 다 사는가"
     *   v1.5        "승급이 무료가 되면 **다른 축이 얼마나 빨라지는가**"
     *
     * 그리고 처음으로 **귀문 전투 시간이 장부에 들어왔다.** v1.3까지는 스테이지
     * 전투 시간만 걸었고, 그래서 "st150까지 사흘"이 실제보다 낙관이었다.
     */
    public class PromotionEconomyTests
    {
        const int Horizon = 400;

        static PromotionEconomyLedger.Result Free(double allocation)
        {
            return PromotionEconomyLedger.Run(PromotionEconomyFixture.Build(
                PromotionEconomyFixture.FreePromotion, allocation, Horizon));
        }

        // ------------------------------------------------------------ 픽스처 동기화

        /**
         * @brief 구운 입력표가 아직 시뮬레이션과 같은 세계를 가리킨다.
         *
         * 장부의 입력은 `StageSimulation`에서 구운 값이다. 곡선이 움직이면 그
         * 표가 조용히 옛 세계를 가리키고, 그때 일수 계약은 아무 근거 없이
         * 통과한다 - "상수의 존재는 연결의 증거가 아니다"(33단계).
         */
        [Test]
        public void FixtureStillMatchesTheSimulation()
        {
            var rows = PromotionTrialFixture.GemFloor();

            Assert.AreEqual(PromotionEconomyFixture.Stages, rows.Count,
                "구운 표의 길이와 시뮬레이션의 길이가 다르다");

            for (int i = 0; i < rows.Count; i++)
                Assert.AreEqual(PromotionEconomyFixture.ProgressionGems[i], rows[i].GemsEarned,
                    string.Format("stage {0}: 구운 진행 보석 {1}, 실측 {2}",
                        i + 1, PromotionEconomyFixture.ProgressionGems[i], rows[i].GemsEarned));

            Assert.AreEqual(1060, 2 * Sum(PromotionEconomyFixture.GradeCosts),
                "장비 등급업 보석 총합이 바뀌었다 (EquipmentCurve.GradeGems)");
            Assert.AreEqual(PetCatalog.TotalUnlockGems, Sum(PromotionEconomyFixture.PetCosts),
                "동료 해금 보석 총합이 카탈로그와 다르다");
        }

        static int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++) total += values[i];
            return total;
        }

        // ------------------------------------------------------------ 무료라는 사실

        /**
         * @brief 승급이 **어떤 배정률에서도 보석을 한 개도 안 쓴다.**
         *
         * 이 검사가 v1.5의 구조를 코드로 붙잡는다. 유료 항이 다시 들어오면
         * 여기서 먼저 걸리고, 그때는 §밴드·초기 경제를 전부 다시 재야 한다.
         */
        [Test]
        public void PromotionCostsNothing()
        {
            foreach (var allocation in new[] { 0d, 0.25d, 0.40d, 1d })
            {
                var r = Free(allocation);

                Assert.AreEqual(0L, r.SpentPromotion, string.Format(
                    "alpha={0}: 승급에 보석 {1}개를 썼다 - v1.5의 승급 비용은 0이다",
                    allocation, r.SpentPromotion));

                for (int t = 0; t < r.TierDay.Length; t++)
                    Assert.Greater(r.TierDay[t], 0, string.Format(
                        "경지 {0}을 못 얻었다 - 비용이 0인데 막힐 이유가 없다", t + 1));
            }
        }

        /**
         * @brief 배정률이 **아무것도 바꾸지 않는다.**
         *
         * v1.2~v1.4에서 `alpha`가 문제였던 이유는 그것이 진행과 파워 수령을
         * 결정했기 때문이다. 비용이 0이면 alpha는 곱할 것이 없다 - 플레이어
         * 행동 가정이 계약에서 완전히 빠졌다는 것의 증인이다.
         */
        [Test]
        public void AllocationPolicy_NoLongerChangesAnything()
        {
            var baseline = Free(0d);

            foreach (var allocation in new[] { 0.25d, 0.40d, 1d })
            {
                var r = Free(allocation);

                Assert.AreEqual(baseline.ArmorDoneDay, r.ArmorDoneDay, "alpha가 방어구 완료일을 움직였다");
                Assert.AreEqual(baseline.WeaponDoneDay, r.WeaponDoneDay, "alpha가 무기 완료일을 움직였다");
                Assert.AreEqual(baseline.PetDoneDay, r.PetDoneDay, "alpha가 동료 완료일을 움직였다");
                Assert.AreEqual(baseline.SpentGacha, r.SpentGacha, "alpha가 뽑기 지출을 움직였다");
            }
        }

        // ------------------------------------------------------------ 장부의 무결성

        [Test]
        public void WalletNeverBecomesNegative()
        {
            var r = Free(0.40d);
            for (int day = 1; day < r.WalletOnDay.Length; day++)
                Assert.GreaterOrEqual(r.WalletOnDay[day], 0L, string.Format(
                    "{0}일차 지갑이 {1}이다 - 장부가 없는 보석을 썼다", day, r.WalletOnDay[day]));
        }

        [Test]
        public void EveryGemIsAccountedFor()
        {
            var r = Free(0.40d);
            long accounted = r.SpentPromotion + r.SpentArmor + r.SpentWeapon
                           + r.SpentPet + r.SpentGacha + r.Unused;

            Assert.AreEqual(r.TotalEarned, accounted, string.Format(
                "획득 {0}인데 장부 합이 {1}이다 - {2}개가 사라지거나 생겼다",
                r.TotalEarned, accounted, r.TotalEarned - accounted));
        }

        [Test]
        public void CoreSinksAreFullyPaidAndBounded()
        {
            var r = Free(0.40d);

            Assert.AreEqual(2L * Sum(PromotionEconomyFixture.GradeCosts),
                r.SpentArmor + r.SpentWeapon, "장비 등급 지출이 카탈로그 총합과 다르다");
            Assert.AreEqual((long)Sum(PromotionEconomyFixture.PetCosts), r.SpentPet,
                "동료 지출이 카탈로그 총합과 다르다");
        }

        // ------------------------------------------------------------ 통합 시간

        /**
         * @brief 귀문 시간이 **실제로 장부에 들어갔다.**
         *
         * v1.3까지 이 항이 없었다. 없으면 도달일이 낙관으로 나오고, 그 낙관 위에
         * 비용을 유도하면 두 번 틀린다.
         */
        [Test]
        public void TrialTimeIsCountedInTheLedger()
        {
            var withTrial = Free(0.40d);
            var withoutTrial = PromotionEconomyLedger.Run(PromotionEconomyFixture.Build(
                PromotionEconomyFixture.FreePromotion, 0.40d, Horizon,
                PromotionEconomyFixture.DailyGems, PromotionEconomyFixture.DailyPlaySeconds,
                new double[6], 1d, PromotionEconomyFixture.NoFarm));

            Assert.Greater(withTrial.TrialSecondsTotal, 0d, "귀문 시간이 0으로 집계됐다");

            double expected = 0d;
            double overhead = PromotionEconomyFixture.TrialEntrySeconds
                            + PromotionEconomyFixture.TrialResultSeconds
                            + PromotionEconomyFixture.TrialReturnSeconds;
            for (int g = 0; g < 6; g++)
                expected += PromotionEconomyFixture.TrialSecondsFloor[g] + overhead;

            Assert.AreEqual(expected, withTrial.TrialSecondsTotal, 1e-9d,
                "귀문 총 시간이 전투 + 오버헤드의 합과 다르다");

            // 최전선이 실제로 밀린다 - 시간이 장부에 들어갔다는 증거
            bool shifted = false;
            for (int day = 1; day <= 20; day++)
                if (withTrial.StageOnDay[day] < withoutTrial.StageOnDay[day]) shifted = true;

            Assert.IsTrue(shifted,
                "귀문 시간을 넣었는데 최전선이 한 번도 밀리지 않았다 - 시간이 어디에도 안 들어갔다");
        }

        /** 귀문 시간이 st150 도달 시간에서 차지하는 몫 */
        [Test]
        public void TrialTimeShare_IsReported()
        {
            var r = Free(0.40d);

            Assert.That(r.TrialTimeShareToGate6, Is.InRange(0.02d, 0.25d), string.Format(
                "귀문이 st150 도달 시간의 {0:P1}을 차지한다 - 이 값이 밴드 밖이면 "
                + "전투 시간이나 게이트 배치를 다시 봐야 한다", r.TrialTimeShareToGate6));
        }

        /**
         * @brief 재도전과 파밍이 **도달일을 되돌리지 않는다** (단조성).
         *
         * 도전 횟수를 늘리거나 파밍을 넣으면 최전선은 느려지기만 해야 한다.
         * 빨라지면 장부가 시간을 두 번 세거나 빼먹은 것이다.
         */
        [Test]
        public void MoreAttemptsAndFarming_OnlySlowProgress()
        {
            var fast = Free(0.40d);
            var slow = PromotionEconomyLedger.Run(PromotionEconomyFixture.Build(
                PromotionEconomyFixture.FreePromotion, 0.40d, Horizon,
                PromotionEconomyFixture.DailyGems, PromotionEconomyFixture.DailyPlaySeconds,
                PromotionEconomyFixture.TrialSecondsFloor, 1.5d,
                PromotionEconomyFixture.UniformFarm(600d)));

            for (int day = 1; day <= 30; day++)
                Assert.LessOrEqual(slow.StageOnDay[day], fast.StageOnDay[day], string.Format(
                    "{0}일차: 재도전·파밍을 늘렸는데 최전선이 앞섰다", day));

            Assert.Greater(slow.FarmSecondsTotal, 0d, "파밍 시간이 집계되지 않았다");
            Assert.Greater(slow.FarmedGold, 0d, "파밍 골드가 집계되지 않았다");
        }

        /**
         * @brief 파밍 되먹임의 크기를 **보고한다.**
         *
         * 하한 앵커에서는 파밍이 0이라 이 값이 0이지만, 다른 앵커를 고르면
         * 0이 아니다. 그때 얼마나 큰지를 미리 재 둔다 - 5분 파밍이 그 게이트
         * 보스 보상의 몇 배인지가 되먹임 모형이 필요한지를 정한다.
         */
        [Test]
        public void FarmingFeedback_IsLargeEnoughToNeedItsOwnModel()
        {
            var field = PromotionTrialFixture.FieldFromAssets();

            for (int g = 0; g < PromotionEconomyFixture.GateStages.Length; g++)
            {
                int stage = PromotionEconomyFixture.GateStages[g];
                double fiveMinutes = PromotionEconomyFixture.GateGoldPerSecond[g] * 300d;
                double bossGold = StageCurve.BossGoldForStage(
                    Onikiri.Core.BigDouble.FromDouble(field.AverageMobGold), stage).ToDouble();

                double ratio = fiveMinutes / bossGold;

                Assert.Greater(ratio, 5d, string.Format(
                    "게이트 {0}(st{1}): 5분 파밍이 보스 보상의 {2:F1}배뿐이다. "
                    + "되먹임이 이보다 작으면 파밍 모형 없이도 앵커를 고를 수 있다",
                    g + 1, stage, ratio));
            }
        }

        // ------------------------------------------------------------ 초기 경제

        /**
         * @brief 승급이 무료가 되어 **핵심 축이 실제로 빨라진다.**
         *
         * v1.3의 28일안(경지 1,050이 방어구 다음·동료 앞)과 나란히 잰다.
         * 원인이 총액이 아니라 **우선순위 자리**였다는 것을 이 비교가 보인다.
         */
        [Test]
        public void FreePromotion_UnblocksTheEarlyEconomy()
        {
            var free = Free(0.40d);
            var paid = PromotionEconomyLedger.Run(PromotionEconomyFixture.Build(
                new[] { 90, 140, 190, 200, 210, 220 }, 0.40d, Horizon));

            Assert.Less(free.WeaponDoneDay, paid.WeaponDoneDay, string.Format(
                "무기 완료: 무료 {0}일, 유료 {1}일 - 무료가 더 빠르지 않다",
                free.WeaponDoneDay, paid.WeaponDoneDay));

            Assert.Less(free.PetDoneDay, paid.PetDoneDay, string.Format(
                "동료 완료: 무료 {0}일, 유료 {1}일", free.PetDoneDay, paid.PetDoneDay));

            Assert.Greater(free.SpentGacha, paid.SpentGacha,
                "무료 세계가 뽑기에 더 못 쓴다 - 지갑이 어디로 샜다");
        }

        /**
         * @brief 30일 안에 **유료 뽑기를 실제로 돌릴 수 있다.**
         *
         * 상점(st14)·스킬 뽑기(st41)를 빠르게 체험시킨다는 기존 UX 방향과
         * 충돌하지 않는다는 것의 계약이다. v1.3의 유료안은 30일까지 0회였다.
         */
        [Test]
        public void PaidGachaIsReachableWithinThirtyDays()
        {
            var r = Free(0.40d);

            long earned = 0, core = 0;
            int day = 30;
            int stage = r.StageOnDay[day];
            earned = (long)(PromotionEconomyFixture.ProgressionGems[System.Math.Min(stage, 200) - 1]
                          + PromotionEconomyFixture.DailyGems * day);

            for (int i = 0; i < PromotionEconomyFixture.GradeCosts.Length; i++)
            {
                if (r.ArmorStepDay[i] > 0 && r.ArmorStepDay[i] <= day) core += PromotionEconomyFixture.GradeCosts[i];
                if (r.WeaponStepDay[i] > 0 && r.WeaponStepDay[i] <= day) core += PromotionEconomyFixture.GradeCosts[i];
            }
            for (int i = 0; i < PromotionEconomyFixture.PetCosts.Length; i++)
                if (r.PetStepDay[i] > 0 && r.PetStepDay[i] <= day) core += PromotionEconomyFixture.PetCosts[i];

            long pulls = (earned - core) / PromotionEconomyLedger.TenPullGems;

            Assert.GreaterOrEqual(pulls, 3L, string.Format(
                "30일에 10연을 {0}회밖에 못 돌린다 (획득 {1}, 핵심 지출 {2}) - "
                + "초기 상점·뽑기 체험이 막힌다", pulls, earned, core));
        }

        /** 동료 셋과 무기 등급이 **한 달 안에** 끝난다 */
        [Test]
        public void CoreAxesFinishWithinAMonth()
        {
            var r = Free(0.40d);

            Assert.LessOrEqual(r.WeaponDoneDay, 30, "무기 등급 완료가 한 달을 넘는다: " + r.WeaponDoneDay + "일");
            Assert.LessOrEqual(r.PetDoneDay, 30, "동료 전체 해금이 한 달을 넘는다: " + r.PetDoneDay + "일");
            Assert.LessOrEqual(r.ArmorDoneDay, 30, "방어구 등급 완료가 한 달을 넘는다: " + r.ArmorDoneDay + "일");
        }

        // ------------------------------------------------------------ 진행

        /**
         * @brief 게이트가 열리는 날과 경지를 얻는 날이 **같다.**
         *
         * 비용이 0이므로 대기가 없다. v1.3이 만든 "돌파는 사흘, 파워는 31일"의
         * 간격이 구조적으로 사라졌다는 것의 증인이다.
         */
        [Test]
        public void GateOpensAndTierArrivesOnTheSameDay()
        {
            foreach (var play in new[] { 1200d, 300d })
            {
                var r = PromotionEconomyLedger.Run(PromotionEconomyFixture.Build(
                    PromotionEconomyFixture.FreePromotion, 0.40d, Horizon,
                    PromotionEconomyFixture.DailyGems, play));

                for (int t = 0; t < r.TierDay.Length; t++)
                    Assert.AreEqual(r.GateOpenDay[t], r.TierDay[t], string.Format(
                        "하루 {0}분, 문 {1}: 열린 날 {2}, 얻은 날 {3} - 비용이 0인데 대기가 있다",
                        play / 60, t + 1, r.GateOpenDay[t], r.TierDay[t]));
            }
        }
    }
}
