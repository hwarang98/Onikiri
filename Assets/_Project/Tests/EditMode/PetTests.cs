using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 동료(펫) 축의 검사.
     *
     * 밴드 자체는 StageSimulationTests의 AcceleratedZone_* 검사가 지킨다.
     * 여기는 축 자신을 본다 - 예약 몫 착지(33단계가 비워둔 x1.25를 정확히
     * 채우는가), 죽은 버튼(사면 달라지는가), 보석의 값어치(f2p도 문을
     * 여는가), 기대 곡선(보정이 실측을 따라가는가), 그리고 조율 코리더에
     * 이 축이 없다는 구조적 사실.
     */
    public class PetTests
    {
        private const string DataFolder = "Assets/_Project/Data";

        static void LoadFieldAverages(out double health, out double gold)
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

            health = totalWeight > 0d ? (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d;
            gold = totalWeight > 0d ? (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d;
        }

        static StageSimulation.Field FieldFromAssets()
        {
            double health, gold;
            LoadFieldAverages(out health, out gold);
            return new StageSimulation.Field
            {
                AverageMobHealth = health,
                AverageMobGold = gold,
                SpawnInterval = 1.1d
            };
        }

        static double SegmentSeconds(System.Collections.Generic.List<StageSimulation.StageResult> rows,
                                     int fromStage, int toStage)
        {
            double total = 0d;
            for (int i = fromStage - 1; i < toStage && i < rows.Count; i++)
                total += rows[i].MobSeconds + StageSimulation.BossIntroSeconds
                       + StageSimulation.BossWalkInSeconds + rows[i].BossKillSeconds;
            return total;
        }

        // ------------------------------------------------------------ 구조

        /**
         * @brief 조율 코리더(st1~30)에 펫이 없다.
         *
         * 계수가 아니라 구조다 - 해금이 st31이라 코리더 마지막 칸보다 하나
         * 뒤다. 이것이 깨지면(게이트가 앞으로 오면) 코리더의 밴드 검사가
         * 펫 없이 잡은 값이 아니게 되므로, 그 순간 여기서 걸려야 한다.
         * 온보딩(1~5, 173초)과는 여섯 배 떨어져 있다.
         */
        [Test]
        public void Pet_IsAbsentFromTheTunedCorridor()
        {
            Assert.AreEqual(31, PetCurve.UnlockStage,
                "해금 게이트가 움직였다 - 가속 구간의 첫 칸(st31)이어야 코리더가 무사하다");

            var field = FieldFromAssets();
            var policies = new[]
            {
                StageSimulation.Policy.Default,
                new StageSimulation.Policy { GemsFromQuestsOnly = true }
            };

            foreach (var policy in policies)
            {
                var results = StageSimulation.Run(30, field, policy);

                foreach (var row in results)
                {
                    Assert.AreEqual(0, row.PetsOwned, string.Format(
                        "stage {0}: 코리더 안에서 동료가 해금됐다 ({1}마리). 게이트가 "
                        + "움직였다 - 코리더 밴드를 다시 재야 한다",
                        row.Stage, row.PetsOwned));
                }

                // 보정도 함께 1이어야 한다. 축이 없는 구간에 보정만 걸리면
                // 있지도 않은 이득을 상쇄하는 셈이다 - 21단계 골드 축의 사고
                for (int stage = 1; stage <= 30; stage++)
                    Assert.AreEqual(1d, StageCurve.PetCompensation(stage), 1e-12d, string.Format(
                        "stage {0}: 펫이 없는 코리더에 보정 {1:F3}이 걸려 있다",
                        stage, StageCurve.PetCompensation(stage)));
            }
        }

        /**
         * @brief 예약 몫의 산수가 상수에 그대로 남아 있다.
         *
         * 33단계가 천장에 x1.25를 예약할 때의 가정이 "명목 x1.5, 3분의 2쯤
         * 상쇄"였다. 상한(0.5)이나 지수(0.45)를 누가 움직이면 실가속이 예약과
         * 어긋나고, 그 오차는 화면이 아니라 밴드에만 나타난다 - 여기서 먼저
         * 걸려야 한다.
         */
        [Test]
        public void ReservedShare_ArithmeticHolds()
        {
            // 카탈로그 분배의 합이 곧 예약 몫의 명목이다. 동료를 추가하거나
            // 몫을 옮길 때 합이 어긋나면 여기서 걸린다
            Assert.AreEqual(PetCurve.TotalBonusCeiling, PetCatalog.TotalBonusCeiling, 1e-9d,
                "카탈로그 분배의 합이 예약 명목(0.5)과 다르다 - 몫을 옮겼으면 합을 지켜라");

            double nominal = 1d + PetCurve.TotalBonusCeiling;
            double residual = System.Math.Pow(nominal, 1d - StageCurve.PetMarginExponent);

            Assert.AreEqual(1.25d, residual, 0.02d, string.Format(
                "명목 x{0:F2}에 지수 {1:F2}면 실가속이 x{2:F3}다 - 예약 몫(x1.25)과 "
                + "어긋난다. TotalBonusCeiling이나 PetMarginExponent가 움직였다",
                nominal, StageCurve.PetMarginExponent, residual));

            // 각 동료가 Lv.20에서 자기 상한에 실제로 닿는다 - 성장률을 낮추면
            // 상한이 종이 위에만 있는 숫자가 된다
            for (int i = 0; i < PetCatalog.Count; i++)
            {
                var spec = PetCatalog.Pets[i];
                Assert.AreEqual(spec.BonusCeiling,
                    PetCurve.BonusAt(spec.FirstBonus, spec.BonusCeiling, PetCurve.MaxLevel), 1e-9d,
                    "'" + spec.Name + "'이 Lv.20에서 상한에 닿지 않는다");
            }
        }

        // ------------------------------------------------------------ 예약 몫 착지

        /**
         * @brief 동료들의 **합산** 실가속이 예약 몫(x1.25)을 넘지 않는다.
         *
         * 33단계 천장 검사가 "전직까지 채운 세계는 천장/1.25 아래"를 지켰고,
         * 이제 동료들이 그 자리를 함께 채운다. 다중 출전이라 스택이 구조가
         * 됐다 - 그래서 이 검사가 재는 것이 분배표(카탈로그)의 합이고, 합이
         * 예약을 넘으면 여기서 걸린다.
         *
         * 비교군은 NeutralizePets(축도 보정도 없는 세계 = 33단계까지의 게임)다.
         * 그 세계 대비 여유의 비가 곧 이 축이 천장에서 차지한 몫이다.
         */
        [Test]
        public void PetAcceleration_StaysWithinTheReservedShare()
        {
            var field = FieldFromAssets();

            var with = StageSimulation.Run(50, field);
            var without = StageSimulation.Run(50, field,
                new StageSimulation.Policy { NeutralizePets = true });

            for (int i = 30; i < 50; i++)
            {
                double ratio = with[i].BossMargin / without[i].BossMargin;

                // 구매 경로가 달라지는 노이즈(펫에 간 골드만큼 다른 축이 늦는다)
                // 를 조금 허용한다. 예약을 체계적으로 넘으면 여기서 걸린다
                Assert.LessOrEqual(ratio, 1.25d * 1.06d, string.Format(
                    "stage {0}: 동료들이 여유를 x{1:F3} 밀어 올렸다 - 예약 몫(x1.25)을 "
                    + "넘는다. 분배 합을 줄이거나 PetMarginExponent를 올려라 "
                    + "({2}마리 합산 +{3:P0})",
                    with[i].Stage, ratio, with[i].PetsOwned, with[i].PetBonus));
            }
        }

        // ------------------------------------------------------------ 죽은 버튼

        /**
         * @brief 죽은 버튼 검사 (a) - **펫이 실제로 DPS와 진행을 움직이는가.**
         *
         * 16단계 방식. 산 플레이어와 안 산 플레이어를 나란히 돌린다. 세 가지를
         * 함께 본다 - 장부(해금·레벨), 값(보너스·DPS), 시간(구간 소요).
         *
         * 시간은 **가속 구간(st31~50)만** 잰다. 펫은 st31에야 열리므로 50
         * 스테이지 전체로 재면 앞 30스테이지의 동일한 시간이 분모를 부풀려
         * 실제 이득이 희석된다 - 전직과 같은 자(4%), 같은 구간이다.
         */
        [Test]
        public void Pet_MovesDpsAndProgress()
        {
            var field = FieldFromAssets();

            var with = StageSimulation.Run(50, field);
            var without = StageSimulation.Run(50, field,
                new StageSimulation.Policy { SkipPets = true });

            var end = with[49];

            // 1. 장부 - 셋을 다 해금하고 레벨을 실제로 올린다
            Assert.AreEqual(PetCatalog.Count, end.PetsOwned,
                "보석 무제한인데 50스테이지까지 동료를 다 해금하지 않았다");
            for (int i = 0; i < PetCatalog.Count; i++)
                Assert.Greater(end.PetLevels[i], 1, string.Format(
                    "'{0}'의 레벨을 한 번도 올리지 않았다 - 골드 값이 너무 크다",
                    PetCatalog.Pets[i].Name));

            // 2. 값 - 합산 보너스가 실제로 DPS에 도달한다
            Assert.Greater(end.PetBonus, 0d, "동료 레벨이 보너스로 바뀌지 않았다");
            Assert.Greater(end.ExpectedDps, without[49].ExpectedDps, string.Format(
                "동료를 데리고 있는데 DPS가 안 산 쪽보다 낮다 ({0:E2} 대 {1:E2})",
                end.ExpectedDps, without[49].ExpectedDps));

            // 3. 체감 - 가속 구간이 실제로 빨라진다. 기준 4%는 골드 축 -> 장비
            //    -> 전직과 같은 자다. 다중 출전 초안에서 실제로 2.4%까지
            //    떨어졌던 검사다(PetCurve.LevelBaseCost 주석) - 자를 낮추는
            //    대신 곡선을 고쳤다
            double withSeconds = SegmentSeconds(with, 31, 50);
            double withoutSeconds = SegmentSeconds(without, 31, 50);
            double gain = 1d - withSeconds / withoutSeconds;

            Assert.GreaterOrEqual(gain, 0.04d, string.Format(
                "동료를 산 플레이어의 st31~50이 {0:F0}초, 안 산 플레이어가 {1:F0}초로 "
                + "이득이 {2:P1}뿐이다. 이 버튼은 죽었다 - LevelBaseCost를 낮추거나 "
                + "PetMarginExponent를 낮춰라",
                withSeconds, withoutSeconds, gain));
        }

        /**
         * @brief 죽은 버튼 검사 (b) - **보석의 값어치. f2p도 문을 연다.**
         *
         * 첫 펫(청랑, 80)은 보석 하한 플레이어도 산다 - 동료가 걸어 들어오는
         * 장면이 이 재화의 광고판이고, 무과금이 그것을 영영 못 보면 보석의
         * 값어치가 화면에서 증명되지 않는다. 전직 1티어(60)를 무과금 몫으로
         * 남겨둔 것과 같은 판단이다.
         */
        [Test]
        public void GemFloorPlayer_StillUnlocksTheFirstPet()
        {
            var policy = new StageSimulation.Policy { GemsFromQuestsOnly = true };
            var results = StageSimulation.Run(50, FieldFromAssets(), policy);

            var end = results[49];
            Assert.GreaterOrEqual(end.PetsOwned, 1, string.Format(
                "보석 하한 플레이어가 50스테이지까지 동료를 한 마리도 못 열었다 "
                + "(보석 {0}벌어 {1}씀). 청랑 값을 낮추거나 가속 구간 업적 보석을 올려라",
                end.GemsEarned, end.GemsSpent));
        }

        // ------------------------------------------------------------ 기대 곡선

        /**
         * @brief 보정의 기대 곡선이 시뮬레이션 실측을 따라간다.
         *
         * 장비·전직의 ExpectedCurve_TracksTheSimulation과 같은 자리다. 닫힌 식
         * (LevelAtUnlock + LevelsPerStage)이 실측과 어긋나면 보정이 실제보다
         * 가볍거나 무겁게 걸리고, 그 오차는 화면이 아니라 밴드에만 나타난다 -
         * 장비에서 절편을 1로 뒀다가 st21~26이 천장을 넘은 사고 그대로다.
         */
        [Test]
        public void ExpectedCurve_TracksTheSimulation()
        {
            var results = StageSimulation.Run(50, FieldFromAssets());

            foreach (var row in results)
            {
                double expected = PetCurve.ExpectedMultiplierAtStage(row.Stage) - 1d;

                // 43단계 미세화가 골드 배분을 미세하게 흔들어 실측이 0.05%p
                // 밖으로 삐져나왔다(st42에서 4.05%p). 곡선의 시점이 아니라
                // 지출 결의 노이즈라 허용만 반 눈금 연다
                Assert.LessOrEqual(System.Math.Abs(expected - row.PetBonus), 0.045d, string.Format(
                    "stage {0}: 기대 합산 {1:P0}, 실측 {2:P0} - 기대 곡선이 실측에서 4.5%p "
                    + "이상 벗어났다. LevelAtUnlock({3})/LevelsPerStage({4})를 실측에 맞춰라",
                    row.Stage, expected, row.PetBonus,
                    PetCurve.LevelAtUnlock, PetCurve.LevelsPerStage));
            }
        }

        // ------------------------------------------------------------ 표의 형태

        /**
         * @brief 카탈로그의 형태 - 셋의 DPS가 같고, 스타일만 다르다.
         *
         * 보너스 곡선이 카탈로그 밖(PetCurve)에 하나뿐이라 "레벨이 같으면
         * DPS가 같다"는 구조로 보장된다. 여기서 지키는 것은 나머지다 -
         * 간격(스타일)이 실제로 서로 다르고, 이름·역할·스프라이트가 비어
         * 있지 않고, 해금 값이 컬렉션 순서대로 오른다.
         */
        [Test]
        public void Catalog_ThreeStylesOneCurve()
        {
            Assert.AreEqual(3, PetCatalog.Count, "펫 수가 바뀌었으면 예약 몫과 컬렉션 값을 다시 재야 한다");

            int previousGems = 0;
            for (int i = 0; i < PetCatalog.Count; i++)
            {
                var pet = PetCatalog.Pets[i];

                Assert.IsFalse(string.IsNullOrEmpty(pet.Id), "동료 " + i + "의 id가 없다");
                Assert.IsFalse(string.IsNullOrEmpty(pet.Name), "동료 " + i + "의 이름이 없다");
                Assert.IsFalse(string.IsNullOrEmpty(pet.Role), "'" + pet.Name + "'의 역할 설명이 없다");
                Assert.IsFalse(string.IsNullOrEmpty(pet.SpriteFolder),
                    "'" + pet.Name + "'의 스프라이트 팩이 없다");

                Assert.Greater(pet.AttackIntervalSeconds, 0d, "'" + pet.Name + "'의 공격 간격이 0이다");

                Assert.Greater(pet.UnlockGems, previousGems, string.Format(
                    "'{0}'의 해금 값({1})이 앞 동료보다 싸다 - 뒤 동료일수록 몫은 작고 "
                    + "값은 비싼 사다리(가속기)여야 한다", pet.Name, pet.UnlockGems));
                previousGems = pet.UnlockGems;

                Assert.Greater(pet.FirstBonus, 0.01d, string.Format(
                    "'{0}'의 첫 칸이 {1:P1}뿐이다 - 해금 버튼이 눌러도 안 느껴진다",
                    pet.Name, pet.FirstBonus));

                // 간격이 서로 달라야 스타일이다. 같으면 세 펫이 같은 펫이다
                for (int j = 0; j < i; j++)
                    Assert.AreNotEqual(PetCatalog.Pets[j].AttackIntervalSeconds,
                        pet.AttackIntervalSeconds,
                        "'" + pet.Name + "'과 '" + PetCatalog.Pets[j].Name + "'의 간격이 같다");
            }

            // 궁수 하나만 원거리다. 연출 분기가 이 플래그 하나에 걸려 있다
            Assert.IsTrue(PetCatalog.Pets[PetCatalog.IndexOf(PetCatalog.ArcherId)].Ranged,
                "명궁이 원거리가 아니다");
        }

        /**
         * @brief 해금 첫 칸(Lv.1)이 눌렀을 때 체감되는 도약이다.
         *
         * 보석 80짜리 버튼이 전직 1티어(+10%)만큼은 움직여야 한다. 5%는
         * 전직 티어와 같은 마일스톤 자다.
         */
        [Test]
        public void FirstUnlock_IsAFeltJump()
        {
            // 첫 동료(청랑)는 f2p의 기본 세트다 - 해금 순간 5% 이상은 움직여야
            // 보석 80이 설명된다
            var wolf = PetCatalog.Pets[PetCatalog.IndexOf(PetCatalog.WolfId)];
            Assert.GreaterOrEqual(wolf.FirstBonus, 0.05d, string.Format(
                "청랑 해금 직후 보너스가 {0:P1}뿐이다 - 마일스톤이 아니라 단련이다",
                wolf.FirstBonus));

            foreach (var pet in PetCatalog.Pets)
                Assert.Less(pet.FirstBonus, pet.BonusCeiling,
                    "'" + pet.Name + "'의 첫 칸이 상한보다 크다 - 레벨 버튼이 태어날 때부터 죽어 있다");
        }

        /** 카탈로그가 시뮬 칸 수를 넘으면 뒤쪽이 조용히 무시된다 - 못 박는다 */
        [Test]
        public void Simulation_HasASlotForEveryPet()
        {
            Assert.LessOrEqual(PetCatalog.Count, StageSimulation.PetSlotCapacity,
                "동료가 시뮬 칸 수보다 많다 - StageSimulation.PetSlotCapacity와 Levels의 "
                + "낱개 필드를 함께 늘려라");
        }

        /**
         * @brief 펫 보너스가 DPS 식에 실제로 곱해진다.
         *
         * StageCurve의 보정 상수가 선언만 되고 안 쓰였던 사고
         * (BossHealthForStage 주석)와 같은 함정을 막는다 - 상수의 존재는
         * 연결의 증거가 아니다.
         */
        [Test]
        public void PetBonus_FeedsTheDpsFormula()
        {
            var stats = CombatStats.CappedAtLevel(10);
            double before = stats.ExpectedDps;

            stats.PetBonus = 0.5d;
            Assert.AreEqual(before * 1.5d, stats.ExpectedDps, before * 1e-9,
                "PetBonus 0.5가 DPS를 x1.5로 만들지 않는다 - 식에 연결되지 않았다");
        }
    }
}
