#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class StageBatchDataTests
    {
        private static IEnumerable<int> Stages => Enumerable.Range(1, 35);
        [TestCaseSource(nameof(Stages))]
        public void EveryReferenceCellMatchesInteractiveDataAndPlacement(int stage)
        {
            var path = $"Assets/BattleCity/Data/Stage{stage:00}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            Assert.That(level, Is.Not.Null); level.Validate(); CampaignDefinition.ValidatePlacement(level);
            Assert.That(level.stageNumber, Is.EqualTo(stage));
            Assert.That(level.enemySequence.Length, Is.EqualTo(20)); Assert.That(level.maxActiveEnemies, Is.EqualTo(4));
            var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveDefinition>("Assets/BattleCity/Data/Stage01EnemyWave.asset");
            _ = new EnemyWaveState(level, wave);
            AssetDatabase.LoadAssetAtPath<PickupDefinition>("Assets/BattleCity/Data/Pickups.asset").ValidateFor(level);
            // Read raw bytes, not imported texture/readability or the author's text matrix.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes($"Assets/Sprites/Stages/NES - Battle City (JPN) - Backgrounds - Stage {stage:00}.png")), Is.True);
                Assert.That(texture.width, Is.EqualTo(208)); Assert.That(texture.height, Is.EqualTo(208));
                for (int sy = 0; sy < 26; sy++) for (int x = 0; x < 26; x++)
                {
                    var pixels = texture.GetPixels(x * 8, (25 - sy) * 8, 8, 8).Select(c => (Color32)c).ToArray();
                    TerrainKind expected;
                    if (x >= 12 && x <= 13 && sy >= 24) expected = TerrainKind.Empty;
                    else if (pixels.Any(p => p.g == 217 && p.r == 137)) expected = TerrainKind.Grass;
                    else if (pixels.Any(p => p.b == 254 && p.r == 66)) expected = TerrainKind.Water;
                    else if (pixels.Any(p => p.r == 153 && p.g == 79)) expected = TerrainKind.Brick;
                    else if (pixels.Count(p => p.r == 174 && p.g == 174) == 37 &&
                        pixels.Count(p => p.r == 254 && p.g == 254) == 14 &&
                        pixels.Count(p => p.r == 102 && p.g == 102) == 13) expected = TerrainKind.Ice;
                    else if (pixels.Any(p => p.r == 174 && p.g == 174)) expected = TerrainKind.Steel;
                    else { Assert.That(pixels.All(p => p.r == 0 && p.g == 0 && p.b == 0), Is.True, $"unrecognized reference cell {stage}/{x}/{sy}"); expected = TerrainKind.Empty; }
                    Assert.That(level.terrain[(25 - sy) * 26 + x], Is.EqualTo(expected), $"reference mismatch stage {stage} source {x},{sy}");
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }
        [Test] public void CampaignHasThirtyFiveDistinctOrderedAssets()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>("Assets/BattleCity/Data/SinglePlayerCampaign.asset");
            Assert.That(campaign.stages.Length, Is.EqualTo(35));
            Assert.That(campaign.stages.Select(s => s.stageNumber), Is.EqualTo(Enumerable.Range(1, 35)));
            Assert.That(campaign.stages.Distinct().Count(), Is.EqualTo(35));
            campaign.Validate(AssetDatabase.LoadAssetAtPath<EnemyWaveDefinition>("Assets/BattleCity/Data/Stage01EnemyWave.asset"),
                AssetDatabase.LoadAssetAtPath<PickupDefinition>("Assets/BattleCity/Data/Pickups.asset"), null);
        }
    }
}
#endif
