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

            // ------------------------------------------------------------ 33단계

            /** 이 스테이지 끝의 전직 티어. 0 = 로닌 */
            public int EvolutionTier;

            /** 전직이 공격력에 곱하고 있는 배수 */
            public double EvolutionAttack;

            /** 전직이 최대 체력에 곱하고 있는 배수 */
            public double EvolutionHealth;

            // ------------------------------------------------------------ 동료

            /** 이 스테이지 끝에 데리고 있는 동료 수 (0~3) */
            public int PetsOwned;

            /** 동료별 레벨. 카탈로그 순서, 미보유면 0 */
            public int[] PetLevels;

            /** 동료들이 DPS에 더하고 있는 합산 몫 (0~0.5) */
            public double PetBonus;

            /** 예전 이름 호환. 보유 동료가 있는가 */
            public bool PetOwned { get { return PetsOwned > 0; } }

            // ------------------------------------------------------------ 43단계

            /** 심화 축 레벨. 치명타 100% 전에는 1(없는 것과 같다)에 머문다 */
            public int TranscendLevel;
            public int ComboLevel;

            // ------------------------------------------------------------ 44단계

            /** 요도 넷의 티어. 카탈로그 순서, 미봉인이면 0 */
            public int[] YodoTiers;

            /** 봉인한 요도 수 (0~4). 4면 오니키리 완성 */
            public int YodoSealed;

            /** 요도 + 세트 보너스가 공격력에 곱하고 있는 배수 */
            public double YodoMultiplier;

            /** 아직 봉인·합성에 쓰이지 않고 남아 있는 혼 (종류별 합) */
            public int SoulsHeld;

            /** 이 스테이지 끝의 파편 잔량 */
            public int Shards;

            // ------------------------------------------------------------ 46단계

            /**
             * @brief 이 스테이지 끝까지 돌린 뽑기 수 (기댓값이라 소수다).
             *
             * 밴드보다 이쪽이 먼저 멈춘다 - 상한(GachaCurve.LeadTiers)에 닿으면
             * 더 돌 이유가 없어 값이 평평해지고, 그것이 곧 "뽑기가 팔 것이
             * 남아 있는가"의 지표다. 계속 자라야 정상이다.
             */
            public double GachaPulls;

            // ------------------------------------------------------------ 47단계

            /** 요도 넷의 혼격 (0~4). 무과금은 st1~∞ 내내 전부 0이다 */
            public int[] YodoRarities;

            /** 전설 妖刀의 사본 수. 0 = 미보유, 그 위는 돌파 */
            public int[] LegendaryCopies;

            // ------------------------------------------------------------ 45단계

            /**
             * @brief 오의별 상성 배수. 카탈로그 순서, 상성이 없으면 1.
             *
             * 배열인 이유는 이 축의 요점이 **오의마다 다르다**는 것이기
             * 때문이다. 합이나 평균으로 접으면 빌드가 표에서 사라진다.
             */
            public double[] SkillAffinity;

            /** 영체가 만드는 초당 환산 공격 횟수 */
            public double SpiritRate;

            /**
             * @brief 상성과 영체가 이 스테이지의 DPS에 곱하고 있는 배수.
             *
             * 밴드보다 이쪽이 먼저 움직인다 - 천장이 아직 안 뚫렸는데 이
             * 값이 기대 곡선(YodoCurve.ExpectedPowerFactorAtStage)보다 크게
             * 앞서 있으면, 다음 계수 변경에서 뚫린다는 뜻이다.
             */
            public double YodoPowerFactor;

            /** 초월 배수와 연격 확률. 표와 죽은 버튼 검사가 읽는다 */
            public double TranscendMultiplier;
            public double ComboChance;

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
             * @brief 전직을 한 번도 하지 않는다. 33단계의 비교군 (a).
             *
             * 죽은 버튼 검사의 한쪽이다. 보정(StageCurve.EvolutionCompensation)은
             * 걷어내지 않는다 - 스테이지의 함수라 플레이어를 구분하지 못하고,
             * 그래서 이 비교군이 재는 것은 **"안 사면 손해인가"**다.
             * Policy.SkipEquipment와 같은 자다.
             *
             * 이 세계는 후반에서 실제로 나쁘다 - st50 여유가 1.0 언저리까지
             * 내려온다. 그것이 과금 지향 재유도의 의도라는 것은
             * StageCurve.EvolutionMarginExponent 주석에 있다.
             */
            public bool SkipEvolution;

            /**
             * @brief 전직도 보정도 **둘 다** 없는 세계. 32단계 이전 그대로다.
             *
             * SkipEvolution과 다른 질문("이 축을 게임에 넣은 것이 이득이었는가")을
             * 재는 자다. 둘을 나눠 두는 이유는 Policy.SkipGoldGain /
             * NeutralizeGoldAxis 주석에 있다 - 20단계가 두 답을 섞어 읽었다.
             */
            public bool NeutralizeEvolution;

            /**
             * @brief 펫을 한 번도 사지 않는다 (해금도 레벨도). 펫 스텝의 비교군 (a).
             *
             * 죽은 버튼 검사의 한쪽이다. 보정(StageCurve.PetCompensation)은
             * 걷어내지 않는다 - 스테이지의 함수라 플레이어를 구분하지 못하고,
             * 그래서 이 비교군이 재는 것은 **"안 사면 손해인가"**다.
             * Policy.SkipEquipment / SkipEvolution과 같은 자다.
             */
            public bool SkipPets;

            /**
             * @brief 펫도 보정도 **둘 다** 없는 세계. 33단계까지의 게임 그대로다.
             *
             * SkipPets와 다른 질문("이 축을 게임에 넣은 것이 이득이었는가")을
             * 재는 자다. 둘을 나눠 두는 이유는 Policy.SkipGoldGain /
             * NeutralizeGoldAxis 주석에 있다 - 20단계가 두 답을 섞어 읽었다.
             *
             * 이 비교군이 하나 더 하는 일이 있다 - **코리더(st1~30) 불변 검사.**
             * 펫 스텝 전과 같은 세계이므로, 이 정책의 st1~30 여유가 기존 밴드
             * 그대로면 펫이 코리더를 안 건드렸다는 증거다.
             */
            public bool NeutralizePets;

            /**
             * @brief 심화 축(초월·연격)을 한 번도 사지 않는다. 43단계의 비교군 (a).
             *
             * 죽은 버튼 검사의 한쪽이다. 보정(StageCurve.MasteryCompensation)은
             * 걷어내지 않는다 - 스테이지의 함수라 플레이어를 구분하지 못하고,
             * 그래서 이 비교군이 재는 것은 **"안 사면 손해인가"**다.
             * Policy.SkipEquipment / SkipEvolution / SkipPets와 같은 자다.
             *
             * 치명타 확률의 벽(60% -> 100%) 구간은 산다 - 그것은 기존 축의
             * 연장이지 심화 축이 아니다.
             */
            public bool SkipMastery;

            /**
             * @brief 발도 개방(43단계) 전체가 없는 세계 - **42단계 그대로다.**
             *
             * 심화 축 둘뿐 아니라 **치명타 확률의 벽 위 구간(Lv.98+)도 없다.**
             * 43단계가 더한 것이 그 셋이고, 보정도 함께 걷어낸다. 이 정책의
             * st1~200 여유가 42단계 밴드 그대로면 개방이 기존 구간을 안
             * 건드렸다는 증거다 - NeutralizePets의 코리더 불변 검사와 같은 자다.
             */
            public bool NeutralizeMastery;

            /**
             * @brief 요도를 한 자루도 벼리지 않는다. 44단계의 비교군 (a).
             *
             * 죽은 버튼 검사의 한쪽이다. 보정(StageCurve.YodoCompensation)은
             * 걷어내지 않는다 - 스테이지의 함수라 플레이어를 구분하지 못하고,
             * 그래서 이 비교군이 재는 것은 **"안 벼리면 손해인가"**다.
             * Policy.SkipEquipment / SkipEvolution / SkipPets와 같은 자다.
             *
             * 이 축은 골드가 아니라 진행으로 들어오므로 "안 산다"가 사실은
             * 성립하지 않는다(혼은 보스를 잡으면 떨어진다). 그래서 이 정책이
             * 재는 것은 정확히 **"봉인 버튼을 한 번도 누르지 않은 플레이어"**다 -
             * 화면에 뜬 버튼이 실제로 무엇을 하는가를 묻는 자다.
             */
            public bool SkipYodo;

            /**
             * @brief 요도도 보정도 **둘 다** 없는 세계 - **43단계 그대로다.**
             *
             * SkipYodo와 다른 질문("이 축을 게임에 넣은 것이 이득이었는가")을
             * 재는 자다. 둘을 나눠 두는 이유는 Policy.SkipGoldGain /
             * NeutralizeGoldAxis 주석에 있다 - 20단계가 두 답을 섞어 읽었다.
             *
             * 이 비교군이 하나 더 하는 일이 있다 - **조율 구간(st1~50) 불변
             * 검사.** 요도 스텝 전과 같은 세계이므로, 이 정책의 st1~50 여유가
             * 기존 밴드와 **비트 단위로** 같으면 요도가 코리더도 가속 구간도
             * 안 건드렸다는 증거다. NeutralizePets의 코리더 불변 검사와 같은
             * 자이고, 이쪽은 구조상 st50까지 기본 정책과도 같아야 한다.
             */
            public bool NeutralizeYodo;

            // ------------------------------------------------------------ 45단계

            /**
             * @brief 혼별 오의 상성이 없는 세계. 45단계의 비교군 (a).
             *
             * 요도는 그대로 벼려지고 티어 배수도 그대로인데, **상성만**
             * 걸리지 않는다. 보정은 걷어내지 않는다 - 스테이지의 함수라
             * 플레이어를 구분하지 못하고, 그래서 이 비교군이 재는 것은
             * "상성이 실제로 그 오의를 움직이는가"다.
             *
             * 지시가 요구한 죽은 버튼 검사("상성 무력화 시 오의 원복")가
             * 정확히 이 정책이다 - 이 세계의 오의 배율이 44단계 값과 같아야
             * 하고, 그렇지 않으면 상성이 어딘가 다른 경로로도 새고 있다.
             */
            public bool SkipAffinity;

            /**
             * @brief 영체 소환이 없는 세계. 45단계의 비교군 (b).
             *
             * 상성과 나눠 둔 이유는 20단계가 두 답을 섞어 읽은 그 자리다.
             * 둘 다 요도 티어에서 파생되므로 한 정책으로 묶으면 "요도를 더
             * 쓴 것이 이득인가"만 답이 나오고, **둘 중 어느 쪽이 진행을
             * 움직였는가**는 영영 알 수 없다.
             *
             * 버스트가 죽은 연출인지 재는 자이기도 하다 - 화면에 큰 것이
             * 나타났는데 진행이 안 변하면 그것은 소환이 아니라 장식이다.
             */
            public bool SkipSpirit;

            /**
             * @brief 상성도 영체도 보정도 없는 세계 - **44단계 그대로다.**
             *
             * 요도 티어와 그 보정(StageCurve.YodoBladeCompensation)은 남고
             * 45단계가 얹은 층만 통째로 벗겨진다. 43단계의
             * NeutralizeMastery가 42단계를 재현한 것과 같은 자이고, 다른
             * 점은 **한 축 위에 층이 쌓였다**는 것이다 - 그래서 보정을
             * 통째로 걷는 대신 새 겹만 나눠 걷는다(StageCurve가 둘로 갈린
             * 이유).
             *
             * 이 정책의 심층 밴드가 44단계 실측(바닥 1.81/1.49/1.21,
             * 천장 12.10/8.72/6.91)과 같으면 새 두 축이 기존 구간을 안
             * 건드렸다는 증거다.
             */
            public bool NeutralizeYodoPower;

            /**
             * @brief 보석으로 파편을 사지 않는다. **보석 소비처의 죽은 버튼 검사.**
             *
             * 기본 정책은 보석이 무제한이므로 파편이 모자랄 때마다 묶음을
             * 사고, 그래서 요도가 언제나 혼에만 막힌다(기대 곡선의 정의).
             * 이것을 켜면 파편이 정예 드랍에만 의존하고, 두 세계의 티어 차이가
             * 곧 **그 버튼이 실제로 시간을 앞당기는가**의 답이다.
             *
             * 무과금(GemsFromQuestsOnly)에게는 이 둘이 거의 같아야 한다 -
             * 그가 이 버튼을 누를 보석이 없기 때문이고, 그것이 "f2p 바닥은
             * 촉매 없이 선다"는 이 스텝의 안전선이다.
             */
            public bool SkipShardPacks;

            /**
             * @brief 뽑기를 한 번도 돌리지 않는다. **46단계의 죽은 버튼 검사.**
             *
             * 뽑기가 파는 것은 파편(촉매와 같은 것)과 혼 정수(드랍 일정을
             * 한 바퀴 앞당기는 것) 둘이다. 앞의 것은 촉매가 이미 하고 있으므로
             * 이 정책이 실제로 재는 것은 **뒤의 것**이다 - 켜면 티어가 드랍
             * 일정 그대로가 되고, 그 차이가 곧 "뽑기가 진행을 가속하는가"의
             * 답이다.
             *
             * 무과금(GemsFromQuestsOnly)에게는 이 정책이 거의 아무것도 바꾸지
             * 않아야 한다 - 그의 보석은 코어 진행에 다 배정돼 있고, 뽑기 접근은
             * 일일 무료(GachaCurve.FreePullsPerDay)에서 오는데 시뮬레이션에는
             * 달력이 없다. 그것이 **f2p 비잠식**의 실측이다.
             */
            public bool SkipGacha;

            /**
             * @brief 무과금이 보석을 **뽑기에 먼저 쓴다**. 잠식을 재는 반례.
             *
             * 기본 규칙은 반대다 - 시뮬레이션의 무과금은 보석을 장비 등급·동료
             * 해금·전직에만 쓰고 뽑기에는 손대지 않는다(그것이 이 게임의 정답
             * 이라고 GachaCurve.FreePullsPerDay 주석이 적었다). 이 정책은 그
             * 정답을 어겼을 때 무엇이 무너지는지를 **숫자로** 남기기 위한
             * 자이고, 설계의 근거가 말이 아니라 실측이 되게 한다.
             */
            public bool GachaBeforeCore;

            // ------------------------------------------------------------ 47단계

            /**
             * @brief 상위 혼(★4)이 안 나오는 세계. **혼격의 죽은 버튼 검사.**
             *
             * 뽑기는 그대로 돌고 파편도 혼 정수도 그대로 들어오는데 혼격만
             * 안 오른다. SkipGacha와 나눠 둔 이유는 20단계가 두 답을 섞어
             * 읽은 그 자리다 - 저쪽은 "뽑기가 진행을 가속하는가"이고
             * 이쪽은 **"사다리의 새 칸이 실제로 무언가를 움직이는가"**다.
             *
             * 켜면 뽑기의 재고가 혼 정수 하나로 돌아가므로 46단계의 정지
             * 지점(st100 평평화)도 함께 재현된다 - 이 스텝이 재고를 하나
             * 더 얹었다는 주장의 실측이 그것이다.
             */
            public bool SkipRarity;

            /**
             * @brief 전설 妖刀(★5)가 안 나오는 세계.
             *
             * 혼격과 나눠 둔 이유는 크기와 성질이 다르기 때문이다 - 혼격은
             * 200스테이지에 걸쳐 조금씩 오르고 전설은 200회에 한 번 통째로
             * 온다. 한 정책으로 묶으면 "사다리를 넣은 것이 이득인가"만
             * 나오고, 둘 중 무엇이 천장을 밀었는지는 알 수 없다.
             */
            public bool SkipLegendary;

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

            // 45단계의 두 축은 정책이 곧 세계의 규칙이라 Levels가 들고 간다
            // (Levels.NoAffinity 주석). 요도 자체가 없는 세계에서는 티어가
            // 0이라 자동으로 꺼지지만, 명시해 두면 "왜 0인가"를 두 번 묻지 않는다
            levels.NoAffinity = policy.SkipAffinity || policy.NeutralizeYodoPower
                             || policy.SkipYodo || policy.NeutralizeYodo;
            levels.NoSpirit = policy.SkipSpirit || policy.NeutralizeYodoPower
                           || policy.SkipYodo || policy.NeutralizeYodo;

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

                // 온보딩 완화(st1~5 잡몹 전용)를 지난 값이다. 스포너와 같은 입구
                // (StageCurve.MobHealth)를 써야 시뮬레이션이 화면과 같은 속도를 잰다
                double mobHealth = StageCurve.MobHealth(
                    BigDouble.FromDouble(field.AverageMobHealth), stage).ToDouble();
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
                if (policy.NeutralizeEvolution) bossKill /= StageCurve.EvolutionCompensation(stage);
                if (policy.NeutralizePets) bossKill /= StageCurve.PetCompensation(stage);
                if (policy.NeutralizeMastery) bossKill /= StageCurve.MasteryCompensation(stage);
                if (policy.NeutralizeYodo) bossKill /= StageCurve.YodoCompensation(stage);

                // 45단계는 요도 위에 **층으로** 얹혔다. 44단계의 세계를
                // 재현하려면 새 겹만 벗겨야 하므로 보정도 그 겹만 나눈다 -
                // 티어 보정(YodoBladeCompensation)은 남는다
                if (policy.NeutralizeYodoPower) bossKill /= StageCurve.YodoPowerCompensation(stage);

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

                // 요도(44단계)는 보석 다음, 구매 앞이다. 보석 다음인 이유는
                // 파편 묶음이 그 보석을 쓰기 때문이고, 구매 앞인 이유는 요도
                // 배수가 화력 저울의 분모(현재 DPS)에 들어가기 때문이다 -
                // 게임에서도 봉인은 강화 화면을 열기 전에 끝나 있다
                TryForgeYodo(ref levels, stage, policy);

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

                    EvolutionTier = levels.Ev,
                    EvolutionAttack = levels.EvolutionAttack,
                    EvolutionHealth = levels.EvolutionHealth,

                    PetsOwned = levels.PetsOwnedCount,
                    PetLevels = levels.PetLevelsSnapshot(),
                    PetBonus = levels.PetBonus,

                    TranscendLevel = levels.Tx,
                    ComboLevel = levels.Cx,
                    TranscendMultiplier = TranscendCurve.MultiplierAtLevel(levels.Tx),
                    ComboChance = ComboCurve.ChanceAtLevel(levels.Cx),

                    YodoTiers = levels.YodoTiersSnapshot(),
                    YodoSealed = levels.YodoSealedCount,
                    YodoMultiplier = levels.YodoMultiplier,
                    SoulsHeld = levels.SoulsHeld,
                    Shards = levels.Shards,
                    GachaPulls = levels.GachaPulls,
                    YodoRarities = levels.YodoRaritiesSnapshot(),
                    LegendaryCopies = levels.LegendaryCopiesSnapshot(),

                    SkillAffinity = levels.AffinitySnapshot(),
                    SpiritRate = stats.SpiritRate,
                    YodoPowerFactor = levels.PowerFactor,

                    SkillLevels = levels.SkillLevelsSnapshot(),

                    // 레벨은 levels에서, 비율은 stats에서 온다. 다른 축과 같은
                    // 규칙이다 - 레벨 칸은 보스 보상까지 쓴 뒤의 값이고, DPS 칸은
                    // **보스를 잡을 때** 갖고 있던 값이다. 보스 여유가 후자에서
                    // 나오므로 둘을 섞으면 표의 여유와 표의 DPS가 어긋난다
                    SkillRate = stats.SkillRate,

                    // 괄호 안의 몫이 곧 DPS의 몫이다. 공격력도 치명타도 괄호
                    // 밖에서 양쪽에 똑같이 곱해지므로 약분된다
                    // 45단계에 분모가 한 항 늘었다. 영체도 괄호 안이라
                    // 오의의 **몫**을 줄인다 - 안 넣으면 이 지표가 1을
                    // 넘을 수 있고, "자동 공격이 장식이 되는 길"을 재는
                    // 자로서 뜻을 잃는다
                    SkillDpsShare = stats.AttacksPerSecond + stats.SkillRate + stats.SpiritRate > 0d
                        ? stats.SkillRate
                          / (stats.AttacksPerSecond + stats.SkillRate + stats.SpiritRate)
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

            // ------------------------------------------------------------ 33단계

            /** 전직 티어. 0(로닌)에서 시작한다 - 다른 축의 "레벨 1"과 같은 자리다 */
            public int EvolutionTier;

            public int Ev { get { return EvolutionTier < 0 ? 0 : EvolutionTier; } }

            public double EvolutionAttack { get { return EvolutionCurve.AttackMultiplierAt(Ev); } }
            public double EvolutionHealth { get { return EvolutionCurve.HealthMultiplierAt(Ev); } }

            // ------------------------------------------------------------ 동료

            /**
             * @brief 동료 셋의 보유·레벨. **배열이 아니라 낱개 필드다.**
             *
             * Levels가 struct라서 그렇다 - 스킬 레벨과 같은 이유(배열이면
             * `var after = levels;`가 참조를 복사해 효율 계산이 원본을 올린다).
             *
             * 보유 동료 전원이 함께 출전하므로 보너스는 **합산**이다. 넷째
             * 동료가 생기면 칸을 하나 더 만들어야 한다 - 카탈로그 수와 칸
             * 수가 갈리면 PetTests.Simulation_HasASlotForEveryPet이 잡는다.
             */
            public bool Pet0Owned;
            public bool Pet1Owned;
            public bool Pet2Owned;
            public int Pet0Level;
            public int Pet1Level;
            public int Pet2Level;

            public bool PetOwnedAt(int index)
            {
                switch (index)
                {
                    case 0: return Pet0Owned;
                    case 1: return Pet1Owned;
                    case 2: return Pet2Owned;
                    default: return false;
                }
            }

            public void SetPetOwned(int index)
            {
                switch (index)
                {
                    case 0: Pet0Owned = true; break;
                    case 1: Pet1Owned = true; break;
                    case 2: Pet2Owned = true; break;
                }
            }

            /** 다른 축과 같은 규칙 - 보유 상태의 레벨은 1이 시작값이다 */
            public int PetLevelAt(int index)
            {
                int level;
                switch (index)
                {
                    case 0: level = Pet0Level; break;
                    case 1: level = Pet1Level; break;
                    case 2: level = Pet2Level; break;
                    default: return 1;
                }
                return level < 1 ? 1 : level;
            }

            public void SetPetLevel(int index, int level)
            {
                switch (index)
                {
                    case 0: Pet0Level = level; break;
                    case 1: Pet1Level = level; break;
                    case 2: Pet2Level = level; break;
                }
            }

            public int PetsOwnedCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < PetSlotCapacity; i++)
                        if (PetOwnedAt(i)) count++;
                    return count;
                }
            }

            public int[] PetLevelsSnapshot()
            {
                var levels = new int[PetSlotCapacity];
                for (int i = 0; i < levels.Length; i++)
                    levels[i] = PetOwnedAt(i) ? PetLevelAt(i) : 0;
                return levels;
            }

            /** 출전 중인 동료들의 합산 보너스. 시뮬의 (1 + PetBonus) 축이 이것이다 */
            public double PetBonus
            {
                get
                {
                    double total = 0d;
                    int count = Math.Min(PetCatalog.Count, PetSlotCapacity);
                    for (int i = 0; i < count; i++)
                        if (PetOwnedAt(i)) total += PetCurve.BonusOfPetAt(i, PetLevelAt(i));
                    return total;
                }
            }

            // ------------------------------------------------------------ 43단계

            /**
             * @brief 심화 축 둘. 다른 축과 같은 규칙 - 레벨 1이 시작값이고,
             * 그 값(배수 1 / 확률 0)은 없는 것과 같다.
             */
            public int Transcend;
            public int Combo;

            public int Tx { get { return Transcend < 1 ? 1 : Transcend; } }
            public int Cx { get { return Combo < 1 ? 1 : Combo; } }

            // ------------------------------------------------------------ 44단계: 요도

            /**
             * @brief 요도 넷의 티어와 아직 안 쓴 혼. **배열이 아니라 낱개 필드다.**
             *
             * Levels가 struct라서 그렇다 - 스킬·동료와 같은 이유(배열이면
             * `var after = levels;`가 참조를 복사해 효율 계산이 원본을 올린다).
             *
             * **티어는 0에서 시작한다.** 다른 축이 전부 "레벨 1이 시작값"인
             * 것과 다른데, 여기서는 0이 "아직 없다"라는 실제 상태이기
             * 때문이다 - 전직 티어(0 = 로닌)와 같은 자리다.
             *
             * 다섯째 요도가 생기면 칸을 하나 더 만들어야 한다. 카탈로그 수와
             * 칸 수가 갈리면 YodoTests.Simulation_HasASlotForEveryBlade가 잡는다.
             */
            public int Yodo0Tier, Yodo1Tier, Yodo2Tier, Yodo3Tier;
            public int Soul0, Soul1, Soul2, Soul3;

            /**
             * @brief 요도 넷의 혼격 (47단계). 티어와 같은 이유로 낱개 필드다.
             *
             * 무과금 경로에서는 넷 다 영원히 0이다 - 자연 출처가 없고
             * 시뮬레이션의 무과금은 뽑기를 안 돌린다. 그 사실이 f2p 바닥의
             * 비트 불변을 구조로 지킨다.
             */
            public int Rarity0, Rarity1, Rarity2, Rarity3;

            /** 전설 妖刀 둘의 사본 수 (47단계). 셋째가 생기면 칸을 늘린다 */
            public int Legend0, Legend1;

            /** 파편 잔량. 정예 드랍 + 상한 요도의 남는 혼 + 보석 묶음 + 뽑기 */
            public int Shards;

            // ------------------------------------------------------------ 46단계

            /** 지금까지 돌린 뽑기 수. 기댓값 단위라 정수가 아니다 */
            public double GachaPulls;

            /**
             * @brief 뽑기 파편의 소수 자리.
             *
             * 파편은 정수인데 기대 수확은 소수라(한 번에 11.54) 버리면
             * 스테이지마다 조금씩 새고, 그 누적이 후반 티어 하나가 된다.
             * 남은 자리를 들고 있다가 1이 차면 넘긴다.
             */
            public double GachaShardCarry;

            /**
             * @brief 다음 혼 정수까지의 진행 (뽑기 수).
             *
             * 게임 쪽 천장 카운터(GachaSystem.pityCounter)와 같은 자리이지만
             * 단위가 다르다 - 저쪽은 정수이고 이쪽은 기댓값이다. 스테이지
             * 경계에서 버리면 뽑기가 잦은 후반일수록 조금씩 새고, 그 누적이
             * 티어 하나가 된다(파편 소수 자리와 같은 이유).
             */
            public double GachaEssenceProgress;

            /**
             * @brief 다음 ★4·★5까지의 진행. **단위가 46단계와 뒤집혔다.**
             *
             * 저쪽은 "몇 번 뽑았는가"를 세고 주기와 비교했고, 여기는
             * **확률 질량**을 모아 1이 차면 하나를 낸다. 뒤집은 이유는
             * 사다리가 셋이 됐기 때문이다 - 세 주기를 각자 세면 어느 것이
             * 어디까지 왔는지가 세 단위로 갈리는데, 질량은 셋 다 같은
             * 단위(0~1)라 한눈에 대조된다.
             *
             * 값 자체는 같다. ★3의 질량이 EffectiveEssenceChance이고
             * 그 역수가 46단계의 ExpectedPullsPerEssence다.
             */
            public double GachaRarityProgress;
            public double GachaLegendProgress;

            /**
             * @brief 혼 정수가 지금 갈 자루. 없으면 -1.
             *
             * 판정은 GachaCurve가 하고 여기서는 값만 모아 넘긴다 - 시뮬레이션과
             * 게임(YodoSystem.TryTakeEssence)이 **같은 함수**를 지나야 상한이
             * 두 곳에서 갈리지 않는다.
             */
            public int EssenceTarget(int frontierStage)
            {
                var tiers = FillScratch();
                for (int i = 0; i < YodoSlotCapacity; i++) SoulScratch[i] = SoulsAt(i);
                return GachaCurve.EssenceTargetFor(frontierStage, tiers, SoulScratch);
            }

            /** 상위 혼(★4)이 갈 자루. 판정은 곡선이 한다 - EssenceTarget과 같은 규칙 */
            public int RarityTarget(int frontierStage)
            {
                return GachaCurve.RarityTargetFor(frontierStage, FillScratch(), FillRarityScratch());
            }

            /** 전설(★5)이 갈 자루 */
            public int LegendaryTarget()
            {
                return GachaCurve.LegendaryTargetFor(FillLegendScratch());
            }

            public int RarityAt(int index)
            {
                switch (index)
                {
                    case 0: return Rarity0;
                    case 1: return Rarity1;
                    case 2: return Rarity2;
                    case 3: return Rarity3;
                    default: return 0;
                }
            }

            public void SetRarity(int index, int value)
            {
                switch (index)
                {
                    case 0: Rarity0 = value; break;
                    case 1: Rarity1 = value; break;
                    case 2: Rarity2 = value; break;
                    case 3: Rarity3 = value; break;
                }
            }

            public int LegendaryAt(int index)
            {
                switch (index)
                {
                    case 0: return Legend0;
                    case 1: return Legend1;
                    default: return 0;
                }
            }

            public void SetLegendary(int index, int value)
            {
                switch (index)
                {
                    case 0: Legend0 = value; break;
                    case 1: Legend1 = value; break;
                }
            }

            public int[] YodoRaritiesSnapshot()
            {
                var values = new int[YodoSlotCapacity];
                for (int i = 0; i < values.Length; i++) values[i] = RarityAt(i);
                return values;
            }

            public int[] LegendaryCopiesSnapshot()
            {
                var values = new int[LegendarySlotCapacity];
                for (int i = 0; i < values.Length; i++) values[i] = LegendaryAt(i);
                return values;
            }

            public int YodoTierAt(int index)
            {
                switch (index)
                {
                    case 0: return Yodo0Tier;
                    case 1: return Yodo1Tier;
                    case 2: return Yodo2Tier;
                    case 3: return Yodo3Tier;
                    default: return 0;
                }
            }

            public void SetYodoTier(int index, int tier)
            {
                switch (index)
                {
                    case 0: Yodo0Tier = tier; break;
                    case 1: Yodo1Tier = tier; break;
                    case 2: Yodo2Tier = tier; break;
                    case 3: Yodo3Tier = tier; break;
                }
            }

            public int SoulsAt(int index)
            {
                switch (index)
                {
                    case 0: return Soul0;
                    case 1: return Soul1;
                    case 2: return Soul2;
                    case 3: return Soul3;
                    default: return 0;
                }
            }

            public void SetSouls(int index, int count)
            {
                switch (index)
                {
                    case 0: Soul0 = count; break;
                    case 1: Soul1 = count; break;
                    case 2: Soul2 = count; break;
                    case 3: Soul3 = count; break;
                }
            }

            public int SoulsHeld
            {
                get
                {
                    int total = 0;
                    for (int i = 0; i < YodoSlotCapacity; i++) total += SoulsAt(i);
                    return total;
                }
            }

            public int YodoSealedCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < YodoSlotCapacity; i++)
                        if (YodoTierAt(i) >= 1) count++;
                    return count;
                }
            }

            public int[] YodoTiersSnapshot()
            {
                var tiers = new int[YodoSlotCapacity];
                for (int i = 0; i < tiers.Length; i++) tiers[i] = YodoTierAt(i);
                return tiers;
            }

            // -------------------------------------------------- 45단계: 상성·영체

            /**
             * @brief 이 세계에 상성·영체가 있는가. **정책을 Levels가 들고 있다.**
             *
             * Stats가 정책을 인자로 받지 않기 때문이다 - 효율 저울
             * (GainPerGoldFor)이 Levels 사본 하나로 전후를 재므로, 그 사본에
             * 세계의 규칙이 함께 실려 있어야 비교가 같은 세계 안에서 일어난다.
             * bool 둘이라 struct 복사 비용도 없다.
             */
            public bool NoAffinity;
            public bool NoSpirit;

            /**
             * @brief 상성 계산에 넘길 티어 배열. **스크래치를 돌려 쓴다.**
             *
             * Levels가 struct라 배열을 필드로 둘 수 없고(사본이 참조를 공유해
             * 효율 계산이 원본을 오염시킨다 - 스킬·동료·요도 칸이 전부 낱개
             * 필드인 이유), 매번 새로 할당하면 Stats 한 번에 배열 하나가
             * 생긴다. Stats는 구매 루프 안에서 축마다 두 번씩 불리므로 그것이
             * 수백만 개다.
             *
             * 그래서 정적 스크래치 하나를 채워 넘긴다. 값을 읽는 쪽
             * (YodoAffinityCurve)이 배열을 보관하지 않으므로 안전하고,
             * **상성 규칙의 단일 출처가 곡선 쪽에 남는다** - 여기서 직접
             * 계산하면 같은 산수가 두 곳에 살고 그 둘은 반드시 갈린다.
             */
            private int[] FillScratch()
            {
                for (int i = 0; i < YodoSlotCapacity; i++) YodoScratch[i] = YodoTierAt(i);
                return YodoScratch;
            }

            /** 혼격 스크래치 (47단계). FillScratch와 같은 규칙, 다른 배열 */
            private int[] FillRarityScratch()
            {
                for (int i = 0; i < YodoSlotCapacity; i++) RarityScratch[i] = RarityAt(i);
                return RarityScratch;
            }

            /** 전설 사본 스크래치 (47단계) */
            private int[] FillLegendScratch()
            {
                for (int i = 0; i < LegendarySlotCapacity; i++) LegendScratch[i] = LegendaryAt(i);
                return LegendScratch;
            }

            /** skillIndex번 오의가 받는 상성 배수. 상성이 없는 세계에서는 1 */
            public double AffinityFor(int skillIndex)
            {
                if (NoAffinity) return 1d;
                return YodoAffinityCurve.FactorForSkill(
                    skillIndex, FillScratch(), FillRarityScratch(), FillLegendScratch());
            }

            /** 영체의 초당 환산 기여. 봉인한 자루들의 평균 배율 / 쿨다운 */
            public double SpiritRate
            {
                get
                {
                    return NoSpirit ? 0d
                         : YodoSpiritCurve.RateFor(
                               FillScratch(), FillRarityScratch(), FillLegendScratch());
                }
            }

            /** 표에 찍는 오의별 상성. 스냅샷이라 여기서만 배열을 만든다 */
            public double[] AffinitySnapshot()
            {
                var values = new double[SkillSlotCapacity];
                int count = Math.Min(SkillCatalog.Count, SkillSlotCapacity);
                for (int i = 0; i < count; i++) values[i] = AffinityFor(i);
                for (int i = count; i < values.Length; i++) values[i] = 1d;
                return values;
            }

            /**
             * @brief 상성·영체가 지금 DPS에 곱하고 있는 배수.
             *
             * 기대 곡선(YodoCurve.ExpectedPowerFactorAtStage)과 **같은 정의**여야
             * 보정이 실제를 따라간다. 저쪽은 상한 값으로 닫힌 식을 쓰고 여기는
             * 실제 레벨을 쓰는데, 심층 구간에서는 둘이 같다 - 그 사실 자체를
             * YodoPowerTests가 잰다.
             */
            public double PowerFactor
            {
                get
                {
                    double attack = AttackSpeedCurve.CappedValueAtLevel(S);
                    double plain = 0d;
                    int count = Math.Min(SkillCatalog.Count, SkillSlotCapacity);
                    for (int i = 0; i < count; i++)
                        plain += SkillCatalog.RateAt(i, SkillLevel(i), L);

                    double before = attack + plain;
                    if (before <= 0d) return 1d;
                    return (attack + SkillRate + SpiritRate) / before;
                }
            }

            /**
             * @brief 요도 축이 공격력에 곱하는 배수. **곱의 순서는 곡선이 안다.**
             *
             * 티어 x 세트 x 혼격 x 전설이고, 47단계에 뒤의 둘이 붙었다.
             * 여기서 다시 곱하지 않고 YodoCurve.AttackFactorFor를 지나는
             * 이유는 게임 쪽(YodoSystem.AttackMultiplier)이 같은 함수를
             * 지나기 때문이다 - 같은 상태에서 두 값이 갈리면 시뮬레이션이
             * 재는 것이 화면의 것이 아니게 된다.
             */
            public double YodoMultiplier
            {
                get
                {
                    return YodoCurve.AttackFactorFor(
                        FillScratch(), FillRarityScratch(), FillLegendScratch());
                }
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
                    // 43단계부터 아홉 축이다. 게임 쪽 TotalLevels가 심화 축을
                    // 세므로 여기도 세야 업적이 같은 순간에 열린다
                    return Math.Max(1, Power) + Math.Max(1, Speed)
                         + Math.Max(1, CritRate) + Math.Max(1, CritDamage)
                         + Math.Max(1, Health) + Math.Max(1, Regen)
                         + Math.Max(1, Gold)
                         + Math.Max(1, Transcend) + Math.Max(1, Combo);
                }
            }

            /**
             * @brief 지금 열려 있는 오의들이 만드는 초당 환산 공격 횟수.
             *
             * **45단계부터 오의마다 상성이 곱해진다.** 합을 낸 뒤에 한 번
             * 곱하지 않는 이유는 상성이 오의마다 다르기 때문이다 - 등롱은
             * 귀참에만, 흑야는 셋 다에 걸린다. 게임 쪽도 같은 자리에서
             * 같은 값을 곱한다(SkillSystem.MultiplierOf).
             */
            public double SkillRate
            {
                get
                {
                    double rate = 0d;
                    int count = Math.Min(SkillCatalog.Count, SkillSlotCapacity);
                    for (int i = 0; i < count; i++)
                        rate += SkillCatalog.RateAt(i, SkillLevel(i), L) * AffinityFor(i);
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
                get { return HealthCurve.ValueAtLevel(H) * HealthAmp * ArmorMultiplier * EvolutionHealth; }
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
                        //
                        // 33단계의 전직 배수도 같은 자리, 같은 이유다
                        //
                        // 44단계의 요도도 같은 자리다. 괄호 밖이라 오의에도
                        // 상속된다 - "요괴를 벤 칼"이 평타에만 듣고 발도에는
                        // 안 듣는다면 그것은 칼이 아니라 버프다
                        Damage = AttackPowerCurve.ValueAtLevel(P) * AttackAmp * WeaponMultiplier
                            * EvolutionAttack * YodoMultiplier,
                        AttacksPerSecond = AttackSpeedCurve.CappedValueAtLevel(S),
                        CritRate = CritRateCurve.CappedValueAtLevel(R),
                        CritMultiplier = CritDamageCurve.ValueAtLevel(D),

                        // 26단계. 괄호 안에 더해지므로 위의 공격력 증폭과 아래
                        // 치명타 계수를 그대로 상속한다 - 여기서 다시 곱하면
                        // 스킬만 증폭이 두 번 걸린다
                        SkillRate = SkillRate,

                        // 영체(45단계). 오의와 같은 괄호 안이다 - 소환 한 번이
                        // 공격력 x 배율 뭉치이므로 단위가 같고, 그래서 더할 수
                        // 있다. 펫이 괄호 밖인 것과 나뉘는 자리가 여기다
                        // (CombatStats.SpiritRate 주석)
                        SpiritRate = SpiritRate,

                        // 펫. 괄호 밖에 곱해진다 - 펫의 한 타가 기대 DPS 기준이라
                        // 치명타·스킬을 이미 담고 있다 (CombatStats.PetBonus 주석)
                        PetBonus = PetBonus,

                        // 심화 축(43단계). UpgradeSystem.Apply와 같은 경로다 -
                        // 두 곳이 갈리면 시뮬레이션이 게임과 다른 밸런스를 잰다
                        TranscendMultiplier = TranscendCurve.MultiplierAtLevel(Tx),
                        ComboChance = ComboCurve.ChanceAtLevel(Cx)
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
                //
                // 33단계의 전직 체력 배수도 같은 자리다. **세 번째로 같은 함정에
                // 물렸다** - 빠뜨린 채 돌리자 체력을 안 사고 회복·방어구로 목표를
                // 쫓다가 st50 생존 여유가 0.86까지 떨어졌다. 곱해지는 항을 하나
                // 늘릴 때마다 이 분자를 반드시 함께 고쳐야 한다
                double healthGain = (SurvivalEfficiency.EffectiveHealth(
                        HealthCurve.ValueAtLevel(levels.H + 1) * levels.HealthAmp
                            * levels.ArmorMultiplier * levels.EvolutionHealth,
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

            // 전직은 생존 다음, 화력 앞이다. 생존보다 뒤인 이유는 생존이 절대
            // 조건이기 때문이고(죽으면 스테이지에 못 들어간다 - 골드 축과 같은
            // 규칙), 화력보다 앞인 이유는 한 칸이 +10~20% DPS라 골드당 효율에서
            // 어떤 화력 축보다 크기 때문이다. 실제 플레이어도 진화 버튼이 열리면
            // 강화를 미루고 그것부터 누른다 - 효율 저울에 올려 같은 결론을 매번
            // 다시 계산할 이유가 없는 자리다.
            //
            // 처음에 생존보다 앞에 뒀다가 물렸다 - 도약형 비용이 지갑을 통째로
            // 비우므로, 생존 축이 살 골드가 사라져 st50 생존 여유가 0.86까지
            // 떨어졌다. "생존 먼저"는 우선순위가 아니라 불변식이다
            TryEvolve(ref levels, ref purse, policy);

            // 동료 해금도 같은 자리다(보석 마일스톤). 골드가 아니라 보석만
            // 들어 지갑을 건드리지 않으므로 생존 불변식과도 충돌하지 않는다.
            // 레벨(골드)은 아래 화력 루프의 저울에 올라간다
            TryUnlockPets(ref levels, currentStage, policy);

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
        /**
         * 동료 레벨이 **세 칸** 붙었다. 이유는 무기와 같다 - 동료 레벨은
         * 골드를 %DPS로 바꾸는 축이고, 자가 같으면 정책도 하나여야 한다.
         * 다중 출전이라 셋이 각자 축이다 - 효율 저울이 첫 칸 큰 청랑을
         * 앞세우고 명궁·묵웅이 한계 이득이 같아지는 간격으로 따라온다.
         * 해금(보석)은 여기 없다 - 재화가 달라 이 저울에 올릴 수 없고,
         * TryUnlockPets가 전직과 같은 자리에서 처리한다.
         */
        /**
         * 43단계에 심화 축이 **두 칸** 붙었다. 이유는 무기·동료와 같다 -
         * 골드를 %DPS로 바꾸는 축이고, 자가 같으면 정책도 하나여야 한다.
         * 해금(치명타 100%)은 TryCost의 문이다 - 잠긴 문의 축은 저울에
         * 올라오지 않는다.
         */
        private const int DamageAxisCount = 4 + SkillSlotCapacity + 1 + PetSlotCapacity + 2;

        /** 무기 축의 번호. 스킬 뒤 한 칸 */
        private const int WeaponAxis = 4 + SkillSlotCapacity;

        /** 동료 레벨 축의 시작 번호. 무기 뒤 세 칸 */
        private const int PetAxisFirst = WeaponAxis + 1;

        /** 심화 축의 번호. 동료 뒤 두 칸 */
        private const int TranscendAxis = PetAxisFirst + PetSlotCapacity;
        private const int ComboAxis = TranscendAxis + 1;

        /**
         * @brief Levels가 들고 있는 동료 칸 수.
         *
         * PetCatalog가 이보다 많은 동료를 들고 오면 뒤쪽이 조용히 무시된다 -
         * 시뮬레이션만 없는 DPS로 계산하게 되므로 테스트가 못 박는다
         * (PetTests.Simulation_HasASlotForEveryPet).
         */
        public const int PetSlotCapacity = 3;

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
                    if (axis == TranscendAxis) { levels.Transcend = levels.Tx + 1; return; }
                    if (axis == ComboAxis) { levels.Combo = levels.Cx + 1; return; }
                    if (axis >= PetAxisFirst)
                    {
                        int pet = axis - PetAxisFirst;
                        levels.SetPetLevel(pet, levels.PetLevelAt(pet) + 1);
                        return;
                    }

                    int index = axis - 4;
                    levels.SetSkillLevel(index, levels.SkillLevel(index) + 1);
                    return;
            }
        }

        // ---------------------------------------------------------------- 44단계: 요도

        /**
         * @brief Levels가 들고 있는 요도 칸 수.
         *
         * YodoCatalog가 이보다 많은 요도를 들고 오면 뒤쪽이 조용히 무시된다 -
         * 시뮬레이션만 없는 DPS로 계산하게 되므로 테스트가 못 박는다
         * (YodoTests.Simulation_HasASlotForEveryBlade).
         */
        public const int YodoSlotCapacity = 4;

        /**
         * @brief 상성·영체 계산이 돌려 쓰는 티어 배열. **Levels.FillScratch만 만진다.**
         *
         * 정적인 이유는 Levels가 struct이기 때문이다(그 주석 참고). 값을
         * 채운 즉시 읽고 버리므로 상태가 남지 않고, 시뮬레이션은 한 스레드에서
         * 돈다 - EditMode 테스트도 마찬가지다.
         */
        private static readonly int[] YodoScratch = new int[YodoSlotCapacity];

        /**
         * @brief 전설 妖刀의 칸 수. **카탈로그와 갈리면 테스트가 잡는다.**
         *
         * YodoSlotCapacity와 같은 규칙이다 - Levels가 struct라 배열을 못
         * 두고 낱개 필드로 펼치므로, 카탈로그가 늘면 여기 칸도 손으로
         * 늘려야 한다.
         */
        public const int LegendarySlotCapacity = 2;

        /** 혼격·전설 스크래치 (47단계). YodoScratch와 같은 규칙이다 */
        private static readonly int[] RarityScratch = new int[YodoSlotCapacity];
        private static readonly int[] LegendScratch = new int[LegendarySlotCapacity];

        /** 혼 정수 상한 판정이 돌려 쓰는 혼 배열. YodoScratch와 같은 규칙이다 */
        private static readonly long[] SoulScratch = new long[YodoSlotCapacity];

        /**
         * @brief 이 스테이지의 보스가 남긴 것을 받고, 받을 수 있는 만큼 벼린다.
         *
         * ## 왜 Buy 안이 아니라 밖인가
         *
         * 다른 축은 전부 골드 저울(GainPerGoldFor) 위에 있다. 요도는 그
         * 저울에 올릴 수가 없다 - 혼은 골드로 못 사고, 파편도 못 산다.
         * 재화가 다르면 나눌 수 없다는 것은 이 프로젝트가 세 번 확인한 규칙이고
         * (TryUnlockPets가 Buy 안에서 따로 도는 이유도 같다), 요도는 그중에서도
         * 골드가 **한 푼도** 안 드는 첫 축이라 아예 밖에 선다.
         *
         * ## 순서 - 보스 보상 다음, 다음 스테이지 구매 앞
         *
         * 혼은 보스를 벤 순간에 떨어진다. 이 스테이지의 여유(BossMargin)는
         * 이미 계산된 뒤이므로 요도는 **다음 스테이지부터** 힘을 낸다 -
         * 게임 쪽도 같다(BossFight.OnEnemyKilled가 클리어 처리와 같은 자리에서
         * 드랍을 보고한다). 그래서 첫 혼이 st50에 떨어져도 st50의 밴드는
         * 움직이지 않고, 가속 구간의 마지막 칸이 비트 단위로 보존된다.
         */
        private static void TryForgeYodo(ref Levels levels, int stage, Policy policy)
        {
            if (policy.SkipYodo || policy.NeutralizeYodo) return;
            if (!YodoCurve.IsUnlockedAt(stage)) return;

            // 정예(챕터)는 파편, 대요괴(피날레)는 혼. 둘 다 이 스테이지의
            // 보스 등급에서 유도한다 - 상수를 적으면 순환 라인업이 바뀌는 날
            // 시뮬레이션만 옛 배치로 센다
            if (YodoCurve.DropsShardsAt(stage)) levels.Shards += YodoCurve.ShardsPerElite;

            int dropped = YodoCurve.SoulIndexDroppedAt(stage);
            if (dropped >= 0 && dropped < YodoSlotCapacity)
            {
                // **기댓값으로 센다.** 확률이 1이면 실제와 같고, 1이 아니게
                // 되는 날(뽑기 스텝) 시뮬레이션은 평균 플레이어를 재게 된다 -
                // 그 사실을 YodoTests가 못 박는다
                var spec = YodoCatalog.Blades[dropped];
                if (spec.DropChance >= 1d) levels.SetSouls(dropped, levels.SoulsAt(dropped) + 1);
            }

            // 뽑기(46단계)는 드랍 **다음**, 벼림 **앞**이다. 드랍 다음인 이유는
            // 혼 정수의 상한이 "지금까지 떨어진 혼"에서 유도되기 때문이고
            // (GachaCurve.EssenceSoulCap), 벼림 앞인 이유는 이번 뽑기가 준
            // 파편과 정수가 곧바로 이 스테이지의 티어가 되어야 게임과 같은
            // 순간이 되기 때문이다 - 업적 보석을 구매 앞에 둔 것과 같은 판단이다
            TryGacha(ref levels, stage, policy);

            int count = Math.Min(YodoCatalog.Count, YodoSlotCapacity);
            for (int i = 0; i < count; i++)
            {
                while (levels.SoulsAt(i) >= YodoCurve.SoulsPerTier)
                {
                    int tier = levels.YodoTierAt(i);

                    // 상한에 닿은 요도에게 온 혼은 갈 곳이 없다. 파편으로
                    // 바꾼다 - 버려지는 드랍을 0으로 만드는 유일한 경로다
                    if (tier >= YodoCurve.MaxTier)
                    {
                        levels.Shards += levels.SoulsAt(i) * YodoCurve.ShardsPerOverflowSoul;
                        levels.SetSouls(i, 0);
                        break;
                    }

                    // 봉인(0 -> 1)은 혼만 든다. 파편은 합성의 재료이지
                    // 탄생의 재료가 아니다
                    int shardCost = tier == 0 ? 0 : YodoCurve.ShardCostAtTier(tier);
                    if (shardCost > levels.Shards && !TryBuyShards(ref levels, shardCost, policy)) break;

                    levels.Shards -= shardCost;
                    levels.SetSouls(i, levels.SoulsAt(i) - YodoCurve.SoulsPerTier);
                    levels.SetYodoTier(i, tier + 1);
                }
            }
        }

        /**
         * @brief 뽑기를 돌린다. **기댓값으로 센다 - 실제는 굴린다.**
         *
         * ## 무엇을 사는가
         *
         * 뽑기의 상품은 둘인데 이 함수가 재는 것은 사실상 **혼 정수 하나**다.
         * 파편은 촉매(TryBuyShards)가 이미 확정으로, 그것도 더 싸게 주고
         * 있으므로(GachaCurve.PullCostGems 주석) 파편 때문에 뽑는 플레이어는
         * 곡선 위에 없다. 그래서 멈추는 조건도 "파편이 찼다"가 아니라
         * **"정수를 받을 자루가 없다"**이다 - 상한(GachaCurve.LeadTiers)이
         * 곧 이 뽑기의 재고다.
         *
         * ## 무과금은 여기 들어오지 않는다
         *
         * 44단계 실측이 그렇게 시켰다 - 무과금 보석 잔액 30~190은 장비 등급·
         * 동료 해금·전직에 이미 다 배정돼 있고, 뽑기가 그것을 가져가면 코어
         * 진행이 무너진다. 그가 뽑기에 닿는 경로는 일일 무료이고, 시뮬레이션에는
         * 달력이 없으므로(44단계가 일일 퀘스트 보석을 뺀 것과 같은 이유) 그
         * 트리클은 세지 않는다. **그래서 보고되는 f2p 바닥은 여전히 하한이다.**
         *
         * 어겼을 때 무엇이 무너지는지는 Policy.GachaBeforeCore가 잰다 -
         * 설계의 근거를 말이 아니라 숫자로 남기기 위한 반례다.
         *
         * ## 단위는 **한 번의 뽑기**다 - 정수 하나가 아니라
         *
         * 한 번씩 굴려서 세면 시뮬레이션에 난수가 들어오고, 그러면 밴드가
         * 실행마다 흔들려 재현이 불가능해진다. 그래서 굴리는 대신 기댓값을
         * 쓰되, **소비의 단위는 뽑기 한 번**으로 둔다: 매번 정가를 내고 기대
         * 파편을 받고, 천장이 접힌 평균(GachaCurve.ExpectedPullsPerEssence)에
         * 닿을 때마다 정수 하나가 나온다.
         *
         * 처음에 "정수 하나"를 단위로 뒀다가 물렸다. 정수 한 덩어리가 보석
         * 500이라 **무과금은 한 번도 못 사고**(그의 잔액 상한이 198이다),
         * 그래서 잠식 반례(GachaBeforeCore)가 무과금 세계에서 아무것도
         * 바꾸지 못했다 - 반례가 통과하는데 그 이유가 "손해라서"가 아니라
         * "살 수 없어서"였다. 부분 지출이 표현되지 않으면 유한한 지갑을
         * 가진 플레이어는 이 축이 통째로 없는 것과 같아진다.
         */
        private static void TryGacha(ref Levels levels, int stage, Policy policy)
        {
            if (policy.SkipGacha) return;
            if (!GachaCurve.IsUnlockedAt(stage)) return;

            // 무과금은 보석을 뽑기에 쓰지 않는다. 위 주석 참고
            if (policy.GemsFromQuestsOnly && !policy.GachaBeforeCore) return;

            for (int guard = 0; guard < 100000; guard++)
            {
                // **재고를 정하는 것은 ★3과 ★4다 - ★5가 아니다.**
                //
                // 47단계에 사다리가 셋이 되면서 정지 규칙을 다시 정해야
                // 했다. 전설(★5)에는 자연 상한이 사본 여덟(두 자루 x
                // MaxCopies)뿐이고 그 여덟은 기대 뽑기 수로 천육백 회라,
                // 전설을 정지 조건에 넣으면 보석 무제한 플레이어가 사실상
                // 무한히 돈다 - 46단계가 "상한이 곧 재고"로 막아 둔 것이
                // 통째로 열린다.
                //
                // 그래서 전설은 **정지 규칙 밖에서 얹혀 온다.** 뽑는 이유는
                // 언제나 혼 정수와 혼격이고, 전설은 그 예산 안에서 확률로
                // 붙는다. 이 선택이 곧 "전설을 몇 자루 가질 수 있는가"의
                // 상한이기도 하다 - 사양이 "소수 바운드"라고 적은 자리를
                // 확률이 아니라 **구조**가 지킨다.
                //
                // **비교군에서는 그 재고도 함께 닫아야 한다.** SkipRarity를
                // 켜 두고 이 조건만 남기면 혼격이 영원히 안 차므로 재고가
                // 절대 안 닫히고, 뽑기가 스테이지마다 가드(10만 회)까지
                // 돈다 - 그 세계는 "혼격이 없는 세계"가 아니라 "파편이
                // 무한한 세계"라 죽은 버튼 검사가 **음수**를 낸다(실제로
                // -3.8%가 나왔고, 그것이 이 두 줄이 있는 이유다).
                bool essenceOpen = levels.EssenceTarget(stage) >= 0;
                bool rarityOpen = !policy.SkipRarity && levels.RarityTarget(stage) >= 0;
                if (!essenceOpen && !rarityOpen) return;

                if (policy.GemsFromQuestsOnly
                    && levels.GemsAvailable < GachaCurve.PullCostGems) return;

                levels.GemsSpent += GachaCurve.PullCostGems;
                levels.GachaPulls += 1d;

                // 파편. 소수 자리를 들고 있다가 1이 차면 넘긴다
                levels.GachaShardCarry += GachaCurve.ExpectedShardsPerPull;
                int whole = (int)levels.GachaShardCarry;
                levels.Shards += whole;
                levels.GachaShardCarry -= whole;

                GrantSimEssence(ref levels, stage);
                if (!policy.SkipRarity) GrantSimRarity(ref levels, stage);
                if (!policy.SkipLegendary) GrantSimLegendary(ref levels);
            }
        }

        /**
         * @brief ★3 한 번의 기대 몫. 질량이 1을 넘으면 정수 하나가 나온다.
         *
         * 남는 자리는 다음 뽑기·다음 스테이지로 넘어간다 - 파편의 소수
         * 자리와 같은 처리이고, 버리면 뽑기가 잦은 후반일수록 조금씩 새서
         * 그 누적이 티어 하나가 된다.
         */
        private static void GrantSimEssence(ref Levels levels, int stage)
        {
            levels.GachaEssenceProgress += GachaCurve.EffectiveEssenceChance;
            if (levels.GachaEssenceProgress + 1e-12d < 1d) return;

            levels.GachaEssenceProgress -= 1d;

            int target = levels.EssenceTarget(stage);
            if (target >= 0) levels.SetSouls(target, levels.SoulsAt(target) + 1);
            else levels.Shards += YodoCurve.ShardsPerOverflowSoul;   // 넘침 규칙 계승
        }

        /**
         * @brief ★4 한 번의 기대 몫. **게임과 같은 순서로 미끄러진다.**
         *
         * 혼격 -> 혼 정수 -> 파편이다(GachaSystem.GrantRarity). 순서가
         * 갈리면 시뮬레이션이 재는 세계와 플레이어가 사는 세계가 달라진다 -
         * 특히 초반에는 혼격이 티어에 막혀 대부분 두 번째 칸으로 내려가므로,
         * 그 미끄러짐이 없으면 시뮬레이션의 티어가 실제보다 느려진다.
         */
        private static void GrantSimRarity(ref Levels levels, int stage)
        {
            levels.GachaRarityProgress += GachaCurve.EffectiveRarityChance;
            if (levels.GachaRarityProgress + 1e-12d < 1d) return;

            levels.GachaRarityProgress -= 1d;

            int target = levels.RarityTarget(stage);
            if (target >= 0)
            {
                levels.SetRarity(target, levels.RarityAt(target) + 1);
                return;
            }

            int fallback = levels.EssenceTarget(stage);
            if (fallback >= 0) levels.SetSouls(fallback, levels.SoulsAt(fallback) + 1);
            else levels.Shards += GachaCurve.ShardsPerOverflowRarity;
        }

        /** ★5 한 번의 기대 몫. 상한 사본이면 파편 뭉치다 */
        private static void GrantSimLegendary(ref Levels levels)
        {
            levels.GachaLegendProgress += GachaCurve.EffectiveLegendaryChance;
            if (levels.GachaLegendProgress + 1e-12d < 1d) return;

            levels.GachaLegendProgress -= 1d;

            int target = levels.LegendaryTarget();
            if (target >= 0) levels.SetLegendary(target, levels.LegendaryAt(target) + 1);
            else levels.Shards += LegendaryYodoCurve.ShardsPerOverflow;
        }

        /**
         * @brief 파편이 모자란 만큼 보석으로 묶음을 산다. **촉매다 - 실패해도 좋다.**
         *
         * 기본 정책(보석 무제한)에서는 언제나 성공하므로 요도가 혼에만 막히고,
         * 그것이 기대 곡선의 정의다(YodoCurve.ExpectedTierAtStage). 무과금
         * (GemsFromQuestsOnly)에서는 잔액이 거의 없어 대부분 실패하고, 그
         * 세계에서도 축이 서는 것이 이 스텝의 f2p 안전선이다.
         */
        private static bool TryBuyShards(ref Levels levels, int needed, Policy policy)
        {
            if (policy.SkipShardPacks) return false;

            for (int guard = 0; guard < 10000; guard++)
            {
                if (levels.Shards >= needed) return true;

                if (policy.GemsFromQuestsOnly
                    && levels.GemsAvailable < YodoCurve.ShardPackGems) return false;

                levels.GemsSpent += YodoCurve.ShardPackGems;
                levels.Shards += YodoCurve.ShardPackShards;
            }
            return levels.Shards >= needed;
        }

        // ---------------------------------------------------------------- 동료

        /**
         * @brief 아직 없는 동료를 싼 문부터 차례로 해금한다. **보석만 든다.**
         *
         * 다중 출전이라 해금 하나하나가 곧 DPS다(합산에 자기 몫이 더해진다).
         * 곡선 추종 플레이어(보석 무제한)는 st31에 셋을 다 열고, 보석 하한
         * 플레이어는 잔액이 닿는 문까지만 연다.
         *
         * 막히는 경로가 셋이다 - 해금 전(st31), 비교군(SkipPets /
         * NeutralizePets), 보석 부족(GemsFromQuestsOnly에서만 실제로 막힌다 -
         * 장비 등급업·전직과 같은 규칙).
         */
        private static void TryUnlockPets(ref Levels levels, int currentStage, Policy policy)
        {
            if (policy.SkipPets || policy.NeutralizePets) return;
            if (!PetCurve.IsUnlockedAt(currentStage)) return;

            int count = Math.Min(PetCatalog.Count, PetSlotCapacity);
            for (int i = 0; i < count; i++)
            {
                if (levels.PetOwnedAt(i)) continue;

                int gemCost = PetCatalog.Pets[i].UnlockGems;
                if (policy.GemsFromQuestsOnly && levels.GemsAvailable < gemCost) continue;

                levels.GemsSpent += gemCost;
                levels.SetPetOwned(i);
                levels.SetPetLevel(i, 1);
            }
        }

        // ---------------------------------------------------------------- 33단계: 전직

        /**
         * @brief 전직 티어를 올릴 수 있는 만큼 올린다.
         *
         * 막히는 경로가 넷이다 - 해금 전(Lv.30), 비교군(SkipEvolution /
         * NeutralizeEvolution), 상한(6티어), 재화 부족. 보석은
         * GemsFromQuestsOnly에서만 실제로 막힌다(장비 등급업과 같은 규칙).
         *
         * 루프인 이유는 보스 보상 직후 골드가 티어 두 개 몫일 수 있기 때문인데,
         * 실제로는 도약형 비용이라 한 스테이지에 한 칸을 넘는 일이 드물다.
         */
        private static void TryEvolve(ref Levels levels, ref double purse, Policy policy)
        {
            if (policy.SkipEvolution || policy.NeutralizeEvolution) return;
            if (!EvolutionCurve.IsUnlockedAt(levels.L)) return;

            for (int guard = 0; guard < EvolutionCurve.MaxTier + 1; guard++)
            {
                if (!EvolutionCurve.CanEvolve(levels.Ev)) break;

                int gemCost = EvolutionCurve.GemCost(levels.Ev);
                double goldCost = EvolutionCurve.GoldCost(levels.Ev);

                if (policy.GemsFromQuestsOnly && levels.GemsAvailable < gemCost) break;
                if (goldCost > purse) break;

                purse -= goldCost;
                levels.GemsSpent += gemCost;
                levels.EvolutionTier = levels.Ev + 1;
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

            // 심화 축(43단계). 문이 셋이다 - 비교군, 해금(치명타 100%), 상한(연격)
            if (axis == TranscendAxis || axis == ComboAxis)
            {
                if (policy.SkipMastery || policy.NeutralizeMastery) return false;
                if (!TranscendCurve.IsUnlockedAt(levels.R)) return false;

                if (axis == TranscendAxis)
                {
                    cost = TranscendCurve.CostAtLevel(levels.Tx);
                    return true;
                }

                if (levels.Cx >= ComboCurve.MaxLevel) return false;
                cost = ComboCurve.CostAtLevel(levels.Cx);
                return true;
            }

            if (axis >= PetAxisFirst && axis < PetAxisFirst + PetSlotCapacity)
            {
                int pet = axis - PetAxisFirst;

                // 해금이 문이다. 없는 동료의 레벨 버튼은 화면에 없다 - 못 사는
                // 것을 저울에 올리면 정책이 그 칸을 고르고 아무것도 안 하는
                // 상태가 된다 (TryEquipmentCost와 같은 규칙)
                if (policy.SkipPets || policy.NeutralizePets) return false;
                if (pet >= PetCatalog.Count) return false;
                if (!levels.PetOwnedAt(pet)) return false;
                if (!PetCurve.CanLevelUp(levels.PetLevelAt(pet))) return false;

                cost = PetCurve.CostAtLevel(levels.PetLevelAt(pet));
                return true;
            }

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
                    // 발도 개방이 없는 세계(43단계 비교군)에서는 60% 문턱 앞에서
                    // 멈춘다 - 42단계의 치명타 축 그대로다
                    if (policy.NeutralizeMastery && levels.R >= CritRateCurve.DeepPhaseLevel - 1) return false;
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
