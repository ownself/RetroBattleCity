using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>One tank identity and shared capabilities; commands do not know their control source.</summary>
    public sealed class TankState
    {
        private readonly float moveSpeed;
        private readonly int initialHitPoints;
        internal float MoveSpeedPixelsPerSecond => moveSpeed;
        public long EntityId { get; }
        public CombatTeam Team { get; }
        public TankMotor Motor { get; }
        public WeaponState Weapon { get; }
        public int HitPoints { get; private set; }
        public bool IsAlive => HitPoints > 0;
        // Generic protection; session-owned respawn protection is applied independently.
        public bool IsProtected { get; set; }
        public Rect Bounds => new Rect(Motor.Position - Vector2.one * TankMotor.HalfSizePixels,
            Vector2.one * (TankMotor.HalfSizePixels * 2));
        internal TankCommand Command { get; set; } = new TankCommand(CardinalDirection.None);

        internal TankState(long entityId, CombatTeam team, Vector2 spawn, TankDefinition definition,
            MapState map, BulletWorld bullets)
        {
            if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            EntityId = entityId; Team = team;
            Motor = new TankMotor(spawn, map);
            Weapon = new WeaponState(definition, bullets, team, entityId);
            moveSpeed = definition.moveSpeedPixelsPerSecond;
            HitPoints = initialHitPoints = definition.hitPoints;
        }

        internal void Respawn(Vector2 position)
        {
            Weapon.ResetForRespawn();
            Motor.Respawn(position);
            HitPoints = initialHitPoints;
            IsProtected = false;
            Command = new TankCommand(CardinalDirection.None);
        }

        internal void Move(MapState map, IReadOnlyList<Rect> blockers)
        {
            Motor.Step(IsAlive ? Command.Direction : CardinalDirection.None, moveSpeed, map, blockers);
        }

        internal void Fire()
        {
            if (IsAlive) Weapon.Step(Command, Motor.Position, Motor.Facing);
            // A pressed edge lasts one logic tick, whereas held commands persist until replaced.
            Command = new TankCommand(Command.Direction, Command.FireHeld);
        }

        /// <summary>Session calls once per immutable impact, only AFTER all sweeps. True only on death.</summary>
        internal bool ApplyHit(BulletHit hit)
        {
            if (!IsAlive || IsProtected || hit.Kind != BulletHitKind.Tank || hit.TargetId != EntityId ||
                !CombatRules.CanHitTank(hit.Team, hit.OwnerId, Team, EntityId)) return false;
            HitPoints--;
            if (IsAlive) return false;
            Command = new TankCommand(CardinalDirection.None);
            // Stop animation immediately; outstanding projectiles keep their original owner lease.
            Motor.Stop();
            return true;
        }
    }
}
