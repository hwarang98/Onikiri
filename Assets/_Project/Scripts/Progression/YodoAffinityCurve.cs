using System;

namespace Onikiri.Progression
{
    /**
     * @brief 혼별 오의 상성. **요도의 티어가 그 혼의 오의를 강화한다.**
     *
     * ## 새 투자 축이 아니다 - 그것이 이 파일의 전부다
     *
     * 상성에는 재화도 레벨도 버튼도 없다. 세기를 정하는 것은 **그 요도의
     * 티어 하나**이고, 티어는 44단계가 이미 만든 것이다(혼 + 파편). 화면에
     * 새로 뜨는 것은 값뿐이다.
     *
     * 축을 새로 만들지 않은 이유는 이 프로젝트가 세 번 배운 것이다. 축이
     * 하나 늘 때마다 재화·비용 곡선·효율 저울·보정항·밴드 검사가 함께 늘고,
     * 43단계에서 그 다섯이 전부 붙은 뒤 심층 천장 여유가 7%까지 얇아졌다.
     * 상성은 **이미 있는 티어의 두 번째 읽는 법**이라 그 다섯 중 넷이 필요
     * 없다 - 밴드만 다시 잰다.
     *
     * ## 그런데 왜 이것이 빌드가 되는가
     *
     * 파편 지갑이 하나이기 때문이다(YodoCurve.ShardCostAtTier). 네 자루가
     * 같은 주머니를 나눠 쓰므로 한 자루를 밀면 나머지 셋이 늦고, 이제 그
     * 결정이 "어느 오의가 세지는가"를 정한다. 곡선 추종 경로(f2p)는 넷을
     * 고르게 올리므로 상성도 고르게 오르고 - 그것이 기대 곡선이자 밴드가
     * 지키는 바닥이다. 몰아주는 플레이어는 그 위에서 **모양이 다른** 같은
     * 크기를 얻는다.
     *
     * ## 곱연산이고, 오의 배율에 곧바로 곱한다
     *
     * SkillSystem.MultiplierOf가 이 값을 곱한 것을 내고, 그 값 하나가
     * 화면(패널·데미지 숫자)과 초당 환산 기여(CastRate)에 함께 간다. 두
     * 경로가 갈리면 "패널에는 올랐는데 데미지는 그대로"가 된다 - 26단계가
     * MultiplierOf 하나로 못 박은 규칙 그대로다.
     *
     * ## DPS로는 몇 %인가 - 오의는 괄호 안이라 몫에 비례한다
     *
     *     DPS = 공격력 x (공격속도 + 오의 초당환산) x 치명타 x ...
     *
     * 상성은 괄호 안의 뒷항만 키우므로, 배율 x1.5가 곧 DPS x1.5가 아니다.
     * 그 몫(SkillDpsShare)이 얼마인지가 이 축의 실제 크기를 정하고,
     * **심층 구간에서 그 몫은 상수다** - 공격속도도 오의 배율도 그때는 이미
     * 상한이기 때문이다(ReferenceSkillShare 주석). 기대 곡선이 닫힌 식으로
     * 적히는 근거가 그 사실이다.
     */
    public static class YodoAffinityCurve
    {
        // ---------------------------------------------------------------- 값

        /**
         * @brief 한 오의를 전담하는 혼(등롱·처형인·적안)의 봉인 배수.
         *
         * 티어 한 칸(AffinityTierStep)보다 크다. 44단계의 SealStep이 티어
         * 한 칸보다 큰 것과 같은 이유이고 같은 사건이다 - 봉인은 칸을 하나
         * 올리는 일이 아니라 **없던 상성이 생기는 일**이다. 도감에 잠긴
         * 미리보기로 서 있던 줄이 그 순간 값을 갖는다.
         */
        public const double AffinitySealStep = 1.13d;

        /**
         * @brief 티어 한 칸. 아홉 번 곱해져 상한에서 x3.13이 된다.
         *
         * ## 왜 티어 배수(1.035)보다 두 배 이상 가파른가 - 실측이 시켰다
         *
         * 처음에 1.040으로 잡았다(티어 배수보다 조금 큰 정도). 하네스로 재보니
         * **상성을 안 걸어도 진행이 2.2%밖에 안 느렸다** - 프로젝트 기준 4%
         * 아래이고, 20단계 골드 축의 함정("지표는 사라는데 실제로는 손해")이
         * 재현되는 크기다.
         *
         * 원인은 이 축이 괄호 **안**에 있다는 사실이다. 오의는 DPS의 38%뿐이고
         * (ReferenceSkillShare), 게다가 파밍 시간은 요괴 공급 하한(SpawnPacing
         * 0.4초)에 묶여 있어 DPS가 올라도 줄지 않는다. 공격력에 곱하는 축과
         * 같은 계수를 쓰면 실제 체감은 그 절반 이하다 - **곱하는 자리가
         * 다르면 같은 숫자가 다른 크기다.**
         */
        public const double AffinityTierStep = 1.12d;

