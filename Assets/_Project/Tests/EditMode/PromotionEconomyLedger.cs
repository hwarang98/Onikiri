using System;

namespace Onikiri.Tests
{
    /**
     * @brief 승급 사다리의 **날짜별 보석 장부**. 테스트 전용 순수 계산기다.
     *
     * ## 왜 식 하나로는 안 되는가
     *
     * 설계 문서 v1.1은 `D = (누적비용 - P(gate)) / (일일 x 배정률)` 한 줄로
     * 일수를 냈다. 그 식의 `P(gate)`는 **현행 전직 비용으로 돌린 시뮬레이션의
     * 결과**다 - 새 비용이 앞에서 보석을 먹으면 동료·무기 구매 시점이 밀리고,
     * 밀린 시점이 다시 `P(gate)`를 바꾼다. 입력이 출력에 의존하는 값을 상수로
     * 쓴 셈이고, 그러면 후보 비용을 바꿀 때마다 답이 조용히 틀린다.
     *
     * 이 계산기는 그 순환을 **풀지 않고 시뮬레이션한다.** 하루씩 걸으며 실제로
     * 지갑에서 빼므로 `P(gate)`가 입력이 아니라 출력이 되고, 반복 재평가가
     * 필요 없어진다.
     *
     * ## 두 축이 직교한다 - 우선순위와 배정 정책
     *
     *   우선순위   엔진(StageSimulation.Buy)의 호출 차례다. 하루 안에서
     *              방어구 -> 경지 -> 동료 -> 무기 -> 뽑기 순으로 지갑을 쓴다
     *   배정 정책  플레이어의 장기 습관이다. 누적 유입의 alpha만큼을 경지에
     *              배정하고, 경지 누적 지출이 그 예산을 넘지 못한다
     *
     * 둘을 섞으면 "엔진이 경지를 먼저 부르니까 경지가 다 먹는다"가 되어
     * alpha가 아무 일도 안 하게 된다. 나눠 두면 alpha=0이 곧 "경지를 안 사는
     * 세계"이고, 그 세계와의 차이가 **경지 때문에 지연된 장비·동료 단계**다.
     *
     * ## 모델링하지 않는 것
     *
     * 골드. 전직에는 골드도 들지만 골드는 진행이 곧 수입이라 날짜의 함수가
     * 아니고(기대 스테이지의 보스 보상 한 번이 앵커다), 이 계산기가 답해야
     * 하는 질문은 "며칠 걸리는가"이며 그 답은 보석이 정한다. 골드가 병목이
     * 되는 세계는 StageSimulation이 이미 잰다.
     */
    public static class PromotionEconomyLedger
    {
        // ---------------------------------------------------------------- 입력

        public struct Inputs
        {
            /**
             * @brief 스테이지 끝까지의 **누적** 진행 보석 (업적 + 반복 티어).
             *
             * 인덱스 0 = st1. 일일은 들어 있지 않다 - 시뮬레이션에 달력이
             * 없어서이고(StageResult.GemsEarned 주석), 그 사실이 이 계산기가
             * 존재하는 이유다.
             */
            public int[] ProgressionGemsByStage;

            /** 스테이지 끝까지의 누적 전투 시간(초). 인덱스 0 = st1 */
            public double[] CumulativeCombatSeconds;

            /** 방어구 등급업 넷의 보석 값과 자격 스테이지 (단련 레벨이 정한다) */
            public int[] ArmorGradeCostGems;
            public int[] ArmorGradeStage;

            public int[] WeaponGradeCostGems;
            public int[] WeaponGradeStage;

            public int[] PetUnlockCostGems;
            public int[] PetUnlockStage;

            /** 경지 여섯의 보석 값과 게이트 스테이지 (그 스테이지를 클리어해야 열린다) */
            public int[] PromotionCostGems;
            public int[] PromotionGateStage;

            /** 일일 퀘스트 다섯의 합. 전부 완료 가정 */
            public double DailyGems;

            /** 경지 배정률. 0 = 경지를 안 산다, 1 = 방어구 다음은 전부 경지 */
            public double PromotionAllocation;

            /** 하루 활성 전투 시간(초). 진행이 병목인지 보는 손잡이다 */
            public double DailyPlaySeconds;

