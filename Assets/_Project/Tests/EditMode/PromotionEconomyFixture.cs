namespace Onikiri.Tests
{
    /**
     * @brief 장부 계산기의 입력 실측표. 테스트 전용이다.
     *
     * 전부 `e76ec48`의 `StageSimulation`에서 구웠다. 손으로 지어낸 값이 하나도
     * 없다는 것이 이 파일의 계약이고, 곡선이 움직이면 여기도 다시 구워야 한다 -
     * `PromotionEconomyTests.FixtureStillMatchesTheSimulation`이 그것을 잡는다.
     */
    public static class PromotionEconomyFixture
    {
        /** 구운 스테이지 수. 여섯째 게이트(st150)보다 넉넉히 뒤까지 본다 */
        public const int Stages = 200;

        /**
         * @brief 무과금 하한(`GemsFromQuestsOnly`)의 **누적** 진행 보석. 인덱스 0 = st1.
         *
         * 업적 열넷(685)과 반복 티어만이다. 일일은 없다 - 그것이 이 축의
         * 하한이고, 달력을 붙이는 것이 장부 계산기의 일이다.
         */
        public static readonly int[] ProgressionGems =
        {
            0, 0, 0, 0, 20, 20, 45, 65, 65, 118, 118, 118, 118, 168, 173, 173, 173, 223, 223, 301,
            301, 301, 301, 306, 306, 306, 306, 311, 311, 444, 444, 449, 449, 449, 489, 494, 494, 494, 494, 572,
            572, 572, 572, 572, 637, 637, 637, 637, 642, 735, 735, 740, 740, 740, 740, 745, 745, 745, 745, 763,
            763, 763, 763, 768, 768, 768, 773, 833, 833, 846, 851, 851, 851, 851, 856, 856, 856, 856, 861, 874,
            874, 874, 879, 879, 879, 879, 884, 884, 884, 897, 897, 897, 902, 902, 902, 902, 902, 902, 902, 920,
            920, 920, 920, 920, 920, 920, 925, 925, 925, 938, 938, 938, 938, 943, 943, 943, 943, 943, 943, 956,
            961, 961, 961, 961, 961, 961, 961, 961, 966, 979, 979, 979, 979, 979, 979, 984, 984, 984, 984, 997,
            997, 997, 997, 1002, 1002, 1002, 1002, 1002, 1002, 1015, 1020, 1020, 1020, 1020, 1020, 1020, 1020, 1020, 1025, 1038,
            1038, 1038, 1038, 1038, 1038, 1038, 1043, 1043, 1043, 1056, 1056, 1056, 1056, 1056, 1061, 1061, 1061, 1061, 1061, 1074,
            1074, 1079, 1079, 1079, 1079, 1079, 1079, 1079, 1079, 1097, 1097, 1097, 1097, 1097, 1097, 1097, 1097, 1102, 1102, 1115
        };

        /**
         * @brief 무과금 하한의 **누적 전투 시간(초)**. 인덱스 0 = st1.
         *
         * 잡몹 + 보스 인트로(1s) + 접근(5.3s) + 보스 처치. 방치 보상은 없다 -
         * 이 값은 "손에 쥐고 있는 시간"이고, 날짜 환산의 분자다.
         */
        public static readonly double[] CumulativeSeconds =
        {
            27.6, 59.7, 91.6, 127.7, 173.8, 213.6, 248.3, 278.4, 305.0, 336.5,
            359.1, 383.1, 408.1, 435.0, 465.0, 490.1, 516.5, 543.8, 571.4, 607.8,
            632.4, 657.2, 683.0, 709.0, 737.9, 761.9, 786.6, 812.2, 838.4, 872.3,
            895.8, 920.4, 945.5, 971.4, 1000.9, 1023.9, 1048.6, 1073.7, 1100.1, 1134.8,
            1158.8, 1183.5, 1209.8, 1236.3, 1267.3, 1292.3, 1319.5, 1347.6, 1376.2, 1414.0,
            1436.3, 1459.5, 1483.1, 1507.3, 1534.4, 1557.1, 1580.8, 1604.8, 1629.3, 1660.9,
            1683.3, 1706.7, 1730.9, 1755.5, 1783.2, 1806.3, 1829.9, 1854.3, 1878.8, 1910.5,
            1932.7, 1955.8, 1979.3, 2003.3, 2029.7, 2051.8, 2074.8, 2098.2, 2122.0, 2152.0,
            2173.1, 2194.9, 2217.2, 2239.8, 2264.9, 2286.1, 2307.9, 2330.3, 2352.9, 2372.9,
            2388.5, 2404.5, 2420.6, 2436.9, 2454.2, 2469.8, 2485.6, 2501.6, 2517.8, 2536.8,
            2552.0, 2567.6, 2583.3, 2599.2, 2616.3, 2631.5, 2647.1, 2662.8, 2678.7, 2697.3,
            2712.4, 2727.7, 2743.2, 2758.8, 2775.6, 2790.5, 2805.7, 2821.2, 2836.8, 2855.1,
            2869.8, 2884.8, 2900.0, 2915.4, 2931.8, 2946.5, 2961.6, 2976.9, 2992.3, 3010.3,
            3025.0, 3040.0, 3055.3, 3070.7, 3087.2, 3102.0, 3117.1, 3132.5, 3147.9, 3166.0,
            3180.7, 3195.7, 3210.9, 3226.3, 3242.7, 3257.5, 3272.7, 3288.0, 3303.5, 3321.4,
            3336.1, 3351.1, 3366.4, 3381.7, 3398.2, 3413.0, 3428.2, 3443.6, 3459.0, 3477.1,
            3491.5, 3506.1, 3521.0, 3535.9, 3551.8, 3566.3, 3581.0, 3596.0, 3611.1, 3628.6,
            3643.0, 3657.8, 3672.7, 3687.8, 3703.9, 3718.5, 3733.4, 3748.4, 3763.6, 3781.2,
            3795.7, 3810.4, 3825.4, 3840.5, 3856.5, 3871.0, 3885.7, 3900.7, 3915.8, 3933.3,
            3947.7, 3962.4, 3977.2, 3992.2, 4008.2, 4022.6, 4037.4, 4052.3, 4067.4, 4084.9
        };

        // ---------------------------------------------------------------- 소비처

        /** `EquipmentCurve.GradeGems`. 1->2, 2->3, 3->4, 4->5 */
        public static readonly int[] GradeCosts = { 40, 80, 150, 260 };

        /**
         * @brief 등급업의 **자격 스테이지**. 보석이 아니라 단련 레벨이 정한다.
         *
         * 보석 무제한(`Policy.Default`) 런에서 각 등급에 처음 닿은 스테이지다 -
         * 그 세계에서는 보석이 안 막으므로 남는 제약이 단련 진도뿐이고, 그것이
         * 곧 자격이다. 방어구가 1에서 3으로 건너뛰는 것은 같은 스테이지에서
         * 두 칸이 함께 팔렸기 때문이라 두 칸의 자격이 같은 st14다.
         */
        public static readonly int[] ArmorGradeStages = { 14, 14, 19, 29 };
        public static readonly int[] WeaponGradeStages = { 11, 12, 14, 15 };

        /** `PetCatalog` 셋. 전부 `PetCurve.UnlockStage`(31)부터 살 수 있다 */
        public static readonly int[] PetCosts = { 80, 200, 400 };
        public static readonly int[] PetStages = { 31, 31, 31 };

        // ---------------------------------------------------------------- 게이트

        /** 귀문 여섯의 게이트 스테이지. 이 스테이지를 클리어해야 문이 열린다 */
        public static readonly int[] GateStages = { 30, 40, 50, 70, 100, 150 };

        /** 일일 퀘스트 다섯의 합 (`QuestCatalog.Daily`) */
        public const double DailyGems = 55d;

        /** 하루 활성 전투 시간. 20분 - 방치형 한 세션의 크기 */
        public const double DailyPlaySeconds = 1200d;

        // ---------------------------------------------------------------- 승급 비용

        /**
         * @brief **승급·전직의 보석 비용은 0이다.** 1.6단계에 확정됐다.
         *
         * 배수도 이름도 기본 외형도 오라도 귀문 돌파로 무료로 온다. 유료 코스튬은
         * 이 시스템 밖의 독립 기능으로 미뤘고, 선택형 추가 전투력은 넣지 않는다.
         *
         * 배열을 없애지 않고 0으로 두는 이유는 장부의 형태를 지키기 위해서다 -
         * 유료 항이 다시 생기는 날 표만 채우면 되고, 그때 이 상수가 "0이었다"는
         * 사실이 코드에 남아 있다.
         */
        public static readonly int[] FreePromotion = { 0, 0, 0, 0, 0, 0 };

        // ---------------------------------------------------------------- 귀문 시간

        /**
         * @brief 게이트별 귀문 전투 시간(초). **하한 앵커라 전 문 45초다.**
         *
         * `M`을 "하한 플레이어가 45초에 통과"로 잡았으므로 구성상 이 값이다.
         * 곡선 추종은 소프트캡 k에 따라 27~43초이고, 도달일을 재는 기준은
         * 느린 쪽이라 하한 값을 쓴다.
         */
        public static readonly double[] TrialSecondsFloor = { 45d, 45d, 45d, 45d, 45d, 45d };

        /** 곡선 추종의 실측 (소프트캡 k=0.45). 감도 분석용 */
        public static readonly double[] TrialSecondsLead = { 42.8d, 43.0d, 33.1d, 29.4d, 27.0d, 27.2d };

        /** 진입 연출 / 결과 화면 / 패널 복귀. **전부 잠정값이다** */
        public const double TrialEntrySeconds = 3d;
        public const double TrialResultSeconds = 4d;
        public const double TrialReturnSeconds = 2d;

        /** 파밍 없음 (하한 앵커의 성질). 감도 분석은 5분 / 10분을 넣는다 */
        public static readonly double[] NoFarm = { 0d, 0d, 0d, 0d, 0d, 0d };

        /**
         * @brief 게이트 스테이지의 초당 골드 (하한 플레이어 실측).
         *
         * 파밍 되먹임의 크기를 보고하는 데만 쓴다. 5분 파밍이 그 스테이지 보스
         * 보상의 **약 43배**라는 것이 이 표에서 나온다 - 되먹임이 작지 않으므로
         * 파밍이 0이 아닌 앵커를 고르면 반드시 별도 모형이 필요하다.
         */
        public static readonly double[] GateGoldPerSecond =
            { 1.151e8d, 2.609e10d, 5.911e12d, 3.036e17d, 3.533e24d, 2.111e36d };

        // ---------------------------------------------------------------- 조립

        public static PromotionEconomyLedger.Inputs Build(int[] promotionCosts,
                                                          double allocation,
                                                          int days)
        {
            return Build(promotionCosts, allocation, days, DailyGems, DailyPlaySeconds);
        }

        public static PromotionEconomyLedger.Inputs Build(int[] promotionCosts,
                                                          double allocation,
                                                          int days,
                                                          double dailyGems,
                                                          double dailyPlaySeconds)
        {
            return Build(promotionCosts, allocation, days, dailyGems, dailyPlaySeconds,
                         TrialSecondsFloor, 1d, NoFarm);
        }

        public static PromotionEconomyLedger.Inputs Build(int[] promotionCosts,
                                                          double allocation,
                                                          int days,
                                                          double dailyGems,
                                                          double dailyPlaySeconds,
                                                          double[] trialSeconds,
                                                          double attempts,
                                                          double[] farmSeconds)
        {
            return new PromotionEconomyLedger.Inputs
            {
                ProgressionGemsByStage = ProgressionGems,
                CumulativeCombatSeconds = CumulativeSeconds,

                ArmorGradeCostGems = GradeCosts,
                ArmorGradeStage = ArmorGradeStages,
                WeaponGradeCostGems = GradeCosts,
                WeaponGradeStage = WeaponGradeStages,
                PetUnlockCostGems = PetCosts,
                PetUnlockStage = PetStages,

                PromotionCostGems = promotionCosts,
                PromotionGateStage = GateStages,

                DailyGems = dailyGems,
                PromotionAllocation = allocation,
                DailyPlaySeconds = dailyPlaySeconds,
                Days = days,

                TrialSecondsByGate = trialSeconds,
                TrialEntrySeconds = TrialEntrySeconds,
                TrialResultSeconds = TrialResultSeconds,
                TrialReturnSeconds = TrialReturnSeconds,
                TrialAttempts = attempts,
                GateFarmSeconds = farmSeconds,
                GateGoldPerSecond = GateGoldPerSecond
            };
        }

        /** 균일한 파밍 시간 배열. 감도 분석용 */
        public static double[] UniformFarm(double seconds)
        {
            return new[] { seconds, seconds, seconds, seconds, seconds, seconds };
        }

        public static int Total(int[] costs)
        {
            int sum = 0;
            for (int i = 0; i < costs.Length; i++) sum += costs[i];
            return sum;
        }
    }
}
