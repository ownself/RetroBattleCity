using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>The collision truth, copied from authoring data. Never queries a Tilemap or Transform.</summary>
    public sealed class MapState
    {
        public const float Epsilon = .0001f;
        public const int DamageCellsPerAxis = PixelGrid.MapSizePixels / PixelGrid.DestructionCellPixels;
        private readonly TerrainKind[] terrain;
        // Each 8px brick owns four 4px quadrants, bits x + y*2 (Y up).
        private readonly byte[] bricks;

        public MapState(LevelDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            terrain = (TerrainKind[])definition.terrain.Clone();
            bricks = new byte[terrain.Length];
            for (int i = 0; i < terrain.Length; i++) if (terrain[i] == TerrainKind.Brick) bricks[i] = 15;
        }

        public TerrainKind TerrainAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= PixelGrid.TerrainCellsPerAxis || y >= PixelGrid.TerrainCellsPerAxis)
                throw new ArgumentOutOfRangeException();
            return terrain[y * PixelGrid.TerrainCellsPerAxis + x];
        }

        public bool IsSolid(int damageX, int damageY)
        {
            if (damageX < 0 || damageY < 0 || damageX >= DamageCellsPerAxis || damageY >= DamageCellsPerAxis) return true;
            int index = damageY / 2 * PixelGrid.TerrainCellsPerAxis + damageX / 2;
            switch (terrain[index])
            {
                case TerrainKind.Brick: return (bricks[index] & (1 << (damageX % 2 + damageY % 2 * 2))) != 0;
                case TerrainKind.Steel:
                case TerrainKind.Water: return true;
                default: return false;
            }
        }

        /// <summary>Projectile solidity is intentionally separate: water blocks tanks, not bullets.</summary>
        public bool BlocksBullet(int damageX, int damageY)
        {
            if (damageX < 0 || damageY < 0 || damageX >= DamageCellsPerAxis || damageY >= DamageCellsPerAxis) return false;
            var kind = TerrainAt(damageX / 2, damageY / 2);
            return kind == TerrainKind.Steel || (kind == TerrainKind.Brick && IsSolid(damageX, damageY));
        }

        public bool CanOccupy(Vector2 center, float halfSize = 8)
        {
            var min = center - Vector2.one * halfSize; var max = center + Vector2.one * halfSize;
            if (min.x < -Epsilon || min.y < -Epsilon || max.x > PixelGrid.MapSizePixels + Epsilon || max.y > PixelGrid.MapSizePixels + Epsilon) return false;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((min.x + Epsilon) / 4));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((min.y + Epsilon) / 4));
            int x1 = Mathf.Min(DamageCellsPerAxis - 1, Mathf.FloorToInt((max.x - Epsilon) / 4));
            int y1 = Mathf.Min(DamageCellsPerAxis - 1, Mathf.FloorToInt((max.y - Epsilon) / 4));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (IsSolid(x, y)) return false;
            return true;
        }

        /// <summary>Axis-aligned swept AABB, clamps exactly to the first face, even across many cells.</summary>
        public Vector2 MoveAxis(Vector2 center, Vector2 delta, float halfSize = 8)
        {
            if (delta.x != 0 && delta.y != 0) throw new ArgumentException("Movement must be cardinal.");
            if (delta == Vector2.zero) return center;
            bool horizontal = delta.x != 0;
            float start = horizontal ? center.x : center.y;
            float amount = horizontal ? delta.x : delta.y;
            float perpendicular = horizontal ? center.y : center.x;
            float end = Mathf.Clamp(start + amount, halfSize, PixelGrid.MapSizePixels - halfSize);
            int acrossMin = Mathf.Max(0, Mathf.FloorToInt((perpendicular - halfSize + Epsilon) / 4));
            int acrossMax = Mathf.Min(DamageCellsPerAxis - 1, Mathf.FloorToInt((perpendicular + halfSize - Epsilon) / 4));
            int alongMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(start, end) - halfSize + Epsilon) / 4));
            int alongMax = Mathf.Min(DamageCellsPerAxis - 1, Mathf.FloorToInt((Mathf.Max(start, end) + halfSize - Epsilon) / 4));
            for (int a = alongMin; a <= alongMax; a++) for (int b = acrossMin; b <= acrossMax; b++)
            {
                if (!IsSolid(horizontal ? a : b, horizontal ? b : a)) continue;
                if (amount > 0 && a * 4 >= start + halfSize - Epsilon) end = Mathf.Min(end, a * 4 - halfSize);
                else if (amount < 0 && (a + 1) * 4 <= start - halfSize + Epsilon) end = Mathf.Max(end, (a + 1) * 4 + halfSize);
            }
            return horizontal ? new Vector2(end, center.y) : new Vector2(center.x, end);
        }
    }
}
