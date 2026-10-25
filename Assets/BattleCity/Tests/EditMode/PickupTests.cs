using System;
using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC303")]
    public sealed class PickupTests
    {
        private LevelDefinition level;
        private TankDefinition player, enemy;
        private EnemyWaveDefinition wave;
        private PickupDefinition rules;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            player = ScriptableObject.CreateInstance<TankDefinition>();
            enemy = ScriptableObject.CreateInstance<TankDefinition>();
            enemy.definitionId = "enemy.armor"; enemy.spritePrefix = "tank.white.enemy.armor"; enemy.hitPoints = 4; enemy.score = 400;
            level.enemySequence = new[] { enemy.definitionId, enemy.definitionId };
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            wave.tankDefinitions = new[] { enemy }; wave.preparationTicks = 1; wave.spawnIntervalTicks = 900;
            rules = ScriptableObject.CreateInstance<PickupDefinition>(); rules.carrierSequenceIndices = new[] { 0 };
            rules.collectionScore = 0; // Framework tests use a zero-reward policy; BC308 covers authored rewards.
        }
        [TearDown] public void TearDown()
        { foreach (var asset in new Object[] { rules, wave, enemy, player, level }) Object.DestroyImmediate(asset); }
        private ArenaSession Session(bool managed = false, bool enabled = true, PlayerLifeParameters life = null)
            => new ArenaSession(level, player, wave, playerLifeParameters: life, enableSessionLifecycle: managed, pickups: enabled ? rules : null);
        private static void Tick(ArenaSession s, int count = 1)
        { for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); }
        private void Hit(ArenaSession s, TankState target, int count = 1, CombatTeam team = CombatTeam.Player)
        {
            s.Bullets.Clear();
            for (int i = 0; i < count; i++)
            {
                var gun = new WeaponState(player, s.Bullets, team, 999 + i);
                Assert.That(gun.Step(new TankCommand(CardinalDirection.Up, false, true), target.Motor.Position - Vector2.up * 18, CardinalDirection.Up), Is.True);
            }
            Tick(s);
        }
        public static Rect[] ExcludeAllBut(Vector2 p) => new[] {
            new Rect(-1000, -1000, p.x - 8 + 1000, 2000), new Rect(p.x + 8, -1000, 2000, 2000),
            new Rect(-1000, -1000, 2000, p.y - 8 + 1000), new Rect(-1000, p.y + 8, 2000, 2000) };

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void InvalidRulesFailEarly(int bad)
        {
            switch (bad)
            {
                case 0: rules.seed = 0; break;
                case 1: rules.carrierSequenceIndices = null; break;
                case 2: rules.carrierSequenceIndices = new[] { -1 }; break;
                case 3: rules.carrierSequenceIndices = new[] { 0, 0 }; break;
                case 4: rules.carrierSequenceIndices = new[] { 2 }; break;
                case 5: rules.requiresCalibration = false; rules.originalRulesSource = " "; break;
            }
            Assert.Throws<InvalidOperationException>(() => new PickupState(rules, level));
        }
        [Test] public void MissingWaveIsRejected_OptOutRetainsLegacy()
        {
            Assert.Throws<ArgumentException>(() => new ArenaSession(level, player, pickups: rules));
            Assert.That(new ArenaSession(level, player).Pickups, Is.Null);
            Assert.That(Session(enabled: false).Pickups, Is.Null);
        }
        [Test] public void AuthoredData_HasThreeExplicitCarriersSixSpritesAndCompleteRedFamilies_NoGunOrEffects()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PickupDefinition>(PickupAuthoring.DefinitionPath);
            var realLevel = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Stage01Authoring.LevelPath);
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            Assert.That(asset, Is.Not.Null); asset.ValidateFor(realLevel); asset.ValidateSprites(catalog);
            Assert.That(asset.carrierSequenceIndices, Is.EqualTo(new[] { 3, 10, 17 }));
            Assert.That(asset.requiresCalibration, Is.True); Assert.That(Enum.GetValues(typeof(PickupKind)).Length, Is.EqualTo(6));
            var roster = AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>(EnemyBalanceAuthoring.BalancePath);
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind))) PickupDefinition.ValidateCarrierSprites(roster.Get(kind), catalog);
            enemy.spritePrefix = "custom.enemy";
            Assert.Throws<InvalidOperationException>(() => PickupDefinition.CarrierPrefix(enemy));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupDefinition.SpriteId((PickupKind)99));
        }
        [Test] public void SnapshotAndIndependentRng_ReproducibleDrops_ValidReplacementGetsNewIdentity()
        {
            var a = new PickupState(rules, level); var b = new PickupState(rules, level);
            rules.seed = 123; rules.carrierSequenceIndices[0] = 1;
            Assert.That(a.IsCarrierIndex(0), Is.True); Assert.That(a.IsCarrierIndex(1), Is.False);
            var map = new MapState(level);
            for (int i = 1; i <= 3; i++)
            {
                Assert.That(a.TryDrop(map, level.playerSpawnPixels, true, null), Is.True);
                Assert.That(b.TryDrop(map, level.playerSpawnPixels, true, null), Is.True);
                Assert.That(a.Active.Value.Id, Is.EqualTo(i)); Assert.That(a.Active.Value.Kind, Is.EqualTo(b.Active.Value.Kind));
                Assert.That(a.Active.Value.Position, Is.EqualTo(b.Active.Value.Position)); Assert.That(a.RandomState, Is.EqualTo(b.RandomState));
            }
            Assert.That(a.SpawnedCount, Is.EqualTo(3)); Assert.That(a.ReplacedCount, Is.EqualTo(2));
        }
        [Test] public void CollectionIsStrictOverlapAliveOnly_ReceiptOnce_DifferentItemsCanBeCollected()
        {
            var state = new PickupState(rules, level); var map = new MapState(level);
            for (int i = 1; i <= 2; i++)
            {
                state.TryDrop(map, level.playerSpawnPixels, true, null); var item = state.Active.Value;
                Assert.That(state.TryCollect(new Rect(item.Position.x + 8, item.Position.y - 8, 16, 16), true, 10), Is.False, "Edge touch is not overlap");
                Assert.That(state.TryCollect(item.Bounds, false, 10), Is.False);
                Assert.That(state.TryCollect(item.Bounds, true, 11), Is.True);
                Assert.That(state.TryCollect(item.Bounds, true, 12), Is.False);
                Assert.That(state.Active, Is.Null); Assert.That(state.CollectedCount, Is.EqualTo(i));
                Assert.That(state.LastCollected.Value.Item.Id, Is.EqualTo(i)); Assert.That(state.LastCollected.Value.Tick, Is.EqualTo(11));
            }
        }
        [Test] public void UnreachableRoomsWaterSteelAndBrickNeverReceivePickup_FractionalPlayerConnects()
        {
            for (int y = 0; y < 26; y++) level.terrain[y * 26 + 12] = TerrainKind.Steel;
            level.terrain[6 * 26 + 4] = TerrainKind.Water; level.terrain[5 * 26 + 5] = TerrainKind.Brick;
            var state = new PickupState(rules, level); var map = new MapState(level);
            var source = new Vector2(24.5f, 24.25f);
            for (int i = 0; i < 30; i++)
            {
                Assert.That(state.TryDrop(map, source, true, null), Is.True);
                Vector2 point = state.Active.Value.Position;
                Assert.That(point.x, Is.LessThanOrEqualTo(88)); Assert.That(map.CanOccupy(point), Is.True);
                Assert.That(point.x % 8, Is.Zero); Assert.That(point.y % 8, Is.Zero);
            }
        }
        [Test] public void BaseBlocksRoute_DestinationExclusionsCoverPlayerLiveTanksAndReservations()
        {
            var state = new PickupState(rules, level); var map = new MapState(level);
            // Static base slab disconnects the map although the underlying terrain is empty.
            var slab = new Rect(80, 0, 16, 208);
            for (int i = 0; i < 12; i++)
            {
                state.TryDrop(map, level.playerSpawnPixels, true, new[] { new Rect(0, 0, 32, 208) }, slab);
                Assert.That(state.Active.Value.Position.x, Is.InRange(40, 72));
            }
            Vector2 only = new Vector2(48, 24);
            Assert.That(state.TryDrop(map, level.playerSpawnPixels, true, ExcludeAllBut(only)), Is.True);
            Assert.That(state.Active.Value.Position, Is.EqualTo(only));
        }
        [Test] public void NoCandidateOrDeadPlayer_PreservesSlotAndRng_ConsumesOpportunity_NoRetryLoop()
        {
            var state = new PickupState(rules, level); var map = new MapState(level);
            state.TryDrop(map, level.playerSpawnPixels, true, null);
            var old = state.Active.Value; uint random = state.RandomState;
            Assert.That(state.TryDrop(map, level.playerSpawnPixels, true, new[] { new Rect(0, 0, 208, 208) }), Is.False);
            Assert.That(state.TryDrop(map, level.playerSpawnPixels, false, null), Is.False);
            Assert.That(state.Active.Value.Id, Is.EqualTo(old.Id)); Assert.That(state.RandomState, Is.EqualTo(random));
            Assert.That(state.DropAttemptCount, Is.EqualTo(3)); Assert.That(state.RejectedDropCount, Is.EqualTo(2));
            state.ClearActive(); Assert.That(state.Active, Is.Null);
        }
        [Test] public void FirstAcceptedCarrierHitDropsBeforeDeath_ProtectedAndFriendlyHitsDoNotConsumeIt()
        {
            var s = Session(); Tick(s, 2); var carrier = s.Tanks.Single(t => t.Team == CombatTeam.Enemy);
            Assert.That(carrier.CarriesPickup, Is.True); carrier.IsProtected = true;
            Hit(s, carrier); Assert.That(carrier.HitPoints, Is.EqualTo(4)); Assert.That(s.Pickups.DropAttemptCount, Is.Zero);
            carrier.IsProtected = false; Hit(s, carrier, team: CombatTeam.Enemy);
            Assert.That(carrier.CarriesPickup, Is.True); Assert.That(s.Pickups.DropAttemptCount, Is.Zero);
            Hit(s, carrier); Assert.That(carrier.HitPoints, Is.EqualTo(3)); Assert.That(carrier.CarriesPickup, Is.False);
            Assert.That(s.Pickups.Active, Is.Not.Null); Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1)); Assert.That(s.Score, Is.Zero);
            Hit(s, carrier, 6);
            Assert.That(carrier.IsAlive, Is.False); Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1));
            Assert.That(s.Score, Is.EqualTo(400)); Assert.That(s.DefeatedEnemyCount, Is.EqualTo(1));
        }
        [Test] public void LethalOverkillDropsOnce_ManualEnemyNeverGetsCarrierFlag()
        {
            enemy.hitPoints = 1; var s = Session(); Tick(s, 2);
            var carrier = s.Tanks.Single(t => t.Team == CombatTeam.Enemy); Hit(s, carrier, 4);
            Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1)); Assert.That(s.Score, Is.EqualTo(400));
            var manual = s.AddTank(enemy, CombatTeam.Enemy, new Vector2(100, 100));
            Assert.That(manual.CarriesPickup, Is.False); Hit(s, manual);
            Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1)); Assert.That(s.Score, Is.EqualTo(800));
        }
        [Test] public void ReadyPauseAndFocusDoNotCollect_ResumeCollectsOnceWithoutScoreOrEffects()
        {
            var s = Session(managed: true);
            Vector2 point = new Vector2(40, 24);
            s.Pickups.TryDrop(s.Map, s.Player.Position, true, ExcludeAllBut(point));
            s.Player.Step(CardinalDirection.Right, 960, s.Map);
            Tick(s); Assert.That(s.Pickups.CollectedCount, Is.Zero);
            s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); s.SetFocus(false); Tick(s);
            Assert.That(s.Pickups.CollectedCount, Is.Zero);
            s.SetFocus(true); s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None); Tick(s);
            Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Assert.That(s.Pickups.Active, Is.Null);
            Tick(s, 3); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1)); Assert.That(s.Score, Is.Zero);
            Assert.That(s.PlayerTank.HitPoints, Is.EqualTo(1)); Assert.That(s.PlayerUpgrades, Is.Null);
        }
        [Test] public void DeadPlayerCannotCollect_PickupSurvivesDeath_ProtectedRespawnCanCollect()
        {
            var s = Session(life: new PlayerLifeParameters(3, 2, 3));
            Assert.That(s.Pickups.TryDrop(s.Map, s.Player.Position, true, ExcludeAllBut(s.Player.Position)), Is.True);
            var gun = new WeaponState(player, s.Bullets, CombatTeam.Enemy, 999);
            gun.Step(new TankCommand(CardinalDirection.Down, false, true), s.Player.Position + Vector2.up * 18, CardinalDirection.Down);
            Tick(s); Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.Pickups.Active, Is.Not.Null);
            Assert.That(s.Pickups.CollectedCount, Is.Zero);
            Tick(s, 2); Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.PlayerLife.IsProtected, Is.True);
            Assert.That(s.Pickups.Active, Is.Null); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1));
        }
        [Test] public void SessionDeadPlayerRejectsCarrierDropWithoutRetryingAfterRespawn()
        {
            var s = Session(life: new PlayerLifeParameters(3, 2, 3)); Tick(s, 2);
            var carrier = s.Tanks.Single(t => t.CarriesPickup); s.Bullets.Clear();
            var attack = new WeaponState(player, s.Bullets, CombatTeam.Player, 999);
            attack.Step(new TankCommand(CardinalDirection.Up, false, true), carrier.Motor.Position - Vector2.up * 18, CardinalDirection.Up);
            var danger = new WeaponState(player, s.Bullets, CombatTeam.Enemy, 1000);
            danger.Step(new TankCommand(CardinalDirection.Down, false, true), s.Player.Position + Vector2.up * 18, CardinalDirection.Down);
            Tick(s); Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(carrier.HitPoints, Is.EqualTo(3));
            Assert.That(carrier.CarriesPickup, Is.False); Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1));
            Assert.That(s.Pickups.RejectedDropCount, Is.EqualTo(1)); Assert.That(s.Pickups.Active, Is.Null);
            Tick(s, 2); Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1));
            Assert.That(s.Pickups.Active, Is.Null);
        }
        [Test] public void LastEnemyTerminalTickClearsSlotAndSkipsNewDropOrCollection()
        {
            level.enemySequence = new[] { enemy.definitionId }; enemy.hitPoints = 1;
            var s = Session(managed: true); s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); Tick(s, 2);
            s.Pickups.TryDrop(s.Map, s.Player.Position, true, null);
            var carrier = s.Tanks.Single(t => t.Team == CombatTeam.Enemy); Hit(s, carrier);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won)); Assert.That(s.Pickups.Active, Is.Null);
            Assert.That(s.Pickups.DropAttemptCount, Is.EqualTo(1), "Only the fixture's initial drop, not the terminal hit");
            Assert.That(s.Pickups.CollectedCount, Is.Zero); Tick(s, 5); Assert.That(s.Pickups.Active, Is.Null);
        }
        [Test] public void UnrelatedWaveAndAiAreUnaffectedByIndependentDropRandomness()
        {
            var a = Session(); var b = Session(enabled: false); Tick(a, 2); Tick(b, 2);
            a.Pickups.TryDrop(a.Map, a.Player.Position, true, null);
            Tick(a, 25); Tick(b, 25);
            Assert.That(a.EnemyWave.RandomState, Is.EqualTo(b.EnemyWave.RandomState));
            Assert.That(a.EnemyWave.SpawnedCount, Is.EqualTo(b.EnemyWave.SpawnedCount));
            Assert.That(a.Tanks[1].Motor.Position, Is.EqualTo(b.Tanks[1].Motor.Position));
            Assert.That(a.Tanks[1].Motor.Facing, Is.EqualTo(b.Tanks[1].Motor.Facing));
        }
    }
}
