using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCity.Editor
{
    /// <summary>BC-200 recipe. The top-down 8px matrix is authoring input, never runtime PNG collision.</summary>
    public static class Stage01Authoring
    {
        public const string LevelPath = Phase0Baseline.Root + "/Data/Stage01.asset";
        public const string ScenePath = Phase0Baseline.Root + "/Scenes/Stage01.unity";
        public const string LayoutPath = Phase0Baseline.Root + "/Editor/Stage01Layout.txt";
        public const string ReferencePath = "Assets/Sprites/NES - Battle City (JPN) - Backgrounds - Stage 01.png";

        [MenuItem("Battle City/Author Stage 01 (BC-200)")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring Stage 01.");
            var input = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                Populate(input);
                ValidatePlacement(input);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(level, LevelPath);
                }
                EditorUtility.CopySerialized(input, level);
                level.name = "Stage01";
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssetIfDirty(level);
            }
            finally { UnityEngine.Object.DestroyImmediate(input); }

            // Save a COPY of the proven arena harness; never rebuild or save its source scene/data.
            var source = EditorSceneManager.OpenScene(Phase1Arena.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("Could not copy arena harness.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            controller.name = "Stage 01 - WASD or arrows; Space fires; Esc pauses";
            controller.level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            controller.playerRenderer.transform.position = PixelGrid.ToWorld(controller.level.playerSpawnPixels);
            controller.mapView.Render(new MapState(controller.level));
            controller.ValidateBindings();
            var baseRenderer = new GameObject("Base - intact display only (BC-204 pending)", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            baseRenderer.sprite = controller.sprites.Get("base.alive");
            baseRenderer.sharedMaterial = controller.playerRenderer.sharedMaterial;
            baseRenderer.sortingLayerName = "Actors";
            baseRenderer.transform.position = PixelGrid.ToWorld(controller.level.baseCenterPixels);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save Stage 01.");
            return "BC-200 Stage 01 saved: " + ScenePath;
        }

        public static void Populate(LevelDefinition level)
        {
            var rows = File.ReadAllLines(LayoutPath);
            int size = PixelGrid.TerrainCellsPerAxis, cellSize = PixelGrid.TerrainCellPixels;
            if (rows.Length != size || rows.Any(row => row.Length != size)) throw new InvalidOperationException("Stage 01 requires 26 top-down rows of 26 cells.");
            level.schemaVersion = 1; level.stageNumber = 1;
            level.terrain = new TerrainKind[size * size];
            int baseCells = 0;
            for (int sourceY = 0; sourceY < size; sourceY++) for (int x = 0; x < size; x++)
            {
                var rect = PixelGrid.SourceRectToUnity(new RectInt(x * cellSize, sourceY * cellSize, cellSize, cellSize), PixelGrid.MapSizePixels);
                int y = rect.y / cellSize;
                TerrainKind kind;
                switch (rows[sourceY][x])
                {
                    case '.': kind = TerrainKind.Empty; break;
                    case 'B': kind = TerrainKind.Brick; break;
                    case 'S': kind = TerrainKind.Steel; break;
                    case 'H':
                        if (x < 12 || x > 13 || sourceY < 24 || sourceY > 25) throw new InvalidOperationException("Unexpected base footprint.");
                        baseCells++; kind = TerrainKind.Empty; break; // Reserved for the separate intact base sprite.
                    default: throw new InvalidOperationException("Unknown Stage 01 layout cell.");
                }
                level.terrain[y * size + x] = kind;
            }
            if (baseCells != 4) throw new InvalidOperationException("Missing 16px base footprint.");
            var baseRect = PixelGrid.SourceRectToUnity(new RectInt(96, 192, 16, 16), PixelGrid.MapSizePixels);
            level.baseCenterPixels = baseRect.position + baseRect.size / 2;
            // Frozen LevelDefinition defaults, NOT pixel-verified: no tanks/spawn markers exist in the PNG.
            level.playerSpawnPixels = new Vector2Int(72, 8);
            level.enemySpawnPixels = new[] { new Vector2Int(8, 200), new Vector2Int(104, 200), new Vector2Int(200, 200) };
            level.enemySequence = Array.Empty<string>(); // BC-203 owns wave content; do not invent an original sequence.
            level.maxActiveEnemies = 4;
            level.Validate();
        }

        public static void ValidatePlacement(LevelDefinition level)
        {
            level.Validate();
            var map = new MapState(level);
            var centers = new[] { level.baseCenterPixels, level.playerSpawnPixels }.Concat(level.enemySpawnPixels).ToArray();
            for (int i = 0; i < centers.Length; i++)
            {
                if (!map.CanOccupy(centers[i])) throw new InvalidOperationException("Stage 01 footprint is out of bounds or obstructed: " + centers[i]);
                for (int j = 0; j < i; j++)
                    if (Mathf.Abs(centers[i].x - centers[j].x) < 16 && Mathf.Abs(centers[i].y - centers[j].y) < 16)
                        throw new InvalidOperationException("Stage 01 footprints overlap.");
            }
        }
    }
}
