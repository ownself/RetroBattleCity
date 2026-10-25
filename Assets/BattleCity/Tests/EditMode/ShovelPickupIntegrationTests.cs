using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC306")]
    public sealed class ShovelPickupIntegrationTests
    {
        private LevelDefinition level;
        private TankDefinition player, enemy;
        private EnemyWaveDefinition wave;
        private PickupDefinition rules;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24); level.baseCenterPixels = new Vector2Int(104, 24);
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            player = ScriptableObject.CreateInstance<TankDefinition>();
            enemy = ScriptableObject.CreateInstance<TankDefinition>(); enemy.definitionId = "enemy.basic";
            level.enemySequence = new[] { enemy.definitionId, enemy.definitionId };
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { enemy };
            wave.preparationTicks = 3000; wave.spawnIntervalTicks = 3000;
            rules = ScriptableObject.CreateInstance<PickupDefinition>(); rules.carrierSequenceIndices = new int[0]; rules.shovelDurationTicks = 3;
        }
        [TearDown] public void TearDown()
        { foreach (var o in new Object[] { level, player, enemy, wave, rules }) Object.DestroyImmediate(o); }
        private ArenaSession Session(bool managed = false, bool withBase = true, int lives = 3)
            => new ArenaSession(level, player, wave, withBase, new PlayerLifeParameters(lives, 2, 4), managed, pickups: rules);
        private static void Tick(ArenaSession s, int count = 1)
        { for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); }
        private static void Start(ArenaSession s) { s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); }
        private static void Drop(ArenaSession s, PickupKind kind)
        {
            for (int i = 0; i < 128; i++)
            {
                Assert.That(s.Pickups.TryDrop(s.Map, s.Player.Position, true, PickupTests.ExcludeAllBut(s.Player.Position), s.Base?.Bounds), Is.True);
                if (s.Pickups.Active.Value.Kind == kind) return;
            }
            Assert.Fail("Bounded deterministic drop search failed");
        }
        private static void Collect(ArenaSession s, PickupKind kind = PickupKind.Shovel) { Drop(s, kind); Tick(s); }
        private void Shoot(ArenaSession s, Vector2 target, CombatTeam team)
        {
            Assert.That(new WeaponState(player, s.Bullets, team, 999).Step(
                new TankCommand(CardinalDirection.Up, false, true), target - Vector2.up * 18, CardinalDirection.Up), Is.True);
        }
        [Test] public void Shovel_Default1200FutureCompleteTicks_SnapshotsConfigurationAndExactDamagedLayout()
        {
            rules.shovelDurationTicks = 1200; var s = Session();
            s.Map.SetTerrainCell(12, 4, TerrainKind.Brick, 5); Collect(s); rules.shovelDurationTicks = 1;
            Assert.That(s.BaseFortification.DurationTicks, Is.EqualTo(1200)); Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(1200));
            for (int remaining = 1199; remaining >= 0; remaining--)
            {
                Assert.That(s.Map.TerrainAt(12, 4), Is.EqualTo(TerrainKind.Steel)); Tick(s);
                Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(remaining));
            }
            Assert.That(s.Map.TerrainAt(12, 4), Is.EqualTo(TerrainKind.Brick)); Assert.That(s.Map.BrickMaskAt(12, 4), Is.EqualTo(5));
            Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Empty)); Tick(s);
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Assert.That(s.BaseFortification.IsActive || s.BaseFortification.IsRestoring, Is.False);
        }
        [Test] public void Shovel_RefreshRebuildsDestroyedSteelWithoutReplacingSnapshot_ReceiptDoesNotReplay()
        {
            var s = Session(); s.Map.SetTerrainCell(12, 4, TerrainKind.Brick, 7); Collect(s); Tick(s);
            s.Map.SetTerrainCell(12, 4, TerrainKind.Empty); Tick(s);
            Assert.That(s.Map.TerrainAt(12, 4), Is.EqualTo(TerrainKind.Empty)); Collect(s);
            Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(3)); Assert.That(s.Map.TerrainAt(12, 4), Is.EqualTo(TerrainKind.Steel));
            Tick(s, 3); Assert.That(s.Map.BrickMaskAt(12, 4), Is.EqualTo(7)); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(2));
        }
        [Test] public void Shovel_ReadyStartPauseFocusAndResumeDoNotConsumeExtraTime()
        {
            var s = Session(true); Drop(s, PickupKind.Shovel); Tick(s, 5); Assert.That(s.Pickups.CollectedCount, Is.Zero);
            Start(s); Assert.That(s.BaseFortification.RemainingTicks, Is.Zero); Tick(s);
            s.SetPaused(true); Tick(s, 4); Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(3));
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); s.SetFocus(false); Tick(s, 4);
            Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(3));
            s.SetFocus(true); s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); Tick(s);
            Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(2));
        }
        [TestCase(CombatTeam.Player)] [TestCase(CombatTeam.Enemy)]
        public void Shovel_ActivationUsesEveryLiveTank_DeferredSteelRetriesAfterTankLeaves(CombatTeam team)
        {
            var s = Session(); var occupant = s.AddTank(enemy, team, new Vector2(88, 24)); Collect(s);
            Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Empty)); Assert.That(s.BaseFortification.PendingCellCount, Is.GreaterThan(0));
            occupant.Motor.Step(CardinalDirection.Left, 960, s.Map); Tick(s);
            Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Steel)); Assert.That(s.BaseFortification.PendingCellCount, Is.Zero);
        }
        [Test] public void Shovel_ActivationAndLaterStepUseCurrentWaveReservationAndNewBirth()
        {
            level.enemySpawnPixels = new[] { new Vector2Int(88, 24) }; wave.preparationTicks = 1;
            var s = Session(); Collect(s); Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1));
            Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Empty)); Tick(s);
            Assert.That(s.EnemyWave.ReservedCount, Is.Zero); Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1));
            Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Empty));
            s.Tanks.Single(t => t.Team == CombatTeam.Enemy).Motor.Step(CardinalDirection.Left, 960, s.Map); Tick(s);
            Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Steel));
        }
        [Test] public void Shovel_ExpiryRestorationWaitsForLiveTank_PauseKeepsPending_ThenRestoresMask()
        {
            var s = Session(); s.Map.SetTerrainCell(11, 2, TerrainKind.Brick, 3); Collect(s);
            s.Map.SetTerrainCell(11, 2, TerrainKind.Empty);
            var occupant = s.AddTank(enemy, CombatTeam.Enemy, new Vector2(88, 16)); Tick(s, 3);
            Assert.That(s.BaseFortification.IsRestoring, Is.True); Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Empty));
            s.SetPaused(true); occupant.Motor.Step(CardinalDirection.Left, 960, s.Map); Tick(s, 3);
            Assert.That(s.BaseFortification.IsRestoring, Is.True); s.SetPaused(false); Tick(s);
            Assert.That(s.Map.BrickMaskAt(11, 2), Is.EqualTo(3)); Assert.That(s.BaseFortification.PendingCellCount, Is.Zero);
        }
        [Test] public void Shovel_ExpiryRestorationUsesReservationUntilBirthMovesAway()
        {
            level.enemySpawnPixels = new[] { new Vector2Int(88, 16) };
            var s = Session(); s.Map.SetTerrainCell(11, 2, TerrainKind.Brick, 9); Collect(s);
            s.Map.SetTerrainCell(11, 2, TerrainKind.Empty); Tick(s, 3);
            Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1)); Assert.That(s.BaseFortification.IsRestoring, Is.True);
            Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Empty));
        }
        [Test] public void Shovel_PlayerDeathAndRespawnDoNotCancelGlobalFortification()
        {
            rules.shovelDurationTicks = 20; var s = Session(); Collect(s); Shoot(s, s.Player.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(19));
            Tick(s, 2); Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(17));
        }
        [TestCase(false)] [TestCase(true)] public void Shovel_TerminalLossOrGrenadeVictoryRestoresAndCancelsPending(bool victory)
        {
            rules.shovelDurationTicks = 20; wave.preparationTicks = 1; level.enemySequence = new[] { enemy.definitionId };
            var s = Session(true); s.Map.SetTerrainCell(12, 4, TerrainKind.Brick, 6); Start(s); Collect(s); Tick(s);
            if (victory) Collect(s, PickupKind.Grenade); else { Shoot(s, s.Base.Position, CombatTeam.Enemy); Tick(s); }
            Assert.That(s.Phase, Is.EqualTo(victory ? SessionPhase.Won : SessionPhase.Lost));
            Assert.That(s.Map.BrickMaskAt(12, 4), Is.EqualTo(6)); Assert.That(s.BaseFortification.RemainingTicks, Is.Zero);
            Assert.That(s.BaseFortification.PendingCellCount, Is.Zero); Assert.That(s.BaseFortification.IsRestoring, Is.False);
            long tick = s.Clock.Tick; Tick(s, 3); Assert.That(s.Clock.Tick, Is.EqualTo(tick));
        }
        [Test] public void Shovel_NoBaseConsumesReceiptWithoutTerrainOrImplicitComponent()
        {
            var s = Session(withBase: false); int revision = s.Map.Revision; Collect(s);
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Assert.That(s.BaseFortification, Is.Null); Assert.That(s.Map.Revision, Is.EqualTo(revision));
            var withoutPickups = new ArenaSession(level, player, wave, true); Assert.That(withoutPickups.BaseFortification, Is.Null);
        }
        [TestCase(0)] [TestCase(-1)] public void Shovel_InvalidDurationRejected(int value)
        { rules.shovelDurationTicks = value; Assert.Throws<InvalidOperationException>(() => Session()); }
        [Test] public void Shovel_AuthoredDefault1200RequiresCalibration()
        {
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<PickupDefinition>(BattleCity.Editor.PickupAuthoring.DefinitionPath);
            Assert.That(authored.shovelDurationTicks, Is.EqualTo(1200)); Assert.That(authored.requiresCalibration, Is.True); authored.Validate();
        }
        [Test] public void Shovel_ClockHelmetAndGrenadeRemainIndependent()
        {
            rules.shovelDurationTicks = 20; rules.clockDurationTicks = 20; rules.helmetDurationTicks = 20;
            var s = Session(); Collect(s); Collect(s, PickupKind.Clock); Collect(s, PickupKind.Helmet);
            var victim = s.AddTank(enemy, CombatTeam.Enemy, new Vector2(64, 160)); Collect(s, PickupKind.Grenade);
            Assert.That(victim.IsAlive, Is.False); Assert.That(s.BaseFortification.RemainingTicks, Is.EqualTo(17));
            Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(18)); Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(19));
            Assert.That(s.Map.TerrainAt(12, 4), Is.EqualTo(TerrainKind.Steel));
        }
    }
}
