using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>A pooled logical projectile. Id identifies this lease, not the reusable object.</summary>
    public sealed class BulletState
    {
        public const float HalfSizePixels = 2;
        public long Id { get; internal set; }
        public WeaponState Owner { get; internal set; }
        public long OwnerId { get; internal set; }
        public CombatTeam Team { get; internal set; }
        public Vector2 Position { get; internal set; }
        public Vector2 PreviousPosition { get; internal set; }
        public CardinalDirection Direction { get; internal set; }
        public float SpeedPixelsPerSecond { get; internal set; }
        public int AgeTicks { get; internal set; }
    }

    /// <summary>Unique projectile simulation: collect continuous contacts, resolve globally, then recycle.</summary>
    public sealed class BulletWorld
    {
        private readonly List<BulletState> active = new List<BulletState>();
        private readonly Stack<BulletState> free = new Stack<BulletState>();
        private readonly List<Vector2> deltas = new List<Vector2>();
        private readonly List<bool> consumed = new List<bool>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<BulletHit> hits = new List<BulletHit>();
        private readonly HashSet<long> targetIds = new HashSet<long>();
        private long nextId;
        public IReadOnlyList<BulletState> Active { get; }
        /// <summary>Impacts of the last logic tick only. No callbacks during resolution.</summary>
        public IReadOnlyList<BulletHit> Hits { get; }

        public BulletWorld() { Active = active.AsReadOnly(); Hits = hits.AsReadOnly(); }

        internal void Spawn(WeaponState owner, Vector2 position, CardinalDirection direction, float speed)
        {
            var bullet = free.Count > 0 ? free.Pop() : new BulletState();
            bullet.Id = checked(++nextId);
            bullet.Owner = owner; bullet.OwnerId = owner.OwnerId; bullet.Team = owner.Team;
            bullet.Position = bullet.PreviousPosition = position;
            bullet.Direction = direction; bullet.SpeedPixelsPerSecond = speed; bullet.AgeTicks = 0;
            active.Add(bullet);
        }

        public void Step(MapState map = null, IReadOnlyList<BulletTarget> targets = null)
        {
            ValidateTargets(targets);
            hits.Clear(); candidates.Clear(); deltas.Clear(); consumed.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var bullet = active[i];
                bullet.PreviousPosition = bullet.Position; bullet.AgeTicks++;
                deltas.Add(bullet.Direction.Vector() * (bullet.SpeedPixelsPerSecond * PixelGrid.LogicStep));
                consumed.Add(false);
                CollectBoundary(i);
                if (map != null) CollectTerrain(i, map);
                if (targets != null) CollectTargets(i, targets);
            }
            for (int i = 0; i < active.Count; i++) for (int j = i + 1; j < active.Count; j++)
            {
                if (active[i].Team == active[j].Team) continue;
                var bounds = new Rect(active[j].Position - Vector2.one * BulletState.HalfSizePixels,
                    Vector2.one * (BulletState.HalfSizePixels * 2));
                if (BulletSweep.TryHit(active[i].Position, deltas[i] - deltas[j], bounds, BulletState.HalfSizePixels, out double time))
                    candidates.Add(new Candidate(i, j, time, BulletHitKind.Bullet, active[j].Id));
            }
            candidates.Sort(CompareCandidates);
            foreach (var candidate in candidates)
            {
                if (consumed[candidate.A] || (candidate.B >= 0 && consumed[candidate.B])) continue;
                var bullet = active[candidate.A];
                var position = At(candidate.A, candidate.Time);
                if (candidate.Kind == BulletHitKind.Boundary)
                    position = new Vector2(Mathf.Clamp(position.x, 2, PixelGrid.MapSizePixels - 2),
                        Mathf.Clamp(position.y, 2, PixelGrid.MapSizePixels - 2));
                long otherId = candidate.B < 0 ? 0 : active[candidate.B].Id;
                var otherPosition = candidate.B < 0 ? Vector2.zero : At(candidate.B, candidate.Time);
                hits.Add(new BulletHit(bullet, candidate.Kind, candidate.TargetId, position, candidate.Time,
                    candidate.Cell, candidate.Terrain, otherId, otherPosition));
                consumed[candidate.A] = true;
                if (candidate.B >= 0) consumed[candidate.B] = true;
            }
            // Do not recycle until every candidate has been resolved against the same tick snapshot.
            for (int i = active.Count - 1; i >= 0; i--)
                if (consumed[i]) ReleaseAt(i);
                else active[i].Position += deltas[i];
        }

        private Vector2 At(int index, double time) => active[index].PreviousPosition + deltas[index] * (float)time;

        private void CollectBoundary(int index)
        {
            var start = active[index].Position; var delta = deltas[index];
            const float low = BulletState.HalfSizePixels, high = PixelGrid.MapSizePixels - low;
            if (start.x <= low + MapState.Epsilon) AddBoundary(index, 0, 1);
            else if (delta.x < 0) AddBoundary(index, (low - (double)start.x) / delta.x, 1);
            if (start.y <= low + MapState.Epsilon) AddBoundary(index, 0, 2);
            else if (delta.y < 0) AddBoundary(index, (low - (double)start.y) / delta.y, 2);
            if (start.x >= high - MapState.Epsilon) AddBoundary(index, 0, 3);
            else if (delta.x > 0) AddBoundary(index, (high - (double)start.x) / delta.x, 3);
            if (start.y >= high - MapState.Epsilon) AddBoundary(index, 0, 4);
            else if (delta.y > 0) AddBoundary(index, (high - (double)start.y) / delta.y, 4);
        }

        private void AddBoundary(int index, double time, long edge)
        {
            if (time >= 0 && time <= 1) candidates.Add(new Candidate(index, -1, time, BulletHitKind.Boundary, edge));
        }

        private void CollectTerrain(int index, MapState map)
        {
            var start = active[index].Position; var end = start + deltas[index];
            const float half = BulletState.HalfSizePixels;
            // Include touching cells on either end. Clamp before converting very large travel to int.
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Clamp(Mathf.Min(start.x, end.x) - half, 0, PixelGrid.MapSizePixels) / 4) - 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Clamp(Mathf.Min(start.y, end.y) - half, 0, PixelGrid.MapSizePixels) / 4) - 1);
            int x1 = Mathf.Min(MapState.DamageCellsPerAxis - 1, Mathf.FloorToInt(Mathf.Clamp(Mathf.Max(start.x, end.x) + half, 0, PixelGrid.MapSizePixels) / 4));
            int y1 = Mathf.Min(MapState.DamageCellsPerAxis - 1, Mathf.FloorToInt(Mathf.Clamp(Mathf.Max(start.y, end.y) + half, 0, PixelGrid.MapSizePixels) / 4));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                if (!map.BlocksBullet(x, y)) continue;
                if (BulletSweep.TryHit(start, deltas[index], new Rect(x * 4, y * 4, 4, 4), half, out double time))
                    candidates.Add(new Candidate(index, -1, time, BulletHitKind.Terrain,
                        1 + y * MapState.DamageCellsPerAxis + x, new Vector2Int(x, y), map.TerrainAt(x / 2, y / 2)));
            }
        }

        private void CollectTargets(int index, IReadOnlyList<BulletTarget> targets)
        {
            var bullet = active[index];
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Kind == BulletHitKind.Tank &&
                    !CombatRules.CanHitTank(bullet.Team, bullet.OwnerId, target.Team, target.EntityId)) continue;
                if (BulletSweep.TryHit(bullet.Position, deltas[index], target.Bounds, BulletState.HalfSizePixels, out double time))
                    candidates.Add(new Candidate(index, -1, time, target.Kind, target.EntityId));
            }
        }

        private void ValidateTargets(IReadOnlyList<BulletTarget> targets)
        {
            targetIds.Clear();
            if (targets == null) return;
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Validate();
                if (!targetIds.Add(targets[i].EntityId)) throw new ArgumentException("Bullet targets require unique stable entity ids.");
            }
        }

        private int CompareCandidates(Candidate a, Candidate b)
        {
            int order = a.Time.CompareTo(b.Time);
            if (order == 0) order = a.Kind.CompareTo(b.Kind);
            // A muzzle touching a wall can overlap the outer 4px layer and touch the next
            // at t=0. Prefer the incoming face, not the lowest row-major terrain id.
            if (order == 0 && a.Kind == BulletHitKind.Terrain)
            {
                var directionA = active[a.A].Direction.Vector();
                var directionB = active[b.A].Direction.Vector();
                float depthA = a.Cell.x * directionA.x + a.Cell.y * directionA.y;
                float depthB = b.Cell.x * directionB.x + b.Cell.y * directionB.y;
                order = depthA.CompareTo(depthB);
            }
            if (order == 0) order = a.TargetId.CompareTo(b.TargetId);
            if (order == 0) order = active[a.A].Id.CompareTo(active[b.A].Id);
            return order;
        }

        /// <summary>Idempotent, including stale ids after an object has been reused.</summary>
        public bool Release(long id)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].Id == id) { ReleaseAt(i); return true; }
            return false;
        }

        internal void ReleaseOwnedBy(WeaponState owner)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (ReferenceEquals(active[i].Owner, owner)) ReleaseAt(i);
        }

        public void Clear()
        {
            while (active.Count > 0) ReleaseAt(active.Count - 1);
            hits.Clear();
        }

        private void ReleaseAt(int index)
        {
            var bullet = active[index]; active.RemoveAt(index);
            bullet.Owner.ReleaseBullet();
            bullet.Id = 0; bullet.Owner = null; bullet.OwnerId = 0; bullet.Team = default;
            bullet.Position = bullet.PreviousPosition = Vector2.zero;
            bullet.Direction = CardinalDirection.None; bullet.SpeedPixelsPerSecond = 0; bullet.AgeTicks = 0;
            free.Push(bullet);
        }

        private readonly struct Candidate
        {
            public readonly int A, B;
            public readonly double Time;
            public readonly BulletHitKind Kind;
            public readonly long TargetId;
            public readonly Vector2Int Cell;
            public readonly TerrainKind Terrain;
            public Candidate(int a, int b, double time, BulletHitKind kind, long targetId,
                Vector2Int cell = default, TerrainKind terrain = TerrainKind.Empty)
            { A = a; B = b; Time = time; Kind = kind; TargetId = targetId; Cell = cell; Terrain = terrain; }
        }
    }
}
