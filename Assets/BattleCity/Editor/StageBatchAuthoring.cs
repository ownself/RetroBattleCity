using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCity.Editor
{
    /// <summary>Reviewed top-down matrices are authoring inputs; gameplay never loads reference PNGs.</summary>
    public static class StageBatchAuthoring
    {
        public static string LayoutPath(int stage) => $"Assets/BattleCity/Editor/Stage{stage:00}Layout.txt";
        public static string LevelPath(int stage) => $"Assets/BattleCity/Data/Stage{stage:00}.asset";
        public static string ReferencePath(int stage) => $"Assets/Sprites/Stages/NES - Battle City (JPN) - Backgrounds - Stage {stage:00}.png";

        public static void Populate(LevelDefinition level, int stage)
        {
            if (stage < 1 || stage > 35) throw new ArgumentOutOfRangeException(nameof(stage));
            var rows = File.ReadAllLines(LayoutPath(stage));
            const int size = PixelGrid.TerrainCellsPerAxis;
            if (rows.Length != size || rows.Any(row => row.Length != size))
                throw new InvalidOperationException($"Stage {stage:00} requires 26 rows of 26 cells.");
            var data = new TerrainKind[size * size];
            int home = 0;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                TerrainKind kind;
                switch (rows[y][x])
                {
                    case '.': kind = TerrainKind.Empty; break;
                    case 'B': kind = TerrainKind.Brick; break;
                    case 'S': kind = TerrainKind.Steel; break;
                    case 'W': kind = TerrainKind.Water; break;
                    case 'G': kind = TerrainKind.Grass; break;
                    case 'I': kind = TerrainKind.Ice; break;
                    case 'H':
                        if (x < 12 || x > 13 || y < 24 || y > 25) throw new InvalidOperationException("Unexpected base footprint.");
                        home++; kind = TerrainKind.Empty; break;
                    default: throw new InvalidOperationException($"Unknown Stage {stage:00} cell at {x},{y}.");
                }
                data[(size - 1 - y) * size + x] = kind;
            }
            if (home != 4) throw new InvalidOperationException("Missing 16px base footprint.");
            level.schemaVersion = 1; level.stageNumber = stage; level.terrain = data;
            level.playerSpawnPixels = new Vector2Int(72, 8); level.baseCenterPixels = new Vector2Int(104, 8);
            level.enemySpawnPixels = new[] { new Vector2Int(8, 200), new Vector2Int(104, 200), new Vector2Int(200, 200) };
            level.enemySequence = EnemyTypesAuthoring.PrototypeSequence(); level.maxActiveEnemies = 4;
            level.Validate(); CampaignDefinition.ValidatePlacement(level);
        }

        public static string BuildFirstBatch() => BuildRange(2, 5);
        public static string BuildRemainingStages() => BuildRange(6, 35);

        private static string BuildRange(int first, int last)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(SinglePlayerAuthoring.CampaignPath);
            if (campaign == null) throw new InvalidOperationException("Missing campaign asset.");
            if (campaign.stages.Length > last) throw new InvalidOperationException("Campaign already has later stages; merge explicitly.");
            var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveDefinition>(Stage01Authoring.WavePath);
            var pickup = AssetDatabase.LoadAssetAtPath<PickupDefinition>("Assets/BattleCity/Data/Pickups.asset");
            if (pickup == null) throw new InvalidOperationException("Missing pickup rules.");
            // Preflight every matrix before creating or changing any asset.
            for (int i = first; i <= last; i++)
            {
                var temporary = ScriptableObject.CreateInstance<LevelDefinition>();
                try { Populate(temporary, i); _ = new EnemyWaveState(temporary, wave); if (pickup != null) pickup.ValidateFor(temporary); }
                finally { UnityEngine.Object.DestroyImmediate(temporary); }
            }
            for (int i = first; i <= last; i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath(i));
                if (level == null) { level = ScriptableObject.CreateInstance<LevelDefinition>(); AssetDatabase.CreateAsset(level, LevelPath(i)); }
                Populate(level, i); EditorUtility.SetDirty(level); AssetDatabase.SaveAssetIfDirty(level);
            }
            var stages = Enumerable.Range(1, last).Select(i => AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath(i))).ToArray();
            campaign.stages = stages; campaign.Validate(wave, pickup, null);
            EditorUtility.SetDirty(campaign); AssetDatabase.SaveAssetIfDirty(campaign);
            return $"Stages 01..{last:00} bound; {first:00}..{last:00} terrain from references; shared uncalibrated prototype waves.";
        }
    }
}
