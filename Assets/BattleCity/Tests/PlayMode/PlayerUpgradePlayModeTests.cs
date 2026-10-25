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
    [Category("BC302")]
    public sealed class PlayerUpgradePlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private ArenaController controller;
        [UnitySetUp] public IEnumerator SetUp()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Load(string name)
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            controller.Session.SetFocus(true);
        }
        private static void Tick(ArenaSession session, int count, PlayerButtons buttons = PlayerButtons.None)
        {
            session.SetPaused(false); session.AdvanceFrame(0, PlayerButtons.None);
            for (int i = 0; i < count; i++) session.AdvanceFrame(LogicClock.StepSeconds, buttons);
            session.SetPaused(true);
        }
        private void AssertSprite(int level)
        {
            var session = controller.Session;
            var expected = controller.sprites.Get("tank.yellow.player.level" + level + "." + session.Player.Facing.SpriteKey() + "." + session.Player.TrackFrame);
            Assert.That(controller.playerRenderer.sprite.GetEntityId(), Is.EqualTo(expected.GetEntityId()));
            Assert.That(session.PlayerUpgrades.Level, Is.EqualTo(level));
        }

        [UnityTest] public IEnumerator Stage01_AllLevelsRenderDirectionTrackPairs_PauseRejectsUpgrade_RealUpdatesAdvance()
        {
            yield return Load("Stage01");
            var s = controller.Session; Assert.That(s.StartBattle(), Is.True); s.AdvanceFrame(0, PlayerButtons.None);
            for (int level = 0; level < 4; level++)
            {
                if (level > 0) { s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); Assert.That(s.TryUpgradePlayer(), Is.True); }
                Tick(s, 8, PlayerButtons.D); yield return null;
                AssertSprite(level); Assert.That(s.Player.Facing, Is.EqualTo(CardinalDirection.Right));
                Assert.That(s.Player.TrackFrame, Is.EqualTo(1));
                Tick(s, 8, PlayerButtons.A); yield return null;
                AssertSprite(level); Assert.That(s.Player.Facing, Is.EqualTo(CardinalDirection.Left));
                Assert.That(s.Player.TrackFrame, Is.Zero);
                long before = s.Clock.Tick; Assert.That(s.TryUpgradePlayer(), Is.False);
                yield return null; Assert.That(s.Clock.Tick, Is.EqualTo(before));
            }
            long tick = s.Clock.Tick; int frame = Time.frameCount;
            s.SetPaused(false);
            float deadline = Time.realtimeSinceStartup + 3;
            while (s.Clock.Tick < tick + 10 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(s.Clock.Tick, Is.GreaterThanOrEqualTo(tick + 10)); Assert.That(Time.frameCount, Is.GreaterThan(frame));
            AssertSprite(3);
            Debug.Log($"BC302 Stage01 level=3 real Update frame={frame}->{Time.frameCount} tick={tick}->{s.Clock.Tick} sprite={controller.playerRenderer.sprite.name}");
        }

        [UnityTest] public IEnumerator Stage01_TopLevelRealProjectileRemovesSteelTile_RestartRestoresMapWeaponAndLevel()
        {
            yield return Load("Stage01");
            var s = controller.Session; s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None);
            for (int i = 0; i < 3; i++) Assert.That(s.TryUpgradePlayer(), Is.True);
            Vector3Int cell = default; bool found = false;
            for (int y = 3; y < 25 && !found; y++) for (int x = 0; x < 26 && !found; x++)
                if (s.Map.TerrainAt(x, y) == TerrainKind.Steel && s.Map.TerrainAt(x, y - 1) == TerrainKind.Empty)
                { cell = new Vector3Int(x, y, 0); found = true; }
            Assert.That(found, Is.True, "Need a steel face exposed from below in the real Stage01 map");
            Assert.That(controller.mapView.obstacles.GetTile(cell), Is.Not.Null);
            var center = new Vector2(cell.x * 8 + 4, cell.y * 8 + 4);
            Assert.That(s.PlayerWeapon.Step(new TankCommand(CardinalDirection.Up, false, true), center - Vector2.up * 18, CardinalDirection.Up), Is.True);
            Tick(s, 1); yield return null;
            Assert.That(s.Bullets.Hits.Single().Kind, Is.EqualTo(BulletHitKind.Terrain));
            Assert.That(s.Bullets.Hits.Single().CanDestroySteel, Is.True);
            Assert.That(s.Map.TerrainAt(cell.x, cell.y), Is.EqualTo(TerrainKind.Empty));
            Assert.That(controller.mapView.obstacles.GetTile(cell), Is.Null);
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
            // Only the private startup snapshots, not these mutable runtime bindings, may seed a restart.
            controller.playerUpgradeDefinition = null; controller.playerDefinition = null;
            controller.RestartSession(); var fresh = controller.Session; yield return null;
            Assert.That(s.TryUpgradePlayer(), Is.False);
            Assert.That(fresh.Phase, Is.EqualTo(SessionPhase.Ready)); AssertSprite(0);
            Assert.That(fresh.Map.TerrainAt(cell.x, cell.y), Is.EqualTo(TerrainKind.Steel));
            Assert.That(controller.mapView.obstacles.GetTile(cell), Is.Not.Null);
            Assert.That(fresh.PlayerWeapon.Parameters.BulletSpeedPixelsPerSecond, Is.EqualTo(120));
            Assert.That(fresh.PlayerWeapon.Parameters.MaxActiveBullets, Is.EqualTo(1));
            Assert.That(fresh.PlayerWeapon.Parameters.CanDestroySteel, Is.False);
            Assert.That(fresh.Bullets.Active, Is.Empty);
            fresh.SetFocus(true); fresh.StartBattle(); fresh.AdvanceFrame(0, PlayerButtons.None);
            for (int i = 0; i < 3; i++) Assert.That(fresh.TryUpgradePlayer(), Is.True);
            Assert.That(fresh.PlayerWeapon.Parameters.CanDestroySteel, Is.True);
        }

        [UnityTest] public IEnumerator Stage01_TopLevelSteelBlockLosesFrontPairThenBackPair_FourDirectionsAndRestartProjection()
        {
            yield return Load("Stage01");
            var s = controller.Session; s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None);
            for (int i = 0; i < 3; i++) Assert.That(s.TryUpgradePlayer(), Is.True);
            s.SetPaused(true);
            var cells = new[] { new Vector3Int(12, 12, 0), new Vector3Int(13, 12, 0),
                new Vector3Int(12, 13, 0), new Vector3Int(13, 13, 0) };
            var original = cells.Select(cell => s.Map.TerrainAt(cell.x, cell.y)).ToArray();
            var originalVisible = cells.Select(cell => controller.mapView.obstacles.HasTile(cell)).ToArray();
            foreach (var direction in new[] { CardinalDirection.Up, CardinalDirection.Down, CardinalDirection.Left, CardinalDirection.Right })
            {
                foreach (var cell in cells) s.Map.SetTerrainCell(cell.x, cell.y, TerrainKind.Steel);
                for (int shot = 0; shot < 2; shot++)
                {
                    if (s.PlayerWeapon.CooldownRemainingTicks > 0) Tick(s, s.PlayerWeapon.CooldownRemainingTicks);
                    var center = new Vector2(104, 104) - direction.Vector() * 16;
                    Assert.That(s.PlayerWeapon.Step(new TankCommand(direction, true), center, direction), Is.True);
                    Tick(s, 1); yield return null;
                    var hit = s.Bullets.Hits.Single(h => h.OwnerId == ArenaSession.PlayerEntityId);
                    Assert.That(hit.Terrain, Is.EqualTo(TerrainKind.Steel)); Assert.That(hit.CanDestroySteel, Is.True);
                    foreach (var cell in cells)
                    {
                        bool front = direction == CardinalDirection.Up ? cell.y == 12 : direction == CardinalDirection.Down ? cell.y == 13 :
                            direction == CardinalDirection.Right ? cell.x == 12 : cell.x == 13;
                        bool remains = shot == 0 && !front;
                        Assert.That(s.Map.TerrainAt(cell.x, cell.y), Is.EqualTo(remains ? TerrainKind.Steel : TerrainKind.Empty));
                        Assert.That(controller.mapView.obstacles.HasTile(cell), Is.EqualTo(remains), "Both tiles must project in the same Update.");
                    }
                    Assert.That(s.Map.CanOccupy(new Vector2(104, 104)), Is.EqualTo(shot == 1));
                    Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
                }
            }
            controller.RestartSession(); yield return null;
            AssertSprite(0);
            for (int i = 0; i < cells.Length; i++)
            {
                Assert.That(controller.Session.Map.TerrainAt(cells[i].x, cells[i].y), Is.EqualTo(original[i]));
                Assert.That(controller.mapView.obstacles.HasTile(cells[i]), Is.EqualTo(originalVisible[i]));
            }
        }

        [UnityTest] public IEnumerator Stage01_ThreeCollectedStarsGiveFastThenTwoLiveShotsThenSteel()
        {
            yield return Load("Stage01");
            var s = controller.Session; s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); s.SetPaused(true);
            // Controlled clear lane: verify flight/capacity, not unrelated terrain impacts or natural drops.
            int column = Mathf.FloorToInt(s.Player.Position.x / 8);
            for (int y = 0; y < 26; y++)
            {
                s.Map.SetTerrainCell(column - 1, y, TerrainKind.Empty);
                s.Map.SetTerrainCell(column, y, TerrainKind.Empty);
            }
            var p = s.Player.Position;
            var excluded = new[] {
                new Rect(-1000, -1000, p.x - 8 + 1000, 2000), new Rect(p.x + 8, -1000, 2000, 2000),
                new Rect(-1000, -1000, 2000, p.y - 8 + 1000), new Rect(-1000, p.y + 8, 2000, 2000) };
            for (int level = 0; level < 4; level++)
            {
                // Upgrades intentionally retain a live cooldown; do not confuse it with a speed/cap change.
                if (s.PlayerWeapon.CooldownRemainingTicks > 0) Tick(s, s.PlayerWeapon.CooldownRemainingTicks);
                s.Bullets.Clear();
                if (level > 0)
                {
                    bool found = false;
                    for (int attempt = 0; attempt < 128 && !found; attempt++)
                    {
                        Assert.That(s.Pickups.TryDrop(s.Map, p, true, excluded, s.Base.Bounds), Is.True);
                        found = s.Pickups.Active.Value.Kind == PickupKind.Star;
                    }
                    Assert.That(found, Is.True, "Bounded Star selection failed.");
                    Tick(s, 1);
                    Assert.That(s.Pickups.LastCollected.Value.Item.Kind, Is.EqualTo(PickupKind.Star));
                    Assert.That(s.Pickups.CollectedCount, Is.EqualTo(level));
                }
                Tick(s, 1, PlayerButtons.Fire);
                var first = s.Bullets.Active.Single(); long id = first.Id;
                Assert.That(first.SpeedPixelsPerSecond, Is.EqualTo(level == 0 ? 120 : 240));
                Assert.That(first.Position.y - first.PreviousPosition.y, Is.EqualTo(level == 0 ? 2 : 4));
                Assert.That(first.CanDestroySteel, Is.EqualTo(level == 3));
                Tick(s, 14, PlayerButtons.Fire);
                Assert.That(s.Bullets.Active.Count, Is.EqualTo(1));
                Assert.That(s.PlayerWeapon.CooldownRemainingTicks, Is.EqualTo(1));
                Tick(s, 1, PlayerButtons.Fire);
                Assert.That(s.Bullets.Active.Any(b => b.Id == id), Is.True);
                Assert.That(s.Bullets.Active.Count, Is.EqualTo(level < 2 ? 1 : 2));
                yield return null;
                AssertSprite(level);
            }
        }

        [UnityTest] public IEnumerator CombatArena_RemainsOptOutWithOriginalWeaponAndSprite()
        {
            yield return Load("CombatArena");
            var s = controller.Session;
            Assert.That(s.PlayerUpgrades, Is.Null); Assert.That(s.TryUpgradePlayer(), Is.False);
            Assert.That(s.PlayerWeapon.Parameters.BulletSpeedPixelsPerSecond, Is.EqualTo(120));
            Assert.That(s.PlayerWeapon.Parameters.MaxActiveBullets, Is.EqualTo(1));
            Assert.That(s.PlayerWeapon.Parameters.CanDestroySteel, Is.False);
            Assert.That(controller.playerRenderer.sprite.name, Does.StartWith("tank.yellow.player.level0"));
        }
    }
}
#endif
