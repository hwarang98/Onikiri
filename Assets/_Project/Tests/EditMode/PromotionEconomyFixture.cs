using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 장부 계산기의 입력 실측표. 테스트 전용이다.
     *
     * 전부 `StageSimulation`에서 구웠다. 손으로 지어낸 값이 하나도 없다는 것이
     * 이 파일의 계약이고, 곡선이 움직이면 여기도 다시 구워야 한다 -
     * `PromotionEconomyTests.FixtureStillMatchesTheSimulation`이 그것을 잡는다.
     *
     * **2단계에 실제로 다시 구웠다.** 승급이 게이트 기반 무료가 되면서 하한
     * 플레이어의 티어가 st31부터 오르고, 그 화력이 두 표를 함께 움직였다 -
     * 진행 보석은 업적 달성 시점이 앞당겨져서(st36의 +5가 st35로), 전투 시간은
     * 보스 처치가 빨라져서(st200 누적 4,084.9초 -> 3,495.4초, -14.4%).
     * 굽는 쪽이 옛 세계면 도달일 계약이 아무 근거 없이 통과한다.
     *
     * **64단계에 다시 구웠다.** 골드 획득 축이 무한 성장이 되어 하한 플레이어의
     * 수입·화력이 st6부터 움직였다 - 업적 시점(st28 311 -> 306 등)과 전투
     * 시간이 함께 움직였다. 에디터 execute_code로 DevSimField 필드에서 구웠다.
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
            0, 0, 0, 0, 20, 20, 45, 65, 65, 118, 118, 118, 118, 168, 173, 173, 173, 223, 228, 301,
            301, 301, 301, 306, 306, 306, 306, 311, 311, 444, 444, 444, 449, 449, 489, 489, 489, 494, 494, 567,
            567, 567, 572, 572, 632, 632, 632, 637, 637, 730, 730, 730, 730, 735, 735, 735, 735, 735, 740, 753,
            753, 753, 753, 753, 758, 758, 758, 818, 818, 836, 836, 836, 836, 836, 836, 836, 841, 841, 841, 854,
            854, 854, 859, 859, 859, 859, 859, 859, 859, 872, 877, 877, 877, 877, 877, 877, 877, 877, 877, 890,
            895, 895, 895, 895, 895, 895, 895, 895, 895, 908, 908, 913, 913, 913, 913, 913, 913, 913, 913, 926,
            926, 926, 931, 931, 931, 931, 931, 931, 931, 944, 944, 944, 944, 949, 949, 949, 949, 949, 949, 962,
            962, 962, 962, 962, 967, 967, 967, 967, 967, 980, 980, 980, 980, 980, 980, 985, 985, 985, 985, 998,
            998, 998, 998, 998, 998, 998, 998, 1003, 1003, 1016, 1016, 1016, 1016, 1016, 1016, 1016, 1016, 1016, 1021, 1034,
            1034, 1034, 1034, 1034, 1034, 1034, 1034, 1034, 1034, 1052, 1052, 1052, 1052, 1052, 1052, 1052, 1052, 1052, 1052, 1065,
        };

        /**
         * @brief 무과금 하한의 **누적 전투 시간(초)**. 인덱스 0 = st1.
         *
         * 잡몹 + 보스 인트로(1s) + 접근(5.3s) + 보스 처치. 방치 보상은 없다 -
         * 이 값은 "손에 쥐고 있는 시간"이고, 날짜 환산의 분자다.
         */
        public static readonly double[] CumulativeSeconds =
        {
            27.6, 59.7, 91.6, 127.7, 173.8, 214.4, 249.9, 279.8, 306.8, 338.2,
            361.0, 385.0, 410.8, 437.8, 467.5, 493.0, 519.2, 545.9, 573.9, 609.8,
            633.7, 658.5, 683.7, 708.8, 737.5, 760.7, 784.4, 809.7, 835.1, 867.1,
            888.9, 911.4, 934.1, 958.0, 984.5, 1005.3, 1027.6, 1050.1, 1072.9, 1102.9,
            1123.1, 1143.9, 1165.6, 1187.2, 1212.1, 1232.3, 1252.7, 1274.4, 1296.0, 1322.8,
            1340.4, 1358.4, 1376.5, 1395.4, 1415.7, 1433.2, 1451.7, 1470.1, 1488.7, 1512.1,
            1529.5, 1547.4, 1566.2, 1584.9, 1605.2, 1623.1, 1641.1, 1659.4, 1678.3, 1701.1,
            1716.9, 1733.4, 1750.0, 1766.6, 1784.8, 1800.6, 1816.9, 1833.5, 1850.2, 1869.6,
            1885.1, 1900.8, 1916.7, 1932.9, 1950.2, 1965.9, 1981.7, 1997.7, 2011.2, 2026.1,
            2038.8, 2051.9, 2065.0, 2078.0, 2091.8, 2104.6, 2117.4, 2130.5, 2143.6, 2157.9,
            2170.4, 2183.0, 2195.7, 2208.6, 2221.8, 2234.3, 2247.0, 2259.6, 2272.3, 2286.4,
            2298.8, 2311.3, 2323.9, 2336.5, 2349.6, 2362.0, 2374.4, 2386.9, 2399.6, 2413.4,
            2425.6, 2438.0, 2450.4, 2462.9, 2475.9, 2488.1, 2500.5, 2513.0, 2525.6, 2539.5,
            2551.8, 2564.1, 2576.7, 2589.3, 2602.2, 2614.6, 2627.0, 2639.5, 2652.2, 2666.0,
            2678.1, 2690.6, 2703.1, 2715.6, 2728.7, 2740.9, 2753.3, 2765.9, 2778.5, 2792.1,
            2804.2, 2816.4, 2828.7, 2841.1, 2853.8, 2865.9, 2878.3, 2890.6, 2902.9, 2916.4,
            2928.5, 2940.7, 2953.0, 2965.4, 2978.1, 2990.2, 3002.5, 3014.8, 3027.3, 3040.7,
            3052.9, 3065.1, 3077.4, 3090.0, 3102.8, 3115.0, 3127.4, 3139.8, 3152.2, 3165.8,
            3178.0, 3190.2, 3202.6, 3215.0, 3227.8, 3240.0, 3252.2, 3264.5, 3277.0, 3290.4,
            3302.5, 3314.7, 3327.0, 3339.3, 3352.2, 3364.3, 3376.5, 3388.8, 3401.2, 3414.6,
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

        /**
         * @brief 귀문 여섯의 게이트 스테이지. 이 스테이지를 클리어해야 문이 열린다.
         *
         * 2단계부터 **표의 출처가 프로덕션**이다(`PromotionTrialCatalog`). 여기
         * 값을 다시 적어 두면 언젠가 한쪽만 바뀌고, 그때 장부가 재는 세계와
         * 게임의 세계가 갈린다.
         */
        public static int[] GateStages { get { return PromotionTrialCatalog.GateStages; } }

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

        /**
         * @brief **실패한 시도 하나의 시간(초). 게이트별 실측이다.**
         *
         * 값을 굽지 않고 매번 `PromotionTrialSimulation`을 돌리는 이유는
         * `PromotionTrialFixture` 머리 주석과 같다 - 이것은 곡선의 함수이고,
         * 구워 두면 곡선이 움직인 날 장부만 옛 세계를 잰다.
         *
         * 재는 플레이어는 **하한의 0.35배**다. 그 화력이 문을 못 넘는다는 것은
         * 폭 계약(`PowerWindow_SeventyPercentPasses_ThirtyFivePercentFails`)이
         * 이미 잡고 있으므로, 여기서 재는 것은 "못 넘는 시도가 얼마나 오래
         * 걸리는가" 하나다.
         *
         * 격노(90초)를 지나야 결판이 나므로 어떤 게이트에서도 90초 밑으로
         * 내려가지 않고, 폐쇄(180초)가 상한이다.
         * `TrialFailureSeconds_AreMeasuredNotGuessed`가 그 범위를 지킨다.
         */
        public static double[] TrialFailureSecondsFloor
        {
            get
            {
                if (failureSeconds != null) return failureSeconds;

                var rows = PromotionTrialFixture.GemFloor();
                var measured = new double[PromotionTrialCatalog.GateCount];

                for (int gate = 1; gate <= measured.Length; gate++)
                {
                    var weak = PromotionTrialFixture.PlayerAt(
                        rows, PromotionTrialCatalog.GateStages[gate - 1]);
                    weak.Dps *= 0.35d;

                    measured[gate - 1] = PromotionTrialSimulation.Run(
                        PromotionTrialFixture.Capped(weak, gate, PromotionTrialCatalog.SoftCapExponent),
                        PromotionTrialFixture.FoesForGate(gate),
                        PromotionTrialFixture.DefaultRules()).Seconds;
                }

                failureSeconds = measured;
                return failureSeconds;
            }
        }

        static double[] failureSeconds;

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

        /**
         * @brief 게이트별 파밍 시간을 **직접 주는** 조립. 민감도 분석용.
         *
         * 균일 배열(`UniformFarm`)이 기본이 아닌 이유가 있다 - 하한의 0.50배
         * 화력은 **문1에서만** 실패한다(§3.2 실측). 여섯 문에 같은 파밍을
         * 일괄로 넣으면 그 세계보다 다섯 배 비관적인 표가 나오고, 그 표로
         * 도달일을 판단하면 게이트 배치를 필요 없이 흔들게 된다.
         */
        public static PromotionEconomyLedger.Inputs BuildWithFarm(double[] farmSeconds,
                                                                  double attempts,
                                                                  int days)
        {
            return Build(FreePromotion, 0.40d, days, DailyGems, DailyPlaySeconds,
                         TrialSecondsFloor, attempts, farmSeconds);
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
                TrialFailureSecondsByGate = TrialFailureSecondsFloor,
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
            var farm = new double[PromotionTrialCatalog.GateCount];
            for (int g = 0; g < farm.Length; g++) farm[g] = seconds;
            return farm;
        }

        /**
         * @brief **문1에만** 파밍을 넣은 배열. 0.50배 화력 세계의 실제 모양이다.
         *
         * 하한의 0.50배는 문1에서만 실패하고 문2~6은 통과한다. 그 사실이
         * `PromotionTrialTests.HalfPowerFloor_OnlyFailsAtTheFirstGate`에
         * 붙잡혀 있고, 이 배열이 그 사실을 장부의 입력으로 옮긴 것이다.
         */
        public static double[] FarmAtGateOne(double seconds)
        {
            var farm = new double[PromotionTrialCatalog.GateCount];
            farm[0] = seconds;
            return farm;
        }

        public static int Total(int[] costs)
        {
            int sum = 0;
            for (int i = 0; i < costs.Length; i++) sum += costs[i];
            return sum;
        }
    }
}
