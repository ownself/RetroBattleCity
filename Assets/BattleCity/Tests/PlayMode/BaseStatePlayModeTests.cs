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
    public sealed class BaseStatePlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private ArenaController Controller => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
        private void Keys(params Key[] keys)
        {
            if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator Stage01_RealUpdateKeyboardWeaponBreachesBrickAndDestroysBase_DeadProjectionAndOneNotification()
        {
            Keys();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = Controller; var s = c.Session;
            Assert.That(s.Base, Is.Not.Null); Assert.That(s.Base.IsAlive, Is.True);
            Assert.That(c.baseRenderer.sprite, Is.SameAs(c.sprites.Get("base.alive")));
            Assert.That(s.EnemyWave.TotalCount, Is.EqualTo(20), "Use default scene, not a wave-disabled fixture");
            Assert.That(c.baseRenderer.GetComponents<Component>().Length, Is.EqualTo(2));
            int notifications = 0, explosionsAtNotification = 0;
            s.BaseDestroyed += () => {
                notifications++;
                explosionsAtNotification = s.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion && e.Position == s.Base.Position);
            };
            s.SetFocus(true); s.SetPaused(false); Keys(Key.D, Key.Space);
            int firstFrame = Time.frameCount; long firstTick = s.Clock.Tick;
            float deadline = Time.realtimeSinceStartup + 5;
            while (s.Base.IsAlive && Time.realtimeSinceStartup < deadline) yield return null;
            Keys(); s.SetPaused(true); yield return null;
            Assert.That(s.Base.IsAlive, Is.False); Assert.That(s.BaseFailureRequested, Is.True);
            Assert.That(Time.frameCount, Is.GreaterThan(firstFrame)); Assert.That(s.Clock.Tick, Is.GreaterThan(firstTick));
            Assert.That(s.Map.Revision, Is.GreaterThan(0), "Real player weapon must first breach the protecting brick");
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(explosionsAtNotification, Is.EqualTo(1));
            Assert.That(c.baseRenderer.sprite, Is.SameAs(c.sprites.Get("base.dead")));
            Assert.That(PixelGrid.ToMapPixels(c.baseRenderer.transform.position), Is.EqualTo(s.Base.Position));
            Assert.That(s.Player.Position.x, Is.LessThanOrEqualTo(88), "Ruins still block the player");
            long deadTick = s.Clock.Tick; int pausedFrame = Time.frameCount;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(s.Clock.Tick, Is.EqualTo(deadTick)); Assert.That(Time.frameCount, Is.GreaterThan(pausedFrame));
            s.SetPaused(false); deadline = Time.realtimeSinceStartup + 3;
            while (s.Clock.Tick < deadTick + 35 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(s.Clock.Tick, Is.GreaterThanOrEqualTo(deadTick + 35), "Failure request does not freeze combat");
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(s.BaseFailureRequested, Is.True);
            Assert.That(c.baseRenderer.sprite, Is.SameAs(c.sprites.Get("base.dead")));
            Assert.That(s.Effects.Active.Any(e => e.Kind == CombatEffectKind.Explosion && e.Position == s.Base.Position), Is.False);
            Debug.Log($"BC204 realBase frame={firstFrame}->{Time.frameCount} tick={firstTick}->dead:{deadTick}->final:{s.Clock.Tick} notifications={notifications} explosionsAtNotification={explosionsAtNotification} sprite={c.baseRenderer.sprite.name} revision={s.Map.Revision} dropped={s.Clock.DroppedTicks}");
        }

        [UnityTest] public IEnumerator Stage01_ReloadCreatesIndependentIntactBaseAndNoFailureRequest()
        {
            BaseState previous = null;
            for (int run = 0; run < 2; run++)
            {
                scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                var c = Controller; var s = c.Session; s.SetPaused(true);
                Assert.That(s.Base, Is.Not.SameAs(previous)); Assert.That(s.Base.IsAlive, Is.True);
                Assert.That(s.BaseFailureRequested, Is.False); Assert.That(c.baseRenderer.sprite.name, Is.EqualTo("base.alive"));
                previous = s.Base;
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest] public IEnumerator CombatArenaUnboundRendererMeansNoBaseOrFailureTarget()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/CombatArena.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = Controller; var s = c.Session; s.SetPaused(true);
            Assert.That(c.baseRenderer, Is.Null); Assert.That(s.Base, Is.Null); Assert.That(s.BaseFailureRequested, Is.False);
            Assert.That(s.EnemyWave, Is.Null);
        }
    }
}
#endif
