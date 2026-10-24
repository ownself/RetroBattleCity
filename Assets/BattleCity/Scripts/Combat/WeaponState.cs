using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Immutable weapon configuration, copied from the shared tank data.</summary>
    public readonly struct WeaponParameters
    {
        public readonly float BulletSpeedPixelsPerSecond;
        public readonly int FireCooldownTicks, MaxActiveBullets;
        public readonly bool CanDestroySteel;
        internal WeaponParameters(TankDefinition definition)
        {
            definition.Validate();
            BulletSpeedPixelsPerSecond = definition.bulletSpeedPixelsPerSecond;
            FireCooldownTicks = definition.fireCooldownTicks;
            MaxActiveBullets = definition.maxActiveBullets;
            CanDestroySteel = definition.canDestroySteel;
        }
    }

    /// <summary>Shared tank weapon: command source is independent of player/AI and presentation.</summary>
    public sealed class WeaponState
    {
        // Tank half-size 8 + bullet half-size 2: the logical boxes touch without overlapping.
        // Prototype muzzle value, not a verified original-game parameter.
        public const float MuzzleOffsetPixels = TankMotor.HalfSizePixels + BulletState.HalfSizePixels;
        private readonly BulletWorld bullets;
        public WeaponParameters Parameters { get; private set; }
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
            Parameters = new WeaponParameters(definition);
        }

        /// <summary>Once per combat tick, after tank movement. A refused request is not queued.</summary>
        public bool Step(TankCommand command, Vector2 tankPosition, CardinalDirection facing)
        {
            if (CooldownRemainingTicks > 0) CooldownRemainingTicks--;
            if ((!command.FireHeld && !command.FirePressed) || CooldownRemainingTicks > 0 || ActiveBullets >= Parameters.MaxActiveBullets)
                return false;
            if (facing == CardinalDirection.None) throw new ArgumentOutOfRangeException(nameof(facing));
            var muzzle = tankPosition + facing.Vector() * MuzzleOffsetPixels;
            bullets.Spawn(this, muzzle, facing, Parameters.BulletSpeedPixelsPerSecond);
            ActiveBullets++;
            CooldownRemainingTicks = Parameters.FireCooldownTicks;
            return true;
        }

        // An upgrade/death never cancels leases, changes in-flight bullets or resets a live cooldown.
        internal void ApplyParameters(WeaponParameters parameters) => Parameters = parameters;

        internal void ResetForRespawn()
        {
            bullets.ReleaseOwnedBy(this);
            if (ActiveBullets != 0) throw new InvalidOperationException("Outstanding player projectile lease after respawn release.");
            CooldownRemainingTicks = 0;
        }

        internal void ReleaseBullet() { ActiveBullets--; }
    }
}
