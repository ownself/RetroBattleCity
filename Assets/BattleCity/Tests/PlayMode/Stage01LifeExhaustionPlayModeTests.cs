#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    public sealed class Stage01LifeExhaustionPlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private TankDefinition shotDefinition;
        private const string EvidenceDirectory = "Docs/Stage2/BC-208-209";

        private void Keys(params Key[] keys)
        {
            if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private static IEnumerator Until(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "bounded Stage01 production Update observation");
        }

        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Directory.CreateDirectory(EvidenceDirectory);
                File.WriteAllBytes(Path.Combine(EvidenceDirectory, name + ".png"), image.EncodeToPNG());
            }
            finally { Object.Destroy(image); }
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (shotDefinition != null) Object.Destroy(shotDefinition);
            shotDefinition = null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator RealUpdate_ThreeStage01LivesExhaustedByControlledEnemyBullets_LostThenPhysicalRReady()
        {
            Keys();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            c.SendMessage("OnApplicationFocus", true);
            var s = c.Session;
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Ready));
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3));
            int firstFrame = Time.frameCount;
            Keys(Key.Enter);
            yield return Until(() => s.Clock.Tick > 0);
            Keys();
            long firstTick = s.Clock.Tick;
            int outcomes = 0;
            s.SessionEnded += () => outcomes++;
            shotDefinition = Object.Instantiate(c.playerDefinition);
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                yield return Until(() => s.PlayerTank.IsAlive && !s.PlayerLife.IsProtected);
                Assert.That(s.PlayerTank.HitPoints, Is.EqualTo(1));
                // Only muzzle placement is controlled: unchanged Stage01 damage/life/timer rules,
                // real enemy projectile sweep, and Controller.Update settle every death and birth.
                var shot = new WeaponState(shotDefinition, s.Bullets, CombatTeam.Enemy, 999);
                Assert.That(shot.Step(new TankCommand(CardinalDirection.Down, true),
                    s.Player.Position + Vector2.up * WeaponState.MuzzleOffsetPixels, CardinalDirection.Down), Is.True);
                yield return Until(() => !s.PlayerTank.IsAlive);
                Assert.That(shot.ActiveBullets, Is.Zero);
                Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(remaining));
                Assert.That(c.hud.DisplayedLives, Is.EqualTo(remaining));
                Assert.That(s.Base.IsAlive, Is.True);
                Debug.Log($"BC209 controlled death frame={Time.frameCount} tick={s.Clock.Tick} lives={remaining} phase={s.Phase}");
                if (remaining > 0)
                {
                    Assert.That(s.Phase, Is.EqualTo(SessionPhase.Playing));
                    yield return Until(() => s.PlayerTank.IsAlive);
                    Assert.That(s.PlayerLife.IsProtected, Is.True);
                    Debug.Log($"BC209 controlled birth frame={Time.frameCount} tick={s.Clock.Tick} lives={s.PlayerLife.RemainingLives}");
                }
            }
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Lost));
            Assert.That(s.FailureReason, Is.EqualTo(SessionFailureReason.PlayerLivesExhausted));
            Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(c.hud.phaseCaption.Text, Is.EqualTo("LOST"));
            long terminalTick = s.Clock.Tick;
            yield return Capture("LifeExhausted-Lost");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(s.Clock.Tick, Is.EqualTo(terminalTick));
            Keys(Key.R);
            yield return Until(() => c.Session != s);
            Keys();
            Assert.That(c.Session.Phase, Is.EqualTo(SessionPhase.Ready));
            Assert.That(c.Session.Clock.Tick, Is.Zero);
            Assert.That(c.Session.PlayerLife.RemainingLives, Is.EqualTo(3));
            Assert.That(c.Session.EnemyWave.PendingCount, Is.EqualTo(20));
            Assert.That(c.Session.Base.IsAlive, Is.True);
            Assert.That(c.hud.phaseCaption.Text, Is.EqualTo("READY  ENTER START"));
            yield return Capture("LifeExhausted-R-Ready");
            Assert.That(Time.frameCount, Is.GreaterThan(firstFrame));
            Assert.That(terminalTick, Is.GreaterThan(firstTick));
            Assert.That(c.Session.Clock.Tick, Is.Zero);
            Debug.Log($"BC209 controlled realUpdate frames={firstFrame}->{Time.frameCount} ticks={firstTick}->{terminalTick} restartTick={c.Session.Clock.Tick} outcomes={outcomes} droppedTicks={s.Clock.DroppedTicks}");
        }
    }
}
#endif
