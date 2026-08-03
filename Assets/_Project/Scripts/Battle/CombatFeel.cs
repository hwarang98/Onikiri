using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Timing rules for impact feedback.
    ///
    /// Hitstop and screen shake are great at one attack per second and unbearable at ten.
    /// An idle game ends up at ten: attack speed is a core upgrade axis, so late game the
    /// samurai swings constantly. A fixed 70ms freeze would then be active most of the
    /// time and the game would read as permanently stuttering.
    ///
    /// So each effect gets a budget of screen time per second rather than a fixed length.
    /// Below the crossover the authored duration is used unchanged; above it the effect
    /// shortens so the total stays bounded no matter how fast the player attacks.
    /// </summary>
    public static class CombatFeel
    {
        /// <summary>
        /// Effect length for a given attack rate:
        /// <c>min(baseSeconds, budgetPerSecond / attacksPerSecond)</c>.
        /// </summary>
        public static float ScaledDuration(float baseSeconds, float budgetPerSecond, float attacksPerSecond)
        {
            if (attacksPerSecond <= 0f) return baseSeconds;
            return Mathf.Min(baseSeconds, budgetPerSecond / attacksPerSecond);
        }

        /// <summary>
        /// Attack rate at which an effect starts being shortened. Handy for tuning and for
        /// documenting where the behaviour changes.
        /// </summary>
        public static float CrossoverAttackSpeed(float baseSeconds, float budgetPerSecond)
        {
            if (baseSeconds <= 0f) return float.PositiveInfinity;
            return budgetPerSecond / baseSeconds;
        }
    }
}
