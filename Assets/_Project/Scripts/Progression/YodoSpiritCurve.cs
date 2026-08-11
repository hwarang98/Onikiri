using System;

namespace Onikiri.Progression
{
    /**
     * @brief 영체 소환. **봉인한 혼이 잠깐 전장에 나와 벤다.**
     *
     * ## 동료와 무엇이 다른가 - 결이 갈리는 자리를 값으로 적는다
     *
     *   동료(펫)  상시 출전. DPS에 **합산**되는 몫이고 화면에 계속 서 있다
     *   영체      쿨다운마다 한 번. **일시 버스트**이고 나타났다 사라진다
     *
     * 산수에서도 갈린다. 펫은 괄호 **밖**의 (1 + 보너스)이고(CombatStats.
     * PetBonus), 영체는 괄호 **안**의 초당 환산 항이다 - 오의와 같은 자리다.
     * 그 선택이 자의적이지 않은 이유는 영체가 하는 일이 오의가 하는 일과
     * 같은 종류이기 때문이다: **쿨다운을 돌리고, 배율 한 뭉치를 몇 타로
     * 나눠 때리고, 끝난다.** 펫처럼 "플레이어 DPS의 몇 %"로 재면 그 사건성이
     * 사라지고 화면의 소환 연출이 숫자와 무관해진다.
     *
     * 겹치지 않는다는 것은 화면에서도 참이어야 한다 - 펫은 자기 자리에
     * 서 있고 영체는 플레이어 뒤에서 솟았다가 사라진다(SpiritSummon).
     *
     * ## 누가 나오는가 - 봉인한 자루의 로테이션
     *
     * 최고 티어 하나만 나오게 하는 안을 버렸다. 그러면 나머지 셋의 영체는
     * **평생 화면에 안 나온다** - 대요괴 넷을 모으는 도감이 있는 게임에서
     * 셋을 안 보여주는 것은 수집을 부정하는 일이다.
     *
     * 로테이션은 그 대신 몰아주기에 값을 매긴다. 한 번의 소환이 내는 힘은
     * 그 순번 자루의 티어이므로, 기대 세기는 **봉인한 자루들의 평균**이다:
     *
     *   고르게 올린 플레이어   평균이 높다. 영체가 강하다
     *   한 자루에 몬 플레이어  평균이 낮다. 대신 그 오의의 상성이 세다
     *
     * 상성이 몰아주기를 보상하고 영체가 고르기를 보상한다. 두 축이 반대
     * 방향을 가리키는 것이 이 스텝이 만든 선택이고, 그래서 정답이 없다.
     *
     * ## 동시 소환은 하나다 (MVP)
     *
     * 둘이 겹치면 화면에서 어느 쪽이 때렸는지 읽히지 않고, 겹침 규칙
     * (같은 놈이 둘 나오는가·쿨다운을 나누는가)이 곧바로 필요해진다. 지금은
     * 로테이션 한 줄이면 되고, 확장 여지는 순번 계산 하나에만 걸려 있다.
     */
    public static class YodoSpiritCurve
    {
        // ---------------------------------------------------------------- 리듬

        /**
         * @brief 소환 간격 (초). **오의 중 가장 긴 것(귀참 22초)보다 길다.**
         *
         * 그래야 영체가 "가끔 오는 큰 사건"으로 읽힌다. 오의 쿨다운
         * 7/13/22초 사이에 끼우면 네 번째 오의가 되고, 그러면 이 축이
         * 만들려던 것(정체성)이 사라진다.
         *
         * 위로도 묶여 있다. 한 스테이지가 30~60초이므로 이보다 길면 스테이지
         * 하나를 통째로 지나쳐버린다 - 26단계가 오의 쿨다운을 20초 안팎에
         * 묶어둔 것과 같은 상한이고, 같은 이유(방치형에서 흘깃 볼 때 보여야
         * 한다)다.
         */
        public const double CooldownSeconds = 30d;

        /** 나타나 있는 시간. 이 안에서 SummonHits 번 벤다 */
        public const double DurationSeconds = 5d;

        /**
         * @brief 한 번 소환에 들어가는 타격 수.
         *
         * 넷인 이유는 총 배율이 크기 때문이다. 상한 티어의 한 뭉치가 귀참
         * 상한(x22.5)보다 큰데 그것을 한 방으로 내면 데미지 숫자 하나가
         * 화면을 채우고, 그 뒤 30초가 조용하다. 넷으로 나누면 5초 동안
         * "무언가가 계속 때리고 있다"가 되고, 그것이 소환물의 체감이다.
         *
         * 총량은 나눠도 같다 - 오의의 다타와 같은 규칙이고 같은 자리에서
         * 나머지를 맞춘다(HitShare).
         */
        public const int SummonHits = 4;

        // ---------------------------------------------------------------- 값

