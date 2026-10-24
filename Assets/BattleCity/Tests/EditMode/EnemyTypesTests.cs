using System;
using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleCity.Tests
{
    [Category("BC301")]
    public sealed class EnemyTypesTests
    {
        private LevelDefinition level;
        private TankDefinition player;
        private TankDefinition[] definitions;
        private ArenaSession session;
        private int owner = 1000;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            player = ScriptableObject.CreateInstance<TankDefinition>();
            var roster = AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>(EnemyBalanceAuthoring.BalancePath);
            definitions = Enum.GetValues(typeof(EnemyKind)).Cast<EnemyKind>()
                .Select(kind => UnityEngine.Object.Instantiate(roster.Get(kind))).ToArray();
            session = new ArenaSession(level, player);
        }
        [TearDown] public void TearDown()
        {
            session.Bullets.Clear();
            UnityEngine.Object.DestroyImmediate(level);
            UnityEngine.Object.DestroyImmediate(player);
            foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
        }
        private void Tick() => Assert.That(session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None), Is.EqualTo(1));
        private TankState Spawn(EnemyKind kind) => session.AddTank(definitions[(int)kind], CombatTeam.Enemy, new Vector2(104, 104));
        private void Shoot(TankState enemy, CombatTeam team = CombatTeam.Player)
        {
            var weapon = new WeaponState(player, session.Bullets, team, owner++);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.Up, false, true),
                enemy.Motor.Position - Vector2.up * 18, CardinalDirection.Up), Is.True);
        }

        [TestCase(EnemyKind.Basic)] [TestCase(EnemyKind.Fast)]
        [TestCase(EnemyKind.FastBullet)] [TestCase(EnemyKind.Armor)]
        public void EachType_MovesAndFiresUsingConfiguredSpeedCooldownAndLimit(EnemyKind kind)
        {
            var definition = definitions[(int)kind];
            var enemy = Spawn(kind);
            var start = enemy.Motor.Position;
            session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Up, true));
            Tick();
            Assert.That(enemy.Motor.Position.y - start.y, Is.EqualTo(definition.moveSpeedPixelsPerSecond / 60).Within(.001f));
            var bullet = session.Bullets.Active.Single(b => b.OwnerId == enemy.EntityId);
            Assert.That(bullet.Position.y - enemy.Motor.Position.y,
                Is.EqualTo(WeaponState.MuzzleOffsetPixels + definition.bulletSpeedPixelsPerSecond / 60).Within(.001f));
            Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.EqualTo(definition.fireCooldownTicks));
            Tick();
            Assert.That(enemy.Weapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.EqualTo(definition.fireCooldownTicks - 1));
            session.Bullets.Clear();
            for (int tick = 2; tick < definition.fireCooldownTicks; tick++) Tick();
            Assert.That(enemy.Weapon.ActiveBullets, Is.Zero, "No early second shot after releasing the bullet cap.");
            Tick();
            Assert.That(enemy.Weapon.ActiveBullets, Is.EqualTo(1));
        }

        [TestCase(EnemyKind.Basic)] [TestCase(EnemyKind.Fast)]
        [TestCase(EnemyKind.FastBullet)] [TestCase(EnemyKind.Armor)]
        public void EachType_RequiresConfiguredHits_OnlyDeathScores_DeadTankNeverScoresOrMovesAgain(EnemyKind kind)
        {
            var definition = definitions[(int)kind];
            var enemy = Spawn(kind);
            Assert.That(enemy.DefinitionId, Is.EqualTo(definition.definitionId));
            Assert.That(enemy.MaxHitPoints, Is.EqualTo(definition.hitPoints));
            for (int hit = 1; hit <= definition.hitPoints; hit++)
            {
                Shoot(enemy); Tick();
                Assert.That(enemy.HitPoints, Is.EqualTo(definition.hitPoints - hit));
                Assert.That(session.Score, Is.EqualTo(hit == definition.hitPoints ? definition.score : 0));
                Assert.That(session.DefeatedEnemyCount, Is.EqualTo(hit == definition.hitPoints ? 1 : 0));
            }
            var position = enemy.Motor.Position;
            session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Right, true));
            Shoot(enemy); Tick();
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.HitPoints, Is.Zero);
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(enemy.Weapon.ActiveBullets, Is.Zero);
            Assert.That(session.Score, Is.EqualTo(definition.score));
            Assert.That(session.DefeatedEnemyCount, Is.EqualTo(1));
        }

        [Test]
        public void Armor_SameTickOverkillScoresOnce_AndSnapshotsScoreAtBirth()
        {
            var enemy = Spawn(EnemyKind.Armor);
            definitions[3].score = 9999; // Editing an authoring object must not rewrite an existing tank's reward.
            for (int i = 0; i < 7; i++) Shoot(enemy);
            Tick();
            Assert.That(session.Bullets.Hits.Count(h => h.TargetId == enemy.EntityId), Is.EqualTo(7));
            Assert.That(enemy.HitPoints, Is.Zero);
            Assert.That(session.Score, Is.EqualTo(400));
            Assert.That(session.DefeatedEnemyCount, Is.EqualTo(1));
            Assert.That(session.AliveEnemyCount, Is.Zero);
        }

        [Test]
        public void ProtectionFriendlyFireAndPause_DoNotDamageOrAwardPoints()
        {
            var enemy = Spawn(EnemyKind.Armor);
            enemy.IsProtected = true;
            Shoot(enemy); Tick();
            enemy.IsProtected = false;
            Shoot(enemy, CombatTeam.Enemy); Tick();
            session.Bullets.Clear();
            Assert.That(enemy.HitPoints, Is.EqualTo(4));
            Shoot(enemy);
            session.SetPaused(true);
            long tick = session.Clock.Tick;
            session.AdvanceFrame(10, PlayerButtons.None);
            Assert.That(session.Clock.Tick, Is.EqualTo(tick));
            Assert.That(enemy.HitPoints, Is.EqualTo(4));
            Assert.That(session.Score, Is.Zero);
            session.SetPaused(false); Tick();
            Assert.That(enemy.HitPoints, Is.EqualTo(3));
            Assert.That(session.Score, Is.Zero);
        }

        [Test]
        public void PlayerDeath_NeverAwardsEnemyKillScore()
        {
            var weapon = new WeaponState(player, session.Bullets, CombatTeam.Enemy, owner++);
            // Player spawns at y=8: shoot down from inside the map, not up from outside its lower boundary.
            weapon.Step(new TankCommand(CardinalDirection.Down, false, true),
                session.PlayerTank.Motor.Position + Vector2.up * 18, CardinalDirection.Down);
            Tick();
            Assert.That(session.PlayerTank.IsAlive, Is.False);
            Assert.That(session.Score, Is.Zero);
            Assert.That(session.DefeatedEnemyCount, Is.Zero);
        }

        [TestCase("null")] [TestCase("length")] [TestCase("first")] [TestCase("empty")]
        public void InvalidDamageStageMappings_AreRejected(string error)
        {
            var armor = definitions[3];
            switch (error)
            {
                case "null": armor.damageSpritePrefixes = null; break;
                case "length": armor.damageSpritePrefixes = new[] { armor.spritePrefix }; break;
                case "first": armor.damageSpritePrefixes[0] = "tank.red.enemy.armor"; break;
                case "empty": armor.damageSpritePrefixes[1] = " "; break;
            }
            Assert.Throws<InvalidOperationException>(armor.Validate);
        }

        [Test]
        public void Armor_AllFourDamagePalettesResolveEveryDirectionAndFrame_MissingDamageFrameFailsEarly()
        {
            var armor = definitions[3];
            var sprites = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            CollectionAssert.AreEqual(new[] { "tank.white.enemy.armor", "tank.green.enemy.armor",
                "tank.yellow.enemy.armor", "tank.red.enemy.armor" }, armor.damageSpritePrefixes);
            Assert.DoesNotThrow(() => armor.ValidateSprites(sprites));
            armor.damageSpritePrefixes[1] = "tank.missing.enemy.armor";
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => armor.ValidateSprites(sprites));
        }
    }
}
