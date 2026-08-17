using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 지금 전진 중인가를 정하고, 배경 스크롤과 달리기 동작을 함께 켠다.
     *
     * ## 리듬
     *
     *   사거리가 비었다  ->  달린다 (배경 스크롤 ON)
     *   벨 것이 들어왔다  ->  멈추고 벤다 (스크롤 OFF)
     *   베고 나면        ->  다시 달린다
     *
     * 16단계까지는 이 리듬이 없었다. 사무라이는 제자리에 서 있고 요괴가
     * 걸어왔으며, 플레이어가 하는 일은 기다리는 것이었다. 10분 플레이 소감의
     * 첫 항목이 "나아가는 느낌이 없다"였고, 그 원인이 여기다.
     *
     * ## 판단이 한 곳에 있어야 하는 이유
     *
     * 배경 스크롤과 달리기 클립은 **같은 사실의 앞뒤**다. 둘을 각자 판단하게
     * 두면 "배경은 흐르는데 사무라이는 서 있는" 조합이 생기고, 그건 버그로
     * 보이지 않고 그냥 어색해 보인다 - 원인을 찾기 가장 어려운 종류다.
     *
     * 그래서 여기가 유일한 판단 지점이고, 결과는 `ParallaxScroller`의 정적
     * 속도 하나로 내보낸다. 달리기 클립은 그 값을 읽어 따라간다.
     */
    [DefaultExecutionOrder(-20)]
    public sealed class StageAdvance : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private BossFight bossFight;

        [Tooltip("배경이 흐르는 속도 (월드 단위/초). 화면폭이 6.75u라 화면 하나 " +
                 "지나가는 데 6.75/이 값 초가 걸린다. Update가 매 프레임 읽으므로 " +
                 "플레이 중에 끌면 바로 반영된다. 발이 미끄러져 보이면 이 값을 " +
                 "내리거나 PlayerCombat.runFrameRate를 올린다 - 둘의 비가 미끄러짐이다")]
        [Range(1f, 24f)]
        [SerializeField] private float scrollSpeed = 4.5f;

        /**
         * @brief 전진 속도. 보스 스폰 거리를 이 값에서 유도한다.
         *
         * 여기서 내보내는 이유는 달려가기 시간이 이 속도에 달려 있기 때문이다.
         * 보스를 고정 거리에 세우면 속도를 바꿀 때마다 달려가기 시간이 조용히
         * 달라지고, 시뮬레이션이 가정한 5.3초와 갈린다.
         */
        public float ScrollSpeed { get { return scrollSpeed; } }

        [Tooltip("멈출 때/출발할 때 속도가 붙고 빠지는 시간. 0이면 툭 끊긴다")]
        [SerializeField] private float rampSeconds = 0.18f;

        private float current;

        /** 지금 전진해야 하는가 */
        public bool ShouldAdvance
        {
            get
            {
                // 벨 것이 사거리에 있으면 멈춘다. 이것이 기본 규칙이다
                if (combat != null && combat.HasTargetInRange) return false;

                if (bossFight == null) return true;

                switch (bossFight.Current)
                {
                    // 보스에게 달려가는 구간. 17단계에서 "보스가 걸어온다"를
                    // "플레이어가 달려간다"로 뒤집은 자리이고, 이 구간이
                    // 제한 시간 밖이다
                    case BossFight.Phase.Approaching: return true;

                    // 암전·클리어 배너·실패 문구가 떠 있는 동안은 멈춘다.
                    // 화면이 무언가를 말하는 중에 배경이 흐르면 그 문구가
                    // 지나가는 풍경의 일부처럼 읽힌다
                    case BossFight.Phase.Intro:
                    case BossFight.Phase.Cleared:
                    case BossFight.Phase.Failed:
                        return false;

                    // 보스와 붙어 있는 동안은 combat 쪽 규칙을 따른다.
                    // 보스가 죽고 다음 보스가 없으면 다시 달린다
                    default: return true;
                }
            }
        }

        private void Update()
        {
            float target = ShouldAdvance ? scrollSpeed : 0f;

            // 스케일 타임. 히트스톱이 걸리면 감속도 함께 얼어야 한다 - 배경만
            // 계속 감속하면 정지 프레임에서 속도가 슬금슬금 변한다
            float step = rampSeconds > 0.0001f
                ? scrollSpeed * Time.deltaTime / rampSeconds
                : scrollSpeed;

            current = Mathf.MoveTowards(current, target, step);
            ParallaxScroller.SetBaseSpeed(current);

            // 달리기 <-> idle 전환을 사무라이에게 알린다. 스크롤 상태가 바뀌는
            // 순간에만 부르므로 매 프레임 클립을 갈아끼우지 않는다.
            //
            // **성공했을 때만 상태를 소비한다.** 스윙 도중이면 거절당하는데
            // (PlayerCombat.RefreshRestingClip), 그때 wasScrolling을 먼저 갱신하면
            // 전환이 통째로 유실된다 - 다음 프레임부터는 바뀐 것이 없으므로 다시
            // 부를 기회가 없고, 사무라이는 idle 자세로 굳는다.
            //
            // 보스 도전 순간에 마침 스윙 중이면 그 상태로 보스까지 달려가는
            // 버그가 정확히 이것이었다. 스윙이 걸리느냐에 달려 있어 간헐적으로만
            // 재현됐다.
            bool scrolling = ParallaxScroller.IsScrolling;
            if (scrolling != wasScrolling)
            {
                if (combat == null || combat.RefreshRestingClip())
                    wasScrolling = scrolling;
            }
        }

        private bool wasScrolling;

        private void OnDisable()
        {
            // 씬을 나가거나 비활성화될 때 배경을 세운다. 정적 값이라 다음
            // 실행에 그대로 남으면 전투 없이 배경만 흐르는 상태로 시작한다
            ParallaxScroller.SetBaseSpeed(0f);
            current = 0f;
        }
    }
}
