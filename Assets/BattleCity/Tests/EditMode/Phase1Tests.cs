using System;
using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BattleCity.Tests
{
    public sealed class Phase1Tests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            tank = ScriptableObject.CreateInstance<TankDefinition>();
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(tank); }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void RenderRate_DoesNotChangeDistanceOrTickCount(int fps)
        {
            var session = new ArenaSession(level, tank);
            for (int frame = 0; frame < fps; frame++) session.AdvanceFrame(1.0 / fps, PlayerButtons.D);
            Assert.That(session.Clock.Tick, Is.EqualTo(60));
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(84, 24)));
        }

        [Test]
        public void Clock_LongFrameIsBoundedAndPauseDropsPartialTime()
        {
            var clock = new LogicClock(); int calls = 0;
            Assert.That(clock.Advance(1, () => calls++), Is.EqualTo(8));
            Assert.That(clock.DroppedTicks, Is.EqualTo(52)); Assert.That(calls, Is.EqualTo(8));
            clock.Advance(LogicClock.StepSeconds * .5, () => calls++);
            clock.SetPaused(true); clock.Advance(100, () => calls++);
            clock.SetPaused(false);
            Assert.That(clock.Advance(LogicClock.StepSeconds * .5, () => calls++), Is.Zero);
            Assert.That(clock.Advance(LogicClock.StepSeconds * .5, () => calls++), Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.NaN, () => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1, () => { }));
        }

        [Test]
        public void Input_NewestPhysicalKeyWinsAndReleaseFallsBack()
        {
            var input = new PlayerInputBuffer();
            input.Sample(PlayerButtons.W, true);
            input.Sample(PlayerButtons.W | PlayerButtons.D, true);
            Assert.That(input.ConsumeTick().Direction, Is.EqualTo(CardinalDirection.Right));
            input.Sample(PlayerButtons.W | PlayerButtons.D | PlayerButtons.Up, true);
            Assert.That(input.ConsumeTick().Direction, Is.EqualTo(CardinalDirection.Up));
            input.Sample(PlayerButtons.W | PlayerButtons.D, true);
            Assert.That(input.ConsumeTick().Direction, Is.EqualTo(CardinalDirection.Right));
            input.Sample(PlayerButtons.W, true);
            Assert.That(input.ConsumeTick().Direction, Is.EqualTo(CardinalDirection.Up));
        }

        [Test]
        public void Input_SimultaneousKeysUseFrozenPriority()
        {
            var input = new PlayerInputBuffer();
            input.Sample(PlayerButtons.W | PlayerButtons.A | PlayerButtons.S | PlayerButtons.D, true);
            Assert.That(input.ConsumeTick().Direction, Is.EqualTo(CardinalDirection.Up));
            input.Sample(PlayerButtons.A | PlayerButtons.S | PlayerButtons.D, true);
            Assert.That(input.ConsumeTick().Direction, Is.EqualTo(CardinalDirection.Left));
            input.Sample(PlayerButtons.S | PlayerButtons.D, true);
            Assert.That(input.ConsumeTick().Direction, Is.EqualTo(CardinalDirection.Down));
        }

        [Test]
        public void Input_QuickFireTapSurvivesUntilExactlyOneTick()
        {
            var input = new PlayerInputBuffer();
            input.Sample(PlayerButtons.Fire, true); input.Sample(PlayerButtons.None, true);
            var command = input.ConsumeTick();
            Assert.That(command.FirePressed, Is.True); Assert.That(command.FireHeld, Is.False);
            Assert.That(input.ConsumeTick().FirePressed, Is.False);
        }

        [Test]
        public void KeyboardAdapter_MapsRealInputSystemKeyboardState()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.RightArrow, Key.Space, Key.Escape));
                InputSystem.Update();
                Assert.That(KeyboardInputSource.Read(), Is.EqualTo(PlayerButtons.W | PlayerButtons.Right | PlayerButtons.Fire | PlayerButtons.Pause));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [Test]
        public void Pause_DiscardsQueuedAndHeldCombatInputUntilRelease()
        {
            var session = new ArenaSession(level, tank);
            session.AdvanceFrame(0, PlayerButtons.W | PlayerButtons.Fire);
            session.AdvanceFrame(1, PlayerButtons.W | PlayerButtons.Fire | PlayerButtons.Pause);
            Assert.That(session.Clock.Paused, Is.True); Assert.That(session.Clock.Tick, Is.Zero);
            session.AdvanceFrame(60, PlayerButtons.W | PlayerButtons.Fire);
            session.AdvanceFrame(60, PlayerButtons.W | PlayerButtons.Fire | PlayerButtons.Pause);
            Assert.That(session.Clock.Paused, Is.False);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W | PlayerButtons.Fire);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(session.LastCommand.FirePressed || session.LastCommand.FireHeld, Is.False);
            session.AdvanceFrame(0, PlayerButtons.None);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W | PlayerButtons.Fire);
            Assert.That(session.Player.Position.y, Is.EqualTo(25));
            Assert.That(session.LastCommand.FirePressed && session.LastCommand.FireHeld, Is.True);
        }

        [Test]
        public void FocusLoss_PausesAndRequiresManualResume()
        {
            var session = new ArenaSession(level, tank);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W);
            session.SetFocus(false); session.AdvanceFrame(5, PlayerButtons.Fire | PlayerButtons.Pause);
            session.SetFocus(true); session.AdvanceFrame(5, PlayerButtons.None);
            Assert.That(session.Clock.Paused, Is.True); Assert.That(session.Clock.Tick, Is.EqualTo(1));
            session.AdvanceFrame(5, PlayerButtons.Pause);
            Assert.That(session.Clock.Paused, Is.False); Assert.That(session.Clock.Tick, Is.EqualTo(1));
        }

        [Test]
        public void Map_CopiesAuthoringDataAndHonorsHalfOpenEdges()
        {
            level.terrain[2 * 26 + 4] = TerrainKind.Brick; // [32,40) x [16,24)
            var map = new MapState(level); level.terrain[2 * 26 + 4] = TerrainKind.Empty;
            Assert.That(map.TerrainAt(4, 2), Is.EqualTo(TerrainKind.Brick));
            Assert.That(map.CanOccupy(new Vector2(24, 24)), Is.True);
            Assert.That(map.CanOccupy(new Vector2(24.01f, 24)), Is.False);
            Assert.That(map.CanOccupy(new Vector2(8, 8)), Is.True);
            Assert.That(map.CanOccupy(new Vector2(200, 200)), Is.True);
            Assert.That(map.CanOccupy(new Vector2(200.01f, 200)), Is.False);
            Assert.That(map.IsSolid(-1, 0), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => map.TerrainAt(26, 0));
        }

        [TestCase(1, 0, 200, 24)] [TestCase(-1, 0, 8, 24)]
        [TestCase(0, 1, 24, 200)] [TestCase(0, -1, 24, 8)]
        public void SweptMovement_ClampsToEveryMapBoundary(int dx, int dy, int x, int y)
        {
            var map = new MapState(level);
            Assert.That(map.MoveAxis(new Vector2(24, 24), new Vector2(dx, dy) * 1000), Is.EqualTo(new Vector2(x, y)));
        }

        [TestCase(TerrainKind.Brick)] [TestCase(TerrainKind.Steel)]
        public void SweptMovement_DoesNotTunnelThroughThinWalls(TerrainKind kind)
        {
            level.terrain[2 * 26 + 8] = kind;
            var map = new MapState(level);
            Assert.That(map.MoveAxis(new Vector2(24, 24), new Vector2(150, 0)), Is.EqualTo(new Vector2(56, 24)));
            Assert.That(map.MoveAxis(new Vector2(104, 24), new Vector2(-100, 0)), Is.EqualTo(new Vector2(80, 24)));
        }

        [TestCase(8, false)] [TestCase(16, true)] [TestCase(24, true)]
        public void Channels_RespectFullSixteenPixelTankWidth(int width, bool fits)
        {
            for (int y = 4; y < 20; y++)
            {
                level.terrain[y * 26 + 4] = TerrainKind.Brick;
                level.terrain[y * 26 + (40 + width) / 8] = TerrainKind.Steel;
            }
            var map = new MapState(level);
            var start = new Vector2(40 + width / 2f, 16);
            var destination = map.MoveAxis(start, new Vector2(0, 100));
            Assert.That(destination.y, Is.EqualTo(fits ? 116 : 24));
            Assert.That(map.CanOccupy(destination), Is.True);
        }

        [Test]
        public void Turn_SmallCorrectionWorksButNeverCrossesWallOrTeleports()
        {
            var map = new MapState(level);
            var motor = new TankMotor(new Vector2(24, 25), map);
            motor.Step(CardinalDirection.Right, 60, map);
            Assert.That(motor.Position, Is.EqualTo(new Vector2(25, 24)));
            motor = new TankMotor(new Vector2(24, 27), map);
            motor.Step(CardinalDirection.Right, 60, map);
            Assert.That(motor.Position, Is.EqualTo(new Vector2(24, 26))); // approaches alignment, no permanent rejection
            Assert.That(motor.Facing, Is.EqualTo(CardinalDirection.Up));
            level.terrain[2 * 26 + 4] = TerrainKind.Steel;
            map = new MapState(level);
            motor = new TankMotor(new Vector2(29, 32), map);
            motor.Step(CardinalDirection.Right, 60, map); // facing becomes horizontal at a clear height
            motor.Step(CardinalDirection.Down, 60, map); // snapping X to 32 is clear, but wall stops forward motion
            Assert.That(map.CanOccupy(motor.Position), Is.True);
            Assert.That(motor.Position.y, Is.EqualTo(32));
        }

        [Test]
        public void Turn_AtWallCornerAndBoundaryKeepsLegalFullSizeBox()
        {
            level.terrain[3 * 26 + 3] = TerrainKind.Steel;
            var map = new MapState(level);
            var motor = new TankMotor(new Vector2(28, 42), map);
            motor.Step(CardinalDirection.Right, 60, map);
            Assert.That(motor.Position, Is.EqualTo(new Vector2(29, 40)));
            for (int i = 0; i < 20; i++) motor.Step(CardinalDirection.Down, 60, map);
            Assert.That(map.CanOccupy(motor.Position), Is.True);
            Assert.That(motor.Position.y, Is.EqualTo(40)); // aligns X, then the wall blocks downward movement
            motor = new TankMotor(new Vector2(8, 9), map);
            motor.Step(CardinalDirection.Right, 60, map);
            Assert.That(motor.Position, Is.EqualTo(new Vector2(9, 8)));
            Assert.That(map.CanOccupy(motor.Position), Is.True);
        }

        [Test]
        public void Animation_OnlyAdvancesForActualMovementAndDoesNotMoveCollider()
        {
            var map = new MapState(level); var motor = new TankMotor(new Vector2(24, 24), map);
            for (int i = 0; i < 8; i++) motor.Step(CardinalDirection.Up, 60, map);
            Assert.That(motor.TrackFrame, Is.EqualTo(1));
            for (int i = 0; i < 20; i++) motor.Step(CardinalDirection.None, 60, map);
            Assert.That(motor.TrackFrame, Is.EqualTo(1)); Assert.That(motor.Position, Is.EqualTo(new Vector2(24, 32)));
            motor = new TankMotor(new Vector2(8, 8), map);
            for (int i = 0; i < 20; i++) motor.Step(CardinalDirection.Down, 60, map);
            Assert.That(motor.TrackFrame, Is.Zero); Assert.That(motor.IsMoving, Is.False);
        }

        [Test]
        public void ArenaAssetAndScene_DisplayExactlyMatchesMapAndHasNoPhysicsState()
        {
            var previous = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(Phase1Arena.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Phase1Arena.ScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
                Assert.DoesNotThrow(controller.ValidateBindings);
                var map = new MapState(controller.level); Assert.That(map.CanOccupy(controller.level.playerSpawnPixels), Is.True);
                var tiles = controller.mapView.obstacles;
                for (int y = 0; y < 26; y++) for (int x = 0; x < 26; x++)
                {
                    var kind = map.TerrainAt(x, y); var cell = new Vector3Int(x, y, 0);
                    Assert.That(tiles.HasTile(cell), Is.EqualTo(kind != TerrainKind.Empty), cell.ToString());
                    if (kind != TerrainKind.Empty)
                    {
                        string prefix = kind == TerrainKind.Brick ? "terrain.brick8." : "terrain.steel8.";
                        Assert.That(tiles.GetSprite(cell).name, Is.EqualTo(prefix + (x % 2) + "." + (1 - y % 2)));
                        Assert.That(((Tile)tiles.GetTile(cell)).colliderType, Is.EqualTo(Tile.ColliderType.None));
                    }
                }
                foreach (var root in scene.GetRootGameObjects())
                {
                    Assert.That(root.GetComponentsInChildren<Rigidbody2D>(), Is.Empty);
                    Assert.That(root.GetComponentsInChildren<Collider2D>(), Is.Empty);
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
