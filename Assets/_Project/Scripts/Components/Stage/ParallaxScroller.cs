using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 배경 레이어 하나를 왼쪽으로 무한 스크롤한다.
     *
     * ## 왜 필요한가
     *
     * 16단계까지 화면은 정지해 있었고 요괴만 걸어왔다. 플레이어가 하는 일은
     * 제자리에서 기다리는 것이었고, 10분 플레이 소감의 첫 항목이 "나아가는
     * 느낌이 없다"였다.
     *
     * 이제 뒤집는다. 사무라이는 화면 X를 지키되 달리는 동작을 하고, 배경이
     * 흘러 전진을 만든다. 적이 사거리에 들어오면 멈추고 벤다.
     *
     * ## 구현
     *
     * 스프라이트 두 장을 이어 붙여 돌린다. 왼쪽으로 한 장 폭만큼 밀리면 그
     * 장을 오른쪽 끝으로 보낸다 - 위치를 계속 더하지 않고 폭으로 나머지 연산을
     * 하므로, 몇 시간을 돌려도 float 정밀도가 무너지지 않는다.
     *
     * 두 장으로 충분한 이유는 레이어 폭(353px = 11.03 units)이 화면 폭보다
     * 넓기 때문이다. 좁아지면 세 장이 필요하다.
     *
     * ## 시간
     *
     * `Time.deltaTime`(스케일 타임)을 쓴다. 히트스톱이 걸리면 배경도 함께
     * 멈춘다 - 벚꽃 파티클과 같은 규칙이고, 타격 순간 배경만 흐르면 그 정지가
     * "게임이 멈췄다"가 아니라 "배경만 고장났다"로 읽힌다.
     */
    [ExecuteAlways]
    public sealed class ParallaxScroller : MonoBehaviour
    {
        [Tooltip("이 레이어의 상대 속도. 0이면 안 움직이고, 1이면 기준 속도. " +
                 "원경일수록 작게 - 그 차이가 깊이를 만든다")]
        [SerializeField] private float relativeSpeed = 1f;

        [Tooltip("한 장의 폭 (월드 단위). 빌더가 스프라이트에서 재서 적어준다")]
        [SerializeField] private float spanWidth;

        /**
         * @brief 이어 붙인 사본들.
         *
         * 원본을 포함한다. 빌더가 만들어 넣고, 런타임에는 위치만 옮긴다 -
         * 매 프레임 Instantiate/Destroy 하지 않는다.
         */
        [SerializeField] private Transform[] pieces;

        /** 지금까지 흘러간 거리. 폭으로 나머지를 취해 정밀도를 지킨다 */
        private float travelled;

        /** 기준 속도 (월드 단위/초). 스크롤 주체가 매 프레임 정한다 */
        public static float BaseSpeed { get; private set; }

        /** 지금 스크롤 중인가. 정지 중에는 달리기 애니메이션도 멈춰야 한다 */
        public static bool IsScrolling { get { return BaseSpeed > 0.001f; } }

        /**
         * @brief 모든 레이어가 공유하는 속도를 정한다.
         *
         * 레이어마다 따로 켜고 끄지 않는 이유는, 그러면 어떤 레이어는 흐르고
         * 어떤 레이어는 멈춘 상태가 조합으로 생기기 때문이다. 하나의 값이
         * 전부를 지배하면 그 상태가 존재할 수 없다.
         */
        public static void SetBaseSpeed(float speed)
        {
            BaseSpeed = Mathf.Max(0f, speed);
        }

        private void Update()
        {
            if (pieces == null || pieces.Length == 0 || spanWidth <= 0.0001f) return;

            float speed = BaseSpeed * relativeSpeed;
            if (speed > 0f)
            {
                // 스케일 타임. 히트스톱이 배경도 함께 얼린다
                travelled += speed * Time.deltaTime;

                // 폭 하나를 넘으면 되감는다. 누적값을 그대로 두면 몇 시간 뒤
                // float이 큰 수에서 정밀도를 잃어 배경이 덜덜 떨린다
                if (travelled >= spanWidth) travelled -= spanWidth;
            }

            Place();
        }

        private void Place()
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;

                float x = i * spanWidth - travelled;

                // 왼쪽으로 완전히 빠진 장은 오른쪽 끝으로 보낸다
                if (x <= -spanWidth) x += spanWidth * pieces.Length;

                var local = pieces[i].localPosition;
                pieces[i].localPosition = new Vector3(x, local.y, local.z);
            }
        }

        /** 빌더가 쓴다. 인스펙터에서 손으로 맞출 값이 아니다 */
        public void Configure(float relative, float span, Transform[] parts)
        {
            relativeSpeed = relative;
            spanWidth = span;
            pieces = parts;
            travelled = 0f;
            Place();
        }
    }
}
