using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Nudges the camera a couple of pixels on impact.
    ///
    /// Runs on unscaled time so it keeps moving while a hitstop has timeScale at zero -
    /// a freeze that also shakes reads as a much heavier hit than either alone.
    ///
    /// Offsets are snapped to whole source pixels. A sub-pixel camera offset would fight
    /// the Pixel Perfect Camera and make the whole scene shimmer instead of shake.
    ///
    /// Runs after <see cref="BattleStageLayout"/> (order 1000) so the layout always reads
    /// an unshaken camera; see <see cref="BasePosition"/>.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public sealed class ScreenShake : MonoBehaviour
    {
        [Tooltip("Default shake strength in source pixels.")]
        [SerializeField] private float defaultPixels = 3f;

        [Tooltip("Default shake length in seconds, before attack-speed scaling.")]
        [SerializeField] private float defaultSeconds = 0.1f;

        private Vector3 basePosition;
        private bool shaking;
        private float remaining;
        private float duration;
        private float pixels;

        /// <summary>
        /// The camera's position with the shake removed.
        ///
        /// Anything that positions world content relative to the camera must use this.
        /// BattleStageLayout derives the battle band from the camera, and if it followed
        /// the shaken position the background would move with the camera and cancel the
        /// shake out entirely.
        /// </summary>
        public Vector3 BasePosition { get { return shaking ? basePosition : transform.position; } }

        public bool IsShaking { get { return shaking; } }

        private void OnEnable()
        {
            basePosition = transform.position;
            shaking = false;
            remaining = 0f;
        }

        private void OnDisable()
        {
            if (shaking)
            {
                transform.position = basePosition;
                shaking = false;
            }
        }

        public void Shake()
        {
            Shake(defaultSeconds, defaultPixels);
        }

        /// <summary>
        /// Starts a shake. Overlapping calls take the stronger of the two rather than
        /// stacking, so a burst of hits cannot escalate into a permanent tremor.
        /// </summary>
        public void Shake(float seconds, float strengthPixels)
        {
            if (seconds <= 0f || strengthPixels <= 0f) return;

            if (!shaking)
            {
                basePosition = transform.position;
                shaking = true;
            }

            duration = Mathf.Max(duration * (remaining > 0f ? 1f : 0f), seconds);
            remaining = Mathf.Max(remaining, seconds);
            pixels = Mathf.Max(pixels * (remaining > 0f ? 1f : 0f), strengthPixels);
        }

        public static void Request(ScreenShake instance, float seconds, float strengthPixels)
        {
            if (instance != null) instance.Shake(seconds, strengthPixels);
        }

        private void LateUpdate()
        {
            if (remaining <= 0f)
            {
                if (shaking)
                {
                    transform.position = basePosition;
                    shaking = false;
                    duration = 0f;
                    pixels = 0f;
                }
                else
                {
                    // Track deliberate camera moves while idle so the shake never snaps
                    // the camera back to a stale position.
                    basePosition = transform.position;
                }
                return;
            }

            remaining -= Time.unscaledDeltaTime;

            float falloff = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            float amplitude = pixels * falloff;

            float offsetX = Mathf.Round(Random.Range(-amplitude, amplitude)) / DisplayConfig.PixelsPerUnit;
            float offsetY = Mathf.Round(Random.Range(-amplitude, amplitude)) / DisplayConfig.PixelsPerUnit;

            transform.position = basePosition + new Vector3(offsetX, offsetY, 0f);
        }
    }
}
