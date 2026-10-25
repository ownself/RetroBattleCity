using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>One map's temporary U-ring. No clock, tanks, or view ownership; the session supplies settled occupancy.</summary>
    public sealed class BaseFortificationState
    {
        public const int DefaultDurationTicks = 1200; // Project default, not calibrated to the original game.
        private readonly MapState map;
        private readonly Vector2Int[] cells;
        private readonly bool[] pending;
        private CellSnapshot[] snapshot;
        public int DurationTicks { get; }
        public int RemainingTicks { get; private set; }
        public bool IsActive => RemainingTicks > 0;
        public bool IsRestoring => snapshot != null && !IsActive;
        public int PendingCellCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < pending.Length; i++) if (pending[i]) count++;
                return count;
            }
        }

        private readonly struct CellSnapshot
        {
            public readonly TerrainKind Terrain;
            public readonly byte BrickMask;
            public CellSnapshot(TerrainKind terrain, byte brickMask) { Terrain = terrain; BrickMask = brickMask; }
        }

        public BaseFortificationState(MapState map, Vector2Int baseCenterPixels, int durationTicks = DefaultDurationTicks)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            int size = PixelGrid.TerrainCellPixels;
            if (baseCenterPixels.x < 8 || baseCenterPixels.y < 8 ||
                baseCenterPixels.x > PixelGrid.MapSizePixels - 8 || baseCenterPixels.y > PixelGrid.MapSizePixels - 8 ||
                baseCenterPixels.x % size != 0 || baseCenterPixels.y % size != 0)
                throw new ArgumentException("Fortification requires an in-bounds 16px base centered on the 8px terrain grid.", nameof(baseCenterPixels));
            DurationTicks = durationTicks;
            int left = baseCenterPixels.x / size - 2, right = baseCenterPixels.x / size + 1;
            int bottom = baseCenterPixels.y / size - 1, top = baseCenterPixels.y / size + 1;
            var ring = new List<Vector2Int>(8);
            for (int y = bottom; y <= top; y++) for (int x = left; x <= right; x++)
                if ((x == left || x == right || y == top) && x >= 0 && y >= 0 &&
                    x < PixelGrid.TerrainCellsPerAxis && y < PixelGrid.TerrainCellsPerAxis)
                    ring.Add(new Vector2Int(x, y));
            cells = ring.ToArray();
            pending = new bool[cells.Length];
        }

        /// <summary>Call only for a new receipt, AFTER this tick's Step. Snapshots once, including empty cells;
        /// refreshes (never adds) duration and retries all steel cells without replacing that snapshot.
        /// Occupants must include every live tank and current birth reservation; an empty list is explicit.</summary>
        public void Activate(IReadOnlyList<Rect> occupants)
        {
            if (occupants == null) throw new ArgumentNullException(nameof(occupants));
            if (snapshot == null)
            {
                snapshot = new CellSnapshot[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                    snapshot[i] = new CellSnapshot(map.TerrainAt(cells[i].x, cells[i].y), map.BrickMaskAt(cells[i].x, cells[i].y));
            }
            RemainingTicks = DurationTicks;
            for (int i = 0; i < pending.Length; i++) pending[i] = true;
            ApplyPending(occupants);
        }

        /// <summary>Exactly once at each future complete combat tick's end, AFTER damage and births/reservations,
        /// BEFORE any new receipt activation. Never call in Ready/pause/terminal. At zero restore only, no more steel.
        /// Only deferred activation cells retry while active; destroyed completed steel stays destroyed until reactivation.</summary>
        public void Step(IReadOnlyList<Rect> occupants)
        {
            if (occupants == null) throw new ArgumentNullException(nameof(occupants));
            if (snapshot == null) return;
            if (IsActive && --RemainingTicks == 0)
                for (int i = 0; i < pending.Length; i++) pending[i] = true;
            ApplyPending(occupants);
            if (IsRestoring && PendingCellCount == 0) snapshot = null;
        }

        private void ApplyPending(IReadOnlyList<Rect> occupants)
        {
            for (int i = 0; i < pending.Length; i++)
            {
                if (!pending[i]) continue;
                var cell = cells[i];
                TerrainKind terrain = IsActive ? TerrainKind.Steel : snapshot[i].Terrain;
                byte mask = IsActive ? (byte)0 : snapshot[i].BrickMask;
                bool unchanged = map.TerrainAt(cell.x, cell.y) == terrain && map.BrickMaskAt(cell.x, cell.y) == mask;
                if (!unchanged && BlocksTanks(terrain) && IsOccupied(cell, occupants)) continue;
                map.SetTerrainCell(cell.x, cell.y, terrain, mask);
                pending[i] = false;
            }
        }

        private static bool BlocksTanks(TerrainKind terrain)
            => terrain == TerrainKind.Brick || terrain == TerrainKind.Steel || terrain == TerrainKind.Water;

        private static bool IsOccupied(Vector2Int cell, IReadOnlyList<Rect> occupants)
        {
            int size = PixelGrid.TerrainCellPixels;
            var bounds = new Rect(cell.x * size, cell.y * size, size, size);
            for (int i = 0; i < occupants.Count; i++)
                if (bounds.xMax > occupants[i].xMin + MapState.Epsilon && bounds.xMin < occupants[i].xMax - MapState.Epsilon &&
                    bounds.yMax > occupants[i].yMin + MapState.Epsilon && bounds.yMin < occupants[i].yMax - MapState.Epsilon) return true;
            return false;
        }

        /// <summary>Terminal/retirement only: force exact restoration without waiting for occupants, then clear all work.
        /// The caller must stop combat movement before cancelling. Idempotent; does not advance time.</summary>
        public void Cancel()
        {
            RemainingTicks = 0;
            if (snapshot == null) return;
            for (int i = 0; i < cells.Length; i++)
            {
                map.SetTerrainCell(cells[i].x, cells[i].y, snapshot[i].Terrain, snapshot[i].BrickMask);
                pending[i] = false;
            }
            snapshot = null;
        }
    }
}
