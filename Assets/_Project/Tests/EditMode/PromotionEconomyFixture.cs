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
     *
     * **65단계에 다시 구웠다.** 명중·회피 구매가 하한 플레이어의 골드 배분을
     * 바꿨다 - 같은 방법(에디터 execute_code, DevSimField)이다.
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
        // 66단계에 다시 구웠다(진행 보석·누적 시간) - 경험치 증폭이 레벨 업적을
        // 앞당기고 골드 증폭이 전투 시간을 줄였다. 소비처 표는 그대로다
        //
        // 66.1단계에 다시 구웠다(진행 보석·누적 시간) - 명중이 st1부터 팔려
        // st1~10 보스 체력에 명중 보정이 붙었다(누적 시간 st200 +2.2초). 진행
        // 보석은 st55·st61 업적이 한 스테이지씩 당겨졌다. 소비처 표는 그대로다
        //
        // 67단계에 다시 구웠다(진행 보석·누적 시간) - 명중 보정 지수 1.0 -> 0.75와
        // 피날레 배수 1.46 -> 1.52가 보스 처치 시간을 바꿨다(누적 시간 st200
        // -33.9초, 진행 보석은 5보석 업적이 몇 자리에서 한 스테이지씩 늦어졌다). 소비처 표는 그대로다
        public static readonly int[] ProgressionGems =
        {
            0, 0, 0, 0, 20, 20, 45, 65, 65, 118, 118, 118, 118, 168, 173, 173, 173, 223, 223, 301,
            301, 301, 301, 306, 306, 306, 306, 346, 351, 444, 444, 444, 444, 449, 489, 489, 489, 489, 494, 567,
            567, 567, 567, 567, 632, 632, 632, 632, 632, 730, 730, 730, 730, 730, 730, 730, 735, 735, 735, 748,
            748, 748, 753, 813, 813, 813, 813, 813, 818, 831, 831, 831, 831, 831, 831, 836, 836, 836, 836, 849,
            849, 849, 854, 854, 854, 854, 854, 854, 854, 867, 872, 872, 872, 872, 872, 872, 872, 872, 872, 885,
            885, 890, 890, 890, 890, 890, 890, 890, 890, 903, 903, 903, 908, 908, 908, 908, 908, 908, 908, 921,
            921, 921, 921, 921, 926, 926, 926, 926, 926, 939, 939, 939, 939, 939, 939, 944, 944, 944, 944, 957,
            957, 957, 957, 957, 957, 957, 957, 962, 962, 975, 975, 975, 975, 975, 975, 975, 975, 975, 980, 993,
            993, 993, 993, 993, 993, 993, 993, 993, 993, 1011, 1011, 1011, 1011, 1011, 1011, 1011, 1011, 1011, 1011, 1024,
            1029, 1029, 1029, 1029, 1029, 1029, 1029, 1029, 1029, 1042, 1042, 1042, 1047, 1047, 1047, 1047, 1047, 1047, 1047, 1060,
        };

        /**
         * @brief 무과금 하한의 **누적 전투 시간(초)**. 인덱스 0 = st1.
         *
         * 잡몹 + 보스 인트로(1s) + 접근(5.3s) + 보스 처치. 방치 보상은 없다 -
         * 이 값은 "손에 쥐고 있는 시간"이고, 날짜 환산의 분자다.
         */
        public static readonly double[] CumulativeSeconds =
        {
            28.0, 59.7, 91.6, 127.1, 171.4, 210.6, 244.8, 273.0, 298.4, 328.5,
            350.3, 373.0, 397.4, 422.6, 450.7, 475.2, 500.4, 526.0, 552.7, 587.7,
            609.9, 633.9, 658.3, 682.6, 710.5, 732.8, 755.8, 780.2, 804.5, 835.8,
            856.8, 878.5, 900.8, 923.7, 949.1, 969.8, 990.8, 1012.2, 1034.8, 1062.9,
            1081.9, 1102.2, 1122.7, 1143.0, 1166.0, 1185.0, 1204.4, 1224.8, 1244.8, 1269.9,
            1286.6, 1303.6, 1320.7, 1338.2, 1356.9, 1373.5, 1390.6, 1407.8, 1425.0, 1446.5,
            1462.7, 1479.3, 1496.5, 1513.7, 1532.2, 1548.8, 1565.8, 1583.3, 1600.6, 1621.5,
            1636.7, 1652.0, 1667.4, 1683.3, 1700.2, 1715.2, 1730.9, 1746.5, 1762.2, 1781.4,
            1796.3, 1811.4, 1826.8, 1842.3, 1858.7, 1873.7, 1888.8, 1904.0, 1917.2, 1931.6,
            1944.2, 1957.0, 1969.8, 1982.5, 1996.0, 2008.5, 2021.1, 2033.9, 2046.6, 2060.6,
            2072.9, 2085.2, 2097.8, 2110.3, 2123.2, 2135.5, 2147.8, 2160.2, 2172.8, 2186.4,
            2198.5, 2210.8, 2223.1, 2235.4, 2248.3, 2260.4, 2272.5, 2284.9, 2297.2, 2310.6,
            2322.7, 2334.8, 2346.9, 2359.3, 2371.9, 2384.0, 2396.2, 2408.5, 2420.8, 2434.4,
            2446.3, 2458.5, 2470.8, 2483.1, 2495.8, 2507.9, 2520.2, 2532.6, 2545.1, 2558.7,
            2571.0, 2583.2, 2595.6, 2608.1, 2621.0, 2633.1, 2645.5, 2658.0, 2670.4, 2684.3,
            2696.4, 2708.5, 2720.8, 2733.2, 2745.9, 2758.0, 2770.2, 2782.5, 2795.0, 2808.5,
            2820.6, 2832.8, 2845.1, 2857.5, 2870.4, 2882.5, 2894.8, 2907.2, 2919.6, 2933.2,
            2945.4, 2957.6, 2970.1, 2982.6, 2995.4, 3007.7, 3019.9, 3032.3, 3044.8, 3058.4,
            3070.5, 3082.7, 3095.0, 3107.4, 3120.3, 3132.4, 3144.6, 3157.1, 3169.5, 3182.9,
            3195.1, 3207.3, 3219.5, 3231.9, 3244.6, 3256.7, 3269.0, 3281.3, 3293.7, 3307.3,
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
