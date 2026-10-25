using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC309")]
    public sealed class ClassicRulesCombinationTests
    {
        private LevelDefinition level;
        private TankDefinition player, basic, armor;
        private EnemyWaveDefinition wave;
        private PickupDefinition rules;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>(); level.playerSpawnPixels = new Vector2Int(24, 24);
            level.baseCenterPixels = new Vector2Int(104, 24); level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            player = ScriptableObject.CreateInstance<TankDefinition>();
            basic = ScriptableObject.CreateInstance<TankDefinition>(); basic.definitionId = "enemy.basic"; basic.score = 100;
            armor = ScriptableObject.CreateInstance<TankDefinition>(); armor.definitionId = "enemy.armor"; armor.score = 400; armor.hitPoints = 4;
            level.enemySequence = new[] { basic.definitionId };
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { basic, armor };
            wave.preparationTicks = 3000; wave.spawnIntervalTicks = 3000;
            rules = ScriptableObject.CreateInstance<PickupDefinition>(); rules.carrierSequenceIndices = new int[0];
            rules.clockDurationTicks = 3; rules.helmetDurationTicks = 3; rules.shovelDurationTicks = 3;
        }
        [TearDown] public void TearDown()
        { foreach (var o in new Object[] { level, player, basic, armor, wave, rules }) Object.DestroyImmediate(o); }
        private ArenaSession Session(bool managed = false, int lives = 3)
            => new ArenaSession(level, player, wave, true, new PlayerLifeParameters(lives, 2, 2), managed, pickups: rules);
        private static void Start(ArenaSession s) { s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); }
        private static void Tick(ArenaSession s, int count = 1, PlayerButtons buttons = PlayerButtons.None)
        { for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, buttons); }
        public static void Drop(ArenaSession s, PickupKind kind)
        {
            for (int i = 0; i < 128; i++)
            {
                Assert.That(s.Pickups.TryDrop(s.Map, s.Player.Position, true, PickupTests.ExcludeAllBut(s.Player.Position), s.Base?.Bounds), Is.True);
                if (s.Pickups.Active.Value.Kind == kind) return;
            }
            Assert.Fail("Bounded deterministic pickup search failed");
        }
        private static void Collect(ArenaSession s, PickupKind kind) { Drop(s, kind); Tick(s); }
        private void Shoot(ArenaSession s, Vector2 target, CombatTeam team, int count = 1)
        {
            for (int i = 0; i < count; i++)
                Assert.That(new WeaponState(player, s.Bullets, team, 900 + i).Step(new TankCommand(CardinalDirection.Up, false, true),
                    target - Vector2.up * 18, CardinalDirection.Up), Is.True);
        }
        [Test, Category("BC308")] public void ClassifiedKills_SnapshotIdsAndScores_ArmorOverkillAndGrenadeExactlyOnce()
        {
            var s = Session(); var first = s.AddTank(basic, CombatTeam.Enemy, new Vector2(64, 120));
            var heavy = s.AddTank(armor, CombatTeam.Enemy, new Vector2(120, 120));
            basic.definitionId = "edited"; basic.score = 999; armor.score = 999;
            Shoot(s, first.Motor.Position, CombatTeam.Player); Drop(s, PickupKind.Grenade); Tick(s);
            Assert.That(first.IsAlive || heavy.IsAlive, Is.False); Assert.That(s.Scoring.EnemyPoints, Is.EqualTo(500));
            Assert.That(s.Score, Is.EqualTo(1000)); Assert.That(s.DefeatedEnemyCount, Is.EqualTo(2));
            var rows = s.Scoring.Enemies; Assert.That(rows.Select(r => r.DefinitionId), Is.EqualTo(new[] { "enemy.armor", "enemy.basic" }));
            Assert.That(rows.Single(r => r.DefinitionId == "enemy.armor").Points, Is.EqualTo(400));
            Shoot(s, first.Motor.Position, CombatTeam.Player, 4); Tick(s, 2);
            Assert.That(s.Score, Is.EqualTo(1000)); Assert.That(s.Scoring.Enemies.Sum(r => r.Count), Is.EqualTo(2));
        }
        [TestCase(PickupKind.Clock)] [TestCase(PickupKind.Grenade)] [TestCase(PickupKind.Helmet)]
        [TestCase(PickupKind.Shovel)] [TestCase(PickupKind.Star)] [TestCase(PickupKind.Tank)]
        [Category("BC308")]
        public void EveryPickup_Exactly500PerReceiptEvenWithoutOptionalCapability_NoReplay(PickupKind kind)
        {
            var s = Session(); Collect(s, kind); Tick(s, 3);
            Assert.That(s.Score, Is.EqualTo(500)); Assert.That(s.Scoring.CollectedCount, Is.EqualTo(1));
            var saved = s.Scoring.Pickups;
            Collect(s, kind); Tick(s, 3); Assert.That(s.Score, Is.EqualTo(1000));
            Assert.That(s.Scoring.Pickups.Single(r => r.Kind == kind).Count, Is.EqualTo(2));
            Assert.That(saved.Single(r => r.Kind == kind).Count, Is.EqualTo(1), "Read snapshots do not mutate");
        }
        [Test, Category("BC308")] public void PickupScore_SnapshotsConfiguration_ZeroAllowed_NegativeRejected()
        {
            rules.collectionScore = 42; var s = Session(); rules.collectionScore = 99;
            Collect(s, PickupKind.Tank); Assert.That(s.Score, Is.EqualTo(42));
            rules.collectionScore = 0; var zero = Session(); Collect(zero, PickupKind.Star); Assert.That(zero.Score, Is.Zero);
            rules.collectionScore = -1; Assert.Throws<InvalidOperationException>(() => Session());
        }
        [Test, Category("BC308")] public void CappedTankPickup_StillScoresAndDoesNotAddLife()
        {
            rules.maximumLives = 3; var s = Session(); Collect(s, PickupKind.Tank);
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3)); Assert.That(s.Score, Is.EqualTo(500));
        }
        [Test, Category("BC308")] public void AllFourAuthoredTypes_ClassifyByDefinitionNotHpColor()
        {
            var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>(BattleCity.Editor.EnemyBalanceAuthoring.BalancePath);
            var s = Session(); int index = 0;
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                var def = roster.Get(kind); var t = s.AddTank(def, CombatTeam.Enemy, new Vector2(40 + index++ * 40, 120));
                Shoot(s, t.Motor.Position, CombatTeam.Player, def.hitPoints + 1); Tick(s);
                Assert.That(t.IsAlive, Is.False);
            }
            Assert.That(s.Scoring.Enemies.Count, Is.EqualTo(4)); Assert.That(s.Scoring.EnemyPoints, Is.EqualTo(1000));
            Assert.That(s.Scoring.Enemies.All(r => r.Count == 1), Is.True); Assert.That(s.Score, Is.EqualTo(1000));
        }
        [Test, Category("BC308")] public void GrenadeLastEnemy_FreezesCompleteResultBeforeOneNotification_NewSessionDoesNotInherit()
        {
            wave.preparationTicks = 1; level.stageNumber = 7; var s = Session(true); Start(s); Tick(s, 2);
            Assert.That(s.Result, Is.Null); StageResult resultInCallback = null; int ended = 0;
            s.SessionEnded += () => { ended++; resultInCallback = s.Result; Assert.That(s.Pickups.ClockRemainingTicks, Is.Zero); };
            level.stageNumber = 99; Collect(s, PickupKind.Clock); Collect(s, PickupKind.Grenade);
            var result = s.Result;
            Assert.That(result, Is.SameAs(resultInCallback)); Assert.That(ended, Is.EqualTo(1)); Assert.That(result.StageNumber, Is.EqualTo(7));
            Assert.That(result.Outcome, Is.EqualTo(SessionPhase.Won)); Assert.That(result.FailureReason, Is.EqualTo(SessionFailureReason.None));
            Assert.That(result.Total, Is.EqualTo(1100)); Assert.That(result.EnemyPoints, Is.EqualTo(100)); Assert.That(result.PickupPoints, Is.EqualTo(1000));
            Assert.That(result.DefeatedCount, Is.EqualTo(1)); Assert.That(result.CollectedCount, Is.EqualTo(2)); Assert.That(result.RemainingLives, Is.EqualTo(3));
            Assert.That(result.EndTick, Is.EqualTo(s.Clock.Tick)); Tick(s, 10); Assert.That(s.Result, Is.SameAs(result)); Assert.That(ended, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<EnemyScoreRow>)result.Enemies).Clear());
            var fresh = Session(true); Assert.That(fresh.Score, Is.Zero); Assert.That(fresh.Scoring.Enemies, Is.Empty); Assert.That(fresh.Result, Is.Null);
            Assert.That(fresh.Pickups.ClockRemainingTicks + fresh.Pickups.HelmetRemainingTicks + fresh.BaseFortification.RemainingTicks, Is.Zero);
            Assert.That(result.Total, Is.EqualTo(1100));
        }
        [Test, Category("BC308")] public void SimultaneousFinalKillAndBaseLoss_RecordsKillButNeverCollectsTerminalReward()
        {
            wave.preparationTicks = 1; var s = Session(true); Start(s); Tick(s, 2);
            var enemy = s.Tanks.Single(t => t.Team == CombatTeam.Enemy);
            Drop(s, PickupKind.Grenade); Shoot(s, enemy.Motor.Position, CombatTeam.Player); Shoot(s, s.Base.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Lost)); Assert.That(s.Result.FailureReason, Is.EqualTo(SessionFailureReason.BaseDestroyed));
            Assert.That(s.Result.Total, Is.EqualTo(100)); Assert.That(s.Result.CollectedCount, Is.Zero); Assert.That(s.Result.DefeatedCount, Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void TerminalOutcome_ClearsRealPlayerAndEnemyIceMomentum(bool victory)
        {
            for (int i = 0; i < level.terrain.Length; i++) level.terrain[i] = TerrainKind.Ice;
            wave.preparationTicks = 1; var s = Session(true); Start(s); Tick(s, 2);
            var enemy = s.Tanks.Single(t => t.Team == CombatTeam.Enemy);
            // The drop algorithm uses 8px anchors: create the item before moving to a fractional lane position.
            if (victory) Drop(s, PickupKind.Grenade);
            s.Player.Step(CardinalDirection.Right, 60, s.Map); enemy.Motor.Step(CardinalDirection.Right, 60, s.Map);
            Assert.That(s.Player.IceRemainingTicks, Is.EqualTo(24)); Assert.That(enemy.Motor.IceRemainingTicks, Is.EqualTo(24));
            if (!victory) Shoot(s, s.Base.Position, CombatTeam.Enemy);
            Tick(s); Assert.That(s.Phase, Is.EqualTo(victory ? SessionPhase.Won : SessionPhase.Lost));
            Assert.That(s.Player.IceRemainingTicks + enemy.Motor.IceRemainingTicks, Is.Zero);
            Assert.That(s.Result, Is.Not.Null);
        }
        [Test] public void ShieldAndDeath_ProtectedBulletConsumes_ExpiredShieldCannotRescueDeathOrEarnPickup()
        {
            var s = Session(lives: 2); Collect(s, PickupKind.Helmet); Shoot(s, s.Player.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.True); Assert.That(s.Bullets.Active, Is.Empty);
            Tick(s, 2); Drop(s, PickupKind.Tank); Shoot(s, s.Player.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.Pickups.CollectedCount, Is.EqualTo(1));
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(1)); Assert.That(s.Score, Is.EqualTo(500));
        }
        [Test] public void ClockPauseAndIce_FreezesEnemyMomentumButPlayerCoasts_ResumesAfterExactExpiry()
        {
            for (int i = 0; i < level.terrain.Length; i++) level.terrain[i] = TerrainKind.Ice;
            var s = Session(); var t = s.AddTank(basic, CombatTeam.Enemy, new Vector2(64, 120));
            s.SetTankCommand(t.EntityId, new TankCommand(CardinalDirection.Right)); Tick(s);
            s.SetTankCommand(t.EntityId, new TankCommand(CardinalDirection.None)); Collect(s, PickupKind.Clock);
            Vector2 position = t.Motor.Position; int ice = t.Motor.IceRemainingTicks;
            s.SetPaused(true); Tick(s, 5); Assert.That(t.Motor.IceRemainingTicks, Is.EqualTo(ice));
            Assert.That(s.Pickups.ClockRemainingTicks, Is.EqualTo(3)); s.SetPaused(false);
            Tick(s, 1, PlayerButtons.Right); Vector2 playerAt = s.Player.Position; Tick(s, 2);
            Assert.That(t.Motor.Position, Is.EqualTo(position)); Assert.That(t.Motor.IceRemainingTicks, Is.EqualTo(ice));
            Assert.That(s.Player.Position.x, Is.GreaterThan(playerAt.x)); Assert.That(s.Pickups.ClockRemainingTicks, Is.Zero);
            Tick(s); Assert.That(t.Motor.Position.x, Is.EqualTo(position.x + 1)); Assert.That(t.Motor.IceRemainingTicks, Is.EqualTo(ice - 1));
        }
        [Test] public void ShovelExpiryOccupiedPartialBrick_OnlyRestoresAfterRelease_RewardNotReplayed()
        {
            var s = Session(); s.Map.SetTerrainCell(11, 2, TerrainKind.Brick, 5); Collect(s, PickupKind.Shovel);
            s.Map.SetTerrainCell(11, 2, TerrainKind.Empty); var t = s.AddTank(basic, CombatTeam.Enemy, new Vector2(88, 16)); Tick(s, 3);
            Assert.That(s.BaseFortification.IsRestoring, Is.True); Assert.That(s.Map.TerrainAt(11, 2), Is.EqualTo(TerrainKind.Empty));
            s.SetPaused(true); Tick(s, 4); Assert.That(s.BaseFortification.IsRestoring, Is.True);
            t.Motor.Step(CardinalDirection.Left, 960, s.Map); s.SetPaused(false); Tick(s);
            Assert.That(s.Map.BrickMaskAt(11, 2), Is.EqualTo(5)); Assert.That(s.BaseFortification.PendingCellCount, Is.Zero); Assert.That(s.Score, Is.EqualTo(500));
        }
        [Test] public void CappedUpgradeRewardsAndIce_HighestWeaponBreaksSteelUsingExistingSweep()
        {
            var upgrades = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerUpgradeDefinition>("Assets/BattleCity/Data/PlayerUpgrades.asset");
            var s = new ArenaSession(level, upgrades.levels[0], wave, true, new PlayerLifeParameters(3, 2, 2), playerUpgrades: upgrades, pickups: rules);
            for (int i = 0; i < 4; i++) Collect(s, PickupKind.Star);
            Assert.That(s.PlayerUpgrades.Level, Is.EqualTo(3)); Assert.That(s.Score, Is.EqualTo(2000));
            s.Map.SetTerrainCell(3, 6, TerrainKind.Steel);
            s.Map.SetTerrainCell(3, 3, TerrainKind.Ice);
            Tick(s, 1, PlayerButtons.Right); Assert.That(s.Player.IceRemainingTicks, Is.EqualTo(24));
            Tick(s, 1, PlayerButtons.Up | PlayerButtons.Fire); Tick(s, 10);
            Assert.That(s.Map.TerrainAt(3, 6), Is.EqualTo(TerrainKind.Empty)); Assert.That(s.Bullets.Active, Is.Empty);
            Assert.That(s.Scoring.EnemyPoints, Is.Zero); Assert.That(s.Score, Is.EqualTo(2000));
        }
        [Test] public void DeathAndRespawnClearIceMomentum_ProtectedRespawnHasNoStaleMotion()
        {
            for (int i = 0; i < level.terrain.Length; i++) level.terrain[i] = TerrainKind.Ice;
            var s = Session(); Tick(s, 1, PlayerButtons.Right); Assert.That(s.Player.IceRemainingTicks, Is.EqualTo(24));
            Shoot(s, s.Player.Position + Vector2.right, CombatTeam.Enemy); Tick(s);
            Assert.That(s.PlayerTank.IsAlive, Is.False); Assert.That(s.Player.IceRemainingTicks, Is.Zero);
            Tick(s, 2); Assert.That(s.PlayerTank.IsAlive, Is.True); Vector2 p = s.Player.Position; Tick(s);
            Assert.That(s.Player.Position, Is.EqualTo(p)); Assert.That(s.PlayerIsProtected, Is.True);
        }
    }
}
