using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Data for one enemy type. Stats and frames live in an asset rather than in code so
    /// balancing and adding new yokai never needs a recompile (handoff spec section 5).
    /// </summary>
    [CreateAssetMenu(menuName = "Onikiri/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Chochin-obake";

        [Header("Animation")]
        [Tooltip("Looping frames used while approaching and while waiting in the queue.")]
        public Sprite[] idleFrames;
        [Tooltip("Short flash played on taking a hit. Falls back to idle when empty.")]
        public Sprite[] hurtFrames;
        [Tooltip("Played once on death; the enemy returns to the pool on the last frame.")]
        public Sprite[] deathFrames;

        public float frameRate = 12f;

        [Header("Combat")]
        public float maxHealth = 12f;

        [Tooltip("World units per second while walking in from the right.")]
        public float moveSpeed = 1.1f;

        [Tooltip("Horizontal spacing kept between queued enemies, in world units.")]
        public float queueSpacing = 0.75f;

        [Header("Presentation")]
        [Tooltip("Height above the ground line, for yokai that float rather than walk.")]
        public float hoverHeight = 0.12f;
    }
}
