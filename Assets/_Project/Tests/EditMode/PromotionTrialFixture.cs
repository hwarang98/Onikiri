using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

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
        private const string DataFolder = "Assets/_Project/Data";

        /** 귀문 여섯의 게이트 스테이지. `PromotionEconomyFixture`와 같은 표다 */
        public static int[] GateStages { get { return PromotionEconomyFixture.GateStages; } }

        // ---------------------------------------------------------------- 필드

        public static StageSimulation.Field FieldFromAssets()
        {
            BigDouble healthSum = BigDouble.Zero;
            BigDouble goldSum = BigDouble.Zero;
            double totalWeight = 0d;

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { DataFolder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.spawnWeight <= 0f) continue;

                healthSum += definition.maxHealth * BigDouble.FromDouble(definition.spawnWeight);
                goldSum += definition.goldReward * BigDouble.FromDouble(definition.spawnWeight);
                totalWeight += definition.spawnWeight;
            }

            return new StageSimulation.Field
            {
                AverageMobHealth = totalWeight > 0d
                    ? (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d,
                AverageMobGold = totalWeight > 0d
                    ? (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d,
                SpawnInterval = 1.1d
            };
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
         * @brief 게이트별 **총 체력 배수** M. 지역 보스 체력에 곱한 값이 셋의 합이다.
         *
         * ## 왜 게이트마다 다른가 - 고정 배수로는 시험이 안 된다
         *
         * 보스 여유가 심층에서 발산한다(곡선 추종 실측: st31 2.72 -> st151 24.2).
         * 고정 배수를 쓰면 뒷문이 앞문보다 압도적으로 쉬워져, 사다리의 마지막이
         * 가장 쉬운 시험이 된다. 그래서 M을 **규칙에서 유도한다.**
         *
         * ## 앵커는 하한 플레이어다 (v1.4에 뒤집었다)
         *
         *   M(gate) = (45초 - 전환 4초) / **보석 하한 플레이어**의 그 게이트 보스 처치 시간
         *
         * v1.3까지는 곡선 추종을 42초에 맞췄는데, 귀문이 진행을 막는 구조에서
         * 그 앵커는 하한 플레이어에게 **벽**이 된다(사문~육문에서 사망). 진행이
         * 막히지 않는다는 보장이 곧 하한 앵커다.
         *
         * **곡선이 움직이면 이 표도 같은 규칙으로 다시 굽는다** - 값이 아니라
         * 규칙이 설계다.
         */
        public static readonly double[] TotalHealthMultiple = { 1.74d, 1.68d, 1.49d, 1.91d, 4.75d, 5.36d };

        // ---------------------------------------------------------------- 소프트캡

        /**
         * @brief 소프트캡 지수. **과잉 화력만 완만하게 줄인다.**
         *
         * 하한 앵커만으로는 곡선 추종이 15초에 끝나 시험이 형식이 된다. 캡을
         * 얹으면 두 플레이어를 동시에 밴드 안에 넣을 수 있다 - 캡 없이는
         * 불가능하다는 것이 산수로 증명된다(격차 x3.61 > 밴드 비 1.571).
         *
         * k=0.45의 실측: 곡선 추종 27.0~43.0초 / 중간 34.8~44.0초 / 하한 45.0초.
         */
        public const double SoftCapExponent = 0.45d;

        /** 이 게이트의 기준 화력 = 귀문 총 체력 / 기준 시간 */
        public static double ReferencePowerForGate(int gateNumber)
        {
            var field = FieldFromAssets();
            int stage = GateStages[gateNumber - 1];
            double bossHealth = StageCurve.BossHealthForStage(
                BigDouble.FromDouble(field.AverageMobHealth), stage).ToDouble();

            return TrialPowerScore.ReferencePower(bossHealth * TotalHealthMultiple[gateNumber - 1]);
        }

        /** 소프트캡을 지난 플레이어. 입장 시 한 번 계산한 공통 배율을 곱한다 */
        public static PromotionTrialSimulation.Player Capped(
            PromotionTrialSimulation.Player player, int gateNumber, double k)
        {
            double reference = ReferencePowerForGate(gateNumber);
            player.Dps = TrialPowerScore.EffectivePower(player.Dps, reference, k);
            return player;
        }

        /** 셋의 체력 배분. 3체가 절반이라 "마지막이 본체"가 성립한다 */
        public const double FirstFoeShare = 0.25d;
        public const double ThirdFoeShare = 0.50d;

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
        public const double ThirdFoeAttackInterval = 2d;

        /** 등장 후 첫 타격까지의 지연. 게임의 보스 접근 시간과 같은 자리다 */
        public const double FirstAttackDelay = 2d;

        /**
         * @brief 이 게이트의 적 셋. 1·2체는 지역 보스, 3체는 **얻으려는 경지**다.
         *
         * 체력은 `StageCurve.BossHealthForStage` - 게임이 실제로 스폰할 때
         * 부르는 그 함수다. 여기서 곱셈을 따로 하면 "계산상으로는 통과하는데
         * 실제로는 실패하는" 상태가 만들어진다(그 주석의 규칙 그대로).
         */
        public static PromotionTrialSimulation.Foe[] FoesAt(int gateStage, double totalMultiple)
        {
            var field = FieldFromAssets();
            double bossHealth = StageCurve.BossHealthForStage(
                BigDouble.FromDouble(field.AverageMobHealth), gateStage).ToDouble();
            double bossAttack = BossCurve.AttackDamageForStage(gateStage);

            return new[]
            {
                new PromotionTrialSimulation.Foe {
                    Health = bossHealth * totalMultiple * FirstFoeShare,
                    AttackDamage = bossAttack,
                    AttackInterval = BossCurve.AttackIntervalSeconds,
                    FirstAttackDelay = FirstAttackDelay
                },
                new PromotionTrialSimulation.Foe {
                    Health = bossHealth * totalMultiple * FirstFoeShare,
                    AttackDamage = bossAttack,
                    AttackInterval = BossCurve.AttackIntervalSeconds,
                    FirstAttackDelay = FirstAttackDelay
                },
                new PromotionTrialSimulation.Foe {
                    Health = bossHealth * totalMultiple * ThirdFoeShare,
                    AttackDamage = bossAttack * ThirdFoeAttackFactor,
                    AttackInterval = ThirdFoeAttackInterval,
                    FirstAttackDelay = FirstAttackDelay
                }
            };
        }

        /** 문 번호(1부터)의 적 셋 */
        public static PromotionTrialSimulation.Foe[] FoesForGate(int gateNumber)
        {
            return FoesAt(GateStages[gateNumber - 1], TotalHealthMultiple[gateNumber - 1]);
        }

        // ---------------------------------------------------------------- 규칙

        /** 적 교체 시간(초). 시계는 흐르고 재생도 계속된다 */
        public const double SwapSeconds = 2d;

        /** 격노 시작(초) */
        public const double EnrageStartSeconds = 90d;

        /** 격노 단계 간격(초) */
        public const double EnrageStepSeconds = 15d;

        /** 단계마다 적 공격력에 곱하는 값 */
        public const double EnrageAttackStep = 2d;

        /** 귀문이 닫히는 시각(초) */
        public const double CloseSeconds = 180d;

        public static PromotionTrialSimulation.Rules DefaultRules()
        {
            return new PromotionTrialSimulation.Rules
            {
                SwapSeconds = SwapSeconds,
                EnrageStartSeconds = EnrageStartSeconds,
                EnrageStepSeconds = EnrageStepSeconds,
                EnrageAttackStep = EnrageAttackStep,
                CloseSeconds = CloseSeconds,
                TimeStep = 0.02d
            };
        }
    }
}
