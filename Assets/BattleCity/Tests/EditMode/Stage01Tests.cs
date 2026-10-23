using System;
using System.IO;
using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BattleCity.Tests
{
    public sealed class Stage01Tests
    {
        private static LevelDefinition Level => AssetDatabase.LoadAssetAtPath<LevelDefinition>(Stage01Authoring.LevelPath);

        [Test]
        public void Stage01_SerializedTerrainOnDiskHasExactly676ValidCellsMatchingLayout()
        {
            // Unity's enum[] scalar is little-endian 32-bit hex. A cached asset can hide malformed disk bytes.
            var lines = File.ReadAllLines(Stage01Authoring.LevelPath);
            var fields = lines.Where(line => line.StartsWith("  terrain: ", StringComparison.Ordinal)).ToArray();
            Assert.That(fields.Length, Is.EqualTo(1));
            var hex = fields[0].Substring("  terrain: ".Length);
            Assert.That(hex, Does.Match(@"\A[0-9a-fA-F]{5408}\z"), "Disk terrain must be exactly 676 complete 32-bit enum values, without an ignored suffix");
            var rows = File.ReadAllLines(Stage01Authoring.LayoutPath);
            Assert.That(rows.Length, Is.EqualTo(26));
            Assert.That(rows.All(row => row.Length == 26), Is.True);
            for (int i = 0; i < 676; i++)
            {
                uint value = 0;
                for (int b = 0; b < 4; b++) value |= (uint)Convert.ToByte(hex.Substring(i * 8 + b * 2, 2), 16) << (b * 8);
                Assert.That(Enum.IsDefined(typeof(TerrainKind), (int)value), Is.True, "Disk cell " + i);
                char symbol = rows[25 - i / 26][i % 26];
                Assert.That(".BSH", Does.Contain(symbol.ToString()), "Unknown layout symbol");
                var expected = symbol == 'B' ? TerrainKind.Brick : symbol == 'S' ? TerrainKind.Steel : TerrainKind.Empty;
                Assert.That((TerrainKind)value, Is.EqualTo(expected), "Disk layout cell " + i);
                Assert.That(Level.terrain[i], Is.EqualTo((TerrainKind)value), "Imported asset must match disk cell " + i);
            }
        }

        [Test]
        public void Stage01_All676CellsMatchIndependentPngSamples()
        {
            // Independent oracle: source PNG color histograms, not the recipe or its matrix parser.
            var png = new Texture2D(2, 2);
            try
            {
                png.LoadImage(File.ReadAllBytes(Stage01Authoring.ReferencePath));
                Assert.That(new Vector2Int(png.width, png.height), Is.EqualTo(new Vector2Int(208, 208)));
                int bricks = 0, steel = 0, empty = 0, baseGray = 0, baseDark = 0, baseBlack = 0;
                for (int sourceY = 0; sourceY < 26; sourceY++) for (int x = 0; x < 26; x++)
                {
                    var pixels = Enumerable.Range(0, 64).Select(i => (Color32)png.GetPixel(x * 8 + i % 8, 207 - (sourceY * 8 + i / 8))).ToArray();
                    bool isBase = x >= 12 && x <= 13 && sourceY >= 24;
                    TerrainKind expected;
                    if (isBase)
                    {
                        baseGray += pixels.Count(p => Rgb(p, 102, 102, 102));
                        baseDark += pixels.Count(p => Rgb(p, 108, 7, 0));
                        baseBlack += pixels.Count(p => Rgb(p, 0, 0, 0));
                        expected = TerrainKind.Empty;
                    }
                    else if (pixels.All(p => Rgb(p, 0, 0, 0))) { expected = TerrainKind.Empty; empty++; }
                    else if (pixels.Count(p => Rgb(p, 153, 79, 0)) == 24 && pixels.Count(p => Rgb(p, 108, 7, 0)) == 18 && pixels.Count(p => Rgb(p, 102, 102, 102)) == 22)
                    { expected = TerrainKind.Brick; bricks++; }
                    else if (pixels.Count(p => Rgb(p, 174, 174, 174)) == 26 && pixels.Count(p => Rgb(p, 254, 254, 254)) == 16 && pixels.Count(p => Rgb(p, 102, 102, 102)) == 22)
                    { expected = TerrainKind.Steel; steel++; }
                    else throw new AssertionException($"Unclassified reference terrain at source cell ({x},{sourceY})");
                    Assert.That(Level.terrain[(25 - sourceY) * 26 + x], Is.EqualTo(expected), $"source ({x},{sourceY}), runtime ({x},{25-sourceY})");
                }
                Assert.That(bricks, Is.EqualTo(220)); Assert.That(steel, Is.EqualTo(8)); Assert.That(empty, Is.EqualTo(444));
                Assert.That(baseGray, Is.EqualTo(115)); Assert.That(baseDark, Is.EqualTo(7)); Assert.That(baseBlack, Is.EqualTo(134));
            }
            finally { UnityEngine.Object.DestroyImmediate(png); }
        }

        [Test]
        public void Stage01_AsymmetricLandmarksAndBaseProtectionHaveCorrectYFlip()
        {
            var map = new MapState(Level);
            foreach (var cell in new[] { new Vector2Int(12, 19), new Vector2Int(13, 19), new Vector2Int(12, 18), new Vector2Int(13, 18), new Vector2Int(0, 11), new Vector2Int(1, 11), new Vector2Int(24, 11), new Vector2Int(25, 11) })
                Assert.That(map.TerrainAt(cell.x, cell.y), Is.EqualTo(TerrainKind.Steel), cell.ToString());
            Assert.That(map.TerrainAt(0, 12), Is.EqualTo(TerrainKind.Brick), "Side ledge: brick ABOVE steel");
            Assert.That(map.TerrainAt(0, 14), Is.EqualTo(TerrainKind.Empty), "Detect an upside-down stage");
            for (int x = 11; x <= 14; x++) Assert.That(map.TerrainAt(x, 2), Is.EqualTo(TerrainKind.Brick));
            for (int y = 0; y < 2; y++)
            {
                Assert.That(map.TerrainAt(11, y), Is.EqualTo(TerrainKind.Brick));
                Assert.That(map.TerrainAt(14, y), Is.EqualTo(TerrainKind.Brick));
                Assert.That(map.TerrainAt(12, y), Is.EqualTo(TerrainKind.Empty));
                Assert.That(map.TerrainAt(13, y), Is.EqualTo(TerrainKind.Empty));
            }
            Assert.That(Level.baseCenterPixels, Is.EqualTo(new Vector2Int(104, 8)));
        }

        [Test]
        public void Stage01_FrozenSpawnsFitAndDoNotOverlapBaseOrTerrain()
        {
            Assert.That(Level.playerSpawnPixels, Is.EqualTo(new Vector2Int(72, 8)));
            Assert.That(Level.enemySpawnPixels, Is.EqualTo(new[] { new Vector2Int(8, 200), new Vector2Int(104, 200), new Vector2Int(200, 200) }));
            Assert.That(Level.enemySequence, Is.Empty, "Wave content is deferred, not claimed as verified from PNG");
            Assert.DoesNotThrow(() => Stage01Authoring.ValidatePlacement(Level));
            var copy = UnityEngine.Object.Instantiate(Level);
            try
            {
                copy.enemySpawnPixels[0] = copy.playerSpawnPixels;
                Assert.Throws<InvalidOperationException>(() => Stage01Authoring.ValidatePlacement(copy));
                copy.enemySpawnPixels[0] = new Vector2Int(24, 24); // Source column bricks near the bottom.
                Assert.Throws<InvalidOperationException>(() => Stage01Authoring.ValidatePlacement(copy));
                copy.enemySpawnPixels[0] = Vector2Int.zero;
                Assert.Throws<InvalidOperationException>(() => Stage01Authoring.ValidatePlacement(copy));
                copy.enemySpawnPixels[0] = copy.baseCenterPixels;
                Assert.Throws<InvalidOperationException>(() => Stage01Authoring.ValidatePlacement(copy));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void Stage01_RuntimeDestructionDoesNotMutateAuthoringData()
        {
            var before = (TerrainKind[])Level.terrain.Clone();
            var map = new MapState(Level);
            var tank = AssetDatabase.LoadAssetAtPath<TankDefinition>(Phase0Baseline.Root + "/Data/PlayerPrototype.asset");
            var session = new ArenaSession(Level, tank);
            session.SetFocus(true); session.SetPaused(false);
            // Spawn x=72 has a clear vertical corridor; move left to the x=56 firing lane under brick.
            for (int i = 0; i < 16; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.A);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W | PlayerButtons.Fire);
            for (int i = 0; i < 60 && session.Map.Revision == 0; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
            Assert.That(session.Map.Revision, Is.GreaterThan(0));
            Assert.That(Level.terrain, Is.EqualTo(before));
            Assert.That(map.Revision, Is.Zero);
            Assert.That(new MapState(Level).BrickMaskAt(6, 2), Is.EqualTo(15));
        }

        [Test]
        public void Stage01_SavedSceneBindsInteractiveTilesCameraAndIntactBaseOnly()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(Stage01Authoring.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Stage01Authoring.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controller = roots.SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
                Assert.That(controller.level, Is.SameAs(Level)); Assert.DoesNotThrow(controller.ValidateBindings);
                Assert.That(PixelGrid.ToMapPixels(controller.playerRenderer.transform.position), Is.EqualTo((Vector2)Level.playerSpawnPixels));
                var baseRenderer = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>()).Single(r => r.sprite.name == "base.alive");
                Assert.That(PixelGrid.ToMapPixels(baseRenderer.transform.position), Is.EqualTo((Vector2)Level.baseCenterPixels));
                Assert.That(baseRenderer.GetComponents<Component>().Length, Is.EqualTo(2), "Base is display only, no damage/death behaviour");
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>()).Count(r => r.sprite.name.StartsWith("tank.")), Is.EqualTo(1), "No fake runtime enemy tanks");
                var map = new MapState(Level);
                for (int y = 0; y < 26; y++) for (int x = 0; x < 26; x++)
                {
                    var cell = new Vector3Int(x, y, 0); var kind = map.TerrainAt(x, y);
                    var expected = kind == TerrainKind.Empty ? null : (kind == TerrainKind.Brick ? controller.mapView.brickTiles : controller.mapView.steelTiles)[x % 2 + (1 - y % 2) * 2];
                    Assert.That(controller.mapView.obstacles.GetTile(cell), Is.SameAs(expected), cell.ToString());
                }
                var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>()).Single();
                Assert.That(camera.transform.position, Is.EqualTo(PixelGrid.CameraPosition));
                var pixel = camera.GetComponent<PixelPerfectCamera>();
                Assert.That(pixel.assetsPPU, Is.EqualTo(16)); Assert.That(pixel.refResolutionX, Is.EqualTo(256)); Assert.That(pixel.refResolutionY, Is.EqualTo(240));
                Assert.That(pixel.cropFrame, Is.EqualTo(PixelPerfectCamera.CropFrame.Windowbox));
                Assert.That(pixel.gridSnapping, Is.EqualTo(PixelPerfectCamera.GridSnapping.UpscaleRenderTexture));
                Assert.That(controller.mapView.obstacles.GetComponentInParent<Grid>().cellSize, Is.EqualTo(new Vector3(.5f,.5f,1)));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static bool Rgb(Color32 p, byte r, byte g, byte b) => p.r == r && p.g == g && p.b == b && p.a == 255;
    }
}
