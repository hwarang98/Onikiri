using System;

namespace Onikiri.Progression
{
    /**
     * @brief 스탯 포인트가 곱하는 증폭률.
     *
     * 골드 강화와 **곱해진다.** 더하지 않는 이유는 두 축의 단위가 다르기 때문이다.
     * 골드 강화는 지수로 자라는데 거기에 덧셈으로 얹으면, 초반에는 스탯 포인트가
     * 전부이고 후반에는 있으나 마나 한 값이 된다. 곱하면 어느 구간에서든 같은
     * 비율만큼 기여한다.
     *
     * 포인트당 0.5%는 한 번 찍어서는 체감되지 않는 크기다. 그것이 의도다 - 레벨은
     * 골드처럼 "지금 하나 사면 지금 세진다"가 아니라 쌓여서 뒤에서 밀어주는 축이다.
     *
     * **다만 실측하면 밀어주는 힘이 거의 없다.** 상한(500)까지 다 찍어도 축당
     * x12.11이고 그것은 공격력 강화 22레벨에 해당하는데, 40스테이지 시점의
     * 공격력은 이미 Lv.162다. 곱셈으로 얹는 한, 골드 축이 지수로 달아나는
     * 속도를 따라갈 수 없다.
     *
     * 12단계 지시가 정한 계수라 그대로 두고 보고서에 수치를 적었다. 구조적으로
     * 맞는 답은 배수가 아니라 **골드 강화 레벨 보너스**일 가능성이 크다 -
     * 포인트 하나가 공격력 강화 +1레벨과 같은 효과라면 골드 축과 같은 지수를
     * 타므로 후반에도 비중이 유지된다.
     */
    public static class StatPointCurve
    {
        /**
         * @brief 포인트 하나가 곱하는 값.
         *
         * **0.5%에서 2.5%로 올렸다 (16단계).**
         *
         * 0.5%는 12단계에서 "한 번 찍어서는 체감되지 않는 크기가 의도"라고 적고
         * 고른 값이었다. 실제로 돌려보니 그 의도가 틀렸다 - 15단계 화면에서
         * 남은 포인트를 쥐고도 증폭 두 축이 **Lv.0으로 방치**돼 있었다.
         * 공격력 강화 Lv.116 옆에서 x1.000 -> x1.005는 눌러야 할 이유가 없다.
         *
         * 회복 축(11단계)이 죽었던 것과 같은 결말이고 원인만 다르다. 그때는
         * 곡선이 다른 축에 밀렸고, 이번에는 계수가 너무 작았다.
         *
         * 2.5%는 "찍는 순간 숫자가 움직인다"의 최소선(1%)을 넉넉히 넘는 값이다.
         * StageSimulationTests.StatPoint_IsFeltTheMomentItIsSpent 가 못 박는다.
         */
        public const double PerPoint = 1.025d;

        /**
         * @brief 한 축에 찍을 수 있는 포인트 수의 상한.
         *
         * 상한이 있어야 하는 이유는 곱연산이기 때문이다. 무제한이면 충분히 오래
         * 놔둔 계정에서 스탯 포인트가 다른 모든 축을 압도하고, 그때는 게임이
         * "레벨을 올리는 것"만 남는다.
         */
        /**
         * 500에서 200으로 내렸다. 포인트당 배수를 5배로 올렸으므로 상한을 그대로
         * 두면 축당 x230,000이 된다 - 그 지점에서 골드 축 전체가 장식이 된다.
         *
         * 200에서 축당 x139이고, 공격력 강화 약 44레벨에 해당한다. 실제로 도달할
         * 수 있는 값도 아니다(40스테이지에서 총 포인트가 30개 남짓) - 상한의
         * 역할은 도달점이 아니라 **폭주 방지**다.
         */
        public const int MaxPoints = 200;

        /**
         * @brief 레벨업당 받는 스탯 포인트.
         *
         * **1에서 2로 올렸다 (66단계).** 축이 둘에서 다섯이 됐다. 1로 두면
         * 새 세 축에 찍는 만큼 공격력·체력 증폭이 그대로 굶는다 - 축을 늘린
         * 것이 아니라 같은 포인트를 다섯으로 쪼갠 것이 된다.
         *
         * 2는 시뮬레이션이 고른 값이다(보고서 66 §2). 12단계 계약
         * (LevelGrowth_DoesNotOutpaceGoldGrowth)과 16단계 체감 검사
         * (StatPoint_IsFeltTheMomentItIsSpent)가 그대로 서는 값이다.
         */
        public const int PointsPerLevel = 2;

