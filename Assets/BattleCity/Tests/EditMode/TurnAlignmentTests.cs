using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class TurnAlignmentTests
    {
        public static IEnumerable<TestCaseData> SideTurns()
        {
            for (int facing = 0; facing < 4; facing++)
            for (int side = 0; side < 4; side++)
            {
                var from = (CardinalDirection)facing;
                var to = (CardinalDirection)side;
                if (from.IsVertical() == to.IsVertical()) continue;
                for (int offset = 0; offset < 8; offset++)
                    yield return new TestCaseData(from, to, offset);
            }
        }

        [TestCaseSource(nameof(SideTurns))]
        public void HeldSideTurn_ConvergesFromEveryGridOffset(CardinalDirection facing, CardinalDirection requested, int offset)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var map = new MapState(level);
                var start = new Vector2(80, 80) + facing.Vector() * offset;
                var motor = new TankMotor(start - facing.Vector(), map);
                motor.Step(facing, 60, map);
                Assert.That(motor.Position, Is.EqualTo(start));
                for (int tick = 0; tick < 5; tick++)
                {
                    var before = motor.Position;
                    motor.Step(requested, 60, map);
                    Assert.That(map.CanOccupy(motor.Position), Is.True);
                    if (motor.Facing == facing)
                    {
                        // Pending alignment moves along only the original axis, at the normal speed.
                        Assert.That(Vector2.Distance(before, motor.Position), Is.LessThanOrEqualTo(1.001f));
                        Assert.That(motor.IsMoving, Is.True);
                    }
                }
                Assert.That(motor.Facing, Is.EqualTo(requested));
                float axis = requested.IsVertical() ? motor.Position.x : motor.Position.y;
                Assert.That(Mathf.Abs(axis / 8 - Mathf.Round(axis / 8)), Is.LessThan(.0001f));
                Assert.That(Vector2.Dot(motor.Position - start, requested.Vector()), Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(level); }
        }

        [TestCase(CardinalDirection.Right, CardinalDirection.Up)]
        [TestCase(CardinalDirection.Right, CardinalDirection.Down)]
        [TestCase(CardinalDirection.Left, CardinalDirection.Up)]
        [TestCase(CardinalDirection.Left, CardinalDirection.Down)]
        [TestCase(CardinalDirection.Up, CardinalDirection.Left)]
        [TestCase(CardinalDirection.Up, CardinalDirection.Right)]
        [TestCase(CardinalDirection.Down, CardinalDirection.Left)]
        [TestCase(CardinalDirection.Down, CardinalDirection.Right)]
        public void PartialBrickWall_SideTurnAlignsAwayFromRemainingLayer(
            CardinalDirection approach, CardinalDirection requested)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var tank = ScriptableObject.CreateInstance<TankDefinition>();
            try
            {
                for (int y = 12; y < 14; y++)
                    for (int x = 12; x < 14; x++) level.terrain[y * 26 + x] = TerrainKind.Brick;
                var map = new MapState(level);
                var start = new Vector2(104, 104) - approach.Vector() * 16;
                var bullets = new BulletWorld();
                tank.bulletSpeedPixelsPerSecond = 6000;
                var weapon = new WeaponState(tank, bullets);
                Assert.That(weapon.Step(new TankCommand(CardinalDirection.None, true), start, approach), Is.True);
                bullets.Step(map);
                Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(4));
                var motor = new TankMotor(start, map);
                for (int tick = 0; tick < 8; tick++) motor.Step(approach, 60, map);
                var touching = start + approach.Vector() * 4;
                Assert.That(motor.Position, Is.EqualTo(touching), "The remaining 4px layer still blocks approach.");
                Assert.That(map.CanOccupy(touching + requested.Vector()), Is.True,
                    "There is no obstacle in the requested movement direction.");
                for (int tick = 0; tick < 8; tick++)
                {
                    var before = motor.Position;
                    motor.Step(requested, 60, map);
                    Assert.That(map.CanOccupy(motor.Position), Is.True, "Never overlap remaining brick.");
                    if (motor.Facing == approach)
                    {
                        Assert.That(Vector2.Distance(before, motor.Position), Is.LessThanOrEqualTo(1.001f));
                        Assert.That(Vector2.Dot(motor.Position - before, approach.Vector()), Is.LessThan(0),
                            "Pending alignment must move away from, not into, the wall.");
                    }
                }
                Assert.That(motor.Facing, Is.EqualTo(requested));
                Assert.That(Vector2.Dot(motor.Position - touching, requested.Vector()), Is.GreaterThan(0));
                float alignedAxis = requested.IsVertical() ? motor.Position.x : motor.Position.y;
                Assert.That(alignedAxis % TankMotor.AlignmentPixels, Is.EqualTo(0).Within(.0001f));
                Assert.That(map.Revision, Is.EqualTo(4), "Turning must not destroy additional wall.");
            }
            finally { Object.DestroyImmediate(level); Object.DestroyImmediate(tank); }
        }

        [Test]
        public void PartialBrickWall_BothAlignmentLinesBlocked_DoesNotCrossDynamicBlocker()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var map = new MapState(level);
                map.SetTerrainCell(12, 12, TerrainKind.Brick, 10); // Right 4px half, x=100..104.
                map.SetTerrainCell(12, 13, TerrainKind.Brick, 10);
                var motor = new TankMotor(new Vector2(88, 104), map);
                for (int tick = 0; tick < 4; tick++) motor.Step(CardinalDirection.Right, 60, map);
                var blockers = new[] { new Rect(68, 96, 16, 16) }; // Touches the tank's left edge at x=84.
                var touching = motor.Position;
                Assert.That(touching, Is.EqualTo(new Vector2(92, 104)));
                Assert.That(map.CanOccupy(touching, TankMotor.HalfSizePixels, blockers), Is.True);
                for (int tick = 0; tick < 8; tick++) motor.Step(CardinalDirection.Up, 60, map, blockers);
                Assert.That(motor.Position, Is.EqualTo(touching));
                Assert.That(motor.Facing, Is.EqualTo(CardinalDirection.Right));
                Assert.That(motor.IsMoving, Is.False);
            }
            finally { Object.DestroyImmediate(level); }
        }

        [Test]
        public void Session_ReleasedOldKeyDoesNotLeaveSideTurnPermanentlyStuck()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var tank = ScriptableObject.CreateInstance<TankDefinition>();
            try
            {
                level.playerSpawnPixels = new Vector2Int(24, 24);
                var session = new ArenaSession(level, tank);
                for (int i = 0; i < 3; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W);
                Assert.That(session.Player.Position, Is.EqualTo(new Vector2(24, 27)));
                for (int i = 0; i < 5; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D);
                Assert.That(session.LastCommand.Direction, Is.EqualTo(CardinalDirection.Right));
                Assert.That(session.Player.Facing, Is.EqualTo(CardinalDirection.Right));
                Assert.That(session.Player.Position.x, Is.GreaterThan(24));
            }
            finally { Object.DestroyImmediate(level); Object.DestroyImmediate(tank); }
        }
    }
}
