using System;

namespace Onikiri.Progression
{
    /**
     * @brief 장비의 등급/단련 곡선. **두 슬롯(무기/방어구)이 같은 곡선을 공유한다.**
     *
     * ## 왜 하나인가
     *
     * SkillCurve가 세 오의에 하나인 것과 같은 이유다. 슬롯마다 곡선을 따로 두면
     * 축이 둘이 아니라 둘 x 곡선 계수가 되고, 그중 하나만 나쁘게 잡히면 그
     * 슬롯은 함정이 된다 - 화면에서는 두 줄이 똑같이 생겼는데 하나만 올리면
     * 손해인 상태다.
     *
     * 두 슬롯의 차이는 곡선이 아니라 **먹이는 스탯과 첫 비용**이 만든다
     * (EquipmentCatalog). %DPS와 %EHP는 이 게임에서 골드당 가격이 원래 다르고
     * (11단계에 자가 갈라진 이유), 그 차이는 비용 하나로 표현하는 것이 맞다.
     *
     * ## 곱연산이다. 가산이 아니다
     *
     * 장비 값은 강화 곡선에 **곱해진다** - 스탯 포인트 증폭과 같은 자리다
     * (UpgradeSystem.Apply).
     *
     *     공격력 = 공격력강화(L) x 스탯증폭 x **무기배수**
     *     최대체력 = 체력강화(L) x 체력증폭 x **방어구배수**
     *
     * 가산으로 두면 이 축이 반드시 죽는다. 강화 곡선은 스테이지마다 x1.72의
     * 골드로 지수 성장하는데 장비가 고정값을 더하면 그 몫이 스테이지마다
     * 절반씩 줄어들고, 어느 지점부터 장비 화면은 눌러도 숫자가 안 움직이는
     * 버튼 두 줄이 된다. 16단계 스탯 포인트에서 이미 겪은 자리다.
     *
     * ## 그래서 상한이 구조적 요구다
     *
     * 곱연산이고 상한이 없으면 DPS가 강화 레벨의 **제곱**으로 자란다 - 장비도
     * 골드로 사고 공격력도 골드로 사므로 둘 다 같은 수입 곡선을 따라 오른다.
     * SkillCurve가 상한을 "밸런스 손잡이가 아니라 구조적 요구"라고 적은 것과
     * 같은 산수이고, GoldGainCurve도 같은 이유로 상한을 갖는다.
     *
     * 여기서 상한은 **등급이 연다.** 등급 g는 단련 레벨을 g x LevelsPerGrade
     * 까지만 허용하므로, 골드로는 그 칸까지밖에 못 간다. 그 위를 열려면 보석이
     * 든다 - 그것이 이 단계의 보석 소비처다.
     */
    public static class EquipmentCurve
    {
        /**
         * @brief 등급 수.
         *
         * 다섯이다. 넷이면 마지막 등급이 st20 언저리에 닿아 뒤가 비고, 여섯이면
         * 보석 가격을 여섯 칸으로 나눠야 해서 한 칸의 도약이 작아진다 -
         * "큰 도약"이 이 재화의 존재 이유인데 그것이 흐려진다.
         */
        public const int GradeCount = 5;

        /**
         * @brief 등급 하나가 여는 단련 칸 수.
         *
         * **둘이다.** 총 10레벨이고, 그중 아홉 칸이 골드, 네 칸이 보석이다.
         *
         * ## 넷이었다가 줄였다 - 밴드가 축의 총량을 정하고, 총량이 칸 수를 정한다
         *
         * 처음에 넷(총 20레벨)으로 잡았다. 그러려면 한 칸이 1% 위여야 하는데
         * (16단계의 MinimumFeltGain), 열아홉 칸이 전부 1% 위이면 축의 총량이
         * 최소 x1.21이 되고 등급 도약까지 얹으면 x1.7을 넘는다.
         *
         * 그 크기가 피날레 밴드에 들어가지 않는다는 것이 이 스텝의 실측이다
         * (아래 무기 계수 주석과 EquipmentCatalog). 총량이 정해지면 칸 수는
         * 따라온다 - **작은 축을 잘게 나누면 각 칸이 죽고, 굵게 나누면 산다.**
         *
         * 남는 대가는 골드로 할 일이 아홉 번뿐이라는 것이다. 그 아홉 번이
         * 스무 스테이지에 흩어지는 이유는 등급 관문이 보석으로 잠겨 있기
         * 때문이고, 그것이 이 축의 리듬이다.
         */
        public const int LevelsPerGrade = 2;

