using System;

namespace Onikiri.Progression
{
    /**
     * @brief 동료(펫)의 값·비용·해금 규칙. 표는 PetCatalog에 있다.
     *
     * ## 이 축의 자리 - 33단계가 예약해 둔 몫
     *
     * 33단계 밴드 재유도가 가속 구간(st31~50) 천장에 **펫 몫 x1.25를 미리
     * 포함**해 두었다(StageSimulationTests.PetReserveMultiplier). 이 스텝은
     * 밴드를 다시 유도하지 않는다 - 그 자리에 착지한다:
     *
     *   명목 크기      **합산** 보너스 상한 0.5 = DPS x1.5 (분배는 카탈로그:
     *                 청랑 0.30 / 명궁 0.12 / 묵웅 0.08)
     *   보정           x1.5^0.45 = x1.20 만큼 보스가 따라온다
     *   실가속         x1.5^0.55 = x1.25 - 예약된 몫 그대로
     *
     * ## 축의 모양 - 보유 동료 전원이 별도 공격자, 밸런스는 곱연산 배수 하나
     *
     * 화면에서 동료들은 로닌 옆의 전투원들이다. 밸런스에서는 DPS에 곱해지는
     * (1 + 합산 보너스) 하나다 - 각자의 한 타가 "자기 보너스 x 플레이어 기대
     * DPS x 자기 공격 간격"이라 초당 기여의 합이 정확히 합산 보너스 x DPS이기
     * 때문이다. 플레이어의 강화·장비·전직·치명타가 오르면 동료도 같은 비율로
     * 세지므로, 동료별 레벨 축만 시뮬레이션에 편입하면 된다.
     *
     * ## 상한이 구조적 요구다
     *
     * 골드로 사는 곱연산 축이라 상한이 없으면 DPS가 제곱으로 자란다 -
     * EquipmentCurve·GoldGainCurve와 같은 산수다. 상한 0.5는 밸런스 손잡이가
     * 아니라 예약 몫의 크기다.
     */
    public static class PetCurve
    {
        /**
         * @brief 펫이 열리는 스테이지. **가속 구간의 첫 칸이다.**
         *
         * ## 왜 스테이지이고 레벨이 아닌가
         *
         * 대장간(st11)과 같은 규칙이다 - 동료는 캐릭터 자신의 성장(전직 Lv.30)이
         * 아니라 **여정에서 만나는 존재**다. 지역 3 피날레(st30)를 넘긴 다음
         * 칸에서 합류한다는 조건은 화면의 서사와 같은 것을 가리킨다.
         *
         * ## 왜 31인가 - 코리더 불변이 계수가 아니라 구조로 지켜진다
         *
         * 조율 코리더(st1~30)의 밴드는 이 스텝에서 한 자리도 움직이면 안 된다.
         * 해금이 st31이면 펫도 보정도 코리더에 **존재하지 않는다** - 전직이
         * Lv.30(실측 st37)으로 코리더 밖에 선 것과 같은 구조이고,
         * PetTests.Pet_IsAbsentFromTheTunedCorridor가 못 박는다. 온보딩(1~5,
         * 173초)에서는 여섯 배 떨어져 있다.
         */
        public const int UnlockStage = 31;

        public static bool IsUnlockedAt(int stage)
        {
            return stage >= UnlockStage;
        }

        // ---------------------------------------------------------------- 값

        /**
         * @brief 레벨 상한. 등급 관문이 없으므로 골드가 끝까지 간다.
         *
         * 장비(등급이 단련을 자르는 구조)와 달리 펫 레벨에는 보석 관문이 없다 -
         * 보석은 해금 문 하나로 끝이고, 그 뒤 이 축은 순수한 골드 싱크다.
         * "해금 = 보석 / 레벨 = 골드"의 분리가 곡선 구조에도 그대로 있다.
         */
        public const int MaxLevel = 20;

        /**
         * @brief 레벨당 보너스 배수. **셋이 공유한다** - 리듬은 같고 크기만 다르다.
         *
         * 각 동료의 첫 칸과 상한은 카탈로그에 있다(PetSpec.FirstBonus /
         * BonusCeiling - 합산 몫의 분배가 밸런스라서 표다). 성장률이 같으므로
         * 셋 다 Lv.20에서 자기 상한에 닿는다.
         */
        public const double BonusGrowth = 1.08d;

        /** 합산 보너스 상한 = 예약 몫의 명목. 카탈로그 분배의 합과 같아야 한다 */
        public const double TotalBonusCeiling = 0.5d;

        /**
         * @brief 이 동료의, 이 레벨의 보너스.
         *
         * 상한에서 자른다. 상한 위의 레벨이 세이브에 있어도(상한이 내려간
         * 업데이트) 값은 여기서 잘린다 - EquipmentCurve.ValueAt과 같은 규칙.
         */
        public static double BonusAt(double firstBonus, double ceiling, int level)
        {
            int l = level < 1 ? 1 : level;
            double bonus = firstBonus * Math.Pow(BonusGrowth, l - 1);
            return bonus > ceiling ? ceiling : bonus;
        }

        /** 카탈로그 인덱스로 읽는 편의 함수. 시뮬레이션과 화면이 쓴다 */
        public static double BonusOfPetAt(int petIndex, int level)
        {
            if (petIndex < 0 || petIndex >= PetCatalog.Count) return 0d;
            var spec = PetCatalog.Pets[petIndex];
            return BonusAt(spec.FirstBonus, spec.BonusCeiling, level);
        }

        // ---------------------------------------------------------------- 비용

