using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Anchors the battle stage to the UI BattleArea band rather than to the camera.
    ///
    /// With CropFrame.None the camera shows more world on taller phones, so a fixed
    /// world position would slide out of the battle band on 9:19.5 / 9:21 devices.
    /// This reads the band's actual screen rect every time the resolution changes and
    /// re-seats the background and ground line inside it.
    ///
    /// Runs after the Pixel Perfect Camera (execution order 1000) so the camera's
    /// orthographic size is already final for this frame.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)]
    public sealed class BattleStageLayout : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform battleArea;
        [Tooltip("Parent of the parallax layers. Its origin is the background's bottom edge.")]
        [SerializeField] private Transform backgroundRoot;
        [Tooltip("Empty transform parked on the ground surface. Characters read their Y from this.")]
        [SerializeField] private Transform groundAnchor;

        [Header("Background")]
        [Tooltip("Height of the drawn ground surface above the background's bottom edge, in source pixels.")]
        [SerializeField] private float groundSurfacePixels = 30f;
        [Tooltip("Source pixel height of the background art, used to report vertical coverage.")]
        [SerializeField] private float backgroundPixelHeight = 180f;
        [Tooltip("Tiled sky quad sized to cover the whole camera, behind every parallax layer.")]
        [SerializeField] private SpriteRenderer skyFill;
        [Tooltip("Extra world units of sky beyond the camera edges, to hide rounding at the seams.")]
        [SerializeField] private float skyOverscan = 1f;

        /// <summary>World Y of the surface characters stand on.</summary>
        public float GroundY { get; private set; }

        /// <summary>The battle band in world units.</summary>
        public BattleLayout.Band Band { get; private set; }

        /// <summary>
        /// True when the background art fully covers the band vertically. False means the
        /// gap above it is showing the camera clear colour, which is fine only because that
        /// colour matches the sky.
        /// </summary>
        public bool BackgroundCoversBand { get; private set; }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (targetCamera == null || battleArea == null) return;

            int screenWidth = targetCamera.pixelWidth;
            int screenHeight = targetCamera.pixelHeight;
            if (screenWidth <= 0 || screenHeight <= 0) return;

            // Deliberately recomputed every frame rather than only on resolution change.
            // Caching the first result latched a bad value captured before the canvas and
            // camera had settled, and nothing ever corrected it.
            int pixelRatio = BattleLayout.PixelRatio(
                screenWidth, screenHeight, DisplayConfig.ReferenceWidth, DisplayConfig.ReferenceHeight);
            float cameraWorldHeight = BattleLayout.CameraWorldHeight(
                screenHeight, pixelRatio, DisplayConfig.PixelsPerUnit);
            float cameraWorldWidth = screenWidth / (float)(pixelRatio * DisplayConfig.PixelsPerUnit);

            Band = MeasureBand(screenHeight, cameraWorldHeight);
            GroundY = BattleLayout.GroundY(Band, groundSurfacePixels, DisplayConfig.PixelsPerUnit);

            float backgroundTop = Band.Bottom + backgroundPixelHeight / DisplayConfig.PixelsPerUnit;
            BackgroundCoversBand = backgroundTop >= Band.Top;

            if (backgroundRoot != null) SetY(backgroundRoot, Band.Bottom);
            if (groundAnchor != null) SetY(groundAnchor, GroundY);

            UpdateSkyFill(cameraWorldWidth, cameraWorldHeight);
        }

        /// <summary>
        /// Sizes the sky quad to cover the entire camera view.
        ///
        /// Previously the area above the 180px background was just the camera clear colour
        /// happening to match Sky.png. That works only while the sky stays a flat colour -
        /// the moment anything is drawn into it the trick breaks. Tiling actual sky art
        /// across the full view removes the dependency, and because the source is a uniform
        /// tone the repeat is invisible.
        /// </summary>
        private void UpdateSkyFill(float cameraWorldWidth, float cameraWorldHeight)
        {
            if (skyFill == null) return;

            if (skyFill.drawMode != SpriteDrawMode.Tiled)
            {
                skyFill.drawMode = SpriteDrawMode.Tiled;
                skyFill.tileMode = SpriteTileMode.Continuous;
            }

            var size = new Vector2(cameraWorldWidth + skyOverscan * 2f, cameraWorldHeight + skyOverscan * 2f);
            if ((skyFill.size - size).sqrMagnitude > 0.0001f) skyFill.size = size;

            var cameraPosition = targetCamera.transform.position;
            var desired = new Vector3(cameraPosition.x, cameraPosition.y, skyFill.transform.position.z);
            if ((skyFill.transform.position - desired).sqrMagnitude > 0.0001f)
                skyFill.transform.position = desired;
        }

        /// <summary>
        /// Reads the band straight off the UI rect, so it stays correct even once safe-area
        /// handling starts moving the bands around.
        ///
        /// The camera's world height is derived from the screen size rather than read from
        /// Camera.orthographicSize: the Pixel Perfect Camera only rewrites that value when
        /// it actually renders, so reading it gives a stale answer on the first frame and
        /// in edit mode. <see cref="BattleLayout.PixelRatio"/> is verified against Unity's
        /// own pixelRatio at every target resolution.
        /// </summary>
        private BattleLayout.Band MeasureBand(int screenHeight, float cameraWorldHeight)
        {
            var corners = new Vector3[4];
            battleArea.GetWorldCorners(corners);

            var canvas = battleArea.GetComponentInParent<Canvas>();
            Camera uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            float bottomScreenY = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]).y;
            float topScreenY = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[1]).y;
            float cameraCenterY = targetCamera.transform.position.y;

            return BattleLayout.ComputeBand(bottomScreenY, topScreenY, screenHeight, cameraWorldHeight, cameraCenterY);
        }

        private static void SetY(Transform target, float y)
        {
            var position = target.position;
            if (Mathf.Approximately(position.y, y)) return;
            position.y = y;
            target.position = position;
        }

        /// <summary>Places a character so its pivot (its feet) rests on the ground line.</summary>
        public void PlaceOnGround(Transform character, float worldX)
        {
            if (character == null) return;
            character.position = new Vector3(worldX, GroundY, character.position.z);
        }
    }
}
