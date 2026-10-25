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
    public sealed class EnemyBrainPlayModeTests
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
        public IEnumerator Stage01_NaturalEnemyWaitsBeforeFirstShot_UsesRandomDefaultWithoutAssetChanges()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var s = controller.Session; s.SetFocus(true); s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None);
            s.PlayerTank.IsProtected = true;
            for (int i = 0; i < 61; i++) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
            var enemy = s.Tanks.Single(t => t.Team == CombatTeam.Enemy);
            Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.Zero);
            int firstShot = 0;
            for (int elapsed = 1; elapsed <= 105; elapsed++)
            {
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                if (elapsed < 45) Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.Zero);
                if (enemy.Weapon.CooldownRemainingTicks > 0) { firstShot = elapsed; break; }
            }
            s.SetPaused(true); yield return null;
            Assert.That(firstShot, Is.InRange(45, 105));
            Debug.Log($"Enemy fire tuning: Stage01 natural first shot {firstShot} complete combat ticks after birth; default range45..105; controlled AdvanceFrame, no asset edits");
        }

        [UnityTest]
        public IEnumerator RealUpdate_RegisteredEnemyMovesTurnsFires_PauseFreezes_ResumeAdvancesWithoutBacklog()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/CombatArena.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var session = controller.Session;
            session.SetFocus(true);
            session.SetPaused(true);
            session.PlayerTank.IsProtected = true;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            definition = ScriptableObject.CreateInstance<TankDefinition>();
            var enemy = session.AddTank(definition, CombatTeam.Enemy, new Vector2(200, 24));
            var brain = session.RegisterEnemyAI(enemy.EntityId, 101, EnemyBrainParameters.Prototype, CardinalDirection.Right);
            int startFrame = Time.frameCount; long startTick = session.Clock.Tick;
            session.SetPaused(false);
            float deadline = Time.realtimeSinceStartup + 3;
            while ((brain.DecisionTicks < 20 || brain.FireAttempts == 0) && Time.realtimeSinceStartup < deadline) yield return null;
            session.SetPaused(true);
            Assert.That(brain.DecisionTicks, Is.GreaterThanOrEqualTo(20), "Actual ArenaController.Update must drive AI, not direct AdvanceFrame");
            Assert.That(Time.frameCount, Is.GreaterThan(startFrame));
            Assert.That(enemy.Motor.Position, Is.Not.EqualTo(new Vector2(200, 24)));
            Assert.That(brain.RandomState, Is.Not.EqualTo(101));
            Assert.That(enemy.Motor.Facing, Is.Not.EqualTo(CardinalDirection.Right), "Outward initial request at boundary must turn");
            Assert.That(enemy.Weapon.CooldownRemainingTicks + enemy.Weapon.ActiveBullets, Is.GreaterThan(0));
            Assert.That(session.Map.CanOccupy(enemy.Motor.Position), Is.True);
            long pausedTick = session.Clock.Tick, decisions = brain.DecisionTicks;
            int pausedFrame = Time.frameCount, timer = brain.DirectionTicksRemaining;
            uint random = brain.RandomState; var position = enemy.Motor.Position;
            int cooldown = enemy.Weapon.CooldownRemainingTicks; int active = enemy.Weapon.ActiveBullets;
            int fireTimer = brain.FireTicksRemaining; long attempts = brain.FireAttempts; uint fireRandom = brain.FireRandomState;
            long dropped = session.Clock.DroppedTicks;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Time.frameCount, Is.GreaterThan(pausedFrame));
            Assert.That(session.Clock.Tick, Is.EqualTo(pausedTick)); Assert.That(brain.DecisionTicks, Is.EqualTo(decisions));
            Assert.That(brain.RandomState, Is.EqualTo(random)); Assert.That(brain.DirectionTicksRemaining, Is.EqualTo(timer));
            Assert.That(enemy.Motor.Position, Is.EqualTo(position)); Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
            Assert.That(enemy.Weapon.ActiveBullets, Is.EqualTo(active));
            Assert.That(brain.FireTicksRemaining, Is.EqualTo(fireTimer)); Assert.That(brain.FireAttempts, Is.EqualTo(attempts));
            Assert.That(brain.FireRandomState, Is.EqualTo(fireRandom));
            int frozenFrame = Time.frameCount;
            session.SetPaused(false);
            deadline = Time.realtimeSinceStartup + 2;
            while (session.Clock.Tick == pausedTick && Time.realtimeSinceStartup < deadline) yield return null;
            session.SetPaused(true);
            long resumedTicks = session.Clock.Tick - pausedTick;
            Assert.That(resumedTicks, Is.InRange(1, LogicClock.MaxTicksPerFrame));
            Assert.That(brain.DecisionTicks - decisions, Is.EqualTo(resumedTicks));
            Assert.That(session.Clock.DroppedTicks, Is.EqualTo(dropped), "Paused wall time must not become combat backlog");
            Assert.That(brain.DecisionTicks, Is.GreaterThan(decisions));
            Debug.Log($"BC202 realAI frame={startFrame}->{pausedFrame}->{frozenFrame}->{Time.frameCount} tick={startTick}->{pausedTick}->{session.Clock.Tick} decisions={decisions}->{brain.DecisionTicks} random={random}->{brain.RandomState} position={position}->{enemy.Motor.Position} cooldown={cooldown} active={active} resumedTicks={resumedTicks} dropped={dropped}->{session.Clock.DroppedTicks}");
        }
    }
}
#endif
