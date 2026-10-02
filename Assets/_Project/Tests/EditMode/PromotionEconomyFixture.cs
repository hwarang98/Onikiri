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
        public static readonly int[] ProgressionGems =
        {
            0, 0, 0, 0, 20, 20, 45, 65, 65, 118, 118, 118, 118, 168, 173, 173, 173, 223, 223, 301,
            301, 301, 301, 306, 306, 306, 306, 346, 351, 444, 444, 444, 449, 449, 489, 489, 489, 489, 494, 567,
            567, 567, 567, 572, 632, 632, 632, 632, 632, 730, 730, 730, 730, 730, 730, 735, 735, 735, 735, 748,
            748, 753, 753, 813, 813, 813, 813, 818, 818, 831, 831, 831, 831, 836, 836, 836, 836, 836, 836, 849,
            854, 854, 854, 854, 854, 854, 854, 854, 859, 872, 872, 872, 872, 872, 872, 872, 872, 872, 877, 890,
            890, 890, 890, 890, 890, 890, 890, 890, 890, 908, 908, 908, 908, 908, 908, 908, 908, 908, 908, 921,
            926, 926, 926, 926, 926, 926, 926, 926, 926, 939, 939, 944, 944, 944, 944, 944, 944, 944, 944, 957,
            957, 957, 957, 962, 962, 962, 962, 962, 962, 975, 975, 975, 975, 975, 980, 980, 980, 980, 980, 993,
            993, 993, 993, 993, 993, 998, 998, 998, 998, 1011, 1011, 1011, 1011, 1011, 1011, 1011, 1016, 1016, 1016, 1029,
            1029, 1029, 1029, 1029, 1029, 1029, 1029, 1034, 1034, 1047, 1047, 1047, 1047, 1047, 1047, 1047, 1047, 1047, 1052, 1065,
        };

        /**
         * @brief 무과금 하한의 **누적 전투 시간(초)**. 인덱스 0 = st1.
         *
         * 잡몹 + 보스 인트로(1s) + 접근(5.3s) + 보스 처치. 방치 보상은 없다 -
         * 이 값은 "손에 쥐고 있는 시간"이고, 날짜 환산의 분자다.
         */
        public static readonly double[] CumulativeSeconds =
        {
            28.1, 59.8, 91.5, 127.0, 171.3, 210.6, 244.7, 273.0, 298.5, 327.8,
            349.8, 372.6, 397.1, 422.6, 450.9, 475.6, 501.0, 526.9, 553.9, 588.5,
            610.9, 635.3, 660.0, 684.6, 713.0, 735.6, 759.0, 783.8, 808.5, 839.5,
            860.9, 883.0, 905.7, 929.1, 955.0, 976.0, 997.5, 1019.2, 1042.3, 1070.4,
            1089.8, 1110.5, 1131.4, 1152.1, 1175.7, 1195.0, 1214.8, 1235.7, 1256.1, 1281.3,
            1298.3, 1315.6, 1333.0, 1350.9, 1370.0, 1386.8, 1404.2, 1421.8, 1439.3, 1460.9,
            1477.3, 1494.3, 1511.7, 1529.3, 1548.1, 1565.1, 1582.3, 1600.3, 1617.9, 1638.8,
            1654.2, 1669.8, 1685.4, 1701.6, 1718.7, 1734.0, 1749.9, 1765.8, 1781.8, 1801.1,
            1816.2, 1831.4, 1847.1, 1862.8, 1879.5, 1894.7, 1910.1, 1925.6, 1938.8, 1953.3,
            1966.0, 1978.8, 1991.8, 2004.7, 2018.3, 2030.9, 2043.6, 2056.5, 2069.3, 2083.4,
            2095.8, 2108.2, 2120.8, 2133.5, 2146.5, 2158.9, 2171.3, 2183.8, 2196.4, 2210.1,
            2222.3, 2234.7, 2247.1, 2259.5, 2272.5, 2284.7, 2296.9, 2309.3, 2321.8, 2335.2,
            2347.3, 2359.5, 2371.8, 2384.2, 2397.0, 2409.1, 2421.4, 2433.8, 2446.2, 2459.8,
            2471.8, 2484.0, 2496.4, 2508.8, 2521.7, 2533.9, 2546.2, 2558.8, 2571.4, 2585.0,
            2597.3, 2609.7, 2622.1, 2634.8, 2647.8, 2660.0, 2672.5, 2685.0, 2697.6, 2711.5,
            2723.7, 2735.9, 2748.3, 2760.7, 2773.6, 2785.8, 2798.1, 2810.5, 2823.0, 2836.6,
            2848.8, 2861.1, 2873.5, 2886.0, 2899.0, 2911.2, 2923.5, 2936.0, 2948.6, 2962.2,
            2974.4, 2986.8, 2999.4, 3011.9, 3024.9, 3037.2, 3049.6, 3062.0, 3074.7, 3088.3,
            3100.4, 3112.8, 3125.2, 3137.6, 3150.7, 3162.9, 3175.2, 3187.8, 3200.2, 3213.7,
            3225.9, 3238.2, 3250.6, 3263.1, 3275.9, 3288.1, 3300.4, 3312.9, 3325.3, 3339.0,
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
