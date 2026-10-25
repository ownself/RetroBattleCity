#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    public sealed class GameFrameRatePlayModeTests
    {
        private Scene scene;
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator StartupHookCapsGameBeforeSceneLoad_RealFramesAdvance()
        {
            // Do not call Apply here: verify the automatic runtime startup entry point.
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/SinglePlayer.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Assert.That(scene.isLoaded, Is.True);
            int frame = Time.frameCount; float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < .5f) yield return null;
            int frames = Time.frameCount - frame; float elapsed = Time.realtimeSinceStartup - start;
            Assert.That(frames, Is.GreaterThan(0));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            Debug.Log($"Frame cap: target=60 vSync=0; Editor sample frames={frames} elapsed={elapsed:F3}s measured={frames / elapsed:F1}fps (not a standalone timing guarantee)");
        }
    }
}
#endif
