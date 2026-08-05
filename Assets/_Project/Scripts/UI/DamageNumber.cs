using System;
using Onikiri.Core;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 풀링된 데미지 팝업 하나. 타격 지점에 나타나 위로 호를 그리며 사라진다.
     *
     * 월드 공간이 아니라 오버레이 캔버스에 둔다. 그래야 글자가 폰트 설계 크기의
     * 정확한 정수배로 유지된다. 월드 공간 라벨은 카메라와 함께 스케일되면서 픽셀 격자가
     * 화면 픽셀과 어긋나고, 래스터 아틀라스를 쓴 의미가 사라진다.
     *
     * 스케일 타임으로 돌기 때문에 히트스톱이 팝업도 함께 얼린다.
     */
    public sealed class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("라벨 뒤에 한 칸 밀려 그려지는 어두운 사본. 비트맵 폰트는 TMP의 SDF " +
                 "아웃라인을 쓸 수 없어서, 이것이 없으면 흰 참격 위에서 숫자가 사라진다")]
        [SerializeField] private TMP_Text shadow;
        [SerializeField] private RectTransform rect;

        [Header("모션")]
        [SerializeField] private float lifetime = 0.75f;
        [Tooltip("초기 상승 속도 (캔버스 단위/초)")]
        [SerializeField] private float riseSpeed = 320f;
        [Tooltip("아래로 당기는 힘. 팝업이 미끄러지지 않고 호를 그리게 한다")]
        [SerializeField] private float gravity = 520f;
        [Tooltip("가로 방향 랜덤 분산. 서로 다른 대상의 팝업이 정확히 겹치지 않게 한다. " +
                 "같은 대상의 연속 타격은 분산이 아니라 합산으로 처리한다")]
        [SerializeField] private float horizontalSpread = 110f;
        [Tooltip("사라지기 전 완전 불투명 상태로 유지하는 수명 비율")]
        [Range(0f, 1f)]
        [SerializeField] private float holdFraction = 0.35f;

        [Header("합산")]
        [Tooltip("데미지가 합산될 때 순간적으로 커지는 배율. 숫자만 바뀌면 여덟 번 " +
                 "때리는 동안 아무 일도 일어나지 않은 것처럼 보인다")]
        [SerializeField] private float mergePunchScale = 1.15f;

        [Tooltip("튄 스케일이 1로 돌아오는 데 걸리는 시간")]
        [SerializeField] private float mergePunchDecay = 0.12f;

        private Action<DamageNumber> finished;
        private Vector2 velocity;
        private Vector2 position;
        private float elapsed;
        private Color baseColor;

        /** 합산 직후 남아 있는 스케일 여분. 0이면 원래 크기 */
        private float punchRemaining;

        /**
         * @brief 이 팝업이 지금까지 합산한 데미지.
         *
         * 같은 요괴를 연속으로 때리면 새 팝업을 띄우는 대신 여기에 더한다. 초당 여덟
         * 번씩 때리는 구간에서는 팝업이 서로 겹쳐 어떤 숫자도 읽을 수 없게 되는데,
         * 합산하면 오히려 한 방에 얼마가 들어갔는지가 더 잘 보인다.
         */
        public BigDouble Accumulated { get; private set; }

        /**
         * @brief 이 팝업이 붙어 있는 대상. 스포너가 합산 대상을 찾는 열쇠.
         *
         * 참조 자체를 열쇠로 쓴다. 인스턴스 ID는 Unity 6에서 폐기 예정이고, 여기서는
         * 동일성만 필요하지 대상의 내용을 들여다볼 일이 없다.
         */
        public object TargetKey { get; private set; }

        /** 합산을 더 받을 수 있는지. 너무 오래 붙들면 숫자가 화면에 머물러 버린다 */
        public bool CanMerge(float mergeWindow)
        {
            return gameObject.activeSelf && elapsed <= mergeWindow;
        }

        private void Awake()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (label == null) label = GetComponent<TMP_Text>();
        }

        /**
         * @brief 팝업 하나를 띄운다.
         *
         * fontSize는 아틀라스를 구운 크기의 정수배여야 한다. 그 사이 값을 넣으면
         * 비트맵이 리샘플되어 래스터 폰트를 쓴 이유가 사라진다.
         */
        public void Play(string text, Vector2 anchoredPosition, Color color, float fontSize,
                         BigDouble amount, object targetKey, Action<DamageNumber> onFinished)
        {
            finished = onFinished;
            Accumulated = amount;
            TargetKey = targetKey;

            SetText(text, color, fontSize);

            position = anchoredPosition;
            rect.anchoredPosition = position;

            velocity = new Vector2(UnityEngine.Random.Range(-horizontalSpread, horizontalSpread), riseSpeed);
            elapsed = 0f;

            // 풀에서 나온 인스턴스는 이전 생애의 스케일을 그대로 들고 온다. 합산 도중
            // 회수되면 1.15배인 채로 반환되고, 다음 타격의 첫 숫자가 이유 없이 크게 뜬다
            punchRemaining = 0f;
            rect.localScale = Vector3.one;

            gameObject.SetActive(true);
        }

        /**
         * @brief 이미 떠 있는 팝업에 데미지를 더한다.
         *
         * 수명은 되돌리지 않고 유지 구간의 시작으로만 당긴다. 완전히 초기화하면
         * 연타가 이어지는 동안 숫자가 화면에 영원히 붙어 있게 된다.
         */
        public void Merge(BigDouble amount, string text, Color color, float fontSize)
        {
            Accumulated += amount;
            SetText(text, color, fontSize);

            float holdEnd = lifetime * holdFraction;
            if (elapsed > holdEnd) elapsed = holdEnd;

            // 합산될 때마다 살짝 위로 튄다. 숫자가 커지는 순간을 눈이 따라가게 한다
            velocity.y = Mathf.Max(velocity.y, riseSpeed * 0.45f);

            // 그리고 크기가 한 번 튄다. 위치 변화만으로는 부족하다 - 초당 여덟 번
            // 구간에서 숫자는 이미 계속 움직이고 있어서, 그 안의 작은 상승은 다른
            // 팝업의 움직임과 구분되지 않는다. 크기는 그 화면에서 유일하게 변하지
            // 않던 축이라 눈에 걸린다.
            //
            // 폰트 크기가 아니라 트랜스폼 스케일을 쓴다. 비트맵 폰트라 fontSize를
            // 정수배 사이의 값으로 흔들면 글리프가 리샘플되어 흐려지고, 강조하려던
            // 숫자가 오히려 읽기 어려워진다. 스케일은 메시를 그대로 두고 늘린다
            punchRemaining = 1f;
        }

        private void SetText(string text, Color color, float fontSize)
        {
            label.text = text;
            label.fontSize = fontSize;
            baseColor = color;
            label.color = color;

            if (shadow != null)
            {
                shadow.text = text;
                shadow.fontSize = fontSize;
                shadow.color = new Color(0f, 0f, 0f, 0.75f);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (elapsed >= lifetime)
            {
                var callback = finished;
                finished = null;
                if (callback != null) callback(this);
                return;
            }

            velocity.y -= gravity * Time.deltaTime;
            position += velocity * Time.deltaTime;
            rect.anchoredPosition = position;

            if (punchRemaining > 0f)
            {
                punchRemaining = Mathf.Max(0f, punchRemaining - Time.deltaTime / Mathf.Max(0.0001f, mergePunchDecay));
                float scale = Mathf.Lerp(1f, mergePunchScale, punchRemaining);
                rect.localScale = new Vector3(scale, scale, 1f);
            }

            float t = elapsed / lifetime;
            float fade = t <= holdFraction ? 1f : 1f - Mathf.InverseLerp(holdFraction, 1f, t);

            var color = baseColor;
            color.a = fade;
            label.color = color;

            if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0.75f * fade);
        }
    }
}
