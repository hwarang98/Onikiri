using System;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 장착 슬롯(49단계). **밴드가 보는 것이 풀이 아니라 자리 수인가.**
     *
     * 이 스텝의 주장은 하나로 접힌다: 오의 풀이 아무리 커져도 DPS는 안 자란다.
     * 자라는 것은 장착 자리가 늘 때뿐이고, 그 자리는 이 스텝에서 셋에서 넷이
     * 되고 **거기서 끝난다.**
     *
     * 주장이 참이면 다음 스텝(스킬 뽑기)이 풀을 스물로 늘려도 밴드를 다시 잴
     * 필요가 없다. 거짓이면 뽑을 때마다 천장이 밀리고, 47단계가 "+28%를 두어 번
     * 더 쌓으면 밴드가 무의미"라고 남긴 경고가 그대로 실현된다.
     *
     * 그래서 여기서 재는 것은 값이 아니라 **성질**이다.
     */
    public class SkillSlotTests
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

        static double TotalSeconds(System.Collections.Generic.List<StageSimulation.StageResult> rows,
                                   int from, int to)
        {
            double total = 0d;
            foreach (var row in rows)
                if (row.Stage >= from && row.Stage <= to) total += row.MobSeconds + row.BossKillSeconds;
            return total;
        }

        // ---------------------------------------------------------------- 구조

        /**
         * @brief 자리가 **한 번만, 그리고 그 문에서만** 열린다.
         *
         * 이 스텝이 밴드에 지는 빚의 전부가 이 한 칸이다. 스테이지가 오를수록
         * 자리가 계속 는다면 그것은 슬롯이 아니라 그냥 새 축이고, 부채가
         * 무한히 쌓인다.
         */
        [Test]
        public void Slots_RampWithTheBaseSkillsAndThenOpenOnce()
        {
            // 49b: 앞의 셋은 **기본 오의를 배울 때마다** 하나씩 열린다.
            // 그 정의가 49단계 이전의 세계("열린 오의가 전부 나간다")를 그대로
            // 재현하고, 그래서 신규 오의를 코리더에 열어도 공짜다
            Assert.AreEqual(0, SkillCurve.SlotsFor(1, 1), "아무 오의도 안 배웠는데 자리가 있다");

            int previous = 0;
            for (int level = 1; level <= 40; level++)
            {
                int slots = SkillCurve.SlotsFor(level, 1);
                Assert.GreaterOrEqual(slots, previous, "자리가 레벨을 올렸는데 줄었다");
                Assert.LessOrEqual(slots, SkillCurve.BaseSlots, "게이트 앞인데 자리가 " + slots + "개다");
                previous = slots;
            }
            Assert.AreEqual(SkillCurve.BaseSlots, previous, "기본 셋을 다 배워도 자리가 셋이 안 된다");

            // 기본 오의의 해금 레벨마다 정확히 하나씩
            foreach (var skill in SkillCatalog.Skills)
            {
                if (skill.StageGated) continue;

                Assert.Greater(SkillCurve.SlotsFor(skill.UnlockLevel, 1),
                               SkillCurve.SlotsFor(skill.UnlockLevel - 1, 1),
                    "'" + skill.DisplayName + "'을 배웠는데 자리가 안 늘었다");
            }

            // 넷째는 스테이지로, 한 번만
            Assert.AreEqual(SkillCurve.BaseSlots,
                SkillCurve.SlotsFor(int.MaxValue, SkillCurve.ExpansionStage - 1),
                "4번 자리가 게이트 앞에서 이미 열려 있다 - 가속 구간(31~50)의 밴드가 움직인다");

            for (int stage = SkillCurve.ExpansionStage; stage <= 5000; stage += 7)
                Assert.AreEqual(SkillCurve.ExpandedSlots, SkillCurve.SlotsFor(int.MaxValue, stage),
                    string.Format("st{0}에서 자리가 {1}개다 - 자리가 계속 늘면 밴드 부채가 무한히 쌓인다",
                        stage, SkillCurve.SlotsFor(int.MaxValue, stage)));

            Assert.AreEqual(SkillCurve.ExpandedSlots - SkillCurve.BaseSlots, 1,
                "자리가 한 번에 둘 이상 열린다 - 재기준이 그만큼 커진다");

            // **신규 오의는 자리를 안 연다.** 열면 "새 오의 하나 = 자리 하나"가
            // 되어 슬롯이 아무것도 안 막는다
            Assert.AreEqual(SkillCurve.BaseSlots, SkillCurve.SlotsFor(int.MaxValue, 50),
                "신규 오의 셋이 코리더에서 자리를 열고 있다");
        }

        /**
         * @brief 진행 해금 셋이 **코리더 안**에서, 겹치지 않게 열린다.
         *
         * 49b의 목적 자체다 - 49단계는 셋 다 st51이라 코리더 서른 스테이지가
         * 오의 셋 그대로였다. 기본 셋의 해금(st8/15/21)과 같은 스테이지에
         * 겹치면 둘 중 하나는 안 읽히므로 그것도 함께 막는다.
         */
        [Test]
        public void ProgressionSkills_UnlockInsideTheCorridorWithoutColliding()
        {
            var gates = new System.Collections.Generic.List<int>();

            foreach (var skill in SkillCatalog.Skills)
            {
                if (!skill.StageGated) continue;
                if (skill.GachaGated) continue;   // 50단계: 뽑기가 연다

                gates.Add(skill.UnlockStage);

                Assert.LessOrEqual(skill.UnlockStage, 30, string.Format(
                    "'{0}'이 st{1}에 열린다 - 코리더(1~30) 밖이면 이 스텝이 고치려던 "
                    + "'초반이 심심하다'가 그대로 남는다", skill.DisplayName, skill.UnlockStage));

                foreach (var other in SkillCatalog.Skills)
                {
                    if (other.StageGated) continue;

                    Assert.AreNotEqual(other.UnlockStage, skill.UnlockStage, string.Format(
                        "'{0}'과 '{1}'이 같은 st{2}에 열린다 - 둘 중 하나는 안 읽힌다",
                        skill.DisplayName, other.DisplayName, skill.UnlockStage));
                }
            }

            Assert.AreEqual(3, gates.Count, "코리더에서 열리는 신규 오의가 셋이 아니다");

            gates.Sort();
            for (int i = 1; i < gates.Count; i++)
                Assert.Greater(gates[i], gates[i - 1], "두 신규 오의가 같은 스테이지에 열린다");

            // 가챠 몫 둘은 **진행으로 안 열린다.** 49단계에는 이 조건이
            // "게이트가 st51 이상"이었는데, 50단계가 게이트를 뽑기로 옮기면서
            // 스테이지 비교로는 말할 수 없게 됐다(UnlockStage가 이제 비용의
            // 기준점이라 st41이다). 재는 것은 값이 아니라 성질이다 -
            // **최전선을 아무리 밀어도, 레벨을 아무리 올려도 안 열린다.**
            foreach (var id in new[] { SkillCatalog.BloodBurstId, SkillCatalog.BloodWhipId })
            {
                int index = SkillCatalog.IndexOf(id);
                var skill = SkillCatalog.Skills[index];

                Assert.IsTrue(skill.GachaGated,
                    "'" + skill.DisplayName + "'이 뽑기 몫이 아니게 됐다");

                Assert.IsFalse(SkillCatalog.IsUnlockedAt(index, int.MaxValue, int.MaxValue),
                    "'" + skill.DisplayName + "'이 진행으로 열린다 - 뽑기 몫이 아니게 된다");

                Assert.IsTrue(SkillCatalog.IsUnlockedAt(index, 1, 1, 1 << index),
                    "'" + skill.DisplayName + "'을 뽑았는데도 안 열린다");
            }
        }

        /**
         * @brief 신규 다섯의 초당 환산 기여가 **하나의 값**이다.
         *
         * 이 성질이 이 스텝의 두 기둥을 동시에 떠받친다:
         *
         *   밴드   4번 자리에 무엇을 끼우든 예산이 같다 -> 닫힌 식이 성립한다
         *   과금   "더 센 오의"라는 상품이 없다 -> 뽑기가 파워를 못 판다
         *
         * 표에는 배율과 쿨다운만 적히고 그 비는 계산해야 나오므로, 손으로 적은
         * 두 숫자가 어긋나는 것을 눈으로는 못 잡는다.
         */
        [Test]
        public void NewSkills_ShareExactlyOneRate()
        {
            int counted = 0;

            foreach (var skill in SkillCatalog.Skills)
            {
                if (!skill.StageGated) continue;
                counted++;

                Assert.AreEqual(SkillCatalog.ExpansionRate, skill.BaseRate,
                    SkillCatalog.ExpansionRate * 1e-9d, string.Format(
                        "'{0}'의 초당 기여가 {1:F5}다 (설계 {2:F5}). 배율 {3} / 쿨 {4}초가 "
                        + "어긋났다 - 열둘이 동률이라는 것이 이 축의 밸런스 전부다",
                        skill.DisplayName, skill.BaseRate, SkillCatalog.ExpansionRate,
                        skill.BaseMultiplier, skill.CooldownSeconds));
            }

            // 49단계의 다섯 + 15종 재설계의 일곱. **수를 세는 이유는 변함없다** -
            // 표에서 오의가 빠지면 이 검사가 조용히 0개를 훑고 통과하기 때문이다
            Assert.AreEqual(12, counted,
                "레벨 게이트가 아닌 오의가 열둘이 아니다 - 표가 바뀌었으면 이 검사도 함께 봐야 한다");
        }

        /**
         * @brief 풀이 커져도 **슬롯 예산이 안 움직인다.**
         *
         * 4번 자리에 다섯 중 아무것이나 끼워도 상한 기여 합이 같은지를 잰다.
         * 같으면 밴드는 "무엇을 골랐는가"를 몰라도 되고, 다음 스텝이 풀을
         * 늘려도 이 값이 그대로다.
         */
        [Test]
        public void TheSlotBudget_DoesNotDependOnWhichSkillIsEquipped()
        {
            Assert.AreEqual(SkillCurve.ExpandedSlots, SkillCatalog.DeepLoadout.Length,
                "심층 구성이 자리 수만큼 안 채워졌다");

            double budget = YodoAffinityCurve.CappedSkillRate;

            int oni = SkillCatalog.IndexOf(SkillCatalog.OniCleaveId);
            int flash = SkillCatalog.IndexOf(SkillCatalog.FlashId);
            int chain = SkillCatalog.IndexOf(SkillCatalog.ChainSlashId);

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                if (!SkillCatalog.Skills[i].StageGated) continue;

                double swapped = SkillCatalog.CeilingRateOf(new[] { oni, flash, chain, i });

                Assert.AreEqual(budget, swapped, budget * 1e-9d, string.Format(
                    "4번 자리를 '{0}'으로 바꾸니 슬롯 예산이 {1:F4}에서 {2:F4}로 움직인다 - "
                    + "밴드가 장착 구성에 따라 달라지면 나머지 선택지가 전부 함정이 된다",
                    SkillCatalog.Skills[i].DisplayName, budget, swapped));
            }
        }

        /**
         * @brief 기준 구성이 **부동소수점 잡음이 아니라 표 순서**로 정해진다.
         *
         * 다섯의 기여는 설계상 같은 값이지만 `배율 / 쿨다운`을 따로 적어 나눈
         * 결과라 double의 마지막 비트가 갈린다(1.44/8과 0.99/5.5가 정확히 같은
         * double이 아니다). 맨 부등호로 고르면 그 잡음이 장착 순서를 정하고,
         * 실제로 그렇게 나왔다 - 혈파동 대신 낙혈이 뽑혔다.
         *
         * 뜻이 있는 규칙(표 순서)이 뜻이 없는 잡음을 이겨야 한다.
         */
        [Test]
        public void ReferenceLoadout_IsDecidedByTheTableNotByFloatingPointNoise()
        {
            var slots = new int[SkillCurve.MaxSlots];
            int filled = SkillCatalog.ReferenceLoadout(int.MaxValue, int.MaxValue, slots);

            Assert.AreEqual(SkillCurve.ExpandedSlots, filled);

            // 기여가 큰 순서. 같은 값이면 표에서 앞선 것
            for (int i = 1; i < filled; i++)
            {
                double previous = SkillCatalog.CeilingRateOf(slots[i - 1]);
                double current = SkillCatalog.CeilingRateOf(slots[i]);

                Assert.GreaterOrEqual(previous, current - previous * 1e-9d,
                    "장착 순서가 기여 내림차순이 아니다");

                if (Math.Abs(previous - current) <= previous * 1e-9d)
                    Assert.Less(slots[i - 1], slots[i], string.Format(
                        "'{0}'과 '{1}'은 기여가 같은데 표 순서를 어겼다 - 부동소수점 잡음이 "
                        + "장착을 정하고 있다",
                        SkillCatalog.Skills[slots[i - 1]].DisplayName,
                        SkillCatalog.Skills[slots[i]].DisplayName));
            }

            // 같은 입력이면 같은 답. 캐시(DeepLoadout)와도 갈리지 않는다
            for (int i = 0; i < filled; i++)
                Assert.AreEqual(SkillCatalog.DeepLoadout[i], slots[i], "심층 구성 캐시가 다시 계산한 값과 다르다");
        }

        // ---------------------------------------------------------------- 게이트

        /**
         * @brief 게이트가 **종류대로** 걸려 있다.
         *
         * ## 49b에 전제가 뒤집혔다
         *
         * 49단계에는 "신규 다섯이 전부 4번 자리와 같은 문(st51)을 쓴다"였다.
         * 재기준이 한 번으로 끝난다는 것이 근거였는데, 그 근거는 **자리 수가
         * 3으로 고정**일 때만 성립했다 - 코리더에 빈 자리가 있으면 일찍 열린
         * 오의가 그 자리에 그냥 들어가 공짜 DPS가 되기 때문이다.
         *
         * 49b가 자리를 램프로 바꾸면서(SkillCurve.SlotsFor) 코리더의 자리가
         * 언제나 차 있게 됐고, 그래서 해금 스테이지가 밴드와 **무관해졌다.**
         * 남는 규칙은 종류에 대한 것뿐이다:
         *
         *   기본 셋      레벨 게이트. 26단계의 리듬이고 바꾸면 코리더가 움직인다
         *   신규 다섯    스테이지 게이트. 게이트 앞에서는 레벨이 아무리 높아도 안 열린다
         *   4번 자리     st51 그대로. **이 축의 유일한 파워 증가**라 여기만 안 움직인다
         */
        [Test]
        public void GatesAreTypedAndTheFourthSlotDidNotMove()
        {
            Assert.AreEqual(51, SkillCurve.ExpansionStage,
                "4번 자리의 문이 움직였다 - 그것이 이 축의 유일한 파워 증가이고, "
                + "움직이면 심층 천장을 다시 재야 한다");

            foreach (var skill in SkillCatalog.Skills)
            {
                int index = SkillCatalog.IndexOf(skill.Id);

                if (skill.GachaGated)
                {
                    // 50단계: 세 번째 종류. 레벨도 최전선도 이 문을 못 연다 -
                    // 여는 것은 보유뿐이고, 그것이 이 둘의 정의다
                    Assert.IsFalse(SkillCatalog.IsUnlockedAt(index, int.MaxValue, int.MaxValue),
                        "'" + skill.DisplayName + "'이 진행으로 열린다");
                    Assert.IsTrue(SkillCatalog.IsUnlockedAt(index, 1, 1, 1 << index),
                        "'" + skill.DisplayName + "'을 뽑았는데도 안 열린다");

                    // 그래도 **자리는 안 연다.** 뽑을 때마다 자리가 늘면
                    // 슬롯 예산이 아무것도 안 막게 된다
                    Assert.IsTrue(skill.StageGated,
                        "'" + skill.DisplayName + "'이 레벨 게이트가 됐다 - 자리가 하나 늘어난다");
                }
                else if (skill.StageGated)
                {
                    Assert.GreaterOrEqual(skill.UnlockStage, 1,
                        "'" + skill.DisplayName + "'의 스테이지 게이트가 없다");

                    // 레벨을 아무리 올려도 게이트 앞에서는 안 열린다.
                    // 두 게이트가 섞이면 "언제 열리는가"를 화면이 말할 수 없다
                    Assert.IsFalse(SkillCatalog.IsUnlockedAt(index, int.MaxValue,
                                                             skill.UnlockStage - 1),
                        "'" + skill.DisplayName + "'이 게이트 앞에서 레벨만으로 열린다");
                    Assert.IsTrue(SkillCatalog.IsUnlockedAt(index, 1, skill.UnlockStage),
                        "'" + skill.DisplayName + "'이 게이트에 닿았는데 레벨을 더 요구한다");
                }
                else
                {
                    Assert.Greater(skill.UnlockLevel, 0,
                        "'" + skill.DisplayName + "'의 레벨 게이트가 사라졌다 - 26단계의 리듬이다");
                    Assert.Less(skill.UnlockStage, SkillCurve.ExpansionStage,
                        "'" + skill.DisplayName + "'이 심층에서 열린다 - 코리더의 오의가 아니게 된다");

                    // 기본 셋은 최전선과 무관하다. 스테이지로도 열리면 자리 램프가
                    // (SkillCurve.SlotsFor) 세는 수가 달라져 코리더가 움직인다
                    Assert.IsFalse(SkillCatalog.IsUnlockedAt(index, skill.UnlockLevel - 1, 9999),
                        "'" + skill.DisplayName + "'이 레벨 없이 스테이지만으로 열린다");
                }
            }
        }

        /**
         * @brief 코리더와 가속 구간이 **비트 단위로** 불변이다.
         *
         * 이 스텝의 기둥이다. 44단계가 요도를 st41에 건 것과 같은 수법이고,
         * 같은 방식으로 증명한다 - 슬롯이 없는 세계와 있는 세계를 나란히 돌려
         * st1~50의 모든 줄이 **부동소수점까지** 같은지 본다.
         *
         * 값 범위가 아니라 등호로 재는 이유는 범위 검사가 "조금 움직였다"를
         * 통과시키기 때문이다. 조율이 끝난 구간에서 조금 움직이는 것은 없다.
         */
        [Test]
        public void TheCorridorAndTheAccelZone_AreBitIdenticalWithoutTheSlot()
        {
            var field = Field();

            var policies = new[]
            {
                // 49b: 대조군이 **신규 오의가 아예 없는 세계**다. 자리만 줄이는
                // SkipSkillSlot으로는 이 증명이 안 된다 - 신규가 풀에 남아 기준
                // 구성에 끼어들 수 있고, 코리더에서 그 일이 일어나는지가 이
                // 후속의 유일한 위험이었다
                new { Name = "가속", With = StageSimulation.Policy.Default,
                      Without = new StageSimulation.Policy { SkipExpansionSkills = true } },
                new { Name = "무과금",
                      With = new StageSimulation.Policy { GemsFromQuestsOnly = true },
                      Without = new StageSimulation.Policy { GemsFromQuestsOnly = true,
                                                            SkipExpansionSkills = true } }
            };

            foreach (var pair in policies)
            {
                var with = StageSimulation.Run(SkillCurve.ExpansionStage - 1, field, pair.With);
                var without = StageSimulation.Run(SkillCurve.ExpansionStage - 1, field, pair.Without);

                for (int i = 0; i < with.Count; i++)
                {
                    Assert.AreEqual(without[i].BossMargin, with[i].BossMargin, 0d, string.Format(
                        "{0} st{1}: 49단계가 조율 구간을 움직였다 (여유 {2:R} -> {3:R})",
                        pair.Name, with[i].Stage, without[i].BossMargin, with[i].BossMargin));

                    Assert.AreEqual(without[i].MobSeconds + without[i].BossKillSeconds,
                                    with[i].MobSeconds + with[i].BossKillSeconds, 0d,
                        pair.Name + " st" + with[i].Stage + ": 총 시간이 움직였다");

                    Assert.LessOrEqual(with[i].SkillSlots, SkillCurve.BaseSlots,
                        pair.Name + " st" + with[i].Stage + ": 게이트 앞인데 자리가 넷이다");
                    Assert.AreEqual(without[i].SkillSlots, with[i].SkillSlots,
                        pair.Name + " st" + with[i].Stage + ": 신규 오의가 자리를 열었다");
                }
            }
        }

        // ---------------------------------------------------------------- 죽은 버튼

        /**
         * @brief 4번 자리가 실제로 진행을 움직이는가. **자를 둘 쓴다.**
         *
         * 47단계가 정한 규칙이다 - 여유가 20배인 세계에서 DPS 한 겹은 보스전
         * 시간을 거의 안 줄이고(이미 즉살) 파밍은 SpawnPacing 0.4초 하한에
         * 묶여 있어서, 시간 비율만 보면 어떤 축이든 작아 보인다. 그래서
         * **도달층**을 두 번째 자로 함께 본다.
         *
         * 무과금 쪽을 기준으로 삼는다. 4번 자리는 진행으로 열리는 것이고
         * (보석으로도 현금으로도 못 산다), 그러므로 이 축이 누구를 위한
         * 것인지가 곧 무과금 플레이어다. 실측도 그쪽이 크다 - 가속 세계는
         * 이미 여유가 커서 같은 DPS가 시간을 덜 줄인다.
         */
        [Test]
        public void TheFourthSlot_IsNotADeadButton()
        {
            var field = Field();

            var with = StageSimulation.Run(400, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true });
            var without = StageSimulation.Run(400, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipSkillSlot = true });

            double gain = TotalSeconds(without, SkillCurve.ExpansionStage, 400)
                          / TotalSeconds(with, SkillCurve.ExpansionStage, 400) - 1d;

            Assert.Greater(gain, 0.03d, string.Format(
                "4번 자리를 안 쓴 무과금이 {0:P2}밖에 안 느리다 - 자리를 하나 열어 준 것이 "
                + "화면에만 있고 진행에는 없다", gain));

            // 두 번째 자: 같은 시간 예산으로 몇 스테이지까지 가는가.
            // 걷기는 52단계에 StageSimulation.ReachedStage로 승격됐다
            double budget = StageSimulation.CombatSeconds(with, 1, 400);
            int reach = StageSimulation.ReachedStage(without, budget, 1);

            Assert.LessOrEqual(reach, 390, string.Format(
                "같은 시간에 4번 자리 없이도 st{0}까지 간다 - 도달층으로도 차이가 없다", reach));
        }

        /**
         * @brief **안 끼운 오의에는 골드가 한 푼도 안 든다.**
         *
         * 슬롯의 정의를 구매 정책 쪽에서 다시 재는 것이다. 안 끼운 오의가
         * 레벨업을 받으면 그것은 나가지도 않는 것에 골드를 태우는 일이고,
         * 곡선 추종 플레이어(=기대 곡선)가 그러면 밴드의 근거가 무너진다.
         */
        [Test]
        public void UnequippedSkills_NeverCostGold()
        {
            var rows = StageSimulation.Run(200, Field());

            foreach (var row in rows)
            {
                if (row.SkillEquipped == null) continue;

                for (int i = 0; i < SkillCatalog.Count && i < row.SkillLevels.Length; i++)
                {
                    if (SkillCatalog.IsEquipped(row.SkillEquipped, i)) continue;

                    Assert.AreEqual(1, row.SkillLevels[i], string.Format(
                        "st{0}: 안 끼운 '{1}'이 Lv.{2}다 - 나가지도 않는 오의에 골드가 들었다",
                        row.Stage, SkillCatalog.Skills[i].DisplayName, row.SkillLevels[i]));
                }
            }
        }

        // ---------------------------------------------------------------- f2p 바닥

        /**
         * @brief **가챠 몫 오의가 진행 해금 오의보다 세지 않다.**
         *
         * 이 스텝이 f2p 바닥을 지키는 방식이고, 값이 아니라 구조로 지킨다:
         * 진행 해금 셋은 각자 혼의 가족에 들어 상성을 받고(YodoSpec.
         * AffinityFamilyId), 가챠 몫 둘은 전 오의를 미는 혼(흑야·백면)만 받는다.
         *
         * 그래서 몰아주기 빌드의 4번 자리 답은 언제나 진행 해금 오의다.
         * **뽑기가 파는 것은 폭이지 파워가 아니다.**
         */
        [Test]
        public void GachaReservedSkills_AreNeverStrongerThanProgressionOnes()
        {
            var tiers = new[] { 10, 8, 6, 4 };

            string[] progression = { SkillCatalog.BloodWaveId, SkillCatalog.BloodFallId,
                                     SkillCatalog.BloodWheelId };
            string[] reserved = { SkillCatalog.BloodBurstId, SkillCatalog.BloodWhipId };

            double weakestProgression = double.MaxValue;
            foreach (var id in progression)
            {
                double factor = YodoAffinityCurve.FactorForSkill(SkillCatalog.IndexOf(id), tiers);
                if (factor < weakestProgression) weakestProgression = factor;
            }

            foreach (var id in reserved)
            {
                int index = SkillCatalog.IndexOf(id);
                double factor = YodoAffinityCurve.FactorForSkill(index, tiers);

                Assert.LessOrEqual(factor, weakestProgression, string.Format(
                    "가챠 몫 '{0}'의 상성이 x{1:F3}로 진행 해금 오의(최소 x{2:F3})보다 세다 - "
                    + "뽑기가 파워를 파는 순간 f2p 바닥이 약속이 아니라 광고가 된다",
                    SkillCatalog.Skills[index].DisplayName, factor, weakestProgression));
            }
        }

        /**
         * @brief 가족 상성이 **전담보다 얕다.**
         *
         * 처음에 같은 크기로 뒀다가 계약 구간의 오의 몫이 50.6%가 되어
         * 자동 공격을 뒤집었다(YodoAffinityCurve.MatchOf 주석). 얕은 곡선으로
         * 눌러 49.0%로 되돌린 것이 이 스텝의 마지막 조정이고, 그 부등호가
         * 뒤집히면 Affinity_DoesNotFlipTheAutoAttackWithinTheContract가 다시
         * 깨진다 - 그 전에 여기가 먼저 알려야 한다.
         */
        [Test]
        public void FamilyAffinity_IsShallowerThanTheDedicatedOne()
        {
            var lantern = YodoCatalog.Find(YodoCatalog.LanternId);

            Assert.AreEqual(YodoAffinityCurve.Match.Primary,
                YodoAffinityCurve.MatchOf(lantern, SkillCatalog.OniCleaveId));
            Assert.AreEqual(YodoAffinityCurve.Match.Family,
                YodoAffinityCurve.MatchOf(lantern, SkillCatalog.BloodWaveId));
            Assert.AreEqual(YodoAffinityCurve.Match.None,
                YodoAffinityCurve.MatchOf(lantern, SkillCatalog.BloodBurstId));

            int index = YodoCatalog.IndexOf(YodoCatalog.LanternId);
            for (int tier = 1; tier <= YodoCurve.MaxTier; tier++)
            {
                double primary = YodoAffinityCurve.ValueAt(index, tier, 0,
                    YodoAffinityCurve.Match.Primary);
                double family = YodoAffinityCurve.ValueAt(index, tier, 0,
                    YodoAffinityCurve.Match.Family);

                Assert.Greater(primary, family, string.Format(
                    "티어 {0}: 가족 상성 x{1:F3}이 전담 x{2:F3}보다 얕지 않다", tier, family, primary));
            }

            // 그래도 값이 있어야 빌드다. 1이면 가족이라는 개념이 화면에서 사라진다
            Assert.Greater(YodoAffinityCurve.ValueAt(index, YodoCurve.MaxTier, 0,
                YodoAffinityCurve.Match.Family), 1.2d,
                "가족 상성이 사실상 1이다 - 몰아주기가 4번 자리의 답을 못 정한다");
        }

        /**
         * @brief 혼마다 가족이 하나씩이고, **한 오의가 두 혼에 들지 않는다.**
         *
         * 45단계가 거부한 방향이 그것이다 - 두 혼이 같은 오의를 물면 그
         * 오의만 두 배로 자라고 나머지 혼을 고를 이유가 사라진다.
         */
        [Test]
        public void EachSoulOwnsItsFamilyAlone()
        {
            var seen = new System.Collections.Generic.Dictionary<string, string>();

            foreach (var blade in YodoCatalog.Blades)
            {
                foreach (var id in new[] { blade.AffinitySkillId, blade.AffinityFamilyId })
                {
                    if (string.IsNullOrEmpty(id)) continue;

                    Assert.GreaterOrEqual(SkillCatalog.IndexOf(id), 0,
                        blade.BladeName + "의 상성 오의 '" + id + "'가 표에 없다");

                    Assert.IsFalse(seen.ContainsKey(id), string.Format(
                        "'{0}'을 {1}과 {2}가 함께 문다 - 그 오의만 두 배로 자란다",
                        id, seen.ContainsKey(id) ? seen[id] : "", blade.BladeName));

                    seen[id] = blade.BladeName;
                }
            }

            // 가챠 몫 둘은 전담 혼이 없다. 결함이 아니라 값이다 - 위 검사 참고
            Assert.IsFalse(seen.ContainsKey(SkillCatalog.BloodBurstId));
            Assert.IsFalse(seen.ContainsKey(SkillCatalog.BloodWhipId));
        }
    }
}
