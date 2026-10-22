using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace BattleCity.Tests
{
    // OverrideGeometry requires the runtime player loop in this Editor, even for transient sprites.
    public sealed class BrickMapViewTests
    {
        private readonly List<Object> assets = new List<Object>();
        private GameObject root;
        private ArenaMapView view;
        private LevelDefinition level;
        private TankDefinition tank;

        [SetUp] public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run Sprite geometry tests in the Test Runner PlayMode tab.");
            level = ScriptableObject.CreateInstance<LevelDefinition>(); assets.Add(level);
            tank = ScriptableObject.CreateInstance<TankDefinition>(); assets.Add(tank);
            tank.bulletSpeedPixelsPerSecond = 6000;
            root = new GameObject("Temporary brick test grid", typeof(Grid));
            root.GetComponent<Grid>().cellSize = new Vector3(.5f, .5f, 1);
            view = root.AddComponent<ArenaMapView>();
            view.obstacles = new GameObject("Display only", typeof(Tilemap)).GetComponent<Tilemap>();
            view.obstacles.transform.SetParent(root.transform, false);
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false); assets.Add(texture);
            texture.Apply(false, true); // Runtime clipping must NOT require readable textures.
            for (int i = 0; i < 4; i++)
            {
                var sprite = Sprite.Create(texture, new Rect(i % 2 * 8, i / 2 * 8, 8, 8), Vector2.one * .5f, 16, 0, SpriteMeshType.FullRect);
                assets.Add(sprite);
                var brick = ScriptableObject.CreateInstance<Tile>(); assets.Add(brick);
                var steel = ScriptableObject.CreateInstance<Tile>(); assets.Add(steel);
                brick.sprite = steel.sprite = sprite;
                brick.colliderType = steel.colliderType = Tile.ColliderType.None;
                view.brickTiles[i] = brick; view.steelTiles[i] = steel;
            }
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null; // ArenaMapView releases its runtime Tile/Sprite cache during OnDestroy.
            for (int i = assets.Count - 1; i >= 0; i--) if (assets[i] != null) Object.Destroy(assets[i]);
            assets.Clear();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private void Fire(MapState map, Vector2 point, CardinalDirection direction)
        {
            var bullets = new BulletWorld();
            var weapon = new WeaponState(tank, bullets);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.None, true),
                point - direction.Vector() * WeaponState.MuzzleOffsetPixels, direction), Is.True);
            bullets.Step(map);
            Assert.That(bullets.Hits.Count, Is.EqualTo(1));
            Assert.That(map.ApplyBulletHits(bullets.Hits), Is.GreaterThan(0));
        }

        [TestCase(12, 12, CardinalDirection.Up, 12)] [TestCase(13, 12, CardinalDirection.Up, 12)]
        [TestCase(12, 13, CardinalDirection.Up, 12)] [TestCase(13, 13, CardinalDirection.Up, 12)]
        [TestCase(12, 12, CardinalDirection.Down, 3)] [TestCase(13, 12, CardinalDirection.Down, 3)]
        [TestCase(12, 13, CardinalDirection.Down, 3)] [TestCase(13, 13, CardinalDirection.Down, 3)]
        [TestCase(12, 12, CardinalDirection.Right, 10)] [TestCase(13, 12, CardinalDirection.Right, 10)]
        [TestCase(12, 13, CardinalDirection.Right, 10)] [TestCase(13, 13, CardinalDirection.Right, 10)]
        [TestCase(12, 12, CardinalDirection.Left, 5)] [TestCase(13, 12, CardinalDirection.Left, 5)]
        [TestCase(12, 13, CardinalDirection.Left, 5)] [TestCase(13, 13, CardinalDirection.Left, 5)]
        public void AllSourceQuadrantsAndDirections_RenderOnlySurvivingHalf(int x, int y, CardinalDirection direction, byte mask)
        {
            level.terrain[y * 26 + x] = TerrainKind.Brick;
            var map = new MapState(level); view.Render(map);
            var cell = new Vector3Int(x, y, 0);
            int sourceQuadrant = x % 2 + (1 - y % 2) * 2;
            var source = (Tile)view.brickTiles[sourceQuadrant];
            Assert.That(view.obstacles.GetTile(cell), Is.SameAs(source));
            var center = new Vector2(x * 8 + 4, y * 8 + 4);
            Fire(map, center - direction.Vector() * 32, direction);
            view.Render(map);
            var partial = (Tile)view.obstacles.GetTile(cell);
            Assert.That(partial, Is.Not.SameAs(source));
            Assert.That(map.BrickMaskAt(x, y), Is.EqualTo(mask));
            AssertGeometry(partial.sprite, source.sprite, mask);
            Assert.That(partial.colliderType, Is.EqualTo(Tile.ColliderType.None));
            Assert.That(source.sprite.vertices.Length, Is.EqualTo(4), "Never modify the source sprite");
            view.Render(map);
            Assert.That(view.obstacles.GetTile(cell), Is.SameAs(partial), "Unchanged revision reuses display objects");
        }

        [TestCase(false, false, 1)] [TestCase(true, false, 2)]
        [TestCase(false, true, 4)] [TestCase(true, true, 8)]
        public void OrthogonalHits_KeepTheCorrectSingleFourPixelCorner(bool right, bool top, byte mask)
        {
            level.terrain[12 * 26 + 12] = TerrainKind.Brick;
            var map = new MapState(level); view.Render(map);
            var vertical = top ? CardinalDirection.Up : CardinalDirection.Down;
            Fire(map, new Vector2(100, 100) - vertical.Vector() * 32, vertical);
            var horizontal = right ? CardinalDirection.Right : CardinalDirection.Left;
            Fire(map, new Vector2(100, top ? 102 : 98) - horizontal.Vector() * 32, horizontal);
            view.Render(map);
            Assert.That(map.BrickMaskAt(12, 12), Is.EqualTo(mask));
            AssertGeometry(((Tile)view.obstacles.GetTile(new Vector3Int(12, 12, 0))).sprite,
                ((Tile)view.brickTiles[2]).sprite, mask);
        }

        private static void AssertGeometry(Sprite actual, Sprite source, byte mask)
        {
            Assert.That(actual.texture, Is.SameAs(source.texture));
            Assert.That(actual.rect, Is.EqualTo(source.rect));
            Assert.That(actual.pivot, Is.EqualTo(source.pivot));
            Assert.That(actual.pixelsPerUnit, Is.EqualTo(16));
            var vertices = actual.vertices; var triangles = actual.triangles; var uv = actual.uv;
            float totalArea = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var pixel = vertices[i] * 16 + source.pivot;
                var expectedUv = (source.rect.position + pixel) / new Vector2(source.texture.width, source.texture.height);
                Assert.That(Vector2.Distance(uv[i], expectedUv), Is.LessThan(.00001f));
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector2 a = vertices[triangles[t]] * 16 + source.pivot;
                Vector2 b = vertices[triangles[t + 1]] * 16 + source.pivot;
                Vector2 c = vertices[triangles[t + 2]] * 16 + source.pivot;
                var centroid = (a + b + c) / 3;
                int bit = Mathf.FloorToInt(centroid.x / 4) + Mathf.FloorToInt(centroid.y / 4) * 2;
                Assert.That(mask & (1 << bit), Is.Not.Zero, "No triangles may cover a destroyed quadrant");
                totalArea += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * .5f;
            }
            int remaining = 0;
            for (int bit = 0; bit < 4; bit++) if ((mask & (1 << bit)) != 0) remaining++;
            Assert.That(totalArea, Is.EqualTo(remaining * 16).Within(.001f));
        }

        [Test]
        public void MultipleDamageTicksBeforeRender_ClearTilesAndResetRestoresOriginals()
        {
            level.terrain[12 * 26 + 12] = TerrainKind.Brick;
            level.terrain[20 * 26 + 20] = TerrainKind.Steel;
            var map = new MapState(level); view.Render(map);
            var steel = view.obstacles.GetTile(new Vector3Int(20, 20, 0));
            Fire(map, new Vector2(100, 68), CardinalDirection.Up);
            Fire(map, new Vector2(100, 68), CardinalDirection.Up);
            view.Render(map);
            Assert.That(view.obstacles.HasTile(new Vector3Int(12, 12, 0)), Is.False);
            Assert.That(view.obstacles.GetTile(new Vector3Int(20, 20, 0)), Is.SameAs(steel));
            view.Render(new MapState(level));
            Assert.That(view.obstacles.GetTile(new Vector3Int(12, 12, 0)), Is.SameAs(view.brickTiles[2]));
        }

        [UnityTest]
        public IEnumerator PartialTilesAreCachedAcrossMapsAndDestroyedWithoutDestroyingSourceAssets()
        {
            level.terrain[12 * 26 + 12] = TerrainKind.Brick;
            var map = new MapState(level); view.Render(map);
            Fire(map, new Vector2(100, 68), CardinalDirection.Up); view.Render(map);
            var partial = (Tile)view.obstacles.GetTile(new Vector3Int(12, 12, 0)); var sprite = partial.sprite;
            var fresh = new MapState(level); view.Render(fresh);
            Fire(fresh, new Vector2(100, 68), CardinalDirection.Up); view.Render(fresh);
            Assert.That(view.obstacles.GetTile(new Vector3Int(12, 12, 0)), Is.SameAs(partial));
            var source = (Tile)view.brickTiles[2];
            Object.Destroy(root);
            yield return null;
            yield return null; // Cached objects are Destroy()ed from the component's OnDestroy.
            Assert.That(partial == null, Is.True); Assert.That(sprite == null, Is.True);
            Assert.That(source != null && source.sprite != null && source.sprite.texture != null, Is.True);
        }
    }
}
