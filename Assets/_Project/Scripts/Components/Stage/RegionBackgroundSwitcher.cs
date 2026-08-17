using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 지역이 바뀌면 배경을 갈아끼운다.
     *
     * ## 왜 스테이지가 아니라 지역인가
     *
     * 스테이지는 10초마다 오르고 지역은 10스테이지마다 바뀐다. 스테이지마다
     * 배경을 다시 만들면 스크롤 위치가 초기화되어 **배경이 매번 튄다** - 지역
     * 안에서는 같은 곳을 계속 달리는 것이 맞다.
     *
     * `BackgroundStage.Apply`가 같은 세트를 걸러내므로 여기서는 매번 불러도
     * 안전하지만, 그 판단을 한 곳에만 두는 편이 낫다.
     *
     * ## 지면선도 함께 옮긴다
     *
     * 팩마다 지면 그림의 두께가 다르다 - 지역 1은 밑단에서 표면까지 24px,
     * 가을숲은 타일에서 구운 스트립이라 64px다. 배경만 갈고 지면선을 그대로
     * 두면 **캐릭터가 흙 속에 서거나 공중에 뜬다.** 실제로 그렇게 나왔다.
     */
    [DefaultExecutionOrder(-40)]
    public sealed class RegionBackgroundSwitcher : MonoBehaviour
    {
        [SerializeField] private BossRoster roster;
        [SerializeField] private BackgroundStage backgroundStage;
        [SerializeField] private BattleStageLayout layout;
        [SerializeField] private StageProgress progress;

        [Tooltip("교체를 가리는 연출. 없으면 즉시 바꾼다")]
        [SerializeField] private Onikiri.UI.RegionTransition transition;

        /** 마지막으로 적용한 지역. 같은 지역이면 아무것도 하지 않는다 */
        private RegionConfig applied;

        /**
         * @brief 첫 적용인가.
         *
         * 세이브를 불러오는 순간에도 이 스위처가 돈다. 거기에 전환을 걸면
         * **게임을 켜자마자 암전이 뜬다** - 지역을 넘은 것이 아니라 그냥
         * 그 지역에서 시작하는 것인데도.
         *
         * 연출은 "바뀌었다"를 말하는 것이고, 첫 적용에는 바뀐 것이 없다.
         */
        private bool applacedOnce;

        private void Start()
        {
            if (progress == null) progress = StageProgress.Instance;
            if (progress != null) progress.Changed += Refresh;

            Refresh();
        }

        private void OnDestroy()
        {
            if (progress != null) progress.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (roster == null || backgroundStage == null) return;

            int stage = progress != null ? progress.Stage : 1;

            int stageInRegion;
            var region = roster.RegionForStage(stage, out stageInRegion);
            if (region == null || region == applied) return;

            bool first = !applacedOnce;
            applacedOnce = true;
            applied = region;

            // 배경이 없는 지역은 앞 지역의 것을 그대로 쓴다. 지역을 늘리는 것과
            // 배경을 준비하는 것이 같은 속도로 진행되지 않기 때문이다
            if (region.background == null) return;

            var set = region.background;
            System.Action swap = () =>
            {
                backgroundStage.Apply(set);

                if (layout != null)
                {
                    // 하늘 채움은 배경마다 새로 만들어진다. 갈아끼우지 않으면 레이아웃이
                    // 파괴된 것을 들고 있어 카메라 크기로 늘리는 일을 멈춘다.
                    // BattleStageLayout.SetSkyFill 참고
                    layout.SetSkyFill(backgroundStage.SkyFill);

                    // 지면선도 함께 옮긴다. 팩마다 지면 두께가 다르므로 배경만 갈면
                    // 캐릭터가 흙 속에 서거나 공중에 뜬다
                    layout.SetBackgroundMetrics(set.groundSurfacePixels, set.backgroundPixelHeight);
                }
            };

            // 첫 적용(세이브 복원)에는 연출이 없다. 바뀐 것이 없기 때문이다
            if (first || transition == null) swap();
            else transition.Play(swap);
        }
    }
}
