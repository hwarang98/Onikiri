using System;
using Onikiri.Battle;

namespace Onikiri.Progression
{
    /**
     * @brief 회피 강화 곡선 (65단계). 보스의 공격을 피하는 **생존 축**이다.
     *
     * 값은 회피 **수치**다. 확률은 보스 명중과 맞대어 만든다(RatingContest):
     *
     *     회피 확률 = 회피 / (회피 + 보스 명중)
     *
     * 명중과 같은 규칙으로 Ceiling·MaxLevel이 없다. 가산(레벨당 +Step)이고
     * 보스 명중도 스테이지의 일차 함수라(StageCurve.BossAccuracyAtStage)
     * 곡선 추종 플레이어의 회피율이 한 자리에 머문다.
     *
     * ## 이 축은 보스전에서만 일한다
     *
     * 잡몹은 플레이어를 때리지 않는다(Enemy.cs). 그래서 회피의 값어치는 보스
     * 생존 하나로 잰다 - 받는 피해가 (1 - 회피 확률)배가 되므로 유효체력은
     *
     *     유효체력 x 1 / (1 - 회피 확률) = 유효체력 x (1 + 회피 / 보스 명중)
     *
     * 이다. **회피 수치에 일차**라 체력·회복과 같은 %EHP 자(SurvivalEfficiency)로
     * 잴 수 있다.
     *
     * 기본값은 0이다 - 강화 전 회피율 0%. 그래서 이 축을 안 산 세계는 64단계와
     * 같고, 회피 판정은 수치가 0이면 난수를 굴리지도 않는다(PlayerHealth).
     */
    public static class EvasionCurve
    {
        public const double BaseValue = CombatBaseline.Evasion;

        /**
         * @brief 레벨당 회피 수치. st1 보스 명중 100을 상대로 첫 칸이 회피율 1.5%다.
         */
        public const double Step = 1.5d;

        /**
         * @brief 첫 구매 비용과 레벨당 배수.
         *
         * 명중과 같은 이유로 x1.2다 - 미세 격자(x1.0176)로 두면 생존 루프의
         * 자투리가 여기로 흘러 st10 회피율이 37%, st700 87%까지 갔다(보스 피해의
         * 1/8만 받는 세계). BaseCost 4에서는 해금(st5) 즉시 여섯 칸을 사 st10
         * 회피 9.5%가 됐고, 그 몫의 골드가 화력으로 가며 st20 피날레 여유를
         * 챕터 관문 위로 밀어 올렸다(코리더 등급 순서가 뒤집혔다). 12면 첫 구매가
         * st8, st10 3.8%로 늦어지고 순서가 돌아온다 - 보고서 §4.
         */
        public const double BaseCost = 12d;
        public const double CostGrowth = 1.2d;

        /**
         * @brief 이 축이 강화 목록에 나타나는 스테이지 - 첫 보스가 실질 위협이 되는 자리.
         *
         * 강화하지 않은 플레이어가 보스전에서 처음 죽는 스테이지
         * (StageSimulation.FirstStageThatKillsAnUnupgradedPlayer)다 - 실측 st5.
         * 그 앞의 보스는 피해를 줘도 죽이지 못하므로 회피가 할 일이 없다.
         * `Evasion_UnlocksWhereTheFirstBossCanKill`이 대조한다.
         */
        public const int UnlockStage = 5;

        public static bool IsUnlockedAt(int stage)
        {
            return stage >= UnlockStage;
        }

        public static double ValueAtLevel(int level)
        {
            return BaseValue + Step * Math.Max(0, level - 1);
        }

        public static double CostAtLevel(int level)
        {
            return UpgradeCost.Quantize(BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1)));
        }

        /** 유효체력에 곱해지는 배수 = 1 / (1 - 회피 확률) */
        public static double EffectiveHealthFactor(double evasion, double bossAccuracy)
        {
            if (evasion <= 0d || bossAccuracy <= 0d) return 1d;
            return 1d + evasion / bossAccuracy;
        }

        /**
         * @brief 효율 지표가 쓰는 **기준 보스 명중** - 이 레벨에서 회피율 10%가 되는 값.
         *
         * 명중의 ReferenceEnemyEvasionAtLevel과 같은 역할이다. 레벨 1(회피 0)은
         * 기준이 정의되지 않으므로 한 칸 값(Step)을 바닥으로 둔다.
         */
        public static double ReferenceBossAccuracyAtLevel(int level)
        {
            double evasion = Math.Max(Step, ValueAtLevel(level));
            return evasion * (1d - ReferenceDodgeChance) / ReferenceDodgeChance;
        }

        public const double ReferenceDodgeChance = 0.10d;
    }
}
