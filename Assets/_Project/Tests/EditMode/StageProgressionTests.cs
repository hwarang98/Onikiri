using System;
using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 스테이지 진행이 성장을 살려두는지 검증한다.
     *
     * 7단계까지의 문제는 요괴 체력이 고정이라는 것이었다. 공격력을 몇 번 올리면
     * 요괴가 한 방에 죽고, 그 뒤로는 공격력을 아무리 올려도 수입이 늘지 않는다.
     * 처치 속도가 공격력이 아니라 요괴 공급에 묶이기 때문이다.
     *
     * 지표는 **처치당 타격 수**다. 이 값이 1로 내려앉으면 공격력 강화가 죽고,
     * 계속 불어나면 후반이 늘어진다. 아래 시뮬레이션이 그 범위를 못 박는다.
     */
    public class StageProgressionTests
    {
        // UpgradePanelBuilder / BattleContentBuilder 가 실제로 기록하는 값
        const double DamageBase = 5d;
        const double DamageStep = 1.12d;
        const double CostBase = 10d;
        const double CostGrowth = 1.15d;

        // 티어 3종을 스폰 가중치로 평균 낸 값 (가중치 3/5/1)
        const double AverageHealth = (3d * 8d + 5d * 14d + 1d * 34d) / 9d;   // 14.22
        const double AverageGold = (3d * 2d + 5d * 5d + 1d * 18d) / 9d;      // 5.44

        [Test]
        public void FirstStage_HasNoMultiplier()
        {
            // 정의 에셋의 값이 곧 1스테이지 밸런스여야 한다. 여기가 1이 아니면
            // 에셋을 보고 짐작한 난이도와 실제가 처음부터 어긋난다
            Assert.AreEqual(1d, StageCurve.HealthMultiplier(1).ToDouble(), 1e-9d);
            Assert.AreEqual(1d, StageCurve.GoldMultiplier(1).ToDouble(), 1e-9d);
        }

        [Test]
        public void Multipliers_GrowByTheAuthoredRate()
        {
            Assert.AreEqual(StageCurve.HealthGrowth, StageCurve.HealthMultiplier(2).ToDouble(), 1e-9d);
            Assert.AreEqual(Math.Pow(StageCurve.HealthGrowth, 9d),
                            StageCurve.HealthMultiplier(10).ToDouble(), 1e-6d);
            Assert.AreEqual(Math.Pow(StageCurve.GoldGrowth, 9d),
                            StageCurve.GoldMultiplier(10).ToDouble(), 1e-6d);
        }

        /**
         * @brief 골드 성장률이 강화 비용 성장률과 맞는지.
         *
         * 이 관계가 깨지면 시뮬레이션이 실패하기 전에 여기서 먼저 드러난다.
         * 곡선 중 하나만 손대고 나머지를 잊는 것이 가장 흔한 실수다.
         */
        [Test]
        public void GoldGrowth_MatchesTheCostOfKeepingUp()
        {
            double levelsPerStage = Math.Log(StageCurve.HealthGrowth) / Math.Log(DamageStep);
            double costGrowthPerStage = Math.Pow(CostGrowth, levelsPerStage);

            Assert.AreEqual(costGrowthPerStage, StageCurve.GoldGrowth, 0.02d,
                "gold growth drifted from the cost of keeping damage level with enemy health");
        }

        [Test]
        public void HitsToKill_RoundsUp()
        {
            Assert.AreEqual(3, StageCurve.HitsToKill(BigDouble.FromDouble(15d), BigDouble.FromDouble(5d)));
            Assert.AreEqual(4, StageCurve.HitsToKill(BigDouble.FromDouble(16d), BigDouble.FromDouble(5d)));
            // 한 방에 죽어도 최소 한 대는 때려야 한다
            Assert.AreEqual(1, StageCurve.HitsToKill(BigDouble.FromDouble(2d), BigDouble.FromDouble(500d)));
        }

        /**
         * @brief 실제 곡선으로 50스테이지를 돌려 처치당 타격 수를 관찰한다.
         *
         * 정책은 단순하다. 스테이지마다 10마리를 잡아 번 골드로 공격력을 살 수 있는
         * 만큼 산다. 이것이 방치형 플레이어가 실제로 하는 행동이다.
         */
        [Test]
        public void HitsPerKill_StaysInAPlayableBandFor50Stages()
        {
            int level = 1;
            double purse = 0d;
            int minHits = int.MaxValue;
            int maxHits = 0;

            for (int stage = 1; stage <= 50; stage++)
            {
                double damage = DamageBase * Math.Pow(DamageStep, level - 1);
                double health = AverageHealth * Math.Pow(StageCurve.HealthGrowth, stage - 1);

                int hits = StageCurve.HitsToKill(BigDouble.FromDouble(health), BigDouble.FromDouble(damage));
                if (hits < minHits) minHits = hits;
                if (hits > maxHits) maxHits = hits;

                Assert.Greater(hits, 1,
                    "enemies die in one hit at stage " + stage + " - attack power stops mattering there");
                Assert.Less(hits, 20,
                    "a kill takes " + hits + " hits at stage " + stage + " - the field would crawl");

                // 이 스테이지의 수입
                purse += StageCurve.KillsPerStage * AverageGold * Math.Pow(StageCurve.GoldGrowth, stage - 1);

                // 살 수 있는 만큼 산다
                while (true)
                {
                    double cost = CostBase * Math.Pow(CostGrowth, level - 1);
                    if (cost > purse) break;
                    purse -= cost;
                    level++;
                }
            }

            // 밴드가 지나치게 넓어지면 밸런스가 흐르고 있다는 뜻이다
            Assert.LessOrEqual(maxHits - minHits, 8,
                "hits per kill drifted from " + minHits + " to " + maxHits + " across 50 stages");
        }

        /**
         * @brief 9단계부터 잡몹 처치는 스테이지를 올리지 않는다. 보스를 연다.
         *
         * 8단계까지는 여기서 stage가 올라갔다. 그 동작이 남아 있으면 보스가
         * 등장하기도 전에 스테이지가 지나가버린다.
         */
        [Test]
        public void TenKills_OpensTheBossInsteadOfAdvancing()
        {
            var go = new UnityEngine.GameObject("~TestStage");
            var progress = go.AddComponent<StageProgress>();

            progress.SetProgress(1, 0, 0);
            for (int i = 0; i < StageCurve.KillsPerStage - 1; i++) progress.RegisterKill();

            Assert.AreEqual(1, progress.Stage);
            Assert.IsFalse(progress.IsBossReady, "boss opened before the kill quota was met");

            progress.RegisterKill();
            Assert.AreEqual(1, progress.Stage, "stage advanced on kills - the boss gate is bypassed");
            Assert.IsTrue(progress.IsBossReady);

            UnityEngine.Object.DestroyImmediate(go);
        }

        /**
         * @brief 보스에서 막힌 동안에도 잡몹은 계속 잡힌다.
         *
         * 소프트락 방지의 핵심이다. 처치 수는 상한에서 멈추지만 처치 자체는 계속
         * 일어나야 하고, 골드가 들어와야 강화해서 다시 도전할 수 있다.
         */
        [Test]
        public void KillsPastTheQuota_DoNotOverflow()
        {
            var go = new UnityEngine.GameObject("~TestStage");
            var progress = go.AddComponent<StageProgress>();

            progress.SetProgress(3, 0, 0);
            for (int i = 0; i < StageCurve.KillsPerStage * 5; i++) progress.RegisterKill();

            Assert.AreEqual(StageCurve.KillsPerStage, progress.KillsThisStage,
                "kill count ran past the quota - the HUD would show 50/10");
            Assert.AreEqual(3, progress.Stage);
            Assert.IsTrue(progress.IsBossReady);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AdvanceStage_ResetsTheQuotaAndCountsTheBoss()
        {
            var go = new UnityEngine.GameObject("~TestStage");
            var progress = go.AddComponent<StageProgress>();

            progress.SetProgress(4, StageCurve.KillsPerStage, 3);
            progress.AdvanceStage();

            Assert.AreEqual(5, progress.Stage);
            Assert.AreEqual(0, progress.KillsThisStage, "kill counter did not reset on stage up");
            Assert.IsFalse(progress.IsBossReady, "the next boss opened immediately");
            Assert.AreEqual(4, progress.BossKillCount);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void SetProgress_ClampsRestoredValues()
        {
            var go = new UnityEngine.GameObject("~TestStage");
            var progress = go.AddComponent<StageProgress>();

            // 손상된 세이브가 0스테이지나 음수 처치 수를 들고 올 수 있다
            progress.SetProgress(0, -5, -2);
            Assert.AreEqual(1, progress.Stage);
            Assert.AreEqual(0, progress.KillsThisStage);
            Assert.AreEqual(0, progress.BossKillCount);

            // 할당량과 같은 값은 유효하다. 9단계부터 그 상태가 "보스가 열렸다"를 뜻하고,
            // 저장했다가 다시 켜면 도전 버튼이 그대로 있어야 한다
            progress.SetProgress(7, StageCurve.KillsPerStage, 6);
            Assert.AreEqual(7, progress.Stage);
            Assert.AreEqual(StageCurve.KillsPerStage, progress.KillsThisStage);
            Assert.IsTrue(progress.IsBossReady);

            progress.SetProgress(7, 999, 6);
            Assert.AreEqual(StageCurve.KillsPerStage, progress.KillsThisStage,
                "a restored kill count above the quota should clamp, not accumulate");

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ---------------------------------------------------------------- 보스

        /**
         * @brief 보스 보상이 스테이지에 맞춰 오르는지.
         *
         * 체력 배수는 10단계부터 스테이지에 따라 함께 오른다(BossHealthGrowth).
         * 골드 배수는 고정이다 - 보상까지 함께 부풀리면 보스 한 번이 그 스테이지의
         * 파밍 전체보다 커져서, 잡몹을 잡을 이유가 사라진다.
         *
         * 26단계에 골드 배수가 12 -> 10.8(=체력 배수)이 됐다. 자세한 이유는
         * StageCurve.BossGoldMultiplier 주석과 아래 단언 참고.
         */
        [Test]
        public void BossRewards_ScaleWithTheStage()
        {
            var mobHealth = BigDouble.FromDouble(AverageHealth);
            var mobGold = BigDouble.FromDouble(AverageGold);

            Assert.AreEqual(AverageHealth * StageCurve.BossHealthMultiplierBase,
                            StageCurve.BossHealth(mobHealth, 1).ToDouble(), 1e-6d);
            Assert.AreEqual(AverageGold * StageCurve.BossGoldMultiplier,
                            StageCurve.BossGold(mobGold).ToDouble(), 1e-6d);

            // 26단계에 **웃돈이 사라졌다.** 이 자리는 9단계에 "골드 배수가 체력
            // 배수보다 커야 도전할 이유가 생긴다"였고, 그때는 보스가 선택이었다.
            // 지금 보스는 스테이지 관문이라(BossGate) 안 싸우면 다음 스테이지가
            // 없고, 도전할 이유를 골드로 살 필요가 없다.
            //
            // 남아 있던 12(체력 10.8 대비 x1.11)는 17단계에 체력 배수만 올리면서
            // 남은 잔여값이었고, 그 잔여 웃돈이 st11 여유 스파이크의 마지막
            // 조각이었다 - 보스 골드는 스테이지 경계를 넘어 곧바로 화력이 된다.
            //
            // 이제 둘이 같다. 검사는 "웃돈이 있는가"에서 **"둘이 한 값에서
            // 나오는가"**로 바뀐다 - 한쪽만 고쳐지면 그 순간 뜻 없는 잔여값이
            // 다시 생긴다.
            Assert.AreEqual(StageCurve.BossHealthMultiplierBase, StageCurve.BossGoldMultiplier, 1e-9d,
                "보스 골드와 체력 배수가 갈렸다 - 웃돈을 되살릴 생각이면 st11 여유부터 다시 재라");

            // 등급 차등은 그대로다. "일반보다 챕터가 후하다"는 살아 있는 설계이고,
            // 지운 것은 **모든 보스에 균일하게 붙던** 웃돈뿐이다
            Assert.Greater(BossCurve.ChapterGoldMultiplier, 1d,
                "챕터 보스의 보상 차등까지 사라졌다");

            // 체력 배수는 스테이지를 따라 오른다. 9단계에서는 고정이었고 그 때문에
            // 제한 시간이 5스테이지부터 무의미해졌다
            Assert.Greater(StageCurve.BossHealthMultiplier(10), StageCurve.BossHealthMultiplier(1),
                "boss health multiplier is flat again - the time limit stops biting");
        }

    }
}
