using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 요괴 봉인 뽑기(가챠)와 상점.
     *
     * 이 축이 앞의 것들과 다른 점 하나가 검사의 절반을 정한다 - **파는 것이
     * 이미 있는 축의 재료다.** 새 파워를 만들지 않고 요도의 시계를 앞당길
     * 뿐이라, 검사의 무게중심이 "얼마나 세지는가"가 아니라 **"얼마나 앞설 수
     * 있는가"**(GachaCurve.LeadTiers)에 있다.
     *
     * 그래서 여기서 가장 많이 검사하는 것은 셋이다:
     *
     *   상한   혼 정수가 드랍 일정보다 정해진 만큼만 앞선다
     *   비잠식 무과금의 바닥이 뽑기 때문에 **한 비트도** 움직이지 않는다
     *   재고   상한에 닿으면 멈추고, 바퀴가 돌면 다시 열린다
     */
    public class GachaTests
    {
        static StageSimulation.Field Field()
        {
            return new StageSimulation.Field
            {
                AverageMobHealth = 14.222d,
                AverageMobGold = 5.444d,
                SpawnInterval = 1.1d
            };
        }

        static double TotalSeconds(List<StageSimulation.StageResult> rows, int from, int to)
        {
            double total = 0d;
            for (int i = from - 1; i < rows.Count && i < to; i++)
                total += rows[i].MobSeconds + rows[i].BossKillSeconds;
            return total;
        }

        // ---------------------------------------------------------------- 확률표

        [Test]
        public void Table_SumsToOne()
        {
            double total = 0d;
            foreach (double chance in GachaCurve.Chances) total += chance;

            Assert.AreEqual(1d, total, 1e-12d,
                "확률표의 합이 1이 아니다 - 화면에 공개하는 표라 여기가 어긋나면 "
                + "표시된 확률과 실제가 다르다");
        }

        [Test]
        public void Table_HasNoBlank()
        {
            // 꽝이 없다. 파편 등급 셋은 파편을 주고, 위쪽 셋은 요도의 상태를
            // 바꾼다 - 방치형에서 아무것도 안 주는 결과는 긴장이 아니라 짜증이다
            for (int i = 0; i < GachaCurve.OutcomeCount; i++)
            {
                var outcome = (GachaCurve.Outcome)i;
                bool gives = GachaCurve.IsBladeReward(outcome) || GachaCurve.ShardsOf[i] > 0;
                Assert.IsTrue(gives, "결과 " + outcome + "이 아무것도 주지 않는다 - 꽝이다");
                Assert.Greater(GachaCurve.Chances[i], 0d, "결과 " + outcome + "의 확률이 0이다");
            }
        }

        // ------------------------------------------------ 47단계: 희귀도 사다리

        /**
         * @brief 등급이 위로 갈수록 **드물어지는가.** 사다리의 정의다.
         *
         * 뒤집히면 사다리가 아니라 목록이다 - "전설이 영웅보다 흔하다"는
         * 표는 확률표가 아니라 오타로 읽힌다.
         */
        [Test]
        public void Ladder_IsMonotoneByGrade()
        {
            var mass = new double[GachaCurve.GradeCount];
            for (int i = 0; i < GachaCurve.OutcomeCount; i++)
                mass[(int)GachaCurve.GradeOf[i]] += GachaCurve.Chances[i];

            for (int g = 1; g < GachaCurve.GradeCount; g++)
            {
                Assert.Greater(mass[g - 1], mass[g], string.Format(
                    "{0}({1:P2})가 {2}({3:P2})보다 드물다 - 사다리가 뒤집혔다",
                    GachaCurve.GradeNames[g - 1], mass[g - 1],
                    GachaCurve.GradeNames[g], mass[g]));

                Assert.Greater(mass[g], 0d,
                    GachaCurve.GradeNames[g] + " 등급에 결과가 하나도 없다");
            }
        }

        /**
         * @brief 46단계의 세 등급이 **비트 단위로** 그대로인가.
         *
         * 이 스텝의 약속이다 - "갈아엎지 말고 그 위에 얹는다". ★3(혼 정수)은
         * 티어를 앞당기고 티어는 심층 밴드가 재는 값이라, 그 확률을 움직이면
         * 46단계의 재기준과 이번 신규 축이 뒤섞여 **무엇이 천장을 밀었는지**를
         * 나눠 잴 수 없게 된다.
         */
        [Test]
        public void Ladder_KeepsTheStep46Rows()
        {
            Assert.AreEqual(6, GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardSmall]);
            Assert.AreEqual(20, GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardLarge]);
            Assert.AreEqual(70, GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardJackpot]);

            Assert.AreEqual(0.05d, GachaCurve.Chances[(int)GachaCurve.Outcome.ShardJackpot], 1e-12d,
                "잭팟 확률이 움직였다");
            Assert.AreEqual(0.03d, GachaCurve.EssenceChance, 1e-12d,
                "혼 정수 확률이 움직였다 - 46단계의 재기준과 이번 신규 축이 섞인다");
        }

        /**
         * @brief Lv.1의 ★4 이상 대기가 **표 그대로**인가 (68단계).
         *
         * 47단계에는 이 자리가 "천장이 ★4+를 지키는가"였다(실효 20.2회).
         * 68단계에 천장이 사라져 Lv.1은 표의 2.9% = 34.5회이고, 그 간격을
         * 메우는 것이 소환 레벨이다 - 옛 천장의 리듬(20.2회)에 닿는 레벨은
         * SummonLevelTests가 잰다.
         */
        [Test]
        public void EpicOrBetter_AtLevelOneIsTheTable()
        {
            Assert.AreEqual(GachaCurve.RarityChance + GachaCurve.LegendaryChance,
                            GachaCurve.EpicOrBetterChance, 1e-12d);
            Assert.AreEqual(GachaCurve.EpicOrBetterChance, GachaCurve.EpicOrBetterChanceAt(1), 1e-12d,
                "Lv.1이 표와 다르다 - 공개 확률표가 신규 계정에게 거짓말을 한다");
            Assert.AreEqual(1d / GachaCurve.EpicOrBetterChance, GachaCurve.ExpectedPullsPerEpic(1), 1e-9d);
        }

        /**
         * @brief 혼격 상한이 **계약 구간 안에서 상수인가.**
         *
         * 이 스텝의 재기준이 한 번의 이동으로 끝나는 근거다. 첫 설계는
         * "혼격 상한 = 티어 / 2"였고, 티어가 계약 구간 안에서 3->5로 자라니
         * 혼격도 1->2로 자라 과금 곡선이 그 구간에서 발산했다 - 수렴 검사가
         * 1.335(문턱 1.35)까지 갔다. 46단계의 리드 상한과 같은 처방으로
         * 고쳤고, 이 검사가 그 회귀를 막는다.
         */
        [Test]
        public void Rarity_CapStaysFlatThroughTheContract()
        {
            for (int stage = YodoCurve.UnlockStage; stage <= 200; stage++)
                for (int i = 0; i < YodoCatalog.Count; i++)
                    Assert.AreEqual(YodoRarityCurve.BaseCap, YodoRarityCurve.CapAt(i, stage),
                        string.Format("st{0}의 {1}번 자루에서 혼격 상한이 {2}가 됐다 - "
                        + "계약 구간 안에서 과금 곡선이 발산한다",
                        stage, i, YodoRarityCurve.CapAt(i, stage)));
        }

        /** 계약 밖에서는 자란다 - 그렇지 않으면 혼격의 재고가 넷으로 끝난다 */
        [Test]
        public void Rarity_CapGrowsOutsideTheContract()
        {
            Assert.Greater(YodoRarityCurve.CapAt(0, 4000), YodoRarityCurve.BaseCap,
                "혼격 상한이 영원히 1이다 - 상위 혼 넷을 받고 나면 팔 것이 없다");

            Assert.LessOrEqual(YodoRarityCurve.CapAt(0, 100000), YodoRarityCurve.MaxRarity,
                "혼격 상한이 MaxRarity를 넘는다");
        }

        [Test]
        public void Rarity_IsRefusedBeforeTheSeal()
        {
            // 미봉인 자루는 혼격을 못 받는다. 44단계의 "첫 봉인은 그 요괴를
            // 벤 사람의 것"이 여기서도 그대로다 - 혼격만 올려 미봉인 칼을
            // 세게 만들 수 있으면 도감의 첫 줄이 결제로 켜진다
            Assert.IsFalse(YodoRarityCurve.Accepts(0, 4000, 0, 0),
                "미봉인 자루가 상위 혼을 받는다 - 킬 게이트가 뚫렸다");

            Assert.IsTrue(YodoRarityCurve.Accepts(0, 4000, 1, 0),
                "봉인된 자루가 상위 혼을 못 받는다");
        }

        [Test]
        public void RarityTarget_PicksTheLowestRarity()
        {
            const int frontier = 4000;
            var tiers = new[] { 3, 3, 3, 3 };
            var rarities = new[] { 2, 0, 1, 2 };

            Assert.AreEqual(1, YodoRarityCurve.TargetFor(frontier, tiers, rarities),
                "상위 혼이 가장 낮은 혼격으로 가지 않는다 - 뽑기가 몰아주기를 판다");
        }

        [Test]
        public void RarityTarget_ReturnsNothingWhenEveryBladeIsCapped()
        {
            const int frontier = 60;
            var tiers = new[] { 3, 3, 3, 3 };
            var rarities = new int[YodoCatalog.Count];
            for (int i = 0; i < rarities.Length; i++)
                rarities[i] = YodoRarityCurve.CapAt(i, frontier);

            Assert.AreEqual(-1, YodoRarityCurve.TargetFor(frontier, tiers, rarities),
                "상한에 다 닿았는데도 상위 혼이 갈 곳이 있다");
        }

        /**
         * @brief 혼격 0과 미보유 전설이 **정확히 1인가.** f2p 안전선이다.
         *
         * 무과금은 이 사다리의 위쪽 두 칸에 자연 출처로 닿을 수 없다. 그
         * 값들이 정확히 1로 떨어지지 않으면 44·45단계의 기대 곡선과 보정이
         * 어긋나고, 그 어긋남이 그대로 f2p 바닥의 이동이 된다.
         */
        [Test]
        public void Ladder_IsExactlyNeutralWhenEmpty()
        {
            Assert.AreEqual(1d, YodoRarityCurve.ValueAt(0), 0d);
            Assert.AreEqual(1d, LegendaryYodoCurve.PowerAt(0), 0d);
            Assert.AreEqual(1d, LegendaryYodoCurve.SpiritAt(0), 0d);

            var tiers = new[] { 5, 5, 5, 5 };
            var zeroRarity = new int[YodoCatalog.Count];
            var noLegend = new int[LegendaryYodoCatalog.Count];

            Assert.AreEqual(YodoCurve.AttackFactorFor(tiers),
                            YodoCurve.AttackFactorFor(tiers, zeroRarity, noLegend), 0d,
                "빈 사다리가 공격력을 움직인다");

            for (int s = 0; s < SkillCatalog.Count; s++)
                Assert.AreEqual(YodoAffinityCurve.FactorForSkill(s, tiers),
                                YodoAffinityCurve.FactorForSkill(s, tiers, zeroRarity, noLegend), 0d,
                    "빈 사다리가 상성을 움직인다");

            Assert.AreEqual(YodoSpiritCurve.RateFor(tiers),
                            YodoSpiritCurve.RateFor(tiers, zeroRarity, noLegend), 0d,
                "빈 사다리가 영체를 움직인다");
        }

        /**
         * @brief 전설이 **오니키리 완성에 안 들어가는가.** 두 축의 정체성이다.
         *
         * 보스 혼 넷은 완성의 세로축이고 전설은 수집의 가로축이다
         * (LegendaryYodoSpec 머리 주석). 전설이 세트 보너스를 건드리면
         * 도감의 마지막 줄이 뽑기로 켜지고, 44단계부터의 기둥("첫 봉인은
         * 영원히 보스의 혼이 필요하다")이 값에서 무너진다.
         */
        [Test]
        public void Legendary_DoesNotCompleteOnikiri()
        {
            var tiers = new int[YodoCatalog.Count];
            var full = new int[LegendaryYodoCatalog.Count];
            for (int i = 0; i < full.Length; i++) full[i] = LegendaryYodoCurve.MaxCopies;

            // 보스 요도가 하나도 없고 전설만 상한인 세계
            double factor = YodoCurve.AttackFactorFor(tiers, null, full);

            Assert.AreEqual(YodoCurve.SetBonusAt(0) * LegendaryYodoCurve.PowerFactor(full),
                            factor, 1e-12d,
                "전설이 세트 보너스를 건드린다 - 오니키리가 뽑기로 완성된다");

            // 가로축이 세로축보다 작다. 전설 둘을 상한 사본까지 돌파해도
            // 보스 요도 넷을 상한 티어까지 벼린 것보다 작아야 한다 -
            // 크기가 뒤집히면 "완성"이 목적지가 아니라 우회로가 된다
            Assert.Less(LegendaryYodoCurve.Ceiling, YodoCurve.Ceiling, string.Format(
                "전설 상한(x{0:F3})이 요도 상한(x{1:F3})보다 크다 - "
                + "세로축이 가로축에 먹혔다",
                LegendaryYodoCurve.Ceiling, YodoCurve.Ceiling));
        }

        [Test]
        public void LegendaryTarget_FillsTheGapBeforeBreaking()
        {
            // 미보유가 있으면 언제나 그쪽이 먼저다 - 두 자루를 다 모으기
            // 전에는 돌파가 시작되지 않고, 그래서 "새 칼"이 앞에 온다
            Assert.AreEqual(0, LegendaryYodoCurve.TargetFor(new[] { 0, 0 }));
            Assert.AreEqual(1, LegendaryYodoCurve.TargetFor(new[] { 2, 0 }));
            Assert.AreEqual(1, LegendaryYodoCurve.TargetFor(new[] { 2, 1 }));

            var full = new int[LegendaryYodoCatalog.Count];
            for (int i = 0; i < full.Length; i++) full[i] = LegendaryYodoCurve.MaxCopies;
            Assert.AreEqual(-1, LegendaryYodoCurve.TargetFor(full),
                "상한 사본인데도 전설이 갈 곳이 있다");
        }

        /**
         * @brief 넘침 값이 **사다리를 따라 커지는가.**
         *
         * 44단계의 "버려지는 드랍 0"을 세 등급이 이어받는다. 등급이 위면
         * 넘쳐도 위여야 하고, 그렇지 않으면 사다리가 그 자리에서 평평해진다 -
         * ★5가 중복이라고 ★1보다 못한 결과가 되면 그것은 확률이 아니라 사고다.
         */
        [Test]
        public void Overflow_ClimbsWithTheLadder()
        {
            int jackpot = GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardJackpot];

            Assert.Greater(YodoCurve.ShardsPerOverflowSoul, 0);
            Assert.Greater(GachaCurve.ShardsPerOverflowRarity, YodoCurve.ShardsPerOverflowSoul,
                "★4의 넘침이 ★3과 같거나 작다 - 사다리가 넘침에서 평평해진다");
            Assert.Greater(LegendaryYodoCurve.ShardsPerOverflow, GachaCurve.ShardsPerOverflowRarity,
                "★5의 넘침이 ★4보다 작다");
            Assert.Greater(LegendaryYodoCurve.ShardsPerOverflow, jackpot,
                "★5의 넘침이 잭팟보다 작다 - 200회에 한 번이 5%보다 못하다");
        }

        [Test]
        public void Table_ShardGradesAreDistinct()
        {
            int small = GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardSmall];
            int large = GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardLarge];
            int jackpot = GachaCurve.ShardsOf[(int)GachaCurve.Outcome.ShardJackpot];

            Assert.Greater(large, small * 2, "'대'가 '소' 두 번과 구분되지 않는다 - 등급이 이름뿐이다");
            Assert.Greater(jackpot, YodoCurve.ShardPackShards * 3,
                "잭팟이 촉매 세 묶음보다 작다 - 한 번의 결과가 사건으로 안 읽힌다");
        }

        [Test]
        public void Roll_CoversTheWholeTable()
        {
            // 표의 경계를 정확히 짚는다. 누적 확률의 끝에서 한 칸 밀리면
            // 마지막 결과(전설)가 영영 안 나온다
            Assert.AreEqual(GachaCurve.Outcome.ShardSmall, GachaCurve.Roll(0d, 1));
            Assert.AreEqual(GachaCurve.Outcome.LegendaryBlade, GachaCurve.Roll(0.99999d, 1));

            double cumulative = 0d;
            for (int i = 0; i < GachaCurve.OutcomeCount; i++)
            {
                double middle = cumulative + GachaCurve.Chances[i] * 0.5d;
                Assert.AreEqual((GachaCurve.Outcome)i, GachaCurve.Roll(middle, 1),
                    "확률 구간 " + i + "의 한가운데가 다른 결과를 낸다");
                cumulative += GachaCurve.Chances[i];
            }
        }

        // ---------------------------------------------------------------- 값

        /**
         * @brief 촉매가 보석당 파편을 더 많이 준다. **두 버튼이 같이 사는 이유다.**
         *
         * 뒤집히면 촉매가 죽는다 - 확정 상품이 확률 상품보다 기대값까지 낮으면
         * 누를 이유가 없다. 이 부등호가 화면의 "파편만 필요하면 촉매"라는
         * 구분을 지탱한다.
         */
        [Test]
        public void Catalyst_BeatsGachaPerGemOnShards()
        {
            double catalyst = (double)YodoCurve.ShardPackShards / YodoCurve.ShardPackGems;
            // Lv.1이 뽑기의 파편이 가장 많은 자리다 - 레벨이 오르면 파편 몫이
            // 위쪽 등급으로 옮겨 간다(GachaCurve.ExpectedShardsPerPull). 여기서
            // 이기면 모든 레벨에서 이긴다
            double gacha = GachaCurve.ExpectedShardsPerPull(1) / GachaCurve.PullCostGems;

            Assert.Greater(catalyst, gacha, string.Format(
                "뽑기가 보석당 파편을 촉매보다 많이 준다 ({0:F3} vs {1:F3}) - "
                + "촉매(파편 조달)가 죽은 버튼이 된다", gacha, catalyst));
        }

        [Test]
        public void TenPull_IsCheaperThanTenSingles()
        {
            Assert.Less(GachaCurve.TenPullCostGems,
                        GachaCurve.PullCostGems * GachaCurve.TenPullCount,
                "10연에 할인이 없다 - 소환 레벨을 미는 단위(10연)가 화면에서 사라진다");
        }

        /**
         * @brief ★3의 기대 대기가 여전히 **확률의 일**인가 - 모든 레벨에서.
         *
         * 47단계에는 천장이 ★3을 눌렀다. 68단계에는 레벨이 ★4·★5를 키우며
         * ★3을 상대적으로 누른다 - 그래서 Lv.1뿐 아니라 st500 추종 레벨 근처
         * (Lv.7~10)에서도 사람이 셀 수 있는 대기인지 본다.
         */
        [Test]
        public void Essence_StaysAReachableRoll()
        {
            foreach (int level in new[] { 1, 5, 10 })
            {
                double wait = GachaCurve.ExpectedPullsPerEssence(level);
                Assert.Greater(wait, 1d, "Lv." + level + " 혼 정수가 사실상 매번 나온다 - 확률이 아니다");
                Assert.Less(wait, 60d, string.Format(
                    "Lv.{0} 혼 정수 대기가 {1:F1}회다 - 10연 여섯보다 길면 ★3이 사다리에서 "
                    + "사실상 사라진다", level, wait));
            }
        }

        /**
         * @brief ★4는 언제나 ★3 이상의 값이다 - **막히면 내려간다** (47단계 계약 유지).
         *
         * 47단계 이름은 "천장이 ★4를 ★3보다 흔하게 만든다"였다. 68단계에 천장이
         * 사라져 Lv.1에서는 표대로 ★3(3.0%) > ★4(2.1%)이고, 소환 레벨이 오르면
         * 둘이 같은 비율로 자란다(SummonLevelCurve.GradeGrowth). 남는 계약은
         * 값의 순서다: 혼격이 막힌 ★4는 ★3으로 내려가므로 넘치는 ★4는 정확히
         * ★3이 된다 - 막힘 캐스케이드는 천장이 아니라 보유 상태의 문제라 그대로다.
         */
        [Test]
        public void Epic_IsAlwaysWorthAtLeastARare()
        {
            Assert.Greater(GachaCurve.EssenceChance, GachaCurve.RarityChance,
                "표에서 ★4가 ★3보다 흔하다 - 공개 확률표가 사다리를 부정한다");

            var system = BuildLadderSystem();
            try
            {
                var yodo = system.Item2;
                yodo.DebugGrantSoul(0);
                yodo.DebugForge(0);                                   // 티어 1
                for (int i = 0; i < YodoRarityCurve.MaxRarity; i++)
                    yodo.DebugGrantRarity(0);                         // 혼격 상한까지

                long before = yodo.GetBlade(0).souls;
                Assert.AreEqual(-1, yodo.TryTakeRarity(), "상한인데 혼격이 또 올랐다");
                Assert.AreEqual(0, yodo.TryTakeEssence(4000),
                    "혼격이 막힌 자리에서 혼 정수도 못 들어간다 - 미끄러질 곳이 없다");
                Assert.AreEqual(before + 1, yodo.GetBlade(0).souls,
                    "미끄러진 ★4가 혼을 안 남겼다");
            }
            finally { Object.DestroyImmediate(system.Item1); }
        }

        /**
         * @brief 하루의 경계가 일일 퀘스트와 **같은 함수**에서 나온다.
         *
         * 두 곳에 적으면 갈리고, 그 증상은 "퀘스트는 리셋됐는데 무료 뽑기는
         * 아직"이다 - 플레이어에게는 버그가 아니라 사기로 읽힌다.
         */
        [Test]
        public void FreePull_SharesTheQuestDayBoundary()
        {
            var probe = new System.DateTime(2026, 8, 11, 18, 30, 0, System.DateTimeKind.Utc);
            Assert.AreEqual(QuestSystem.QuestDayOf(probe), GachaCurve.DayOf(probe));

            // KST 04:00 = UTC 19:00. 그 앞뒤가 다른 날이어야 한다
            var before = new System.DateTime(2026, 8, 11, 18, 59, 0, System.DateTimeKind.Utc);
            var after = new System.DateTime(2026, 8, 11, 19, 1, 0, System.DateTimeKind.Utc);
            Assert.AreNotEqual(GachaCurve.DayOf(before), GachaCurve.DayOf(after),
                "KST 새벽 4시가 무료 뽑기의 하루 경계가 아니다");
        }

        // ---------------------------------------------------------------- 해금

        [Test]
        public void Unlock_MatchesTheYodo()
        {
            Assert.AreEqual(YodoCurve.UnlockStage, GachaCurve.UnlockStage,
                "뽑기와 요도의 해금 스테이지가 갈렸다 - 뽑았는데 넣을 칼이 없다");
        }

        /**
         * @brief 조율 구간(st1~50)이 **비트 단위로** 불변인가.
         *
         * 44·45단계가 같은 자리에서 같은 것을 쟀다. 이 스텝에서는 구조가 두
         * 겹으로 지킨다 - 뽑기가 st41에 열리고, 그때는 봉인된 자루가 아직
         * 하나도 없어 혼 정수가 갈 곳이 없다(첫 봉인은 st50 피날레의 혼).
         */
        [Test]
        public void Gacha_IsAbsentFromTheTunedBands()
        {
            var field = Field();
            var with = StageSimulation.Run(50, field);
            var without = StageSimulation.Run(50, field,
                new StageSimulation.Policy { SkipGacha = true });

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(without[i].BossMargin, with[i].BossMargin, 0d,
                    "stage " + (i + 1) + ": 뽑기가 조율 구간의 여유를 움직였다");
                Assert.AreEqual(0d, with[i].GachaPulls, 0d,
                    "stage " + (i + 1) + ": 조율 구간에서 뽑기가 돌았다");
            }
        }

        // ---------------------------------------------------------------- 상한

        [Test]
        public void Essence_IsRefusedBeforeTheSeal()
        {
            // 티어 0 = 미봉인. 첫 봉인은 그 요괴를 벤 사람의 것이다
            Assert.IsFalse(GachaCurve.AcceptsEssence(0, 200, 0, 0L),
                "미봉인 자루가 혼 정수를 받는다 - 킬 게이트가 뚫렸다");
        }

        [Test]
        public void Essence_IsRefusedAtTheMaxTier()
        {
            Assert.IsFalse(GachaCurve.AcceptsEssence(0, 4000, YodoCurve.MaxTier, 0L),
                "상한 티어가 혼 정수를 더 받는다");
        }

        /**
         * @brief 리드 상한이 **손에 든 혼까지** 센다.
         *
         * 티어만 세면 혼을 쟁여 두는 것으로 상한을 통과할 수 있고, 그러면
         * 상한이 아니라 지연일 뿐이다.
         */
        [Test]
        public void Essence_CapCountsHeldSouls()
        {
            const int frontier = 200;
            int dropped = GachaCurve.SoulsDroppedThrough(0, frontier);
            int cap = GachaCurve.EssenceSoulCap(0, frontier);

            Assert.IsTrue(GachaCurve.AcceptsEssence(0, frontier, dropped, 0L),
                "일정만큼 올린 자루가 정수를 못 받는다 - 리드가 0이다");
            Assert.IsFalse(GachaCurve.AcceptsEssence(0, frontier, dropped, cap - dropped),
                "티어는 낮은데 혼을 쟁여 상한을 넘긴 자루가 정수를 더 받는다");
        }

        /**
         * @brief 계약 구간(st51~200)에서 리드는 **영원히 1이다.**
         *
         * 이번 재기준이 한 번의 이동으로 끝나는 근거다 - 리드가 자라는 주기가
         * 5바퀴이고 다섯째 혼은 st220 언저리라 계약 구간 밖이다.
         */
        [Test]
        public void Lead_StaysAtOneThroughTheContract()
        {
            for (int stage = YodoCurve.UnlockStage; stage <= 200; stage++)
                for (int i = 0; i < YodoCatalog.Count; i++)
                {
                    int dropped = GachaCurve.SoulsDroppedThrough(i, stage);
                    Assert.AreEqual(GachaCurve.LeadTiers, GachaCurve.LeadTiersAt(dropped),
                        string.Format("st{0}의 {1}번 자루에서 리드가 {2}가 됐다 - "
                        + "계약 구간 안에서 재기준이 두 겹이 된다",
                        stage, i, GachaCurve.LeadTiersAt(dropped)));
                }
        }

        /** 계약 밖에서는 자란다 - 그렇지 않으면 뽑기의 재고가 네 번으로 끝난다 */
        [Test]
        public void Lead_GrowsOutsideTheContract()
        {
            Assert.Greater(GachaCurve.LeadTiersAt(GachaCurve.LeadGrowthCycles),
                           GachaCurve.LeadTiers,
                "리드가 영원히 1이다 - 정수 넷을 사고 나면 상점에 팔 것이 없다");
        }

        /**
         * @brief 혼 정수가 **가장 낮은 티어**로 간다. 타겟팅이 없다.
         *
         * 45단계가 만든 트레이드오프를 지키는 규칙이다 - 상성은 몰아주기를,
         * 영체는 고르기를 보상하는데, 뽑기가 한 자루를 지목할 수 있으면
         * 과금은 언제나 몰아주기를 산다.
         */
        [Test]
        public void EssenceTarget_PicksTheLowestTier()
        {
            const int frontier = 400;
            var tiers = new[] { 5, 2, 4, 3 };
            var souls = new long[4];

            Assert.AreEqual(1, GachaCurve.EssenceTargetFor(frontier, tiers, souls),
                "혼 정수가 가장 낮은 티어로 가지 않는다 - 뽑기가 몰아주기를 판다");
        }

        [Test]
        public void EssenceTarget_SkipsUnsealedBlades()
        {
            const int frontier = 400;
            var tiers = new[] { 0, 3, 0, 4 };
            var souls = new long[4];

            Assert.AreEqual(1, GachaCurve.EssenceTargetFor(frontier, tiers, souls),
                "혼 정수가 미봉인 자루로 갔다");
        }

        [Test]
        public void EssenceTarget_ReturnsNothingWhenEveryBladeIsCapped()
        {
            const int frontier = 60;
            var tiers = new int[YodoCatalog.Count];
            var souls = new long[YodoCatalog.Count];

            for (int i = 0; i < tiers.Length; i++)
                tiers[i] = GachaCurve.EssenceSoulCap(i, frontier);

            Assert.AreEqual(-1, GachaCurve.EssenceTargetFor(frontier, tiers, souls),
                "상한에 다 닿았는데도 정수가 갈 곳이 있다 - 상한이 안 걸린다");
        }

        // ---------------------------------------------------------------- f2p 비잠식

        /**
         * @brief 무과금의 바닥이 뽑기 때문에 **한 비트도** 움직이지 않는가.
         *
         * 이 스텝의 안전선이다. 무과금은 보석을 뽑기에 쓰지 않고(코어 진행에
         * 다 배정돼 있다 - 44단계 실측) 그의 뽑기 접근은 일일 무료인데
         * 시뮬레이션에는 달력이 없다. 그래서 보고되는 f2p 바닥은 여전히
         * **하한**이고, 무료 뽑기는 그 위로만 얹힌다.
         */
        [Test]
        public void FreeToPlayFloor_IsUntouchedByGacha()
        {
            var field = Field();
            var with = StageSimulation.Run(300, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true });
            var without = StageSimulation.Run(300, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true,
                                             SkipGacha = true });

            for (int i = 0; i < 300; i++)
            {
                Assert.AreEqual(without[i].BossMargin, with[i].BossMargin, 0d,
                    "stage " + (i + 1) + ": 뽑기가 무과금의 바닥을 움직였다");
                Assert.AreEqual(0d, with[i].GachaPulls, 0d,
                    "stage " + (i + 1) + ": 무과금이 보석을 뽑기에 썼다 - 코어 진행이 잠식된다");
            }
        }

        /**
         * @brief 무과금이 **뽑기를 골라도** 바닥이 무너지지 않는가.
         *
         * ## 이 검사는 처음에 반대 방향이었고, 실측이 뒤집었다
         *
         * 원래 의도는 "뽑기에 보석을 부으면 손해다"를 못 박는 것이었다 -
         * 뽑기의 보석당 파편이 촉매보다 1.44배 낮으니(Catalyst_BeatsGachaPerGem
         * OnShards) 당연히 느릴 줄 알았다. **아니었다.** 잃는 것(동료 셋째
         * 해금·무기 5등급)보다 얻는 것(요도 티어)이 조금 크거나 비슷해서
         * 총 시간이 st100~400에서 0.3% 안쪽으로 같다.
         *
         * 그래서 이 검사가 지키는 것을 바꿨다. "뽑기는 손해다"는 사실이
         * 아니므로 못 박을 수 없고, 못 박아야 하는 것은 **어느 쪽도 함정이
         * 아니다**이다 - 무과금이 뽑기를 골라도 심층 바닥(밴드 계약)을 계속
         * 지나야 하고, 대신 그 선택에 **대가가 있어야** 한다(공짜면 선택이
         * 아니다). 20단계가 "안 사면 손해인가"와 "넣은 것이 이득인가"를
         * 나눈 것처럼, 여기서도 묻는 것을 실측에 맞춰 다시 정한다.
         */
        [Test]
        public void FreeToPlay_CanChooseGachaWithoutBreakingTheFloor()
        {
            var field = Field();
            var core = StageSimulation.Run(400, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipGacha = true });
            var spree = StageSimulation.Run(400, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true,
                                             GachaBeforeCore = true });

            // 바닥 계약(심층 밴드의 f2p 하한)을 계속 지난다
            for (int stage = 51; stage <= 200; stage++)
            {
                var row = spree[stage - 1];
                var tier = BossCurve.TierOf(row.Stage);
                double floor = tier == BossCurve.Tier.Finale ? 1.08d
                             : tier == BossCurve.Tier.Chapter ? 1.25d
                             : 1.4d;

                Assert.GreaterOrEqual(row.BossMargin, floor, string.Format(
                    "stage {0}: 뽑기를 고른 무과금의 여유가 {1:F2}로 바닥을 뚫었다 - "
                    + "화면이 권하지 않아도 누를 수 있는 버튼이면 그 길도 밴드 안이어야 한다",
                    row.Stage, row.BossMargin));
            }

            // 그런데 공짜는 아니다. 보석이 코어에서 빠져나간 만큼 다른 것을 잃는다
            var coreEnd = core[399];
            var spreeEnd = spree[399];
            Assert.Less(spreeEnd.PetsOwned + spreeEnd.WeaponGrade,
                        coreEnd.PetsOwned + coreEnd.WeaponGrade,
                "뽑기를 골라도 동료·장비가 그대로다 - 보석이 두 곳에서 동시에 쓰인다");
        }

        // ---------------------------------------------------------------- 죽은 버튼

        /**
         * @brief 뽑기가 실제 진행을 가속하는가. 프로젝트 기준은 4%다.
         *
         * 뽑기가 파는 것은 파편과 혼 정수 둘인데, 파편은 촉매가 이미 확정으로
         * 주고 있으므로 이 검사가 실제로 재는 것은 **혼 정수**다 - 끄면 티어가
         * 드랍 일정 그대로가 되고, 그 차이가 곧 이 스텝이 판 것이다.
         */
        [Test]
        public void Gacha_IsNotADeadButton()
        {
            var field = Field();
            var with = StageSimulation.Run(400, field);
            var without = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipGacha = true });

            double gain = TotalSeconds(without, 51, 400) / TotalSeconds(with, 51, 400) - 1d;

            Assert.Greater(gain, 0.04d, string.Format(
                "뽑기의 이득이 {0:P1}뿐이다 - 20단계 골드 축의 함정이 재현되고 있다. "
                + "리드 상한(GachaCurve.LeadTiers {1})을 올려라",
                gain, GachaCurve.LeadTiers));
        }

        /**
         * @brief 촉매가 **여전히** 이득인가.
         *
         * 겹치는 두 버튼이라 "둘 다 있을 때 하나를 빼는" 자로는 잴 수 없다 -
         * 보석이 무제한인 가속 플레이어에게 뽑기가 파편을 흘려주기 때문에
         * 촉매를 빼도 0%가 나온다. 그래서 **둘 다 없는 세계**를 기준으로
         * 각자를 잰다. 20단계가 "SkipGoldGain(사면 손해인가)"과
         * "NeutralizeGoldAxis(넣은 것이 이득인가)"를 나눈 것과 같은 처리이고,
         * 그때 배운 것도 같다 - 두 질문의 답을 섞어 읽으면 살아 있는 버튼이
         * 죽은 것으로 보인다.
         */
        [Test]
        public void Catalyst_IsStillWorthAddingAtAll()
        {
            var field = Field();
            var none = StageSimulation.Run(500, field,
                new StageSimulation.Policy { SkipShardPacks = true, SkipGacha = true });
            var onlyPack = StageSimulation.Run(500, field,
                new StageSimulation.Policy { SkipGacha = true });

            double gain = TotalSeconds(none, 100, 500) / TotalSeconds(onlyPack, 100, 500) - 1d;

            Assert.Greater(gain, 0.04d, string.Format(
                "촉매(파편 조달)를 넣은 이득이 {0:P1}뿐이다 - 44단계가 만든 버튼이 "
                + "뽑기 때문에 죽었다", gain));
        }

        // ---------------------------------------------------------------- 재고

        /**
         * @brief 뽑기가 상한에 닿으면 멈추고, 바퀴가 돌면 **다시 열리는가.**
         *
         * 상한이 곧 재고다. 계속 돌면 밴드가 무너지고, 한 번 멈춘 뒤 영영
         * 안 열리면 상점이 st100에 죽는다.
         */
        [Test]
        public void GachaStock_StopsAtTheCapAndReopensLater()
        {
            var rows = StageSimulation.Run(460, Field());

            double at100 = rows[99].GachaPulls;
            double at200 = rows[199].GachaPulls;
            double at300 = rows[299].GachaPulls;

            Assert.Greater(at100, 0d, "st100까지 뽑기가 한 번도 안 돌았다");
            Assert.AreEqual(at100, at200, 1e-9d,
                "계약 구간 안에서 뽑기가 계속 돈다 - 리드 상한이 안 걸린다");
            Assert.Greater(at300, at200,
                "상한에 닿은 뒤 영영 안 열린다 - 상점이 죽는다 "
                + "(GachaCurve.LeadGrowthCycles)");
        }

        // ---------------------------------------------------------------- 런타임

        /**
         * @brief 씬 없이 도는 GachaSystem 하나. 빌더가 씬에 하는 일의 최소판이다.
         *
         * YodoSystem을 안 물린다 - 여기서 재는 것은 **표와 천장**이고, 요도가
         * 붙으면 상한 판정이 끼어들어 무엇이 무엇을 막았는지 갈린다.
         * 상한 쪽은 위의 순수 함수 검사가 따로 잰다.
         */
        /**
         * @brief 요도까지 물린 시스템 한 벌 (47단계). 사다리의 실제 경로를 재려면 필요하다.
         *
         * 위의 BuildSystem은 **표와 천장만** 재려고 요도를 일부러 안 물린다.
         * 사다리는 반대다 - 혼격도 전설도 요도의 상태가 판정하므로
         * (YodoSystem.TryTakeRarity·TryTakeLegendary) 그쪽이 없으면 잴 것이
         * 없다.
         *
         * 최전선을 크게 두는 이유는 리드 상한을 검사 밖으로 밀기 위해서다 -
         * 여기서 재는 것은 상한이 아니라 **미끄러짐**이고, 상한이 끼어들면
         * 무엇이 무엇을 막았는지 갈린다(46단계가 BuildSystem에서 한 판단과
         * 같은 종류).
         */
        static System.Tuple<GameObject, YodoSystem> BuildLadderSystem()
        {
            var go = new GameObject("GachaLadderTestSystem");

            var progress = go.AddComponent<StageProgress>();
            progress.SetProgress(4000, 0, 0, 4000);

            var yodo = go.AddComponent<YodoSystem>();
            var so = new SerializedObject(yodo);
            so.FindProperty("stage").objectReferenceValue = progress;

            var blades = so.FindProperty("blades");
            blades.arraySize = YodoCatalog.Count;
            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                var spec = YodoCatalog.Blades[i];
                var element = blades.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("soulName").stringValue = spec.SoulName;
                element.FindPropertyRelative("bladeName").stringValue = spec.BladeName;
                element.FindPropertyRelative("bossName").stringValue = spec.BossName;
            }

            var legendaries = so.FindProperty("legendaries");
            legendaries.arraySize = LegendaryYodoCatalog.Count;
            for (int i = 0; i < LegendaryYodoCatalog.Count; i++)
            {
                var spec = LegendaryYodoCatalog.Blades[i];
                var element = legendaries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("bladeName").stringValue = spec.BladeName;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return System.Tuple.Create(go, yodo);
        }

        static GachaSystem BuildSystem()
        {
            var go = new GameObject("GachaTestSystem");
            var system = go.AddComponent<GachaSystem>();

            var progress = go.AddComponent<StageProgress>();
            progress.SetProgress(GachaCurve.UnlockStage, 0, 0, GachaCurve.UnlockStage);

            var so = new SerializedObject(system);
            so.FindProperty("stage").objectReferenceValue = progress;
            so.ApplyModifiedPropertiesWithoutUndo();

            return system;
        }

        /** 뽑은 횟수만큼 소환 경험치가 쌓인다 (68단계 확정 3: 1회 = 1 XP) */
        [Test]
        public void Runtime_PullsEarnSummonXp()
        {
            var system = BuildSystem();
            try
            {
                system.DebugPull(GachaCurve.TenPullCount);
                Assert.AreEqual((long)GachaCurve.TenPullCount, system.SummonXp,
                    "10연이 소환 경험치 10을 안 줬다");
                Assert.AreEqual(GachaCurve.TenPullCount, system.TotalPulls);

                var noon = new System.DateTime(2026, 8, 11, 3, 0, 0, System.DateTimeKind.Utc);
                Assert.IsTrue(system.TryFreePullAt(noon));
                Assert.AreEqual(GachaCurve.TenPullCount + 1L, system.SummonXp,
                    "무료 뽑기가 소환 경험치를 안 줬다 - 무과금이 이 축에 닿는 길이 그것이다");
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /** 레벨 하나가 오르면 이벤트가 **정확히 한 번**, 새 레벨로 온다 */
        [Test]
        public void Runtime_LevelUpRaisesOneEvent()
        {
            var system = BuildSystem();
            try
            {
                var heard = new List<int>();
                system.SummonLevelUp += heard.Add;

                system.DebugPushToLevelUp();
                Assert.AreEqual(1, system.SummonLevel, "레벨업 직전인데 이미 올랐다");
                Assert.AreEqual(0, heard.Count);

                system.DebugPull(1);
                Assert.AreEqual(2, system.SummonLevel);
                CollectionAssert.AreEqual(new[] { 2 }, heard, "레벨업 이벤트가 한 번이 아니다");
                Assert.AreEqual(2, system.LastBatchLevelUp, "결과 팝업이 읽을 값이 안 섰다");

                system.DebugPull(1);
                Assert.AreEqual(1, heard.Count, "레벨이 안 올랐는데 이벤트가 또 왔다");
                Assert.AreEqual(0, system.LastBatchLevelUp, "다음 묶음에 지난 레벨업이 남았다");
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /**
         * @brief 무료 뽑기가 하루에 한 번인가. **KST 04:00 경계로.**
         */
        [Test]
        public void Runtime_FreePullIsOncePerQuestDay()
        {
            var system = BuildSystem();
            try
            {
                var noon = new System.DateTime(2026, 8, 11, 3, 0, 0, System.DateTimeKind.Utc);

                Assert.IsTrue(system.HasFreePullAt(noon), "첫 무료 뽑기가 막혀 있다");
                Assert.IsTrue(system.TryFreePullAt(noon));
                Assert.IsFalse(system.HasFreePullAt(noon.AddHours(10)),
                    "같은 퀘스트일에 두 번 뽑힌다");

                // KST 04:00 = UTC 19:00을 넘으면 다음 날이다
                Assert.IsTrue(system.HasFreePullAt(noon.AddHours(17)),
                    "하루가 넘었는데 무료 뽑기가 안 열린다");
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /**
         * @brief 세이브 왕복. **소환 경험치가 살아 돌아오는가.**
         *
         * 저장하지 않으면 껐다 켠 플레이어의 레벨이 0으로 돌아가고, 그것은
         * 성장 축이 아니라 몰수다(SaveData.yodoSummonXp 주석).
         */
        [Test]
        public void Runtime_SaveRoundTripKeepsTheSummonXp()
        {
            var system = BuildSystem();
            try
            {
                system.DebugPull(GachaCurve.TenPullCount);
                system.DebugPushToLevelUp();
                long xp = system.CollectSummonXp();
                int total = system.CollectTotalPulls();
                long day = system.CollectFreePullDay();

                system.DebugReset();
                Assert.AreEqual(0L, system.SummonXp);

                system.Restore(xp, total, day);
                Assert.AreEqual(xp, system.CollectSummonXp(), "소환 경험치가 복원되지 않았다");
                Assert.AreEqual(SummonLevelCurve.LevelFor(xp), system.SummonLevel);
                Assert.AreEqual(total, system.CollectTotalPulls());
                Assert.AreEqual(day, system.CollectFreePullDay());
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }

        /**
         * @brief 사다리가 세이브를 왕복하는가 (47단계 - 세이브 v16).
         *
         * 혼격과 전설 사본은 **플레이어가 지불한 것**이다. 소환 경험치를
         * 저장하는 것과 같은 기준이고(SaveData.yodoSummonXp 주석) 여기는
         * 그보다 세다 - 전설은 200회에 한 번이라 몰수의 크기가 다르다.
         */
        [Test]
        public void Runtime_LadderSurvivesASaveRoundTrip()
        {
            var built = BuildLadderSystem();
            try
            {
                var yodo = built.Item2;

                yodo.DebugGrantSoul(0);
                yodo.DebugForge(0);
                yodo.DebugGrantRarity(0);
                yodo.DebugGrantRarity(0);
                yodo.DebugGrantLegendary(1);
                yodo.DebugGrantLegendary(1);

                var ids = yodo.CollectIds();
                var souls = yodo.CollectSouls();
                var tiers = yodo.CollectTiers();
                var discovered = yodo.CollectDiscovered();
                var rarities = yodo.CollectRarities();
                var legendIds = yodo.CollectLegendaryIds();
                var copies = yodo.CollectLegendaryCopies();
                long shards = yodo.CollectShards();

                Assert.AreEqual(2, rarities[0], "치트가 혼격을 안 올렸다");
                Assert.AreEqual(2, copies[1], "치트가 전설 사본을 안 올렸다");

                yodo.DebugReset();
                Assert.AreEqual(0, yodo.GetBlade(0).rarity);
                Assert.AreEqual(0, yodo.GetLegendary(1).copies);

                yodo.Restore(ids, souls, tiers, discovered, shards, rarities, legendIds, copies);

                Assert.AreEqual(2, yodo.GetBlade(0).rarity, "혼격이 복원되지 않았다");
                Assert.AreEqual(2, yodo.GetLegendary(1).copies, "전설 사본이 복원되지 않았다");
            }
            finally { Object.DestroyImmediate(built.Item1); }
        }

        /**
         * @brief v15 세이브(사다리 없음)를 넘겨도 **44·45단계 값 그대로**인가.
         *
         * 인자 셋이 기본값(null)이라 옛 호출부가 그대로 도는데, 그 사실이
         * 값에서도 참인지를 잰다 - 기본값이 조용히 0이 아닌 무언가로 바뀌면
         * 마이그레이션이 소급을 하게 된다.
         */
        [Test]
        public void Runtime_RestoreWithoutLadderLeavesItEmpty()
        {
            var built = BuildLadderSystem();
            try
            {
                var yodo = built.Item2;
                yodo.DebugGrantRarity(0);
                yodo.DebugGrantLegendary(0);

                var ids = yodo.CollectIds();
                var tiers = yodo.CollectTiers();

                // v15의 호출 모양 - 뒤의 셋을 안 넘긴다
                yodo.Restore(ids, yodo.CollectSouls(), tiers, yodo.CollectDiscovered(), 0L);

                Assert.AreEqual(1, yodo.GetBlade(0).rarity,
                    "혼격을 안 넘겼는데 값이 움직였다 - 복원이 소급하고 있다");
                Assert.AreEqual(1, yodo.GetLegendary(0).copies,
                    "전설을 안 넘겼는데 값이 움직였다");
            }
            finally { Object.DestroyImmediate(built.Item1); }
        }

        // ------------------------------------------------ 47단계: 밸런스

        /**
         * @brief 사다리 둘을 합쳐서 **진행을 움직이는가.** 프로젝트 기준 4%.
         *
         * ## 왜 합쳐서 재는가 - 하나씩 재면 둘 다 4% 아래다
         *
         * 실측: ★4만 빼면 +2.46%, ★5만 빼면 +0.48%, 둘 다 빼면 **+4.66%**다.
         * 20단계가 "안 사면 손해인가"와 "넣은 것이 이득인가"를 나눈 그 자리
         * 이고, 여기서는 **한 스텝이 얹은 층 전체**가 단위다 - 46단계가
         * 촉매와 뽑기를 "둘 다 없는 세계" 기준으로 다시 잰 것과 같은 처리다.
         *
         * 개별 값이 낮은 이유는 크기가 아니라 **자**에 있다. 가속 플레이어의
         * 보스 여유는 이미 17배라 DPS를 더해도 시간이 거의 안 줄고, 잡몹
         * 파밍은 공급 하한(SpawnPacing 0.4초)에 묶여 있다. 그래서 같은 축을
         * **도달층**으로 재면 크기가 제대로 보인다 -
         * Ladder_MovesTheReachedStage가 그 자다.
         */
        [Test]
        public void Ladder_IsNotADeadButton()
        {
            var field = Field();
            var with = StageSimulation.Run(400, field);
            var without = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipRarity = true, SkipLegendary = true });

            double gain = TotalSeconds(without, 51, 400) / TotalSeconds(with, 51, 400) - 1d;

            Assert.Greater(gain, 0.04d, string.Format(
                "희귀도 사다리의 이득이 {0:P1}뿐이다 - 20단계 골드 축의 함정이 "
                + "재현되고 있다. 혼격 배수(YodoRarityCurve.RarityStep {1})나 "
                + "전설 배수(LegendaryYodoCurve.GrantStep {2})를 올려라",
                gain, YodoRarityCurve.RarityStep, LegendaryYodoCurve.GrantStep));
        }

        /**
         * @brief **도달층** - 같은 시간에 더 멀리 가는가.
         *
         * 여유(BossMargin)가 아니라 진짜 진행을 재는 자다. 심층에서 여유는
         * 이미 20배라 DPS 한 겹이 여유를 30% 밀어도 시간은 3%밖에 안 준다 -
         * 그 두 숫자를 같은 자로 읽으면 "밴드는 뚫리는데 축은 죽었다"는
         * 모순이 나온다. 층으로 세면 그 모순이 풀린다.
         */
        [Test]
        public void Ladder_MovesTheReachedStage()
        {
            var field = Field();
            var with = StageSimulation.Run(600, field);
            var without = StageSimulation.Run(600, field,
                new StageSimulation.Policy { SkipRarity = true, SkipLegendary = true });

            // 52단계에 걷기가 StageSimulation.ReachedStage로 승격됐다 - 이
            // 검사가 처음 세운 자가 이제 밴드 계약의 자이기도 하다
            double budget = StageSimulation.CombatSeconds(with, 51, 400);
            int reached = StageSimulation.ReachedStage(without, budget, 51);

            Assert.Less(reached, 390, string.Format(
                "사다리를 빼도 같은 시간에 st{0}까지 간다 - st400과 차이가 없으면 "
                + "이 스텝이 판 것이 화면에서 아무 일도 안 한다", reached));
        }

        /**
         * @brief 사다리가 **뽑기의 보석당 가치**를 올리는가. R-4의 핵심 수치다.
         *
         * ## 46단계가 남긴 구멍
         *
         * 뽑기의 보석당 파편은 촉매의 66%다(Catalyst_BeatsGachaPerGemOnShards -
         * 그 부등호는 47단계에도 그대로다). 46단계는 "혼 정수가 그 부등호를
         * 뒤집는다"로 넘겼는데, 혼 정수는 리드 상한에 묶여 재고가 얇아서
         * 상한에 닿은 뒤로는 뽑기가 촉매보다 순수하게 나쁜 버튼이었다.
         *
         * 사다리가 그 자리를 메운다. 같은 보석으로 **더 많은 시간을 산다**는
         * 것이 이 검사이고, 그것이 "파편만 보면 촉매가 낫지만 그래도 뽑기를
         * 돈다"의 수치다.
         */
        [Test]
        public void Ladder_MakesGachaWorthMorePerGem()
        {
            var field = Field();
            const int from = 100, to = 400;

            var none = StageSimulation.Run(to, field,
                new StageSimulation.Policy { SkipShardPacks = true, SkipGacha = true });
            var flat = StageSimulation.Run(to, field,
                new StageSimulation.Policy { SkipShardPacks = true,
                                             SkipRarity = true, SkipLegendary = true });
            var ladder = StageSimulation.Run(to, field,
                new StageSimulation.Policy { SkipShardPacks = true });

            double baseline = TotalSeconds(none, from, to);
            int baseGems = none[to - 1].GemsSpent;

            double flatValue = (baseline - TotalSeconds(flat, from, to))
                             / System.Math.Max(1, flat[to - 1].GemsSpent - baseGems);
            double ladderValue = (baseline - TotalSeconds(ladder, from, to))
                               / System.Math.Max(1, ladder[to - 1].GemsSpent - baseGems);

            Assert.Greater(ladderValue, flatValue * 1.3d, string.Format(
                "사다리를 얹어도 뽑기의 보석당 가치가 {0:F4} -> {1:F4}뿐이다 - "
                + "촉매(확정·저렴)를 두고 뽑기를 돌 이유가 수치로 안 선다",
                flatValue, ladderValue));
        }

        /**
         * @brief f2p 바닥이 사다리 때문에 **한 비트도** 안 움직이는가.
         *
         * 46단계의 FreeToPlayFloor_IsUntouchedByGacha와 같은 자리, 한 겹 더
         * 강한 주장이다. 저쪽은 "무과금이 뽑기에 보석을 안 쓴다"는 정책이
         * 지켰고, 이쪽은 그 위에 **자연 출처가 아예 없다**는 사실이 더해진다 -
         * 무료 뽑기로 ★4를 받아도 그것은 시뮬레이션 밖의 트리클이다.
         */
        [Test]
        public void FreeToPlayFloor_IsUntouchedByTheLadder()
        {
            var field = Field();
            var with = StageSimulation.Run(300, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true });
            var without = StageSimulation.Run(300, field,
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true,
                                             SkipRarity = true, SkipLegendary = true });

            for (int i = 0; i < 300; i++)
            {
                Assert.AreEqual(without[i].BossMargin, with[i].BossMargin, 0d,
                    "stage " + (i + 1) + ": 희귀도 사다리가 무과금의 바닥을 움직였다");

                foreach (int rarity in with[i].YodoRarities)
                    Assert.AreEqual(0, rarity,
                        "stage " + (i + 1) + ": 무과금이 혼격을 얻었다 - 자연 출처가 생겼다");
                foreach (int copies in with[i].LegendaryCopies)
                    Assert.AreEqual(0, copies,
                        "stage " + (i + 1) + ": 무과금이 전설을 얻었다");
            }
        }

        /**
         * @brief 시뮬레이션 칸 수가 카탈로그와 맞는가.
         *
         * Levels가 struct라 배열을 못 두고 낱개 필드로 펼친다 - 전설이
         * 셋째가 되는 날 칸을 손으로 늘려야 하고, 잊으면 조용히 그 자루만
         * 시뮬레이션에서 빠진다. YodoTests.Simulation_HasASlotForEveryBlade와
         * 같은 자리, 같은 이유다.
         */
        [Test]
        public void Simulation_HasASlotForEveryLegendary()
        {
            Assert.AreEqual(LegendaryYodoCatalog.Count, StageSimulation.LegendarySlotCapacity,
                "전설 카탈로그와 시뮬레이션 칸 수가 갈렸다");
        }

        /**
         * @brief 손상된 세이브의 음수는 0이 되고, **큰 값은 그대로 들어온다.**
         *
         * 47단계에는 천장 카운터를 [0, 29]로 잘랐다 - 넘치면 다음 한 번이 무조건
         * 보장이 됐기 때문이다. 소환 경험치에는 위쪽 상한이 없다(SummonLevelCurve
         * 머리 주석) - 아무리 커도 레벨이 높을 뿐이고, 자르면 그것이 하드캡이다.
         */
        [Test]
        public void Runtime_RestoreRejectsOnlyNegatives()
        {
            var system = BuildSystem();
            try
            {
                system.Restore(-5L, -5, -1L);
                Assert.AreEqual(0L, system.SummonXp);
                Assert.AreEqual(0, system.CollectTotalPulls());
                Assert.AreEqual(0L, system.CollectFreePullDay());

                system.Restore(1000000L, 0, 0L);
                Assert.AreEqual(1000000L, system.SummonXp, "큰 소환 경험치가 잘렸다 - 그것이 상한이다");
            }
            finally { Object.DestroyImmediate(system.gameObject); }
        }
    }
}
