using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 전직(사무라이 진화) 축의 검사 (33단계).
     *
     * 밴드 재유도 자체는 StageSimulationTests의 AcceleratedZone_* 두 검사가
     * 지킨다. 여기는 축 자신을 본다 - 죽은 버튼(사면 달라지는가), 보석의
     * 값어치(무엇이 병목인가), 기대 곡선(보정이 실측을 따라가는가), 그리고
     * 조율 코리더에 이 축이 없다는 구조적 사실.
     */
    public class EvolutionTests
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
         * @brief 조율 코리더(st1~30)에 전직이 없다.
         *
         * 계수가 아니라 구조다 - 해금이 Lv.30이고 곡선 추종 플레이어는 st37에야
         * 도달한다. 이것이 깨지면(경험치 곡선이 빨라지면) 코리더의 밴드 검사가
         * 전직 없이 잡은 값이 아니게 되므로, 그 순간 여기서 걸려야 한다.
         *
         * 보석 정책 양쪽을 다 본다. 어느 쪽 플레이어든 코리더 안에서는 같은
         * 게임이어야 한다.
         */
        [Test]
        public void Evolution_IsAbsentFromTheTunedCorridor()
        {
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
                    Assert.AreEqual(0, row.EvolutionTier, string.Format(
                        "stage {0}: 코리더 안에서 전직 {1}티어가 나왔다 (Lv.{2}). "
                        + "해금 레벨이나 경험치 곡선이 움직였다 - 코리더 밴드를 다시 재야 한다",
                        row.Stage, row.EvolutionTier, row.CharacterLevel));
                }

                // 보정도 함께 1이어야 한다. 축이 없는 구간에 보정만 걸리면
                // 있지도 않은 이득을 상쇄하는 셈이다 - 21단계 골드 축의 사고
                for (int stage = 1; stage <= 30; stage++)
                    Assert.AreEqual(1d, StageCurve.EvolutionCompensation(stage), 1e-12d, string.Format(
                        "stage {0}: 전직이 없는 코리더에 보정 {1:F3}이 걸려 있다",
                        stage, StageCurve.EvolutionCompensation(stage)));
            }
        }

        // ------------------------------------------------------------ 죽은 버튼

        /**
         * @brief 죽은 버튼 검사 (a) - **전직이 실제로 DPS와 진행을 움직이는가.**
         *
         * 16단계 방식. 산 플레이어와 안 산 플레이어를 나란히 돌린다. 세 가지를
         * 함께 본다 - 장부(티어), 값(배수·DPS), 시간(구간 소요).
         *
         * 시간은 **가속 구간(st31~50)만** 잰다. 전직은 st37에야 열리므로 50
         * 스테이지 전체로 재면 앞 30스테이지의 동일한 787초가 분모를 부풀려
         * 실제 이득이 희석된다 - 장비의 4% 기준을 전 구간에 그대로 쓰면 이
         * 축은 아무리 커도 통과하지 못하거나, 반대로 기준을 낮추면 "이 축은
         * 원래 작다"가 변명이 된다. 자(4%)는 같고 재는 구간만 축이 사는
         * 구간이다.
         */
        [Test]
        public void Evolution_MovesDpsAndProgress()
        {
            var field = FieldFromAssets();

            var with = StageSimulation.Run(50, field);
            var without = StageSimulation.Run(50, field,
                new StageSimulation.Policy { SkipEvolution = true });

            var end = with[49];

            // 1. 장부 - 사다리를 실제로 끝까지 오른다
            Assert.AreEqual(EvolutionCurve.MaxTier, end.EvolutionTier, string.Format(
                "보석 무제한인데 50스테이지까지 {0}티어에서 멈췄다 - 골드 값이 "
                + "기대 스테이지의 지갑 스파이크보다 크다", end.EvolutionTier));

            // 2. 값 - 배수가 실제로 스탯에 도달한다
            Assert.Greater(end.EvolutionAttack, 1.9d, string.Format(
                "6티어의 공격 배수가 x{0:F2}뿐이다", end.EvolutionAttack));
            Assert.Greater(end.ExpectedDps, without[49].ExpectedDps,
                "전직을 다 올렸는데 DPS가 안 올린 쪽보다 낮다");
            Assert.Greater(end.MaxHealth, without[49].MaxHealth,
                "전직 체력 배수가 최대 체력에 도달하지 않았다");

            // 3. 시간 - 가속 구간이 실제로 빨라진다. 기준 4%는 20단계 골드 축
            //    -> 32단계 장비와 같은 자다
            double withSeconds = SegmentSeconds(with, 31, 50);
            double withoutSeconds = SegmentSeconds(without, 31, 50);
            double gain = 1d - withSeconds / withoutSeconds;

            Assert.GreaterOrEqual(gain, 0.04d, string.Format(
                "전직을 산 플레이어의 st31~50이 {0:F0}초, 안 산 플레이어가 {1:F0}초로 "
                + "이득이 {2:P1}뿐이다. 이 버튼은 죽었다 - 티어 배수를 키우거나 "
                + "EvolutionMarginExponent를 낮춰라",
                withSeconds, withoutSeconds, gain));
        }

        /**
         * @brief 죽은 버튼 검사 (b) - **보석이 실제 병목인가.**
         *
         * 장비의 Gems_AreTheBindingConstraintNotDeadWeight와 같은 양면 검사다.
         * 보석 하한 플레이어(일일 퀘스트 0회)가:
         *
         *   한 티어도 못 오르면      너무 비싸다 - 무과금에게 사다리가 광고판조차
         *                          못 된다 (첫 진화가 스프라이트 교체를 보여주는
         *                          자리다)
         *   사다리를 다 오르면      너무 싸다 - 일일 보석과 과금이 살 것이 없다
         *
         * 사이 어딘가에서 멈추고, 다음 티어의 보석 값이 잔액보다 커야 한다 -
         * 그 거리가 일일 퀘스트와 보석 상품의 값어치다.
         */
        [Test]
        public void Gems_AreTheBindingConstraintForTheFloorPlayer()
        {
            var policy = new StageSimulation.Policy { GemsFromQuestsOnly = true };
            var end = StageSimulation.Run(50, FieldFromAssets(), policy)[49];

            Assert.GreaterOrEqual(end.EvolutionTier, 1,
                "무과금이 50스테이지까지 한 번도 진화하지 못했다 - 1티어 보석 값을 낮춰라");

            Assert.Less(end.EvolutionTier, EvolutionCurve.MaxTier, string.Format(
                "무과금이 사다리를 다 올랐다 ({0}티어) - 보석이 죽은 재화다. 티어 값을 올려라",
                end.EvolutionTier));

            int nextCost = EvolutionCurve.GemCost(end.EvolutionTier);
            int available = end.GemsEarned - end.GemsSpent;
            Assert.Less(available, nextCost, string.Format(
                "잔액 {0}개로 다음 티어({1}개)를 살 수 있는데 안 샀다 - 병목이 보석이 아니라 "
                + "골드라는 뜻이고, 그러면 이 재화의 값어치 계산이 전부 틀린다",
                available, nextCost));
        }

        // ------------------------------------------------------------ 기대 곡선

        /**
         * @brief 보정의 기대 곡선이 시뮬레이션 실측을 따라간다.
         *
         * 장비의 ExpectedCurve_TracksTheSimulation과 같은 자리다. 닫힌 식
         * (FirstTierStage + StagesPerTier)이 실측과 어긋나면 보정이 실제보다
         * 가볍거나 무겁게 걸리고, 그 오차는 화면이 아니라 밴드에만 나타난다 -
         * 장비에서 절편을 1로 뒀다가 st21~26이 천장을 넘은 사고 그대로다.
         */
        [Test]
        public void ExpectedCurve_TracksTheSimulation()
        {
            var results = StageSimulation.Run(50, FieldFromAssets());

            foreach (var row in results)
            {
                int expected = EvolutionCurve.ExpectedTierAtStage(row.Stage);

                Assert.LessOrEqual(System.Math.Abs(expected - row.EvolutionTier), 1, string.Format(
                    "stage {0}: 기대 {1}티어, 실측 {2}티어 - 기대 곡선이 실측에서 두 칸 "
                    + "이상 벗어났다. FirstTierStage({3})/StagesPerTier({4})를 실측에 맞춰라",
                    row.Stage, expected, row.EvolutionTier,
                    EvolutionCurve.FirstTierStage, EvolutionCurve.StagesPerTier));
            }
        }

        // ------------------------------------------------------------ 표의 형태

        /**
         * @brief 모든 티어가 눌렀을 때 체감되는 도약이다.
         *
         * 16단계의 MinimumFeltGain(1%)이 아니라 **5%**를 쓴다. 전직은 강화
         * 한 칸이 아니라 마일스톤이고, 보석 수백 개짜리 버튼이 5%도 안 움직이면
         * 그 값이 설명되지 않는다.
         */
        [Test]
        public void EveryTier_IsAFeltJump()
        {
            Assert.AreEqual(6, EvolutionCurve.MaxTier, "티어 수가 바뀌었으면 밴드 몫도 다시 재야 한다");

            for (int t = 0; t < EvolutionCatalog.Count; t++)
            {
                var spec = EvolutionCatalog.Tiers[t];

                Assert.GreaterOrEqual(spec.AttackStep, 1.05d, string.Format(
                    "'{0}'의 공격 도약이 {1:P1}뿐이다 - 마일스톤이 아니라 단련이다",
                    spec.Name, spec.AttackStep - 1d));
                Assert.GreaterOrEqual(spec.HealthStep, 1.01d,
                    "'" + spec.Name + "'의 체력 도약이 1% 미만이다");

                Assert.IsFalse(string.IsNullOrEmpty(spec.Name), "티어 " + (t + 1) + "의 이름이 없다");
                Assert.IsFalse(string.IsNullOrEmpty(spec.SpriteFolder),
                    "'" + spec.Name + "'의 스프라이트 팩이 없다");
            }

            // 보석 값이 단조 증가한다. 뒤 티어가 앞 티어보다 싸면 도약형 비용의
            // "비싼 칸이 더 크게 뛴다"가 깨진다
            for (int t = 1; t < EvolutionCatalog.Count; t++)
                Assert.Greater(EvolutionCatalog.Tiers[t].GemCost, EvolutionCatalog.Tiers[t - 1].GemCost,
                    "'" + EvolutionCatalog.Tiers[t].Name + "'의 보석 값이 이전 티어보다 싸다");

            // 마지막 티어만 Aura다. 중간에 Aura가 있으면 최종의 서명이 아니게 된다
            for (int t = 0; t < EvolutionCatalog.Count; t++)
                Assert.AreEqual(t == EvolutionCatalog.Count - 1, EvolutionCatalog.Tiers[t].Aura,
                    "'" + EvolutionCatalog.Tiers[t].Name + "'의 Aura 플래그가 자리에 맞지 않는다");
        }

        /**
         * @brief 상한 밖에서 값이 이어지지 않는다 - 경계값.
         */
        [Test]
        public void CurveEdges_AreClamped()
        {
            Assert.AreEqual(1d, EvolutionCurve.AttackMultiplierAt(-1), 1e-12d);
            Assert.AreEqual(EvolutionCurve.AttackMultiplierAt(EvolutionCurve.MaxTier),
                EvolutionCurve.AttackMultiplierAt(EvolutionCurve.MaxTier + 5), 1e-12d,
                "상한 위의 티어가 더 큰 배수를 낸다 - 세이브 손상이 스탯이 된다");

            Assert.AreEqual(0, EvolutionCurve.GemCost(EvolutionCurve.MaxTier),
                "마지막 티어 위의 보석 값이 0이 아니다");
            Assert.AreEqual(0d, EvolutionCurve.GoldCost(-1), 1e-12d);

            Assert.IsFalse(EvolutionCurve.CanEvolve(EvolutionCurve.MaxTier));
            Assert.IsTrue(EvolutionCurve.CanEvolve(0));

            // 해금 경계
            Assert.IsFalse(EvolutionCurve.IsUnlockedAt(EvolutionCurve.UnlockLevel - 1));
            Assert.IsTrue(EvolutionCurve.IsUnlockedAt(EvolutionCurve.UnlockLevel));
        }

        /**
         * @brief 사다리 전체의 보석 총합이 설계값이다.
         *
         * 3,010 = 업적 전체(445)의 약 일곱 배 = 일일 보석(55/일) 기준 약 두 달.
         * 이 총합이 움직이면 gem sink의 크기가 움직인 것이므로 여기 숫자도
         * 의도적으로 함께 고쳐야 한다.
         */
        [Test]
        public void GemLadder_TotalIsIntentional()
        {
            Assert.AreEqual(3010, EvolutionCurve.TotalGems,
                "전직 사다리의 보석 총합이 바뀌었다 - 보고서와 일일 보석 환산도 함께 고쳐라");
        }
    }
}