            /** 며칠까지 걸을 것인가 */
            public int Days;

            // ------------------------------------------------------------ 귀문 시간

            /**
             * @brief 게이트별 귀문 전투 시간(초). **1.6단계에 들어온 항이다.**
             *
             * v1.3까지 이 장부는 스테이지 전투 시간만 걸었다. 귀문 여섯 번이
             * 실제로 시계를 먹는데 그것이 도달일에 안 들어가 있었고, 그래서
             * "st150까지 사흘"이 실제보다 낙관이었다.
             */
            public double[] TrialSecondsByGate;

            /** 진입 연출(초) */
            public double TrialEntrySeconds;

            /** 결과 화면(초) */
            public double TrialResultSeconds;

            /** 전직 패널 복귀(초) */
            public double TrialReturnSeconds;

            /**
             * @brief 평균 도전 횟수. 1.0이면 첫 시도 통과.
             *
             * **성공 시간에 곱하지 않는다.** 1.5회는 "45초짜리를 1.5번" 이
             * 아니라 "실패 0.5번 + 성공 1번"이고, 실패는 성공보다 두 배 넘게
             * 걸린다(격노를 지나 죽거나 폐쇄에 닿는다). 곱으로 치면 재도전
             * 비용이 절반 이하로 과소평가된다 - 2단계에 고친 자리다.
             */
            public double TrialAttempts;

            /**
             * @brief 실패한 시도 하나의 시간(초). 게이트별 **실측**이다.
             *
             * 성공은 45초에 끝나지만 실패는 그 자리에서 끝나지 않는다 -
             * 화력이 모자라면 격노(90초)를 지나 죽거나 폐쇄(180초)에 닿는다.
             * 실측 범위가 90~108초라 성공의 두 배가 넘고, 그래서 이 항이
             * 없으면 재도전이 있는 세계의 도달일이 통째로 낙관이 된다.
             *
             * 비어 있으면 성공 시간으로 대신한다 - 옛 계약(곱하기 모형)과
             * 같은 답을 내므로, 이 항을 안 채운 호출자가 조용히 다른 세계를
             * 재는 일이 없다.
             */
            public double[] TrialFailureSecondsByGate;

            /**
             * @brief 게이트에서 못 넘어 반복 파밍한 시간(초).
             *
             * 하한 앵커에서는 0이다(도착 즉시 통과). 다른 앵커를 고르면 0이
             * 아니고, 그때는 파밍이 골드를 쌓아 다음 게이트를 쉽게 만드는
             * **되먹임**이 생긴다 - 이 장부는 그 시간을 세고 골드를 보고할
             * 뿐, 되먹임을 화력으로 환산하지는 않는다(§ 그것은 StageSimulation의 일이다).
             */
            public double[] GateFarmSeconds;

            /** 게이트 스테이지의 초당 골드. 파밍 골드 보고에만 쓴다 */
            public double[] GateGoldPerSecond;
        }

        // ---------------------------------------------------------------- 출력

        public struct Result
        {
            /** 경지 k(1부터)를 얻은 날. 못 얻었으면 -1 */
            public int[] TierDay;

            /** 그날의 최전선 스테이지 */
            public int[] TierStage;

            /**
             * @brief 문이 열린 날의 **경지 예산 잔액** - 재계산된 P(gate).
             *
             * v1.1이 상수로 쓰던 값이 여기서는 출력이다. 후보 비용을 바꾸면
             * 이 값도 바뀐다는 것이 이 계산기의 요점이다.
             */
            public int[] GateBudgetAtOpen;

            /** 문이 열린 날 (조건 충족). 실제 취득일과 다르면 그 차이가 보석 대기다 */
            public int[] GateOpenDay;

            public int ArmorDoneDay;
            public int WeaponDoneDay;
            public int PetDoneDay;

            public long SpentPromotion;
            public long SpentArmor;
            public long SpentWeapon;
            public long SpentPet;
            public long SpentGacha;

            public long TotalEarned;

            /** 마지막 날의 잔액. 어느 소비처에도 안 간 보석이다 */
            public long Unused;

            /** 하루하루의 지갑 잔액. 음수가 한 번이라도 나오면 장부가 틀렸다 */
            public long[] WalletOnDay;

