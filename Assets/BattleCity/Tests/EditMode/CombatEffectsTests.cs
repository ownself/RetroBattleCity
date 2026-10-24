using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class CombatEffectsTests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        private ArenaSession session;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            tank = ScriptableObject.CreateInstance<TankDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            tank.bulletSpeedPixelsPerSecond = 6000;
            tank.fireCooldownTicks = 1;
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(level); Object.DestroyImmediate(tank); }
        private void Tick(int count = 1)
        { for (int i = 0; i < count; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); }
        private void Wall(TerrainKind kind = TerrainKind.Brick)
        { level.terrain[2 * 26 + 4] = level.terrain[3 * 26 + 4] = kind; }

        [Test]
        public void MultipleLogicSteps_KeepEveryHitEvenWhenLastTickHasNone()
        {
            Wall(); session = new ArenaSession(level, tank);
            Assert.That(session.AdvanceFrame(2 * LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire), Is.EqualTo(2));
            Assert.That(session.Effects.Active.Count, Is.EqualTo(2));
            Assert.That(session.Effects.Active[0].AgeTicks, Is.EqualTo(1));
            Assert.That(session.Effects.Active[1].AgeTicks, Is.Zero);
            Assert.That(session.Effects.Active[0].Id, Is.Not.EqualTo(session.Effects.Active[1].Id));
            Assert.That(session.Map.Revision, Is.EqualTo(8));
            Tick();
            Assert.That(session.Bullets.Hits, Is.Empty);
            Assert.That(session.Effects.Active.Count, Is.EqualTo(2));
            Assert.That(session.Map.Revision, Is.EqualTo(8), "Effects cannot drill another layer");
        }

        [Test]
        public void SameTickSamePosition_IndependentBulletsAreNotDeduplicated()
        {
            Wall(); session = new ArenaSession(level, tank);
            for (int i = 0; i < 3; i++)
            {
                var weapon = new WeaponState(tank, session.Bullets);
                weapon.Step(new TankCommand(CardinalDirection.None, true), new Vector2(24, 24), CardinalDirection.Right);
            }
            Tick();
            Assert.That(session.Bullets.Hits.Count, Is.EqualTo(3));
            Assert.That(session.Effects.Active.Count, Is.EqualTo(3));
            Assert.That(session.Effects.Active[0].Position, Is.EqualTo(session.Bullets.Hits[0].Position));
            Assert.That(session.Effects.Active[1].Position, Is.EqualTo(session.Effects.Active[0].Position));
            Assert.That(session.Map.Revision, Is.EqualTo(4), "One old sweep snapshot, not three layers");
            Tick();
            Assert.That(session.Effects.Active.Count, Is.EqualTo(3));
            Assert.That(session.Map.Revision, Is.EqualTo(4));
        }

        [TestCase(TerrainKind.Brick)] [TestCase(TerrainKind.Steel)]
        public void TerrainHit_ExpiresAfterTwelveTicksAndReusesResetLease(TerrainKind terrain)
        {
            Wall(terrain); session = new ArenaSession(level, tank);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire);
            var first = session.Effects.Active[0]; long id = first.Id;
            Tick(3); Assert.That(first.Frame, Is.Zero);
            Tick(); Assert.That(first.Frame, Is.EqualTo(1));
            Tick(4); Assert.That(first.Frame, Is.EqualTo(2));
            Tick(3); Assert.That(session.Effects.Active.Count, Is.EqualTo(1));
            Tick(); Assert.That(session.Effects.Active, Is.Empty);
            Assert.That(first.Id, Is.Zero);
            session.Effects.PlayExplosion(new Vector2(90, 91));
            Assert.That(session.Effects.Active[0], Is.SameAs(first));
            Assert.That(first.Id, Is.GreaterThan(id));
            Assert.That(first.AgeTicks, Is.Zero);
            Assert.That(first.Kind, Is.EqualTo(CombatEffectKind.Explosion));
            Assert.That(first.Position, Is.EqualTo(new Vector2(90, 91)));
        }

        [Test]
        public void Explosion_IsPresentationOnlyAndExpiresWithoutRendering()
        {
            session = new ArenaSession(level, tank);
            session.Effects.PlayExplosion(session.Player.Position);
            Tick(16);
            Assert.That(session.Effects.Active[0].Frame, Is.EqualTo(4));
            Tick(4);
            Assert.That(session.Effects.Active, Is.Empty);
            Assert.That(session.Map.Revision, Is.Zero);
            Assert.That(session.Bullets.Hits, Is.Empty);
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(24, 24)));
        }

        [Test]
        public void PauseAndFocusLoss_FreezeAgeAndNeverReplayLastHit()
        {
            Wall(); session = new ArenaSession(level, tank);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire);
            var effect = session.Effects.Active[0]; long tick = session.Clock.Tick;
            session.SetFocus(false);
            for (int i = 0; i < 5; i++) session.AdvanceFrame(2, PlayerButtons.Fire);
            Assert.That(session.Clock.Tick, Is.EqualTo(tick));
            Assert.That(effect.AgeTicks, Is.Zero);
            Assert.That(session.Effects.Active.Count, Is.EqualTo(1));
            session.SetFocus(true); session.SetPaused(false); Tick();
            Assert.That(effect.AgeTicks, Is.EqualTo(1));
            Assert.That(session.Effects.Active.Count, Is.EqualTo(1));
            Assert.That(session.Map.Revision, Is.EqualTo(4));
        }

        [Test]
        public void ClearWhilePaused_DoesNotReplayOldHitsAndFreshSessionIsEmpty()
        {
            Wall(); session = new ArenaSession(level, tank);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire);
            var old = session.Effects.Active[0];
            session.SetPaused(true); session.Effects.Clear(); session.Effects.Clear();
            Assert.That(old.Id, Is.Zero);
            session.AdvanceFrame(1, PlayerButtons.None);
            Assert.That(session.Effects.Active, Is.Empty);
            session.SetPaused(false); Tick();
            Assert.That(session.Effects.Active, Is.Empty);
            Assert.That(new ArenaSession(level, tank).Effects.Active, Is.Empty);
        }

        [Test]
        public void BoundaryAndEnemyBulletPair_EachProduceOneSmallEffect()
        {
            session = new ArenaSession(level, tank);
            var boundary = new WeaponState(tank, session.Bullets);
            boundary.Step(new TankCommand(CardinalDirection.None, true), new Vector2(14, 60), CardinalDirection.Left);
            var a = new WeaponState(tank, session.Bullets, CombatTeam.Player, 2);
            var b = new WeaponState(tank, session.Bullets, CombatTeam.Enemy, 3);
            a.Step(new TankCommand(CardinalDirection.None, true), new Vector2(60, 100), CardinalDirection.Right);
            b.Step(new TankCommand(CardinalDirection.None, true), new Vector2(140, 100), CardinalDirection.Left);
            Tick();
            Assert.That(session.Bullets.Hits.Count, Is.EqualTo(2));
            Assert.That(session.Effects.Active.Count, Is.EqualTo(2));
            Assert.That(session.Bullets.Active, Is.Empty);
            Assert.That(a.ActiveBullets + b.ActiveBullets + boundary.ActiveBullets, Is.Zero);
        }

        [Test]
        public void NonlethalTankHit_IsNotAutomaticallyADeathExplosion()
        {
            tank.hitPoints = 2;
            session = new ArenaSession(level, tank);
            var enemy = new WeaponState(tank, session.Bullets, CombatTeam.Enemy, 2);
            enemy.Step(new TankCommand(CardinalDirection.None, true), new Vector2(24, 100), CardinalDirection.Down);
            Tick();
            Assert.That(session.Bullets.Hits[0].Kind, Is.EqualTo(BulletHitKind.Tank));
            Assert.That(session.PlayerTank.HitPoints, Is.EqualTo(1));
            Assert.That(session.PlayerTank.IsAlive, Is.True);
            Assert.That(session.Effects.Active.Count, Is.EqualTo(1));
            Assert.That(session.Effects.Active[0].Kind, Is.EqualTo(CombatEffectKind.Hit));
            Tick(12);
            Assert.That(session.Effects.Active, Is.Empty);
            Assert.That(session.Bullets.Hits, Is.Empty);
            Assert.That(enemy.ActiveBullets, Is.Zero);
        }
    }
}
