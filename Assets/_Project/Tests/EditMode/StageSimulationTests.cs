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
         * @brief 보스가 걸어 들어오는 동안 제한 시간이 흐른다.
         *
         * 이것을 빼먹으면 판정이 실제보다 후해진다. 9단계 초안의 계산이 그랬고,
         * 마침 치명타를 함께 빼먹어서 두 오차가 상쇄되는 바람에 드러나지 않았다.
         */
        [Test]
        public void BossDamageWindow_IsShorterThanTheClock()
        {
            Assert.Less(StageSimulation.BossDamageWindowSeconds, StageCurve.BossTimeLimitSeconds,
                "걸어 들어오는 시간이 제한 시간에서 빠지지 않았다");

            Assert.AreEqual(StageCurve.BossTimeLimitSeconds - StageSimulation.BossWalkInSeconds,
                            StageSimulation.BossDamageWindowSeconds, 1e-9d);

            // 창이 절반 아래로 내려가면 전투보다 기다림이 길어진다
            Assert.Greater(StageSimulation.BossDamageWindowSeconds, StageCurve.BossTimeLimitSeconds * 0.5d,
                "제한 시간의 절반 이상이 걸어 들어오는 데 쓰인다");
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

        /** 여유가 머물러야 하는 구간. 아래로 나가면 벽, 위로 나가면 제한 시간이 무의미 */
        const double MarginFloor = 1.5d;
        const double MarginCeiling = 3.0d;

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
                Assert.GreaterOrEqual(row.BossMargin, MarginFloor, string.Format(
                    "stage {0}: 여유 {1:F2}배 - 곡선을 따라왔는데도 보스가 벽이다 " +
                    "(처치 {2:F1}초 / 때릴 수 있는 {3:F1}초)",
                    row.Stage, row.BossMargin, row.BossKillSeconds, StageSimulation.BossDamageWindowSeconds));

                Assert.LessOrEqual(row.BossMargin, MarginCeiling, string.Format(
                    "stage {0}: 여유 {1:F2}배 - 제한 시간이 아무 일도 하지 않는다 " +
                    "(공격력 Lv.{2} 속도 Lv.{3} 치명타율 Lv.{4} 피해 Lv.{5})",
                    row.Stage, row.BossMargin,
                    row.AttackPowerLevel, row.AttackSpeedLevel,
                    row.CritRateLevel, row.CritDamageLevel));
            }
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
         */
        [Test]
        public void StageOneToFive_TakesTheDocumentedTime()
        {
            var results = StageSimulation.Run(5, FieldFromAssets());
            double total = StageSimulation.TotalSeconds(results);

            Assert.AreEqual(158d, total, 12d,
                "1~5 스테이지 소요 시간이 " + total.ToString("F0") + "초로 바뀌었다 (보고서 기준 158초)");
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
    }
}