        /** 5등급 마지막 칸. 단련 레벨은 등급을 넘어 **이어진다** */
        public const int MaxLevel = GradeCount * LevelsPerGrade;

        /**
         * @brief 한 칸의 배수는 **슬롯마다 다르다.** 여기 있는 것은 구조뿐이다.
         *
         * SkillCurve가 세 오의에 곡선을 공유하면서 배율과 쿨다운만 다르게 둔
         * 것과 같은 구조다. 공유하는 것은 등급 수·칸 수·비용 증가율이고,
         * 슬롯이 정하는 것은 **한 칸이 얼마나 큰가**다
         * (EquipmentSpec.TemperStep / GradeStep).
         *
         * ## 왜 계수까지 공유하지 못했는가 - 밴드가 둘을 다르게 대한다
         *
         * 처음에는 둘 다 x2.0으로 두고 계수까지 공유했다. 실측에서 **st30
         * 피날레 여유가 천장 1.70에 정확히 닿았다.**
         *
         * 원인은 두 스탯이 서는 밴드가 다르다는 것이다.
         *
         *   무기(%DPS)   보스 여유 밴드에 **천장이 있다.** 피날레는 1.15~1.70,
         *                비가 1.478뿐인데 기존 st10(1.65)과 st20(1.35)이 이미
         *                1.22를 쓰고 있다. 남는 자리가 x1.21이다
         *   방어구(%EHP) 생존 밴드는 **바닥만 있다**(>= 1.16). 위로는 얼마든지
         *                두꺼워져도 되고, 여유로워진 만큼 골드가 화력으로 가는
         *                간접 효과만 밴드에 잡힌다
         *
         * 그래서 무기는 x1.70, 방어구는 x2.0이다. 같은 크기로 맞추는 것이
         * 대칭이지만, 그 대칭은 **화면에서 보이지 않고 밴드에서만 보인다** -
         * 두 줄은 여전히 5등급 20칸으로 같은 모양이고 같은 리듬으로 오른다.
         */

        /** 슬롯의 계수로 계산한 20레벨 만점의 총 배수 */
        public static double CeilingFor(double gradeStep, double temperStep)
        {
            return Math.Pow(gradeStep, GradeCount - 1) * Math.Pow(temperStep, MaxLevel - 1);
        }

        // ---------------------------------------------------------------- 값

        /** 이 등급이 허용하는 마지막 단련 레벨 */
        public static int MaxLevelForGrade(int grade)
        {
            int g = Clamp(grade, 1, GradeCount);
            return g * LevelsPerGrade;
        }

        /** 이 단련 레벨을 담고 있는 등급. 등급업 조건 판정이 쓴다 */
        public static int GradeOfLevel(int level)
        {
            int l = Clamp(level, 1, MaxLevel);
            return Clamp(1 + (l - 1) / LevelsPerGrade, 1, GradeCount);
        }

        /**
         * @brief 이 등급/레벨의 배수. **레벨이 등급 상한을 넘으면 잘린다.**
         *
         * 자르는 것이 중요하다. 상한이 내려간 업데이트에서도 세이브의 레벨을
         * 깎지 않는 것이 이 프로젝트의 규칙이고(SkillSystem.RestoreLevels),
         * 그러면 "등급이 허용하는 것보다 높은 레벨"이 실제로 존재할 수 있다.
         * 값에서 자르면 등급이 다시 오를 때 잠든 레벨이 깨어난다.
         */
        public static double ValueAt(double gradeStep, double temperStep, int grade, int level)
        {
            int g = Clamp(grade, 1, GradeCount);
            int l = Clamp(level, 1, MaxLevelForGrade(g));

            return Math.Pow(gradeStep, g - 1) * Math.Pow(temperStep, l - 1);
        }

        /** 단련을 한 칸 올렸을 때의 배수. 화면의 "전 -> 후"가 쓴다 */
        public static double NextTemperValue(double gradeStep, double temperStep, int grade, int level)
        {
            return ValueAt(gradeStep, temperStep, grade, level + 1);
        }

