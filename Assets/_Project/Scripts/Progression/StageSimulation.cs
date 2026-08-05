using System;
using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 곡선을 그대로 돌려 진행 시간과 보스 난이도를 낸다.
     *
     * 이것이 런타임 코드로 존재하는 이유는 9단계에서 겪은 일 때문이다. 보고서의
     * 진행 시간 계산을 일회용 스크립트로 짰는데, 그 스크립트가 쓴 상수가 실제 게임의
     * 값과 같다는 보장이 어디에도 없었다. 실제로 계산은 "1스테이지 보스는 무강화로도
     * 클리어"라고 했는데 플레이 화면은 실패를 보여줬고, 둘 중 어느 쪽이 맞는지
     * 판단할 근거가 없었다.
     *
     * (그때의 원인은 계산이 아니라 화면 쪽이었다 - 제한 시간을 강제로 소진시킨
     * 스크린샷이었다. 하지만 그것을 확인하는 데 든 비용이 이 파일이 존재해야 하는
     * 이유다.)
     *
     * 이제 계산은 여기 하나뿐이고, StageSimulationTests가 이 안의 값들이 실제
     * 에셋·트랙·전투와 일치하는지 검사한다.
     *
     * **모델링하는 것**: 치명타 기대값(CombatBaseline), 요괴 공급 하한, 보스가
     * 걸어 들어오는 동안 제한 시간이 흐르는 것.
     *
     * 앞의 두 개는 처음에 빠져 있었고, 마침 서로 반대 방향으로 비슷한 크기라
     * 결과가 우연히 맞았다. 우연히 맞는 계산은 다음 계수 변경에서 조용히 틀린다.
     *
     * **모델링하지 않는 것**: 히트스톱(제한 시간이 스케일 타임이라 정지한 만큼은
     * 시계도 멈춘다 - 계산에 넣을 필요가 없다), 처치 순간의 이동/큐 재정렬.
     */
    public static class StageSimulation
    {
        /** 한 스테이지의 결과 한 줄 */
        public struct StageResult
        {
            public int Stage;
            public double MobSeconds;
            public double BossKillSeconds;
            public bool BossCleared;
            public int AttackPowerLevel;
            public int AttackSpeedLevel;
            public double Damage;
            public double AttacksPerSecond;
        }

        public struct Field
        {
            /** 스폰 가중치로 평균 낸 1스테이지 기준 잡몹 체력/골드 */
            public double AverageMobHealth;
            public double AverageMobGold;

            /**
             * @brief 요괴 보충 간격 (초).
             *
             * 처치 간격의 하한이다. DPS가 아무리 높아도 다음 요괴가 오지 않으면
             * 때릴 것이 없다. 후반에는 이쪽이 실제 상한이 된다.
             */
            public double SpawnInterval;
        }

        /**
         * @brief 기대 DPS. 치명타를 포함한다.
         *
         * 치명타를 빼면 실제보다 12% 낮게 나온다. 보스전은 제한 시간 판정이라
         * 그 12%가 통과와 실패를 가르는 구간이 실제로 존재한다.
         */
        public static double ExpectedDps(double damage, double attacksPerSecond)
        {
            return damage * attacksPerSecond * CombatBaseline.ExpectedDamageMultiplier;
        }

        /** 지금 스탯으로 이 체력을 깎는 데 걸리는 시간 */
        public static double SecondsToKill(double health, double damage, double attacksPerSecond)
        {
            double dps = ExpectedDps(damage, attacksPerSecond);
            if (dps <= 0d) return double.PositiveInfinity;
            return health / dps;
        }

        /**
         * @brief 이 스테이지의 보스를 제한 시간 안에 잡을 수 있는가.
         *
         * 체력 계산은 StageCurve.BossHealthForStage를 그대로 쓴다. BossFight가
         * 실제로 스폰할 때 부르는 것과 같은 함수다.
         */
        public static double BossKillSeconds(double averageMobHealth, int stage,
                                             double damage, double attacksPerSecond)
        {
            var health = StageCurve.BossHealthForStage(BigDouble.FromDouble(averageMobHealth), stage);
            return SecondsToKill(health.ToDouble(), damage, attacksPerSecond);
        }

        /**
         * @brief 제한 시간 중 실제로 때릴 수 있는 시간.
         *
         * 시계는 보스가 스폰되는 순간 시작하지만 보스는 화면 밖에서 걸어 들어온다.
         * 그 5.3초 동안 사무라이의 사거리에는 아무도 없다. 제한 시간 30초를 그대로
         * 쓰면 실제보다 후한 판정이 나온다.
         *
         * 이것을 시계 쪽에서 고치지 않고 계산 쪽에서 반영하는 이유는, 걸어 들어오는
         * 장면이 연출이기도 하기 때문이다. 시계가 보스 도착 후에 시작하면 등장이
         * 공짜가 되어 플레이어가 그 시간을 기다림으로만 느낀다.
         */
        public static double BossDamageWindowSeconds
        {
            get { return StageCurve.BossTimeLimitSeconds - BossWalkInSeconds; }
        }

        public static bool BossClears(double averageMobHealth, int stage,
                                      double damage, double attacksPerSecond)
        {
            return BossKillSeconds(averageMobHealth, stage, damage, attacksPerSecond)
                   <= BossDamageWindowSeconds;
        }

        /**
         * @brief 시작 스탯 그대로(강화 없음)의 DPS.
         *
         * 게이트가 언제부터 무는지를 재는 기준선이다.
         */
        public static double StartingDamage { get { return AttackPowerCurve.ValueAtLevel(1); } }
        public static double StartingAttacksPerSecond { get { return AttackSpeedCurve.CappedValueAtLevel(1); } }

        /**
         * @brief 강화를 전혀 하지 않은 플레이어가 처음 실패하는 스테이지.
         *
         * 1이면 첫 보스부터 벽이라 온보딩이 끊기고, 너무 크면 게이트가 한참 동안
         * 아무 일도 하지 않는다.
         */
        public static int FirstStageThatBlocksAnUnupgradedPlayer(double averageMobHealth, int searchTo)
        {
            for (int stage = 1; stage <= searchTo; stage++)
            {
                if (!BossClears(averageMobHealth, stage, StartingDamage, StartingAttacksPerSecond))
                    return stage;
            }
            return -1;
        }

        /**
         * @brief 1스테이지부터 throughStage까지를 돌린다.
         *
         * 구매 정책은 "골드가 되는 대로 지금 더 싼 축을 산다"이다. 두 축의 골드당
         * 효율이 같은 레벨에서 1.20배로 일정하므로(UpgradeEfficiency), 실제 플레이도
         * 이렇게 번갈아 오른다.
         */
        public static List<StageResult> Run(int throughStage, Field field)
        {
            var results = new List<StageResult>();

            int powerLevel = 1;
            int speedLevel = 1;
            double purse = 0d;

            for (int stage = 1; stage <= throughStage; stage++)
            {
                double mobHealth = field.AverageMobHealth * StageCurve.HealthMultiplier(stage).ToDouble();
                double goldPerMob = field.AverageMobGold * StageCurve.GoldMultiplier(stage).ToDouble();

                double mobSeconds = 0d;
                for (int k = 0; k < StageCurve.KillsPerStage; k++)
                {
                    double kill = SecondsToKill(mobHealth,
                        AttackPowerCurve.ValueAtLevel(powerLevel),
                        AttackSpeedCurve.CappedValueAtLevel(speedLevel));

                    // 요괴 공급이 하한이다. 아무리 세도 다음 요괴를 기다려야 한다
                    mobSeconds += Math.Max(kill, field.SpawnInterval);

                    purse += goldPerMob;
                    Buy(ref powerLevel, ref speedLevel, ref purse);
                }

                double damage = AttackPowerCurve.ValueAtLevel(powerLevel);
                double aps = AttackSpeedCurve.CappedValueAtLevel(speedLevel);
                double bossKill = BossKillSeconds(field.AverageMobHealth, stage, damage, aps);

                purse += goldPerMob * StageCurve.BossGoldMultiplier;
                Buy(ref powerLevel, ref speedLevel, ref purse);

                results.Add(new StageResult
                {
                    Stage = stage,
                    MobSeconds = mobSeconds,
                    BossKillSeconds = bossKill,
                    BossCleared = bossKill <= BossDamageWindowSeconds,
                    AttackPowerLevel = powerLevel,
                    AttackSpeedLevel = speedLevel,
                    Damage = damage,
                    AttacksPerSecond = aps
                });
            }

            return results;
        }

        /**
         * @brief 총 소요 시간. 보스의 등장 연출과 걸어 들어오는 시간을 포함한다.
         *
         * 연출 시간을 여기 넣는 이유는, 그것이 스테이지마다 반드시 드는 고정 비용이라
         * 후반에서 무시할 수 없는 비중이 되기 때문이다. 5스테이지쯤이면 보스 처치
         * 자체보다 걸어 들어오는 시간이 더 길다.
         */
        public const double BossIntroSeconds = 1d;

        /** 화면 밖에서 큐 앞줄까지 걸어오는 시간. 이동 속도 0.85로 약 4.5 units */
        public const double BossWalkInSeconds = 5.3d;

        public static double TotalSeconds(List<StageResult> results)
        {
            double total = 0d;
            foreach (var row in results)
                total += row.MobSeconds + BossIntroSeconds + BossWalkInSeconds + row.BossKillSeconds;
            return total;
        }

        private static void Buy(ref int powerLevel, ref int speedLevel, ref double purse)
        {
            // 무한 루프 방어. 비용이 0이 되는 곡선이 들어오면 여기서 멈춘다
            for (int guard = 0; guard < 100000; guard++)
            {
                double powerCost = AttackPowerCurve.CostAtLevel(powerLevel);
                double speedCost = AttackSpeedCurve.CostAtLevel(speedLevel);
                bool speedBuyable = speedLevel < AttackSpeedCurve.MaxLevel;

                if (speedBuyable && speedCost <= powerCost && speedCost <= purse)
                {
                    purse -= speedCost;
                    speedLevel++;
                    continue;
                }

                if (powerCost <= purse)
                {
                    purse -= powerCost;
                    powerLevel++;
                    continue;
                }

                break;
            }
        }
    }
}
