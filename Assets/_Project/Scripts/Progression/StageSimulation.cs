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

            /** 잡몹 10마리에 걸린 총 시간 */
            public double MobSeconds;

            /** 스테이지 끝 시점의 잡몹 한 마리 처치 시간 */
            public double MobKillSeconds;

            /** 그때 수렴한 보충 간격. 하한 0.4초에 닿았는지 보는 값 */
            public double SpawnInterval;

            public double BossKillSeconds;
            public bool BossCleared;

            /** 때릴 수 있는 시간 / 실제 처치 시간. 1.5~3.0 밴드가 목표다 */
            public double BossMargin;

            public int AttackPowerLevel;
            public int AttackSpeedLevel;
            public int CritRateLevel;
            public int CritDamageLevel;

            public double Damage;
            public double AttacksPerSecond;
            public double CritRate;
            public double CritMultiplier;
            public double ExpectedDps;

            public int HealthLevel;
            public int RegenLevel;
            public double MaxHealth;
            public double RegenPerSecond;

            /** 유효체력 / 이 보스가 제한 시간 동안 낼 피해. 1 아래면 죽는다 */
            public double SurvivalMargin;
            public bool Survived;

            /** 이 스테이지가 챕터 보스인가 */
            public bool IsChapterBoss;

            // ------------------------------------------------------------ 12단계

            /** 이 스테이지를 끝냈을 때의 캐릭터 레벨 */
            public int CharacterLevel;

            /** 이 스테이지에서 오른 레벨 수 */
            public int LevelsGained;

            public int AttackPoints;
            public int HealthPoints;

            /**
             * @brief 이 스테이지에서 한 레벨을 올리는 데 걸린 평균 시간.
             *
             * 목표 리듬을 재는 값이다. 초반 30초 안팎에서 시작해 후반으로 갈수록
             * 늘어나야 한다 - 레벨이 골드보다 느리게 자라야 하기 때문이다.
             * 이 스테이지에서 한 번도 안 올랐으면 무한대다.
             */
            public double SecondsPerLevel;

            /** 스탯 포인트가 곱하고 있는 배수. 골드 축과 얼마나 벌어졌는지 보는 값 */
            public double AttackAmp;
            public double HealthAmp;

            // ------------------------------------------------------------ 20단계

            public int GoldGainLevel;

            /** 골드 보상에 곱해지고 있는 배수 */
            public double GoldGain;

            /** 이 스테이지의 파밍 속도. 회수 시간의 분모다 */
            public double GoldPerSecond;

            /** 지금 레벨에서 한 칸 더 살 때의 회수 시간 (초). 밴드를 재는 값 */
            public double GoldGainPaybackSeconds;
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
        public static double ExpectedDps(CombatStats stats)
        {
            return stats.ExpectedDps;
        }

        /** 지금 스탯으로 이 체력을 깎는 데 걸리는 시간 */
        public static double SecondsToKill(double health, CombatStats stats)
        {
            double dps = stats.ExpectedDps;
            if (dps <= 0d) return double.PositiveInfinity;
            return health / dps;
        }

        /**
         * @brief 이 스테이지의 보스를 제한 시간 안에 잡을 수 있는가.
         *
         * 체력 계산은 StageCurve.BossHealthForStage를 그대로 쓴다. BossFight가
         * 실제로 스폰할 때 부르는 것과 같은 함수다.
         */
        public static double BossKillSeconds(double averageMobHealth, int stage, CombatStats stats)
        {
            var health = StageCurve.BossHealthForStage(BigDouble.FromDouble(averageMobHealth), stage);
            return SecondsToKill(health.ToDouble(), stats);
        }

        /**
         * @brief 제한 시간 중 실제로 때릴 수 있는 시간. **제한 시간 전부다.**
         *
         * 16단계까지는 여기서 워크인 5.3초를 뺐다. 시계가 보스 스폰과 함께
         * 돌기 시작하는데 보스는 화면 밖에서 걸어 들어와서, 그 5.3초 동안
         * 사거리에 아무도 없었기 때문이다 - 제한 시간의 18%가 기다림이었다.
         *
         * 17단계에서 그것을 **시계 쪽에서** 고쳤다. 이제 플레이어가 보스에게
         * 달려가고(연출), 도달한 순간부터 30초가 시작한다. 달려가는 구간은
         * 타이머 밖이므로 뺄 것이 없다.
         *
         * 16단계 주석은 "시계가 보스 도착 후에 시작하면 등장이 공짜가 되어
         * 플레이어가 그 시간을 기다림으로만 느낀다"고 적었는데, 그 전제가
         * 바뀌었다. 기다리는 것이 아니라 **달려가는 것**이면 그 시간은 대기가
         * 아니라 전진이다.
         */
        public static double BossDamageWindowSeconds
        {
            get { return StageCurve.BossTimeLimitSeconds; }
        }

        public static bool BossClears(double averageMobHealth, int stage, CombatStats stats)
        {
            return BossKillSeconds(averageMobHealth, stage, stats) <= BossDamageWindowSeconds;
        }

        /**
         * @brief 시작 스탯 그대로(강화 없음)의 DPS.
         *
         * 게이트가 언제부터 무는지를 재는 기준선이다.
         */
        public static CombatStats StartingStats { get { return CombatStats.CappedAtLevel(1); } }

        public static double StartingDamage { get { return StartingStats.Damage; } }
        public static double StartingAttacksPerSecond { get { return StartingStats.AttacksPerSecond; } }

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
                if (!BossClears(averageMobHealth, stage, StartingStats))
                    return stage;
            }
            return -1;
        }

        /** 강화를 전혀 하지 않은 플레이어의 유효체력 */
        public static double StartingEffectiveHealth
        {
            get
            {
                return SurvivalEfficiency.EffectiveHealth(
                    HealthCurve.ValueAtLevel(1), HealthRegenCurve.ValueAtLevel(1));
            }
        }

        /**
         * @brief 강화를 전혀 하지 않은 플레이어가 처음 **죽는** 스테이지.
         *
         * 시간 초과로 막히는 스테이지와 다른 값이다. 둘이 같으면 체력 축이
         * 아무 일도 하지 않는다는 뜻이고, 체력 게이트가 화력 게이트보다 한참
         * 뒤에 오면 생존 축을 살 이유가 늦게 생긴다.
         */
        public static int FirstStageThatKillsAnUnupgradedPlayer(int searchTo)
        {
            double ehp = StartingEffectiveHealth;

            for (int stage = 1; stage <= searchTo; stage++)
            {
                double incoming = BossCurve.TotalDamageOverFight(stage, StageCurve.BossTimeLimitSeconds);
                if (ehp < incoming) return stage;
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

            var levels = new Levels();
            double purse = 0d;

            for (int stage = 1; stage <= throughStage; stage++)
            {
                double mobHealth = field.AverageMobHealth * StageCurve.HealthMultiplier(stage).ToDouble();
                double rawGoldPerMob = field.AverageMobGold * StageCurve.GoldMultiplier(stage).ToDouble();
                double expPerMob = ExpCurve.MobExp(stage).ToDouble();

                double mobSeconds = 0d;
                double lastKill = 0d;
                double lastInterval = 0d;
                double lastGoldPerSecond = 0d;
                int levelsGained = 0;

                for (int k = 0; k < StageCurve.KillsPerStage; k++)
                {
                    double kill = SecondsToKill(mobHealth, levels.Stats);

                    // 보충 간격은 고정이 아니라 처치 속도에 수렴한다. 그래서 처치가
                    // 빨라지면 파밍 시간도 함께 줄어든다 - 9단계에서 11.0초에
                    // 고정되던 지점이 여기다. 하한 0.4초. SpawnPacing 참고
                    double interval = SpawnPacing.SettledInterval(kill);
                    double seconds = Math.Max(kill, interval);
                    mobSeconds += seconds;

                    lastKill = kill;
                    lastInterval = interval;

                    // 획득 축이 곱해진 실제 수령액. 루프 안에서 매번 다시 읽는
                    // 이유는 바로 아래 Buy가 이 축의 레벨을 올릴 수 있기 때문이다
                    double goldPerMob = rawGoldPerMob * levels.GoldGain;
                    purse += goldPerMob;

                    // 회수 시간의 분모. 파밍 한 마리에 걸린 시간으로 나눈 것이
                    // 이 시점의 초당 골드다 - 게임 쪽 IdleIncome.GoldPerSecond가
                    // 처치 속도와 공급 하한 중 낮은 쪽을 쓰는 것과 같은 값이다
                    lastGoldPerSecond = seconds > 0d ? goldPerMob / seconds : 0d;

                    // 경험치는 골드와 같은 자리에서 들어온다. 게임에서도 처치
                    // 하나가 둘 다 준다(EnemySpawner.OnEnemyKilled)
                    levelsGained += levels.GainExp(expPerMob);
                    Buy(ref levels, ref purse, stage, lastGoldPerSecond, stage);
                }

                var stats = levels.Stats;
                double bossKill = BossKillSeconds(field.AverageMobHealth, stage, stats);

                // 이 보스가 제한 시간을 다 쓰면 낼 총 피해. 유효체력이 이보다
                // 작으면 시간이 다 되기 전에 죽는다
                double incoming = BossCurve.TotalDamageOverFight(stage, StageCurve.BossTimeLimitSeconds);

                // 보스 보상을 받은 뒤의 구매는 **다음** 스테이지를 대비한다.
                // 생존 축이 "다음 보스에게 죽지 않을 만큼"을 기준으로 사기 때문에
                // 여기서 stage를 넘기면 이미 지나간 보스를 대비하게 된다
                // 챕터 배수는 BossGoldForStage 안에 들어 있다. 여기서 또 곱하면
                // 두 번 적용된다 - 시뮬레이션만 후하게 계산하는 상태가 된다
                // 보스 골드와 클리어 보너스에도 획득 배수가 곱해진다. 게임 쪽
                // 두 지점(EnemySpawner, BossFight)과 같아야 하고, 여기만 빠지면
                // 시뮬레이션이 실제보다 가난한 플레이어를 재게 된다
                purse += StageCurve.BossGoldForStage(
                    BigDouble.FromDouble(field.AverageMobGold), stage).ToDouble() * levels.GoldGain;

                // 17단계의 클리어 보너스. 화면에는 축하 숫자로 뜨지만 밸런스에는
                // 그대로 들어온다 - 16단계에서 피날레 골드가 다음 스테이지를
                // 망가뜨린 것과 같은 경로다
                purse += StageCurve.ClearGoldForStage(
                    BigDouble.FromDouble(field.AverageMobGold), stage).ToDouble() * levels.GoldGain;
                levelsGained += levels.GainExp(ExpCurve.BossExp(stage).ToDouble());
                Buy(ref levels, ref purse, stage + 1, lastGoldPerSecond, stage);

                results.Add(new StageResult
                {
                    Stage = stage,
                    MobSeconds = mobSeconds,
                    MobKillSeconds = lastKill,
                    SpawnInterval = lastInterval,
                    BossKillSeconds = bossKill,
                    BossCleared = bossKill <= BossDamageWindowSeconds,
                    BossMargin = BossDamageWindowSeconds / bossKill,
                    AttackPowerLevel = levels.Power,
                    AttackSpeedLevel = levels.Speed,
                    CritRateLevel = levels.CritRate,
                    CritDamageLevel = levels.CritDamage,
                    Damage = stats.Damage,
                    AttacksPerSecond = stats.AttacksPerSecond,
                    CritRate = stats.CritRate,
                    CritMultiplier = stats.CritMultiplier,
                    ExpectedDps = stats.ExpectedDps,

                    HealthLevel = levels.H,
                    RegenLevel = levels.G,
                    MaxHealth = levels.MaxHealth,
                    RegenPerSecond = levels.RegenPerSecond,
                    SurvivalMargin = incoming > 0d ? levels.EffectiveHealth / incoming : double.PositiveInfinity,
                    Survived = levels.EffectiveHealth >= incoming,
                    IsChapterBoss = BossCurve.IsChapterBoss(stage),

                    CharacterLevel = levels.L,
                    LevelsGained = levelsGained,
                    AttackPoints = levels.AttackPoints,
                    HealthPoints = levels.HealthPoints,
                    AttackAmp = levels.AttackAmp,
                    HealthAmp = levels.HealthAmp,

                    GoldGainLevel = levels.Gd,
                    GoldGain = levels.GoldGain,
                    GoldPerSecond = lastGoldPerSecond,
                    GoldGainPaybackSeconds =
                        GoldGainEfficiency.PaybackSeconds(levels.Gd, lastGoldPerSecond),

                    // 이 스테이지에 든 시간을 오른 레벨 수로 나눈다. 보스 연출과
                    // 처치 시간까지 포함하는 이유는 그것도 플레이어가 앉아 있는
                    // 시간이기 때문이다 - 리듬은 체감이고 체감은 벽시계다
                    SecondsPerLevel = levelsGained > 0
                        ? (mobSeconds + BossIntroSeconds + BossWalkInSeconds + bossKill) / levelsGained
                        : double.PositiveInfinity
                });
            }

            return results;
        }

        /** 여섯 축의 레벨. 구매 정책이 이것을 굴린다 */
        private struct Levels
        {
            public int Power;
            public int Speed;
            public int CritRate;
            public int CritDamage;
            public int Health;
            public int Regen;

            /** 20단계의 획득 축 */
            public int Gold;

            // ------------------------------------------------------------ 12단계

            /** 캐릭터 레벨. 1부터 */
            public int Character;

            /** 현재 레벨에서 모은 경험치 */
            public double Exp;

            public int AttackPoints;
            public int HealthPoints;

            public int L { get { return Character < 1 ? 1 : Character; } }

            public double AttackAmp { get { return StatPointCurve.Multiplier(AttackPoints); } }
            public double HealthAmp { get { return StatPointCurve.Multiplier(HealthPoints); } }

            /**
             * @brief 경험치를 받고 올릴 수 있는 만큼 올린다.
             *
             * 자동으로 올린다. 게임에서는 버튼을 눌러야 하지만(CharacterLevel),
             * 시뮬레이션이 재는 것은 "이 시점에 올릴 수 있는가"이고 플레이어가
             * 누르기를 미루는 시간까지 모델링하면 진행 속도가 임의의 가정에
             * 좌우된다. 실제로도 레벨업 버튼은 떠 있으면 누르는 버튼이다.
             */
            public int GainExp(double amount)
            {
                Exp += amount;

                int gained = 0;
                for (int guard = 0; guard < 100000; guard++)
                {
                    double need = ExpCurve.RequiredForLevel(L + gained).ToDouble();
                    if (Exp < need) break;

                    Exp -= need;
                    gained++;
                }

                Character = L + gained;
                return gained;
            }

            /**
             * @brief 스탯 포인트를 찍는다. 배분 규칙은 다음 하나다.
             *
             *   **다음 보스에게 죽지 않을 만큼까지는 체력 증폭, 그 뒤는 전부
             *   공격력 증폭.**
             *
             * 골드 축의 정책("생존 먼저, 그 다음 화력")과 같은 규칙이고, 같아야
             * 한다 - 두 재화가 서로 다른 우선순위를 쓰면 시뮬레이션이 재는 것이
             * 어느 쪽 플레이어인지 알 수 없게 된다.
             *
             * 축이 둘뿐이고 포인트당 효과가 같아서 골드 축처럼 효율을 비교할
             * 것이 없다. 남는 판단 기준은 "지금 무엇이 모자란가" 하나다.
             *
             * 상한(StatPointCurve.MaxPoints)에 닿은 축은 건너뛴다. 실제로
             * 도달할 일은 없지만, 도달했는데 계속 찍으면 포인트가 조용히
             * 사라진다.
             */
            public void SpendPoints(double neededEffectiveHealth)
            {
                int unspent = StatPointCurve.TotalPointsAtLevel(L) - AttackPoints - HealthPoints;

                for (int i = 0; i < unspent; i++)
                {
                    bool wantHealth = EffectiveHealth < neededEffectiveHealth;

                    if (wantHealth && HealthPoints < StatPointCurve.MaxPoints) HealthPoints++;
                    else if (AttackPoints < StatPointCurve.MaxPoints) AttackPoints++;
                    else if (HealthPoints < StatPointCurve.MaxPoints) HealthPoints++;
                    else break;
                }
            }

            public int H { get { return Health < 1 ? 1 : Health; } }
            public int G { get { return Regen < 1 ? 1 : Regen; } }

            /** 획득 축의 레벨과 지금 곱하고 있는 배수 */
            public int Gd { get { return Gold < 1 ? 1 : Gold; } }
            public double GoldGain { get { return GoldGainCurve.CappedValueAtLevel(Gd); } }

            /** 스탯 포인트 증폭이 곱해진 값. UpgradeSystem.Apply와 같은 순서다 */
            public double MaxHealth { get { return HealthCurve.ValueAtLevel(H) * HealthAmp; } }

            /**
             * @brief 초당 회복 비율 (최대 체력 대비). **상한이 적용된 값이다.**
             *
             * 16단계에서 상한이 생겼다. 여기서 무상한 값을 쓰면 시뮬레이션은
             * 무적 플레이어를 기준으로 밸런스를 재고, 화면과 갈린다.
             */
            public double RegenFraction { get { return HealthRegenCurve.CappedValueAtLevel(G); } }

            /** 초당 절대 회복량. 표에 찍을 때만 쓴다 */
            public double RegenPerSecond { get { return MaxHealth * RegenFraction; } }

            public double EffectiveHealth
            {
                get { return SurvivalEfficiency.EffectiveHealth(MaxHealth, RegenFraction); }
            }

            /** 0으로 시작하지 않는다. 모든 축은 레벨 1이 시작 스탯이다 */
            public int P { get { return Power < 1 ? 1 : Power; } }
            public int S { get { return Speed < 1 ? 1 : Speed; } }
            public int R { get { return CritRate < 1 ? 1 : CritRate; } }
            public int D { get { return CritDamage < 1 ? 1 : CritDamage; } }

            public CombatStats Stats
            {
                get
                {
                    return new CombatStats
                    {
                        // 증폭은 공격력에만 붙는다. UpgradeSystem.Apply와 같다 -
                        // 두 곳이 갈리면 시뮬레이션이 게임과 다른 밸런스를 잰다
                        Damage = AttackPowerCurve.ValueAtLevel(P) * AttackAmp,
                        AttacksPerSecond = AttackSpeedCurve.CappedValueAtLevel(S),
                        CritRate = CritRateCurve.CappedValueAtLevel(R),
                        CritMultiplier = CritDamageCurve.ValueAtLevel(D)
                    };
                }
            }
        }

        /**
         * @brief 총 소요 시간. 보스의 등장 연출과 걸어 들어오는 시간을 포함한다.
         *
         * 연출 시간을 여기 넣는 이유는, 그것이 스테이지마다 반드시 드는 고정 비용이라
         * 후반에서 무시할 수 없는 비중이 되기 때문이다. 5스테이지쯤이면 보스 처치
         * 자체보다 걸어 들어오는 시간이 더 길다.
         */
        public const double BossIntroSeconds = 1d;

        /**
         * @brief 보스에게 달려가는 시간.
         *
         * 16단계까지 이 값은 "보스가 화면 밖에서 걸어 들어오는 시간"이었고
         * **제한 시간 안에** 있었다. 17단계에서 방향이 뒤집혔다 - 이제
         * 플레이어가 달려가고, 이 구간은 타이머 밖이다.
         *
         * 총 소요 시간에는 여전히 들어간다. 타이머가 안 돌 뿐 플레이어가
         * 화면 앞에 앉아 있는 시간은 맞고, 스테이지마다 반드시 드는 고정
         * 비용이라 진행 속도 계산에서 빼면 안 된다.
         */
        public const double BossRunUpSeconds = 5.3d;

        /** 예전 이름. 뜻이 바뀌었으므로 새 이름을 쓴다 */
        public const double BossWalkInSeconds = BossRunUpSeconds;

        public static double TotalSeconds(List<StageResult> results)
        {
            double total = 0d;
            foreach (var row in results)
                total += row.MobSeconds + BossIntroSeconds + BossWalkInSeconds + row.BossKillSeconds;
            return total;
        }

        /**
         * @brief 골드가 되는 대로, 지금 골드당 DPS 이득이 가장 큰 축을 산다.
         *
         * 축이 둘일 때는 "더 싼 쪽"으로 충분했다. 두 축의 효율 비율이 레벨과
         * 무관하게 일정해서 비용 비교가 곧 효율 비교였기 때문이다.
         *
         * 넷이 되면 그것이 성립하지 않는다. 치명타 피해는 레벨이 오를수록 DPS
         * 기여가 커지고 치명타 확률은 언덕을 그린다. 그래서 실제 효율
         * (UpgradeEfficiency가 재는 것과 같은 값)로 고른다 - 시뮬레이션의 구매
         * 정책과 게임이 플레이어에게 보여주는 지표가 같은 함수여야 한다.
         */
        /**
         * @brief 여섯 축의 구매 정책.
         *
         * **생존이 먼저, 그 다음이 화력이다.**
         *
         *   1. 다음 보스에게 죽지 않을 만큼 생존 축을 산다. 생존 축 둘 중에서는
         *      골드당 %EHP가 큰 쪽을 고른다.
         *   2. 남는 골드로 화력 축을 산다. 넷 중에서는 골드당 %DPS가 큰 쪽.
         *
         * 순서를 이렇게 둔 이유는 두 자원의 성질이 다르기 때문이다. 화력이 모자라면
         * 보스전이 길어질 뿐이지만 생존이 모자라면 **그 스테이지에 아예 들어갈 수
         * 없다.** 실제 플레이어도 죽고 나면 체력부터 올린다.
         *
         * "죽지 않을 만큼"에는 여유를 둔다. 정확히 맞추면 스테이지가 오르는 순간마다
         * 한 번씩 죽고, 그 죽음은 정보가 아니라 반복 작업이 된다.
         */
        private const double SurvivalSafetyMargin = 1.15d;

        private static void Buy(ref Levels levels, ref double purse, int nextStage, double goldPerSecond,
                                int currentStage)
        {
            // 생존 먼저. 다음 보스가 낼 총 피해를 여유를 두고 넘길 때까지 산다
            double needed = BossCurve.TotalDamageOverFight(nextStage, StageCurve.BossTimeLimitSeconds)
                            * SurvivalSafetyMargin;

            // 스탯 포인트가 먼저다. 골드가 들지 않으므로 미룰 이유가 없고,
            // 미루면 골드 축이 그만큼을 대신 메우게 되어 두 축의 기여가 섞인다
            levels.SpendPoints(needed);

            // 해금 전에는 목록에 없다(GoldGainCurve.UnlockStage). 게이트를 시뮬레이션
            // 쪽에도 넣지 않으면 계산만 온보딩에서 이 축을 사고, 그 차이가 그대로
            // "보고서의 소요 시간과 실제 플레이가 다르다"가 된다
            if (GoldGainCurve.IsUnlockedAt(currentStage))
                BuyGoldGain(ref levels, ref purse, goldPerSecond);

            for (int guard = 0; guard < 100000; guard++)
            {
                if (levels.EffectiveHealth >= needed) break;

                double healthCost = HealthCurve.CostAtLevel(levels.H);
                double regenCost = HealthRegenCurve.CostAtLevel(levels.G);

                // 골드당 %EHP가 큰 쪽. UpgradeEfficiency가 아니라
                // SurvivalEfficiency와 같은 자다.
                //
                // 회복은 상한이 적용된 값으로 잰다. 상한 위에서는 이득이 0이라
                // 자연히 안 사게 되고, 그것이 실제 플레이어가 하는 판단이다 -
                // 값이 안 오르는 버튼에 골드를 쓰지 않는다
                // **증폭을 곱해서 비교한다.** levels.MaxHealth에는 스탯 포인트
                // 증폭이 이미 곱해져 있는데(12단계), 여기만 곡선값을 그대로 쓰면
                // 증폭이 커질수록 "다음 레벨"이 현재보다 작아져 이득이 음수가 된다.
                //
                // 증폭이 x1.005일 때는 묻혀 있다가 16단계에서 x1.56이 되자
                // 드러났다 - 비교가 뒤집혀 체력을 한 번도 안 사고 회복만
                // Lv.129까지 사들였다. 같은 것끼리 비교해야 한다
                double healthGain = (SurvivalEfficiency.EffectiveHealth(
                        HealthCurve.ValueAtLevel(levels.H + 1) * levels.HealthAmp,
                        levels.RegenFraction)
                    / levels.EffectiveHealth - 1d) / healthCost;

                double regenGain = levels.G >= HealthRegenCurve.MaxLevel
                    ? 0d
                    : (SurvivalEfficiency.EffectiveHealth(
                            levels.MaxHealth, HealthRegenCurve.CappedValueAtLevel(levels.G + 1))
                        / levels.EffectiveHealth - 1d) / regenCost;

                bool buyHealth = healthGain >= regenGain;
                double cost = buyHealth ? healthCost : regenCost;
                if (cost > purse) break;

                purse -= cost;
                if (buyHealth) levels.Health = levels.H + 1;
                else levels.Regen = levels.G + 1;
            }

            // 무한 루프 방어. 비용이 0이 되는 곡선이 들어오면 여기서 멈춘다
            for (int guard = 0; guard < 100000; guard++)
            {
                int bestAxis = -1;
                double bestGain = 0d;
                double bestCost = 0d;

                for (int axis = 0; axis < 4; axis++)
                {
                    double cost;
                    if (!TryCost(levels, axis, out cost) || cost > purse) continue;

                    double gain = GainPerGoldFor(levels, axis);
                    if (gain <= bestGain) continue;

                    bestGain = gain;
                    bestAxis = axis;
                    bestCost = cost;
                }

                if (bestAxis < 0) break;

                purse -= bestCost;
                switch (bestAxis)
                {
                    case 0: levels.Power = levels.P + 1; break;
                    case 1: levels.Speed = levels.S + 1; break;
                    case 2: levels.CritRate = levels.R + 1; break;
                    case 3: levels.CritDamage = levels.D + 1; break;
                }
            }
        }

        /**
         * @brief 획득 축을 회수 시간이 밴드 안일 때만 산다.
         *
         * ## 왜 생존 다음, 화력 앞인가
         *
         * 생존보다 뒤인 이유는 생존이 절대 조건이기 때문이다 - 죽으면 그 스테이지에
         * 아예 들어갈 수 없고, 그때 골드 수입이 몇 배든 의미가 없다.
         *
         * 화력보다 앞인 이유는 이 축이 **화력을 사는 속도 자체를 올리기** 때문이다.
         * 회수 시간이 5분이라는 것은 5분 뒤부터 같은 골드로 화력을 더 산다는 뜻이라,
         * 화력을 먼저 사면 그 5분만큼 손해다. 실제 플레이어도 "골드 벌이부터 올리고
         * 나머지"를 한다.
         *
         * 다만 그 순서가 성립하려면 **회수가 밴드 안**이어야 한다. 무조건 이 축부터
         * 사면 골드가 있는 한 계속 사게 되고, 그것이 스노볼이다. 임계값이 그 선을
         * 긋는다(GoldGainEfficiency.BuyThresholdSeconds).
         *
         * 회수 시간은 살 때마다 나빠지므로(비용 x1.15 대 수입 x1.04) 이 루프는
         * 자연히 멈춘다. 그것이 이 축의 자기 제한이고, 별도의 개수 제한이 필요 없는
         * 이유다.
         */
        private static void BuyGoldGain(ref Levels levels, ref double purse, double goldPerSecond)
        {
            // 파밍 속도를 아직 모르는 시점(첫 처치 전)에는 사지 않는다. 0으로
            // 재면 회수 시간이 무한대라 어차피 안 사지만, 명시적으로 둔다
            if (goldPerSecond <= 0d) return;

            for (int guard = 0; guard < 100000; guard++)
            {
                if (levels.Gd >= GoldGainCurve.MaxLevel) break;
                if (!GoldGainEfficiency.WorthBuying(levels.Gd, goldPerSecond)) break;

                double cost = GoldGainCurve.CostAtLevel(levels.Gd);
                if (cost > purse) break;

                purse -= cost;
                levels.Gold = levels.Gd + 1;

                // 사고 나면 수입이 늘어난다. 그 늘어난 값으로 다음 칸의 회수
                // 시간을 재야 한다 - 안 그러면 자기 제한이 한 박자 늦게 걸린다
                goldPerSecond *= GoldGainCurve.CappedValueAtLevel(levels.Gd)
                                 / GoldGainCurve.CappedValueAtLevel(levels.Gd - 1);
            }
        }

        /** 살 수 있으면 비용을 낸다. 상한에 닿은 축은 false */
        private static bool TryCost(Levels levels, int axis, out double cost)
        {
            cost = 0d;
            switch (axis)
            {
                case 0:
                    cost = AttackPowerCurve.CostAtLevel(levels.P);
                    return true;
                case 1:
                    if (levels.S >= AttackSpeedCurve.MaxLevel) return false;
                    cost = AttackSpeedCurve.CostAtLevel(levels.S);
                    return true;
                case 2:
                    if (levels.R >= CritRateCurve.MaxLevel) return false;
                    cost = CritRateCurve.CostAtLevel(levels.R);
                    return true;
                default:
                    cost = CritDamageCurve.CostAtLevel(levels.D);
                    return true;
            }
        }

        /**
         * @brief 이 축을 한 레벨 올릴 때의 골드당 DPS 증가율.
         *
         * 상한이 적용된 실제 스탯으로 잰다. UpgradeEfficiency는 상한을 걷어낸
         * 곡선으로 재는데(형태를 보는 지표라서), 여기서는 반대로 플레이어가 실제로
         * 얻는 것을 알아야 한다 - 상한에 막힌 축을 사는 것은 골드 낭비다.
         */
        private static double GainPerGoldFor(Levels levels, int axis)
        {
            double cost;
            if (!TryCost(levels, axis, out cost) || cost <= 0d) return 0d;

            var before = levels.Stats;

            var after = levels;
            switch (axis)
            {
                case 0: after.Power = levels.P + 1; break;
                case 1: after.Speed = levels.S + 1; break;
                case 2: after.CritRate = levels.R + 1; break;
                default: after.CritDamage = levels.D + 1; break;
            }

            double dpsBefore = before.ExpectedDps;
            if (dpsBefore <= 0d) return 0d;

            return (after.Stats.ExpectedDps / dpsBefore - 1d) / cost;
        }
    }
}
