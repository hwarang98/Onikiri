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

        /**
         * @brief 치명타가 섞였을 때의 튀는 배율.
         *
         * 치명타는 이미 색(금색)과 크기(2배)로 구분된다. 그런데 **합산 중에는 그
         * 구분이 잘 전달되지 않는다.** 평타 세 대가 합쳐진 숫자가 이미 떠 있는
         * 상태에서 네 번째가 치명타면, 색과 크기가 그 자리에서 바뀔 뿐이라
         * "원래 저랬나?" 로 읽힌다. 새로 뜨는 숫자였다면 등장 자체가 신호가 되는데
         * 합산에는 등장이 없다.
         *
         * 그래서 치명타가 섞이는 순간에만 더 크게 튀긴다. 움직임은 색이나 크기와
         * 달리 '변화' 그 자체라서, 이미 떠 있는 숫자에서도 새 사건으로 읽힌다.
         */
        [Tooltip("치명타가 섞인 합산에서의 튀는 배율. 평타보다 크게 잡는다")]
        [SerializeField] private float critPunchScale = 1.4f;

        [Tooltip("튄 스케일이 1로 돌아오는 데 걸리는 시간")]
        [SerializeField] private float mergePunchDecay = 0.12f;

        private Action<DamageNumber> finished;
        private Vector2 velocity;
        private Vector2 position;
        private float elapsed;
        private Color baseColor;

        /** 합산 직후 남아 있는 스케일 여분. 0이면 원래 크기 */
        private float punchRemaining;

        /** 지금 진행 중인 튀김의 목표 배율. 평타와 치명타가 다르다 */
        private float punchScale;

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
                         BigDouble amount, object targetKey, bool emphasised,
                         Action<DamageNumber> onFinished)
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

            // 이전 생애가 흡수였으면 그 모드가 그대로 남는다. 풀에서 나온 인스턴스가
            // 스케일을 들고 오는 것과 같은 종류의 문제이고, 여기서는 데미지 숫자가
            // 경험치 바로 날아가는 모습으로 나타난다
            absorbing = false;

            // 첫 숫자가 치명타면 등장부터 튀긴다. 합산이 아니라 새로 뜨는 경우라
            // 등장 자체가 이미 신호이긴 하지만, 평타 사이에서 한 번 더 도드라져야
            // 초당 네 번 구간에서 눈에 걸린다
            if (emphasised) StartPunch(true);

            gameObject.SetActive(true);
        }

        /**
         * @brief 이미 떠 있는 팝업에 데미지를 더한다.
         *
         * 수명은 되돌리지 않고 유지 구간의 시작으로만 당긴다. 완전히 초기화하면
         * 연타가 이어지는 동안 숫자가 화면에 영원히 붙어 있게 된다.
         */
        public void Merge(BigDouble amount, string text, Color color, float fontSize, bool emphasised)
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
            StartPunch(emphasised);
        }

        /**
         * @brief 튀김을 시작한다. 진행 중이면 더 큰 쪽이 이긴다.
         *
         * 치명타 직후에 평타가 합산되면서 튀김을 작게 덮어쓰면, 강조가 나타나자마자
         * 사라진다. 강조는 한 방향으로만 올라간다 - 색·크기와 같은 규칙이다
         * (DamageNumberSpawner의 mergedStyle 참고).
         */
        private void StartPunch(bool emphasised)
        {
            float target = emphasised ? critPunchScale : mergePunchScale;
            punchScale = punchRemaining > 0f ? Mathf.Max(punchScale, target) : target;
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

        // ---------------------------------------------------------------- 경험치 흡수

        [Header("경험치 흡수")]
        [Tooltip("처치 지점에서 경험치 바까지 날아가는 데 걸리는 시간")]
        [SerializeField] private float absorbSeconds = 0.55f;

        private bool absorbing;
        private Vector2 absorbFrom;
        private Vector2 absorbTo;

        /**
         * @brief 처치 지점에서 경험치 바로 날아가는 팝업.
         *
         * 데미지 팝업과 같은 풀에서 나온다. 별도 풀을 두지 않는 이유는 두 연출이
         * **서로 배타적으로 많아지기** 때문이다 - 타격이 잦은 구간은 처치가 잦은
         * 구간이기도 해서, 풀을 나누면 양쪽 모두 최대치를 잡아둬야 한다.
         *
         * 호를 그리며 사라지는 대신 목표 지점으로 빨려 들어간다. 위로 떠서 사라지는
         * 것은 "여기서 무슨 일이 있었다"이고, 어딘가로 날아가는 것은 "그것이 저기로
         * 갔다"이다. 경험치는 후자다 - 상단 바의 숫자가 왜 늘었는지가 연결된다.
         */
        public void PlayAbsorb(string text, Vector2 from, Vector2 to, Color color, float fontSize,
                               Action<DamageNumber> onFinished)
        {
            finished = onFinished;
            Accumulated = BigDouble.Zero;

            // 합산 대상이 되어서는 안 된다. 열쇠를 남기면 다음 타격이 날아가는
            // 중인 이 팝업에 데미지를 더하려 든다
            TargetKey = null;

            SetText(text, color, fontSize);

            absorbing = true;
            absorbFrom = from;
            absorbTo = to;
            position = from;
            rect.anchoredPosition = position;

            elapsed = 0f;
            punchRemaining = 0f;
            rect.localScale = Vector3.one;

            gameObject.SetActive(true);
        }

        private void UpdateAbsorb()
        {
            float duration = Mathf.Max(0.01f, absorbSeconds);
            float t = Mathf.Clamp01(elapsed / duration);

            // 처음엔 느리고 끝에서 빨라진다. 등속으로 움직이면 UI 요소가 이동하는
            // 것처럼 보이고, 가속이 붙어야 '빨려 들어간다'로 읽힌다
            float eased = t * t;

            position = Vector2.Lerp(absorbFrom, absorbTo, eased);
            rect.anchoredPosition = position;

            // 도착하면서 작아진다. 목표 지점에서 그대로 사라지면 마지막 프레임이
            // 툭 끊기는데, 줄어들면 흡수가 끝난 것으로 보인다
            float scale = Mathf.Lerp(1f, 0.4f, eased);
            rect.localScale = new Vector3(scale, scale, 1f);

            var color = baseColor;
            color.a = 1f - eased * eased;
            label.color = color;
            if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0.75f * color.a);

            if (t < 1f) return;

            absorbing = false;
            var callback = finished;
            finished = null;
            if (callback != null) callback(this);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (absorbing)
            {
                UpdateAbsorb();
                return;
            }

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
                float scale = Mathf.Lerp(1f, punchScale, punchRemaining);
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
