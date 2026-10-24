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
    public sealed class TankStatePlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private TankDefinition definition;

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (definition != null) Object.Destroy(definition);
            definition = null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RealUpdate_EnemyCommandKillsSharedPlayerOnce_ThenPauseFreezesCombat()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/CombatArena.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var session = controller.Session;
            session.SetFocus(true);
            session.SetPaused(true);
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            definition = ScriptableObject.CreateInstance<TankDefinition>();
            definition.bulletSpeedPixelsPerSecond = 6000;
            var enemy = session.AddTank(definition, CombatTeam.Enemy, new Vector2(72, 24));
            int initialFrame = Time.frameCount;
            long initialTick = session.Clock.Tick;
            session.SetPaused(false);
            session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Left, false, true));
            float deadline = Time.realtimeSinceStartup + 2;
            while (session.PlayerTank.IsAlive && Time.realtimeSinceStartup < deadline) yield return null;
            session.SetPaused(true);
            Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
            Assert.That(session.Clock.Tick, Is.GreaterThan(initialTick));
            Assert.That(session.PlayerTank.IsAlive, Is.False);
            Assert.That(session.PlayerTank.HitPoints, Is.Zero);
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.Motor.Position.x, Is.LessThan(72));
            Assert.That(enemy.Weapon.ActiveBullets, Is.Zero);
            Assert.That(session.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion), Is.EqualTo(1));
            yield return null;
            Assert.That(controller.playerRenderer.enabled, Is.False);
            Assert.That(controller.EffectView.GetComponentsInChildren<SpriteRenderer>().Length, Is.GreaterThanOrEqualTo(2));
            long pausedTick = session.Clock.Tick;
            int pausedFrame = Time.frameCount;
            var position = enemy.Motor.Position;
            int cooldown = enemy.Weapon.CooldownRemainingTicks;
            int explosionAge = session.Effects.Active.Single(e => e.Kind == CombatEffectKind.Explosion).AgeTicks;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Time.frameCount, Is.GreaterThan(pausedFrame));
            Assert.That(session.Clock.Tick, Is.EqualTo(pausedTick));
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
            Assert.That(session.Effects.Active.Single(e => e.Kind == CombatEffectKind.Explosion).AgeTicks, Is.EqualTo(explosionAge));
            Debug.Log($"BC201 realShared frame={initialFrame}->{pausedFrame}->{Time.frameCount} tick={initialTick}->{pausedTick}->{session.Clock.Tick} playerHP={session.PlayerTank.HitPoints} enemy={enemy.EntityId}:{enemy.Motor.Position} cooldown={cooldown} explosionAge={explosionAge}");
            foreach (var root in scene.GetRootGameObjects())
            {
                Assert.That(root.GetComponentsInChildren<Rigidbody2D>(), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider2D>(), Is.Empty);
            }
        }
    }
}
#endif
