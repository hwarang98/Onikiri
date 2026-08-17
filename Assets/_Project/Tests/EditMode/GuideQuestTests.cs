using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 가이드 진행선의 **표의 성질**을 검사한다.
     *
     * 카드 자체(상태 전환·갱신)는 MonoBehaviour라 EditMode에서 돌리기 어렵고,
     * 그쪽은 테스트 패널의 "가이드 퀘스트" 절이 잡는다. 여기서 지키는 것은
     * 표가 가리키는 곳이 실제로 존재하는가와, 그 순서가 학습 순서로서 말이
     * 되는가다 - 둘 다 화면에서는 "카드가 좀 이상하다" 정도로만 보인다.
     *
     * 가이드는 새 퀘스트를 만들지 않고 기존 업적을 가리키므로
     * (GuideQuestCatalog 머리 주석), **퀘스트 표를 고치는 사람이 가이드를
     * 모르는 채로 고칠 수 있다.** 업적 id 하나를 지우면 그 칸은 조용히
     * 건너뛰어지고, 진행선에서 한 단계가 소리 없이 사라진다. 그 소리를
     * 여기서 낸다.
     */
    public class GuideQuestTests
    {
        [Test]
        public void EveryStep_PointsAtARealQuest()
        {
            Assert.Greater(GuideQuestCatalog.Count, 0, "가이드 진행선이 비어 있다");

            foreach (var step in GuideQuestCatalog.Steps)
            {
                // 퀘스트를 안 가리키는 칸은 이 검사의 대상이 아니다. 완료를
                // 세이브에서 직접 읽으므로(GuideGate) 가리킬 퀘스트가 없다 -
                // 그 칸의 계약은 TheGuideGate_OnlyShowsAfterTheShopOpens 가 잰다
                if (step.Gate != GuideGate.Quest) continue;

                Assert.GreaterOrEqual(step.Index, 0,
                    "가이드가 가리키는 퀘스트 '" + step.QuestId + "' 가 QuestCatalog에 없다. "
                    + "퀘스트를 표에서 빼려면 GuideQuestCatalog의 그 칸도 함께 고쳐야 한다 - "
                    + "안 그러면 진행선에서 한 단계가 조용히 사라진다");

                var specs = QuestCatalog.Of(step.Kind);
                Assert.Less(step.Index, specs.Length);
                Assert.AreEqual(step.QuestId, specs[step.Index].Id);
            }
        }

        [Test]
        public void Steps_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var step in GuideQuestCatalog.Steps)
                Assert.IsTrue(seen.Add(step.QuestId),
                    "가이드에 '" + step.QuestId + "' 가 두 번 있다. 같은 목표가 두 번 오면 "
                    + "두 번째 칸은 처음부터 받은 상태라 화면에 뜨지 않는다");
        }

        /**
         * @brief 행동 문구는 제목과 **다른 문장이어야 한다.**
         *
         * 제목("5스테이지 도달")은 무엇이 끝인지를 말하고, 행동은 무엇을
         * 하라는 말이다. 둘이 같으면 카드의 두 줄이 같은 말을 반복하고,
         * 그 순간 이 카드가 퀘스트 화면의 목록 한 줄과 구별되지 않는다.
         */
        [Test]
        public void EveryStep_HasAnActionThatIsNotTheTitle()
        {
            foreach (var step in GuideQuestCatalog.Steps)
            {
                Assert.IsFalse(string.IsNullOrEmpty(step.Action),
                    "'" + step.QuestId + "' 에 행동 문구가 없다");

                if (step.Index < 0) continue;   // 위 테스트가 이미 보고했다

                Assert.AreNotEqual(QuestCatalog.Of(step.Kind)[step.Index].Title, step.Action,
                    "'" + step.QuestId + "' 의 행동 문구가 제목과 같다");
            }
        }

        /**
         * @brief 같은 지표가 연달아 서지 않는다.
         *
         * 기획(개선안 v2, 2절 원칙 4)이 "3~4스테이지마다 새 기능 하나"라고
         * 적은 것의 표 쪽 번역이다. 스테이지 업적만 넷이 연달아 서면 가이드가
         * 하는 말은 "계속 진행하라" 하나뿐이고, 그것은 안내가 아니다.
         *
         * **코리더(st1~30) 안에서만** 두 칸까지다. 그 뒤는 새 기능이 아니라
         * 깊이라 가리킬 것이 스테이지밖에 없고(업적 표에 st35·40·45·50이
         * 넷 연달아 있다), 거기서 규칙을 강요하면 표에 없는 업적을 만들라는
         * 요구가 된다. 규칙이 지켜야 하는 것은 **학습이 일어나는 구간**이다.
         */
        [Test]
        public void TheLine_DoesNotRepeatTheSameMetricThreeTimesInTheCorridor()
        {
            var steps = GuideQuestCatalog.Steps;
            int run = 1;

            for (int i = 1; i < steps.Length; i++)
            {
                if (steps[i].Index < 0 || steps[i - 1].Index < 0) { run = 1; continue; }

                var spec = QuestCatalog.Of(steps[i].Kind)[steps[i].Index];
                var before = QuestCatalog.Of(steps[i - 1].Kind)[steps[i - 1].Index].Metric;

                run = spec.Metric == before ? run + 1 : 1;
                if (run < 3) continue;

                bool pastTheCorridor = spec.Metric == QuestMetric.StageReached && spec.Target > 30d;

                Assert.IsTrue(pastTheCorridor,
                    "가이드 " + (i + 1) + "번째까지 " + spec.Metric + " 가 세 칸 연달아 선다. "
                    + "코리더(st1~30)에서 그것은 학습 순서가 아니라 같은 말의 반복이다");
            }
        }

        /**
         * @brief 같은 지표 안에서는 목표치가 커지기만 한다.
         *
         * 진행선은 차례이지 요약이 아니므로(GuideQuestLine 주석) 앞 칸이 뒤
         * 칸보다 어려우면 **뒤 칸이 뜰 때 이미 다 채워져 있다.** 그러면
         * 카드가 "완료 가능"으로 한 번 깜빡이고 지나가고, 그 칸은 학습
         * 순서에서 없는 것과 같다.
         */
        [Test]
        public void TheLine_RisesWithinEachMetric()
        {
            var highest = new Dictionary<QuestMetric, double>();

            foreach (var step in GuideQuestCatalog.Steps)
            {
                if (step.Index < 0) continue;

                var spec = QuestCatalog.Of(step.Kind)[step.Index];

                double before;
                if (highest.TryGetValue(spec.Metric, out before))
                    Assert.Greater(spec.Target, before,
                        "'" + spec.Id + "' 의 목표(" + spec.Target + ")가 같은 지표의 앞 칸("
                        + before + ")보다 크지 않다. 뒤 칸은 뜨자마자 완료 상태가 된다");

                highest[spec.Metric] = spec.Target;
            }
        }

        /**
         * @brief 카드가 약속하는 보상은 **가리키는 퀘스트의 것 그대로**다.
         *
         * 이 스텝의 밸런스 계약이 여기 걸려 있다 - 가이드는 화면이지 수도꼭지가
         * 아니고, 그래서 보스 여유 밴드가 한 칸도 안 움직인다. 카드가 자기
         * 숫자를 적기 시작하면 그 순간 화면과 지급이 두 벌이 되고, 둘이
         * 어긋나는 것은 "받았는데 적힌 것보다 적다"로만 나타난다.
         */
        [Test]
        public void RewardText_ShowsTheQuestsOwnGems()
        {
            foreach (var step in GuideQuestCatalog.Steps)
            {
                if (step.Index < 0) continue;

                var spec = QuestCatalog.Of(step.Kind)[step.Index];
                var view = default(GuideQuestView);
                view.Gems = spec.Gems;
                view.HasGold = spec.GoldMobs > 0d;

                StringAssert.Contains(spec.Gems.ToString(), GuideQuestLine.RewardText(view),
                    "카드가 적는 보상이 가리키는 퀘스트('" + spec.Id + "')의 보상과 다르다");
            }
        }
    }
}
