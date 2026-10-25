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
    [Category("BC304")]
    public sealed class PlayerPickupPlayModeTests
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
        private static void Tick(ArenaSession s)
        {
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
            s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); s.SetPaused(true);
        }
        private SpriteRenderer Shield => controller.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Player shield - display only");

        [UnityTest] public IEnumerator Stage01_RealKeyboardUpdateCollectsAllThree_ProjectsUpgradeShieldAndLifeHudOnce()
        {
            var s = controller.Session; s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); s.SetPaused(true);
            long initialTick = s.Clock.Tick; int initialFrame = Time.frameCount;
            foreach (var kind in new[] { PickupKind.Star, PickupKind.Helmet, PickupKind.Tank })
            {
                var destination = new Vector2(Mathf.Floor(s.Player.Position.x / 8) * 8 - 16, s.Player.Position.y);
                int count = s.Pickups.CollectedCount;
                Drop(s, kind, destination); controller.PickupView.Render();
                s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
                float deadline = Time.realtimeSinceStartup + 3;
                while (s.Pickups.CollectedCount == count && Time.realtimeSinceStartup < deadline) yield return null;
                s.SetPaused(true); ReleaseKeys(); yield return null;
                Assert.That(s.Pickups.CollectedCount, Is.EqualTo(count + 1));
                Assert.That(s.Pickups.LastCollected.Value.Item.Kind, Is.EqualTo(kind)); Assert.That(s.Pickups.Active, Is.Null);
            }
            Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(1));
            Assert.That(controller.playerRenderer.sprite.name, Does.StartWith("tank.yellow.player.level1"));
            Assert.That(s.PlayerIsProtected, Is.True); Assert.That(s.Pickups.HelmetRemainingTicks, Is.InRange(1, 600));
            Assert.That(Shield.enabled, Is.True); Assert.That(Shield.sprite.name, Does.StartWith("effect.shield."));
            Assert.That(Shield.transform.position, Is.EqualTo(controller.playerRenderer.transform.position));
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(4)); Assert.That(controller.hud.DisplayedLives, Is.EqualTo(4));
            Assert.That(s.Score, Is.EqualTo(1500)); Assert.That(s.Scoring.PickupPoints, Is.EqualTo(1500));
            Assert.That(s.Clock.Tick, Is.GreaterThan(initialTick));
            Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
            int remaining = s.Pickups.HelmetRemainingTicks; long tick = s.Clock.Tick;
            yield return null; yield return null; yield return null;
            Assert.That(s.Clock.Tick, Is.EqualTo(tick)); Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(remaining));
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(4)); Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(1));
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(3));
            Debug.Log($"BC304 actual keyboard/Update Star+Helmet+Tank frame={initialFrame}->{Time.frameCount} tick={initialTick}->{tick}; level1 shield={remaining} lives/HUD4; pause frozen");
        }
        [UnityTest] public IEnumerator Stage01_RepeatedRestartClearsEffectsAndProgress_UsesStartupSnapshotAndSingleShield()
        {
            var old = controller.Session;
            for (int restart = 0; restart < 3; restart++)
            {
                old.StartBattle(); old.AdvanceFrame(0, PlayerButtons.None); old.SetPaused(true);
                foreach (var kind in new[] { PickupKind.Star, PickupKind.Helmet, PickupKind.Tank })
                { Drop(old, kind, old.Player.Position); Tick(old); }
                yield return null;
                Assert.That(old.PlayerUpgrades.Level, Is.EqualTo(1)); Assert.That(old.PlayerLife.RemainingLives, Is.EqualTo(4));
                Assert.That(Shield.enabled, Is.True);
                // Runtime edits must not replace the controller's captured startup configuration.
                controller.pickupDefinition = null; controller.playerUpgradeDefinition = null;
                controller.RestartSession(); yield return null;
                var fresh = controller.Session;
                Assert.That(old.Pickups.HelmetRemainingTicks, Is.Zero); Assert.That(old.Pickups.Active, Is.Null);
                Assert.That(old.AdvanceFrame(.1, PlayerButtons.None), Is.Zero);
                Assert.That(fresh.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(fresh.PlayerUpgrades.Level, Is.Zero);
                Assert.That(fresh.Pickups.HelmetDurationTicks, Is.EqualTo(600)); Assert.That(fresh.Pickups.MaximumLives, Is.EqualTo(9));
                Assert.That(fresh.Pickups.HelmetRemainingTicks, Is.Zero); Assert.That(fresh.Pickups.CollectedCount, Is.Zero);
                Assert.That(fresh.PlayerLife.RemainingLives, Is.EqualTo(3)); Assert.That(controller.hud.DisplayedLives, Is.EqualTo(3));
                Assert.That(Shield.enabled, Is.False); Assert.That(Shield.sprite, Is.Null);
                Assert.That(controller.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == Shield.name), Is.EqualTo(1));
                Assert.That(controller.playerRenderer.sprite.name, Does.StartWith("tank.yellow.player.level0"));
                old = fresh;
            }
        }
    }
}
#endif
