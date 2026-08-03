using NUnit.Framework;
using Onikiri.Battle;

namespace Onikiri.Tests
{
    /// <summary>
    /// Hitstop and shake are tuned for early game, where the samurai swings about once a
    /// second. Attack speed is a core upgrade axis, so late game reaches ten swings a
    /// second - at which point a fixed 70ms freeze would be active 70% of the time and the
    /// game would read as broken. These pin the budget rule that prevents that.
    /// </summary>
    public class CombatFeelTests
    {
        // The values the builder writes.
        const float HitStopBase = 0.07f;
        const float HitStopBudget = 0.3f;

        [Test]
        public void BelowCrossover_UsesTheAuthoredDuration()
        {
            // 0.3 / 1.15 = 0.26, well above the authored 0.07, so nothing is clamped.
            Assert.AreEqual(0.07f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 1.15f), 1e-5f);
            Assert.AreEqual(0.07f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 0.5f), 1e-5f);
        }

        [Test]
        public void AboveCrossover_ShortensToTheBudget()
        {
            // The case this rule exists for: 10 attacks/sec.
            Assert.AreEqual(0.03f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 10f), 1e-5f);
            Assert.AreEqual(0.015f, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, 20f), 1e-5f);
        }

        [Test]
        public void TotalFreezeTimePerSecond_StaysBounded()
        {
            // The actual guarantee: however fast you attack, the freeze never occupies more
            // than the budget's share of each second.
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

            // Either side of the crossover the shorter rule wins.
            Assert.AreEqual(HitStopBase, CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, crossover - 0.5f), 1e-5f);
            Assert.Less(CombatFeel.ScaledDuration(HitStopBase, HitStopBudget, crossover + 0.5f), HitStopBase);
        }

        [Test]
        public void DurationNeverIncreases()
        {
            // Faster attacking must never make an effect longer.
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
            // Guards a divide-by-zero at startup before any attack rate is set.
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
