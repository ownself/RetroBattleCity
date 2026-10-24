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
        public const string EnemyPath = Phase0Baseline.Root + "/Data/EnemyBasicPrototype.asset";
        public const string WavePath = Phase0Baseline.Root + "/Data/Stage01EnemyWave.asset";
        public const string EnemyDefinitionId = "enemy.basic.prototype";
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

            var wave = BuildWaveAssets();
            // Save a COPY of the proven arena harness; never rebuild or save its source scene/data.
            var source = EditorSceneManager.OpenScene(Phase1Arena.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("Could not copy arena harness.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Opening another scene may unload the wave asset held only by the previous scene.
            wave = AssetDatabase.LoadAssetAtPath<EnemyWaveDefinition>(WavePath);
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            controller.name = "Stage 01 - Enter starts; R restarts; WASD or arrows; Space fires; Esc pauses";
            controller.level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            controller.enemyWaveDefinition = wave;
            controller.playerUpgradeDefinition = PlayerUpgradeAuthoring.Prepare();
            controller.pickupDefinition = PickupAuthoring.Prepare();
            controller.enablePlayerLifecycle = true;
            controller.enableSessionLifecycle = true;
            controller.playerRenderer.transform.position = PixelGrid.ToWorld(controller.level.playerSpawnPixels);
            controller.mapView.Render(new MapState(controller.level));
            var baseRenderer = new GameObject("Base - session state projection", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            baseRenderer.sprite = controller.sprites.Get("base.alive");
            baseRenderer.sharedMaterial = controller.playerRenderer.sharedMaterial;
            baseRenderer.sortingLayerName = "Actors";
            baseRenderer.transform.position = PixelGrid.ToWorld(controller.level.baseCenterPixels);
            controller.baseRenderer = baseRenderer;
            var hud = Stage01HudAuthoring.Create(controller);
            hud.Render(new ArenaSession(controller.level, controller.playerDefinition, wave, true,
                PlayerLifeParameters.Prototype, true, controller.playerUpgradeDefinition, controller.pickupDefinition), controller.level.stageNumber);
            controller.ValidateBindings();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save Stage 01.");
            return "BC-301 Stage 01 saved Ready with pixel HUD, session/player lifecycle, real base and 20 mixed prototype enemies: " + ScenePath;
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
            // Project prototype sequence, NOT a claim about the original game's first wave.
            level.enemySequence = EnemyTypesAuthoring.PrototypeSequence();
            level.maxActiveEnemies = 4;
            level.Validate();
        }

        private static EnemyWaveDefinition BuildWaveAssets()
        {
            var roster = EnemyTypesAuthoring.PrepareRoster();
            var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveDefinition>(WavePath);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
                AssetDatabase.CreateAsset(wave, WavePath);
            }
            wave.tankDefinitions = Enum.GetValues(typeof(EnemyKind)).Cast<EnemyKind>().Select(roster.Get).ToArray();
            wave.seed = 1;
            wave.preparationTicks = 60;
            wave.spawnIntervalTicks = 120;
            wave.requiresCalibration = true;
            wave.Validate();
            EditorUtility.SetDirty(wave);
            AssetDatabase.SaveAssetIfDirty(wave);
            return wave;
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
