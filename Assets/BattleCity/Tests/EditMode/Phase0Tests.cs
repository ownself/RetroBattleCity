using System;
using System.IO;
using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BattleCity.Tests
{
    public sealed class Phase0Tests
    {
        [Test]
        public void CoordinateContract_RoundTripsAndBoundaries()
        {
            var p = new Vector2(104, 8);
            Assert.That(PixelGrid.ToMapPixels(PixelGrid.ToWorld(p)), Is.EqualTo(p));
            Assert.That(PixelGrid.ToWorld(new Vector2(208, 208)), Is.EqualTo(new Vector3(13, 13, 0)));
            Assert.That(PixelGrid.TerrainCellAt(new Vector2(-.01f, 0)).x, Is.EqualTo(-1));
            Assert.That(PixelGrid.TerrainCellAt(new Vector2(207.99f, 207.99f)), Is.EqualTo(new Vector2Int(25, 25)));
            Assert.That(PixelGrid.Contains(new Vector2(208, 0)), Is.False);
            Assert.That(PixelGrid.Contains(Vector2.zero), Is.True);
            Assert.That(PixelGrid.TerrainCellCenter(new Vector2Int(0, 0)), Is.EqualTo(new Vector2(4, 4)));
            Assert.That(PixelGrid.SourceRectToUnity(new RectInt(304, 32, 16, 16), 256), Is.EqualTo(new RectInt(304, 208, 16, 16)));
        }

        [Test]
        public void Catalog_All319SlicesMatchInventoryAndPixelContract()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            Assert.That(catalog, Is.Not.Null); Assert.DoesNotThrow(catalog.Validate);
            var rows = File.ReadAllLines("Docs/Stage0/SpriteInventory.csv").Skip(1).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            Assert.That(rows.Length, Is.EqualTo(319)); Assert.That(catalog.Entries.Count, Is.EqualTo(rows.Length));
            foreach (var row in rows)
            {
                var columns = row.Split(','); var sprite = catalog.Get(columns[0]);
                int x = int.Parse(columns[1]), y = int.Parse(columns[2]), w = int.Parse(columns[3]), h = int.Parse(columns[4]);
                Assert.That(sprite.rect, Is.EqualTo(new Rect(x, 256-y-h, w, h)), columns[0]);
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(w / 2f, h / 2f)), columns[0]);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
                Assert.That(sprite.texture.width, Is.EqualTo(400)); Assert.That(sprite.texture.height, Is.EqualTo(256));
            }
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => catalog.Get("missing"));
        }

        [Test]
        public void TankFrames_AllDirectionsUseSameCanvasAndPivot()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
            var tanks = catalog.Entries.Where(e => e.id.StartsWith("tank.", StringComparison.Ordinal)).ToArray();
            Assert.That(tanks.Length, Is.EqualTo(256));
            foreach (var entry in tanks)
            {
                Assert.That(entry.sprite.rect.size, Is.EqualTo(new Vector2(16, 16)), entry.id);
                Assert.That(entry.sprite.pivot, Is.EqualTo(new Vector2(8, 8)), entry.id);
                Assert.That(entry.sprite.bounds.center, Is.EqualTo(Vector3.zero), entry.id);
                Assert.That((Vector2)entry.sprite.bounds.size, Is.EqualTo(Vector2.one), entry.id);
            }
        }

        [Test]
        public void Importers_PointUncompressedNoMipAndUniformPPU()
        {
            foreach (var path in new[] { Phase0Baseline.AtlasPath, "Assets/Sprites/NES - Battle City (JPN) - Miscellaneous - General Sprites.png", "Assets/Sprites/NES - Battle City (JPN) - Backgrounds - Stage 01.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16)); Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(importer.GetPlatformTextureSettings("Standalone").overridden, Is.False);
            }
        }

        [Test]
        public void Transparency_BackgroundMaskPreservesTrueBlackAndNearBlackHudInk()
        {
            var source = new Texture2D(2, 2); var derived = new Texture2D(2, 2);
            try
            {
                source.LoadImage(File.ReadAllBytes("Assets/Sprites/NES - Battle City (JPN) - Miscellaneous - General Sprites.png"));
                derived.LoadImage(File.ReadAllBytes(Phase0Baseline.AtlasPath));
                var a = source.GetPixels32(); var b = derived.GetPixels32(); int changed = 0, black = 0;
                Assert.That(a.Length, Is.EqualTo(b.Length));
                for (int i = 0; i < a.Length; i++)
                {
                    var expected = a[i];
                    int x = i % 400, yTop = 255 - i / 400;
                    bool hudInkPanel = x >= 328 && x < 368 && yTop >= 176 && yTop < 200;
                    if (!hudInkPanel && expected.r == 0 && expected.g == 0 && expected.b == 1) { expected.a = 0; changed++; }
                    else if (expected.r == 0 && expected.g == 0 && expected.b == 0) black++;
                    Assert.That(b[i], Is.EqualTo(expected), "pixel " + i);
                }
                Assert.That(changed, Is.GreaterThan(0)); Assert.That(black, Is.EqualTo(1967));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(derived); }
        }

        [Test]
        public void ConfigurationSchemas_RejectInvalidValues()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>(); var tank = ScriptableObject.CreateInstance<TankDefinition>();
            try
            {
                Assert.DoesNotThrow(level.Validate); Assert.DoesNotThrow(tank.Validate);
                level.terrain[0] = (TerrainKind)999; Assert.Throws<InvalidOperationException>(level.Validate);
                level.terrain[0] = TerrainKind.Empty; level.playerSpawnPixels = Vector2Int.zero;
                Assert.Throws<InvalidOperationException>(level.Validate);
                tank.moveSpeedPixelsPerSecond = float.NaN; Assert.Throws<InvalidOperationException>(tank.Validate);
                var prototype = AssetDatabase.LoadAssetAtPath<TankDefinition>(Phase0Baseline.Root + "/Data/PlayerPrototype.asset");
                Assert.That(prototype.requiresCalibration, Is.True); Assert.DoesNotThrow(prototype.Validate);
                var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(Phase0Baseline.CatalogPath);
                foreach (var direction in new[] { "up", "left", "down", "right" })
                    for (int frame = 0; frame < 2; frame++) Assert.That(catalog.Get(prototype.spritePrefix + "." + direction + "." + frame), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(tank); }
        }

        [Test]
        public void BaselineScene_CameraBindingsSortingAndTilesAreValid()
        {
            var previous = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(Phase0Baseline.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Phase0Baseline.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects(); var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>()).Single();
                Assert.That(camera.transform.position, Is.EqualTo(PixelGrid.CameraPosition));
                Assert.That(camera.allowMSAA || camera.allowHDR || camera.allowDynamicResolution, Is.False);
                var pixel = camera.GetComponent<PixelPerfectCamera>();
                Assert.That(pixel.assetsPPU, Is.EqualTo(16)); Assert.That(pixel.refResolutionX, Is.EqualTo(256)); Assert.That(pixel.refResolutionY, Is.EqualTo(240));
                Assert.That(pixel.cropFrame, Is.EqualTo(PixelPerfectCamera.CropFrame.Windowbox));
                Assert.That(pixel.gridSnapping, Is.EqualTo(PixelPerfectCamera.GridSnapping.UpscaleRenderTexture));
                Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.False);
                var renderers = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>()).ToArray();
                Assert.That(renderers.Length, Is.GreaterThan(40));
                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sprite, Is.Not.Null, renderer.name);
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                    Assert.That(renderer.bounds.min.x, Is.GreaterThanOrEqualTo(-1f), renderer.name);
                    Assert.That(renderer.bounds.max.x, Is.LessThanOrEqualTo(15f), renderer.name);
                    Assert.That(renderer.bounds.min.y, Is.GreaterThanOrEqualTo(-1f), renderer.name);
                    Assert.That(renderer.bounds.max.y, Is.LessThanOrEqualTo(14f), renderer.name);
                }
                var tilemap = roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>()).Single();
                Assert.That(tilemap.GetUsedTilesCount(), Is.EqualTo(8));
                Assert.That(tilemap.GetComponentInParent<Grid>().cellSize, Is.EqualTo(new Vector3(.5f, .5f, 1)));
                for(int y=5;y<9;y++) for(int x=1;x<25;x++) Assert.That(tilemap.HasTile(new Vector3Int(x,y,0)), Is.True);
                var names = SortingLayer.layers.Select(l => l.name).ToArray();
                foreach (var layer in new[] { "Ground", "Obstacles", "Actors", "Projectiles", "Effects", "Foliage", "HUD" }) Assert.That(names, Does.Contain(layer));
                Assert.That(SortingLayer.layers.Select(l => l.id).Distinct().Count(), Is.EqualTo(SortingLayer.layers.Length));
                foreach (var renderer in renderers) Assert.That(renderer.sortingLayerName, Is.Not.EqualTo("Default"), renderer.name);
                Assert.That(GraphicsSettings.currentRenderPipeline, Is.TypeOf<UniversalRenderPipelineAsset>());
                Assert.That(((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).msaaSampleCount, Is.EqualTo(1));
                Assert.That(QualitySettings.antiAliasing, Is.EqualTo(0));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
