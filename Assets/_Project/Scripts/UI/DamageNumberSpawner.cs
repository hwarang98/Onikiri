using Onikiri.Core;
using UnityEngine;

namespace Onikiri.UI
{
    /// <summary>
    /// Pooled damage popups.
    ///
    /// These are the highest-frequency UI in the game - one per hit, and attack speed is an
    /// upgrade axis - so they are pooled like everything else in combat and never
    /// instantiated per hit.
    ///
    /// Values print through <see cref="NumberFormatter"/>, so the same "1.5K / 3.2M / 1.2aa"
    /// notation used by the HUD applies here without a second formatting path.
    /// </summary>
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] private DamageNumber prefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas canvas;

        [Tooltip("Popups alive at once before the pool has to grow.")]
        [SerializeField] private int prewarm = 12;

        [SerializeField] private Color normalColor = new Color32(0xFF, 0xF4, 0xD6, 0xFF);
        [SerializeField] private Color killColor = new Color32(0xFF, 0x8A, 0x7A, 0xFF);

        [Tooltip("World offset from the hit point. Lifted clear of the slash effect, which " +
                 "is a big white shape the number would otherwise be lost inside.")]
        [SerializeField] private Vector2 worldOffset = new Vector2(0f, 0.85f);

        private ObjectPool<DamageNumber> pool;

        public int PoolGrowthCount { get { return pool != null ? pool.GrowthCount : 0; } }

        private void Awake()
        {
            if (container == null) container = (RectTransform)transform;
            if (worldCamera == null) worldCamera = Camera.main;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();

            if (prefab != null) pool = new ObjectPool<DamageNumber>(prefab, container, prewarm);
        }

        /// <summary>Shows <paramref name="amount"/> at a world position.</summary>
        public void Show(BigDouble amount, Vector3 worldPosition, bool wasKill)
        {
            if (pool == null || canvas == null) return;

            var screenPoint = worldCamera.WorldToScreenPoint(
                worldPosition + new Vector3(worldOffset.x, worldOffset.y, 0f));

            // Overlay canvases take a null camera here; passing one offsets everything.
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 anchored;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container, screenPoint, uiCamera, out anchored))
                return;

            var popup = pool.Get();
            popup.Play(NumberFormatter.Format(amount), anchored,
                       wasKill ? killColor : normalColor, Release);
        }

        private void Release(DamageNumber popup)
        {
            pool.Release(popup);
        }
    }
}
