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
     * 데미지 값은 **축약하지 않고 전체 자릿수로** 찍는다(NumberFormatter.FormatFull).
     * HUD의 재화 표기와 갈라지는 유일한 자리이고, 이유는 그쪽 주석에 적어뒀다 -
     * 잔고는 크기만 알면 되지만 타격은 크기의 변화가 곧 정보다.
     *
     * 경험치 흡수(ShowExp)만 예외로 축약을 쓴다. 그것이 향하는 경험치 바가 축약
     * 표기라, 날아가는 숫자와 도착지의 숫자가 같은 표기여야 "저것이 여기로 들어갔다"가
     * 읽힌다.
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

        [Tooltip("같은 대상을 이 시간 안에 다시 때리면 새 팝업 대신 기존 숫자에 더한다. " +
                 "초당 여덟 번씩 때리는 구간에서는 팝업이 서로 겹쳐 어떤 숫자도 읽을 수 " +
                 "없어지는데, 합산하면 한 방에 얼마가 들어갔는지가 더 잘 보인다")]
        [SerializeField] private float mergeWindow = 0.3f;

        private ObjectPool<DamageNumber> pool;

        /**
         * @brief 대상별로 지금 떠 있는 팝업.
         *
         * 요괴는 풀링되므로 인스턴스 ID를 열쇠로 쓴다. 죽어서 반환된 뒤 재사용되면
         * 같은 ID가 다시 오는데, 그때는 이미 팝업이 회수된 뒤라 문제가 없다.
         */
        private readonly System.Collections.Generic.Dictionary<object, DamageNumber> active =
            new System.Collections.Generic.Dictionary<object, DamageNumber>();

        public int PoolGrowthCount { get { return pool != null ? pool.GrowthCount : 0; } }

        private void Awake()
        {
            if (container == null) container = (RectTransform)transform;
            if (worldCamera == null) worldCamera = Camera.main;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();

            if (prefab != null) pool = new ObjectPool<DamageNumber>(prefab, container, prewarm);
        }

        /**
         * @brief 월드 좌표에 amount를 표시한다.
         *
         * targetKey가 같고 아직 합산 창 안이면 새 팝업 대신 기존 숫자에 더한다.
         * null을 넘기면 합산하지 않고 항상 새로 띄운다.
         */
        public void Show(BigDouble amount, Vector3 worldPosition, DamageStyle style, object targetKey)
        {
            if (pool == null || canvas == null) return;

            DamageNumber existing;
            if (targetKey != null && active.TryGetValue(targetKey, out existing))
            {
                if (existing != null && existing.CanMerge(mergeWindow))
                {
                    // 강조는 위로만 올라간다(평타 < 치명타 < 처치). 평타 여러 대가
                    // 합쳐진 뒤 마지막 한 대가 처치였다면 그 숫자는 처치로 읽혀야 하고,
                    // 반대로 처치 뒤에 평타가 섞여 등급이 내려가서는 안 된다
                    DamageStyle previous;
                    if (!lastStyle.TryGetValue(targetKey, out previous)) previous = DamageStyle.Normal;

                    var mergedStyle = style > previous ? style : previous;
                    lastStyle[targetKey] = mergedStyle;

                    var total = existing.Accumulated + amount;
                    // 이번에 들어온 타격이 강조 대상이면 크게 튀긴다. mergedStyle이
                    // 아니라 style을 보는 이유는, 이미 치명타로 올라간 숫자에 평타가
                    // 더해질 때마다 매번 크게 튀면 강조가 평범해지기 때문이다.
                    // 튀김은 '지금 무슨 일이 있었나'를 말한다
                    existing.Merge(amount, NumberFormatter.FormatFull(total),
                                   ColorFor(mergedStyle), FontSizeFor(mergedStyle),
                                   style != DamageStyle.Normal);
                    return;
                }
                active.Remove(targetKey);
                lastStyle.Remove(targetKey);
            }

            var screenPoint = worldCamera.WorldToScreenPoint(
                worldPosition + new Vector3(worldOffset.x, worldOffset.y, 0f));

            // 오버레이 캔버스는 여기에 null 카메라를 넘겨야 한다. 카메라를 넘기면 전체가 어긋난다
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 anchored;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container, screenPoint, uiCamera, out anchored))
                return;

            var popup = pool.Get();
            popup.Play(NumberFormatter.FormatFull(amount), anchored,
                       ColorFor(style), FontSizeFor(style), amount, targetKey,
                       style != DamageStyle.Normal, Release);

            if (targetKey != null)
            {
                active[targetKey] = popup;
                lastStyle[targetKey] = style;
            }
        }

        /**
         * @brief 오의 피해. **색을 부르는 쪽이 정하고, 합산하지 않는다.**
         *
         * ## 왜 DamageStyle을 하나 더 만들지 않았는가
         *
         * DamageStyle은 **강조의 단계**이고 셋뿐인 것이 설계다(그쪽 주석 참고).
         * 넷째를 만들면 평타/치명/처치의 대비가 흐려진다. 게다가 오의는 색이
         * 셋이므로(흰/적/금) 단계 하나로는 표현되지 않는다 - 단계는 서열이고
         * 오의 색은 서열이 아니다.
         *
         * ## 왜 합산하지 않는가
         *
         * 합산은 초당 여덟 번 때리는 평타가 서로를 덮는 것을 막는 장치다. 오의는
         * 반대다 - **연참의 세 대는 각자 떠야 "다다닥"이 된다.** 합산하면 숫자
         * 하나가 세 번 커지는 것으로 보이고, 그것은 평타 연타와 구분되지 않는다.
         *
         * 그래서 targetKey를 남기지 않는다. 같은 요괴를 세 번 때려도 세 숫자가
         * 각자 뜨고, 서로 겹치지 않게 DamageNumber의 가로 분산이 흩는다.
         *
         * @param sizeMultiple 아틀라스 크기의 **정수** 배수. 래스터 폰트는 정수배가
         *                     아니면 리샘플되어 흐려진다(PixelFontSizes)
         */
        public void ShowSkill(BigDouble amount, Vector3 worldPosition, Color tint, int sizeMultiple)
        {
            if (pool == null || canvas == null) return;

            var screenPoint = worldCamera.WorldToScreenPoint(
                worldPosition + new Vector3(worldOffset.x, worldOffset.y, 0f));

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 anchored;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container, screenPoint, uiCamera, out anchored))
                return;

            int multiple = sizeMultiple < 1 ? 1 : sizeMultiple;

            var popup = pool.Get();
            // 등장부터 튀긴다. 오의는 드물게 나오므로 등장 자체가 사건이고,
            // 평타 숫자가 계속 흐르는 화면에서 그 튀김이 눈에 걸리는 유일한 축이다
            popup.Play(NumberFormatter.FormatFull(amount), anchored,
                       tint, PixelFontSizes.ThaleahDamage * multiple, amount,
                       null, true, Release);
        }

        [Header("경험치 흡수")]
        [Tooltip("경험치가 빨려 들어갈 목표. 보통 상단 바의 경험치 바다. " +
                 "비어 있으면 흡수 연출을 생략한다 - 목표 없이 날리면 화면 " +
                 "왼쪽 아래 구석으로 사라진다")]
        [SerializeField] private RectTransform expTarget;

        [Tooltip("경험치 색. 경험치 바의 채움색과 같아야 어디로 가는지가 읽힌다")]
        [SerializeField] private Color expColor = new Color32(0x7C, 0xC5, 0x9A, 0xFF);

        /**
         * @brief 처치 지점에서 경험치 바로 날아가는 "+N".
         *
         * 데미지 팝업과 같은 풀을 쓴다. 초당 처치 수는 스폰 간격 하한(0.4초)이
         * 막고 있어서, 이 연출이 풀을 놓고 데미지 팝업과 다투는 구간은 없다.
         */
        public void ShowExp(BigDouble amount, Vector3 worldPosition)
        {
            if (pool == null || canvas == null || expTarget == null) return;
            if (amount <= BigDouble.Zero) return;

            var screenPoint = worldCamera.WorldToScreenPoint(
                worldPosition + new Vector3(worldOffset.x, worldOffset.y, 0f));

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 from;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container, screenPoint, uiCamera, out from))
                return;

            // 목표는 다른 계층에 있다(상단 바). 월드를 거쳐 컨테이너 좌표로 옮긴다 -
            // 두 rect의 앵커가 달라서 anchoredPosition을 그대로 쓸 수 없다
            var targetScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, expTarget.position);

            Vector2 to;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container, targetScreen, uiCamera, out to))
                return;

            var popup = pool.Get();
            popup.PlayAbsorb("+" + NumberFormatter.Format(amount), from, to,
                             expColor, PixelFontSizes.ThaleahDamage, Release);
        }

        /** 대상별로 지금까지 올라간 강조 단계 */
        private readonly System.Collections.Generic.Dictionary<object, DamageStyle> lastStyle =
            new System.Collections.Generic.Dictionary<object, DamageStyle>();

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
            // 회수되는 팝업이 아직 대상에 걸려 있으면 지운다. 남겨두면 다음 타격이
            // 이미 풀에 돌아간 인스턴스에 합산을 시도한다
            if (popup.TargetKey != null)
            {
                DamageNumber registered;
                if (active.TryGetValue(popup.TargetKey, out registered) && registered == popup)
                {
                    active.Remove(popup.TargetKey);
                    lastStyle.Remove(popup.TargetKey);
                }
            }

            pool.Release(popup);
        }
    }
}
