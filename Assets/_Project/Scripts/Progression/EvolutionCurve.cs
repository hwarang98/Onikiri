using System;

namespace Onikiri.Progression
{
    /**
     * @brief 전직(진화)의 값·비용·해금 규칙. 표는 EvolutionCatalog에 있다.
     *
     * ## 이 축의 자리 - 밴드 재유도의 첫 손님
     *
     * 32단계가 끝났을 때 피날레 밴드[1.15~1.70]의 비 1.478 중 1.22를 기존 축이
     * 쓰고 있었다. 남은 자리 x1.21에 "큰 마일스톤"이 들어갈 수 없어서, 이번
     * 스텝은 축을 줄이는 대신 **밴드 자체를 다시 유도했다** (StageCurve의
     * 가속 구간 주석 참고). 요지는 이렇다:
     *
     *   조율 코리더 (st1~30)   기존 밴드 그대로. 온보딩·게이트·리듬의 구간이고
     *                          전직은 여기 **구조적으로 없다** - 해금이 Lv.30이고
     *                          곡선 추종 플레이어는 st37에야 도달한다
     *   가속 구간 (st31~50)    바닥 = 무과금 클리어 보장. 천장 = 가속(전직) 곡선의
     *                          상한 + 펫 몫 예약. 과금이 실제로 속도를 사는 구간이다
     *
     * ## 곱연산이고, 무기·스탯 증폭과 같은 자리다
     *
     *   공격력   = 공격력강화(L) x 스탯증폭 x 무기배수 x **전직배수**
     *   최대체력 = 체력강화(L) x 체력증폭 x 방어구배수 x **전직배수**
     *
     * 가산이면 이 축이 반드시 죽는다는 산수는 EquipmentCurve 머리 주석에 있다.
     * 스킬 데미지가 공격력을 상속하므로 전직 배수도 스킬에 그대로 실린다
     * (UpgradeSystem.Apply 한 곳에서 곱한다).
     *
     * ## 상한이 구조적 요구가 아니다 - 티어가 유한해서다
     *
     * 골드로 무한히 사는 축은 상한이 없으면 DPS가 제곱으로 자라지만
     * (EquipmentCurve 주석), 전직은 여섯 칸이 전부다. 표의 끝이 곧 상한이다.
     */
    public static class EvolutionCurve
    {
        /**
         * @brief 해금 캐릭터 레벨. 12단계부터 잠금 탭에 적혀 있던 값이다.
         *
         * 스테이지가 아니라 레벨인 이유는 대장간(스테이지)과 반대다 - 전직은
         * 지역의 랜드마크가 아니라 **캐릭터 자신의 성장**이고, 캐릭터의 조건은
         * 캐릭터의 숫자로 잠근다. 스킬(Lv.10)과 같은 규칙이다.
         *
         * 곡선 추종 플레이어는 st37에 Lv.30이 된다(시뮬레이션 실측). 조율
         * 코리더(st1~30)에 전직이 없는 것이 계수가 아니라 구조로 지켜지는
         * 자리다 - EvolutionTests.Evolution_IsAbsentFromTheTunedCorridor.
         */
        public const int UnlockLevel = 30;

        public static int MaxTier { get { return EvolutionCatalog.Count; } }

        public static bool IsUnlockedAt(int characterLevel)
        {
            return characterLevel >= UnlockLevel;
        }

        // ---------------------------------------------------------------- 값

        /** 이 티어까지의 누적 공격력 배수. 티어 0 = 1배 */
        public static double AttackMultiplierAt(int tier)
        {
            double product = 1d;
            int count = Clamp(tier, 0, MaxTier);
            for (int t = 0; t < count; t++) product *= EvolutionCatalog.Tiers[t].AttackStep;
            return product;
        }

        /** 이 티어까지의 누적 최대 체력 배수. 티어 0 = 1배 */
        public static double HealthMultiplierAt(int tier)
        {
            double product = 1d;
            int count = Clamp(tier, 0, MaxTier);
            for (int t = 0; t < count; t++) product *= EvolutionCatalog.Tiers[t].HealthStep;
            return product;
        }

