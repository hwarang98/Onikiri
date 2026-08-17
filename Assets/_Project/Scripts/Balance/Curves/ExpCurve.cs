using System;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 경험치 획득량과 레벨업에 필요한 양.
     *
     * 골드와 경험치는 **같은 행동에서 나오지만 다른 속도로 자라야 한다.** 둘이 같은
     * 비율로 오르면 경험치는 골드를 다시 표시한 것에 지나지 않고, 레벨이 따로 존재할
     * 이유가 없다.
     *
     * 그래서 획득량은 골드보다 완만하게(스테이지당 {@link ExpStageGrowth}) 오르고
     * 필요량은 레벨당 {@link RequirementStep}으로 오른다. 결과적으로 레벨은 초반에
     * 빠르게, 후반으로 갈수록 느리게 오른다 - 골드 축이 지수로 벌어지는 동안 레벨은
     * 로그에 가깝게 따라간다. 스탯 포인트가 골드 강화를 대체하지 않고 **얹히는**
     * 보조 축으로 남는 것이 이 격차 덕분이다.
     *
     * 12-4의 시뮬레이션이 이 관계를 실제로 확인한다. 상수를 손대면 그 테스트가
     * 먼저 깨진다.
     */
    public static class ExpCurve
    {
        /**
         * @brief Lv.1 -> Lv.2 에 필요한 경험치.
         *
         * 1스테이지 잡몹이 4를 주고 처치가 1초 남짓이므로 첫 레벨업은 30초쯤
         * 걸린다. 첫 보스(10마리 할당량)를 만나기 전에 한 번은 오르는 값이다 -
         * 레벨업 버튼이 무엇인지 배우는 자리가 보스 게이트보다 앞에 있어야 한다.
         */
        public const double BaseRequirement = 48d;

        /**
         * @brief 레벨당 필요 경험치 증가율.
         *
         * 획득량 증가율({@link ExpStageGrowth})보다 낮다. 낮아야 하는 이유는
         * 한 스테이지에서 얻는 경험치가 고정이 아니기 때문이다 - 스테이지가 오르면
         * 잡몹당 경험치도 오르므로, 필요량이 더 가파르면 어느 지점부터 레벨이
         * 사실상 멈춘다.
         */
        public const double RequirementStep = 1.26d;

        /** 1스테이지 잡몹 한 마리가 주는 경험치 */
        public const double MobBaseExp = 4d;

        /**
         * @brief 스테이지당 경험치 증가율.
         *
         * 골드 증가율(StageCurve.GoldGrowth)보다 낮게 잡는다. 그 차이가 레벨을
         * 골드보다 느리게 만드는 유일한 장치다.
         */
        public const double ExpStageGrowth = 1.16d;

        /**
         * @brief 보스가 잡몹 몇 마리치 경험치를 주는가.
         *
         * 할당량이 10마리이므로 보스 하나가 그 스테이지 파밍 전체와 맞먹는다.
         * 보스를 잡는 것이 스테이지를 넘기는 유일한 길인데 경험치까지 잡몹과
         * 비슷하면, 막힌 스테이지에서 파밍만 하는 편이 이득이 되어버린다.
         */
        public const double BossExpMultiplier = 12d;

        /** 챕터 보스는 그 위에 한 번 더. BossCurve.ChapterGoldMultiplier와 같은 뜻 */
        public const double ChapterExpMultiplier = 2d;

        /**
         * @brief 지역 피날레.
         *
         * **골드보다 크게 잡는다.** 16단계에서 갈라놨다 - 피날레 직후 스테이지의
         * 보스 여유가 천장을 뚫는 원인이 골드 한 방이었기 때문이다. 골드는 즉시
         * 화력으로 바뀌어 다음 보스를 무의미하게 만들지만, 경험치는 레벨을 통해
         * 들어오고 증폭은 포인트당 상한이 있어 그렇게 튀지 않는다.
         *
         * 그래서 "피날레가 더 준다"는 설계는 유지하되, 주는 재화를 골드에서
         * 경험치 쪽으로 옮겼다. 챕터(x2)보다 두 배다.
         */
        public const double FinaleExpMultiplier = 4d;

        public static double MultiplierFor(int stage)
        {
            switch (BossCurve.TierOf(stage))
            {
                case BossCurve.Tier.Finale: return FinaleExpMultiplier;
                case BossCurve.Tier.Chapter: return ChapterExpMultiplier;
                default: return 1d;
            }
        }

        /**
         * @brief level 에서 level+1 로 가는 데 필요한 경험치.
         *
         * 누적이 아니라 이번 레벨분만이다. 초과분이 다음 레벨로 넘어가는 구조라
         * (CharacterLevel 참고) 누적값을 들고 있을 이유가 없다.
         */
        public static BigDouble RequiredForLevel(int level)
        {
            int steps = level < 1 ? 0 : level - 1;
            return BigDouble.FromDouble(BaseRequirement) * BigDouble.Pow(
                BigDouble.FromDouble(RequirementStep), steps);
        }

        /** 이 스테이지의 잡몹 한 마리가 주는 경험치 */
        public static BigDouble MobExp(int stage)
        {
            int steps = stage < 1 ? 0 : stage - 1;
            return BigDouble.FromDouble(MobBaseExp) * BigDouble.Pow(
                BigDouble.FromDouble(ExpStageGrowth), steps);
        }

        /**
         * @brief 재선택(37단계)을 아는 판본. **최전선 아래에서는 잡몹 경험치가 없다.**
         *
         * 획득량이 골드보다 완만하게 오르는 것({@link ExpStageGrowth} 1.16 <
         * 체력 배수 1.55)이 레벨을 골드보다 느리게 만드는 유일한 장치인데,
         * 그 완만함이 재선택과 만나면 역전이 된다 - 한 스테이지 내려가면 처치가
         * 1.55배 빨라지는데 경험치는 1.16배만 깎여서, **뒤로 갈수록 경험치/초가
         * 오른다** (StageReselectTests가 실측. 보스에 막힌 플레이어일수록 격차가
         * 커져 몇 배까지 벌어진다).
         *
         * 곡선을 세우는 것은 밸런스 변경이라 막혔고(둘의 비율이 레벨 속도의
         * 전부다), 대신 축을 닫는다: 경험치는 전진의 재화이고, 클리어한 스테이지의
         * 파밍은 골드만 준다. 방치 경험치(GameSession.EstimateExpPerSecond)도
         * 같은 판정을 쓴다.
         */
        public static BigDouble MobExp(int stage, bool atFrontier)
        {
            return atFrontier ? MobExp(stage) : BigDouble.Zero;
        }

        /** 이 스테이지의 보스가 주는 경험치. 챕터 보스면 배수가 한 번 더 붙는다 */
        public static BigDouble BossExp(int stage)
        {
            var exp = MobExp(stage) * BigDouble.FromDouble(BossExpMultiplier);
            return exp * BigDouble.FromDouble(MultiplierFor(stage));
        }

        /**
         * @brief 이 경험치로 몇 레벨을 올릴 수 있는가.
         *
         * 레벨업이 수동이라 경험치는 여러 레벨분이 쌓인 채로 기다릴 수 있다.
         * 버튼 한 번에 한 레벨씩 올리므로 UI는 "지금 몇 번 누를 수 있는가"를
         * 알아야 하고, 오프라인 보상도 같은 답이 필요하다.
         *
         * @param level 현재 레벨
         * @param exp   들고 있는 경험치
         * @return 지금 올릴 수 있는 레벨 수. 0이면 아직 부족하다
         */
        public static int LevelsAffordable(int level, BigDouble exp)
        {
            int gained = 0;
            var remaining = exp;

            // 상한을 둔다. 오프라인 보상이 비정상적으로 큰 값을 들고 오면
            // (시계가 앞으로 튄 기기 등) 여기서 프레임이 멈추는 것보다 낫다
            for (int guard = 0; guard < 100000; guard++)
            {
                var need = RequiredForLevel(level + gained);
                if (remaining < need) break;

                remaining = remaining - need;
                gained++;
            }

            return gained;
        }
    }
}
