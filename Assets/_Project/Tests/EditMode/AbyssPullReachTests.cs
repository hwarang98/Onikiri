using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 나락인력의 **공간 효과**를 실측한다. 6단계의 첫 자물쇠.
     *
     * ## 왜 StageSimulation으로는 못 재는가
     *
     * 시뮬레이션은 초당 환산 기여(Rate) 하나로 세계를 본다. 나락인력의 Rate는
     * 다른 여덟과 똑같이 0.180이므로 **그쪽에서는 흡인이 존재하지 않는다** -
     * 켜든 끄든 같은 숫자가 나온다. 설계가 "Rate 비교만으로 통과 처리하지
     * 않는다"고 못 박은 자리가 정확히 그곳이다.
     *
     * 흡인이 실제로 바꾸는 것은 **이후 오의가 몇 마리를 맞히는가**다. 그것은
     * 배율의 문제가 아니라 자리의 문제이고, 자리는 씬의 상수에서 나온다.
     *
     * ## 이 검사가 재는 것 - 자리에서 나오는 타격 수
     *
     * 잡몹은 큐로 선다(EnemySpawner.Reflow) - 전선(x 0.05)부터 뒤로 큐 간격
     * (0.75u)마다 한 마리다. 각 오의의 판정 창은 안무에 적혀 있고, 그 둘을
     * 겹치면 **한 번의 시전이 몇 마리를 맞히는지가 산수로 나온다.**
     *
     * 흡인은 그 자리를 바꾼다. 최대 여덟을 시전자 +1.6u로 모으므로, 그 뒤의
     * 시전은 짧은 사거리로도 여덟을 다 맞힌다.
     *
     * ## 합격식은 설계의 것을 그대로 쓴다
     *
     *     0.9804 <= T(장착) / T(미장착) <= 1.0200
     *
     * 시간 대신 **타격 수**로 잰다. 고정된 잡몹 체력 풀에서 총 타격 수는 총
     * 피해에 비례하고 총 피해는 소요 시간에 반비례하므로, 시간 비는 타격 비의
     * 역수다. 그 환산을 여기 적어 두는 이유는 **가정이 검사 안에 보이게**
     * 하기 위해서다 - 숨기면 다음 사람이 이 검사를 시간 측정으로 오해한다.
     *
     * ## ⚠ 이것은 PlayMode 측정이 아니다
     *
     * 설계 §6.4는 고정 시드·고정 웨이브의 PlayMode 실주행을 요구한다. 이
     * 검사는 그 자리를 대신하지 않는다 - **자리 계산이 맞는지**를 먼저 잠그고,
     * 실제 전달 경로(DeliverSkillLane/Around/Captured)가 이 계산대로 동작하는지는
     * PlayMode 리그가 선 뒤에 잰다. 둘 다 있어야 6단계가 닫힌다.
     */
    public class AbyssPullReachTests
    {
        // ---------------------------------------------------------------- 씬 실측 상수

        /** 사무라이의 월드 X (Main.unity의 Samurai) */
        private const float SamuraiX = -1.2f;

        /** 잡몹 큐의 맨 앞자리 (EnemySpawner.frontLineX) */
        private const float FrontLineX = 0.05f;

        /** 큐에서 한 마리가 차지하는 간격 (EnemyDefinition.queueSpacing) */
        private const float QueueSpacing = 0.75f;

        /** 화면 오른쪽 끝 (EnemySpawner.RightEdgeX의 기본값) */
        private const float RightEdgeX = 3.375f;

        /** 관통 판정이 뒤로 봐주는 폭 (PlayerCombat.DeliverSkillLane) */
        private const float LaneBackTolerance = 0.3f;

        /** 나락인력이 대상을 옮기는 자리 (안무 pullDestinationOffset) */
        private const float PullDestination = SamuraiX + 1.6f;

        /** 한 번에 잡는 최대 대상 수 (SkillSpec.PullTargetCount) */
        private const int PullTargets = 8;

        /**
         * @brief 흡인이 대상을 찾는 전방 거리 (안무 pullRange).
         *
         * **가장 짧은 판정 창과 같은 값이다.** 그보다 길면 멀리 있던 잡몹이
         * 짧은 창 안으로 들어와 흡인이 없던 사거리를 만든다 - 아래 예산
         * 검사가 그 순간을 잡는다.
         */
        private const float PullRange = 3.0f;

        /**
         * @brief 화면에 설 수 있는 잡몹 수.
         *
         * 전선부터 화면 우단까지 큐 간격으로 나눈 값이다. 이보다 많으면
         * 뒤쪽은 화면 밖이라 어느 오의도 못 맞힌다.
         */
        private static int ScreenMobCount
        {
            get { return (int)((RightEdgeX - FrontLineX) / QueueSpacing) + 1; }
        }

        /** i번째 잡몹의 월드 X. 0이 맨 앞이다 */
        private static float MobX(int index)
        {
            return FrontLineX + index * QueueSpacing;
        }

        // ---------------------------------------------------------------- 판정 창

        /**
         * @brief 오의별 가로 판정 창 (사무라이 기준 dx). 안무의 실측값이다.
         *
         * Screen은 자리를 안 보므로 목록에 없다 - 흡인이 있든 없든 살아 있는
         * 전부를 맞히고, 그래서 이 검사의 대상이 아니다.
         */
        private struct Window
        {
            public string Id;
            public float Reach;      // 전방 사거리
            public bool Circle;      // 원이면 뒤로도 Reach 만큼 든다
        }

        private static readonly Window[] Windows =
        {
            new Window { Id = SkillCatalog.FlashId,      Reach = 4.6f },   // 일섬
            new Window { Id = SkillCatalog.BloodFallId,  Reach = 4.0f },   // 낙혈
            new Window { Id = SkillCatalog.BloodWhipId,  Reach = 5.4f },   // 혈조
            new Window { Id = SkillCatalog.SwordFieldId, Reach = 3.6f },   // 검진 (장판)
            new Window { Id = SkillCatalog.MoonArcId,    Reach = 3.0f, Circle = true }, // 회월참
        };

        /** 이 창이 잡몹 자리 목록에서 몇 마리를 맞히는가 */
        private static int HitsIn(Window window, IList<float> mobs)
        {
            int hits = 0;
            foreach (var x in mobs)
            {
                float dx = x - SamuraiX;
                float back = window.Circle ? -window.Reach : -LaneBackTolerance;
                if (dx >= back && dx <= window.Reach) hits++;
            }
            return hits;
        }

        /** 흡인 전: 큐에 늘어선 자리 */
        private static List<float> SpreadMobs(int count)
        {
            var mobs = new List<float>();
            for (int i = 0; i < count; i++) mobs.Add(MobX(i));
            return mobs;
        }

        /**
         * @brief 흡인 후: 앞의 여덟이 도착점으로 모이고 나머지는 제자리.
         *
         * 흡인 범위(3.0u) 밖은 안 끌린다. 화면이 꽉 차면 다섯 중 셋만 그 안에
         * 들고, 그 셋은 **이미 모든 판정 창이 닿는 자리**다 - 그래서 자리는
         * 바뀌는데 누가 맞는지는 안 바뀐다.
         */
        private static List<float> PulledMobs(int count)
        {
            var mobs = new List<float>();
            int taken = 0;

            for (int i = 0; i < count; i++)
            {
                float dx = MobX(i) - SamuraiX;
                bool inRange = dx >= 0f && dx <= PullRange && taken < PullTargets;

                if (inRange) taken++;
                mobs.Add(inRange ? PullDestination : MobX(i));
            }
            return mobs;
        }

        // ---------------------------------------------------------------- 검사

        /**
         * @brief 화면 구성이 씬 상수에서 나오는가. 아래 둘의 전제다.
         */
        [Test]
        public void TheQueueGeometry_MatchesTheScene()
        {
            Assert.AreEqual(5, ScreenMobCount, string.Format(
                "화면에 서는 잡몹이 {0}마리다 - 전선 {1} · 간격 {2} · 우단 {3}에서 나오는 "
                + "수와 다르면 아래 타격 수가 전부 어긋난다",
                ScreenMobCount, FrontLineX, QueueSpacing, RightEdgeX));

            // 도착점은 평타 사거리(2.9u) 안이어야 한다. 끌어모아 놓고 못 때리면
            // 이 오의의 유틸리티가 아무 뜻이 없다
            float destinationDx = PullDestination - SamuraiX;
            Assert.Less(destinationDx, 2.9f,
                "흡인 도착점이 평타 사거리 밖이다 - 모아 놓고 못 때린다");

            // 그리고 가장 짧은 판정 창(검진 3.6u) 안이기도 해야 한다
            Assert.Less(destinationDx, 3.6f, "흡인 도착점이 장판 밖이다");

            // **흡인 사거리는 가장 짧은 판정 창을 넘지 않는다.** 넘으면 멀리
            // 있던 잡몹이 짧은 창 안으로 들어와 없던 사거리가 생긴다 -
            // 실측으로 5.0u에서 클리어가 16% 빨라졌다
            float shortest = float.MaxValue;
            foreach (var window in Windows)
                if (window.Reach < shortest) shortest = window.Reach;

            Assert.LessOrEqual(PullRange, shortest, string.Format(
                "흡인 사거리 {0}u가 가장 짧은 판정 창 {1}u보다 길다 - 흡인이 "
                + "위치를 옮기는 것을 넘어 사거리를 만든다", PullRange, shortest));
        }

        /**
         * @brief **흡인이 타격 수를 2% 넘게 못 바꾼다.** 설계 §6.4의 합격식.
         *
         * 화면이 꽉 찬 상태(다섯)에서 잰다. 그보다 적으면 짧은 창도 전부
         * 맞히므로 흡인의 값이 0이 되고, 그 세계에서 통과하는 것은 아무것도
         * 증명하지 못한다 - **가장 불리한 구성**에서 재야 한다.
         */
        [Test]
        public void ThePull_StaysInsideTheClearTimeBudget()
        {
            var spread = SpreadMobs(ScreenMobCount);
            var pulled = PulledMobs(ScreenMobCount);

            int before = 0, after = 0;
            var detail = new System.Text.StringBuilder();

            foreach (var window in Windows)
            {
                int b = HitsIn(window, spread);
                int a = HitsIn(window, pulled);
                before += b;
                after += a;

                int index = SkillCatalog.IndexOf(window.Id);
                detail.Append(string.Format("{0} {1}->{2}  ",
                    index >= 0 ? SkillCatalog.Skills[index].DisplayName : window.Id, b, a));
            }

            Assert.Greater(before, 0, "흡인 전에 아무도 안 맞는다 - 자리 계산이 깨졌다");

            // 타격 비의 역수가 곧 시간 비다 (머리 주석의 환산)
            double hitRatio = after / (double)before;
            double timeRatio = 1d / hitRatio;

            Assert.LessOrEqual(timeRatio, 1.0200d, string.Format(
                "흡인이 클리어를 {0:P2} 느리게 만든다 - 상한 2.00%. [{1}]", timeRatio - 1d, detail));

            Assert.GreaterOrEqual(timeRatio, 0.9804d, string.Format(
                "흡인이 클리어를 {0:P2} 빠르게 만든다 - 상한 2.00%. 유틸리티가 파워가 "
                + "됐다는 뜻이고, 그러면 이 오의만 초당 기여 저울 밖에 선다. [{1}]",
                1d - timeRatio, detail));
        }

        /**
         * @brief 화면이 **덜 찬** 구성에서도 같은 결론인가.
         *
         * 위 검사가 최악을 재는 동안 이쪽은 흔한 구성을 잰다. 셋뿐이면 짧은
         * 창도 전부 닿으므로 흡인의 값이 0이어야 한다 - 여기서 값이 나오면
         * 흡인이 자리를 바꾸는 것 이상을 하고 있다는 뜻이다.
         */
        [Test]
        public void ThePull_ChangesNothingWhenTheQueueIsShort()
        {
            var spread = SpreadMobs(3);
            var pulled = PulledMobs(3);

            foreach (var window in Windows)
            {
                Assert.AreEqual(HitsIn(window, spread), HitsIn(window, pulled), string.Format(
                    "잡몹이 셋뿐인데 흡인이 '{0}'의 타격 수를 바꿨다 - 이미 전부 닿는 "
                    + "구성에서 흡인은 아무것도 더할 것이 없어야 한다", window.Id));
            }
        }
    }
}
