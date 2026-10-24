using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>The collision truth, copied from authoring data. Never queries a Tilemap or Transform.</summary>
    public sealed class MapState
    {
        public const float Epsilon = .0001f;
        public const int BrickStripWidthPixels = 16; // Prototype, not a verified original-game value.
        public const int DamageCellsPerAxis = PixelGrid.MapSizePixels / PixelGrid.DestructionCellPixels;
        private readonly TerrainKind[] terrain;
        // Each 8px brick owns four 4px quadrants, bits x + y*2 (Y up).
        private readonly byte[] bricks;
        private readonly int[] cellRevisions;
        public int Revision { get; private set; }

        public MapState(LevelDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            terrain = (TerrainKind[])definition.terrain.Clone();
            bricks = new byte[terrain.Length];
            cellRevisions = new int[terrain.Length];
            for (int i = 0; i < terrain.Length; i++) if (terrain[i] == TerrainKind.Brick) bricks[i] = 15;
        }

        public TerrainKind TerrainAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= PixelGrid.TerrainCellsPerAxis || y >= PixelGrid.TerrainCellsPerAxis)
                throw new ArgumentOutOfRangeException();
            return terrain[y * PixelGrid.TerrainCellsPerAxis + x];
        }

        public byte BrickMaskAt(int x, int y)
        {
            TerrainAt(x, y); // Shared bounds validation.
            return bricks[y * PixelGrid.TerrainCellsPerAxis + x];
        }

        public int CellRevisionAt(int x, int y)
        {
            TerrainAt(x, y);
            return cellRevisions[y * PixelGrid.TerrainCellsPerAxis + x];
        }

        /// <summary>Damage phase, AFTER all sweeps for this tick. Returns removed 4px cells.</summary>
        public int ApplyBulletHits(IReadOnlyList<BulletHit> hits)
        {
            if (hits == null) throw new ArgumentNullException(nameof(hits));
            int removed = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.Kind != BulletHitKind.Terrain) continue;
                if (hit.Terrain == TerrainKind.Steel && hit.CanDestroySteel)
                {
                    // Project default: consume the projectile and remove only the contacted 8px steel tile.
                    // All bullets still sweep the pre-damage map; no same-tick piercing through a new hole.
                    int steelIndex = hit.DamageCell.y / 2 * PixelGrid.TerrainCellsPerAxis + hit.DamageCell.x / 2;
                    if (terrain[steelIndex] == TerrainKind.Steel)
                    {
                        terrain[steelIndex] = TerrainKind.Empty;
                        cellRevisions[steelIndex] = ++Revision;
                        removed += 4;
                    }
                    continue;
                }
                if (hit.Terrain != TerrainKind.Brick) continue;
                bool horizontal = hit.Direction == CardinalDirection.Left || hit.Direction == CardinalDirection.Right;
                if (!horizontal && hit.Direction != CardinalDirection.Up && hit.Direction != CardinalDirection.Down) continue;
                int along = horizontal ? hit.DamageCell.x : hit.DamageCell.y;
                float across = horizontal ? hit.Position.y : hit.Position.x;
                // Lower edge is floored to the 4px lattice; never shift an edge-clipped strip inward.
                int first = Mathf.FloorToInt((across - BrickStripWidthPixels / 2 + Epsilon) / PixelGrid.DestructionCellPixels);
                for (int offset = 0; offset < BrickStripWidthPixels / PixelGrid.DestructionCellPixels; offset++)
                {
                    int x = horizontal ? along : first + offset;
                    int y = horizontal ? first + offset : along;
                    if (x < 0 || y < 0 || x >= DamageCellsPerAxis || y >= DamageCellsPerAxis) continue;
                    int index = y / 2 * PixelGrid.TerrainCellsPerAxis + x / 2;
                    if (terrain[index] != TerrainKind.Brick) continue;
                    int bit = 1 << (x % 2 + y % 2 * 2);
                    if ((bricks[index] & bit) == 0) continue;
                    bricks[index] = (byte)(bricks[index] & ~bit);
                    if (bricks[index] == 0) terrain[index] = TerrainKind.Empty;
                    cellRevisions[index] = ++Revision;
                    removed++;
                }
            }
            return removed;
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

        public bool CanOccupy(Vector2 center, float halfSize = TankMotor.HalfSizePixels,
            IReadOnlyList<Rect> blockers = null)
        {
            var min = center - Vector2.one * halfSize; var max = center + Vector2.one * halfSize;
            if (min.x < -Epsilon || min.y < -Epsilon || max.x > PixelGrid.MapSizePixels + Epsilon || max.y > PixelGrid.MapSizePixels + Epsilon) return false;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((min.x + Epsilon) / 4));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((min.y + Epsilon) / 4));
            int x1 = Mathf.Min(DamageCellsPerAxis - 1, Mathf.FloorToInt((max.x - Epsilon) / 4));
            int y1 = Mathf.Min(DamageCellsPerAxis - 1, Mathf.FloorToInt((max.y - Epsilon) / 4));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (IsSolid(x, y)) return false;
            if (blockers != null) for (int i = 0; i < blockers.Count; i++)
            {
                var bounds = blockers[i];
                if (max.x > bounds.xMin + Epsilon && min.x < bounds.xMax - Epsilon &&
                    max.y > bounds.yMin + Epsilon && min.y < bounds.yMax - Epsilon) return false;
            }
            return true;
        }

        /// <summary>Axis-aligned swept AABB, clamps exactly to the first face, even across many cells.</summary>
        public Vector2 MoveAxis(Vector2 center, Vector2 delta, float halfSize = TankMotor.HalfSizePixels,
            IReadOnlyList<Rect> blockers = null)
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
            if (blockers != null) for (int i = 0; i < blockers.Count; i++)
            {
                var bounds = blockers[i];
                float acrossLow = horizontal ? bounds.yMin : bounds.xMin;
                float acrossHigh = horizontal ? bounds.yMax : bounds.xMax;
                if (perpendicular + halfSize <= acrossLow + Epsilon || perpendicular - halfSize >= acrossHigh - Epsilon) continue;
                float low = horizontal ? bounds.xMin : bounds.yMin;
                float high = horizontal ? bounds.xMax : bounds.yMax;
                if (amount > 0 && low >= start + halfSize - Epsilon) end = Mathf.Min(end, low - halfSize);
                else if (amount < 0 && high <= start - halfSize + Epsilon) end = Mathf.Max(end, high + halfSize);
            }
            return horizontal ? new Vector2(end, center.y) : new Vector2(center.x, end);
        }
    }
}
