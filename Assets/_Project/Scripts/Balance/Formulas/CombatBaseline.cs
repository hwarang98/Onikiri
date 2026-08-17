namespace Onikiri.Battle
{
    /**
     * @brief 전투의 시작 스탯 중 강화 곡선에 속하지 않는 값들.
     *
     * 치명타는 아직 강화 대상이 아니라 고정값이다. 그런데 **DPS 계산에는 반드시
     * 들어가야 한다** - 12% 확률 2배 피해는 기대 피해를 12% 올리고, 보스전처럼
     * 제한 시간이 걸린 판정에서 그 12%가 통과와 실패를 가른다.
     *
     * 9단계에서 이 값이 계산에서 빠져 있었다. 마침 보스가 걸어 들어오는 시간
     * (제한 시간을 갉아먹는다)이 비슷한 크기로 반대 방향이라 결과가 우연히 맞았고,
     * 그래서 둘 다 빠져 있다는 사실이 드러나지 않았다. 우연히 맞는 계산은 다음
     * 계수 변경에서 조용히 틀린다.
     *
     * PlayerCombat의 인스펙터 값이 아니라 여기가 단일 출처다. BattleContentBuilder가
     * 이 값을 씬에 기록하고 VerifyWiring이 어긋남을 빌드 시점에 잡는다.
     */
    public static class CombatBaseline
    {
        /** 치명타 확률 */
        public const float CritChance = 0.12f;

        /** 치명타 배수 */
        public const float CritMultiplier = 2f;

        /**
         * @brief 치명타를 포함한 기대 피해 배수.
         *
         * 12% x 2배 = 기대값 1.12배. 시뮬레이션은 한 타격씩 굴리지 않고 이 값을 쓴다 -
         * 30초 동안 수십 번 때리므로 기대값과 실제의 차이가 작고, 무작위를 넣으면
         * 테스트가 실행할 때마다 다른 답을 낸다.
         */
        public static float ExpectedDamageMultiplier
        {
            get { return 1f + CritChance * (CritMultiplier - 1f); }
        }
    }
}
