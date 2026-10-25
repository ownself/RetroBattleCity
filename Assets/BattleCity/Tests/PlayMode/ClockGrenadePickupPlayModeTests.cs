#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    [Category("BC305")]
    public sealed class ClockGrenadePickupPlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private ArenaController controller;
        [UnitySetUp] public IEnumerator SetUp()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); ReleaseKeys(); yield return null;
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            controller.Session.SetFocus(true);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        private void ReleaseKeys() { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); }
        private static Rect[] ExcludeAllBut(Vector2 p) => new[] {
            new Rect(-1000, -1000, p.x - 8 + 1000, 2000), new Rect(p.x + 8, -1000, 2000, 2000),
            new Rect(-1000, -1000, 2000, p.y - 8 + 1000), new Rect(-1000, p.y + 8, 2000, 2000) };
        private static void Drop(ArenaSession s, PickupKind kind, Vector2 destination)
        {
            for (int i = 0; i < 128; i++)
            {
                Assert.That(s.Pickups.TryDrop(s.Map, s.Player.Position, true, ExcludeAllBut(destination), s.Base.Bounds), Is.True);
                if (s.Pickups.Active.Value.Kind == kind) return;
            }
            Assert.Fail("Bounded deterministic drop search failed");
        }
        private IEnumerator CollectByRealKeyboardUpdate(ArenaSession s, PickupKind kind)
        {
            var destination = new Vector2(Mathf.Floor(s.Player.Position.x / 8) * 8 - 16, s.Player.Position.y);
            int count = s.Pickups.CollectedCount; Drop(s, kind, destination); controller.PickupView.Render();
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
            float deadline = Time.realtimeSinceStartup + 3;
            while (s.Pickups.CollectedCount == count && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); ReleaseKeys(); yield return null;
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(count + 1));
            Assert.That(s.Pickups.LastCollected.Value.Item.Kind, Is.EqualTo(kind)); Assert.That(s.Pickups.Active, Is.Null);
        }
        [UnityTest] public IEnumerator Stage01_RealKeyboardUpdateClockFreezesArmorAIThenGrenadeKillsOnce_PauseAndProjection()
        {
            var s = controller.Session; s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); s.SetPaused(true);
            var armor = AssetDatabase.LoadAssetAtPath<TankDefinition>("Assets/BattleCity/Data/EnemyArmor.asset");
            var tank = s.AddTank(armor, CombatTeam.Enemy, new Vector2(200, 200));
            var brain = s.RegisterEnemyAI(tank.EntityId, 305);
            int initialFrame = Time.frameCount; long initialTick = s.Clock.Tick;
            yield return CollectByRealKeyboardUpdate(s, PickupKind.Clock);
            var position = tank.Motor.Position; uint rng = brain.RandomState; long decisions = brain.DecisionTicks;
            int cooldown = tank.Weapon.CooldownRemainingTicks, track = tank.Motor.TrackFrame, remaining = s.Pickups.ClockRemainingTicks;
            Assert.That(remaining, Is.InRange(1, 600));
            long pausedTick = s.Clock.Tick; yield return null; yield return null; yield return null;
            Assert.That(s.Clock.Tick, Is.EqualTo(pausedTick)); Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(remaining));
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
            float deadline = Time.realtimeSinceStartup + 3;
            while (s.Clock.Tick < pausedTick + 3 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(s.Clock.Tick, Is.GreaterThanOrEqualTo(pausedTick + 3));
            Assert.That(s.Pickups.ClockRemainingTicks, Is.LessThan(remaining));
            Assert.That(tank.Motor.Position, Is.EqualTo(position)); Assert.That(brain.RandomState, Is.EqualTo(rng));
            Assert.That(brain.DecisionTicks, Is.EqualTo(decisions)); Assert.That(tank.Motor.TrackFrame, Is.EqualTo(track));
            Assert.That(tank.Weapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
            yield return CollectByRealKeyboardUpdate(s, PickupKind.Grenade);
            Assert.That(tank.IsAlive, Is.False); Assert.That(s.DefeatedEnemyCount, Is.EqualTo(1)); Assert.That(s.Score, Is.EqualTo(armor.score));
            Assert.That(s.EnemyWave.AliveCount, Is.Zero);
            Assert.That(s.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion), Is.EqualTo(1));
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(2)); Assert.That(s.PlayerTank.IsAlive && s.Base.IsAlive, Is.True);
            Assert.That(Time.frameCount, Is.GreaterThan(initialFrame)); Assert.That(s.Clock.Tick, Is.GreaterThan(initialTick));
            Debug.Log($"BC305 real keyboard/Update Clock+Grenade frame={initialFrame}->{Time.frameCount} tick={initialTick}->{s.Clock.Tick}; frozen AI RNG/pose/cooldown/tracks; Armor dead score={s.Score} once; pause preserved {remaining}");
        }
        [UnityTest] public IEnumerator Stage01_RestartRetiresClockAndReceipts_StartupSnapshotAndFreshWave()
        {
            var old = controller.Session;
            for (int restart = 0; restart < 2; restart++)
            {
                old.StartBattle(); old.AdvanceFrame(0, PlayerButtons.None); old.SetPaused(true);
                Drop(old, PickupKind.Clock, old.Player.Position);
                old.SetPaused(false); old.AdvanceFrame(0, PlayerButtons.None);
                old.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); old.SetPaused(true);
                Assert.That(old.Pickups.ClockRemainingTicks, Is.EqualTo(600));
                Drop(old, PickupKind.Grenade, old.Player.Position);
                controller.pickupDefinition = null; controller.RestartSession(); yield return null;
                var fresh = controller.Session;
                Assert.That(old.Pickups.ClockRemainingTicks, Is.Zero); Assert.That(old.Pickups.Active, Is.Null);
                Assert.That(old.AdvanceFrame(.1, PlayerButtons.None), Is.Zero);
                Assert.That(fresh.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(fresh.Pickups.ClockDurationTicks, Is.EqualTo(600));
                Assert.That(fresh.Pickups.ClockRemainingTicks, Is.Zero); Assert.That(fresh.Pickups.CollectedCount, Is.Zero);
                Assert.That(fresh.Pickups.LastCollected, Is.Null); Assert.That(fresh.Score, Is.Zero);
                Assert.That(fresh.EnemyWave.SpawnedCount, Is.Zero); Assert.That(fresh.AliveEnemyCount, Is.Zero);
                old = fresh;
            }
        }
    }
}
#endif
