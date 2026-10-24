using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class SessionLifecycleIntegrationTests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        private EnemyWaveDefinition wave;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24); level.baseCenterPixels = new Vector2Int(104, 104);
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            tank = ScriptableObject.CreateInstance<TankDefinition>(); tank.definitionId = "lifecycle.enemy";
            tank.moveSpeedPixelsPerSecond = .001f; tank.bulletSpeedPixelsPerSecond = 6000;
            level.enemySequence = new[] { tank.definitionId };
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { tank };
            wave.preparationTicks = 1; wave.spawnIntervalTicks = 1;
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(wave); UnityEngine.Object.DestroyImmediate(tank); UnityEngine.Object.DestroyImmediate(level); }
        private ArenaSession Session(int lives = 3) => new ArenaSession(level, tank, wave, true, new PlayerLifeParameters(lives, 1, 1), true);
        private static int Tick(ArenaSession s, int count = 1, PlayerButtons keys = PlayerButtons.None)
            => s.AdvanceFrame(LogicClock.StepSeconds * count, keys);
        private static void Start(ArenaSession s)
        { Assert.That(s.StartBattle(), Is.True); Assert.That(s.AdvanceFrame(10, PlayerButtons.None), Is.Zero); }
        private WeaponState Shot(ArenaSession s, Vector2 target, CombatTeam team)
        {
            var weapon = new WeaponState(tank, s.Bullets, team, 999);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.Down, true), target + Vector2.up * 18, CardinalDirection.Down), Is.True);
            return weapon;
        }

        [Test] public void OptInRequiresWave_OldConstructorsStillAdvanceAndNeverSettle()
        {
            Assert.Throws<ArgumentNullException>(() => new ArenaSession(level, tank, null, false, null, true));
            var s = new ArenaSession(level, tank, wave);
            Assert.That(s.EnableSessionLifecycle, Is.False); Assert.That(Tick(s), Is.EqualTo(1));
            Assert.That(s.Clock.Paused, Is.False);
        }

        [Test] public void ReadyStartPauseFocusAndHeldCombat_TransitionsNeverContributeElapsed()
        {
            tank.moveSpeedPixelsPerSecond = 60;
            var s = Session(); int states = 0; s.StateChanged += () => states++;
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Ready)); s.SetPaused(false);
            Tick(s, 8, PlayerButtons.D | PlayerButtons.Fire | PlayerButtons.Pause);
            Assert.That(s.Clock.Tick, Is.Zero); Assert.That(s.EnemyWave.ReservedCount, Is.Zero);
            s.SetFocus(false); Assert.That(s.StartBattle(), Is.False);
            Tick(s, 8, PlayerButtons.Start); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Ready));
            s.SetFocus(true); Tick(s); Tick(s, 8, PlayerButtons.Start | PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Playing)); Assert.That(s.Clock.Tick, Is.Zero);
            Tick(s, 1, PlayerButtons.Start | PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(s.Player.Position, Is.EqualTo(new Vector2(24, 24))); Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
            Tick(s); Tick(s, 1, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(s.Player.Position.x, Is.EqualTo(25)); Assert.That(s.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
            long tick = s.Clock.Tick;
            Tick(s, 8, PlayerButtons.Pause | PlayerButtons.D); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Paused));
            Tick(s, 8, PlayerButtons.Pause); Assert.That(s.Clock.Tick, Is.EqualTo(tick));
            Tick(s); Tick(s, 8, PlayerButtons.Pause | PlayerButtons.D); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Playing));
            Assert.That(s.Clock.Tick, Is.EqualTo(tick));
            Tick(s, 1, PlayerButtons.D); Assert.That(s.Player.Position.x, Is.EqualTo(25));
            s.SetFocus(false); s.SetFocus(true); Tick(s, 8);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Paused)); Assert.That(s.Clock.Tick, Is.EqualTo(tick + 1));
            Assert.That(states, Is.EqualTo(4)); Assert.That(s.Clock.DroppedTicks, Is.Zero);
        }

        [TestCase(false, false, SessionPhase.Won, SessionFailureReason.None)]
        [TestCase(true, false, SessionPhase.Lost, SessionFailureReason.BaseDestroyed)]
        [TestCase(false, true, SessionPhase.Lost, SessionFailureReason.PlayerLivesExhausted)]
        [TestCase(true, true, SessionPhase.Lost, SessionFailureReason.BaseDestroyed | SessionFailureReason.PlayerLivesExhausted)]
        public void ActualSharedBullets_LastEnemyAndFailures_SettleAllDamageBeforeOneFrozenOutcome(
            bool hitBase, bool hitPlayer, SessionPhase phase, SessionFailureReason reason)
        {
            var s = Session(1); Start(s); Tick(s, 2);
            Assert.That(s.EnemyWave.SequenceExhausted, Is.True); var enemy = s.Tanks.Last();
            var enemyShot = Shot(s, enemy.Motor.Position, CombatTeam.Player);
            var baseShot = hitBase ? Shot(s, s.Base.Position, CombatTeam.Enemy) : null;
            var playerShot = hitPlayer ? Shot(s, s.Player.Position, CombatTeam.Enemy) : null;
            int outcomes = 0, baseDeaths = 0, playerDeaths = 0;
            s.BaseDestroyed += () => { baseDeaths++; Assert.That(s.Phase, Is.EqualTo(phase)); Assert.That(s.Clock.Paused, Is.True); };
            s.PlayerLivesExhausted += () => { playerDeaths++; Assert.That(enemy.IsAlive, Is.False); Assert.That(s.Base.IsAlive, Is.EqualTo(!hitBase)); };
            s.SessionEnded += () =>
            {
                outcomes++;
                Assert.That(s.Clock.Tick, Is.EqualTo(3)); Assert.That(s.Clock.Paused, Is.True);
                Assert.That(s.FailureReason, Is.EqualTo(reason)); Assert.That(s.AliveEnemyCount, Is.Zero);
                Assert.That(s.AdvanceFrame(1, PlayerButtons.None), Is.Zero, "notification cannot reenter combat");
                Assert.That(s.StartBattle(), Is.False); s.SetPaused(false);
            };
            Assert.That(Tick(s, 8), Is.EqualTo(1), "remaining seven render-frame ticks must stop");
            Assert.That(s.Phase, Is.EqualTo(phase)); Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(baseDeaths, Is.EqualTo(hitBase ? 1 : 0)); Assert.That(playerDeaths, Is.EqualTo(hitPlayer ? 1 : 0));
            Assert.That(enemyShot.ActiveBullets, Is.Zero); Assert.That(baseShot?.ActiveBullets ?? 0, Is.Zero); Assert.That(playerShot?.ActiveBullets ?? 0, Is.Zero);
            Assert.That(s.Effects.Active.Any(e => e.Kind == CombatEffectKind.Explosion), Is.True);
            var ages = s.Effects.Active.Select(e => e.AgeTicks).ToArray(); int hitCount = s.Bullets.Hits.Count;
            s.SetFocus(false); s.SetFocus(true); s.SetPaused(false);
            Tick(s, 8, PlayerButtons.Pause | PlayerButtons.Fire); Tick(s); Tick(s, 8, PlayerButtons.Start);
            Assert.That(s.Clock.Tick, Is.EqualTo(3)); Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(s.Effects.Active.Select(e => e.AgeTicks), Is.EqualTo(ages)); Assert.That(s.Bullets.Hits.Count, Is.EqualTo(hitCount));
        }

        [Test] public void EmptyFieldPendingBlockedAndReservationNeverWins_BirthSettlesBeforeOutcome()
        {
            level.enemySpawnPixels = new[] { level.playerSpawnPixels };
            var s = Session(); Start(s); Tick(s, 8);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Playing)); Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(1));
            Assert.That(s.EnemyWave.ReservedCount, Is.Zero);
            tank.moveSpeedPixelsPerSecond = 60;
            // A separate clear fixture reaches natural reservation then birth without a premature win.
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) }; s = Session(); Start(s);
            Tick(s); Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1)); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Playing));
            Tick(s); Assert.That(s.EnemyWave.SequenceExhausted, Is.True); Assert.That(s.AliveEnemyCount, Is.EqualTo(1));
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Playing));
        }

        [Test] public void StateNotificationCannotReenterStartPauseOrAdvance()
        {
            var s = Session(); int notifications = 0;
            s.StateChanged += () => { notifications++; s.SetPaused(true); Assert.That(s.StartBattle(), Is.False); Assert.That(Tick(s, 8), Is.Zero); };
            Start(s); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Playing)); Assert.That(notifications, Is.EqualTo(1));
            Tick(s); Assert.That(s.Clock.Tick, Is.EqualTo(1));
        }
    }
}
