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
