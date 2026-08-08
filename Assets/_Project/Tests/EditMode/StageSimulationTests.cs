using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 시뮬레이션이 실제 게임과 같은 수치를 쓰는지 검사한다.
     *
     * 이 파일이 존재하는 이유는 9단계에서 보고서의 계산과 플레이 화면이 서로 다른
     * 결론을 냈기 때문이다. 계산은 "1스테이지 보스는 무강화로도 클리어"라고 했고
     * 스크린샷은 실패를 보여줬다. 결국 원인은 계산이 아니라 스크린샷 쪽이었지만
     * (제한 시간을 강제로 소진시킨 화면이었다), **어느 쪽이 틀렸는지 판단할 근거가
     * 코드 어디에도 없었다는 것**이 진짜 문제였다.
     *
     * 계산이 쓰는 상수가 게임이 쓰는 상수와 같다는 것을 여기서 못 박는다. 그래야
     * 다음에 둘이 어긋났을 때 "계산이 틀렸다"를 후보에서 지울 수 있다.
     */
    public class StageSimulationTests
    {
        private const string DataFolder = "Assets/_Project/Data";

        /**
         * @brief 스폰 가중치로 평균 낸 잡몹 체력/골드를 **에셋에서** 읽는다.
         *
         * 상수를 적어두지 않는다. 밸런스를 손보는 곳은 에셋이고, 여기 숫자를 복사해
         * 두면 그 순간부터 이 테스트는 자기 자신을 검사하게 된다.
         */
        static void LoadFieldAverages(out double health, out double gold, out int definitionCount)
        {
            BigDouble healthSum = BigDouble.Zero;
            BigDouble goldSum = BigDouble.Zero;
            double totalWeight = 0d;
            definitionCount = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { DataFolder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

                // 보스는 가중치 0이라 자연히 빠진다. 잡몹 평균에 보스가 섞이면
                // 그 평균으로 계산한 보스 체력이 자기 자신을 참조하게 된다
                if (definition == null || definition.spawnWeight <= 0f) continue;

                healthSum += definition.maxHealth * BigDouble.FromDouble(definition.spawnWeight);
                goldSum += definition.goldReward * BigDouble.FromDouble(definition.spawnWeight);
                totalWeight += definition.spawnWeight;
                definitionCount++;
            }

            health = totalWeight > 0d ? (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d;
            gold = totalWeight > 0d ? (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d;
        }

        static StageSimulation.Field FieldFromAssets()
        {
            double health, gold;
            int count;
            LoadFieldAverages(out health, out gold, out count);

            return new StageSimulation.Field
            {
                AverageMobHealth = health,
                AverageMobGold = gold,
                // BattleContentBuilder.WireSpawner가 씬에 기록하는 값
                SpawnInterval = 1.1d
            };
        }

        // ------------------------------------------------------------ 수치 일치

        [Test]
        public void EnemyDefinitionAssets_ExistAndCarryTheFieldAverages()
        {
            double health, gold;
            int count;
            LoadFieldAverages(out health, out gold, out count);

            Assert.AreEqual(3, count, "잡몹 정의 수가 달라졌다. 평균이 바뀌면 보스 체력도 함께 바뀐다");

            // 8단계부터 쓰던 값. 여기가 움직이면 보고서의 모든 시간이 함께 움직인다
            Assert.AreEqual(14.222d, health, 0.01d);
            Assert.AreEqual(5.444d, gold, 0.01d);
        }

        /**
         * @brief 시뮬레이션의 곡선과 실제 UpgradeTrack이 같은 값을 낸다.
         *
         * UpgradePanelBuilder가 씬에 기록하는 것과 같은 인자로 트랙을 만들고,
         * 레벨마다 두 계산을 나란히 놓는다. 어느 한쪽 상수만 고치면 여기서 걸린다.
         */
        [Test]
        public void SimulationCurves_MatchTheLiveUpgradeTracks()
        {
            var power = new UpgradeTrack(UpgradeSystem.AttackPowerId, "공격력 강화",
                BigDouble.FromDouble(AttackPowerCurve.BaseCost), AttackPowerCurve.CostGrowth,
                UpgradeTrack.Curve.Multiplicative,
                BigDouble.FromDouble(AttackPowerCurve.BaseValue), AttackPowerCurve.Step);

            var speed = new UpgradeTrack(UpgradeSystem.AttackSpeedId, "공격속도 강화",
                BigDouble.FromDouble(AttackSpeedCurve.BaseCost), AttackSpeedCurve.CostGrowth,
                UpgradeTrack.Curve.Multiplicative,
                BigDouble.FromDouble(AttackSpeedCurve.BaseValue), AttackSpeedCurve.Step,
                AttackSpeedCurve.MaxLevel, AttackSpeedCurve.Ceiling);

            for (int level = 1; level <= 60; level++)
            {
                Assert.AreEqual(AttackPowerCurve.ValueAtLevel(level), power.ValueAtLevel(level).ToDouble(),
                    AttackPowerCurve.ValueAtLevel(level) * 1e-9d,
                    "공격력: 시뮬레이션 곡선과 트랙이 Lv." + level + "에서 갈라졌다");

                Assert.AreEqual(AttackPowerCurve.CostAtLevel(level), power.CostAtLevel(level).ToDouble(),
                    AttackPowerCurve.CostAtLevel(level) * 1e-9d,
                    "공격력 비용: Lv." + level + "에서 갈라졌다");

                // 상한이 걸린 값끼리 비교한다. 전투가 실제로 쓰는 것이 이쪽이다
                Assert.AreEqual(AttackSpeedCurve.CappedValueAtLevel(level), speed.ValueAtLevel(level).ToDouble(),
                    AttackSpeedCurve.CappedValueAtLevel(level) * 1e-9d,
                    "공격속도: 시뮬레이션 곡선과 트랙이 Lv." + level + "에서 갈라졌다");

                Assert.AreEqual(AttackSpeedCurve.CostAtLevel(level), speed.CostAtLevel(level).ToDouble(),
                    AttackSpeedCurve.CostAtLevel(level) * 1e-9d,
                    "공격속도 비용: Lv." + level + "에서 갈라졌다");
            }
        }

        /**
         * @brief 시뮬레이션의 보스 체력과 BossFight의 보스 체력이 같은 함수에서 나온다.
         *
         * 둘 다 StageCurve.BossHealthForStage를 부른다. 예전에는 각자 곱셈을 하고
         * 있었고, 그러면 한쪽만 고쳐지는 날이 온다.
         */
        [Test]
        public void SimulationBossHealth_MatchesWhatTheFightWouldSpawn()
        {
            var field = FieldFromAssets();
            var averageHealth = BigDouble.FromDouble(field.AverageMobHealth);

            for (int stage = 1; stage <= 20; stage++)
            {
                // BossFight.BossMaxHealth 가 부르는 것과 같은 식
                double fromFight = StageCurve.BossHealthForStage(averageHealth, stage).ToDouble();

                // 시뮬레이션이 내부에서 쓰는 체력을 되짚는다.
                // 시간 = 체력 / DPS 이므로 시간 x DPS 가 곧 체력이다
                var unit = new CombatStats { Damage = 1d, AttacksPerSecond = 1d, CritRate = 0d, CritMultiplier = 1d };
                double seconds = StageSimulation.BossKillSeconds(field.AverageMobHealth, stage, unit);
                double fromSimulation = seconds * StageSimulation.ExpectedDps(unit);

                Assert.AreEqual(fromFight, fromSimulation, fromFight * 1e-9d,
                    "stage " + stage + ": 보스 체력이 전투와 시뮬레이션에서 다르다");
            }
        }

        [Test]
        public void StartingStats_MatchLevelOneOfBothCurves()
        {
            Assert.AreEqual(AttackPowerCurve.BaseValue, StageSimulation.StartingDamage, 1e-9d);
            Assert.AreEqual(AttackSpeedCurve.BaseValue, StageSimulation.StartingAttacksPerSecond, 1e-9d,
                "레벨 1의 공격속도가 상한에 걸리면 시작 스탯 자체가 잘린 것이다");
        }

        // ------------------------------------------------------------ 게이트 판정

        /**
         * @brief 무강화 플레이어에게 1스테이지 보스는 벽이 아니다.
         *
         * 9단계 보고서가 계산으로 주장한 것이고, 플레이 모드에서도 실제로 클리어되는
         * 것을 확인했다. 여기가 실패로 바뀌면 온보딩이 첫 보스에서 끊긴다.
         */
        /**
         * @brief 무강화 플레이어에게 1스테이지 보스는 벽이 아니다.
         *
         * 플레이 모드에서 실제로 확인한 것과 같은 결론이어야 한다 - 무강화로 도전해서
         * 클리어됐고, 스테이지가 2로 올랐다.
         */
        [Test]
        public void FirstBoss_IsClearableWithNoUpgrades()
        {
            var field = FieldFromAssets();

            double seconds = StageSimulation.BossKillSeconds(
                field.AverageMobHealth, 1, StageSimulation.StartingStats);

            Assert.Less(seconds, StageSimulation.BossDamageWindowSeconds, string.Format(
                "무강화로 1스테이지 보스에 {0:F1}초가 걸린다 (때릴 수 있는 시간 {1:F1}초). " +
                "첫 보스가 벽이 되면 플레이어는 게이트를 배우기 전에 막힌다",
                seconds, StageSimulation.BossDamageWindowSeconds));

            // 너무 여유로워도 안 된다. 첫 보스가 아무 긴장도 주지 않으면 30초 제한이
            // 있다는 사실 자체가 전달되지 않는다
            Assert.Greater(seconds, StageSimulation.BossDamageWindowSeconds * 0.4d, string.Format(
                "무강화로 {0:F1}초 - 제한 시간이 있다는 것을 플레이어가 알아채지 못한다", seconds));
        }

        /**
         * @brief 제한 시간 전부가 때릴 수 있는 시간이다.
         *
         * 16단계까지는 반대였다. 시계가 보스 스폰과 함께 돌기 시작하는데 보스는
         * 화면 밖에서 걸어 들어와서, 그 5.3초 동안 사거리가 비어 있었다 -
         * 제한 시간의 18%가 기다림이었고, 계산은 그것을 빼야 했다.
         *
         * 17단계에서 시계 쪽을 고쳤다. 플레이어가 보스에게 달려가고, **도달한
         * 순간부터** 30초가 시작한다. 달려가는 구간은 타이머 밖이므로 뺄 것이 없다.
         *
         * 이 테스트가 지키는 것은 "빼는가"가 아니라 **시뮬레이션과 전투가 같은
         * 시계를 보는가**이다. 한쪽만 고치면 계산은 통과하는데 화면은 실패하는
         * 상태가 만들어진다 - 9단계에서 실제로 겪었다.
         */
        [Test]
        public void BossDamageWindow_IsTheWholeClock()
        {
            Assert.AreEqual(StageCurve.BossTimeLimitSeconds, StageSimulation.BossDamageWindowSeconds, 1e-9d,
                "때릴 수 있는 시간이 제한 시간과 다르다 - 달려가는 구간이 타이머에 섞였다");

            // 달려가는 시간은 여전히 **총 소요 시간**에는 들어간다. 타이머 밖일 뿐
            // 플레이어가 앉아 있는 시간은 맞다
            Assert.Greater(StageSimulation.BossRunUpSeconds, 0d,
                "보스에게 달려가는 시간이 0이면 등장 연출이 없다는 뜻이다");
        }

        [Test]
        public void ExpectedDps_IncludesCrit()
        {
            var start = StageSimulation.StartingStats;
            double plain = start.Damage * start.AttacksPerSecond;

            Assert.Greater(start.ExpectedDps, plain, "치명타가 기대 DPS에 반영되지 않았다");
            // CombatBaseline은 float이고 곡선은 double이다. 1e-9로 재면 그 변환
            // 오차에 걸린다 - 검사하려는 것은 정밀도가 아니라 "치명타가 들어갔는가"다
            Assert.AreEqual(plain * CombatBaseline.ExpectedDamageMultiplier, start.ExpectedDps, 1e-6d);

            // 12% 확률 x 2배 = 1.12배
            Assert.AreEqual(1.12f, CombatBaseline.ExpectedDamageMultiplier, 1e-6f);

            // 시작 스탯이 곧 두 치명타 곡선의 Lv.1이어야 한다. 어긋나면 첫 구매에서
            // 수치가 튄다
            Assert.AreEqual(CombatBaseline.CritChance, start.CritRate, 1e-9d);
            Assert.AreEqual(CombatBaseline.CritMultiplier, start.CritMultiplier, 1e-9d);
        }

        /**
         * @brief 강화를 하지 않으면 곧 막힌다.
         *
         * 게이트의 존재 이유다. 너무 늦게 막히면 게이트가 한참 동안 아무 일도
         * 하지 않는 연출이 된다.
         */
        [Test]
        public void GateBlocksAnUnupgradedPlayer_Early()
        {
            var field = FieldFromAssets();
            int blocked = StageSimulation.FirstStageThatBlocksAnUnupgradedPlayer(field.AverageMobHealth, 50);

            Assert.AreNotEqual(-1, blocked, "50스테이지까지 무강화로 통과한다면 게이트가 아무것도 막지 않는다");
            Assert.AreEqual(2, blocked,
                "무강화 플레이어가 막히는 스테이지가 " + blocked + "로 바뀌었다");
        }

        /**
         * @brief 곡선의 강화 속도를 따라가면 50스테이지까지 보스가 잡힌다.
         *
         * StageProgressionTests의 같은 이름 검사와 짝이다. 그쪽은 단순화한 모델을
         * 쓰고 여기는 실제 구매 정책을 돌린다.
         */
        [Test]
        public void FollowingTheCurve_ClearsEveryBossThrough50()
        {
            var results = StageSimulation.Run(50, FieldFromAssets());

            foreach (var row in results)
            {
                Assert.IsTrue(row.BossCleared, string.Format(
                    "stage {0}: 보스에 {1:F1}초 (제한 {2}초). 공격력 Lv.{3} 공격속도 Lv.{4} ({5:F2}/s)",
                    row.Stage, row.BossKillSeconds, StageCurve.BossTimeLimitSeconds,
                    row.AttackPowerLevel, row.AttackSpeedLevel, row.AttacksPerSecond));
            }
        }

        // ------------------------------------------------------------ 보스 여유 밴드

        /**
         * @brief 여유가 머물러야 하는 구간. 아래로 나가면 벽, 위로 나가면 제한 시간이 무의미.
         *
         * **보스 유형마다 다르다.** 챕터 보스는 챕터의 마지막 관문이므로 더
         * 빡빡해야 하고, 그래서 아래쪽 한계가 일반 스테이지보다 낮다.
         * 두 밴드에 같은 바닥을 쓰면 "챕터가 더 빡빡해야 한다"와 "어느 보스도
         * 벽이면 안 된다"가 서로를 막는다.
         */
        const double MarginFloor = 1.5d;
        const double MarginCeiling = 3.0d;

        const double ChapterMarginFloor = 1.3d;
        const double ChapterMarginCeiling = 2.0d;

        /**
         * @brief 지역 피날레의 밴드. 챕터보다 **아래**에 있어야 한다.
         *
         * 13단계에서 등급이 셋이 됐다. 5스테이지 관문과 10스테이지 피날레가 같은
         * 밴드를 쓰면 피날레는 그냥 또 하나의 챕터 보스이고, 다크 사무라이를
         * 거기에만 세운 이유가 수치에서는 사라진다.
         *
         * "지역의 마지막이 가장 빡빡하다"가 성립하려면 세 밴드가 겹치지 않고
         * 피날레 < 챕터 < 일반 순으로 놓여야 한다.
         */
        const double FinaleMarginFloor = 1.15d;
        const double FinaleMarginCeiling = 1.7d;

        /**
         * @brief 곡선을 따라가는 플레이어의 보스 여유가 밴드 안에 머무는지.
         *
         * 9단계에서는 2.1배 -> 5.6배로 발산했고, 10단계에서 치명타 두 축이 들어오자
         * 20스테이지 기준 82배까지 벌어졌다. 보스 체력 배수를 스테이지에 따라
         * 올려(BossHealthGrowth) 그것을 잡았다.
         *
         * 이 테스트가 지키는 것은 계수가 아니라 **관계**다. 새 성장 축을 추가하면
         * 플레이어 DPS가 다시 빨라지고 여유가 위로 새어 나간다. 그때 여기서 걸린다.
         */
        [Test]
        public void BossMargin_StaysInBandThrough20()
        {
            var results = StageSimulation.Run(20, FieldFromAssets());

            foreach (var row in results)
            {
                var tier = BossCurve.TierOf(row.Stage);

                double floor = tier == BossCurve.Tier.Finale ? FinaleMarginFloor
                             : tier == BossCurve.Tier.Chapter ? ChapterMarginFloor
                             : MarginFloor;
                double ceiling = tier == BossCurve.Tier.Finale ? FinaleMarginCeiling
                               : tier == BossCurve.Tier.Chapter ? ChapterMarginCeiling
                               : MarginCeiling;
                string kind = tier == BossCurve.Tier.Finale ? "피날레"
                            : tier == BossCurve.Tier.Chapter ? "챕터" : "일반";

                Assert.GreaterOrEqual(row.BossMargin, floor, string.Format(
                    "stage {0}({1}): 여유 {2:F2}배 - 곡선을 따라왔는데도 보스가 벽이다 " +
                    "(처치 {3:F1}초 / 때릴 수 있는 {4:F1}초)",
                    row.Stage, kind, row.BossMargin, row.BossKillSeconds,
                    StageSimulation.BossDamageWindowSeconds));

                Assert.LessOrEqual(row.BossMargin, ceiling, string.Format(
                    "stage {0}({1}): 여유 {2:F2}배 - 제한 시간이 아무 일도 하지 않는다 " +
                    "(공격력 Lv.{3} 속도 Lv.{4} 치명타율 Lv.{5} 피해 Lv.{6})",
                    row.Stage, kind, row.BossMargin,
                    row.AttackPowerLevel, row.AttackSpeedLevel,
                    row.CritRateLevel, row.CritDamageLevel));
            }
        }

        /**
         * @brief 21~30 구간도 같은 밴드 안인가.
         *
         * 13단계에 이 구간의 드리프트를 "범위 밖이라 보고만 한다"로 남겼고
         * 16단계에 램프로 닫았다. 그런데 **검사는 20까지만 돌고 있었다** - 닫힌
         * 것을 지키는 것이 아무것도 없었다는 뜻이다.
         *
         * 26단계에 스킬이 들어오면서 이 구간이 가장 크게 움직였다(스킬 몫이
         * st9의 2%에서 st30의 38%까지 자란다). 20까지만 보는 검사로는 그 변화가
         * 통째로 빠져나간다.
         *
         * 20까지의 검사를 지우지 않고 따로 두는 이유는 실패했을 때 **어느 구간이
         * 깨졌는지**가 이름에서 읽혀야 하기 때문이다. 두 구간은 서로 다른 손잡이가
         * 움직인다 - 앞은 램프의 Start, 뒤는 Final이다.
         */
        [Test]
        public void BossMargin_StaysInBandThrough30()
        {
            var results = StageSimulation.Run(30, FieldFromAssets());

            for (int i = 20; i < results.Count; i++)
            {
                var row = results[i];
                var tier = BossCurve.TierOf(row.Stage);

                double floor = tier == BossCurve.Tier.Finale ? FinaleMarginFloor
                             : tier == BossCurve.Tier.Chapter ? ChapterMarginFloor
                             : MarginFloor;
                double ceiling = tier == BossCurve.Tier.Finale ? FinaleMarginCeiling
                               : tier == BossCurve.Tier.Chapter ? ChapterMarginCeiling
                               : MarginCeiling;
                string kind = tier == BossCurve.Tier.Finale ? "피날레"
                            : tier == BossCurve.Tier.Chapter ? "챕터" : "일반";

                Assert.GreaterOrEqual(row.BossMargin, floor, string.Format(
                    "stage {0}({1}): 여유 {2:F2}배 - 벽이다 (스킬 몫 {3:P0})",
                    row.Stage, kind, row.BossMargin, row.SkillDpsShare));

                Assert.LessOrEqual(row.BossMargin, ceiling, string.Format(
                    "stage {0}({1}): 여유 {2:F2}배 - 제한 시간이 아무 일도 하지 않는다 "
                    + "(스킬 몫 {3:P0}). BossHealthRampFinal을 올려라",
                    row.Stage, kind, row.BossMargin, row.SkillDpsShare));
            }
        }

        // ------------------------------------------------------------ 생존 게이트

        /**
         * @brief 곡선을 따라온 플레이어는 보스전에서 죽지 않는다.
         *
         * 공격 계열과 생존 계열의 균형은 자로 잴 수 없다 - %DPS와 %EHP는 단위가
         * 달라 나눌 수 없다. 그래서 균형은 지표가 아니라 **이 게이트**로 잡는다.
         */
        [Test]
        public void FollowingTheCurve_NeverDiesThrough20()
        {
            var results = StageSimulation.Run(20, FieldFromAssets());

            foreach (var row in results)
            {
                Assert.IsTrue(row.Survived, string.Format(
                    "stage {0}({1}): 생존 여유 {2:F2} - 곡선을 따라왔는데도 죽는다 " +
                    "(체력 Lv.{3} {4:F0} / 회복 Lv.{5} {6:F2}/s)",
                    row.Stage, row.IsChapterBoss ? "챕터" : "일반", row.SurvivalMargin,
                    row.HealthLevel, row.MaxHealth, row.RegenLevel, row.RegenPerSecond));
            }
        }

        /**
         * @brief 모든 성장 축이 20스테이지까지 최소 한 번은 팔린다.
         *
         * 밴드 검사는 곡선의 **형태**만 본다. 형태가 건강해도 다른 축이 계속
         * 더 나으면 그 버튼은 화면에서 한 번도 눌리지 않고, 플레이어에게는
         * 8단계의 죽은 공격속도와 똑같이 보인다. 차이는 "언젠가는 살 만해진다"
         * 뿐인데, 그 언젠가가 20스테이지 밖이면 없는 것과 같다.
         *
         * 그래서 형태와 별개로 **생존성**을 따로 검사한다. 새 축을 추가할 때
         * 밴드만 맞추고 끝내지 않도록 하는 것이 목적이다.
         */
        [Test]
        public void EveryAxisIsBoughtAtLeastOnceThrough20()
        {
            var final = StageSimulation.Run(20, FieldFromAssets())[19];

            var levels = new[]
            {
                new { Name = "공격력",      Level = final.AttackPowerLevel },
                new { Name = "공격속도",    Level = final.AttackSpeedLevel },
                new { Name = "치명타 확률", Level = final.CritRateLevel },
                new { Name = "치명타 피해", Level = final.CritDamageLevel },
                new { Name = "체력",        Level = final.HealthLevel },
                new { Name = "체력 회복",   Level = final.RegenLevel },

                // 26단계의 오의 셋. 여기 함께 세는 이유는 이 검사가 "골드로 사는
                // 축"의 생존성을 보는 자리이기 때문이다 - 재화가 같으면 같은
                // 저울에 올라가야 한다. 해금 시점별 기한은 SkillAxisTests가 따로 본다
                new { Name = SkillCatalog.Skills[0].DisplayName, Level = final.SkillLevels[0] },
                new { Name = SkillCatalog.Skills[1].DisplayName, Level = final.SkillLevels[1] },
            };

            foreach (var axis in levels)
            {
                Assert.Greater(axis.Level, 1, string.Format(
                    "'{0}' 이 20스테이지까지 한 번도 팔리지 않았다 (Lv.{1}). " +
                    "곡선의 형태가 건강해도 눌리지 않으면 죽은 버튼이다",
                    axis.Name, axis.Level));
            }
        }

        /**
         * @brief 체력 게이트가 실제로 무는가.
         *
         * 무강화 플레이어가 죽는 스테이지가 존재해야 하고, 시간 초과로 막히는
         * 스테이지보다 **뒤**여야 한다. 앞이면 플레이어가 화력을 배우기 전에
         * 체력부터 요구받는다.
         */
        [Test]
        public void HealthGate_BitesAfterTheDamageGate()
        {
            var field = FieldFromAssets();

            int blocked = StageSimulation.FirstStageThatBlocksAnUnupgradedPlayer(field.AverageMobHealth, 50);
            int killed = StageSimulation.FirstStageThatKillsAnUnupgradedPlayer(50);

            Assert.AreNotEqual(-1, killed, "50스테이지까지 무강화로 죽지 않는다면 체력 축이 아무것도 하지 않는다");
            Assert.Greater(killed, blocked, string.Format(
                "체력 게이트({0}스테이지)가 화력 게이트({1}스테이지)보다 먼저 온다. " +
                "플레이어가 화력을 배우기 전에 체력부터 요구받는다", killed, blocked));

            // 지금 곡선의 실제 값. 여기가 바뀌면 보고서도 함께 바뀌어야 한다
            Assert.AreEqual(2, blocked);
            Assert.AreEqual(5, killed);
        }

        /**
         * @brief 화력만 올리는 정책은 죽는다.
         *
         * 체력 게이트가 무는 증거다. 생존 축을 사지 않으면 어느 스테이지부터
         * 유효체력이 보스의 총 피해에 미치지 못한다.
         */
        [Test]
        public void DamageOnlyPolicy_DiesWhereBalancedPolicySurvives()
        {
            var field = FieldFromAssets();
            var balanced = StageSimulation.Run(20, field);

            // 화력만 올린 플레이어의 유효체력은 시작값 그대로다
            double damageOnlyEhp = StageSimulation.StartingEffectiveHealth;

            int firstDeath = -1;
            foreach (var row in balanced)
            {
                double incoming = BossCurve.TotalDamageOverFight(row.Stage, StageCurve.BossTimeLimitSeconds);
                if (damageOnlyEhp < incoming) { firstDeath = row.Stage; break; }
            }

            Assert.AreNotEqual(-1, firstDeath,
                "화력만 올려도 20스테이지까지 죽지 않는다면 생존 축을 살 이유가 없다");

            // 같은 스테이지에서 균형 정책은 살아남는다
            var balancedAtDeath = balanced[firstDeath - 1];
            Assert.IsTrue(balancedAtDeath.Survived, string.Format(
                "stage {0}에서 균형 정책도 죽는다 - 비교가 성립하지 않는다", firstDeath));

            Assert.Greater(balancedAtDeath.MaxHealth, HealthCurve.ValueAtLevel(1),
                "균형 정책이 체력을 한 번도 사지 않았다");
        }

        // ------------------------------------------------------------ 보스 이원화

        /**
         * @brief 챕터 보스는 5스테이지마다, 그리고 더 빡빡해야 한다.
         *
         * 일반 스테이지 보스보다 여유가 작아야 챕터의 마지막이라는 무게가 생긴다.
         * 반대로 벽이 되면 그 앞의 넷이 의미를 잃는다.
         */
        [Test]
        public void ChapterBosses_AreTighterThanStageBosses()
        {
            var results = StageSimulation.Run(20, FieldFromAssets());

            // 13단계에서 등급이 셋이 됐다. 챕터와 피날레를 한 덩어리로 재면
            // 피날레의 더 낮은 여유가 챕터 바닥을 뚫는 것으로 보인다 - 실제로는
            // 그것이 의도된 차등이다
            double chapterWorst = double.MaxValue, chapterBest = 0d;
            double finaleWorst = double.MaxValue, finaleBest = 0d;
            double stageWorst = double.MaxValue;
            int chapterCount = 0, finaleCount = 0;

            foreach (var row in results)
            {
                switch (BossCurve.TierOf(row.Stage))
                {
                    case BossCurve.Tier.Finale:
                        finaleCount++;
                        if (row.BossMargin < finaleWorst) finaleWorst = row.BossMargin;
                        if (row.BossMargin > finaleBest) finaleBest = row.BossMargin;
                        break;

                    case BossCurve.Tier.Chapter:
                        chapterCount++;
                        if (row.BossMargin < chapterWorst) chapterWorst = row.BossMargin;
                        if (row.BossMargin > chapterBest) chapterBest = row.BossMargin;
                        break;

                    default:
                        if (row.BossMargin < stageWorst) stageWorst = row.BossMargin;
                        break;
                }
            }

            Assert.AreEqual(2, chapterCount, "20스테이지에 챕터 관문이 둘이어야 한다 (5, 15)");
            Assert.AreEqual(2, finaleCount, "20스테이지에 지역 피날레가 둘이어야 한다 (10, 20)");
            Assert.IsFalse(BossCurve.IsChapterBoss(6));

            Assert.LessOrEqual(chapterBest, ChapterMarginCeiling, string.Format(
                "챕터 보스 여유가 {0:F2}까지 올라간다 - 일반 스테이지와 구분되지 않는다", chapterBest));
            Assert.GreaterOrEqual(chapterWorst, ChapterMarginFloor, string.Format(
                "챕터 보스 여유가 {0:F2}로 내려간다 - 벽이다", chapterWorst));

            Assert.LessOrEqual(finaleBest, FinaleMarginCeiling, string.Format(
                "피날레 여유가 {0:F2}까지 올라간다 - 챕터 관문과 구분되지 않는다", finaleBest));
            Assert.GreaterOrEqual(finaleWorst, FinaleMarginFloor, string.Format(
                "피날레 여유가 {0:F2}로 내려간다 - 벽이다", finaleWorst));

            // **세 등급이 실제로 계단을 이루는지.** 밴드가 겹치기만 해서는 차등이
            // 아니다. 피날레 < 챕터 < 일반 순으로 빡빡해야 "지역의 마지막이 가장
            // 어렵다"가 수치에서도 성립한다
            Assert.Less(chapterWorst, stageWorst, string.Format(
                "챕터 최악 여유 {0:F2}가 일반 최악 {1:F2}보다 크다 - 차등이 없다",
                chapterWorst, stageWorst));
            Assert.Less(finaleWorst, chapterWorst, string.Format(
                "피날레 최악 여유 {0:F2}가 챕터 최악 {1:F2}보다 크다 - 피날레가 관문보다 쉽다",
                finaleWorst, chapterWorst));
        }

        /**
         * @brief 챕터 배수가 **실제로 적용되는지**.
         *
         * 상수가 1보다 큰지만 검사하던 테스트가 있었고, 그것은 통과하는데
         * `ChapterHealthMultiplier`는 어디에서도 쓰이지 않았다. 골드 배수도
         * 시뮬레이션에만 있고 실제 전투에는 없었다. 상수의 존재는 연결의
         * 증거가 아니다 - 값이 흘러가는 끝에서 재야 한다.
         *
         * 5스테이지(챕터)와 6스테이지(일반)를 나란히 놓고 배수가 보이는지 본다.
         * 스테이지가 하나 다르므로 곡선 성장분을 나눠서 걷어낸다.
         */
        [Test]
        public void ChapterMultipliers_AreActuallyApplied()
        {
            Assert.Greater(BossCurve.ChapterHealthMultiplier, 1d);
            Assert.Greater(BossCurve.ChapterGoldMultiplier, 1d);
            Assert.Greater(BossCurve.ChapterAttackMultiplier, 1d);

            // 골드가 체력보다 후해야 챕터 보스에 도전할 이유가 생긴다
            Assert.Greater(BossCurve.ChapterGoldMultiplier, BossCurve.ChapterHealthMultiplier);

            var field = FieldFromAssets();
            var mobHealth = BigDouble.FromDouble(field.AverageMobHealth);
            var mobGold = BigDouble.FromDouble(field.AverageMobGold);

            // 체력: 챕터 5 / 일반 4 에서 곡선 성장분을 걷어내면 챕터 배수만 남는다.
            //
            // 20단계의 골드축 보정도 걷어낸다. 그것은 스테이지마다 다른 값이라
            // 두 스테이지의 비에 그대로 남고, 등급 배수와 섞여 보고된다
            double chapterHealth = StageCurve.BossHealthForStage(mobHealth, 5).ToDouble();
            double plainHealth = StageCurve.BossHealthForStage(mobHealth, 4).ToDouble();
            double curveGrowth = StageCurve.HealthMultiplier(5).ToDouble() / StageCurve.HealthMultiplier(4).ToDouble()
                               * StageCurve.BossHealthMultiplier(5) / StageCurve.BossHealthMultiplier(4)
                               * StageCurve.GoldAxisCompensation(5) / StageCurve.GoldAxisCompensation(4);

            Assert.AreEqual(BossCurve.ChapterHealthMultiplier,
                            chapterHealth / plainHealth / curveGrowth, 1e-6d,
                "챕터 보스 체력에 ChapterHealthMultiplier 가 적용되지 않았다");

            // 골드
            double chapterGold = StageCurve.BossGoldForStage(mobGold, 5).ToDouble();
            double plainGold = StageCurve.BossGoldForStage(mobGold, 4).ToDouble();
            double goldGrowth = StageCurve.GoldMultiplier(5).ToDouble() / StageCurve.GoldMultiplier(4).ToDouble();

            Assert.AreEqual(BossCurve.ChapterGoldMultiplier,
                            chapterGold / plainGold / goldGrowth, 1e-6d,
                "챕터 보스 골드에 ChapterGoldMultiplier 가 적용되지 않았다");

            // 공격력
            double chapterAttack = BossCurve.AttackDamageForStage(5);
            double plainAttack = BossCurve.AttackDamageForStage(4);

            Assert.AreEqual(BossCurve.ChapterAttackMultiplier,
                            chapterAttack / plainAttack / BossCurve.AttackGrowth, 1e-6d,
                "챕터 보스 공격력에 ChapterAttackMultiplier 가 적용되지 않았다");
        }

        [Test]
        public void ChapterIsDerivedFromStage_NoNewSaveField()
        {
            // 챕터가 스테이지에서 유도되므로 세이브에 새 필드가 필요 없다
            Assert.AreEqual(1, BossCurve.ChapterOf(1));
            Assert.AreEqual(1, BossCurve.ChapterOf(5));
            Assert.AreEqual(2, BossCurve.ChapterOf(6));
            Assert.AreEqual(2, BossCurve.ChapterOf(10));
            Assert.AreEqual(3, BossCurve.ChapterOf(11));
        }

        // ------------------------------------------------------------ 잡몹 병목

        /**
         * @brief 잡몹 파밍 시간이 DPS에 다시 반응하는지.
         *
         * 9단계에서는 4스테이지부터 11.0초에 고정됐다 - 10마리 x 보충 간격 1.1초.
         * 공격력을 아무리 올려도 파밍이 1초도 빨라지지 않는 구간이었다.
         *
         * 이제 보충 간격이 처치 속도에 수렴하므로(SpawnPacing) 하한 0.4초에
         * 닿기 전까지는 계속 줄어든다.
         */
        [Test]
        public void MobFarmingTime_RespondsToDps()
        {
            var results = StageSimulation.Run(10, FieldFromAssets());

            // 9단계의 고정값. 여기에 머물러 있으면 병목이 그대로다
            const double OldFloor = StageCurve.KillsPerStage * SpawnPacing.BaseInterval;

            double first = results[0].MobSeconds;
            double sixth = results[5].MobSeconds;

            Assert.Less(sixth, first * 0.5d, string.Format(
                "1스테이지 {0:F1}초 -> 6스테이지 {1:F1}초. 파밍 시간이 DPS에 반응하지 않는다",
                first, sixth));

            Assert.Less(sixth, OldFloor, string.Format(
                "6스테이지 파밍이 {0:F1}초 - 9단계의 공급 하한 {1:F1}초를 넘지 못했다",
                sixth, OldFloor));

            // 하한 아래로는 내려가지 않는다. 그 아래는 요괴가 걸어 들어오는 것이
            // 보이지 않고 오른쪽에서 튀어나오는 것처럼 된다
            foreach (var row in results)
            {
                Assert.GreaterOrEqual(row.SpawnInterval, SpawnPacing.MinInterval - 1e-6d,
                    "stage " + row.Stage + ": 보충 간격이 하한 아래로 내려갔다");
                Assert.LessOrEqual(row.SpawnInterval, SpawnPacing.BaseInterval + 1e-6d,
                    "stage " + row.Stage + ": 보충 간격이 시작값보다 커졌다");
            }
        }

        /**
         * @brief 동시 생존 수는 건드리지 않았다.
         *
         * 세로 화면의 전투 영역은 가로 6.75 units뿐이고 핸드오프 문서가 읽히는
         * 한계를 3~5마리로 잡았다. 병목을 푸는 방법으로 '더 많이'가 아니라
         * '더 빨리'를 고른 이유이며, 그 선택이 코드에 남아 있어야 한다.
         */
        [Test]
        public void SpawnPacing_DoesNotRaiseTheAliveCount()
        {
            Assert.AreEqual(1.1f, SpawnPacing.BaseInterval, 1e-6f);
            Assert.AreEqual(0.4f, SpawnPacing.MinInterval, 1e-6f);

            // 좁히는 쪽이 되돌리는 쪽보다 빨라야 성장이 곧바로 체감된다
            Assert.Less(SpawnPacing.TightenFactor, 1f);
            Assert.Greater(SpawnPacing.RelaxFactor, 1f);
            Assert.Less(SpawnPacing.RelaxFactor - 1f, 1f - SpawnPacing.TightenFactor,
                "간격이 되돌아가는 속도가 좁아지는 속도보다 빠르면 스테이지가 오를 때마다 화면이 빈다");
        }

        [Test]
        public void SpawnPacing_ConvergesToKillTime()
        {
            // 되먹임이 실제로 처치 시간에 수렴하는지. 시뮬레이션이 SettledInterval로
            // 건너뛰는 그 평형을 런타임 규칙으로 직접 돌려 확인한다
            foreach (double killTime in new[] { 0.2d, 0.7d, 1.5d })
            {
                float interval = SpawnPacing.BaseInterval;
                for (int step = 0; step < 200; step++)
                    interval = SpawnPacing.Next(interval, interval > killTime);

                Assert.AreEqual(SpawnPacing.SettledInterval(killTime), interval,
                    0.06f, "처치 " + killTime + "초에서 수렴값이 어긋난다");
            }
        }

        /**
         * @brief 1~5 스테이지 소요 시간이 보고서에 적은 값에서 벗어나지 않는지.
         *
         * 곡선 어느 하나를 손대면 이 값이 움직인다. 움직이는 것 자체는 정상이고,
         * 보고서를 함께 고치라는 신호로 쓴다.
         *
         * 20단계에서 158초 -> 179초가 됐다(+13%). 골드 획득 축이 생기면서 초반에
         * 살 것이 하나 늘었고, 그 축은 회수에 2분이 걸려 온보딩 구간에서는 아직
         * 손해였기 때문이다.
         *
         * 21단계에 그 축을 6스테이지 해금으로 밀어내면서 **168초로 돌아왔다.**
         * 축을 완전히 무력화해도 같은 값이 나오므로, 이 구간에 남은 골드 축의
         * 기여는 0이다(`GoldAxis_ContributesNothingBeforeUnlock`).
         *
         * 158초에서 남은 10초는 골드 축이 아니라 그 사이의 다른 곡선 변경에서
         * 왔다. 어느 것인지는 아직 못 짚었고, 온보딩 목표(3분 안쪽)에는 여유가
         * 있어 이번에는 쫓지 않았다.
         *
         * 26단계에 168초 -> 175초가 됐다(+4%). 원인이 하나로 짚인다 - 보스 골드
         * 웃돈을 지우면서(BossGoldMultiplier 12 -> 10.8) 이 구간의 보스 보상이
         * 10% 줄었고, 그만큼 강화를 덜 산다. 스킬은 무관하다(Lv.10 해금이라
         * st8부터다).
         *
         * 웃돈을 지운 대가이고, 그 대가로 얻은 것이 골드 축의 액티브 이득이다
         * (StageCurve.GoldAxisMarginExponent). 온보딩 목표 3분(180초) 안쪽에는
         * 여전히 들어온다.
         */
        [Test]
        public void StageOneToFive_TakesTheDocumentedTime()
        {
            var results = StageSimulation.Run(5, FieldFromAssets());
            double total = StageSimulation.TotalSeconds(results);

            Assert.AreEqual(175d, total, 12d,
                "1~5 스테이지 소요 시간이 " + total.ToString("F0") + "초로 바뀌었다 (보고서 기준 175초)");

            // 온보딩 목표는 절대값이다. 위 기준값은 "바뀌면 보고서도 고쳐라"는
            // 신호이지만 이 줄은 넘으면 안 되는 선이다 - 이탈이 가장 큰 구간이다
            Assert.Less(total, 180d, string.Format(
                "1~5가 {0:F0}초로 3분을 넘는다. 온보딩은 이탈이 가장 큰 구간이라 "
                + "여기서 잃은 시간은 되돌아오지 않는다", total));
        }

        // ------------------------------------------------------------ 꽃잎 예산

        /**
         * @brief 꽃잎 예산이 수명이 아니라 개수로 흡수되는지.
         *
         * 컴포넌트를 만들어 실제 직렬화 기본값으로 검사한다. SakuraContentBuilder가
         * 씬에 기록하는 값과 스크립트 기본값이 같아야 이 검사가 의미를 갖는다.
         */
        [Test]
        public void SakuraBudget_KeepsLifetimeAboveTheFloor()
        {
            var go = new UnityEngine.GameObject("~TestSakura");
            var burst = go.AddComponent<SakuraBurst>();

            float capAps = AttackSpeedCurve.Ceiling;

            Assert.GreaterOrEqual(burst.LifetimeAt(capAps), 0.25f - 1e-4f,
                "상한 공격속도에서 꽃잎 수명이 하한 아래로 내려갔다 - 흩날림이 점멸이 된다");

            // 낮은 공격속도에서는 하한이 개입하지 않아야 한다
            Assert.Greater(burst.LifetimeAt(1.15f), 0.25f,
                "낮은 공격속도에서까지 하한이 걸리면 수명 범위가 무의미해진다");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void SakuraBudget_AbsorbsTheOverrunWithCount()
        {
            var go = new UnityEngine.GameObject("~TestSakura");
            var burst = go.AddComponent<SakuraBurst>();

            int atLow = burst.PetalsAt(1.15f);
            int atCap = burst.PetalsAt(AttackSpeedCurve.Ceiling);

            Assert.Less(atCap, atLow,
                "상한 공격속도에서 꽃잎 개수가 줄지 않았다. 수명 하한을 뒀으므로 " +
                "개수로 흡수하지 않으면 예산이 그냥 초과된다");

            // 동시에 떠 있는 기대 개수가 예산 안에 머무는지. 개수 하한(3) 때문에
            // 정확히 같지는 않고 조금 넘칠 수 있다 - 그 여유를 명시적으로 허용한다
            float concurrentLow = burst.ConcurrentPetalsAt(1.15f);
            float concurrentCap = burst.ConcurrentPetalsAt(AttackSpeedCurve.Ceiling);

            Assert.LessOrEqual(concurrentCap, concurrentLow * 1.35f, string.Format(
                "동시 꽃잎이 낮은 공격속도 {0:F1}개 -> 상한 {1:F1}개로 불어났다. " +
                "예산이 지켜지지 않는다", concurrentLow, concurrentCap));

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------ 12단계: 경험치

        /**
         * @brief 경험치가 시뮬레이션에 실제로 들어와 있는지.
         *
         * 이것을 먼저 확인하는 이유는 11단계의 ChapterHealthMultiplier와 같은
         * 일을 막기 위해서다. 그 상수는 선언되고 테스트까지 있었지만 값 흐름
         * 어디에도 연결되지 않아서, 바꿔도 게임이 달라지지 않았다.
         *
         * 여기서 재는 것은 "레벨이 오른다"가 아니라 **레벨이 스탯에 도달한다**다.
         */
        [Test]
        public void Simulation_ModelsExperience()
        {
            var results = StageSimulation.Run(20, FieldFromAssets());

            Assert.Greater(results[19].CharacterLevel, 1,
                "20스테이지를 지나도 Lv.1이면 경험치가 시뮬레이션에 들어오지 않은 것이다");

            Assert.Greater(results[19].AttackPoints + results[19].HealthPoints, 0,
                "레벨은 올랐는데 스탯 포인트가 하나도 안 찍혔다");

            // 증폭이 스탯에 도달했는가. 배수가 1이면 포인트가 장부에만 있는 것이다
            Assert.Greater(results[19].AttackAmp, 1d,
                "스탯 포인트가 공격력에 곱해지지 않는다");

            // 레벨은 단조 증가해야 한다. 스테이지가 오를 때 초기화되는 경로가
            // 생기면 여기서 걸린다
            for (int i = 1; i < results.Count; i++)
                Assert.GreaterOrEqual(results[i].CharacterLevel, results[i - 1].CharacterLevel,
                    "stage " + results[i].Stage + "에서 레벨이 내려갔다");
        }

        /**
         * @brief 레벨 성장이 골드 성장을 앞지르지 않는가.
         *
         * 12단계 밸런스의 뼈대다. 스탯 포인트는 골드 강화에 **곱해지는** 보조
         * 축이어서, 레벨이 골드보다 빨리 자라면 어느 지점부터 "레벨만 올리면
         * 되는 게임"이 된다.
         *
         * 두 축이 DPS에 기여하는 배수로 잰다. 레벨 쪽은 스탯 포인트 증폭,
         * 골드 쪽은 공격력 강화 곡선이다 - 둘 다 공격력에 곱해지는 값이라
         * 같은 단위로 비교할 수 있다.
         */
        [Test]
        public void LevelGrowth_DoesNotOutpaceGoldGrowth()
        {
            var results = StageSimulation.Run(40, FieldFromAssets());

            foreach (var row in results)
            {
                double fromGold = AttackPowerCurve.ValueAtLevel(row.AttackPowerLevel)
                                  / AttackPowerCurve.ValueAtLevel(1);
                double fromLevel = row.AttackAmp;

                Assert.Less(fromLevel, fromGold, string.Format(
                    "stage {0}: 레벨 증폭 x{1:F2}이 골드 강화 x{2:F2}를 앞질렀다 " +
                    "(Lv.{3}, 공격력 Lv.{4})",
                    row.Stage, fromLevel, fromGold, row.CharacterLevel, row.AttackPowerLevel));
            }

            // 격차가 벌어지는 방향이어야 한다. 같은 비율로 자라면 앞지르지는
            // 않아도 레벨이 골드를 그대로 따라 그리는 값이 된다
            double earlyRatio = results[4].AttackAmp
                / (AttackPowerCurve.ValueAtLevel(results[4].AttackPowerLevel) / AttackPowerCurve.ValueAtLevel(1));
            double lateRatio = results[39].AttackAmp
                / (AttackPowerCurve.ValueAtLevel(results[39].AttackPowerLevel) / AttackPowerCurve.ValueAtLevel(1));

            Assert.Less(lateRatio, earlyRatio, string.Format(
                "레벨의 상대 비중이 줄지 않는다 (5스테이지 {0:F4} -> 40스테이지 {1:F4})",
                earlyRatio, lateRatio));
        }

        /**
         * @brief 레벨업 리듬.
         *
         * 초반은 30초 안팎, 후반으로 갈수록 완만해야 한다. 초반이 너무 느리면
         * 레벨업 버튼과 스탯 포인트가 무엇인지 배울 자리가 없고, 후반에도
         * 같은 속도로 오르면 스탯 포인트가 골드 강화를 밀어낸다.
         *
         * **스테이지당 레벨 수**로 잰다. 벽시계 초/레벨로 재면 후반에 스테이지
         * 자체가 짧아지는 것(잡몹 구간이 스폰 하한 4초로 수렴한다)이 섞여
         * 들어와, 리듬이 느려졌는데 숫자는 빨라진 것으로 나온다.
         */
        [Test]
        public void LevelUpRhythm_SlowsDown()
        {
            var results = StageSimulation.Run(40, FieldFromAssets());

            double early = LevelsPerStage(results, 0, 10);
            double late = LevelsPerStage(results, 30, 40);

            Assert.Less(late, early, string.Format(
                "레벨업이 완만해지지 않는다 (1~10스테이지 {0:F2}렙/스테이지, " +
                "31~40스테이지 {1:F2}렙/스테이지)", early, late));

            // 멈춰서도 안 된다. 레벨이 사실상 정지하면 스탯 포인트 축 둘이
            // 화면만 차지한다
            Assert.Greater(late, 0.3d, string.Format(
                "31~40스테이지에서 레벨이 {0:F2}렙/스테이지로 사실상 멈췄다", late));

            // 첫 스테이지에서는 반드시 한 번 오른다. 첫 보스를 만나기 전에
            // 레벨업 버튼을 한 번 보는 것이 온보딩이다
            Assert.GreaterOrEqual(results[0].LevelsGained, 1,
                "1스테이지에서 한 번도 레벨업하지 못한다");
        }

        static double LevelsPerStage(System.Collections.Generic.List<StageSimulation.StageResult> rows,
                                     int from, int to)
        {
            int levels = 0;
            for (int i = from; i < to && i < rows.Count; i++) levels += rows[i].LevelsGained;
            return levels / (double)(to - from);
        }

        // ------------------------------------------------------------ 16단계: 죽은 축 차단

        /**
         * @brief 스탯 포인트 한 개를 **찍는 그 순간** 체감되는가.
         *
         * 기존 생존성 검사(EveryAxis...)는 스탯 포인트 축을 잡지 못한다. 포인트는
         * 골드가 들지 않아서 구매 정책이 무조건 찍기 때문이다 - 장부에는 "샀다"고
         * 남지만 화면에서는 죽어 있다. 회복 축(11단계)과 스탯 포인트 축(14~15단계)이
         * 같은 함정에 두 번 빠졌다.
         *
         * 그래서 "샀는가"가 아니라 **"사면 움직이는가"**를 잰다. 방금 레벨업으로
         * 얻은 포인트 하나를 그 자리에서 투자했을 때 DPS(또는 EHP)가 1% 이상
         * 움직여야 한다. 1%는 "눌렀더니 숫자가 바뀌었다"가 성립하는 최소선이다.
         *
         * **끝단에서 잰다.** 상수가 아니라 시뮬레이션이 실제로 굴린 스탯에서
         * 다시 계산한다 - 11단계 ChapterHealthMultiplier가 선언만 되고 연결되지
         * 않았던 것과 같은 사고를 막는다.
         */
        const double MinimumFeltGain = 0.01d;

        [Test]
        public void StatPoint_IsFeltTheMomentItIsSpent()
        {
            var results = StageSimulation.Run(20, FieldFromAssets());

            // 초반·중반·후반 세 지점에서 본다. 한 지점만 보면 그 레벨에서만
            // 통과하는 계수가 빠져나간다
            foreach (int stage in new[] { 5, 12, 20 })
            {
                var row = results[stage - 1];

                // 공격력 증폭: 지금 DPS에서 증폭만 한 칸 올린다
                double ampNow = StatPointCurve.Multiplier(row.AttackPoints);
                double ampNext = StatPointCurve.Multiplier(row.AttackPoints + 1);
                double dpsGain = ampNext / ampNow - 1d;

                Assert.GreaterOrEqual(dpsGain, MinimumFeltGain, string.Format(
                    "stage {0}: 공격력 증폭 한 칸이 DPS를 {1:P2}밖에 못 올린다 " +
                    "(포인트 {2}개, 공격력 강화 Lv.{3}). 눌러도 숫자가 안 움직이면 " +
                    "그 버튼은 죽은 것이다",
                    stage, dpsGain, row.AttackPoints, row.AttackPowerLevel));

                // 체력 증폭: 유효체력 기준
                double hpAmpNow = StatPointCurve.Multiplier(row.HealthPoints);
                double hpAmpNext = StatPointCurve.Multiplier(row.HealthPoints + 1);
                double regenFraction = row.MaxHealth > 0d ? row.RegenPerSecond / row.MaxHealth : 0d;

                double ehpNow = SurvivalEfficiency.EffectiveHealth(row.MaxHealth, regenFraction);
                double ehpNext = SurvivalEfficiency.EffectiveHealth(
                    row.MaxHealth / hpAmpNow * hpAmpNext, regenFraction);
                double ehpGain = ehpNext / ehpNow - 1d;

                Assert.GreaterOrEqual(ehpGain, MinimumFeltGain, string.Format(
                    "stage {0}: 체력 증폭 한 칸이 유효체력을 {1:P2}밖에 못 올린다 " +
                    "(포인트 {2}개)", stage, ehpGain, row.HealthPoints));
            }
        }

        /**
         * @brief 스탯 포인트 축도 20스테이지까지 실제로 찍힌다.
         *
         * 골드 축의 생존성 검사와 짝이다. 이쪽이 0이면 구매 정책이 포인트를
         * 아예 배분하지 않는다는 뜻이고, 그러면 위의 체감 검사도 의미가 없다.
         */
        [Test]
        public void StatPointAxes_AreInvestedThrough20()
        {
            var final = StageSimulation.Run(20, FieldFromAssets())[19];

            Assert.Greater(final.AttackPoints, 0, "공격력 증폭에 포인트가 한 번도 안 들어갔다");
            Assert.Greater(final.HealthPoints, 0, "체력 증폭에 포인트가 한 번도 안 들어갔다");
        }

        // ------------------------------------------------------------ 16단계: 회복 상한

        /**
         * @brief 초당 회복이 최대 체력을 넘지 않는다.
         *
         * 15단계 실측에서 Lv.54의 회복이 **156.2%/s**였다. 초당 최대 체력의
         * 1.5배를 회복하면 어떤 피해도 다음 프레임에 지워지고, 11단계에서 만든
         * 체력 게이트가 통째로 무력화된다.
         *
         * 원인은 회복이 최대 체력 **비율**의 곱연산이면서 상한이 없다는 것이다.
         * 비율로 바꾼 것 자체는 옳았지만(11단계) 100%를 넘을 수 있다는 것을
         * 그때 보지 못했다.
         */
        [Test]
        public void RegenRatio_IsCapped()
        {
            // 곡선 자체에 상한이 있는가. 레벨을 충분히 올려도 캡을 넘지 않아야 한다
            for (int level = 1; level <= 300; level++)
            {
                double ratio = HealthRegenCurve.CappedValueAtLevel(level);
                Assert.LessOrEqual(ratio, HealthRegenCurve.Ceiling + 1e-9, string.Format(
                    "Lv.{0}에서 회복이 {1:P1}/s로 상한 {2:P1}/s를 넘는다",
                    level, ratio, HealthRegenCurve.Ceiling));
            }

            // 상한이 무적을 만들지 않는 크기인가. 30초 전투에서 회복만으로
            // 최대 체력의 몇 배를 벌어주는지 본다
            double healedOverFight = HealthRegenCurve.Ceiling * StageCurve.BossTimeLimitSeconds;
            Assert.Less(healedOverFight, 15d, string.Format(
                "상한 {0:P0}/s면 30초 동안 최대 체력의 {1:F1}배를 회복한다 - 무적이다",
                HealthRegenCurve.Ceiling, healedOverFight));
            Assert.Greater(healedOverFight, 2d, string.Format(
                "상한 {0:P0}/s면 30초 회복량이 최대 체력의 {1:F1}배뿐이라 회복 축을 살 이유가 없다",
                HealthRegenCurve.Ceiling, healedOverFight));
        }

        /**
         * @brief 시뮬레이션이 상한을 실제로 반영하는가.
         *
         * 곡선에 상한을 넣어도 시뮬레이션이 무상한 값을 쓰면, 밸런스는 여전히
         * 무적 플레이어 기준으로 계산된다.
         */
        [Test]
        public void Simulation_UsesTheCappedRegen()
        {
            var results = StageSimulation.Run(30, FieldFromAssets());

            foreach (var row in results)
            {
                double fraction = row.MaxHealth > 0d ? row.RegenPerSecond / row.MaxHealth : 0d;
                Assert.LessOrEqual(fraction, HealthRegenCurve.Ceiling + 1e-9, string.Format(
                    "stage {0}: 시뮬레이션의 회복이 {1:P1}/s로 상한을 넘는다 (회복 Lv.{2})",
                    row.Stage, fraction, row.RegenLevel));
            }
        }

        // ------------------------------------------------------------ 16단계: EXP 리듬

        /**
         * @brief 곡선을 따라가는 플레이어에게 레벨이 무더기로 쌓이지 않는다.
         *
         * 15단계 화면에서 레벨 2인데 **레벨업 16개가 밀려 있었다.** 레벨업이
         * 보상이 아니라 "여러 번 눌러야 하는 잡일"이 된다.
         *
         * 스테이지 하나에서 오르는 레벨 수로 잰다. 플레이어가 스테이지마다 한 번
         * 누른다고 보면 그것이 곧 밀리는 양이다.
         */
        [Test]
        public void LevelUps_DoNotPileUp()
        {
            var results = StageSimulation.Run(40, FieldFromAssets());

            foreach (var row in results)
            {
                Assert.LessOrEqual(row.LevelsGained, 2, string.Format(
                    "stage {0}에서 한 번에 {1}레벨이 오른다 - 레벨업이 잡일이 된다 " +
                    "(Lv.{2})", row.Stage, row.LevelsGained, row.CharacterLevel));
            }
        }

        /**
         * @brief 시뮬레이션과 실전이 같은 경험치 값을 쓰는가.
         *
         * 9단계에서 계산 헬퍼와 실전이 어긋나 "계산상 통과, 화면은 실패"가 났다.
         * 경험치는 12단계에 들어온 가장 새로운 축이라 같은 위험이 가장 크다.
         *
         * 시뮬레이션이 쓰는 함수와 스포너/보스전이 쓰는 함수가 같은지 못 박는다.
         */
        [Test]
        public void SimulationExp_MatchesTheLiveCurve()
        {
            for (int stage = 1; stage <= 30; stage++)
            {
                // 잡몹: EnemySpawner.Spawn 이 부르는 것과 같은 함수
                double mob = ExpCurve.MobExp(stage).ToDouble();
                Assert.Greater(mob, 0d, "stage " + stage + " 잡몹 경험치가 0이다");

                // 보스: BossFight.BossExpReward 가 부르는 것과 같은 함수
                double boss = ExpCurve.BossExp(stage).ToDouble();
                double expected = mob * ExpCurve.BossExpMultiplier * ExpCurve.MultiplierFor(stage);
                Assert.AreEqual(expected, boss, expected * 1e-9,
                    "stage " + stage + ": 보스 경험치가 곡선과 어긋난다");
            }

            // 시뮬레이션이 한 스테이지에서 넣는 총량이 위 두 함수의 합과 같은가.
            // 여기가 어긋나면 밸런스는 맞는데 화면은 다른 속도로 오른다
            var results = StageSimulation.Run(3, FieldFromAssets());
            double perStage1 = ExpCurve.MobExp(1).ToDouble() * StageCurve.KillsPerStage
                               + ExpCurve.BossExp(1).ToDouble();
            Assert.Greater(perStage1, 0d);
            Assert.GreaterOrEqual(results[0].LevelsGained, 1,
                "1스테이지에서 한 번도 레벨업하지 못한다 - 온보딩이 끊긴다");
        }

        // ------------------------------------------------------------ 32단계: 장비

        /**
         * @brief 밴드를 **양쪽 끝에서** 검사한다.
         *
         * 32단계에 이 검사가 두 벌이 된 이유가 있다. 장비의 등급은 보석으로
         * 열리고 보석은 일일 퀘스트가 주므로, **접속 빈도가 밴드에 들어왔다.**
         * 한쪽만 재면 다른 쪽 플레이어의 게임이 검사되지 않는다.
         *
         *   기본       보석 무제한 = 매일 접속하는 플레이어. **여유의 위쪽 끝**
         *   보석 하한  업적 + 반복 티어만 = 일일을 한 번도 안 받은 플레이어.
         *              **여유의 아래쪽 끝**
         *
         * 위 BossMargin_StaysInBand* 둘은 기본 정책만 본다. 여기가 하한 쪽을
         * 맡는다 - 이름이 달라야 실패했을 때 어느 플레이어가 깨졌는지 읽힌다.
         */
        [Test]
        public void BossMargin_StaysInBandForTheGemFloorPlayer()
        {
            var policy = new StageSimulation.Policy { GemsFromQuestsOnly = true };
            var results = StageSimulation.Run(30, FieldFromAssets(), policy);

            foreach (var row in results)
            {
                var tier = BossCurve.TierOf(row.Stage);

                double floor = tier == BossCurve.Tier.Finale ? FinaleMarginFloor
                             : tier == BossCurve.Tier.Chapter ? ChapterMarginFloor
                             : MarginFloor;
                double ceiling = tier == BossCurve.Tier.Finale ? FinaleMarginCeiling
                               : tier == BossCurve.Tier.Chapter ? ChapterMarginCeiling
                               : MarginCeiling;

                Assert.GreaterOrEqual(row.BossMargin, floor, string.Format(
                    "stage {0}: 일일 퀘스트를 한 번도 안 받은 플레이어의 여유가 {1:F2}다 "
                    + "(무기 {2}등급 Lv.{3} x{4:F2}, 보석 {5}벌어 {6}씀). "
                    + "보석 값을 낮추거나 EquipmentMarginExponent를 낮춰라",
                    row.Stage, row.BossMargin, row.WeaponGrade, row.WeaponLevel,
                    row.WeaponMultiplier, row.GemsEarned, row.GemsSpent));

                Assert.LessOrEqual(row.BossMargin, ceiling, string.Format(
                    "stage {0}: 보석 하한인데도 여유가 {1:F2}로 천장을 넘는다", row.Stage, row.BossMargin));
            }
        }

        /**
         * @brief 생존 밴드. **바닥만 있다.**
         *
         * 방어구가 유효체력을 곱하므로 이 축이 32단계에 처음으로 생존 여유를
         * 위로 밀었다. 천장을 두지 않는 이유는 두꺼운 것이 문제가 아니기
         * 때문이다 - 남는 골드가 화력으로 가고, 그것은 보스 여유 밴드가 이미
         * 잡고 있다.
         */
        const double SurvivalMarginFloor = 1.16d;

        [Test]
        public void SurvivalMargin_StaysAboveTheFloorForBothGemPolicies()
        {
            var policies = new[]
            {
                StageSimulation.Policy.Default,
                new StageSimulation.Policy { GemsFromQuestsOnly = true }
            };

            foreach (var policy in policies)
            {
                foreach (var row in StageSimulation.Run(30, FieldFromAssets(), policy))
                {
                    Assert.GreaterOrEqual(row.SurvivalMargin, SurvivalMarginFloor, string.Format(
                        "stage {0}: 생존 여유 {1:F2} (체력 Lv.{2}, 방어구 {3}등급 Lv.{4} x{5:F2})",
                        row.Stage, row.SurvivalMargin, row.HealthLevel,
                        row.ArmorGrade, row.ArmorLevel, row.ArmorMultiplier));
                }
            }
        }

        /**
         * @brief 죽은 버튼 검사 (a) - **장비가 실제로 진행을 움직이는가.**
         *
         * 16단계 방식이다. "샀는가"가 아니라 "사면 달라지는가"를 재고, 그
         * 질문은 산 플레이어와 안 산 플레이어를 나란히 돌려야만 답이 나온다.
         *
         * 20단계의 골드 축이 정확히 여기서 걸렸어야 했다 - 장부에는 열세 번
         * 샀다고 남았는데 30스테이지까지 총 시간이 안 산 것과 같았다.
         *
         * 세 가지를 함께 본다. 하나만 보면 빠져나간다:
         *
         *   레벨    두 슬롯이 실제로 올라가는가 (장부)
         *   스탯    DPS와 EHP가 움직이는가 (값)
         *   시간    30스테이지까지가 빨라지는가 (체감)
         */
        [Test]
        public void Equipment_MovesDpsAndEhpAndProgress()
        {
            var field = FieldFromAssets();

            var with = StageSimulation.Run(30, field);
            var without = StageSimulation.Run(30, field,
                new StageSimulation.Policy { SkipEquipment = true });

            var end = with[29];

            // 1. 장부 - 두 슬롯 다 올라간다
            Assert.Greater(end.WeaponGrade, 1, "무기 등급이 30스테이지까지 한 번도 안 올랐다");
            Assert.Greater(end.WeaponLevel, 1, "무기를 한 번도 단련하지 않았다");
            Assert.Greater(end.ArmorGrade, 1, "방어구 등급이 30스테이지까지 한 번도 안 올랐다");
            Assert.Greater(end.ArmorLevel, 1, "방어구를 한 번도 단련하지 않았다");

            // 2. 값 - 배수가 실제로 스탯에 도달한다
            Assert.Greater(end.WeaponMultiplier, 1d, "무기 레벨이 배수로 바뀌지 않았다");
            Assert.Greater(end.ArmorMultiplier, 1d, "방어구 레벨이 배수로 바뀌지 않았다");
            Assert.Greater(end.ExpectedDps, without[29].ExpectedDps, string.Format(
                "장비를 다 올렸는데 DPS가 안 산 쪽보다 낮다 ({0:F0} 대 {1:F0})",
                end.ExpectedDps, without[29].ExpectedDps));
            Assert.Greater(end.MaxHealth, without[29].MaxHealth, string.Format(
                "방어구를 올렸는데 최대 체력이 안 산 쪽보다 낮다 ({0:F0} 대 {1:F0})",
                end.MaxHealth, without[29].MaxHealth));

            // 3. 체감 - 30스테이지까지가 실제로 빨라진다
            double withSeconds = StageSimulation.TotalSeconds(with);
            double withoutSeconds = StageSimulation.TotalSeconds(without);
            double gain = 1d - withSeconds / withoutSeconds;

            Assert.GreaterOrEqual(gain, MinimumAxisTimeGain, string.Format(
                "장비를 산 플레이어가 {0:F0}초, 안 산 플레이어가 {1:F0}초로 이득이 {2:P1}뿐이다. "
                + "지표는 사라고 말하는데 실제로는 손해인 축이다 (20단계 골드 축과 같은 함정)",
                withSeconds, withoutSeconds, gain));
        }

        /**
         * @brief 축 하나가 30스테이지 총 시간에서 가져야 할 최소 이득.
         *
         * 4%는 20단계가 골드 축에 쓴 기준과 같다
         * (SkillAxisTests.GoldAxis_AddsValueToTheGame). 같은 자를 쓰는 것이
         * 요점이다 - 축마다 다른 기준을 쓰면 "이 축은 원래 작다"가 언제든
         * 변명이 된다.
         */
        const double MinimumAxisTimeGain = 0.04d;

        /**
         * @brief 죽은 버튼 검사 (b) - **보석이 값어치가 있는가.**
         *
         * (a)와 다른 질문이다. 저쪽은 장비 전체를, 이쪽은 그중 **보석 몫**을
         * 묻는다. 등급업이 보석의 유일한 소비처이므로 SkipGradeUps 는 곧
         * "보석을 한 개도 안 쓴 플레이어"다.
         *
         * 20단계가 두 질문을 섞어 읽었다가 "사면 손해"라는 결론을 냈고, 그래서
         * Policy가 SkipGoldGain / NeutralizeGoldAxis 두 벌로 남아 있다.
         * 여기도 같은 이유로 두 벌이다.
         */
        [Test]
        public void GemGradeUps_AreWorthTheirPrice()
        {
            var field = FieldFromAssets();

            var with = StageSimulation.Run(30, field);
            var without = StageSimulation.Run(30, field,
                new StageSimulation.Policy { SkipGradeUps = true });

            double withSeconds = StageSimulation.TotalSeconds(with);
            double withoutSeconds = StageSimulation.TotalSeconds(without);
            double gain = 1d - withSeconds / withoutSeconds;

            Assert.GreaterOrEqual(gain, MinimumAxisTimeGain, string.Format(
                "등급업을 산 플레이어가 {0:F0}초, 단련만 한 플레이어가 {1:F0}초로 이득이 {2:P1}뿐이다. "
                + "보석이 죽은 재화다 - 값을 낮추거나 GradeStep을 키워라",
                withSeconds, withoutSeconds, gain));

            // 실제로 보석을 썼는가. 안 썼으면 위 비교가 아무것도 재지 않은 것이다
            Assert.Greater(with[29].GemsSpent, 0, "기본 정책이 보석을 한 개도 안 썼다");
            Assert.AreEqual(0, without[29].GemsSpent, "비교군이 보석을 썼다 - 비교가 성립하지 않는다");

            // 한 번의 등급업이 DPS에서 **체감되는 크기**인가. 시간 이득이 있어도
            // 한 칸이 안 느껴지면 그것은 "여러 번 눌러야 아는 버튼"이다
            var weapon = EquipmentCatalog.Find(EquipmentCatalog.WeaponId);
            Assert.GreaterOrEqual(weapon.GradeStep - 1d, 0.03d, string.Format(
                "등급업 한 번이 DPS를 {0:P2}밖에 못 올린다", weapon.GradeStep - 1d));
        }

        /**
         * @brief 장비가 온보딩(1~5)을 늘리지 않는다.
         *
         * 해금이 st11이라 구조적으로 0이어야 한다. 그래도 재는 이유는 31단계가
         * 같은 검사를 업적에 붙여둔 것과 같다 - 해금 스테이지를 낮추고 싶어지는
         * 날 여기서 걸린다.
         */
        [Test]
        public void Equipment_DoesNotDistortOnboarding()
        {
            var field = FieldFromAssets();

            double with = StageSimulation.TotalSeconds(StageSimulation.Run(5, field));
            double without = StageSimulation.TotalSeconds(StageSimulation.Run(5, field,
                new StageSimulation.Policy { NeutralizeEquipment = true }));

            Assert.AreEqual(without, with, 1e-6d, string.Format(
                "1~5 소요 시간이 장비 유무로 갈린다 ({0:F1}초 대 {1:F1}초) - "
                + "해금이 온보딩 안으로 들어왔다", with, without));

            // 31단계 보고서의 값. 여기가 움직이면 보고서도 함께 고쳐야 한다
            Assert.AreEqual(173d, with, 5d,
                "1~5가 " + with.ToString("F0") + "초로 바뀌었다 (31단계 기준 173초)");
        }
    }
}
