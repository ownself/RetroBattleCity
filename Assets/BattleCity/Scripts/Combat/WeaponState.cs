using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Shared tank weapon: command source is independent of player/AI and presentation.</summary>
    public sealed class WeaponState
    {
        // Tank half-size 8 + bullet half-size 2: the logical boxes touch without overlapping.
        // Prototype muzzle value, not a verified original-game parameter.
        public const float MuzzleOffsetPixels = 8 + BulletState.HalfSizePixels;
        private readonly BulletWorld bullets;
        private readonly float bulletSpeed;
        private readonly int cooldownTicks, maxActiveBullets;
        public CombatTeam Team { get; }
        public long OwnerId { get; }
        public int CooldownRemainingTicks { get; private set; }
        public int ActiveBullets { get; private set; }

        public WeaponState(TankDefinition definition, BulletWorld bullets, CombatTeam team = CombatTeam.Player, long ownerId = 0)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (bullets == null) throw new ArgumentNullException(nameof(bullets));
            definition.Validate();
            if (team != CombatTeam.Player && team != CombatTeam.Enemy) throw new ArgumentOutOfRangeException(nameof(team));
            if (ownerId < 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
            Team = team; OwnerId = ownerId;
            this.bullets = bullets;
            bulletSpeed = definition.bulletSpeedPixelsPerSecond;
            cooldownTicks = definition.fireCooldownTicks;
            maxActiveBullets = definition.maxActiveBullets;
        }

        /// <summary>Once per combat tick, after tank movement. A refused request is not queued.</summary>
        public bool Step(TankCommand command, Vector2 tankPosition, CardinalDirection facing)
        {
            if (CooldownRemainingTicks > 0) CooldownRemainingTicks--;
            if ((!command.FireHeld && !command.FirePressed) || CooldownRemainingTicks > 0 || ActiveBullets >= maxActiveBullets)
                return false;
            if (facing == CardinalDirection.None) throw new ArgumentOutOfRangeException(nameof(facing));
            var muzzle = tankPosition + facing.Vector() * MuzzleOffsetPixels;
            bullets.Spawn(this, muzzle, facing, bulletSpeed);
            ActiveBullets++;
            CooldownRemainingTicks = cooldownTicks;
            return true;
        }

        internal void ReleaseBullet() { ActiveBullets--; }
    }
}
