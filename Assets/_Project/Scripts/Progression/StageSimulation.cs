using System;
using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 곡선을 그대로 돌려 진행 시간과 보스 난이도를 낸다.
     *
     * 이것이 런타임 코드로 존재하는 이유는 9단계에서 겪은 일 때문이다. 보고서의
     * 진행 시간 계산을 일회용 스크립트로 짰는데, 그 스크립트가 쓴 상수가 실제 게임의
     * 값과 같다는 보장이 어디에도 없었다. 실제로 계산은 "1스테이지 보스는 무강화로도
     * 클리어"라고 했는데 플레이 화면은 실패를 보여줬고, 둘 중 어느 쪽이 맞는지
     * 판단할 근거가 없었다.
     *
     * (그때의 원인은 계산이 아니라 화면 쪽이었다 - 제한 시간을 강제로 소진시킨
     * 스크린샷이었다. 하지만 그것을 확인하는 데 든 비용이 이 파일이 존재해야 하는
     * 이유다.)
     *
     * 이제 계산은 여기 하나뿐이고, StageSimulationTests가 이 안의 값들이 실제
     * 에셋·트랙·전투와 일치하는지 검사한다.
     *
     * **모델링하는 것**: 치명타 기대값(CombatBaseline), 요괴 공급 하한, 보스가
     * 걸어 들어오는 동안 제한 시간이 흐르는 것.
     *
     * 앞의 두 개는 처음에 빠져 있었고, 마침 서로 반대 방향으로 비슷한 크기라
     * 결과가 우연히 맞았다. 우연히 맞는 계산은 다음 계수 변경에서 조용히 틀린다.
     *
     * **모델링하지 않는 것**: 히트스톱(제한 시간이 스케일 타임이라 정지한 만큼은
     * 시계도 멈춘다 - 계산에 넣을 필요가 없다), 처치 순간의 이동/큐 재정렬.
     */
    public static class StageSimulation
    {
        /** 한 스테이지의 결과 한 줄 */
        public struct StageResult
        {
            public int Stage;

            /** 잡몹 10마리에 걸린 총 시간 */
            public double MobSeconds;

            /** 스테이지 끝 시점의 잡몹 한 마리 처치 시간 */
            public double MobKillSeconds;

            /** 그때 수렴한 보충 간격. 하한 0.4초에 닿았는지 보는 값 */
            public double SpawnInterval;

            public double BossKillSeconds;
            public bool BossCleared;

            /** 때릴 수 있는 시간 / 실제 처치 시간. 1.5~3.0 밴드가 목표다 */
            public double BossMargin;

            public int AttackPowerLevel;
            public int AttackSpeedLevel;
            public int CritRateLevel;
            public int CritDamageLevel;

            public double Damage;
            public double AttacksPerSecond;
            public double CritRate;
            public double CritMultiplier;
            public double ExpectedDps;

            public int HealthLevel;
            public int RegenLevel;
            public double MaxHealth;
            public double RegenPerSecond;

            /** 유효체력 / 이 보스가 제한 시간 동안 낼 피해. 1 아래면 죽는다 */
            public double SurvivalMargin;
            public bool Survived;

            /** 이 스테이지가 챕터 보스인가 */
            public bool IsChapterBoss;

            // ------------------------------------------------------------ 12단계

            /** 이 스테이지를 끝냈을 때의 캐릭터 레벨 */
            public int CharacterLevel;

            /** 이 스테이지에서 오른 레벨 수 */
            public int LevelsGained;

            public int AttackPoints;
            public int HealthPoints;

            /**
             * @brief 이 스테이지에서 한 레벨을 올리는 데 걸린 평균 시간.
             *
             * 목표 리듬을 재는 값이다. 초반 30초 안팎에서 시작해 후반으로 갈수록
             * 늘어나야 한다 - 레벨이 골드보다 느리게 자라야 하기 때문이다.
             * 이 스테이지에서 한 번도 안 올랐으면 무한대다.
             */
            public double SecondsPerLevel;

            /** 스탯 포인트가 곱하고 있는 배수. 골드 축과 얼마나 벌어졌는지 보는 값 */
            public double AttackAmp;
            public double HealthAmp;

            // ------------------------------------------------------------ 20단계

            public int GoldGainLevel;

            /** 골드 보상에 곱해지고 있는 배수 */
            public double GoldGain;

            /** 이 스테이지의 파밍 속도. 회수 시간의 분모다 */
            public double GoldPerSecond;

            /** 지금 레벨에서 한 칸 더 살 때의 회수 시간 (초). 밴드를 재는 값 */
            public double GoldGainPaybackSeconds;

            // ------------------------------------------------------------ 26단계

            /** 세 스킬의 레벨. 잠긴 스킬도 1로 들어온다 */
            public int[] SkillLevels;

            /** 자동 시전이 만드는 초당 환산 공격 횟수 */
            public double SkillRate;

            /**
             * @brief 스킬이 DPS에서 차지하는 몫 (0~1).
             *
             * 밴드보다 이쪽이 먼저 움직인다. 여유가 아직 밴드 안인데 이 값이
             * 0.5를 넘어가고 있다면, 자동 공격이 장식이 되는 길 위에 있다는
             * 뜻이고 다음 계수 변경에서 밴드가 깨진다.
             */
            public double SkillDpsShare;

            // ------------------------------------------------------------ 32단계

            public int WeaponGrade;
            public int WeaponLevel;
            public int ArmorGrade;
            public int ArmorLevel;

            /** 무기가 공격력에 곱하고 있는 배수 */
            public double WeaponMultiplier;

            /** 방어구가 최대 체력에 곱하고 있는 배수 */
            public double ArmorMultiplier;

            /**
             * @brief 이 스테이지 끝까지 **벌어들인** 보석 총량.
             *
             * 일일 퀘스트는 들어 있지 않다. 시뮬레이션에는 달력이 없기 때문이고,
             * 그래서 이 값은 **하한**이다 - 매일 접속하는 플레이어는 여기에
             * 하루 55개씩을 더 갖는다. 밴드는 그 양쪽 끝을 모두 검사한다
             * (Policy.GemsFromQuestsOnly).
             */
            public int GemsEarned;

            /** 등급업에 쓴 보석 총량 */
            public int GemsSpent;

            /**
             * @brief 이 스테이지에서 골드 획득 축을 **실제로 살 때** 잰 회수 시간.
             *
             * 한 번도 안 샀으면 무한대다.
             *
             * StageResult.GoldGainPaybackSeconds와 다른 값이다. 그쪽은 "스테이지가
             * 끝난 시점에 한 칸 더 산다면"이라 축이 이미 상한이면 무한대가 되고,
             * 그러면 회수 밴드 검사가 **한 스테이지도 못 재고 통과한다** - 20단계에
             * 넣은 밴드 테스트가 실제로 그 상태였다(축이 해금 스테이지 안에서
             * 상한까지 팔려서 검사 대상 행이 하나도 남지 않았다).
             *
             * 지표는 사는 순간을 재야 한다. 안 사는 순간의 회수 시간은 밴드를
             * 지켰다는 증거가 되지 않는다.
             */
            public double GoldGainPaybackAtPurchase;
        }

        /**
         * @brief 시뮬레이션이 재는 플레이어의 구매 성향.
         *
         * 기본값은 "곡선을 따라가는 플레이어"이고 그것이 밸런스의 기준선이다.
         * 나머지 둘은 **비교군**이다 - 어떤 축이 죽은 버튼인지는 그 축을 산
         * 플레이어와 안 산 플레이어를 나란히 돌려야만 알 수 있다.
         *
         * 16단계의 `EveryAxisIsBoughtAtLeastOnceThrough20`은 "샀는가"만 봤고,
         * 그 자로는 **사고 나서 손해인 축**을 잡을 수 없다. 20단계의 골드 획득
         * 축이 정확히 그랬다 - 장부에는 열세 번 샀다고 남았는데 30스테이지까지
         * 총 시간은 안 산 것과 같았다.
         */
        public struct Policy
        {
            /**
             * @brief 골드 획득 축을 한 번도 사지 않는다. **세상은 그대로다.**
             *
             * 보스 체력 보정(StageCurve.GoldAxisCompensation)은 스테이지의 함수라
             * 플레이어를 구분하지 못한다. 그래서 이 비교군은 "축을 산 사람 기준으로
             * 무거워진 보스를 축 없이 상대하는 플레이어"이고, 재는 것은
             * **"안 사면 손해인가"**다 - 20단계가 남긴 함정 버튼 의혹이 그 질문이다.
             */
            public bool SkipGoldGain;

            /**
             * @brief 축도 보정도 **둘 다** 없는 세계. 20단계가 쟀던 자다.
             *
             * 위와 다른 질문이다. 이쪽은 "이 축을 게임에 넣은 것이 이득이었는가"를
             * 묻고, 그래서 보정도 함께 걷어낸다 - 축이 없으면 상쇄할 것도 없다.
             *
             * 둘을 나눠 두지 않으면 두 질문의 답이 섞인다. 20단계는 뒤의 답이
             * "0"이라고 보고했고, 그것을 앞의 답으로 읽으면 "사면 손해"라는
             * 결론이 나온다. 실제로는 앞의 답이 계속 +였다.
             */
            public bool NeutralizeGoldAxis;

            /** 스킬을 한 번도 올리지 않는다 (해금은 되므로 레벨 1의 기여는 남는다) */
            public bool SkipSkills;

            /**
             * @brief 업적 보상을 한 번도 받지 않는다. 31단계의 비교군.
             *
             * 일일·반복은 보석만 주므로 여기 없다 - 보석은 DPS로 환산되지 않아
             * 시뮬레이션에 들어갈 것이 없다. **업적만** 골드/경험치를 준다.
             *
             * 이 자를 두는 이유는 "업적을 넣어서 밴드가 얼마나 움직였는가"를
             * 재기 위해서다. 20단계의 골드 축이 그랬듯, 새 faucet은 넣은 뒤가
             * 아니라 **넣기 전과 비교해야** 크기를 알 수 있다.
             */
            public bool SkipAchievements;

            /**
             * @brief 장비를 한 번도 사지 않는다. 32단계의 비교군 (a).
             *
             * 죽은 버튼 검사의 한쪽이다. "장비 단련/등급업이 실제 DPS·EHP·진행을
             * 움직이는가"는 산 사람과 안 산 사람을 나란히 돌려야만 답이 나온다 -
             * 20단계의 골드 축이 장부에는 열세 번 샀다고 남고도 총 시간이 같았던
             * 그 자리다.
             *
             * 보정(StageCurve.EquipmentCompensation)은 걷어내지 않는다. 그것은
             * 스테이지의 함수라 플레이어를 구분하지 못하고, 그래서 이 비교군이
             * 재는 것은 **"안 사면 손해인가"**다. Policy.SkipGoldGain과 같은 자다.
             */
            public bool SkipEquipment;

            /**
             * @brief 단련만 하고 등급은 올리지 않는다. 32단계의 비교군 (b).
             *
             * **보석이 값어치가 있는가**를 재는 자다. 등급업이 유일한 보석
             * 소비처이므로, 이 정책은 곧 "보석을 한 개도 안 쓴 플레이어"다.
             *
             * SkipEquipment와 나눠 두지 않으면 두 질문의 답이 섞인다. 장비 전체가
             * 이득이라는 것과 그중 보석 몫이 이득이라는 것은 다른 사실이고,
             * 20단계가 그 둘을 섞어 읽었다가 "사면 손해"라는 결론을 냈다.
             */
            public bool SkipGradeUps;

            /**
             * @brief 장비도 보정도 **둘 다** 없는 세계. 32단계 이전 그대로다.
             *
             * SkipEquipment와 다른 질문이다. 저쪽은 "안 사면 손해인가"를 묻고
             * 이쪽은 **"이 축을 게임에 넣은 것이 이득이었는가"**를 묻는다 -
             * 축이 없으면 상쇄할 것도 없으므로 보정도 함께 걷어낸다.
             *
             * 둘을 나눠 두지 않으면 두 질문의 답이 섞인다. 20단계가 뒤의 답을
             * 앞의 답으로 읽어 "사면 손해"라는 결론을 낸 자리이고, 그 구분이
             * Policy.SkipGoldGain / NeutralizeGoldAxis 두 벌로 남아 있다.
             */
            public bool NeutralizeEquipment;

            /**
             * @brief 등급업 보석을 **퀘스트 실수령분으로만** 낸다. 밴드의 아래쪽 끝.
             *
             * 기본값(false)은 보석이 무제한이라고 본다 - 매일 접속해 일일 다섯을
             * 받는 플레이어이고, 등급이 골드와 단련 진도에만 막히는 상태다.
             * 그쪽이 **여유의 위쪽 끝**이므로 천장 검사가 그 값을 봐야 한다.
             *
             * 이것을 켜면 업적 보석과 반복 티어만으로 등급을 산다. 일일은
             * 시뮬레이션에 달력이 없어 셀 수 없고, 빼면 정확히 "한 번도 일일을
             * 받지 않은 플레이어"가 되어 **여유의 아래쪽 끝**이 된다.
             *
             * 두 끝을 다 검사하는 것이 이 단계의 핵심이다. 재화의 수입이
             * 접속 빈도에 달린 축을 밴드에 넣으면, 한쪽만 재는 순간 다른 쪽
             * 플레이어의 게임이 검사되지 않는다.
             */
            public bool GemsFromQuestsOnly;

            public static Policy Default { get { return new Policy(); } }
        }

        public struct Field
        {
            /** 스폰 가중치로 평균 낸 1스테이지 기준 잡몹 체력/골드 */
            public double AverageMobHealth;
            public double AverageMobGold;

            /**
             * @brief 요괴 보충 간격 (초).
             *
             * 처치 간격의 하한이다. DPS가 아무리 높아도 다음 요괴가 오지 않으면
             * 때릴 것이 없다. 후반에는 이쪽이 실제 상한이 된다.
             */
            public double SpawnInterval;
        }

        /**
         * @brief 기대 DPS. 치명타를 포함한다.
         *
         * 치명타를 빼면 실제보다 12% 낮게 나온다. 보스전은 제한 시간 판정이라
         * 그 12%가 통과와 실패를 가르는 구간이 실제로 존재한다.
         */
        public static double ExpectedDps(CombatStats stats)
        {
            return stats.ExpectedDps;
        }

        /** 지금 스탯으로 이 체력을 깎는 데 걸리는 시간 */
        public static double SecondsToKill(double health, CombatStats stats)
        {
            double dps = stats.ExpectedDps;
            if (dps <= 0d) return double.PositiveInfinity;
            return health / dps;
        }

        /**
         * @brief 이 스테이지의 보스를 제한 시간 안에 잡을 수 있는가.
         *
         * 체력 계산은 StageCurve.BossHealthForStage를 그대로 쓴다. BossFight가
         * 실제로 스폰할 때 부르는 것과 같은 함수다.
         */
        public static double BossKillSeconds(double averageMobHealth, int stage, CombatStats stats)
        {
            var health = StageCurve.BossHealthForStage(BigDouble.FromDouble(averageMobHealth), stage);
            return SecondsToKill(health.ToDouble(), stats);
        }

        /**
         * @brief 제한 시간 중 실제로 때릴 수 있는 시간. **제한 시간 전부다.**
         *
         * 16단계까지는 여기서 워크인 5.3초를 뺐다. 시계가 보스 스폰과 함께
         * 돌기 시작하는데 보스는 화면 밖에서 걸어 들어와서, 그 5.3초 동안
         * 사거리에 아무도 없었기 때문이다 - 제한 시간의 18%가 기다림이었다.
         *
         * 17단계에서 그것을 **시계 쪽에서** 고쳤다. 이제 플레이어가 보스에게
         * 달려가고(연출), 도달한 순간부터 30초가 시작한다. 달려가는 구간은
         * 타이머 밖이므로 뺄 것이 없다.
         *
         * 16단계 주석은 "시계가 보스 도착 후에 시작하면 등장이 공짜가 되어
         * 플레이어가 그 시간을 기다림으로만 느낀다"고 적었는데, 그 전제가
         * 바뀌었다. 기다리는 것이 아니라 **달려가는 것**이면 그 시간은 대기가
         * 아니라 전진이다.
         */
        public static double BossDamageWindowSeconds
        {
            get { return StageCurve.BossTimeLimitSeconds; }
        }

        public static bool BossClears(double averageMobHealth, int stage, CombatStats stats)
        {
            return BossKillSeconds(averageMobHealth, stage, stats) <= BossDamageWindowSeconds;
        }

        /**
         * @brief 시작 스탯 그대로(강화 없음)의 DPS.
         *
         * 게이트가 언제부터 무는지를 재는 기준선이다.
         */
        public static CombatStats StartingStats { get { return CombatStats.CappedAtLevel(1); } }

        public static double StartingDamage { get { return StartingStats.Damage; } }
        public static double StartingAttacksPerSecond { get { return StartingStats.AttacksPerSecond; } }

        /**
         * @brief 강화를 전혀 하지 않은 플레이어가 처음 실패하는 스테이지.
         *
         * 1이면 첫 보스부터 벽이라 온보딩이 끊기고, 너무 크면 게이트가 한참 동안
         * 아무 일도 하지 않는다.
         */
        public static int FirstStageThatBlocksAnUnupgradedPlayer(double averageMobHealth, int searchTo)
        {
            for (int stage = 1; stage <= searchTo; stage++)
            {
                if (!BossClears(averageMobHealth, stage, StartingStats))
                    return stage;
            }
            return -1;
        }

        /** 강화를 전혀 하지 않은 플레이어의 유효체력 */
        public static double StartingEffectiveHealth
        {
            get
            {
                return SurvivalEfficiency.EffectiveHealth(
                    HealthCurve.ValueAtLevel(1), HealthRegenCurve.ValueAtLevel(1));
            }
        }

        /**
         * @brief 강화를 전혀 하지 않은 플레이어가 처음 **죽는** 스테이지.
         *
         * 시간 초과로 막히는 스테이지와 다른 값이다. 둘이 같으면 체력 축이
         * 아무 일도 하지 않는다는 뜻이고, 체력 게이트가 화력 게이트보다 한참
         * 뒤에 오면 생존 축을 살 이유가 늦게 생긴다.
         */
        public static int FirstStageThatKillsAnUnupgradedPlayer(int searchTo)
        {
            double ehp = StartingEffectiveHealth;

            for (int stage = 1; stage <= searchTo; stage++)
            {
                double incoming = BossCurve.TotalDamageOverFight(stage, StageCurve.BossTimeLimitSeconds);
                if (ehp < incoming) return stage;
            }
            return -1;
        }

        /**
         * @brief 1스테이지부터 throughStage까지를 돌린다.
         *
         * 구매 정책은 "골드가 되는 대로 지금 더 싼 축을 산다"이다. 두 축의 골드당
         * 효율이 같은 레벨에서 1.20배로 일정하므로(UpgradeEfficiency), 실제 플레이도
         * 이렇게 번갈아 오른다.
         */
        public static List<StageResult> Run(int throughStage, Field field)
        {
            return Run(throughStage, field, Policy.Default);
        }

        public static List<StageResult> Run(int throughStage, Field field, Policy policy)
        {
            var results = new List<StageResult>();

            var levels = new Levels();
            double purse = 0d;

            // 이미 받은 업적. 일회성이므로 한 번만 지급된다 - 게임 쪽
            // QuestSystem.achievementClaimed와 같은 뜻이다
            var achievementTaken = new bool[QuestCatalog.AchievementCount];

            // 반복 퀘스트가 읽는 누적 카운터. 보석 하한을 세는 데만 쓴다
            double totalMobKills = 0d, totalBossKills = 0d, totalSkillCasts = 0d;
            int repeatGemsTaken = 0;

            for (int stage = 1; stage <= throughStage; stage++)
            {
                // 이 스테이지에서 획득 축을 산 순간의 회수 시간. 스테이지마다
                // 비운다 - 표의 한 줄은 그 스테이지에서 일어난 일만 말해야 한다
                double purchasePayback = double.PositiveInfinity;

                double mobHealth = field.AverageMobHealth * StageCurve.HealthMultiplier(stage).ToDouble();
                double rawGoldPerMob = field.AverageMobGold * StageCurve.GoldMultiplier(stage).ToDouble();
                double expPerMob = ExpCurve.MobExp(stage).ToDouble();

                double mobSeconds = 0d;
                double lastKill = 0d;
                double lastInterval = 0d;
                double lastGoldPerSecond = 0d;
                int levelsGained = 0;

                for (int k = 0; k < StageCurve.KillsPerStage; k++)
                {
                    double kill = SecondsToKill(mobHealth, levels.Stats);

                    // 보충 간격은 고정이 아니라 처치 속도에 수렴한다. 그래서 처치가
                    // 빨라지면 파밍 시간도 함께 줄어든다 - 9단계에서 11.0초에
                    // 고정되던 지점이 여기다. 하한 0.4초. SpawnPacing 참고
                    double interval = SpawnPacing.SettledInterval(kill);
                    double seconds = Math.Max(kill, interval);
                    mobSeconds += seconds;

                    lastKill = kill;
                    lastInterval = interval;

                    // 획득 축이 곱해진 실제 수령액. 루프 안에서 매번 다시 읽는
                    // 이유는 바로 아래 Buy가 이 축의 레벨을 올릴 수 있기 때문이다
                    double goldPerMob = rawGoldPerMob * levels.GoldGain;
                    purse += goldPerMob;

                    // 회수 시간의 분모. 파밍 한 마리에 걸린 시간으로 나눈 것이
                    // 이 시점의 초당 골드다 - 게임 쪽 IdleIncome.GoldPerSecond가
                    // 처치 속도와 공급 하한 중 낮은 쪽을 쓰는 것과 같은 값이다
                    lastGoldPerSecond = seconds > 0d ? goldPerMob / seconds : 0d;

                    // 경험치는 골드와 같은 자리에서 들어온다. 게임에서도 처치
                    // 하나가 둘 다 준다(EnemySpawner.OnEnemyKilled)
                    levelsGained += levels.GainExp(expPerMob);
                    Buy(ref levels, ref purse, stage, lastGoldPerSecond, stage, policy, ref purchasePayback);
                }

                var stats = levels.Stats;

                // 보정을 걷어낸 세계에서는 보스가 그만큼 가벼워진다. 체력을
                // 나누는 것이 아니라 처치 시간을 나눈다 - 둘은 같은 값이고
                // (시간 = 체력 / DPS), 이쪽은 BossHealthForStage를 건드리지
                // 않으므로 **전투와 시뮬레이션이 같은 함수를 지난다**는 성질이
                // 유지된다
                double bossKill = BossKillSeconds(field.AverageMobHealth, stage, stats);
                if (policy.NeutralizeGoldAxis) bossKill /= StageCurve.GoldAxisCompensation(stage);
                if (policy.NeutralizeEquipment) bossKill /= StageCurve.EquipmentCompensation(stage);

                // 이 보스가 제한 시간을 다 쓰면 낼 총 피해. 유효체력이 이보다
                // 작으면 시간이 다 되기 전에 죽는다
                double incoming = BossCurve.TotalDamageOverFight(stage, StageCurve.BossTimeLimitSeconds);

                // 보스 보상을 받은 뒤의 구매는 **다음** 스테이지를 대비한다.
                // 생존 축이 "다음 보스에게 죽지 않을 만큼"을 기준으로 사기 때문에
                // 여기서 stage를 넘기면 이미 지나간 보스를 대비하게 된다
                // 챕터 배수는 BossGoldForStage 안에 들어 있다. 여기서 또 곱하면
                // 두 번 적용된다 - 시뮬레이션만 후하게 계산하는 상태가 된다
                // 보스 골드와 클리어 보너스에도 획득 배수가 곱해진다. 게임 쪽
                // 두 지점(EnemySpawner, BossFight)과 같아야 하고, 여기만 빠지면
                // 시뮬레이션이 실제보다 가난한 플레이어를 재게 된다
                purse += StageCurve.BossGoldForStage(
                    BigDouble.FromDouble(field.AverageMobGold), stage).ToDouble() * levels.GoldGain;

                // 17단계의 클리어 보너스. 화면에는 축하 숫자로 뜨지만 밸런스에는
                // 그대로 들어온다 - 16단계에서 피날레 골드가 다음 스테이지를
                // 망가뜨린 것과 같은 경로다
                purse += StageCurve.ClearGoldForStage(
                    BigDouble.FromDouble(field.AverageMobGold), stage).ToDouble() * levels.GoldGain;
                levelsGained += levels.GainExp(ExpCurve.BossExp(stage).ToDouble());

                // 업적 보상은 **보스를 잡은 직후**에 들어온다. 스테이지 도달
                // 업적이 그 시점에 열리고, 레벨/총합 업적도 보스 경험치와 보스
                // 골드로 산 강화까지 반영된 뒤라야 실제와 같은 순간이 된다.
                //
                // 구매(Buy)보다 **먼저** 지급한다. 나중에 두면 이번 스테이지의
                // 업적 골드가 다음 스테이지에 가서야 쓰이고, 그러면 게임보다
                // 한 스테이지 늦게 반영되는 시뮬레이션이 된다
                levelsGained += GrantAchievements(
                    ref levels, ref purse, achievementTaken, stage, field, policy);

                // 반복 퀘스트 보석. **업적 다음, 구매 앞이다** - 업적과 같은
                // 이유로 이 스테이지에서 열린 티어는 이 스테이지의 구매에
                // 쓰여야 게임보다 한 칸 늦게 반영되지 않는다.
                //
                // 오의 시전 수는 세지 않고 **환산한다.** 잡몹 구간과 보스전
                // 동안만 쿨다운이 도는 것이 게임 쪽 규칙이고(SkillSystem.Update가
                // 사거리를 확인한다), 달려가는 5.3초는 빠진다
                totalMobKills += StageCurve.KillsPerStage;
                totalBossKills += 1d;
                totalSkillCasts += SkillCastsIn(levels, mobSeconds + bossKill);

                int repeatGems = RepeatGems(totalMobKills, totalBossKills, totalSkillCasts);
                levels.GemsEarned += repeatGems - repeatGemsTaken;
                repeatGemsTaken = repeatGems;

                Buy(ref levels, ref purse, stage + 1, lastGoldPerSecond, stage, policy, ref purchasePayback);

                results.Add(new StageResult
                {
                    Stage = stage,
                    MobSeconds = mobSeconds,
                    MobKillSeconds = lastKill,
                    SpawnInterval = lastInterval,
                    BossKillSeconds = bossKill,
                    BossCleared = bossKill <= BossDamageWindowSeconds,
                    BossMargin = BossDamageWindowSeconds / bossKill,
                    AttackPowerLevel = levels.Power,
                    AttackSpeedLevel = levels.Speed,
                    CritRateLevel = levels.CritRate,
                    CritDamageLevel = levels.CritDamage,
                    Damage = stats.Damage,
                    AttacksPerSecond = stats.AttacksPerSecond,
                    CritRate = stats.CritRate,
                    CritMultiplier = stats.CritMultiplier,
                    ExpectedDps = stats.ExpectedDps,

                    HealthLevel = levels.H,
                    RegenLevel = levels.G,
                    MaxHealth = levels.MaxHealth,
                    RegenPerSecond = levels.RegenPerSecond,
                    SurvivalMargin = incoming > 0d ? levels.EffectiveHealth / incoming : double.PositiveInfinity,
                    Survived = levels.EffectiveHealth >= incoming,
                    IsChapterBoss = BossCurve.IsChapterBoss(stage),

                    CharacterLevel = levels.L,
                    LevelsGained = levelsGained,
                    AttackPoints = levels.AttackPoints,
                    HealthPoints = levels.HealthPoints,
                    AttackAmp = levels.AttackAmp,
                    HealthAmp = levels.HealthAmp,

                    GoldGainLevel = levels.Gd,
                    GoldGain = levels.GoldGain,
                    GoldPerSecond = lastGoldPerSecond,
                    GoldGainPaybackSeconds =
                        GoldGainEfficiency.PaybackSeconds(levels.Gd, lastGoldPerSecond),
                    GoldGainPaybackAtPurchase = purchasePayback,

                    WeaponGrade = levels.Wg,
                    WeaponLevel = levels.Wl,
                    ArmorGrade = levels.Ag,
                    ArmorLevel = levels.Al,
                    WeaponMultiplier = levels.WeaponMultiplier,
                    ArmorMultiplier = levels.ArmorMultiplier,
                    GemsEarned = levels.GemsEarned,
                    GemsSpent = levels.GemsSpent,

                    SkillLevels = levels.SkillLevelsSnapshot(),

                    // 레벨은 levels에서, 비율은 stats에서 온다. 다른 축과 같은
                    // 규칙이다 - 레벨 칸은 보스 보상까지 쓴 뒤의 값이고, DPS 칸은
                    // **보스를 잡을 때** 갖고 있던 값이다. 보스 여유가 후자에서
                    // 나오므로 둘을 섞으면 표의 여유와 표의 DPS가 어긋난다
                    SkillRate = stats.SkillRate,

                    // 괄호 안의 몫이 곧 DPS의 몫이다. 공격력도 치명타도 괄호
                    // 밖에서 양쪽에 똑같이 곱해지므로 약분된다
                    SkillDpsShare = stats.AttacksPerSecond + stats.SkillRate > 0d
                        ? stats.SkillRate / (stats.AttacksPerSecond + stats.SkillRate)
                        : 0d,

                    // 이 스테이지에 든 시간을 오른 레벨 수로 나눈다. 보스 연출과
                    // 처치 시간까지 포함하는 이유는 그것도 플레이어가 앉아 있는
                    // 시간이기 때문이다 - 리듬은 체감이고 체감은 벽시계다
                    SecondsPerLevel = levelsGained > 0
                        ? (mobSeconds + BossIntroSeconds + BossWalkInSeconds + bossKill) / levelsGained
                        : double.PositiveInfinity
                });
            }

            return results;
        }

        /**
         * @brief 이번 스테이지에 열린 업적을 지급한다. 오른 레벨 수를 돌려준다.
         *
         * ## 조건을 시뮬레이션의 상태에서 읽는다
         *
         * 게임 쪽 QuestSystem.CurrentStateOf가 스테이지·레벨·강화 총합·오의
         * 총합을 읽는 것과 같은 네 가지를 여기서도 읽는다. 상수를 쓰지 않는 것이
         * 요점이다 - "레벨 25 업적은 대략 9스테이지쯤"이라고 적어두면 곡선을
         * 손볼 때마다 시뮬레이션과 게임이 다른 시점에 보상을 준다.
         *
         * ## 자동으로 받는다
         *
         * 게임에서는 수령 버튼을 눌러야 하지만 시뮬레이션은 열리는 즉시 받는다.
         * Levels.GainExp가 레벨업을 자동으로 처리하는 것과 같은 판단이다 -
         * 재는 것은 "이 시점에 받을 수 있는가"이고, 미루는 시간까지 모델링하면
         * 진행 속도가 임의의 가정에 좌우된다.
         */
        private static int GrantAchievements(ref Levels levels, ref double purse,
                                             bool[] taken, int stage, Field field, Policy policy)
        {
            if (policy.SkipAchievements) return 0;

            int levelsGained = 0;
            var specs = QuestCatalog.Achievement;

            for (int i = 0; i < specs.Length; i++)
            {
                if (taken[i]) continue;

                var spec = specs[i];
                double state;
                switch (spec.Metric)
                {
                    case QuestMetric.StageReached: state = stage; break;
                    case QuestMetric.LevelReached: state = levels.L; break;
                    case QuestMetric.UpgradeLevelTotal: state = levels.UpgradeTotal; break;
                    case QuestMetric.SkillLevelTotal: state = levels.SkillTotal; break;
                    default: continue;
                }

                if (state < spec.Target) continue;

                taken[i] = true;

                // **획득 축(levels.GoldGain)을 곱하지 않는다.** 잡몹·보스·클리어
                // 골드에는 전부 곱하는데 여기만 빼는 것이 의도다.
                //
                // 업적 보상은 파밍이 아니라 마일스톤이다. 획득 축을 곱하면 그 축의
                // 효율 계산(GoldGainEfficiency)에 파밍이 아닌 수입이 섞여 들어가고,
                // 그러면 "얼마나 벌고 있는가"로 회수 시간을 재는 식이 어긋난다.
                //
                // 게임 쪽 QuestSystem.GrantAchievementSpoils도 곱하지 않는다.
                // 두 곳이 같아야 시뮬레이션과 화면이 같은 크기를 낸다
                purse += QuestCatalog.AchievementGold(
                    spec, BigDouble.FromDouble(field.AverageMobGold), stage).ToDouble();

                // 31단계에는 보석을 세지 않았다. 소비처가 없어서 DPS로 환산되지
                // 않았고, GemWallet 주석이 "이 사실이 깨지는 날 시뮬레이션에
                // 편입해야 한다"고 적어뒀다. 32단계가 그 날이다
                levels.GemsEarned += spec.Gems;

                levelsGained += levels.GainExp(QuestCatalog.AchievementExp(spec, stage).ToDouble());
            }

            return levelsGained;
        }

        /**
         * @brief 지금까지의 누적 카운터가 연 반복 퀘스트 티어의 보석 총합.
         *
         * 게임 쪽 QuestSystem.ClaimableCount와 같은 식(누적 / 목표치의 몫)이다.
         * 상수를 쓰지 않고 QuestCatalog를 읽는 것이 요점이다 - 표가 바뀌면
         * 시뮬레이션의 보석 하한도 함께 움직여야 한다.
         *
         * **받는 즉시 받는다고 본다.** 업적과 같은 판단이고 같은 이유다
         * (GrantAchievements 주석) - 미루는 시간까지 모델링하면 진행 속도가
         * 임의의 가정에 좌우된다.
         */
        private static int RepeatGems(double mobKills, double bossKills, double skillCasts)
        {
            int total = 0;

            foreach (var spec in QuestCatalog.Repeat)
            {
                if (spec.Target <= 0d) continue;

                double counter;
                switch (spec.Metric)
                {
                    case QuestMetric.MobKills: counter = mobKills; break;
                    case QuestMetric.BossKills: counter = bossKills; break;
                    case QuestMetric.SkillCasts: counter = skillCasts; break;

                    // 골드 누적 반복은 지금 표에 없다. 생기면 여기서 조용히
                    // 0으로 세어지므로, 그때 이 switch를 함께 고쳐야 한다는 것을
                    // QuestTests.RepeatMetrics_AreAllModelledInTheSimulation 이 못 박는다
                    default: continue;
                }

                total += (int)Math.Floor(counter / spec.Target) * spec.Gems;
            }

            return total;
        }

        /** 이 구간에서 자동 시전이 만들어냈을 오의 횟수. 반복 퀘스트가 센다 */
        private static double SkillCastsIn(Levels levels, double activeSeconds)
        {
            if (activeSeconds <= 0d) return 0d;

            double casts = 0d;
            int count = Math.Min(SkillCatalog.Count, SkillSlotCapacity);

            for (int i = 0; i < count; i++)
            {
                if (!SkillCatalog.IsUnlockedAt(i, levels.L)) continue;

                double cooldown = SkillCatalog.Skills[i].CooldownSeconds;
                if (cooldown > 0d) casts += activeSeconds / cooldown;
            }

            return casts;
        }

        /** 여섯 축의 레벨. 구매 정책이 이것을 굴린다 */
        private struct Levels
        {
            public int Power;
            public int Speed;
            public int CritRate;
            public int CritDamage;
            public int Health;
            public int Regen;

            /** 20단계의 획득 축 */
            public int Gold;

            // ------------------------------------------------------------ 32단계

            /**
             * @brief 장비 두 슬롯. **낱개 필드다** - 스킬 레벨과 같은 이유다.
             *
             * Levels가 struct라서 그렇다. 배열을 두면 `var after = levels;`가
             * 참조를 복사하고, 효율을 재려고 만든 사본이 원본까지 올려버린다
             * (GainPerGoldFor). 값 복사가 목적인 자리에 참조를 두면 그 버그는
             * "효율 계산만 하면 등급이 오른다"로 나타난다.
             */
            public int WeaponGrade;
            public int WeaponLevel;
            public int ArmorGrade;
            public int ArmorLevel;

            /** 퀘스트로 벌어들인 보석 (업적 + 반복 티어). 일일은 없다 */
            public int GemsEarned;
            public int GemsSpent;

            public int GemsAvailable { get { return GemsEarned - GemsSpent; } }

            /** 0으로 시작하지 않는다. 등급도 레벨도 1이 시작값이다 */
            public int Wg { get { return WeaponGrade < 1 ? 1 : WeaponGrade; } }
            public int Wl { get { return WeaponLevel < 1 ? 1 : WeaponLevel; } }
            public int Ag { get { return ArmorGrade < 1 ? 1 : ArmorGrade; } }
            public int Al { get { return ArmorLevel < 1 ? 1 : ArmorLevel; } }

            public double WeaponMultiplier
            {
                get { return EquipmentCurve.ValueAt(WeaponSpec.GradeStep, WeaponSpec.TemperStep, Wg, Wl); }
            }
            public double ArmorMultiplier
            {
                get { return EquipmentCurve.ValueAt(ArmorSpec.GradeStep, ArmorSpec.TemperStep, Ag, Al); }
            }

            // ------------------------------------------------------------ 26단계

            /**
             * @brief 세 스킬의 레벨. **배열이 아니라 낱개 필드다.**
             *
             * Levels가 struct라서 그렇다. 배열을 두면 `var after = levels;`가
             * 참조를 복사하고, 효율을 재려고 만든 사본이 원본의 레벨을 함께
             * 올려버린다(GainPerGoldFor). 값 복사가 목적인 자리에 참조를 두면
             * 그 버그는 "효율 계산만 하면 레벨이 오른다"로 나타나서, 화면에도
             * 로그에도 원인이 남지 않는다.
             *
             * 스킬이 넷째가 되면 여기 칸을 하나 더 만들어야 한다. 잊지 않도록
             * SkillSlotCapacity와 테스트가 짝으로 지킨다.
             */
            public int Skill0;
            public int Skill1;
            public int Skill2;

            public int SkillLevel(int index)
            {
                int level;
                switch (index)
                {
                    case 0: level = Skill0; break;
                    case 1: level = Skill1; break;
                    case 2: level = Skill2; break;
                    default: return 1;
                }
                // 다른 축과 같은 규칙 - 모든 축은 레벨 1이 시작값이다
                return level < 1 ? 1 : level;
            }

            public void SetSkillLevel(int index, int level)
            {
                switch (index)
                {
                    case 0: Skill0 = level; break;
                    case 1: Skill1 = level; break;
                    case 2: Skill2 = level; break;
                }
            }

            public int[] SkillLevelsSnapshot()
            {
                var levels = new int[SkillSlotCapacity];
                for (int i = 0; i < levels.Length; i++) levels[i] = SkillLevel(i);
                return levels;
            }

            /**
             * @brief 오의 레벨 총합. 업적 조건이 읽는다.
             *
             * 카탈로그에 있는 만큼만 센다. 게임 쪽 SkillSystem.TotalLevels가
             * 슬롯 수만큼 세는 것과 같은 값이다 - 슬롯 수와 카탈로그 수가
             * 갈리면 SkillAxisTests가 잡는다
             */
            public int SkillTotal
            {
                get
                {
                    int total = 0;
                    int count = Math.Min(SkillCatalog.Count, SkillSlotCapacity);
                    for (int i = 0; i < count; i++) total += SkillLevel(i);
                    return total;
                }
            }

            /**
             * @brief 일곱 강화 축의 레벨 총합. 업적 조건이 읽는다.
             *
             * 각 축은 레벨 1에서 시작하므로 아무것도 안 산 상태가 7이다 -
             * 게임 쪽 UpgradeSystem.TotalLevels와 같은 기준이다. 여기서 0부터
             * 세면 업적이 게임보다 일곱 칸 늦게 열린다
             */
            public int UpgradeTotal
            {
                get
                {
                    return Math.Max(1, Power) + Math.Max(1, Speed)
                         + Math.Max(1, CritRate) + Math.Max(1, CritDamage)
                         + Math.Max(1, Health) + Math.Max(1, Regen)
                         + Math.Max(1, Gold);
                }
            }

            /** 지금 열려 있는 스킬들이 만드는 초당 환산 공격 횟수 */
            public double SkillRate
            {
                get
                {
                    double rate = 0d;
                    int count = Math.Min(SkillCatalog.Count, SkillSlotCapacity);
                    for (int i = 0; i < count; i++)
                        rate += SkillCatalog.RateAt(i, SkillLevel(i), L);
                    return rate;
                }
            }

            // ------------------------------------------------------------ 12단계

            /** 캐릭터 레벨. 1부터 */
            public int Character;

            /** 현재 레벨에서 모은 경험치 */
            public double Exp;

            public int AttackPoints;
            public int HealthPoints;

            public int L { get { return Character < 1 ? 1 : Character; } }

            public double AttackAmp { get { return StatPointCurve.Multiplier(AttackPoints); } }
            public double HealthAmp { get { return StatPointCurve.Multiplier(HealthPoints); } }

            /**
             * @brief 경험치를 받고 올릴 수 있는 만큼 올린다.
             *
             * 자동으로 올린다. 게임에서는 버튼을 눌러야 하지만(CharacterLevel),
             * 시뮬레이션이 재는 것은 "이 시점에 올릴 수 있는가"이고 플레이어가
             * 누르기를 미루는 시간까지 모델링하면 진행 속도가 임의의 가정에
             * 좌우된다. 실제로도 레벨업 버튼은 떠 있으면 누르는 버튼이다.
             */
            public int GainExp(double amount)
            {
                Exp += amount;

                int gained = 0;
                for (int guard = 0; guard < 100000; guard++)
                {
                    double need = ExpCurve.RequiredForLevel(L + gained).ToDouble();
                    if (Exp < need) break;

                    Exp -= need;
                    gained++;
                }

                Character = L + gained;
                return gained;
            }

            /**
             * @brief 스탯 포인트를 찍는다. 배분 규칙은 다음 하나다.
             *
             *   **다음 보스에게 죽지 않을 만큼까지는 체력 증폭, 그 뒤는 전부
             *   공격력 증폭.**
             *
             * 골드 축의 정책("생존 먼저, 그 다음 화력")과 같은 규칙이고, 같아야
             * 한다 - 두 재화가 서로 다른 우선순위를 쓰면 시뮬레이션이 재는 것이
             * 어느 쪽 플레이어인지 알 수 없게 된다.
             *
             * 축이 둘뿐이고 포인트당 효과가 같아서 골드 축처럼 효율을 비교할
             * 것이 없다. 남는 판단 기준은 "지금 무엇이 모자란가" 하나다.
             *
             * 상한(StatPointCurve.MaxPoints)에 닿은 축은 건너뛴다. 실제로
             * 도달할 일은 없지만, 도달했는데 계속 찍으면 포인트가 조용히
             * 사라진다.
             */
            public void SpendPoints(double neededEffectiveHealth)
            {
                int unspent = StatPointCurve.TotalPointsAtLevel(L) - AttackPoints - HealthPoints;

                for (int i = 0; i < unspent; i++)
                {
                    bool wantHealth = EffectiveHealth < neededEffectiveHealth;

                    if (wantHealth && HealthPoints < StatPointCurve.MaxPoints) HealthPoints++;
                    else if (AttackPoints < StatPointCurve.MaxPoints) AttackPoints++;
                    else if (HealthPoints < StatPointCurve.MaxPoints) HealthPoints++;
                    else break;
                }
            }

            public int H { get { return Health < 1 ? 1 : Health; } }
            public int G { get { return Regen < 1 ? 1 : Regen; } }

            /** 획득 축의 레벨과 지금 곱하고 있는 배수 */
            public int Gd { get { return Gold < 1 ? 1 : Gold; } }
            public double GoldGain { get { return GoldGainCurve.CappedValueAtLevel(Gd); } }

            /**
             * @brief 스탯 포인트 증폭과 **방어구 배수**가 곱해진 값.
             *
             * UpgradeSystem.Apply와 같은 순서다. 두 곳이 갈리면 시뮬레이션이
             * 게임과 다른 밸런스를 잰다.
             */
            public double MaxHealth
            {
                get { return HealthCurve.ValueAtLevel(H) * HealthAmp * ArmorMultiplier; }
            }

            /**
             * @brief 초당 회복 비율 (최대 체력 대비). **상한이 적용된 값이다.**
             *
             * 16단계에서 상한이 생겼다. 여기서 무상한 값을 쓰면 시뮬레이션은
             * 무적 플레이어를 기준으로 밸런스를 재고, 화면과 갈린다.
             */
            public double RegenFraction { get { return HealthRegenCurve.CappedValueAtLevel(G); } }

            /** 초당 절대 회복량. 표에 찍을 때만 쓴다 */
            public double RegenPerSecond { get { return MaxHealth * RegenFraction; } }

            public double EffectiveHealth
            {
                get { return SurvivalEfficiency.EffectiveHealth(MaxHealth, RegenFraction); }
            }

            /** 0으로 시작하지 않는다. 모든 축은 레벨 1이 시작 스탯이다 */
            public int P { get { return Power < 1 ? 1 : Power; } }
            public int S { get { return Speed < 1 ? 1 : Speed; } }
            public int R { get { return CritRate < 1 ? 1 : CritRate; } }
            public int D { get { return CritDamage < 1 ? 1 : CritDamage; } }

            public CombatStats Stats
            {
                get
                {
                    return new CombatStats
                    {
                        // 증폭은 공격력에만 붙는다. UpgradeSystem.Apply와 같다 -
                        // 두 곳이 갈리면 시뮬레이션이 게임과 다른 밸런스를 잰다
                        //
                        // 32단계의 무기 배수도 같은 자리다. **괄호 밖이라
                        // 스킬에도 상속된다** - 스킬 데미지가 공격력 x 배율이고
                        // 그 공격력이 이 값이기 때문이다. 게임 쪽도 같다
                        // (PlayerCombat.Damage 하나를 두 경로가 읽는다)
                        Damage = AttackPowerCurve.ValueAtLevel(P) * AttackAmp * WeaponMultiplier,
                        AttacksPerSecond = AttackSpeedCurve.CappedValueAtLevel(S),
                        CritRate = CritRateCurve.CappedValueAtLevel(R),
                        CritMultiplier = CritDamageCurve.ValueAtLevel(D),

                        // 26단계. 괄호 안에 더해지므로 위의 공격력 증폭과 아래
                        // 치명타 계수를 그대로 상속한다 - 여기서 다시 곱하면
                        // 스킬만 증폭이 두 번 걸린다
                        SkillRate = SkillRate
                    };
                }
            }
        }

        /**
         * @brief 총 소요 시간. 보스의 등장 연출과 걸어 들어오는 시간을 포함한다.
         *
         * 연출 시간을 여기 넣는 이유는, 그것이 스테이지마다 반드시 드는 고정 비용이라
         * 후반에서 무시할 수 없는 비중이 되기 때문이다. 5스테이지쯤이면 보스 처치
         * 자체보다 걸어 들어오는 시간이 더 길다.
         */
        public const double BossIntroSeconds = 1d;

        /**
         * @brief 보스에게 달려가는 시간.
         *
         * 16단계까지 이 값은 "보스가 화면 밖에서 걸어 들어오는 시간"이었고
         * **제한 시간 안에** 있었다. 17단계에서 방향이 뒤집혔다 - 이제
         * 플레이어가 달려가고, 이 구간은 타이머 밖이다.
         *
         * 총 소요 시간에는 여전히 들어간다. 타이머가 안 돌 뿐 플레이어가
         * 화면 앞에 앉아 있는 시간은 맞고, 스테이지마다 반드시 드는 고정
         * 비용이라 진행 속도 계산에서 빼면 안 된다.
         */
        public const double BossRunUpSeconds = 5.3d;

        /** 예전 이름. 뜻이 바뀌었으므로 새 이름을 쓴다 */
        public const double BossWalkInSeconds = BossRunUpSeconds;

        public static double TotalSeconds(List<StageResult> results)
        {
            double total = 0d;
            foreach (var row in results)
                total += row.MobSeconds + BossIntroSeconds + BossWalkInSeconds + row.BossKillSeconds;
            return total;
        }

        /**
         * @brief 골드가 되는 대로, 지금 골드당 DPS 이득이 가장 큰 축을 산다.
         *
         * 축이 둘일 때는 "더 싼 쪽"으로 충분했다. 두 축의 효율 비율이 레벨과
         * 무관하게 일정해서 비용 비교가 곧 효율 비교였기 때문이다.
         *
         * 넷이 되면 그것이 성립하지 않는다. 치명타 피해는 레벨이 오를수록 DPS
         * 기여가 커지고 치명타 확률은 언덕을 그린다. 그래서 실제 효율
         * (UpgradeEfficiency가 재는 것과 같은 값)로 고른다 - 시뮬레이션의 구매
         * 정책과 게임이 플레이어에게 보여주는 지표가 같은 함수여야 한다.
         */
        /**
         * @brief 골드로 사는 모든 축의 구매 정책. 26단계부터 **열 개**다.
         *
         * **생존이 먼저, 그 다음이 화력이다.**
         *
         *   1. 다음 보스에게 죽지 않을 만큼 생존 축을 산다. 생존 축 둘 중에서는
         *      골드당 %EHP가 큰 쪽을 고른다.
         *   2. 남는 골드로 화력 축을 산다. **일곱**(공격력·공격속도·치명타
         *      둘·스킬 셋) 중에서는 골드당 %DPS가 큰 쪽.
         *
         * 스킬이 2번 안에 그대로 들어간 것이 26단계의 핵심이다. 스킬을 위한
         * 별도 정책을 두면 시뮬레이션이 재는 플레이어가 "여섯 축은 효율로 사고
         * 스킬은 규칙으로 사는 사람"이 되는데, 화면에서 그 둘은 똑같이 생긴
         * 골드 버튼이다.
         *
         * 순서를 이렇게 둔 이유는 두 자원의 성질이 다르기 때문이다. 화력이 모자라면
         * 보스전이 길어질 뿐이지만 생존이 모자라면 **그 스테이지에 아예 들어갈 수
         * 없다.** 실제 플레이어도 죽고 나면 체력부터 올린다.
         *
         * "죽지 않을 만큼"에는 여유를 둔다. 정확히 맞추면 스테이지가 오르는 순간마다
         * 한 번씩 죽고, 그 죽음은 정보가 아니라 반복 작업이 된다.
         */
        private const double SurvivalSafetyMargin = 1.15d;

        private static void Buy(ref Levels levels, ref double purse, int nextStage, double goldPerSecond,
                                int currentStage, Policy policy, ref double purchasePayback)
        {
            // 생존 먼저. 다음 보스가 낼 총 피해를 여유를 두고 넘길 때까지 산다
            double needed = BossCurve.TotalDamageOverFight(nextStage, StageCurve.BossTimeLimitSeconds)
                            * SurvivalSafetyMargin;

            // 스탯 포인트가 먼저다. 골드가 들지 않으므로 미룰 이유가 없고,
            // 미루면 골드 축이 그만큼을 대신 메우게 되어 두 축의 기여가 섞인다
            levels.SpendPoints(needed);

            // 해금 전에는 목록에 없다(GoldGainCurve.UnlockStage). 게이트를 시뮬레이션
            // 쪽에도 넣지 않으면 계산만 온보딩에서 이 축을 사고, 그 차이가 그대로
            // "보고서의 소요 시간과 실제 플레이가 다르다"가 된다
            if (!policy.SkipGoldGain && !policy.NeutralizeGoldAxis
                && GoldGainCurve.IsUnlockedAt(currentStage))
                BuyGoldGain(ref levels, ref purse, goldPerSecond, ref purchasePayback);

            for (int guard = 0; guard < 100000; guard++)
            {
                if (levels.EffectiveHealth >= needed) break;

                double healthCost = HealthCurve.CostAtLevel(levels.H);
                double regenCost = HealthRegenCurve.CostAtLevel(levels.G);

                // 골드당 %EHP가 큰 쪽. UpgradeEfficiency가 아니라
                // SurvivalEfficiency와 같은 자다.
                //
                // 회복은 상한이 적용된 값으로 잰다. 상한 위에서는 이득이 0이라
                // 자연히 안 사게 되고, 그것이 실제 플레이어가 하는 판단이다 -
                // 값이 안 오르는 버튼에 골드를 쓰지 않는다
                // **증폭을 곱해서 비교한다.** levels.MaxHealth에는 스탯 포인트
                // 증폭이 이미 곱해져 있는데(12단계), 여기만 곡선값을 그대로 쓰면
                // 증폭이 커질수록 "다음 레벨"이 현재보다 작아져 이득이 음수가 된다.
                //
                // 증폭이 x1.005일 때는 묻혀 있다가 16단계에서 x1.56이 되자
                // 드러났다 - 비교가 뒤집혀 체력을 한 번도 안 사고 회복만
                // Lv.129까지 사들였다. 같은 것끼리 비교해야 한다
                //
                // 32단계에 **방어구 배수도 같은 이유로 곱한다.** levels.MaxHealth에
                // 방어구가 들어오면서(EquipmentSystem) 분모에는 있고 분자에는 없는
                // 상태가 됐고, 그러면 방어구를 살수록 체력 강화의 이득이 음수로
                // 계산된다. 16단계에 스탯 포인트 증폭으로 겪은 것과 같은 버그가
                // 곱해지는 항이 하나 더 늘면서 다시 열린 자리다
                double healthGain = (SurvivalEfficiency.EffectiveHealth(
                        HealthCurve.ValueAtLevel(levels.H + 1) * levels.HealthAmp
                            * levels.ArmorMultiplier,
                        levels.RegenFraction)
                    / levels.EffectiveHealth - 1d) / healthCost;

                double regenGain = levels.G >= HealthRegenCurve.MaxLevel
                    ? 0d
                    : (SurvivalEfficiency.EffectiveHealth(
                            levels.MaxHealth, HealthRegenCurve.CappedValueAtLevel(levels.G + 1))
                        / levels.EffectiveHealth - 1d) / regenCost;

                // 32단계의 방어구도 **같은 저울**에 올린다. 재화가 같으면 자도
                // 하나여야 한다는 것이 이 프로젝트의 규칙이고(DamageAxisCount
                // 주석), 화면에서 셋은 똑같이 생긴 골드 버튼이다.
                //
                // 별도 규칙으로 빼면 시뮬레이션이 재는 플레이어가 "체력·회복은
                // 효율로 사고 방어구는 순서로 사는 사람"이 된다.
                double armorCost;
                double armorGain = ArmorGainPerGold(levels, currentStage, policy, out armorCost);

                double cost;
                int pick;   // 0 체력 / 1 회복 / 2 방어구

                if (armorGain > healthGain && armorGain > regenGain) { pick = 2; cost = armorCost; }
                else if (healthGain >= regenGain) { pick = 0; cost = healthCost; }
                else { pick = 1; cost = regenCost; }

                if (cost > purse) break;

                purse -= cost;
                if (pick == 0) levels.Health = levels.H + 1;
                else if (pick == 1) levels.Regen = levels.G + 1;
                else AdvanceArmor(ref levels);
            }

            // 무한 루프 방어. 비용이 0이 되는 곡선이 들어오면 여기서 멈춘다
            for (int guard = 0; guard < 100000; guard++)
            {
                int bestAxis = -1;
                double bestGain = 0d;
                double bestCost = 0d;

                for (int axis = 0; axis < DamageAxisCount; axis++)
                {
                    // 스킬을 안 사는 비교군. 해금과 레벨 1의 기여는 남긴다 -
                    // 재려는 것이 "스킬 시스템이 있는가"가 아니라 **"스킬에
                    // 골드를 쓰는 것이 이득인가"**이기 때문이다
                    if (policy.SkipSkills && axis >= 4) continue;

                    double cost;
                    if (!TryCost(levels, axis, currentStage, policy, out cost) || cost > purse) continue;

                    double gain = GainPerGoldFor(levels, axis, currentStage, policy);
                    if (gain <= bestGain) continue;

                    bestGain = gain;
                    bestAxis = axis;
                    bestCost = cost;
                }

                if (bestAxis < 0) break;

                purse -= bestCost;
                Advance(ref levels, bestAxis);
            }
        }

        /**
         * @brief 화력 축의 개수. 여섯 축 중 넷 + 스킬 셋.
         *
         * 스킬을 별도 루프로 두지 않는 이유는 **같은 저울에 올라가야 하기**
         * 때문이다. 골드 축 넷과 스킬 셋은 전부 "골드를 %DPS로 바꾸는" 축이라
         * 재는 자가 같고(GainPerGoldFor), 자가 같으면 정책도 하나여야 한다.
         *
         * 생존 축과 골드 획득 축이 각자의 루프를 갖는 것은 반대의 이유다 -
         * 그쪽은 %DPS로 잴 수 없어서 자가 다르다.
         */
        /**
         * 32단계에 **무기가 한 칸 더 붙었다.** 이유는 위와 같다 - 무기는 골드를
         * %DPS로 바꾸는 축이고, 자가 같으면 정책도 하나여야 한다.
         *
         * 방어구가 여기 없는 것도 같은 규칙이다. 그쪽은 %EHP라 자가 다르고,
         * 그래서 생존 루프에 들어간다.
         */
        private const int DamageAxisCount = 4 + SkillSlotCapacity + 1;

        /** 무기 축의 번호. 스킬 뒤 한 칸 */
        private const int WeaponAxis = 4 + SkillSlotCapacity;

        /**
         * @brief Levels가 들고 있는 스킬 칸 수.
         *
         * SkillCatalog가 이보다 많은 스킬을 들고 오면 뒤쪽이 조용히 무시된다 -
         * 시뮬레이션만 없는 DPS로 계산하게 되므로 테스트가 못 박는다
         * (Simulation_HasASlotForEverySkill).
         */
        public const int SkillSlotCapacity = 3;

        private static void Advance(ref Levels levels, int axis)
        {
            switch (axis)
            {
                case 0: levels.Power = levels.P + 1; return;
                case 1: levels.Speed = levels.S + 1; return;
                case 2: levels.CritRate = levels.R + 1; return;
                case 3: levels.CritDamage = levels.D + 1; return;
                default:
                    if (axis == WeaponAxis) { AdvanceWeapon(ref levels); return; }

                    int index = axis - 4;
                    levels.SetSkillLevel(index, levels.SkillLevel(index) + 1);
                    return;
            }
        }

        // ---------------------------------------------------------------- 32단계: 장비

        /**
         * @brief 장비 한 칸을 올린다. **단련이 남아 있으면 단련, 아니면 등급업.**
         *
         * 이 우선순위는 선택이 아니라 규칙이다 - 등급업은 단련을 끝까지 올린
         * 뒤에만 열린다(EquipmentCurve.CanUpgradeGrade). 게임 쪽과 같은 조건을
         * 여기서도 읽는 것이 요점이고, 상수를 쓰지 않는 이유는 QuestCatalog가
         * 기준 스테이지를 손으로 적었다가 물린 것과 같다.
         *
         * 등급업이면 보석을 여기서 뺀다. 골드는 부르는 쪽이 이미 뺐다 -
         * 두 재화의 지출 지점이 갈리는 것이 마음에 걸리지만, 골드는 효율
         * 비교에 쓰이고 보석은 안 쓰여서(재화가 다르면 나눌 수 없다) 자리가
         * 다를 수밖에 없다. 대신 조건 판정이 한 함수 안에 있다.
         */
        private static void AdvanceWeapon(ref Levels levels)
        {
            if (EquipmentCurve.CanTemper(levels.Wg, levels.Wl)) { levels.WeaponLevel = levels.Wl + 1; return; }

            levels.GemsSpent += EquipmentCurve.GradeGemCost(levels.Wg);
            levels.WeaponGrade = levels.Wg + 1;
        }

        private static void AdvanceArmor(ref Levels levels)
        {
            if (EquipmentCurve.CanTemper(levels.Ag, levels.Al)) { levels.ArmorLevel = levels.Al + 1; return; }

            levels.GemsSpent += EquipmentCurve.GradeGemCost(levels.Ag);
            levels.ArmorGrade = levels.Ag + 1;
        }

        /**
         * @brief 이 슬롯을 한 칸 올릴 수 있는가. 살 수 있으면 골드 비용을 낸다.
         *
         * 막히는 경로가 넷이다.
         *
         *   해금 전         대장간이 안 열렸다 (EquipmentCurve.UnlockStage)
         *   비교군          policy.SkipEquipment / SkipGradeUps
         *   상한           5등급 마지막 칸
         *   보석 부족       GemsFromQuestsOnly 에서만 실제로 막힌다
         *
         * 보석을 여기서 확인하는 이유는, 못 사는 것을 저울에 올리면 구매 정책이
         * 그 칸을 고르고 나서 아무것도 안 하는 상태가 되기 때문이다. 21단계에
         * 골드 축 해금을 시뮬레이션에 안 넣어 겪은 것과 같은 자리다.
         */
        private static bool TryEquipmentCost(Levels levels, bool weapon, int currentStage,
                                             Policy policy, out double cost)
        {
            cost = 0d;
            if (policy.SkipEquipment || policy.NeutralizeEquipment) return false;
            if (!EquipmentCurve.IsUnlockedAt(currentStage)) return false;

            int grade = weapon ? levels.Wg : levels.Ag;
            int level = weapon ? levels.Wl : levels.Al;
            double baseCost = weapon ? WeaponBaseCost : ArmorBaseCost;

            if (EquipmentCurve.CanTemper(grade, level))
            {
                cost = EquipmentCurve.TemperCost(baseCost, level);
                return true;
            }

            if (policy.SkipGradeUps) return false;
            if (!EquipmentCurve.CanUpgradeGrade(grade, level)) return false;

            if (policy.GemsFromQuestsOnly
                && levels.GemsAvailable < EquipmentCurve.GradeGemCost(grade)) return false;

            cost = EquipmentCurve.GradeGoldCost(baseCost, grade);
            return true;
        }

        /**
         * @brief 방어구 한 칸의 골드당 %EHP. 생존 루프가 체력·회복과 나란히 잰다.
         *
         * 증폭을 곱해서 비교하는 것은 체력 축과 같은 이유다 - levels.MaxHealth
         * 에는 이미 증폭과 방어구가 곱해져 있으므로, 여기만 곡선값을 쓰면 비교가
         * 뒤집힌다. 16단계에 회복 축에서 실제로 겪은 자리다.
         */
        private static double ArmorGainPerGold(Levels levels, int currentStage, Policy policy,
                                               out double cost)
        {
            if (!TryEquipmentCost(levels, false, currentStage, policy, out cost) || cost <= 0d) return 0d;

            var after = levels;
            AdvanceArmor(ref after);

            double current = levels.EffectiveHealth;
            if (current <= 0d) return 0d;

            double next = SurvivalEfficiency.EffectiveHealth(after.MaxHealth, after.RegenFraction);
            return (next / current - 1d) / cost;
        }

        /**
         * @brief 두 슬롯의 단련 첫 칸 비용. 카탈로그에서 읽는다.
         *
         * 상수로 적지 않는 이유는 EquipmentCatalog가 단일 출처여야 하기
         * 때문이다. 여기 숫자를 복사해 두면 그 순간부터 시뮬레이션은 게임이
         * 아니라 자기 자신을 검사한다.
         */
        private static double WeaponBaseCost { get { return WeaponSpec.TemperBaseCost; } }
        private static double ArmorBaseCost { get { return ArmorSpec.TemperBaseCost; } }

        internal static EquipmentSpec WeaponSpec
        {
            get { return EquipmentCatalog.Slots[EquipmentCatalog.IndexOf(EquipmentCatalog.WeaponId)]; }
        }

        internal static EquipmentSpec ArmorSpec
        {
            get { return EquipmentCatalog.Slots[EquipmentCatalog.IndexOf(EquipmentCatalog.ArmorId)]; }
        }

        /**
         * @brief 획득 축을 회수 시간이 밴드 안일 때만 산다.
         *
         * ## 왜 생존 다음, 화력 앞인가
         *
         * 생존보다 뒤인 이유는 생존이 절대 조건이기 때문이다 - 죽으면 그 스테이지에
         * 아예 들어갈 수 없고, 그때 골드 수입이 몇 배든 의미가 없다.
         *
         * 화력보다 앞인 이유는 이 축이 **화력을 사는 속도 자체를 올리기** 때문이다.
         * 회수 시간이 5분이라는 것은 5분 뒤부터 같은 골드로 화력을 더 산다는 뜻이라,
         * 화력을 먼저 사면 그 5분만큼 손해다. 실제 플레이어도 "골드 벌이부터 올리고
         * 나머지"를 한다.
         *
         * 다만 그 순서가 성립하려면 **회수가 밴드 안**이어야 한다. 무조건 이 축부터
         * 사면 골드가 있는 한 계속 사게 되고, 그것이 스노볼이다. 임계값이 그 선을
         * 긋는다(GoldGainEfficiency.BuyThresholdSeconds).
         *
         * 회수 시간은 살 때마다 나빠지므로(비용 x1.15 대 수입 x1.04) 이 루프는
         * 자연히 멈춘다. 그것이 이 축의 자기 제한이고, 별도의 개수 제한이 필요 없는
         * 이유다.
         */
        private static void BuyGoldGain(ref Levels levels, ref double purse, double goldPerSecond,
                                        ref double purchasePayback)
        {
            // 파밍 속도를 아직 모르는 시점(첫 처치 전)에는 사지 않는다. 0으로
            // 재면 회수 시간이 무한대라 어차피 안 사지만, 명시적으로 둔다
            if (goldPerSecond <= 0d) return;

            for (int guard = 0; guard < 100000; guard++)
            {
                if (levels.Gd >= GoldGainCurve.MaxLevel) break;
                if (!GoldGainEfficiency.WorthBuying(levels.Gd, goldPerSecond)) break;

                double cost = GoldGainCurve.CostAtLevel(levels.Gd);
                if (cost > purse) break;

                // 사는 순간의 회수 시간을 남긴다. 밴드 검사가 재야 하는 값이
                // 이것이다 - 다 사고 난 뒤의 회수 시간은 무한대라 아무것도
                // 증명하지 않는다. 스테이지 안에서 여러 번 사면 **가장 짧은**
                // 것을 남긴다. 스노볼은 가장 이득이 컸던 한 칸이 만든다
                double payback = GoldGainEfficiency.PaybackSeconds(levels.Gd, goldPerSecond);
                if (payback < purchasePayback) purchasePayback = payback;

                purse -= cost;
                levels.Gold = levels.Gd + 1;

                // 사고 나면 수입이 늘어난다. 그 늘어난 값으로 다음 칸의 회수
                // 시간을 재야 한다 - 안 그러면 자기 제한이 한 박자 늦게 걸린다
                goldPerSecond *= GoldGainCurve.CappedValueAtLevel(levels.Gd)
                                 / GoldGainCurve.CappedValueAtLevel(levels.Gd - 1);
            }
        }

        /** 살 수 있으면 비용을 낸다. 상한에 닿았거나 아직 안 열린 축은 false */
        private static bool TryCost(Levels levels, int axis, int currentStage, Policy policy,
                                    out double cost)
        {
            cost = 0d;
            if (axis == WeaponAxis)
                return TryEquipmentCost(levels, true, currentStage, policy, out cost);

            switch (axis)
            {
                case 0:
                    cost = AttackPowerCurve.CostAtLevel(levels.P);
                    return true;
                case 1:
                    if (levels.S >= AttackSpeedCurve.MaxLevel) return false;
                    cost = AttackSpeedCurve.CostAtLevel(levels.S);
                    return true;
                case 2:
                    if (levels.R >= CritRateCurve.MaxLevel) return false;
                    cost = CritRateCurve.CostAtLevel(levels.R);
                    return true;
                case 3:
                    cost = CritDamageCurve.CostAtLevel(levels.D);
                    return true;
                default:
                    int index = axis - 4;
                    if (index >= SkillCatalog.Count) return false;

                    // 해금은 캐릭터 레벨이 정한다. 게이트를 시뮬레이션 쪽에
                    // 안 넣으면 계산만 오의를 미리 쓰고, 그 차이가 그대로
                    // "보고서의 밴드와 실제 플레이가 다르다"가 된다 - 골드
                    // 획득 축에서 21단계에 한 번 겪은 자리다
                    if (!SkillCatalog.IsUnlockedAt(index, levels.L)) return false;

                    int skillLevel = levels.SkillLevel(index);
                    if (skillLevel >= SkillCurve.MaxLevel) return false;

                    cost = SkillCurve.CostAtLevel(SkillCatalog.Skills[index].BaseCost, skillLevel);
                    return true;
            }
        }

        /**
         * @brief 이 축을 한 레벨 올릴 때의 골드당 DPS 증가율.
         *
         * 상한이 적용된 실제 스탯으로 잰다. UpgradeEfficiency는 상한을 걷어낸
         * 곡선으로 재는데(형태를 보는 지표라서), 여기서는 반대로 플레이어가 실제로
         * 얻는 것을 알아야 한다 - 상한에 막힌 축을 사는 것은 골드 낭비다.
         */
        private static double GainPerGoldFor(Levels levels, int axis, int currentStage, Policy policy)
        {
            double cost;
            if (!TryCost(levels, axis, currentStage, policy, out cost) || cost <= 0d) return 0d;

            var before = levels.Stats;

            // 값 복사다. Levels가 struct이고 스킬 레벨도 낱개 필드라 원본이
            // 딸려 오르지 않는다 - 배열이었으면 여기서 조용히 올랐을 것이다
            var after = levels;
            Advance(ref after, axis);

            double dpsBefore = before.ExpectedDps;
            if (dpsBefore <= 0d) return 0d;

            return (after.Stats.ExpectedDps / dpsBefore - 1d) / cost;
        }
    }
}
