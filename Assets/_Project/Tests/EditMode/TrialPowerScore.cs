using System;

namespace Onikiri.Tests
{
    /**
     * @brief 귀문 소프트캡의 **구현 가능한 계약**. 테스트 전용 정의다.
     *
     * v1.4는 소프트캡을 "actualDps"라는 추상값 위에 정의했다. 그것으로는 구현할
     * 수 없다 - 런타임에 `actualDps`라는 값이 없고, 무엇을 세고 무엇을 빼는지가
     * 정해져 있지 않으면 사람마다 다른 수를 넣는다.
     *
     * ## 두 가지를 못 박는다
     *
     *   무엇을 세는가   `Components` 의 여섯 항. 하나라도 빠지면 그 축을 올린
     *                   플레이어가 캡을 우회한다
     *   언제 정하는가   **입장 시 한 번.** 전투 중에 다시 계산하지 않는다
     *
     * ## 왜 타격마다 걸면 안 되는가
     *
     * 캡을 개별 타격에 걸면 같은 초당 피해라도 **연타 빌드와 단타 빌드의 효율이
     * 달라진다** - 한 방이 큰 쪽만 깎이기 때문이다. 그러면 귀문이 빌드를
     * 고르는 시험이 되고, 그것은 이 시험이 재기로 한 것(지속 화력)이 아니다.
     *
     * 그래서 입장 시 **공통 배율 `damageScale` 하나**를 정하고, 귀문 안의 모든
     * 플레이어 측 피해 경로(평타·오의·다단·지속·동료)에 같은 값을 곱한다.
     *
     * ## 기준(ref)은 시험 자신에서 나온다
     *
     * `ref = 귀문 총 체력 / 기준 시간(41초)`. 별도 곡선 상수를 두지 않는 것이
     * 요점이다 - 적 체력이 바뀌면 기준도 같이 움직이므로 두 값이 어긋날 수 없다.
     */
    public static class TrialPowerScore
    {
        /**
         * @brief 점수에 들어가는 여섯 항. **전부 초당 피해로 환산된 값이다.**
         *
         * 무기·장비 배수와 귀문으로 받은 `evolutionTier` 배수는 각 항에 **이미
         * 곱해져 있어야 한다** - 곱 자리가 `UpgradeSystem.Apply` 한 곳이므로
         * 런타임에서도 자연히 그렇게 된다.
         */
        public struct Components
        {
            /** 평타. 공격력 x 공격속도 x 치명타 기대배수 */
            public double AutoAttack;

            /** 장착 오의. 배율 x 공격력 x 상성 / 쿨타임. 다단은 타수를 합산한다 */
            public double Skills;

            /** 다단 히트 중 위 항에 안 들어간 몫 (연격 등) */
            public double MultiHit;

            /** 지속 피해(출혈 등)의 초당 환산 */
            public double DamageOverTime;

            /** 동료(펫)의 초당 피해 */
            public double Companions;

            /**
             * @brief 처형 등 특수 규칙의 **보스 적용값.**
             *
             * 귀문의 적은 셋 다 보스다. 잡몹에만 듣는 규칙은 0으로 넣고,
             * 보스에 듣는 규칙은 그 값으로 환산해 넣는다 - "잡몹 기준으로
             * 세고 보스전에서 안 듣는" 축이 캡을 부풀리면 그 플레이어만
             * 손해를 본다.
             */
            public double BossApplicableSpecials;

            public double Total
            {
                get
                {
                    return AutoAttack + Skills + MultiHit
                         + DamageOverTime + Companions + BossApplicableSpecials;
                }
            }
        }

        /** 기준 시간. `M`을 유도할 때 쓴 값과 같아야 한다 */
        public const double ReferenceSeconds = 41d;

        /** 이 귀문의 기준 화력 = 총 체력 / 기준 시간 */
        public static double ReferencePower(double totalTrialHealth)
        {
            return totalTrialHealth / ReferenceSeconds;
        }

        /**
         * @brief 입장 시 한 번 정하는 공통 배율.
         *
         *   power <= ref   damageScale = 1
         *   power >  ref   effective = ref x (power/ref)^k
         *                  damageScale = effective / power
         *
         * 기준 이하는 손대지 않는다 - 약한 플레이어를 더 약하게 만들 이유가
         * 없고, "과잉 화력만 완만하게 줄인다"가 이 규칙의 전부다.
         */
        public static double DamageScale(double power, double reference, double k)
        {
            if (power <= 0d) return 1d;
            if (reference <= 0d) return 1d;
            if (power <= reference) return 1d;

            double effective = reference * Math.Pow(power / reference, k);
            return effective / power;
        }

        /** 캡을 지난 뒤의 실효 화력 */
        public static double EffectivePower(double power, double reference, double k)
        {
            return power * DamageScale(power, reference, k);
        }

        /**
         * @brief 공통 배율을 여섯 항에 각각 곱한다. **합에 곱한 것과 같아야 한다.**
         *
         * 이 함수가 존재하는 이유는 그 등식을 테스트가 붙잡기 위해서다 -
         * 런타임에서 동료나 지속 피해가 배율을 우회하면 여기서 갈린다.
         */
        public static Components Apply(Components c, double damageScale)
        {
            return new Components
            {
                AutoAttack = c.AutoAttack * damageScale,
                Skills = c.Skills * damageScale,
                MultiHit = c.MultiHit * damageScale,
                DamageOverTime = c.DamageOverTime * damageScale,
                Companions = c.Companions * damageScale,
                BossApplicableSpecials = c.BossApplicableSpecials * damageScale
            };
        }

        /**
         * @brief 같은 총점을 **다른 구성**으로 만든다. 타격당 캡 금지의 반례용.
         *
         * 연타 빌드(평타 위주)와 단타 빌드(오의 위주)가 같은 점수를 갖게 만들고,
         * 두 빌드의 실효 화력이 같은지 검사한다. 타격마다 캡을 걸면 여기가 갈린다.
         */
        public static Components Redistribute(double total, double autoShare)
        {
            return new Components
            {
                AutoAttack = total * autoShare,
                Skills = total * (1d - autoShare)
            };
        }
    }
}
