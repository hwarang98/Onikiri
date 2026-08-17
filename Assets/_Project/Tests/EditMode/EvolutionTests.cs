using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.Tests
{
    /**
     * @brief 전직(사무라이 진화) 축의 검사. **승급 재설계 2단계에 재계약했다.**
     *
     * ## 무엇이 바뀌었는가 - 이 축의 재화가 사라졌다
     *
     * 33단계의 전직은 "보석 + 골드로 사는 사다리"였고, 이 파일의 검사 넷이
     * 그 세계를 재고 있었다. 승급이 **귀문 돌파로 무료**가 되면서 그중 셋의
     * 질문이 성립하지 않게 됐다. 지운 것이 아니라 **다른 질문으로 바꿨다** -
     * 무엇이 왜 유효하지 않은지가 각 검사 주석에 남아 있다.
     *
     *   Evolution_MovesDpsAndProgress          "사면 달라지는가"
     *                                       -> "**문을 넘으면** 달라지는가"
     *   Gems_AreTheBindingConstraint...        "보석이 병목인가"
     *                                       -> **폐기.** 재화가 이 축에 없다
     *   ExpectedCurve_TracksTheSimulation      "닫힌 식이 실측을 따라가는가"
     *                                       -> "**게이트 표와 티어가 같은가**"
     *   GemLadder_TotalIsIntentional           "사다리 총합 3,010이 의도값인가"
     *                                       -> "**승급의 값은 0인가**"
     *
     * 밴드 재유도 자체는 StageSimulationTests의 AcceleratedZone_* 두 검사가
     * 지킨다. 여기는 축 자신을 본다.
     */
    public class EvolutionTests
    {
        private const string DataFolder = "Assets/_Project/Data";

        static void LoadFieldAverages(out double health, out double gold)
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

            health = totalWeight > 0d ? (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d;
            gold = totalWeight > 0d ? (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d;
        }

        static StageSimulation.Field FieldFromAssets()
        {
            double health, gold;
            LoadFieldAverages(out health, out gold);
            return new StageSimulation.Field
            {
                AverageMobHealth = health,
                AverageMobGold = gold,
                SpawnInterval = 1.1d
            };
        }

        static double SegmentSeconds(System.Collections.Generic.List<StageSimulation.StageResult> rows,
                                     int fromStage, int toStage)
        {
            double total = 0d;
            for (int i = fromStage - 1; i < toStage && i < rows.Count; i++)
                total += rows[i].MobSeconds + StageSimulation.BossIntroSeconds
                       + StageSimulation.BossWalkInSeconds + rows[i].BossKillSeconds;
            return total;
        }

        // ------------------------------------------------------------ 구조

        /**
         * @brief 조율 코리더(st1~30)에 전직이 없다.
         *
         * ## 근거가 더 단단해졌다 - 레벨이 아니라 게이트다
         *
         * 33단계에는 "해금이 Lv.30이고 곡선 추종은 st37에야 도달한다"가
         * 근거였다. 그것은 **경험치 곡선의 함수**라, 곡선이 빨라지는 날 조용히
         * 무너지는 종류의 근거다(그래서 이 검사가 있었다).
         *
         * 이제 근거는 `GateStages[0] = 30`이고, 문은 **클리어**해야 열리므로
         * 코리더 서른 줄이 문을 한 번도 지나지 않는다. 경험치 곡선이 어떻게
         * 움직여도 이 사실은 안 바뀐다.
         *
         * 보석 정책 양쪽을 다 본다. 어느 쪽 플레이어든 코리더 안에서는 같은
         * 게임이어야 한다.
         */
        [Test]
        public void Evolution_IsAbsentFromTheTunedCorridor()
        {
            var field = FieldFromAssets();
            var policies = new[]
            {
                StageSimulation.Policy.Default,
                new StageSimulation.Policy { GemsFromQuestsOnly = true }
            };

            foreach (var policy in policies)
            {
                var results = StageSimulation.Run(30, field, policy);

                foreach (var row in results)
                {
                    Assert.AreEqual(0, row.EvolutionTier, string.Format(
                        "stage {0}: 코리더 안에서 전직 {1}티어가 나왔다. 첫 문이 "
                        + "st{2}보다 앞으로 내려왔다 - 코리더 밴드를 다시 재야 한다",
                        row.Stage, row.EvolutionTier, PromotionTrialCatalog.GateStages[0]));
                }
            }

            // 첫 문의 자리가 그 사실의 **유일한** 근거다. 여기가 움직이면
            // 위 루프가 아니라 이 줄이 먼저 실패해야 원인이 보인다
            Assert.GreaterOrEqual(PromotionTrialCatalog.GateStages[0], 30,
                "첫 귀문이 코리더 안으로 들어왔다 - st1~30의 비트 불변이 깨진다");

            // 코리더에는 보정도 없다. **항이 아예 사라져서** 그렇다 -
            // 1.0을 곱하는 코드가 남아 있지 않다는 것이 이 검사의 뜻이고,
            // 그 사실은 EvolutionCharacterizationTests가 비트로 못 박는다
            for (int stage = 1; stage <= 30; stage++)
                Assert.AreEqual(0, EvolutionCurve.ExpectedTierAtStage(stage), string.Format(
                    "stage {0}: 기대 티어가 0이 아니다", stage));
        }

        // ------------------------------------------------------------ 죽은 버튼

        /**
         * @brief 죽은 버튼 검사 - **귀문을 넘으면 실제로 세지고 빨라지는가.**
         *
         * ## 33단계의 이 검사가 왜 더 이상 유효하지 않은가
         *
         * 그때 비교한 두 세계는 "전직을 산 플레이어"와 "안 산 플레이어"였고,
         * `SkipEvolution`이 그 선택을 껐다. 승급이 무료가 되면서 **고를 수
         * 있는 것이 없어졌다** - 문을 넘으면 오고, 안 넘으면 진행이 거기서
         * 멈춘다.
         *
         * 그래서 같은 정책 플래그로 다른 질문을 잰다:
         *
         *   `SkipEvolution` = **귀문을 한 번도 안 깬 세계** (반사실 비교군)
         *
         * 자(4%)는 그대로 두고 재는 구간도 그대로다 - 축이 사는 구간이
         * st31~50이라는 사실은 문의 자리(st30)가 정한 것이라 안 바뀐다.
         */
        [Test]
        public void Evolution_MovesDpsAndProgress()
        {
            var field = FieldFromAssets();

            var with = StageSimulation.Run(50, field);
            var without = StageSimulation.Run(50, field,
                new StageSimulation.Policy { SkipEvolution = true });

            var end = with[49];

            // 1. 장부 - 지나온 문의 수가 그대로 티어다. st50이면 문 둘(30·40)
            Assert.AreEqual(PromotionTrialCatalog.TierAtFrontier(50), end.EvolutionTier,
                string.Format("st50에서 티어 {0} - 게이트 표와 시뮬레이션이 갈렸다",
                    end.EvolutionTier));

            Assert.AreEqual(0, without[49].EvolutionTier,
                "귀문을 안 깬 세계인데 티어가 올랐다 - 비교군이 비교군이 아니다");

            // 2. 값 - 배수가 실제로 스탯에 도달한다
            Assert.Greater(end.EvolutionAttack, 1d, string.Format(
                "문 둘을 넘었는데 공격 배수가 x{0:F2}다", end.EvolutionAttack));
            Assert.Greater(end.ExpectedDps, without[49].ExpectedDps,
                "문을 넘었는데 DPS가 안 넘은 쪽보다 낮다");
            Assert.Greater(end.MaxHealth, without[49].MaxHealth,
                "전직 체력 배수가 최대 체력에 도달하지 않았다");

            // 3. 시간 - 가속 구간이 실제로 빨라진다. 기준 4%는 20단계 골드 축
            //    -> 32단계 장비와 같은 자다
            double withSeconds = SegmentSeconds(with, 31, 50);
            double withoutSeconds = SegmentSeconds(without, 31, 50);
            double gain = 1d - withSeconds / withoutSeconds;

            Assert.GreaterOrEqual(gain, 0.04d, string.Format(
                "문을 넘은 플레이어의 st31~50이 {0:F0}초, 안 넘은 쪽이 {1:F0}초로 "
                + "이득이 {2:P1}뿐이다. 이 관문은 죽었다 - 티어 배수를 키워라 "
                + "(보정항은 이미 없으므로 낮출 지수가 없다)",
                withSeconds, withoutSeconds, gain));
        }

        // ------------------------------------------------------------ 게이트 표

        /**
         * @brief 게이트 표와 시뮬레이션의 티어가 **정확히** 같다.
         *
         * ## 33단계의 이 검사가 왜 더 이상 유효하지 않은가
         *
         * 그때 이 자리에는 닫힌 식(`FirstTierStage` 37 + 2스테이지당 한 칸)이
         * 있었고, 그것은 "곡선 추종 플레이어가 언제 살 수 있는가"의 **추정**
         * 이었다. 추정이라 허용 오차가 ±1티어였고, 그 폭 안에서 보정이 실제보다
         * 가볍거나 무겁게 걸릴 수 있었다.
         *
         * 티어의 출처가 게이트로 바뀌면서 그 식이 **사실**이 됐다. 허용 오차를
         * 남길 이유가 없어졌으므로 등호로 잰다 - 한 칸이라도 어긋나면 표가
         * 두 벌이 됐다는 뜻이고, 그때 세이브 마이그레이션과 밸런스가 다른
         * 세계를 잰다.
         *
         * 구간을 st50이 아니라 첫 문 뒤 전부로 넓혔다. 문 여섯 중 셋이 st50
         * 뒤에 있으므로, 옛 구간으로는 절반을 아무도 안 잰다.
         */
        [Test]
        public void ExpectedCurve_TracksTheSimulation()
        {
            var results = StageSimulation.Run(200, FieldFromAssets());

            foreach (var row in results)
            {
                Assert.AreEqual(EvolutionCurve.ExpectedTierAtStage(row.Stage), row.EvolutionTier,
                    string.Format("stage {0}: 게이트 표 {1}티어, 실측 {2}티어",
                        row.Stage, EvolutionCurve.ExpectedTierAtStage(row.Stage), row.EvolutionTier));
            }

            // 경계 열셋. 부등호가 `<`라는 사실이 마이그레이션 경계 전부를
            // 정하므로, 표와 함수가 같은 부등호를 쓰는지 여기서 못 박는다
            var boundaries = new[]
            {
                new[] { 29, 0 }, new[] { 30, 0 }, new[] { 31, 1 },
                new[] { 40, 1 }, new[] { 41, 2 },
                new[] { 50, 2 }, new[] { 51, 3 },
                new[] { 70, 3 }, new[] { 71, 4 },
                new[] { 100, 4 }, new[] { 101, 5 },
                new[] { 150, 5 }, new[] { 151, 6 }
            };

            foreach (var pair in boundaries)
                Assert.AreEqual(pair[1], EvolutionCurve.ExpectedTierAtStage(pair[0]), string.Format(
                    "최전선 {0}에서 티어 {1}이어야 하는데 {2}다 - 도착과 클리어를 섞었다",
                    pair[0], pair[1], EvolutionCurve.ExpectedTierAtStage(pair[0])));
        }

        // ------------------------------------------------------------ 표의 형태

        /**
         * @brief 모든 티어가 눌렀을 때 체감되는 도약이다.
         *
         * 16단계의 MinimumFeltGain(1%)이 아니라 **5%**를 쓴다. 전직은 강화
         * 한 칸이 아니라 마일스톤이고, 보석 수백 개짜리 버튼이 5%도 안 움직이면
         * 그 값이 설명되지 않는다.
         */
        [Test]
        public void EveryTier_IsAFeltJump()
        {
            Assert.AreEqual(6, EvolutionCurve.MaxTier, "티어 수가 바뀌었으면 밴드 몫도 다시 재야 한다");

            for (int t = 0; t < EvolutionCatalog.Count; t++)
            {
                var spec = EvolutionCatalog.Tiers[t];

                Assert.GreaterOrEqual(spec.AttackStep, 1.05d, string.Format(
                    "'{0}'의 공격 도약이 {1:P1}뿐이다 - 마일스톤이 아니라 단련이다",
                    spec.Name, spec.AttackStep - 1d));
                Assert.GreaterOrEqual(spec.HealthStep, 1.01d,
                    "'" + spec.Name + "'의 체력 도약이 1% 미만이다");

                Assert.IsFalse(string.IsNullOrEmpty(spec.Name), "티어 " + (t + 1) + "의 이름이 없다");
                Assert.IsFalse(string.IsNullOrEmpty(spec.SpriteFolder),
                    "'" + spec.Name + "'의 스프라이트 팩이 없다");
            }

            // **표에 값 칸이 없다.** 3단계가 `GemCost`/`GoldCost`를 지웠다 -
            // 승급은 귀문 돌파로 무료로 온다. 값의 단조 증가를 재던 옛 계약은
            // 잴 대상이 사라져 함께 폐기됐고, 그 자리를 `PromotionCostsNothing`이
            // 대신한다.

            // 마지막 티어만 Aura다. 중간에 Aura가 있으면 최종의 서명이 아니게 된다
            for (int t = 0; t < EvolutionCatalog.Count; t++)
                Assert.AreEqual(t == EvolutionCatalog.Count - 1, EvolutionCatalog.Tiers[t].Aura,
                    "'" + EvolutionCatalog.Tiers[t].Name + "'의 Aura 플래그가 자리에 맞지 않는다");
        }

        /**
         * @brief 상한 밖에서 값이 이어지지 않는다 - 경계값.
         */
        [Test]
        public void CurveEdges_AreClamped()
        {
            Assert.AreEqual(1d, EvolutionCurve.AttackMultiplierAt(-1), 1e-12d);
            Assert.AreEqual(EvolutionCurve.AttackMultiplierAt(EvolutionCurve.MaxTier),
                EvolutionCurve.AttackMultiplierAt(EvolutionCurve.MaxTier + 5), 1e-12d,
                "상한 위의 티어가 더 큰 배수를 낸다 - 세이브 손상이 스탯이 된다");

            Assert.IsFalse(EvolutionCurve.CanEvolve(EvolutionCurve.MaxTier));
            Assert.IsTrue(EvolutionCurve.CanEvolve(0));

            // 해금 경계는 **레벨이 아니라 문이다.** `UnlockLevel`은 3단계에
            // 사라졌다 - 레벨이 차도 살 것이 없고, 문을 넘으면 레벨과 무관하게
            // 오른다. 그 경계는 PromotionDomainTests가 잰다
            Assert.AreEqual(0, PromotionTrialCatalog.TierAtFrontier(
                PromotionTrialCatalog.GateStages[0]));
            Assert.AreEqual(1, PromotionTrialCatalog.TierAtFrontier(
                PromotionTrialCatalog.GateStages[0] + 1));
        }

        // ------------------------------------------------------------ 값

        /**
         * @brief **승급의 값은 0이다.** 이 축에 재화가 한 개도 들어오지 않는다.
         *
         * ## 33단계의 `GemLadder_TotalIsIntentional`을 대체한다
         *
         * 그 검사는 사다리 총합 3,010이 의도값인지를 물었다 - "업적 전체의 약
         * 일곱 배 = 일일 보석 기준 두 달"이 그때의 설계였고, gem sink의 크기를
         * 붙잡는 자리였다.
         *
         * 승급이 귀문 돌파로 무료가 되면서 그 질문이 통째로 사라졌다. gem sink는
         * 이제 장비(1,060) · 동료(680) · 요도·오의 뽑기(무한)가 담당하고, 이 축은
         * **경제 밖**이다. 유료 항이 다시 들어오면 여기서 먼저 걸리고, 그때는
         * 밴드·초기 경제·도달일을 전부 다시 재야 한다.
         */
        [Test]
        public void PromotionCostsNothing()
        {
            Assert.AreEqual(0, PromotionTrialCatalog.PromotionGemCost,
                "승급에 보석 값이 생겼다 - gem sink가 바뀌었으므로 초기 경제를 다시 재라");
            Assert.AreEqual(0d, PromotionTrialCatalog.PromotionGoldCost, 0d,
                "승급에 골드 값이 생겼다 - 지갑을 비우면 생존 축이 굶는다(33단계 실측 0.86)");

            // 상수만 0이고 코드가 여전히 재화를 빼면 이 계약은 아무것도 안
            // 지킨다 - "상수의 존재는 연결의 증거가 아니다"(33단계). 그래서
            // 시뮬레이션에서도 잰다.
            //
            // ## 지출 총액을 비교하지 않는 이유
            //
            // 두 보석 정책은 **화력이 달라서** 장비 단련 진도와 동료 해금 시점이
            // 갈리고, 그 결과 같은 스테이지에서 지출 총액이 다르다. 총액은
            // 승급 비용의 함수가 아니라 진행 속도의 함수이므로 그것으로는
            // 아무것도 증명하지 못한다.
            //
            // 대신 **직교성**을 잰다: 재화가 다른 두 세계에서 티어가 스테이지마다
            // 완전히 같으면, 티어는 재화의 함수가 아니다. 승급이 한 푼이라도
            // 받으면 두 곡선이 갈린다 - 그것이 33단계의 세계였다(하한 1티어,
            // 무제한 6티어).
            var rich = StageSimulation.Run(200, FieldFromAssets());
            var poor = StageSimulation.Run(200, FieldFromAssets(),
                new StageSimulation.Policy { GemsFromQuestsOnly = true });

            for (int i = 0; i < rich.Count; i++)
                Assert.AreEqual(rich[i].EvolutionTier, poor[i].EvolutionTier, string.Format(
                    "stage {0}: 보석 무제한이 {1}티어, 하한이 {2}티어다 - 승급이 재화를 "
                    + "받고 있다 (보석 {3} 대 {4})",
                    i + 1, rich[i].EvolutionTier, poor[i].EvolutionTier,
                    rich[i].GemsEarned, poor[i].GemsEarned));

            // 그리고 그 티어가 실제로 여섯까지 간다. 위 등식만으로는 "둘 다
            // 0티어"라는 죽은 세계도 통과한다
            Assert.AreEqual(EvolutionCurve.MaxTier, poor[199].EvolutionTier,
                "하한 플레이어가 st200에서 사다리 끝에 없다");
        }

        /**
         * @brief 폐기된 검사의 자리 - **`Gems_AreTheBindingConstraintForTheFloorPlayer`.**
         *
         * ## 왜 지웠는가
         *
         * 그 검사는 하한 플레이어가 "한 티어는 오르고 여섯은 못 오른다"를
         * 요구했다. 그 사이 어딘가에서 멈추는 거리가 일일 보석과 보석 상품의
         * 값어치라는 것이 33단계의 설계였다.
         *
         * 무료 세계에서 그 요구는 **거짓이 되는 것이 옳다** - 하한 플레이어도
         * 문을 넘으면 여섯 티어를 전부 갖는다. 검사를 고쳐서 살릴 수가 없다.
         * 요구하던 것(사다리 중간에서 멈춤)이 이제 버그이기 때문이다.
         *
         * ## 무엇이 그 자리를 대신하는가
         *
         *   보석의 값어치     `PromotionEconomyTests.PaidGachaIsReachableWithinThirtyDays`
         *                    `PromotionEconomyTests.CoreAxesFinishWithinAMonth`
         *                    (승급이 무료가 되어 **다른 축이 빨라진 것**이 값어치다)
         *   진행이 안 막힘   `PromotionTrialTests.GemFloorPlayer_ClearsOnArrivalWithoutFarming`
         *                    (재화가 아니라 **화력**이 병목이라는 새 계약)
         *   이 축의 무료     위 `PromotionCostsNothing`
         *
         * 이 검사는 그 이동을 코드에 남기려고 있다. 지운 이유를 커밋 메시지에만
         * 적으면 반년 뒤에 아무도 못 찾는다.
         */
        [Test]
        public void FloorPlayerIsGatedByPower_NotByGems()
        {
            var floor = StageSimulation.Run(200, FieldFromAssets(),
                new StageSimulation.Policy { GemsFromQuestsOnly = true });

            // 하한 플레이어도 여섯 문을 전부 갖는다 - 옛 검사가 금지하던 상태다
            Assert.AreEqual(EvolutionCurve.MaxTier, floor[199].EvolutionTier, string.Format(
                "하한 플레이어가 st200에서 {0}티어다 - 게이트가 재화를 다시 보고 있다",
                floor[199].EvolutionTier));

            // 그리고 그 문들을 실제 화력으로 넘는다. 넘지 못하면 진행이 막히고,
            // 그것이 이 설계에서 유일하게 남은 병목이다
            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
            {
                int stage = PromotionTrialCatalog.GateStages[gate - 1];
                var result = PromotionTrialSimulation.Run(
                    PromotionTrialFixture.Capped(
                        PromotionTrialFixture.PlayerAt(PromotionTrialFixture.GemFloor(), stage),
                        gate, PromotionTrialCatalog.SoftCapExponent),
                    PromotionTrialFixture.FoesForGate(gate),
                    PromotionTrialFixture.DefaultRules());

                Assert.AreEqual(PromotionTrialSimulation.Outcome.Cleared, result.Outcome,
                    string.Format("문{0}: 하한 플레이어가 {1}로 끝났다 - 진행이 막힌다",
                        gate, result.Outcome));
            }
        }
    }
}
