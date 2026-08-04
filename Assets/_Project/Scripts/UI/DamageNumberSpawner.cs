using Onikiri.Core;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 풀링된 데미지 팝업.
     *
     * 게임에서 가장 빈도가 높은 UI다. 타격마다 하나씩 생기는데 공격속도가 성장 축이므로,
     * 전투의 다른 요소와 마찬가지로 풀링하며 타격마다 Instantiate하지 않는다.
     *
     * 값은 NumberFormatter를 통해 찍는다. HUD가 쓰는 "1.5K / 3.2M / 1.2aa" 표기가
     * 별도 포맷 경로 없이 그대로 적용된다.
     */
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] private DamageNumber prefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas canvas;

        [Tooltip("풀이 늘어나기 전까지 동시에 살아 있을 수 있는 팝업 수")]
        [SerializeField] private int prewarm = 12;

        [Header("강조")]
        [Tooltip("평타. 화면에 가장 많이 나오므로 의도적으로 조용한 색이다")]
        [SerializeField] private Color normalColor = new Color32(0xFF, 0xF4, 0xD6, 0xFF);

        [Tooltip("치명타. 금색")]
        [SerializeField] private Color critColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        [Tooltip("처치. 붉은색")]
        [SerializeField] private Color killColor = new Color32(0xFF, 0x8A, 0x7A, 0xFF);

        [Tooltip("타격 지점으로부터의 월드 오프셋. 참격 이펙트를 피해 위로 띄운다. " +
                 "참격은 큰 흰색 덩어리라 그 안에 들어가면 숫자가 묻힌다")]
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

        /** 월드 좌표에 amount를 표시한다 */
        public void Show(BigDouble amount, Vector3 worldPosition, DamageStyle style)
        {
            if (pool == null || canvas == null) return;

            var screenPoint = worldCamera.WorldToScreenPoint(
                worldPosition + new Vector3(worldOffset.x, worldOffset.y, 0f));

            // 오버레이 캔버스는 여기에 null 카메라를 넘겨야 한다. 카메라를 넘기면 전체가 어긋난다
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 anchored;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container, screenPoint, uiCamera, out anchored))
                return;

            var popup = pool.Get();
            popup.Play(NumberFormatter.Format(amount), anchored,
                       ColorFor(style), FontSizeFor(style), Release);
        }

        private Color ColorFor(DamageStyle style)
        {
            if (style == DamageStyle.Kill) return killColor;
            if (style == DamageStyle.Critical) return critColor;
            return normalColor;
        }

        /**
         * @brief 강조 단계별 글자 크기.
         *
         * 평타는 아틀라스와 1:1, 강조는 정확히 2배다. 그 사이 값은 허용하지 않는다.
         * 래스터 폰트를 정수배가 아닌 크기로 그리면 픽셀 격자가 화면 픽셀 사이에
         * 놓여, 강조하려고 키운 숫자가 오히려 흐려진다.
         */
        private static float FontSizeFor(DamageStyle style)
        {
            return style == DamageStyle.Normal
                ? PixelFontSizes.ThaleahDamage
                : PixelFontSizes.ThaleahDamage * 2f;
        }

        private void Release(DamageNumber popup)
        {
            pool.Release(popup);
        }
    }
}
