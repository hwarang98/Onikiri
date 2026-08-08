using System;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 장비(대장간) 축. **곡선·비용·보석 관문·죽은 버튼**을 검사한다.
     *
     * 밴드 검사는 StageSimulationTests에 있다. 여기는 축 자체의 성질을 본다 -
     * 밴드는 여러 축이 함께 만드는 결과이고, 이 파일이 지키는 것은 그 결과에
     * 들어가기 전의 전제들이다.
     */
    public class EquipmentTests
    {
        private const string DataFolder = "Assets/_Project/Data";

        static StageSimulation.Field FieldFromAssets()
        {
            BigDouble healthSum = BigDouble.Zero, goldSum = BigDouble.Zero;
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

        static EquipmentSpec Weapon { get { return EquipmentCatalog.Find(EquipmentCatalog.WeaponId); } }
        static EquipmentSpec Armor { get { return EquipmentCatalog.Find(EquipmentCatalog.ArmorId); } }

        // ------------------------------------------------------------ 곡선의 모양

        /**
         * @brief 1등급 Lv.1의 배수는 **정확히 1배**다.
         *
         * 이 성질 하나가 두 가지를 동시에 지킨다.
         *
         *   마이그레이션 무해   v8 세이브가 스탯 변화 없이 올라온다
         *   무장비 게이트 보존  st2/st5 게이트가 장비와 무관하게 그대로다
         *
         * GoldGainCurve.BaseValue가 1인 것과 같은 이유이고, 여기가 깨지면
         * 두 사실이 **동시에** 조용히 틀어진다.
         */
        [Test]
        public void GradeOneLevelOne_IsExactlyNeutral()
        {
            foreach (var slot in EquipmentCatalog.Slots)
                Assert.AreEqual(1d, EquipmentCurve.ValueAt(slot.GradeStep, slot.TemperStep, 1, 1), 1e-12d,
                    "'" + slot.SlotName + "' 시작 배수가 1배가 아니다");
        }

        /**
         * @brief 한 칸이 **1% 이상** 움직인다. 16단계가 그은 선이다.
         *
         * 죽은 버튼의 정의가 이것이다 - 눌러도 숫자가 안 움직이면 그 버튼은
         * 없는 것과 같다(StageSimulationTests.MinimumFeltGain).
         */
        [Test]
        public void EverySingleStep_IsFelt()
        {
            const double Floor = 0.01d;

            foreach (var slot in EquipmentCatalog.Slots)
            {
                Assert.GreaterOrEqual(slot.TemperStep - 1d, Floor, string.Format(
                    "'{0}' 단련 한 칸이 {1:P2}뿐이다 - 눌러도 숫자가 안 움직인다",
                    slot.SlotName, slot.TemperStep - 1d));

                Assert.GreaterOrEqual(slot.GradeStep - 1d, Floor, string.Format(
                    "'{0}' 등급 한 칸이 {1:P2}뿐이다", slot.SlotName, slot.GradeStep - 1d));
            }
        }

        /**
         * @brief 등급업이 **단련보다 확실히 크다.** 두 재화를 가르는 근거다.
         *
         * 비슷해지면 보석이 "네 칸 더 살 권리"로만 남고 도약이 사라진다.
         * E-3이 요구한 "보석 = 등급 게이트(도약) / 골드 = 점진"이 수치에서도
         * 성립해야 한다.
         */
        [Test]
        public void GradeUp_IsALeapNotAnotherStep()
        {
            foreach (var slot in EquipmentCatalog.Slots)
            {
                double ratio = (slot.GradeStep - 1d) / (slot.TemperStep - 1d);
                Assert.GreaterOrEqual(ratio, 3d, string.Format(
                    "'{0}' 등급업이 단련의 {1:F1}배뿐이다 - 도약이 아니라 또 하나의 칸이다",
                    slot.SlotName, ratio));
            }
        }

        /**
         * @brief 등급업은 **단련을 끝까지 올린 뒤에만** 열린다.
         *
         * 순서가 없으면 보석을 모아둔 플레이어가 골드를 한 푼도 쓰지 않고
         * 5등급까지 뛴다. 그러면 이 축의 실제 크기가 "며칠 접속했는가"에
         * 좌우되고, 시뮬레이션이 잴 수 없는 변수가 밴드 한가운데로 들어온다.
         */
        [Test]
        public void GradeGate_RequiresAFullTemper()
        {
            for (int grade = 1; grade < EquipmentCurve.GradeCount; grade++)
            {
                int cap = EquipmentCurve.MaxLevelForGrade(grade);

                for (int level = 1; level < cap; level++)
                {
                    Assert.IsTrue(EquipmentCurve.CanTemper(grade, level),
                        grade + "등급 Lv." + level + ": 아직 단련할 칸이 남아야 한다");
                    Assert.IsFalse(EquipmentCurve.CanUpgradeGrade(grade, level),
                        grade + "등급 Lv." + level + ": 단련이 남았는데 등급업이 열렸다");
                }

                Assert.IsFalse(EquipmentCurve.CanTemper(grade, cap),
                    grade + "등급 Lv." + cap + ": 상한을 넘어 단련이 열렸다");
                Assert.IsTrue(EquipmentCurve.CanUpgradeGrade(grade, cap),
                    grade + "등급 Lv." + cap + ": 다 단련했는데 등급업이 안 열린다");
            }

            // 마지막 등급 마지막 칸은 둘 다 닫혀야 한다. 열려 있으면 화면에
            // 눌리는데 아무 일도 안 하는 버튼이 남는다
            int last = EquipmentCurve.MaxLevelForGrade(EquipmentCurve.GradeCount);
            Assert.IsFalse(EquipmentCurve.CanTemper(EquipmentCurve.GradeCount, last));
            Assert.IsFalse(EquipmentCurve.CanUpgradeGrade(EquipmentCurve.GradeCount, last));
        }

        /**
         * @brief 등급업에서 **약해지지 않는다.**
         *
         * 단련 레벨을 등급업에서 1로 되돌리는 설계였다면 계수에 따라 배수가
         * 내려갈 수 있었다. 레벨을 이어 두는 것이 그 상태를 없앤다 - 값이
         * 언제나 정확히 GradeStep 배로 오른다.
         */
        [Test]
        public void Progression_IsMonotone()
        {
            foreach (var slot in EquipmentCatalog.Slots)
            {
                double previous = 0d;

                // 단련 레벨은 등급을 넘어 **이어진다.** g등급이 담는 것은
                // 1..g*칸수가 아니라 (g-1)*칸수+1 .. g*칸수다
                for (int grade = 1; grade <= EquipmentCurve.GradeCount; grade++)
                {
                    int first = (grade - 1) * EquipmentCurve.LevelsPerGrade + 1;

                    for (int level = first; level <= EquipmentCurve.MaxLevelForGrade(grade); level++)
                    {
                        double value = EquipmentCurve.ValueAt(slot.GradeStep, slot.TemperStep, grade, level);
                        Assert.Greater(value, previous, string.Format(
                            "'{0}' {1}등급 Lv.{2}에서 배수가 내려갔다 ({3:F4} -> {4:F4})",
                            slot.SlotName, grade, level, previous, value));
                        previous = value;
                    }
                }

                Assert.AreEqual(slot.Ceiling, previous, previous * 1e-9d,
                    "'" + slot.SlotName + "' 마지막 칸이 상한과 다르다");
            }
        }

        /**
         * @brief 무기 상한이 **방어구보다 작다.** 밴드가 그렇게 시켰다.
         *
         * 대칭이 아닌 것이 의도다. 무기는 보스 여유 밴드에 천장이 있고 방어구는
         * 없다(EquipmentCatalog 주석). 이 부등호가 뒤집히면 st30 피날레가
         * 천장을 넘는다 - 실제로 x2.0 / x1.70에서 두 번 넘었다.
         */
        [Test]
        public void WeaponCeiling_IsSmallerThanArmorCeiling()
        {
            Assert.Less(Weapon.Ceiling, Armor.Ceiling, string.Format(
                "무기 x{0:F2} >= 방어구 x{1:F2} - 피날레 천장이 무기를 감당하지 못한다",
                Weapon.Ceiling, Armor.Ceiling));

            // 크기 자체도 못 박는다. 여기가 움직이면 보고서의 밴드가 함께 움직인다
            Assert.AreEqual(1.305d, Weapon.Ceiling, 0.01d);
            Assert.AreEqual(1.994d, Armor.Ceiling, 0.01d);
        }

        // ------------------------------------------------------------ 온보딩 / 게이트

        /**
         * @brief 온보딩(1~5)에 장비가 **존재하지 않는다.**
         *
         * 계수로 지키는 것이 아니라 구조로 지킨다 - 해금이 st11이므로 1~10에는
         * 살 수 있는 칸 자체가 없다. E-1이 "온보딩 침범 금지"라고 못 박은 것이
         * 이 검사다.
         */
        [Test]
        public void Equipment_IsAbsentFromOnboarding()
        {
            Assert.Greater(EquipmentCurve.UnlockStage, 5,
                "장비가 온보딩(1~5) 안에서 열린다");
            Assert.AreEqual(BossCurve.RegionLength + 1, EquipmentCurve.UnlockStage,
                "해금이 지역 1 돌파 직후가 아니다 - 대장간은 지역 1의 랜드마크다");

            var rows = StageSimulation.Run(10, FieldFromAssets());
            foreach (var row in rows)
            {
                Assert.AreEqual(1, row.WeaponGrade, "stage " + row.Stage + "에 무기 등급이 올랐다");
                Assert.AreEqual(1, row.WeaponLevel, "stage " + row.Stage + "에 무기를 단련했다");
                Assert.AreEqual(1, row.ArmorGrade, "stage " + row.Stage + "에 방어구 등급이 올랐다");
                Assert.AreEqual(1, row.ArmorLevel, "stage " + row.Stage + "에 방어구를 단련했다");
            }
        }

        /**
         * @brief 무장비 게이트가 그대로다. **화력 st2 / 체력 st5.**
         *
         * 두 게이트는 시작 스탯으로 재는데(StageSimulation.StartingStats), 장비
         * 1등급 Lv.1이 1배라 그 값이 바뀌지 않는다. 그래도 검사하는 이유는
         * 언젠가 시작 배수를 1이 아닌 값으로 두고 싶어지는 날이 오기 때문이다.
         */
        [Test]
        public void UnequippedGates_AreUnchanged()
        {
            var field = FieldFromAssets();

            Assert.AreEqual(2, StageSimulation.FirstStageThatBlocksAnUnupgradedPlayer(
                field.AverageMobHealth, 50), "화력 게이트가 움직였다");
            Assert.AreEqual(5, StageSimulation.FirstStageThatKillsAnUnupgradedPlayer(50),
                "체력 게이트가 움직였다");
        }

        // ------------------------------------------------------------ 비용

        /**
         * @brief 단련 첫 칸이 경쟁 축과 **비슷한 골드당 효율**에서 시작한다.
         *
         * EquipmentCatalog.GoldPerPercent는 해금 스테이지의 절대 골드로 잰
         * 값인데, 그 시점의 경쟁 축 레벨은 곡선을 손볼 때마다 움직인다.
         * 상수를 적어두고 잊으면 축이 죽거나(비싸서) 스노볼한다(싸서).
         *
         * SkillSpec.UnlockStage를 실측과 대조하는 것과 같은 처리다 - 상수의
         * 존재는 연결의 증거가 아니다.
         *
         * **범위가 넓다(1/8 ~ 8배).** 정확한 패리티가 목표가 아니기 때문이다 -
         * 방어구는 일부러 싸게 뒀고(그러지 않으면 한 칸도 안 팔린다) 무기는
         * 패리티 근처다. 이 검사가 잡는 것은 자릿수가 틀어지는 사고이고,
         * 실제로 한 번 잡았다(스테이지 배수를 두 번 곱해 300배가 됐다).
         */
        [Test]
        public void TemperCost_StartsAtParityWithTheCompetingAxis()
        {
            var rows = StageSimulation.Run(EquipmentCurve.UnlockStage, FieldFromAssets());
            var atUnlock = rows[rows.Count - 1];

            // 무기: 공격력 강화와 비교한다. 둘 다 %DPS다
            double powerCost = AttackPowerCurve.CostAtLevel(atUnlock.AttackPowerLevel);
            double powerGoldPerPercent = powerCost / ((AttackPowerCurve.Step - 1d) * 100d);
            double weaponGoldPerPercent = Weapon.TemperBaseCost / ((Weapon.TemperStep - 1d) * 100d);

            AssertWithinFactor(weaponGoldPerPercent, powerGoldPerPercent, 8d, string.Format(
                "무기 첫 칸 {0:F0}골드/% 대 공격력 강화 Lv.{1} {2:F0}골드/%",
                weaponGoldPerPercent, atUnlock.AttackPowerLevel, powerGoldPerPercent));

            // 방어구: 체력 강화와 비교한다. 둘 다 %EHP다
            double healthCost = HealthCurve.CostAtLevel(atUnlock.HealthLevel);
            double healthGoldPerPercent = healthCost / ((HealthCurve.Step - 1d) * 100d);
            double armorGoldPerPercent = Armor.TemperBaseCost / ((Armor.TemperStep - 1d) * 100d);

            AssertWithinFactor(armorGoldPerPercent, healthGoldPerPercent, 8d, string.Format(
                "방어구 첫 칸 {0:F1}골드/% 대 체력 강화 Lv.{1} {2:F1}골드/%",
                armorGoldPerPercent, atUnlock.HealthLevel, healthGoldPerPercent));
        }

        static void AssertWithinFactor(double actual, double reference, double factor, string message)
        {
            Assert.Greater(actual, reference / factor, message + " - 너무 싸다");
            Assert.Less(actual, reference * factor, message + " - 너무 비싸다");
        }

        /**
         * @brief 보정 곡선이 시뮬레이션 실측을 따라간다.
         *
         * StageCurve.EquipmentCompensation이 이 곡선을 읽어 보스를 무겁게 한다.
         * 어긋나면 축은 죽지 않고 **시점만 어긋난 채로 살아 있어서** 밴드만
         * 이상해진다 - 실제로 한 번 겪었다(절편을 1로 두어 st21~26이 천장을
         * 넘었다). GoldGainCurve.StagesToCeiling과 같은 처리다.
         */
        [Test]
        public void ExpectedCurve_TracksTheSimulation()
        {
            var rows = StageSimulation.Run(30, FieldFromAssets());

            foreach (var row in rows)
            {
                if (row.Stage < EquipmentCurve.UnlockStage) continue;

                int expected = EquipmentCurve.ExpectedLevelAtStage(row.Stage);

                Assert.LessOrEqual(Math.Abs(expected - row.WeaponLevel), 2, string.Format(
                    "stage {0}: 보정이 Lv.{1}을 가정하는데 실측은 Lv.{2}다. "
                    + "EquipmentCurve.LevelAtUnlock / LevelsPerStage 를 다시 재라",
                    row.Stage, expected, row.WeaponLevel));
            }

            // 해금 전에는 보정이 없어야 한다. 있으면 있지도 않은 이득을
            // 상쇄하는 셈이고, 21단계에 골드 축에서 정확히 그 사고가 있었다
            for (int stage = 1; stage < EquipmentCurve.UnlockStage; stage++)
                Assert.AreEqual(1d, StageCurve.EquipmentCompensation(stage), 1e-12d,
                    "stage " + stage + ": 장비가 없는데 보스가 무거워졌다");
        }

        // ------------------------------------------------------------ 보석

        /**
         * @brief 등급업이 **보석 소비처의 전부**이고, 값이 오른다.
         *
         * 31단계가 파킹으로 남긴 자리다. GemWallet 주석이 "이 사실이 깨지는 날
         * 시뮬레이션에 편입해야 한다"고 적어뒀고, 이 스텝이 그 날이다.
         */
        [Test]
        public void GemCosts_RiseWithGrade()
        {
            int previous = 0;
            for (int grade = 1; grade < EquipmentCurve.GradeCount; grade++)
            {
                int cost = EquipmentCurve.GradeGemCost(grade);
                Assert.Greater(cost, previous, grade + " -> " + (grade + 1) + " 등급업 보석이 안 올랐다");
                previous = cost;
            }

            // 마지막 등급 뒤에는 살 것이 없다
            Assert.AreEqual(0, EquipmentCurve.GradeGemCost(EquipmentCurve.GradeCount));
        }

        /**
         * @brief 보석이 **실제로 병목이다.** 죽은 재화도 무한 재화도 아니다.
         *
         * 두 방향에서 잡는다.
         *
         *   너무 비싸면  일일을 안 받는 플레이어가 st30까지 등급을 하나도 못 올린다
         *   너무 싸면    그 플레이어도 다 사버려서 매일 접속할 이유가 없다
         *
         * 실측으로 하한 플레이어는 벌이의 대부분을 쓰고도 5등급에 닿지 못한다.
         * 그 남은 거리가 곧 일일 퀘스트의 값어치다.
         */
        [Test]
        public void Gems_AreTheBindingConstraintNotDeadWeight()
        {
            var policy = new StageSimulation.Policy { GemsFromQuestsOnly = true };
            var rows = StageSimulation.Run(30, FieldFromAssets(), policy);
            var last = rows[rows.Count - 1];

            Assert.Greater(last.GemsSpent, 0,
                "일일을 한 번도 안 받은 플레이어가 등급을 하나도 못 올린다 - 보석이 너무 비싸다");

            Assert.Less(last.WeaponGrade, EquipmentCurve.GradeCount, string.Format(
                "일일 없이도 무기가 {0}등급에 닿는다 - 보석이 너무 싸서 매일 올 이유가 없다",
                last.WeaponGrade));

            // 벌이의 대부분을 쓴다. 남아돌면 그것도 병목이 아니다
            Assert.Greater(last.GemsSpent, last.GemsEarned * 0.5d, string.Format(
                "보석 {0}개를 벌어 {1}개만 썼다 - 소비처가 수입을 못 따라간다",
                last.GemsEarned, last.GemsSpent));
        }

        /**
         * @brief 시뮬레이션이 세는 보석이 **퀘스트 표에서 나온다.**
         *
         * 하한이 상수로 적혀 있으면 퀘스트 보상을 손보는 날 밴드가 조용히
         * 어긋난다. 업적 보석 총합을 표에서 직접 더해 대조한다.
         */
        [Test]
        public void GemIncome_ComesFromTheQuestTable()
        {
            var field = FieldFromAssets();

            int both = StageSimulation.Run(30, field, new StageSimulation.Policy())[29].GemsEarned;
            int repeatOnly = StageSimulation.Run(30, field,
                new StageSimulation.Policy { SkipAchievements = true })[29].GemsEarned;

            // 두 갈래가 **둘 다** 들어온다. 하나만이면 리텐션 축이 하나 빠진 것이다
            Assert.Greater(repeatOnly, 0, "반복 퀘스트 티어가 한 번도 안 열렸다");
            Assert.Greater(both - repeatOnly, 0, "업적 보석이 시뮬레이션에 안 들어왔다");

            // 업적 몫이 표의 총합을 넘지 않는다. 넘으면 두 번 세고 있는 것이다
            int achievementTotal = 0;
            foreach (var spec in QuestCatalog.Achievement) achievementTotal += spec.Gems;

            Assert.LessOrEqual(both - repeatOnly, achievementTotal, string.Format(
                "업적 보석을 {0}개 셌는데 표의 총합은 {1}개다 - 같은 업적을 두 번 받는다",
                both - repeatOnly, achievementTotal));

            // 그리고 대부분이 열린다. 절반도 안 열리면 st30까지의 목표가 너무 멀다
            Assert.Greater(both - repeatOnly, achievementTotal * 0.5d, string.Format(
                "30스테이지까지 업적 보석이 {0}/{1}개만 열린다", both - repeatOnly, achievementTotal));
        }
    }
}
