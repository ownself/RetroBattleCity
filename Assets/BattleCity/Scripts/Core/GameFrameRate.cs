using UnityEngine;

namespace BattleCity
{
    /// <summary>Rendering policy only; the independent 60Hz LogicClock remains unchanged.</summary>
    public static class GameFrameRate
    {
        public const int TargetFramesPerSecond = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            // Desktop ignores targetFrameRate when vSyncCount is nonzero.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFramesPerSecond;
        }
    }
}
