#if UNITY_EDITOR
using System.Collections;
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
    public sealed class RenderLoopRegressionTests
    {
        private Scene scene;
        private Keyboard keyboard;

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealUpdate_PauseResumeDoesNotAccumulateTimeAndKeyboardShotHitsSteel()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/CombatArena.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var session = controller.Session;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            session.SetFocus(true);
            session.SetPaused(false);
            // Set up a clear firing lane facing the real arena's steel at x=80..96, y=48.
            for (int i = 0; i < 64; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D);
            session.SetPaused(true);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(88, 24)));
            long pausedTick = session.Clock.Tick;
            long dropped = session.Clock.DroppedTicks;
            int frame = Time.frameCount;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(session.Clock.Tick, Is.EqualTo(pausedTick));
            Assert.That(session.Clock.DroppedTicks, Is.EqualTo(dropped));
            session.SetPaused(false);
            int resumeFrame = Time.frameCount;
            float resumeTime = Time.realtimeSinceStartup;
            while (session.Clock.Tick == pausedTick && Time.realtimeSinceStartup - resumeTime < 1)
            {
                yield return null;
                // A paused interval must not be included in the first advancing Update.
                Assert.That(session.Clock.Tick - pausedTick,
                    Is.LessThanOrEqualTo(Mathf.Min(LogicClock.MaxTicksPerFrame, Mathf.CeilToInt(Time.unscaledDeltaTime * 60) + 1)));
            }
            Assert.That(Time.frameCount, Is.GreaterThan(resumeFrame));
            Assert.That(session.Clock.Tick, Is.GreaterThan(pausedTick));
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(88, 24)));
            Debug.Log($"BC108 realResume frame={Time.frameCount} tick={session.Clock.Tick} pausedTick={pausedTick} delta={Time.unscaledDeltaTime:R} dropped={session.Clock.DroppedTicks}");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            InputSystem.Update();
            float deadline = Time.realtimeSinceStartup + 2;
            while (session.Effects.Active.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            session.SetPaused(true);
            Assert.That(session.Effects.Active.Count, Is.EqualTo(1));
            Assert.That(session.Effects.Active[0].Position, Is.EqualTo(new Vector2(88, 46)));
            Assert.That(session.Player.Position.y, Is.GreaterThan(24));
            Assert.That(session.Player.Position.y, Is.LessThanOrEqualTo(40));
            Assert.That(session.Map.Revision, Is.Zero, "Steel stops the shot without being destroyed");
            Assert.That(session.Map.TerrainAt(11, 6), Is.EqualTo(TerrainKind.Steel));
            Assert.That(session.Bullets.Active, Is.Empty);
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
            yield return null;
            Assert.That(controller.EffectView.GetComponentsInChildren<SpriteRenderer>().Single().sprite.name,
                Does.StartWith("effect.explosion.small."));
            Assert.That(controller.mapView.obstacles.HasTile(new Vector3Int(11, 6, 0)), Is.True);
            Debug.Log($"BC108 realSteel frame={Time.frameCount} tick={session.Clock.Tick} dropped={session.Clock.DroppedTicks} player={session.Player.Position} revision={session.Map.Revision} effectAge={session.Effects.Active[0].AgeTicks}");
        }
    }
}
#endif