            /** 하루하루의 최전선 스테이지 */
            public int[] StageOnDay;

            /** 각 단계를 산 날. 못 샀으면 -1 */
            public int[] ArmorStepDay;
            public int[] WeaponStepDay;
            public int[] PetStepDay;

            // ------------------------------------------------------------ 귀문 시간

            /** 귀문에 쓴 총 시간(초). 전투 + 오버헤드 x 도전 횟수 */
            public double TrialSecondsTotal;

            /** 게이트에서 반복 파밍한 총 시간(초) */
            public double FarmSecondsTotal;

            /** 그 파밍이 벌어들인 골드 */
            public double FarmedGold;

            /** 귀문·파밍이 st150 도달에 더한 시간의 비율 */
            public double TrialTimeShareToGate6;
        }

        // ---------------------------------------------------------------- 걷기

        public static Result Run(Inputs input)
        {
            int days = input.Days;
            int tiers = input.PromotionCostGems.Length;

            var result = new Result
            {
                TierDay = Filled(tiers, -1),
                TierStage = new int[tiers],
                GateBudgetAtOpen = Filled(tiers, -1),
                GateOpenDay = Filled(tiers, -1),
                ArmorDoneDay = -1,
                WeaponDoneDay = -1,
                PetDoneDay = -1,
                WalletOnDay = new long[days + 1],
                StageOnDay = new int[days + 1],
                ArmorStepDay = Filled(input.ArmorGradeCostGems.Length, -1),
                WeaponStepDay = Filled(input.WeaponGradeCostGems.Length, -1),
                PetStepDay = Filled(input.PetUnlockCostGems.Length, -1)
            };

            long wallet = 0;
            long earned = 0;
            long spentPromotion = 0;

            int armorStep = 0, weaponStep = 0, petStep = 0, tier = 0;
            int previousStage = 0;

            for (int day = 1; day <= days; day++)
            {
                int stage = StageOnDay(input, day);
                result.StageOnDay[day] = stage;

                // 유입. 진행 보석은 **최전선이 나아간 만큼**만 들어온다 - 같은
                // 스테이지를 다시 돌아도 업적은 두 번 주지 않는다
                long progression = CumulativeProgression(input, stage)
                                 - CumulativeProgression(input, previousStage);
                long income = progression + (long)input.DailyGems;

                earned += income;
                wallet += income;
                previousStage = stage;

                // 경지 예산은 **누적 유입의 alpha**다. 하루치가 아니라 누적인
                // 이유는 배정이 저축이기 때문이다 - 오늘 안 쓴 경지 몫이
                // 내일 사라지면 alpha는 배정이 아니라 소멸이 된다
                long promotionBudget = (long)Math.Floor(input.PromotionAllocation * earned) - spentPromotion;
                if (promotionBudget < 0) promotionBudget = 0;

                // ------------------------------------------------ 1. 방어구 등급
                while (armorStep < input.ArmorGradeCostGems.Length
                       && stage >= input.ArmorGradeStage[armorStep]
                       && wallet >= input.ArmorGradeCostGems[armorStep])
                {
                    wallet -= input.ArmorGradeCostGems[armorStep];
                    result.SpentArmor += input.ArmorGradeCostGems[armorStep];
                    result.ArmorStepDay[armorStep] = day;
                    armorStep++;
                    if (armorStep == input.ArmorGradeCostGems.Length) result.ArmorDoneDay = day;
                }

                // ------------------------------------------------ 2. 경지
                while (tier < tiers && stage >= input.PromotionGateStage[tier])
                {
                    // 문이 열린 날과 재계산된 P(gate)를 처음 한 번만 적는다
                    if (result.GateOpenDay[tier] < 0)
                    {
                        result.GateOpenDay[tier] = day;
                        result.GateBudgetAtOpen[tier] = (int)Math.Min(promotionBudget, wallet);
                    }

                    int cost = input.PromotionCostGems[tier];
                    if (cost > promotionBudget || cost > wallet) break;

                    wallet -= cost;
                    promotionBudget -= cost;
                    spentPromotion += cost;
                    result.SpentPromotion += cost;
                    result.TierDay[tier] = day;
                    result.TierStage[tier] = stage;
                    tier++;
                }

                // ------------------------------------------------ 3. 동료 해금
                while (petStep < input.PetUnlockCostGems.Length
                       && stage >= input.PetUnlockStage[petStep]
                       && wallet >= input.PetUnlockCostGems[petStep])
                {
                    wallet -= input.PetUnlockCostGems[petStep];
                    result.SpentPet += input.PetUnlockCostGems[petStep];
                    result.PetStepDay[petStep] = day;
                    petStep++;
                    if (petStep == input.PetUnlockCostGems.Length) result.PetDoneDay = day;
                }

                // ------------------------------------------------ 4. 무기 등급
                while (weaponStep < input.WeaponGradeCostGems.Length
                       && stage >= input.WeaponGradeStage[weaponStep]
                       && wallet >= input.WeaponGradeCostGems[weaponStep])
                {
                    wallet -= input.WeaponGradeCostGems[weaponStep];
                    result.SpentWeapon += input.WeaponGradeCostGems[weaponStep];
                    result.WeaponStepDay[weaponStep] = day;
                    weaponStep++;
                    if (weaponStep == input.WeaponGradeCostGems.Length) result.WeaponDoneDay = day;
                }

                // ------------------------------------------------ 5. 뽑기
                //
                // **핵심 축이 전부 끝난 뒤에만 돈다.** 그 전에 돌면 이 모델이
                // 재는 플레이어가 "장비를 굶기고 뽑기를 도는 사람"이 되고,
                // 그러면 일수 계약이 실제 무과금과 다른 사람을 재게 된다.
                //
                // 경지 예산은 뽑기에 안 쓴다 - 배정의 뜻이 그것이다
                bool coreDone = armorStep == input.ArmorGradeCostGems.Length
                             && weaponStep == input.WeaponGradeCostGems.Length
                             && petStep == input.PetUnlockCostGems.Length;

                if (coreDone)
                {
                    // 예약은 **아직 안 낸 경지 비용**까지만이다. 배정 예산 전체를
                    // 묶어 두면, 승급이 무료가 된 세계에서 쓸 곳 없는 예산이
                    // 뽑기를 막는다 - 1.6단계에 실제로 그렇게 나왔다(배정률이
                    // 뽑기 지출을 움직였다).
                    long remainingCost = 0;
                    for (int t = tier; t < tiers; t++) remainingCost += input.PromotionCostGems[t];

                    long budget = (long)Math.Floor(input.PromotionAllocation * earned) - spentPromotion;
                    if (budget < 0) budget = 0;

                    long reserved = Math.Min(budget, remainingCost);
                    long spendable = wallet - reserved;
                    while (spendable >= TenPullGems)
                    {
                        wallet -= TenPullGems;
                        spendable -= TenPullGems;
                        result.SpentGacha += TenPullGems;
                    }
                }

                result.WalletOnDay[day] = wallet;
            }

            result.TotalEarned = earned;
            result.Unused = wallet;

            // ------------------------------------------------------------ 귀문 시간 보고
            if (input.TrialSecondsByGate != null)
            {
                for (int g = 0; g < input.TrialSecondsByGate.Length; g++)
                {
                    result.TrialSecondsTotal += TrialSecondsForGate(input, g);

                    if (input.GateFarmSeconds != null && g < input.GateFarmSeconds.Length)
                    {
                        result.FarmSecondsTotal += input.GateFarmSeconds[g];

                        if (input.GateGoldPerSecond != null && g < input.GateGoldPerSecond.Length)
                            result.FarmedGold += input.GateFarmSeconds[g] * input.GateGoldPerSecond[g];
                    }
                }

                int lastGate = input.PromotionGateStage[input.PromotionGateStage.Length - 1];
                double stageOnly = CumulativeProgressionSeconds(input, lastGate);
                double added = TrialSecondsThrough(input, lastGate + 1);
                result.TrialTimeShareToGate6 = stageOnly > 0d ? added / (stageOnly + added) : 0d;
            }

            return result;
        }

