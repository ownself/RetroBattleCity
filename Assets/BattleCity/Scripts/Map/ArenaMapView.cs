using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BattleCity
{
    /// <summary>Read-only projection of the M1 map, no colliders and no independent obstacle state.</summary>
    public sealed class ArenaMapView : MonoBehaviour
    {
        public Tilemap obstacles;
        // Source quadrant order x + sourceY*2; sourceY points down, map Y points up.
        public TileBase[] brickTiles = new TileBase[4];
        public TileBase[] steelTiles = new TileBase[4];

        public void Render(MapState map)
        {
            ValidateBindings();
            obstacles.ClearAllTiles();
            for (int y = 0; y < PixelGrid.TerrainCellsPerAxis; y++) for (int x = 0; x < PixelGrid.TerrainCellsPerAxis; x++)
            {
                TileBase tile;
                int quadrant = x % 2 + (1 - y % 2) * 2;
                switch (map.TerrainAt(x, y))
                {
                    case TerrainKind.Empty: continue;
                    case TerrainKind.Brick: tile = brickTiles[quadrant]; break;
                    case TerrainKind.Steel: tile = steelTiles[quadrant]; break;
                    default: throw new InvalidOperationException("M1 arena supports Empty, Brick and Steel only.");
                }
                obstacles.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        public void ValidateBindings()
        {
            if (obstacles == null || brickTiles == null || steelTiles == null || brickTiles.Length != 4 || steelTiles.Length != 4)
                throw new InvalidOperationException("Invalid arena map bindings.");
            for (int i = 0; i < 4; i++)
                if (brickTiles[i] == null || steelTiles[i] == null) throw new InvalidOperationException("Missing terrain tile.");
        }
    }
}
