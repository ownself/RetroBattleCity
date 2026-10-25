using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BattleCity
{
    /// <summary>Read-only, revision-based projection. Partial bricks clip geometry, not source textures.</summary>
    public sealed class ArenaMapView : MonoBehaviour
    {
        public Tilemap obstacles;
        // Source quadrant order x + sourceY*2; sourceY points down, map Y points up.
        public TileBase[] brickTiles = new TileBase[4];
        public TileBase[] steelTiles = new TileBase[4];
        private readonly Tile[,] partialTiles = new Tile[4, 16];
        private readonly int[] renderedCells = new int[PixelGrid.TerrainCellsPerAxis * PixelGrid.TerrainCellsPerAxis];
        private MapState renderedMap;
        private int renderedRevision;
        private Tilemap surfaces, foliage;
        private readonly TileBase[,] specialTiles = new TileBase[3, 4];
        private readonly System.Collections.Generic.List<UnityEngine.Object> ownedSpecial = new System.Collections.Generic.List<UnityEngine.Object>();
        public Tilemap Surfaces => surfaces;
        public Tilemap Foliage => foliage;

        /// <summary>Derive 8px tiles from the existing semantic 16px sprites; no imported asset mutation.</summary>
        public void ConfigureSpecialTerrain(SpriteCatalog catalog)
        {
            if (specialTiles[0, 0] != null) return;
            ValidateBindings();
            string[] ids = { "terrain.water.0", "terrain.grass16", "terrain.ice16" };
            for (int kind = 0; kind < ids.Length; kind++)
            {
                var source = catalog.Get(ids[kind]);
                if (source.packed || source.rect.size != new Vector2(16, 16))
                    throw new InvalidOperationException("Special terrain needs unpacked 16px semantic sprites.");
                for (int sy = 0; sy < 2; sy++) for (int x = 0; x < 2; x++)
                {
                    var sprite = Sprite.Create(source.texture, new Rect(source.rect.x + x * 8,
                        source.rect.y + (1 - sy) * 8, 8, 8), Vector2.one * .5f, PixelGrid.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = ids[kind] + ".quadrant." + x + "." + sy;
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite; tile.colliderType = Tile.ColliderType.None; tile.hideFlags = HideFlags.HideAndDontSave;
                    specialTiles[kind, x + sy * 2] = tile;
                    ownedSpecial.Add(tile); ownedSpecial.Add(sprite);
                }
            }
            // Do not create a second collision system. Layers are display-only siblings of obstacles.
            surfaces = CreateLayer("Water and ice - display only", "Obstacles", -1);
            foliage = CreateLayer("Grass - hides tanks, not projectiles or effects", "Actors", 1);
            renderedMap = null;
        }

        private Tilemap CreateLayer(string label, string sortingLayer, int order)
        {
            var layer = new GameObject(label, typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            layer.transform.SetParent(obstacles.transform.parent, false);
            var renderer = layer.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = obstacles.GetComponent<TilemapRenderer>().sharedMaterial;
            renderer.sortingLayerName = sortingLayer; renderer.sortingOrder = order;
            return layer;
        }

        public void Render(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (ReferenceEquals(map, renderedMap) && renderedRevision == map.Revision) return;
            ValidateBindings();
            bool full = !ReferenceEquals(map, renderedMap);
            if (full)
            {
                obstacles.ClearAllTiles();
                if (surfaces != null) surfaces.ClearAllTiles();
                if (foliage != null) foliage.ClearAllTiles();
            }
            for (int y = 0; y < PixelGrid.TerrainCellsPerAxis; y++) for (int x = 0; x < PixelGrid.TerrainCellsPerAxis; x++)
            {
                int index = y * PixelGrid.TerrainCellsPerAxis + x;
                if (!full && renderedCells[index] == map.CellRevisionAt(x, y)) continue;
                TileBase tile = null;
                int quadrant = x % 2 + (1 - y % 2) * 2;
                switch (map.TerrainAt(x, y))
                {
                    case TerrainKind.Empty: break;
                    case TerrainKind.Brick: tile = BrickTile(quadrant, map.BrickMaskAt(x, y)); break;
                    case TerrainKind.Steel: tile = steelTiles[quadrant]; break;
                    case TerrainKind.Water:
                    case TerrainKind.Grass:
                    case TerrainKind.Ice:
                        if (surfaces == null || foliage == null) throw new InvalidOperationException("Configure special terrain before rendering it.");
                        break;
                    default: throw new InvalidOperationException("Unknown terrain.");
                }
                var cell = new Vector3Int(x, y, 0);
                var kind = map.TerrainAt(x, y);
                obstacles.SetTile(cell, tile);
                if (surfaces != null) surfaces.SetTile(cell, kind == TerrainKind.Water ? specialTiles[0, quadrant] :
                    kind == TerrainKind.Ice ? specialTiles[2, quadrant] : null);
                if (foliage != null) foliage.SetTile(cell, kind == TerrainKind.Grass ? specialTiles[1, quadrant] : null);
                renderedCells[index] = map.CellRevisionAt(x, y);
            }
            renderedMap = map; renderedRevision = map.Revision;
        }

        private TileBase BrickTile(int quadrant, byte mask)
        {
            if (mask == 0) return null;
            if (mask == 15) return brickTiles[quadrant];
            if (partialTiles[quadrant, mask] != null) return partialTiles[quadrant, mask];
            var source = (Tile)brickTiles[quadrant];
            var original = source.sprite;
            var sprite = Sprite.Create(original.texture, original.rect,
                original.pivot / original.rect.size, original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = original.name + ".mask." + mask;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            int count = 0;
            for (int bit = 0; bit < 4; bit++) if ((mask & (1 << bit)) != 0) count++;
            var vertices = new Vector2[count * 4];
            var triangles = new ushort[count * 6];
            int quad = 0;
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
            {
                if ((mask & (1 << (x + y * 2))) == 0) continue;
                int v = quad * 4, t = quad * 6;
                // OverrideGeometry takes RECT PIXELS (0..8), not Sprite.vertices' pivot-relative units.
                vertices[v] = new Vector2(x * 4, y * 4);
                vertices[v + 1] = new Vector2(x * 4, y * 4 + 4);
                vertices[v + 2] = new Vector2(x * 4 + 4, y * 4 + 4);
                vertices[v + 3] = new Vector2(x * 4 + 4, y * 4);
                triangles[t] = (ushort)v; triangles[t + 1] = (ushort)(v + 1); triangles[t + 2] = (ushort)(v + 2);
                triangles[t + 3] = (ushort)v; triangles[t + 4] = (ushort)(v + 2); triangles[t + 5] = (ushort)(v + 3);
                quad++;
            }
            sprite.OverrideGeometry(vertices, triangles); // UVs follow the original 8px texture rectangle.
            var tile = Instantiate(source);
            tile.name = source.name + ".mask." + mask;
            tile.hideFlags = HideFlags.HideAndDontSave;
            tile.sprite = sprite; tile.colliderType = Tile.ColliderType.None;
            partialTiles[quadrant, mask] = tile;
            return tile;
        }

        private void OnDestroy()
        {
            // Layers are siblings; destroying just this component must not strand hidden tiles.
            if (surfaces != null) DestroyOwned(surfaces.gameObject);
            if (foliage != null) DestroyOwned(foliage.gameObject);
            foreach (var owned in ownedSpecial) if (owned != null) DestroyOwned(owned);
            ownedSpecial.Clear();
            for (int quadrant = 0; quadrant < 4; quadrant++) for (int mask = 1; mask < 15; mask++)
            {
                var tile = partialTiles[quadrant, mask];
                if (tile == null) continue;
                var sprite = tile.sprite;
                if (Application.isPlaying) { Destroy(tile); Destroy(sprite); }
                else { DestroyImmediate(tile); DestroyImmediate(sprite); }
                partialTiles[quadrant, mask] = null;
            }
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
        }

        public void ValidateBindings()
        {
            if (obstacles == null || brickTiles == null || steelTiles == null || brickTiles.Length != 4 || steelTiles.Length != 4)
                throw new InvalidOperationException("Invalid arena map bindings.");
            for (int i = 0; i < 4; i++)
            {
                if (!(brickTiles[i] is Tile brick) || brick.sprite == null || steelTiles[i] == null)
                    throw new InvalidOperationException("Missing terrain tile or brick sprite.");
                if (brick.sprite.rect.size != new Vector2(8, 8) || brick.sprite.pixelsPerUnit != PixelGrid.PixelsPerUnit ||
                    brick.sprite.pivot != new Vector2(4, 4) || brick.sprite.packed)
                    throw new InvalidOperationException("Partial bricks require the baseline unpacked 8px/PPU16/center-pivot sprites.");
            }
        }
    }
}
