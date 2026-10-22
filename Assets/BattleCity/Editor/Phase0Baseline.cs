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
    /// <summary>Explicit CLI-driven, repeatable M0 authoring; never runs automatically on import.</summary>
    public static class Phase0Baseline
    {
        public const string Root = "Assets/BattleCity";
        public const string CatalogPath = Root + "/Data/SpriteCatalog.asset";
        public const string ScenePath = Root + "/Scenes/PixelBaseline.unity";
        public const string AtlasPath = Root + "/Art/GeneralSprites.png";

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before rebuilding.");
            DetectPipeline();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (sprites.Length != 319) throw new InvalidOperationException("Run Tools/Phase0/PrepareSprites.cs first.");
            var catalog = LoadOrCreate<SpriteCatalog>(CatalogPath);
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries"); entries.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = sprites[i].name;
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprites[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); catalog.Validate(); EditorUtility.SetDirty(catalog);
            var tank = LoadOrCreate<TankDefinition>(Root + "/Data/PlayerPrototype.asset");
            tank.Validate(); EditorUtility.SetDirty(tank);
            ConfigureSortingLayers();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/SpriteUnlit.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("URP Sprite Unlit shader missing.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, Root + "/Art/SpriteUnlit.mat");
            }
            ConfigureRendering();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = PixelGrid.CameraPosition;
            camera.orthographic = true; camera.orthographicSize = 7.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(99, 99, 99, 255);
            camera.allowHDR = false; camera.allowMSAA = false; camera.allowDynamicResolution = false;
            camera.nearClipPlane = .3f; camera.farClipPlane = 30f;
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            var pixel = camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = PixelGrid.PixelsPerUnit; pixel.refResolutionX = PixelGrid.ReferenceWidth; pixel.refResolutionY = PixelGrid.ReferenceHeight;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixel.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            var black = CreateBlackSprite();
            var background = RenderSprite("Map bounds - 208x208 pixels (not a playable stage)", black, new Vector2(104, 104), material, "Ground");
            background.transform.localScale = new Vector3(13, 13, 1);
            var gallery = new GameObject("TankGallery - four palettes x eight variants").transform;
            string[] palettes = { "yellow", "white", "green", "red" };
            string[] variants = { "player.level0", "player.level1", "player.level2", "player.level3", "enemy.basic", "enemy.fast", "enemy.power", "enemy.armor" };
            for (int p = 0; p < palettes.Length; p++) for (int v = 0; v < variants.Length; v++)
            {
                var prefix = "tank." + palettes[p] + "." + variants[v];
                var renderer = RenderSprite(prefix, catalog.Get(prefix + ".up.0"), new Vector2(16 + v * 24, 184 - p * 24), material, "Actors");
                renderer.transform.SetParent(gallery, true);
            }
            string[] directions = { "up", "left", "down", "right" };
            for (int d = 0; d < 4; d++) for (int f = 0; f < 2; f++)
                RenderSprite("Direction + track pair " + directions[d] + f, catalog.Get("tank.yellow.player.level0." + directions[d] + "." + f), new Vector2(16 + (d * 2 + f) * 24, 88), material, "Actors");
            var grid = new GameObject("SeamCheckGrid - 8px cells", typeof(Grid)).GetComponent<Grid>();
            grid.cellSize = new Vector3(.5f, .5f, 1);
            var tilemap = new GameObject("Basic terrain seam check", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            tilemap.transform.SetParent(grid.transform, false);
            var tileRenderer = tilemap.GetComponent<TilemapRenderer>(); tileRenderer.sharedMaterial = material; tileRenderer.sortingLayerName = "Obstacles";
            for (int y = 0; y < 4; y++) for (int x = 0; x < 24; x++)
            {
                var id = (x < 12 ? "terrain.brick8." : "terrain.steel8.") + (x % 2) + "." + (1 - y % 2);
                var tile = LoadOrCreate<Tile>(Root + "/Data/" + id + ".asset");
                tile.sprite = catalog.Get(id); tile.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tile);
                tilemap.SetTile(new Vector3Int(x + 1, y + 5, 0), tile);
            }
            string[] samples = { "base.alive", "base.dead", "terrain.water.0", "terrain.grass16", "terrain.ice16", "pickup.star", "effect.explosion.small.1", "effect.explosion.large.0" };
            for (int i = 0; i < samples.Length; i++)
                RenderSprite(samples[i], catalog.Get(samples[i]), new Vector2(12 + i * 25, 20), material, "Effects");
            for (int i = 0; i < 10; i++)
                RenderSprite("Digit " + i, catalog.Get("hud.digit." + i), new Vector2(220 + i % 2 * 8, 180 - i / 2 * 8), material, "HUD");
            RenderSprite("Flag sample", catalog.Get("hud.flag"), new Vector2(224, 112), material, "HUD");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            return "M0 baseline saved: " + ScenePath + "; sprites=" + sprites.Length;
        }

        public static void SetGalleryFrame(string direction, int frame)
        {
            if (!new[] { "up", "left", "down", "right" }.Contains(direction) || frame < 0 || frame > 1) throw new ArgumentOutOfRangeException();
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(CatalogPath);
            var gallery = GameObject.Find("TankGallery - four palettes x eight variants");
            if (gallery == null) throw new InvalidOperationException("Open PixelBaseline first.");
            foreach (Transform child in gallery.transform) child.GetComponent<SpriteRenderer>().sprite = catalog.Get(child.name + "." + direction + "." + frame);
        }

        private static void DetectPipeline()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null || !(asset.GetType().FullName ?? "").Contains("Universal"))
                throw new InvalidOperationException("M0 requires the existing URP pipeline, not Built-in/HDRP.");
        }

        private static void ConfigureRendering()
        {
            var path = Root + "/Data/PixelBaselineURP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                pipeline = UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
                AssetDatabase.CreateAsset(pipeline, path);
            }
            pipeline.msaaSampleCount = 1; pipeline.supportsHDR = false; pipeline.renderScale = 1;
            EditorUtility.SetDirty(pipeline); GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline;
                QualitySettings.antiAliasing = 0; QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            }
            QualitySettings.SetQualityLevel(current, false);
            EditorSnapSettings.move = Vector3.one / PixelGrid.PixelsPerUnit;
        }

        private static void ConfigureSortingLayers()
        {
            var manager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serialized = new SerializedObject(manager); var layers = serialized.FindProperty("m_SortingLayers");
            string[] required = { "Ground", "Obstacles", "Actors", "Projectiles", "Effects", "Foliage", "HUD" };
            foreach (var name in required)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++) if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) exists = true;
                if (exists) continue;
                int index = layers.arraySize; layers.InsertArrayElementAtIndex(index);
                var layer = layers.GetArrayElementAtIndex(index); layer.FindPropertyRelative("name").stringValue = name;
                // TagManager stores an unsigned ID. Negative int assignments clamp to zero.
                layer.FindPropertyRelative("uniqueID").intValue = 0;
                layer.FindPropertyRelative("locked").boolValue = false;
            }
            var used = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < layers.arraySize; i++) used.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("uniqueID").intValue);
            int nextId = 1000001;
            for (int i = 0; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (layer.FindPropertyRelative("name").stringValue == "Default" || layer.FindPropertyRelative("uniqueID").intValue != 0) continue;
                while (used.Contains(nextId)) nextId++;
                layer.FindPropertyRelative("uniqueID").intValue = nextId; used.Add(nextId++);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite CreateBlackSprite()
        {
            var path = Root + "/Art/MapBackground.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "MapBackground", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels(Enumerable.Repeat(Color.black, 256).ToArray()); texture.Apply();
                AssetDatabase.CreateAsset(texture, path);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16, 0, SpriteMeshType.FullRect);
                sprite.name = "BlackSquare16"; AssetDatabase.AddObjectToAsset(sprite, texture);
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }

        private static SpriteRenderer RenderSprite(string name, Sprite sprite, Vector2 mapPixels, Material material, string layer)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingLayerName = layer;
            renderer.transform.position = PixelGrid.ToWorld(mapPixels); return renderer;
        }
    }
}
