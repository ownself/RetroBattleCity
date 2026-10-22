using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BattleCity.Editor
{
    /// <summary>Explicit authoring command; does not alter the M0 gallery or Build Settings.</summary>
    public static class Phase1Arena
    {
        public const string ScenePath = Phase0Baseline.Root + "/Scenes/CombatArena.unity";
        public const string LevelPath = Phase0Baseline.Root + "/Data/CombatArena.asset";

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before rebuilding.");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                throw new InvalidOperationException("The arena requires the M0 URP baseline.");
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }
            level.name = "CombatArena"; // Synthetic test layout, deliberately not the Stage 01 asset.
            level.terrain = new TerrainKind[26 * 26];
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.enemySequence = Array.Empty<string>();
            Fill(level, 48, 48, 16, 96, TerrainKind.Brick);
            Fill(level, 80, 48, 16, 96, TerrainKind.Steel); // 16px corridor
            Fill(level, 112, 48, 8, 96, TerrainKind.Brick);
            Fill(level, 128, 48, 8, 96, TerrainKind.Steel); // 8px: impassable for a 16px tank
            Fill(level, 152, 48, 16, 96, TerrainKind.Brick);
            Fill(level, 192, 48, 8, 96, TerrainKind.Steel); // 24px corridor
            Fill(level, 16, 168, 64, 16, TerrainKind.Brick);
            Fill(level, 112, 168, 72, 16, TerrainKind.Steel);
            level.Validate();
            var map = new MapState(level);
            if (!map.CanOccupy(level.playerSpawnPixels)) throw new InvalidOperationException("Arena spawn obstructed.");
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssetIfDirty(level);

            // Reuse the actually validated camera, rather than independently guessing its pixel settings.
            var baseline = EditorSceneManager.OpenScene(Phase0Baseline.ScenePath, OpenSceneMode.Single);
            var sourceCamera = baseline.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Single();
            var camera = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
            camera.name = "Main Camera";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(camera, scene);
            EditorSceneManager.CloseScene(baseline, true);
            SceneManager.SetActiveScene(scene);
            // Single-scene loads may unload unused assets despite local managed references.
            // Resolve bindings only after scene transitions, then immediately root them in the new scene.
            level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Phase0Baseline.Root + "/Art/SpriteUnlit.mat");
            var tank = AssetDatabase.LoadAssetAtPath<TankDefinition>(Phase0Baseline.Root + "/Data/PlayerPrototype.asset");
            if (catalog == null || material == null || tank == null) throw new InvalidOperationException("Complete M0 first.");
            var black = AssetDatabase.LoadAllAssetsAtPath(Phase0Baseline.Root + "/Art/MapBackground.asset").OfType<Sprite>().Single();
            var ground = CreateSprite("Ground - 208x208", black, new Vector2(104, 104), material, "Ground");
            ground.transform.localScale = new Vector3(13, 13, 1);
            var grid = new GameObject("MapGrid - 8px cells", typeof(Grid)).GetComponent<Grid>();
            grid.cellSize = new Vector3(.5f, .5f, 1);
            var obstacles = new GameObject("Obstacles - display only", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            obstacles.transform.SetParent(grid.transform, false);
            var renderer = obstacles.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = material; renderer.sortingLayerName = "Obstacles";
            var view = grid.gameObject.AddComponent<ArenaMapView>(); view.obstacles = obstacles;
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
            {
                view.brickTiles[x + y * 2] = AssetDatabase.LoadAssetAtPath<Tile>(Phase0Baseline.Root + "/Data/terrain.brick8." + x + "." + y + ".asset");
                view.steelTiles[x + y * 2] = AssetDatabase.LoadAssetAtPath<Tile>(Phase0Baseline.Root + "/Data/terrain.steel8." + x + "." + y + ".asset");
            }
            view.Render(map);
            var player = CreateSprite("Player", catalog.Get(tank.spritePrefix + ".up.0"), level.playerSpawnPixels, material, "Actors");
            var arena = new GameObject("Arena - WASD or arrows; Esc pauses; Space fires").AddComponent<ArenaController>();
            arena.level = level; arena.playerDefinition = tank; arena.sprites = catalog; arena.mapView = view; arena.playerRenderer = player;
            arena.ValidateBindings();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save arena.");
            return "BC-100..104 arena saved: " + ScenePath;
        }

        private static void Fill(LevelDefinition level, int x, int y, int width, int height, TerrainKind kind)
        {
            for (int row = y / 8; row < (y + height) / 8; row++)
                for (int col = x / 8; col < (x + width) / 8; col++) level.terrain[row * 26 + col] = kind;
        }

        private static SpriteRenderer CreateSprite(string name, Sprite sprite, Vector2 position, Material material, string layer)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingLayerName = layer;
            renderer.transform.position = PixelGrid.ToWorld(position);
            return renderer;
        }
    }
}
