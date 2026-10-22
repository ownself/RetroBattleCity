using System;
using UnityEngine;

namespace BattleCity
{
    public enum TerrainKind { Empty, Brick, Steel, Water, Grass, Ice }

    /// <summary>Authoring only. Runtime must copy this into its unique mutable map state.</summary>
    [CreateAssetMenu(menuName = "Battle City/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public int schemaVersion = 1;
        public int stageNumber = 1;
        // 26x26, row-major bottom-left: index = y * 26 + x. One value per 8px cell.
        public TerrainKind[] terrain = new TerrainKind[PixelGrid.TerrainCellsPerAxis * PixelGrid.TerrainCellsPerAxis];
        public Vector2Int playerSpawnPixels = new Vector2Int(72, 8);
        public Vector2Int baseCenterPixels = new Vector2Int(104, 8);
        public Vector2Int[] enemySpawnPixels = { new Vector2Int(8, 200), new Vector2Int(104, 200), new Vector2Int(200, 200) };
        public string[] enemySequence = Array.Empty<string>();
        public int maxActiveEnemies = 4;

        public void Validate()
        {
            if (schemaVersion != 1) throw new InvalidOperationException("Unsupported level schema.");
            if (terrain == null || terrain.Length != PixelGrid.TerrainCellsPerAxis * PixelGrid.TerrainCellsPerAxis)
                throw new InvalidOperationException("Level terrain must contain 26x26 cells.");
            foreach (var cell in terrain)
                if (!Enum.IsDefined(typeof(TerrainKind), cell)) throw new InvalidOperationException("Unknown terrain.");
            if (!ValidTankCenter(playerSpawnPixels) || !ValidTankCenter(baseCenterPixels))
                throw new InvalidOperationException("Player/base center must fit inside map bounds.");
            if (enemySpawnPixels == null || enemySpawnPixels.Length == 0 || maxActiveEnemies < 1)
                throw new InvalidOperationException("Missing enemy spawn points or invalid active limit.");
            foreach (var spawn in enemySpawnPixels)
                if (!ValidTankCenter(spawn)) throw new InvalidOperationException("Enemy spawn outside map bounds.");
            if (enemySequence == null) throw new InvalidOperationException("Enemy sequence is null.");
            foreach (var id in enemySequence)
                if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Empty tank definition ID.");
        }

        private static bool ValidTankCenter(Vector2Int p) =>
            p.x >= 8 && p.x <= 200 && p.y >= 8 && p.y <= 200;
    }
}
