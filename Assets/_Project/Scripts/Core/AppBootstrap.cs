using UnityEngine;

namespace Onikiri.Core
{
    /**
     * @brief Player Settings로는 표현할 수 없는 런타임 설정을 적용한다.
     *
     * 첫 씬이 로드되기 전에 실행되므로 어느 씬에서 플레이해도 동일하게 걸린다.
     */
    public static class AppBootstrap
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // VSync를 켜두면 지원 플랫폼에서 targetFrameRate를 덮어쓴다
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;

            // 방치형은 손대지 않는 동안에도 화면을 보게 되므로 화면이 꺼지면 안 된다
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
