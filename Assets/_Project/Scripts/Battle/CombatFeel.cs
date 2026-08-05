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
    }
}
