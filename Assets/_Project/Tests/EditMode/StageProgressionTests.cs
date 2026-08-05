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

        [Test]
        public void BossRewards_ScaleWithTheStage()
        {
            var mobHealth = BigDouble.FromDouble(AverageHealth);
            var mobGold = BigDouble.FromDouble(AverageGold);

            Assert.AreEqual(AverageHealth * StageCurve.BossHealthMultiplier,
                            StageCurve.BossHealth(mobHealth).ToDouble(), 1e-6d);
            Assert.AreEqual(AverageGold * StageCurve.BossGoldMultiplier,
                            StageCurve.BossGold(mobGold).ToDouble(), 1e-6d);

            // 골드 배수가 체력 배수보다 커야 도전할 이유가 생긴다. 같거나 작으면
            // 보스는 "위험하기만 한 잡몹"이 된다
            Assert.Greater(StageCurve.BossGoldMultiplier, StageCurve.BossHealthMultiplier,
                "the boss pays no premium for the 30s risk");
        }

        /**
         * @brief 보스가 제한 시간 안에 잡히는 구간에 있는지.
         *
         * 잡몹을 따라가는 만큼만 강화한 플레이어가 보스에서 막히면 게이트가 아니라
         * 벽이다. 반대로 항상 여유롭게 잡히면 게이트가 아무 일도 하지 않는다.
         * 스테이지 진행 시뮬레이션과 같은 구매 정책으로 돌려서 그 사이에 있는지 본다.
         */
        [Test]
        public void Boss_IsBeatableWithinTheTimeLimit()
        {
            int level = 1;
            double purse = 0d;
            double worstMargin = double.MaxValue;
            int worstStage = 0;

            for (int stage = 1; stage <= 50; stage++)
            {
                double damage = DamageBase * Math.Pow(DamageStep, level - 1);
                double mobHealth = AverageHealth * Math.Pow(StageCurve.HealthGrowth, stage - 1);
                double bossHealth = mobHealth * StageCurve.BossHealthMultiplier;

                // 공격속도는 상한까지만 오른다. 보스전은 순수 DPS 싸움이라 이 상한이
                // 그대로 제한 시간의 여유를 정한다
                double aps = Math.Min(AttackSpeedCurve.Ceiling,
                                      AttackSpeedCurve.ValueAtLevel(SpeedLevelFor(stage)));

                double secondsToKill = bossHealth / (damage * aps);
                double margin = StageCurve.BossTimeLimitSeconds / secondsToKill;

                if (margin < worstMargin) { worstMargin = margin; worstStage = stage; }

                purse += StageCurve.KillsPerStage * AverageGold * Math.Pow(StageCurve.GoldGrowth, stage - 1);
                purse += AverageGold * Math.Pow(StageCurve.GoldGrowth, stage - 1) * StageCurve.BossGoldMultiplier;

                while (true)
                {
                    double cost = CostBase * Math.Pow(CostGrowth, level - 1);
                    if (cost > purse) break;
                    purse -= cost;
                    level++;
                }
            }

            Assert.Greater(worstMargin, 1d, string.Format(
                "stage {0}'s boss cannot be killed in {1}s even with the curve's own upgrade pace " +
                "- the gate is a wall, not a checkpoint (needed {2:F1}x more time)",
                worstStage, StageCurve.BossTimeLimitSeconds, 1d / worstMargin));

            // 항상 두 배 이상 여유가 있으면 제한 시간이 아무것도 하지 않는다
            Assert.Less(worstMargin, 4d, string.Format(
                "every boss finishes with {0:F1}x time to spare - the 30s limit never bites", worstMargin));
        }

        /** 공격속도는 공격력보다 늦게 산다고 가정한다. 비용이 같은 곡선이라 번갈아 오른다 */
        static int SpeedLevelFor(int stage)
        {
            return Math.Min(AttackSpeedCurve.MaxLevel, 1 + stage / 2);
        }
    }
}
