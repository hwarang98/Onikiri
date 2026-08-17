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
     *                          전직은 여기 **구조적으로 없다** - 첫 귀문이
     *                          st30 클리어라 코리더는 문을 한 번도 안 지난다
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
         * @brief 해금 레벨은 **없다.** 첫 문(st30)이 그 자리를 대신한다.
         *
         * 3단계 전까지 `UnlockLevel = 30`이 전직 탭을 잠갔다. 승급이 귀문
         * 돌파로만 오르게 되면서 그 잠금이 뜻을 잃었다 - 레벨이 차도 살 것이
         * 없고, 문을 넘으면 레벨과 무관하게 오른다.
         *
         * 조율 코리더(st1~30)에 승급이 없다는 사실도 이제 레벨이 아니라
         * **게이트 표**가 지킨다(`PromotionTrialCatalog.GateStages[0] = 30`).
         * 경험치 곡선이 어떻게 움직여도 안 깨지는 근거라 그쪽이 더 단단하다.
         */

        public static int MaxTier { get { return EvolutionCatalog.Count; } }

        // ---------------------------------------------------------------- 값

        /** 이 티어까지의 누적 공격력 배수. 티어 0 = 1배 */
        public static double AttackMultiplierAt(int tier)
        {
            double product = 1d;
            int count = Clamp(tier, 0, MaxTier);
            for (int t = 0; t < count; t++) product *= EvolutionCatalog.Tiers[t].AttackStep;
            return product;
        }

        /**
         * @brief `fromTier`에서 `toTier`까지 **오르는 동안 곱해지는** 공격 배수.
         *
         * 누적이 아니라 구간이다 - `AttackMultiplierAt(to) / AttackMultiplierAt(from)`과
         * 같은 값이지만 나눗셈을 쓰지 않는다. 심층 수렴 보정
         * (`StageCurve.DeepPromotionCompensation`)이 **문 다섯째부터의 몫만** 잡아야
         * 하는데, 나눗셈으로 내면 두 큰 곱의 차에서 반올림이 새고 그 오차가
         * 조율 구간의 비트 불변을 흔들 수 있다.
         *
         * `from >= to`면 정확히 1.0을 낸다 - 곱셈 항등원이라 "보정이 없는 구간"이
         * 계수가 아니라 구조로 지켜진다.
         */
        public static double AttackMultiplierBetween(int fromTier, int toTier)
        {
            int from = Clamp(fromTier, 0, MaxTier);
            int to = Clamp(toTier, 0, MaxTier);

            double product = 1d;
            for (int t = from; t < to; t++) product *= EvolutionCatalog.Tiers[t].AttackStep;
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

        /**
         * @brief 비용 API는 **없다.** 승급은 귀문 돌파로 무료로 온다 (승인 A-1).
         *
         * 3단계 전까지 `GemCost` · `GoldCost` · `TotalGems`가 여기 있었고
         * `EvolutionSystem.TryEvolve`가 그것을 받았다. 셋을 함께 지운 이유는
         * 재화를 읽는 경로가 하나라도 남으면 "귀문 없이 살 수 있다"가 다시
         * 열리기 때문이다.
         */

        // ---------------------------------------------------------------- 상태

        /** 다음 티어가 남아 있는가. 조건(레벨·재화)은 시스템 쪽이 본다 */
        public static bool CanEvolve(int tier)
        {
            return tier >= 0 && tier < MaxTier;
        }

        // ---------------------------------------------------------------- 기대 곡선

        /**
         * @brief 이 스테이지에 선 플레이어가 갖고 있을 티어. **이제 게이트가 정한다.**
         *
         * ## 33단계의 닫힌 식(st37 + 2스테이지당 1티어)을 지웠다
         *
         * 그 식은 "곡선 추종(보석 무제한) 플레이어가 언제 티어를 살 수 있는가"를
         * 재고 있었다. 보석 값이 결정하는 값이라 **플레이어마다 달랐고**, 그래서
         * 보정(`StageCurve.EvolutionCompensation`)이 어느 플레이어를 앵커할지가
         * 설계 판단이었다.
         *
         * 승급 재설계에서 티어의 출처가 재화에서 **귀문 돌파**로 바뀌었다. 문의
         * 자리는 상수이고 모든 플레이어에게 같으므로, 이 함수는 이제 추정이
         * 아니라 **사실**이다 - 앵커를 고를 일도, 실측과 어긋날 일도 없다.
         *
         * 부등호가 `<`인 이유는 `PromotionTrialCatalog.TierAtFrontier` 주석에
         * 있다 - st30에 서 있는 것은 st30을 클리어한 것이 아니다.
         *
         * 표가 하나뿐이라는 것이 요점이다. 여기서 게이트를 다시 적으면 언젠가
         * 한쪽만 바뀌고, 그때 밴드와 세이브 마이그레이션이 다른 세계를 잰다.
         */
        public static int ExpectedTierAtStage(int stage)
        {
            return Clamp(PromotionTrialCatalog.TierAtFrontier(stage), 0, MaxTier);
        }

        /** 티어 t를 주는 문의 스테이지. 그 문을 클리어한 **다음** 스테이지부터 유효하다 */
        public static int ExpectedStageOfTier(int tier)
        {
            return PromotionTrialCatalog.GateStageOfTier(Clamp(tier, 1, MaxTier));
        }

        /** 그 스테이지에 선 플레이어의 공격력 배수 */
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
