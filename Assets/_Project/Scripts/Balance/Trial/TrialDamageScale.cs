using System;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 귀문의 공통 `damageScale`을 **런타임의 단 한 곳**에서 거는 자리.
     *
     * ## 왜 정적 상태인가 - 우회할 수 없어야 하기 때문이다
     *
     * 소프트캡의 계약은 "모든 플레이어 측 피해가 같은 배율을 한 번 지난다"이다.
     * 그것을 참조로 배선하면 배선을 잊은 경로가 조용히 캡 밖에 서고, 그 경로를
     * 올린 플레이어만 이득을 본다 - 검사가 잡기 가장 어려운 종류의 결함이다.
     *
     * 그래서 배율을 `Enemy.TakeDamage` 안에서 건다. 플레이어가 요괴의 체력을
     * 깎는 경로가 그 함수 하나뿐이므로(`health -= amount`가 프로젝트 전체에서
     * 한 줄이다), 새 피해 출처가 생겨도 자동으로 캡을 지난다.
     *
     * ## 무엇에 걸지 않는가
     *
     *   적 -> 플레이어   `PlayerHealth.TakeDamage`는 이 파일을 모른다.
     *                    캡은 **플레이어의 과잉 화력**을 완만하게 하는 규칙이지
     *                    적을 세게 만드는 규칙이 아니다
     *   귀문 밖          `IsActive`가 false면 `Apply`가 입력을 그대로 낸다.
     *                    일반 스테이지는 이 파일이 없는 것과 같다
     *
     * ## 이중 적용 금지
     *
     * `TrialPowerScore`는 **점수를 계산할 뿐** 피해를 건드리지 않는다. 배율이
     * 실제로 곱해지는 곳은 여기 하나이고, 점수 쪽에서 한 번 더 곱하면 캡이
     * 제곱으로 걸린다. `PromotionTrialDamageTests`가 그 등식을 붙잡는다.
     */
    public static class TrialDamageScale
    {
        /**
         * @brief 피해 출처. **감사(audit)용이다** - 배율 자체는 출처를 안 가린다.
         *
         * 3단계의 계약이 "누락도 중복도 없다"인데, 총합만 세면 그 둘을 구분할 수
         * 없다. 출처별로 세어 두면 "동료가 캡을 안 지났다"와 "동료가 두 번
         * 지났다"가 다른 표로 나온다.
         */
        public enum Source
        {
            /** 어느 경로인지 태그하지 않은 피해. **0이어야 한다** */
            Unattributed = 0,

            AutoAttack,
            Skill,
            Companion,

            /** 영체 소환 등 보스에 듣는 특수 규칙 */
            Special
        }

        static readonly int SourceCount = Enum.GetValues(typeof(Source)).Length;

        /** 지금 걸려 있는 배율. 귀문 밖에서는 1 */
        public static double Current { get; private set; }

        /** 귀문 안인가. `Enter`와 `Exit`이 뒤집는다 */
        public static bool IsActive { get; private set; }

        /** 입장 시 계산한 점수와 기준. 결과 화면과 감사 표가 읽는다 */
        public static double EnteredPower { get; private set; }
        public static double EnteredReference { get; private set; }

        static TrialDamageScale()
        {
            Current = 1d;
        }

        // ---------------------------------------------------------------- 감사

        static readonly BigDouble[] rawBySource = new BigDouble[SourceCount];
        static readonly BigDouble[] scaledBySource = new BigDouble[SourceCount];
        static readonly long[] hitsBySource = new long[SourceCount];

        public static BigDouble RawTotal { get; private set; }
        public static BigDouble ScaledTotal { get; private set; }
        public static long HitCount { get; private set; }

        public static BigDouble RawOf(Source source) { return rawBySource[(int)source]; }
        public static BigDouble ScaledOf(Source source) { return scaledBySource[(int)source]; }
        public static long HitsOf(Source source) { return hitsBySource[(int)source]; }

        /**
         * @brief 감사 장부를 비운다. 입장할 때마다 새로 센다.
         *
         * 시도마다 리셋하는 이유는 재도전이 무제한이기 때문이다 - 누적으로 두면
         * 세 번째 시도의 표가 앞선 두 번과 섞여 아무것도 못 읽는다.
         */
        public static void ResetAudit()
        {
            for (int i = 0; i < SourceCount; i++)
            {
                rawBySource[i] = BigDouble.Zero;
                scaledBySource[i] = BigDouble.Zero;
                hitsBySource[i] = 0L;
            }

            RawTotal = BigDouble.Zero;
            ScaledTotal = BigDouble.Zero;
            HitCount = 0L;
        }

        // ---------------------------------------------------------------- 진입·퇴장

        /**
         * @brief 귀문에 들어간다. **배율은 여기서 한 번만 정해진다.**
         *
         * 전투 중에 다시 계산하지 않는 이유는 `TrialPowerScore` 머리 주석에
         * 있다 - 타격마다 재면 같은 초당 피해라도 연타와 단타의 효율이 갈리고,
         * 그러면 귀문이 지속 화력이 아니라 빌드 모양을 재게 된다.
         *
         * @param power     입장 시점의 `TrialPowerScore.Components.Total`
         * @param reference 이 문의 기준 화력 (`PromotionTrialCatalog.ReferencePowerForGate`)
         * @param k         소프트캡 지수. 후보 확정값은 `PromotionTrialCatalog.SoftCapExponent`
         */
        public static void Enter(double power, double reference, double k)
        {
            EnteredPower = power;
            EnteredReference = reference;
            Current = TrialPowerScore.DamageScale(power, reference, k);
            IsActive = true;

            ResetAudit();
        }

        /**
         * @brief 귀문을 나온다. 승리·실패·폐쇄 **전부** 여기로 온다.
         *
         * 경로를 하나로 묶어 둔 이유는 소프트락과 같은 종류의 위험이기 때문이다 -
         * 어느 한쪽만 되돌리는 것을 잊으면 배율이 일반 스테이지까지 따라 나가고,
         * 그러면 플레이어의 화력이 영구히 깎인 채로 남는다.
         */
        public static void Exit()
        {
            Current = 1d;
            IsActive = false;
        }

        // ---------------------------------------------------------------- 적용

        /**
         * @brief 이 피해에 공통 배율을 건다. **`Enemy.TakeDamage`만 부른다.**
         *
         * 귀문 밖이면 입력을 그대로 낸다 - 곱셈조차 하지 않는다. 일반 스테이지의
         * 피해 계산이 이 스텝 이전과 비트 단위로 같아야 하기 때문이다
         * (`Enemy_TakeDamageIsUnchangedOutsideTheTrial`).
         */
        public static BigDouble Apply(BigDouble raw, Source source)
        {
            if (!IsActive) return raw;

            var scaled = Current >= 1d ? raw : raw * BigDouble.FromDouble(Current);

            int index = (int)source;
            rawBySource[index] += raw;
            scaledBySource[index] += scaled;
            hitsBySource[index]++;

            RawTotal += raw;
            ScaledTotal += scaled;
            HitCount++;

            return scaled;
        }
    }
}