        /**
         * @brief 등급을 한 칸 올렸을 때의 배수.
         *
         * **레벨은 그대로 둔다.** 등급업에서 레벨을 1로 되돌리는 설계도 흔하지만
         * 여기서는 하지 않는다 - 되돌리면 배수가 GradeStep / TemperStep^(칸수-1)
         * 배가 되어, 계수에 따라 **등급을 올렸는데 약해지는** 상태가 생긴다.
         * 이어지게 두면 등급업은 언제나 정확히 GradeStep 배다.
         */
        public static double NextGradeValue(double gradeStep, double temperStep, int grade, int level)
        {
            return ValueAt(gradeStep, temperStep, grade + 1, level);
        }

        // ---------------------------------------------------------------- 상태

        /** 지금 등급에서 더 단련할 칸이 남아 있는가 */
        public static bool CanTemper(int grade, int level)
        {
            return level < MaxLevelForGrade(grade);
        }

        /**
         * @brief 등급업이 열려 있는가. **단련을 끝까지 올린 뒤에만 열린다.**
         *
         * ## 왜 순서를 강제하는가
         *
         * 보석이 진행을 앞지르지 못하게 하는 장치다. 순서가 없으면 보석을 모아둔
         * 플레이어가 골드를 한 푼도 쓰지 않고 5등급까지 뛸 수 있고, 그러면 이
         * 축의 실제 크기가 "그 사람이 며칠 접속했는가"에 좌우된다. 시뮬레이션이
         * 잴 수 없는 변수가 밸런스의 한가운데로 들어오는 셈이다.
         *
         * 단련을 먼저 채우게 하면 **골드가 속도를 정하고 보석은 관문을 연다.**
         * 그 둘이 E-3이 요구한 "보석 = 등급 게이트(도약) / 골드 = 단련(점진)"이고,
         * 화면에서 두 재화가 섞이지 않는 이유이기도 하다.
         */
        public static bool CanUpgradeGrade(int grade, int level)
        {
            return grade < GradeCount && level >= MaxLevelForGrade(grade);
        }

        // ---------------------------------------------------------------- 비용

        /**
         * @brief 단련 비용의 레벨당 배수. **여섯 축(1.15)보다 가파르다.**
         *
         * 상한이 있는 축은 반드시 빨리 팔린다. 26단계에 스킬이, 21단계에 골드
         * 획득 축이 같은 자리에서 물렸다 - 수입이 스테이지마다 x1.72로 자라고
         * 경쟁 축의 비용이 x1.75로 자라므로, 두 힘의 합 x3.0을 비용 증가율이
         * 흡수하지 못하면 한 스테이지에 남은 칸을 통째로 사버린다.
         *
         *     레벨/스테이지 = ln(3.0) / ln(CostGrowth)
         *
         * 1.55면 2.3레벨/스테이지다. 등급 하나가 네 칸이므로 골드만으로는 한
         * 등급이 약 두 스테이지에 소진되고, 그 뒤로는 보석을 기다린다 - 그
         * 기다림이 이 축의 리듬이고 퀘스트를 열어볼 이유다.
         */
        public const double TemperCostGrowth = 1.55d;

        /**
         * @brief 단련 첫 칸의 비용 = **한 칸의 % x 그 %당 골드**.
         *
         * ## 스테이지 배수를 곱하지 않는다 - 한 번 물렸다
         *
         * 처음에 SkillSpec.BaseCost를 그대로 흉내 내 해금 스테이지의 골드
         * 배수(x226)를 곱했다. 첫 칸이 137,225골드가 됐고, 시뮬레이션에서
         * **st11~st24 내내 한 칸도 안 팔리다가 후반에 몰아서 팔렸다.**
         *
         * 두 곡선의 성질이 다르다. 오의 비용은 `배율/쿨다운`이라는 **무차원
         * 비**에서 나오므로 골드 단위로 옮기려면 스테이지 배수가 필요하다.
         * 여기 GoldPerPercent는 이미 **해금 스테이지의 절대 골드**로 잰 값이라
         * (경쟁 축의 그 시점 비용에서 유도했다) 배수가 이미 안에 들어 있다.
         * 한 번 더 곱하면 두 번 스케일된다.
         *
         * 상수가 어느 단위인지가 곧 이 함수의 전부이고, 그것이 어긋나면 축은
         * 죽지 않고 **시점만 어긋난 채로 살아 있어서** 밴드만 이상해진다 -
         * 실제로 그렇게 나타났다. 그래서 값을 선언으로 두지 않고
         * `EquipmentTests.TemperCost_StartsAtParityWithTheCompetingAxis` 가
         * 시뮬레이션 실측 레벨과 대조한다.
         */
        public static double TemperBaseCost(double goldPerPercent, double temperStep)
        {
            double percentPerStep = (temperStep - 1d) * 100d;
            return goldPerPercent * percentPerStep;
        }

