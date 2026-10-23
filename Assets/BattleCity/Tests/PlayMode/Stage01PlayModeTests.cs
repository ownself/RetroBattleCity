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
    public sealed class Stage01PlayModeTests
    {
        private const string ScenePath = "Assets/BattleCity/Scenes/Stage01.unity";
        private Scene scene;
        private Keyboard keyboard;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            LogAssert.NoUnexpectedReceived();
        }

        private ArenaController Controller => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
        private void Keys(params Key[] keys)
        {
            if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator Stage01_RealKeyboardMovesAndBrickDamageProjectsWithoutAssetMutation()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = Controller; var session = controller.Session;
            var authored = (TerrainKind[])controller.level.terrain.Clone();
            session.SetFocus(true); session.SetPaused(false);
            Keys(Key.A);
            int firstFrame = Time.frameCount; long firstTick = session.Clock.Tick;
            float deadline = Time.realtimeSinceStartup + 2;
            while (session.Player.Position.x > 56 && Time.realtimeSinceStartup < deadline) yield return null;
            session.SetPaused(true); Keys();
            Assert.That(Time.frameCount, Is.GreaterThan(firstFrame)); Assert.That(session.Clock.Tick, Is.GreaterThan(firstTick));
            Assert.That(session.Player.Position.x, Is.InRange(49, 56));
            yield return null;
            Assert.That(PixelGrid.ToMapPixels(controller.playerRenderer.transform.position).x, Is.EqualTo(Mathf.Round(session.Player.Position.x)));
            long tick = session.Clock.Tick;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(session.Clock.Tick, Is.EqualTo(tick));
            session.SetPaused(false); Keys(Key.W, Key.Space);
            deadline = Time.realtimeSinceStartup + 2;
            while (session.Map.Revision == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            session.SetPaused(true); Keys();
            Assert.That(session.Map.Revision, Is.GreaterThan(0));
            Assert.That(session.Player.Position.y, Is.EqualTo(8), "16px tank is blocked by the brick immediately above this lane");
            yield return null;
            Assert.That(session.Map.BrickMaskAt(6, 2), Is.EqualTo(12));
            var sprite = controller.mapView.obstacles.GetSprite(new Vector3Int(6, 2, 0));
            Assert.That(sprite.name, Does.EndWith(".mask.12"));
            Assert.That(sprite.vertices.All(v => v.y >= -.0001f), Is.True);
            Assert.That(controller.level.terrain, Is.EqualTo(authored));
            Debug.Log($"BC200 realBrick frame={firstFrame}->{Time.frameCount} tick={firstTick}->{session.Clock.Tick} player={session.Player.Position} revision={session.Map.Revision} mask={session.Map.BrickMaskAt(6,2)}");
        }

        [UnityTest]
        public IEnumerator Stage01_RealKeyboardShotAndMovementAreBlockedByCentralSteel()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = Controller; var session = controller.Session;
            session.SetFocus(true); session.SetPaused(false);
            for (int i = 0; i < 192; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W);
            for (int i = 0; i < 32; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(104, 200)));
            Keys(Key.S, Key.Space);
            long tick = session.Clock.Tick; int frame = Time.frameCount;
            float deadline = Time.realtimeSinceStartup + 2;
            while (session.Effects.Active.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            session.SetPaused(true); Keys();
            Assert.That(Time.frameCount, Is.GreaterThan(frame)); Assert.That(session.Clock.Tick, Is.GreaterThan(tick));
            Assert.That(session.Effects.Active.Count, Is.EqualTo(1));
            Assert.That(session.Effects.Active[0].Position, Is.EqualTo(new Vector2(104, 162)));
            Assert.That(session.Bullets.Active, Is.Empty); Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
            Assert.That(session.Map.Revision, Is.Zero, "Ordinary shots must not destroy steel");
            session.SetPaused(false);
            // Pause deliberately suppresses held buttons until a release is sampled by the session.
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
            for (int i = 0; i < 80; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.S);
            session.SetPaused(true);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(104, 168)));
            yield return null;
            Assert.That(controller.mapView.obstacles.GetTile(new Vector3Int(12, 19, 0)), Is.SameAs(controller.mapView.steelTiles[0]));
            Assert.That(controller.mapView.obstacles.GetTile(new Vector3Int(13, 19, 0)), Is.SameAs(controller.mapView.steelTiles[1]));
            Debug.Log($"BC200 realSteel frame={frame}->{Time.frameCount} tick={tick}->{session.Clock.Tick} player={session.Player.Position} revision={session.Map.Revision}");
        }

        [UnityTest]
        public IEnumerator Stage01_ReloadRestoresBricksAndReservedIntactBase()
        {
            for (int run = 0; run < 2; run++)
            {
                scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                var controller = Controller; var session = controller.Session;
                Assert.That(session.Player.Position, Is.EqualTo(new Vector2(72, 8)));
                Assert.That(session.Map.BrickMaskAt(6, 2), Is.EqualTo(15));
                Assert.That(session.Map.Revision, Is.Zero);
                var baseRenderer = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>()).Single(r => r.sprite.name == "base.alive");
                Assert.That(PixelGrid.ToMapPixels(baseRenderer.transform.position), Is.EqualTo(new Vector2(104, 8)));
                session.SetFocus(true); session.SetPaused(false);
                for (int i = 0; i < 16; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.A);
                session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W | PlayerButtons.Fire);
                Assert.That(session.Map.Revision, Is.GreaterThan(0));
                Assert.That(controller.level.terrain[2 * 26 + 6], Is.EqualTo(TerrainKind.Brick));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