        // ---------------------------------------------------------------- 66단계: 골드로 못 사는 세 축

        /**
         * @brief 경험치 획득 증폭의 포인트당 배수 (66단계).
         *
         * 처치 경험치에만 곱한다(CharacterLevel.AddKillExp). 업적 경험치는
         * 되돌릴 수 없는 축이라 31단계가 faucet을 막은 자리다.
         *
         * 1.025 그대로다. 경험치 곡선이 가팔라 배수가 레벨로 번지는 몫이
         * 작다 - 66단계 실측 Lv.30 도달이 st37 -> st35, Lv.100이 st146 ->
         * st138이고, 심층 수렴·폭주 검사는 이 축 단독으로 0.14 / 32다.
         */
        public const double ExpPerPoint = 1.025d;

        /**
         * @brief 경험치 획득 증폭이 **열리는 레벨** (66단계). 일섬 해금과 같다.
         *
         * 상한이 아니라 등장 시점 게이트다 - 골드 획득 축의 UnlockStage(21단계)와
         * 같은 종류. 이 축이 처음부터 있으면 Lv.15가 st15에서 st14로 당겨지고,
         * 일섬의 비용(SkillCatalog UnlockStage 15)이 가정한 골드 규모보다 한
         * 스테이지(x1.72) 일찍 열린다. 계수를 1.015로 내려도 그대로였다 - 크기가
         * 아니라 **있느냐**의 문제라 게이트로 푼다.
         *
         * Lv.15 전에는 레벨이 66단계 전과 똑같이 오른다(경험치 축이 없으므로).
         */
        public const int ExpUnlockLevel = 15;

        /** 축이 열리는 레벨. 게이트가 없으면 0 */
        public static int UnlockLevelFor(string axisId)
        {
            return axisId == CharacterLevel.ExpAmpId ? ExpUnlockLevel : 0;
        }

        /** 이 레벨의 캐릭터에게 축이 열려 있는가. 경험치 축만 게이트가 있다 */
        public static bool IsUnlocked(string axisId, int level)
        {
            if (axisId == CharacterLevel.ExpAmpId) return level >= ExpUnlockLevel;
            return true;
        }

        /**
         * @brief 골드 획득 증폭의 포인트당 배수 (66단계).
         *
         * 골드 강화의 획득 축(GoldGainCurve) 위에 **곱해진다** -
         * UpgradeSystem.CurrentGoldGain 한 자리에서. 골드 축은 64단계부터
         * 상한이 없지만 이 축은 다른 스탯 포인트 축과 같은 200에서 멈춘다.
         *
         * **1.025에서 출발해 1.0075로 내렸다 (66단계 시뮬레이션).** 골드는
         * 그 자체가 다른 모든 축의 재화라 이 배수는 강화 전부에 복리로
         * 번진다. 1.025에서 200점이면 x139이고, st100 보스 여유가 7.7에서
         * 27.8로, 심층 최대 여유가 125에서 3365로 튄다 - 심층 수렴
         * (|m200/m100-1| < 0.35)과 폭주 검사(< 150)가 먼저 깨진다.
         *
         *   1.005   수렴 0.02  최대  78  안 산 플레이어 최저 1.001 (문턱)
         *   1.0075  수렴 0.09  최대 124  최저 1.024   <- 채택
         *   1.01    수렴 0.32  최대 191  (폭주 검사 실패)
         *
         * 1.0075는 66단계 전 세계의 최대 여유(125)와 같은 자리다. 200점이면
         * x4.46이다.
         *
         * ## Step16 체감 기준(1%/pt)의 명시적 예외
         *
         * 표시는 한 점에 +0.75%라 16단계의 "찍는 순간 1% 이상 움직인다"에 못
         * 미친다. 예외로 두는 이유는 이것이 **복리 축**이기 때문이다 - 골드는
         * 강화 전부의 재화라, 표시 0.75%의 실효 체감은 더 크다. 공짜 골드 배수를
         * 얹어 잰 st50의 DPS 탄성은 약 1.15이고, 한 점의 실효는 약 0.86%다
         * (StageSimulationTests.StatPoint_IsFeltTheMomentItIsSpent의 골드 축 행).
         * 그래도 1%는 넘지 않는다 - 넘기려면 계수가 1.01 위여야 하는데, 1.01은
         * 폭주 검사 191(>150)로 탈락했다. 이 축의 체감은 숫자가 아니라 강화
         * 목록의 다음 칸이 빨리 열리는 것으로 온다.
         */
        public const double GoldPerPoint = 1.0075d;

