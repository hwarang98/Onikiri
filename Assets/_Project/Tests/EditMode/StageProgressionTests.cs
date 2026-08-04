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

        [Test]
        public void StageProgress_AdvancesEveryTenKills()
        {
            var go = new UnityEngine.GameObject("~TestStage");
            var progress = go.AddComponent<StageProgress>();

            progress.SetProgress(1, 0);
            for (int i = 0; i < StageCurve.KillsPerStage - 1; i++) progress.RegisterKill();

            Assert.AreEqual(1, progress.Stage, "stage advanced before the kill quota was met");
            Assert.AreEqual(StageCurve.KillsPerStage - 1, progress.KillsThisStage);

            progress.RegisterKill();
            Assert.AreEqual(2, progress.Stage);
            Assert.AreEqual(0, progress.KillsThisStage, "kill counter did not reset on stage up");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void SetProgress_ClampsRestoredValues()
        {
            var go = new UnityEngine.GameObject("~TestStage");
            var progress = go.AddComponent<StageProgress>();

            // 손상된 세이브가 0스테이지나 음수 처치 수를 들고 올 수 있다
            progress.SetProgress(0, -5);
            Assert.AreEqual(1, progress.Stage);
            Assert.AreEqual(0, progress.KillsThisStage);

            progress.SetProgress(7, 999);
            Assert.AreEqual(7, progress.Stage);
            Assert.Less(progress.KillsThisStage, StageCurve.KillsPerStage,
                "a restored kill count at or above the quota would skip a stage-up");

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
