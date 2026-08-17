using System;

namespace Onikiri.Progression
{
    /**
     * @brief 초월 치명타 곡선 (43단계 심화 축).
     *
     * 치명타 확률이 100%에 닿으면(CritRateCurve.Ceiling) 모든 타격이
     * 치명타다 - 10단계 주석의 말대로 "그것은 그냥 공격력"이고, 확률 축은
     * 거기서 일을 마친다. 이 축이 그 문 뒤를 잇는다: 완성된 발도가 한 겹
     * 더 벼려지는 것이고, 수치로는 **피해 전체에 곱해지는 순수 배수**다.
     *
     * ## 왜 복리인가
     *
     * 가산이면 8단계 공격속도의 죽음을 반복한다 - 레벨당 DPS 기여율이 0으로
     * 수렴한다. 복리(레벨당 +0.19%)는 기여율이 일정해서, 비용 지수와
     * 짝을 이뤄 무한 골드의 싱크로 영원히 동작한다. 상한이 없는 이유다 -
     * 공격력·치명타 피해와 같은 부류이고, 자기 제한은 비용이 한다.
     *
     * ## 비용의 기준점
     *
     * 해금이 st80 언저리(치명타 100% 실측)이므로 기준 비용도 그 시대의
     * 골드에 앉힌다. 코리더·가속 구간에는 이 축이 구조적으로 없으므로
     * (해금 게이트) 거기 밸런스와는 무관하고, 크기는 무한 밴드 실측으로
     * 잡았다.
     */
    public static class TranscendCurve
    {
        /**
         * 레벨당 곱해지는 복리. Lv.1은 배수 1(없는 것과 같다).
         *
         * 43단계 확정 스펙으로 여덟 배 미세화됐다(주력 축과 같은 값 등가
         * 재스케일 - step^8 = 1.015, growth^8 = 2.0). 균형 걸음이 스테이지당
         * 6.3레벨이 되어 무한 구간에서 수천 레벨까지 오르고, 드립
         * (x1.0117/스테이지)은 재스케일 전과 같다 - 연속체 등가의 요점이다.
         */
        public const double Step = 1.0018628d;

        /**
         * @brief 비용. **여덟 칸 묶음의 증가율이 2.0이다 - 전 축 공통(1.15)을 깬다.**
         *
         * 깨는 이유가 이 축의 존재 이유다. 공통 증가율로 두면 골드 예산의
         * 균형점에서 이 축의 복리가 x1.06/스테이지로 영원히 쌓여 여유를
         * 지수 발산시킨다 - 실측으로 st200 리프트 x10310. 무상한 복리 축은
         * 걸음이 느려야 무한 구간과 공존한다.
         *
         * 묶음 2.0("옛 한 칸이 앞 칸의 두 배")에서 균형 걸음은 미세화 기준
         * 6.3레벨/스테이지, 복리 기울기 x1.012/스테이지다 - 이 드립은 보정이
         * 전량 흡수한다(StageCurve.MasteryCompensation의 드립부). 효율 비율이
         * 레벨을 따라 벌어지는 문제("언젠가 한쪽이 죽는다")는 여기 없다 -
         * 이 축은 죽지 않고 **느려지는 것이 설계**이고, 죽은 버튼 검사가
         * 실제 구매를 계속 확인한다.
         */
        /** E-3 수정: 전 축 일괄 상향 + 정수화. 규칙과 배율은 UpgradeCost가 단일 출처다 */
        public const double BaseCost = 9.05077e13d * UpgradeCost.RaiseScale;
        public const double CostGrowth = 1.0905077d;

        public static double MultiplierAtLevel(int level)
        {
            return Math.Pow(Step, Math.Max(0, level - 1));
        }

        public static double CostAtLevel(int level)
        {
            return UpgradeCost.Quantize(BaseCost * Math.Pow(CostGrowth, Math.Max(0, level - 1)));
        }

        /**
         * @brief 심화 축의 해금 조건 - 치명타 확률이 상한(100%)에 서 있는가.
         *
         * 조건을 여기 한 곳에 두고 화면(UpgradeButton)·시뮬(StageSimulation)·
         * 시스템(UpgradeSystem)이 전부 이것을 읽는다. 값을 각자 적으면
         * 탭은 열렸는데 버튼이 잠긴 화면이 나온다 - 스킬 탭이 해금 레벨을
         * 코드에서 끌어오는 것과 같은 규칙이다.
         */
        public static bool IsUnlockedAt(int critRateLevel)
        {
            return critRateLevel >= CritRateCurve.MaxLevel;
        }

        // ------------------------------------------------------------ 기대 곡선