        /**
         * @brief 봉인 직후(티어 1) 영체 한 번의 총 배율.
         *
         * 귀참 상한(x22.5)보다 크다. 쿨다운이 30초로 더 길어 초당 환산
         * 기여는 0.90이고, 화면에서 "봉인하니 새 것이 나온다"가 첫 순간에
         * 읽혀야 한다 - 44단계의 SealStep이 티어 한 칸보다 큰 것과 같은
         * 판단이다. 상한 티어에서는 x135, 초당 환산 4.51이 된다.
         *
         * 처음에 9로 잡았다가 세 번 올렸다. 하네스로 재보니 **영체를 통째로
         * 꺼도 진행이 1.5%밖에 안 느렸다** - 화면에 대요괴가 솟는데 진행이
         * 안 변하면 그것은 소환이 아니라 장식이다. 크기가 작아서만은 아니고
         * 곱해지는 자리 때문이기도 하다(YodoAffinityCurve.AffinityTierStep의
         * 같은 실측 참고 - 괄호 안의 항은 체감이 절반 이하다).
         */
        public const double BaseMultiplier = 27d;

        /**
         * @brief 티어의 **지수**. 1보다 작다 - 이 프로젝트에서 처음이다.
         *
         * ## 왜 오목한가 - 로테이션이 몰아주기를 벌하려면 이것뿐이다
         *
         * 다른 모든 티어 곡선은 볼록(지수적)하다. 여기만 오목한 이유는
         * **로테이션이 평균을 내기 때문**이다.
         *
         * 처음에 볼록하게(티어당 x1.15) 잡았다가 검사가 잡았다. 티어 총합이
         * 같은 두 빌드를 재보니 몰아준 쪽[10,2,2,2]의 평균이 고르게 올린
         * 쪽[4,4,4,4]보다 **컸다** - 볼록 함수의 평균은 몰린 분포에서 더
         * 크기 때문이다(옌센 부등식). 그러면 상성도 영체도 몰아주기를
         * 보상하고, 두 축이 같은 방향을 가리키는 순간 "어느 요도에 파편을
         * 몰까"의 답이 하나로 정해진다 - 이 스텝이 만들려던 것이 사라진다.
         *
         * 오목하게 두면 뒤집힌다. 그리고 그 모양에는 설정이 있다: 영체의
         * 크기는 **혼 자체의 격**이지 칼의 예리함이 아니다. 봉인하는 순간
         * 그 요괴가 통째로 들어오고, 그 뒤의 티어는 붙들어두는 힘을 더할 뿐
         * 다른 요괴를 만들지 못한다.
         *
         * 0.70인 이유는 실측이다. 1에 가까우면 두 분포의 차가 사라져 트레이드
         * 오프가 이름뿐이 되고, 너무 작으면 티어를 올려도 영체가 안 자라
         * 후반에 죽은 축이 된다.
         */
        public const double TierExponent = 0.70d;

        /**
         * @brief 이 티어의 영체가 한 번에 내는 총 배율. 미봉인이면 0.
         *
         * **0으로 떨어지는 것이 중요하다.** 티어 배수(TierValue)는 1로
         * 떨어지지만 여기는 0이다 - 저쪽은 곱해지는 값이고 이쪽은 더해지는
         * 항이라, 각자의 "없음"이 다른 숫자다. 1을 내면 미봉인 요도가 영체
         * 없이 초당 1/30의 유령 기여를 만든다.
         */
        /**
         * @param rarity 혼격(47단계). 짙은 혼일수록 나오는 영체가 크다 -
         *               혼격이 "무엇을 먹였는가"이므로 나오는 것도 그것이다.
         *               0이면 45단계와 부동소수점까지 같다.
         */
        public static double MultiplierAtTier(int tier, int rarity = 0)
        {
            if (tier < 1) return 0d;
            int t = tier > YodoCurve.MaxTier ? YodoCurve.MaxTier : tier;
            return BaseMultiplier * Math.Pow(t, TierExponent) * YodoRarityCurve.ValueAt(rarity);
        }

        /**
         * @brief index번째 타격이 받는 배율. **마지막이 나머지를 받는다.**
         *
         * SkillCatalog.HitDamageShare와 같은 규칙이고 같은 이유다 - 총 배율을
         * N으로 나눠 N번 더하면 부동소수점에서 원래 값과 미세하게 어긋나고,
         * 그러면 "총량 불변"을 검사할 수가 없다.
         */
        public static double HitShare(int hitIndex, double totalMultiplier)
        {
            if (SummonHits <= 1) return totalMultiplier;

            double share = totalMultiplier / SummonHits;
            if (hitIndex < SummonHits - 1) return share;
            return totalMultiplier - share * (SummonHits - 1);
        }

        // ---------------------------------------------------------------- 로테이션

