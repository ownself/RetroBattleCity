using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class EnemyBrainTests
    {
        private LevelDefinition level;
        private TankDefinition definition;
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
        private void Wall(int x, int y, TerrainKind terrain) => level.terrain[y * 26 + x] = terrain;
        private static uint Next(uint value)
        {
            value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value;
        }
        private static string Snapshot(ArenaSession s, EnemyBrain b, TankState t)
            => $"{s.Clock.Tick}|{t.Motor.Position.x:R},{t.Motor.Position.y:R}|{t.Motor.Facing}|{t.Motor.TrackFrame}|{b.State}|{b.Direction}|{b.RandomState}|{b.DirectionTicksRemaining}|{b.DecisionTicks}|{b.FireTicksRemaining}|{b.FireRandomState}|{b.FireAttempts}|{t.Weapon.CooldownRemainingTicks}|" +
                string.Join(";", s.Bullets.Active.Select(p => $"{p.OwnerId}:{p.Position.x:R},{p.Position.y:R}:{p.Direction}"));

        [Test]
        public void Registration_IsExplicitAndUsesSameSharedTank_ExternalControlRemainsAvailable()
        {
            var s = new ArenaSession(level, definition);
            var external = s.AddTank(definition, CombatTeam.Enemy, new Vector2(72, 24));
            var controlled = s.AddTank(definition, CombatTeam.Enemy, new Vector2(120, 120));
            s.SetTankCommand(external.EntityId, new TankCommand(CardinalDirection.Up));
            // This legacy registration/weapon seam test explicitly requests every tick.
            var b = s.RegisterEnemyAI(controlled.EntityId, 7, new EnemyBrainParameters(60, 180, 4, 1, 1, 1));
            Tick(s);
            Assert.That(s.Tanks.Single(t => t.EntityId == controlled.EntityId), Is.SameAs(controlled));
            Assert.That(external.Motor.Position, Is.EqualTo(new Vector2(72, 25)));
            Assert.That(external.Weapon.ActiveBullets, Is.Zero);
            Assert.That(controlled.Motor.Position, Is.EqualTo(new Vector2(120, 119)));
            Assert.That(controlled.Motor.Facing, Is.EqualTo(CardinalDirection.Down));
            Assert.That(controlled.Weapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(b.DecisionTicks, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => s.SetTankCommand(controlled.EntityId, new TankCommand(CardinalDirection.Up)));
            Assert.Throws<ArgumentException>(() => s.RegisterEnemyAI(controlled.EntityId, 8));
            Assert.Throws<ArgumentException>(() => s.RegisterEnemyAI(1, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.RegisterEnemyAI(99, 8));
            Assert.That(b.InitialSeed, Is.EqualTo(7u));
        }

        [Test]
        public void PrototypeParameters_AreExplicitValidatedAndUncalibrated()
        {
            var p = EnemyBrainParameters.Prototype;
            Assert.That(p.RequiresCalibration, Is.True);
            Assert.That(p.MinDirectionTicks, Is.EqualTo(60)); Assert.That(p.MaxDirectionTicks, Is.EqualTo(180));
            Assert.That(p.DownWeight, Is.EqualTo(4)); Assert.That(p.OtherDirectionWeight, Is.EqualTo(1));
            Assert.That(p.MinFireDelayTicks, Is.EqualTo(45)); Assert.That(p.MaxFireDelayTicks, Is.EqualTo(105));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBrainParameters(0, 10, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBrainParameters(10, 9, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBrainParameters(1, int.MaxValue, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBrainParameters(1, 10, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBrainParameters(1, 10, int.MaxValue, 1));
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(120, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.RegisterEnemyAI(t.EntityId, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.RegisterEnemyAI(t.EntityId, 1, initialDirection: CardinalDirection.None));
            Assert.That(s.RegisterEnemyAI(t.EntityId, 1).InitialSeed, Is.EqualTo(1));
        }

        [TestCase(1u)] [TestCase(7u)] [TestCase(123456u)] [TestCase(uint.MaxValue)]
        public void RandomDrawsAndDirectionWeights_HaveExactInspectableSequence(uint seed)
        {
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.RegisterEnemyAI(t.EntityId, seed, new EnemyBrainParameters(1, 7, 4, 1));
            Tick(s); // Initial minimum expires; all four directions are locally clear.
            uint directionDraw = Next(seed), durationDraw = Next(directionDraw);
            int roll = (int)(directionDraw % 7);
            var expected = roll == 0 ? CardinalDirection.Up : roll == 1 ? CardinalDirection.Left : roll < 6 ? CardinalDirection.Down : CardinalDirection.Right;
            Assert.That(b.Direction, Is.EqualTo(expected));
            Assert.That(b.DirectionTicksRemaining, Is.EqualTo(1 + durationDraw % 7));
            Assert.That(b.RandomState, Is.EqualTo(durationDraw));
            Assert.That(t.Motor.Facing, Is.EqualTo(expected));
        }

        [TestCase(CardinalDirection.Up, 104, 200)]
        [TestCase(CardinalDirection.Down, 104, 8)]
        [TestCase(CardinalDirection.Left, 8, 104)]
        [TestCase(CardinalDirection.Right, 200, 104)]
        public void Boundary_BlockTurnsAndFires_ThenRemainsInside(CardinalDirection initial, int x, int y)
        {
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(x, y));
            var b = s.RegisterEnemyAI(t.EntityId, 7, new EnemyBrainParameters(60, 180, 4, 1, 1, 1), initial);
            Tick(s);
            Assert.That(b.Direction, Is.Not.EqualTo(initial));
            Assert.That(t.Motor.IsMoving, Is.True);
            Assert.That(t.Weapon.CooldownRemainingTicks, Is.EqualTo(definition.fireCooldownTicks));
            for (int i = 0; i < 600; i++)
            {
                Tick(s);
                Assert.That(s.Map.CanOccupy(t.Motor.Position), Is.True);
                Assert.That(t.Bounds.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(t.Bounds.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(t.Bounds.xMax, Is.LessThanOrEqualTo(208)); Assert.That(t.Bounds.yMax, Is.LessThanOrEqualTo(208));
            }
        }

        [TestCase(TerrainKind.Brick, CardinalDirection.Up, 12, 14)]
        [TestCase(TerrainKind.Steel, CardinalDirection.Up, 12, 14)]
        [TestCase(TerrainKind.Brick, CardinalDirection.Down, 12, 11)]
        [TestCase(TerrainKind.Steel, CardinalDirection.Down, 12, 11)]
        [TestCase(TerrainKind.Brick, CardinalDirection.Left, 11, 12)]
        [TestCase(TerrainKind.Steel, CardinalDirection.Left, 11, 12)]
        [TestCase(TerrainKind.Brick, CardinalDirection.Right, 14, 12)]
        [TestCase(TerrainKind.Steel, CardinalDirection.Right, 14, 12)]
        public void Wall_BlockTurnsWithoutEnteringBrickOrSteel(TerrainKind terrain, CardinalDirection initial, int x, int y)
        {
            Wall(x, y, terrain);
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.RegisterEnemyAI(t.EntityId, 11, new EnemyBrainParameters(60, 180, 4, 1, 1, 1), initial);
            Tick(s);
            Assert.That(b.Direction, Is.Not.EqualTo(initial));
            Assert.That(t.Motor.IsMoving, Is.True);
            Assert.That(t.Weapon.CooldownRemainingTicks, Is.EqualTo(definition.fireCooldownTicks));
            for (int i = 0; i < 300; i++) { Tick(s); Assert.That(s.Map.CanOccupy(t.Motor.Position), Is.True); }
        }

        [TestCase(false)] [TestCase(true)]
        public void NarrowCorridor_Exact16PixelsNeverCrossesWalls_DeadEndReverses(bool horizontal)
        {
            for (int a = 0; a < 26; a++) for (int b = 0; b < 26; b++)
                if (b < 12 || b > 13) Wall(horizontal ? a : b, horizontal ? b : a, TerrainKind.Steel);
            level.playerSpawnPixels = horizontal ? new Vector2Int(24, 104) : new Vector2Int(104, 24);
            var s = new ArenaSession(level, definition);
            s.PlayerTank.IsProtected = true;
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            s.RegisterEnemyAI(t.EntityId, 71, new EnemyBrainParameters(1000, 1000, 4, 1), horizontal ? CardinalDirection.Right : CardinalDirection.Up);
            bool reversed = false;
            for (int i = 0; i < 500; i++)
            {
                Tick(s);
                Assert.That(s.Map.CanOccupy(t.Motor.Position), Is.True);
                Assert.That(horizontal ? t.Motor.Position.y : t.Motor.Position.x, Is.EqualTo(104));
                if (t.Motor.Facing == (horizontal ? CardinalDirection.Left : CardinalDirection.Down)) reversed = true;
            }
            Assert.That(reversed, Is.True);
        }

        [Test]
        public void Decisions_UseStartOfTickOccupancy_NotEarlierIdsMovementOrFuturePlayerCommand()
        {
            var s = new ArenaSession(level, definition);
            var earlier = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var later = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 88));
            // Register in reverse order: stable tank ID, not registration/dictionary order, controls evaluation.
            var b = s.RegisterEnemyAI(later.EntityId, 7, initialDirection: CardinalDirection.Up);
            s.RegisterEnemyAI(earlier.EntityId, 9, initialDirection: CardinalDirection.Up);
            Tick(s);
            Assert.That(earlier.Motor.Position, Is.EqualTo(new Vector2(104, 105)));
            Assert.That(b.Direction, Is.Not.EqualTo(CardinalDirection.Up), "Earlier ID vacates contact this tick, but decision must see old occupied box");
            Assert.That(later.Bounds.Overlaps(earlier.Bounds), Is.False);
            // Player command cannot influence a decision until its movement becomes current state next tick.
            level.playerSpawnPixels = new Vector2Int(104, 104);
            var a = new ArenaSession(level, definition); var c = new ArenaSession(level, definition);
            var ta = a.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 88));
            var tc = c.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 88));
            var ba = a.RegisterEnemyAI(ta.EntityId, 7, initialDirection: CardinalDirection.Up);
            var bc = c.RegisterEnemyAI(tc.EntityId, 7, initialDirection: CardinalDirection.Up);
            Tick(a, PlayerButtons.W); Tick(c, PlayerButtons.None);
            Assert.That(ba.Direction, Is.EqualTo(bc.Direction)); Assert.That(ba.RandomState, Is.EqualTo(bc.RandomState));
        }

        [Test]
        public void DynamicMutualBlock_TurnsAndNeverOverlapsAcrossTicks()
        {
            var s = new ArenaSession(level, definition);
            var a = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 120));
            var ba = s.RegisterEnemyAI(a.EntityId, 31, initialDirection: CardinalDirection.Up);
            var bb = s.RegisterEnemyAI(b.EntityId, 41, initialDirection: CardinalDirection.Down);
            Tick(s);
            Assert.That(ba.Direction, Is.Not.EqualTo(CardinalDirection.Up));
            Assert.That(bb.Direction, Is.Not.EqualTo(CardinalDirection.Down));
            for (int i = 0; i < 1000; i++)
            {
                Tick(s);
                Assert.That(a.Bounds.Overlaps(b.Bounds), Is.False);
                Assert.That(s.Map.CanOccupy(a.Motor.Position), Is.True); Assert.That(s.Map.CanOccupy(b.Motor.Position), Is.True);
            }
        }

        [Test]
        public void FullyBlocked_RetriesWithoutRandomChurn_AndShootsBrickOpen()
        {
            for (int y = 11; y <= 14; y++) for (int x = 11; x <= 14; x++)
                if (x == 11 || x == 14 || y == 11 || y == 14) Wall(x, y, TerrainKind.Brick);
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.RegisterEnemyAI(t.EntityId, 17, new EnemyBrainParameters(60, 180, 4, 1, 1, 1), CardinalDirection.Up);
            Tick(s);
            Assert.That(b.State, Is.EqualTo(EnemyDecisionState.Blocked));
            Assert.That(b.RandomState, Is.EqualTo(17));
            Assert.That(t.Motor.Position, Is.EqualTo(new Vector2(104, 104)));
            Assert.That(s.Map.Revision, Is.GreaterThan(0), "Stationary AI uses real shared weapon and brick damage");
            for (int i = 0; i < 90; i++) Tick(s);
            Assert.That(t.Motor.Position, Is.Not.EqualTo(new Vector2(104, 104)));
            Assert.That(s.Map.CanOccupy(t.Motor.Position), Is.True);
        }

        [Test]
        public void Death_StopsDecisionsRandomTimersMovementAndNewShots()
        {
            definition.bulletSpeedPixelsPerSecond = 6000;
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(24, 64));
            var b = s.RegisterEnemyAI(t.EntityId, 29, initialDirection: CardinalDirection.Right);
            Tick(s, PlayerButtons.Fire);
            Assert.That(t.IsAlive, Is.False);
            var position = t.Motor.Position; uint random = b.RandomState; long decisions = b.DecisionTicks;
            int timer = b.DirectionTicksRemaining, cooldown = t.Weapon.CooldownRemainingTicks;
            for (int i = 0; i < 120; i++) Tick(s);
            Assert.That(b.State, Is.EqualTo(EnemyDecisionState.Dead)); Assert.That(b.DecisionTicks, Is.EqualTo(decisions));
            Assert.That(b.RandomState, Is.EqualTo(random)); Assert.That(b.DirectionTicksRemaining, Is.EqualTo(timer));
            Assert.That(t.Motor.Position, Is.EqualTo(position)); Assert.That(t.Weapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
            Assert.That(t.Weapon.ActiveBullets, Is.Zero);
            Assert.Throws<ArgumentException>(() => s.RegisterEnemyAI(t.EntityId, 3));
        }

        [TestCase(1u)] [TestCase(7u)] [TestCase(123456u)] [TestCase(0x9E3779B9u)]
        public void Fire_DefaultRandomInitialAndLaterIntervalsAreBounded_ReproducibleAndNotHeld(uint seed)
        {
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.RegisterEnemyAI(t.EntityId, seed);
            int first = b.FireTicksRemaining; Assert.That(first, Is.InRange(45, 105));
            long previousAttempt = 0, previousTick = 0;
            var intervals = new HashSet<long>();
            for (int i = 1; i <= 1200; i++)
            {
                s.Bullets.Clear(); Tick(s);
                if (i < first) Assert.That(b.FireAttempts, Is.Zero);
                if (b.FireAttempts == previousAttempt) continue;
                Assert.That(b.FireAttempts, Is.EqualTo(previousAttempt + 1));
                long interval = i - previousTick; Assert.That(interval, Is.InRange(45, 105));
                intervals.Add(interval); previousTick = i; previousAttempt = b.FireAttempts;
                Assert.That(t.Weapon.CooldownRemainingTicks, Is.EqualTo(definition.fireCooldownTicks));
            }
            Assert.That(previousAttempt, Is.InRange(11, 27)); Assert.That(intervals.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Fire_ReleasingProjectileCapacityDoesNotImmediatelyShootOrQueueRequests()
        {
            definition.fireCooldownTicks = 1;
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.RegisterEnemyAI(t.EntityId, 7, new EnemyBrainParameters(60, 180, 4, 1, 3, 3));
            Tick(s); Tick(s); Assert.That(t.Weapon.ActiveBullets, Is.Zero); Assert.That(b.FireAttempts, Is.Zero);
            Tick(s); Assert.That(t.Weapon.ActiveBullets, Is.EqualTo(1)); Assert.That(b.FireAttempts, Is.EqualTo(1));
            // Keep the cap full through the next request; that request is discarded, not saved for later.
            Tick(s); Tick(s); Tick(s); Assert.That(b.FireAttempts, Is.EqualTo(2)); Assert.That(t.Weapon.ActiveBullets, Is.EqualTo(1));
            s.Bullets.Clear(); Tick(s); Tick(s);
            Assert.That(t.Weapon.ActiveBullets, Is.Zero); Assert.That(b.FireAttempts, Is.EqualTo(2));
            Tick(s); Assert.That(t.Weapon.ActiveBullets, Is.EqualTo(1)); Assert.That(b.FireAttempts, Is.EqualTo(3));
        }

        [Test]
        public void Fire_FullyBlockedStillWaitsAndShoots_WithoutChangingMovementRandom()
        {
            for (int y = 11; y <= 14; y++) for (int x = 11; x <= 14; x++)
                if (x == 11 || x == 14 || y == 11 || y == 14) Wall(x, y, TerrainKind.Steel);
            var s = new ArenaSession(level, definition); var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.RegisterEnemyAI(t.EntityId, 17, new EnemyBrainParameters(60, 180, 4, 1, 3, 3));
            Tick(s); Tick(s); Assert.That(b.FireAttempts, Is.Zero); Assert.That(t.Weapon.CooldownRemainingTicks, Is.Zero);
            Tick(s); Assert.That(b.FireAttempts, Is.EqualTo(1)); Assert.That(t.Weapon.CooldownRemainingTicks, Is.EqualTo(15));
            Assert.That(b.State, Is.EqualTo(EnemyDecisionState.Blocked)); Assert.That(b.RandomState, Is.EqualTo(17));
        }

        [Test]
        public void Fire_TuningDoesNotPerturbDirectionRandomOrMovement()
        {
            var a = new ArenaSession(level, definition); var c = new ArenaSession(level, definition);
            a.PlayerTank.IsProtected = c.PlayerTank.IsProtected = true;
            var ta = a.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var tc = c.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var ba = a.RegisterEnemyAI(ta.EntityId, 73, new EnemyBrainParameters(2, 8, 4, 1, 1, 1));
            var bc = c.RegisterEnemyAI(tc.EntityId, 73, new EnemyBrainParameters(2, 8, 4, 1));
            for (int i = 0; i < 1000; i++)
            {
                Tick(a); Tick(c);
                Assert.That(ba.Direction, Is.EqualTo(bc.Direction)); Assert.That(ba.RandomState, Is.EqualTo(bc.RandomState));
                Assert.That(ta.Motor.Position, Is.EqualTo(tc.Motor.Position));
            }
            Assert.That(ba.FireAttempts, Is.GreaterThan(bc.FireAttempts));
        }

        [TestCase(0, 10)] [TestCase(10, 9)] [TestCase(1, int.MaxValue)]
        public void Fire_InvalidDelayRangeRejected(int min, int max)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyBrainParameters(60, 180, 4, 1, min, max));

        [Test]
        public void Fire_ClockAndPauseFreezeIndependentCountdownAndRandom_NoBacklogOnResume()
        {
            var wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            var rules = ScriptableObject.CreateInstance<PickupDefinition>();
            try
            {
                wave.tankDefinitions = new[] { definition }; rules.carrierSequenceIndices = new int[0]; rules.clockDurationTicks = 3;
                var s = new ArenaSession(level, definition, wave, pickups: rules);
                var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
                var b = s.RegisterEnemyAI(t.EntityId, 7, new EnemyBrainParameters(60, 180, 4, 1, 6, 6));
                ClassicRulesCombinationTests.Drop(s, PickupKind.Clock); Tick(s);
                Assert.That(b.FireTicksRemaining, Is.EqualTo(5)); uint random = b.FireRandomState;
                s.SetPaused(true); for (int i = 0; i < 5; i++) Tick(s);
                s.SetPaused(false); for (int i = 0; i < 3; i++) Tick(s);
                Assert.That(b.FireTicksRemaining, Is.EqualTo(5)); Assert.That(b.FireRandomState, Is.EqualTo(random));
                Assert.That(b.FireAttempts, Is.Zero); Tick(s); Assert.That(b.FireTicksRemaining, Is.EqualTo(4));
            }
            finally { UnityEngine.Object.DestroyImmediate(wave); UnityEngine.Object.DestroyImmediate(rules); }
        }

        [Test]
        public void PauseFreezesFullBrainAndResumeDoesNotCatchUp()
        {
            var s = new ArenaSession(level, definition);
            var t = s.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var b = s.RegisterEnemyAI(t.EntityId, 31, new EnemyBrainParameters(2, 8, 4, 1));
            for (int i = 0; i < 5; i++) Tick(s);
            s.SetPaused(true); string paused = Snapshot(s, b, t);
            for (int i = 0; i < 500; i++) s.AdvanceFrame(100, PlayerButtons.None);
            Assert.That(Snapshot(s, b, t), Is.EqualTo(paused));
            s.SetPaused(false);
            Assert.That(s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None), Is.EqualTo(1));
            Assert.That(b.DecisionTicks, Is.EqualTo(6)); Assert.That(s.Clock.DroppedTicks, Is.Zero);
        }

        [Test]
        public void SameSeed_ReplaysEveryTickIncludingPositionFacingShotsAndRandom_WithoutUnityRandom()
        {
            var a = new ArenaSession(level, definition); var c = new ArenaSession(level, definition);
            var ta = a.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var tc = c.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var ba = a.RegisterEnemyAI(ta.EntityId, 73); var bc = c.RegisterEnemyAI(tc.EntityId, 73);
            var unityState = UnityEngine.Random.state;
            for (int i = 0; i < 2000; i++)
            {
                Tick(a); Tick(c);
                Assert.That(Snapshot(a, ba, ta), Is.EqualTo(Snapshot(c, bc, tc)), $"tick {i}");
            }
            Assert.That(UnityEngine.Random.state, Is.EqualTo(unityState));
            Assert.That(ba.RandomState, Is.Not.EqualTo(73));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void RenderDelta_Same720TicksMatches60HzReference(int fps)
        {
            var reference = new ArenaSession(level, definition); var rendered = new ArenaSession(level, definition);
            var tr = reference.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var tf = rendered.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            var br = reference.RegisterEnemyAI(tr.EntityId, 97); var bf = rendered.RegisterEnemyAI(tf.EntityId, 97);
            for (int i = 0; i < 720; i++) Tick(reference);
            for (int frame = 0; frame < fps * 12; frame++) rendered.AdvanceFrame(1.0 / fps, PlayerButtons.None);
            Assert.That(rendered.Clock.Tick, Is.EqualTo(720));
            Assert.That(Snapshot(rendered, bf, tf), Is.EqualTo(Snapshot(reference, br, tr)));
            Assert.That(rendered.Clock.DroppedTicks, Is.Zero);
        }
    }
}
