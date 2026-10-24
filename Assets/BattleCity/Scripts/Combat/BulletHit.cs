using System;
using UnityEngine;

namespace BattleCity
{
    public enum CombatTeam { Player, Enemy }

    public static class CombatRules
    {
        public static bool CanHitTank(CombatTeam attackerTeam, long ownerId, CombatTeam targetTeam, long targetId)
            => attackerTeam != targetTeam && (ownerId == 0 || ownerId != targetId);
    }
    // Numeric order is the frozen same-time hit priority.
    public enum BulletHitKind { Boundary, Terrain, Base, Tank, Bullet }

    /// <summary>Post-movement logical target snapshot, not a Collider or Transform reference.</summary>
    public readonly struct BulletTarget
    {
        public readonly long EntityId;
        public readonly BulletHitKind Kind;
        public readonly CombatTeam Team;
        public readonly Rect Bounds;

        public BulletTarget(long entityId, BulletHitKind kind, CombatTeam team, Rect bounds)
        {
            EntityId = entityId; Kind = kind; Team = team; Bounds = bounds;
            Validate();
        }

        internal void Validate()
        {
            if (EntityId <= 0 || (Kind != BulletHitKind.Base && Kind != BulletHitKind.Tank) ||
                (Team != CombatTeam.Player && Team != CombatTeam.Enemy) ||
                !Finite(Bounds.xMin) || !Finite(Bounds.yMin) || !Finite(Bounds.xMax) || !Finite(Bounds.yMax) ||
                Bounds.width <= 0 || Bounds.height <= 0)
                throw new ArgumentException("Invalid bullet target snapshot.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Immutable impact for downstream damage/terrain/effects; survives pooled bullet reuse.</summary>
    public readonly struct BulletHit
    {
        public readonly long BulletId, OwnerId, TargetId, OtherBulletId;
        public readonly CombatTeam Team;
        public readonly BulletHitKind Kind;
        public readonly CardinalDirection Direction;
        public readonly Vector2 Position, OtherPosition;
        public readonly Vector2Int DamageCell;
        public readonly TerrainKind Terrain;
        public readonly double TickFraction;

        internal BulletHit(BulletState bullet, BulletHitKind kind, long targetId, Vector2 position,
            double fraction, Vector2Int cell, TerrainKind terrain, long otherId = 0, Vector2 otherPosition = default)
        {
            BulletId = bullet.Id; OwnerId = bullet.OwnerId; Team = bullet.Team;
            Direction = bullet.Direction; Kind = kind; TargetId = targetId;
            Position = position; TickFraction = fraction; DamageCell = cell; Terrain = terrain;
            OtherBulletId = otherId; OtherPosition = otherPosition;
        }
    }
}
