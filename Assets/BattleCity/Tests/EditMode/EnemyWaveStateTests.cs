using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class EnemyWaveStateTests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        private EnemyWaveDefinition configuration;
        private static bool Clear(Vector2Int _) => false;
        private static bool Create(EnemySpawnRequest _) => true;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            tank = ScriptableObject.CreateInstance<TankDefinition>();
            tank.definitionId = "enemy.basic";
            configuration = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            configuration.tankDefinitions = new[] { tank };
            level.enemySequence = Enumerable.Repeat(tank.definitionId, 20).ToArray();
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(configuration);
            UnityEngine.Object.DestroyImmediate(tank);
            UnityEngine.Object.DestroyImmediate(level);
        }
        private EnemyWaveState Fast(int cap = 4)
        {
            level.maxActiveEnemies = cap;
            configuration.preparationTicks = configuration.spawnIntervalTicks = 1;
            return new EnemyWaveState(level, configuration);
        }
        private static void Invariants(EnemyWaveState w)
        {
            Assert.That(w.TotalCount, Is.EqualTo(w.SpawnedCount + w.PendingCount));
            Assert.That(w.PendingCount, Is.EqualTo(w.UnreservedCount + w.ReservedCount));
            Assert.That(w.ReservedCount, Is.InRange(0, 1));
            Assert.That(w.AliveCount + w.ReservedCount, Is.LessThanOrEqualTo(w.MaxActiveEnemies));
            Assert.That(w.UnreservedCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(w.PreparationRemainingTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(w.IntervalRemainingTicks, Is.GreaterThanOrEqualTo(0));
        }
        private static string Snapshot(EnemyWaveState w) =>
            $"{w.TotalCount}|{w.SpawnedCount}|{w.PendingCount}|{w.ReservedCount}|{w.AliveCount}|{w.RandomState}|" +
            $"{w.PreparationRemainingTicks}|{w.IntervalRemainingTicks}|{w.Reservation?.SequenceIndex}|" +
            $"{w.Reservation?.PositionPixels}|{w.Reservation?.BrainSeed}";

        [Test]
        public void DefaultTiming_FirstCreationCall61_Second241_DeathBeforeStepAndNextTickControlAreHostContract()
        {
            var wave = new EnemyWaveState(level, configuration);
            var creationCalls = new List<int>();
            for (int call = 1; call <= 241; call++)
            {
                int currentCall = call;
                wave.Step(0, Clear, r => { creationCalls.Add(currentCall); return true; });
                if (call == 1) { Assert.That(wave.ReservedCount, Is.EqualTo(1)); Assert.That(wave.PreparationRemainingTicks, Is.EqualTo(60)); }
                if (call == 60) Assert.That(wave.PreparationRemainingTicks, Is.EqualTo(1));
                if (call == 61) Assert.That(wave.IntervalRemainingTicks, Is.EqualTo(120));
                if (call == 180) { Assert.That(wave.IntervalRemainingTicks, Is.EqualTo(1)); Assert.That(wave.ReservedCount, Is.Zero); }
                if (call == 181) { Assert.That(wave.ReservedCount, Is.EqualTo(1)); Assert.That(wave.PreparationRemainingTicks, Is.EqualTo(60)); }
                Invariants(wave);
            }
            CollectionAssert.AreEqual(new[] { 61, 241 }, creationCalls);
            Assert.That(configuration.requiresCalibration, Is.True);
        }

        [TestCase(1)] [TestCase(4)]
        public void Capacity_IncludesReservation_DeathSnapshotReleasesSlotWithoutNotifications(int cap)
        {
            var wave = Fast(cap);
            int alive = 0;
            for (int i = 0; i < cap * 2; i++)
            {
                if (wave.Step(alive, Clear, Create)) alive++;
                Invariants(wave);
            }
            Assert.That(wave.SpawnedCount, Is.EqualTo(cap));
            for (int i = 0; i < 10; i++) Assert.That(wave.Step(alive, Clear, Create), Is.False);
            Assert.That(wave.ReservedCount, Is.Zero);
            alive--; // Host already settled a death. Repeated identical dead snapshots are not extra deaths.
            Assert.That(wave.Step(alive, Clear, Create), Is.False);
            Assert.That(wave.AliveCount + wave.ReservedCount, Is.EqualTo(cap));
            Assert.That(wave.Step(alive, Clear, Create), Is.True);
            alive++;
            for (int i = 0; i < 10; i++) wave.Step(alive, Clear, Create);
            Assert.That(wave.SpawnedCount, Is.EqualTo(cap + 1));
            Assert.That(wave.PendingCount, Is.EqualTo(20 - cap - 1));
            Invariants(wave);
        }

        [Test]
        public void AllPointsBlocked_WaitsWithoutConsumingRandomSequenceOrReservation_ThenUsesOnlyClearPoint()
        {
            var wave = Fast();
            uint initial = wave.RandomState;
            for (int i = 0; i < 10; i++)
            {
                Assert.That(wave.Step(0, p => true, r => { Assert.Fail("Blocked spawn created"); return true; }), Is.False);
                Invariants(wave);
            }
            Assert.That(wave.RandomState, Is.EqualTo(initial));
            Assert.That(wave.PendingCount, Is.EqualTo(20)); Assert.That(wave.ReservedCount, Is.Zero);
            var free = level.enemySpawnPixels[1];
            wave.Step(0, p => p != free, Create);
            Assert.That(wave.Reservation.Value.PositionPixels, Is.EqualTo(free));
            Assert.That(wave.Step(0, p => p != free, Create), Is.True);
        }

        [Test]
        public void PreparationFinishedThenOccupied_RetriesSameReservationWithoutResetOrLostCountOrSeed()
        {
            var wave = Fast();
            wave.Step(0, Clear, Create);
            var reserved = wave.Reservation.Value;
            uint random = wave.RandomState;
            for (int i = 0; i < 10; i++)
            {
                wave.Step(0, p => p == reserved.PositionPixels, r => { Assert.Fail("Occupied materialization"); return true; });
                Assert.That(wave.PreparationRemainingTicks, Is.Zero);
                Assert.That(wave.Reservation.Value, Is.EqualTo(reserved));
                Assert.That(wave.RandomState, Is.EqualTo(random));
                Assert.That(wave.SpawnedCount, Is.Zero); Assert.That(wave.PendingCount, Is.EqualTo(20));
                Invariants(wave);
            }
            Assert.That(wave.Step(0, Clear, r => { Assert.That(r, Is.EqualTo(reserved)); return true; }), Is.True);
            Assert.That(wave.PendingCount, Is.EqualTo(19)); Assert.That(wave.ReservedCount, Is.Zero);
        }

        [Test]
        public void FailedCreation_IsNotCommitted_AndCanRetryWithIdenticalRequest()
        {
            var wave = Fast(); wave.Step(0, Clear, Create);
            var request = wave.Reservation.Value;
            for (int i = 0; i < 10; i++)
            {
                Assert.That(wave.Step(0, Clear, r => false), Is.False);
                Assert.That(wave.Reservation.Value, Is.EqualTo(request));
                Assert.That(wave.SpawnedCount, Is.Zero); Assert.That(wave.IntervalRemainingTicks, Is.Zero);
                Invariants(wave);
            }
            wave.Step(0, Clear, r =>
            {
                Assert.That(wave.SpawnedCount, Is.Zero); Assert.That(wave.PendingCount, Is.EqualTo(20));
                Assert.That(wave.ReservedCount, Is.EqualTo(1));
                Assert.That(r.Definition, Is.SameAs(tank)); Assert.That(r.DefinitionId, Is.EqualTo("enemy.basic"));
                return true;
            });
            Assert.That(wave.SpawnedCount, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantOrThrowingCallback_CannotDoubleCommit_ReservationIsRetryable()
        {
            var wave = Fast(); wave.Step(0, Clear, Create);
            Assert.Throws<InvalidOperationException>(() => wave.Step(0, Clear, r => wave.Step(0, Clear, Create)));
            Assert.That(wave.SpawnedCount, Is.Zero); Assert.That(wave.ReservedCount, Is.EqualTo(1));
            Assert.That(wave.Step(0, Clear, Create), Is.True);
            Invariants(wave);
        }

        [TestCase(1u)] [TestCase(7u)] [TestCase(uint.MaxValue)]
        public void FixedSeed_ReplaysPointsAndBrainSeeds_IndependentOfGlobalRandomAndBlockedRetries(uint seed)
        {
            configuration.seed = seed;
            var a = Fast(); var b = Fast();
            var expected = new List<EnemySpawnRequest>(); var actual = new List<EnemySpawnRequest>();
            var global = UnityEngine.Random.state;
            try
            {
                for (int i = 0; i < 40; i++) a.Step(0, Clear, r => { expected.Add(r); return true; });
                for (int i = 0; i < 20; i++)
                {
                    UnityEngine.Random.InitState(i + 37);
                    for (int j = 0; j < 3; j++) b.Step(0, p => true, Create);
                    b.Step(0, Clear, Create); // reservation
                    b.Step(0, Clear, r => false); // callback refusal must not draw again
                    b.Step(0, Clear, r => { actual.Add(r); return true; });
                }
                CollectionAssert.AreEqual(expected, actual);
                Assert.That(actual.Select(r => r.PositionPixels).Distinct().Count(), Is.GreaterThan(1));
                Assert.That(actual.All(r => r.BrainSeed != 0), Is.True);
                Assert.That(actual.Select(r => r.BrainSeed).Distinct().Count(), Is.EqualTo(20));
            }
            finally { UnityEngine.Random.state = global; }
        }

        [Test]
        public void ExactSeedOne_FirstPointAndBrainSeedAreStable()
        {
            var wave = Fast(); wave.Step(0, Clear, Create);
            Assert.That(wave.RandomState, Is.EqualTo(270369u));
            Assert.That(wave.Reservation.Value.PositionPixels, Is.EqualTo(level.enemySpawnPixels[0]));
            Assert.That(wave.Reservation.Value.BrainSeed, Is.EqualTo(2527132011u));
        }

        [Test]
        public void SequenceExhausted_IsNotVictory_AliveSnapshotAndConservationRemainAvailable()
        {
            level.enemySequence = new[] { tank.definitionId };
            var wave = Fast(1);
            wave.Step(0, Clear, Create); wave.Step(0, Clear, Create);
            Assert.That(wave.SequenceExhausted, Is.True); Assert.That(wave.AliveCount, Is.EqualTo(1));
            Assert.That(wave.ReservedCount, Is.Zero); Assert.That(wave.PendingCount, Is.Zero);
            for (int i = 0; i < 5; i++) wave.Step(1, Clear, r => { Assert.Fail("Exhausted sequence created"); return true; });
            wave.Step(0, Clear, Create); wave.Step(0, Clear, Create); // repeated same death snapshot
            Assert.That(wave.AliveCount, Is.Zero); Assert.That(wave.SpawnedCount, Is.EqualTo(1));
            Invariants(wave);
        }

        [Test]
        public void EmptySequence_IsExhaustedWithoutReservationOrCallbacks()
        {
            level.enemySequence = Array.Empty<string>(); configuration.tankDefinitions = Array.Empty<TankDefinition>();
            var wave = new EnemyWaveState(level, configuration);
            for (int i = 0; i < 10; i++) wave.Step(0, p => throw new Exception(), r => throw new Exception());
            Assert.That(wave.SequenceExhausted, Is.True); Assert.That(wave.RandomState, Is.EqualTo(1));
            Invariants(wave);
        }

        [Test]
        public void AuthorArraysAndTiming_AreSnapshotted_AndRuntimeNeverMutatesThem()
        {
            var sequence = (string[])level.enemySequence.Clone();
            var points = (Vector2Int[])level.enemySpawnPixels.Clone();
            var mapping = (TankDefinition[])configuration.tankDefinitions.Clone();
            var wave = Fast();
            for (int i = 0; i < 4; i++) wave.Step(0, Clear, Create);
            CollectionAssert.AreEqual(sequence, level.enemySequence); CollectionAssert.AreEqual(points, level.enemySpawnPixels);
            CollectionAssert.AreEqual(mapping, configuration.tankDefinitions);
            level.enemySequence[2] = "changed"; level.enemySpawnPixels[0] = new Vector2Int(40, 40);
            configuration.tankDefinitions[0] = null; configuration.preparationTicks = 90; configuration.spawnIntervalTicks = 99;
            configuration.seed = 8; level.maxActiveEnemies = 1;
            Assert.That(wave.PreparationTicks, Is.EqualTo(1)); Assert.That(wave.SpawnIntervalTicks, Is.EqualTo(1));
            Assert.That(wave.InitialSeed, Is.EqualTo(1)); Assert.That(wave.MaxActiveEnemies, Is.EqualTo(4));
            wave.Step(0, Clear, Create);
            Assert.That(wave.Reservation.Value.DefinitionId, Is.EqualTo(sequence[2]));
            Assert.That(points, Does.Contain(wave.Reservation.Value.PositionPixels));
        }

        [Test]
        public void NoStep_PauseFreezesReservationIntervalCountersAndRandom()
        {
            var wave = Fast(); wave.Step(0, Clear, Create);
            string preparing = Snapshot(wave);
            for (int i = 0; i < 100; i++) Assert.That(Snapshot(wave), Is.EqualTo(preparing));
            wave.Step(0, Clear, Create);
            string interval = Snapshot(wave);
            for (int i = 0; i < 100; i++) Assert.That(Snapshot(wave), Is.EqualTo(interval));
        }

        [TestCase("unknown")] [TestCase("duplicate")] [TestCase("nullMapping")] [TestCase("nullDefinition")]
        [TestCase("zeroSeed")] [TestCase("zeroPreparation")] [TestCase("negativePreparation")]
        [TestCase("zeroInterval")] [TestCase("negativeInterval")] [TestCase("invalidTank")]
        [TestCase("nullSequence")] [TestCase("emptyId")] [TestCase("zeroCap")] [TestCase("noSpawns")]
        public void InvalidConfiguration_FailsEarlyWithClearMessage(string invalid)
        {
            switch (invalid)
            {
                case "unknown": level.enemySequence[0] = "missing"; break;
                case "duplicate": configuration.tankDefinitions = new[] { tank, tank }; break;
                case "nullMapping": configuration.tankDefinitions = null; break;
                case "nullDefinition": configuration.tankDefinitions[0] = null; break;
                case "zeroSeed": configuration.seed = 0; break;
                case "zeroPreparation": configuration.preparationTicks = 0; break;
                case "negativePreparation": configuration.preparationTicks = -1; break;
                case "zeroInterval": configuration.spawnIntervalTicks = 0; break;
                case "negativeInterval": configuration.spawnIntervalTicks = -1; break;
                case "invalidTank": tank.hitPoints = 0; break;
                case "nullSequence": level.enemySequence = null; break;
                case "emptyId": level.enemySequence[0] = " "; break;
                case "zeroCap": level.maxActiveEnemies = 0; break;
                case "noSpawns": level.enemySpawnPixels = Array.Empty<Vector2Int>(); break;
            }
            var error = Assert.Throws<InvalidOperationException>(() => new EnemyWaveState(level, configuration));
            Assert.That(error.Message, Is.Not.Empty);
            if (invalid == "unknown") Assert.That(error.Message, Does.Contain("missing"));
            if (invalid == "duplicate") Assert.That(error.Message, Does.Contain("Duplicate"));
        }

        [Test]
        public void InvalidStepInput_RejectsCapacityIncludingExistingReservationWithoutMutation()
        {
            var wave = Fast(1); wave.Step(0, Clear, Create);
            string before = Snapshot(wave);
            Assert.Throws<ArgumentOutOfRangeException>(() => wave.Step(-1, Clear, Create));
            Assert.Throws<ArgumentOutOfRangeException>(() => wave.Step(1, Clear, Create));
            Assert.Throws<ArgumentNullException>(() => wave.Step(0, null, Create));
            Assert.Throws<ArgumentNullException>(() => wave.Step(0, Clear, null));
            Assert.That(Snapshot(wave), Is.EqualTo(before));
        }
    }
}
