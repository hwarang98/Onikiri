using System;
using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 45단계 - 혼별 오의 상성과 영체 소환.
     *
     * 두 축 다 **새 재화가 없다.** 세기를 정하는 것이 요도 티어 하나이므로,
     * 44단계처럼 "언제 무엇이 떨어지는가"를 다시 검사할 필요가 없다 - 그쪽은
     * YodoTests가 이미 지킨다. 여기서 지키는 것은 셋이다:
     *
     *   매핑    넷 대 셋의 불일치가 표에서 실제로 해소돼 있는가
     *   층      두 축이 44단계 위에 **얹혔는가**, 섞이지 않았는가
     *   밴드    얹은 대가로 밴드가 어디까지 움직였는가
     */
    public class YodoPowerTests
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
            for (int i = from - 1; i < to && i < rows.Count; i++)
                total += rows[i].MobSeconds + StageSimulation.BossIntroSeconds
                       + StageSimulation.BossWalkInSeconds + rows[i].BossKillSeconds;
            return total;
        }

        static int[] Tiers(int t0, int t1, int t2, int t3)
        {
            return new[] { t0, t1, t2, t3 };
        }

        // ---------------------------------------------------------------- 매핑

        /**
         * @brief 넷 대 셋의 불일치가 실제로 해소돼 있는가.
         *
         * 세 가지를 함께 본다. 하나라도 깨지면 이 축이 만들려던 것(빌드
         * 다양성)이 사라진다:
         *
         *   모든 오의가 적어도 한 혼에 물려 있다 - 아니면 그 오의는 상성이
         *   없는 오의가 되고, 그 자루를 고를 이유가 세 개가 아니라 두 개다
         *
         *   전 오의 혼은 **정확히 하나** - 둘이면 흑야가 두 배가 되고, 그
         *   순간 몰아주기의 답이 하나로 정해진다
         *
         *   id가 실재한다 - 오타 하나가 "이 자루는 아무것도 강화하지 않는다"로
         *   조용히 나타난다
         */
        [Test]
        public void Affinity_ResolvesTheFourVersusThreeMismatch()
        {
            var covered = new bool[SkillCatalog.Count];
            int broad = 0;

            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                string id = YodoCatalog.Blades[i].AffinitySkillId;

                if (string.IsNullOrEmpty(id))
                {
                    broad++;
                    Assert.IsTrue(YodoAffinityCurve.IsBroad(i),
                        YodoCatalog.Blades[i].BladeName + ": 빈 id인데 전 오의로 안 읽힌다");
                    for (int s = 0; s < covered.Length; s++) covered[s] = true;
                    continue;
                }

                int index = SkillCatalog.IndexOf(id);
                Assert.GreaterOrEqual(index, 0, YodoCatalog.Blades[i].BladeName
                    + "의 상성 오의 '" + id + "'가 오의 표에 없다");
                covered[index] = true;
            }

            Assert.AreEqual(1, broad,
                "전 오의를 미는 혼이 하나가 아니다 - 넷 대 셋을 그렇게 풀기로 한 것이 "
                + "이 스텝의 매핑이다(YodoSpec.AffinitySkillId 주석)");

            for (int s = 0; s < covered.Length; s++)
                Assert.IsTrue(covered[s], SkillCatalog.Skills[s].DisplayName
                    + "을 강화하는 혼이 없다 - 그 오의만 빌드에서 빠진다");
        }

        /** 영체 이름이 넷 다 있다. 소환 순간 화면 한가운데 뜬다 */
        [Test]
        public void EverySoulHasASpiritName()
        {
            var names = new HashSet<string>();
            foreach (var blade in YodoCatalog.Blades)
            {
                Assert.IsFalse(string.IsNullOrEmpty(blade.SpiritName),
                    blade.BladeName + "의 영체 이름이 비었다");
                Assert.IsTrue(names.Add(blade.SpiritName),
                    "영체 이름이 겹친다: " + blade.SpiritName);
                Assert.AreNotEqual(blade.SoulName, blade.SpiritName,
                    "영체 이름이 혼 이름과 같다 - 재료가 걸어 나온 것으로 읽힌다");
            }
        }

        /**
         * @brief 전 오의 혼이 전담 혼보다 **약한가.**
         *
         * 셋에 걸리므로 같은 세기면 흑야 한 자루가 나머지 셋을 합친 것보다
         * 세진다. 그러면 몰아주기의 답이 언제나 흑야이고, 빌드는 다시 하나다.
         *
         * 재는 것은 배수가 아니라 **DPS 기여**다 - 오의마다 몫이 다르므로
         * (귀참 0.32 / 일섬 0.25 / 연참 0.18) 배수만 비교하면 답이 갈린다.
         */
        [Test]
        public void BroadAffinity_IsWeakerThanADedicatedOne()
        {
            int broad = -1, dedicated = -1;
            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                if (YodoAffinityCurve.IsBroad(i)) broad = i;
                else if (dedicated < 0) dedicated = i;
            }

            Assert.GreaterOrEqual(broad, 0);
            Assert.GreaterOrEqual(dedicated, 0);

            var onlyBroad = new int[YodoCatalog.Count];
            var onlyDedicated = new int[YodoCatalog.Count];
            onlyBroad[broad] = YodoCurve.MaxTier;
            onlyDedicated[dedicated] = YodoCurve.MaxTier;

            Assert.Less(YodoAffinityCurve.DpsFactor(onlyBroad),
                        YodoAffinityCurve.DpsFactor(onlyDedicated),
                "전 오의 혼이 전담 혼보다 세다 - 몰아주기의 답이 하나로 정해진다");
        }

        // ---------------------------------------------------------------- 층

        /**
         * @brief 조율 구간(st1~50)에 두 축도 그 보정도 **존재하지 않는다.**
         *
         * 44단계가 티어 배수에서 구조로 얻은 불변을 그대로 물려받는다 -
         * 봉인된 요도가 없으면 상성은 1이고 영체 배율은 0이다. 계수가 아니라
         * 구조가 지키는 것이 요점이라, 값을 바꿔도 이 검사는 안 깨져야 한다.
         */
        [Test]
        public void YodoPower_IsAbsentFromTheTunedBands()
        {
            for (int stage = 1; stage <= 50; stage++)
            {
                Assert.AreEqual(1d, YodoCurve.ExpectedPowerFactorAtStage(stage), 1e-12d,
                    "stage " + stage + ": 조율 구간에 상성·영체가 있다");
                Assert.AreEqual(1d, StageCurve.YodoPowerCompensation(stage), 1e-12d,
                    "stage " + stage + ": 조율 구간에 상성·영체 보정이 걸린다 - "
                    + "21단계 골드 축의 사고(축은 없는데 보정만)가 재현된다");
            }

            Assert.Greater(YodoCurve.ExpectedPowerFactorAtStage(51), 1d,
                "st51에 상성·영체가 없다 - st50 피날레의 혼이 안 들어왔다");
        }

        /** 미봉인 요도는 상성 1, 영체 0. 두 "없음"의 숫자가 다르다 */
        [Test]
        public void UnsealedBlade_HasNeitherAffinityNorSpirit()
        {
            var none = new int[YodoCatalog.Count];

            for (int s = 0; s < SkillCatalog.Count; s++)
                Assert.AreEqual(1d, YodoAffinityCurve.FactorForSkill(s, none), 1e-12d);

            Assert.AreEqual(0d, YodoSpiritCurve.MultiplierAtTier(0), 1e-12d,
                "미봉인 요도의 영체가 0이 아니다 - 유령 기여가 생긴다");
            Assert.AreEqual(0d, YodoSpiritCurve.RateFor(none), 1e-12d);
            Assert.AreEqual(-1, YodoSpiritCurve.BladeForTurn(0, none),
                "봉인한 요도가 없는데 순번이 나온다 - 아무도 안 나오는 소환 턴이 생긴다");

            // 상한 위는 값에서 자른다. 티어는 안 자른다(YodoCurve.TierValue 규칙)
            Assert.AreEqual(YodoAffinityCurve.ValueAt(0, YodoCurve.MaxTier),
                            YodoAffinityCurve.ValueAt(0, YodoCurve.MaxTier + 5), 1e-12d);
            Assert.AreEqual(YodoSpiritCurve.MultiplierAtTier(YodoCurve.MaxTier),
                            YodoSpiritCurve.MultiplierAtTier(YodoCurve.MaxTier + 5), 1e-12d);
        }

        /** 다타로 나눠도 총량이 **정확히** 같다. 오의의 HitDamageShare와 같은 규칙 */
        [Test]
        public void SpiritHits_SumToExactlyTheTotal()
        {
            double total = YodoSpiritCurve.MultiplierAtTier(7);

            double sum = 0d;
            for (int i = 0; i < YodoSpiritCurve.SummonHits; i++)
                sum += YodoSpiritCurve.HitShare(i, total);

            Assert.AreEqual(total, sum, 1e-12d,
                "영체의 타격 합이 총 배율과 다르다 - '총량 불변'을 검사할 수 없게 된다");
        }

        /**
         * @brief 로테이션이 **봉인한 자루만** 한 바퀴에 한 번씩 돈다.
         *
         * 미봉인을 순번에 포함하면 아무도 안 나오는 턴이 생기고, 화면에서
         * 그것은 쿨다운 버그로 읽힌다.
         */
        [Test]
        public void SpiritRotation_VisitsEverySealedBladeOnce()
        {
            var tiers = Tiers(3, 0, 5, 2);   // 둘째만 미봉인

            var seen = new List<int>();
            for (int turn = 0; turn < 3; turn++)
                seen.Add(YodoSpiritCurve.BladeForTurn(turn, tiers));

            CollectionAssert.AreEquivalent(new[] { 0, 2, 3 }, seen,
                "로테이션이 봉인한 셋을 한 바퀴에 한 번씩 돌지 않는다");

            Assert.AreEqual(YodoSpiritCurve.BladeForTurn(0, tiers),
                            YodoSpiritCurve.BladeForTurn(3, tiers),
                "한 바퀴 뒤에 처음으로 돌아오지 않는다");
        }

        /**
         * @brief 몰아주기와 고르기가 **서로 반대 방향을 가리키는가.**
         *
         * 이 스텝이 만든 선택 그 자체다. 상성은 한 자루에 몰수록 세지고
         * (한 오의에 곱이 몰린다), 영체는 고를수록 세다(로테이션 평균).
         * 둘 중 하나라도 방향이 같아지면 정답이 하나로 정해진다.
         */
        [Test]
        public void FocusAndSpread_PullInOppositeDirections()
        {
            // 같은 티어 총합 16을 두 모양으로 나눈다
            var focused = Tiers(10, 2, 2, 2);
            var spread = Tiers(4, 4, 4, 4);

            int skill = SkillCatalog.IndexOf(YodoCatalog.Blades[0].AffinitySkillId);
            Assert.GreaterOrEqual(skill, 0);

            Assert.Greater(YodoAffinityCurve.FactorForSkill(skill, focused),
                           YodoAffinityCurve.FactorForSkill(skill, spread),
                "몰아줘도 그 오의의 상성이 안 세진다 - 빌드가 성립하지 않는다");

            Assert.Greater(YodoSpiritCurve.RateFor(spread),
                           YodoSpiritCurve.RateFor(focused),
                "고르게 올려도 영체가 안 세진다 - 두 축이 같은 방향을 가리키면 "
                + "선택이 아니라 정답이 된다");
        }

        /**
         * @brief 영체는 괄호 **안**, 펫은 괄호 **밖**. 결의 차이가 식에 있다.
         *
         * 같은 크기의 기여를 두 자리에 넣어 비교한다. 영체 쪽만 치명타를
         * 상속하므로 치명타가 클수록 둘이 벌어지고, 그것이 "일시 버스트는
         * 오의와 같은 종류의 사건"이라는 설계가 숫자로 참인 자리다.
         */
        [Test]
        public void Spirit_InheritsCritButPetDoesNot()
        {
            var basis = new CombatStats
            {
                Damage = 100d, AttacksPerSecond = 4d, SkillRate = 2d,
                CritRate = 0.6d, CritMultiplier = 3d
            };

            var withSpirit = basis; withSpirit.SpiritRate = 1d;

            // 같은 몫을 펫으로 준다 - 괄호 안 1.0은 (4+2) 대비 1/6
            var withPet = basis; withPet.PetBonus = 1d / 6d;

            Assert.Greater(withSpirit.ExpectedDps, withPet.ExpectedDps,
                "영체가 치명타를 상속하지 않는다 - 괄호 밖에 붙은 것이다");

            // 기본값 0이면 두 축이 없던 시절의 값 그대로여야 한다
            Assert.AreEqual(basis.ExpectedDps, new CombatStats
            {
                Damage = 100d, AttacksPerSecond = 4d, SkillRate = 2d,
                CritRate = 0.6d, CritMultiplier = 3d
            }.ExpectedDps, 1e-9d);
        }

        // ---------------------------------------------------------------- 시뮬레이션

        [Test]
        public void Simulation_HasAnAffinitySlotForEverySkill()
        {
            var rows = StageSimulation.Run(60, Field());
            Assert.GreaterOrEqual(rows[59].SkillAffinity.Length, SkillCatalog.Count,
                "표의 상성 칸이 오의 수보다 적다 - 뒤쪽 오의의 상성이 조용히 사라진다");
        }

        /**
         * @brief 심층 구간에서 오의 몫이 **상수인가.** 기대 곡선의 근거다.
         *
         * 상성·영체의 기대 곡선(YodoCurve.ExpectedPowerFactorAtStage)은 공격속도와
         * 오의 배율이 둘 다 상한이라고 가정하고 닫힌 식을 쓴다. 그 가정이
         * 실제로 참인지를 여기서 잰다 - 44단계 세계(NeutralizeYodoPower)에서
         * 재는 이유는 상성이 끼면 그 값이 상한 그대로가 아니기 때문이다.
         *
         * 깨지면 기대 곡선이 근사가 되고, 보정이 실제를 못 따라간다.
         */
        /**
         * @brief 오의 배율이 실제로 상한에 닿는 스테이지.
         *
         * ## 49단계에 65로 밀렸다가 49b에 51로 돌아왔다
         *
         * 49단계는 신규 오의를 전부 st51에 걸었다. 그러면 4번 자리가 열리는
         * 그 순간 자리에 들어오는 것이 **레벨 1짜리 오의**이고, 첫 비용이
         * st51의 골드 규모(16.1T)라 상한까지 열네 스테이지가 걸렸다.
         *
         * 49b가 해금을 코리더로 앞당기면서(st12/18/27) 첫 비용도 그 스테이지의
         * 규모(1.2K)가 됐다 - st51에서는 끝전이라 자리가 열리는 순간 이미
         * 상한이다(하네스 실측 st51 rate 2.976 = 상한).
         *
         * 그래서 심층 전 구간에서 몫이 다시 상수이고, 기대 곡선의 닫힌 식
         * 가정이 첫 칸부터 참이다. 예외 구간이 사라진 것이 49b의 배당금이다.
         */
        const int SkillCapStage = 51;

        [Test]
        public void SkillShare_IsFlatAcrossTheDeepZone()
        {
            var rows = StageSimulation.Run(200, Field(),
                new StageSimulation.Policy { NeutralizeYodoPower = true, SkipGacha = true });

            for (int stage = SkillCapStage; stage <= 200; stage++)
            {
                var row = rows[stage - 1];

                Assert.AreEqual(YodoAffinityCurve.CappedSkillRate, row.SkillRate, 1e-9d,
                    "stage " + stage + ": 오의 배율이 상한이 아니다 - 기대 곡선의 "
                    + "상수 몫 가정이 깨진다");
                Assert.AreEqual(YodoAffinityCurve.CappedAttackRate, row.AttacksPerSecond, 1e-6d,
                    "stage " + stage + ": 공격속도가 상한이 아니다 - 같은 가정이 깨진다");
            }
        }

        /**
         * @brief 상성이 **자동 공격을 뒤집지 않는가** (계약 구간).
         *
         * SkillAxisTests.SkillShare_StaysBelowHalfThrough30을 심층으로 늘린
         * 것이다. 저쪽은 st30까지만 보는데, 상성은 st51부터 존재하므로 그
         * 검사가 닿지 않는 구간에서 몫을 밀어 올린다 - 실제로 st200의 몫이
         * 0.34에서 0.41로 올랐다.
         *
         * 계약 구간(st200)까지만 검사한다. 그 밖은 52단계가 실측으로 답을
         * 냈다 - **50%는 꼬리에서 지켜지지 않고, 지켜질 수도 없다.** st500
         * 기준 플레이어 몫이 0.646인데, 그것을 미는 것은 과금 층이 아니라
         * **무과금도 그대로 받는 티어 성장**이다(무과금 몫이 st290 언저리에서
         * 0.5를 넘고 st500에 0.569다). 꼬리에서 이 계약을 강제하려면 44·45
         * 단계의 티어 곡선 자체를 걷어내야 하고, 그것은 무과금의 유일한
         * 후반 축을 죽인다. 그래서 꼬리의 계약은 50%가 아니라 **유한 상한**
         * 이다 - 아래 TailShare_StaysBounded가 그 자다.
         */
        [Test]
        public void Affinity_DoesNotFlipTheAutoAttackWithinTheContract()
        {
            var rows = StageSimulation.Run(200, Field());

            for (int stage = 51; stage <= 200; stage++)
                Assert.Less(rows[stage - 1].SkillDpsShare, 0.5d, string.Format(
                    "st{0}에서 오의가 DPS의 {1:P0}를 맡는다 - 상성이 자동 공격을 "
                    + "장식으로 만들고 있다 (rate {2:F2}, 공격속도 {3:F2})",
                    stage, rows[stage - 1].SkillDpsShare, rows[stage - 1].SkillRate,
                    rows[stage - 1].AttacksPerSecond));
        }

        /**
         * @brief 꼬리(st201~500)의 오의 몫 계약 - **유한 상한** (52단계).
         *
         * 위 검사의 주석이 근거다. 50% 대신 상한 0.68을 계약으로 둔다:
         * 실측 최대 0.646(기준 플레이어, st441)에 5% 헤드룸이고, 자동
         * 공격이 어느 층에서도 DPS의 3분의 1 아래로 내려가지 않는다는
         * 보증이다. 미는 축이 전부 하드캡이라(티어 10 · 혼격 4 · 전설 4)
         * 이 상한은 재기준이 필요 없는 종류다 - 뚫린다면 그것은 성장이
         * 아니라 상한 없는 새 괄호 안 축이 생긴 것이다.
         */
        [Test]
        public void TailShare_StaysBounded()
        {
            var policies = new[]
            {
                StageSimulation.Policy.Default,
                new StageSimulation.Policy { GemsFromQuestsOnly = true }
            };

            foreach (var policy in policies)
            {
                var rows = StageSimulation.Run(StageSimulation.ReachContractTo, Field(), policy);

                for (int stage = 201; stage <= StageSimulation.ReachContractTo; stage++)
                    Assert.Less(rows[stage - 1].SkillDpsShare, 0.68d, string.Format(
                        "st{0}에서 오의 몫이 {1:P1}다 - 꼬리의 유한 상한(68%)을 넘었다. "
                        + "상한 없는 괄호 안 축이 생겼다는 뜻이다",
                        stage, rows[stage - 1].SkillDpsShare));
            }
        }

        /**
         * @brief 꼬리의 성장 축이 실제로 **포화하는가.** 유한 상한의 증인.
         *
         * st900의 기준 플레이어는 티어·혼격·전설이 전부 하드캡에 서 있어야
         * 한다. 그래야 위의 0.68과 도달층 리드 압축이 "아직 안 자란 것"이
         * 아니라 "**다 자란 것**"에서 잰 값이 된다 - 포화 전에 재면 다음
         * 스텝에서 조용히 자라 뚫는다.
         */
        [Test]
        public void TailGrowth_ActuallySaturates()
        {
            var rows = StageSimulation.Run(900, Field());
            var last = rows[899];

            for (int i = 0; i < last.YodoTiers.Length; i++)
                Assert.AreEqual(YodoCurve.MaxTier, last.YodoTiers[i],
                    "st900에서 " + i + "번 요도의 티어가 상한이 아니다 - 포화 가정이 깨진다");

            for (int i = 0; i < last.YodoRarities.Length; i++)
                Assert.AreEqual(YodoRarityCurve.MaxRarity, last.YodoRarities[i],
                    "st900에서 " + i + "번 요도의 혼격이 상한이 아니다");

            // 몫도 포화한다 - st700과 st900이 1%p 안이면 더 자랄 것이 없다
            Assert.AreEqual(rows[699].SkillDpsShare, rows[899].SkillDpsShare, 0.01d,
                "st700과 st900의 오의 몫이 갈린다 - 아직 자라는 축이 남아 있다");
        }

        /**
         * @brief 기대 곡선이 무과금 경로를 따라가는가.
         *
         * 44단계의 ExpectedCurve_TracksTheSimulation과 짝이다. 저쪽은 공격력
         * 배수를, 이쪽은 상성·영체가 DPS에 곱하는 배수를 대조한다 - 둘이
         * 갈리면 무과금이 없는 이득을 상쇄당한다.
         */
        [Test]
        public void ExpectedPowerCurve_TracksTheSimulation()
        {
            var rows = StageSimulation.Run(300, Field(),
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true });

            for (int stage = YodoCurve.UnlockStage; stage < 300; stage++)
            {
                // 표의 한 줄은 그 스테이지를 **끝낸 뒤**의 상태다
                Assert.AreEqual(YodoCurve.ExpectedPowerFactorAtStage(stage + 1),
                                rows[stage - 1].YodoPowerFactor, 1e-9d,
                    "stage " + stage + ": 상성·영체의 기대 곡선이 실측과 갈렸다");
            }
        }

        /**
         * @brief 상성을 무력화하면 **오의가 44단계 값으로 원복되는가.**
         *
         * 지시가 요구한 죽은 버튼 검사의 앞쪽이다. 상성이 다른 경로로도
         * 새고 있으면 여기서 걸린다 - 예를 들어 오의 배율 상한 안쪽에
         * 곱해졌다면 이 값이 안 돌아온다.
         */
        [Test]
        public void AffinityNeutralized_RestoresTheOugiMultiplier()
        {
            var rows = StageSimulation.Run(200, Field(),
                new StageSimulation.Policy { SkipAffinity = true });

            for (int stage = SkillCapStage; stage <= 200; stage++)
            {
                var row = rows[stage - 1];

                foreach (double factor in row.SkillAffinity)
                    Assert.AreEqual(1d, factor, 1e-12d,
                        "stage " + stage + ": 상성을 껐는데 배수가 1이 아니다");

                Assert.AreEqual(YodoAffinityCurve.CappedSkillRate, row.SkillRate, 1e-9d,
                    "stage " + stage + ": 상성을 껐는데 오의 초당환산이 44단계 값과 다르다");
            }

            // 반대쪽 - 켜면 실제로 움직인다
            var with = StageSimulation.Run(200, Field());
            Assert.Greater(with[199].SkillRate, rows[199].SkillRate,
                "상성을 켜도 오의가 안 세진다");
        }

        /** 영체를 무력화하면 초당 환산이 정확히 0이다 */
        [Test]
        public void SpiritNeutralized_ContributesNothing()
        {
            var rows = StageSimulation.Run(200, Field(),
                new StageSimulation.Policy { SkipSpirit = true });

            for (int stage = 51; stage <= 200; stage++)
                Assert.AreEqual(0d, rows[stage - 1].SpiritRate, 1e-12d,
                    "stage " + stage + ": 영체를 껐는데 기여가 남아 있다");

            var with = StageSimulation.Run(200, Field());
            Assert.Greater(with[199].SpiritRate, 0d, "영체가 아무 기여도 안 한다");
        }

        // ---------------------------------------------------------------- 죽은 버튼

        /**
         * @brief 상성이 실제 진행을 움직이는가.
         *
         * 보정은 걷어내지 않는다 - 스테이지의 함수라 플레이어를 구분하지
         * 못하고, 그래서 이 비교군이 재는 것은 "이 축이 없으면 손해인가"다.
         * 프로젝트 기준은 4%다.
         *
         * ## 47단계에 **기준 세계를 뽑기 앞으로 옮겼다** - 값이 아니라 자다
         *
         * 45단계에 +4.8%였던 값이 47단계에 +3.92%로 내려갔다. 상성은 한
         * 계수도 안 움직였다 - **분모가 커진 것**이다. 46단계의 뽑기와
         * 47단계의 사다리가 기준 세계를 빠르게 만들었고, 같은 절약 시간이
         * 더 짧아진 총 시간에 나뉘면 비율이 내려간다.
         *
         * 46단계가 촉매에서 정확히 같은 것을 겪었다(SkipShardPacks가 +4.7%
         * 에서 0.0%로) - "촉매가 죽은 것이 아니라 이 자로는 잴 수 없게 된
         * 것"이고, 그때의 처방은 **둘 다 없는 세계를 기준으로 각자를 재는**
         * 것이었다. 여기서도 같다: 상성이 이 게임의 마지막 층이던 세계
         * (SkipGacha = 45단계)를 기준으로 잰다.
         *
         * 지금 세계에서의 값도 함께 못 박는다. 자를 바꾸는 것은 값을 봐주는
         * 일이 아니므로, **두 세계에서 다 살아 있어야** 통과다.
         */
        [Test]
        public void Affinity_IsNotADeadButton()
        {
            var field = Field();

            var era = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipGacha = true });
            var eraWithout = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipGacha = true, SkipAffinity = true });

            double gain = TotalSeconds(eraWithout, 51, 400) / TotalSeconds(era, 51, 400) - 1d;

            Assert.Greater(gain, 0.04d, string.Format(
                "45단계 세계에서 상성의 이득이 {0:P1}뿐이다 - 20단계 골드 축의 함정이 "
                + "재현되고 있다. AffinityTierStep({1})을 올리거나 "
                + "YodoPowerMarginExponent({2})를 낮춰라",
                gain, YodoAffinityCurve.AffinityTierStep, StageCurve.YodoPowerMarginExponent));

            var now = StageSimulation.Run(400, field);
            var nowWithout = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipAffinity = true });

            double today = TotalSeconds(nowWithout, 51, 400) / TotalSeconds(now, 51, 400) - 1d;

            Assert.Greater(today, 0.03d, string.Format(
                "지금 세계에서 상성의 이득이 {0:P1}까지 닳았다 - 자를 바꿔 봐주는 것이 "
                + "아니라 축이 실제로 묻히고 있다", today));
        }

        /**
         * @brief 영체 소환이 실제 진행을 움직이는가. **버스트가 죽은 연출인가.**
         *
         * 화면에 큰 것이 나타나는데 진행이 안 변하면 그것은 소환이 아니라
         * 장식이다 - 이 스텝이 "정체성 킬러 기능"이라고 부른 것의 최소 조건이
         * 이 한 줄이다.
         *
         * 기준 세계를 45단계(SkipGacha)로 옮긴 이유는 상성 쪽과 같다 -
         * Affinity_IsNotADeadButton 주석에 있다.
         */
        [Test]
        public void SpiritSummon_IsNotADeadButton()
        {
            var field = Field();

            // 49단계: 여기도 슬롯을 셋으로 되돌린다. 46단계가 촉매에서 정한
            // 처방 그대로다 - **그 축이 마지막 층이던 세계**를 기준으로 재야
            // 한다. 4번 슬롯을 남기면 분모(총 DPS)가 커져 영체의 이득이
            // 5.31%에서 4.00%로 내려앉고, 그것은 영체가 약해진 것이 아니라
            // 재는 자가 바뀐 것이다
            var era = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipGacha = true, SkipSkillSlot = true });
            var eraWithout = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipGacha = true, SkipSkillSlot = true,
                                             SkipSpirit = true });

            double gain = TotalSeconds(eraWithout, 51, 400) / TotalSeconds(era, 51, 400) - 1d;

            Assert.Greater(gain, 0.04d, string.Format(
                "45단계 세계에서 영체의 이득이 {0:P1}뿐이다 - 버스트가 죽은 연출이다. "
                + "SpiritCurve.BaseMultiplier({1})를 올리거나 쿨다운({2}초)을 줄여라",
                gain, YodoSpiritCurve.BaseMultiplier, YodoSpiritCurve.CooldownSeconds));

            var now = StageSimulation.Run(400, field);
            var nowWithout = StageSimulation.Run(400, field,
                new StageSimulation.Policy { SkipSpirit = true });

            double today = TotalSeconds(nowWithout, 51, 400) / TotalSeconds(now, 51, 400) - 1d;

            Assert.Greater(today, 0.025d, string.Format(
                "지금 세계에서 영체의 이득이 {0:P1}까지 닳았다 - 자를 바꿔 봐주는 것이 "
                + "아니라 축이 실제로 묻히고 있다", today));
        }

        /** 두 축을 얹은 것 자체가 이득이었는가. 비교군의 다른 쪽 */
        [Test]
        public void YodoPower_IsWorthAddingAtAll()
        {
            var field = Field();
            var with = StageSimulation.Run(400, field);
            var before = StageSimulation.Run(400, field,
                new StageSimulation.Policy { NeutralizeYodoPower = true });

            double gain = TotalSeconds(before, 51, 400) / TotalSeconds(with, 51, 400) - 1d;

            Assert.Greater(gain, 0.04d, string.Format(
                "두 축을 넣은 이득이 {0:P1}뿐이다 - 보정 지수"
                + "(StageCurve.YodoPowerMarginExponent {1})가 너무 높다",
                gain, StageCurve.YodoPowerMarginExponent));
        }

        // ---------------------------------------------------------------- 밴드 재현

        /**
         * @brief 두 축을 무력화하면 **44단계 밴드가 그대로 재현되는가.**
         *
         * 43단계의 MasteryNeutralized_ReproducesTheStep42World와 같은 자다.
         * 다른 점은 이 축이 44단계 **위에 층으로** 얹혔다는 것이라, 보정을
         * 통째로 걷는 대신 새 겹만 벗긴다(StageCurve가 둘로 갈린 이유).
         *
         * 앵커는 44단계 보고서의 심층 밴드 실측이다.
         *
         * **46단계에 SkipGacha가 붙었다.** 44단계를 재현하려면 그 뒤에 쌓인
         * 층을 **전부** 벗겨야 하는데, 뽑기는 45단계의 상성·영체와 다른
         * 겹이다 - 저쪽은 티어를 읽기만 하고 이쪽은 **티어 자체를 밀어
         * 올린다**(혼 정수). NeutralizeYodoPower만으로는 뽑기가 만든 티어가
         * 그대로 남아 천장이 13.89까지 뜬다. 층이 하나 늘 때마다 이 재현
         * 정책도 한 항목씩 길어지는 것이 정상이고, 그 길이가 곧 "44단계
         * 위에 몇 겹이 쌓였는가"다.
         */
        [Test]
        public void YodoPowerNeutralized_ReproducesTheStep44World()
        {
            var field = Field();
            // 49단계: **4번 슬롯도 걷어낸다.** 이 세계의 정의가 "44단계를 재현한다"인데
            // 그때는 장착 자리가 셋이었다 - 상성·영체·뽑기만 끄고 슬롯을 남기면
            // 재현이 아니라 "44단계 + 슬롯 하나"가 되고, 천장이 12.10에서 13.21로
            // 뜬다. 정책에 한 줄을 더하는 것이 상수를 옮기는 것보다 맞다: 옮기면
            // 이 검사가 지키던 44단계 앵커 자체가 사라진다
            var pay = StageSimulation.Run(200, field,
                new StageSimulation.Policy { NeutralizeYodoPower = true, SkipGacha = true,
                                             SkipSkillSlot = true });
            var f2p = StageSimulation.Run(200, field,
                new StageSimulation.Policy { NeutralizeYodoPower = true, SkipGacha = true,
                                             SkipSkillSlot = true, GemsFromQuestsOnly = true });

            // 조율 구간은 애초에 두 축이 없는 구간이라 기본 정책과도 같아야 한다
            var full = StageSimulation.Run(50, field);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(full[i].BossMargin, pay[i].BossMargin, 1e-12d,
                    "stage " + (i + 1) + ": 상성·영체 또는 뽑기가 조율 구간을 움직였다");

            for (int i = 56 - 1; i < 200; i++)
            {
                var tier = BossCurve.TierOf(pay[i].Stage);

                double ceiling = tier == BossCurve.Tier.Finale ? 6.91d * 1.02d
                               : tier == BossCurve.Tier.Chapter ? 8.72d * 1.02d
                               : 12.10d * 1.02d;
                double floor = tier == BossCurve.Tier.Finale ? 1.21d * 0.98d
                             : tier == BossCurve.Tier.Chapter ? 1.49d * 0.98d
                             : 1.81d * 0.98d;

                Assert.LessOrEqual(pay[i].BossMargin, ceiling, string.Format(
                    "stage {0}: 무력화 세계의 천장 {1:F2} - 44단계 재현이 깨졌다",
                    pay[i].Stage, pay[i].BossMargin));
                Assert.GreaterOrEqual(f2p[i].BossMargin, floor, string.Format(
                    "stage {0}: 무력화 세계의 f2p 바닥 {1:F2} - 44단계 재현이 깨졌다",
                    f2p[i].Stage, f2p[i].BossMargin));
            }
        }

        /**
         * @brief 무과금이 상성·영체를 **최소로도** 계속 받는가. f2p 바닥.
         *
         * 두 축 다 봉인에서 시작하고 봉인에는 파편이 들지 않으므로, 여기가
         * 막히면 드랍 일정이 어긋난 것이다. 그 뒤의 성장(티어)은 파편에
         * 막히지만 멈추지는 않아야 한다.
         */
        [Test]
        public void FreeToPlay_GetsAffinityAndSpiritWithoutTheCatalyst()
        {
            var rows = StageSimulation.Run(300, Field(),
                new StageSimulation.Policy { GemsFromQuestsOnly = true, SkipShardPacks = true });

            // 오니키리가 완성되는 st80이면 네 오의가 전부 상성을 받고 영체도 돈다
            var at80 = rows[79];
            foreach (double factor in at80.SkillAffinity)
                Assert.Greater(factor, 1d,
                    "st80 무과금: 상성이 하나도 안 붙었다 - 봉인에는 파편이 들지 않으므로 "
                    + "여기가 막히면 드랍 일정이 어긋난 것이다");
            Assert.Greater(at80.SpiritRate, 0d, "st80 무과금: 영체가 안 돈다");

            // 그리고 계속 자란다
            Assert.Greater(rows[199].YodoPowerFactor, rows[79].YodoPowerFactor,
                "무과금의 상성·영체가 st80에서 멈춘다 - 촉매 없이는 안 자란다는 뜻이다");
            Assert.Greater(rows[299 - 1].YodoPowerFactor, rows[199].YodoPowerFactor,
                "무과금의 상성·영체가 st200에서 멈춘다");
        }

        // ---------------------------------------------------------------- 방향

        /**
         * @brief 영체가 **아군 규칙**으로 방향을 잡는가.
         *
         * 45단계에 실기에서 물린 자리다 - 영체가 요괴에게 등을 돌리고 섰다.
         * 아트는 대요괴의 것이지만 지금 어느 편에서 싸우는가가 다르고,
         * **같은 스프라이트를 두 진영이 나눠 쓰는 첫 자리**라 아트의 출처를
         * 따라가면 규칙이 뒤집힌다.
         *
         * 두 식이 느낌표 하나 차이라(Facing 머리 주석) 값을 직접 적으면
         * 반드시 한 번은 틀린다. 여기서는 **두 규칙이 서로 반대라는 것**과
         * 아군 규칙이 무엇인지를 못 박고, 영체가 그중 아군 쪽을 쓴다는
         * 사실은 SpiritSummon.TrySummon이 Facing.Ally를 부르는 것으로
         * 표현된다.
         */
        [Test]
        public void Facing_AllyAndEnemyRulesAreOpposites()
        {
            foreach (bool artFacesLeft in new[] { true, false })
                Assert.AreNotEqual(Onikiri.Battle.Facing.Ally(artFacesLeft),
                                   Onikiri.Battle.Facing.Enemy(artFacesLeft),
                    "아군과 적의 방향 규칙이 같아졌다 - 한쪽이 등을 돌린다");

            // 아군은 **왼쪽으로 그려진 팩만** 뒤집어야 오른쪽을 본다
            Assert.IsTrue(Onikiri.Battle.Facing.Ally(true));
            Assert.IsFalse(Onikiri.Battle.Facing.Ally(false));
        }

        /**
         * @brief 네 영체의 아트가 실제로 존재하는가.
         *
         * 없으면 그 자루만 로테이션에서 조용히 빠지고, 증상은 "특정 영체만
         * 안 나온다"로 나타난다 - 쿨다운 버그로 읽히기 쉬운 자리다.
         */
        [Test]
        public void EverySpiritHasArt()
        {
            var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<Onikiri.Battle.BossRoster>(
                "Assets/_Project/Data/Bosses/BossRoster.asset");
            Assert.IsNotNull(roster);

            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                var config = roster.BossForStage(YodoCurve.FirstDropStage(i));
                Assert.IsNotNull(config, YodoCatalog.Blades[i].SpiritName + ": 보스 애셋이 없다");

                var definition = config.Definition;
                Assert.IsNotNull(definition,
                    YodoCatalog.Blades[i].SpiritName + ": 정의가 없다 - 로테이션에서 빠진다");

                bool hasFrames = (definition.attackFrames != null && definition.attackFrames.Length > 0)
                              || (definition.idleFrames != null && definition.idleFrames.Length > 0);
                Assert.IsTrue(hasFrames,
                    YodoCatalog.Blades[i].SpiritName + ": 프레임이 하나도 없다");
            }
        }

        // ---------------------------------------------------------------- 세이브

        /**
         * @brief 두 축이 **티어에서만** 나오는가. 세이브가 v14로 남는 근거다.
         *
         * 새 영구 상태가 하나라도 있으면 v15가 필요하다. 그것이 없다는 것을
         * 말이 아니라 검사로 못 박는다 - **세이브를 복원하는 것만으로 상성과
         * 영체가 통째로 되살아나면** 저장할 것이 티어 말고 없다는 뜻이다.
         *
         * 소환 순번과 쿨다운은 여기 없다. 런타임이고, 저장하지 않는 이유는
         * 오의 쿨다운과 같다(YodoSpiritCurve.BladeForTurn 주석).
         */
        [Test]
        public void AffinityAndSpirit_AreDerivedFromTierAlone()
        {
            var system = BuildSystem();
            try
            {
                // 아무것도 안 벼린 상태 - 상성 1, 영체 0
                for (int s = 0; s < SkillCatalog.Count; s++)
                    Assert.AreEqual(1d, system.AffinityForSkill(s), 1e-12d);
                Assert.AreEqual(0d, system.SpiritRate, 1e-12d);
                Assert.AreEqual(-1, system.SpiritBladeForTurn(0));

                // 티어만 세이브에서 복원한다. 상성·영체 칸은 세이브에 없다
                var ids = new string[YodoCatalog.Count];
                var souls = new long[YodoCatalog.Count];
                var tiers = new int[YodoCatalog.Count];
                var discovered = new int[YodoCatalog.Count];

                for (int i = 0; i < YodoCatalog.Count; i++)
                {
                    ids[i] = YodoCatalog.Blades[i].Id;
                    tiers[i] = i + 2;               // 2, 3, 4, 5
                }

                system.Restore(ids, souls, tiers, discovered, 0L);

                for (int s = 0; s < SkillCatalog.Count; s++)
                    Assert.AreEqual(YodoAffinityCurve.FactorForSkill(s, tiers),
                                    system.AffinityForSkill(s), 1e-12d,
                        SkillCatalog.Skills[s].DisplayName
                        + ": 복원한 티어에서 상성이 안 나온다 - 저장할 것이 더 있다는 뜻이다");

                Assert.AreEqual(YodoSpiritCurve.RateFor(tiers), system.SpiritRate, 1e-12d,
                    "복원한 티어에서 영체가 안 나온다");

                for (int turn = 0; turn < YodoCatalog.Count * 2; turn++)
                    Assert.AreEqual(YodoSpiritCurve.BladeForTurn(turn, tiers),
                                    system.SpiritBladeForTurn(turn),
                        "turn " + turn + ": 시스템과 곡선의 로테이션이 갈렸다");

                // 되돌리면 둘 다 사라진다 - 파생이므로 지울 것도 티어뿐이다
                system.DebugReset();
                for (int s = 0; s < SkillCatalog.Count; s++)
                    Assert.AreEqual(1d, system.AffinityForSkill(s), 1e-12d);
                Assert.AreEqual(0d, system.SpiritRate, 1e-12d);
            }
            finally { UnityEngine.Object.DestroyImmediate(system.gameObject); }
        }

        /**
         * @brief 해금된 상태의 YodoSystem 하나. YodoTests.BuildSystem과 같은 것.
         *
         * 사본을 두는 이유는 저쪽이 private이기 때문이고, 두 벌이 갈리면
         * 여기 검사가 다른 씬을 재게 된다 - 값이 카탈로그에서 오므로 갈릴
         * 여지는 id 배선 하나뿐이다.
         */
        static YodoSystem BuildSystem()
        {
            var go = new UnityEngine.GameObject("YodoPowerTestSystem");

            var progress = go.AddComponent<StageProgress>();
            progress.SetProgress(YodoCurve.UnlockStage, 0, 0, YodoCurve.UnlockStage);

            var system = go.AddComponent<YodoSystem>();

            var so = new UnityEditor.SerializedObject(system);
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
                element.FindPropertyRelative("souls").longValue = 0L;
                element.FindPropertyRelative("tier").intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }
    }
}
