using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 사다리를 **걸어서 못 닿는 경우**가 있다는 계약.
     *
     * ## 이 파일은 멈춘 게임에서 나왔다
     *
     * 결과 팝업이 미끄러진 경로를 적으려고 `Rolled`에서 `Outcome`까지
     * `SlideFor`로 걸었다:
     *
     * ```csharp
     * for (var at = result.Rolled; at != result.Outcome; at = SlideFor(at))
     * ```
     *
     * 사다리는 아래로만 간다(개안 -> 해금 -> XP, 그 아래는 자기 자신).
     * 그런데 **천장은 결과를 위로 덮는다** - `RollOnce`가 낮게 굴린 회차를
     * 개안으로 바꾼다. 그러면 낮은 칸에서 높은 칸에 영영 못 닿고, 바닥이
     * 자기 자신을 돌려주므로 루프가 안 끝나 문자열이 무한히 자란다.
     *
     * 소프트 천장이 서른 회마다 서니 십연 몇 번이면 걸린다. 그런데도
     * 643개가 전부 통과했다 - **어느 검사도 뽑기 결과를 글자로 만들지
     * 않았기 때문이다.** 계산은 다 잠겨 있는데 그것을 읽는 쪽이 안 잠겨
     * 있었고, 이 파일이 그 자리를 메운다.
     */
    public class SlideChainTests
    {
        private static readonly SkillGachaCurve.Outcome[] All =
            (SkillGachaCurve.Outcome[])System.Enum.GetValues(typeof(SkillGachaCurve.Outcome));

        /**
         * @brief 어느 두 칸을 넣어도 **끝난다.**
         *
         * 이것이 이 파일의 본론이다. 무한 루프는 단언으로 못 잡으므로
         * 함수가 돌아오는 것 자체가 검사다 - 안 돌아오면 러너가 멈춘다.
         */
        [Test]
        public void TheSlideWalk_TerminatesForEveryPair()
        {
            foreach (var from in All)
                foreach (var to in All)
                {
                    bool reachable = SkillGachaCurve.SlidesTo(from, to);

                    // 닿는다고 했으면 실제로 걸어서 닿아야 한다. 여기가
                    // 터지는 대신 멈춘다면 그것이 옛 버그 그대로다
                    if (!reachable) continue;

                    var at = from;
                    for (int guard = 0; at != to; guard++)
                    {
                        Assert.Less(guard, 8,
                            "SlidesTo가 참이라 했는데 걸어서 못 닿는다 (" + from + " -> " + to + ")");
                        at = SkillGachaCurve.SlideFor(at);
                    }
                }
        }

        /** 같은 칸은 걷지 않고 도착이다 */
        [Test]
        public void TheSlideWalk_AcceptsTheSameRung()
        {
            foreach (var outcome in All)
                Assert.IsTrue(SkillGachaCurve.SlidesTo(outcome, outcome),
                    outcome + " 이 자기 자신에 못 닿는다고 나온다");
        }

        /**
         * @brief **위로는 못 걷는다.** 게임을 멈춘 그 방향이다.
         *
         * 개안이 XP보다 위라는 것은 등급이 말한다. 아래에서 위로 물으면
         * 반드시 거짓이어야 하고, 그것이 팝업의 for 문을 막는 유일한 장치다.
         */
        [Test]
        public void TheSlideWalk_NeverClimbs()
        {
            foreach (var low in All)
                foreach (var high in All)
                {
                    if (SkillGachaCurve.GradeFor(high) <= SkillGachaCurve.GradeFor(low)) continue;

                    Assert.IsFalse(SkillGachaCurve.SlidesTo(low, high),
                        "아래(" + low + ")에서 위(" + high + ")로 걸을 수 있다고 나온다. "
                        + "천장이 덮은 회차의 팝업이 여기서 무한히 돈다");
                }
        }

        /** 사다리의 두 칸은 실제로 이어져 있어야 한다 - 위 검사가 헛것이 아니게 */
        [Test]
        public void TheSlideWalk_StillDescendsTheRealLadder()
        {
            Assert.IsTrue(SkillGachaCurve.SlidesTo(SkillGachaCurve.Outcome.Awakening,
                                                   SkillGachaCurve.Outcome.SkillUnlock),
                "개안이 해금으로 못 내려간다");

            Assert.IsTrue(SkillGachaCurve.SlidesTo(SkillGachaCurve.Outcome.Awakening,
                                                   SkillGachaCurve.Outcome.XpSurge),
                "개안이 두 칸 내려 XP까지 못 간다 - 팝업의 세 단어 줄이 이것이다");

            Assert.IsTrue(SkillGachaCurve.SlidesTo(SkillGachaCurve.Outcome.SkillUnlock,
                                                   SkillGachaCurve.Outcome.XpSurge),
                "해금이 XP로 못 내려간다");
        }

        /**
         * @brief 천장이 위로 덮은 회차는 **미끄러짐이 아니다.**
         *
         * `Downgraded`가 `Rolled != Outcome`이던 시절에는 천장 회차가
         * 전부 "내려갔다"로 적혔다. 화면이 올라간 것을 내려갔다고 말한다.
         */
        [Test]
        public void ThePityPull_IsNotADowngrade()
        {
            // 천장이 실제로 만드는 쌍이다 - 낮게 굴렸는데 개안을 받는다
            Assert.IsFalse(SkillGachaCurve.SlidesTo(SkillGachaCurve.Outcome.XpSmall,
                                                    SkillGachaCurve.Outcome.Awakening),
                "XP 회차에서 개안으로 '미끄러졌다'고 나온다");

            Assert.IsFalse(SkillGachaCurve.SlidesTo(SkillGachaCurve.Outcome.XpSmall,
                                                    SkillGachaCurve.Outcome.SkillUnlock),
                "XP 회차에서 해금으로 '미끄러졌다'고 나온다");
        }
    }
}
