using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Freezes game time for a few dozen milliseconds on impact.
    ///
    /// This is the cheapest and most effective part of making a hit feel like it connected:
    /// the swing, the enemy and the slash effect all stop dead for a moment, which reads as
    /// weight. Because everything gameplay-side runs on scaled time, one timeScale change
    /// covers all of it.
    ///
    /// Recovery is driven by unscaled time, otherwise a zero timeScale would never tick the
    /// timer back down and the game would hang.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Tooltip("Time scale restored once a freeze ends.")]
        [SerializeField] private float normalTimeScale = 1f;

        private float remainingUnscaled;

        public bool IsFrozen { get { return remainingUnscaled > 0f; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // Never leave the game frozen because this object went away mid-freeze.
                if (Time.timeScale == 0f) Time.timeScale = normalTimeScale;
            }
        }

        private void OnDisable()
        {
            if (remainingUnscaled > 0f)
            {
                remainingUnscaled = 0f;
                Time.timeScale = normalTimeScale;
            }
        }

        /// <summary>
        /// Freezes for <paramref name="seconds"/> of real time. Overlapping calls extend
        /// rather than stack, so a flurry of hits cannot compound into a long stall.
        /// </summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f) return;

            remainingUnscaled = Mathf.Max(remainingUnscaled, seconds);
            Time.timeScale = 0f;
        }

        public static void Request(float seconds)
        {
            if (Instance != null) Instance.Freeze(seconds);
        }

        private void Update()
        {
            if (remainingUnscaled <= 0f) return;

            remainingUnscaled -= Time.unscaledDeltaTime;
            if (remainingUnscaled <= 0f)
            {
                remainingUnscaled = 0f;
                Time.timeScale = normalTimeScale;
            }
        }
    }
}
