using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC305")]
    public sealed class ClockGrenadePickupTests
    {
        private LevelDefinition level;
        private TankDefinition player, enemy, armor;
        private EnemyWaveDefinition wave;
        private PickupDefinition rules;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.baseCenterPixels = new Vector2Int(104, 24);
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            player = ScriptableObject.CreateInstance<TankDefinition>();
            enemy = ScriptableObject.CreateInstance<TankDefinition>(); enemy.definitionId = "enemy.basic"; enemy.score = 100;
            armor = ScriptableObject.CreateInstance<TankDefinition>(); armor.definitionId = "enemy.armor"; armor.hitPoints = 4; armor.score = 400;
            level.enemySequence = new[] { enemy.definitionId, enemy.definitionId };
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { enemy, armor };
            wave.preparationTicks = 900; wave.spawnIntervalTicks = 900;
            rules = ScriptableObject.CreateInstance<PickupDefinition>(); rules.carrierSequenceIndices = new int[0]; rules.clockDurationTicks = 3;
        }
        [TearDown] public void TearDown()
        {
            foreach (var o in new Object[] { level, player, enemy, armor, wave, rules }) Object.DestroyImmediate(o);
        }
        private ArenaSession Session(bool managed = false, bool withBase = false, int lives = 3)
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
            Assert.Fail("Bounded deterministic drop search failed for " + kind);
        }
        private static void Collect(ArenaSession s, PickupKind kind) { Drop(s, kind); Tick(s); }
        private void Shoot(ArenaSession s, Vector2 target, CombatTeam team, int count = 1)
        {
            for (int i = 0; i < count; i++)
                Assert.That(new WeaponState(player, s.Bullets, team, 900 + i).Step(
                    new TankCommand(CardinalDirection.Down, false, true), target + Vector2.up * 18, CardinalDirection.Down), Is.True);
        }
        [Test] public void Clock_Default600FutureCompleteTicks_SnapshotsRules_FreezesAIAndExternalWeaponMotorTracks()
        {
            rules.clockDurationTicks = 600; var s = Session();
            var ai = s.AddTank(enemy, CombatTeam.Enemy, new Vector2(64, 160)); var brain = s.RegisterEnemyAI(ai.EntityId, 123);
            var external = s.AddTank(enemy, CombatTeam.Enemy, new Vector2(120, 120));
            s.SetTankCommand(external.EntityId, new TankCommand(CardinalDirection.Right, true)); Collect(s, PickupKind.Clock);
            rules.clockDurationTicks = 1;
            var position = ai.Motor.Position; var otherPosition = external.Motor.Position;
            uint rng = brain.RandomState; long decisions = brain.DecisionTicks;
            int directionTicks = brain.DirectionTicksRemaining, cooldown = ai.Weapon.CooldownRemainingTicks;
            int otherCooldown = external.Weapon.CooldownRemainingTicks, tracks = ai.Motor.TrackFrame, otherTracks = external.Motor.TrackFrame;
            for (int remaining = 599; remaining >= 0; remaining--)
            {
                s.SetTankCommand(external.EntityId, new TankCommand(CardinalDirection.Left, true, true)); Tick(s);
                Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(remaining));
                Assert.That(ai.Motor.Position, Is.EqualTo(position)); Assert.That(external.Motor.Position, Is.EqualTo(otherPosition));
                Assert.That(ai.Motor.TrackFrame, Is.EqualTo(tracks)); Assert.That(external.Motor.TrackFrame, Is.EqualTo(otherTracks));
                Assert.That(ai.Weapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
                Assert.That(external.Weapon.CooldownRemainingTicks, Is.EqualTo(otherCooldown));
                Assert.That(brain.RandomState, Is.EqualTo(rng)); Assert.That(brain.DecisionTicks, Is.EqualTo(decisions));
                Assert.That(brain.DirectionTicksRemaining, Is.EqualTo(directionTicks));
            }
            Tick(s); Assert.That(brain.DecisionTicks, Is.EqualTo(decisions + 1));
            Assert.That(ai.Motor.Position, Is.Not.EqualTo(position)); Assert.That(external.Motor.Position, Is.Not.EqualTo(otherPosition));
            Collect(s, PickupKind.Clock); Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(600));
        }
        [Test] public void Clock_RepeatRefreshesNotAdds_NoReceiptReplay_PauseReadyAndFocusConsumeNothing()
        {
            var s = Session(true); Drop(s, PickupKind.Clock); Tick(s, 8); Assert.That(s.Pickups.CollectedCount, Is.Zero);
            Start(s); Tick(s); Tick(s); Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(2));
            Collect(s, PickupKind.Clock); Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(3));
            s.SetPaused(true); Tick(s, 8); Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(3));
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); s.SetFocus(false); Tick(s, 8);
            Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(3));
            s.SetFocus(true); s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); Tick(s, 3);
            Assert.That(s.Pickups.ClockRemainingTicks, Is.Zero); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(2));
        }
        [Test] public void Clock_NewWaveBirthStillOccurs_StaysFrozenAndBlocksPlayer_ButCanBeKilled()
        {
            rules.clockDurationTicks = 20; wave.preparationTicks = 1; var s = Session(); Collect(s, PickupKind.Clock);
            Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1)); Tick(s);
            var born = s.Tanks.Single(t => t.Team == CombatTeam.Enemy); var p = born.Motor.Position;
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1)); Tick(s, 4);
            Assert.That(born.Motor.Position, Is.EqualTo(p)); Assert.That(born.Weapon.CooldownRemainingTicks, Is.Zero);
            Assert.Throws<ArgumentException>(() => s.AddTank(enemy, CombatTeam.Player, p));
            Shoot(s, p, CombatTeam.Player); Tick(s); Assert.That(born.IsAlive, Is.False); Assert.That(s.Score, Is.EqualTo(100));
        }
        [Test] public void Clock_PlayerAndEnemyBulletsAndEffectsContinue_PlayerDeathDoesNotCancelGlobalEffect()
        {
            rules.clockDurationTicks = 20; var s = Session(); Collect(s, PickupKind.Clock);
            var gun = new WeaponState(enemy, s.Bullets, CombatTeam.Enemy, 999);
            gun.Step(new TankCommand(CardinalDirection.Right, false, true), new Vector2(64, 64), CardinalDirection.Right);
            var bullet = s.Bullets.Active.Single(); var before = bullet.Position;
            s.Effects.PlayExplosion(new Vector2(80, 80));
            s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Right | PlayerButtons.Fire);
            Assert.That(s.Player.Position.x, Is.GreaterThan(24)); Assert.That(s.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(bullet.Position.x, Is.GreaterThan(before.x)); Assert.That(s.Effects.Active.Single().AgeTicks, Is.EqualTo(1));
            Shoot(s, s.Player.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(18));
            Tick(s, 2); Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(16));
        }
        [Test] public void Grenade_ArmorBatchAndSameTickBulletDeath_ScoreAndExplosionExactlyOnce_NoProjectileTerrainOrBaseDamage()
        {
            level.terrain[10 * 26 + 10] = TerrainKind.Brick; var s = Session(withBase: true);
            var basic = s.AddTank(enemy, CombatTeam.Enemy, new Vector2(64, 120));
            var heavy = s.AddTank(armor, CombatTeam.Enemy, new Vector2(120, 120)); heavy.IsProtected = true;
            Shoot(s, basic.Motor.Position, CombatTeam.Player);
            var gun = new WeaponState(enemy, s.Bullets, CombatTeam.Enemy, 998);
            gun.Step(new TankCommand(CardinalDirection.Right, false, true), new Vector2(64, 64), CardinalDirection.Right);
            var bullet = s.Bullets.Active.Last(); Drop(s, PickupKind.Grenade); Tick(s);
            Assert.That(basic.IsAlive || heavy.IsAlive, Is.False); Assert.That(heavy.HitPoints, Is.Zero);
            Assert.That(s.Score, Is.EqualTo(500)); Assert.That(s.DefeatedEnemyCount, Is.EqualTo(2));
            Assert.That(s.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion), Is.EqualTo(2));
            Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(s.Pickups.SpawnedCount));
            Assert.That(s.Bullets.Active, Does.Contain(bullet)); Assert.That(s.Base.IsAlive && s.PlayerTank.IsAlive, Is.True);
            Assert.That(s.EnemyWave.AliveCount, Is.Zero);
            Assert.That(new MapState(level).CanOccupy(new Vector2(84, 84)), Is.False);
            Assert.That(s.Map.CanOccupy(new Vector2(84, 84)), Is.False);
            Collect(s, PickupKind.Grenade); Tick(s, 2);
            Assert.That(s.Score, Is.EqualTo(500)); Assert.That(s.DefeatedEnemyCount, Is.EqualTo(2));
        }
        [Test] public void Grenade_DoesNotReplayOnLaterEnemy_ReservationAndPendingAndCadenceConserved()
        {
            var s = Session(); var victim = s.AddTank(armor, CombatTeam.Enemy, new Vector2(80, 120));
            Collect(s, PickupKind.Grenade);
            Assert.That(victim.IsAlive, Is.False); Assert.That(s.EnemyWave.SpawnedCount, Is.Zero);
            Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1)); Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(2));
            Assert.That(s.EnemyWave.PreparationRemainingTicks, Is.EqualTo(900));
            var survivor = s.AddTank(enemy, CombatTeam.Enemy, new Vector2(120, 120)); Tick(s);
            Assert.That(survivor.IsAlive, Is.True); Assert.That(s.EnemyWave.PreparationRemainingTicks, Is.EqualTo(899));
            Assert.That(s.EnemyWave.AliveCount, Is.EqualTo(1)); Assert.That(s.Score, Is.EqualTo(400));
        }
        [Test] public void Grenade_KillsNewlyCommittedCarrierWithoutDropping_OnceWaveStepOnly()
        {
            wave.preparationTicks = 1; wave.spawnIntervalTicks = 7; rules.carrierSequenceIndices = new[] { 0 };
            var s = Session(); Tick(s); Collect(s, PickupKind.Grenade);
            var born = s.Tanks.Single(t => t.Team == CombatTeam.Enemy);
            Assert.That(born.CarriesPickup, Is.True); Assert.That(born.IsAlive, Is.False);
            Assert.That(s.Pickups.SpawnedCount, Is.EqualTo(s.Pickups.DropAttemptCount));
            Assert.That(s.Pickups.Active, Is.Null); Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1));
            Assert.That(s.EnemyWave.AliveCount, Is.Zero); Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(1));
            Assert.That(s.EnemyWave.ReservedCount, Is.Zero); Assert.That(s.EnemyWave.IntervalRemainingTicks, Is.EqualTo(7));
        }
        [Test] public void Grenade_LastEnemyWinsSameTick_OneTerminalEventAndCleanup()
        {
            level.enemySequence = new[] { armor.definitionId }; wave.preparationTicks = 1;
            rules.clockDurationTicks = 50; var s = Session(true); Start(s); Collect(s, PickupKind.Clock); Tick(s);
            int ended = 0; s.SessionEnded += () => ended++;
            Collect(s, PickupKind.Grenade);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won)); Assert.That(s.Score, Is.EqualTo(400));
            Assert.That(s.EnemyWave.AliveCount, Is.Zero); Assert.That(s.EnemyWave.SpawnedCount + s.EnemyWave.PendingCount, Is.EqualTo(1));
            Assert.That(s.Pickups.ClockRemainingTicks, Is.Zero); Assert.That(s.Pickups.Active, Is.Null);
            Tick(s, 3); Assert.That(ended, Is.EqualTo(1)); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(2));
        }
        [Test] public void Grenade_SameTickBaseFailurePreventsCollectionAndKills_FailureWins()
        {
            level.enemySequence = new[] { armor.definitionId }; wave.preparationTicks = 1;
            var s = Session(true, true); Start(s); Tick(s, 2); Drop(s, PickupKind.Grenade);
            Shoot(s, s.Base.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Lost)); Assert.That(s.FailureReason, Is.EqualTo(SessionFailureReason.BaseDestroyed));
            Assert.That(s.AliveEnemyCount, Is.EqualTo(1)); Assert.That(s.Score, Is.Zero); Assert.That(s.Pickups.CollectedCount, Is.Zero);
        }
        [Test] public void AlreadyTerminalBulletKillPreventsAnyPickupReward()
        {
            level.enemySequence = new[] { enemy.definitionId }; wave.preparationTicks = 1;
            var s = Session(true); Start(s); Tick(s, 2); Drop(s, PickupKind.Clock);
            Shoot(s, s.Tanks.Single(t => t.Team == CombatTeam.Enemy).Motor.Position, CombatTeam.Player); Tick(s);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won)); Assert.That(s.Pickups.CollectedCount, Is.Zero);
            Assert.That(s.Pickups.ClockRemainingTicks, Is.Zero);
        }
        [Test] public void SimultaneousLastEnemyBulletDeathAndPlayerExhaustion_IsLossNotGrenadeReward()
        {
            level.enemySequence = new[] { enemy.definitionId }; wave.preparationTicks = 1;
            var s = Session(true, lives: 1); Start(s); Tick(s, 2); Drop(s, PickupKind.Grenade);
            Shoot(s, s.Tanks.Single(t => t.Team == CombatTeam.Enemy).Motor.Position, CombatTeam.Player);
            Shoot(s, s.Player.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Lost)); Assert.That(s.FailureReason, Is.EqualTo(SessionFailureReason.PlayerLivesExhausted));
            Assert.That(s.Score, Is.EqualTo(100)); Assert.That(s.Pickups.CollectedCount, Is.Zero);
        }
        [TestCase(0)] [TestCase(-1)] public void InvalidClockDurationRejected(int duration)
        { rules.clockDurationTicks = duration; Assert.Throws<InvalidOperationException>(() => Session()); }
        [Test] public void AuthoredClockDefaultIsExplicitAndUncalibrated()
        {
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<PickupDefinition>(BattleCity.Editor.PickupAuthoring.DefinitionPath);
            Assert.That(authored.clockDurationTicks, Is.EqualTo(600)); Assert.That(authored.requiresCalibration, Is.True); authored.Validate();
        }
    }
}
