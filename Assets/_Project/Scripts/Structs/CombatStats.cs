using System;

namespace Onikiri.Progression
{
    /**
     * @brief 네 성장 축이 합쳐진 기대 DPS.
     *
     * 축이 둘일 때는 DPS = 공격력 x 공격속도라 어느 축이든 "값을 몇 % 올리는가"가
     * 곧 "DPS를 몇 % 올리는가"였다. 치명타가 들어오면서 그것이 깨진다.
     *
     *   DPS = 공격력 x 공격속도 x (1 + 치명타율 x (치명타배수 - 1))
     *
     * 치명타율을 12%에서 12.5%로 올리는 것은 값을 4.2% 올리는 일이지만 DPS는
     * 0.45%밖에 오르지 않는다. 그 차이가 배수에 달려 있고, 배수는 다른 축이다.
     * **한 축의 값어치가 다른 축의 현재 값에 의존한다.**
     *
     * 그래서 효율 비교는 "값이 몇 % 오르는가"가 아니라 "이 상태에서 저 축을 한 레벨
     * 올리면 DPS가 몇 % 오르는가"로 재야 한다. 이 구조체가 그 '이 상태'다.
     */
    [Serializable]
    public struct CombatStats
    {
        public double Damage;
        public double AttacksPerSecond;

        /** 0~1 */
        public double CritRate;

        /** 1 이상. 2면 치명타가 두 배 */
        public double CritMultiplier;

        /**
         * @brief 자동 시전 스킬이 만드는 **초당 환산 공격 횟수**.
         *
         * 스킬 한 번은 공격력 x 배율이고 자동 공격 한 대는 공격력 x1이므로,
         * 배율을 쿨다운으로 나누면 두 값이 같은 단위가 된다. 그래서 공격속도에
         * 더할 수 있다(SkillCatalog.CastRate).
         *
         * **더하는 자리가 괄호 안이라 치명타와 스탯 포인트 증폭이 자동으로
         * 상속된다.** 지시가 요구한 "스킬 데미지 = 공격력 x 배율(치명타·증폭
         * 상속)"이 계수를 따로 곱하지 않고 구조에서 나온다.
         *
         * 기본값 0이 중요하다. 스킬이 없던 시절의 스탯(AtLevel/CappedAtLevel과
         * 효율 지표가 만드는 것)이 그대로 예전 값을 낸다.
         */
        public double SkillRate;

        /**
         * @brief 영체 소환이 만드는 **초당 환산 공격 횟수** (45단계).
         *
         * SkillRate와 같은 자리, 같은 단위다 - 소환 한 번이 공격력 x 배율
         * 뭉치이고 그것을 쿨다운으로 나눴다. 오의와 같은 괄호 안이라
         * 치명타·증폭·초월·연격을 자동으로 상속한다.
         *
         * **펫과 나눠 둔 것이 설계다.** 펫은 괄호 밖의 (1 + 보너스)이고
         * 영체는 괄호 안의 항이다 - 상시 합산과 일시 버스트라는 결의 차이가
         * 식의 자리로 적혀 있다(YodoSpiritCurve 머리 주석).
         *
         * SkillRate와 굳이 합치지 않은 이유는 죽은 버튼 검사다. 합치면
         * "오의가 세진 것"과 "영체가 때린 것"이 한 숫자가 되어, 둘 중 어느
         * 쪽이 진행을 움직였는지 잴 수 없다.
         *
         * 기본값 0이 중요하다 - 영체가 없던 시절의 스탯(AtLevel/CappedAtLevel과
         * 효율 지표가 만드는 것)이 그대로 예전 값을 낸다. SkillRate·PetBonus와
         * 같은 규칙이다.
         */
        public double SpiritRate;

        /**
         * @brief 액티브 펫이 DPS에 더하는 몫 (0~0.5).
         *
         * 곱하는 자리가 괄호 **바깥**이다. 펫의 한 타가 "보너스 x 플레이어 기대
         * DPS x 공격 간격"이라(PetCurve 주석) 치명타·스킬까지 다 곱해진 값을
         * 기준으로 삼기 때문이다 - SkillRate처럼 괄호 안에 더하면 펫 기여가
         * 치명타 계수를 한 번 더 곱해 실제보다 크게 계산된다.
         *
         * 기본값 0이 중요하다. 펫이 없던 시절의 스탯(AtLevel/CappedAtLevel과
         * 효율 지표가 만드는 것)이 그대로 예전 값을 낸다 - SkillRate와 같은 규칙.
         */
        public double PetBonus;

        /**
         * @brief 초월 치명타 배수 (43단계 심화 축). 피해 전체에 곱해진다.
         *
         * 기본값 0이 중요하다 - TranscendFactor가 1 미만을 1로 올리므로,
         * 심화 축이 없던 시절의 스탯(AtLevel 등)이 그대로 예전 값을 낸다.
         * SkillRate·PetBonus와 같은 규칙이다.
         */
        public double TranscendMultiplier;

        /** 연격 확률 (0~1). 타격마다 한 번 더 - 기대값으로는 x(1 + 확률) */
        public double ComboChance;