        /**
         * @brief 전 오의를 미는 혼(흑야)의 봉인 배수. **셋에 걸리므로 얕다.**
         *
         * 같은 값을 쓰면 흑야 한 자루가 나머지 셋을 합친 것보다 세진다 -
         * 세 오의에 각각 곱해지기 때문이다. 여기 값은 상한에서 x1.47이고
         * 전담 한 자루는 x3.13이다. 셋에 걸리는 몫을 DPS로 환산하면 전담
         * 하나의 절반 언저리가 되고, 그것이 의도다: **흑야는 가장 강한
         * 선택이 아니라 가장 무던한 선택**이어야 한다. 몰아주기의 답이
         * 언제나 흑야면 빌드는 다시 하나가 된다.
         *
         * 배수가 아니라 **DPS 기여**로 비교하는 것이 요점이고, 그것을
         * YodoPowerTests.BroadAffinity_IsWeakerThanADedicatedOne이 잰다 -
         * 오의마다 몫이 다르므로(귀참 0.32 / 일섬 0.25 / 연참 0.18) 배수만
         * 비교하면 답이 갈린다.
         */
        public const double BroadSealStep = 1.05d;
        public const double BroadTierStep = 1.038d;

        /** 이 혼이 전 오의를 미는가. 카탈로그의 빈 id가 그 뜻이다 */
        public static bool IsBroad(int bladeIndex)
        {
            if (bladeIndex < 0 || bladeIndex >= YodoCatalog.Count) return false;
            return string.IsNullOrEmpty(YodoCatalog.Blades[bladeIndex].AffinitySkillId);
        }

        /**
         * @brief 이 요도가 자기 오의에 곱하는 배수. **티어 0이면 정확히 1이다.**
         *
         * 1로 떨어지는 것이 st1~50 전체의 불변이다 - 그 구간에는 봉인된
         * 요도가 없으므로(YodoCurve.UnlockStage) 상성도, 상성의 보정도
         * 존재하지 않는다. 44단계가 구조로 지킨 것을 그대로 물려받는다.
         *
         * 상한 위의 티어는 자른다. 값에서 자르고 티어는 안 자르는 규칙
         * (YodoCurve.TierValue)과 같다.
         */
        /**
         * @param rarity 혼격(47단계). 0이면 44·45단계와 **비트 단위로** 같다 -
         *               YodoRarityCurve.ValueAt이 0에서 정확히 1을 내므로
         *               무과금의 기대 곡선과 보정이 한 톨도 안 움직인다.
         */
        public static double ValueAt(int bladeIndex, int tier, int rarity = 0)
        {
            if (tier < 1) return 1d;

            int t = tier > YodoCurve.MaxTier ? YodoCurve.MaxTier : tier;
            bool broad = IsBroad(bladeIndex);

            double seal = broad ? BroadSealStep : AffinitySealStep;
            double step = broad ? BroadTierStep : AffinityTierStep;
            return seal * Math.Pow(step, t - 1) * YodoRarityCurve.ValueAt(rarity);
        }

        /**
         * @brief skillIndex번 오의가 지금 받는 총 상성 배수.
         *
         * 곱이다 - 전담 혼 하나와 흑야가 겹치면 둘 다 걸린다. 더하지 않는
         * 이유는 나머지 배수 축(요도 티어·세트·장비·전직)이 전부 곱이기
         * 때문이고, 한 화면 안에서 합과 곱이 섞이면 "x1.2와 +0.2 중 어느
         * 쪽이 큰가"를 플레이어가 셀 수 없다.
         *
         * ## 47단계 - 두 배열이 더 붙었고, 둘 다 없으면 예전 그대로다
         *
         * `rarities`는 자루마다의 혼격이고 `legends`는 전설 요도의 사본
         * 수다. 둘 다 기본값 null이라 **넘기지 않는 호출부는 44·45단계와
         * 부동소수점까지 같은 값을 받는다** - 그 성질이 f2p 바닥의 비트
         * 불변을 구조로 지킨다(무과금에게는 두 값이 언제나 0이다).
         *
         * @param tiers     요도 넷의 티어. 길이가 모자라면 있는 만큼만 센다
         * @param rarities  요도 넷의 혼격 (47단계). null이면 전부 0
         * @param legends   전설 요도의 사본 수 (47단계). null이면 미보유
         */
        public static double FactorForSkill(int skillIndex, int[] tiers,
                                            int[] rarities = null, int[] legends = null)
        {
            if (tiers == null) return 1d;
            if (skillIndex < 0 || skillIndex >= SkillCatalog.Count) return 1d;

            string skillId = SkillCatalog.Skills[skillIndex].Id;

            double factor = 1d;
            int count = Math.Min(YodoCatalog.Count, tiers.Length);
            for (int i = 0; i < count; i++)
            {
                if (tiers[i] < 1) continue;

                string target = YodoCatalog.Blades[i].AffinitySkillId;
                if (!string.IsNullOrEmpty(target) && target != skillId) continue;

                factor *= ValueAt(i, tiers[i], YodoRarityCurve.At(rarities, i));
            }

            // 전설은 봉인한 혼이 아니라 유물이므로 자기 표를 지난다.
            // 곱해지는 자리는 같다 - 한 오의가 받는 값은 언제나 한 숫자다
            return factor * LegendaryYodoCurve.AffinityForSkill(skillIndex, legends);
        }

