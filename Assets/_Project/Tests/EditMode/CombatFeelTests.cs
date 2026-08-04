using NUnit.Framework;
using Onikiri.Battle;

namespace Onikiri.Tests
{
    /**
     * @brief 히트스톱과 흔들림은 사무라이가 초당 한 번쯤 휘두르는 초반 기준으로 맞춰져 있다.
     *
     * 공격속도가 핵심 성장 축이라 후반에는 초당 10회에 도달하는데, 그 시점에 70ms
     * 고정 정지라면 시간의 70%가 정지 상태가 되어 게임이 망가진 것처럼 보인다.
     * 이 테스트들이 그것을 막는 예산 규칙을 못 박는다.
     */
    public class CombatFeelTests
    {
        // 빌더가 실제로 기록하는 값
        const float HitStopBase = 0.07f;
        const float HitStopBudget = 0.3f;

        [Test]
        public void BelowCrossover_UsesTheAuthoredDuration()
        {
            // 0.3 / 1.15 = 0.26. 설계값 0.07보다 훨씬 크므로 아무것도 잘리지 않는다
            Assert.AreEqual(0.07f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 1.15f), 1e-5f);
            Assert.AreEqual(0.07f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 0.5f), 1e-5f);
        }

        [Test]
        public void AboveCrossover_ShortensToTheBudget()
        {
            // 이 규칙이 존재하는 이유인 경우. 초당 10회 공격
            Assert.AreEqual(0.03f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 10f), 1e-5f);
            Assert.AreEqual(0.015f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 20f), 1e-5f);
        }

        [Test]
        public void TotalFreezeTimePerSecond_StaysBounded()
        {
            // 실제 보장. 아무리 빨리 공격해도 정지가 매 초에서 차지하는 비율이
            // 예산을 넘지 않는다
            float[] rates = { 0.5f, 1f, 1.15f, 4f, 10f, 25f, 100f };
            foreach (var rate in rates)
            {
                float perHit = CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, rate);
                float perSecond = perHit * rate;
                Assert.LessOrEqual(perSecond, HitStopBudget + 1e-4f,
                    "freeze occupied " + perSecond + "s of every second at " + rate + " attacks/sec");
            }
        }

        [Test]
        public void Crossover_IsWhereTheTwoRulesMeet()
        {
            float crossover = CombatFeel.CrossoverAttackSpeed(HitStopBase, HitStopBudget);
            Assert.AreEqual(0.3f / 0.07f, crossover, 1e-4f);

            // 교차점 양쪽에서 더 짧은 규칙이 이긴다
            Assert.AreEqual(HitStopBase, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, crossover - 0.5f), 1e-5f);
            Assert.Less(CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, crossover + 0.5f), HitStopBase);
        }

        [Test]
        public void DurationNeverIncreases()
        {
            // 더 빨리 공격한다고 효과가 길어지는 일은 없어야 한다
            float previous = float.MaxValue;
            for (float rate = 0.5f; rate <= 30f; rate += 0.5f)
            {
                float current = CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, rate);
                Assert.LessOrEqual(current, previous + 1e-6f, "duration grew at " + rate + " attacks/sec");
                previous = current;
            }
        }

        [Test]
        public void ZeroOrNegativeRate_FallsBackToTheAuthoredDuration()
        {
            // 공격 속도가 설정되기 전 시작 시점의 0 나누기를 방어한다
            Assert.AreEqual(HitStopBase, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 0f), 1e-5f);
            Assert.AreEqual(HitStopBase, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, -3f), 1e-5f);
        }

        [Test]
        public void ShakeUsesTheSameRuleWithItsOwnBudget()
        {
            const float shakeBase = 0.1f;
            const float shakeBudget = 0.4f;

            Assert.AreEqual(0.1f, CombatFeel.ScaledDuration(shakeBase, shakeBudget, 1.15f), 1e-5f);
            Assert.AreEqual(0.04f, CombatFeel.ScaledDuration(shakeBase, shakeBudget, 10f), 1e-5f);
        }
    }
}
