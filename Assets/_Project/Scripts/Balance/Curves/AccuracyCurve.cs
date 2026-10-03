using System;
using Onikiri.Battle;

namespace Onikiri.Progression
{
    /**
     * @brief 명중 강화 곡선 (65단계). 적 회피를 뚫는 **공격 축**이다.
     *
     * 값은 명중 **수치**이고 확률이 아니다. 확률은 판정식이 적 회피와 맞대어
     * 만든다(RatingContest). 그래서 이 곡선에는 Ceiling·MaxLevel·
     * CappedValueAtLevel이 없다 - 수치는 무한히 쌓이고, 확률은 1에 수렴할
     * 뿐 닿지 않는다(65단계 확정 3·7).
     *
     * **가산이다.** 레벨당 +Step. 적 회피도 스테이지의 일차 함수라
     * (StageCurve.EnemyEvasionAtStage), 곡선 추종 플레이어가 레벨을 스테이지에
     * 비례해 사면 명중률이 한 자리에 머문다. 곱연산이면 수치가 적 회피를
     * 지수로 앞질러 명중률이 곧 1에 붙고 축이 죽는다.
     *
     * 효율을 재는 자는 UpgradeEfficiency(골드당 %DPS)이고, 적 회피라는 문맥이
     * 필요해서 기준 적 회피를 함께 넘긴다(ReferenceEnemyEvasionAtLevel).
     *
     * ## 비용은 다른 화력 축의 미세 격자(x1.0176)가 아니라 x1.2다
     *
     * 처음에 같은 격자로 돌렸더니 이 축이 **남는 골드의 배수구**가 됐다. 가산
     * 축은 수치가 커질수록 한 칸의 %DPS가 1/수치로 줄어드는데(명중률이 1에
     * 다가가므로), 비용이 그보다 느리게 자라면 공격력을 못 사는 자투리 골드가
     * 매번 여기로 흘러 st11에 Lv.146, st200에 명중률 99.4%가 됐고, 그만큼
     * 화력이 빠져 st200 실효 DPS가 64단계의 7%까지 떨어졌다. x1.2면 곡선 추종
     * 레벨이 스테이지당 약 3칸(ln 1.72 / ln 1.2)으로 묶이고, 그 속도가 적
     * 회피의 일차 성장과 맞물려 추종 명중률이 보스 상대 88~95%에 머문다.
     *
     * 실측과 전수조사는 65단계 보고서 §3에 있다. **Step 10 방식의 5배 효율
     * 밴드는 이 축으로 성립하지 않는다**(정수 비용 >= 1 · 가산 · 판정식의 셋이
     * 함께 걸린다) - 그 근거와 남은 결정도 같은 절에 있다.
     */
    public static class AccuracyCurve
    {
        /** 레벨 1의 명중 수치 = 강화 전 기본값 */
        public const double BaseValue = CombatBaseline.Accuracy;

        /**
         * @brief 레벨당 명중 수치 = 기본값의 2%.
         *
         * 해금 시점(st11) 보스 회피 18.6을 상대로 한 칸이 명중률을 약 +0.25%p
         * 올린다. 더 잘게 나누면(0.1~0.8 전수조사) 첫 칸의 값어치가 1골드보다
         * 작아져 정수 비용과 맞지 않는다(보고서 §3).
         */
        public const double Step = 1.8d;

        public const double BaseCost = 2d;
        public const double CostGrowth = 1.2d;

        /**
         * @brief 이 축이 강화 목록에 나타나는 스테이지 = **게임 시작부터**.
         *
         * 65단계에는 st11이었다(적 회피가 체감되기 시작하는 자리). 66.1단계에
         * 게이트를 걷었다 - 첫 칸이 2골드라 곡선 추종 플레이어가 st1에 바로
         * 네 칸을 사고(Lv.5), st11에서 65단계와 같은 Lv.25에 합류한다.
         * 다시 게이트를 넣거나 상한을 두어 밴드를 맞추지 않는다 - 밴드가
         * 깨지면 손잡이는 초반 잡몹 체력 하나다(66.1단계 확정).
         */
        public const int UnlockStage = 1;

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

        /**
         * @brief 효율 지표가 쓰는 **기준 적 회피** - 이 레벨에서 명중률 90%가 되는 값.
         *
         * 효율 비교(UpgradeEfficiency)는 레벨만 받는다. 명중의 값어치는 적 회피에
         * 달려 있으므로 기준을 하나 정해야 하고, 강화 전 기본 명중률(90%)을
         * 레벨마다 유지하는 적을 기준으로 둔다 - 곡선 추종 플레이어가 실제로
         * 서 있는 자리에 가깝다(실측 92~95%, 보고서 §3).
         */
        public static double ReferenceEnemyEvasionAtLevel(int level)
        {
            return ValueAtLevel(level) * (1d - CombatBaseline.BaseHitChance) / CombatBaseline.BaseHitChance;
        }

        // ------------------------------------------------------------ 기대 곡선

        /**
         * @brief 닫힌 식의 st1 절편 (실측 적합). **1 아래인 것이 맞다.**
         *
         * 직선은 st11 뒤의 기울기(칸당 x1.2 비용)를 따라가야 하고, 그 직선을
         * st1로 끌어내리면 1 아래가 된다. 실측은 st1 Lv.5다(첫 칸 2골드라 바로
         * 산다) - 직선이 st1~3을 강화 전 값으로 읽어 그 자리 오차가 0.93%p로
         * 가장 크다(허용 1%p). 절편을 올려 그 자리를 맞추면 st11 뒤가 위로
         * 뜬다. 66.1단계 보고서 §2.
         *
         * 시뮬레이션의 결과라 상수로 두고
         * `Accuracy_ExpectedCurve_TracksTheSimulation`이 대조한다.
         */
        public const double ExpectedLevelAtUnlock = -4d;

        /**
         * @brief 곡선 추종 레벨이 스테이지마다 오르는 칸 수 (실측 적합 기울기).
         *
         * st1~500에서 기대 명중률의 최대 오차를 줄이는 쪽으로 골랐다(st1~10
         * 0.93%p@st2 / st11~500 0.47%p@st13). 닫힌 식으로는
         * ln(1.72) / ln(CostGrowth) = 2.97이다. 65단계 값(st11~500 적합)은
         * 22.1 + 3.0144 x (st - 11)이었고 그 직선을 st1로 늘이면 st4 오차가
         * 1.38%p라 허용 밖이었다.
         */
        public const double ExpectedLevelsPerStage = 2.98d;

        /**
         * @brief 그 스테이지에서 곡선 추종 플레이어가 갖고 있을 명중 수치. **닫힌 식이다.**
         *
         * 보스 체력 보정(StageCurve.AccuracyAxisCompensation)이 읽는다 -
         * 시뮬레이션 결과를 참조하면 보스 체력이 자기 결과를 필요로 하는 순환이
         * 된다(GoldGainCurve.ExpectedAtStage와 같은 규칙). 66.1단계부터 해금
         * 전 분기가 없다 - 축이 st1부터 열려 있다.
         */
        public static double ExpectedValueAtStage(int stage)
        {
            double level = ExpectedLevelAtUnlock + ExpectedLevelsPerStage * (stage - UnlockStage);
            return BaseValue + Step * Math.Max(0d, level - 1d);
        }
    }
}
