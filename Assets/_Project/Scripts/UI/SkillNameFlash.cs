using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 오의 이름을 짧게 띄운다. "연참" / "일섬" / "귀참".
     *
     * ## 왜 이것이 필요했는가
     *
     * 26단계 플레이 소감이 "자동 시전을 켰는데 나가는지 모르겠다"였다. 실측으로
     * 오의는 정상 발동 중이었고 DPS의 38%를 내고 있었으므로, 문제는 발동이 아니라
     * **식별**이었다.
     *
     * 그때 화면에 있던 신호는 전부 평타와 같은 종류였다 - 같은 스윙, 같은 타격음,
     * 0.2초짜리 아크(사무라이 스프라이트가 이미 0.26초마다 흰 참격을 그린다),
     * 치명타와 같은 단계의 데미지 숫자(치명타율 60% 구간에서는 큰 숫자가 이미
     * 대부분이다). 같은 종류의 신호를 아무리 키워도 "무엇이 나갔는지"는 안 읽힌다.
     *
     * **이름은 다른 종류의 신호다.** 평타는 이름이 없으므로 글자가 뜨는 것 자체가
     * 오의라는 뜻이고, 어느 오의인지까지 한 번에 말한다.
     *
     * ## 인스턴스가 하나뿐인 이유
     *
     * 오의는 2.5~11초에 한 번이라 이따금 겹치고, 겹치면 **나중 것이 이긴다.**
     * 둘을 나란히 띄우면 어느 쪽이 방금 나간 것인지 알 수 없어서, 이름을 띄운
     * 목적이 사라진다. 풀링할 이유도 없다 - 하나면 Instantiate가 없다.
     *
     * 데미지 팝업의 풀을 쓰지 않는다. 그쪽은 Thaleah(라틴 전용) 서체라 한글
     * 글리프가 아틀라스에 없다.
     */
    public sealed class SkillNameFlash : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        /**
         * @brief 라벨 뒤에 한 칸 밀려 그려지는 어두운 사본.
         *
         * 데미지 팝업이 같은 이유로 갖고 있다 - **비트맵 폰트는 TMP의 SDF
         * 아웃라인을 쓸 수 없어서**, 이것이 없으면 밝은 배경 위에서 글자가
         * 사라진다. 오의 이름은 그 위험이 가장 큰 자리다: 연참의 색이 강철빛
         * 흰색이고 지역 1의 배경은 하늘과 벚꽃이다.
         */
        [SerializeField] private TMP_Text shadow;

        [SerializeField] private RectTransform rect;

        [Tooltip("완전 불투명으로 머무는 시간")]
        [SerializeField] private float holdSeconds = 0.22f;

        [Tooltip("사라지는 데 걸리는 시간")]
        [SerializeField] private float fadeSeconds = 0.30f;

        [Tooltip("떠오르는 거리 (캔버스 단위). 위로 밀려 올라가며 사라진다")]
        [SerializeField] private float riseDistance = 70f;

        [Tooltip("등장할 때 이 배율에서 1로 줄어든다. 튀어나오는 느낌을 만든다")]
        [SerializeField] private float punchScale = 1.35f;

        [Tooltip("튄 스케일이 1로 돌아오는 시간")]
        [SerializeField] private float punchSeconds = 0.10f;

        private float elapsed;
        private bool playing;
        private Vector2 restPosition;

        /**
         * @brief 기준 위치만 기억한다. **자기 자신을 끄지 않는다.**
         *
         * 처음에 여기서 Hide()를 불렀고, 그것이 **첫 시전을 통째로 삼켰다.**
         * 이 오브젝트는 꺼진 채로 씬에 저장되므로 Awake가 씬 로드가 아니라
         * 처음 켜지는 순간에 도는데, 켜는 쪽이 Play이기 때문이다. 자세한 순서는
         * ScreenFlash.Awake 주석 참고 - 같은 함정을 두 컴포넌트에서 같이 밟았다.
         *
         * 끄는 것은 Update가 끝날 때와 빌더뿐이다.
         */
        private void Awake()
        {
            if (rect == null) rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
        }

        /**
         * @brief 이름을 띄운다. 색은 그 오의의 참격 색과 같다.
         *
         * 색을 함께 넘기는 이유는 이름과 아크와 숫자가 **같은 색**이어야 세 신호가
         * 한 사건으로 읽히기 때문이다. 셋이 각자 색을 갖고 있으면 화면이 오의
         * 하나에 세 가지 색을 쓴다.
         */
        public void Play(string skillName, Color tint)
        {
            if (label == null) return;

            // 활성화가 먼저다. SetActive(true)가 Awake를 그 자리에서 돌리므로,
            // 상태를 먼저 쓰면 Awake가 그것을 덮어쓴다(위 주석)
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            label.text = skillName;
            label.color = tint;

            if (shadow != null)
            {
                shadow.text = skillName;
                shadow.color = new Color(0f, 0f, 0f, 0.75f);
            }

            elapsed = 0f;
            playing = true;

            rect.anchoredPosition = restPosition;
            rect.localScale = Vector3.one * punchScale;
        }

        private void Hide()
        {
            playing = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /**
         * @brief 스케일 타임으로 돈다. 히트스톱이 이름도 함께 얼린다.
         *
         * 23단계가 확인한 규칙이다 - 정지 중에 혼자 움직이는 것이 있으면 그 순간
         * 정지가 연출이 아니라 버그로 보인다. 귀참은 강한 정지를 함께 내므로
         * 이 규칙이 가장 잘 보이는 자리이기도 하다.
         */
        private void Update()
        {
            if (!playing) return;

            elapsed += Time.deltaTime;

            float total = holdSeconds + fadeSeconds;
            if (elapsed >= total) { Hide(); return; }

            // 튀김: 등장 직후에만
            float punch = punchSeconds > 0f ? Mathf.Clamp01(elapsed / punchSeconds) : 1f;
            float scale = Mathf.Lerp(punchScale, 1f, punch);
            rect.localScale = new Vector3(scale, scale, 1f);

            // 떠오름: 전 구간에 걸쳐 천천히
            float rise = Mathf.Clamp01(elapsed / total);
            rect.anchoredPosition = restPosition + new Vector2(0f, riseDistance * rise);

            // 사라짐: hold 뒤에만
            float alpha = elapsed <= holdSeconds
                ? 1f
                : 1f - Mathf.InverseLerp(holdSeconds, total, elapsed);

            var color = label.color;
            color.a = alpha;
            label.color = color;

            if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0.75f * alpha);
        }
    }
}