        /**
         * @brief 레벨 비용의 증가율. **여섯 축(1.15)보다 훨씬 가파르다.**
         *
         * 상한이 있는 축은 반드시 빨리 팔린다(EquipmentCurve.TemperCostGrowth
         * 주석의 산수). 수입이 스테이지마다 x1.72로 자라고 경쟁 축 비용이
         * x1.75로 자라므로 이 축이 감당하는 성장 압력은 약 x3.0/스테이지다:
         *
         *     레벨/스테이지 = ln(3.0) / ln(CostGrowth) = ln(3.0)/ln(1.9) = 1.71
         *
         * 열아홉 칸이 가속 구간 스무 스테이지의 절반쯤(st31~42)에 걸쳐 팔리고,
         * 그 뒤는 상한이다 - 등급 관문이 없는 대신 가격이 리듬을 만든다.
         */
        public const double CostGrowth = 1.9d;

        /**
         * @brief 레벨 첫 칸(Lv.1 -> 2)의 골드. **해금 스테이지의 절대 골드다.**
         *
         * 스테이지 배수를 곱하지 않는다 - EquipmentCurve.TemperBaseCost가
         * 물린 자리 그대로다. 이 상수는 이미 st31 시점의 골드 단위로 잰
         * 값이라(그 시점 경쟁 축의 %DPS당 가격과 같은 자리) 배수가 안에
         * 들어 있다. 실측과 어긋나면
         * PetTests.ExpectedCurve_TracksTheSimulation이 잡는다.
         *
         * ## 다중 출전에서 1e8 -> 1e7로 내렸다 - 죽은 버튼이 실제로 열렸다
         *
         * 합산 상한(0.5)은 그대로인데 곡선이 셋으로 갈라지면서, **전체 사다리의
         * 총 골드가 단일 곡선 시절의 2.7배**(6.1e12 -> 1.65e13)가 됐다. 그
         * 상태로 실측하자 st31~50 이득이 2.4%로 기준(4%) 밑 - 늘어난 값어치는
         * 5%p뿐인데 값이 2.7배라, 동료에 간 골드가 정규 축의 성장을 그만큼
         * 깎아 순이득이 사라진 것이다. 20단계 골드 축의 함정("지표는 사라는데
         * 실제로는 손해") 그대로라, **세 곡선의 총 가격이 단일 곡선 시절과
         * 같도록** 첫 칸을 10분의 1로 내렸다(3 x 1e7 x Σ1.9^k ≈ 6.6e12).
         */
        public const double LevelBaseCost = 1e7d;

        public static double CostAtLevel(int level)
        {
            int l = level < 1 ? 1 : level;
            return LevelBaseCost * Math.Pow(CostGrowth, l - 1);
        }

        /** 레벨이 더 남아 있는가. 재화는 시스템 쪽이 본다 */
        public static bool CanLevelUp(int level)
        {
            return level >= 1 && level < MaxLevel;
        }

        // ---------------------------------------------------------------- 보스 보정

        /**
         * @brief 곡선 추종(보석 무제한) 플레이어의 기대 상태.
         *
         * 보스 체력 보정(StageCurve.PetCompensation)이 이것을 따라간다. 장비의
         * ExpectedLevelAtStage와 같은 자리이고 같은 제약을 받는다 - **닫힌
         * 식이어야 한다**(순환 금지). 해금 전에는 배수 1이다 - 축이 없는
         * 구간에 보정만 걸리면 있지도 않은 이득을 상쇄하는 셈이고, 21단계
         * 골드 축의 사고 그대로다.
         *
         * ## 다중 출전의 기대 곡선 - 셋이 나란히 오른다
         *
         * 곡선 추종 플레이어는 st31에 셋을 다 해금하고(보석 무제한) 효율로
         * 레벨을 산다. 처음에는 "첫 칸 큰 청랑이 앞서고 나머지가 지연으로
         * 따라온다"고 가정했는데(지연 7/12), **실측은 셋이 거의 나란히
         * 오른다** - 비용 증가율(x1.9)이 첫 칸 비율(최대 1.67배)을 압도해서,
         * 한 칸 사면 그 동료의 다음 칸이 두 배가 되어 자연히 차례가 넘어간다.
         * 그래서 지연이 0이고, 기대 총보너스는 공통 레벨 하나로 적는다.
         *
         * 절편·기울기는 실측 앵커다(장비가 절편을 1로 뒀다가 물린 자리).
         * 실측과 어긋나면 PetTests.ExpectedCurve_TracksTheSimulation이 잡는다.
         */
        public const int LevelAtUnlock = 3;   // 43단계 미세화 재적합 - 실측 st31 평균 Lv.3 (옛 5)
        public const double LevelsPerStage = 0.85d;

        /** 후발 동료의 레벨 지연. 실측 0 - 위 주석 참고. 표는 재실측 대비로 남긴다 */
        public static readonly int[] ExpectedLevelLag = { 0, 0, 0 };

        /** 선두(청랑)의 기대 레벨 */
        public static int ExpectedLevelAtStage(int stage)
        {
            if (!IsUnlockedAt(stage)) return 0;

            int level = LevelAtUnlock + (int)Math.Floor((stage - UnlockStage) * LevelsPerStage);
            return level > MaxLevel ? MaxLevel : level;
        }

        /** 그 스테이지의 기대 DPS 배수 (1 + 합산 보너스). 해금 전에는 정확히 1 */
        public static double ExpectedMultiplierAtStage(int stage)
        {
            int lead = ExpectedLevelAtStage(stage);
            if (lead < 1) return 1d;

            double total = 0d;
            for (int i = 0; i < PetCatalog.Count; i++)
            {
                int lag = i < ExpectedLevelLag.Length ? ExpectedLevelLag[i] : 0;
                int level = lead - lag;
                if (level < 1) level = 1;   // 해금은 st31에 이미 됐다 - Lv.1 몫은 남는다
                total += BonusOfPetAt(i, level);
            }

            return 1d + total;
        }
    }
}
