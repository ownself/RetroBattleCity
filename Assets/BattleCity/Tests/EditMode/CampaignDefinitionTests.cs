using System;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class CampaignDefinitionTests
    {
        private CampaignDefinition campaign;
        private LevelDefinition level;
        private EnemyWaveDefinition wave;
        private TankDefinition tank;
        [SetUp] public void SetUp()
        {
            campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            tank = ScriptableObject.CreateInstance<TankDefinition>();
            tank.definitionId = "test.enemy";
            wave.tankDefinitions = new[] { tank };
            level.enemySequence = new[] { tank.definitionId };
            campaign.stages = new[] { level };
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(campaign); UnityEngine.Object.DestroyImmediate(level);
            UnityEngine.Object.DestroyImmediate(wave); UnityEngine.Object.DestroyImmediate(tank);
        }
        [Test] public void ValidSingleStageAccepted() => Assert.DoesNotThrow(() => campaign.Validate(wave, null, null));
        [TestCase("size", "26x26")]
        [TestCase("terrain", "Unknown terrain")]
        [TestCase("spawn", "Missing enemy spawn")]
        [TestCase("blocked", "Blocked spawn/base")]
        [TestCase("overlap", "Overlapping")]
        [TestCase("sequence", "nonempty")]
        [TestCase("id", "unknown")]
        [TestCase("number", "Stage number")]
        [TestCase("missing", "Missing level")]
        [TestCase("duplicate", "unique")]
        public void InvalidDataReportsEntryAndCause(string kind, string expected)
        {
            switch (kind)
            {
                case "size": level.terrain = new TerrainKind[1]; break;
                case "terrain": level.terrain[0] = (TerrainKind)999; break;
                case "spawn": level.enemySpawnPixels = Array.Empty<Vector2Int>(); break;
                case "blocked": level.terrain[9] = TerrainKind.Water; break; // player's 16px footprint
                case "overlap": level.playerSpawnPixels = level.baseCenterPixels; break;
                case "sequence": level.enemySequence = Array.Empty<string>(); break;
                case "id": level.enemySequence[0] = "unknown"; break;
                case "number": level.stageNumber = 0; break;
                case "missing": campaign.stages[0] = null; break;
                case "duplicate": campaign.stages = new[] { level, level }; break;
            }
            var error = Assert.Throws<InvalidOperationException>(() => campaign.Validate(wave, null, null));
            StringAssert.Contains("Campaign entry", error.Message);
            StringAssert.Contains(expected.ToLowerInvariant(), error.Message.ToLowerInvariant());
        }
        [Test] public void EmptyCampaignRejected()
        {
            campaign.stages = Array.Empty<LevelDefinition>();
            StringAssert.Contains("no stages", Assert.Throws<InvalidOperationException>(() => campaign.Validate(wave, null, null)).Message);
        }
        [Test] public void MissingWaveRejected() => Assert.Throws<InvalidOperationException>(() => campaign.Validate(null, null, null));
        [Test] public void LaterStageInvalidPickupIndexIsRejectedBeforePlay()
        {
            var pickup = ScriptableObject.CreateInstance<PickupDefinition>();
            try { StringAssert.Contains("carrier index", Assert.Throws<InvalidOperationException>(() => campaign.Validate(wave, pickup, null)).Message); }
            finally { UnityEngine.Object.DestroyImmediate(pickup); }
        }
        [Test] public void PickupFortificationMisalignedBaseRejected()
        {
            var pickup = ScriptableObject.CreateInstance<PickupDefinition>();
            pickup.carrierSequenceIndices = Array.Empty<int>();
            level.baseCenterPixels = new Vector2Int(105, 8);
            try { StringAssert.Contains("8px-aligned", Assert.Throws<InvalidOperationException>(() => campaign.Validate(wave, pickup, null)).Message); }
            finally { UnityEngine.Object.DestroyImmediate(pickup); }
        }
        [Test] public void NextStageConfirmationIsSingleEdgeAndFinalVictoryEnds()
        {
            var flow = new SinglePlayerFlowState();
            flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            flow.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            flow.Sample(PlayerButtons.None, true, SessionPhase.Won);
            Assert.AreEqual(SinglePlayerAction.NextStage, flow.Sample(PlayerButtons.Start, true, SessionPhase.Won, true));
            Assert.AreEqual(SinglePlayerScreen.StageIntro, flow.Screen);
            Assert.AreEqual(SinglePlayerAction.None, flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready, true));
            flow.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            flow.Sample(PlayerButtons.None, true, SessionPhase.Won);
            flow.Sample(PlayerButtons.Start, true, SessionPhase.Won, false);
            Assert.AreEqual(SinglePlayerScreen.Complete, flow.Screen);
        }
    }
}
