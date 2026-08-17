using System;
using NUnit.Framework;
using Onikiri.DevTools;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief k 후보 × 문 × 프로필 전체 비교 (승급 5.0단계 §3).
     *
     * ## 밴드가 둘로 갈린다 - 섞지 않는다
     *
     *   하드 계약   하한 `[35,55]` · 중간 `[34.5,55]` · 추종 `>=25` ·
     *               격노 여유 `>=2` · 0.70x 통과 / 0.35x 실패
     *   선호 목표   Step3이 제시한 `35~45`초
     *
     * 선호 목표는 자동 검사에 없다는 이유로 폐기하지 않는다. 하드를 통과한 후보
     * 사이에서 선호에 더 가까운 쪽을 고르는 자로 쓴다.
     *
     * ## 프레임이 둘이다 - 그리고 **하드 계약이 서 있는 프레임은 하나다**
     *
     *   즉시 도전     P = `row.ExpectedDps` / ref.
     *                 **하드 계약(`[35,55]` 등)이 이 프레임 기준이다**
     *   쇼핑 후 도전  P = 구매 뒤 화력 / ref. 게이트 보스의 보상을 쓴 같은 플레이어
     *
     * 둘 다 실재한다 - `BossFight.OnBossKilled`이 게이트에서도 보상을 평소대로
     * 주고(멈추는 것은 진행뿐이다) 강화 화면은 입장 전까지 열려 있다.
     *
     * **그러나 쇼핑 후의 단축은 계약 위반이 아니다.** 그것은 플레이어가 방금 벤
     * 보스의 보상으로 **성장한 결과**이고, 시험이 빨라지는 것이 성장의 뜻이다.
     * 45초 앵커는 "보상을 쓰기 전의 하한 플레이어도 통과한다"는 보장이고, 그
     * 보장은 즉시 도전 프레임에서 재는 것이 맞다.
     *
     * 그래서 이 파일의 검사는 둘로 갈린다:
     *
     *   즉시 프레임    **계약**을 잰다 (Cleared · 밴드 · 25초 바닥 · 격노 여유)
     *   쇼핑 후 프레임 **계측**한다. 유일하게 계약으로 남는 것은 "시험이 형식이
     *                  되지 않는다"(25초 바닥)뿐이다 - 그 아래로 내려가면 성장이
     *                  아니라 시험의 소멸이고, 그것이 k=0.60을 기각한 근거다
     */
    public class PromotionSoftCapMatrixTests
    {
        const int Gates = 6;

        /** 후보 셋. `PromotionTrialTests`가 쓰는 것과 같은 목록이다 */
        static readonly double[] Candidates = { 0.35d, 0.45d, 0.60d };

        // 하드 계약
        const double FloorBandLow = 35d;
        const double BandHigh = 55d;
        const double StrongFloorSeconds = 25d;

        // 선호 목표 (Step3)
        const double PreferredLow = 35d;
        const double PreferredHigh = 45d;

        static PromotionTrialSimulation.Rules Rules()
        {
            return PromotionTrialFixture.DefaultRules();
        }

        /** 밴드가 앵커로 쓴 플레이어 - 보상을 쓰기 전 */
        static PromotionTrialSimulation.Player OnArrival(
            System.Collections.Generic.List<StageSimulation.StageResult> rows, int gate)
        {
            return PromotionTrialFixture.PlayerAt(rows, PromotionTrialFixture.GateStages[gate - 1]);
        }

        /**
         * @brief 같은 플레이어가 **게이트 보스의 보상을 쓴 뒤.** 성장한 상태다.
         *
         * 화력만 올라간다 - 체력·재생은 `StageResult`가 이미 구매 뒤의 값을 담고
         * 있으므로(`levels.MaxHealth`) 그대로 쓴다. 즉 이 플레이어는 밴드 앵커와
         * **체력이 같고 화력만 큰** 자이고, 그래서 두 프레임의 차이가 정확히
         * 소프트캡이 성장을 얼마나 남기는지로 남는다.
         */
        static PromotionTrialSimulation.Player AfterShopping(
            System.Collections.Generic.List<StageSimulation.StageResult> rows, int gate)
        {
            int stage = PromotionTrialFixture.GateStages[gate - 1];
            var row = rows[stage - 1];

            var player = PromotionTrialFixture.PlayerAt(rows, stage);
            player.Dps = TrialPresetForge.Measure(row, PromotionTrialFixture.ReferencePowerForGate(gate)).PresetDps;
            return player;
        }

        static PromotionTrialSimulation.Result Run(PromotionTrialSimulation.Player raw, int gate, double k)
        {
            return PromotionTrialSimulation.Run(
                PromotionTrialFixture.Capped(raw, gate, k),
                PromotionTrialFixture.FoesForGate(gate), Rules());
        }

        // ------------------------------------------------------------ 프레임 1

        /**
         * @brief 즉시 도전 프레임에서는 **셋 다 하드 계약을 통과한다.**
         *
         * 2단계 이후 계속 그랬다(격차가 x3.61에서 x1.91로 무너진 뒤로). 출시값
         * 0.45가 이 계약 안에 서 있다는 것이 이 검사의 요점이고, 그것이 5.0단계가
         * 0.45를 유지한 근거다.
         */
        [Test]
        public void OnArrival_EveryCandidateHoldsTheHardContracts()
        {
            var floor = PromotionTrialFixture.GemFloor();
            var lead = PromotionTrialFixture.Lead();

            foreach (var k in Candidates)
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var f = Run(OnArrival(floor, gate), gate, k);
                    Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, f.Outcome,
                        string.Format("k={0} 문{1}: 하한이 {2}로 끝났다", k, gate, f.Outcome));
                    Assert.That(f.Seconds, Is.InRange(FloorBandLow, BandHigh), string.Format(
                        "k={0} 문{1}: 하한 {2:F1}초가 하드 밴드 밖이다", k, gate, f.Seconds));

                    var l = Run(OnArrival(lead, gate), gate, k);
                    Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, l.Outcome,
                        string.Format("k={0} 문{1}: 추종이 {2}로 끝났다", k, gate, l.Outcome));
                    Assert.GreaterOrEqual(l.Seconds, StrongFloorSeconds, string.Format(
                        "k={0} 문{1}: 추종 {2:F1}초가 25초 바닥 아래다", k, gate, l.Seconds));
                    Assert.GreaterOrEqual(PromotionTrialSimulation.EnrageHeadroom(Rules(), l), 2d,
                        string.Format("k={0} 문{1}: 격노 여유가 2배 미만이다", k, gate));
                }
        }

        // ------------------------------------------------------------ 프레임 2

        /**
         * @brief 쇼핑 후 프레임 — **출시값 0.45는 시험을 시험으로 남긴다.**
         *
         * ## 이 프레임에서 계약으로 남는 것은 하나다
         *
         * 쇼핑 후에 시간이 줄어드는 것은 성장의 결과이고, 그것을 밴드 위반으로
         * 읽지 않는다(클래스 머리 주석). 그래도 **바닥은 있다** - 25초 아래로
         * 내려가면 그것은 빨라진 것이 아니라 시험이 없어진 것이다(v1.3의 15.4초가
         * 승인받지 못한 자리).
         *
         * ## 실측 (5.0단계)
         *
         *   k=0.45   하한 34.9~36.3 · 추종 **28.0**~34.4   전부 25초 위 -> 채택
         *   k=0.60   하한 32.1~33.8 · 추종 **24.1**        25초 아래 -> **기각**
         *   k=0.35   하한 36.9~38.1 · 추종 31.0~36.5      통과하지만 채택 안 함
         *            (성장 체감이 가장 적다 - 화력 2배가 x1.27뿐)
         *
         * 0.60을 기각한 근거가 그 24.1초 하나다. 그리고 0.45가 그 프레임에서도
         * 28.0초를 남긴다는 사실이 출시값을 유지한 근거다.
         */
        [Test]
        public void AfterShopping_TheShippedExponentKeepsTheTrialFromBecomingFormal()
        {
            var floor = PromotionTrialFixture.GemFloor();
            var lead = PromotionTrialFixture.Lead();

            double shippedLeadMin = double.MaxValue;
            double shippedFloorMin = double.MaxValue;
            double highestLeadMin = double.MaxValue;

            for (int gate = 1; gate <= Gates; gate++)
            {
                // ---- 출시값. 성장해서 들어온 플레이어도 여전히 시험을 치른다
                var f = Run(AfterShopping(floor, gate), gate, PromotionTrialCatalog.SoftCapExponent);
                var l = Run(AfterShopping(lead, gate), gate, PromotionTrialCatalog.SoftCapExponent);

                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, f.Outcome,
                    string.Format("문{0}: 쇼핑 후 하한이 {1}로 끝났다 - 캡이 성장을 죽였다", gate, f.Outcome));
                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, l.Outcome,
                    string.Format("문{0}: 쇼핑 후 추종이 {1}로 끝났다", gate, l.Outcome));

                Assert.GreaterOrEqual(l.Seconds, StrongFloorSeconds, string.Format(
                    "문{0}: 쇼핑 후 추종이 {1:F1}초에 끝났다 - 25초 아래면 귀문이 형식이 된다",
                    gate, l.Seconds));

                if (l.Seconds < shippedLeadMin) shippedLeadMin = l.Seconds;
                if (f.Seconds < shippedFloorMin) shippedFloorMin = f.Seconds;

                // ---- 기각한 후보. 같은 프레임에서 바닥을 뚫는다
                var high = Run(AfterShopping(lead, gate), gate, 0.60d);
                if (high.Seconds < highestLeadMin) highestLeadMin = high.Seconds;
            }

            // 크기도 굳힌다. 임계만 재면 "간신히"와 "여유롭게"가 구분되지 않는다
            Assert.That(shippedLeadMin, Is.InRange(27d, 30d),
                "출시값의 쇼핑 후 추종 최속 (5.0단계 실측 28.0초)");
            Assert.That(shippedFloorMin, Is.InRange(34d, 37d),
                "출시값의 쇼핑 후 하한 최속 (5.0단계 실측 34.9초)");

            Assert.Less(highestLeadMin, StrongFloorSeconds, string.Format(
                "k=0.60의 쇼핑 후 추종 최속이 {0:F1}초로 25초 위다. 5.0단계 실측은 24.1초(문5)이고 "
                + "그것이 그 후보를 기각한 근거였다 - 근거가 사라졌으면 다시 재야 한다",
                highestLeadMin));
        }

        /**
         * @brief 선호 밴드(35~45)까지의 거리 — **계측이고 결정 근거가 아니다.**
         *
         * 두 프레임의 24칸에서 35 아래·45 위로 벗어난 몫을 합한다. 실측:
         * 10.6 / 24.1 / 61.9초. 작은 k가 가깝다.
         *
         * 이 자가 k를 정하지 않는다 - 쇼핑 후의 단축은 성장의 결과이므로 그
         * 이탈분은 "빨라진 만큼"이고, 그것을 벌점으로 세면 성장을 벌주는 셈이다.
         * 여기서 굳히는 것은 **순서**뿐이다: k가 커지면 이탈이 커진다는 성질이
         * 뒤집히면 소프트캡의 방향이 바뀐 것이고, 그때는 §3 전체를 다시 읽어야 한다.
         */
        [Test]
        public void TheDistanceToThePreferredBand_GrowsWithK()
        {
            var floor = PromotionTrialFixture.GemFloor();
            var lead = PromotionTrialFixture.Lead();

            var shortfall = new double[Candidates.Length];

            for (int i = 0; i < Candidates.Length; i++)
            {
                double k = Candidates[i];
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var times = new[]
                    {
                        Run(OnArrival(floor, gate), gate, k).Seconds,
                        Run(OnArrival(lead, gate), gate, k).Seconds,
                        Run(AfterShopping(floor, gate), gate, k).Seconds,
                        Run(AfterShopping(lead, gate), gate, k).Seconds
                    };

                    foreach (var t in times)
                    {
                        if (t < PreferredLow) shortfall[i] += PreferredLow - t;
                        if (t > PreferredHigh) shortfall[i] += t - PreferredHigh;
                    }
                }
            }

            for (int i = 1; i < Candidates.Length; i++)
                Assert.Less(shortfall[i - 1], shortfall[i], string.Format(
                    "선호 밴드 이탈 합이 k={0}에서 {1:F1}초, k={2}에서 {3:F1}초다 - "
                    + "순서가 뒤집혔으면 §3의 선택 기준 2가 다른 답을 낸다",
                    Candidates[i - 1], shortfall[i - 1], Candidates[i], shortfall[i]));
        }

        // ------------------------------------------------------------ 단조성

        /**
         * @brief **화력을 올리면 실효도 오르고 시간은 줄어든다.** 어떤 k에서도.
         *
         * 두 끝점 사이 어디에 서든 역전이 없다는 것을 이것이 보장한다 - 그래서
         * Floor와 CurveFollower 둘만 실측해도 사이의 플레이어를 말할 수 있다.
         */
        [Test]
        public void MoreRawPower_NeverMeansLessEffectivePower_NorALongerClear()
        {
            var floor = PromotionTrialFixture.GemFloor();

            foreach (var k in Candidates)
                for (int gate = 1; gate <= Gates; gate++)
                {
                    int stage = PromotionTrialFixture.GateStages[gate - 1];
                    double reference = PromotionTrialFixture.ReferencePowerForGate(gate);
                    var seed = PromotionTrialFixture.PlayerAt(floor, stage);

                    double previousEffective = -1d;
                    double previousSeconds = double.MaxValue;

                    for (double p = 0.30d; p <= 12.0001d; p *= 1.10d)
                    {
                        var player = seed;
                        player.Dps = reference * p;

                        double effective = TrialPowerScore.EffectivePower(player.Dps, reference, k);
                        Assert.Greater(effective, previousEffective, string.Format(
                            "k={0} 문{1}: P={2:F3}에서 실효 화력이 줄었다 - 올릴수록 손해가 된다",
                            k, gate, p));
                        previousEffective = effective;

                        var result = Run(player, gate, k);
                        if (result.Outcome != PromotionTrialSimulation.Outcome.Cleared) continue;

                        Assert.LessOrEqual(result.Seconds, previousSeconds + 1e-9d, string.Format(
                            "k={0} 문{1}: P={2:F3}에서 클리어 시간이 늘었다", k, gate, p));
                        previousSeconds = result.Seconds;
                    }
                }
        }

        /**
         * @brief **남는 성장의 몫이 정확히 k다.** "k가 클수록 성장이 더 남는다"의 산수.
         *
         * 실효 화력이 `ref x P^k`이므로 화력을 두 배로 올리면 실효는 `2^k`배가
         * 된다. 그 값이 곧 "강화가 귀문에서 얼마나 실감되는가"이고, §3의 선택
         * 기준 3(체감)과 기준 2(선호 밴드)가 **서로 반대 방향으로** 당기는 이유다.
         */
        [Test]
        public void TheSurvivingShareOfGrowth_IsExactlyK()
        {
            const double reference = 1000d;

            foreach (var k in Candidates)
            {
                double before = TrialPowerScore.EffectivePower(reference * 2d, reference, k);
                double after = TrialPowerScore.EffectivePower(reference * 4d, reference, k);

                Assert.AreEqual(Math.Pow(2d, k), after / before, 1e-12d, string.Format(
                    "k={0}: 화력을 두 배로 올렸을 때 실효가 2^k배가 아니다", k));
            }

            // 큰 k가 더 많이 남긴다 - 순서가 뒤집히면 체감 기준이 무의미해진다
            for (int i = 1; i < Candidates.Length; i++)
                Assert.Less(Math.Pow(2d, Candidates[i - 1]), Math.Pow(2d, Candidates[i]),
                    "k가 커졌는데 남는 성장의 몫이 줄었다");
        }

        /** 기준 이하에는 보정이 걸리지 않는다 - "과잉 화력만" 줄인다는 규칙 그대로 */
        [Test]
        public void AtOrBelowTheReference_NothingIsScaled()
        {
            foreach (var k in Candidates)
                for (int gate = 1; gate <= Gates; gate++)
                {
                    double reference = PromotionTrialFixture.ReferencePowerForGate(gate);

                    foreach (var share in new[] { 0.10d, 0.50d, 0.90d, 1.00d })
                        Assert.AreEqual(1d, TrialPowerScore.DamageScale(reference * share, reference, k), 0d,
                            string.Format("k={0} 문{1}: 기준의 {2:P0}인데 배율이 걸렸다", k, gate, share));
                }
        }

        /**
         * @brief 25초 바닥에 닿는 화력이 **작은 k에서 더 멀다.** 과화력 여유의 자다.
         *
         * 시험이 형식이 되는 지점을 P로 적으면 `(41/21)^(1/k)`다. 실측:
         * 6.76 / 4.42 / 3.05. 추종의 최대 P가 1.907이므로 여유는 각각
         * x3.55 / x2.32 / x1.60이다 - 큰 k에서는 추종보다 60%만 더 세면
         * 귀문이 형식이 된다.
         */
        [Test]
        public void TheHeadroomBeforeTheTrialBecomesFormal_ShrinksAsKGrows()
        {
            double previous = double.MaxValue;

            foreach (var k in Candidates)
            {
                double atFloor = Math.Pow(
                    (TrialPowerScore.ReferenceSeconds) / (StrongFloorSeconds - 2d * PromotionTrialCatalog.SwapSeconds),
                    1d / k);

                Assert.Less(atFloor, previous, string.Format(
                    "k={0}: 25초 바닥에 닿는 P가 {1:F2}로 앞 후보보다 크다 - "
                    + "작은 k가 더 오래 시험을 시험으로 남긴다는 성질이 깨졌다", k, atFloor));
                previous = atFloor;
            }
        }
    }
}