        // ---------------------------------------------------------------- 기대 곡선

        /**
         * @brief 심층 구간에서 오의가 DPS의 몇 %인가. **상수다 - 근사가 아니다.**
         *
         *     몫 = 오의 초당환산 상한 / (공격속도 상한 + 오의 초당환산 상한)
         *
         * 두 값이 다 상한이라는 것이 요점이다. 공격속도는 아트가 정한 상한
         * (AttackSpeedCurve.Ceiling)에서 멈추고, 오의 배율은 열두 레벨에서
         * 멈추며(SkillCurve.MaxLevel) 구매 정책이 그 열두 칸을 st25 언저리에
         * 다 산다. 상성이 존재하는 구간(st51+) 전체가 그 뒤이므로 이 몫은
         * 스테이지에 무관한 값이고, **기대 곡선이 시뮬레이션을 참조하지
         * 않고도 닫힌 식으로 적힐 수 있는 근거**가 된다.
         *
         * 근사가 아니라는 것은 검사로 못 박는다 - 시뮬레이션의 실제 몫이
         * 심층 전 구간에서 이 값과 같은지
         * (YodoPowerTests.SkillShare_IsFlatAcrossTheDeepZone).
         *
         * 치명타·초월·연격·펫·장비·전직은 여기 없다. 전부 괄호 밖에서
         * 분자와 분모에 똑같이 곱해져 약분되기 때문이다 - 그것이 오의를
         * 괄호 안에 더한 26단계 설계의 배당금이다.
         */
        public static double CappedSkillRate
        {
            get
            {
                double rate = 0d;
                for (int i = 0; i < SkillCatalog.Count; i++)
                {
                    var spec = SkillCatalog.Skills[i];
                    if (spec.CooldownSeconds <= 0d) continue;
                    rate += SkillCurve.CeilingFor(spec.BaseMultiplier) / spec.CooldownSeconds;
                }
                return rate;
            }
        }

        /**
         * @brief 공격속도의 **실제 도달값.** 괄호 안의 앞항이다.
         *
         * `AttackSpeedCurve.Ceiling`(4.00)이 아니라 마지막 레벨의 값(3.879)이다.
         * 상한은 값에서 자르고 **레벨은 그 아래에서 멈추므로**
         * (AttackSpeedCurve.MaxLevelWithin - 상한을 넘지 않는 마지막 레벨),
         * 플레이어가 실제로 서는 자리는 상한이 아니라 그 직전 칸이다.
         *
         * 처음에 Ceiling을 썼다가 실측이 잡았다 - 기대 곡선이 st51부터
         * 시뮬레이션과 0.1%씩 어긋났고(ExpectedPowerCurve_TracksTheSimulation),
         * 그 어긋남이 그대로 보정과 실제의 차가 된다. 8단계가 "값에서 자르는
         * 상한과 레벨에서 멈추는 상한은 다른 숫자"라고 적어둔 자리이고,
         * 여기가 그 구분이 실제로 물린 두 번째 곳이다.
         */
        public static double CappedAttackRate
        {
            get { return AttackSpeedCurve.CappedValueAtLevel(AttackSpeedCurve.MaxLevel); }
        }

        public static double ReferenceSkillShare
        {
            get
            {
                double skill = CappedSkillRate;
                double total = CappedAttackRate + skill;
                return total > 0d ? skill / total : 0d;
            }
        }

        /**
         * @brief 이 티어 조합의 상성이 **DPS에** 곱하는 배수.
         *
         * 오의별 배율을 각자 상성으로 키운 뒤 괄호를 다시 세운다:
         *
         *     (공격속도 + Σ 오의i x 상성i) / (공격속도 + Σ 오의i)
         *
         * 오의마다 몫이 다르므로(귀참 0.32 / 일섬 0.25 / 연참 0.18) 같은
         * 배수라도 어느 오의에 붙었는지에 따라 DPS 기여가 다르다 - 빌드가
         * 모양뿐 아니라 크기로도 갈리는 자리이고, 그래서 여기서 오의별로
         * 더한다(평균 상성 하나로 접지 않는다).
         */
        public static double DpsFactor(int[] tiers, int[] rarities = null, int[] legends = null)
        {
            double attack = CappedAttackRate;

            double plain = 0d, boosted = 0d;
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                if (spec.CooldownSeconds <= 0d) continue;

                double rate = SkillCurve.CeilingFor(spec.BaseMultiplier) / spec.CooldownSeconds;
                plain += rate;
                boosted += rate * FactorForSkill(i, tiers, rarities, legends);
            }

            double before = attack + plain;
            if (before <= 0d) return 1d;
            return (attack + boosted) / before;
        }
    }
}