        // ---------------------------------------------------------------- 비용

        /** tier에서 tier+1로 오르는 보석 값. 마지막 티어면 0 */
        public static int GemCost(int tier)
        {
            if (tier < 0 || tier >= MaxTier) return 0;
            return EvolutionCatalog.Tiers[tier].GemCost;
        }

        /** tier에서 tier+1로 오르는 골드 값. 마지막 티어면 0 */
        public static double GoldCost(int tier)
        {
            if (tier < 0 || tier >= MaxTier) return 0d;
            return EvolutionCatalog.Tiers[tier].GoldCost;
        }

        /** 전체 사다리의 보석 총합. 보고와 테스트가 쓴다 */
        public static int TotalGems
        {
            get
            {
                int total = 0;
                for (int t = 0; t < MaxTier; t++) total += EvolutionCatalog.Tiers[t].GemCost;
                return total;
            }
        }

        // ---------------------------------------------------------------- 상태

        /** 다음 티어가 남아 있는가. 조건(레벨·재화)은 시스템 쪽이 본다 */
        public static bool CanEvolve(int tier)
        {
            return tier >= 0 && tier < MaxTier;
        }

        // ---------------------------------------------------------------- 보스 보정

        /**
         * @brief 곡선 추종(보석 무제한) 플레이어가 이 스테이지에서 갖고 있을 티어.
         *
         * 보스 체력 보정(StageCurve.EvolutionCompensation)이 이것을 따라간다.
         * 장비의 ExpectedLevelAtStage와 같은 자리이고 같은 제약을 받는다 -
         * **닫힌 식이어야 한다** (시뮬레이션이 보스 체력을 계산하려고 자기
         * 결과를 참조하는 순환을 막는다).
         *
         * ## 어느 플레이어의 기대인가 - 무제한 쪽이다. 여기가 과금 설계의 핵심이다
         *
         * 장비 보정은 기대 곡선을 곡선 추종 플레이어(보석 무제한)에 앵커했고
         * 여기도 같다. 그런데 전직에서 이 선택의 뜻이 다르다:
         *
         *   무과금(보석 하한)은 1티어에서 멈춘다. 보정은 그보다 무겁게 걸리므로
         *   무과금의 후반 여유가 **내려간다** - 그 내려간 바닥이 "노력하면 전
         *   구간 클리어"(여유 1.1 안팎)이도록 지수를 잡았다.
         *
         *   과금(무제한)은 여섯 티어를 다 오른다. 지수가 1보다 작아 상쇄가
         *   부분이므로, 남는 몫이 **실제 가속**이다 - 과금이 사는 것이 이것이다.
         *
         * 밴드의 재정의("코리더"가 아니라 "무과금 바닥 보장")가 이 두 줄이다.
         *
         * ## 앵커 값은 실측이다
         *
         * 첫 티어 스테이지 37 = 곡선 추종 플레이어가 Lv.30(해금)에 닿는
         * 스테이지. 이후 2스테이지당 1티어 = 골드 도약 비용(기대 스테이지의
         * 2분치)이 모이는 간격. 실측과 어긋나면
         * EvolutionTests.ExpectedCurve_TracksTheSimulation이 잡는다.
         */
        public const int FirstTierStage = 37;
        public const int StagesPerTier = 2;

        public static int ExpectedTierAtStage(int stage)
        {
            if (stage < FirstTierStage) return 0;
            return Clamp(1 + (stage - FirstTierStage) / StagesPerTier, 0, MaxTier);
        }

        /** 티어 t의 기대 도달 스테이지. 골드 비용의 앵커다 */
        public static int ExpectedStageOfTier(int tier)
        {
            int t = Clamp(tier, 1, MaxTier);
            return FirstTierStage + (t - 1) * StagesPerTier;
        }

        /** 그 스테이지에서 곡선 추종 플레이어가 갖고 있을 공격력 배수 */
        public static double ExpectedPowerAtStage(int stage)
        {
            return AttackMultiplierAt(ExpectedTierAtStage(stage));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
