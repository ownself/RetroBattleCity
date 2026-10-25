using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC304")]
    public sealed class PlayerPickupTests
    {
        private LevelDefinition level;
        private TankDefinition[] tanks;
        private PlayerUpgradeDefinition upgrades;
        private EnemyWaveDefinition wave;
        private PickupDefinition rules;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            tanks = new TankDefinition[4];
            for (int i = 0; i < 4; i++)
            {
                tanks[i] = ScriptableObject.CreateInstance<TankDefinition>();
                tanks[i].definitionId = "player.level" + i;
                tanks[i].spritePrefix = "tank.yellow.player.level" + i;
                tanks[i].bulletSpeedPixelsPerSecond = i == 0 ? 180 : 300;
                tanks[i].maxActiveBullets = i < 2 ? 1 : 2; tanks[i].canDestroySteel = i == 3;
            }
            upgrades = ScriptableObject.CreateInstance<PlayerUpgradeDefinition>(); upgrades.levels = tanks;
            level.enemySequence = new[] { tanks[0].definitionId, tanks[0].definitionId };
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            wave.tankDefinitions = new[] { tanks[0] }; wave.preparationTicks = 900; wave.spawnIntervalTicks = 900;
            rules = ScriptableObject.CreateInstance<PickupDefinition>();
            rules.collectionScore = 0; // Isolate BC304 effects; default reward accounting is covered by BC308.
            rules.carrierSequenceIndices = new int[0]; rules.helmetDurationTicks = 3; rules.maximumLives = 5;
        }
        [TearDown] public void TearDown()
        {
            foreach (var t in tanks) Object.DestroyImmediate(t);
            Object.DestroyImmediate(level); Object.DestroyImmediate(upgrades); Object.DestroyImmediate(wave); Object.DestroyImmediate(rules);
        }
        private ArenaSession Session(bool managed = false, bool capabilities = true, int lives = 3)
            => new ArenaSession(level, tanks[0], wave,
                playerLifeParameters: capabilities ? new PlayerLifeParameters(lives, 2, 4) : null,
                enableSessionLifecycle: managed, playerUpgrades: capabilities ? upgrades : null, pickups: rules);
        private static void Tick(ArenaSession s, int count = 1)
        { for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); }
        // Use the real drop algorithm/ledger, not reflection, fake receipts or a production spawn shortcut.
        private static void Drop(ArenaSession s, PickupKind kind, Vector2? destination = null)
        {
            for (int i = 0; i < 128; i++)
            {
                Assert.That(s.Pickups.TryDrop(s.Map, s.Player.Position, true,
                    PickupTests.ExcludeAllBut(destination ?? s.Player.Position), s.Base?.Bounds), Is.True);
                if (s.Pickups.Active.Value.Kind == kind) return;
            }
            Assert.Fail("Bounded deterministic drop search failed for " + kind);
        }
        private void Collect(ArenaSession s, PickupKind kind) { Drop(s, kind); Tick(s); }
        private void ShootPlayer(ArenaSession s, int count = 1)
        {
            s.Bullets.Clear();
            for (int i = 0; i < count; i++)
            {
                var gun = new WeaponState(tanks[0], s.Bullets, CombatTeam.Enemy, 900 + i);
                Assert.That(gun.Step(new TankCommand(CardinalDirection.Down, false, true),
                    s.Player.Position + Vector2.up * 18, CardinalDirection.Down), Is.True);
            }
            Tick(s);
        }

        [Test] public void Star_OneLevelPerNewReceipt_CapsAtThree_NoReplayOrScore()
        {
            var s = Session();
            for (int i = 1; i <= 5; i++)
            {
                Collect(s, PickupKind.Star); Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(Math.Min(i, 3)));
                Assert.That(s.Pickups.CollectedCount, Is.EqualTo(i)); Assert.That(s.Pickups.Active, Is.Null);
                Tick(s, 2); Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(Math.Min(i, 3)));
            }
            Assert.That(s.Score, Is.Zero); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3));
        }
        [Test] public void Star_PreservesExistingBulletSnapshotAndWeaponCooldown()
        {
            var s = Session(); s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
            var bullet = s.Bullets.Active.Single(); var weapon = s.PlayerWeapon;
            Collect(s, PickupKind.Star);
            Assert.That(s.PlayerWeapon, Is.SameAs(weapon)); Assert.That(weapon.CooldownRemainingTicks, Is.EqualTo(14));
            Assert.That(bullet.SpeedPixelsPerSecond, Is.EqualTo(180)); Assert.That(bullet.CanDestroySteel, Is.False);
            Assert.That(weapon.Parameters.BulletSpeedPixelsPerSecond, Is.EqualTo(300));
        }
        [Test] public void Star_DeathImmediatelyResetsLevel_RespawnStaysZero()
        {
            var s = Session(); Collect(s, PickupKind.Star); ShootPlayer(s, 3);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.PlayerUpgrades.Level, Is.Zero);
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2)); Tick(s, 2);
            Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.PlayerUpgrades.Level, Is.Zero);
        }
        [Test] public void Helmet_ProtectsExactlyFullFutureDamagePhases_ConsumesEveryIncomingBullet()
        {
            var s = Session(); Collect(s, PickupKind.Helmet);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(3)); Assert.That(s.PlayerIsProtected, Is.True);
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                ShootPlayer(s, 3); Assert.That(s.PlayerTank.IsAlive, Is.True);
                Assert.That(s.PlayerTank.HitPoints, Is.EqualTo(1)); Assert.That(s.Bullets.Active.Count, Is.Zero);
                Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(remaining));
            }
            Assert.That(s.PlayerIsProtected, Is.False); ShootPlayer(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2));
        }
        [Test] public void Helmet_RepeatRefreshesNotAdds_ReceiptDoesNotRefreshOnLaterTicks()
        {
            var s = Session(); Collect(s, PickupKind.Helmet); Tick(s);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(2)); Collect(s, PickupKind.Helmet);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(3)); Tick(s, 3);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.Zero); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(2));
        }
        [Test] public void Helmet_AndRespawnProtectionTickIndependently_UnionDoesNotShortenLongerProtection()
        {
            rules.helmetDurationTicks = 2;
            var s = Session(); ShootPlayer(s); Tick(s, 2);
            Assert.That(s.PlayerLife.ProtectionRemainingTicks, Is.EqualTo(4)); Collect(s, PickupKind.Helmet);
            Assert.That(s.PlayerLife.ProtectionRemainingTicks, Is.EqualTo(3));
            ShootPlayer(s); ShootPlayer(s);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.Zero); Assert.That(s.PlayerIsProtected, Is.True);
            ShootPlayer(s);
            Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.PlayerIsProtected, Is.False);
            ShootPlayer(s); Assert.That(s.PlayerTank.IsAlive, Is.False);
        }
        [Test] public void HelmetLongerThanRespawnProtection_ContinuesAfterRespawnShieldExpires()
        {
            rules.helmetDurationTicks = 8; var s = Session(); ShootPlayer(s); Tick(s, 2); Collect(s, PickupKind.Helmet);
            for (int i = 0; i < 8; i++) ShootPlayer(s);
            Assert.That(s.PlayerLife.IsProtected, Is.False); Assert.That(s.PlayerTank.IsAlive, Is.True);
            ShootPlayer(s); Assert.That(s.PlayerTank.IsAlive, Is.False);
        }
        [Test] public void Tank_IncreasesTotalLivesOnce_CapsAndConsumesFurtherItems_DeathSpendsOne()
        {
            var s = Session();
            for (int i = 1; i <= 4; i++)
            {
                Collect(s, PickupKind.Tank); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(Math.Min(3 + i, 5)));
                Tick(s); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(Math.Min(3 + i, 5)));
            }
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(4)); ShootPlayer(s, 3);
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(4)); Tick(s, 2);
            Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(4));
            Assert.That(s.Score, Is.Zero);
        }
        [TestCase(PickupKind.Star)] [TestCase(PickupKind.Helmet)] [TestCase(PickupKind.Tank)]
        public void LethalDamageBeforePickup_DeadPlayerCannotReceiveReward(PickupKind kind)
        {
            var s = Session(); Drop(s, kind); ShootPlayer(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.Pickups.CollectedCount, Is.Zero);
            Assert.That(s.Pickups.Active, Is.Not.Null); Assert.That(s.Pickups.HelmetRemainingTicks, Is.Zero);
            Assert.That(s.PlayerUpgrades.Level, Is.Zero); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2));
            Tick(s, 2); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1));
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(kind == PickupKind.Tank ? 3 : 2));
            Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(kind == PickupKind.Star ? 1 : 0));
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(kind == PickupKind.Helmet ? 3 : 0));
        }
        [TestCase(PickupKind.Star)] [TestCase(PickupKind.Helmet)] [TestCase(PickupKind.Tank)]
        public void ReadyPauseFocusFreezeCollectionAndHelmetTime(PickupKind kind)
        {
            var s = Session(managed: true); Drop(s, kind); Tick(s, 5); Assert.That(s.Pickups.CollectedCount, Is.Zero);
            s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); Tick(s);
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Collect(s, PickupKind.Helmet);
            s.SetPaused(true); Tick(s, 10); Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(3));
            s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); s.SetFocus(false); Tick(s, 10);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(3));
            s.SetFocus(true); s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); Tick(s);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(2));
        }
        [TestCase(PickupKind.Clock)] [TestCase(PickupKind.Grenade)] [TestCase(PickupKind.Shovel)]
        public void FutureEffectsRemainLedgerOnly(PickupKind kind)
        {
            var s = Session(); Collect(s, kind);
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Assert.That(s.PlayerUpgrades.Level, Is.Zero);
            Assert.That(s.Pickups.HelmetRemainingTicks, Is.Zero); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3));
            Assert.That(s.Score, Is.Zero);
        }
        [Test] public void OptionalCapabilitiesRemainOptional_HelmetWorksWithoutPlayerLife()
        {
            var s = Session(capabilities: false); Collect(s, PickupKind.Star); Collect(s, PickupKind.Tank);
            Assert.That(s.PlayerUpgrades, Is.Null); Assert.That(s.PlayerLife, Is.Null);
            Collect(s, PickupKind.Helmet); ShootPlayer(s); Assert.That(s.PlayerTank.IsAlive, Is.True);
        }
        [TestCase(0, 5)] [TestCase(-1, 5)] [TestCase(3, 0)] [TestCase(3, -1)]
        public void InvalidEffectRulesRejected(int duration, int cap)
        {
            rules.helmetDurationTicks = duration; rules.maximumLives = cap;
            Assert.Throws<InvalidOperationException>(() => Session());
        }
        [Test] public void SnapshotEffectRules_DoNotFollowMutableAsset()
        {
            var s = Session(); rules.helmetDurationTicks = 90; rules.maximumLives = 99;
            Collect(s, PickupKind.Helmet); Assert.That(s.Pickups.HelmetRemainingTicks, Is.EqualTo(3));
            for (int i = 0; i < 4; i++) Collect(s, PickupKind.Tank);
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(5));
        }
        [Test] public void AuthoredEffectDefaultsAreExplicitAndUncalibrated()
        {
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<PickupDefinition>(BattleCity.Editor.PickupAuthoring.DefinitionPath);
            Assert.That(authored, Is.Not.Null); authored.Validate();
            Assert.That(authored.helmetDurationTicks, Is.EqualTo(600)); Assert.That(authored.maximumLives, Is.EqualTo(9));
            Assert.That(authored.requiresCalibration, Is.True);
        }
        [Test] public void LifeRewardRejectsDeadExhaustedAndOverflow_NeverTruncatesHigherInitialLives()
        {
            var life = new PlayerLifeState(new PlayerLifeParameters(int.MaxValue, 1, 1));
            Assert.That(life.TryAddLife(9), Is.False); Assert.That(life.TryAddLife(int.MaxValue), Is.False);
            Assert.That(life.RemainingLives, Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => life.TryAddLife(0));
            life = new PlayerLifeState(new PlayerLifeParameters(1, 1, 1)); life.Step(false, () => false);
            Assert.That(life.TryAddLife(9), Is.False); Assert.That(life.IsExhausted, Is.True);
        }
        [Test] public void TerminalTickClearsHelmetAndSlot_DoesNotApplyTankReward()
        {
            // Base death terminates even if the player is protected and touching a life reward.
            level.baseCenterPixels = new Vector2Int(104, 24);
            var s = new ArenaSession(level, tanks[0], wave, true, new PlayerLifeParameters(3, 2, 4), true, upgrades, rules);
            s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); Collect(s, PickupKind.Helmet); Drop(s, PickupKind.Tank);
            var gun = new WeaponState(tanks[0], s.Bullets, CombatTeam.Enemy, 999);
            gun.Step(new TankCommand(CardinalDirection.Down, false, true), s.Base.Position + Vector2.up * 18, CardinalDirection.Down);
            Tick(s); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Lost)); Assert.That(s.Pickups.HelmetRemainingTicks, Is.Zero);
            Assert.That(s.Pickups.Active, Is.Null); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1));
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3));
        }
    }
}