        /**
         * @brief 봉인한 자루들의 평균 배율. **기대 세기다.**
         *
         * 로테이션이 한 바퀴를 돌면 각 자루가 한 번씩 나오므로, 긴 시간의
         * 평균은 산술평균이다. 봉인한 자루가 없으면 0 - 소환할 것이 없다.
         */
        public static double AverageMultiplier(int[] tiers, int[] rarities = null)
        {
            if (tiers == null) return 0d;

            double total = 0d;
            int sealedCount = 0;
            int count = Math.Min(YodoCatalog.Count, tiers.Length);

            for (int i = 0; i < count; i++)
            {
                if (tiers[i] < 1) continue;
                total += MultiplierAtTier(tiers[i], YodoRarityCurve.At(rarities, i));
                sealedCount++;
            }

            return sealedCount > 0 ? total / sealedCount : 0d;
        }

        /**
         * @brief 영체가 만드는 초당 환산 공격 횟수. 오의의 CastRate와 같은 단위다.
         *
         * ## 전설 요도는 여기에만 붙는다 - 로테이션 밖에서
         *
         * 전설 요도는 소환되지 않고 **소환되는 것들을 키운다**
         * (LegendaryYodoCurve 머리 주석 - 봉인한 혼이 없는 칼이라 무대에
         * 세울 것이 없다). 그래서 평균 안이 아니라 평균 **밖**에 곱해진다:
         * 로테이션의 구성원 수를 바꾸지 않으므로 45단계의 트레이드오프
         * (고르게 올린 쪽이 영체가 세다)가 그대로 남고, 전설이 그 선택을
         * 뒤집지 않는다.
         *
         * 봉인한 자루가 하나도 없으면 0이다 - 전설만 들고 있어도 나올 것이
         * 없다. AverageMultiplier가 0을 내므로 곱이 저절로 0으로 떨어진다.
         */
        public static double RateFor(int[] tiers, int[] rarities = null, int[] legends = null)
        {
            if (CooldownSeconds <= 0d) return 0d;
            return AverageMultiplier(tiers, rarities)
                 * LegendaryYodoCurve.SpiritFactor(legends) / CooldownSeconds;
        }

        /**
         * @brief 순번. 소환 횟수 n번째에 누가 나오는가. 봉인한 자루가 없으면 -1.
         *
         * 봉인한 자루만 줄에 선다. 미봉인을 건너뛰지 않고 순번에 포함하면
         * "소환됐는데 아무도 안 나오는" 턴이 생기고, 화면에서 그것은 쿨다운
         * 버그로 읽힌다.
         *
         * 순번은 저장하지 않는다(런타임). 재시작마다 첫 자루부터 도는 것이
         * 규칙 하나로 끝나고, 저장하면 "껐다 켜서 원하는 영체를 부른다"는
         * 경로가 생긴다 - 오의 쿨다운을 저장하지 않는 것과 같은 판단이다
         * (SkillSystem.RestoreLevels).
         */
        public static int BladeForTurn(int turn, int[] tiers)
        {
            if (tiers == null) return -1;

            int count = Math.Min(YodoCatalog.Count, tiers.Length);
            int sealedCount = 0;
            for (int i = 0; i < count; i++) if (tiers[i] >= 1) sealedCount++;
            if (sealedCount == 0) return -1;

            int wanted = ((turn % sealedCount) + sealedCount) % sealedCount;

            int seen = 0;
            for (int i = 0; i < count; i++)
            {
                if (tiers[i] < 1) continue;
                if (seen == wanted) return i;
                seen++;
            }
            return -1;
        }

        // ---------------------------------------------------------------- 기대 곡선

        /**
         * @brief 영체가 **상성이 이미 걸린 세계 위에** DPS로 곱하는 배수.
         *
         *     (공격속도 + Σ 오의i x 상성i + 영체) / (공격속도 + Σ 오의i x 상성i)
         *
         * 분모에 상성을 포함하는 것이 요점이다. 두 축이 같은 괄호 안에서
         * 더해지므로 순서를 정해야 곱이 정확히 전체와 같아진다 -
         * 상성 배수 x 이 값 = (공격속도 + 상성오의 + 영체)/(공격속도 + 오의).
         * 각자 독립으로 재서 곱하면 그 등식이 깨지고, 그 어긋남이 그대로
         * 보정과 실제의 차가 된다.
         *
         * 상수 몫(YodoAffinityCurve.ReferenceSkillShare 주석)을 쓰는 근거도
         * 저쪽과 같다 - 심층 구간에서 공격속도와 오의 배율이 둘 다 상한이다.
         */
        public static double DpsFactorAfterAffinity(int[] tiers, int[] rarities = null,
                                                    int[] legends = null)
        {
            double attack = YodoAffinityCurve.CappedAttackRate;

            double boosted = 0d;
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                if (spec.CooldownSeconds <= 0d) continue;

                double rate = SkillCurve.CeilingFor(spec.BaseMultiplier) / spec.CooldownSeconds;
                boosted += rate * YodoAffinityCurve.FactorForSkill(i, tiers, rarities, legends);
            }

            double before = attack + boosted;
            if (before <= 0d) return 1d;
            return (before + RateFor(tiers, rarities, legends)) / before;
        }
    }
}