        static double CumulativeProgressionSeconds(Inputs input, int stage)
        {
            var table = input.CumulativeCombatSeconds;
            if (stage <= 0 || table == null || table.Length == 0) return 0d;
            return table[Math.Min(stage, table.Length) - 1];
        }

        /** 오의·요도 배너가 공유하는 10연 값 (GachaCurve.TenPullCostGems) */
        public const int TenPullGems = 225;

        // ---------------------------------------------------------------- 보조

        /**
         * @brief 이 문 하나에 실제로 들어가는 시간(초). **2단계에 고친 식이다.**
         *
         * ## 옛 식이 무엇을 틀렸는가
         *
         *     (성공 시간 + 오버헤드) x 도전 횟수          <- 틀림
         *
         * `TrialAttempts = 1.5`를 성공 시간에 곱하면 "45초짜리를 1.5번"이 되어
         * 67.5초가 나온다. 실제로는 **실패 0.5번 + 성공 1번**이고, 실패는
         * 격노를 지나 죽거나 폐쇄에 닿으므로 90~108초다. 같은 1.5회가
         * 45 + 0.5 x 99 = 94.5초여서, 옛 식은 재도전 비용을 30% 넘게
         * 과소평가했다.
         *
         * ## 고친 식
         *
         *     전투     = 성공 시간 + max(0, 횟수 - 1) x 실패 시간
         *     오버헤드 = 횟수 x (진입 + 결과 + 복귀)
         *
         * 오버헤드에 횟수를 곱하는 것은 옛 식과 같다 - **실패한 시도도 진입
         * 연출과 결과 화면을 지난다.** 그 항만은 원래 맞았다.
         */
        public static double TrialSecondsForGate(Inputs input, int gateIndex)
        {
            if (input.TrialSecondsByGate == null) return 0d;
            if (gateIndex < 0 || gateIndex >= input.TrialSecondsByGate.Length) return 0d;

            double attempts = input.TrialAttempts > 0d ? input.TrialAttempts : 1d;
            double overhead = input.TrialEntrySeconds + input.TrialResultSeconds + input.TrialReturnSeconds;

            double success = input.TrialSecondsByGate[gateIndex];
            double failure = input.TrialFailureSecondsByGate != null
                          && gateIndex < input.TrialFailureSecondsByGate.Length
                ? input.TrialFailureSecondsByGate[gateIndex]
                : success;

            return success
                 + Math.Max(0d, attempts - 1d) * failure
                 + attempts * overhead;
        }