        /**
         * @brief 곡선 추종 플레이어의 **발도 개방 전체**(치명타 60->100% 재평가
         * + 초월 + 연격)가 이 스테이지에 곱하고 있을 기대 배수.
         *
         * 보정항(StageCurve.MasteryCompensation)이 읽는다. 세 축을 하나로
         * 묶는 이유는 같은 문(치명타 100%) 뒤에서 같은 시기에 함께 자라기
         * 때문이다 - 따로 재면 곡선 셋이 같은 구간을 세 번 근사한다.
         *
         * 닫힌 식이어야 한다(GoldGainCurve.ExpectedAtStage와 같은 규칙 -
         * 시뮬레이션을 돌려서 얻으면 순환이 된다). 파라미터는 하네스 실측이다:
         * 벽 통과 st59, 착지 st90 언저리, 착지 배수는 비무력화/무력화 여유
         * 비의 st120+ 평탄값.
         */
        /**
         * @brief 착지 곡선의 마디들 - 실측 형태가 두 박자다.
         *
         *   st59~90    빠른 상승 x1 -> x2.5   치명타 재평가(60->100%) + 연격 초반
         *   st90~170   느린 크리프 -> x4.65   연격 완성(Lv.251, ~st117) + 초월 축적
         *
         * 한 구간 기하 보간으로는 안 맞았다 - 실제 곡선이 앞쪽으로 쏠려 있어
         * (front-loaded) 초반을 과소 보정하고, 그 구간(st80~90 피날레)이 천장을
         * 뚫었다. 값은 전부 하네스 실측(무력화 대비 여유 비, 드립 제거 후)이다.
         */
        /**
         * E-3 수정: 비용 상향(x3.5)으로 벽 통과·착지가 세 스테이지쯤 뒤로
         * 밀렸다(하네스 실측 - 문턱 st60->61, MASTER st87->90). 마디를 실측에
         * 다시 맞춘다 - 기대 곡선이 실제보다 앞서 걸리면 벽 구간(st60~90)의
         * 보정만 먼저 무거워져 f2p 계곡이 바닥을 뚫는다(21단계 골드 축 사고의
         * 마스터리판). 착지 배수·드립은 값 등가라 그대로다.
         */
        public const int ExpectedUnlockStage = 61;
        public const int ExpectedMidStage = 93;
        public const double ExpectedMidPower = 2.5d;
        public const int ExpectedLandingStage = 173;
        public const double ExpectedLandingPower = 4.65d;

        /**
         * @brief 초월이 계속 더하는 복리 (스테이지당). 드립은 st90부터 잰다 -
         * 그 앞은 착지 곡선이 담는다.
         *
         * 균형 걸음(미세화 기준 6.3레벨/스테이지)의 복리에서 온 값이고,
         * 재스케일 전과 같다(연속체 등가). 착지분과
         * 달리 이 드립은 보정이 **전량** 흡수한다 - 무한 싱크의 템포이지
         * 이득이 아니고, 흡수하지 않으면 여유가 다시 발산한다(램프가
         * 공격력·치명타 피해의 성장을 흡수하는 것과 같은 지위다).
         */
        public const double ExpectedDripPerStage = 1.0117d;
        /** 드립의 기산점은 중간 마디와 같이 움직인다 (E-3 수정: 90 -> 93) */
        public const int ExpectedDripStartStage = 93;

        /** 착지분 (상한 = LandingPower). 보정의 지수부가 읽는다 */
        public static double ExpectedLandingAtStage(int stage)
        {
            if (stage <= ExpectedUnlockStage) return 1d;

            // 두 구간 다 지수 보간이다. 배수는 곱으로 자라므로 선형이 아니라
            // 기하가 맞다
            if (stage <= ExpectedMidStage)
            {
                double into = (stage - ExpectedUnlockStage)
                            / (double)(ExpectedMidStage - ExpectedUnlockStage);
                return Math.Pow(ExpectedMidPower, into);
            }

            if (stage >= ExpectedLandingStage) return ExpectedLandingPower;

            double later = (stage - ExpectedMidStage)
                         / (double)(ExpectedLandingStage - ExpectedMidStage);
            return ExpectedMidPower * Math.Pow(ExpectedLandingPower / ExpectedMidPower, later);
        }

        /** 드립분 (st90부터 1을 넘는다). 보정이 전량 흡수한다 */
        public static double ExpectedDripAtStage(int stage)
        {
            int past = stage - ExpectedDripStartStage;
            return past <= 0 ? 1d : Math.Pow(ExpectedDripPerStage, past);
        }

        /** 개방 전체의 기대 배수. 표·문서가 읽는 값이다 */
        public static double ExpectedPowerAtStage(int stage)
        {
            return ExpectedLandingAtStage(stage) * ExpectedDripAtStage(stage);
        }
    }
}
