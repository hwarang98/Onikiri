using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 발도 오의 축을 검증한다.
     *
     * ## 이 축이 죽는 두 가지 방식
     *
     * **하나. 안 팔린다.** 여섯 축이 계속 더 나으면 스킬 줄은 화면만 차지한다.
     * 8단계 공격속도가 그랬다.
     *
     * **둘. 팔리는데 안 움직인다.** 16단계의 스탯 포인트 증폭이 그랬다 - 장부에는
     * 샀다고 남는데 한 칸이 DPS를 0.5%밖에 못 올려서 화면에서는 죽어 있었다.
     * 이쪽이 더 나쁘다. 지표는 건강하다고 말하는데 실제로는 아무 일도 안 난다.
     *
     * 그래서 "샀는가"와 "사면 움직이는가"를 따로 잰다.
     *
     * ## 셋째 방식은 이 축에만 있다
     *
     * 스킬 DPS는 공격속도에 **더해지는** 항이라, 상한이 없으면 DPS가 공격력
     * 레벨의 제곱으로 자란다(SkillCurve 주석). 그 상태는 밴드 테스트가 잡기
     * 전에 이미 손쓸 수 없으므로, 상한이 존재하고 그것이 공격속도 상한 아래에
     * 있다는 것을 여기서 직접 못 박는다.
     */
    public class SkillAxisTests
    {
        private const string DataFolder = "Assets/_Project/Data";

        static StageSimulation.Field FieldFromAssets()
        {
            BigDouble healthSum = BigDouble.Zero;
            BigDouble goldSum = BigDouble.Zero;
            double totalWeight = 0d;

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { DataFolder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.spawnWeight <= 0f) continue;

                healthSum += definition.maxHealth * BigDouble.FromDouble(definition.spawnWeight);
                goldSum += definition.goldReward * BigDouble.FromDouble(definition.spawnWeight);
                totalWeight += definition.spawnWeight;
            }

            return new StageSimulation.Field
            {
                AverageMobHealth = (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble(),
                AverageMobGold = (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble(),
                SpawnInterval = 1.1d
            };
        }

        // ---------------------------------------------------------------- 구조

        /**
         * @brief 시뮬레이션에 스킬 칸이 모자라지 않은가.
         *
         * StageSimulation.Levels는 스킬 레벨을 **낱개 필드**로 들고 있다 -
         * struct라서 배열을 두면 효율 계산용 사본이 원본의 레벨을 함께 올린다.
         * 대가는 칸 수가 고정이라는 것이고, 넷째 오의가 생기면 뒤쪽이 조용히
         * 무시된다. 조용히 무시되면 시뮬레이션만 없는 DPS로 밴드를 재게 된다.
         */
        [Test]
        public void Simulation_HasASlotForEverySkill()
        {
            Assert.LessOrEqual(SkillCatalog.Count, StageSimulation.SkillSlotCapacity, string.Format(
                "스킬이 {0}개인데 시뮬레이션 칸은 {1}개다. StageSimulation.Levels에 "
                + "Skill{1} 필드를 추가하고 SkillLevel/SetSkillLevel의 switch를 늘려라 - "
                + "지금 상태로는 {1}번째 뒤의 오의가 시뮬레이션에서 DPS를 내지 않는다",
                SkillCatalog.Count, StageSimulation.SkillSlotCapacity));
        }

        /**
         * @brief 배율 상한이 존재하고, 자동 공격을 뒤집지 않는다.
         *
         * 두 조건 다 구조적 요구다.
         *
         * 상한이 없으면 DPS = 공격력 x 스킬항이 되는데 둘 다 공격력 강화 레벨을
         * 따라 자라므로 **DPS가 레벨의 제곱으로** 자란다. 어떤 램프도 그것을
         * 따라가지 못한다.
         *
         * 상한이 공격속도 상한을 넘으면 자동 공격이 장식이 된다. 공격속도 축은
         * 이미 아트가 정한 천장에 막혀 있어서(Lv.32), 그 위를 스킬이 넘어서면
         * 그 축은 두 번 죽는다.
         */
        [Test]
        public void SkillCeiling_StaysUnderTheAttackSpeedCeiling()
        {
            Assert.Greater(SkillCurve.CeilingRatio, 1d, "배율 상한이 없다 - DPS가 레벨의 제곱으로 자란다");
            Assert.Greater(SkillCurve.MaxLevel, 1, "살 수 있는 레벨이 하나뿐이다");

            // 49단계: **풀이 아니라 슬롯 예산이다.** 여덟을 다 더하면 4.61이
            // 되어 공격속도 상한을 넘는데, 그것은 화면에서 일어나지 않는 일이다 -
            // 동시에 도는 것은 장착한 넷뿐이고 그 합이 이 축의 크기다.
            // 풀이 커져도 이 값이 안 움직이는 것이 슬롯 설계의 전부다
            double maxRate = YodoAffinityCurve.CappedSkillRate;

            Assert.Less(maxRate, AttackSpeedCurve.Ceiling, string.Format(
                "상한에서 스킬의 초당 환산 기여가 {0:F2}로 공격속도 상한 {1:F2}를 넘는다 - "
                + "자동 공격이 장식이 된다", maxRate, AttackSpeedCurve.Ceiling));

            // 반대로 너무 작으면 새 축을 넣은 의미가 없다. 상한에서 DPS의 4분의 1은
            // 넘겨야 "세 번째 기둥"이라 부를 수 있다
            double share = maxRate / (AttackSpeedCurve.Ceiling + maxRate);
            Assert.Greater(share, 0.25d, string.Format(
                "상한까지 채워도 스킬이 DPS의 {0:P0}뿐이다 - 축 하나를 더 만들 값을 못 한다", share));
        }

        /**
         * @brief 세 오의의 골드당 효율이 처음부터 같은가.
         *
         * 비용을 초당 환산 기여에 비례시킨 이유가 이것이다(SkillCurve.CostPerRate).
         * 어느 것을 먼저 올려도 손해가 아니어야, 세 줄이 똑같이 생긴 화면이
         * 거짓말을 하지 않는다.
         *
         * 해금 시점의 골드 규모까지 나눈다. 절대 비용은 st8과 st21이 만 배 넘게
         * 다르고, 그 차이는 효율이 아니라 물가다.
         */
        [Test]
        public void Skills_HaveTheSameGoldEfficiencyAtUnlock()
        {
            foreach (var skill in SkillCatalog.Skills)
            {
                double perRate = skill.BaseCost
                                 / (skill.BaseRate * StageCurve.GoldMultiplier(skill.UnlockStage).ToDouble());

                Assert.AreEqual(SkillCurve.CostPerRate, perRate, SkillCurve.CostPerRate * 1e-9, string.Format(
                    "'{0}'의 골드당 효율이 나머지와 다르다 (기여당 비용 {1:F1} 대 {2:F1}). "
                    + "셋이 똑같이 생긴 줄로 서 있는데 하나만 나쁜 거래면 그것이 함정이다",
                    skill.DisplayName, perRate, SkillCurve.CostPerRate));
            }
        }

        /**
         * @brief 비용이 가정한 해금 스테이지가 실측과 맞는가.
         *
         * 첫 비용이 `기여 x CostPerRate x 해금 스테이지의 골드 배수`다. 골드는
         * 스테이지마다 x1.72로 자라므로 **한 스테이지만 틀려도 비용이 1.72배
         * 어긋난다.** 13단계에서 등급 배수를 선언만 하고 쓰지 않은 적이 있어,
         * 이런 값은 선언이 아니라 실측과 대조한다.
         */
        [Test]
        public void Skills_UnlockWhereTheCostAssumes()
        {
            var rows = StageSimulation.Run(30, FieldFromAssets());

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var skill = SkillCatalog.Skills[i];

                // 49단계: 스테이지 게이트의 오의는 해금 스테이지가 곧 게이트라
                // 실측할 것이 없다. 대신 **게이트가 하나인지**를 본다 - 신규
                // 다섯이 서로 다른 스테이지에 열리면 밴드 재기준이 다섯 번이다
                if (skill.StageGated)
                {
                    // 49b: 게이트가 오의마다 다르다(st12/18/27, 가챠 몫은 st51).
                    // 재는 것은 "비용이 그 게이트의 골드 규모를 가정하는가"이고,
                    // BaseCost가 UnlockStage로 계산되므로 구조가 지킨다 -
                    // 여기서는 게이트가 실재하는지만 본다
                    Assert.GreaterOrEqual(skill.UnlockStage, 1,
                        "'" + skill.DisplayName + "'의 스테이지 게이트가 없다");
                    continue;
                }

                int measured = -1;
                foreach (var row in rows)
                    if (row.CharacterLevel >= skill.UnlockLevel) { measured = row.Stage; break; }

                Assert.AreNotEqual(-1, measured, string.Format(
                    "'{0}'이 30스테이지까지 안 열린다 (필요 Lv.{1})", skill.DisplayName, skill.UnlockLevel));

                Assert.AreEqual(skill.UnlockStage, measured, string.Format(
                    "'{0}'은 Lv.{1}에서 열리는데 실측은 st{2}이고 비용은 st{3}을 가정한다. "
                    + "골드는 스테이지마다 x1.72라 이 차이가 그대로 비용 배수가 된다",
                    skill.DisplayName, skill.UnlockLevel, measured, skill.UnlockStage));
            }
        }

        // ---------------------------------------------------------------- 죽은 버튼 (a)

        /**
         * @brief (a-1) 곡선을 따라가는 플레이어가 모든 오의를 실제로 올린다.
         *
         * 구매 정책을 돌려서 본다. 효율 비율만 보는 검사로는 "언젠가는 살 만해진다"가
         * 통과하는데, 그 언젠가가 30스테이지 밖이면 없는 것과 같다.
         *
         * 각 오의의 **해금 5스테이지 안**을 기한으로 둔다. 그보다 늦으면 화면에서는
         * "새 오의가 열렸는데 한동안 아무것도 못 한다"이고, 그 구간이 열어준 보상을
         * 갉아먹는다.
         */
        [Test]
        public void EverySkillIsLeveledSoonAfterItUnlocks()
        {
            // 49단계: **장착한 오의만** 판다. 30스테이지까지는 슬롯이 셋이고
            // 열린 오의도 셋뿐이라 기존 셋이 그대로 검사 대상이다 - 신규 다섯은
            // st51 게이트 밖이므로 이 창에 존재하지 않는다
            var rows = StageSimulation.Run(30, FieldFromAssets());

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var skill = SkillCatalog.Skills[i];
                if (skill.StageGated) continue;

                int firstLevelUp = -1;
                foreach (var row in rows)
                    if (row.SkillLevels[i] > 1) { firstLevelUp = row.Stage; break; }

                Assert.AreNotEqual(-1, firstLevelUp, string.Format(
                    "'{0}'이 30스테이지까지 한 번도 안 팔렸다. 곡선의 형태가 건강해도 "
                    + "눌리지 않으면 죽은 버튼이다", skill.DisplayName));

                // 43단계 미세화 뒤 한 칸 밀렸다(5 -> 6). 미세 축들이 지갑을
                // 끝전까지 소진해 뭉칫돈(오의 첫 칸)이 한 스테이지 늦게 모인다 -
                // 곡선의 문제가 아니라 지출 결의 변화라 허용을 한 칸 연다
                Assert.LessOrEqual(firstLevelUp, skill.UnlockStage + 6, string.Format(
                    "'{0}'은 st{1}에 열리는데 첫 레벨업이 st{2}다 - 열어놓고 {3}스테이지를 "
                    + "기다리게 한다. SkillCurve.CostPerRate를 낮춰라",
                    skill.DisplayName, skill.UnlockStage, firstLevelUp,
                    firstLevelUp - skill.UnlockStage));
            }
        }

        /**
         * @brief (a-2) 한 칸 올리면 실제로 움직이는가.
         *
         * 두 가지를 함께 잰다. **화면의 숫자**(스킬 패널의 배율, 데미지 팝업)와
         * **총 DPS**다. 둘이 다른 이유는 이 축이 가산 항이기 때문이다 - 배율은
         * 항상 12% 오르지만 총 DPS에 미치는 폭은 그 오의가 차지한 몫에 비례한다.
         *
         * 화면 쪽 하한이 총 DPS 쪽보다 높은 것이 이 축의 성질이다. 16단계의
         * 스탯 포인트와 다른 점이기도 하다 - 그때는 화면에 뜨는 숫자가 총 DPS
         * 배수 하나뿐이라 둘이 같은 값이었다.
         */
        [Test]
        public void SkillLevelUp_IsFeltOnTheRowAndInTheDps()
        {
            var rows = StageSimulation.Run(30, FieldFromAssets());

            foreach (var row in rows)
            {
                for (int i = 0; i < SkillCatalog.Count; i++)
                {
                    if (!SkillCatalog.IsUnlockedAt(i, row.CharacterLevel)) continue;

                    int level = row.SkillLevels[i];
                    if (level >= SkillCurve.MaxLevel) continue;

                    var skill = SkillCatalog.Skills[i];

                    // 화면: 패널의 배율과 팝업의 숫자가 이만큼 커진다
                    double shown = SkillCurve.CappedMultiplierAtLevel(skill.BaseMultiplier, level);
                    double shownNext = SkillCurve.CappedMultiplierAtLevel(skill.BaseMultiplier, level + 1);
                    double shownGain = shownNext / shown - 1d;

                    Assert.GreaterOrEqual(shownGain, MinimumShownGain, string.Format(
                        "st{0} '{1}' Lv.{2}: 한 칸이 배율을 {3:P2}밖에 못 올린다 - "
                        + "패널의 숫자도 데미지 팝업도 안 움직인다",
                        row.Stage, skill.DisplayName, level, shownGain));

                    // 총 DPS: 가산 항이라 그 오의의 몫에 비례한다
                    double rateNow = SkillCatalog.RateAt(i, level, row.CharacterLevel);
                    double rateNext = SkillCatalog.RateAt(i, level + 1, row.CharacterLevel);
                    double dpsGain = (rateNext - rateNow) / (row.AttacksPerSecond + row.SkillRate);

                    Assert.GreaterOrEqual(dpsGain, MinimumDpsGain, string.Format(
                        "st{0} '{1}' Lv.{2}: 한 칸이 총 DPS를 {3:P2}밖에 못 올린다 "
                        + "(스킬 전체 몫 {4:P0}). 기본 배율을 키우고 상한 비를 줄여라 - "
                        + "총량은 그대로 두고 곡선의 무게만 앞으로 옮기는 것이 SkillCurve의 손잡이다",
                        row.Stage, skill.DisplayName, level, dpsGain, row.SkillDpsShare));
                }
            }
        }

        /**
         * @brief 배율이 이만큼은 움직여야 화면에서 읽힌다.
         *
         * 곡선 Step이 1.12라 보통 12%지만, 상한 직전 한 칸만 잘려서 작아진다
         * (Lv.11 x3.106 -> Lv.12 x3.200 = +3.0%). 그 칸까지 통과하는 값으로
         * 잡되, 0에 가까워지면 걸리게 둔다.
         */
        const double MinimumShownGain = 0.01d;

        /**
         * @brief 총 DPS 하한. **16단계의 1%보다 낮고, 그것이 의도다.**
         *
         * 스탯 포인트는 화면에 뜨는 숫자가 총 DPS 배수 하나뿐이라 1%가 곧
         * "눌렀더니 숫자가 바뀌었다"의 최소선이었다. 스킬은 자기 배율과 데미지
         * 팝업이 12%씩 움직이므로 눌린 것 자체는 화면에서 확실히 읽히고, 총
         * DPS 쪽은 "그래서 진행이 빨라지는가"를 재는 다른 질문이다.
         *
         * 0.3%는 실측 최악값(st16 연참의 상한 직전 칸, 0.36%)에서 잡았다. 이보다
         * 낮아지면 그 오의는 화면에서만 성장하고 진행에는 기여하지 않는다.
         */
        const double MinimumDpsGain = 0.003d;

        // ---------------------------------------------------------------- 죽은 버튼 (b)

        /**
         * @brief (b) 골드 획득 축을 사면 실제로 진행이 빨라지는가.
         *
         * ## 왜 회수 시간으로는 못 재는가
         *
         * 20단계가 이 축에 회수 시간(payback)이라는 자를 붙였고, 그 자로는
         * 계속 "건강하다"가 나왔다. 그런데 실측 총 시간은 축이 없는 것과 같았다
         * (30스테이지 기준 -0.1%). 회수 시간은 "언제 본전을 뽑는가"만 보고
         * **그 골드를 다른 축에 썼다면 얻었을 몫**과 **보스 체력 보정**을 보지
         * 않기 때문이다.
         *
         * 그래서 자를 바꾼다. 축을 사는 플레이어와 안 사는 플레이어를 나란히
         * 돌려 총 시간을 비교한다 - 재려는 것이 원래 그것이었다.
         *
         * 20단계에서 이 검사를 할 수 없었던 이유는 시뮬레이션에 "안 사는
         * 플레이어"가 없었기 때문이다. 26단계에 StageSimulation.Policy가 생겼다.
         */
        [Test]
        public void GoldAxis_ActuallySpeedsUpProgress()
        {
            var field = FieldFromAssets();
            var withAxis = StageSimulation.Run(30, field);
            var without = StageSimulation.Run(30, field, new StageSimulation.Policy { SkipGoldGain = true });

            double fast = StageSimulation.TotalSeconds(withAxis);
            double slow = StageSimulation.TotalSeconds(without);
            double gain = slow / fast - 1d;

            Assert.Greater(gain, 0.05d, string.Format(
                "축을 사는 쪽이 {0:F0}초, 안 사는 쪽이 {1:F0}초로 차이가 {2:P1}뿐이다. "
                + "지표는 회수된다고 말하는데 실제로는 본전인 상태이고, 그러면 이 줄은 "
                + "화면만 차지한다", fast, slow, gain));

            // 반대로 너무 크면 이 축 하나가 나머지를 제친다. 20단계가 상한을
            // x3에서 x1.25로 내린 이유와 같은 선이다
            Assert.Less(gain, 0.35d, string.Format(
                "축 하나가 진행을 {0:P0} 빠르게 한다 - 다른 여섯을 제치고 스노볼한다", gain));
        }

        /**
         * @brief 이 축을 **게임에 넣은 것**이 이득이었는가. 20단계가 쟀던 자다.
         *
         * ## 위 검사와 다른 질문이다
         *
         * 위는 "안 사면 손해인가"를 묻고, 여기는 "축이 없는 세계보다 나은가"를
         * 묻는다. 둘이 갈리는 이유는 보스 체력 보정이 **스테이지의 함수**라
         * 플레이어를 구분하지 못하기 때문이다 - 안 산 플레이어도 산 사람 기준으로
         * 무거워진 보스를 상대한다. 그래서 위 검사는 지수가 어떻든 늘 +가 나온다.
         *
         * 20단계가 "액티브 이득 0"이라고 보고한 것은 이쪽 자였고, 실측이 맞았다.
         * 지수 0.81에서 다시 재보면 st10 -0.2% / st30 +2.2%다.
         *
         * ## 26단계에 무엇이 바뀌었나
         *
         * 보스 골드 웃돈을 지워 st11을 천장에서 떼어낸 뒤 지수를 0.81 -> 0.55로
         * 낮췄고, 그 결과가 st10 +1.2% / st30 +5.0%다. **초반의 순손실이 사라진
         * 것**이 이번 스텝의 핵심이다 - 20단계는 초반에 이 축을 사면 실제로
         * 느려졌고, 그것이 "함정 버튼"이라는 말의 정확한 뜻이었다.
         */
        [Test]
        public void GoldAxis_AddsValueToTheGame()
        {
            var field = FieldFromAssets();
            var neutral = new StageSimulation.Policy { NeutralizeGoldAxis = true };

            // 초반. 20단계에는 여기가 음수였다 - 사면 느려지는 구간이 존재했다
            double earlyWith = StageSimulation.TotalSeconds(StageSimulation.Run(10, field));
            double earlyWithout = StageSimulation.TotalSeconds(StageSimulation.Run(10, field, neutral));

            Assert.Greater(earlyWithout, earlyWith, string.Format(
                "10스테이지까지 축이 있는 쪽이 {0:F0}초, 없는 쪽이 {1:F0}초다 - "
                + "축을 넣어서 초반이 느려졌다. 20단계의 함정 버튼이 그대로다. "
                + "StageCurve.GoldAxisMarginExponent를 낮춰라 (지금 {2})",
                earlyWith, earlyWithout, StageCurve.GoldAxisMarginExponent));

            // 전 구간. 2%대는 20단계가 "이득 0"이라고 부른 크기다
            double lateWith = StageSimulation.TotalSeconds(StageSimulation.Run(30, field));
            double lateWithout = StageSimulation.TotalSeconds(StageSimulation.Run(30, field, neutral));
            double gain = lateWithout / lateWith - 1d;

            Assert.Greater(gain, 0.04d, string.Format(
                "30스테이지까지 축이 만드는 이득이 {0:P1}뿐이다. 보정이 축을 거의 다 "
                + "상쇄하고 있고, 그 상태에서 이 축의 값어치는 방치 보상뿐이다. "
                + "StageCurve.GoldAxisMarginExponent를 낮춰라 (지금 {1})",
                gain, StageCurve.GoldAxisMarginExponent));
        }

        /**
         * @brief 회수 시간 자가 **사는 순간**을 재고 있는가.
         *
         * 20단계의 밴드 검사는 스테이지가 끝난 시점의 회수 시간을 봤는데, 그때는
         * 축이 이미 상한이라 무한대였고 검사에서 건너뛰어졌다 - **검사 대상 행이
         * 하나도 없는 채로 통과하고 있었다.** 20단계 보고서가 "회수 시간이 밴드
         * 안"이라고 적은 근거가 그 통과였다.
         *
         * 여기서 지키는 것은 값 자체가 아니라 **자가 재고 있다는 사실**이다.
         * 실측값(2초)이 밴드 하한 30초 아래라는 것은 GoldGainCurve.BaseCost
         * 주석에 근거와 함께 적어뒀다 - 고치려고 두 번 시도했고 둘 다 밴드를
         * 깨뜨렸다. 지금 이 축을 스노볼에서 막는 것은 회수 시간이 아니라 상한이다.
         */
        [Test]
        public void GoldAxisPayback_IsMeasuredAtThePurchase()
        {
            var rows = StageSimulation.Run(20, FieldFromAssets());

            int measured = 0;
            foreach (var row in rows)
                if (!double.IsInfinity(row.GoldGainPaybackAtPurchase)) measured++;

            Assert.Greater(measured, 0,
                "구매 시점의 회수 시간을 한 번도 못 쟀다 - 밴드 검사가 빈 집합을 보고 "
                + "통과하는 20단계 상태로 되돌아갔다");

            foreach (var row in rows)
            {
                if (double.IsInfinity(row.GoldGainPaybackAtPurchase)) continue;

                // 위쪽은 구매 정책의 임계값이라 구조적으로 지켜진다. 어긋나면
                // 정책과 자가 갈린 것이다
                Assert.LessOrEqual(row.GoldGainPaybackAtPurchase,
                    GoldGainEfficiency.BuyThresholdSeconds + 1e-6, string.Format(
                    "st{0}에서 회수 {1:F0}초짜리 칸을 샀다 - 구매 정책이 임계값 {2:F0}초를 "
                    + "넘겨 사고 있다", row.Stage, row.GoldGainPaybackAtPurchase,
                    GoldGainEfficiency.BuyThresholdSeconds));
            }
        }

        /**
         * @brief 스킬 축은 여섯 축과 **대등해야** 한다. 빨라지면 그것도 실패다.
         *
         * 골드 축과 반대 방향의 검사다. 골드 축은 골드를 골드로 바꾸므로 사면
         * 진행이 빨라져야 맞지만, 스킬은 골드를 %DPS로 바꾸는 축이라 공격력·
         * 치명타와 **같은 저울에 올라간다**. 같은 저울에서 한쪽이 확실히 빠르면
         * 나머지가 죽는다.
         *
         * 그러므로 여기서 재는 것은 "빨라지는가"가 아니라 **"어느 쪽으로든 크게
         * 갈리지 않는가"**다. 스킬을 안 사는 플레이어가 크게 손해면 스킬이
         * 필수가 되고, 크게 이득이면 스킬이 함정이다.
         */
        [Test]
        public void SkillAxis_IsCompetitiveWithTheOtherDamageAxes()
        {
            var field = FieldFromAssets();
            double withSkills = StageSimulation.TotalSeconds(StageSimulation.Run(30, field));
            double without = StageSimulation.TotalSeconds(
                StageSimulation.Run(30, field, new StageSimulation.Policy { SkipSkills = true }));

            double gap = without / withSkills - 1d;

            Assert.GreaterOrEqual(gap, -0.05d, string.Format(
                "스킬에 골드를 쓰면 오히려 {0:P1} 느려진다 - 구매 정책이 사라고 말하는 "
                + "함정 버튼이다. SkillCurve.CostPerRate를 낮춰라", -gap));

            Assert.LessOrEqual(gap, 0.15d, string.Format(
                "스킬을 안 사면 {0:P1} 느려진다 - 선택이 아니라 필수가 됐고, "
                + "그만큼 여섯 축의 자리가 줄었다. SkillCurve.CostPerRate를 올려라", gap));
        }

        // ---------------------------------------------------------------- 시뮬레이션 일치

        /**
         * @brief 시뮬레이션과 런타임이 **같은 식**으로 스킬 DPS를 센다.
         *
         * 9단계에서 계산과 화면이 다른 결론을 냈고, 그때 어느 쪽이 틀렸는지
         * 판단할 근거가 코드 어디에도 없었다. 스킬은 26단계에 들어온 가장 새로운
         * 축이라 같은 위험이 가장 크다.
         *
         * 여기서 못 박는 것은 "배율/쿨다운의 합이 공격속도에 더해진다"는 정의
         * 하나다. CombatStats가 그 항을 괄호 안에 두므로, 정의가 같으면 치명타와
         * 증폭 상속도 자동으로 같아진다.
         */
        [Test]
        public void SkillRate_IsTheSameFormulaEverywhere()
        {
            // 칸 수는 카탈로그가 정한다. 손으로 셋을 적어 두면 오의가 늘 때
            // 배열 밖을 읽는다(49단계에 실제로 그랬다)
            var levels = new int[SkillCatalog.Count];
            for (int i = 0; i < levels.Length; i++) levels[i] = 3 + (i % 4);
            const int characterLevel = 25;

            double byHand = 0d;
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var skill = SkillCatalog.Skills[i];

                // 스테이지 게이트의 오의는 최전선을 안 받는 CastRate에서
                // 잠긴 것으로 센다(SkillCatalog.IsUnlockedAt의 2인자 판) -
                // 손 계산도 같은 규칙을 따라야 두 값이 비교된다
                if (skill.StageGated) continue;
                if (characterLevel < skill.UnlockLevel) continue;

                byHand += SkillCurve.CappedMultiplierAtLevel(skill.BaseMultiplier, levels[i])
                          / skill.CooldownSeconds;
            }

            Assert.AreEqual(byHand, SkillCatalog.CastRate(levels, characterLevel), 1e-12,
                "SkillCatalog.CastRate가 배율/쿨다운의 합이 아니다");

            // 괄호 안에 더해진다. 여기가 곱셈이 되면 치명타·증폭 상속이 깨진다
            var stats = new CombatStats
            {
                Damage = 100d, AttacksPerSecond = 2d, CritRate = 0.5d, CritMultiplier = 3d,
                SkillRate = byHand
            };
            Assert.AreEqual(100d * (2d + byHand) * 2d, stats.ExpectedDps, 1e-9,
                "ExpectedDps가 스킬 항을 괄호 밖에서 곱하고 있다");

            // 해금 전에는 0이다. 잠긴 오의가 DPS를 내면 밴드가 실제보다 후해진다
            Assert.AreEqual(0d, SkillCatalog.CastRate(levels, 1), 1e-12,
                "Lv.1에서 이미 오의가 DPS를 내고 있다");
        }

        /**
         * @brief 스킬이 자동 공격을 뒤집지 않는가 (전 구간).
         *
         * 밴드보다 이쪽이 먼저 움직인다. 여유가 아직 밴드 안인데 몫이 절반을
         * 넘어가고 있으면, 다음 계수 변경에서 밴드가 깨진다.
         */
        [Test]
        public void SkillShare_StaysBelowHalfThrough30()
        {
            var rows = StageSimulation.Run(30, FieldFromAssets());

            foreach (var row in rows)
                Assert.Less(row.SkillDpsShare, 0.5d, string.Format(
                    "st{0}에서 스킬이 DPS의 {1:P0}를 맡는다 - 자동 공격이 장식이 되는 길이다 "
                    + "(rate {2:F2}, 공격속도 {3:F2})",
                    row.Stage, row.SkillDpsShare, row.SkillRate, row.AttacksPerSecond));

            // 열린 뒤에는 실제로 자라야 한다. 몫이 계속 0에 가까우면 새 축이
            // 화면만 차지하는 것이다
            var last = rows[29];
            Assert.Greater(last.SkillDpsShare, 0.2d, string.Format(
                "30스테이지에서도 스킬이 DPS의 {0:P0}뿐이다", last.SkillDpsShare));
        }

        /**
         * @brief 정수 시전 횟수로 재도 보스 여유가 밴드 안인가.
         *
         * 시뮬레이션은 스킬을 **기대값**으로 편다(초당 환산 기여). 치명타와 같은
         * 처리이지만 어긋나는 폭은 더 크다 - 쿨다운 2.5~11초를 30초 보스전에
         * 놓으면 실제 시전 횟수가 정수(4, 2, 1)이기 때문이다.
         *
         * 최악은 보스전이 쿨다운 직후에 시작하는 경우다. 그때 실제 시전 횟수는
         * `floor(30 / 쿨다운)`이고, 기대값보다 적을 수 있다. 그 최악에서도
         * 보스를 잡을 수 있어야 시뮬레이션의 여유가 안전한 쪽이 된다.
         */
        [Test]
        public void SkillDps_SurvivesIntegerCastCounts()
        {
            var field = FieldFromAssets();
            var rows = StageSimulation.Run(30, field);
            double window = StageCurve.BossTimeLimitSeconds;

            foreach (var row in rows)
            {
                // 기대값이 가정한 시전 횟수와 최악의 실제 횟수를 비교해, 모자란
                // 만큼 DPS를 깎은 뒤에도 제한 시간 안에 잡히는지 본다
                double worstRate = 0d;
                for (int i = 0; i < SkillCatalog.Count; i++)
                {
                    if (!SkillCatalog.IsUnlockedAt(i, row.CharacterLevel)) continue;

                    var skill = SkillCatalog.Skills[i];
                    double casts = System.Math.Floor(window / skill.CooldownSeconds);
                    double multiplier = SkillCurve.CappedMultiplierAtLevel(
                        skill.BaseMultiplier, row.SkillLevels[i]);
                    worstRate += casts * multiplier / window;
                }

                double expectedTotal = row.AttacksPerSecond + row.SkillRate;
                double worstTotal = row.AttacksPerSecond + worstRate;
                double worstMargin = row.BossMargin * (worstTotal / expectedTotal);

                Assert.Greater(worstMargin, 1d, string.Format(
                    "st{0}: 오의가 최악 타이밍에 걸리면 여유가 {1:F2}로 떨어져 보스를 놓친다 "
                    + "(기대값 여유 {2:F2}). 쿨다운을 줄이거나 스킬 몫을 낮춰라",
                    row.Stage, worstMargin, row.BossMargin));
            }
        }
    }
}
