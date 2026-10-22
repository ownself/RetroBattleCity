using System;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class BulletCollisionTests
    {
        private LevelDefinition level;
        private TankDefinition definition;
        private BulletWorld world;
        private long nextOwner;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            definition = ScriptableObject.CreateInstance<TankDefinition>();
            world = new BulletWorld(); nextOwner = 10;
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(definition); }

        private WeaponState Spawn(Vector2 center, CardinalDirection direction, float speed = 6000,
            CombatTeam team = CombatTeam.Player, long ownerId = 0)
        {
            definition.bulletSpeedPixelsPerSecond = speed;
            var weapon = new WeaponState(definition, world, team, ownerId == 0 ? ++nextOwner : ownerId);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.None, true),
                center - direction.Vector() * WeaponState.MuzzleOffsetPixels, direction), Is.True);
            return weapon;
        }

        private static BulletTarget Tank(long id, CombatTeam team, float x = 80, float y = 96) =>
            new BulletTarget(id, BulletHitKind.Tank, team, new Rect(x, y, 8, 8));
        private void Tile(int x, int y, TerrainKind kind) { level.terrain[y / 8 * 26 + x / 8] = kind; }
        private void AssertPoint(Vector2 actual, Vector2 expected)
        { Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.001f)); }

        [TestCase(CardinalDirection.Up, 100, 64, 100, 94)]
        [TestCase(CardinalDirection.Left, 144, 100, 106, 100)]
        [TestCase(CardinalDirection.Down, 100, 144, 100, 106)]
        [TestCase(CardinalDirection.Right, 64, 100, 94, 100)]
        public void HighSpeed_FourSidesStopAtFirstSteelFace(CardinalDirection direction, float x, float y, float hx, float hy)
        {
            Tile(96, 96, TerrainKind.Steel);
            var weapon = Spawn(new Vector2(x, y), direction);
            world.Step(new MapState(level));
            Assert.That(world.Active, Is.Empty);
            Assert.That(weapon.ActiveBullets, Is.Zero);
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Terrain));
            Assert.That(world.Hits[0].Terrain, Is.EqualTo(TerrainKind.Steel));
            AssertPoint(world.Hits[0].Position, new Vector2(hx, hy));
        }

        [TestCase(CardinalDirection.Up, 100, 206)] [TestCase(CardinalDirection.Left, 2, 100)]
        [TestCase(CardinalDirection.Down, 100, 2)] [TestCase(CardinalDirection.Right, 206, 100)]
        public void BoundaryImpact_RecordsExactBoxContact(CardinalDirection direction, float x, float y)
        {
            Spawn(new Vector2(100, 100), direction, 60000);
            world.Step();
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Boundary));
            AssertPoint(world.Hits[0].Position, new Vector2(x, y));
        }

        [Test]
        public void Terrain_NearestWinsAndBrickIsNotDestroyedYet()
        {
            Tile(64, 96, TerrainKind.Brick); Tile(96, 96, TerrainKind.Steel);
            var map = new MapState(level);
            Spawn(new Vector2(40, 100), CardinalDirection.Right, 100000000);
            world.Step(map);
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Terrain, Is.EqualTo(TerrainKind.Brick));
            AssertPoint(world.Hits[0].Position, new Vector2(62, 100));
            Assert.That(world.Hits[0].DamageCell, Is.EqualTo(new Vector2Int(16, 24)));
            Assert.That(map.BlocksBullet(16, 24), Is.True, "BC-106 will consume the event to damage brick");
        }

        [TestCase(TerrainKind.Water)] [TestCase(TerrainKind.Grass)] [TestCase(TerrainKind.Ice)]
        public void NonBlockingTerrain_DoesNotConsumeBullets(TerrainKind kind)
        {
            Tile(64, 96, kind);
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            world.Step(new MapState(level));
            Assert.That(world.Hits, Is.Empty);
            Assert.That(world.Active.Count, Is.EqualTo(1));
            AssertPoint(world.Active[0].Position, new Vector2(140, 100));
        }

        [Test]
        public void SpawnInsideBrick_IsConsumedAtZeroTimeAndOnlyOnce()
        {
            Tile(64, 96, TerrainKind.Brick);
            var weapon = Spawn(new Vector2(66, 100), CardinalDirection.Right);
            long id = world.Active[0].Id;
            world.Step(new MapState(level));
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].TickFraction, Is.Zero);
            Assert.That(weapon.ActiveBullets, Is.Zero);
            Assert.That(world.Release(id), Is.False);
            world.Step(new MapState(level));
            Assert.That(world.Hits, Is.Empty);
        }

        [TestCase(CombatTeam.Player)] [TestCase(CombatTeam.Enemy)]
        public void Tanks_FriendlyAndOwnerAreIgnoredButHostileIsHit(CombatTeam team)
        {
            var other = team == CombatTeam.Player ? CombatTeam.Enemy : CombatTeam.Player;
            var weapon = Spawn(new Vector2(40, 100), CardinalDirection.Right, 6000, team, 10);
            world.Step(null, new[] { Tank(30, other, 100), Tank(20, team, 64), Tank(10, other, 48) });
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].TargetId, Is.EqualTo(30));
            Assert.That(world.Hits[0].OwnerId, Is.EqualTo(10));
            Assert.That(world.Hits[0].Team, Is.EqualTo(team));
            Assert.That(weapon.ActiveBullets, Is.Zero);
            AssertPoint(world.Hits[0].Position, new Vector2(98, 100));
        }

        [TestCase(CombatTeam.Player)] [TestCase(CombatTeam.Enemy)]
        public void Base_BothTeamsCanHit(CombatTeam team)
        {
            Spawn(new Vector2(40, 100), CardinalDirection.Right, 6000, team);
            world.Step(null, new[] { new BulletTarget(50, BulletHitKind.Base, team, new Rect(80, 96, 8, 8)) });
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Base));
            AssertPoint(world.Hits[0].Position, new Vector2(78, 100));
        }

        [Test]
        public void NearestDistanceWinsBeforeKindPriority()
        {
            Tile(96, 96, TerrainKind.Steel);
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            world.Step(new MapState(level), new[] { Tank(20, CombatTeam.Enemy, 64) });
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Tank));
        }

        [Test]
        public void SameTime_TerrainBeforeBaseAndTank()
        {
            Tile(80, 96, TerrainKind.Steel);
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            world.Step(new MapState(level), new[] {
                Tank(1, CombatTeam.Enemy), new BulletTarget(2, BulletHitKind.Base, CombatTeam.Player, new Rect(80, 96, 8, 8)) });
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Terrain));
        }

        [Test]
        public void SameTime_BaseBeforeTank()
        {
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            world.Step(null, new[] { Tank(1, CombatTeam.Enemy),
                new BulletTarget(2, BulletHitKind.Base, CombatTeam.Player, new Rect(80, 96, 8, 8)) });
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Base));
        }

        [Test]
        public void SameTime_TankBeforeBulletPair()
        {
            Spawn(new Vector2(50, 100), CardinalDirection.Right);
            var enemy = Spawn(new Vector2(110, 100), CardinalDirection.Left, 6000, CombatTeam.Enemy);
            world.Step(null, new[] { Tank(1, CombatTeam.Enemy) });
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Tank));
            Assert.That(enemy.ActiveBullets, Is.EqualTo(1));
        }

        [Test]
        public void SameTime_BoundaryBeforeTerrainAndBulletPair()
        {
            Tile(200, 96, TerrainKind.Steel);
            Spawn(new Vector2(206, 100), CardinalDirection.Left);
            Spawn(new Vector2(204, 100), CardinalDirection.Right, 6000, CombatTeam.Enemy);
            world.Step(new MapState(level));
            Assert.That(world.Hits.Count, Is.EqualTo(2));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Boundary));
            Assert.That(world.Hits[1].Kind, Is.EqualTo(BulletHitKind.Terrain));
            Assert.That(world.Active, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void SameTimeSameKind_LowestStableTargetIdWinsRegardlessOfInputOrder(bool reverse)
        {
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            var targets = new[] { Tank(20, CombatTeam.Enemy), Tank(10, CombatTeam.Enemy) };
            if (reverse) Array.Reverse(targets);
            world.Step(null, targets);
            Assert.That(world.Hits[0].TargetId, Is.EqualTo(10));
        }

        [Test]
        public void GlobalOrdering_UsesTickTimeNotDistanceOrSpawnOrder()
        {
            Spawn(new Vector2(50, 100), CardinalDirection.Right, 600, CombatTeam.Player);
            long first = world.Active[0].Id;
            Spawn(new Vector2(50, 150), CardinalDirection.Right, 6000, CombatTeam.Enemy);
            long second = world.Active[1].Id;
            world.Step(null, new[] { Tank(1, CombatTeam.Enemy, 60), Tank(2, CombatTeam.Player, 100, 146) });
            Assert.That(world.Hits.Count, Is.EqualTo(2));
            Assert.That(world.Hits[0].BulletId, Is.EqualTo(second));
            Assert.That(world.Hits[1].BulletId, Is.EqualTo(first));
            Assert.That(world.Hits[0].TickFraction, Is.LessThan(world.Hits[1].TickFraction));
        }

        [Test]
        public void HostileHeadOnPair_CannotTunnelAndReleasesBothOwners()
        {
            var a = Spawn(new Vector2(40, 100), CardinalDirection.Right);
            long aId = world.Active[0].Id;
            var b = Spawn(new Vector2(160, 100), CardinalDirection.Left, 6000, CombatTeam.Enemy);
            long bId = world.Active[1].Id;
            world.Step();
            Assert.That(world.Active, Is.Empty);
            Assert.That(a.ActiveBullets + b.ActiveBullets, Is.Zero);
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            var hit = world.Hits[0];
            Assert.That(hit.Kind, Is.EqualTo(BulletHitKind.Bullet));
            Assert.That(hit.BulletId, Is.EqualTo(aId)); Assert.That(hit.OtherBulletId, Is.EqualTo(bId));
            AssertPoint(hit.Position, new Vector2(98, 100)); AssertPoint(hit.OtherPosition, new Vector2(102, 100));
        }

        [Test]
        public void HostilePerpendicularPair_UsesRelativeSweep()
        {
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            Spawn(new Vector2(100, 40), CardinalDirection.Up, 6000, CombatTeam.Enemy);
            world.Step();
            Assert.That(world.Active, Is.Empty);
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Bullet));
        }

        [Test]
        public void FriendlyBullets_PassThroughEachOther()
        {
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            Spawn(new Vector2(160, 100), CardinalDirection.Left);
            world.Step();
            Assert.That(world.Active.Count, Is.EqualTo(2));
            Assert.That(world.Hits, Is.Empty);
        }

        [Test]
        public void EarlierWallInvalidatesPair_AndOtherBulletStillGetsItsLaterWallHit()
        {
            Tile(64, 96, TerrainKind.Steel);
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            Spawn(new Vector2(160, 100), CardinalDirection.Left, 6000, CombatTeam.Enemy);
            world.Step(new MapState(level));
            Assert.That(world.Active, Is.Empty);
            Assert.That(world.Hits.Count, Is.EqualTo(2));
            foreach (var hit in world.Hits) Assert.That(hit.Kind, Is.EqualTo(BulletHitKind.Terrain));
            AssertPoint(world.Hits[0].Position, new Vector2(62, 100));
            AssertPoint(world.Hits[1].Position, new Vector2(74, 100));
        }

        [Test]
        public void EarlierPairInvalidatesLaterWallAndBoundaryHits()
        {
            Tile(128, 96, TerrainKind.Steel);
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            Spawn(new Vector2(100, 100), CardinalDirection.Left, 6000, CombatTeam.Enemy);
            world.Step(new MapState(level));
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].Kind, Is.EqualTo(BulletHitKind.Bullet));
            Assert.That(world.Active, Is.Empty);
        }

        [Test]
        public void ThreeWayContact_ConsumesEachLeaseAtMostOnce()
        {
            Spawn(new Vector2(100, 100), CardinalDirection.Right);
            Spawn(new Vector2(100, 100), CardinalDirection.Left, 60, CombatTeam.Enemy);
            Spawn(new Vector2(100, 100), CardinalDirection.Up, 60, CombatTeam.Enemy);
            world.Step();
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Active.Count, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void StartingContact_HitsOnlyWhenMovingIntoTarget(bool toward)
        {
            Spawn(new Vector2(78, 100), toward ? CardinalDirection.Right : CardinalDirection.Left, 600);
            world.Step(null, new[] { Tank(1, CombatTeam.Enemy) });
            Assert.That(world.Hits.Count, Is.EqualTo(toward ? 1 : 0));
            if (toward) Assert.That(world.Hits[0].TickFraction, Is.Zero);
        }

        [Test]
        public void GrazingParallelEdge_IsNotAHit()
        {
            Spawn(new Vector2(40, 94), CardinalDirection.Right);
            world.Step(null, new[] { Tank(1, CombatTeam.Enemy) });
            Assert.That(world.Hits, Is.Empty);
        }

        [Test]
        public void ContactExactlyAtTickEnd_IsAHit()
        {
            Spawn(new Vector2(68, 100), CardinalDirection.Right, 600);
            world.Step(null, new[] { Tank(1, CombatTeam.Enemy) });
            Assert.That(world.Hits.Count, Is.EqualTo(1));
            Assert.That(world.Hits[0].TickFraction, Is.EqualTo(1).Within(.00001));
        }

        [Test]
        public void ImmutableHitSurvivesPoolReuseAndStaleIdCannotReleaseNewLease()
        {
            var owner = Spawn(new Vector2(40, 100), CardinalDirection.Right);
            var pooled = world.Active[0]; long stale = pooled.Id;
            world.Step(null, new[] { Tank(1, CombatTeam.Enemy) });
            var hit = world.Hits[0];
            Spawn(new Vector2(100, 150), CardinalDirection.Up, 60, CombatTeam.Enemy);
            Assert.That(world.Active[0], Is.SameAs(pooled));
            Assert.That(world.Active[0].OwnerId, Is.Not.EqualTo(hit.OwnerId));
            Assert.That(world.Release(stale), Is.False);
            Assert.That(hit.BulletId, Is.EqualTo(stale));
            Assert.That(hit.OwnerId, Is.EqualTo(owner.OwnerId));
            AssertPoint(hit.Position, new Vector2(78, 100));
            Assert.That(owner.ActiveBullets, Is.Zero);
        }

        [Test]
        public void DuplicateTargetIds_AreRejectedBeforeAdvancingBullets()
        {
            Spawn(new Vector2(40, 100), CardinalDirection.Right);
            Assert.Throws<ArgumentException>(() => world.Step(null, new[] { Tank(1, CombatTeam.Enemy), Tank(1, CombatTeam.Player) }));
            AssertPoint(world.Active[0].Position, new Vector2(40, 100));
            Assert.That(world.Active[0].AgeTicks, Is.Zero);
        }

        [Test]
        public void Session_ActuallyUsesTerrainSweep()
        {
            level.playerSpawnPixels = new Vector2Int(24, 24);
            Tile(32, 24, TerrainKind.Steel); // adjacent to the tank, muzzle starts in wall
            var session = new ArenaSession(level, definition);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(session.Bullets.Active, Is.Empty);
            Assert.That(session.Bullets.Hits.Count, Is.EqualTo(1));
            Assert.That(session.Bullets.Hits[0].Kind, Is.EqualTo(BulletHitKind.Terrain));
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
        }
    }
}
