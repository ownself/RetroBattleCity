using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class PlayerLifeIntegrationTests
    {
        private LevelDefinition level;
        private TankDefinition definition;
        private EnemyWaveDefinition wave;
        private static readonly TankCommand Idle = new TankCommand(CardinalDirection.None);
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.baseCenterPixels = new Vector2Int(104, 104);
            definition = ScriptableObject.CreateInstance<TankDefinition>();
        }
        [TearDown] public void TearDown()
        {
            if (wave != null) UnityEngine.Object.DestroyImmediate(wave);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(level);
        }
        private ArenaSession Session(PlayerLifeParameters parameters = null, bool enableBase = false)
            => new ArenaSession(level, definition, wave, enableBase, parameters ?? PlayerLifeParameters.Prototype);
        private static void Tick(ArenaSession s, int count = 1, PlayerButtons buttons = PlayerButtons.None)
        { for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, buttons); }
        private WeaponState Shot(ArenaSession s, Vector2 target, CombatTeam team = CombatTeam.Enemy)
        {
            var weapon = new WeaponState(definition, s.Bullets, team, 999);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.Down, true), target + Vector2.up * 18, CardinalDirection.Down), Is.True);
            return weapon;
        }
        private void Kill(ArenaSession s)
        {
            for (int i = 0; i < s.PlayerTank.HitPoints; i++) Shot(s, s.Player.Position);
            Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False);
        }

        [Test] public void LegacyTwoThreeFourArgumentSessionsRemainDeathOnly()
        {
            var sessions = new[] { new ArenaSession(level, definition), new ArenaSession(level, definition, null), new ArenaSession(level, definition, null, true) };
            foreach (var s in sessions)
            {
                Kill(s); Tick(s, 300);
                Assert.That(s.PlayerLife, Is.Null); Assert.That(s.PlayerTank.IsAlive, Is.False);
                Assert.That(s.PlayerLifeFailureRequested, Is.False);
            }
        }

        [Test] public void SharedEnemyWeaponDeath_AbsoluteDelayAndProtectionEndpoints_ConsumesProtectedHits()
        {
            definition.bulletSpeedPixelsPerSecond = 6000;
            var s = Session(); var enemy = s.AddTank(definition, CombatTeam.Enemy, new Vector2(24, 64));
            Tick(s, 36);
            s.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Down, false, true));
            Tick(s);
            Assert.That(s.Clock.Tick, Is.EqualTo(37)); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2));
            Assert.That(s.PlayerLife.RespawnRemainingTicks, Is.EqualTo(60));
            Assert.That(s.Bullets.Hits.Single().OwnerId, Is.EqualTo(enemy.EntityId));
            s.SetTankCommand(enemy.EntityId, Idle);
            Tick(s, 59); Assert.That(s.Clock.Tick, Is.EqualTo(96)); Assert.That(s.PlayerTank.IsAlive, Is.False);
            Assert.That(s.PlayerLife.RespawnRemainingTicks, Is.EqualTo(1));
            Tick(s); Assert.That(s.Clock.Tick, Is.EqualTo(97)); Assert.That(s.PlayerTank.IsAlive, Is.True);
            Assert.That(s.PlayerLife.ProtectionRemainingTicks, Is.EqualTo(120));
            for (int i = 1; i <= 120; i++)
            {
                Assert.That(s.PlayerLife.IsProtected, Is.True);
                var shot = Shot(s, s.Player.Position); Tick(s);
                Assert.That(shot.ActiveBullets, Is.Zero); Assert.That(s.PlayerTank.HitPoints, Is.EqualTo(1));
                Assert.That(s.Bullets.Hits.Single().TargetId, Is.EqualTo(1));
            }
            Assert.That(s.Clock.Tick, Is.EqualTo(217)); Assert.That(s.PlayerLife.IsProtected, Is.False);
            Kill(s); Assert.That(s.Clock.Tick, Is.EqualTo(218)); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(1));
        }

        [Test] public void MultiHitDeathDoesNotTruncateOtherDamage_ExhaustionEventOnce_NoWorldFreeze()
        {
            var s = Session(new PlayerLifeParameters(1, 2, 2), true);
            var enemy = s.AddTank(definition, CombatTeam.Enemy, new Vector2(160, 160));
            int exhausted = 0;
            s.PlayerLivesExhausted += () => { exhausted++; Assert.That(enemy.IsAlive, Is.False); Assert.That(s.Base.IsAlive, Is.False); };
            Shot(s, s.Player.Position); Shot(s, s.Player.Position);
            Shot(s, enemy.Motor.Position, CombatTeam.Player); Shot(s, s.Base.Position);
            Tick(s);
            Assert.That(s.Bullets.Hits.Count, Is.EqualTo(4)); Assert.That(exhausted, Is.EqualTo(1));
            Assert.That(s.PlayerLife.RemainingLives, Is.Zero); Assert.That(s.PlayerLifeFailureRequested, Is.True);
            Tick(s, 300); Assert.That(s.Clock.Tick, Is.EqualTo(301)); Assert.That(exhausted, Is.EqualTo(1));
            Assert.That(s.PlayerTank.IsAlive, Is.False);
        }

        [Test] public void RespawnPreservesReferencesIdsAndSnapshots_ResetsPoseHealthWeaponAndOnlyPlayerLeases()
        {
            definition.hitPoints = 2; definition.bulletSpeedPixelsPerSecond = 1;
            var s = Session(new PlayerLifeParameters(3, 2, 3));
            var player = s.PlayerTank; var motor = s.Player; var weapon = s.PlayerWeapon;
            var enemy = s.AddTank(definition, CombatTeam.Enemy, new Vector2(160, 160));
            Tick(s, 8, PlayerButtons.D); Assert.That(motor.TrackFrame, Is.EqualTo(1));
            Tick(s, 1, PlayerButtons.Fire);
            long oldPlayerLease = s.Bullets.Active.Single().Id;
            s.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Up, false, true)); Tick(s);
            s.SetTankCommand(enemy.EntityId, Idle);
            var enemyBullet = s.Bullets.Active.Single(b => b.OwnerId == enemy.EntityId); long enemyLease = enemyBullet.Id;
            definition.bulletSpeedPixelsPerSecond = 180;
            Shot(s, motor.Position); Shot(s, motor.Position); Tick(s);
            Assert.That(player.IsAlive, Is.False); Assert.That(weapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(s.Bullets.Active.Any(b => b.Id == oldPlayerLease), Is.True);
            level.playerSpawnPixels = new Vector2Int(72, 72); definition.hitPoints = 9;
            definition.fireCooldownTicks = 999; definition.maxActiveBullets = 99; definition.bulletSpeedPixelsPerSecond = 6000;
            Tick(s, 2);
            Assert.That(s.PlayerTank, Is.SameAs(player)); Assert.That(s.Player, Is.SameAs(motor)); Assert.That(s.PlayerWeapon, Is.SameAs(weapon));
            Assert.That(s.Tanks[1], Is.SameAs(enemy)); Assert.That(player.EntityId, Is.EqualTo(1));
            Assert.That(motor.Position, Is.EqualTo(new Vector2(24, 24))); Assert.That(player.HitPoints, Is.EqualTo(2));
            Assert.That(motor.Facing, Is.EqualTo(CardinalDirection.Up)); Assert.That(motor.TrackFrame, Is.Zero); Assert.That(motor.IsMoving, Is.False);
            Assert.That(weapon.ActiveBullets, Is.Zero); Assert.That(weapon.CooldownRemainingTicks, Is.Zero);
            Assert.That(s.Bullets.Active.Single().Id, Is.EqualTo(enemyLease)); Assert.That(enemy.Weapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(s.Bullets.Release(oldPlayerLease), Is.False);
            Tick(s, 1, PlayerButtons.Fire);
            Assert.That(weapon.ActiveBullets, Is.EqualTo(1)); Assert.That(weapon.CooldownRemainingTicks, Is.EqualTo(15));
            Assert.That(s.Bullets.Active.Single(b => b.OwnerId == 1).SpeedPixelsPerSecond, Is.EqualTo(1));
            Assert.That(s.AddTank(definition, CombatTeam.Player, new Vector2(80, 80)).EntityId, Is.EqualTo(3));
        }

        [Test] public void FinalDeathRetainsPlayerProjectileUntilItsNormalRelease()
        {
            definition.bulletSpeedPixelsPerSecond = 1;
            var s = Session(new PlayerLifeParameters(1, 1, 1)); Tick(s, 1, PlayerButtons.Fire);
            long lease = s.Bullets.Active.Single().Id; definition.bulletSpeedPixelsPerSecond = 180;
            Kill(s); Tick(s, 100);
            Assert.That(s.Bullets.Active.Single().Id, Is.EqualTo(lease)); Assert.That(s.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(s.Bullets.Release(lease), Is.True); Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
        }

        [TestCase(39, false)] [TestCase(40, true)]
        public void ReadyBirthUsesFullSixteenPixelLivingTankFootprint_DeadPositionNotReserved(int blockerX, bool immediately)
        {
            var s = Session(new PlayerLifeParameters(3, 2, 2));
            Tick(s, 32, PlayerButtons.W); Kill(s);
            var blocker = s.AddTank(definition, CombatTeam.Enemy, new Vector2(blockerX, 24));
            var atCorpse = s.AddTank(definition, CombatTeam.Player, s.Player.Position);
            Tick(s, 2);
            Assert.That(s.PlayerTank.IsAlive, Is.EqualTo(immediately)); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2));
            if (!immediately)
            {
                Tick(s, 10); Assert.That(s.PlayerLife.RespawnRemainingTicks, Is.Zero);
                s.SetTankCommand(blocker.EntityId, new TankCommand(CardinalDirection.Right)); Tick(s);
                Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2));
            }
            Assert.That(atCorpse.IsAlive, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void BirthAllowsExactBaseEdgeTouch_RegistrationStillRejectsIntactOrRuinedBase(bool ruin)
        {
            level.baseCenterPixels = new Vector2Int(40, 24);
            var s = Session(new PlayerLifeParameters(3, 1, 1), true);
            // The actual creation snapshot is clear and touching; moving the player must not change it.
            Tick(s, 32, PlayerButtons.W); Kill(s);
            if (ruin) { Shot(s, s.Base.Position); Tick(s); Assert.That(s.Base.IsAlive, Is.False); }
            Tick(s, 2); Assert.That(s.PlayerTank.IsAlive, Is.True);
            Assert.That(s.Player.Position, Is.EqualTo(new Vector2(24, 24)), "Base edge touching is legal, not overlapping");
            Assert.Throws<ArgumentException>(() => s.AddTank(definition, CombatTeam.Player, s.Base.Position));
        }

        [Test] public void EnemyReservationAndThenLivingEnemyBlockReadyPlayer_UntilDamageReleasesSpace()
        {
            definition.definitionId = "enemy.life";
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { definition };
            wave.preparationTicks = 3; wave.spawnIntervalTicks = 100;
            level.enemySpawnPixels = new[] { level.playerSpawnPixels }; level.enemySequence = new[] { definition.definitionId };
            var s = Session(new PlayerLifeParameters(3, 1, 1));
            Tick(s, 16, PlayerButtons.W); // Freed spawn now has an enemy reservation.
            Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1)); Kill(s); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.PlayerLife.IsRespawnReady, Is.True);
            Tick(s, 3); Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1));
            var enemy = s.Tanks.Last(); Shot(s, enemy.Motor.Position, CombatTeam.Player); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.True);
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
        }

        [Test] public void PlayerBirthPrecedesNewEnemyReservationWhenDamageFreesWaveCapacity()
        {
            definition.definitionId = "enemy.life";
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { definition };
            wave.preparationTicks = 3; wave.spawnIntervalTicks = 100;
            level.maxActiveEnemies = 1; level.enemySpawnPixels = new[] { level.playerSpawnPixels };
            level.enemySequence = new[] { definition.definitionId };
            var s = Session(new PlayerLifeParameters(3, 1, 1));
            var enemy = s.AddTank(definition, CombatTeam.Enemy, new Vector2(160, 160));
            Kill(s); Assert.That(s.EnemyWave.ReservedCount, Is.Zero);
            Shot(s, enemy.Motor.Position, CombatTeam.Player); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(enemy.IsAlive, Is.False);
            Assert.That(s.EnemyWave.ReservedCount, Is.Zero, "Player birth occupies the only spawn before wave queries it");
            Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(1));
        }

        [Test] public void DeadInputAndMultiTickFrameCannotReplayHeldMoveOrFire_ReleaseThenPressRearms_EscapeStillWorks()
        {
            var s = Session(new PlayerLifeParameters(3, 2, 2));
            Shot(s, s.Player.Position);
            s.AdvanceFrame(LogicClock.StepSeconds * 8, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.Player.Position, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(s.LastCommand.Direction, Is.EqualTo(CardinalDirection.None)); Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
            Tick(s, 3, PlayerButtons.D | PlayerButtons.Fire); Assert.That(s.Player.Position, Is.EqualTo(new Vector2(24, 24)));
            Tick(s); Tick(s, 1, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(s.Player.Position, Is.EqualTo(new Vector2(25, 24))); Assert.That(s.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
            s.Bullets.Clear(); Tick(s, 3); Kill(s);
            Tick(s, 1, PlayerButtons.Pause); Assert.That(s.Clock.Paused, Is.True);
        }

        [Test] public void NewPressDuringDeadRenderFramesIsNotCachedAcrossRespawn()
        {
            var s = Session(new PlayerLifeParameters(3, 2, 2)); Kill(s);
            s.AdvanceFrame(0, PlayerButtons.W | PlayerButtons.Fire);
            Assert.That(s.Clock.Tick, Is.EqualTo(1));
            s.AdvanceFrame(LogicClock.StepSeconds * 4, PlayerButtons.W | PlayerButtons.Fire);
            Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.Player.Position, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero); Assert.That(s.LastCommand.FirePressed, Is.False);
            Tick(s); Tick(s, 1, PlayerButtons.W | PlayerButtons.Fire);
            Assert.That(s.Player.Position, Is.EqualTo(new Vector2(24, 25))); Assert.That(s.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
        }

        [Test] public void ThreeActualGenerationsSpendExactlyThreeLives_ThenNeverRespawn()
        {
            var s = Session(new PlayerLifeParameters(3, 1, 1)); int exhausted = 0;
            s.PlayerLivesExhausted += () => exhausted++;
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                Kill(s); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(remaining));
                Tick(s);
                Assert.That(s.PlayerTank.IsAlive, Is.EqualTo(remaining > 0));
                if (remaining > 0) Tick(s); // The one full protected damage phase.
            }
            Tick(s, 100); Assert.That(s.PlayerLife.RemainingLives, Is.Zero);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(exhausted, Is.EqualTo(1));
        }

        [Test] public void PauseAndFocusFreezeBothTimers_NoCatchupAndManualResume()
        {
            var s = Session(); Kill(s); long death = s.Clock.Tick;
            s.SetPaused(true); s.AdvanceFrame(100, PlayerButtons.Fire); Assert.That(s.PlayerLife.RespawnRemainingTicks, Is.EqualTo(60));
            s.SetPaused(false); Tick(s, 60); Assert.That(s.PlayerLife.ProtectionRemainingTicks, Is.EqualTo(120));
            s.SetFocus(false); s.AdvanceFrame(100, PlayerButtons.W); s.SetFocus(true); Tick(s, 10);
            Assert.That(s.Clock.Paused, Is.True); Assert.That(s.Clock.Tick, Is.EqualTo(death + 60));
            Assert.That(s.PlayerLife.ProtectionRemainingTicks, Is.EqualTo(120));
            s.SetPaused(false); Tick(s); Assert.That(s.PlayerLife.ProtectionRemainingTicks, Is.EqualTo(119));
            Assert.That(s.Clock.DroppedTicks, Is.Zero);
        }
    }
}
