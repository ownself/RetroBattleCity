using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class TankStateTests
    {
        private LevelDefinition level;
        private TankDefinition definition;
        private static readonly TankCommand Idle = new TankCommand(CardinalDirection.None);

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            definition = ScriptableObject.CreateInstance<TankDefinition>();
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(level);
            UnityEngine.Object.DestroyImmediate(definition);
        }
        private static void Tick(ArenaSession session, PlayerButtons buttons = PlayerButtons.None)
            => session.AdvanceFrame(LogicClock.StepSeconds, buttons);

        [TestCase(CombatTeam.Player)] [TestCase(CombatTeam.Enemy)]
        public void TankCommand_SharesMovementAnimationMuzzleCooldownAndCapacity(CombatTeam team)
        {
            var session = new ArenaSession(level, definition);
            var other = session.AddTank(definition, team, new Vector2(72, 24));
            Assert.That(session.Player, Is.SameAs(session.PlayerTank.Motor));
            Assert.That(session.PlayerWeapon, Is.SameAs(session.PlayerTank.Weapon));
            for (int i = 0; i < 8; i++)
            {
                session.SetTankCommand(other.EntityId, new TankCommand(CardinalDirection.Up, true));
                Tick(session, PlayerButtons.W | PlayerButtons.Fire);
                Assert.That(other.Motor.Position - session.Player.Position, Is.EqualTo(new Vector2(48, 0)));
                Assert.That(other.Motor.Facing, Is.EqualTo(session.Player.Facing));
                Assert.That(other.Motor.TrackFrame, Is.EqualTo(session.Player.TrackFrame));
                Assert.That(other.Weapon.CooldownRemainingTicks, Is.EqualTo(session.PlayerWeapon.CooldownRemainingTicks));
            }
            Assert.That(other.Motor.TrackFrame, Is.EqualTo(1));
            Assert.That(other.Weapon.ActiveBullets, Is.EqualTo(1));
            var bullet = session.Bullets.Active.Single(b => b.OwnerId == other.EntityId);
            Assert.That(bullet.Team, Is.EqualTo(team));
            Assert.That(bullet.Position, Is.EqualTo(new Vector2(72, 59)));
            Assert.That(other.Bounds, Is.EqualTo(new Rect(64, 24, 16, 16)));
        }

        [Test]
        public void StableIds_SpawnRejectsOverlapAndNeverConsumesIdOnFailure()
        {
            var session = new ArenaSession(level, definition);
            Assert.Throws<ArgumentException>(() => session.AddTank(definition, CombatTeam.Enemy, new Vector2(30, 24)));
            Assert.Throws<ArgumentException>(() => session.AddTank(definition, CombatTeam.Enemy, new Vector2(float.NaN, 24)));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AddTank(definition, (CombatTeam)2, new Vector2(72, 24)));
            var other = session.AddTank(definition, CombatTeam.Enemy, new Vector2(40, 24));
            Assert.That(session.Tanks.Select(t => t.EntityId), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(other.Weapon.OwnerId, Is.EqualTo(other.EntityId));
            Assert.Throws<ArgumentException>(() => session.SetTankCommand(1, Idle));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SetTankCommand(99, Idle));
        }

        [TestCase(CombatTeam.Player)] [TestCase(CombatTeam.Enemy)]
        public void AllTanksBlock_HighSpeedHeadOnCannotSwapOrTunnel(CombatTeam team)
        {
            definition.moveSpeedPixelsPerSecond = 6000;
            var session = new ArenaSession(level, definition);
            var other = session.AddTank(definition, team, new Vector2(72, 24));
            session.SetTankCommand(other.EntityId, new TankCommand(CardinalDirection.Left));
            Tick(session, PlayerButtons.D);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(56, 24)), "ID 1 moves first against ID 2's original box");
            Assert.That(other.Motor.Position, Is.EqualTo(new Vector2(72, 24)));
            Assert.That(session.Player.IsMoving, Is.True);
            Assert.That(other.Motor.IsMoving, Is.False);
            Tick(session, PlayerButtons.D);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(56, 24)));
        }

        [TestCase(CardinalDirection.Up, 24, 72, 24, 56)]
        [TestCase(CardinalDirection.Down, 24, 8, 24, 24)]
        [TestCase(CardinalDirection.Right, 72, 24, 56, 24)]
        [TestCase(CardinalDirection.Left, 8, 24, 24, 24)]
        public void DynamicSweep_ClampsAtEveryTankFace(CardinalDirection direction, int x, int y, int expectedX, int expectedY)
        {
            var session = new ArenaSession(level, definition);
            var other = session.AddTank(definition, CombatTeam.Enemy, new Vector2(x, y));
            var blockers = new[] { other.Bounds };
            Assert.That(session.Map.MoveAxis(session.Player.Position, direction.Vector() * 1000,
                TankMotor.HalfSizePixels, blockers), Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void DynamicSweep_ExactParallelTouchPassesButOverlapBlocks()
        {
            var session = new ArenaSession(level, definition);
            var other = session.AddTank(definition, CombatTeam.Enemy, new Vector2(72, 40));
            Assert.That(session.Map.MoveAxis(new Vector2(24, 24), new Vector2(100, 0), 8, new[] { other.Bounds }),
                Is.EqualTo(new Vector2(124, 24)));
            Assert.That(session.Map.MoveAxis(new Vector2(24, 24.01f), new Vector2(100, 0), 8, new[] { other.Bounds }),
                Is.EqualTo(new Vector2(56, 24.01f)));
        }

        [TestCase(25)] [TestCase(27)]
        public void SideTurn_RequiresWholeDynamicAlignmentPathClear(int y)
        {
            var session = new ArenaSession(level, definition);
            var mover = session.AddTank(definition, CombatTeam.Enemy, new Vector2(72, y));
            session.AddTank(definition, CombatTeam.Player, new Vector2(72, 8));
            session.SetTankCommand(mover.EntityId, new TankCommand(CardinalDirection.Right));
            for (int i = 0; i < 3; i++) Tick(session);
            Assert.That(mover.Motor.Position, Is.EqualTo(new Vector2(y == 25 ? 75 : 74, 24)), "Exact touching aligned box is legal");
            // Fresh arrangement: a dynamic box obstructs the alignment correction.
            var map = new MapState(level);
            var motor = new TankMotor(new Vector2(72, y), map);
            motor.Step(CardinalDirection.Right, 60, map, new[] { new Rect(64, y - 11, 16, 2.5f) });
            Assert.That(motor.Position, Is.EqualTo(new Vector2(72, y)));
            Assert.That(motor.Facing, Is.EqualTo(CardinalDirection.Up));
        }

        [TestCase(CombatTeam.Player, CombatTeam.Player, 2, false)]
        [TestCase(CombatTeam.Enemy, CombatTeam.Enemy, 2, false)]
        [TestCase(CombatTeam.Player, CombatTeam.Enemy, 2, true)]
        [TestCase(CombatTeam.Enemy, CombatTeam.Player, 2, true)]
        [TestCase(CombatTeam.Player, CombatTeam.Enemy, 1, false)]
        public void CombatRules_FrozenSelfFriendlyHostileMatrix(CombatTeam attacker, CombatTeam target, long targetId, bool expected)
        {
            Assert.That(CombatRules.CanHitTank(attacker, 1, target, targetId), Is.EqualTo(expected));
        }

        [Test]
        public void FriendlyTank_IsIgnoredByRealSweepBeforeHostileTank()
        {
            definition.bulletSpeedPixelsPerSecond = 6000;
            var session = new ArenaSession(level, definition);
            var friendly = session.AddTank(definition, CombatTeam.Player, new Vector2(24, 48));
            var hostile = session.AddTank(definition, CombatTeam.Enemy, new Vector2(24, 80));
            Tick(session, PlayerButtons.Fire);
            Assert.That(friendly.HitPoints, Is.EqualTo(1));
            Assert.That(hostile.IsAlive, Is.False);
            Assert.That(session.Bullets.Hits.Single().TargetId, Is.EqualTo(hostile.EntityId));
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void IncomingHit_UsesHealthAndProtectionForPlayerAndEnemy(bool targetPlayer)
        {
            definition.hitPoints = 2;
            definition.bulletSpeedPixelsPerSecond = 6000;
            definition.fireCooldownTicks = 1;
            var session = new ArenaSession(level, definition);
            var enemy = session.AddTank(definition, CombatTeam.Enemy, new Vector2(24, 64));
            var target = targetPlayer ? session.PlayerTank : enemy;
            target.IsProtected = true;
            Action shoot = () =>
            {
                session.SetTankCommand(enemy.EntityId, targetPlayer ? new TankCommand(CardinalDirection.Down, true) : Idle);
                Tick(session, targetPlayer ? PlayerButtons.None : PlayerButtons.Fire);
            };
            shoot();
            Assert.That(session.Bullets.Hits.Single().TargetId, Is.EqualTo(target.EntityId));
            Assert.That(target.HitPoints, Is.EqualTo(2));
            Assert.That(session.Bullets.Active, Is.Empty, "Protection consumes hostile projectiles");
            target.IsProtected = false;
            shoot(); Assert.That(target.HitPoints, Is.EqualTo(1));
            shoot(); Assert.That(target.HitPoints, Is.Zero);
            Assert.That(target.IsAlive, Is.False);
            Assert.That(target.Motor.IsMoving, Is.False);
            Assert.That(session.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion), Is.EqualTo(1));
            var deadPosition = target.Motor.Position;
            for (int i = 0; i < 3; i++) shoot();
            Assert.That(target.Motor.Position, Is.EqualTo(deadPosition));
            Assert.That(target.HitPoints, Is.Zero);
            Assert.That(session.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion), Is.EqualTo(1));
        }

        [Test]
        public void SameTickAllSweepsSeeLivingFrontTarget_DeathOnlyOnceThenNextTickOpensPath()
        {
            definition.bulletSpeedPixelsPerSecond = 6000;
            definition.fireCooldownTicks = 1;
            var session = new ArenaSession(level, definition);
            var ally = session.AddTank(definition, CombatTeam.Player, new Vector2(40, 24));
            var front = session.AddTank(definition, CombatTeam.Enemy, new Vector2(32, 64));
            var back = session.AddTank(definition, CombatTeam.Enemy, new Vector2(32, 96));
            session.SetTankCommand(ally.EntityId, new TankCommand(CardinalDirection.None, false, true));
            Tick(session, PlayerButtons.Fire);
            Assert.That(session.Bullets.Hits.Count, Is.EqualTo(2));
            Assert.That(session.Bullets.Hits.All(h => h.TargetId == front.EntityId), Is.True);
            Assert.That(front.HitPoints, Is.Zero);
            Assert.That(back.IsAlive, Is.True);
            Assert.That(session.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion), Is.EqualTo(1));
            Assert.That(session.Map.MoveAxis(new Vector2(32, 24), new Vector2(0, 40), 8,
                session.Tanks.Where(t => t.IsAlive && t.EntityId != 1 && t != ally).Select(t => t.Bounds).ToArray()),
                Is.EqualTo(new Vector2(32, 64)), "Dead tank no longer blocks");
            Tick(session, PlayerButtons.Fire);
            Assert.That(back.IsAlive, Is.False);
            Assert.That(session.Bullets.Hits.Single().TargetId, Is.EqualTo(back.EntityId));
            Assert.That(session.Bullets.Hits.Single().BulletId, Is.EqualTo(3), "Non-player fire edge was not replayed");
            var replacement = session.AddTank(definition, CombatTeam.Enemy, front.Motor.Position);
            Assert.That(replacement.EntityId, Is.EqualTo(5));
        }

        [Test]
        public void DamageSettlesAfterAllWeapons_IndependentLanesCanKillBothTeamsInOneTick()
        {
            definition.bulletSpeedPixelsPerSecond = 6000;
            var session = new ArenaSession(level, definition);
            var victim = session.AddTank(definition, CombatTeam.Enemy, new Vector2(64, 24));
            var shooter = session.AddTank(definition, CombatTeam.Enemy, new Vector2(24, 64));
            session.SetTankCommand(shooter.EntityId, new TankCommand(CardinalDirection.Down, false, true));
            Tick(session, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(session.Bullets.Hits.Count, Is.EqualTo(2));
            Assert.That(session.PlayerTank.IsAlive, Is.False);
            Assert.That(victim.IsAlive, Is.False);
            Assert.That(shooter.IsAlive, Is.True);
            Assert.That(session.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion), Is.EqualTo(2));
            Assert.That(session.PlayerWeapon.ActiveBullets + shooter.Weapon.ActiveBullets, Is.Zero);
            Tick(session, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(25, 24)));
            Assert.That(session.Bullets.Hits, Is.Empty);
        }

        [Test]
        public void Death_ReleasesMovementOccupancyButNotOutstandingProjectileLease()
        {
            definition.fireCooldownTicks = 1;
            var session = new ArenaSession(level, definition);
            var victim = session.AddTank(definition, CombatTeam.Enemy, new Vector2(24, 64));
            session.SetTankCommand(victim.EntityId, new TankCommand(CardinalDirection.Right, false, true));
            Tick(session); // Enemy projectile continues away from the player's firing lane.
            long outgoingId = session.Bullets.Active.Single().Id;
            session.SetTankCommand(victim.EntityId, Idle);
            // Use a faster independent shooter to kill the owner while its projectile is in flight.
            definition.bulletSpeedPixelsPerSecond = 6000;
            var shooter = session.AddTank(definition, CombatTeam.Player, new Vector2(24, 88));
            session.SetTankCommand(shooter.EntityId, new TankCommand(CardinalDirection.Down, false, true));
            Tick(session);
            session.SetTankCommand(shooter.EntityId, Idle);
            Assert.That(victim.IsAlive, Is.False);
            Assert.That(victim.Weapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(session.Bullets.Active.Single().Id, Is.EqualTo(outgoingId));
            for (int i = 0; i < 40; i++) Tick(session, PlayerButtons.W);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(24, 64)), "Shared movement passes through the dead tank's old box");
            Assert.That(session.Bullets.Release(outgoingId), Is.True);
            Assert.That(session.Bullets.Release(outgoingId), Is.False);
            Assert.That(victim.Weapon.ActiveBullets, Is.Zero);
        }

        [Test]
        public void Pause_FreezesAllTanksAndClearsNonPlayerCommands()
        {
            var session = new ArenaSession(level, definition);
            var enemy = session.AddTank(definition, CombatTeam.Enemy, new Vector2(72, 24));
            session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Up, true));
            Tick(session);
            var position = enemy.Motor.Position;
            int cooldown = enemy.Weapon.CooldownRemainingTicks;
            session.SetPaused(true);
            session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Right, true));
            session.AdvanceFrame(5, PlayerButtons.Fire);
            Assert.That(session.Clock.Tick, Is.EqualTo(1));
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
            session.SetPaused(false);
            session.Bullets.Clear();
            for (int i = 0; i < 30; i++) Tick(session);
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(session.Bullets.Active, Is.Empty);
        }
    }
}
