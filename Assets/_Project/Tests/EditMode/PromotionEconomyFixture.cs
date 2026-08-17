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
            301, 301, 301, 306, 306, 306, 306, 311, 311, 444, 444, 449, 449, 449, 489, 489, 494, 494, 494, 567,
            572, 572, 572, 572, 632, 632, 637, 637, 637, 730, 730, 735, 735, 735, 735, 735, 740, 740, 740, 753,
            753, 758, 758, 758, 758, 758, 763, 823, 823, 836, 836, 841, 841, 841, 841, 841, 841, 846, 846, 859,
            859, 859, 859, 859, 864, 864, 864, 864, 864, 877, 877, 882, 882, 882, 882, 882, 882, 882, 882, 895,
            900, 900, 900, 900, 900, 900, 900, 900, 900, 913, 918, 918, 918, 918, 918, 918, 918, 918, 918, 931,
            936, 936, 936, 936, 936, 936, 936, 936, 936, 949, 949, 954, 954, 954, 954, 954, 954, 954, 954, 967,
            967, 972, 972, 972, 972, 972, 972, 972, 972, 985, 985, 985, 990, 990, 990, 990, 990, 990, 990, 1003,
            1003, 1003, 1003, 1008, 1008, 1008, 1008, 1008, 1008, 1021, 1021, 1021, 1021, 1026, 1026, 1026, 1026, 1026, 1026, 1039,
            1039, 1039, 1039, 1039, 1044, 1044, 1044, 1044, 1044, 1057, 1057, 1057, 1057, 1057, 1057, 1062, 1062, 1062, 1062, 1075
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
            894.6, 917.9, 941.7, 966.2, 993.9, 1015.7, 1038.6, 1062.2, 1086.3, 1117.6,
            1138.8, 1160.8, 1183.5, 1206.4, 1231.9, 1253.0, 1274.8, 1297.2, 1320.0, 1349.1,
            1367.4, 1386.4, 1405.6, 1425.2, 1446.8, 1465.4, 1484.7, 1504.2, 1524.0, 1548.6,
            1567.0, 1586.1, 1605.8, 1625.7, 1647.6, 1666.5, 1685.8, 1705.5, 1724.5, 1748.0,
            1764.8, 1782.1, 1799.6, 1817.4, 1836.6, 1853.4, 1870.6, 1888.1, 1905.8, 1926.9,
            1943.1, 1959.7, 1976.6, 1993.7, 2012.1, 2028.4, 2045.0, 2061.9, 2078.9, 2094.5,
            2107.8, 2121.2, 2134.7, 2148.3, 2162.4, 2175.6, 2188.9, 2202.4, 2215.9, 2230.9,
            2243.8, 2256.8, 2269.9, 2283.1, 2296.9, 2309.7, 2322.7, 2335.8, 2349.0, 2363.6,
            2376.3, 2389.2, 2402.2, 2415.2, 2428.8, 2441.6, 2454.4, 2467.3, 2480.4, 2494.8,
            2507.3, 2520.1, 2532.9, 2545.8, 2559.2, 2571.8, 2584.6, 2597.5, 2610.4, 2624.7,
            2637.3, 2650.0, 2662.8, 2675.7, 2689.2, 2701.9, 2714.6, 2727.6, 2740.5, 2754.8,
            2767.4, 2780.1, 2792.9, 2805.9, 2819.3, 2831.9, 2844.7, 2857.6, 2870.6, 2884.8,
            2897.2, 2909.7, 2922.4, 2935.1, 2948.3, 2960.7, 2973.4, 2986.1, 2998.8, 3012.8,
            3025.2, 3037.7, 3050.4, 3063.1, 3076.3, 3088.8, 3101.4, 3114.1, 3126.9, 3140.9,
            3153.4, 3166.0, 3178.7, 3191.4, 3204.8, 3217.3, 3230.0, 3242.7, 3255.5, 3269.6,
            3282.1, 3294.7, 3307.4, 3320.2, 3333.5, 3345.9, 3358.5, 3371.3, 3384.0, 3398.1,
            3410.5, 3423.1, 3435.7, 3448.4, 3461.7, 3474.1, 3486.8, 3499.5, 3512.2, 3526.2
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