        public static double TemperCost(double baseCost, int level)
        {
            return baseCost * Math.Pow(TemperCostGrowth, Math.Max(0, level - 1));
        }

        /**
         * @brief 등급업의 골드 몫. 그 등급 마지막 단련 칸의 몇 배인가.
         *
         * 골드도 함께 받는 이유는 등급업이 **보석만의 일이 되면 안 되기**
         * 때문이다. 보석만 받으면 이 버튼은 골드 경제 밖에 서고, 그러면
         * "장비에 골드를 쓰는 것이 이득인가"라는 질문에서 등급이 통째로 빠진다.
         *
         * 크기는 마지막 단련 칸의 세 배다. 그 칸이 그 시점에 살 만한 크기이므로
         * 세 배도 살 만하고, 동시에 "이번 등급의 마지막 지출"로 읽힌다.
         */
        public const double GradeGoldMultiplier = 3d;

        public static double GradeGoldCost(double baseCost, int grade)
        {
            int lastLevel = MaxLevelForGrade(grade);
            return TemperCost(baseCost, lastLevel) * GradeGoldMultiplier;
        }

        /**
         * @brief 등급업의 보석 값. **grade -> grade+1 의 값이다.**
         *
         * 배열이 아니라 표로 두는 이유는 이 값들이 곡선이 아니기 때문이다.
         * 보석 수입은 퀘스트에서 오고(하루 55개 + 업적 445개) 그 수입은 지수로
         * 자라지 않는다 - 골드와 달리 스테이지에 묶여 있지 않다. 그래서 등급
         * 가격도 지수가 아니라 손으로 적은 네 숫자다.
         *
         * 크기의 근거:
         *
         *   업적 전부(445) + 반복 티어(st30까지 약 90) = **535개**  <- 하한
         *   일일 다섯을 매일 받으면 하루 55개                        <- 그 위
         *
         * 하한만으로는 두 슬롯을 3등급까지 올린다(40+80 x 2 = 240, 4등급까지
         * 가려면 +300). 매일 접속하면 열흘 남짓에 5등급이 열린다. **그 격차가
         * 이 재화의 존재 이유이고, 밴드는 양쪽 끝을 모두 검사한다**
         * (StageSimulationTests의 GemFloor / 기본 정책 두 벌).
         */
        private static readonly int[] GradeGems = { 40, 80, 150, 260 };

        /** grade에서 grade+1로 올리는 보석 값. 마지막 등급이면 0 */
        public static int GradeGemCost(int grade)
        {
            int index = grade - 1;
            if (index < 0 || index >= GradeGems.Length) return 0;
            return GradeGems[index];
        }

        /** 1등급에서 이 등급까지 드는 보석 총합. 보고와 테스트가 쓴다 */
        public static int TotalGemsThroughGrade(int grade)
        {
            int total = 0;
            for (int g = 1; g < Clamp(grade, 1, GradeCount); g++) total += GradeGemCost(g);
            return total;
        }

        // ---------------------------------------------------------------- 해금

