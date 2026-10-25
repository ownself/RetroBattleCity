using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class GameFrameRateTests
    {
        [TestCase(0, -1, 30)] [TestCase(1, 144, 60)] [TestCase(2, 30, 144)]
        public void ApplyOverridesVSyncAndOldCap_WithoutChangingLogicRate(int vSync, int oldCap, int simulatedFps)
        {
            int originalCap = Application.targetFrameRate, originalVSync = QualitySettings.vSyncCount;
            float originalTimeScale = Time.timeScale, originalFixedStep = Time.fixedDeltaTime;
            try
            {
                QualitySettings.vSyncCount = vSync; Application.targetFrameRate = oldCap;
                GameFrameRate.Apply();
                Assert.That(Application.targetFrameRate, Is.EqualTo(60));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalFixedStep));
                var clock = new LogicClock();
                for (int frame = 0; frame < simulatedFps; frame++) clock.Advance(1.0 / simulatedFps, () => { });
                Assert.That(clock.Tick, Is.EqualTo(60));
                Assert.That(clock.DroppedTicks, Is.Zero);
            }
            finally
            {
                Application.targetFrameRate = originalCap; QualitySettings.vSyncCount = originalVSync;
            }
        }
    }
}
