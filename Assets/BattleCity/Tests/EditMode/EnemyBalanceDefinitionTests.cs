using System;
using System.IO;
using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCity.Tests
{
    [Category("BC300")]
    public sealed class EnemyBalanceDefinitionTests
    {
        private EnemyBalanceDefinition roster;
        private TankDefinition[] tanks;

        [SetUp]
        public void SetUp()
        {
            roster = ScriptableObject.CreateInstance<EnemyBalanceDefinition>();
            tanks = Enumerable.Range(0, 4).Select(i =>
            {
                var tank = ScriptableObject.CreateInstance<TankDefinition>();
                tank.definitionId = "enemy.test." + i;
                return tank;
            }).ToArray();
            roster.entries = tanks.Select((tank, i) => new EnemyBalanceDefinition.Entry
                { kind = (EnemyKind)i, tank = tank }).ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(roster);
            foreach (var tank in tanks) UnityEngine.Object.DestroyImmediate(tank);
        }

        [Test]
        public void ValidRoster_ResolvesTypedDefinitionsWithoutDependingOnArrayOrder()
        {
            Array.Reverse(roster.entries);
            Assert.DoesNotThrow(roster.Validate);
            for (int i = 0; i < 4; i++) Assert.That(roster.Get((EnemyKind)i), Is.SameAs(tanks[i]));
            Assert.Throws<ArgumentOutOfRangeException>(() => roster.Get((EnemyKind)99));
        }

        [TestCase("null")] [TestCase("missing")] [TestCase("extra")]
        [TestCase("duplicateKind")] [TestCase("unknownKind")] [TestCase("nullTank")]
        [TestCase("duplicateId")] [TestCase("sharedTank")]
        public void InvalidRoster_IsRejected(string error)
        {
            switch (error)
            {
                case "null": roster.entries = null; break;
                case "missing": roster.entries = roster.entries.Take(3).ToArray(); break;
                case "extra": roster.entries = roster.entries.Concat(new[] { roster.entries[0] }).ToArray(); break;
                case "duplicateKind": roster.entries[3].kind = EnemyKind.Basic; break;
                case "unknownKind": roster.entries[3].kind = (EnemyKind)99; break;
                case "nullTank": roster.entries[3].tank = null; break;
                case "duplicateId": tanks[3].definitionId = tanks[0].definitionId; break;
                case "sharedTank": roster.entries[3].tank = tanks[0]; break;
            }
            Assert.Throws<InvalidOperationException>(roster.Validate);
        }

        [TestCase("id")] [TestCase("sprite")]
        [TestCase("moveZero")] [TestCase("moveNegative")] [TestCase("moveNaN")] [TestCase("moveInfinity")]
        [TestCase("bulletZero")] [TestCase("bulletNegative")] [TestCase("bulletNaN")] [TestCase("bulletInfinity")]
        [TestCase("cooldown")] [TestCase("bulletLimit")] [TestCase("hp")] [TestCase("score")]
        public void InvalidCombatParameters_AreRejectedBySharedTankValidation(string field)
        {
            var tank = tanks[0];
            switch (field)
            {
                case "id": tank.definitionId = " "; break;
                case "sprite": tank.spritePrefix = " "; break;
                case "moveZero": tank.moveSpeedPixelsPerSecond = 0; break;
                case "moveNegative": tank.moveSpeedPixelsPerSecond = -1; break;
                case "moveNaN": tank.moveSpeedPixelsPerSecond = float.NaN; break;
                case "moveInfinity": tank.moveSpeedPixelsPerSecond = float.PositiveInfinity; break;
                case "bulletZero": tank.bulletSpeedPixelsPerSecond = 0; break;
                case "bulletNegative": tank.bulletSpeedPixelsPerSecond = -1; break;
                case "bulletNaN": tank.bulletSpeedPixelsPerSecond = float.NaN; break;
                case "bulletInfinity": tank.bulletSpeedPixelsPerSecond = float.NegativeInfinity; break;
                case "cooldown": tank.fireCooldownTicks = 0; break;
                case "bulletLimit": tank.maxActiveBullets = 0; break;
                case "hp": tank.hitPoints = 0; break;
                case "score": tank.score = -1; break;
            }
            Assert.Throws<InvalidOperationException>(roster.Validate);
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        public void VerifiedOriginalValues_RequireEvidence(string source)
        {
            tanks[0].requiresCalibration = false;
            roster.entries[0].originalValuesSource = source;
            Assert.Throws<InvalidOperationException>(roster.Validate);
        }

        [Test]
        public void Provenance_DefaultsRemainUnverified_ExplicitVerificationCanCiteEvidence()
        {
            Assert.DoesNotThrow(roster.Validate);
            tanks[0].requiresCalibration = false;
            roster.entries[0].originalValuesSource = "Test fixture evidence, not a claim about shipped defaults";
            Assert.DoesNotThrow(roster.Validate);
            Assert.That(tanks.Skip(1).All(t => t.requiresCalibration), Is.True);
        }

        [TestCase(EnemyKind.Basic, "basic", 60, 180, 1, 100)]
        [TestCase(EnemyKind.Fast, "fast", 90, 180, 1, 200)]
        [TestCase(EnemyKind.FastBullet, "power", 60, 300, 1, 300)]
        [TestCase(EnemyKind.Armor, "armor", 45, 180, 4, 400)]
        public void ShippedAssets_HaveDocumentedDefaultsAndEightValidSpriteFrames(
            EnemyKind kind, string variant, float move, float bullet, int hp, int score)
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>(EnemyBalanceAuthoring.BalancePath);
            Assert.That(asset, Is.Not.Null);
            var sprites = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            Assert.DoesNotThrow(() => asset.ValidateSprites(sprites));
            var tank = asset.Get(kind);
            Assert.That(tank.definitionId, Is.EqualTo("enemy." + variant));
            Assert.That(tank.spritePrefix, Is.EqualTo("tank.white.enemy." + variant));
            Assert.That(tank.moveSpeedPixelsPerSecond, Is.EqualTo(move));
            Assert.That(tank.bulletSpeedPixelsPerSecond, Is.EqualTo(bullet));
            Assert.That(tank.hitPoints, Is.EqualTo(hp));
            Assert.That(tank.score, Is.EqualTo(score));
            Assert.That(tank.fireCooldownTicks, Is.EqualTo(15));
            Assert.That(tank.maxActiveBullets, Is.EqualTo(1));
            Assert.That(tank.requiresCalibration, Is.True);
            Assert.That(asset.entries.Single(e => e.kind == kind).originalValuesSource, Is.Null.Or.Empty);
        }

        [Test]
        public void MissingSpriteCatalogOrFrame_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => roster.ValidateSprites(null));
            var sprites = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            tanks[0].spritePrefix = "tank.missing.enemy";
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => roster.ValidateSprites(sprites));
        }

        [Test]
        public void NewDefinitions_AreCompatibleWithWaveMapping_Stage01UsesMixedPrototype_LegacyAssetRetained()
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>(EnemyBalanceAuthoring.BalancePath);
            var wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            try
            {
                wave.tankDefinitions = asset.entries.Select(e => e.tank).ToArray();
                Assert.DoesNotThrow(wave.Validate);
                var stage = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Stage01Authoring.LevelPath);
                Assert.That(stage.enemySequence, Has.Length.EqualTo(20));
                CollectionAssert.AreEqual(EnemyTypesAuthoring.PrototypeSequence(), stage.enemySequence);
                var legacy = AssetDatabase.LoadAssetAtPath<TankDefinition>(Stage01Authoring.EnemyPath);
                Assert.That(legacy.score, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(wave); }
        }

        [Test]
        public void RepeatedAuthoring_RetainsAssetsAndDoesNotTouchActiveSceneOrLegacyBindings()
        {
            var paths = new[] { EnemyBalanceAuthoring.BalancePath, Stage01Authoring.ScenePath,
                Stage01Authoring.LevelPath, Stage01Authoring.WavePath, Stage01Authoring.EnemyPath }
                .Concat(new[] { "Basic", "Fast", "FastBullet", "Armor" }.Select(n => "Assets/BattleCity/Data/Enemy" + n + ".asset")).ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var scene = SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;
            EnemyBalanceAuthoring.Build();
            EnemyBalanceAuthoring.Build();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
            Assert.That(scene.isDirty, Is.EqualTo(dirty));
        }
    }
}
