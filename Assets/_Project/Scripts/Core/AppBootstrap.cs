using UnityEngine;

namespace Onikiri.Core
{
    /// <summary>
    /// Applies runtime application settings that cannot live in Player Settings.
    /// Runs before the first scene loads so it applies no matter which scene is played.
    /// </summary>
    public static class AppBootstrap
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // VSync would override targetFrameRate on platforms that support it.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;

            // An idle game is watched while not being touched; don't dim the screen.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
