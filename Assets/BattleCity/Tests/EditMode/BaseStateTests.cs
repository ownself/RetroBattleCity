using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class BaseStateTests
    {
        private LevelDefinition level;
        private TankDefinition definition;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.baseCenterPixels = new Vector2Int(104, 104);
            definition = ScriptableObject.CreateInstance<TankDefinition>();
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(level);
            UnityEngine.Object.DestroyImmediate(definition);
        }
        private ArenaSession Session() => new ArenaSession(level, definition, enableBase: true);
        private static void Tick(ArenaSession s, int count = 1, PlayerButtons buttons = PlayerButtons.None)
        { for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, buttons); }
        private WeaponState Shoot(BulletWorld world, CombatTeam team, Vector2 position, CardinalDirection direction, long owner = 0)
        {
            var weapon = new WeaponState(definition, world, team, owner);
            Assert.That(weapon.Step(new TankCommand(direction, true), position, direction), Is.True);
            return weapon;
        }

        [TestCase(CombatTeam.Player, CardinalDirection.Up)] [TestCase(CombatTeam.Player, CardinalDirection.Left)]
        [TestCase(CombatTeam.Player, CardinalDirection.Down)] [TestCase(CombatTeam.Player, CardinalDirection.Right)]
        [TestCase(CombatTeam.Enemy, CardinalDirection.Up)] [TestCase(CombatTeam.Enemy, CardinalDirection.Left)]
        [TestCase(CombatTeam.Enemy, CardinalDirection.Down)] [TestCase(CombatTeam.Enemy, CardinalDirection.Right)]
        public void EitherTeamFromEveryDirectionDestroysBase_WithoutTankOwnerFiltering(CombatTeam team, CardinalDirection direction)
        {
            var s = Session(); int notifications = 0; s.BaseDestroyed += () => notifications++;
            var weapon = Shoot(s.Bullets, team, s.Base.Position - direction.Vector() * 32, direction, BaseState.EntityId);
            // Muzzle to base contact is 12px: six ticks at the normal 2px/tick speed.
            Tick(s, 6);
            Assert.That(s.Base.IsAlive, Is.False); Assert.That(s.BaseFailureRequested, Is.True);
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(weapon.ActiveBullets, Is.Zero);
            Assert.That(s.Bullets.Hits.Single().Kind, Is.EqualTo(BulletHitKind.Base));
            Assert.That(s.Bullets.Hits.Single().TargetId, Is.EqualTo(BaseState.EntityId));
        }

        [Test] public void HighSpeedSweepCannotTunnel_OneHitDestroys()
        {
            definition.bulletSpeedPixelsPerSecond = 12000;
            var s = Session(); Shoot(s.Bullets, CombatTeam.Enemy, new Vector2(24, 104), CardinalDirection.Right);
            Tick(s);
            Assert.That(s.Base.IsAlive, Is.False);
            Assert.That(s.Bullets.Hits.Single().Position.x, Is.EqualTo(94).Within(.001));
            Assert.That(s.Bullets.Hits.Single().TickFraction, Is.LessThan(1));
        }

        [TestCase(TerrainKind.Brick)] [TestCase(TerrainKind.Steel)]
        public void EarlierTerrainConsumesShotBeforeBase(TerrainKind terrain)
        {
            level.terrain[13 * 26 + 9] = terrain; definition.bulletSpeedPixelsPerSecond = 12000;
            var s = Session(); Shoot(s.Bullets, CombatTeam.Player, new Vector2(24, 104), CardinalDirection.Right);
            Tick(s);
            Assert.That(s.Base.IsAlive, Is.True); Assert.That(s.BaseFailureRequested, Is.False);
            Assert.That(s.Bullets.Hits.Single().Kind, Is.EqualTo(BulletHitKind.Terrain));
        }

        [TestCase(64, BulletHitKind.Tank)] [TestCase(104, BulletHitKind.Base)] [TestCase(144, BulletHitKind.Base)]
        public void TankBeforeBaseWinsDistance_BaseWinsSameDistancePriority(int tankX, BulletHitKind expected)
        {
            definition.bulletSpeedPixelsPerSecond = 12000;
            var s = Session();
            var targets = new[] {
                new BulletTarget(2, BulletHitKind.Tank, CombatTeam.Enemy, new Rect(tankX-8, 96, 16, 16)),
                new BulletTarget(BaseState.EntityId, BulletHitKind.Base, CombatTeam.Player, s.Base.Bounds) };
            Shoot(s.Bullets, CombatTeam.Player, new Vector2(24, 104), CardinalDirection.Right);
            s.Bullets.Step(s.Map, targets);
            Assert.That(s.Bullets.Hits.Single().Kind, Is.EqualTo(expected));
        }

        [Test] public void SameTickMultipleShotsConsumeOldBase_OneExplosionAndNotification_AllTankDamageStillSettles()
        {
            var s = Session(); var enemy = s.AddTank(definition, CombatTeam.Enemy, new Vector2(160, 160));
            int notifications = 0;
            s.BaseDestroyed += () => { notifications++; Assert.That(enemy.IsAlive, Is.False, "Notification sees all settled damage"); };
            var a = Shoot(s.Bullets, CombatTeam.Player, new Vector2(86, 104), CardinalDirection.Right);
            var b = Shoot(s.Bullets, CombatTeam.Enemy, new Vector2(122, 104), CardinalDirection.Left);
            Shoot(s.Bullets, CombatTeam.Player, new Vector2(160, 142), CardinalDirection.Up);
            Tick(s);
            Assert.That(s.Bullets.Hits.Count(h => h.Kind == BulletHitKind.Base), Is.EqualTo(2));
            Assert.That(s.Bullets.Hits.Count(h => h.Kind == BulletHitKind.Tank), Is.EqualTo(1));
            Assert.That(a.ActiveBullets + b.ActiveBullets, Is.Zero);
            Assert.That(s.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion && e.Position == s.Base.Position), Is.EqualTo(1));
            Assert.That(s.AliveEnemyCount, Is.Zero); Assert.That(s.BaseFailureRequested, Is.True);
            Assert.That(s.Clock.Paused, Is.False); Assert.That(notifications, Is.EqualTo(1));
            Tick(s, 30);
            Shoot(s.Bullets, CombatTeam.Player, new Vector2(86, 104), CardinalDirection.Right);
            Tick(s, 5);
            Assert.That(s.Bullets.Active.Count, Is.EqualTo(1), "Ruins are no longer a bullet target");
            Assert.That(s.Bullets.Hits, Is.Empty); Assert.That(s.Effects.Active, Is.Empty);
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(s.Clock.Tick, Is.EqualTo(36));
        }

        [TestCase(false)] [TestCase(true)] public void IntactAndRuinedFootprintsBlockMovementAndExplicitSpawns(bool destroy)
        {
            level.playerSpawnPixels = new Vector2Int(72, 104); var s = Session();
            if (destroy) { Shoot(s.Bullets, CombatTeam.Enemy, new Vector2(122, 104), CardinalDirection.Left); Tick(s); }
            Tick(s, 40, PlayerButtons.D);
            Assert.That(s.Player.Position, Is.EqualTo(new Vector2(88, 104)), "Half-open contact is allowed but never overlap");
            Assert.Throws<ArgumentException>(() => s.AddTank(definition, CombatTeam.Enemy, s.Base.Position));
            Assert.DoesNotThrow(() => s.AddTank(definition, CombatTeam.Enemy, new Vector2(120, 104)));
        }

        [TestCase(false)] [TestCase(true)] public void IntactAndRuinedBaseBlockNaturalBirths(bool destroy)
        {
            level.enemySpawnPixels = new[] { level.baseCenterPixels }; level.enemySequence = new[] { definition.definitionId };
            var wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            try
            {
                wave.tankDefinitions = new[] { definition }; wave.preparationTicks = 1; wave.spawnIntervalTicks = 1;
                var s = new ArenaSession(level, definition, wave, enableBase: true);
                if (destroy) Shoot(s.Bullets, CombatTeam.Enemy, new Vector2(122, 104), CardinalDirection.Left);
                Tick(s, 100);
                Assert.That(s.EnemyWave.SpawnedCount, Is.Zero); Assert.That(s.EnemyWave.ReservedCount, Is.Zero);
                Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(1)); Assert.That(s.Base.IsAlive, Is.EqualTo(!destroy));
            }
            finally { UnityEngine.Object.DestroyImmediate(wave); }
        }

        [Test] public void PauseFreezesIncomingShotAndFailure_ResumeSettlesOnce()
        {
            var s = Session(); int notifications = 0; s.BaseDestroyed += () => notifications++;
            Shoot(s.Bullets, CombatTeam.Enemy, new Vector2(122, 104), CardinalDirection.Left);
            s.SetPaused(true); Tick(s, 100);
            Assert.That(s.Base.IsAlive, Is.True); Assert.That(s.Clock.Tick, Is.Zero); Assert.That(notifications, Is.Zero);
            Assert.That(s.Bullets.Active.Single().AgeTicks, Is.Zero);
            s.SetPaused(false); Tick(s, 100);
            Assert.That(s.Base.IsAlive, Is.False); Assert.That(notifications, Is.EqualTo(1));
        }

        [Test] public void LegacySessionHasNoInvisibleBase_EnabledSessionsOwnIndependentSnapshot()
        {
            var legacy = new ArenaSession(level, definition);
            Assert.That(legacy.Base, Is.Null); Assert.That(legacy.BaseFailureRequested, Is.False);
            Assert.DoesNotThrow(() => legacy.AddTank(definition, CombatTeam.Enemy, level.baseCenterPixels));
            var a = Session(); var b = Session(); level.baseCenterPixels = new Vector2Int(160, 24);
            Shoot(a.Bullets, CombatTeam.Enemy, new Vector2(122, 104), CardinalDirection.Left); Tick(a); Tick(b);
            Assert.That(a.Base.IsAlive, Is.False); Assert.That(b.Base.IsAlive, Is.True); Assert.That(b.BaseFailureRequested, Is.False);
            Assert.That(a.Base.Position, Is.EqualTo(new Vector2(104, 104))); Assert.That(b.Base.Position, Is.EqualTo(a.Base.Position));
            Assert.That(a.Tanks.All(t => t.EntityId != BaseState.EntityId), Is.True);
        }
    }
}
