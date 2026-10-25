using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Ordered interactive levels, not reference PNGs. All stages share the host's battle rules.</summary>
    [CreateAssetMenu(menuName = "Battle City/Campaign Definition")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        public LevelDefinition[] stages = Array.Empty<LevelDefinition>();

        public void Validate(EnemyWaveDefinition wave, PickupDefinition pickups, SpriteCatalog sprites)
        {
            if (stages == null || stages.Length == 0) throw new InvalidOperationException("Campaign has no stages.");
            if (wave == null) throw new InvalidOperationException("Campaign requires enemy wave rules.");
            var numbers = new HashSet<int>();
            for (int i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                try
                {
                    if (stage == null) throw new InvalidOperationException("Missing level asset.");
                    stage.Validate();
                    if (stage.stageNumber < 1 || stage.stageNumber > 9999 || !numbers.Add(stage.stageNumber))
                        throw new InvalidOperationException("Stage number must be unique and within 1..9999.");
                    ValidatePlacement(stage);
                    if (stage.enemySequence.Length == 0) throw new InvalidOperationException("Stage requires a nonempty enemy sequence.");
                    _ = new EnemyWaveState(stage, wave);
                    if (pickups != null)
                    {
                        pickups.ValidateFor(stage);
                        if (stage.baseCenterPixels.x % PixelGrid.TerrainCellPixels != 0 || stage.baseCenterPixels.y % PixelGrid.TerrainCellPixels != 0)
                            throw new InvalidOperationException("Pickup fortification requires an 8px-aligned base center.");
                        if (sprites != null)
                            foreach (int index in pickups.carrierSequenceIndices)
                                foreach (var tank in wave.tankDefinitions)
                                    if (tank.definitionId == stage.enemySequence[index]) PickupDefinition.ValidateCarrierSprites(tank, sprites);
                    }
                }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
                {
                    throw new InvalidOperationException("Campaign entry " + i + " (" + (stage == null ? "missing" : stage.name) + "): " + error.Message, error);
                }
            }
        }

        public static void ValidatePlacement(LevelDefinition stage)
        {
            var map = new MapState(stage);
            var centers = new List<Vector2Int> { stage.playerSpawnPixels, stage.baseCenterPixels };
            centers.AddRange(stage.enemySpawnPixels);
            for (int i = 0; i < centers.Count; i++)
            {
                if (!map.CanOccupy(centers[i])) throw new InvalidOperationException("Blocked spawn/base footprint at " + centers[i]);
                for (int j = 0; j < i; j++)
                    if (Mathf.Abs(centers[i].x - centers[j].x) < 16 && Mathf.Abs(centers[i].y - centers[j].y) < 16)
                        throw new InvalidOperationException("Overlapping spawn/base footprints at " + centers[i]);
            }
        }
    }
}
