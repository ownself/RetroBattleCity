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
using UnityEngine.Tilemaps;

namespace BattleCity.Tests
{
    [Category("BC309")]
    public sealed class ClassicRulesPlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private ArenaController controller;
        [UnitySetUp] public IEnumerator SetUp()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); Keys(); yield return null;
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
        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        private ArenaSession StartPaused()
        {
            var s = controller.Session; s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); s.SetPaused(true); return s;
        }
        private static void Resume(ArenaSession s) { s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); }
        private static void Collect(ArenaSession s, PickupKind kind)
        {
            ClassicRulesCombinationTestsProxy.Drop(s, kind);
            Resume(s); s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); s.SetPaused(true);
        }
        [UnityTest] public IEnumerator Stage01_RealKeyboardUpdateIceCoasts_PauseFreezes_ReleaseEndsWithoutStaleInput()
        {
            var s = StartPaused();
            for (int y = 0; y < 2; y++) for (int x = 4; x <= 10; x++) s.Map.SetTerrainCell(x, y, TerrainKind.Ice);
            s.Map.SetTerrainCell(20, 20, TerrainKind.Grass); s.Map.SetTerrainCell(21, 20, TerrainKind.Water);
            controller.mapView.Render(s.Map);
            int frame = Time.frameCount; long tick = s.Clock.Tick; Vector2 initial = s.Player.Position;
            Resume(s); Keys(Key.A);
            float deadline = Time.realtimeSinceStartup + 3;
            while (s.Clock.Tick < tick + 3 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); Keys(); yield return null;
            Assert.That(s.Player.Position.x, Is.LessThan(initial.x)); Assert.That(s.Player.IceRemainingTicks, Is.EqualTo(24));
            Vector2 paused = s.Player.Position; int remaining = s.Player.IceRemainingTicks; long pausedTick = s.Clock.Tick;
            yield return null; yield return null; yield return null;
            Assert.That(s.Clock.Tick, Is.EqualTo(pausedTick)); Assert.That(s.Player.Position, Is.EqualTo(paused));
            Resume(s); deadline = Time.realtimeSinceStartup + 3;
            while (s.Clock.Tick < pausedTick + 26 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(s.Clock.Tick, Is.GreaterThanOrEqualTo(pausedTick + 26));
            Assert.That(s.Player.Position.x, Is.EqualTo(paused.x - remaining).Within(.001));
            Assert.That(s.Player.IceRemainingTicks, Is.Zero); Assert.That(s.Player.IsMoving, Is.False);
            Assert.That(controller.playerRenderer.transform.position, Is.EqualTo(PixelGrid.ToWorld(new Vector2(Mathf.Round(s.Player.Position.x), Mathf.Round(s.Player.Position.y)))));
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Debug.Log($"BC309 actual keyboard/Update ice frame={frame}->{Time.frameCount} tick={tick}->{s.Clock.Tick}; pause at {pausedTick}; release coast={remaining}px; no acceleration or stale held input");
        }
        [UnityTest] public IEnumerator Stage01_AllSpecialTileQuadrantsAndLayerRules_RevisionClearAndRestart_NoSavedLayoutChange()
        {
            var s = StartPaused(); var view = controller.mapView;
            foreach (TerrainKind kind in new[] { TerrainKind.Water, TerrainKind.Grass, TerrainKind.Ice })
            {
                for (int y = 10; y < 12; y++) for (int x = 10; x < 12; x++) s.Map.SetTerrainCell(x, y, kind);
                view.Render(s.Map);
                for (int y = 10; y < 12; y++) for (int x = 10; x < 12; x++)
                {
                    var cell = new Vector3Int(x, y, 0); int quadrant = x % 2 + (1 - y % 2) * 2;
                    Assert.That(view.obstacles.GetTile(cell), Is.Null);
                    var tile = (Tile)(kind == TerrainKind.Grass ? view.Foliage : view.Surfaces).GetTile(cell);
                    Assert.That(tile, Is.Not.Null); Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.None));
                    string id = kind == TerrainKind.Water ? "terrain.water.0" : kind == TerrainKind.Grass ? "terrain.grass16" : "terrain.ice16";
                    var original = controller.sprites.Get(id);
                    Assert.That(tile.sprite.texture, Is.SameAs(original.texture)); Assert.That(tile.sprite.rect.size, Is.EqualTo(new Vector2(8, 8)));
                    Assert.That(tile.sprite.rect.x, Is.EqualTo(original.rect.x + (quadrant % 2) * 8));
                    Assert.That(tile.sprite.rect.y, Is.EqualTo(original.rect.y + (1 - quadrant / 2) * 8));
                    Assert.That(tile.sprite.pixelsPerUnit, Is.EqualTo(16)); Assert.That(tile.sprite.pivot, Is.EqualTo(new Vector2(4, 4)));
                }
                Assert.That((kind == TerrainKind.Grass ? view.Surfaces : view.Foliage).GetTile(new Vector3Int(10, 10, 0)), Is.Null);
            }
            var foliage = view.Foliage.GetComponent<TilemapRenderer>(); var surface = view.Surfaces.GetComponent<TilemapRenderer>();
            Assert.That(foliage.sortingLayerName, Is.EqualTo("Actors")); Assert.That(foliage.sortingOrder, Is.GreaterThan(controller.playerRenderer.sortingOrder));
            Assert.That(surface.sortingLayerName, Is.EqualTo("Obstacles"));
            Assert.That(SortingLayer.GetLayerValueFromName("Projectiles"), Is.GreaterThan(SortingLayer.GetLayerValueFromName("Actors")));
            Assert.That(SortingLayer.GetLayerValueFromName("Effects"), Is.GreaterThan(SortingLayer.GetLayerValueFromName("Actors")));
            ClassicRulesCombinationTestsProxy.Drop(s, PickupKind.Star); controller.PickupView.Render();
            Assert.That(controller.PickupView.GetComponentInChildren<SpriteRenderer>().sortingOrder, Is.GreaterThan(foliage.sortingOrder));
            s.Map.SetTerrainCell(10, 10, TerrainKind.Empty); view.Render(s.Map);
            Assert.That(view.Surfaces.GetTile(new Vector3Int(10, 10, 0)), Is.Null); Assert.That(view.Foliage.GetTile(new Vector3Int(10, 10, 0)), Is.Null);
            controller.RestartSession(); yield return null;
            Assert.That(controller.Session.Map.TerrainAt(10, 10), Is.EqualTo(controller.level.terrain[10 * 26 + 10]));
            Assert.That(view.Surfaces.GetUsedTilesCount(), Is.Zero); Assert.That(view.Foliage.GetUsedTilesCount(), Is.Zero);
            Assert.That(view.GetComponentsInChildren<Tilemap>().Length, Is.EqualTo(3), "No layer duplication across restart");
        }
        [UnityTest] public IEnumerator Stage01_TerminalUpdateFreezesResultOnce_RestartCleansScoreEffectsAndIce_OldResultSurvives()
        {
            var s = StartPaused(); Collect(s, PickupKind.Star); Collect(s, PickupKind.Helmet); Collect(s, PickupKind.Clock); Collect(s, PickupKind.Shovel);
            Assert.That(s.Score, Is.EqualTo(2000));
            var gun = new WeaponState(controller.playerDefinition, s.Bullets, CombatTeam.Enemy, 999);
            // Controlled lethal impact inside the base footprint, not inside the active fortified roof.
            Assert.That(gun.Step(new TankCommand(CardinalDirection.Down, false, true), s.Base.Position + Vector2.up * 10, CardinalDirection.Down), Is.True);
            int events = 0; s.SessionEnded += () => { events++; Assert.That(s.Result, Is.Not.Null); };
            long tick = s.Clock.Tick; int frame = Time.frameCount; Resume(s);
            float deadline = Time.realtimeSinceStartup + 3;
            while (s.Phase != SessionPhase.Lost && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Lost)); Assert.That(s.Result.Total, Is.EqualTo(2000)); Assert.That(events, Is.EqualTo(1));
            var result = s.Result; yield return null; yield return null; Assert.That(s.Result, Is.SameAs(result)); Assert.That(events, Is.EqualTo(1));
            Assert.That(s.Pickups.ClockRemainingTicks + s.Pickups.HelmetRemainingTicks + s.BaseFortification.RemainingTicks + s.Player.IceRemainingTicks, Is.Zero);
            controller.RestartSession(); yield return null;
            Assert.That(controller.Session.Score, Is.Zero); Assert.That(controller.Session.Result, Is.Null);
            Assert.That(controller.Session.Scoring.Enemies, Is.Empty); Assert.That(controller.Session.Scoring.CollectedCount, Is.Zero);
            Assert.That(controller.Session.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(controller.hud.DisplayedSession, Is.SameAs(controller.Session));
            Assert.That(result.Total, Is.EqualTo(2000)); Assert.That(s.AdvanceFrame(.1, PlayerButtons.None), Is.Zero);
            Debug.Log($"BC309 actual terminal Update frame={frame}->{Time.frameCount} tick={tick}->{s.Clock.Tick}; result points=2000, one event, restart clean");
        }
    }
    // PlayMode assembly cannot reference EditMode helpers. Uses only the public production pickup path.
    internal static class ClassicRulesCombinationTestsProxy
    {
        public static void Drop(ArenaSession s, PickupKind kind)
        {
            Vector2 p = s.Player.Position;
            var blockers = new[] { new Rect(-1000, -1000, p.x - 8 + 1000, 2000), new Rect(p.x + 8, -1000, 2000, 2000),
                new Rect(-1000, -1000, 2000, p.y - 8 + 1000), new Rect(-1000, p.y + 8, 2000, 2000) };
            for (int i = 0; i < 128; i++)
            {
                Assert.That(s.Pickups.TryDrop(s.Map, p, true, blockers, s.Base?.Bounds), Is.True);
                if (s.Pickups.Active.Value.Kind == kind) return;
            }
            Assert.Fail("Bounded deterministic pickup search failed");
        }
    }
}
#endif
