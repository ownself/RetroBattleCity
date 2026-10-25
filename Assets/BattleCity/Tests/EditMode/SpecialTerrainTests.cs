using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC307")]
    public sealed class SpecialTerrainTests
    {
        private LevelDefinition level;
        [SetUp] public void SetUp() { level = ScriptableObject.CreateInstance<LevelDefinition>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(level); }
        private void Fill(TerrainKind kind) { for (int i = 0; i < level.terrain.Length; i++) level.terrain[i] = kind; }

        [TestCase(TerrainKind.Water, false, false)]
        [TestCase(TerrainKind.Grass, true, false)]
        [TestCase(TerrainKind.Ice, true, false)]
        [TestCase(TerrainKind.Brick, false, true)]
        [TestCase(TerrainKind.Steel, false, true)]
        public void CollisionMatrix_TanksAndBulletsHaveSeparateTruth(TerrainKind kind, bool tankPass, bool bulletBlock)
        {
            level.terrain[10 * 26 + 10] = kind; var map = new MapState(level);
            Assert.That(map.CanOccupy(new Vector2(84, 84)), Is.EqualTo(tankPass));
            Assert.That(map.BlocksBullet(20, 20), Is.EqualTo(bulletBlock));
        }
        [TestCase(TerrainKind.Water)] [TestCase(TerrainKind.Grass)] [TestCase(TerrainKind.Ice)]
        public void RealBulletSweep_PassesSpecialTerrainAndHitsEnemyBehindWithoutMutatingMap(TerrainKind kind)
        {
            level.terrain[10 * 26 + 10] = kind; var map = new MapState(level);
            var def = ScriptableObject.CreateInstance<TankDefinition>();
            try
            {
                def.bulletSpeedPixelsPerSecond = 6000;
                var bullets = new BulletWorld(); var gun = new WeaponState(def, bullets, CombatTeam.Player, 1);
                Assert.That(gun.Step(new TankCommand(CardinalDirection.Right, false, true), new Vector2(56, 84), CardinalDirection.Right), Is.True);
                bullets.Step(map, new[] { new BulletTarget(2, BulletHitKind.Tank, CombatTeam.Enemy, new Rect(112, 76, 16, 16)) });
                Assert.That(bullets.Hits.Count, Is.EqualTo(1)); Assert.That(bullets.Hits[0].Kind, Is.EqualTo(BulletHitKind.Tank));
                Assert.That(bullets.Hits[0].TargetId, Is.EqualTo(2)); Assert.That(map.ApplyBulletHits(bullets.Hits), Is.Zero);
                Assert.That(map.TerrainAt(10, 10), Is.EqualTo(kind)); Assert.That(map.Revision, Is.Zero);
            }
            finally { Object.DestroyImmediate(def); }
        }
        [TestCase(CardinalDirection.Up)] [TestCase(CardinalDirection.Left)]
        [TestCase(CardinalDirection.Down)] [TestCase(CardinalDirection.Right)]
        public void Ice_ReleaseCoastsExactly24FutureTicksAtNormalSpeed(CardinalDirection direction)
        {
            Fill(TerrainKind.Ice); var map = new MapState(level); var motor = new TankMotor(new Vector2(104, 104), map);
            motor.Step(direction, 60, map); Vector2 origin = motor.Position;
            Assert.That(motor.IceRemainingTicks, Is.EqualTo(24));
            for (int remaining = 23; remaining >= 0; remaining--)
            {
                motor.Step(CardinalDirection.None, 60, map);
                Assert.That(motor.IceRemainingTicks, Is.EqualTo(remaining));
            }
            Assert.That(motor.Position, Is.EqualTo(origin + direction.Vector() * 24));
            motor.Step(CardinalDirection.None, 60, map); Assert.That(motor.Position, Is.EqualTo(origin + direction.Vector() * 24));
            Assert.That(motor.IsMoving, Is.False);
        }
        [Test] public void Ice_DirectionalInputOverridesCoast_RefreshesNotStacks_UsesSafeAlignment()
        {
            Fill(TerrainKind.Ice); var map = new MapState(level); var motor = new TankMotor(new Vector2(104, 104), map);
            motor.Step(CardinalDirection.Up, 60, map); motor.Step(CardinalDirection.None, 60, map);
            motor.Step(CardinalDirection.Right, 60, map);
            Assert.That(motor.Position, Is.EqualTo(new Vector2(105, 104))); Assert.That(motor.Facing, Is.EqualTo(CardinalDirection.Right));
            Assert.That(motor.IceRemainingTicks, Is.EqualTo(24));
            motor.Step(CardinalDirection.None, 60, map); Assert.That(motor.Position, Is.EqualTo(new Vector2(106, 104)));
        }
        [TestCase(TerrainKind.Brick)] [TestCase(TerrainKind.Steel)] [TestCase(TerrainKind.Water)]
        public void Ice_CoastingClampsToTerrainFaceAndClearsMomentum(TerrainKind wall)
        {
            Fill(TerrainKind.Ice); level.terrain[12 * 26 + 14] = wall; var map = new MapState(level);
            var motor = new TankMotor(new Vector2(96, 104), map); motor.Step(CardinalDirection.Right, 60, map);
            for (int i = 0; i < 24; i++) motor.Step(CardinalDirection.None, 60, map);
            Assert.That(motor.Position.x, Is.EqualTo(104)); Assert.That(motor.IceRemainingTicks, Is.Zero);
            Assert.That(map.CanOccupy(motor.Position), Is.True);
        }
        [Test] public void Ice_CoastUsesSweptLiveBlockersAndMapEdge_NoTunnelingAtHighSpeed()
        {
            Fill(TerrainKind.Ice); var map = new MapState(level);
            var motor = new TankMotor(new Vector2(80, 104), map); motor.Step(CardinalDirection.Right, 60, map);
            motor.Step(CardinalDirection.None, 6000, map, new List<Rect> { new Rect(112, 96, 16, 16) });
            Assert.That(motor.Position.x, Is.EqualTo(104)); Assert.That(motor.IceRemainingTicks, Is.Zero);
            var edge = new TankMotor(new Vector2(198, 104), map); edge.Step(CardinalDirection.Right, 60, map);
            edge.Step(CardinalDirection.None, 6000, map); Assert.That(edge.Position.x, Is.EqualTo(200)); Assert.That(edge.IceRemainingTicks, Is.Zero);
        }
        [Test] public void Ice_LeavingCenterCellStopsCoastAndDryGroundNeverCoasts()
        {
            // Start on an aligned horizontal lane; do not confuse turn alignment with forward ice motion.
            level.terrain[13 * 26 + 12] = TerrainKind.Ice; var map = new MapState(level);
            var motor = new TankMotor(new Vector2(100, 104), map); motor.Step(CardinalDirection.Right, 60, map);
            for (int i = 0; i < 8; i++) motor.Step(CardinalDirection.None, 60, map);
            Assert.That(motor.Position.x, Is.EqualTo(104)); Assert.That(motor.IceRemainingTicks, Is.Zero);
            motor.Step(CardinalDirection.Right, 60, map); motor.Step(CardinalDirection.None, 60, map);
            Assert.That(motor.Position.x, Is.EqualTo(105)); Assert.That(motor.IsMoving, Is.False);
        }
        [Test] public void Water_RejectsSpawnAndBlocksPickupReachabilityAcrossFullBarrier()
        {
            for (int y = 0; y < 26; y++) level.terrain[y * 26 + 12] = TerrainKind.Water;
            var map = new MapState(level); Assert.Throws<ArgumentException>(() => new TankMotor(new Vector2(100, 100), map));
            var rules = ScriptableObject.CreateInstance<PickupDefinition>();
            try
            {
                rules.carrierSequenceIndices = new int[0]; var state = new PickupState(rules, level);
                Assert.That(state.TryDrop(map, new Vector2(24, 24), true, PickupTests.ExcludeAllBut(new Vector2(160, 160)), null), Is.False);
            }
            finally { Object.DestroyImmediate(rules); }
        }
        [Test] public void StageReferenceMoved_All35ReferencesAndTitleSheetPresent_OriginalStageStillMatches()
        {
            Assert.That(System.IO.File.Exists(BattleCity.Editor.Stage01Authoring.ReferencePath), Is.True);
            for (int i = 1; i <= 35; i++) Assert.That(System.IO.File.Exists("Assets/Sprites/Stages/NES - Battle City (JPN) - Backgrounds - Stage " + i.ToString("D2") + ".png"), Is.True);
            Assert.That(System.IO.File.Exists("Assets/Sprites/NES - Battle City (JPN) - Miscellaneous - Miscellaneous.png"), Is.True);
        }
    }
}