        /**
         * @brief 이 스테이지에 **닿기 위해** 지불한 귀문 시간의 합.
         *
         * 귀문은 게이트 스테이지를 클리어한 뒤 다음 스테이지로 넘어갈 때
         * 막으므로, `stage`에 서려면 `GateStage < stage`인 문을 전부 넘었어야
         * 한다. 등호가 아니라 부등호인 것이 이 계약의 전부다.
         */
        public static double TrialSecondsThrough(Inputs input, int stage)
        {
            if (input.TrialSecondsByGate == null) return 0d;

            double total = 0d;
            for (int g = 0; g < input.TrialSecondsByGate.Length; g++)
            {
                if (input.PromotionGateStage[g] >= stage) continue;

                total += TrialSecondsForGate(input, g);
                if (input.GateFarmSeconds != null && g < input.GateFarmSeconds.Length)
                    total += input.GateFarmSeconds[g];
            }
            return total;
        }

        /** 이 날 하루 활성 전투 시간으로 닿는 최전선 스테이지. **귀문 시간을 포함한다** */
        public static int StageOnDay(Inputs input, int day)
        {
            double budget = input.DailyPlaySeconds * day;
            var cumulative = input.CumulativeCombatSeconds;

            int stage = 0;
            for (int i = 0; i < cumulative.Length; i++)
            {
                // i+1 스테이지를 끝내는 시각 = 그 스테이지까지의 전투 시간
                //                            + 거기 오기까지 넘은 문들의 시간
                if (cumulative[i] + TrialSecondsThrough(input, i + 1) > budget) break;
                stage = i + 1;
            }
            return stage;
        }

        static long CumulativeProgression(Inputs input, int stage)
        {
            if (stage <= 0) return 0;
            var table = input.ProgressionGemsByStage;
            int index = Math.Min(stage, table.Length) - 1;
            return table[index];
        }

        static int[] Filled(int length, int value)
        {
            var array = new int[length];
            for (int i = 0; i < length; i++) array[i] = value;
            return array;
        }
    }
}
