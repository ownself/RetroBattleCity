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

        public void Render(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (ReferenceEquals(map, renderedMap) && renderedRevision == map.Revision) return;
            ValidateBindings();
            bool full = !ReferenceEquals(map, renderedMap);
            if (full) obstacles.ClearAllTiles();
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
                    default: throw new InvalidOperationException("M1 arena supports Empty, Brick and Steel only.");
                }
                obstacles.SetTile(new Vector3Int(x, y, 0), tile);
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
