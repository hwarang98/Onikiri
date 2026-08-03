namespace Onikiri.Core
{
    /// <summary>
    /// Every sorting order in the battle scene, in one place.
    ///
    /// Draw order back to front:
    ///   sky fill -> parallax layers -> enemies -> player -> ground cover -> VFX -> UI
    ///
    /// Two deliberate choices worth knowing:
    ///  - Enemies draw BEHIND the player. The samurai is the thing the eye should track,
    ///    and enemies queue up to his right rather than overlapping him, so nothing is
    ///    hidden in practice.
    ///  - Ground cover (the grass layer) draws IN FRONT of both. Grass crossing the
    ///    fighters' ankles is what makes them read as standing in the scene rather than
    ///    pasted on top of it.
    /// </summary>
    public static class SortingOrders
    {
        /// <summary>Sky quad stretched over the whole camera, behind every parallax layer.</summary>
        public const int SkyFill = -300;

        /// <summary>First parallax layer; each subsequent layer adds one.</summary>
        public const int BackgroundBase = -200;

        /// <summary>
        /// Enemies occupy a small band so several on screen at once never z-fight.
        /// Each enemy takes BaseEnemy + (slot % EnemySlots).
        /// </summary>
        public const int EnemyBase = 0;
        public const int EnemySlots = 40;

        public const int Player = 50;

        /// <summary>Grass drawn over the fighters' feet.</summary>
        public const int GroundCover = 70;

        /// <summary>Slash effects always read on top of everyone.</summary>
        public const int Vfx = 100;

        /// <summary>Reserved for the damage numbers added in a later step.</summary>
        public const int DamageNumber = 200;
    }
}