        /**
         * @brief 치명타 기대값을 포함한 초당 피해.
         *
         * 한 타격씩 굴리지 않고 기대값을 쓴다. 보스전은 30초에 수십 번 때리므로
         * 기대값과 실제의 차이가 작고, 무작위를 넣으면 테스트가 실행할 때마다
         * 다른 답을 낸다.
         *
         * 스킬도 같은 기대값 처리다. 쿨다운 8~25초를 30초 보스전에 펴면 실제
         * 시전 횟수는 정수(3~4번, 1~2번)라 기대값과 어긋나는 폭이 치명타보다
         * 크지만, 그 어긋남을 모델링하려면 "언제 보스전이 시작했는가"라는
         * 임의의 가정이 하나 더 필요하다. 대신 시뮬레이션이 보고하는 보스 여유가
         * 정수 시전 횟수에 대해 안전한 쪽인지를 테스트가 따로 확인한다
         * (SkillDps_SurvivesIntegerCastCounts).
         */
        public double ExpectedDps
        {
            get
            {
                return Damage * (AttacksPerSecond + SkillRate + SpiritRate) * CritFactor * PetFactor
                     * TranscendFactor * ComboFactor;
            }
        }

        /** 펫이 DPS에 곱하는 배수. 펫이 없으면 1 */
        public double PetFactor
        {
            get { return 1d + (PetBonus < 0d ? 0d : PetBonus); }
        }

        /** 초월 치명타가 곱하는 배수. 축이 없으면(기본값 0) 1 */
        public double TranscendFactor
        {
            get { return TranscendMultiplier < 1d ? 1d : TranscendMultiplier; }
        }

        /** 연격이 곱하는 배수. 추가타가 온전한 한 타라 기대값이 (1 + 확률)이다 */
        public double ComboFactor
        {
            get { return 1d + (ComboChance < 0d ? 0d : ComboChance > 1d ? 1d : ComboChance); }
        }

        /** 치명타가 DPS에 곱하는 배수. 치명타가 없으면 1 */
        public double CritFactor
        {
            get
            {
                double rate = CritRate < 0d ? 0d : CritRate;
                double bonus = CritMultiplier - 1d;
                if (bonus < 0d) bonus = 0d;
                return 1d + rate * bonus;
            }
        }

        /**
         * @brief 모든 축이 같은 레벨일 때의 스탯.
         *
         * 효율 비교의 기준 상태다. 축마다 다른 레벨을 가정하면 비교할 때마다
         * 나머지 세 축의 레벨을 함께 정해야 하고, 그러면 결과가 그 선택에 좌우된다.
         */
        public static CombatStats AtLevel(int level)
        {
            return new CombatStats
            {
                Damage = AttackPowerCurve.ValueAtLevel(level),
                AttacksPerSecond = AttackSpeedCurve.ValueAtLevel(level),
                CritRate = CritRateCurve.ValueAtLevel(level),
                CritMultiplier = CritDamageCurve.ValueAtLevel(level),
                TranscendMultiplier = TranscendCurve.MultiplierAtLevel(level),
                ComboChance = ComboCurve.UncappedChanceAtLevel(level)
            };
        }

        /**
         * @brief 상한이 적용된, 전투가 실제로 쓰는 스탯.
         *
         * AtLevel은 상한을 무시한다 - 효율 지표가 보는 것은 곡선의 형태이지 지금
         * 낼 수 있는 값이 아니기 때문이다(UpgradeEfficiency 참고). 진행 시뮬레이션은
         * 반대로 실제 값이 필요하므로 이쪽을 쓴다.
         */
        public static CombatStats CappedAtLevel(int level)
        {
            return new CombatStats
            {
                Damage = AttackPowerCurve.ValueAtLevel(level),
                AttacksPerSecond = AttackSpeedCurve.CappedValueAtLevel(level),
                CritRate = CritRateCurve.CappedValueAtLevel(level),
                CritMultiplier = CritDamageCurve.ValueAtLevel(level),
                TranscendMultiplier = TranscendCurve.MultiplierAtLevel(level),
                ComboChance = ComboCurve.ChanceAtLevel(level)
            };
        }

        /**
         * @brief 한 축만 다른 값으로 바꾼 사본.
         *
         * 효율 계산이 "이 축만 한 레벨 올렸을 때"를 만들 때 쓴다. 축을 문자열
         * id로 고르는 이유는 UpgradeTrack이 자기가 어느 스탯을 먹이는지 id로만
         * 알기 때문이다 - UpgradeSystem.Apply와 같은 기준이다.
         */
        public CombatStats With(string trackId, double value)
        {
            var copy = this;
            switch (trackId)
            {
                case UpgradeSystem.AttackPowerId: copy.Damage = value; break;
                case UpgradeSystem.AttackSpeedId: copy.AttacksPerSecond = value; break;
                case UpgradeSystem.CritRateId: copy.CritRate = value; break;
                case UpgradeSystem.CritDamageId: copy.CritMultiplier = value; break;
                case UpgradeSystem.TranscendId: copy.TranscendMultiplier = value; break;
                case UpgradeSystem.ComboId: copy.ComboChance = value; break;
            }
            return copy;
        }

        /**
         * @brief 이 축이 DPS에 기여하는 통로가 있는가. 없으면 이 자로는 잴 수 없다.
         *
         * 11단계의 체력·회복은 여기서 false다. 그 축들은 SurvivalEfficiency가
         * 유효체력으로 잰다. 두 자는 단위가 달라 나눌 수 없고, 그래서 공격 계열과
         * 생존 계열의 균형은 지표가 아니라 시뮬레이션 게이트로 잡는다.
         */
        public static bool FeedsDps(string trackId)
        {
            return trackId == UpgradeSystem.AttackPowerId
                || trackId == UpgradeSystem.AttackSpeedId
                || trackId == UpgradeSystem.CritRateId
                || trackId == UpgradeSystem.CritDamageId
                || trackId == UpgradeSystem.TranscendId
                || trackId == UpgradeSystem.ComboId;
        }
    }
}
