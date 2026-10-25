using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Reusable tank movement; the control source is just a direction, not a player component.</summary>
    public sealed class TankMotor
    {
        public const float HalfSizePixels = 8;
        public const int AlignmentPixels = 8;
        public const float MaxTurnCorrectionPixels = 2;
        public const int TrackFrameTicks = 8;
        public const int IceCoastTicks = 24; // Project default, not original-game calibration.
        private int movingTicks;
        private CardinalDirection coastDirection = CardinalDirection.None;
        public int IceRemainingTicks { get; private set; }
        public Vector2 Position { get; private set; }
        public CardinalDirection Facing { get; private set; } = CardinalDirection.Up;
        public bool IsMoving { get; private set; }
        public int TrackFrame => movingTicks / TrackFrameTicks % 2;

        public TankMotor(Vector2 spawn, MapState map)
        {
            if (float.IsNaN(spawn.x) || float.IsInfinity(spawn.x) || float.IsNaN(spawn.y) || float.IsInfinity(spawn.y) ||
                !map.CanOccupy(spawn)) throw new ArgumentException("Tank spawn is invalid or obstructed.", nameof(spawn));
            Position = spawn;
        }

        public void Step(CardinalDirection requested, float speedPixelsPerSecond, MapState map,
            IReadOnlyList<Rect> blockers = null)
        {
            var before = Position;
            var effective = EffectiveDirection(requested, map);
            Position = CalculateStep(effective, speedPixelsPerSecond, map, blockers, out var facing);
            Facing = facing;
            UpdateMovement(before);
            float distance = speedPixelsPerSecond * PixelGrid.LogicStep;
            bool blocked = (Position - before).sqrMagnitude < distance * distance - MapState.Epsilon;
            if (!IsMoving || blocked || !map.IsIceAt(Position)) ClearInertia();
            else if (requested != CardinalDirection.None && facing == requested)
            {
                coastDirection = facing;
                IceRemainingTicks = IceCoastTicks;
            }
            else if (requested == CardinalDirection.None && IceRemainingTicks > 0)
            {
                IceRemainingTicks--;
                if (IceRemainingTicks == 0) coastDirection = CardinalDirection.None;
            }
        }

        // Decision query uses exactly the movement/alignment rules, without advancing pose or animation.
        internal Vector2 PreviewStep(CardinalDirection requested, float speedPixelsPerSecond, MapState map,
            IReadOnlyList<Rect> blockers) => CalculateStep(EffectiveDirection(requested, map), speedPixelsPerSecond, map, blockers, out _);

        private CardinalDirection EffectiveDirection(CardinalDirection requested, MapState map)
            => requested != CardinalDirection.None ? requested :
                IceRemainingTicks > 0 && map.IsIceAt(Position) ? coastDirection : CardinalDirection.None;

        private void ClearInertia() { IceRemainingTicks = 0; coastDirection = CardinalDirection.None; }

        private Vector2 CalculateStep(CardinalDirection requested, float speedPixelsPerSecond, MapState map,
            IReadOnlyList<Rect> blockers, out CardinalDirection facing)
        {
            facing = Facing;
            if (requested == CardinalDirection.None) return Position;
            var direction = requested.Vector(); // validates before changing state
            if (speedPixelsPerSecond <= 0 || float.IsNaN(speedPixelsPerSecond) || float.IsInfinity(speedPixelsPerSecond))
                throw new ArgumentOutOfRangeException(nameof(speedPixelsPerSecond));
            var position = Position;
            if (requested.IsVertical() != Facing.IsVertical())
            {
                float axis = requested.IsVertical() ? Position.x : Position.y;
                float snapped = Mathf.Round(axis / AlignmentPixels) * AlignmentPixels;
                var aligned = requested.IsVertical() ? new Vector2(snapped, Position.y) : new Vector2(Position.x, snapped);
                // Both endpoint and whole correction path must be clear. Never jump over a wall.
                if (!map.CanOccupy(aligned, HalfSizePixels, blockers) ||
                    (map.MoveAxis(Position, aligned - Position, HalfSizePixels, blockers) - aligned).sqrMagnitude > MapState.Epsilon * MapState.Epsilon) return Position;
                if (Mathf.Abs(snapped - axis) > MaxTurnCorrectionPixels + MapState.Epsilon)
                {
                    // A held side-turn must make progress even after the old movement key is released.
                    // Approach the alignment line at normal speed instead of rejecting this request forever.
                    return map.MoveAxis(Position, Vector2.MoveTowards(Position, aligned,
                        speedPixelsPerSecond * PixelGrid.LogicStep) - Position, HalfSizePixels, blockers);
                }
                position = aligned;
            }
            facing = requested;
            return map.MoveAxis(position, direction * (speedPixelsPerSecond * PixelGrid.LogicStep), HalfSizePixels, blockers);
        }

        internal void Stop() { IsMoving = false; ClearInertia(); }

        // Caller has checked the complete spawn footprint against current blockers.
        internal void Respawn(Vector2 position)
        {
            Position = position;
            Facing = CardinalDirection.Up;
            movingTicks = 0;
            Stop();
        }

        private void UpdateMovement(Vector2 before)
        {
            IsMoving = (Position - before).sqrMagnitude > MapState.Epsilon * MapState.Epsilon;
            if (IsMoving) movingTicks = (movingTicks + 1) % (TrackFrameTicks * 2);
        }
    }
}
