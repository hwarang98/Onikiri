using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 3단계 연결의 **순수 검사** - 씬 없이 잴 수 있는 것들.
     *
     * 전투가 실제로 도는지는 PlayMode가 잰다. 여기는 그 전투가 읽는 판정과
     * 산수를 본다:
     *
     *   D-4 대기 상태   `PromotionTrialCatalog.PendingGate`의 다섯 조건
     *   소프트캡 배율   `TrialDamageScale`의 진입·퇴장·감사
     *   피해 일치       표시값과 적용값이 같은 수인가
     */
    public class PromotionTrialWiringTests
    {
        [TearDown]
        public void ResetScale()
        {
            // 정적 상태라 검사 사이에 새어 나간다. 실제로 이 누수가
            // "일반 스테이지 피해가 보정된 채 남는" 결함과 같은 모양이라,
            // 여기서 끄는 습관이 곧 그 결함의 예방이다
            TrialDamageScale.Exit();
        }

        // ------------------------------------------------------------ D-4

        /**
         * @brief 대기 상태의 **정상 흐름**. 세 시점이 각각 다르게 읽혀야 한다.
         *
         * ```
         * st30 진입 전     bossKillCount 29   대기 아님 (보스를 아직 안 벴다)
         * 게이트 보스 처치  bossKillCount 30   **대기** (문이 남았다)
         * 귀문 승리        stage 31 · 30      대기 아님 (문을 넘었다)
         * ```
         */
        [Test]
        public void PendingGate_FollowsTheNormalFlow()
        {
            const int gateStage = 30;

            // 1. 보스를 아직 안 벴다 - 정상 불변식(bossKillCount == max - 1)
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(
                gateStage, gateStage, StageCurve.KillsPerStage, gateStage - 1, 0),
                "보스를 안 벴는데 귀문이 열렸다");

            // 2. 게이트 보스를 벴다 - bossKillCount만 한 칸 앞선다
            Assert.AreEqual(1, PromotionTrialCatalog.PendingGate(
                gateStage, gateStage, StageCurve.KillsPerStage, gateStage, 0),
                "게이트 보스를 벴는데 귀문이 안 열렸다");

            // 3. 귀문을 넘었다 - 스테이지가 오르고 티어가 생겼다
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(
                gateStage + 1, gateStage + 1, 0, gateStage, 1),
                "문을 넘었는데 아직 열려 있다");
        }

        /**
         * @brief 다섯 조건이 **각각** 필요하다. 하나씩 깨서 확인한다.
         *
         * 조건을 느슨하게 두면 손상된 세이브나 테스트 패널 점프에서 문이
         * 저절로 열린다. 그 경우 일반 보스 보상을 건너뛰게 되므로, 엄격한
         * 쪽이 안전하다 - 어긋나면 보스를 다시 잡는 경로(D-1)로 떨어진다.
         */
        [Test]
        public void PendingGate_NeedsEveryCondition()
        {
            const int stage = 40, gate = 2;
            const int kills = StageCurve.KillsPerStage;

            // 기준선 - 다섯이 다 맞으면 열린다
            Assert.AreEqual(gate, PromotionTrialCatalog.PendingGate(stage, stage, kills, stage, 1));

            // 1. 최전선이 아니다 (재선택으로 되돌아갔다)
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(stage, stage + 5, kills, stage, 1),
                "재선택으로 되돌아간 스테이지에서 문이 열렸다");

            // 2. 문이 없는 스테이지
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(41, 41, kills, 41, 1),
                "문이 없는 스테이지에서 열렸다");

            // 3. 이미 그 티어를 갖고 있다 (재도전은 이 경로가 아니다)
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(stage, stage, kills, stage, gate),
                "이미 가진 문이 다시 열렸다");

            // 4. 보스가 안 열려 있었다
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(stage, stage, kills - 1, stage, 1),
                "할당량이 안 찼는데 열렸다");

            // 5. 보스를 안 벴다 (정상 불변식)
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(stage, stage, kills, stage - 1, 1),
                "보스를 안 벴는데 열렸다");

            // 손상 - bossKillCount가 터무니없이 크다
            Assert.AreEqual(0, PromotionTrialCatalog.PendingGate(stage, stage, kills, stage + 99, 1),
                "손상된 bossKillCount로 문이 열렸다");
        }

        /**
         * @brief 여섯 문 **전부**에서 대기 상태가 유도된다.
         *
         * 문 하나만 되는 구현(예: 첫 문에 특수 분기)을 막는다.
         */
        [Test]
        public void PendingGate_WorksAtEveryGate()
        {
            for (int gate = 1; gate <= PromotionTrialCatalog.GateCount; gate++)
            {
                int stage = PromotionTrialCatalog.GateStages[gate - 1];

                Assert.AreEqual(gate, PromotionTrialCatalog.PendingGate(
                    stage, stage, StageCurve.KillsPerStage, stage, gate - 1),
                    string.Format("문{0}(st{1})의 대기 상태가 유도되지 않는다", gate, stage));
            }
        }

        // ------------------------------------------------------------ 소프트캡

        /** 귀문 밖에서는 배율이 **입력을 그대로** 낸다 - 일반 스테이지 불변 */
        [Test]
        public void DamageScale_IsInertOutsideTheTrial()
        {
            Assert.IsFalse(TrialDamageScale.IsActive);

            var raw = BigDouble.FromDouble(12345d);
            Assert.AreEqual(raw.ToDouble(),
                TrialDamageScale.Apply(raw, TrialDamageScale.Source.AutoAttack).ToDouble(), 0d,
                "귀문 밖인데 피해가 움직였다");

            Assert.AreEqual(0L, TrialDamageScale.HitCount,
                "귀문 밖의 피해가 감사 장부에 들어갔다");
        }

        /** 진입 시 한 번 정해지고, 나갈 때 확실히 꺼진다 */
        [Test]
        public void DamageScale_EntersOnceAndAlwaysExits()
        {
            TrialDamageScale.Enter(2000d, 1000d, 0.45d);

            Assert.IsTrue(TrialDamageScale.IsActive);
            Assert.AreEqual(TrialPowerScore.DamageScale(2000d, 1000d, 0.45d),
                TrialDamageScale.Current, 1e-12d);
            Assert.Less(TrialDamageScale.Current, 1d, "기준을 넘었는데 안 깎였다");

            TrialDamageScale.Exit();

            Assert.IsFalse(TrialDamageScale.IsActive);
            Assert.AreEqual(1d, TrialDamageScale.Current, 0d, "나왔는데 배율이 남았다");
        }

        /** 기준 이하는 손대지 않는다 - "과잉 화력만" 줄인다는 규칙 */
        [Test]
        public void DamageScale_LeavesWeakPlayersAlone()
        {
            TrialDamageScale.Enter(500d, 1000d, 0.45d);

            Assert.AreEqual(1d, TrialDamageScale.Current, 0d);

            var raw = BigDouble.FromDouble(777d);
            Assert.AreEqual(raw.ToDouble(),
                TrialDamageScale.Apply(raw, TrialDamageScale.Source.Skill).ToDouble(), 1e-9d);
        }

        /**
         * @brief **모든 출처가 정확히 한 번** 배율을 지난다.
         *
         * 출처별 합이 총합과 같고, 총합의 비가 배율과 같아야 한다. 어느 하나가
         * 우회하면 첫 등식이 깨지고, 두 번 걸리면 두 번째가 깨진다.
         */
        [Test]
        public void EveryDamageSource_PassesTheScaleExactlyOnce()
        {
            TrialDamageScale.Enter(4000d, 1000d, 0.45d);
            double scale = TrialDamageScale.Current;

            var sources = new[]
            {
                TrialDamageScale.Source.AutoAttack,
                TrialDamageScale.Source.Skill,
                TrialDamageScale.Source.Companion,
                TrialDamageScale.Source.Special
            };

            double rawSum = 0d, scaledSum = 0d;
            foreach (var source in sources)
            {
                var raw = BigDouble.FromDouble(100d);
                var applied = TrialDamageScale.Apply(raw, source);

                // 출처별로 정확히 배율 한 번
                Assert.AreEqual(100d * scale, applied.ToDouble(), 1e-9d,
                    source + "가 배율을 한 번 지나지 않았다");

                rawSum += 100d;
                scaledSum += applied.ToDouble();
            }

            Assert.AreEqual(rawSum, TrialDamageScale.RawTotal.ToDouble(), 1e-9d);
            Assert.AreEqual(scaledSum, TrialDamageScale.ScaledTotal.ToDouble(), 1e-9d);
            Assert.AreEqual(scale, TrialDamageScale.ScaledTotal.ToDouble()
                                 / TrialDamageScale.RawTotal.ToDouble(), 1e-12d,
                "총합의 비가 배율과 다르다 - 어딘가에서 두 번 걸리거나 우회했다");

            // 태그 없는 피해는 0이어야 한다
            Assert.AreEqual(0L, TrialDamageScale.HitsOf(TrialDamageScale.Source.Unattributed),
                "출처를 안 밝힌 피해가 있다");
        }

        /** 시도마다 감사 장부가 **비워진다** - 재도전이 무제한이라 누적하면 못 읽는다 */
        [Test]
        public void Audit_ResetsOnEveryEntry()
        {
            TrialDamageScale.Enter(4000d, 1000d, 0.45d);
            TrialDamageScale.Apply(BigDouble.FromDouble(50d), TrialDamageScale.Source.AutoAttack);
            Assert.AreEqual(1L, TrialDamageScale.HitCount);

            TrialDamageScale.Exit();
            TrialDamageScale.Enter(4000d, 1000d, 0.45d);

            Assert.AreEqual(0L, TrialDamageScale.HitCount, "두 번째 시도가 앞선 장부를 이어받았다");
            Assert.AreEqual(0d, TrialDamageScale.RawTotal.ToDouble(), 0d);
        }

        // ------------------------------------------------------------ 점수

        /**
         * @brief `MultiHit`과 `DamageOverTime`은 **0이다.**
         *
         * 연격은 세 항에 이미 곱해져 있어 따로 세면 점수가 부풀고, 캡이 실제보다
         * 세게 걸려 그 플레이어만 손해를 본다. 지속 피해는 시스템이 없다.
         *
         * 런타임 수집기(`PlayerCombat.ReadTrialPower`)가 그 규칙을 지키는지는
         * PlayMode가 재고, 여기서는 규칙 자체를 문서로 못 박는다.
         */
        [Test]
        public void PowerScore_FoldsComboAndHasNoDotYet()
        {
            var components = new TrialPowerScore.Components
            {
                AutoAttack = 300d,
                Skills = 200d,
                MultiHit = 0d,
                DamageOverTime = 0d,
                Companions = 100d,
                BossApplicableSpecials = 50d
            };

            Assert.AreEqual(650d, components.Total, 1e-12d);

            // 여섯 항에 각각 곱한 것이 총합에 곱한 것과 같다
            double scale = TrialPowerScore.DamageScale(components.Total, 400d, 0.45d);
            var scaled = TrialPowerScore.Apply(components, scale);

            Assert.AreEqual(components.Total * scale, scaled.Total, 1e-9d);
            Assert.AreEqual(components.Companions * scale, scaled.Companions, 1e-9d,
                "동료가 배율을 우회했다");
        }
    }
}
