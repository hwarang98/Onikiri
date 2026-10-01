using System;
using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 골드 획득 축을 회수 시간(payback)으로 검증한다.
     *
     * ## 왜 기존 효율 테스트에 못 넣는가
     *
     * UpgradeEfficiencyTests는 골드당 %DPS를 재고 축 사이 비율이 5배 안에 있는지
     * 본다. 이 축의 %DPS는 0이라 그 자로는 항상 "무한히 나쁨"이 나온다 - 10·11단계
     * 주석이 예고한 바로 그 문제다. 생존 축이 SurvivalEfficiencyTests로 갈라진 것과
     * 같은 이유로 여기서 따로 잰다.
     *
     * ## 무엇을 막는가
     *
     * 두 방향의 실패가 있고 둘 다 화면에서는 안 보인다.
     *
     * **너무 빠른 회수**는 스노볼이다. 이 축은 자기 수입을 늘리므로, 즉시 회수되면
     * "무조건 이것부터"가 정답이 되고 나머지 여섯이 전부 나중 문제가 된다. 화면에는
     * 멀쩡한 일곱 줄이 보이지만 실제 선택지는 하나뿐인 상태다.
     *
     * **너무 느린 회수**는 8단계 공격속도와 같은 죽은 버튼이다. 곡선상으로는 살아
     * 있고 아무도 안 산다.
     *
     * ## 64단계 - 상한이 사라졌다
     *
     * 20~26단계의 이 축은 해금 즉시 상한(x1.25)까지 사버리는 스위치였고, 그래서
     * 여기 검사 절반이 "상한에 닿았는가"를 재고 있었다. 64단계에 상한(Ceiling·
     * MaxLevel·StagesToCeiling)이 사라졌으므로 그 검사들은 뜻을 잃었고, 대신
     * **무한 축의 계약**을 잰다 - 기대 곡선(닫힌 식)이 실측과 맞는가, 보정이 축을
     * 따라가는가, 안 산 플레이어의 게임이 막히지 않는가, 회수가 계속 나빠지는가.
     */
    public class GoldGainAxisTests
    {
        /** 시뮬레이션이 쓰는 실제 필드값. EnemyTier 표에서 나온 가중 평균이다 */
        static StageSimulation.Field Field()
        {
            return new StageSimulation.Field
            {
                AverageMobHealth = 128d / 9d,
                AverageMobGold = 49d / 9d,
                SpawnInterval = 1.2f
            };
        }

        static List<StageSimulation.StageResult> Run(int through)
        {
            return StageSimulation.Run(through, Field());
        }

        static List<StageSimulation.StageResult> Run(int through, StageSimulation.Policy policy)
        {
            return StageSimulation.Run(through, Field(), policy);
        }

        // ---------------------------------------------------------------- (a) 밴드

        /**
         * @brief 축이 살아 있는 동안 회수 시간이 건강 밴드 안에 머문다.
         *
         * 64단계부터 축은 늘 살아 있다(상한이 없다). 스테이지가 끝난 시점의 회수
         * 시간이 하한 아래면 다음 칸이 즉시 이득이라는 뜻이고, 그러면 곡선 추종
         * 플레이어가 그 칸을 이미 샀어야 했다 - 구매 정책이 멈춘 자리의 회수
         * 시간은 임계값(120초) 위여야 한다.
         */
        [Test]
        public void GoldAxis_PaybackStaysInHealthyBand()
        {
            var rows = Run(20);

            foreach (var row in rows)
            {
                // 해금 전에는 살 수 없으므로 회수 시간이 밴드 밖이어도 무해하다.
                // 오히려 그 구간의 회수가 짧은 것이 게이트를 두는 이유다 -
                // 지표만 보면 사고 싶지만 실제로는 손해인 구간
                if (!GoldGainCurve.IsUnlockedAt(row.Stage)) continue;

                double payback = row.GoldGainPaybackSeconds;

                Assert.GreaterOrEqual(payback, GoldGainEfficiency.HealthyMinSeconds,
                    string.Format("st{0}: 회수 {1:F0}초는 즉시 이득에 가깝다 - 이 축이 "
                        + "다른 여섯을 제치고 스노볼한다. 비용 곡선을 가파르게 하라",
                        row.Stage, payback));
            }
        }

        /**
         * @brief 옛 상한 위의 칸은 **사는 순간에도** 밴드 안이다.
         *
         * 26단계가 밝힌 대로 첫 열두 칸(8골드 x1.25^L, 26단계의 스위치)은 해금
         * 스테이지에서 회수 9초에 팔린다 - 그 구간은 26단계가 근거와 함께
         * 유지한 값이고 64단계도 그대로 둔다(GoldGainCurve.BaseCost 주석).
         *
         * 이 검사는 **그 위**를 잰다. 옛 상한(Lv.13)을 넘어 성장 결(WallGrowth)로
         * 사는 칸은 전부 30~120초 사이여야 한다. 한 칸 사면 회수가 WallGrowth/Step
         * 배 나빠지므로 다음 칸은 수입이 그만큼 자란 뒤에야 임계값 아래로
         * 내려오고, 그 순간의 회수는 (120 x Step / WallGrowth)초 위다 - WallGrowth
         * 가 4.08을 넘으면 이 하한이 30초 아래로 내려간다. 실측은 이산 구매 타이밍
         * 때문에 그보다 위에 앉는다.
         */
        [Test]
        public void GoldAxis_PurchasesAboveTheOldCap_StayInsideTheBand()
        {
            var rows = Run(200);
            int measured = 0;

            foreach (var row in rows)
            {
                if (row.Stage < GoldGainCurve.UnlockStage + 2) continue;
                if (double.IsInfinity(row.GoldGainPaybackAtPurchase)) continue;
                measured++;

                Assert.GreaterOrEqual(row.GoldGainPaybackAtPurchase, GoldGainEfficiency.HealthyMinSeconds,
                    string.Format("st{0}: 옛 상한 위의 칸을 회수 {1:F0}초에 샀다 - 스노볼 쪽이다. "
                        + "WallGrowth를 내려라(지금 {2})",
                        row.Stage, row.GoldGainPaybackAtPurchase, GoldGainCurve.WallGrowth));

                Assert.LessOrEqual(row.GoldGainPaybackAtPurchase, GoldGainEfficiency.HealthyMaxSeconds,
                    string.Format("st{0}: 회수 {1:F0}초짜리 칸을 샀다 - 구매 정책이 밴드 밖을 산다",
                        row.Stage, row.GoldGainPaybackAtPurchase));
            }

            Assert.Greater(measured, 20,
                "옛 상한 위에서 산 스테이지가 스물도 안 된다 - 축이 다시 스위치가 됐다");
        }

        /**
         * @brief 1스테이지에서는 안 팔리고, 수입이 자란 뒤에 팔린다.
         *
         * **1스테이지에 팔리지 않는 것이 의도다.** 이 축은 회수에 2분 남짓이
         * 걸리는데 온보딩(1~5)이 3분이라, 거기서 사면 회수 전에 구간이 끝나
         * 화력만 밀린 채 보스를 만난다. BaseCost 4로 st1부터 팔리게 했더니
         * 1~5 소요 시간이 158초에서 178초로 늘었다.
         *
         * 그렇다고 영영 안 팔리면 죽은 버튼이므로, 두 조건을 함께 못 박는다 -
         * 시작 속도에서는 밴드 밖, 자란 속도에서는 밴드 안.
         */
        [Test]
        public void GoldAxis_SkipsTheOnboardingRate_ButSellsOnceIncomeGrows()
        {
            // E-3 후속의 온보딩 잡몹 완화까지 지난 **실제** st1 체력이다.
            // 완화 전 체력으로 재면 여기 결론이 화면과 다른 세계의 것이 된다
            var stats = StageSimulation.StartingStats;
            double killSeconds = StageSimulation.SecondsToKill(
                StageCurve.MobHealth(Onikiri.Core.BigDouble.FromDouble(
                    Field().AverageMobHealth), 1).ToDouble(), stats);
            double perKill = System.Math.Max(killSeconds, SpawnPacing.SettledInterval(killSeconds));
            double startingRate = Field().AverageMobGold / perKill;

            // E-3 후속으로 이 반쪽의 뜻이 뒤집혔다. 완화가 st1 처치를 당겨
            // 무강화 수입이 초당 6골드대가 됐고, 그 수입이면 회수(64초)가 이미
            // 밴드 안이다 - **온보딩에서 안 팔리는 것은 이제 가격이 아니라
            // 게이트의 일이다**(UnlockStage=6, GoldAxis_ContributesNothingBeforeUnlock).
            // 21단계가 "등장 시점은 비용이 아니라 게이트가 정한다"로 떼어낸
            // 분업이 문자 그대로가 됐고, 이 사실이 뒤집히면(회수가 다시 밴드
            // 밖으로) 그건 완화나 곡선이 움직였다는 신호라 여기 못 박는다
            Assert.IsTrue(GoldGainEfficiency.IsHealthy(
                    GoldGainEfficiency.PaybackSeconds(1, startingRate)),
                string.Format("완화된 온보딩 수입(초당 {0:F2}골드)에서 회수 {1:F0}초가 "
                    + "건강 밴드 밖이다 - 완화 크기나 비용 곡선이 움직였다",
                    startingRate, GoldGainEfficiency.PaybackSeconds(1, startingRate)));

            // 수입이 자라면 여전히 팔려야 한다. 이 조건이 없으면 죽은 곡선도
            // 통과한다
            double grownRate = startingRate * 3d;

            Assert.IsTrue(GoldGainEfficiency.WorthBuying(1, grownRate),
                string.Format("수입이 세 배가 돼도 안 팔린다 - 회수 {0:F0}초",
                    GoldGainEfficiency.PaybackSeconds(1, grownRate)));
        }

        // ---------------------------------------------------------------- (b) 죽은 버튼

        /**
         * @brief 곡선을 따라가는 플레이어가 이 축을 실제로 산다.
         *
         * 16단계에서 회복 축이 "곡선상으로는 살아 있고 20스테이지까지 한 번도
         * 안 팔린" 상태였다. 그것을 잡은 검사와 같은 계열이다 - 효율 비율이
         * 아니라 **구매 정책을 돌려서 팔리는지 본다.**
         *
         * 5스테이지를 기준으로 잡은 이유는 그때까지가 온보딩이기 때문이다. 그
         * 안에 한 번도 안 눌리는 버튼은 플레이어에게 존재하지 않는 것과 같다.
         */
        [Test]
        public void GoldAxis_IsActuallyBoughtEarly_NotADeadButton()
        {
            var rows = Run(GoldGainCurve.UnlockStage + 1);

            // 해금 전에는 한 번도 안 팔려야 한다. 게이트가 시뮬레이션 쪽에만
            // 빠지면 계산이 실제 플레이보다 후해진다
            Assert.AreEqual(1, rows[GoldGainCurve.UnlockStage - 2].GoldGainLevel,
                "해금 전인데 시뮬레이션이 이 축을 사고 있다");

            // 해금되면 곧바로 팔려야 한다. 열렸는데 아무도 안 사면 게이트가
            // 죽은 버튼을 더 늦게 보여주는 장치가 될 뿐이다
            Assert.Greater(rows[GoldGainCurve.UnlockStage - 1].GoldGainLevel, 1,
                string.Format("해금({0}스테이지)됐는데 한 번도 안 팔렸다",
                    GoldGainCurve.UnlockStage));
        }

        /**
         * @brief 상한이 없다 - 곡선 추종 플레이어는 **계속** 산다.
         *
         * 64단계의 본체다. 옛 세계에서는 st7에 Lv.13으로 굳은 뒤 900스테이지
         * 동안 한 칸도 안 팔렸다(MASTER). 지금은 균형 레벨이 스테이지마다
         * k칸씩 오르고(GoldGainCurve.LevelsPerStage), 실측 레벨이 그 직선을
         * 따라 끝없이 자라야 한다. 여기가 다시 어느 레벨에서 멈추면 어딘가에
         * 상한이 되살아난 것이다.
         */
        [Test]
        public void GoldAxis_HasNoCeiling_LevelKeepsGrowing()
        {
            var rows = Run(500);

            int at50 = rows[49].GoldGainLevel;
            int at100 = rows[99].GoldGainLevel;
            int at500 = rows[499].GoldGainLevel;

            Assert.Greater(at100, at50, string.Format(
                "st50 Lv.{0} -> st100 Lv.{1}: 축이 멈췄다 - 상한이 되살아났다", at50, at100));
            Assert.Greater(at500, at100, string.Format(
                "st100 Lv.{0} -> st500 Lv.{1}: 축이 멈췄다 - 상한이 되살아났다", at100, at500));

            // 값 곡선 자체에도 천장이 없어야 한다. 수천 레벨까지 단조 증가
            for (int level = 1; level < 3000; level++)
                Assert.Greater(GoldGainCurve.ValueAtLevel(level + 1), GoldGainCurve.ValueAtLevel(level),
                    "Lv." + level + "에서 배수가 안 오른다 - 값에 상한이 걸렸다");
        }

        /**
         * @brief 온보딩 구간에서 이 축이 **아무것도 하지 않는지.**
         *
         * 게이트의 존재 이유가 이것이다. 20단계에 축이 들어오면서 1~5 소요
         * 시간이 13% 늘었는데 그 구간의 액티브 이득은 0이라 순손실이었다.
         *
         * 절대 시간(`StageOneToFive_TakesTheDocumentedTime`)이 아니라 **기여**를
         * 검사한다. 시간은 다른 곡선을 손대도 움직이므로, 그것으로 이 게이트를
         * 재면 무관한 변경에 이 테스트가 깨진다.
         *
         * 두 조건이면 기여가 0인 것이 증명된다 - 축을 사지 않았고(레벨 1),
         * 보스도 그만큼 무거워지지 않았다(보정 1.0).
         */
        [Test]
        public void GoldAxis_ContributesNothingBeforeUnlock()
        {
            var rows = Run(GoldGainCurve.UnlockStage - 1);

            foreach (var row in rows)
            {
                Assert.AreEqual(1, row.GoldGainLevel,
                    string.Format("st{0}에서 이미 이 축을 샀다", row.Stage));

                Assert.AreEqual(1d, StageCurve.GoldAxisCompensation(row.Stage), 1e-9,
                    string.Format("st{0}에서 보스가 이미 무거워졌다 - 살 수도 없는 축의 "
                        + "이득을 상쇄하고 있다", row.Stage));
            }
        }

        /**
         * @brief 사고 나면 회수 시간이 나빠진다. 그것이 자기 제한이다.
         *
         * 이 축은 자기 수입을 늘리므로, 사면 살수록 다음 칸이 **싸지면** 무한
         * 루프가 된다. 비용 증가율이 값 증가율보다 커야 그 고리가 끊긴다 -
         * 두 비용 결(x1.25 / xWallGrowth) 모두 Step(x1.02)보다 가파르고, 그
         * 부등식이 "상한 없이도 스노볼하지 않는다"의 전부다. 관문(Lv.13)을
         * 지나는 자리도 함께 본다.
         */
        [Test]
        public void GoldAxis_PaybackWorsensAsYouBuy()
        {
            const double income = 100d;

            Assert.Greater(GoldGainCurve.CostGrowth, GoldGainCurve.Step,
                "첫 결의 비용 증가율이 값 증가율보다 완만하다 - 수렴 조건이 깨졌다");
            Assert.Greater(GoldGainCurve.WallGrowth, GoldGainCurve.Step,
                "성장 결의 비용 증가율이 값 증가율보다 완만하다 - 수렴 조건이 깨졌다");

            for (int level = 1; level < 200; level++)
            {
                double now = GoldGainEfficiency.PaybackSeconds(level, income);
                double next = GoldGainEfficiency.PaybackSeconds(level + 1, income);

                Assert.Greater(next, now,
                    string.Format("Lv.{0}->{1}에서 회수 시간이 짧아진다 - 같은 수입에서 "
                        + "다음 칸이 더 이득이면 골드가 있는 한 멈추지 않는다",
                        level, level + 1));
            }
        }

        /**
         * @brief 두 비용 결이 옛 상한에서 **이음새 없이** 만난다.
         *
         * 첫 결은 26단계의 스위치(8골드 x1.25^L)를 비트 단위로 보존해야 한다 -
         * 기존 세이브(v21)의 Lv.13 근방이 마이그레이션 없이 같은 비용·같은 배수로
         * 이어지는 근거가 이것이다. 두 번째 결은 도약 없이(WallJump 1) 첫 결의
         * 끝값에서 WallGrowth로 이어진다. 도약을 두면 옛 상한이 다시 벽으로
         * 읽힌다.
         */
        [Test]
        public void GoldAxis_CostRegimes_MeetAtTheOldCapWithoutAJump()
        {
            for (int level = 1; level < GoldGainCurve.WallFromLevel; level++)
            {
                double legacy = UpgradeCost.Quantize(8d * Math.Pow(1.25d, level - 1));
                Assert.AreEqual(legacy, GoldGainCurve.CostAtLevel(level), legacy * 1e-9d,
                    "Lv." + level + ": 26단계 스위치의 비용이 바뀌었다 - 기존 세이브의 열두 칸이 흔들린다");
            }

            double last = GoldGainCurve.CostAtLevel(GoldGainCurve.WallFromLevel - 1);
            double first = GoldGainCurve.CostAtLevel(GoldGainCurve.WallFromLevel);

            Assert.Greater(first, last, "관문에서 비용이 내려간다");
            Assert.LessOrEqual(first / last, GoldGainCurve.CostGrowth * 1.001d,
                string.Format("관문에서 비용이 x{0:F2} 뛴다 - 도약 없이 이어져야 한다", first / last));

            for (int level = GoldGainCurve.WallFromLevel; level < GoldGainCurve.WallFromLevel + 40; level++)
            {
                double ratio = GoldGainCurve.CostAtLevel(level + 1) / GoldGainCurve.CostAtLevel(level);
                Assert.AreEqual(GoldGainCurve.WallGrowth, ratio, 0.02d,
                    "Lv." + level + ": 성장 결의 증가율이 WallGrowth와 다르다");
            }
        }

        // ---------------------------------------------------------------- 기대 곡선

        /**
         * @brief 보정이 읽는 닫힌 식(ExpectedAtStage)이 시뮬레이션 실측과 맞는다.
         *
         * 옛 `GoldAxis_ReachesCeilingWhenExpected`의 후임이다. 그 검사는 "상한
         * 도달 시점"을 대조했고, 상한이 없는 지금은 **곡선 전체**를 대조한다 -
         * 균형 레벨 L* = UnlockLevel + k(stage - Unlock)의 직선이 실측 레벨의
         * 계단을 따라가는가. 26단계 StagesToCeiling 사고(상수만 두고 실측과
         * 대조하지 않아 보스만 먼저 무거워진 것)가 여기서 막힌다.
         *
         * 허용 오차 5%: 실측 레벨은 정수 계단이라 직선 주위를 한 칸(+2%) 안에서
         * 오르내리고, 코리더에서는 수입 성장이 1.72보다 조금 빨라(처치 속도가
         * 함께 오른다) 기울기가 잠깐 가파르다. 하네스 실측 최대 오차는 2~3%였다.
         */
        [Test]
        public void GoldAxis_ExpectedCurve_MatchesTheSimulation()
        {
            var rows = Run(500);
            const double tolerance = 0.05d;

            // 해금 스테이지의 실측 레벨이 닫힌 식의 시작점이다
            Assert.AreEqual(GoldGainCurve.UnlockLevel, rows[GoldGainCurve.UnlockStage - 1].GoldGainLevel, 1.0d,
                string.Format("해금 스테이지 실측 Lv.{0} vs UnlockLevel {1} - 시작점이 어긋났다",
                    rows[GoldGainCurve.UnlockStage - 1].GoldGainLevel, GoldGainCurve.UnlockLevel));

            foreach (var row in rows)
            {
                if (!GoldGainCurve.IsUnlockedAt(row.Stage)) continue;

                double expected = GoldGainCurve.ExpectedAtStage(row.Stage);
                double error = Math.Abs(expected / row.GoldGain - 1d);

                Assert.LessOrEqual(error, tolerance, string.Format(
                    "st{0}: 기대 배수 {1:F3} vs 실측 {2:F3} (Lv.{3}) - {4:P1} 어긋난다. 보정이 "
                    + "실제와 다른 축을 상쇄하고 있다. UnlockLevel이나 LevelsPerStage를 실측에 맞춰라",
                    row.Stage, expected, row.GoldGain, row.GoldGainLevel, error));
            }

            // 기울기도 대조한다. 절대 오차 5%는 넓어서 기울기가 조금 틀려도
            // 500스테이지 안에서는 통과할 수 있는데, 그 어긋남은 심층에서 누적된다
            int n = 0; double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int stage = 50; stage <= 500; stage++)
            {
                double x = stage, y = rows[stage - 1].GoldGainLevel;
                n++; sx += x; sy += y; sxx += x * x; sxy += x * y;
            }
            double slope = (n * sxy - sx * sy) / (n * sxx - sx * sx);

            Assert.AreEqual(GoldGainCurve.LevelsPerStage, slope, 0.03d, string.Format(
                "실측 기울기 {0:F3}칸/스테이지 vs 닫힌 식 k {1:F3} - 수입 성장이나 비용 결이 움직였다",
                slope, GoldGainCurve.LevelsPerStage));
        }

        /**
         * @brief 수렴 근거 - 균형 레벨의 기울기는 ln(G) / ln(WallGrowth / Step)이다.
         *
         * 지시서는 ln(1.72)/ln(CostGrowth)로 적었다. 실측은 분모가 CostGrowth가
         * 아니라 **CostGrowth/Step**임을 보였다 - 이 축의 배수가 자기 수입에
         * 곱해지므로 한 칸 살 때 회수 시간은 (비용 배수 / 값 배수)만큼만
         * 나빠진다. x1.25 결에서 지시서 값은 2.43, 실측은 2.67(= 이 식)이었다.
         * 그 되먹임이 있어도 분모가 양수인 한(WallGrowth > Step) k는 유한하고,
         * 그것이 "상한 없이도 스노볼하지 않는다"의 정확한 뜻이다.
         */
        [Test]
        public void GoldAxis_EquilibriumSlope_IsTheClosedForm()
        {
            double k = Math.Log(StageCurve.GoldGrowth) / Math.Log(GoldGainCurve.WallGrowth / GoldGainCurve.Step);

            Assert.AreEqual(k, GoldGainCurve.LevelsPerStage, 1e-12d);
            Assert.Greater(k, 0d, "기울기가 음수다 - 비용이 값보다 느리게 자란다");

            // 스테이지당 배수 성장 = Step^k. 수입 성장(1.72)보다 훨씬 작아야
            // 한다 - 그것이 "배수는 수입의 작은 거듭제곱"이라는 말의 뜻이다
            Assert.Less(GoldGainCurve.DripPerStage, 1.05d, string.Format(
                "스테이지당 배수 성장 x{0:F3} - 수입 성장의 눈에 띄는 몫이다. WallGrowth를 올려라",
                GoldGainCurve.DripPerStage));
        }

        // ---------------------------------------------------------------- 보정항

        /**
         * @brief 보정이 축을 **따라간다** - 해금 전 1, 해금 뒤 기대 배수의 거듭제곱.
         *
         * 옛 `GoldAxisCompensation_StopsWhereTheAxisStops`의 후임이다. 그때는
         * "축이 멈추는 곳에서 보정도 멈춘다"가 계약이었고, 지금 축은 멈추지
         * 않으므로 보정도 멈추지 않는다 - 단조 증가하고, 어떤 형태의 상한도
         * 없다. 지수는 1 아래여야 한다(액티브 이득이 남는다).
         */
        [Test]
        public void GoldAxisCompensation_FollowsTheAxisWithoutACeiling()
        {
            Assert.AreEqual(1d, StageCurve.GoldAxisCompensation(1), 1e-9,
                "1스테이지에는 축이 없으므로 보정도 없어야 한다");

            // **해금 전에는 보정이 없어야 한다.** 살 수 없는 축의 이득을
            // 상쇄하면 순손실이고, 그것이 온보딩을 갉아먹은 원인이었다
            Assert.AreEqual(1d,
                StageCurve.GoldAxisCompensation(GoldGainCurve.UnlockStage - 1), 1e-9,
                "해금 전인데 보스가 이미 무거워진다 - 있지도 않은 이득을 상쇄하고 있다");

            Assert.Greater(StageCurve.GoldAxisMarginExponent, 0d);
            Assert.Less(StageCurve.GoldAxisMarginExponent, 1d,
                "지수가 1 이상이다 - 축의 이득이 전부 상쇄돼 함정 버튼이 된다");

            for (int stage = GoldGainCurve.UnlockStage; stage < 1000; stage++)
            {
                double expected = Math.Pow(GoldGainCurve.ExpectedAtStage(stage), StageCurve.GoldAxisMarginExponent);
                Assert.AreEqual(expected, StageCurve.GoldAxisCompensation(stage), expected * 1e-9,
                    "st" + stage + ": 보정이 기대 배수의 거듭제곱이 아니다");

                Assert.Greater(StageCurve.GoldAxisCompensation(stage + 1), StageCurve.GoldAxisCompensation(stage),
                    "st" + stage + ": 보정이 자라지 않는다 - 상한이 걸렸다");
            }
        }

        /**
         * @brief 심층 램프가 드립의 잔여분을 흡수한다 - 닫힌 식끼리의 계약.
         *
         * 보정 지수 e는 축 성장의 e 몫만 상쇄한다. 심층(st51+)에서 DPS는 골드에
         * 1:1로 반응하므로(하네스 실측 탄성 1.0~1.02) 남은 (1-e) 몫은 스테이지마다
         * Step^(k(1-e))의 단조 발산이 되고, 그것을 심층 램프가 곱한다. 두 값이
         * 따로 적혀 있으면 한쪽만 고치는 날이 온다 - 유도로 묶였는지 여기서 본다.
         */
        [Test]
        public void DeepRamp_AbsorbsTheGoldDripResidual()
        {
            double residual = Math.Pow(GoldGainCurve.DripPerStage, 1d - StageCurve.GoldAxisMarginExponent);

            Assert.AreEqual(StageCurve.BossHealthRampDeepBase * residual, StageCurve.BossHealthRampDeep, 1e-12d,
                "심층 램프가 골드 축 드립의 잔여분과 묶여 있지 않다");
            Assert.Greater(StageCurve.BossHealthRampDeep, StageCurve.BossHealthRampDeepBase);
            Assert.Less(StageCurve.BossHealthRampDeep, StageCurve.BossHealthRampDeepBase * 1.02d,
                "램프 보정이 2%를 넘는다 - 드립이 너무 빠르다");
        }

        // ---------------------------------------------------------------- 안 산 플레이어

        /**
         * @brief 이 축을 **한 번도 안 산** 플레이어의 게임이 조율 구간(st1~50)에서 막히지 않는다.
         *
         * 64단계의 핵심 위험이다. 보정은 스테이지의 함수라 플레이어를 구분하지
         * 못하므로, 안 산 플레이어는 "산 사람 기준으로 무거워진 보스"를 축 없이
         * 상대한다. 축이 상한 없이 자라면 보정도 자라고, 26단계 x3 실험(보스
         * 2.8배 -> 안 산 플레이어에게 게임이 통째로 2.8배 어려움)이 재현된다.
         *
         * 바닥은 무과금 바닥(1.4/1.25/1.08)이 아니라 그 아래 **클리어 바닥**
         * (1.35/1.15/1.0)이다. 안 산 플레이어는 최적 빌드가 아니라 아홉 축 중
         * 하나를 버린 빌드이고, 계약은 "무과금이 노력으로 깬다"가 아니라 "이
         * 축을 몰라도 게임이 막히지 않는다"다. 옛 세계의 안 산 플레이어는
         * st15 챕터 1.24 / st20 피날레 1.08이었다 - 무과금 바닥은 이미 그때부터
         * 못 지키고 있었다. 64단계 실측은 st20 피날레 1.018 / st30 1.037이다 -
         * 지수 0.50이 그 자리를 정했다(피날레가 챕터보다 조이는 코리더 순서를
         * 지키려면 지수를 더 내릴 수 없다, StageCurve.GoldAxisMarginExponent).
         * 심층(st51+)은 재지 않는다 - 그곳에서 이 축은 골드로 사는 다른 축들과
         * 같이 필수다.
         */
        [Test]
        public void GoldAxis_SkippingPlayer_StillClearsTheTunedZone()
        {
            var rows = Run(50, new StageSimulation.Policy { SkipGoldGain = true });

            foreach (var row in rows)
            {
                var tier = BossCurve.TierOf(row.Stage);
                double floor = tier == BossCurve.Tier.Finale ? 1.0d
                             : tier == BossCurve.Tier.Chapter ? 1.15d : 1.35d;
                string kind = tier == BossCurve.Tier.Finale ? "피날레"
                            : tier == BossCurve.Tier.Chapter ? "챕터" : "일반";

                Assert.GreaterOrEqual(row.BossMargin, floor, string.Format(
                    "st{0}({1}): 골드 축을 안 산 플레이어의 여유 {2:F2} - 보정(x{3:F3})이 이 축을 "
                    + "필수로 만들었다. GoldAxisMarginExponent를 낮추거나 WallGrowth를 올려라",
                    row.Stage, kind, row.BossMargin, StageCurve.GoldAxisCompensation(row.Stage)));

                Assert.IsTrue(row.Survived, string.Format(
                    "st{0}: 골드 축을 안 산 플레이어가 보스전에서 죽는다", row.Stage));
            }
        }
    }
}
