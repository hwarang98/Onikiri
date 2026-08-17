using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 요괴 보충 간격을 필드 상태에 맞춰 조절하는 규칙.
     *
     * 9단계 시뮬레이션에서 드러난 문제를 고친다. 4스테이지부터 잡몹 처치 시간이
     * 11.0초에 고정됐는데, 그것은 10마리 x 1.1초 - **처치가 공격력이 아니라 요괴
     * 공급에 묶였다는 뜻이다.** 그 구간에서는 공격력을 아무리 올려도 잡몹 파밍
     * 속도가 1초도 줄지 않는다.
     *
     * 동시 생존 수를 늘리는 방법은 쓰지 않았다. 세로 화면의 전투 영역은 가로
     * 6.75 units뿐이고 핸드오프 문서가 읽히는 한계를 3~5마리로 잡았다. 다섯을
     * 넘기면 요괴가 겹쳐 그려져 픽셀 아트가 뭉개진다 - 성장을 살리려다 화면을
     * 잃는 교환이다.
     *
     * 대신 **간격을 줄인다.** 화면에 있는 수는 그대로 두고 회전을 빠르게 한다.
     *
     * 조절은 되먹임으로 한다. 보충 시점에 필드가 목표 수에 못 미치면(=요괴가
     * 들어오는 것보다 빨리 죽는다) 간격을 줄이고, 목표를 채우고 있으면(=쌓인다)
     * 늘린다. 이 규칙은 플레이어의 DPS를 알 필요가 없고, 스스로 처치 시간에
     * 수렴한다 - 간격이 처치 시간보다 길면 굶고, 짧으면 쌓이므로 평형이 곧
     * 처치 시간이다.
     *
     * 하한 0.4초는 그 수렴을 막는 지점이다. 그 아래로는 요괴가 걸어 들어오는
     * 동작조차 보이지 않고 화면 오른쪽에서 튀어나오는 것처럼 된다.
     */
    public static class SpawnPacing
    {
        /** 성장하지 않은 상태의 보충 간격. 여기서 시작해 줄어든다 */
        public const float BaseInterval = 1.1f;

        /** 아무리 빨리 죽여도 이보다 촘촘해지지 않는다 */
        public const float MinInterval = 0.4f;

        /** 굶고 있을 때 간격에 곱하는 값. 한 번에 15%씩 좁힌다 */
        public const float TightenFactor = 0.85f;

        /**
         * @brief 쌓이고 있을 때 간격에 곱하는 값.
         *
         * 좁히는 쪽보다 느리게 되돌린다. 스테이지가 올라 요괴가 갑자기 단단해지는
         * 순간마다 간격이 튀어 오르면, 그 직후 몇 초 동안 화면이 눈에 띄게 비어 보인다.
         */
        public const float RelaxFactor = 1.06f;

        /**
         * @brief 다음 보충 간격.
         *
         * @param current 지금 간격
         * @param starved 보충 시점에 필드가 목표 생존 수에 못 미쳤는가
         */
        public static float Next(float current, bool starved)
        {
            float next = starved ? current * TightenFactor : current * RelaxFactor;
            return Mathf.Clamp(next, MinInterval, BaseInterval);
        }

        /**
         * @brief 되먹임이 수렴하는 간격.
         *
         * 시뮬레이션이 쓴다. 런타임은 매 보충마다 Next로 한 걸음씩 움직이지만,
         * 그 평형은 처치 시간을 하한/상한으로 자른 값이다. 스테이지 하나가
         * 10마리라 수렴에 드는 몇 마리는 전체 시간에서 무시할 수 있다.
         */
        public static float SettledInterval(double secondsToKill)
        {
            return Mathf.Clamp((float)secondsToKill, MinInterval, BaseInterval);
        }
    }
}
