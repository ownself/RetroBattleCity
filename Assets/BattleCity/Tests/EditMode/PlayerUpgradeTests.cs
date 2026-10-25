using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC302")]
    public sealed class PlayerUpgradeTests
    {
        private LevelDefinition level;
        private PlayerUpgradeDefinition upgrades;
        private TankDefinition[] definitions;
        private EnemyWaveDefinition wave;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            definitions = new TankDefinition[4];
            for (int i = 0; i < definitions.Length; i++)
            {
                var tank = definitions[i] = ScriptableObject.CreateInstance<TankDefinition>();
                tank.definitionId = "player.level" + i; tank.spritePrefix = "tank.yellow.player.level" + i;
                tank.bulletSpeedPixelsPerSecond = i == 0 ? 180 : 300;
                tank.maxActiveBullets = i < 2 ? 1 : 2; tank.canDestroySteel = i == 3;
            }
            upgrades = ScriptableObject.CreateInstance<PlayerUpgradeDefinition>(); upgrades.levels = definitions;
        }
        [TearDown] public void TearDown()
        {
            if (wave != null) Object.DestroyImmediate(wave);
            foreach (var tank in definitions) Object.DestroyImmediate(tank);
            Object.DestroyImmediate(upgrades); Object.DestroyImmediate(level);
        }
        private ArenaSession Session(PlayerLifeParameters life = null)
            => new ArenaSession(level, definitions[0], playerLifeParameters: life, playerUpgrades: upgrades);
        private static void Tick(ArenaSession s, int count = 1, PlayerButtons buttons = PlayerButtons.None)
        { for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, buttons); }
        private static void Upgrade(ArenaSession s, int count)
        { for (int i = 0; i < count; i++) Assert.That(s.TryUpgradePlayer(), Is.True); }
        private void HitPlayer(ArenaSession s)
        {
            var weapon = new WeaponState(definitions[0], s.Bullets, CombatTeam.Enemy, 999);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.Down, false, true),
                s.Player.Position + Vector2.up * 18, CardinalDirection.Down), Is.True);
            Tick(s);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void EachLevel_RealMovementSpeedBulletSpeedCooldownAndLeaseLimit(int target)
        {
            var s = Session(); Upgrade(s, target); Tick(s, 1, PlayerButtons.D | PlayerButtons.Fire);
            var bullet = s.Bullets.Active.Single();
            Assert.That(s.Player.Position.x, Is.EqualTo(25));
            Assert.That(bullet.SpeedPixelsPerSecond, Is.EqualTo(target == 0 ? 180 : 300));
            Assert.That(bullet.Position.x, Is.EqualTo(25 + WeaponState.MuzzleOffsetPixels + bullet.SpeedPixelsPerSecond / 60));
            Assert.That(bullet.CanDestroySteel, Is.EqualTo(target == 3));
            Assert.That(s.PlayerWeapon.CooldownRemainingTicks, Is.EqualTo(15));
            Tick(s, 30, PlayerButtons.Fire);
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.EqualTo(target < 2 ? 1 : 2));
            Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(target));
            Assert.That(s.PlayerUpgrades.SpritePrefix, Is.EqualTo("tank.yellow.player.level" + target));
            Assert.That(s.PlayerTank.HitPoints, Is.EqualTo(1)); Assert.That(s.Score, Is.Zero);
        }

        [Test] public void Upgrade_DoesNotRecreateWeaponResetCooldownOrMutateExistingBullets_SecondLeaseUsesNewProfile()
        {
            var s = Session(); var weapon = s.PlayerWeapon; Tick(s, 1, PlayerButtons.Fire);
            var first = s.Bullets.Active.Single(); long lease = first.Id;
            Upgrade(s, 2);
            Assert.That(s.PlayerWeapon, Is.SameAs(weapon)); Assert.That(first.Id, Is.EqualTo(lease));
            Assert.That(first.SpeedPixelsPerSecond, Is.EqualTo(180)); Assert.That(first.CanDestroySteel, Is.False);
            Assert.That(weapon.CooldownRemainingTicks, Is.EqualTo(15));
            Tick(s, 14, PlayerButtons.Fire); Assert.That(s.Bullets.Active.Count, Is.EqualTo(1));
            Tick(s, 1, PlayerButtons.Fire); Assert.That(s.Bullets.Active.Count, Is.EqualTo(2));
            Assert.That(s.Bullets.Active.Single(b => b.Id != lease).SpeedPixelsPerSecond, Is.EqualTo(300));
            Upgrade(s, 1);
            Assert.That(s.Bullets.Active.All(b => !b.CanDestroySteel), Is.True);
            Assert.That(s.TryUpgradePlayer(), Is.False); Assert.That(weapon.ActiveBullets, Is.EqualTo(2));
            Assert.That(weapon.CooldownRemainingTicks, Is.EqualTo(15));
            s.Bullets.Release(lease); Tick(s, 15, PlayerButtons.Fire);
            Assert.That(s.Bullets.Active.Count, Is.EqualTo(2));
            Assert.That(s.Bullets.Active.Count(b => b.CanDestroySteel), Is.EqualTo(1));
        }

        [Test] public void SessionSnapshotsAllProfilesAndSprites_NoAssetMutation_EnemyWeaponsNeverUpgrade()
        {
            string original = JsonUtility.ToJson(upgrades);
            var s = Session(); var enemy = s.AddTank(definitions[0], CombatTeam.Enemy, new Vector2(160, 160));
            definitions[3].bulletSpeedPixelsPerSecond = 1; definitions[3].maxActiveBullets = 8;
            definitions[3].spritePrefix = "missing"; definitions[3].canDestroySteel = false;
            Upgrade(s, 3);
            Assert.That(s.PlayerWeapon.Parameters.BulletSpeedPixelsPerSecond, Is.EqualTo(300));
            Assert.That(s.PlayerWeapon.Parameters.MaxActiveBullets, Is.EqualTo(2));
            Assert.That(s.PlayerWeapon.Parameters.CanDestroySteel, Is.True);
            Assert.That(s.PlayerUpgrades.SpritePrefix, Is.EqualTo("tank.yellow.player.level3"));
            Assert.That(enemy.Weapon.Parameters.BulletSpeedPixelsPerSecond, Is.EqualTo(180));
            Assert.That(enemy.Weapon.Parameters.CanDestroySteel, Is.False);
            Assert.That(JsonUtility.ToJson(upgrades), Is.EqualTo(original), "The table itself is never rewritten");
        }

        [Test] public void DeathImmediatelyResetsToZero_OldBulletRetainsCapability_RespawnClearsOnlyPlayerLeases()
        {
            definitions[3].bulletSpeedPixelsPerSecond = 1;
            var s = Session(new PlayerLifeParameters(3, 2, 3)); Upgrade(s, 3);
            Tick(s, 1, PlayerButtons.Fire); var old = s.Bullets.Active.Single(); long oldId = old.Id;
            HitPlayer(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.PlayerUpgrades.Level, Is.Zero);
            Assert.That(s.PlayerWeapon.Parameters.BulletSpeedPixelsPerSecond, Is.EqualTo(180));
            Assert.That(s.PlayerWeapon.Parameters.MaxActiveBullets, Is.EqualTo(1));
            Assert.That(s.PlayerWeapon.Parameters.CanDestroySteel, Is.False);
            Assert.That(old.CanDestroySteel, Is.True); Assert.That(old.SpeedPixelsPerSecond, Is.EqualTo(1));
            Assert.That(s.TryUpgradePlayer(), Is.False);
            Tick(s, 2); Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.PlayerUpgrades.Level, Is.Zero);
            Assert.That(s.PlayerLife.IsProtected, Is.True); Assert.That(s.PlayerWeapon.CooldownRemainingTicks, Is.Zero);
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero); Assert.That(s.Bullets.Release(oldId), Is.False);
            Tick(s, 1, PlayerButtons.Fire);
            Assert.That(s.Bullets.Active.Single().CanDestroySteel, Is.False);
            Assert.That(s.Bullets.Active.Single().SpeedPixelsPerSecond, Is.EqualTo(180));
        }

        [Test] public void LegacyUnboundPauseFocusReadyAndTerminalRejectUpgrade_ResumeAndShieldPermitIt()
        {
            Assert.That(new ArenaSession(level, definitions[0]).TryUpgradePlayer(), Is.False);
            var s = Session(); s.SetPaused(true); Assert.That(s.TryUpgradePlayer(), Is.False);
            s.SetFocus(false); s.SetFocus(true); Assert.That(s.TryUpgradePlayer(), Is.False);
            s.SetPaused(false); s.PlayerTank.IsProtected = true; Assert.That(s.TryUpgradePlayer(), Is.True);
            HitPlayer(s); Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(1));
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { definitions[0] };
            var managed = new ArenaSession(level, definitions[0], wave, enableSessionLifecycle: true, playerUpgrades: upgrades);
            Assert.That(managed.TryUpgradePlayer(), Is.False); managed.StartBattle();
            managed.AdvanceFrame(0, PlayerButtons.None); Assert.That(managed.TryUpgradePlayer(), Is.True);
            Tick(managed); Assert.That(managed.Phase, Is.EqualTo(SessionPhase.Won));
            Assert.That(managed.TryUpgradePlayer(), Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void FourDirections_SteelRequiresTopLevel_IsolatedCellConsumedWithoutPiercingDiagonalNeighbor(int target)
        {
            foreach (CardinalDirection direction in new[] { CardinalDirection.Up, CardinalDirection.Down, CardinalDirection.Left, CardinalDirection.Right })
            {
                level.terrain[12 * 26 + 12] = level.terrain[13 * 26 + 13] = TerrainKind.Steel;
                var map = new MapState(level); var world = new BulletWorld();
                var weapon = new WeaponState(definitions[target], world);
                var center = new Vector2(100, 100);
                weapon.Step(new TankCommand(direction, false, true), center - direction.Vector() * 18, direction);
                world.Step(map);
                Assert.That(world.Hits.Single().Kind, Is.EqualTo(BulletHitKind.Terrain));
                Assert.That(world.Hits.Single().CanDestroySteel, Is.EqualTo(target == 3));
                Assert.That(map.ApplyBulletHits(world.Hits), Is.EqualTo(target == 3 ? 4 : 0));
                Assert.That(map.ApplyBulletHits(world.Hits), Is.Zero);
                Assert.That(map.TerrainAt(12, 12), Is.EqualTo(target == 3 ? TerrainKind.Empty : TerrainKind.Steel));
                Assert.That(map.TerrainAt(13, 13), Is.EqualTo(TerrainKind.Steel));
                Assert.That(world.Active, Is.Empty); Assert.That(weapon.ActiveBullets, Is.Zero);
                Assert.That(map.Revision, Is.EqualTo(target == 3 ? 1 : 0));
            }
        }

        [Test] public void TwoSteelShotsSharePreDamageSnapshot_ImpactAndPoolReuseKeepCapabilitySnapshots()
        {
            level.terrain[12 * 26 + 12] = TerrainKind.Steel;
            var map = new MapState(level); var world = new BulletWorld();
            for (int i = 0; i < 2; i++)
            {
                var weapon = new WeaponState(definitions[3], world, CombatTeam.Player, i + 1);
                weapon.Step(new TankCommand(CardinalDirection.Up, false, true), new Vector2(100, 82), CardinalDirection.Up);
            }
            world.Step(map); var hits = world.Hits.ToArray();
            Assert.That(hits.Length, Is.EqualTo(2)); Assert.That(map.ApplyBulletHits(hits), Is.EqualTo(4));
            Assert.That(map.Revision, Is.EqualTo(1));
            var plain = new WeaponState(definitions[0], world);
            plain.Step(new TankCommand(CardinalDirection.Up, false, true), new Vector2(40, 40), CardinalDirection.Up);
            Assert.That(world.Active.Single().CanDestroySteel, Is.False);
            Assert.That(hits.All(h => h.CanDestroySteel), Is.True);
        }

        [TestCase("null")] [TestCase("length")] [TestCase("entry")] [TestCase("duplicate")]
        [TestCase("movement")] [TestCase("hp")] [TestCase("score")] [TestCase("damage")]
        public void InvalidTablesAreRejectedBeforeSessionCreation(string fault)
        {
            switch (fault)
            {
                case "null": upgrades.levels = null; break;
                case "length": upgrades.levels = new[] { definitions[0] }; break;
                case "entry": upgrades.levels = (TankDefinition[])definitions.Clone(); upgrades.levels[3] = null; break;
                case "duplicate": definitions[3].definitionId = definitions[0].definitionId; break;
                case "movement": definitions[3].moveSpeedPixelsPerSecond++; break;
                case "hp": definitions[3].hitPoints++; break;
                case "score": definitions[3].score++; break;
                case "damage": definitions[3].damageSpritePrefixes = new[] { definitions[3].spritePrefix }; break;
            }
            Assert.Throws<InvalidOperationException>(() => Session());
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void AuthoredProfiles_NormalTwoPixelsFastFour_SecondShotAfterCooldownWhileFirstStillFlying(int target)
        {
            var authored = AssetDatabase.LoadAssetAtPath<PlayerUpgradeDefinition>(PlayerUpgradeAuthoring.DefinitionPath);
            var s = new ArenaSession(level, authored.levels[0], playerUpgrades: authored);
            Upgrade(s, target);
            Tick(s, 1, PlayerButtons.Fire);
            var first = s.Bullets.Active.Single(); long firstId = first.Id;
            Assert.That(first.SpeedPixelsPerSecond, Is.EqualTo(target == 0 ? 120 : 240));
            Assert.That(first.Position.y - first.PreviousPosition.y, Is.EqualTo(target == 0 ? 2 : 4));
            Assert.That(first.CanDestroySteel, Is.EqualTo(target == 3));
            Tick(s, 14, PlayerButtons.Fire);
            Assert.That(s.PlayerWeapon.CooldownRemainingTicks, Is.EqualTo(1));
            Assert.That(s.Bullets.Active.Count, Is.EqualTo(1), "No instant double shot or cooldown bypass.");
            Tick(s, 1, PlayerButtons.Fire);
            Assert.That(s.Bullets.Active.Any(b => b.Id == firstId), Is.True, "First projectile must still be flying.");
            Assert.That(s.Bullets.Active.Count, Is.EqualTo(target < 2 ? 1 : 2));
            Tick(s, 15, PlayerButtons.Fire);
            Assert.That(s.Bullets.Active.Count, Is.EqualTo(target < 2 ? 1 : 2), "Capacity is not an automatic burst.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InitialCampaignTierRestoresProfileInReadyWithoutCombatOrAssetMutation(int target)
        {
            var authored = AssetDatabase.LoadAssetAtPath<PlayerUpgradeDefinition>(PlayerUpgradeAuthoring.DefinitionPath);
            string before = JsonUtility.ToJson(authored.levels[target]);
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { authored.levels[0] };
            var s = new ArenaSession(level, authored.levels[0], wave, enableSessionLifecycle: true,
                playerUpgrades: authored, initialPlayerUpgradeLevel: target);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Ready));
            Assert.That(s.Clock.Tick, Is.Zero); Assert.That(s.Clock.Paused, Is.True);
            Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(target));
            Assert.That(s.PlayerUpgrades.SpritePrefix, Is.EqualTo("tank.yellow.player.level" + target));
            Assert.That(s.PlayerWeapon.Parameters.BulletSpeedPixelsPerSecond, Is.EqualTo(target == 0 ? 120 : 240));
            Assert.That(s.PlayerWeapon.Parameters.MaxActiveBullets, Is.EqualTo(target < 2 ? 1 : 2));
            Assert.That(s.PlayerWeapon.Parameters.CanDestroySteel, Is.EqualTo(target == 3));
            Assert.That(s.TryUpgradePlayer(), Is.False, "Ready still rejects gameplay upgrades.");
            Assert.That(s.PlayerWeapon.CooldownRemainingTicks, Is.Zero);
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero); Assert.That(s.Bullets.Active, Is.Empty);
            Assert.That(s.Score, Is.Zero); Assert.That(s.Result, Is.Null);
            Assert.That(JsonUtility.ToJson(authored.levels[target]), Is.EqualTo(before));
        }

        [TestCase(-1)] [TestCase(4)] [TestCase(int.MaxValue)]
        public void InvalidInitialCampaignTierIsRejected(int target)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaSession(level, definitions[0],
                playerUpgrades: upgrades, initialPlayerUpgradeLevel: target));
        }

        [Test] public void NonzeroInitialCampaignTierRequiresUpgradeTable()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaSession(level, definitions[0], initialPlayerUpgradeLevel: 1));
        }

        [Test] public void BaseDefinitionMismatchIsRejected()
        {
            var other = Object.Instantiate(definitions[0]);
            try { other.maxActiveBullets = 2; Assert.Throws<InvalidOperationException>(() => upgrades.ValidateFor(other)); }
            finally { Object.DestroyImmediate(other); }
        }

        [Test] public void AuthoredDefaultsAndAll32SpritesResolve_MissingUpgradeFrameFailsEarly()
        {
            var authored = AssetDatabase.LoadAssetAtPath<PlayerUpgradeDefinition>(PlayerUpgradeAuthoring.DefinitionPath);
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            Assert.That(authored, Is.Not.Null); authored.ValidateFor(authored.levels[0]); authored.ValidateSprites(catalog);
            Assert.That(authored.levels[0], Is.SameAs(AssetDatabase.LoadAssetAtPath<TankDefinition>(PlayerUpgradeAuthoring.BasePath)));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(authored.levels[i].requiresCalibration, Is.True);
                Assert.That(authored.levels[i].bulletSpeedPixelsPerSecond, Is.EqualTo(i == 0 ? 120 : 240));
                Assert.That(authored.levels[i].fireCooldownTicks, Is.EqualTo(15));
                Assert.That(authored.levels[i].maxActiveBullets, Is.EqualTo(i < 2 ? 1 : 2));
                Assert.That(authored.levels[i].canDestroySteel, Is.EqualTo(i == 3));
            }
            upgrades.ValidateSprites(catalog);
            definitions[3].spritePrefix = "tank.missing.player.level3";
            Assert.Throws<KeyNotFoundException>(() => upgrades.ValidateSprites(catalog));
        }
    }
}
