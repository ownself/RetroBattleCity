#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    public sealed class ArenaPlayModeTests
    {
        private Scene scene;
        private const string ArenaPath = "Assets/BattleCity/Scenes/CombatArena.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Arena_RuntimeUpdatesRenderStateAndPauseFreezesClock()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(ArenaPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; // LoadSceneInPlayMode completes activation on the next player-loop frame.
            Assert.That(scene.isLoaded, Is.True);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            Assert.That(controller.Session, Is.Not.Null);
            controller.Session.SetFocus(true); controller.Session.SetPaused(false);
            long start = controller.Session.Clock.Tick;
            int firstFrame = Time.frameCount;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Time.frameCount, Is.GreaterThan(firstFrame));
            Assert.That(controller.Session.Clock.Tick, Is.GreaterThan(start));
            for (int i = 0; i < 8; i++) controller.Session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W);
            controller.Session.SetPaused(true);
            yield return null; // controller's real Update must project logic, not a test renderer
            Assert.That(controller.playerRenderer.sprite.name, Is.EqualTo("tank.yellow.player.level0.up.1"));
            Assert.That(PixelGrid.ToMapPixels(controller.playerRenderer.transform.position), Is.EqualTo(new Vector2(24, 32)));
            long pausedTick = controller.Session.Clock.Tick;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(controller.Session.Clock.Tick, Is.EqualTo(pausedTick));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Arena_KeyboardFiresAndRendererIsReusedAfterRelease()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(ArenaPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var session = controller.Session;
            session.SetFocus(true); session.SetPaused(false);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
                InputSystem.Update();
                int frame = Time.frameCount;
                yield return null;
                yield return null;
                Assert.That(Time.frameCount, Is.GreaterThan(frame));
                Assert.That(session.Bullets.Active.Count, Is.EqualTo(1));
                var bullet = session.Bullets.Active[0]; long id = bullet.Id;
                Assert.That(bullet.Direction, Is.EqualTo(CardinalDirection.Right));
                session.SetPaused(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                var renderer = controller.BulletView.GetComponentsInChildren<SpriteRenderer>().Single();
                Assert.That(renderer.sprite.name, Is.EqualTo("bullet.right"));
                Assert.That(renderer.sortingLayerName, Is.EqualTo("Projectiles"));
                var frozenPosition = bullet.Position; int frozenAge = bullet.AgeTicks;
                yield return null;
                Assert.That(bullet.Position, Is.EqualTo(frozenPosition));
                Assert.That(bullet.AgeTicks, Is.EqualTo(frozenAge));
                Assert.That(PixelGrid.ToMapPixels(renderer.transform.position), Is.EqualTo(new Vector2(Mathf.Round(frozenPosition.x), Mathf.Round(frozenPosition.y))));
                session.Bullets.Release(id);
                yield return null;
                Assert.That(renderer.gameObject.activeSelf, Is.False);
                session.SetPaused(false);
                for (int i = 0; i < 15; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
                session.SetPaused(true);
                yield return null;
                Assert.That(controller.BulletView.GetComponentsInChildren<SpriteRenderer>().Single(), Is.SameAs(renderer));
                Assert.That(session.Bullets.Active[0].Id, Is.Not.EqualTo(id));
                Assert.That(session.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Arena_BrickHitRemovesFourPixelLayerAndDisplaysRemainingWall()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(ArenaPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var session = controller.Session;
            session.SetFocus(true); session.SetPaused(false);
            bool hit = false;
            for (int tick = 0; tick < 100; tick++)
            {
                session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
                if (session.Bullets.Hits.Count == 0) continue;
                var impact = session.Bullets.Hits[0];
                Assert.That(impact.Kind, Is.EqualTo(BulletHitKind.Terrain));
                Assert.That(impact.Terrain, Is.EqualTo(TerrainKind.Brick));
                Assert.That(impact.Position.y, Is.EqualTo(166).Within(.001f));
                hit = true;
                break;
            }
            session.SetPaused(true);
            Assert.That(hit, Is.True);
            Assert.That(session.Bullets.Active, Is.Empty);
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
            Assert.That(session.Map.TerrainAt(3, 21), Is.EqualTo(TerrainKind.Brick));
            Assert.That(session.Map.BrickMaskAt(3, 21), Is.EqualTo(12));
            Assert.That(session.Map.IsSolid(6, 42), Is.False);
            Assert.That(session.Map.IsSolid(6, 43), Is.True);
            yield return null;
            Assert.That(controller.BulletView.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
            var cell = new Vector3Int(3, 21, 0);
            var partial = controller.mapView.obstacles.GetSprite(cell);
            Assert.That(partial, Is.Not.Null);
            Assert.That(partial, Is.Not.SameAs(((UnityEngine.Tilemaps.Tile)controller.mapView.brickTiles[1]).sprite));
            Assert.That(partial.vertices.All(v => v.y >= -.0001f), Is.True, "Only the upper 4px half remains visible");
            Assert.That(controller.level.terrain[21 * 26 + 3], Is.EqualTo(TerrainKind.Brick));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Arena_FourBrickLayersOpenA16PixelTankPassage()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(ArenaPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var session = controller.Session;
            session.SetFocus(true); session.SetPaused(false);
            int impacts = 0;
            for (int tick = 0; tick < 400 && impacts < 4; tick++)
            {
                session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
                if (session.Bullets.Hits.Count > 0)
                {
                    Assert.That(session.Bullets.Hits[0].Terrain, Is.EqualTo(TerrainKind.Brick));
                    impacts++;
                }
            }
            session.SetPaused(true);
            Assert.That(impacts, Is.EqualTo(4));
            Assert.That(session.Map.MoveAxis(new Vector2(24, 160), new Vector2(0, 40)).y, Is.EqualTo(200));
            Assert.That(session.Map.IsSolid(8, 42), Is.True, "Neighboring wall must survive");
            yield return null;
            for (int y = 21; y <= 22; y++) for (int x = 2; x <= 3; x++)
            {
                Assert.That(session.Map.TerrainAt(x, y), Is.EqualTo(TerrainKind.Empty));
                Assert.That(controller.mapView.obstacles.HasTile(new Vector3Int(x, y, 0)), Is.False);
                Assert.That(controller.level.terrain[y * 26 + x], Is.EqualTo(TerrainKind.Brick));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Arena_ReloadRecreatesRuntimeStateWithoutMutatingAsset()
        {
            for (int run = 0; run < 2; run++)
            {
                scene = EditorSceneManager.LoadSceneInPlayMode(ArenaPath, new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                Assert.That(scene.isLoaded, Is.True);
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
                Assert.That(controller.Session.Player.Position, Is.EqualTo(new Vector2(24, 24)));
                Assert.That(controller.Session.Bullets.Active, Is.Empty);
                Assert.That(controller.Session.PlayerWeapon.ActiveBullets, Is.Zero);
                Assert.That(controller.Session.PlayerWeapon.CooldownRemainingTicks, Is.Zero);
                Assert.That(controller.Session.Map.BrickMaskAt(3, 21), Is.EqualTo(15));
                controller.Session.SetFocus(true); controller.Session.SetPaused(false);
                for (int tick = 0; tick < 100; tick++)
                {
                    controller.Session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
                    if (controller.Session.Bullets.Hits.Count > 0) break;
                }
                Assert.That(controller.Session.Map.BrickMaskAt(3, 21), Is.EqualTo(12));
                controller.Session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D);
                Assert.That(controller.Session.Player.Position, Is.EqualTo(new Vector2(25, 24)));
                Assert.That(controller.level.playerSpawnPixels, Is.EqualTo(new Vector2Int(24, 24)));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
