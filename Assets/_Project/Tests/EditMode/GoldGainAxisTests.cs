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

        // ---------------------------------------------------------------- (a) 밴드

        /**
         * @brief 축이 살아 있는 동안 회수 시간이 건강 밴드 안에 머문다.
         *
         * 상한에 닿은 뒤는 검사하지 않는다. 그때는 회수가 영원히 안 되는 것이
         * 맞고(더 살 수 없으므로), 무한대는 실패가 아니라 "완성"의 표현이다.
         */
        [Test]
        public void GoldAxis_PaybackStaysInHealthyBand()
        {
            var rows = Run(20);

            foreach (var row in rows)
            {
                if (row.GoldGainLevel >= GoldGainCurve.MaxLevel) continue;

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
         * 루프가 된다. 비용 증가율이 값 증가율보다 커야 그 고리가 끊긴다.
         */
        [Test]
        public void GoldAxis_PaybackWorsensAsYouBuy()
        {
            const double income = 100d;

            for (int level = 1; level < GoldGainCurve.MaxLevel - 1; level++)
            {
                double now = GoldGainEfficiency.PaybackSeconds(level, income);
                double next = GoldGainEfficiency.PaybackSeconds(level + 1, income);

                Assert.Greater(next, now,
                    string.Format("Lv.{0}->{1}에서 회수 시간이 짧아진다 - 같은 수입에서 "
                        + "다음 칸이 더 이득이면 골드가 있는 한 상한까지 멈추지 않는다",
                        level, level + 1));
            }
        }

        // ---------------------------------------------------------------- 보정항

        /**
         * @brief 보스 체력 보정이 쓰는 상한 도달 시점이 실측과 맞는다.
         *
         * StagesToCeiling은 보정 곡선의 모양을 정하는 값인데, 곡선 계수를 손대면
         * 실제 도달 시점이 움직인다. 13단계에서 보스 등급 배수를 선언만 하고
         * 쓰지 않은 적이 있어서, 이런 값은 선언이 아니라 실측과 대조한다.
         */
        [Test]
        public void GoldAxis_ReachesCeilingWhenExpected()
        {
            var rows = Run(12);

            int actual = -1;
            foreach (var row in rows)
                if (row.GoldGainLevel >= GoldGainCurve.MaxLevel) { actual = row.Stage; break; }

            Assert.Greater(actual, 0, "30스테이지 안에 상한에 닿지 않는다");

            // 보정이 가정하는 도달 시점 = 해금 + StagesToCeiling.
            // 한 스테이지 오차는 허용한다 - 보정은 근사식이고 그 정도 어긋남은
            // 여유에 5% 안쪽으로 들어온다(GoldGainCurve.ExpectedAtStage 참고)
            int assumed = GoldGainCurve.UnlockStage + GoldGainCurve.StagesToCeiling;

            Assert.That(actual, Is.InRange(assumed, assumed + 1),
                string.Format("실제 상한 도달은 st{0}인데 보정은 st{1}을 가정한다. "
                    + "StagesToCeiling(지금 {2})을 맞추거나 곡선을 되돌려라",
                    actual, assumed, GoldGainCurve.StagesToCeiling));
        }

        /**
         * @brief 보정이 축과 같은 곳에서 멈춘다.
         *
         * 램프가 아니라 보정항을 쓴 이유가 이것이다. 축이 상한에서 멈추는데
         * 보스만 계속 무거워지면, 축을 다 산 플레이어가 스테이지마다 손해를 본다.
         */
        [Test]
        public void GoldAxisCompensation_StopsWhereTheAxisStops()
        {
            int ceilingStage = GoldGainCurve.UnlockStage + GoldGainCurve.StagesToCeiling;

            double atCeiling = StageCurve.GoldAxisCompensation(ceilingStage);
            double farLater = StageCurve.GoldAxisCompensation(60);

            Assert.AreEqual(atCeiling, farLater, 1e-9,
                "보정이 상한 뒤에도 계속 자란다 - 램프와 같은 실수다");

            Assert.AreEqual(1d, StageCurve.GoldAxisCompensation(1), 1e-9,
                "1스테이지에는 축이 없으므로 보정도 없어야 한다");

            // **해금 전에는 보정이 0이어야 한다.** 살 수 없는 축의 이득을
            // 상쇄하면 순손실이고, 그것이 온보딩을 갉아먹은 원인이었다
            Assert.AreEqual(1d,
                StageCurve.GoldAxisCompensation(GoldGainCurve.UnlockStage - 1), 1e-9,
                "해금 전인데 보스가 이미 무거워진다 - 있지도 않은 이득을 상쇄하고 있다");
        }
    }
}
