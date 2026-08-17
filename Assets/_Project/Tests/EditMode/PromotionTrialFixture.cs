using System.Collections.Generic;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 귀문 시뮬레이션의 입력을 **살아 있는 곡선에서** 만든다. 테스트 전용.
     *
     * 값을 굽지 않고 매번 `StageSimulation`·`StageCurve`·`BossCurve`를 부르는
     * 이유는 이 입력들이 곡선의 함수이기 때문이다. 구워 두면 곡선이 움직인
     * 날 귀문만 옛 세계를 재게 되고, 그것이 9단계가 물린 "계산과 게임 중
     * 어느 쪽이 맞는지 모르는" 상태다.
     *
     * 굳혀 둔 것은 §1의 특성화 표뿐이다 - 그쪽은 **움직이면 안 되는 것**을
     * 재고, 여기는 **움직이면 함께 움직여야 하는 것**을 잰다.
     */
    public static class PromotionTrialFixture
    {
        /** 귀문 여섯의 게이트 스테이지. **표의 출처는 프로덕션이다** */
        public static int[] GateStages { get { return PromotionTrialCatalog.GateStages; } }

        // ---------------------------------------------------------------- 필드

        /**
         * @brief 에셋에서 만든 시뮬레이션 입구. **본체는 `DevSimField`로 올라갔다.**
         *
         * 5.0단계에 옮겼다. 프리셋 세이브를 찍는 Editor 메뉴가 같은 값을 봐야
         * 하는데 그쪽은 테스트 어셈블리를 참조할 수 없어서, 함수를 양쪽이 다
         * 보이는 에디터 전용 어셈블리로 올리고 여기는 부르기만 한다 - 두 벌이
         * 되면 프리셋이 밴드와 다른 잡몹 평균에서 유도되고 그 어긋남은 어디에도
         * 안 적힌다(그 함수 머리 주석).
         */
        public static StageSimulation.Field FieldFromAssets()
        {
            return Onikiri.DevTools.DevSimField.FieldFromAssets();
        }

        static readonly Dictionary<string, List<StageSimulation.StageResult>> cache =
            new Dictionary<string, List<StageSimulation.StageResult>>();

        /** 정책별 200스테이지 런. 한 번만 돌리고 재사용한다 */
        public static List<StageSimulation.StageResult> Run(string key, StageSimulation.Policy policy)
        {
            List<StageSimulation.StageResult> rows;
            if (cache.TryGetValue(key, out rows)) return rows;

            rows = StageSimulation.Run(PromotionEconomyFixture.Stages, FieldFromAssets(), policy);
            cache[key] = rows;
            return rows;
        }

        public static List<StageSimulation.StageResult> Lead()
        {
            return Run("lead", StageSimulation.Policy.Default);
        }

        public static List<StageSimulation.StageResult> GemFloor()
        {
            return Run("floor", new StageSimulation.Policy { GemsFromQuestsOnly = true });
        }

        public static List<StageSimulation.StageResult> NoEvolution()
        {
            return Run("noevo", new StageSimulation.Policy { SkipEvolution = true });
        }

        // ---------------------------------------------------------------- 플레이어

        public static PromotionTrialSimulation.Player PlayerAt(
            List<StageSimulation.StageResult> rows, int stage)
        {
            var row = rows[stage - 1];
            return new PromotionTrialSimulation.Player
            {
                Dps = row.ExpectedDps,
                MaxHealth = row.MaxHealth,
                RegenPerSecond = row.RegenPerSecond
            };
        }

        /**
         * @brief 저화력·최대 재생 합성 플레이어. 종료 보장의 반례다.
         *
         * 실제 플레이어가 아니라 **정지 시도**를 만들려고 지어낸 조합이다 -
         * DPS를 100분의 1로 낮추고 재생을 최대 체력의 상한 비율로 올린다.
         * 이 조합이 끝나지 않으면 귀문은 무한 전투를 여는 문이 된다.
         */
        public static PromotionTrialSimulation.Player StallerAt(
            List<StageSimulation.StageResult> rows, int stage)
        {
            var row = rows[stage - 1];
            return new PromotionTrialSimulation.Player
            {
                Dps = row.ExpectedDps / 100d,
                MaxHealth = row.MaxHealth,
                RegenPerSecond = row.MaxHealth * HealthRegenCurve.Ceiling
            };
        }

        // ---------------------------------------------------------------- 적

        /**
         * @brief 게이트별 **총 체력 배수** M. **표의 출처는 프로덕션이다.**
         *
         * 1.6단계에는 이 표가 여기 있었다. 2단계에 `PromotionTrialCatalog`으로
         * 옮긴 이유는 3단계의 전투가 그 표를 읽어야 하기 때문이다 - 테스트에만
         * 있는 표는 게임이 같은 값을 스폰한다는 보장이 없다.
         *
         * 유도 규칙(45초 하한 앵커)은 그 파일 주석에 있고, 규칙과 값이 어긋나면
         * `PromotionTrialTests.HealthMultiple_TracksTheFortyFiveSecondAnchor`가
         * 잡는다.
         */
        public static double[] TotalHealthMultiple
        {
            get { return PromotionTrialCatalog.TotalHealthMultiple; }
        }

        // ---------------------------------------------------------------- 소프트캡

        /** 소프트캡 지수. **후보 확정값**이고 출처는 프로덕션이다 */
        public const double SoftCapExponent = PromotionTrialCatalog.SoftCapExponent;

        /** 이 게이트의 기준 화력 = 귀문 총 체력 / 기준 시간 */
        public static double ReferencePowerForGate(int gateNumber)
        {
            var field = FieldFromAssets();
            return PromotionTrialCatalog.ReferencePowerForGate(
                BigDouble.FromDouble(field.AverageMobHealth), gateNumber);
        }

        /**
         * @brief 45초 앵커가 요구하는 M을 **지금 곡선에서 다시 유도한다.**
         *
         * 표가 아니라 규칙이 설계라는 말을 코드로 옮긴 것이다. 곡선이 움직이면
         * 이 값이 움직이고, 카탈로그의 표와 벌어지면 검사가 그 벌어짐을 잰다.
         *
         *     M(gate) = (45초 - 전환 2회) / 하한 플레이어의 그 게이트 보스 처치 시간
         */
        public static double DerivedHealthMultiple(int gateNumber)
        {
            var field = FieldFromAssets();
            int stage = GateStages[gateNumber - 1];

            double bossHealth = StageCurve.BossHealthForStage(
                BigDouble.FromDouble(field.AverageMobHealth), stage).ToDouble();
            double dps = PlayerAt(GemFloor(), stage).Dps;

            // 전환 두 번이 시계 안에서 흐르므로 전투에 쓸 수 있는 시간은
            // 45초가 아니라 41초다. TrialPowerScore.ReferenceSeconds와 같은 값
            return TrialPowerScore.ReferenceSeconds / (bossHealth / dps);
        }

        /** 소프트캡을 지난 플레이어. 입장 시 한 번 계산한 공통 배율을 곱한다 */
        public static PromotionTrialSimulation.Player Capped(
            PromotionTrialSimulation.Player player, int gateNumber, double k)
        {
            double reference = ReferencePowerForGate(gateNumber);
            player.Dps = TrialPowerScore.EffectivePower(player.Dps, reference, k);
            return player;
        }

        /**
         * @brief 3체의 공격력·간격 계수. **둘 다 지역 보스와 같다(1.0 / 2.0초).**
         *
         * ## 왜 3체를 더 세게 때리게 만들지 않았는가 - 재생 임계가 칼날이다
         *
         * 실측: 게이트 여섯에서 **보스 DPS / 초당 재생 = 0.88 ~ 1.22**다. 생존 축이
         * `EffectiveHealth >= 보스 피해 x 1.15`까지만 사고(`StageSimulation`의
         * 생존 루프) 재생 상한이 0.30이라, 플레이어는 정확히 그 임계 위에 앉는다.
         *
         * 그래서 적 공격을 조금만 올리면 세계가 뒤집힌다. 3체 간격을 1.8초로
         * 줄이면 곡선 추종이 전 문을 통과하지만, **1.7초에서는 여섯 문 중 넷에서
         * 죽는다**(실측). 6% 옆이 의도한 플레이어를 죽이는 값은 조율된 값이 아니라
         * 운이다.
         *
         * 그러므로 차별화는 **공격이 아니라 체력과 시간**에 둔다. 3체의 정체성은
         * 총 체력의 절반과 외형과 서사이지 다른 공격 리듬이 아니다 - 그 사실을
         * 문서에도 솔직히 적는다.
         */
        public const double ThirdFoeAttackFactor = 1d;

        /**
         * @brief 문 번호(1부터)의 적 셋. **체력은 프로덕션 카탈로그가 낸다.**
         *
         * `FoesAt`이 아니라 `PromotionTrialCatalog.FoeHealth`를 지나는 것이
         * 요점이다 - 3단계의 스포너가 부를 함수와 시뮬레이션이 재는 함수가
         * 같아야, 계산이 통과하는데 화면에서 실패하는 상태가 안 생긴다.
         */
        public static PromotionTrialSimulation.Foe[] FoesForGate(int gateNumber)
        {
            var mobHealth = BigDouble.FromDouble(FieldFromAssets().AverageMobHealth);
            int stage = GateStages[gateNumber - 1];
            double bossAttack = BossCurve.AttackDamageForStage(stage);

            var foes = new PromotionTrialSimulation.Foe[PromotionTrialCatalog.FoeCount];
            for (int i = 0; i < foes.Length; i++)
            {
                bool third = i == PromotionTrialCatalog.FoeCount - 1;

                foes[i] = new PromotionTrialSimulation.Foe
                {
                    Health = PromotionTrialCatalog.FoeHealth(mobHealth, gateNumber, i).ToDouble(),
                    AttackDamage = third ? bossAttack * ThirdFoeAttackFactor : bossAttack,
                    AttackInterval = PromotionTrialCatalog.FoeAttackIntervalSeconds,
                    FirstAttackDelay = PromotionTrialCatalog.FirstAttackDelaySeconds
                };
            }
            return foes;
        }

        // ---------------------------------------------------------------- 규칙

        /**
         * @brief 시간 규칙 넷의 **출처는 프로덕션 카탈로그다.**
         *
         * 1.6단계에는 여기 상수 넷이 서 있었고(격노 90 / 15초마다 x2 / 폐쇄
         * 180), 승인 과정에서 간격과 배수가 10초 / x1.3으로 바뀌었다. 그때
         * 시뮬레이션만 고치고 카탈로그를 안 고치면 3단계의 전투가 다른 리듬을
         * 돌린다 - 값을 두 벌 두지 않는 것이 그것을 구조적으로 막는다.
         */
        public static PromotionTrialSimulation.Rules DefaultRules()
        {
            return new PromotionTrialSimulation.Rules
            {
                SwapSeconds = PromotionTrialCatalog.SwapSeconds,
                EnrageStartSeconds = PromotionTrialCatalog.EnrageSeconds,
                EnrageStepSeconds = PromotionTrialCatalog.EnrageIntervalSeconds,
                EnrageAttackStep = PromotionTrialCatalog.EnrageMultiplierPerStep,
                CloseSeconds = PromotionTrialCatalog.CloseSeconds,
                TimeStep = 0.02d
            };
        }
    }
}
