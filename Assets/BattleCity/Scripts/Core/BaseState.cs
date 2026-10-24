using UnityEngine;

namespace BattleCity
{
    /// <summary>One session's 16px base. Ruins retain the prototype movement/spawn footprint.</summary>
    public sealed class BaseState
    {
        // Tanks allocate upward from 1; this reserved target identity is never allocated to a tank.
        public const long EntityId = long.MaxValue;
        public const float HalfSizePixels = 8;
        public Vector2 Position { get; }
        public Rect Bounds => new Rect(Position - Vector2.one * HalfSizePixels, Vector2.one * (HalfSizePixels * 2));
        public bool IsAlive { get; private set; } = true;

        internal BaseState(Vector2 position) => Position = position;

        /// <summary>Called only after all sweeps. True exactly once, irrespective of attacking team/owner.</summary>
        internal bool ApplyHit(BulletHit hit)
        {
            if (!IsAlive || hit.Kind != BulletHitKind.Base || hit.TargetId != EntityId) return false;
            IsAlive = false;
            return true;
        }
    }
}
