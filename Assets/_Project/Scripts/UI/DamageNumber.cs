using System;
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
        [Tooltip("가로 방향 랜덤 분산. 연속 타격이 정확히 겹치지 않게 한다")]
        [SerializeField] private float horizontalSpread = 60f;
        [Tooltip("사라지기 전 완전 불투명 상태로 유지하는 수명 비율")]
        [Range(0f, 1f)]
        [SerializeField] private float holdFraction = 0.35f;

        private Action<DamageNumber> finished;
        private Vector2 velocity;
        private Vector2 position;
        private float elapsed;
        private Color baseColor;

        private void Awake()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (label == null) label = GetComponent<TMP_Text>();
        }

        public void Play(string text, Vector2 anchoredPosition, Color color, Action<DamageNumber> onFinished)
        {
            finished = onFinished;

            label.text = text;
            baseColor = color;
            label.color = color;

            if (shadow != null)
            {
                shadow.text = text;
                shadow.color = new Color(0f, 0f, 0f, 0.75f);
            }

            position = anchoredPosition;
            rect.anchoredPosition = position;

            velocity = new Vector2(UnityEngine.Random.Range(-horizontalSpread, horizontalSpread), riseSpeed);
            elapsed = 0f;

            gameObject.SetActive(true);
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

            float t = elapsed / lifetime;
            float fade = t <= holdFraction ? 1f : 1f - Mathf.InverseLerp(holdFraction, 1f, t);

            var color = baseColor;
            color.a = fade;
            label.color = color;

            if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0.75f * fade);
        }
    }
}
