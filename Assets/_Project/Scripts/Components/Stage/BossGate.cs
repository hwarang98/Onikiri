using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 보스전 앞에 한 번 세워지는 토리이 관문.
     *
     * ## 왜 스크롤 배경에서 뺐는가
     *
     * 17단계 초안에서 토리이는 배경 소품이었다. 배경이 흐르기 시작하자 두 가지가
     * 드러났다 - 반복 스크롤에 섞인 랜드마크는 **계속 다시 나오고**, 스크롤에서
     * 빠진 랜드마크는 **화면에 붙박여 따라온다.** 둘 다 "왜 토리이가 계속
     * 나오지?"가 된다.
     *
     * 랜드마크는 반복되면 안 된다. 그래서 토리이는 배경이 아니라 **사건**이 됐다:
     * 보스에게 달려가는 동안 딱 한 번 나타나고, 그 밑을 지나면 보스의 영역이다.
     *
     * ## 순서
     *
     * 보스보다 **가까이** 세운다. 보스는 자기 걸음까지 더해 더 빨리 다가오므로,
     * 같은 자리에서 출발하면 보스가 관문을 앞질러 먼저 도착한다 - 관문을 지나기
     * 전에 보스를 만나면 "영역에 들어섰다"는 뜻이 사라진다.
     *
     * ## 시간
     *
     * 배경과 같은 속도(`ParallaxScroller.BaseSpeed`)로 흐른다. 그래야 관문이
     * 지면에 박힌 것으로 보이고, 히트스톱이 걸리면 함께 언다 - 벚꽃 파티클과
     * 같은 규칙이다.
     */
    public sealed class BossGate : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sprite;

        /**
         * @brief 달려가기의 몇 지점에서 관문을 지나는가 (0~1).
         *
         * 0.45면 5.3초 달려가기의 2.4초쯤에 통과한다. 보스는 그보다 뒤에
         * 도착하므로 "관문을 지나 보스의 영역에 들어섰다"는 순서가 성립한다.
         *
         * 거리가 아니라 **비율**인 이유는, 전진 속도나 달려가기 시간을 바꿔도
         * 순서가 유지되어야 하기 때문이다. 고정 거리로 두면 속도를 올리는
         * 순간 보스가 관문을 앞지른다 - 실제로 그렇게 만들었다가 고쳤다.
         */
        [Tooltip("달려가기 구간의 몇 %에서 관문을 지나는가")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float passFraction = 0.45f;

        [Tooltip("이보다 왼쪽으로 지나가면 치운다. 관문은 통과하면 끝이고 " +
                 "화면에 남아 있을 이유가 없다")]
        [SerializeField] private float despawnX = -9f;

        [Header("등급")]
        /**
         * @brief 지역 피날레의 관문 배율.
         *
         * **정수만.** 픽셀 격자가 어긋나지 않는 유일한 값이다(보스 확대 배율과
         * 같은 규칙).
         *
         * 2배는 시도했다가 되돌렸다 - 세로로도 두 배가 되면서 관문 상단이 화면
         * 밖으로 잘리고 보스 체력 바·타이머와 겹쳤다. 전투 밴드 높이가 정해져
         * 있어서(화면의 45~90%) 세로로 키울 여유가 없다.
         *
         * 그래서 크기가 아니라 **색**으로 무게를 준다. 붉은 틴트와 등장 연출이
         * 피날레를 가르고, 크기는 표준 관문과 같다.
         */
        [SerializeField] private int finaleScale = 1;

        /**
         * @brief 지역 피날레 관문의 틴트.
         *
         * 크기를 못 키우므로(위 finaleScale 참고) 이것이 피날레를 가르는 유일한
         * 신호다. 그만큼 분명해야 한다.
         *
         * 곱연산이라 밝은 값이어야 아트가 안 뭉갠다 - 한 채널을 255에 두고
         * 나머지를 낮춘다. 보스 확대판 틴트와 같은 규칙이다.
         */
        [SerializeField] private Color finaleTint = new Color32(0xFF, 0x5A, 0x4A, 0xFF);

        [SerializeField] private Color normalTint = Color.white;

        private float baseY;
        private bool active;

        /** 플레이어가 이 관문을 지났는가. 연출 판단에만 쓴다 */
        public bool Passed { get; private set; }

        private void Awake()
        {
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            baseY = transform.localPosition.y;
            Hide();
        }

        /**
         * @brief 관문을 세운다. 보스 달려가기가 시작될 때 한 번.
         *
         * @param finale 지역 피날레인가. 더 크고 붉은 관문이 선다
         */
        /**
         * @param finale     지역 피날레인가. 더 크고 붉은 관문이 선다
         * @param runUpSeconds 달려가기에 걸리는 시간. 관문 위치를 여기서 유도한다
         * @param scrollSpeed  전진 속도
         */
        public void Show(bool finale, float runUpSeconds, float scrollSpeed)
        {
            if (sprite == null) return;

            int scale = finale ? Mathf.Max(1, finaleScale) : 1;
            transform.localScale = new Vector3(scale, scale, 1f);

            // 배율이 커지면 밑동도 함께 올라간다. 피벗이 중앙이라 절반 높이만큼
            // 올려야 기둥이 지면선에 닿는데, 그 절반이 배율만큼 커진다
            float startX = scrollSpeed * runUpSeconds * passFraction;
            transform.localPosition = new Vector3(startX, baseY * scale, 0f);

            sprite.color = finale ? finaleTint : normalTint;
            sprite.enabled = true;

            active = true;
            Passed = false;
        }

        public void Hide()
        {
            active = false;
            Passed = false;
            if (sprite != null) sprite.enabled = false;
        }

        private void Update()
        {
            if (!active) return;

            // 배경과 같은 속도. 스케일 타임이라 히트스톱이 함께 얼린다
            var local = transform.localPosition;
            local.x -= ParallaxScroller.BaseSpeed * Time.deltaTime;
            transform.localPosition = local;

            // 플레이어(월드 원점 근처)를 지나면 통과로 친다
            if (!Passed && local.x <= 0f) Passed = true;

            if (local.x <= despawnX) Hide();
        }
    }
}