        /**
         * @brief 장비가 열리는 스테이지. **지역 1을 돌파한 다음이다.**
         *
         * ## 왜 스테이지이고 레벨이 아닌가
         *
         * 다른 탭은 전부 캐릭터 레벨로 잠근다(스킬 Lv.10, 전직 Lv.30). 여기만
         * 다른 이유는 대장간이 **지역 1의 랜드마크**이기 때문이다 - 화면에 서
         * 있는 그 건물이 열리는 조건은 "그 지역을 지나왔는가"여야 말이 된다.
         * 레벨로 잠그면 조건과 화면이 서로 다른 것을 가리킨다.
         *
         * ## 왜 11인가
         *
         * 10스테이지가 지역 1의 피날레이고, 그것을 넘긴 다음 칸이다. 온보딩
         * (1~5, 173초)에서 두 배 이상 떨어져 있으므로 이 축은 그 구간에
         * 존재하지 않는다 - E-1이 요구한 "온보딩 침범 금지"가 계수가 아니라
         * 구조로 지켜진다(EquipmentTests.Equipment_IsAbsentFromOnboarding).
         */
        public const int UnlockStage = 11;

        public static bool IsUnlockedAt(int stage)
        {
            return stage >= UnlockStage;
        }

        // ---------------------------------------------------------------- 보스 보정

        /**
         * @brief 곡선 추종 플레이어가 이 스테이지에서 갖고 있을 단련 레벨.
         *
         * 보스 체력 보정(StageCurve.EquipmentCompensation)이 이것을 따라간다.
         * 골드 획득 축의 ExpectedAtStage와 같은 자리이고 같은 제약을 받는다.
         *
         * **닫힌 식이어야 한다.** 보스 체력이 이 값을 쓰는데 시뮬레이션 결과를
         * 참조하면, 시뮬레이션이 보스 체력을 계산하려고 자기 결과를 필요로 하는
         * 순환이 된다.
         *
         * **해금 전에는 1레벨(배수 1배)이다.** 축이 없는데 보스만 무거워지면
         * 있지도 않은 이득을 상쇄하는 셈이고, 21단계에 골드 축에서 정확히 그
         * 사고가 있었다.
         *
         * 두 계수 모두 상수가 아니라 실측과 대조한다
         * (EquipmentTests.ExpectedCurve_TracksTheSimulation) - 상수의 존재는
         * 연결의 증거가 아니다.
         *
         * ## 절편이 1이 아니라 4다 - 여기서 한 번 물렸다
         *
         * 처음에 `1 + (stage - 11) x 기울기`로 두었다. 해금 스테이지에 레벨 1을
         * 가정한 셈인데, 실측은 **st11에 이미 Lv.4**였다 - 단련 첫 칸이 그 시점
         * 수입의 몇 초치라 해금된 스테이지 안에서 한 등급을 다 채운다.
         *
         * 그 어긋남이 밴드에 그대로 나타났다. 보정이 실제보다 가볍게 걸려 st21~26의
         * 여유가 천장(일반 3.00 / 챕터 2.01)을 넘었다. 21단계에 골드 축의
         * StagesToCeiling을 실측과 맞추지 않아 겪은 것과 **같은 종류이고 방향만
         * 반대**다 - 그때는 보정이 너무 무거워 보스를 못 잡았다.
         */
        public const int LevelAtUnlock = 4;
        public const double LevelsPerStage = 1.25d;

        public static int ExpectedLevelAtStage(int stage)
        {
            if (!IsUnlockedAt(stage)) return 1;

            int level = LevelAtUnlock + (int)Math.Floor((stage - UnlockStage) * LevelsPerStage);
            return Clamp(level, 1, MaxLevel);
        }

        /**
         * @brief 그 스테이지에서 곡선 추종 플레이어가 갖고 있을 **무기** 배수.
         *
         * 방어구가 아니라 무기다. 보정이 상쇄해야 하는 것은 보스 여유이고,
         * 여유를 직접 미는 것은 %DPS뿐이기 때문이다. 방어구가 골드를 아껴
         * 화력으로 돌리는 간접 효과는 아래 지수(StageCurve.EquipmentMarginExponent)
         * 를 실측할 때 함께 잡힌다 - 나눠 재려면 방어구 없는 세계를 또 하나
         * 만들어야 하는데, 그 비교군이 답하는 질문이 없다.
         */
        public static double ExpectedPowerAtStage(int stage)
        {
            var weapon = EquipmentCatalog.Slots[EquipmentCatalog.IndexOf(EquipmentCatalog.WeaponId)];
            int level = ExpectedLevelAtStage(stage);
            return ValueAt(weapon.GradeStep, weapon.TemperStep, GradeOfLevel(level), level);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
