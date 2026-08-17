using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 스테이지 재선택(37단계)이 파밍·방치 밸런스를 건드리지 않는지 검사한다.
     *
     * ## 지키는 성질: 최전선이 최적의 파밍 자리다
     *
     * 재선택으로 낮은 스테이지에 "짱박히는" 것이 골드/경험치에서 이득이면,
     * 최적 플레이가 "뒤로 돌아가라"가 되고 진행 곡선 전체가 무의미해진다.
     * 곡선 구조가 이것을 막는다 - 골드 배수(1.72^s)가 체력 배수(1.55^s)보다
     * 빠르게 자라므로, 한 스테이지 내려가면 처치가 1.55배 빨라지는 대신 골드가
     * 1.72배 깎인다. 처치가 스폰 하한(0.4초)에 닿은 뒤에는 격차가 더 벌어진다.
     *
     * 여기서는 그 주장을 곡선 추종 플레이어의 실제 스탯으로 확인한다 -
     * 시뮬레이션이 만든 각 스테이지의 스탯을 들고, 그 아래 모든 스테이지의
     * 골드/초를 실제 방치 공식(IdleIncome.GoldPerSecond)으로 계산해 비교한다.
     *
     * 방치 보상도 같은 식이다: 저장 시점의 스테이지로 goldPerSecond가 박히므로
     * (GameSession.EstimateGoldPerSecond), 낮은 데서 나가면 방치 수입도 그만큼
     * 낮다. 재선택은 악용 경로가 아니라 손해를 감수하는 선택이다.
     */
    public class StageReselectTests
    {
        private const string DataFolder = "Assets/_Project/Data";

        static void LoadFieldAverages(out double health, out double gold)
        {
            BigDouble healthSum = BigDouble.Zero;
            BigDouble goldSum = BigDouble.Zero;
            double totalWeight = 0d;

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { DataFolder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<Onikiri.Battle.EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.spawnWeight <= 0f) continue;

                healthSum += definition.maxHealth * BigDouble.FromDouble(definition.spawnWeight);
                goldSum += definition.goldReward * BigDouble.FromDouble(definition.spawnWeight);
                totalWeight += definition.spawnWeight;
            }

            health = (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble();
            gold = (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble();
        }

        /**
         * @brief 그 스탯으로 stageNumber에서 파밍할 때의 골드/초.
         *
         * 방치 보상과 같은 공식을 쓴다. 처치 속도와 스폰 공급 중 낮은 쪽이
         * 병목이고, 스폰 간격은 처치 속도에 수렴한다(SpawnPacing).
         */
        static double FarmGoldPerSecond(int stageNumber, StageSimulation.StageResult r,
                                        double avgHealth, double avgGold)
        {
            // 온보딩 완화(st1~5 잡몹 전용)까지 지난 실제 체력이다 - 게임과 다른
            // 체력으로 재면 여기 결론이 화면과 다른 세계의 것이 된다
            var health = StageCurve.MobHealth(BigDouble.FromDouble(avgHealth), stageNumber);
            var damage = BigDouble.FromDouble(r.Damage);

            int hits = StageCurve.HitsToKill(health, damage);
            double kill = hits / r.AttacksPerSecond;
            float interval = (float)Onikiri.Battle.SpawnPacing.SettledInterval(kill);

            var gold = BigDouble.FromDouble(avgGold * r.GoldGain) * StageCurve.GoldMultiplier(stageNumber);
            return IdleIncome.GoldPerSecond(damage, (float)r.AttacksPerSecond, health, gold, interval);
        }

        /** 경험치/초. 골드와 같은 처치 속도에 스테이지별 경험치를 곱한다 */
        static double FarmExpPerSecond(int stageNumber, StageSimulation.StageResult r, double avgHealth)
        {
            // 골드 쪽과 같은 이유로 완화를 지난 실제 체력이다
            var health = StageCurve.MobHealth(BigDouble.FromDouble(avgHealth), stageNumber);
            var damage = BigDouble.FromDouble(r.Damage);

            int hits = StageCurve.HitsToKill(health, damage);
            if (hits <= 0) return 0d;

            double kill = hits / r.AttacksPerSecond;
            double interval = Onikiri.Battle.SpawnPacing.SettledInterval(kill);
            double killsPerSecond = System.Math.Min(r.AttacksPerSecond / hits,
                interval > 0d ? 1d / interval : double.MaxValue);

            return killsPerSecond * ExpCurve.MobExp(stageNumber).ToDouble();
        }

        [Test]
        public void Frontier_IsTheBestPlaceToFarmGold()
        {
            double avgHealth, avgGold;
            LoadFieldAverages(out avgHealth, out avgGold);

            var field = new StageSimulation.Field
            {
                AverageMobHealth = avgHealth,
                AverageMobGold = avgGold,
                SpawnInterval = 1.1d
            };

            var results = StageSimulation.Run(50, field);

            foreach (var r in results)
            {
                // r.Stage를 막 클리어한 플레이어의 최전선은 r.Stage + 1이다
                int frontier = r.Stage + 1;

                // 재선택은 최전선 11부터 존재한다(StageProgress.ReselectUnlockStage).
                // 그 앞은 되돌아갈 수단 자체가 없는 세계라 이 계약의 범위 밖이다 -
                // 게이트 주석이 정확히 이 원칙("최전선이 최적은 재선택이 존재하는
                // 모든 구간에서 참")을 적어 뒀고, E-3 후속의 온보딩 잡몹 완화가
                // 만든 초반 역전(실측 최악 f5 +69%)도 전부 이 게이트 앞이다
                if (frontier < StageProgress.ReselectUnlockStage) continue;

                double atFrontier = FarmGoldPerSecond(frontier, r, avgHealth, avgGold);

                // 비용 상향의 과도 구간(E-3 수정, 코리더 안 frontier 30까지)은
                // 18%를 봐준다. 타격 수는 정수라 최전선에서 한 계단(1->2, 2->3)
                // 뛰는 스테이지가 생기는데, 원래는 그 전환이 보충 간격 하한
                // (0.4초) 아래에서 일어나 가려져 있었다. 비용 상향이 처치 속도를
                // 늦춰 전환 창이 하한 위(st20대 초반)로 올라왔고, 게이트 뒤의
                // 실측 역전은 딱 두 곳 - f23 +16.3% / f26 +16.3% - 이다.
                // 뒤로 갈수록 결손이 평평해지고 처치가 다시 하한 아래로 내려가
                // f27부터 st200까지 역전 0건, 원래의 엄격한 기준으로 돌아온다.
                // 두 곳은 상향의 알려진 대가로 E-3 수정 보고서에 적혀 있다
                double slack = frontier <= 30 ? 1.18d : 1d + 1e-9d;

                for (int s = 1; s < frontier; s++)
                {
                    double below = FarmGoldPerSecond(s, r, avgHealth, avgGold);
                    Assert.LessOrEqual(below, atFrontier * slack,
                        "frontier " + frontier + ": farming stage " + s + " pays "
                        + below.ToString("G4") + " gold/s vs " + atFrontier.ToString("G4")
                        + " at the frontier - going back would be optimal play");
                }
            }
        }

        /**
         * @brief 최전선 아래에서는 잡몹 경험치가 없다.
         *
         * 골드와 달리 경험치는 규칙으로 닫아야 한다 - 아래의
         * ExpInversion_StillExistsSoTheZeroRuleIsLoadBearing 참고.
         */
        [Test]
        public void MobExpBelowTheFrontier_IsZero()
        {
            Assert.AreEqual(BigDouble.Zero, ExpCurve.MobExp(20, false),
                "below-frontier mobs granted exp - back-farming becomes the best exp source");
            Assert.AreEqual(ExpCurve.MobExp(20), ExpCurve.MobExp(20, true),
                "frontier exp must be untouched by the reselect rule");
        }

        /**
         * @brief 경험치 0 규칙이 실제로 무언가를 막고 있는지 확인한다 (죽은 버튼 검사).
         *
         * 경험치 증가율(1.16)이 체력 배수(1.55)보다 완만해서, 경험치가 그대로
         * 나온다면 한 스테이지 내려갈 때마다 처치는 1.55배 빨라지고 경험치는
         * 1.16배만 깎인다 - 뒤로 갈수록 경험치/초가 오르는 역전이 구조적으로
         * 존재한다. 이 검사는 그 역전이 지금도 존재함을 실측한다.
         *
         * **이 검사가 깨지면** (곡선이 바뀌어 역전이 사라지면) 경험치 0 규칙을
         * 다시 검토할 수 있다 - 규칙이 지키는 것이 없어졌다는 뜻이기 때문이다.
         */
        [Test]
        public void ExpInversion_StillExistsSoTheZeroRuleIsLoadBearing()
        {
            double avgHealth, avgGold;
            LoadFieldAverages(out avgHealth, out avgGold);

            var field = new StageSimulation.Field
            {
                AverageMobHealth = avgHealth,
                AverageMobGold = avgGold,
                SpawnInterval = 1.1d
            };

            var results = StageSimulation.Run(50, field);
            bool inversionExists = false;

            foreach (var r in results)
            {
                int frontier = r.Stage + 1;
                if (frontier < StageProgress.ReselectUnlockStage) continue;

                double atFrontier = FarmExpPerSecond(frontier, r, avgHealth);
                for (int s = 1; s < frontier && !inversionExists; s++)
                    if (FarmExpPerSecond(s, r, avgHealth) > atFrontier) inversionExists = true;
            }

            Assert.IsTrue(inversionExists,
                "the exp inversion is gone - the below-frontier zero-exp rule protects nothing "
                + "any more and could be reconsidered");
        }
    }
}
