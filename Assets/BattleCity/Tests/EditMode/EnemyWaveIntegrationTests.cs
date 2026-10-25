using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class EnemyWaveIntegrationTests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        private EnemyWaveDefinition wave;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.enemySpawnPixels = new[] { new Vector2Int(104, 104) };
            tank = ScriptableObject.CreateInstance<TankDefinition>();
            tank.definitionId = "enemy.test";
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            wave.tankDefinitions = new[] { tank };
            wave.preparationTicks = 2; wave.spawnIntervalTicks = 3; wave.seed = 1;
            level.enemySequence = Enumerable.Repeat(tank.definitionId, 6).ToArray();
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(wave);
            UnityEngine.Object.DestroyImmediate(tank);
            UnityEngine.Object.DestroyImmediate(level);
        }
        private static void Tick(ArenaSession s, int count = 1, PlayerButtons input = PlayerButtons.None)
        {
            for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, input);
        }
        private static TankState Enemy(ArenaSession s) => s.Tanks.Last(t => t.Team == CombatTeam.Enemy);
        private static void Counts(ArenaSession s)
        {
            var w = s.EnemyWave;
            Assert.That(w.SpawnedCount + w.PendingCount, Is.EqualTo(w.TotalCount));
            Assert.That(w.UnreservedCount + w.ReservedCount, Is.EqualTo(w.PendingCount));
            Assert.That(s.AliveEnemyCount + w.ReservedCount, Is.LessThanOrEqualTo(w.MaxActiveEnemies));
            Assert.That(w.AliveCount, Is.EqualTo(s.AliveEnemyCount));
        }
        private WeaponState ShootEnemy(ArenaSession s, TankState victim)
        {
            var weapon = new WeaponState(tank, s.Bullets, CombatTeam.Player, 999);
            weapon.Step(new TankCommand(CardinalDirection.Down, true), victim.Motor.Position + new Vector2(0, 18), CardinalDirection.Down);
            return weapon;
        }

        [Test] public void OptionalWave_PreservesLegacyPureLogicAndEmptySequence()
        {
            var legacy = new ArenaSession(level, tank);
            Tick(legacy, 500);
            Assert.That(legacy.EnemyWave, Is.Null); Assert.That(legacy.Tanks.Count, Is.EqualTo(1));
            level.enemySequence = Array.Empty<string>();
            var empty = new ArenaSession(level, tank, wave);
            Tick(empty, 500); Counts(empty);
            Assert.That(empty.EnemyWave.SequenceExhausted, Is.True); Assert.That(empty.Tanks.Count, Is.EqualTo(1));
        }

        [Test] public void SpawnCommitsSharedTankAndBrainAfterCombat_NewEnemyOnlyActsNextTick()
        {
            var s = new ArenaSession(level, tank, wave);
            Tick(s, 2);
            Assert.That(s.Tanks.Count, Is.EqualTo(1)); Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1));
            Tick(s);
            var enemy = Enemy(s); Counts(s);
            Assert.That(s.GetWaveEnemyDefinition(enemy.EntityId), Is.SameAs(tank));
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1));
            Assert.That(enemy.Motor.Position, Is.EqualTo(new Vector2(104, 104)));
            Assert.That(enemy.Weapon.ActiveBullets, Is.Zero); Assert.That(s.Bullets.Active, Is.Empty);
            Assert.Throws<ArgumentException>(() => s.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Up)));
            Tick(s);
            Assert.That(enemy.Motor.Position, Is.EqualTo(new Vector2(104, 103)));
            Assert.That(enemy.Motor.Facing, Is.EqualTo(CardinalDirection.Down));
            // Movement starts next tick, but the independent first-shot delay must still be honored.
            Assert.That(enemy.Weapon.ActiveBullets, Is.Zero);
            Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.Zero);
            Assert.That(s.Bullets.Active, Is.Empty);
        }

        [Test] public void AllSpawnsOccupiedByPlayer_WaitsWithoutConsumingSequenceThenUsesReleasedSpace()
        {
            level.playerSpawnPixels = level.enemySpawnPixels[0];
            var s = new ArenaSession(level, tank, wave);
            Tick(s, 30); Counts(s);
            Assert.That(s.EnemyWave.SpawnedCount, Is.Zero); Assert.That(s.EnemyWave.ReservedCount, Is.Zero);
            Assert.That(s.EnemyWave.RandomState, Is.EqualTo(1u));
            Tick(s, 16, PlayerButtons.W);
            Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1));
            Tick(s, 2); Counts(s);
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1));
            Assert.That(s.PlayerTank.Bounds.Overlaps(Enemy(s).Bounds), Is.False);
        }

        [Test] public void PreparationEndsWithPlayerInTheWay_HoldsRequestAndCountUntilPlayerLeaves()
        {
            level.playerSpawnPixels = new Vector2Int(104, 80); wave.preparationTicks = 20;
            var s = new ArenaSession(level, tank, wave);
            Tick(s);
            var request = s.EnemyWave.Reservation.Value;
            Tick(s, 16, PlayerButtons.W); Tick(s, 40);
            Assert.That(s.Player.Position, Is.EqualTo(new Vector2(104, 96)));
            Assert.That(s.EnemyWave.PreparationRemainingTicks, Is.Zero);
            Assert.That(s.EnemyWave.Reservation.Value.BrainSeed, Is.EqualTo(request.BrainSeed));
            Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(6)); Assert.That(s.Tanks.Count, Is.EqualTo(1));
            Tick(s, 8, PlayerButtons.S); Counts(s);
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1));
            Assert.That(Enemy(s).Motor.Position, Is.EqualTo(new Vector2(104, 104)));
            Assert.That(s.PlayerTank.Bounds.Overlaps(Enemy(s).Bounds), Is.False, "Half-open touching is allowed, overlap is not");
        }

        [TestCase(true)] [TestCase(false)]
        public void MapAndBaseFootprintsBlockNaturalSpawns(bool terrain)
        {
            if (terrain) level.terrain[13 * 26 + 13] = TerrainKind.Steel;
            else level.enemySpawnPixels[0] = level.baseCenterPixels;
            var s = new ArenaSession(level, tank, wave, enableBase: true);
            Tick(s, 100); Counts(s);
            Assert.That(s.EnemyWave.ReservedCount, Is.Zero); Assert.That(s.EnemyWave.SpawnedCount, Is.Zero);
        }

        [Test] public void DeathSettlesBeforeReservation_RepeatedDeadTicksDoNotLoseOrDoubleCountEnemies()
        {
            level.maxActiveEnemies = 1;
            var s = new ArenaSession(level, tank, wave);
            s.PlayerTank.IsProtected = true;
            Tick(s, 8);
            var first = Enemy(s);
            Assert.That(s.EnemyWave.IntervalRemainingTicks, Is.Zero);
            var shot = ShootEnemy(s, first);
            Tick(s); Counts(s);
            Assert.That(first.IsAlive, Is.False); Assert.That(shot.ActiveBullets, Is.Zero);
            Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1), "Death releases capacity in this same tick");
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1)); Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(5));
            Tick(s, 2); Counts(s);
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(2)); Assert.That(s.AliveEnemyCount, Is.EqualTo(1));
            Assert.That(Enemy(s).EntityId, Is.GreaterThan(first.EntityId));
            Tick(s, 50); Counts(s);
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(2)); Assert.That(first.HitPoints, Is.Zero);
        }

        [TestCase(1)] [TestCase(4)]
        public void ActivePlusReservedNeverExceedsCap_ExternalRegistrationCannotStealReservedCapacity(int cap)
        {
            level.maxActiveEnemies = cap;
            level.enemySpawnPixels = new[] { new Vector2Int(8, 200), new Vector2Int(104, 200), new Vector2Int(200, 200) };
            var s = new ArenaSession(level, tank, wave); s.PlayerTank.IsProtected = true;
            for (int i = 0; i < 1000; i++) { Tick(s); Counts(s); }
            Assert.That(s.AliveEnemyCount, Is.EqualTo(cap));
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(cap));
            Assert.Throws<InvalidOperationException>(() => s.AddTank(tank, CombatTeam.Enemy, new Vector2(72, 24)));
            var reserved = new ArenaSession(level, tank, wave); Tick(reserved);
            int before = reserved.Tanks.Count;
            if (cap == 1) Assert.Throws<InvalidOperationException>(() => reserved.AddTank(tank, CombatTeam.Enemy, new Vector2(72, 24)));
            else Assert.Throws<ArgumentException>(() => reserved.AddTank(tank, CombatTeam.Enemy, reserved.EnemyWave.Reservation.Value.PositionPixels));
            Assert.That(reserved.Tanks.Count, Is.EqualTo(before));
        }

        [Test] public void PauseFreezesPreparationIntervalBrainAndRandom_ResumeDoesNotCatchUp()
        {
            var s = new ArenaSession(level, tank, wave); Tick(s);
            s.SetPaused(true);
            Tick(s, 1000);
            Assert.That(s.Clock.Tick, Is.EqualTo(1)); Assert.That(s.EnemyWave.PreparationRemainingTicks, Is.EqualTo(2));
            Assert.That(s.EnemyWave.RandomState, Is.EqualTo(270369u));
            s.SetPaused(false); Tick(s, 2);
            var enemy = Enemy(s); var position = enemy.Motor.Position;
            s.SetPaused(true); Tick(s, 1000);
            Assert.That(s.EnemyWave.IntervalRemainingTicks, Is.EqualTo(3)); Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(s.Bullets.Active, Is.Empty);
            s.SetPaused(false); Tick(s);
            Assert.That(enemy.Motor.Position, Is.EqualTo(position + Vector2.down));
            Assert.That(s.EnemyWave.IntervalRemainingTicks, Is.EqualTo(2)); Assert.That(s.Clock.DroppedTicks, Is.Zero);
        }

        [Test] public void ExhaustionMeansAllCreatedNotVictory_DeathsAndLateTicksKeepConservation()
        {
            level.maxActiveEnemies = 1; level.enemySequence = Enumerable.Repeat(tank.definitionId, 3).ToArray();
            var s = new ArenaSession(level, tank, wave); s.PlayerTank.IsProtected = true;
            for (int index = 0; index < 3; index++)
            {
                int timeout = 0;
                while (s.EnemyWave.SpawnedCount <= index && timeout++ < 20) { Tick(s); Counts(s); }
                Assert.That(timeout, Is.LessThan(20));
                if (index < 2) { ShootEnemy(s, Enemy(s)); Tick(s); Assert.That(Enemy(s).IsAlive, Is.False); }
            }
            Assert.That(s.EnemyWave.SequenceExhausted, Is.True); Assert.That(s.AliveEnemyCount, Is.EqualTo(1));
            ShootEnemy(s, Enemy(s)); Tick(s, 50); Counts(s);
            Assert.That(s.AliveEnemyCount, Is.Zero); Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(3));
            Assert.That(s.EnemyWave.PendingCount, Is.Zero); Assert.That(s.EnemyWave.ReservedCount, Is.Zero);
        }

        [Test] public void InvalidMappingFailsAtConstruction_NotAfterAnEnemyIsPartlyRegistered()
        {
            level.enemySequence[5] = "missing";
            Assert.Throws<InvalidOperationException>(() => new ArenaSession(level, tank, wave));
            level.enemySequence[5] = tank.definitionId;
            tank.hitPoints = 0;
            Assert.Throws<InvalidOperationException>(() => new ArenaSession(level, tank, wave));
        }

        [Test] public void SameSeedAndInputReplayIdenticalIntegratedWorld_AuthoredArraysStayUnchanged()
        {
            level.enemySpawnPixels = new[] { new Vector2Int(8, 200), new Vector2Int(104, 200), new Vector2Int(200, 200) };
            var sequence = (string[])level.enemySequence.Clone(); var terrain = (TerrainKind[])level.terrain.Clone();
            var a = new ArenaSession(level, tank, wave); var b = new ArenaSession(level, tank, wave);
            for (int i = 0; i < 600; i++)
            {
                Tick(a); Tick(b); Counts(a); Counts(b);
                Assert.That(a.EnemyWave.RandomState, Is.EqualTo(b.EnemyWave.RandomState));
                Assert.That(a.EnemyWave.SpawnedCount, Is.EqualTo(b.EnemyWave.SpawnedCount));
                Assert.That(a.Tanks.Select(t => $"{t.EntityId}|{t.Motor.Position}|{t.Motor.Facing}|{t.HitPoints}|{t.Weapon.ActiveBullets}"),
                    Is.EqualTo(b.Tanks.Select(t => $"{t.EntityId}|{t.Motor.Position}|{t.Motor.Facing}|{t.HitPoints}|{t.Weapon.ActiveBullets}")));
            }
            Assert.That(level.enemySequence, Is.EqualTo(sequence)); Assert.That(level.terrain, Is.EqualTo(terrain));
        }
    }
}