        /**
         * @brief 방치 보상 증폭 - 포인트당 **최대 누적 시간** +5분 (66단계).
         *
         * 배수가 아니라 시간이다. 방치 효율(IdleIncome.Efficiency 0.5)에
         * 곱하면 28포인트(x2)에서 방치가 실제 플레이를 따라잡는다 - "켜두는
         * 것이 이득"이라는 계약이 깨진다. 그것을 지키면서 16단계 체감
         * 기준까지 맞추려면 계수가 포인트당 0.34% 아래여야 하고 그러면
         * 눌러도 아무것도 안 움직인다.
         *
         * 시간은 둘 다 지킨다. 초당 수입은 그대로라 0.5 계약이 무수정이고,
         * 한 점이 8시간 상한의 1%를 넘는다(5분 / 480분 = 1.04%).
         *
         * 가산이다. 200포인트 = +16시간 40분, 상한은 8시간에서 24시간
         * 40분까지 간다. 새 상한이 아니라 기존 상한의 값이 커지는 것이다.
         */
        public const int IdleMinutesPerPoint = 5;

        /**
         * @brief 보석 초기화 비용 (66단계). 첫 1회는 무료다.
         *
         * 일일 퀘스트 다섯 개의 보석이 하루 55다(QuestCatalog.Daily:
         * 10+15+10+10+10). 사흘치 165를 뽑기 단가(25)의 격자로 내린 값이
         * 150 - 뽑기 6회, 장비 3등급 승급과 같은 값이다. 반복 퀘스트 보석은
         * 진행도에 따라 갈려 기준에서 뺐다.
         *
         * "사흘"은 마음대로 다시 찍는 것이 아니라 **방향을 바꾸는** 값이라는
         * 뜻이다. 하루치면 매일 다시 찍는 것이 최적이 되고 그때 이 축들은
         * 배분이 아니라 상황별 스위치가 된다.
         */
        public const int ResetGemCost = 150;

        /** points 만큼 찍었을 때의 증폭 배수. 상한을 넘겨 넣어도 상한에서 멈춘다 */
        public static double Multiplier(int points)
        {
            return Math.Pow(PerPoint, Clamp(points));
        }

        /**
         * @brief 축별 포인트당 배수. 공격력·체력은 PerPoint 그대로다.
         *
         * 방치 축은 배수가 아니라 1을 돌려준다 - 그 축은 시간을 늘리므로
         * (IdleExtraAccrual) 배수 자리로 새어 들어가면 안 된다.
         */
        public static double PerPointFor(string axisId)
        {
            if (axisId == CharacterLevel.ExpAmpId) return ExpPerPoint;
            if (axisId == CharacterLevel.GoldAmpId) return GoldPerPoint;
            if (axisId == CharacterLevel.IdleAmpId) return 1d;
            return PerPoint;
        }

        /** 축별 배수. 축을 모르는 옛 호출부는 Multiplier(points)를 그대로 쓴다 */
        public static double Multiplier(string axisId, int points)
        {
            return Math.Pow(PerPointFor(axisId), Clamp(points));
        }

        /** 방치 보상 축이 늘려 주는 최대 누적 시간. 포인트에 선형이다 */
        public static TimeSpan IdleExtraAccrual(int points)
        {
            return TimeSpan.FromMinutes((double)Clamp(points) * IdleMinutesPerPoint);
        }

        public static int Clamp(int points)
        {
            if (points < 0) return 0;
            return points > MaxPoints ? MaxPoints : points;
        }

        /** 레벨 level 까지 올린 캐릭터가 지금까지 받은 총 포인트 */
        public static int TotalPointsAtLevel(int level)
        {
            // Lv.1은 시작 레벨이라 포인트를 주지 않는다. 첫 레벨업(1->2)이 첫 포인트다
            int levelUps = level < 1 ? 0 : level - 1;
            return levelUps * PointsPerLevel;
        }
    }
}
