using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 오의 뽑기(50단계). **파는 것이 폭과 시간이지 파워가 아닌가.**
     *
     * 이 스텝의 주장은 한 줄이다: 뽑기는 오의를 **상한까지 더 빨리** 밀고
     * **고를 것을 넓힐** 뿐, 상한 자체를 한 톨도 안 민다. 그 주장이 참이면
     * 45단계의 오의 몫 계약(49.0% / 한계 50%)이 이 스텝을 통과하고, 거짓이면
     * 계약이 깨진 것을 밴드가 알아채기 전에 여기가 먼저 알려야 한다.
     *
     * 그래서 재는 것이 세 겹이다.
     *
     *   **상한**   XP를 아무리 부어도 MaxLevel에서 멈추는가 (유일한 안전선)
     *   **불변**   밴드가 가정하는 세계가 뽑기로 안 움직이는가 (등호)
     *   **값**     그래도 무언가 하는가 (상한 도달 가속 · 폭)
     *
     * 세 번째가 특히 중요하다. 이 축은 보석당 DPS가 0이라 47단계가 쓰던 자
     * (시간·도달층)로는 0이 나오고, 0을 그대로 "죽은 버튼"으로 읽으면 이
     * 스텝이 지킨 생명줄이 결함으로 보고된다. **자를 하나 더 만든다** -
     * 상한에 몇 스테이지 일찍 닿는가.
     */
    public class SkillGachaTests
    {
        static StageSimulation.Field Field()
        {
            // 36단계 불변식: 주력 w5(HP12/골드5) + 부몹 w4(HP17/골드6)
            return new StageSimulation.Field
            {
                AverageMobHealth = 128d / 9d,
                AverageMobGold = 49d / 9d,
                SpawnInterval = 1.1d
            };
        }

        static int Index(string id) { return SkillCatalog.IndexOf(id); }

        static double TotalSeconds(List<StageSimulation.StageResult> rows, int from, int to)
        {
            double total = 0d;
            foreach (var row in rows)
                if (row.Stage >= from && row.Stage <= to) total += row.MobSeconds + row.BossKillSeconds;
            return total;
        }

        static double MaxSkillShare(List<StageSimulation.StageResult> rows, int from)
        {
            double share = 0d;
            foreach (var row in rows)
                if (row.Stage >= from && row.SkillDpsShare > share) share = row.SkillDpsShare;
            return share;
        }

        /** 이 오의가 상한에 처음 닿는 스테이지. 끝까지 안 닿으면 -1 */
        static int CeilingStage(List<StageSimulation.StageResult> rows, int skill)
        {
            foreach (var row in rows)
                if (row.SkillLevels[skill] >= SkillCurve.MaxLevel) return row.Stage;
            return -1;
        }

        static StageSimulation.Policy WithGacha()
        {
            var policy = new StageSimulation.Policy();
            policy.SkillGacha = true;
            return policy;
        }

        // ---------------------------------------------------------------- 사다리

        /**
         * @brief **사다리가 하나다.** 확률·등급·천장·가격·일일 무료가 요도 표에서 온다.
         *
         * 값을 옮겨 적지 않고 가리키는 것이 설계다(SkillGachaCurve 머리 주석) -
         * 상점의 두 배너가 다른 확률·다른 천장을 쓰면 47단계가 등급 색과 별로
         * 세운 눈금이 배너마다 다른 뜻이 되고, 플레이어는 사다리를 두 번 배워야
         * 한다.
         *
         * 이 검사가 지키는 것은 값이 아니라 **그 결정**이다. 언젠가 두 뽑기가
         * 다른 물건이 되는 날 이 검사가 먼저 깨지고, 그때 표를 나누는 것이
         * 의식적인 선택이 된다.
         *
         * ---------------------------------------------------------------------
         * ⚠ **교체 예정 (재설계 v2.2 §12.2-1, 구현 5단계)**
         *
         * 그날이 왔다. 스킬 뽑기가 st14(ShopCurve.UnlockStage)로 내려오고 요도는
         * st41에 남으므로 **아래 UnlockStage 단언만 제거**한다. 나머지는 그대로다 -
         * 확률표·등급·가격·일일 무료·★4 소프트 천장은 계속 공유하기 때문이다.
         *
         * 이름도 바뀐다: `TheLadder_SharesTheTableButNotTheGate`.
         * 새 문장은 "기본 확률표·가격·일일 무료·★4 소프트 천장은 공유하고,
         * 상점 해금 시점과 ★5 하드 천장은 스킬 뽑기 전용이다"이다.
         * ---------------------------------------------------------------------
         */
        [Test]
        public void TheLadder_IsTheSameLadderAsTheYodoBanner()
        {
            Assert.AreSame(GachaCurve.Chances, SkillGachaCurve.Chances,
                "확률표가 갈렸다 - 두 배너가 다른 사다리를 쓰면 등급 색이 뜻을 잃는다");

            Assert.AreEqual(GachaCurve.OutcomeCount, SkillGachaCurve.OutcomeCount,
                "결과 수가 갈렸다 - 확률표 UI가 같은 상자를 못 쓴다");
            Assert.AreEqual(GachaCurve.PityPulls, SkillGachaCurve.PityPulls);
            Assert.AreEqual(GachaCurve.PullCostGems, SkillGachaCurve.PullCostGems);
            Assert.AreEqual(GachaCurve.TenPullCostGems, SkillGachaCurve.TenPullCostGems);
            Assert.AreEqual(GachaCurve.FreePullsPerDay, SkillGachaCurve.FreePullsPerDay);

            // ⚠ 이 한 줄이 5단계에 삭제된다 (v2.2 §12.2-1). 스킬 배너는 st14,
            //   요도 배너는 st41로 갈리므로 등호가 성립하지 않는다
            Assert.AreEqual(GachaCurve.UnlockStage, SkillGachaCurve.UnlockStage,
                "배너가 상점보다 먼저(또는 나중에) 열린다");

            // 등급이 자리마다 맞물려야 GradeOf를 그대로 쓸 수 있다
            for (int i = 0; i < SkillGachaCurve.OutcomeCount; i++)
                Assert.AreEqual(GachaCurve.GradeOf[i],
                                SkillGachaCurve.GradeFor((SkillGachaCurve.Outcome)i),
                    "결과 " + i + "의 등급이 요도 표와 어긋난다");
        }

        /**
         * @brief **꽝이 없다.** 여섯 결과가 전부 무언가를 준다.
         *
         * 46단계가 요도 표에서 세운 규칙 그대로다 - 방치형에서 아무것도 안
         * 주는 결과는 긴장이 아니라 짜증이다. 이쪽에서는 그 규칙이 사다리의
         * **바닥이 XP**라는 사실로 성립한다: 위 둘이 막혀도 미끄러져 내려오면
         * XP는 언제나 받는다.
         */
        [Test]
        public void EveryOutcome_GivesSomething()
        {
            for (int i = 0; i < SkillGachaCurve.OutcomeCount; i++)
            {
                var outcome = (SkillGachaCurve.Outcome)i;

                if (outcome == SkillGachaCurve.Outcome.SkillUnlock
                    || outcome == SkillGachaCurve.Outcome.Awakening)
                {
                    Assert.AreNotEqual(outcome, SkillGachaCurve.SlideFor(outcome),
                        "'" + outcome + "'이 막히면 갈 곳이 없다 - 천장이 준 결과가 사라진다");
                    continue;
                }

                Assert.Greater(SkillGachaCurve.XpFor(outcome), 0,
                    "'" + outcome + "'이 아무것도 안 준다 - 꽝 없음이 이 표의 계약이다");
            }
        }

        /**
         * @brief 미끄러짐이 **반드시 끝난다.** 사다리의 바닥은 XP다.
         *
         * `SkillGachaSystem.Grant`가 스스로를 재귀로 다시 부르므로 이 성질이
         * 없으면 뽑기 한 번이 무한 루프가 된다. 47단계는 미끄러짐이 손으로
         * 두 번 적혀 있어 이 위험이 없었는데, 두 칸이 되면서 재귀가 더 정직한
         * 표현이 됐고 그 대가로 정지 조건을 검사가 지켜야 한다.
         */
        [Test]
        public void SlidingDownTheLadder_AlwaysEndsInXp()
        {
            for (int i = 0; i < SkillGachaCurve.OutcomeCount; i++)
            {
                var at = (SkillGachaCurve.Outcome)i;

                for (int step = 0; step < SkillGachaCurve.OutcomeCount + 1; step++)
                {
                    var next = SkillGachaCurve.SlideFor(at);
                    if (next == at) break;
                    at = next;
                }

                Assert.AreEqual(at, SkillGachaCurve.SlideFor(at),
                    "'" + (SkillGachaCurve.Outcome)i + "'의 미끄러짐이 안 멈춘다");
                Assert.Greater(SkillGachaCurve.XpFor(at), 0,
                    "'" + (SkillGachaCurve.Outcome)i + "'이 아무것도 아닌 곳에서 멈춘다");
            }
        }

        /**
         * @brief 가챠 몫이 **표 순서대로, 그리고 두 번만** 열린다.
         *
         * 무작위로 고르지 않는 이유는 44단계 요도와 같다 - 무작위면 "무엇을
         * 뽑으려 하는지"를 화면이 말할 수 없고, 둘째를 먼저 받은 플레이어와
         * 아닌 플레이어가 다른 세계에 살게 된다.
         *
         * 그리고 **셋째가 없다.** 그것이 이 축의 재고가 유한하다는 사실이고,
         * 다 팔린 배너가 닫히는 근거다.
         *
         * ---------------------------------------------------------------------
         * ⚠ **폐기 예정 (재설계 v2.2 §12.2-2, 구현 5단계)**
         *
         * `Assert.AreEqual(2, UnlockOrder.Length)`가 **단일 풀 전제**다. v2.2는
         * 배열을 둘로 가른다:
         *
         *     StandardUnlockOrder   혈조 -> 혈폭 -> 심격 -> 회월참 -> 검진   (5종)
         *     OniSecretUnlockOrder  귀신난무 -> 나락인력 -> 참수 -> 귀왕강림  (4종)
         *
         * 그러므로 이 검사를 둘로 나눈다:
         *     TheStandardPool_IsTheTableOrderAndItRunsOut    (5종)
         *     TheOniSecretPool_IsTheTableOrderAndItRunsOut   (4종)
         *
         * 그리고 `SkillRosterContractTests.TheTwoPools_...`가 두 배열의 교집합이
         * 비었는지를 함께 잰다.
         * ---------------------------------------------------------------------
         */
        [Test]
        public void TheUnlockOrder_IsTheTableOrderAndItRunsOut()
        {
            Assert.AreEqual(2, SkillGachaCurve.UnlockOrder.Length,
                "가챠 몫이 둘이 아니다 - 재고가 바뀌었으면 배너가 닫히는 조건도 함께 봐야 한다");

            int owned = 0;
            for (int i = 0; i < SkillGachaCurve.UnlockOrder.Length; i++)
            {
                int expected = SkillCatalog.IndexOf(SkillGachaCurve.UnlockOrder[i]);
                int target = SkillGachaCurve.UnlockTargetFor(owned);

                Assert.AreEqual(expected, target, "해금 순서가 표 순서와 다르다");
                Assert.IsTrue(SkillGachaCurve.IsGachaGated(target),
                    "진행으로 열리는 오의가 뽑기 몫에 들어 있다");

                owned |= 1 << target;
            }

            Assert.IsTrue(SkillGachaCurve.AllUnlocked(owned));
            Assert.AreEqual(-1, SkillGachaCurve.UnlockTargetFor(owned),
                "셋째 오의가 열린다 - 이 축의 재고는 둘로 끝나야 한다");
        }

        // ---------------------------------------------------------------- 상한 (안전선)

        /**
         * @brief **상한이 안 움직인다.** 이 스텝의 유일한 안전선이다.
         *
         * 45단계가 못 박은 오의 몫 계약(49.0% / 한계 50%)은 배율 상한
         * (SkillCurve.CeilingRatio) 위에서 유도된 값이다. 뽑기가 그 상한을
         * 한 칸이라도 넘기면 계약이 그 자리에서 깨지고, 밴드가 알아채는
         * 것은 그다음 재기준 때다.
         *
         * 그래서 여기서 재는 것이 값 둘이 아니라 **경로가 없다는 사실**이다 -
         * XP는 레벨을 사고, 레벨은 MaxLevel에서 멈추고, MaxLevel의 다음 칸에는
         * 값이 없다.
         */
        [Test]
        public void SkillXp_CannotPushPastTheCeiling()
        {
            Assert.AreEqual(12, SkillCurve.MaxLevel,
                "오의 레벨 상한이 움직였다 - 오의 몫 계약이 이 값 위에 서 있다");
            Assert.AreEqual(3.2d, SkillCurve.CeilingRatio, 1e-12d,
                "배율 상한이 움직였다 - 45단계의 계약을 다시 유도해야 한다");

            Assert.AreEqual(0L, SkillGachaCurve.XpToNextLevel(SkillCurve.MaxLevel),
                "상한 레벨에 다음 칸의 값이 있다 - XP가 상한을 넘길 경로가 생긴다");
            Assert.AreEqual(0L, SkillGachaCurve.XpToNextLevel(SkillCurve.MaxLevel + 5),
                "상한 위에도 값이 있다");

            // 배율도 상한에서 닫힌다. 레벨이 어떤 이유로 넘어 들어와도(손으로
            // 고친 세이브 등) 값은 안 넘는다 - 26단계부터의 규칙이다
            foreach (var skill in SkillCatalog.Skills)
            {
                double ceiling = SkillCurve.CeilingFor(skill.BaseMultiplier);
                Assert.AreEqual(ceiling,
                    SkillCurve.CappedMultiplierAtLevel(skill.BaseMultiplier, 9999), ceiling * 1e-12d,
                    "'" + skill.DisplayName + "'의 배율이 상한 위로 간다");
            }
        }

        /**
         * @brief XP 곡선이 단조롭고, 한 오의의 상한이 **천장 한 바퀴** 언저리다.
         *
         * 크기의 근거가 여기 있다(SkillGachaCurve.XpBase 주석). 682 XP는 기대
         * 37.7회이고 천장이 30회이므로, "10연 셋이면 오의 하나가 거의 상한"이
         * 47단계가 잡아 둔 리듬 위에 그대로 선다.
         *
         * 더 싸면 첫 10연에 상한이 닿아 남은 구간이 잉여가 되고(21단계 골드
         * 축), 더 비싸면 무과금의 일일 무료가 한 오의에 두 달을 쓴다.
         */
        [Test]
        public void TheXpCurve_PutsOneSkillAtAboutOnePityCycle()
        {
            long previous = 0L;
            for (int level = 1; level < SkillCurve.MaxLevel; level++)
            {
                long cost = SkillGachaCurve.XpToNextLevel(level);
                Assert.Greater(cost, 0L, "Lv." + level + "의 다음 칸이 공짜다");
                Assert.Greater(cost, previous - 1L, "XP 곡선이 내려간다 - 뒤 칸이 더 싸다");
                previous = cost;
            }

            double pulls = SkillGachaCurve.TotalXpToCap / SkillGachaCurve.ExpectedXpPerPull;

            Assert.Greater(pulls, SkillGachaCurve.PityPulls * 0.8d, string.Format(
                "오의 하나를 상한까지 미는 데 {0:F1}회밖에 안 든다 - 첫 10연에 닿으면 "
                + "남은 구간이 전부 잉여가 된다", pulls));
            Assert.Less(pulls, SkillGachaCurve.PityPulls * 2d, string.Format(
                "오의 하나에 {0:F1}회가 든다 - 일일 무료만 도는 무과금에게 두 달이다", pulls));
        }

        // ---------------------------------------------------------------- 불변

        /**
         * @brief **밴드가 가정하는 구성이 안 움직인다.** 뽑아도 기준 구성은 그대로다.
         *
         * 가챠 몫 둘은 신규 셋과 초당 기여가 동률이고(SkillCatalog.ExpansionRate)
         * 동률은 표 순서로 갈리므로, 표에서 뒤에 선 이 둘은 기준 구성에 못
         * 들어간다. 그래서 마스크를 켜도 심층 구성이 한 비트도 안 바뀐다.
         *
         * 이 성질이 **오의 몫 계약을 구조로 지킨다** - 밴드가 재는 세계에
         * 가챠 몫이 애초에 없으므로, 49단계가 잰 49.0%를 다시 유도할 필요가
         * 없다. 49단계가 슬롯으로 "풀 크기와 밴드를 떼어놓은" 것의 배당금이다.
         *
         * ---------------------------------------------------------------------
         * ⚠ **수정 예정 (재설계 v2.2 §12.2-5, 구현 5단계)**
         *
         * 아래에서 마스크를 만드는 줄이 `UnlockOrder` 하나를 훑는다. 배열이 둘로
         * 갈리면 **두 배열을 합쳐** 마스크를 지어야 한다 - 안 그러면 귀오의 넷이
         * 마스크에서 빠져 "뽑아도 기준 구성이 안 움직인다"를 절반만 재게 된다.
         *
         * 검사의 주장 자체는 안 바뀐다. 신규 일곱도 전부 GachaGated이고 상한 기여가
         * 0.576 동률이라 기준 구성은 그대로다.
         * ---------------------------------------------------------------------
         */
        [Test]
        public void OwningTheGachaSkills_DoesNotMoveTheReferenceLoadout()
        {
            int owned = 0;
            foreach (var id in SkillGachaCurve.UnlockOrder) owned |= 1 << SkillCatalog.IndexOf(id);

            var without = new int[SkillCurve.MaxSlots];
            int a = SkillCatalog.ReferenceLoadout(int.MaxValue, int.MaxValue, without,
                                                  SkillCurve.MaxSlots, 0);
            var with = new int[SkillCurve.MaxSlots];
            int b = SkillCatalog.ReferenceLoadout(int.MaxValue, int.MaxValue, with,
                                                  SkillCurve.MaxSlots, owned);

            Assert.AreEqual(a, b, "뽑고 나니 자리가 더 찼다 - 슬롯 예산이 뽑기에 흔들린다");
            for (int i = 0; i < a; i++)
                Assert.AreEqual(without[i], with[i], string.Format(
                    "{0}번 자리가 '{1}'에서 '{2}'로 바뀌었다 - 뽑기가 밴드를 움직인다",
                    i, SkillCatalog.Skills[without[i]].DisplayName,
                    SkillCatalog.Skills[with[i]].DisplayName));

            // 심층 구성 캐시도 같은 답이어야 한다. 이것이 기대 곡선이 읽는 값이다
            Assert.AreEqual(a, SkillCatalog.DeepLoadout.Length);
        }

        /**
         * @brief 코리더·가속·심층이 **비트 단위로** 불변이다.
         *
         * 44단계가 요도를 st41에 건 것과 같은 수법이고 같은 증명이다 - 뽑기가
         * 있는 세계와 없는 세계를 나란히 돌려 모든 줄을 등호로 비교한다.
         *
         * 값 범위가 아니라 등호인 이유는 범위 검사가 "조금 움직였다"를
         * 통과시키기 때문이다. 조율이 끝난 구간에서 조금 움직이는 것은 없다.
         *
         * 심층까지 함께 재는 것이 이 스텝에만 있는 요구다. 46·47단계는 심층이
         * **움직여야 했고**(그래서 재기준했다), 여기서는 안 움직이는 것이
         * 계약이다 - 뽑기가 파워를 안 파므로 재기준할 것이 없어야 한다.
         */
        [Test]
        public void TheWholeBand_IsBitIdenticalWithAndWithoutTheGacha()
        {
            var field = Field();
            var without = StageSimulation.Run(200, field, new StageSimulation.Policy());
            var with = StageSimulation.Run(200, field, WithGacha());

            Assert.AreEqual(without.Count, with.Count);

            int different = 0;
            double worst = 0d;
            for (int i = 0; i < without.Count; i++)
            {
                double delta = System.Math.Abs(without[i].BossMargin - with[i].BossMargin);
                if (delta <= 0d) continue;

                different++;
                if (delta > worst) worst = delta;
            }

            Assert.AreEqual(0, different, string.Format(
                "뽑기가 {0}개 스테이지의 여유를 움직였다 (최대 편차 {1:E3}) - "
                + "이 축은 파워를 안 팔기로 한 축이고, 움직였다면 어딘가에서 판 것이다",
                different, worst));
        }

        /**
         * @brief 오의 몫이 계약 안에 있고, **뽑기가 그것을 못 민다.**
         *
         * 45단계의 한계가 50%다(자동 공격이 뒤집히는 자리). 49단계가 49.0%까지
         * 밀어 두었으므로 여유가 1%p뿐이고, 이 스텝이 그 1%p를 쓰면 다음
         * 스텝은 쓸 자리가 없다.
         *
         * 두 세계를 다 잰다 - 뽑기가 없는 세계와 있는 세계. 값이 같아야 하고
         * 둘 다 한계 아래여야 한다.
         */
        [Test]
        public void TheOugiShare_StaysInsideTheContractWithTheGachaOn()
        {
            var field = Field();
            double without = MaxSkillShare(StageSimulation.Run(200, field,
                                                              new StageSimulation.Policy()), 51);
            double with = MaxSkillShare(StageSimulation.Run(200, field, WithGacha()), 51);

            Assert.Less(without, 0.5d, "뽑기 없이도 오의 몫이 계약 한계를 넘었다");
            Assert.AreEqual(without, with, 1e-12d, string.Format(
                "뽑기가 오의 몫을 {0:P2}에서 {1:P2}로 밀었다 - 상한을 넘겼거나 "
                + "기준 구성이 바뀌었다는 뜻이고, 둘 다 이 스텝이 안 하기로 한 일이다",
                without, with));
        }

        /**
         * @brief **뽑기로 얻는 것이 진행으로 얻는 것보다 세지 않다.**
         *
         * 49단계가 상성 배정으로 만든 성질을 여기서 **시간으로** 잰다. 그쪽은
         * 배수를 비교했고(가챠 몫 x1.88 대 가족 x2.79), 이쪽은 그 배수가 실제
         * 진행에서 어느 쪽으로 나오는지를 본다 - 배수의 부등호가 시간의
         * 부등호로 뒤집히지 않는지가 f2p 바닥의 실제 근거이기 때문이다.
         *
         * 오의 몫도 함께 잰다. 가챠 몫을 끼운 세계가 **더 낮아야** 한다 -
         * 그것이 "밴드는 언제나 최선의 구성을 가정한다"가 참인 이유다.
         */
        [Test]
        public void EquippingAGachaSkill_IsNeverFasterThanTheProgressionOne()
        {
            var field = Field();
            int oni = Index(SkillCatalog.OniCleaveId);
            int flash = Index(SkillCatalog.FlashId);
            int chain = Index(SkillCatalog.ChainSlashId);
            int wave = Index(SkillCatalog.BloodWaveId);
            int burst = Index(SkillCatalog.BloodBurstId);

            var reference = new StageSimulation.Policy();
            reference.ForceLoadout = new[] { oni, flash, chain, wave };

            var gacha = WithGacha();
            gacha.ForceLoadout = new[] { oni, flash, chain, burst };

            var referenceRows = StageSimulation.Run(200, field, reference);
            var gachaRows = StageSimulation.Run(200, field, gacha);

            double slower = TotalSeconds(gachaRows, 41, 200) / TotalSeconds(referenceRows, 41, 200) - 1d;

            Assert.GreaterOrEqual(slower, 0d, string.Format(
                "가챠 몫을 끼운 세계가 {0:P2} 빠르다 - 뽑기가 파워를 파는 순간 "
                + "f2p 바닥이 약속이 아니라 광고가 된다", -slower));

            Assert.LessOrEqual(MaxSkillShare(gachaRows, 51), MaxSkillShare(referenceRows, 51),
                "가챠 몫을 끼우니 오의 몫이 올랐다 - 밴드가 가정하는 최선이 최선이 아니게 된다");
        }

        // ---------------------------------------------------------------- 값

        /**
         * @brief (a) **가속이 실재한다** - 새 자를 하나 만든다.
         *
         * 47단계가 도달층을 만든 이유와 같은 자리다. 그쪽은 여유가 20배인
         * 세계에서 시간이 정밀도를 잃어 자를 바꿨고, 여기서는 **애초에 시간이
         * 0이어야 하는 축**이라 시간으로는 아무것도 못 잰다 - 파워를 안 파는
         * 것이 이 스텝의 계약이므로 0은 통과이지 실패다.
         *
         * 그래서 재는 것이 **상한에 몇 스테이지 일찍 닿는가**다. 이것이
         * 플레이어가 실제로 사는 것이고("고르고 싶은 오의를 골라도 DPS를 덜
         * 잃는다"), 상한을 안 넘으므로 밴드와 무관하다.
         *
         * 비교군은 축 전체가 아니라 **XP만 없는 세계**다(SkillGachaWithoutXp) -
         * 47단계가 `SkipRarity`로 사다리 층만 걷어낸 것과 같은 처리이고,
         * 축을 통째로 지우면 폭과 가속이 한 숫자에 섞인다.
         */
        [Test]
        public void TheXpLadder_ReachesTheCeilingEarlier()
        {
            var field = Field();
            int oni = Index(SkillCatalog.OniCleaveId);
            int flash = Index(SkillCatalog.FlashId);
            int chain = Index(SkillCatalog.ChainSlashId);
            int burst = Index(SkillCatalog.BloodBurstId);

            var withXp = WithGacha();
            withXp.ForceLoadout = new[] { oni, flash, chain, burst };

            var withoutXp = WithGacha();
            withoutXp.SkillGachaWithoutXp = true;
            withoutXp.ForceLoadout = new[] { oni, flash, chain, burst };

            var fast = StageSimulation.Run(200, field, withXp);
            var slow = StageSimulation.Run(200, field, withoutXp);

            int fastStage = CeilingStage(fast, burst);
            int slowStage = CeilingStage(slow, burst);

            Assert.AreNotEqual(-1, fastStage, "뽑은 오의가 200스테이지까지 상한에 못 닿는다");
            Assert.AreNotEqual(-1, slowStage);

            Assert.LessOrEqual(fastStage, slowStage - 2, string.Format(
                "스킬 XP가 상한 도달을 st{0} -> st{1}로 {2}스테이지밖에 못 앞당긴다 - "
                + "이 축이 파는 것이 가속인데 그 가속이 안 보이면 죽은 버튼이다",
                slowStage, fastStage, slowStage - fastStage));

            // 해금 **직후**의 체감. 뽑은 오의가 Lv.1로 벤치에 앉는 순간
            // 그동안 모은 XP가 그리로 흘러가므로, 화면에서 "뽑았더니 이미
            // 자라 있다"가 된다 - 그것이 이 축의 첫인상이다
            int atUnlock = fast[SkillGachaCurve.UnlockStage - 1].SkillLevels[burst];
            Assert.GreaterOrEqual(atUnlock, 5, string.Format(
                "해금 스테이지에서 뽑은 오의가 Lv.{0}이다 - 상한의 절반도 못 미치면 "
                + "'뽑았는데 약하다'가 되고, 바꿔 끼워 볼 이유가 안 생긴다", atUnlock));
        }

        /**
         * @brief (b) **재고가 유한하고, 다 팔리면 멈춘다.**
         *
         * 47단계는 반대 선택을 했다 - 혼격 상한을 바퀴로 늘려 재고를 계속
         * 만들었고 그 대가로 심층 천장을 +28% 재기준했다. 이 축에서는 그
         * 길이 막혀 있다: 재고를 늘리는 유일한 방법이 오의 레벨 상한을
         * 올리는 것인데, 그 상한이 오의 몫 계약의 유일한 안전선이다.
         *
         * 그래서 유한한 채로 닫는다. 이 검사는 그 사실이 **실제로 일어나는지**를
         * 본다 - 안 멈추면 화면의 배너도 안 닫히고, 보석이 아무것도 안 주는
         * 곳으로 계속 흘러간다.
         *
         * ---------------------------------------------------------------------
         * ⚠ **기준 재설계 예정 (재설계 v2.2 §12.2-4, 구현 5단계)**
         *
         * 두 가지가 틀어진다.
         *
         * **하나. `settledAt <= 60`이 st41 해금 · 재고 2종 전제다.** 상점이 st14로
         * 내려오고 재고가 9종이 되면 이 숫자가 아무것도 안 막는다.
         *
         * **둘. 이름이 `HasStock`의 실제 계약과 어긋난다.** `SkillSystem.HasStock`은
         * 미보유 뽑기 스킬 **또는 장착 중 상한 미도달**을 본다. 두 해금 풀이
         * 소진돼도 장착 오의가 미상한이면 배너는 계속 열려 있고, 그 중간 상태가
         * 정상이다(화면은 "해금 소진 - XP로 지급"을 적는다).
         *
         * 교체본: `TheBanner_ClosesOnlyAfterBothPoolsAreDrainedAndEquippedSkillsAreMaxed`
         *     ① StandardSoldOut == true
         *     ② OniSecretSoldOut == true
         *     ③ 장착 스킬 중 미상한 대상 없음
         *     ④ 그 뒤 뽑기 수가 증가하지 않음
         *     ⑤ 두 풀만 소진된 중간 상태에서는 배너가 **열려 있다**
         * ---------------------------------------------------------------------
         */
        [Test]
        public void TheBanner_StopsWhenItsStockRunsOut()
        {
            var rows = StageSimulation.Run(200, Field(), WithGacha());

            var last = rows[rows.Count - 1];
            Assert.IsTrue(SkillGachaCurve.AllUnlocked(last.SkillGachaOwned),
                "보석 무제한인데 200스테이지까지 가챠 몫을 다 못 열었다");

            // 재고가 닫힌 뒤로는 한 번도 안 돈다. 마지막 뽑기가 일어난
            // 스테이지를 찾아 그 뒤가 평평한지 본다
            double settled = last.SkillGachaPulls;
            int settledAt = -1;
            foreach (var row in rows)
                if (row.SkillGachaPulls >= settled) { settledAt = row.Stage; break; }

            Assert.AreNotEqual(-1, settledAt);
            Assert.LessOrEqual(settledAt, 60, string.Format(
                "재고가 st{0}까지 안 닫힌다 - 유한한 재고라는 것이 이 축의 설계이고, "
                + "안 닫히면 배너가 영원히 열려 아무것도 안 판다", settledAt));

            Assert.Greater(settled, 0d, "보석 무제한인데 한 번도 안 뽑았다");
        }

        // ---------------------------------------------------------------- f2p

        /**
         * @brief **f2p 바닥이 비트 불변이다.** 기본 정책이 이 배너를 안 지난다.
         *
         * 46단계가 요도 뽑기에서 세운 규칙("무과금은 뽑기에 보석을 쓰지
         * 않는다")이 여기서는 **모두에게** 적용된다 - 이 배너의 보석당 DPS가
         * 정확히 0이라, 밴드가 가정하는 최적 지출에 이 배너가 없다
         * (Policy.SkillGacha 주석).
         *
         * 그 결정의 결과가 이 등호다. 무과금의 심층 바닥이 45·46·47·49단계와
         * 부동소수점까지 같고, 무과금이 이 배너에 닿는 경로는 일일 무료뿐이라
         * 시뮬레이션 밖에 있다 - **보고되는 f2p 바닥은 여전히 하한이다.**
         */
        [Test]
        public void TheFreeToPlayFloor_IsUntouched()
        {
            var field = Field();

            var policy = new StageSimulation.Policy();
            policy.GemsFromQuestsOnly = true;
            var without = StageSimulation.Run(200, field, policy);

            var pulled = policy;
            pulled.SkillGacha = false;
            var with = StageSimulation.Run(200, field, pulled);

            for (int i = 0; i < without.Count; i++)
                Assert.AreEqual(without[i].BossMargin, with[i].BossMargin, 0d,
                    "무과금 바닥이 이 스텝에서 움직였다");

            // 그리고 무과금은 이 배너에 보석을 한 푼도 안 쓴다
            Assert.AreEqual(0d, without[without.Count - 1].SkillGachaPulls,
                "기본 정책의 무과금이 오의 뽑기를 돌았다 - 코어 진행의 보석을 가져간다");
        }

        /**
         * @brief 무과금이 **여기에 보석을 쓰면 실제로 손해다.** 잠식 반례.
         *
         * 46단계의 `GachaBeforeCore`와 같은 자리, 같은 목적이다 - 설계의
         * 근거를 말이 아니라 숫자로 남긴다. 이쪽은 재는 것이 하나 더 있다:
         * 이제 무과금의 보석을 노리는 배너가 **둘**이고, 나눠 쓰면 44단계가
         * 지킨 코어 진행이 두 번 쪼개진다.
         *
         * 동시에 **바닥 상수는 안 뚫린다**는 것도 함께 잰다. 잘못 쓴 플레이어가
         * 진행이 느려지는 것과 밴드 밖으로 나가는 것은 다른 일이고, 뒤쪽이면
         * 그것은 선택이 아니라 함정이다.
         */
        [Test]
        public void SpendingFreeToPlayGemsHere_CostsProgressButNeverBreaksTheFloor()
        {
            var field = Field();

            var careful = new StageSimulation.Policy();
            careful.GemsFromQuestsOnly = true;

            var spender = careful;
            spender.SkillGacha = true;

            var carefulRows = StageSimulation.Run(200, field, careful);
            var spenderRows = StageSimulation.Run(200, field, spender);

            double slower = TotalSeconds(spenderRows, 41, 200)
                            / TotalSeconds(carefulRows, 41, 200) - 1d;

            Assert.Greater(slower, 0d,
                "무과금이 이 배너에 보석을 써도 손해가 아니다 - 그러면 일일 무료를 "
                + "f2p 경로로 둔 이유가 사라진다");

            // 그래도 밴드 밖으로는 안 나간다. 33단계의 f2p 바닥 상수다
            foreach (var row in spenderRows)
            {
                if (row.Stage < 51) continue;

                double floor = BossCurve.TierOf(row.Stage) == BossCurve.Tier.Finale ? 1.08d
                             : BossCurve.TierOf(row.Stage) == BossCurve.Tier.Chapter ? 1.25d
                             : 1.4d;

                Assert.GreaterOrEqual(row.BossMargin, floor, string.Format(
                    "st{0}에서 여유가 {1:F3}으로 바닥({2:F2}) 아래다 - 보석을 여기 쓴 것이 "
                    + "느려지는 선택이 아니라 막히는 함정이 된다",
                    row.Stage, row.BossMargin, floor));
            }
        }

        /**
         * @brief **일일 무료만으로 두 오의를 연다.** 두 달이 그 값이다.
         *
         * 천장이 ★4(해금)를 보장하므로 30회 안에 반드시 하나가 열리고
         * (SkillGachaCurve.PityPulls), 하루 한 번이면 그것이 한 달이다.
         * 둘이면 두 달 - 무과금에게 이 축은 **필수가 아니라 시간**이라는
         * 것이 이 산수다.
         *
         * 시뮬레이션에는 달력이 없으므로 이 트리클은 표 밖에 있다(47단계의
         * 일일 무료와 같은 자리). 그래서 여기서 재는 것은 곡선의 산수뿐이다.
         *
         * ---------------------------------------------------------------------
         * ⚠ **폐기 예정 (재설계 v2.2 §12.2-3, 구현 5단계)**
         *
         * `PityPulls * UnlockOrder.Length <= 62`가 **2종 · 단일 천장** 전제다.
         * 9종 · 이중 천장에서는 이 산수가 성립하지 않는다.
         *
         * 그리고 아래 611행 언저리가 `ExpectedPullsPerUnlock`의 **유일한 테스트
         * 소비처**다. 그 값이 단일 천장 닫힌 식(20.2208)에서 이중 천장 정상해
         * (25.6475)로 바뀌므로 소비처도 함께 교체된다.
         *
         * 교체본 셋 (v2.2 §12.3의 5·6·7):
         *     TheIntroReward_GuaranteesBloodWhipOnDayZero
         *         무료 10연이 첫 표준 스킬(혈조)을 0일차에 보장한다
         *     TheHardPity_BoundsTheFifthStarAtOneHundred
         *         ★5 간격이 100을 넘지 않는다. 4종 최악 = 4 x 100 = 400회
         *     TheSoftPity_IsResetByFifthStarToo
         *         ★5가 소프트 카운터를 0으로 만든다 (★4 보장이 아니라 ★4 **이상** 보장)
         * ---------------------------------------------------------------------
         */
        [Test]
        public void TheDailyFree_OpensBothSkillsWithinTwoMonths()
        {
            Assert.AreEqual(1, SkillGachaCurve.FreePullsPerDay,
                "일일 무료가 하루 한 번이 아니다 - 아래 산수가 통째로 바뀐다");

            int worstCaseDays = SkillGachaCurve.PityPulls * SkillGachaCurve.UnlockOrder.Length;

            Assert.LessOrEqual(worstCaseDays, 62, string.Format(
                "일일 무료만으로 두 오의를 여는 데 최악 {0}일이 걸린다 - 무과금에게 "
                + "이 축이 '언젠가'가 되면 폭을 판다는 말이 거짓이 된다", worstCaseDays));

            // 평균은 그보다 훨씬 짧다. 천장이 접힌 대기가 20.22회다
            double average = SkillGachaCurve.ExpectedPullsPerUnlock
                             * SkillGachaCurve.UnlockOrder.Length;
            Assert.Less(average, worstCaseDays,
                "천장이 평균을 못 줄인다 - 표와 천장이 어긋났다");
        }
    }
}
