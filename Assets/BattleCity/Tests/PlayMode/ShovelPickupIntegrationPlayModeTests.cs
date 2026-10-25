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
    [Category("BC306")]
    public sealed class ShovelPickupIntegrationPlayModeTests
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
        private static void Drop(ArenaSession s, Vector2 destination)
        {
            for (int i = 0; i < 128; i++)
            {
                Assert.That(s.Pickups.TryDrop(s.Map, s.Player.Position, true, ExcludeAllBut(destination), s.Base.Bounds), Is.True);
                if (s.Pickups.Active.Value.Kind == PickupKind.Shovel) return;
            }
            Assert.Fail("Bounded deterministic drop search failed");
        }
        private static readonly Vector3Int Cell = new Vector3Int(12, 2, 0);
        [UnityTest] public IEnumerator Stage01_RealKeyboardUpdateShovel_AdvancesPausesAndRestoresPartialBrickTilemapAtExpiry()
        {
            var s = controller.Session; s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); s.SetPaused(true);
            s.Map.SetTerrainCell(Cell.x, Cell.y, TerrainKind.Brick, 5); controller.mapView.Render(s.Map);
            var partial = (Tile)controller.mapView.obstacles.GetTile(Cell);
            Assert.That(partial.sprite.vertices.Length, Is.EqualTo(8));
            int initialFrame = Time.frameCount; long initialTick = s.Clock.Tick;
            Drop(s, s.Player.Position - Vector2.right * 16);
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update();
            float deadline = Time.realtimeSinceStartup + 3;
            while (s.Pickups.CollectedCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); ReleaseKeys(); yield return null;
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Assert.That(s.Pickups.LastCollected.Value.Item.Kind, Is.EqualTo(PickupKind.Shovel));
            Assert.That(s.BaseFortification.RemainingTicks, Is.InRange(1, 1200));
            Assert.That(controller.mapView.obstacles.GetTile(Cell), Is.SameAs(controller.mapView.steelTiles[2]));
            Assert.That(s.Map.TerrainAt(Cell.x, Cell.y), Is.EqualTo(TerrainKind.Steel));
            Assert.That(controller.mapView.obstacles.GetComponent<TilemapRenderer>().enabled, Is.True);
            long pausedTick = s.Clock.Tick; int remaining = s.BaseFortification.RemainingTicks;
            yield return null; yield return null; yield return null;
            Assert.That(s.Clock.Tick, Is.EqualTo(pausedTick)); Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(remaining));
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); deadline = Time.realtimeSinceStartup + 3;
            while (s.Clock.Tick < pausedTick + 3 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(s.Clock.Tick, Is.GreaterThanOrEqualTo(pausedTick + 3));
            Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(remaining - (s.Clock.Tick - pausedTick)));
            // Bound the test duration: accelerate middle combat ticks through the public session clock,
            // then let the actual Controller.Update expire and project the last future tick.
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
            while (s.BaseFortification.RemainingTicks > 1) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
            s.SetPaused(true); yield return null;
            Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(1));
            Assert.That(controller.mapView.obstacles.GetTile(Cell), Is.SameAs(controller.mapView.steelTiles[2]));
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); deadline = Time.realtimeSinceStartup + 3;
            while (s.BaseFortification.RemainingTicks > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(s.BaseFortification.RemainingTicks, Is.Zero); Assert.That(s.BaseFortification.PendingCellCount, Is.Zero);
            Assert.That(s.Map.BrickMaskAt(Cell.x, Cell.y), Is.EqualTo(5));
            Assert.That(controller.mapView.obstacles.GetTile(Cell), Is.SameAs(partial));
            Assert.That(((Tile)controller.mapView.obstacles.GetTile(Cell)).sprite.vertices.Length, Is.EqualTo(8));
            Assert.That(Time.frameCount, Is.GreaterThan(initialFrame)); Assert.That(s.Clock.Tick, Is.GreaterThan(initialTick));
            Debug.Log($"BC306 real keyboard/Update frame={initialFrame}->{Time.frameCount} tick={initialTick}->{s.Clock.Tick}; pause preserved {remaining}; middle ticks accelerated via AdvanceFrame; final Update restored mask5 Tilemap renderer");
        }
        [UnityTest] public IEnumerator Stage01_RestartRetiresActiveAndPendingRestoration_OldMapRestoredFreshSessionAndTilemapClean()
        {
            var old = controller.Session;
            for (int restart = 0; restart < 2; restart++)
            {
                old.StartBattle(); old.AdvanceFrame(0, PlayerButtons.None); old.SetPaused(true);
                old.Map.SetTerrainCell(Cell.x, Cell.y, TerrainKind.Brick, 7);
                Drop(old, old.Player.Position); old.SetPaused(false); old.AdvanceFrame(0, PlayerButtons.None);
                old.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); old.SetPaused(true);
                Assert.That(old.BaseFortification.RemainingTicks, Is.EqualTo(1200));
                if (restart == 1)
                {
                    old.Map.SetTerrainCell(Cell.x, Cell.y, TerrainKind.Empty);
                    old.Map.SetTerrainCell(13, 2, TerrainKind.Empty);
                    var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<TankDefinition>("Assets/BattleCity/Data/EnemyBasic.asset");
                    var occupant = old.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 24));
                    old.SetPaused(false); old.AdvanceFrame(0, PlayerButtons.None);
                    for (int i = 0; i < 1200; i++) old.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                    old.SetPaused(true); Assert.That(occupant.IsAlive, Is.True); Assert.That(old.BaseFortification.IsRestoring, Is.True);
                }
                controller.pickupDefinition = null; controller.RestartSession(); yield return null;
                var fresh = controller.Session;
                Assert.That(old.BaseFortification.RemainingTicks, Is.Zero); Assert.That(old.BaseFortification.PendingCellCount, Is.Zero);
                Assert.That(old.BaseFortification.IsRestoring, Is.False); Assert.That(old.Map.BrickMaskAt(Cell.x, Cell.y), Is.EqualTo(7));
                Assert.That(old.AdvanceFrame(.1, PlayerButtons.None), Is.Zero);
                Assert.That(fresh.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(fresh.BaseFortification.DurationTicks, Is.EqualTo(1200));
                Assert.That(fresh.BaseFortification.RemainingTicks, Is.Zero); Assert.That(fresh.Pickups.CollectedCount, Is.Zero);
                Assert.That(fresh.Pickups.LastCollected, Is.Null); Assert.That(fresh.EnemyWave.SpawnedCount, Is.Zero);
                Assert.That(fresh.Map.BrickMaskAt(Cell.x, Cell.y), Is.EqualTo(15));
                Assert.That(controller.mapView.obstacles.GetTile(Cell), Is.SameAs(controller.mapView.brickTiles[2]));
                old = fresh;
            }
        }
    }
}
#endif
