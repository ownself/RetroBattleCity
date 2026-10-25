using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    public readonly struct PickupItem
    {
        public long Id { get; }
        public PickupKind Kind { get; }
        public Vector2 Position { get; }
        public Rect Bounds => new Rect(Position - Vector2.one * 8, Vector2.one * 16);
        internal PickupItem(long id, PickupKind kind, Vector2 position) { Id = id; Kind = kind; Position = position; }
    }

    public readonly struct PickupReceipt
    {
        public PickupItem Item { get; }
        public long Tick { get; }
        internal PickupReceipt(PickupItem item, long tick) { Item = item; Tick = tick; }
    }

    /// <summary>Scene-free single-slot ledger. Host calls only after damage, once per running combat tick.</summary>
    public sealed class PickupState
    {
        private readonly HashSet<int> carriers;
        public int HelmetDurationTicks { get; }
        public int ClockDurationTicks { get; }
        /// <summary>Future complete enemy simulation phases frozen, independent of player life.</summary>
        public int ClockRemainingTicks { get; private set; }
        public int MaximumLives { get; }
        /// <summary>Future complete damage phases protected; pickup refreshes, never adds duration.</summary>
        public int HelmetRemainingTicks { get; private set; }
        public uint InitialSeed { get; }
        public uint RandomState { get; private set; }
        public PickupItem? Active { get; private set; }
        public PickupReceipt? LastCollected { get; private set; }
        public long SpawnedCount { get; private set; }
        public int DropAttemptCount { get; private set; }
        public int RejectedDropCount { get; private set; }
        public int ReplacedCount { get; private set; }
        public int CollectedCount { get; private set; }

        public PickupState(PickupDefinition definition, LevelDefinition level, uint? randomSeed = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.ValidateFor(level);
            uint seed = randomSeed ?? definition.seed;
            if (seed == 0) throw new ArgumentOutOfRangeException(nameof(randomSeed), "Pickup seed must be nonzero.");
            carriers = new HashSet<int>(definition.carrierSequenceIndices);
            InitialSeed = RandomState = seed;
            HelmetDurationTicks = definition.helmetDurationTicks;
            ClockDurationTicks = definition.clockDurationTicks;
            MaximumLives = definition.maximumLives;
        }

        public bool IsCarrierIndex(int index) => carriers.Contains(index);

        /// <summary>
        /// Conservative reachability: flood 16px tank footprints on 8px anchors, starting from the live player.
        /// Static base/terrain block routes; temporary tanks/reservations exclude destinations, not entire routes.
        /// No reachable free anchor (or no live player): consume the drop opportunity, preserve the existing slot/RNG.
        /// </summary>
        public bool TryDrop(MapState map, Vector2 playerPosition, bool playerAlive,
            IReadOnlyList<Rect> destinationBlockers, Rect? baseBounds = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            DropAttemptCount++;
            var permanent = baseBounds.HasValue ? new[] { baseBounds.Value } : Array.Empty<Rect>();
            var candidates = playerAlive ? ReachablePositions(map, playerPosition, destinationBlockers, permanent) : new List<Vector2>();
            if (candidates.Count == 0) { RejectedDropCount++; return false; }
            RandomState = Next(RandomState);
            var kind = (PickupKind)(RandomState % 6);
            RandomState = Next(RandomState);
            var position = candidates[(int)(RandomState % (uint)candidates.Count)];
            var item = new PickupItem(checked(SpawnedCount + 1), kind, position);
            if (Active.HasValue) ReplacedCount++;
            SpawnedCount++;
            Active = item;
            return true;
        }

        public bool TryCollect(Rect playerBounds, bool playerAlive, long tick)
        {
            if (!playerAlive || !Active.HasValue || !Overlaps(playerBounds, Active.Value.Bounds)) return false;
            var item = Active.Value;
            Active = null; // Commit before publishing the immutable receipt; repeats cannot settle twice.
            LastCollected = new PickupReceipt(item, tick);
            CollectedCount++;
            return true;
        }

        public void ClearActive() => Active = null;

        // Session owns effect ordering: after damage, before this tick's pickup. No independent time source.
        internal void StepPlayerEffects(bool playerAlive)
        {
            if (!playerAlive) ClearPlayerEffects();
            else if (HelmetRemainingTicks > 0) HelmetRemainingTicks--;
        }

        // After enemy simulation, before new receipts: collection tick never consumes its own duration.
        internal void StepGlobalEffects() { if (ClockRemainingTicks > 0) ClockRemainingTicks--; }
        internal void RefreshClock() => ClockRemainingTicks = ClockDurationTicks;
        internal void ClearGlobalEffects() => ClockRemainingTicks = 0;

        internal void RefreshHelmet() => HelmetRemainingTicks = HelmetDurationTicks;
        internal void ClearPlayerEffects() => HelmetRemainingTicks = 0;

        private static bool Overlaps(Rect a, Rect b) => a.xMax > b.xMin + MapState.Epsilon &&
            a.xMin < b.xMax - MapState.Epsilon && a.yMax > b.yMin + MapState.Epsilon && a.yMin < b.yMax - MapState.Epsilon;

        private static List<Vector2> ReachablePositions(MapState map, Vector2 player, IReadOnlyList<Rect> blockers, Rect[] permanent)
        {
            const int side = 25; // Centers 8..200 inclusive, 16px footprint inside the 208px arena.
            var clear = new bool[side * side];
            var visited = new bool[clear.Length];
            var queue = new Queue<int>();
            var result = new List<Vector2>();
            if (!map.CanOccupy(player, 8, permanent)) return result;
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
                clear[y * side + x] = map.CanOccupy(new Vector2((x + 1) * 8, (y + 1) * 8), 8, permanent);
            int fx = Mathf.FloorToInt(player.x / 8), fy = Mathf.FloorToInt(player.y / 8);
            for (int gy = fy; gy <= fy + 1; gy++) for (int gx = fx; gx <= fx + 1; gx++)
            {
                if (gx < 1 || gy < 1 || gx > side || gy > side) continue;
                int index = (gy - 1) * side + gx - 1;
                if (!clear[index] || visited[index]) continue;
                var point = new Vector2(gx * 8, gy * 8);
                if (!Connects(map, player, point, permanent, true) && !Connects(map, player, point, permanent, false)) continue;
                visited[index] = true; queue.Enqueue(index);
            }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue(), x = index % side, y = index / side;
                var point = new Vector2((x + 1) * 8, (y + 1) * 8);
                if (map.CanOccupy(point, 8, blockers) && map.CanOccupy(point, 8, permanent)) result.Add(point);
                // Adjacent clear 16px footprints overlap along their cardinal 8px route: no hidden intervening wall.
                if (x > 0) Visit(index - 1);
                if (x + 1 < side) Visit(index + 1);
                if (y > 0) Visit(index - side);
                if (y + 1 < side) Visit(index + side);
            }
            return result;

            void Visit(int index)
            {
                if (clear[index] && !visited[index]) { visited[index] = true; queue.Enqueue(index); }
            }
        }

        private static bool Connects(MapState map, Vector2 start, Vector2 end, Rect[] permanent, bool horizontalFirst)
        {
            var middle = horizontalFirst ? new Vector2(end.x, start.y) : new Vector2(start.x, end.y);
            var reached = map.MoveAxis(start, middle - start, 8, permanent);
            if ((reached - middle).sqrMagnitude > MapState.Epsilon * MapState.Epsilon) return false;
            reached = map.MoveAxis(middle, end - middle, 8, permanent);
            return (reached - end).sqrMagnitude <= MapState.Epsilon * MapState.Epsilon;
        }

        private static uint Next(uint value) { value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value; }
    }
}
