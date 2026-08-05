using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 타격 피드백의 지속 시간 규칙.
     *
     * 히트스톱과 화면 흔들림은 초당 1회 공격에서는 좋지만 초당 10회에서는 견딜 수 없다.
     * 방치형은 결국 초당 10회에 도달한다. 공격속도가 핵심 성장 축이라 후반에는
     * 사무라이가 쉬지 않고 휘두르기 때문이다. 70ms 고정 정지라면 대부분의 시간이
     * 정지 상태가 되어 게임이 계속 끊기는 것처럼 보인다.
     *
     * 그래서 각 효과에 '고정 길이'가 아니라 '초당 화면 점유 예산'을 준다.
     * 교차점 아래에서는 설계한 길이를 그대로 쓰고, 위에서는 효과를 줄여
     * 아무리 빨리 공격해도 총량이 한계를 넘지 않게 한다.
     */
    public static class CombatFeel
    {
        /**
         * @brief 주어진 공격 속도에서의 효과 길이.
         *
         * 계산식: min(baseSeconds, budgetPerSecond / attacksPerSecond)
         */
        public static float ScaledDuration(float baseSeconds, float budgetPerSecond, float attacksPerSecond)
        {
            if (attacksPerSecond <= 0f) return baseSeconds;
            return Mathf.Min(baseSeconds, budgetPerSecond / attacksPerSecond);
        }

        /**
         * @brief 효과가 줄어들기 시작하는 공격 속도.
         *
         * 밸런싱할 때와 동작이 바뀌는 지점을 문서화할 때 쓴다.
         */
        public static float CrossoverAttackSpeed(float baseSeconds, float budgetPerSecond)
        {
            if (baseSeconds <= 0f) return float.PositiveInfinity;
            return budgetPerSecond / baseSeconds;
        }

        /**
         * @brief 캐릭터 애니메이션을 원속도의 몇 배까지 당겨도 되는가.
         *
         * 히트스톱·흔들림·참격은 '초당 예산'으로 줄이면 되지만, 캐릭터 스윙은 그럴 수
         * 없다. 스윙은 프레임 수가 정해진 손그림이고, 재생만 빨리 하면 프레임 사이
         * 간격이 그대로 짧아진다. 7프레임 스윙을 4배로 당기면 프레임 하나가 18ms라
         * 60fps에서 한 프레임씩만 스치고 지나간다. 그 순간 눈에 들어오는 것은 '빠른
         * 발도'가 아니라 스프라이트가 깜빡이는 노이즈다.
         *
         * 2배가 한계인 이유는 픽셀 아트의 프레임 밀도에 있다. 원본이 14fps로 그려져
         * 있으므로 2배는 28fps - 60fps 화면에서 프레임당 두 화면 프레임을 차지해
         * 아직 각 자세가 읽힌다. 그 위로는 한 화면 프레임짜리 자세가 생긴다.
         *
         * 이 상수가 공격속도 강화의 상한을 결정한다. 스윙이 공격 간격 안에 들어가야
         * 하고 스윙은 2배까지만 빨라질 수 있으므로, 낼 수 있는 최대 공격속도는
         * 아트가 정한다. AttackSpeedCurve 참고.
         */
        public const float MaxAnimationSpeed = 2f;

        /** 2배속까지 당겼을 때의 스윙 길이. 이보다 짧게 재생하지 않는다 */
        public static float MinSwingDuration(float baseDuration)
        {
            return baseDuration / MaxAnimationSpeed;
        }

        /**
         * @brief 스윙이 공격 간격 안에 들어가면서 낼 수 있는 최대 공격속도.
         *
         * 스윙 길이의 역수다. 이 값을 넘기면 이전 스윙이 아직 재생 중일 때 다음
         * 스윙이 시작되어 동작이 뭉개진다.
         */
        public static float MaxAttacksPerSecond(float baseDuration)
        {
            if (baseDuration <= 0f) return float.PositiveInfinity;
            return MaxAnimationSpeed / baseDuration;
        }
    }
}
