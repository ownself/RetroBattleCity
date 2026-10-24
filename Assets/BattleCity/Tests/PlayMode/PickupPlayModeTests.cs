#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    [Category("BC303")]
    public sealed class PickupPlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private ArenaController controller;
        [UnitySetUp] public IEnumerator SetUp()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); ReleaseKeys(); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        private void ReleaseKeys() { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); }
        private IEnumerator Load(string name)
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            controller.Session.SetFocus(true);
        }
        private static Rect[] ExcludeAllBut(Vector2 p) => new[] {
            new Rect(-1000, -1000, p.x - 8 + 1000, 2000), new Rect(p.x + 8, -1000, 2000, 2000),
            new Rect(-1000, -1000, 2000, p.y - 8 + 1000), new Rect(-1000, p.y + 8, 2000, 2000) };
        private static void Tick(ArenaSession s, int count)
        {
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
            for (int i = 0; i < count; i++) { s.Bullets.Clear(); s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); }
            s.SetPaused(true);
        }
        private static SpriteRenderer Display(PickupView view) => view.GetComponentInChildren<SpriteRenderer>();

        [UnityTest] public IEnumerator Stage01_NaturalCarrierFlashes_FirstNonlethalHitDrops_RealInputCollectsExactlyOnce()
        {
            yield return Load("Stage01"); var s = controller.Session;
            // Isolate carrier/pickup behavior from unrelated enemy shots on the hit/collection ticks.
            s.PlayerTank.IsProtected = true;
            s.StartBattle(); Tick(s, 601); yield return null;
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(4));
            var carrier = s.Tanks.Single(t => t.CarriesPickup);
            Assert.That(carrier.DefinitionId, Is.EqualTo("enemy.armor")); Assert.That(carrier.HitPoints, Is.EqualTo(4));
            var view = controller.GetComponentsInChildren<EnemyTankView>().Single(v => v.name.StartsWith("Enemy " + carrier.EntityId + " -"));
            var renderer = view.GetComponentInChildren<SpriteRenderer>();
            view.Render(0); Assert.That(renderer.sprite.name, Does.StartWith("tank.white.enemy.armor"));
            view.Render(8); Assert.That(renderer.sprite.name, Does.StartWith("tank.red.enemy.armor"));
            long before = s.Clock.Tick; view.Render(8); view.Render(8);
            Assert.That(s.Clock.Tick, Is.EqualTo(before)); Assert.That(carrier.HitPoints, Is.EqualTo(4));
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); s.Bullets.Clear();
            var direction = carrier.Motor.Position.x <= 104 ? CardinalDirection.Left : CardinalDirection.Right;
            var gun = new WeaponState(controller.playerDefinition, s.Bullets, CombatTeam.Player, 999);
            Assert.That(gun.Step(new TankCommand(direction, false, true), carrier.Motor.Position - direction.Vector() * 18, direction), Is.True);
            s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); s.SetPaused(true); yield return null;
            Assert.That(carrier.HitPoints, Is.EqualTo(3)); Assert.That(carrier.CarriesPickup, Is.False);
            Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1)); Assert.That(s.Pickups.Active, Is.Not.Null);
            Assert.That(renderer.sprite.name, Does.StartWith("tank.green.enemy.armor"));
            Assert.That(Display(controller.PickupView).sprite.name, Is.EqualTo(PickupDefinition.SpriteId(s.Pickups.Active.Value.Kind)));
            // Controlled reachable destination beside the untouched player, then actual keyboard/Update collection.
            var destination = s.Player.Position + Vector2.left * 16;
            Assert.That(s.Pickups.TryDrop(s.Map, s.Player.Position, true, ExcludeAllBut(destination), s.Base.Bounds), Is.True);
            Assert.That(s.Pickups.ReplacedCount, Is.EqualTo(1));
            controller.PickupView.Render(); int frame = Time.frameCount; long tick = s.Clock.Tick;
            s.Bullets.Clear(); s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
            float deadline = Time.realtimeSinceStartup + 3;
            while (s.Pickups.CollectedCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); ReleaseKeys(); yield return null;
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Assert.That(s.Pickups.Active, Is.Null);
            Assert.That(s.Clock.Tick, Is.GreaterThan(tick)); Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(Display(controller.PickupView).enabled, Is.False); Assert.That(Display(controller.PickupView).sprite, Is.Null);
            Assert.That(s.Score, Is.Zero); Assert.That(s.PlayerUpgrades.Level, Is.Zero);
            for (int i = 0; i < 3; i++) { controller.PickupView.Render(); yield return null; }
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1));
            Debug.Log($"BC303 Stage01 natural carrier hp4->3, drop/replace, keyboard collection={s.Pickups.CollectedCount} frame={frame}->{Time.frameCount} tick={tick}->{s.Clock.Tick}");
        }

        [UnityTest] public IEnumerator Stage01_DisableRebindAndRepeatedRestart_ClearOldSlotUseStartupRulesAndOneRenderer()
        {
            yield return Load("Stage01"); var s = controller.Session;
            s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); s.SetPaused(true);
            s.Pickups.TryDrop(s.Map, s.Player.Position, true, null, s.Base.Bounds); controller.PickupView.Render();
            long itemId = s.Pickups.Active.Value.Id;
            controller.enabled = false;
            Assert.That(Display(controller.PickupView).enabled, Is.False); Assert.That(s.Pickups.Active.Value.Id, Is.EqualTo(itemId));
            controller.enabled = true;
            Assert.That(Display(controller.PickupView).enabled, Is.True);
            controller.pickupDefinition = null;
            for (int i = 0; i < 3; i++)
            {
                controller.RestartSession(); yield return null;
                Assert.That(s.Pickups.Active, Is.Null); Assert.That(s.AdvanceFrame(.1, PlayerButtons.None), Is.Zero);
                var fresh = controller.Session;
                Assert.That(fresh.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(fresh.Pickups.SpawnedCount, Is.Zero);
                Assert.That(fresh.Pickups.CollectedCount, Is.Zero); Assert.That(fresh.Pickups.Active, Is.Null);
                Assert.That(fresh.Pickups.RandomState, Is.EqualTo(fresh.Pickups.InitialSeed));
                Assert.That(fresh.Pickups.IsCarrierIndex(3), Is.True); Assert.That(fresh.Pickups.IsCarrierIndex(0), Is.False);
                Assert.That(Display(controller.PickupView).enabled, Is.False); Assert.That(Display(controller.PickupView).sprite, Is.Null);
                Assert.That(controller.GetComponentsInChildren<PickupView>().Length, Is.EqualTo(1));
                Assert.That(controller.PickupView.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
                s = fresh;
            }
        }

        [UnityTest] public IEnumerator CombatArenaOptOut_StandaloneViewProjectsAllSixKindsReadOnlyAndClearsOnDisable()
        {
            yield return Load("CombatArena"); Assert.That(controller.Session.Pickups, Is.Null); Assert.That(controller.PickupView, Is.Null);
            var rules = ScriptableObject.CreateInstance<PickupDefinition>(); rules.carrierSequenceIndices = new int[0];
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var state = new PickupState(rules, level); var map = new MapState(level);
            var host = new GameObject("Pickup test host"); var view = host.AddComponent<PickupView>();
            try
            {
                view.Initialize(state, controller.sprites, controller.playerRenderer.sharedMaterial);
                var seen = new HashSet<PickupKind>();
                for (int i = 0; i < 100 && seen.Count < 6; i++)
                {
                    state.TryDrop(map, level.playerSpawnPixels, true, null);
                    var item = state.Active.Value; uint random = state.RandomState;
                    view.Render(); view.Render(); seen.Add(item.Kind);
                    Assert.That(Display(view).sprite.GetEntityId(), Is.EqualTo(controller.sprites.Get(PickupDefinition.SpriteId(item.Kind)).GetEntityId()));
                    Assert.That(Display(view).transform.position, Is.EqualTo(PixelGrid.ToWorld(item.Position)));
                    Assert.That(state.Active.Value.Id, Is.EqualTo(item.Id)); Assert.That(state.RandomState, Is.EqualTo(random));
                }
                Assert.That(seen.Count, Is.EqualTo(6)); Assert.That(state.CollectedCount, Is.Zero);
                view.enabled = false; Assert.That(Display(view).enabled, Is.False); Assert.That(Display(view).sprite, Is.Null);
                view.enabled = true; view.Render(); Assert.That(Display(view).enabled, Is.False, "Standalone view requires explicit rebind");
                view.Initialize(state, controller.sprites, controller.playerRenderer.sharedMaterial); Assert.That(Display(view).enabled, Is.True);
                state.ClearActive(); view.Render(); Assert.That(Display(view).enabled, Is.False);
            }
            finally { Object.Destroy(host); Object.Destroy(rules); Object.Destroy(level); }
            yield return null;
        }
    }
}
#endif
