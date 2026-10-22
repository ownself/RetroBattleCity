using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class WeaponTests
    {
        private TankDefinition definition;
        private BulletWorld world;
        private WeaponState weapon;
        private static readonly TankCommand Fire = new TankCommand(CardinalDirection.None, true);
        [SetUp] public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<TankDefinition>();
            world = new BulletWorld();
            weapon = new WeaponState(definition, world);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(definition); }

        [TestCase(CardinalDirection.Up)] [TestCase(CardinalDirection.Left)]
        [TestCase(CardinalDirection.Down)] [TestCase(CardinalDirection.Right)]
        public void MuzzleAndTravel_UseFacingAndLogicalPixels(CardinalDirection facing)
        {
            var center = new Vector2(104, 104);
            Assert.That(weapon.Step(Fire, center, facing), Is.True);
            var bullet = world.Active[0];
            var muzzle = center + facing.Vector() * 10;
            Assert.That(bullet.Position, Is.EqualTo(muzzle));
            Assert.That(bullet.PreviousPosition, Is.EqualTo(muzzle));
            Assert.That(bullet.Owner, Is.SameAs(weapon));
            Assert.That(bullet.Direction, Is.EqualTo(facing));
            Assert.That(bullet.AgeTicks, Is.Zero);
            world.Step();
            Assert.That(bullet.Position, Is.EqualTo(muzzle + facing.Vector() * 3));
            Assert.That(bullet.PreviousPosition, Is.EqualTo(muzzle));
            Assert.That(bullet.AgeTicks, Is.EqualTo(1));
            for (int i = 0; i < 100; i++) world.Step();
            Assert.That(world.Active, Is.Empty);
            Assert.That(weapon.ActiveBullets, Is.Zero);
        }

        [Test]
        public void Cooldown_FirstShotImmediateNextShotExactlyFifteenTicksLater()
        {
            Assert.That(weapon.Step(Fire, new Vector2(24, 24), CardinalDirection.Up), Is.True);
            world.Clear();
            for (int i = 1; i < 15; i++)
                Assert.That(weapon.Step(Fire, new Vector2(24, 24), CardinalDirection.Up), Is.False, "offset " + i);
            Assert.That(weapon.Step(Fire, new Vector2(24, 24), CardinalDirection.Up), Is.True);
        }

        [Test]
        public void ActiveLimit_RejectsWithoutRestartingCooldownAndRearmsOnRelease()
        {
            weapon.Step(Fire, new Vector2(104, 104), CardinalDirection.Up);
            long id = world.Active[0].Id;
            for (int i = 0; i < 30; i++) Assert.That(weapon.Step(Fire, Vector2.zero, CardinalDirection.Up), Is.False);
            Assert.That(world.Active.Count, Is.EqualTo(1));
            Assert.That(weapon.CooldownRemainingTicks, Is.Zero);
            Assert.That(world.Release(id), Is.True);
            Assert.That(world.Release(id), Is.False);
            Assert.That(weapon.ActiveBullets, Is.Zero);
            Assert.That(weapon.Step(Fire, new Vector2(104, 104), CardinalDirection.Down), Is.True);
        }

        [Test]
        public void ConfiguredCap_IsPerOwnerAndCanExceedOne()
        {
            definition.maxActiveBullets = 2; definition.fireCooldownTicks = 1;
            var other = new WeaponState(definition, world);
            for (int i = 0; i < 2; i++) Assert.That(other.Step(Fire, new Vector2(104, 104), CardinalDirection.Left), Is.True);
            Assert.That(other.Step(Fire, Vector2.zero, CardinalDirection.Left), Is.False);
            Assert.That(weapon.Step(Fire, new Vector2(24, 24), CardinalDirection.Up), Is.True);
            Assert.That(other.ActiveBullets, Is.EqualTo(2));
            Assert.That(weapon.ActiveBullets, Is.EqualTo(1));
            world.Clear(); world.Clear();
            Assert.That(other.ActiveBullets + weapon.ActiveBullets, Is.Zero);
        }

        [Test]
        public void PoolReuse_ResetsStateAndStaleLeaseCannotReleaseNewBullet()
        {
            weapon.Step(Fire, new Vector2(104, 104), CardinalDirection.Up);
            var pooled = world.Active[0]; long staleId = pooled.Id;
            world.Step(); world.Release(staleId);
            Assert.That(pooled.Owner, Is.Null);
            Assert.That(pooled.Direction, Is.EqualTo(CardinalDirection.None));
            Assert.That(pooled.SpeedPixelsPerSecond, Is.Zero);
            definition.bulletSpeedPixelsPerSecond = 240;
            var other = new WeaponState(definition, world);
            other.Step(Fire, new Vector2(80, 80), CardinalDirection.Left);
            var reused = world.Active[0];
            Assert.That(reused, Is.SameAs(pooled));
            Assert.That(reused.Id, Is.Not.EqualTo(staleId));
            Assert.That(reused.Owner, Is.SameAs(other));
            Assert.That(reused.Direction, Is.EqualTo(CardinalDirection.Left));
            Assert.That(reused.Position, Is.EqualTo(new Vector2(70, 80)));
            Assert.That(reused.PreviousPosition, Is.EqualTo(reused.Position));
            Assert.That(reused.SpeedPixelsPerSecond, Is.EqualTo(240));
            Assert.That(reused.AgeTicks, Is.Zero);
            Assert.That(world.Release(staleId), Is.False);
            Assert.That(other.ActiveBullets, Is.EqualTo(1));
            world.Clear();
            Assert.That(other.ActiveBullets + weapon.ActiveBullets, Is.Zero);
        }

        [Test]
        public void HeldFire_RepeatsWhenCooldownAndCapacityBothAllow()
        {
            int shots = 0;
            for (int tick = 0; tick < 60; tick++)
            {
                if (weapon.Step(Fire, new Vector2(104, 184), CardinalDirection.Up)) shots++;
                world.Step();
            }
            Assert.That(shots, Is.EqualTo(4));
            Assert.That(world.Active, Is.Empty);
        }

        [Test]
        public void RefusedTap_IsNotQueuedForLater()
        {
            weapon.Step(Fire, new Vector2(24, 24), CardinalDirection.Up); world.Clear();
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.None, false, true), new Vector2(24, 24), CardinalDirection.Up), Is.False);
            for (int i = 0; i < 30; i++) weapon.Step(new TankCommand(CardinalDirection.None), new Vector2(24, 24), CardinalDirection.Up);
            Assert.That(world.Active, Is.Empty);
        }

        [Test]
        public void Session_MovesThenSpawnsAndAdvancesBulletInSameTick()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                level.playerSpawnPixels = new Vector2Int(24, 24);
                var session = new ArenaSession(level, definition);
                session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire);
                Assert.That(session.Player.Position, Is.EqualTo(new Vector2(25, 24)));
                Assert.That(session.Bullets.Active[0].PreviousPosition, Is.EqualTo(new Vector2(35, 24)));
                Assert.That(session.Bullets.Active[0].Position, Is.EqualTo(new Vector2(38, 24)));
                Assert.That(session.Bullets.Active[0].Direction, Is.EqualTo(CardinalDirection.Right));
            }
            finally { Object.DestroyImmediate(level); }
        }

        [Test]
        public void Pause_FreezesBulletAndCooldownAndDoesNotResumeHeldFire()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                level.playerSpawnPixels = new Vector2Int(24, 24);
                var session = new ArenaSession(level, definition);
                session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
                var bullet = session.Bullets.Active[0]; var position = bullet.Position;
                int cooldown = session.PlayerWeapon.CooldownRemainingTicks;
                session.AdvanceFrame(1, PlayerButtons.Fire | PlayerButtons.Pause);
                session.AdvanceFrame(1, PlayerButtons.Fire);
                Assert.That(bullet.Position, Is.EqualTo(position));
                Assert.That(session.PlayerWeapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
                session.AdvanceFrame(1, PlayerButtons.Fire | PlayerButtons.Pause);
                session.Bullets.Clear();
                for (int i = 0; i < 30; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
                Assert.That(session.Bullets.Active, Is.Empty);
                session.AdvanceFrame(0, PlayerButtons.None);
                session.AdvanceFrame(0, PlayerButtons.Fire);
                session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                Assert.That(session.Bullets.Active.Count, Is.EqualTo(1), "short tap survives until the tick");
            }
            finally { Object.DestroyImmediate(level); }
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void BulletTravel_IsRenderRateIndependent(int fps)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                level.playerSpawnPixels = new Vector2Int(24, 24);
                var session = new ArenaSession(level, definition);
                session.AdvanceFrame(0, PlayerButtons.Fire);
                for (int frame = 0; frame < fps / 2; frame++) session.AdvanceFrame(1.0 / fps, PlayerButtons.None);
                Assert.That(session.Clock.Tick, Is.EqualTo(30));
                Assert.That(session.Bullets.Active[0].Position, Is.EqualTo(new Vector2(24, 124)));
            }
            finally { Object.DestroyImmediate(level); }
        }
    }
}
