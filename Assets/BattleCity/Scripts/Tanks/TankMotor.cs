using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Reusable tank movement; the control source is just a direction, not a player component.</summary>
    public sealed class TankMotor
    {
        public const int AlignmentPixels = 8;
        public const float MaxTurnCorrectionPixels = 2;
        public const int TrackFrameTicks = 8;
        private int movingTicks;
        public Vector2 Position { get; private set; }
        public CardinalDirection Facing { get; private set; } = CardinalDirection.Up;
        public bool IsMoving { get; private set; }
        public int TrackFrame => movingTicks / TrackFrameTicks % 2;

        public TankMotor(Vector2 spawn, MapState map)
        {
            if (!map.CanOccupy(spawn)) throw new ArgumentException("Tank spawn is obstructed.", nameof(spawn));
            Position = spawn;
        }

        public void Step(CardinalDirection requested, float speedPixelsPerSecond, MapState map)
        {
            IsMoving = false;
            if (requested == CardinalDirection.None) return;
            var direction = requested.Vector(); // validates before changing state
            if (speedPixelsPerSecond <= 0 || float.IsNaN(speedPixelsPerSecond) || float.IsInfinity(speedPixelsPerSecond))
                throw new ArgumentOutOfRangeException(nameof(speedPixelsPerSecond));
            var before = Position;
            if (requested.IsVertical() != Facing.IsVertical())
            {
                float axis = requested.IsVertical() ? Position.x : Position.y;
                float snapped = Mathf.Round(axis / AlignmentPixels) * AlignmentPixels;
                var aligned = requested.IsVertical() ? new Vector2(snapped, Position.y) : new Vector2(Position.x, snapped);
                // Both endpoint and whole correction path must be clear. Never jump over a wall.
                if (!map.CanOccupy(aligned) || (map.MoveAxis(Position, aligned - Position) - aligned).sqrMagnitude > MapState.Epsilon * MapState.Epsilon) return;
                if (Mathf.Abs(snapped - axis) > MaxTurnCorrectionPixels + MapState.Epsilon)
                {
                    // A held side-turn must make progress even after the old movement key is released.
                    // Approach the alignment line at normal speed instead of rejecting this request forever.
                    Position = map.MoveAxis(Position, Vector2.MoveTowards(Position, aligned,
                        speedPixelsPerSecond * PixelGrid.LogicStep) - Position);
                    UpdateMovement(before);
                    return;
                }
                Position = aligned;
            }
            Facing = requested;
            Position = map.MoveAxis(Position, direction * (speedPixelsPerSecond * PixelGrid.LogicStep));
            UpdateMovement(before);
        }

        private void UpdateMovement(Vector2 before)
        {
            IsMoving = (Position - before).sqrMagnitude > MapState.Epsilon * MapState.Epsilon;
            if (IsMoving) movingTicks = (movingTicks + 1) % (TrackFrameTicks * 2);
        }
    }
}
